using Aion2Assistant.Core.Events;

namespace Aion2Assistant.Combat;

/// <summary>Dégâts par seconde d'un acteur sur la durée du combat (une case par seconde écoulée).</summary>
public sealed record ActorSeries(uint ActorId, string Name, bool IsSelf, IReadOnlyList<long> DamagePerSecond,
    GameClass Class = GameClass.Unknown);

/// <summary>Dégâts par seconde d'un couple (cible, source), stockés à partir de la première seconde touchée (coût proportionnel à l'activité, pas à la durée).</summary>
internal sealed class SecondSeries
{
    public int Start;
    public readonly List<long> Values = new();

    public void Add(int second, long amount)
    {
        if (Values.Count == 0) Start = second;
        else if (second < Start)
        {
            Values.InsertRange(0, new long[Start - second]);
            Start = second;
        }
        var i = second - Start;
        while (Values.Count <= i) Values.Add(0);
        Values[i] += amount;
    }
}

public static class Timeline
{
    public const int MaxSeries = 8;
    public const int SmoothingSeconds = 5;

    /// <summary>Moyenne glissante : r[i] = somme(perSecond[max(0,i-window+1)..i]) / min(window, i+1). Vide → vide.</summary>
    public static double[] Rolling(IReadOnlyList<long> perSecond, int window = SmoothingSeconds)
    {
        window = Math.Max(1, window);
        var result = new double[perSecond.Count];
        long sum = 0;
        for (var i = 0; i < perSecond.Count; i++)
        {
            sum += perSecond[i];
            if (i >= window) sum -= perSecond[i - window];
            result[i] = sum / (double)Math.Min(window, i + 1);
        }
        return result;
    }

    /// <summary>
    /// Une série par acteur du meter (dans son ordre, limitée à MaxSeries), cumulée sur les cibles retenues.
    /// Les secondes au-delà de la durée sont ajoutées à la dernière case : la somme égale le total de l'acteur.
    /// </summary>
    internal static IReadOnlyList<ActorSeries> Build(MeterSnapshot meter, IEnumerable<KeyValuePair<uint, Dictionary<uint, SecondSeries>>> targets,
        long durationMs)
    {
        var n = Math.Max(1, (int)Math.Ceiling(durationMs / 1000.0));
        var targetList = targets.ToList();
        var series = new List<ActorSeries>();
        foreach (var row in meter.Actors.Take(MaxSeries))
        {
            var perSecond = new long[n];
            foreach (var (_, bySource) in targetList)
            {
                if (!bySource.TryGetValue(row.ActorId, out var seconds)) continue;
                for (var i = 0; i < seconds.Values.Count; i++) perSecond[Math.Min(seconds.Start + i, n - 1)] += seconds.Values[i];
            }
            series.Add(new ActorSeries(row.ActorId, row.Name, row.IsSelf, perSecond, row.Class));
        }
        return series;
    }
}
