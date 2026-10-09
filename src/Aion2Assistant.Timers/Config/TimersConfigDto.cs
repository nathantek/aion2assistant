namespace Aion2Assistant.Timers.Config;

public sealed class TimersConfigDto
{
    public string ServerTimeZone { get; set; } = DefaultTimers.ServerTimeZone;
    public List<TimerDto> Timers { get; set; } = [];
}

public sealed class TimerDto
{
    public string Id { get; set; } = "";
    public string? Name { get; set; }
    public string? ShortName { get; set; }
    public TimerKind Kind { get; set; }
    public string? Category { get; set; }
    public string? Anchor { get; set; }
    public string? Interval { get; set; }
    public string? OpenFor { get; set; }
    public DayOfWeek? Day { get; set; }
    public string? Time { get; set; }
    public bool Verified { get; set; }
}
