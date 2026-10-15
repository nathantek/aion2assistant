using Aion2Assistant.App.Localization;
using Aion2Assistant.App.ViewModels;
using Aion2Assistant.Combat;
using Aion2Assistant.Core.Events;
using Aion2Assistant.Core.Settings;
using Serilog;

namespace Aion2Assistant.App.Services;

/// <summary>Enregistre les combats terminés dans la base et alimente l'onglet Historique (thread UI).</summary>
internal sealed class HistoryController : IDisposable
{
    private readonly MetersController _meters;
    private readonly AppSettings _settings;
    private readonly Func<int, string> _mapName;
    private readonly HistoryStore? _store;

    // Combats enregistrés depuis la dernière réattribution des ids, avec leurs acteurs encore « #id ».
    private readonly Dictionary<long, HashSet<uint>> _unnamed = new();

    public HistoryController(MetersController meters, AppSettings settings, Func<int, string> mapName, BossFilter bossFilter,
        MeterDisplayOptions display)
    {
        _meters = meters;
        _settings = settings;
        _mapName = mapName;
        View = new HistoryViewModel(Load, "DpsBrush", "HealBrush", "TakenBrush", bossFilter, display);
        _meters.EncounterEnded += OnEncounterEnded;
        _meters.ActorNamed += OnActorNamed;
        _meters.IdsReset += OnIdsReset;
        try
        {
            _store = new HistoryStore(AppPaths.HistoryDatabase);
            if (_store.BackfillError is { } backfill) Log.Warning(backfill, "Historique : remplissage des anciens combats échoué");
            if (_store.BackfillSkipped > 0)
                Log.Information("Historique : {Count} combats illisibles ignorés au remplissage", _store.BackfillSkipped);
            var now = DateTimeOffset.UtcNow;
            var removed = _store.Purge(now, Options());
            if (removed > 0) Log.Information("Historique : {Count} combats purgés", removed);
            View.SetItems(_store.List(1000), now.LocalDateTime);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Historique indisponible");
            View.ErrorMessage = UiStrings.Format(nameof(UiStrings.HistoryUnavailable), ex.Message);
            _store?.Dispose();
            _store = null;
        }
    }

    public HistoryViewModel View { get; }

    /// <summary>Levé après chaque fin de combat ; l'id vaut null si le combat n'est pas enregistré ou si l'historique est indisponible.</summary>
    public event Action<EncounterSnapshot, long?>? EncounterSaved;

    private HistoryOptions Options() => new()
    {
        RetentionDays = _settings.History.RetentionDays,
        MaxEncounters = _settings.History.MaxEncounters,
        MinDurationMs = _settings.History.MinDurationSeconds * 1000L,
    };

    private EncounterSnapshot? Load(long id)
    {
        try
        {
            var snapshot = _store?.Load(id);
            return snapshot is null ? null : _meters.Data.Rename(snapshot);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Lecture d'un combat de l'historique impossible");
            return null;
        }
    }

    private void OnEncounterEnded(EncounterSnapshot snapshot)
    {
        long? id = null;
        if (_store is not null)
        {
            try
            {
                if (HistoryStore.ShouldSave(snapshot, Options()))
                {
                    var zone = _mapName(snapshot.MapId);
                    id = _store.Save(snapshot, zone);
                    var unnamed = SnapshotNames.UnnamedIds(snapshot);
                    if (unnamed.Count > 0) _unnamed[id.Value] = unnamed;
                    View.Insert(EncounterSummary.From(id.Value, snapshot, zone), DateTime.Now);
                    Log.Information("Combat enregistré : {Title}", snapshot.Title);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Enregistrement du combat impossible");
            }
        }
        try
        {
            EncounterSaved?.Invoke(snapshot, id);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Abonné de fin de combat en erreur");
        }
    }

    /// <summary>
    /// Pseudo arrivé après coup : complète les combats enregistrés depuis la dernière réattribution des ids. Le
    /// snapshot brut est relu, renommé puis réécrit (les noms de skills restent ré-résolus à la lecture).
    /// </summary>
    private void OnActorNamed(uint actorId, string name, GameClass cls)
    {
        if (_store is null || _unnamed.Count == 0) return;
        try
        {
            var count = 0;
            foreach (var (id, ids) in _unnamed.Where(kv => kv.Value.Contains(actorId)).ToList())
            {
                ids.Remove(actorId);
                if (ids.Count == 0) _unnamed.Remove(id);
                if (_store.Load(id) is not { } raw) continue;
                var renamed = SnapshotNames.Rename(raw, actorId, name, cls);
                if (ReferenceEquals(renamed, raw)) continue;
                _store.UpdateSnapshot(id, renamed);
                View.ReloadIfOpen(id);
                count++;
            }
            if (count > 0) Log.Information("Historique : pseudo complété dans {Count} combat(s)", count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Historique : pseudo non complété");
        }
    }

    // Ids réattribués : un pseudo annoncé désormais ne concerne plus les combats déjà enregistrés.
    private void OnIdsReset() => _unnamed.Clear();

    public void Dispose()
    {
        _meters.EncounterEnded -= OnEncounterEnded;
        _meters.ActorNamed -= OnActorNamed;
        _meters.IdsReset -= OnIdsReset;
        _store?.Dispose();
    }
}
