using PacketDotNet;
using SharpPcap;

namespace Aion2Assistant.Capture;

public static class SegmentReader
{
    /// <summary>Paquet capturé (Ethernet ou loopback Npcap) → segment TCP. Les sommes de contrôle ne sont pas vérifiées (offload de la carte).</summary>
    public static bool TryRead(RawCapture raw, out TcpSegment? segment)
    {
        segment = null;
        try
        {
            var packet = Packet.ParsePacket(raw.LinkLayerType, raw.Data);
            var ip = packet.Extract<IPPacket>();
            var tcp = packet.Extract<TcpPacket>();
            if (ip is null || tcp is null) return false;
            var flow = new FlowKey(ip.SourceAddress, tcp.SourcePort, ip.DestinationAddress, tcp.DestinationPort);
            var ms = (long)raw.Timeval.Seconds * 1000 + (long)raw.Timeval.MicroSeconds / 1000;
            segment = new TcpSegment(flow, tcp.SequenceNumber, tcp.Synchronize, tcp.Finished || tcp.Reset, tcp.PayloadData ?? [], ms);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
