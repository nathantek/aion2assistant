using System.Globalization;

namespace Aion2Assistant.Combat;

/// <summary>Suffixe « · puissance » affiché après le nom d'un joueur dans les meters.</summary>
public static class CombatPowerFormat
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    /// <summary>Même règle que le format compact des meters : ≥ 1 M → 0.00 M, ≥ 10 k → 0.0 k, sinon N0 (fr-FR).</summary>
    public static string Suffix(long? value, bool show)
    {
        if (!show || value is not { } v) return "";
        return " · " + Compact(v);
    }

    private static string Compact(double v)
    {
        if (v >= 1_000_000) return (v / 1_000_000).ToString("0.00", French) + " M";
        if (v >= 10_000) return (v / 1_000).ToString("0.0", French) + " k";
        return v.ToString("N0", French);
    }
}
