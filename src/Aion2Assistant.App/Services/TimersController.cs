using System.Windows.Threading;
using Aion2Assistant.App.ViewModels;
using Aion2Assistant.Core;
using Aion2Assistant.Core.Settings;
using Aion2Assistant.Timers;
using Aion2Assistant.Timers.Notifications;
using Serilog;

namespace Aion2Assistant.App.Services;

/// <summary>Chaque seconde : calcule les états, rafraîchit l'onglet, émet les alertes et mémorise celles déjà émises.</summary>
internal sealed class TimersController
{
    private readonly IClock _clock;
    private readonly TimerEngine _engine;
    private readonly TimerNotifier _notifier;
    private readonly TimersViewModel _view;
    private readonly AppSettings _settings;
    private readonly SettingsSaver _saver;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    public TimersController(IClock clock, TimerEngine engine, TimerNotifier notifier, TimersViewModel view,
        AppSettings settings, SettingsSaver saver)
    {
        _clock = clock;
        _engine = engine;
        _notifier = notifier;
        _view = view;
        _settings = settings;
        _saver = saver;
        _timer.Tick += (_, _) => Tick();
    }

    public event Action<TimerAlert>? AlertRaised;

    public void Start()
    {
        _notifier.Prune(_clock.UtcNow);
        Tick();
        _timer.Start();
    }

    private void Tick()
    {
        DateTimeOffset now;
        IReadOnlyList<TimerState> states;
        try
        {
            now = _clock.UtcNow;
            states = _engine.Compute();
            _view.Refresh(states, now);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erreur pendant le calcul des timers");
            return;
        }

        try
        {
            var alerts = _notifier.Evaluate(states, now);
            if (alerts.Count == 0) return;

            _settings.EmittedAlerts = _notifier.EmittedKeys.ToList();
            _saver.RequestSave();
            foreach (var alert in alerts)
            {
                try { AlertRaised?.Invoke(alert); }
                catch (Exception ex) { Log.Error(ex, "Erreur dans un gestionnaire d'alerte"); }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erreur pendant l'émission des alertes");
        }
    }
}
