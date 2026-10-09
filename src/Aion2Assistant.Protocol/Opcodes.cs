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

    private static readonly Dictionary<ushort, string> Names = new()
    {
        [Heartbeat] = "Heartbeat", [SelfInfo] = "Personnage", [PlayerInfo] = "Joueur", [PlayerInfoOld] = "Joueur (ancien)",
        [Spawn] = "Apparition", [Death] = "Mort", [MapLoad] = "Carte", [Teleport] = "Téléportation", [Cast] = "Lancement",
        [Damage] = "Dégâts", [Dot] = "DoT/HoT", [RemainHp] = "PV restants", [KillCredit] = "Mort + tueur", [HpMp] = "PV/PM",
        [PartyRoster] = "Groupe", [FieldBossList] = "Boss de terrain",
    };

    public static bool IsKnown(ushort opcode) => Names.ContainsKey(opcode);

    public static string Name(ushort opcode) => Names.TryGetValue(opcode, out var n) ? n : $"{opcode >> 8:x2} {opcode & 0xFF:x2}";
}
