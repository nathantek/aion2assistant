using System.Windows;
using System.Windows.Threading;
using Aion2Assistant.App.Localization;
using Aion2Assistant.App.ViewModels;
using Aion2Assistant.Core.Localization;
using Aion2Assistant.Core.Settings;
using Aion2Assistant.Core.Updates;
using Serilog;

namespace Aion2Assistant.App.Services;

/// <summary>
/// Mises à jour de l'application. Installée par Setup.exe : orchestrateur Velopack (téléchargement en arrière-plan,
/// bandeau « Version X prête », jamais de redémarrage sans clic). Sinon : ancienne vérification qui guide vers Setup.exe.
/// </summary>
internal sealed class UpdateController : IDisposable
{
    private readonly AppSettings _settings;
    private readonly MainViewModel _main;
    private readonly NotificationService _notifications;
    private readonly Dispatcher _dispatcher;
    private readonly VelopackUpdateBackend _backend;
    private readonly UpdateOrchestrator? _orchestrator;
    private readonly UpdateService? _legacy;
    private readonly RepeatedFailureFilter _failures = new();
    private AppVersion? _notifiedVersion;
    private bool _disposed;

    public UpdateController(AppSettings settings, MainViewModel main, NotificationService notifications, Dispatcher dispatcher)
    {
        _settings = settings;
        _main = main;
        _notifications = notifications;
        _dispatcher = dispatcher;
        CurrentVersionText = UpdateService.ReadVersionText();
        _backend = new VelopackUpdateBackend();

        if (_backend.IsInstalled && AppVersion.TryParse(CurrentVersionText, out var current))
        {
            // Lecture du paquet téléchargé (disque) hors du fil de l'interface ; le résultat revient par StateChanged.
            _orchestrator = new UpdateOrchestrator(_backend, new TimerUpdateScheduler(), current, read => Task.Run(read));
            // Événements possiblement levés depuis le pool, voire sous le verrou de l'orchestrateur : jamais d'appel bloquant.
            _orchestrator.StateChanged += () => _dispatcher.BeginInvoke(Refresh);
            _orchestrator.Failed += OnFailed;
            _orchestrator.PendingPackageFound += version => Log.Information(
                "Mise à jour {Version} téléchargée mais pas encore installée (installation précédente interrompue ?)", version);
            _main.RestartUpdate = RequestRestart;
            _main.DeferUpdate = Defer;
        }
        else
        {
            if (_backend.IsInstalled) Log.Warning("Mises à jour : version courante {Version} illisible", CurrentVersionText);
            _legacy = new UpdateService(() => _settings.CheckForUpdates);
            _legacy.Changed += () => Changed?.Invoke();
            _legacy.UpdateFound += (version, _) => _notifications.Show(
                UiStrings.Format(nameof(UiStrings.UpdateAvailableTitle), version), UiStrings.UpdateAvailableMessage, AppHost.Resource("TimerBrush"), "");
        }
    }

    /// <summary>Version courante affichée (sans le suffixe +hash éventuel).</summary>
    public string CurrentVersionText { get; }

    /// <summary>Levé sur le thread UI quand l'état affiché change.</summary>
    public event Action? Changed;

    public string StatusText => UpdateStatusText.Describe(
        _settings.CheckForUpdates, _orchestrator is not null, _orchestrator?.State ?? UpdateState.Idle,
        _orchestrator?.Version, _backend.UpToDate, _legacy?.AvailableVersion);

    /// <summary>Hors installation : page de la version plus récente (où se trouve Setup.exe).</summary>
    public string? DownloadUrl => _settings.CheckForUpdates ? _legacy?.AvailableUrl : null;

    /// <summary>Une version est téléchargée et peut être installée par un redémarrage (réglage activé).</summary>
    public bool CanRestart => _settings.CheckForUpdates && _orchestrator?.State is UpdateState.Ready or UpdateState.Deferred;

    public void Start()
    {
        if (_orchestrator is not null) _orchestrator.Start(_settings.CheckForUpdates);
        else if (_settings.CheckForUpdates) _legacy?.Start();
    }

    /// <summary>Réglage « Mises à jour automatiques » : pris en compte immédiatement.</summary>
    public void SetEnabled(bool enabled)
    {
        if (_orchestrator is not null) _orchestrator.SetEnabled(enabled);
        else if (enabled) _legacy?.Start();
        Refresh();
    }

    /// <summary>« Redémarrer » : fermeture normale de l'app, la mise à jour s'installe puis relance l'app.</summary>
    public void RequestRestart()
    {
        if (!CanRestart) return;
        Log.Information("Redémarrage demandé pour installer la version {Version}", _orchestrator!.Version);
        _backend.RestartAfterApply = true;
        Application.Current.Shutdown();
    }

    /// <summary>
    /// Relance demandée pour une autre raison (langue) : une version téléchargée s'installe à la fermeture et
    /// Velopack relance l'app ; sinon l'app se relance elle-même (voir <see cref="ApplyOnExit"/>).
    /// </summary>
    public void PrepareRelaunch() => _backend.RestartAfterApply = true;

    /// <summary>« Plus tard » : bandeau masqué, installation à la fermeture.</summary>
    public void Defer()
    {
        if (_orchestrator is null) return;
        Log.Information("Mise à jour {Version} reportée à la fermeture", _orchestrator.Version);
        _orchestrator.Defer();
    }

    private void Refresh()
    {
        if (_disposed) return;
        if (_orchestrator is not null)
        {
            var state = _orchestrator.State;
            var version = _orchestrator.Version;
            // Réglage coupé : bandeau masqué (et rien ne sera installé).
            _main.UpdateBannerText = _settings.CheckForUpdates && state == UpdateState.Ready && version is not null
                ? Strings.Format(nameof(Strings.UpdateVersionReady), version) : null;
            // Vérification réussie : une prochaine erreur sera de nouveau signalée.
            if (state == UpdateState.Idle && _backend.UpToDate) _failures.Reset();
            if (_settings.CheckForUpdates && state == UpdateState.Ready && version is not null && !Equals(version, _notifiedVersion))
            {
                _notifiedVersion = version;
                _failures.Reset();
                _notifications.Show(UiStrings.Format(nameof(UiStrings.UpdateReadyTitle), version), UiStrings.UpdateReadyMessage,
                    AppHost.Resource("TimerBrush"), "");
            }
        }
        Changed?.Invoke();
    }

    private void OnFailed(Exception ex)
    {
        // Même erreur d'un cycle à l'autre (réseau coupé, GitHub injoignable) : un seul avertissement.
        if (_failures.IsNew(ex)) Log.Warning(ex, "Mise à jour automatique en échec");
        else Log.Debug("Mise à jour automatique toujours en échec : {Message}", ex.Message);
    }

    /// <summary>Arrête les cycles (sans réseau ensuite) ; l'installation éventuelle reste possible via <see cref="ApplyOnExit"/>.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _orchestrator?.StopScheduling();
    }

    /// <summary>
    /// Toute dernière étape de la fermeture (après les logs et le mutex) : l'outil de Velopack attend la fin
    /// du processus 60 s au plus. Installe la version téléchargée, avec relance si « Redémarrer » a été cliqué.
    /// </summary>
    /// <returns>Vrai si Velopack relancera l'app après l'installation.</returns>
    public bool ApplyOnExit()
    {
        if (_orchestrator is null) return false;
        // L'attente bornée de la lecture a déjà eu lieu dans LogPendingApply : pas de seconde attente.
        try { _orchestrator.ApplyOnExit(TimeSpan.Zero); }
        finally { _orchestrator.Dispose(); }
        return _backend.ApplyStarted && _backend.RestartAfterApply;
    }

    /// <summary>À journaliser avant la fermeture des logs : l'installation elle-même a lieu après.</summary>
    public void LogPendingApply()
    {
        // Fermeture juste après le lancement : la lecture du paquet peut ne pas être terminée (2 s au plus).
        if (_settings.CheckForUpdates) _orchestrator?.WaitForPendingRead(UpdateOrchestrator.ExitReadWait);
        if (_settings.CheckForUpdates && _orchestrator?.Version is { } version)
            Log.Information(_backend.RestartAfterApply
                ? "Installation de la version {Version} puis relance"
                : "Installation de la version {Version} à la fermeture", version);
    }
}
