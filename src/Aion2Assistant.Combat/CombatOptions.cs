namespace Aion2Assistant.Combat;

public sealed class CombatOptions
{
    public long IdleTimeoutMs { get; set; } = 8_000;
    public long BossMinHp { get; set; } = 1_000_000;
    public bool IncludeAllPlayers { get; set; } = true;
    public long RecapWindowMs { get; set; } = 10_000;
}
