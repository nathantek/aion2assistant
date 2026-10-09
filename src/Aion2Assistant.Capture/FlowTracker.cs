using Aion2Assistant.Protocol.Framing;

namespace Aion2Assistant.Capture;

/// <summary>
/// Reconnaît le flux serveur → client du jeu à ses heartbeats, puis le réassemble et le découpe en trames.
/// Utilisé depuis un seul thread.
/// </summary>
public sealed class FlowTracker(FrameHandler handler)
{
    public const int LockThreshold = 3;
    /// <summary>Fenêtre glissante (temps de capture) dans laquelle <see cref="LockThreshold"/> heartbeats doivent tomber pour verrouiller un flux.</summary>
    public const long LockWindowMs = 10_000;
    private const int MaxPreLockBytes = 512 * 1024;
    private const long IdleMs = 120_000;
    private const long PruneEveryMs = 10_000;

    private sealed class Flow
    {
        public readonly List<TcpSegment> PreLock = new();
        public int PreLockBytes;
        public uint? SynNext;
        public bool IsTls;
        public readonly Queue<long> Heartbeats = new();
        public TcpReassembler? Reassembler;
        public FrameDecoder? Decoder;
        public long LastSeenMs;
    }

    private readonly Dictionary<FlowKey, Flow> _flows = new();
    private readonly List<FrameDecoder> _decoders = new();
    private long _lastPruneMs;

    public event Action<FlowKey>? FlowLocked;

    public IReadOnlyList<FrameDecoder> Decoders => _decoders;
    public long GapCount { get; private set; }

    public void Process(TcpSegment segment)
    {
        if (!_flows.TryGetValue(segment.Flow, out var flow))
            _flows[segment.Flow] = flow = new Flow();
        flow.LastSeenMs = segment.TimeMs;
        Prune(segment.TimeMs);

        if (segment.Syn)
        {
            flow.SynNext = segment.Seq + 1;
            return;
        }
        if (segment.Payload.Length == 0)
        {
            if (segment.Fin && flow.Reassembler is null) _flows.Remove(segment.Flow);
            return;
        }
        if (flow.Reassembler is not null)
        {
            flow.Reassembler.Push(segment.Seq, segment.Payload, segment.TimeMs);
            return;
        }
        if (flow.IsTls) return;
        if (flow.PreLock.Count == 0 && HeartbeatScanner.LooksLikeTls(segment.Payload))
        {
            flow.IsTls = true;
            return;
        }

        flow.PreLock.Add(segment);
        flow.PreLockBytes += segment.Payload.Length;
        while (flow.PreLockBytes > MaxPreLockBytes && flow.PreLock.Count > 1)
        {
            flow.PreLockBytes -= flow.PreLock[0].Payload.Length;
            flow.PreLock.RemoveAt(0);
            flow.SynNext = null; // le début du flux est perdu
            flow.Heartbeats.Clear(); // les heartbeats comptés appartenaient aux segments évincés
        }

        var beats = HeartbeatScanner.Count(segment.Payload);
        for (var i = 0; i < beats; i++) flow.Heartbeats.Enqueue(segment.TimeMs);
        while (flow.Heartbeats.Count > 0 && segment.TimeMs - flow.Heartbeats.Peek() > LockWindowMs) flow.Heartbeats.Dequeue();
        if (flow.Heartbeats.Count >= LockThreshold) Lock(segment.Flow, flow);
    }

    private void Lock(FlowKey key, Flow flow)
    {
        var first = flow.PreLock[0];
        var aligned = flow.SynNext == first.Seq;
        var decoder = new FrameDecoder(handler, aligned);
        var reassembler = new TcpReassembler(data => decoder.Feed(data), () => { GapCount++; decoder.Reset(); });
        reassembler.Start(first.Seq);
        flow.Decoder = decoder;
        flow.Reassembler = reassembler;
        _decoders.Add(decoder);
        flow.Heartbeats.Clear();
        FlowLocked?.Invoke(key); // avant de décoder : les consommateurs voient le début du flux en premier
        foreach (var s in flow.PreLock) reassembler.Push(s.Seq, s.Payload, s.TimeMs);
        flow.PreLock.Clear();
        flow.PreLockBytes = 0;
    }

    private void Prune(long nowMs)
    {
        if (nowMs - _lastPruneMs < PruneEveryMs) return;
        _lastPruneMs = nowMs;
        foreach (var (key, flow) in _flows.Where(kv => nowMs - kv.Value.LastSeenMs > IdleMs).ToList())
        {
            _flows.Remove(key);
            if (flow.Decoder is not null) _decoders.Remove(flow.Decoder);
        }
    }
}
