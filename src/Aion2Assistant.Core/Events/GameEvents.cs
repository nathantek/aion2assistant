namespace Aion2Assistant.Core.Events;

[Flags]
public enum HitFlags : ushort
{
    None = 0,
    Critical = 1 << 0,
    Back = 1 << 1,
    Front = 1 << 2,
    Perfect = 1 << 3,
    Heavy = 1 << 4,
    Multi = 1 << 5,
    Parry = 1 << 6,
    Double = 1 << 7,
    Dot = 1 << 8,
}

public enum GameClass : byte { Unknown = 0, Gladiator, Templar, Ranger, Assassin, Elementalist, Sorcerer, Cleric, Chanter, Brawler }

/// <summary>Événement décodé d'un paquet. <see cref="TimeMs"/> = horodatage Unix (ms) du paquet capturé.</summary>
public abstract record GameEvent(long TimeMs);

/// <summary>Dégâts directs ou sur la durée. <see cref="Damage"/> inclut les coups supplémentaires ; <see cref="HitCount"/> = 1 + coups supplémentaires.</summary>
public sealed record DamageEvent(long TimeMs, uint SourceId, uint TargetId, int SkillCode, long Damage,
    HitFlags Flags, int HitCount, int PowerScalar) : GameEvent(TimeMs);

public sealed record HealEvent(long TimeMs, uint SourceId, uint TargetId, int SkillCode, long Amount, HitFlags Flags) : GameEvent(TimeMs);

public sealed record SelfIdentifiedEvent(long TimeMs, uint ActorId, string Name, int ServerId, GameClass Class, int Level) : GameEvent(TimeMs);

/// <summary>Statistiques du joueur local (0x008D sortes 1/3, reçues seulement par soi) : permet de déduire son id sans paquet d'identité.</summary>
public sealed record SelfStatsSeenEvent(long TimeMs, uint ActorId) : GameEvent(TimeMs);

public sealed record PlayerSeenEvent(long TimeMs, uint ActorId, string Name, int ServerId, GameClass Class) : GameEvent(TimeMs);

/// <summary>Puissance de combat actuelle d'un joueur. <see cref="ActorId"/> = 0 quand la source ne donne que le nom (roster, inspection).</summary>
public sealed record CombatPowerSeenEvent(long TimeMs, uint ActorId, string? Name, long CombatPower) : GameEvent(TimeMs);

public static class CombatPowerRange
{
    public const long Min = 1, Max = 100_000;

    /// <summary>Hors de 1..100 000 (dont 0) = puissance inconnue.</summary>
    public static bool IsValid(long value) => value is >= Min and <= Max;
}

public sealed record NpcSeenEvent(long TimeMs, uint ActorId, int NpcCode, long MaxHp, float X, float Y, float Z) : GameEvent(TimeMs);

/// <summary>Invocation, esprit ou effet persistant. Propriétaire connu par id (<see cref="OwnerId"/> ≠ 0) ou par nom.</summary>
public sealed record SummonSeenEvent(long TimeMs, uint ActorId, uint OwnerId, string? OwnerName, int NpcCode) : GameEvent(TimeMs);

/// <summary>PV d'une entité. <see cref="MaxHp"/> = 0 quand le paquet ne le donne pas.</summary>
public sealed record HpEvent(long TimeMs, uint ActorId, long CurrentHp, long MaxHp) : GameEvent(TimeMs);

public sealed record DeathEvent(long TimeMs, uint ActorId, bool AlreadyDead) : GameEvent(TimeMs);

/// <summary>Annonce de lancement d'un skill. Le code est la variante brute (non normalisée).</summary>
public sealed record CastEvent(long TimeMs, uint ActorId, uint TargetId, int SkillCode) : GameEvent(TimeMs);

/// <summary>Un flux de jeu vient d'être verrouillé (première connexion, reconnexion ou changement de serveur) : l'état par flux des consommateurs doit être réinitialisé.</summary>
public sealed record StreamStartedEvent(long TimeMs, string Flow) : GameEvent(TimeMs);

/// <summary>Chargement de carte. Un second chargement de la même carte est une téléportation interne.</summary>
public sealed record ZoneChangedEvent(long TimeMs, int MapId, bool IsDungeon, bool IsTeleport) : GameEvent(TimeMs);

public sealed record FieldBossSlot(int SlotId, bool Alive, long AtMs, float X, float Y, float Z);

/// <summary>Liste des boss de terrain d'une carte (carte en jeu). <see cref="FieldBossSlot.AtMs"/> = apparu depuis (vivant) ou retour à (mort), 0 = inconnu.</summary>
public sealed record FieldBossListEvent(long TimeMs, int MapId, int Count, IReadOnlyList<FieldBossSlot> Slots) : GameEvent(TimeMs);
