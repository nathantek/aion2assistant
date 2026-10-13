namespace Aion2Assistant.Timers.Config;

public sealed class TimersConfigDto
{
    public string ServerTimeZone { get; set; } = DefaultTimers.ServerTimeZone;
    public List<TimerDto> Timers { get; set; } = [];
}

public sealed class TimerDto
{
    public string Id { get; set; } = "";
    /// <summary>Nom français (champ d'origine) ; repli de toutes les langues.</summary>
    public string? Name { get; set; }
    public string? ShortName { get; set; }
    /// <summary>Noms anglais (facultatifs, depuis la 0.4) ; absents ou vides = Name / ShortName.</summary>
    public string? NameEn { get; set; }
    public string? ShortNameEn { get; set; }
    public TimerKind Kind { get; set; }
    public string? Category { get; set; }
    public string? Anchor { get; set; }
    public string? Interval { get; set; }
    public string? OpenFor { get; set; }
    public DayOfWeek? Day { get; set; }
    public string? Time { get; set; }
    public bool Verified { get; set; }
}
