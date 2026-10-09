namespace Aion2Assistant.Protocol.Framing;

public sealed class FrameStats
{
    public long Frames { get; internal set; }
    public long Bundles { get; internal set; }
    public long BadBundles { get; internal set; }
    public long Resyncs { get; internal set; }
    public long DroppedBytes { get; internal set; }
}
