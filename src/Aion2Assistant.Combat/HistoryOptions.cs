namespace Aion2Assistant.Combat;

public sealed class HistoryOptions
{
    public int RetentionDays { get; set; } = 30;
    public int MaxEncounters { get; set; } = 100;
    public long MinDurationMs { get; set; } = 20_000;
}
