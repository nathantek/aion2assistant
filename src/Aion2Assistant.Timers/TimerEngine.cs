using Aion2Assistant.Core;

namespace Aion2Assistant.Timers;

/// <summary>Calcule l'état de chaque timer. Sans état interne : chaque appel repart de l'horloge, ce qui gère aussi la sortie de veille.</summary>
public sealed class TimerEngine(
    IClock clock,
    ServerTime serverTime,
    IReadOnlyList<TimerDefinition> definitions,
    IFieldBossSource? fieldBosses = null)
{
    public IReadOnlyList<TimerDefinition> Definitions => definitions;

    public IReadOnlyList<TimerState> Compute()
    {
        var now = clock.UtcNow;
        var states = definitions.Select(d => ComputeOne(d, now)).ToList();
        states.AddRange((fieldBosses ?? NoFieldBosses.Instance).GetStates(now));
        states.Sort((a, b) => a.NextAt.CompareTo(b.NextAt));
        return states;
    }

    private TimerState ComputeOne(TimerDefinition definition, DateTimeOffset now)
    {
        var today = serverTime.ServerDate(now);
        var span = definition.Rule.LookaroundDays;
        DateTimeOffset? previous = null;
        DateTimeOffset? next = null;

        for (var offset = -span; offset <= span; offset++)
        {
            var day = today.AddDays(offset);
            foreach (var time in definition.Rule.TimesOn(day))
            {
                var at = serverTime.ToUtc(day, time);
                if (at <= now)
                {
                    if (previous is null || at > previous) previous = at;
                }
                else if (next is null || at < next)
                {
                    next = at;
                }
            }
        }

        if (next is not { } nextAt)
            throw new InvalidOperationException($"Aucune occurrence trouvée pour le timer « {definition.Id} ».");

        DateTimeOffset? openUntil = previous is { } prev && definition.OpenFor is { } duration && now < prev + duration
            ? prev + duration
            : null;
        var progress = previous is { } p ? Math.Clamp((now - p) / (nextAt - p), 0, 1) : 0;

        return new TimerState(
            definition.Id,
            definition.Name,
            definition.Category,
            nextAt,
            previous,
            nextAt - now,
            openUntil is null ? TimerPhase.Waiting : TimerPhase.Open,
            openUntil,
            progress,
            definition.Verified);
    }
}
