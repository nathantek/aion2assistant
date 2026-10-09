using System.Globalization;
using Aion2Assistant.Core.Settings;

namespace Aion2Assistant.Timers.Notifications;

public enum AlertKind { Soon, Now }

public sealed record TimerAlert(string TimerId, string Name, string Category, AlertKind Kind, DateTimeOffset DueAt);

/// <summary>
/// Transforme les états des timers en alertes « bientôt » (au seuil) et « maintenant » (à l'échéance).
/// Chaque couple (timer, type, échéance) n'est émis qu'une fois, y compris après un redémarrage
/// (les clés émises sont sauvegardées dans les réglages).
/// </summary>
public sealed class TimerNotifier(IEnumerable<string> emittedKeys, Func<string, TimerSettings> settingsFor)
{
    public static readonly TimeSpan NowWindow = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan KeyRetention = TimeSpan.FromDays(2);

    private readonly HashSet<string> _emitted = new(emittedKeys);

    public IReadOnlyCollection<string> EmittedKeys => _emitted;

    public IReadOnlyList<TimerAlert> Evaluate(IReadOnlyList<TimerState> states, DateTimeOffset now)
    {
        var alerts = new List<TimerAlert>();
        foreach (var state in states)
        {
            var settings = settingsFor(state.Id);
            if (!settings.Notify) continue;

            if (state.PreviousAt is { } previous
                && now - previous <= NowWindow
                && _emitted.Add(Key(state.Id, AlertKind.Now, previous)))
            {
                alerts.Add(new TimerAlert(state.Id, state.Name, state.Category, AlertKind.Now, previous));
            }

            var threshold = TimeSpan.FromMinutes(settings.SoonThresholdMinutes);
            if (state.Remaining > TimeSpan.Zero
                && state.Remaining <= threshold
                && _emitted.Add(Key(state.Id, AlertKind.Soon, state.NextAt)))
            {
                alerts.Add(new TimerAlert(state.Id, state.Name, state.Category, AlertKind.Soon, state.NextAt));
            }
        }
        return alerts;
    }

    public void Prune(DateTimeOffset now) =>
        _emitted.RemoveWhere(key => !TryReadDue(key, out var due) || now - due > KeyRetention);

    private static string Key(string id, AlertKind kind, DateTimeOffset due) =>
        $"{id}|{kind}|{due.UtcTicks.ToString(CultureInfo.InvariantCulture)}";

    private static bool TryReadDue(string key, out DateTimeOffset due)
    {
        due = default;
        var ticksText = key[(key.LastIndexOf('|') + 1)..];
        if (!long.TryParse(ticksText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks)) return false;
        due = new DateTimeOffset(ticks, TimeSpan.Zero);
        return true;
    }
}
