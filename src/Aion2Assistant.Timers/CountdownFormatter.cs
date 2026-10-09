namespace Aion2Assistant.Timers;

public static class CountdownFormatter
{
    /// <summary>Compte à rebours compact : 04:05, 1:05:09, 2 j 04:10.</summary>
    public static string Format(TimeSpan remaining)
    {
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        if (remaining.TotalDays >= 1)
            return $"{(int)remaining.TotalDays} j {remaining.Hours:00}:{remaining.Minutes:00}";
        if (remaining.TotalHours >= 1)
            return $"{(int)remaining.TotalHours}:{remaining.Minutes:00}:{remaining.Seconds:00}";
        return $"{remaining.Minutes:00}:{remaining.Seconds:00}";
    }

    /// <summary>Durée en langage courant, pour les pop-ups : « 5 min », « 1 h 05 ».</summary>
    public static string FormatHuman(TimeSpan remaining)
    {
        if (remaining < TimeSpan.FromMinutes(1)) return "moins d'une minute";
        if (remaining < TimeSpan.FromHours(1)) return $"{(int)Math.Ceiling(remaining.TotalMinutes)} min";
        return $"{(int)remaining.TotalHours} h {remaining.Minutes:00}";
    }
}
