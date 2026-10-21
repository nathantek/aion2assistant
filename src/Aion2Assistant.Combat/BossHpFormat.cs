using Aion2Assistant.Core.Localization;

namespace Aion2Assistant.Combat;

/// <summary>Texte des PV de la cible principale, au-dessus de sa barre de vie (meters et image copiée).</summary>
public static class BossHpFormat
{
    /// <summary>
    /// Chiffres : « 1,24 M / 2,00 M » (format compact des meters) ; pourcentage : « 62 % » sans décimale.
    /// Arrondi vers le bas : un boss vivant n'affiche jamais 100 % ni ses PV max, et jamais 0 % tant qu'il lui reste des PV.
    /// null quand il n'y a rien à afficher (PV inconnus, ou historique antérieur sans PV en chiffres).
    /// </summary>
    public static string? Text(long? hp, long? maxHp, double? pct, bool asPercent)
    {
        if (asPercent) return pct is { } p ? AppFormat.Percent(PercentShown(hp, maxHp, p)) : null;
        if (hp is not { } cur || maxHp is not { } max || max <= 0) return null;
        cur = Math.Clamp(cur, 0, max);
        return AppFormat.Compact(cur < max ? FloorToCompact(cur) : cur) + " / " + AppFormat.Compact(max);
    }

    private static double PercentShown(long? hp, long? maxHp, double pct)
    {
        var shown = Math.Floor(Math.Clamp(pct, 0, 100));
        // PV en chiffres connus : ils priment sur le pourcentage (arrondi en amont) pour dire si le boss est vivant / entamé.
        var alive = hp is { } cur ? cur > 0 : pct > 0;
        if (alive) shown = Math.Max(1, shown);
        if (hp is { } h && maxHp is { } max && h < max) shown = Math.Min(99, shown);
        if (hp is 0) shown = 0;
        return shown;
    }

    /// <summary>
    /// Tronque à la précision de <see cref="AppFormat.Compact"/> (unité sous 10 000, centaine dès 10 000, dizaine de milliers dès 1 M) pour que
    /// l'arrondi au plus proche ne remonte jamais un PV courant jusqu'au max (1 995 000 → « 1,99 M », pas « 2,00 M »).
    /// </summary>
    private static long FloorToCompact(long v)
    {
        var step = v >= 1_000_000 ? 10_000 : v >= 10_000 ? 100 : 1;
        return v / step * step;
    }

    public static string? Text(EncounterSnapshot s, bool asPercent) => Text(s.TargetHp, s.TargetMaxHp, s.TargetHpPct, asPercent);
}
