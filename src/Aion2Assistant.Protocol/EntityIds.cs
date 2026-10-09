namespace Aion2Assistant.Protocol;

public static class EntityIds
{
    public const uint Max = 9_999_999;

    public static bool IsValid(uint id) => id is >= 1 and <= Max;

    /// <summary>Les ids d'apparition > 1 000 000 sont repliés dans l'espace d'ids des paquets de combat.</summary>
    public static uint FoldSpawnId(uint raw) => raw > 1_000_000 ? (raw & 0x3FFF) | 0x4000 : raw;
}
