namespace Aion2Assistant.Protocol.Parsing;

/// <summary>Reçus = toutes les trames de cet opcode ; Parsed / Rejected = seulement pour les opcodes décodés.</summary>
public sealed record OpcodeStat(ushort Opcode, long Received, long Parsed, long Rejected);
