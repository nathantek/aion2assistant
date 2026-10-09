namespace Aion2Assistant.Timers.FieldBosses;

public sealed class FieldBossSlotState
{
    public int MapId { get; set; }
    public int SlotId { get; set; }
    public bool Alive { get; set; }

    /// <summary>Vivant depuis / de retour à ; null = inconnu.</summary>
    public DateTimeOffset? At { get; set; }

    /// <summary>Réception de la liste.</summary>
    public DateTimeOffset LastSeen { get; set; }
}

/// <summary>Contenu de bosses.json.</summary>
public sealed class FieldBossFile
{
    public int LastServerId { get; set; }

    /// <summary>Serveur → slotId → état.</summary>
    public Dictionary<string, Dictionary<string, FieldBossSlotState>> Servers { get; set; } = new();
}
