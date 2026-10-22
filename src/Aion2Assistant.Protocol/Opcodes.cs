using Aion2Assistant.Core.Localization;

namespace Aion2Assistant.Protocol;

/// <summary>Opcodes = les 2 octets dans l'ordre du câble (0xAABB = octets AA BB), client Global d'après juin 2026.</summary>
public static class Opcodes
{
    public const ushort Heartbeat = 0x0036;
    public const ushort SelfInfo = 0x3336;
    public const ushort PlayerInfo = 0x4536;
    public const ushort PlayerInfoOld = 0x4436;
    public const ushort Spawn = 0x4136;
    public const ushort Death = 0x4236;
    public const ushort MapLoad = 0x2136;
    public const ushort Teleport = 0x2336;
    public const ushort Cast = 0x0238;
    public const ushort Damage = 0x0438;
    public const ushort Dot = 0x0538;
    public const ushort RemainHp = 0x008D;
    public const ushort KillCredit = 0x048D;
    public const ushort HpMp = 0x1B92;
    public const ushort PartyRoster = 0x0297;
    public const ushort FieldBossList = 0x0191;
    public const ushort CombatPowerUpdate = 0x1C56;
    public const ushort Inspect = 0x5036;
    public const ushort FamiliarState = 0x0090;
    public const ushort FamiliarSoul = 0x0D90;
    public const ushort FamiliarTier = 0x1490;
    // 0x0E90 (u8 01 | u32 id | u32 âmes consommées), reçu juste après 0x1490 : volontairement non décodé. Il
    // redonnerait le seuil du palier franchi, alors que le menu affiche 0 âme juste après (0x1490 suffit).

    // Clé de ressource du nom affiché (onglet Capture), traduit à la lecture.
    private static readonly Dictionary<ushort, string> NameKeys = new()
    {
        [Heartbeat] = nameof(Strings.OpcodeHeartbeat), [SelfInfo] = nameof(Strings.OpcodeSelfInfo),
        [PlayerInfo] = nameof(Strings.OpcodePlayerInfo), [PlayerInfoOld] = nameof(Strings.OpcodePlayerInfoOld),
        [Spawn] = nameof(Strings.OpcodeSpawn), [Death] = nameof(Strings.OpcodeDeath), [MapLoad] = nameof(Strings.OpcodeMapLoad),
        [Teleport] = nameof(Strings.OpcodeTeleport), [Cast] = nameof(Strings.OpcodeCast), [Damage] = nameof(Strings.OpcodeDamage),
        [Dot] = nameof(Strings.OpcodeDot), [RemainHp] = nameof(Strings.OpcodeRemainHp), [KillCredit] = nameof(Strings.OpcodeKillCredit),
        [HpMp] = nameof(Strings.OpcodeHpMp), [PartyRoster] = nameof(Strings.OpcodePartyRoster),
        [FieldBossList] = nameof(Strings.OpcodeFieldBossList), [CombatPowerUpdate] = nameof(Strings.OpcodeCombatPower),
        [Inspect] = nameof(Strings.OpcodeInspect), [FamiliarState] = nameof(Strings.OpcodeFamiliarState),
        [FamiliarSoul] = nameof(Strings.OpcodeFamiliarSoul), [FamiliarTier] = nameof(Strings.OpcodeFamiliarTier),
    };

    public static bool IsKnown(ushort opcode) => NameKeys.ContainsKey(opcode);

    public static string Name(ushort opcode) =>
        NameKeys.TryGetValue(opcode, out var key) ? Strings.Get(key) : $"{opcode >> 8:x2} {opcode & 0xFF:x2}";
}
