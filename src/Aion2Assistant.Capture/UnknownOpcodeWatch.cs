using Aion2Assistant.Protocol;
using Aion2Assistant.Protocol.Parsing;

namespace Aion2Assistant.Capture;

/// <summary>Repère les opcodes inconnus fréquents (plus de 50 par minute) : signe probable d'un changement de protocole.</summary>
public sealed class UnknownOpcodeWatch
{
    private const long PeriodMs = 60_000;
    private const long Threshold = 50;

    private readonly Dictionary<ushort, long> _baseline = new();
    private long _periodStartMs = long.MinValue;

    public IReadOnlyList<(ushort Opcode, long PerMinute)> Check(IReadOnlyList<OpcodeStat> stats, long nowMs)
    {
        if (_periodStartMs == long.MinValue)
        {
            Reset(stats, nowMs);
            return [];
        }
        if (nowMs - _periodStartMs < PeriodMs) return [];

        var hits = stats
            .Where(s => !Opcodes.IsKnown(s.Opcode))
            .Select(s => (s.Opcode, PerMinute: s.Received - _baseline.GetValueOrDefault(s.Opcode)))
            .Where(x => x.PerMinute > Threshold)
            .ToList();
        Reset(stats, nowMs);
        return hits;
    }

    private void Reset(IReadOnlyList<OpcodeStat> stats, long nowMs)
    {
        _periodStartMs = nowMs;
        _baseline.Clear();
        foreach (var s in stats) _baseline[s.Opcode] = s.Received;
    }
}
