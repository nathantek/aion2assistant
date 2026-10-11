using Aion2Assistant.Core.Events;

namespace Aion2Assistant.Combat;

/// <summary>Règles sur les codes de skill bruts (variantes, pierre divine, classe).</summary>
public static class SkillCodes
{
    private static bool IsClassRange(int raw) => raw is >= 10_000_000 and <= 19_999_999;
    private static bool IsDivineStone(int raw) => raw is >= 3_000_000 and <= 3_099_999;

    public static int Normalize(int raw)
    {
        if (IsClassRange(raw)) return raw - raw % 10_000;
        if (IsDivineStone(raw)) return raw * 10 + 1;
        return raw;
    }

    /// <summary>Enregistrements du lien d'esprit (Élémentaliste, 1699xxxx) : ni dégâts ni soins.</summary>
    public static bool IsSpiritLinkRecord(int raw) => raw is >= 16_990_000 and <= 16_999_999;

    /// <summary>Spirit Communion (Élémentaliste, 1677xxxx) : vrai soin, mais pas un dégât.</summary>
    public static bool IsSpiritCommunion(int raw) => raw is >= 16_770_000 and <= 16_779_999;

    /// <summary>Codes émis sur l'opcode de dégâts sans être des dégâts.</summary>
    public static bool IsNonDamage(int raw) => IsSpiritLinkRecord(raw) || IsSpiritCommunion(raw);

    public static bool IsPlayerSkill(int code) =>
        (IsClassRange(code) && (code / 10_000) % 100 != 0) || IsDivineStone(code);

    /// <summary>Skill de classe (plage classe, hors codes génériques XX00000 : objets, etc.) ; fonctionne sur code brut ou normalisé.</summary>
    public static bool IsClassSkill(int code) => IsClassRange(code) && (code / 10_000) % 100 != 0;

    public static GameClass ClassOf(int code)
    {
        if (!IsPlayerSkill(code) || code < 10_000_000) return GameClass.Unknown;
        return (code / 1_000_000) switch
        {
            11 => GameClass.Gladiator,
            12 => GameClass.Templar,
            13 => GameClass.Assassin,
            14 => GameClass.Ranger,
            15 => GameClass.Sorcerer,
            16 => GameClass.Elementalist,
            17 => GameClass.Cleric,
            18 => GameClass.Chanter,
            19 => GameClass.Brawler,
            _ => GameClass.Unknown,
        };
    }
}
