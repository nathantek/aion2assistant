using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using Aion2Assistant.App.Localization;
using Aion2Assistant.Core.Localization;
using Aion2Assistant.Core.Settings;
using Aion2Assistant.Core.Startup;
using Aion2Assistant.Core.Storage;
using Serilog;
using Velopack;

namespace Aion2Assistant.App;

public partial class App : Application
{
    private const string SingleInstanceName = "Aion2Assistant.SingleInstance";
    // Relance (changement de langue) : attente bornée de la fin de l'instance précédente.
    private static readonly TimeSpan RelaunchWait = TimeSpan.FromSeconds(30);

    private readonly Mutex _singleInstance;
    private readonly SettingsStore _settingsStore;
    private readonly LoadResult<AppSettings>? _settings;
    private readonly Exception? _settingsError;
    private AppHost? _host;

    private App(Mutex singleInstance, SettingsStore settingsStore, LoadResult<AppSettings>? settings, Exception? settingsError)
    {
        _singleInstance = singleInstance;
        _settingsStore = settingsStore;
        _settings = settings;
        _settingsError = settingsError;
    }

    /// <summary>
    /// Point d'entrée : Velopack traite ses hooks (install, mise à jour, désinstallation) avant toute UI.
    /// Pas d'installation automatique au lancement : un second lancement ne doit jamais redémarrer l'overlay en cours ;
    /// une version déjà téléchargée est reproposée par le bandeau.
    /// Les réglages sont lus ensuite, avant toute UI et toute ressource : leur langue fixe la culture du processus.
    /// Relance par l'app elle-même (<c>--wait-for-pid</c>) : on attend que l'ancienne instance ait quitté.
    /// </summary>
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().SetAutoApplyOnStartup(false).Run();

        var relaunched = RelaunchArguments.TryParseWaitPid(args, out var previousPid);
        if (relaunched) WaitForExit(previousPid, RelaunchWait);

        var singleInstance = new Mutex(initiallyOwned: false, SingleInstanceName);
        if (!TryAcquire(singleInstance, relaunched ? RelaunchWait : TimeSpan.Zero))
        {
            singleInstance.Dispose();
            return;
        }

        // Lecture avant AppCulture.Apply : au premier lancement, la langue suit encore celle de Windows.
        var store = new SettingsStore(AppPaths.SettingsFile);
        LoadResult<AppSettings>? settings = null;
        Exception? settingsError = null;
        try { settings = store.Load(); }
        catch (Exception ex) { settingsError = ex; }
        ApplyCulture(settings?.Value.Language ?? AppLanguage.FromCulture(CultureInfo.CurrentUICulture));

        var app = new App(singleInstance, store, settings, settingsError);
        app.InitializeComponent();
        app.Run();
    }

    /// <summary>Prend le verrou d'instance unique ; faux si une autre instance le garde au-delà du délai.</summary>
    private static bool TryAcquire(Mutex mutex, TimeSpan timeout)
    {
        try { return mutex.WaitOne(timeout); }
        catch (AbandonedMutexException) { return true; } // instance précédente terminée sans le relâcher : il est à nous
    }

    /// <summary>Attend la fin de l'instance qui a demandé la relance (pid réutilisé par un autre programme : pas d'attente).</summary>
    private static void WaitForExit(int pid, TimeSpan timeout)
    {
        try
        {
            using var previous = Process.GetProcessById(pid);
            using var current = Process.GetCurrentProcess();
            if (!string.Equals(previous.ProcessName, current.ProcessName, StringComparison.OrdinalIgnoreCase)) return;
            previous.WaitForExit(timeout);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Déjà terminée ou inaccessible : rien à attendre.
        }
    }

    /// <summary>Relance l'exe courant, qui attendra la fin de ce processus avant de prendre le verrou.</summary>
    private static void Relaunch()
    {
        if (Environment.ProcessPath is not { } exe) return;
        var start = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = AppContext.BaseDirectory };
        foreach (var arg in RelaunchArguments.Build(Environment.ProcessId)) start.ArgumentList.Add(arg);
        Process.Start(start)?.Dispose();
    }

    /// <summary>Culture de l'app (code et liaisons WPF) ; une seule fois, avant la création du moindre élément.</summary>
    private static void ApplyCulture(string language)
    {
        AppCulture.Apply(language);
        // Les liaisons WPF (StringFormat, convertisseurs) suivent la propriété Language, en-US par défaut ;
        // les éléments de texte (Run…) l'héritent de leur parent.
        var xmlLanguage = XmlLanguage.GetLanguage(AppCulture.Current.IetfLanguageTag);
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(xmlLanguage));
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        Logging.Configure();
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "Exception non gérée sur le thread UI");
            args.Handled = true;
            try { _host?.ShowUnexpectedError(); }
            catch (Exception ex) { Log.Error(ex, "Impossible d'afficher la notification d'erreur"); }
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error(args.Exception, "Exception de tâche non observée");
            args.SetObserved();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Fatal(args.ExceptionObject as Exception, "Exception fatale");

        base.OnStartup(e);
        AppHost? host = null;
        try
        {
            if (_settings is null) throw new InvalidOperationException("Lecture des réglages impossible", _settingsError);
            Log.Information("Langue : {Language}", AppCulture.Language);
            host = new AppHost(_settingsStore, _settings);
            host.Start();
            _host = host;
        }
        catch (EulaDeclinedException)
        {
            Log.Information("CLUF refusé : arrêt");
            Shutdown();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Échec du démarrage");
            MessageBox.Show(UiStrings.StartupFailed, "Aion2Assistant",
                MessageBoxButton.OK, MessageBoxImage.Error);
            try { host?.Dispose(); }
            catch (Exception disposeEx) { Log.Error(disposeEx, "Échec du nettoyage après l'erreur de démarrage"); }
            _host = null;
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        var relaunch = _host?.RelaunchRequested == true;
        _host?.Dispose();
        _host?.Updates.LogPendingApply();
        if (relaunch) Log.Information("Relance de l'app après la fermeture");
        Log.CloseAndFlush();
        // Verrou pris par cette instance dans Main (même thread) : toujours relâché, même après un échec de démarrage.
        try { _singleInstance.ReleaseMutex(); }
        catch (ApplicationException) { /* déjà relâché */ }
        _singleInstance.Dispose();
        base.OnExit(e);
        // Toute dernière étape : l'outil de mise à jour attend la fin du processus (60 s au plus).
        var updaterRelaunches = false;
        try { updaterRelaunches = _host?.Updates.ApplyOnExit() == true; }
        catch { /* logs déjà fermés : Velopack réessaiera au prochain lancement */ }
        // Changement de langue sans mise à jour à installer : l'app se relance elle-même.
        if (relaunch && !updaterRelaunches)
        {
            try { Relaunch(); }
            catch { /* logs déjà fermés : l'utilisateur relancera l'app */ }
        }
    }
}
