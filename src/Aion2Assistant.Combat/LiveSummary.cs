namespace Aion2Assistant.Combat;

/// <summary>Valeurs clés d'un instantané, pour les affichages compacts (barre réduite).</summary>
public static class LiveSummary
{
    /// <summary>DPS (PerSecond) de la ligne IsSelf du meter Dps si l'instantané est actif ; sinon null. Avec bossOnly et un BossDps disponible, lit celui-ci (suit le sélecteur [Boss | Tout]).</summary>
    public static double? SelfDps(EncounterSnapshot? snapshot, bool bossOnly = false)
    {
        if (snapshot is not { Active: true }) return null;
        var self = (bossOnly && snapshot.BossDps is { } boss ? boss : snapshot.Dps).Actors.FirstOrDefault(a => a.IsSelf);
        return self?.PerSecond;
    }

    /// <summary>
    /// Barre réduite : ton DPS sur le combat affiché par le meter (voir <see cref="EncounterDisplay.Select"/>), tant
    /// qu'un combat est en cours ; sinon null. En mode [Boss] pendant un combat hors boss, c'est celui du dernier boss
    /// (IsPastBoss).
    /// </summary>
    public static (double Dps, bool IsPastBoss)? MiniDps(EncounterSnapshot? current, EncounterSnapshot? lastBoss, bool bossOnly)
    {
        if (current is not { Active: true }) return null;
        var shown = EncounterDisplay.Select(current, lastBoss, bossOnly);
        if (shown.Snapshot is not { } s) return null;
        var self = (bossOnly && s.BossDps is { } boss ? boss : s.Dps).Actors.FirstOrDefault(a => a.IsSelf);
        return self is null ? null : (self.PerSecond, shown.IsPastBoss);
    }
}
