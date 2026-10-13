namespace Aion2Assistant.Timers.Config;

/// <summary>Valeurs de secours si timers.json est absent ou illisible. Horaires vérifiés en jeu.</summary>
public static class DefaultTimers
{
    public const string ServerTimeZone = "Europe/Paris";

    public static TimersConfigDto Create() => new()
    {
        ServerTimeZone = ServerTimeZone,
        Timers =
        [
            new() { Id = "rift", Name = "Faille spatio-temporelle", NameEn = "Rift", Kind = TimerKind.Recurring, Anchor = "20:00", Interval = "03:00", OpenFor = "00:10", Category = "rift", Verified = true },
            new() { Id = "daily-reset", Name = "Reset quotidien", NameEn = "Daily reset", Kind = TimerKind.Daily, Time = "09:00", Category = "reset", Verified = true },
            new() { Id = "weekly-reset", Name = "Reset hebdomadaire", NameEn = "Weekly reset", Kind = TimerKind.Weekly, Day = DayOfWeek.Wednesday, Time = "09:00", Category = "reset", Verified = true },
        ],
    };
}
