namespace Aion2Assistant.Timers.Config;

/// <summary>Valeurs de secours si timers.json est absent ou illisible. Horaires vérifiés en jeu.</summary>
public static class DefaultTimers
{
    // Resets fixes en UTC (ils ne suivent pas l'heure d'été) ; les failles suivent l'heure de Paris (fuseau propre). Affichage à l'heure locale du PC.
    public const string ServerTimeZone = "UTC";

    public static TimersConfigDto Create() => new()
    {
        ServerTimeZone = ServerTimeZone,
        Timers =
        [
            new() { Id = "rift", Name = "Faille spatio-temporelle", NameEn = "Rift", Kind = TimerKind.Recurring, Anchor = "20:00", Interval = "03:00", OpenFor = "00:10", Category = "rift", Verified = true, TimeZone = "Europe/Paris" },
            new() { Id = "daily-reset", Name = "Reset quotidien", NameEn = "Daily reset", Kind = TimerKind.Daily, Time = "07:00", Category = "reset", Verified = true },
            new() { Id = "weekly-reset", Name = "Reset hebdomadaire", NameEn = "Weekly reset", Kind = TimerKind.Weekly, Day = DayOfWeek.Wednesday, Time = "07:00", Category = "reset", Verified = true },
        ],
    };
}
