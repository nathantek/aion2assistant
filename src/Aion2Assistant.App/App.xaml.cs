using System.Windows;
using Serilog;
using Velopack;

namespace Aion2Assistant.App;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private AppHost? _host;

    /// <summary>
    /// Point d'entrée : Velopack traite ses hooks (install, mise à jour, désinstallation) avant toute UI.
    /// Pas d'installation automatique au lancement : un second lancement ne doit jamais redémarrer l'overlay en cours ;
    /// une version déjà téléchargée est reproposée par le bandeau.
    /// </summary>
    [STAThread]
    public static void Main()
    {
        VelopackApp.Build().SetAutoApplyOnStartup(false).Run();
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstance = new Mutex(initiallyOwned: true, "Aion2Assistant.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

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
            host = new AppHost();
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
            MessageBox.Show("Aion2Assistant n'a pas pu démarrer. Détails dans les logs.", "Aion2Assistant",
                MessageBoxButton.OK, MessageBoxImage.Error);
            try { host?.Dispose(); }
            catch (Exception disposeEx) { Log.Error(disposeEx, "Échec du nettoyage après l'erreur de démarrage"); }
            _host = null;
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        _host?.Updates.LogPendingApply();
        Log.CloseAndFlush();
        if (_host is not null) _singleInstance?.ReleaseMutex();
        _singleInstance?.Dispose();
        base.OnExit(e);
        // Toute dernière étape : l'outil de mise à jour attend la fin du processus (60 s au plus).
        try { _host?.Updates.ApplyOnExit(); }
        catch { /* logs déjà fermés : Velopack réessaiera au prochain lancement */ }
    }
}
