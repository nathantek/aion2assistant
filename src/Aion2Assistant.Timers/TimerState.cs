namespace Aion2Assistant.Timers;

public enum TimerPhase { Waiting, Open }

/// <summary>Photographie d'un timer à un instant donné. <see cref="Progress"/> va de 0 à 1 entre l'occurrence précédente et la suivante.</summary>
public sealed record TimerState(
    string Id,
    string Name,
    string Category,
    DateTimeOffset NextAt,
    DateTimeOffset? PreviousAt,
    TimeSpan Remaining,
    TimerPhase Phase,
    DateTimeOffset? OpenUntil,
    double Progress,
    bool Verified,
    string? ShortName = null);

/// <summary>Timers de boss de terrain alimentés par la capture réseau (étape 5).</summary>
public interface IFieldBossSource
{
    IReadOnlyList<TimerState> GetStates(DateTimeOffset nowUtc);
}

public sealed class NoFieldBosses : IFieldBossSource
{
    public static readonly NoFieldBosses Instance = new();

    public IReadOnlyList<TimerState> GetStates(DateTimeOffset nowUtc) => [];
}
