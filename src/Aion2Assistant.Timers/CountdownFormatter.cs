using Aion2Assistant.Core.Localization;

namespace Aion2Assistant.Timers;

public static class CountdownFormatter
{
    /// <summary>Compte à rebours compact : 04:05, 1:05:09, 2 j 04:10 (en : 2d 04:10).</summary>
    public static string Format(TimeSpan remaining)
    {
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        if (remaining.TotalDays >= 1)
            return Strings.Format(nameof(Strings.CountdownDays), (int)remaining.TotalDays, remaining.Hours, remaining.Minutes);
        if (remaining.TotalHours >= 1)
            return $"{(int)remaining.TotalHours}:{remaining.Minutes:00}:{remaining.Seconds:00}";
        return $"{remaining.Minutes:00}:{remaining.Seconds:00}";
    }

    /// <summary>Durée en langage courant, pour les pop-ups : « 5 min », « 1 h 05 » (en : 1h 05).</summary>
    public static string FormatHuman(TimeSpan remaining)
    {
        if (remaining < TimeSpan.FromMinutes(1)) return Strings.CountdownUnderMinute;
        if (remaining < TimeSpan.FromHours(1)) return Strings.Format(nameof(Strings.CountdownMinutes), (int)Math.Ceiling(remaining.TotalMinutes));
        return Strings.Format(nameof(Strings.CountdownHours), (int)remaining.TotalHours, remaining.Minutes);
    }
}
