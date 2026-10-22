using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Aion2Assistant.App.Interop;
using Aion2Assistant.App.Localization;
using Aion2Assistant.App.Services;
using Aion2Assistant.App.Windows;
using Aion2Assistant.App.ViewModels;
using Aion2Assistant.Capture;
using Aion2Assistant.Combat;
using Aion2Assistant.Core;
using Aion2Assistant.Core.Audio;
using Aion2Assistant.Core.Hotkeys;
using Aion2Assistant.Core.Settings;
using Aion2Assistant.Core.Storage;
using Aion2Assistant.Timers;
using Aion2Assistant.Timers.Config;
using Aion2Assistant.Timers.Notifications;
using Serilog;

namespace Aion2Assistant.App;

/// <summary>Levée par le constructeur d'AppHost quand l'utilisateur refuse le CLUF.</summary>
internal sealed class EulaDeclinedException : Exception;

/// <summary>Point d'assemblage : crée les services, les relie, et pilote leur cycle de vie.</summary>
internal sealed class AppHost : IDisposable
{
    public AppSettings Settings { get; }
    public SettingsSaver Saver { get; }
    public MainViewModel Main { get; }
    public MainWindow Window { get; }
    public DockingController Docking { get; }
    public HotkeyService Hotkeys { get; }

    public static string ExclusiveFullscreenMessage => UiStrings.ExclusiveFullscreenMessage;

    public TopmostKeeper Topmost { get; }
    public TrayIconService Tray { get; }
    public IReadOnlyList<TimerDefinition> TimerDefinitions { get; }
    public TimersController TimersCtl { get; }
    public SoundService Sound { get; }
    public NotificationService Notifications { get; }
    public CaptureController Capture { get; }
    public BossFilter BossFilter { get; }
    public MeterDisplayOptions MeterDisplay { get; }
    public MetersController Meters { get; }
    private MiniBarController _miniBar = null!;
    public HistoryController History { get; }
    public RecordController Records { get; }
    public FieldBossController FieldBosses { get; }
    public UpdateController Updates { get; }
    private SettingsViewModel _settingsPage = null!;
    private IntPtr _foregroundBeforeCapture;

    /// <param name="store">Magasin des réglages, déjà lus par <see cref="App.Main"/> (la langue fixe la culture avant toute UI).</param>
    public AppHost(SettingsStore store, LoadResult<AppSettings> loaded)
    {
        if (loaded.Error is not null)
            Log.Warning("Réglages : {Error} (source utilisée : {Source})", loaded.Error, loaded.Source);
        Settings = loaded.Value;
        Saver = new SettingsSaver(store, Settings);
        ThemeManager.Initialize(Path.Combine(AppContext.BaseDirectory, "themes"));
        ThemeManager.Apply(Settings);
        // Après les réglages et le thème (la fenêtre utilise les brosses), avant toute fenêtre de l'overlay.
        if (!EnsureEulaAccepted()) throw new EulaDeclinedException();
        BossFilter = new BossFilter(Settings.Combat, Saver.RequestSave);
        MeterDisplay = new MeterDisplayOptions(Settings.Combat, Saver.RequestSave);

        Main = new MainViewModel(Settings, Saver.RequestSave);
        var timersConfig = TimersConfigLoader.Load(AppPaths.TimersFile);
        foreach (var error in timersConfig.Errors) Log.Warning("timers.json : {Error}", error);
        TimerDefinitions = timersConfig.Config.Definitions;
        var clock = new SystemClock();
        FieldBosses = new FieldBossController(FieldBossController.LoadNames(Path.Combine(AppContext.BaseDirectory, "data")));
        var engine = new TimerEngine(clock, timersConfig.Config.ServerTime, TimerDefinitions, FieldBosses.Tracker);
        var notifier = new TimerNotifier(Settings.EmittedAlerts, Settings.GetTimer);
        var timersPage = new TimersViewModel(Settings, Saver.RequestSave);
        Main.Pages[OverlayTab.Timers] = timersPage;
        TimersCtl = new TimersController(clock, engine, notifier, timersPage, Settings, Saver);
        Main.Pages[OverlayTab.Dps] = new PlaceholderViewModel(UiStrings.TabDps, UiStrings.PlaceholderLoading, "", "DpsBrush");
        Main.Pages[OverlayTab.Taken] = new PlaceholderViewModel(UiStrings.TabTaken, UiStrings.PlaceholderLoading, "", "TakenBrush");
        Main.Pages[OverlayTab.Heal] = new PlaceholderViewModel(UiStrings.TabHeal, UiStrings.PlaceholderLoading, "", "HealBrush");
        Main.Pages[OverlayTab.History] = new PlaceholderViewModel(UiStrings.TabHistory, UiStrings.PlaceholderLoading, "", "TextBrush");

        Window = new MainWindow { DataContext = Main };
        Docking = new DockingController(Window, Main, Settings, Saver);
        Docking.Apply();

        Hotkeys = new HotkeyService(Window);
        Hotkeys.Pressed += HandleHotkey;
        Hotkeys.RegisterAll(Settings.Hotkeys);

        Topmost = new TopmostKeeper(Window, Docking);
        Topmost.ExclusiveFullscreenDetected += OnExclusiveFullscreen;

        Sound = new SoundService(AppPaths.SoundsDirectory);
        Notifications = new NotificationService(Window, Main);
        TimersCtl.AlertRaised += OnTimerAlert;
        Updates = new UpdateController(Settings, Main, Notifications, Window.Dispatcher);

        _settingsPage = new SettingsViewModel(
            Settings, Main, Saver.RequestSave, sound => Sound.Play(sound, Settings.Volume), Hotkeys, TimerDefinitions, SetKeyboardCapture, Docking.ResetPosition,
            Updates, () => new EulaWindow(readOnly: true) { Owner = Window }.ShowDialog(), MeterDisplay, RequestRelaunch);
        Main.Pages[OverlayTab.Settings] = _settingsPage;
        Window.Deactivated += (_, _) => _settingsPage.CancelCapture();
        Window.IsVisibleChanged += (_, _) => { if (!Window.IsVisible) _settingsPage.CancelCapture(); };

        Capture = new CaptureController();
        FieldBosses.Attach(Capture);
        Main.Capture = Capture.View;
        _settingsPage.Capture = Capture.View;
        var npcapInstalled = NpcapProbe.IsInstalled();
        if (!npcapInstalled)
        {
            var npcap = UiStrings.NpcapMissing;
            Main.Pages[OverlayTab.Dps] = new PlaceholderViewModel(UiStrings.TabDps, npcap, "", "DpsBrush");
            Main.Pages[OverlayTab.Taken] = new PlaceholderViewModel(UiStrings.TabTaken, npcap, "", "TakenBrush");
            Main.Pages[OverlayTab.Heal] = new PlaceholderViewModel(UiStrings.TabHeal, npcap, "", "HealBrush");
            Main.Pages[OverlayTab.History] = new PlaceholderViewModel(UiStrings.TabHistory, npcap, "", "TextBrush");
        }

        Meters = new MetersController(Capture, Settings, Main, Window, "DpsBrush", "HealBrush", "TakenBrush", BossFilter, MeterDisplay);
        Meters.EncounterEnded += snapshot => Log.Information("Combat terminé : {Title} — {Outcome} — {Duration} — {GroupDps}/s",
            snapshot.Title, snapshot.Outcome, MeterFormat.Duration(snapshot.DurationMs), MeterFormat.Compact(snapshot.Dps.PerSecond));
        if (npcapInstalled)
        {
            Main.Pages[OverlayTab.Dps] = Meters.Dps;
            Main.Pages[OverlayTab.Taken] = Meters.Taken;
            Main.Pages[OverlayTab.Heal] = Meters.Heal;
        }

        History = new HistoryController(Meters, Settings, MapNames.Load(Path.Combine(AppContext.BaseDirectory, "data")), BossFilter, MeterDisplay);
        if (npcapInstalled) Main.Pages[OverlayTab.History] = History.View;
        // Recherche de l'historique : focus clavier le temps de la saisie, rendu au jeu ensuite.
        History.View.SetKeyboardInput = SetKeyboardCapture;
        Window.Deactivated += (_, _) => History.View.EndTyping();
        Window.IsVisibleChanged += (_, _) => { if (!Window.IsVisible) History.View.EndTyping(); };
        Records = new RecordController(History, Notifications);

        // Barre réduite : lit l'instantané du suivi (les meters ne se mettent pas à jour fenêtre réduite).
        _miniBar = new MiniBarController(Main, npcapInstalled ? () => Meters.LastSnapshot : () => null,
            npcapInstalled ? () => Meters.LastBossSnapshot : () => null, timersPage, BossFilter);

        // État vide des meters live : résumé du dernier combat + lien vers l'historique.
        if (npcapInstalled)
        {
            var meters = new[] { Meters.Dps, Meters.Taken, Meters.Heal };
            void UpdateLastFight()
            {
                var text = History.View.Latest is { } s ? CombatShareFormatter.SummaryLine(s) : null;
                foreach (var m in meters) m.LastFightText = text;
            }
            foreach (var m in meters) m.OpenLastFight = () => { ShowTab(OverlayTab.History); History.View.OpenLatest(); };
            History.View.LatestChanged += UpdateLastFight;
            UpdateLastFight();
        }

        // Dernier : un échec de construction plus haut ne laisse pas d'icône fantôme dans la zone de notification.
        InstallTabSwitchProbe();

        Tray = new TrayIconService(
            toggleVisible: ToggleVisible,
            toggleDock: () => Main.ToggleDockCommand.Execute(null),
            resetPosition: Docking.ResetPosition,
            openSettings: () => ShowTab(OverlayTab.Settings),
            exit: () => Application.Current.Shutdown());
    }

    /// <summary>Relance demandée (changement de langue) : App.OnExit relance l'app après la fermeture.</summary>
    public bool RelaunchRequested { get; private set; }

    /// <summary>Fermeture normale de l'app puis relance ; une mise à jour prête s'installe au passage.</summary>
    private void RequestRelaunch()
    {
        Log.Information("Redémarrage demandé (langue : {Language})", Settings.Language);
        RelaunchRequested = true;
        Updates.PrepareRelaunch();
        Application.Current.Shutdown();
    }

    /// <summary>Montre le CLUF s'il n'a pas été accepté dans la version en vigueur ; false = l'utilisateur quitte.</summary>
    private bool EnsureEulaAccepted()
    {
        if (Settings.EulaAcceptedVersion == AppSettings.EulaVersion) return true;
        if (new EulaWindow(readOnly: false).ShowDialog() != true) return false;
        Settings.EulaAcceptedVersion = AppSettings.EulaVersion;
        Saver.Flush();
        return true;
    }

    private int _tabSwitches;

    /// <summary>Mesure la latence d'un changement d'onglet (jusqu'au rendu inactif) et la journalise.</summary>
    private void InstallTabSwitchProbe()
    {
        Main.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(MainViewModel.CurrentTab)) return;
            var tab = Main.CurrentTab;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, () =>
            {
                var ms = sw.ElapsedMilliseconds;
                if (ms > 100 || _tabSwitches < 5) Log.Information("Changement d'onglet {Tab} : {Ms} ms", tab, ms);
                _tabSwitches++;
            });
        };
    }

    /// <summary>Pendant la saisie d'un raccourci, l'overlay doit pouvoir prendre le focus clavier.</summary>
    private void SetKeyboardCapture(bool capturing)
    {
        if (capturing)
        {
            _foregroundBeforeCapture = NativeMethods.GetForegroundWindow();
            OverlayWindowHelper.SetNoActivate(Window, false);
            Window.Activate();
            return;
        }
        OverlayWindowHelper.SetNoActivate(Window, true);
        var previous = _foregroundBeforeCapture;
        _foregroundBeforeCapture = IntPtr.Zero;
        var own = new WindowInteropHelper(Window).Handle;
        if (previous != IntPtr.Zero && previous != own && NativeMethods.GetForegroundWindow() == own)
            NativeMethods.SetForegroundWindow(previous);
    }

    public void Start()
    {
        Window.Show();
        Docking.Apply();
        Topmost.Start();
        TimersCtl.Start();
        Capture.Start();
        Meters.Start();
        Updates.Start();
        Log.Information("Aion2Assistant démarré");
    }

    public void ShowTab(OverlayTab tab)
    {
        Main.SelectTabCommand.Execute(tab);
        EnsureVisible();
    }

    public void ToggleVisible()
    {
        if (Window.IsVisible)
        {
            Window.Hide();
            return;
        }
        Window.Show();
        Docking.Apply();
    }

    private void OnExclusiveFullscreen()
    {
        Log.Warning("Plein écran exclusif détecté");
        Main.WarningMessage = ExclusiveFullscreenMessage;
        Tray.ShowWarning(UiStrings.ExclusiveFullscreenDetected, ExclusiveFullscreenMessage);
        Notifications.Show(UiStrings.ExclusiveFullscreenTitle, UiStrings.ExclusiveFullscreenShort, Resource("AlertBrush"), "");
    }

    public void ShowUnexpectedError() =>
        Notifications.Show(UiStrings.UnexpectedErrorTitle, UiStrings.UnexpectedErrorMessage, Resource("AlertBrush"), "");

    private void OnTimerAlert(TimerAlert alert)
    {
        var timersOnScreen = Window.IsVisible && !Main.IsMinimized && Main.CurrentTab == OverlayTab.Timers;
        if (!timersOnScreen && !Topmost.ExclusiveFullscreenActive)
        {
            var message = alert.Kind == AlertKind.Soon
                ? UiStrings.Format(nameof(UiStrings.TimerAlertIn), CountdownFormatter.FormatHuman(alert.DueAt - DateTimeOffset.UtcNow))
                : UiStrings.TimerAlertNow;
            var accent = Resource(alert.Kind == AlertKind.Soon ? "AlertBrush" : "TimerBrush");
            Notifications.Show(alert.Name, message, accent, CategoryGlyphs.For(alert.Category));
        }
        var timer = Settings.GetTimer(alert.TimerId);
        if (timer.Sound) Sound.Play(SoundResolver.ForTimer(Settings, timer), Settings.Volume);
    }

    private void EnsureVisible()
    {
        if (Window.IsVisible) return;
        Window.Show();
        Docking.Apply();
    }

    private void HandleHotkey(HotkeyAction action)
    {
        switch (action)
        {
            case HotkeyAction.TabTimers: ShowTab(OverlayTab.Timers); break;
            case HotkeyAction.TabDps: ShowTab(OverlayTab.Dps); break;
            case HotkeyAction.TabTaken: ShowTab(OverlayTab.Taken); break;
            case HotkeyAction.TabHeal: ShowTab(OverlayTab.Heal); break;
            case HotkeyAction.TabHistory: ShowTab(OverlayTab.History); break;
            case HotkeyAction.PreviousTab: Main.PreviousTabCommand.Execute(null); EnsureVisible(); break;
            case HotkeyAction.NextTab: Main.NextTabCommand.Execute(null); EnsureVisible(); break;
            case HotkeyAction.ToggleVisible: ToggleVisible(); break;
            case HotkeyAction.ToggleDock: Main.ToggleDockCommand.Execute(null); break;
            case HotkeyAction.ToggleMinimize: Main.ToggleMinimizeCommand.Execute(null); break;
            case HotkeyAction.ScaleUp: Main.ScaleUpCommand.Execute(null); break;
            case HotkeyAction.ScaleDown: Main.ScaleDownCommand.Execute(null); break;
            case HotkeyAction.ResetEncounter:
                Meters.Reset();
                break;
        }
    }

    public void Dispose()
    {
        // Arrête les cycles ; l'installation éventuelle se fait après, en toute fin de fermeture (App.OnExit).
        Updates.Dispose();
        _miniBar.Dispose();
        Meters.Dispose();
        Records.Dispose();
        History.Dispose();
        FieldBosses.Dispose();
        Capture.Dispose();
        Topmost.Dispose();
        Tray.Dispose();
        Hotkeys.Dispose();
        Saver.Flush();
    }

    public static Brush Resource(string key) => (Brush)Application.Current.Resources[key];
}
