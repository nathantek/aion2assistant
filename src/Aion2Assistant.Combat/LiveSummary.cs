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
}
