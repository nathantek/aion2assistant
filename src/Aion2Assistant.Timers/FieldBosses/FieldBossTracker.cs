using System.Globalization;
using Aion2Assistant.Core.Events;
using Aion2Assistant.Core.Localization;

namespace Aion2Assistant.Timers.FieldBosses;

/// <summary>Timers de boss de terrain issus des listes réseau (0x0191). Thread UI uniquement.</summary>
public sealed class FieldBossTracker : IFieldBossSource
{
    public const string Category = "fieldboss";
    /// <summary>Suffixe du nom d'un boss supposé apparu, dans la langue de l'app (« (supposé) »).</summary>
    public static string AssumedSuffix => Strings.FieldBossAssumedSuffix;
    public static readonly TimeSpan AssumedAliveFor = TimeSpan.FromHours(2);

    private readonly Func<int, int, string> _bossName;
    private int _serverId;

    public FieldBossTracker(FieldBossFile state, Func<int, int, string> bossName)
    {
        // Un bosses.json valide mais édité à la main peut contenir des null : on les retire,
        // sinon GetStates lèverait à chaque calcul et viderait tout l'onglet Timers.
        state.Servers ??= new();
        foreach (var key in state.Servers.Where(p => p.Value is null).Select(p => p.Key).ToList())
            state.Servers.Remove(key);
        foreach (var slots in state.Servers.Values)
            foreach (var key in slots.Where(p => p.Value is null).Select(p => p.Key).ToList())
                slots.Remove(key);
        State = state;
        _bossName = bossName;
        _serverId = state.LastServerId;
    }

    public FieldBossFile State { get; }

    /// <summary>0 = inconnu ; la valeur est aussi écrite dans <see cref="FieldBossFile.LastServerId"/>.</summary>
    public int ServerId
    {
        get => _serverId;
        set
        {
            _serverId = value;
            State.LastServerId = value;
        }
    }

    public static string TimerId(int serverId, int slotId) => $"fieldboss:{serverId}:{slotId}";

    public bool Apply(FieldBossListEvent e)
    {
        var key = ServerId.ToString(CultureInfo.InvariantCulture);
        if (!State.Servers.TryGetValue(key, out var slots)) State.Servers[key] = slots = new();
        var seen = DateTimeOffset.FromUnixTimeMilliseconds(e.TimeMs);
        var changed = false;
        foreach (var s in e.Slots)
        {
            var at = s.AtMs == 0 ? (DateTimeOffset?)null : DateTimeOffset.FromUnixTimeMilliseconds(s.AtMs);
            var slotKey = s.SlotId.ToString(CultureInfo.InvariantCulture);
            if (!slots.TryGetValue(slotKey, out var cur))
            {
                changed = true;
                slots[slotKey] = cur = new FieldBossSlotState();
            }
            else if (cur.Alive != s.Alive || cur.At != at)
            {
                changed = true;
            }
            cur.MapId = e.MapId;
            cur.SlotId = s.SlotId;
            cur.Alive = s.Alive;
            cur.At = at;
            cur.LastSeen = seen;
        }
        return changed;
    }

    public IReadOnlyList<TimerState> GetStates(DateTimeOffset nowUtc)
    {
        var result = new List<TimerState>();
        if (!State.Servers.TryGetValue(ServerId.ToString(CultureInfo.InvariantCulture), out var slots)) return result;
        foreach (var s in slots.Values)
        {
            var id = TimerId(ServerId, s.SlotId);
            var name = _bossName(s.MapId, s.SlotId);
            if (s.Alive)
            {
                result.Add(Open(id, name, s.At ?? s.LastSeen));
            }
            else if (s.At is { } at)
            {
                if (at > nowUtc)
                    result.Add(new TimerState(id, name, Category, at, null, at - nowUtc, TimerPhase.Waiting, null, 0, true));
                else if (nowUtc - at < AssumedAliveFor)
                    result.Add(Open(id, name + AssumedSuffix, at));
            }
        }
        return result;
    }

    private static TimerState Open(string id, string name, DateTimeOffset nextAt) =>
        new(id, name, Category, nextAt, null, TimeSpan.Zero, TimerPhase.Open, null, 0, true);
}
