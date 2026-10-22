using Aion2Assistant.Core.Events;

namespace Aion2Assistant.Protocol.Parsing;

/// <summary>
/// Aiguille chaque trame vers son parseur et compte les résultats. Ne lève jamais d'exception :
/// un corps invalide est compté comme rejeté. Appelé depuis un seul thread ; <see cref="Snapshot"/> est sûr depuis un autre.
/// </summary>
public sealed class PacketParser(Action<GameEvent> emit)
{
    private sealed class Counter { public long Received, Parsed, Rejected; }

    private readonly object _sync = new();
    private readonly Dictionary<ushort, Counter> _counters = new();
    private int _lastMapId;

    public long TimeMs { get; set; }

    public void Handle(ushort opcode, ReadOnlySpan<byte> body)
    {
        bool? result;
        try
        {
            result = Dispatch(opcode, body);
        }
        catch (Exception)
        {
            result = false;
        }

        lock (_sync)
        {
            if (!_counters.TryGetValue(opcode, out var c)) _counters[opcode] = c = new Counter();
            c.Received++;
            if (result == true) c.Parsed++;
            else if (result == false) c.Rejected++;
        }
    }

    /// <summary>Nouveau flux de jeu : oublie l'état propre au flux (les compteurs sont conservés).</summary>
    public void ResetStreamState() => _lastMapId = 0;

    public IReadOnlyList<OpcodeStat> Snapshot()
    {
        lock (_sync)
        {
            return _counters.Select(kv => new OpcodeStat(kv.Key, kv.Value.Received, kv.Value.Parsed, kv.Value.Rejected)).ToList();
        }
    }

    /// <summary>null = opcode non décodé ; true = traité ; false = rejeté.</summary>
    private bool? Dispatch(ushort opcode, ReadOnlySpan<byte> body) => opcode switch
    {
        Opcodes.Heartbeat => true,
        Opcodes.SelfInfo => IdentityParser.Parse(body, isSelf: true, TimeMs, emit),
        Opcodes.PlayerInfo or Opcodes.PlayerInfoOld => IdentityParser.Parse(body, isSelf: false, TimeMs, emit),
        Opcodes.Damage => DamageParser.Parse(body, TimeMs, emit),
        Opcodes.Dot => DotParser.Parse(body, TimeMs, emit),
        Opcodes.RemainHp => SimpleParsers.RemainHp(body, TimeMs, emit),
        Opcodes.HpMp => SimpleParsers.HpMp(body, TimeMs, emit),
        Opcodes.Death => SimpleParsers.Death(body, TimeMs, emit),
        Opcodes.KillCredit => SimpleParsers.KillCredit(body, TimeMs, emit),
        Opcodes.MapLoad => SimpleParsers.MapLoad(body, TimeMs, ref _lastMapId, emit),
        Opcodes.Cast => SimpleParsers.Cast(body, TimeMs, emit),
        Opcodes.Spawn => SpawnParser.Parse(body, TimeMs, emit),
        Opcodes.FieldBossList => FieldBossListParser.Parse(body, TimeMs, emit),
        Opcodes.CombatPowerUpdate => CombatPowerParsers.Update(body, TimeMs, emit),
        Opcodes.Inspect => CombatPowerParsers.Inspect(body, TimeMs, emit),
        Opcodes.PartyRoster => CombatPowerParsers.PartyRoster(body, TimeMs, emit),
        Opcodes.FamiliarState => FamiliarParsers.State(body, TimeMs, emit),
        Opcodes.FamiliarSoul => FamiliarParsers.Soul(body, TimeMs, emit),
        Opcodes.FamiliarTier => FamiliarParsers.Tier(body, TimeMs, emit),
        _ => null,
    };
}
