using Aion2Assistant.Core.Events;

namespace Aion2Assistant.Combat;

public enum EntityKind { Unknown, Player, Npc, Summon }

/// <summary>Tables id → joueur / PNJ / invocation, alimentées par les événements de jeu.</summary>
public sealed class EntityRegistry
{
    private readonly GameData _data;
    private readonly Dictionary<uint, (string Name, GameClass Class)> _players = new();
    private readonly HashSet<uint> _inferred = new();
    private readonly Dictionary<uint, int> _npcs = new();
    private readonly Dictionary<uint, (uint OwnerId, string? OwnerName)> _summons = new();
    private readonly Dictionary<uint, long> _maxHp = new();
    private readonly Dictionary<uint, long> _curHp = new();
    private GameClass _selfClass;

    public EntityRegistry(GameData data) => _data = data;

    public uint SelfId { get; private set; }
    public string? SelfName { get; private set; }

    public void Apply(GameEvent e)
    {
        switch (e)
        {
            case SelfIdentifiedEvent s:
                SelfId = s.ActorId;
                SelfName = s.Name;
                _selfClass = s.Class;
                RegisterPlayer(s.ActorId, s.Name, s.Class);
                break;
            case PlayerSeenEvent p:
                RegisterPlayer(p.ActorId, p.Name, p.Class);
                break;
            case NpcSeenEvent n:
                var sameNpc = _npcs.TryGetValue(n.ActorId, out var oldCode) && oldCode == n.NpcCode;
                _players.Remove(n.ActorId);
                _summons.Remove(n.ActorId);
                _npcs[n.ActorId] = n.NpcCode;
                if (!sameNpc) ForgetHp(n.ActorId);
                if (n.MaxHp > 0) _maxHp[n.ActorId] = n.MaxHp;
                break;
            case SummonSeenEvent m:
                _players.Remove(m.ActorId);
                _npcs.Remove(m.ActorId);
                if (!_summons.ContainsKey(m.ActorId)) ForgetHp(m.ActorId);
                _summons[m.ActorId] = (m.OwnerId, m.OwnerName);
                break;
            case HpEvent h:
                _curHp[h.ActorId] = h.CurrentHp;
                if (h.MaxHp > 0) _maxHp[h.ActorId] = h.MaxHp;
                break;
            case ZoneChangedEvent { IsTeleport: false }:
                ClearAll();
                if (SelfId != 0 && SelfName is not null) RegisterPlayer(SelfId, SelfName, _selfClass);
                break;
            case StreamStartedEvent:
                ClearAll();
                SelfId = 0;
                SelfName = null;
                _selfClass = GameClass.Unknown;
                break;
        }
    }

    /// <summary>Déduit qu'un id inconnu est un joueur (il a utilisé un skill de joueur) ; sans nom. Un PlayerSeen/NpcSeen ultérieur prévaut.</summary>
    public void MarkInferredPlayer(uint id, GameClass cls = GameClass.Unknown)
    {
        if (KindOf(id) != EntityKind.Unknown) return;
        _players[id] = ($"#{id}", cls);
        _inferred.Add(id);
    }

    private void RegisterPlayer(uint id, string name, GameClass cls)
    {
        _npcs.Remove(id);
        _summons.Remove(id);
        if (!_players.ContainsKey(id)) ForgetHp(id);
        _players[id] = (name, cls);
        _inferred.Remove(id);
    }

    private void ForgetHp(uint id)
    {
        _curHp.Remove(id);
        _maxHp.Remove(id);
    }

    private void ClearAll()
    {
        _players.Clear();
        _inferred.Clear();
        _npcs.Clear();
        _summons.Clear();
        _maxHp.Clear();
        _curHp.Clear();
    }

    public EntityKind KindOf(uint id) =>
        _players.ContainsKey(id) ? EntityKind.Player
        : _npcs.ContainsKey(id) ? EntityKind.Npc
        : _summons.ContainsKey(id) ? EntityKind.Summon
        : EntityKind.Unknown;

    public uint ResolveOwner(uint id)
    {
        if (!_summons.TryGetValue(id, out var s)) return id;
        if (s.OwnerId != 0) return s.OwnerId;
        if (s.OwnerName is not null)
        {
            foreach (var (pid, p) in _players)
                if (string.Equals(p.Name, s.OwnerName, StringComparison.Ordinal)) return pid;
        }
        return id;
    }

    public string NameOf(uint id)
    {
        if (_players.TryGetValue(id, out var p)) return p.Name;
        if (_npcs.TryGetValue(id, out var code)) return _data.NpcName(code);
        return $"#{id}";
    }

    /// <summary>Vrai si un vrai nom a été capturé (joueur annoncé ou soi-même) ; faux pour un joueur seulement déduit (« #id »), un PNJ ou un inconnu.</summary>
    public bool HasName(uint id) => _players.ContainsKey(id) && !_inferred.Contains(id);

    public GameClass ClassOf(uint id) => _players.TryGetValue(id, out var p) ? p.Class : GameClass.Unknown;

    public int NpcCodeOf(uint id) => _npcs.TryGetValue(id, out var c) ? c : 0;

    public long MaxHpOf(uint id) => _maxHp.TryGetValue(id, out var v) ? v : 0;

    public long? CurrentHpOf(uint id) => _curHp.TryGetValue(id, out var v) ? v : null;

    public void SetCurrentHp(uint id, long hp) => _curHp[id] = hp;
}
