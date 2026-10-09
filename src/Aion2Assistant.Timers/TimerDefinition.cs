using Aion2Assistant.Timers.Scheduling;

namespace Aion2Assistant.Timers;

public sealed record TimerDefinition(
    string Id,
    string Name,
    string Category,
    IScheduleRule Rule,
    TimeSpan? OpenFor,
    bool Verified,
    string? ShortName = null);
