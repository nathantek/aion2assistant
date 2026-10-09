namespace Aion2Assistant.Timers.Scheduling;

/// <summary>Heures murales (heure serveur) auxquelles un timer tombe pour un jour donné.</summary>
public interface IScheduleRule
{
    /// <summary>Nombre de jours à explorer de part et d'autre d'aujourd'hui pour trouver l'occurrence précédente et la suivante.</summary>
    int LookaroundDays { get; }

    IEnumerable<TimeOnly> TimesOn(DateOnly day);
}

public sealed record RecurringRule : IScheduleRule
{
    private static readonly TimeSpan Day = TimeSpan.FromHours(24);

    public RecurringRule(TimeOnly anchor, TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero || interval > Day || Day.Ticks % interval.Ticks != 0)
            throw new ArgumentException($"l'intervalle {interval} doit diviser 24 h", nameof(interval));
        Anchor = anchor;
        Interval = interval;
    }

    public TimeOnly Anchor { get; }
    public TimeSpan Interval { get; }
    public int LookaroundDays => 1;

    public IEnumerable<TimeOnly> TimesOn(DateOnly day)
    {
        var count = (int)(Day.Ticks / Interval.Ticks);
        return Enumerable.Range(0, count)
            .Select(k => Anchor.Add(Interval * k))
            .Order();
    }
}

public sealed record DailyRule(TimeOnly Time) : IScheduleRule
{
    public int LookaroundDays => 1;

    public IEnumerable<TimeOnly> TimesOn(DateOnly day) => [Time];
}

public sealed record WeeklyRule(DayOfWeek Day, TimeOnly Time) : IScheduleRule
{
    public int LookaroundDays => 7;

    public IEnumerable<TimeOnly> TimesOn(DateOnly day) => day.DayOfWeek == Day ? [Time] : [];
}
