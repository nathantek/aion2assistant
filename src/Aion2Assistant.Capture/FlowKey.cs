using System.Net;

namespace Aion2Assistant.Capture;

/// <summary>Un sens d'une connexion TCP.</summary>
public readonly record struct FlowKey(IPAddress Source, ushort SourcePort, IPAddress Destination, ushort DestinationPort)
{
    public override string ToString() => $"{Source}:{SourcePort} → {Destination}:{DestinationPort}";
}

/// <summary>Segment TCP capturé. <see cref="TimeMs"/> = horodatage Unix (ms) de la capture.</summary>
public sealed record TcpSegment(FlowKey Flow, uint Seq, bool Syn, bool Fin, byte[] Payload, long TimeMs);
