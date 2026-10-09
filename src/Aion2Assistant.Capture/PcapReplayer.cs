using SharpPcap;
using SharpPcap.LibPcap;

namespace Aion2Assistant.Capture;

public static class PcapReplayer
{
    /// <summary>Rejoue un fichier .pcap dans le pipeline et renvoie le nombre de segments TCP lus (0 si le fichier est illisible).</summary>
    public static int Replay(string path, CapturePipeline pipeline)
    {
        var count = 0;
        try
        {
            using var reader = new CaptureFileReaderDevice(path);
            reader.Open();
            while (reader.GetNextPacket(out var capture) == GetPacketStatus.PacketRead)
            {
                if (!SegmentReader.TryRead(capture.GetPacket(), out var segment)) continue;
                pipeline.Process(segment!);
                count++;
            }
        }
        catch (Exception)
        {
            // fichier absent, tronqué ou Npcap indisponible : on rend ce qui a été traité
        }
        return count;
    }
}
