namespace Aion2Assistant.Combat;

/// <summary>Une compétence dans les deux essais ; Delta = (B-A)/A*100, null si A ou B absent ou A == 0.</summary>
public sealed record SkillComparison(string Name, long? TotalA, long? TotalB, double? DeltaPct);

/// <summary>Comparaison du joueur entre deux essais. Les totaux (hors contrat de base) alimentent la ligne « Dégâts totaux ».</summary>
public sealed record TrainingComparison(string? Character, long DurationA, long DurationB, double? DpsA, double? DpsB,
    double? DpsDeltaPct, double? CritA, double? CritB, IReadOnlyList<SkillComparison> Skills,
    long? TotalA = null, long? TotalB = null, double? TotalDeltaPct = null);

public static class TrainingComparer
{
    /// <summary>Compare la ligne IsSelf du meter Dps des deux essais (A = le plus ancien). Compétences triées par max(A, B) décroissant.</summary>
    public static TrainingComparison Compare(EncounterSnapshot a, EncounterSnapshot b)
    {
        if (a.StartMs > b.StartMs) (a, b) = (b, a);
        var ra = a.Dps.Actors.FirstOrDefault(r => r.IsSelf);
        var rb = b.Dps.Actors.FirstOrDefault(r => r.IsSelf);

        var skills = new List<SkillComparison>();
        var names = (ra?.Skills ?? []).Concat(rb?.Skills ?? []).Select(s => s.Name).Distinct();
        foreach (var name in names)
        {
            long? ta = Total(ra, name), tb = Total(rb, name);
            skills.Add(new SkillComparison(name, ta, tb, DeltaPct(ta, tb)));
        }
        var sorted = skills.OrderByDescending(s => Math.Max(s.TotalA ?? 0, s.TotalB ?? 0)).ThenBy(s => s.Name, StringComparer.Ordinal).ToList();

        double? dpsA = ra?.PerSecond, dpsB = rb?.PerSecond;
        long? totA = ra?.Total, totB = rb?.Total;
        return new TrainingComparison(rb?.Name ?? ra?.Name, a.DurationMs, b.DurationMs, dpsA, dpsB, DeltaPct(dpsA, dpsB),
            Crit(ra), Crit(rb), sorted, totA, totB, DeltaPct(totA, totB));
    }

    private static long? Total(ActorRow? row, string name)
    {
        if (row is null) return null;
        long? sum = null;
        foreach (var s in row.Skills)
            if (s.Name == name) sum = (sum ?? 0) + s.Total;
        return sum;
    }

    /// <summary>(B-A)/A*100 ; null si A ou B absent ou A == 0.</summary>
    public static double? DeltaPct(double? a, double? b) => a is { } x && b is { } y && x != 0 ? (y - x) / x * 100 : null;

    /// <summary>Moyenne des CritPct non nuls, pondérée par les coups ; null s'il n'y en a aucun.</summary>
    private static double? Crit(ActorRow? row)
    {
        if (row is null) return null;
        double sum = 0;
        long hits = 0;
        foreach (var s in row.Skills)
        {
            if (s.CritPct is not { } c || s.Hits <= 0) continue;
            sum += c * s.Hits;
            hits += s.Hits;
        }
        return hits == 0 ? null : sum / hits;
    }
}
