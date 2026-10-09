using System.Windows;
using Serilog;

namespace Aion2Assistant.App;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private AppHost? _host;

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
        Log.CloseAndFlush();
        if (_host is not null) _singleInstance?.ReleaseMutex();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
