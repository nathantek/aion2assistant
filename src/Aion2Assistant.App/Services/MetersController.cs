using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Aion2Assistant.App.ViewModels;
using Aion2Assistant.Combat;
using Aion2Assistant.Core.Events;
using Aion2Assistant.Core.Settings;
using Serilog;

namespace Aion2Assistant.App.Services;

/// <summary>Alimente le suivi de combat avec les lots de capture et rafraîchit les trois meters toutes les 250 ms (thread UI).</summary>
internal sealed class MetersController : IDisposable
{
    private readonly CaptureController _capture;
    private readonly MainViewModel _main;
    private readonly Window _window;
    private EncounterSnapshot? _lastSnapshot;
    private readonly AppSettings _settings;
    private readonly CombatOptions _options = new();
    private readonly EncounterTracker _tracker;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };

    public MetersController(CaptureController capture, AppSettings settings, MainViewModel main, Window window, string dps, string heal, string taken, BossFilter bossFilter)
    {
        _capture = capture;
        _main = main;
        _window = window;
        _settings = settings;
        Dps = new MeterViewModel(MeterKind.Dps, "DPS", dps, bossFilter) { Training = new TrainingControls(StartTraining, StopTraining) };
        Heal = new MeterViewModel(MeterKind.Heal, "Soins", heal);
        Taken = new MeterViewModel(MeterKind.Taken, "Dégâts reçus", taken);
        var data = GameData.Load(Path.Combine(AppContext.BaseDirectory, "data"));
        Log.Information("Données de jeu : {Skills} skills, {Npcs} PNJ", data.SkillCount, data.NpcCount);
        if (data.SkillCount == 0 && data.NpcCount == 0)
            Log.Warning("Aucun fichier de noms dans data/ : les codes s'afficheront");
        _tracker = new EncounterTracker(data, _options);
        _tracker.Faulted += OnFaulted;
        _tracker.EncounterEnded += snapshot => EncounterEnded?.Invoke(snapshot);
        _timer.Tick += (_, _) => Tick();
    }

    private bool _faultLogged;
    private int _suppressedFaults;
    private long _lastFaultWarnTicks;

    private void OnFaulted(Exception ex)
    {
        if (!_faultLogged)
        {
            _faultLogged = true;
            _lastFaultWarnTicks = Environment.TickCount64;
            Log.Error(ex, "Erreur dans le suivi de combat");
            return;
        }
        _suppressedFaults++;
        var now = Environment.TickCount64;
        if (now - _lastFaultWarnTicks < 30_000) return;
        _lastFaultWarnTicks = now;
        Log.Warning("Combat : {Count} erreurs supplémentaires", _suppressedFaults);
        _suppressedFaults = 0;
    }

    /// <summary>Dernier instantané calculé (rafraîchi toutes les 250 ms, même fenêtre réduite).</summary>
    public EncounterSnapshot? LastSnapshot => _lastSnapshot;

    public MeterViewModel Dps { get; }
    public MeterViewModel Heal { get; }
    public MeterViewModel Taken { get; }

    /// <summary>Fin d'un combat (consommé par l'historique).</summary>
    public event Action<EncounterSnapshot>? EncounterEnded;

    public void Start()
    {
        _capture.EventsReceived += OnEvents;
        _main.PropertyChanged += OnMainPropertyChanged;
        _timer.Start();
    }

    public void Reset()
    {
        try
        {
            _tracker.Reset();
            Refresh();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erreur pendant la réinitialisation du combat");
        }
    }

    /// <summary>Arme un essai chronométré (0 = libre) : il démarre au prochain coup.</summary>
    public void StartTraining(long durationMs)
    {
        try
        {
            _tracker.StartTraining(durationMs);
            Refresh();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erreur pendant l'armement de l'entraînement");
        }
    }

    /// <summary>Termine l'essai en cours ou désarme l'essai demandé.</summary>
    public void StopTraining()
    {
        try
        {
            _tracker.StopTraining(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            Refresh();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erreur pendant l'arrêt de l'entraînement");
        }
    }

    private void OnMainPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.CurrentTab)) return;
        var vm = VisibleMeter();
        if (vm is not null && _window.IsVisible && !_main.IsMinimized) vm.Update(_lastSnapshot);
    }

    private MeterViewModel? VisibleMeter() => _main.CurrentTab switch
    {
        OverlayTab.Dps => Dps,
        OverlayTab.Heal => Heal,
        OverlayTab.Taken => Taken,
        _ => null,
    };

    private void OnEvents(IReadOnlyList<GameEvent> batch)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            _tracker.Handle(batch);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erreur pendant le traitement d'un lot d'événements de combat");
        }
        if (sw.ElapsedMilliseconds > 100) Log.Warning("Lent : {Where} {Ms} ms ({Count} éléments)", "Meters.OnEvents", sw.ElapsedMilliseconds, batch.Count);
    }

    private void Tick()
    {
        try
        {
            Refresh();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erreur pendant la mise à jour des meters");
        }
    }

    private void Refresh()
    {
        var sw = Stopwatch.StartNew();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _options.IdleTimeoutMs = _settings.Combat.IdleTimeoutSeconds * 1000L;
        _options.IncludeAllPlayers = _settings.Combat.IncludeAllPlayers;
        _tracker.Tick(now);
        var snapshot = _tracker.Snapshot(now);
        _lastSnapshot = snapshot;
        // L'état de l'essai est relu à chaque rafraîchissement ; la durée d'un instantané actif suit l'horloge.
        Dps.ApplyTraining(new TrainingState(_tracker.TrainingArmed, _tracker.IsTraining,
            snapshot?.TrainingDurationMs ?? 0, snapshot?.DurationMs ?? 0));
        if (_window.IsVisible && !_main.IsMinimized) VisibleMeter()?.Update(snapshot);
        if (sw.ElapsedMilliseconds > 100)
            Log.Warning("Lent : {Where} {Ms} ms ({Count} éléments)", "Meters.Refresh", sw.ElapsedMilliseconds, snapshot?.Dps.Actors.Count ?? 0);
    }

    public void Dispose()
    {
        try
        {
            _tracker.Flush(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erreur pendant la clôture du combat en cours");
        }
        _capture.EventsReceived -= OnEvents;
        _main.PropertyChanged -= OnMainPropertyChanged;
        _timer.Stop();
    }
}
