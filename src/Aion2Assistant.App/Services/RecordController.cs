using Aion2Assistant.App.Localization;
using Aion2Assistant.App.ViewModels;
using Aion2Assistant.Combat;
using Aion2Assistant.Core.Storage;
using Serilog;

namespace Aion2Assistant.App.Services;

/// <summary>Suit les records personnels de DPS boss, les garde dans records.json et notifie les records battus (thread UI).</summary>
internal sealed class RecordController : IDisposable
{
    private readonly HistoryController _history;
    private readonly NotificationService _notifications;
    private readonly RecordFile _file;

    public RecordController(HistoryController history, NotificationService notifications)
    {
        _history = history;
        _notifications = notifications;
        _file = Load();
        try
        {
            _history.View.IsRecord = IsRecord;
            _history.View.RefreshRecords();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Rafraîchissement des records impossible");
        }
        _history.EncounterSaved += OnEncounterSaved;
    }

    /// <summary>Charge records.json ; toute erreur (fichier corrompu, listes nulles) est journalisée et donne un fichier vide.</summary>
    private static RecordFile Load()
    {
        try
        {
            var loaded = JsonFileStore.Load(AppPaths.Records, () => new RecordFile());
            if (loaded.Error is not null)
                Log.Warning("records.json : {Error} (source utilisée : {Source})", loaded.Error, loaded.Source);
            var file = loaded.Value ?? new RecordFile();
            file.Records ??= new();
            file.Records.RemoveAll(r => r is null);
            return file;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "records.json illisible, records ignorés");
            return new RecordFile();
        }
    }

    /// <summary>Vrai si ce combat de l'historique porte un record actuel.</summary>
    public bool IsRecord(long encounterId) => _file.Records.Any(r => r.EncounterId == encounterId);

    private void OnEncounterSaved(EncounterSnapshot snapshot, long? id)
    {
        try
        {
            var result = RecordBook.Evaluate(_file, snapshot, id);
            if (result is null) return;
            RecordBook.Apply(_file, result.Record);
            Log.Information("Nouveau record : {Boss} {Dps}/s", result.Record.Boss, result.Record.Dps);
            try
            {
                JsonFileStore.Save(AppPaths.Records, _file);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "records.json non enregistré");
            }
            _history.View.RefreshRecords();
            if (result.Previous is { } old)
            {
                var pct = old.Dps > 0 ? (int)Math.Round((result.Record.Dps / old.Dps - 1) * 100, MidpointRounding.AwayFromZero) : 0;
                _notifications.Show(UiStrings.RecordNewTitle,
                    UiStrings.Format(nameof(UiStrings.RecordNewMessage), result.Record.Boss, MeterFormat.Compact(result.Record.Dps),
                        MeterFormat.Compact(old.Dps), pct),
                    AppHost.Resource("AlertBrush"), "");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Évaluation du record impossible");
        }
    }

    public void Dispose() => _history.EncounterSaved -= OnEncounterSaved;
}
