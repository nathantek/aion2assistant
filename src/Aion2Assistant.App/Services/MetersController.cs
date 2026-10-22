using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Aion2Assistant.App.Localization;
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
    private DisplayedEncounter _displayed;
    private readonly BossFilter _bossFilter;
    private readonly AppSettings _settings;
    private readonly CombatOptions _options = new();
    private readonly EncounterTracker _tracker;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };

    public MetersController(CaptureController capture, AppSettings settings, MainViewModel main, Window window, string dps, string heal, string taken, BossFilter bossFilter,
        MeterDisplayOptions display)
    {
        _capture = capture;
        _main = main;
        _window = window;
        _settings = settings;
        _bossFilter = bossFilter;
        Dps = new MeterViewModel(MeterKind.Dps, UiStrings.TabDps, dps, bossFilter, display) { Training = new TrainingControls(StartTraining, StopTraining) };
        Heal = new MeterViewModel(MeterKind.Heal, UiStrings.TabHeal, heal, display: display);
        Taken = new MeterViewModel(MeterKind.Taken, UiStrings.TabTaken, taken, display: display);
        var data = GameData.Load(Path.Combine(AppContext.BaseDirectory, "data"));
        Log.Information("Données de jeu : {Skills} skills, {Npcs} PNJ", data.SkillCount, data.NpcCount);
        if (data.SkillCount == 0 && data.NpcCount == 0)
            Log.Warning("Aucun fichier de noms dans data/ : les codes s'afficheront");
        Data = data;
        _tracker = new EncounterTracker(data, _options);
        _tracker.Faulted += OnFaulted;
        _tracker.DebugMessage += message => Log.Debug("Combat : {Message}", message);
        _tracker.InfoMessage += message => Log.Information("{Message}", message);
        _tracker.EncounterEnded += snapshot => EncounterEnded?.Invoke(snapshot);
        _tracker.ActorNamed += (id, name, cls) => ActorNamed?.Invoke(id, name, cls);
        _tracker.IdsReset += () => IdsReset?.Invoke();
        _timer.Tick += (_, _) => Tick();
        // Bascule [Boss | Tout] : le combat affiché change aussitôt (retour au dernier boss en mode [Boss]).
        bossFilter.PropertyChanged += OnBossFilterChanged;
    }

    /// <summary>Noms de jeu chargés (aussi utilisés pour ré-résoudre les noms de l'historique).</summary>
    public GameData Data { get; }

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

    /// <summary>Dernier combat de boss du suivi (mode [Boss] des meters et de la barre réduite).</summary>
    public EncounterSnapshot? LastBossSnapshot => _tracker.LastBossSnapshot;

    public MeterViewModel Dps { get; }
    public MeterViewModel Heal { get; }
    public MeterViewModel Taken { get; }

    /// <summary>Fin d'un combat (consommé par l'historique).</summary>
    public event Action<EncounterSnapshot>? EncounterEnded;

    /// <summary>Premier vrai pseudo d'un id (relais du tracker ; consommé par l'historique).</summary>
    public event Action<uint, string, GameClass>? ActorNamed;

    /// <summary>Ids réattribués (relais du tracker ; consommé par l'historique).</summary>
    public event Action? IdsReset;

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
        if (_window.IsVisible && !_main.IsMinimized) UpdateVisible(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    /// <summary>Met à jour le meter visible avec le combat choisi (dernier boss en mode [Boss], voir EncounterDisplay).</summary>
    private void UpdateVisible(long nowMs)
    {
        if (VisibleMeter() is not { } vm) return;
        vm.PastBossText = PastBossText(_displayed, nowMs);
        vm.Update(_displayed.Snapshot);
    }

    /// <summary>« Dernier boss · il y a 2 min » quand on garde un boss passé ; null sinon. Minutes entières : le texte change chaque minute.</summary>
    private static string? PastBossText(DisplayedEncounter shown, long nowMs)
    {
        if (!shown.IsPastBoss || shown.Snapshot is not { } s) return null;
        var minutes = EncounterDisplay.MinutesSinceEnd(s, nowMs);
        return minutes < 1 ? UiStrings.MeterLastBossJustNow : UiStrings.Format(nameof(UiStrings.MeterLastBossAgo), minutes);
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

    private void OnBossFilterChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => Tick();

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
        _displayed = EncounterDisplay.Select(snapshot, _tracker.LastBossSnapshot, _bossFilter.BossOnly);
        // L'état de l'essai est relu à chaque rafraîchissement ; la durée d'un instantané actif suit l'horloge.
        Dps.ApplyTraining(new TrainingState(_tracker.TrainingArmed, _tracker.IsTraining,
            snapshot?.TrainingDurationMs ?? 0, snapshot?.DurationMs ?? 0));
        if (_window.IsVisible && !_main.IsMinimized) UpdateVisible(now);
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
        _bossFilter.PropertyChanged -= OnBossFilterChanged;
        _timer.Stop();
    }
}
