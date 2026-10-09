namespace Aion2Assistant.Capture;

public delegate void StreamDataHandler(ReadOnlySpan<byte> data);

/// <summary>
/// Remet un sens de connexion TCP dans l'ordre. Un trou qui ne se comble pas (1,5 s ou 2 Mio en attente)
/// est sauté et signalé, pour que le découpage en trames se resynchronise au lieu de lire des octets décalés.
/// </summary>
public sealed class TcpReassembler(StreamDataHandler onData, Action onGap)
{
    public const long GapTimeoutMs = 1500;
    public const int MaxPendingBytes = 2 * 1024 * 1024;
    public const int MaxPendingSegments = 4096;

    private readonly List<(uint Seq, byte[] Data, long TimeMs)> _pending = new();
    private int _pendingBytes;
    private uint _next;
    private bool _started;

    public long Duplicates { get; private set; }
    public long OutOfOrder { get; private set; }
    public long Gaps { get; private set; }

    public void Start(uint seq)
    {
        _next = seq;
        _started = true;
        _pending.Clear();
        _pendingBytes = 0;
    }

    public void Push(uint seq, ReadOnlySpan<byte> payload, long timeMs)
    {
        if (payload.IsEmpty) return;
        if (!_started) Start(seq);

        var diff = (int)(seq - _next);
        if (diff < 0)
        {
            if (diff == int.MinValue || -diff >= payload.Length) { Duplicates++; return; }
            payload = payload[-diff..];
            diff = 0;
        }

        if (diff == 0)
        {
            Deliver(payload);
            DrainPending();
        }
        else
        {
            OutOfOrder++;
            var length = payload.Length;
            if (!_pending.Exists(p => p.Seq == seq && p.Data.Length >= length))
            {
                _pending.Add((seq, payload.ToArray(), timeMs));
                _pendingBytes += length;
            }
        }

        while (_pending.Count > 0 && (_pendingBytes > MaxPendingBytes || _pending.Count > MaxPendingSegments || timeMs - _pending.Min(p => p.TimeMs) > GapTimeoutMs))
        {
            var current = _next;
            _next = _pending.Select(p => p.Seq).MinBy(s => (int)(s - current));
            Gaps++;
            onGap();
            DrainPending();
        }
    }

    private void Deliver(ReadOnlySpan<byte> data)
    {
        _next += (uint)data.Length;
        onData(data);
    }

    private void DrainPending()
    {
        bool progressed;
        do
        {
            progressed = false;
            for (var i = 0; i < _pending.Count; i++)
            {
                var (seq, data, _) = _pending[i];
                var diff = (int)(seq - _next);
                if (diff > 0) continue;
                _pending.RemoveAt(i);
                _pendingBytes -= data.Length;
                if (diff == int.MinValue || -diff >= data.Length) Duplicates++;
                else Deliver(data.AsSpan(-diff));
                progressed = true;
                break;
            }
        }
        while (progressed);
    }
}
