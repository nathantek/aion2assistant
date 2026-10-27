using Aion2Assistant.Timers.Scheduling;

namespace Aion2Assistant.Timers;

/// <summary>Définition d'un timer. <see cref="Zone"/> : fuseau propre au timer (null = fuseau global de timers.json).</summary>
public sealed record TimerDefinition(
    string Id,
    string Name,
    string Category,
    IScheduleRule Rule,
    TimeSpan? OpenFor,
    bool Verified,
    string? ShortName = null,
    ServerTime? Zone = null);
