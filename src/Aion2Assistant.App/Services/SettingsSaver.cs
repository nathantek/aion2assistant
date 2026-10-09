using System.Windows.Threading;
using Aion2Assistant.Core.Settings;
using Serilog;

namespace Aion2Assistant.App.Services;

/// <summary>Regroupe les sauvegardes de réglages : une seule écriture 500 ms après la dernière modification.</summary>
internal sealed class SettingsSaver
{
    private readonly SettingsStore _store;
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };

    public SettingsSaver(SettingsStore store, AppSettings settings)
    {
        _store = store;
        _settings = settings;
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            Flush();
        };
    }

    public void RequestSave()
    {
        _timer.Stop();
        _timer.Start();
    }

    public void Flush()
    {
        try
        {
            _store.Save(_settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning(ex, "Échec de la sauvegarde des réglages");
        }
    }
}
