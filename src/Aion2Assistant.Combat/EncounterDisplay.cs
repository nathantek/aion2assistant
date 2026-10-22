namespace Aion2Assistant.Combat;

/// <summary>
/// Combat montré par les meters live. IsPastBoss : en mode [Boss], on garde le dernier combat de boss alors qu'un
/// combat plus récent existe (en cours ou terminé) ; l'en-tête l'indique (« Dernier boss · il y a 2 min »).
/// </summary>
public readonly record struct DisplayedEncounter(EncounterSnapshot? Snapshot, bool IsPastBoss);

/// <summary>Choix purs du combat affiché (mode [Boss | Tout]) et règle du combat vide.</summary>
public static class EncounterDisplay
{
    /// <summary>Combat avec une cible boss (cible principale ou dégâts sur un boss).</summary>
    public static bool IsBossEncounter(EncounterSnapshot s) => s.IsBoss || s.BossDps is not null;

    /// <summary>Combat vide : aucun dégât ni soin infligé par les joueurs suivis, et aucune mort d'un membre.</summary>
    public static bool IsEmpty(EncounterSnapshot s) => s.Dps.Total == 0 && s.Heal.Total == 0 && s.Deaths.Count == 0;

    /// <summary>
    /// Mode [Tout] : le combat courant du suivi. Mode [Boss] : le combat courant s'il a une cible boss (ou si c'est un
    /// essai d'entraînement), sinon le dernier combat de boss ; sans dernier boss, le combat courant (comme avant).
    /// </summary>
    public static DisplayedEncounter Select(EncounterSnapshot? current, EncounterSnapshot? lastBoss, bool bossOnly)
    {
        if (!bossOnly || lastBoss is null) return new(current, false);
        if (current is not null && (IsBossEncounter(current) || current.IsTraining)) return new(current, false);
        // Renommage tardif : les instances peuvent différer, le début du combat l'identifie.
        return new(lastBoss, current is null || current.StartMs != lastBoss.StartMs);
    }

    /// <summary>Minutes entières écoulées depuis la fin du combat (0 au-dessous d'une minute).</summary>
    public static int MinutesSinceEnd(EncounterSnapshot s, long nowMs) =>
        (int)Math.Max(0, (nowMs - (s.StartMs + s.DurationMs)) / 60_000);
}
