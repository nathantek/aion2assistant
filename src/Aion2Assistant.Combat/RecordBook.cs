namespace Aion2Assistant.Combat;

/// <summary>Meilleur DPS boss d'un personnage sur un PNJ cible.</summary>
public sealed record PersonalRecord(string Character, int NpcCode, string Boss, double Dps, long At, long? EncounterId);

public sealed class RecordFile
{
    public List<PersonalRecord> Records { get; set; } = new();
}

/// <summary>Nouveau record ; <see cref="Previous"/> est null au premier passage sur le boss.</summary>
public sealed record RecordResult(PersonalRecord Record, PersonalRecord? Previous);

public static class RecordBook
{
    /// <summary>null si le combat n'est pas éligible ou ne bat pas le record ; sinon le nouveau record.</summary>
    public static RecordResult? Evaluate(RecordFile file, EncounterSnapshot s, long? encounterId)
    {
        if (s.Outcome != EncounterOutcome.Kill || !s.IsBoss || s.IsTraining || s.TargetNpcCode == 0 || s.BossDps is null)
            return null;
        var me = s.BossDps.Actors.FirstOrDefault(a => a.IsSelf);
        if (me is null) return null;
        var previous = file.Records.FirstOrDefault(r => r.Character == me.Name && r.NpcCode == s.TargetNpcCode);
        if (previous is not null && me.PerSecond <= previous.Dps) return null;
        return new RecordResult(new PersonalRecord(me.Name, s.TargetNpcCode, s.Title, me.PerSecond, s.StartMs, encounterId), previous);
    }

    /// <summary>Remplace l'entrée de la clé (personnage, PNJ), sans doublon.</summary>
    public static void Apply(RecordFile file, PersonalRecord record)
    {
        file.Records.RemoveAll(r => r.Character == record.Character && r.NpcCode == record.NpcCode);
        file.Records.Add(record);
    }
}
