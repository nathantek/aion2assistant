using Aion2Assistant.Core.Events;
using Aion2Assistant.Protocol.Parsing;

namespace Aion2Assistant.Capture;

public sealed record PipelineStats(
    long Segments, long Bytes, long Frames, long Bundles, long BadBundles, long Resyncs, long Gaps,
    long Events, long Parsed, long Rejected, string? LockedFlow, IReadOnlyList<OpcodeStat> Opcodes);

/// <summary>Segments TCP → flux verrouillé → trames → événements. Process depuis un seul thread ; Snapshot depuis n'importe lequel.</summary>
public sealed class CapturePipeline
{
    private readonly object _sync = new();
    private readonly PacketParser _parser;
    private readonly FlowTracker _tracker;
    private long _segments;
    private long _bytes;
    private long _events;
    private string? _lockedFlow;

    public CapturePipeline()
    {
        _parser = new PacketParser(OnEvent);
        _tracker = new FlowTracker(_parser.Handle);
        _tracker.FlowLocked += key =>
        {
            _lockedFlow = key.ToString();
            _parser.ResetStreamState();
            OnEvent(new StreamStartedEvent(_parser.TimeMs, _lockedFlow));
        };
    }

    /// <summary>Levé sur le thread de traitement : le gestionnaire doit être rapide (mise en file).</summary>
    public event Action<GameEvent>? EventDecoded;

    public void Process(TcpSegment segment)
    {
        lock (_sync)
        {
            _segments++;
            _bytes += segment.Payload.Length;
            _parser.TimeMs = segment.TimeMs;
            _tracker.Process(segment);
        }
    }

    public PipelineStats Snapshot()
    {
        lock (_sync)
        {
            var decoders = _tracker.Decoders;
            var opcodes = _parser.Snapshot();
            return new PipelineStats(
                _segments, _bytes,
                decoders.Sum(d => d.Stats.Frames), decoders.Sum(d => d.Stats.Bundles), decoders.Sum(d => d.Stats.BadBundles),
                decoders.Sum(d => d.Stats.Resyncs), _tracker.GapCount, _events,
                opcodes.Sum(o => o.Parsed), opcodes.Sum(o => o.Rejected), _lockedFlow, opcodes);
        }
    }

    private void OnEvent(GameEvent e)
    {
        _events++;
        EventDecoded?.Invoke(e);
    }
}
