using Aion2Assistant.Core.Localization;

namespace Aion2Assistant.Combat;

/// <summary>Suffixe « · puissance » affiché après le nom d'un joueur dans les meters.</summary>
public static class CombatPowerFormat
{
    /// <summary>Même règle que le format compact des meters (<see cref="AppFormat.Compact"/>).</summary>
    public static string Suffix(long? value, bool show)
    {
        if (!show || value is not { } v) return "";
        return " · " + AppFormat.Compact(v);
    }
}
