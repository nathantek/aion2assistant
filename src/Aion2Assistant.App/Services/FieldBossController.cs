using System.Globalization;
using System.Text.Json;
using Aion2Assistant.Core.Events;
using Aion2Assistant.Core.Storage;
using Aion2Assistant.Timers.FieldBosses;
using Serilog;

namespace Aion2Assistant.App.Services;

/// <summary>Alimente le suivi des boss de terrain avec les listes réseau et conserve son état dans bosses.json (thread UI).</summary>
internal sealed class FieldBossController : IDisposable
{
    private CaptureController? _capture;

    public FieldBossController(Func<int, int, string> bossName)
    {
        var loaded = JsonFileStore.Load(AppPaths.Bosses, () => new FieldBossFile());
        if (loaded.Error is not null)
            Log.Warning("bosses.json : {Error} (source utilisée : {Source})", loaded.Error, loaded.Source);
        Tracker = new FieldBossTracker(loaded.Value, bossName);
    }

    public FieldBossTracker Tracker { get; }

    /// <summary>Noms des boss : <c>field_bosses.json</c> (facultatif, clé = carte × 100 + rang), sinon « Boss {rang} — carte {carte} ».</summary>
    public static Func<int, int, string> LoadNames(string directory)
    {
        var table = new Dictionary<int, string>();
        try
        {
            var path = Path.Combine(directory, "field_bosses.json");
            if (File.Exists(path))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var p in doc.RootElement.EnumerateObject())
                    {
                        if (p.Value.ValueKind == JsonValueKind.String
                            && int.TryParse(p.Name, NumberStyles.None, CultureInfo.InvariantCulture, out var key))
                            table[key] = p.Value.GetString() ?? "";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "field_bosses.json illisible");
            table.Clear();
        }
        return (map, slot) => table.TryGetValue(slot, out var name) ? name : $"Boss {slot - map * 100} — carte {map}";
    }

    public void Attach(CaptureController capture)
    {
        _capture = capture;
        capture.EventsReceived += OnEvents;
    }

    private void OnEvents(IReadOnlyList<GameEvent> batch)
    {
        try
        {
            foreach (var e in batch)
            {
                switch (e)
                {
                    case SelfIdentifiedEvent self when self.ServerId != 0 && self.ServerId != Tracker.ServerId:
                        Tracker.ServerId = self.ServerId;
                        Log.Information("Boss de terrain : serveur {ServerId}", self.ServerId);
                        Save();
                        break;
                    case FieldBossListEvent list:
                        if (Tracker.Apply(list)) Save();
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erreur pendant le traitement des boss de terrain");
        }
    }

    private void Save()
    {
        try
        {
            JsonFileStore.Save(AppPaths.Bosses, Tracker.State);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Échec de la sauvegarde de bosses.json");
        }
    }

    public void Dispose()
    {
        if (_capture is not null) _capture.EventsReceived -= OnEvents;
    }
}
