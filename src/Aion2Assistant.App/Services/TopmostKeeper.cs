using System.Windows;
using System.Windows.Threading;
using Aion2Assistant.App.Interop;
using Serilog;
using static Aion2Assistant.App.Interop.NativeMethods;

namespace Aion2Assistant.App.Services;

/// <summary>
/// Réaffirme le premier plan toutes les 2 s et à chaque fois que le jeu reprend le focus
/// (certains jeux repassent devant au rechargement). Signale aussi le plein écran exclusif, sous lequel aucun overlay n'est visible.
/// </summary>
internal sealed class TopmostKeeper : IDisposable
{
    private readonly Window _window;
    private readonly DockingController _docking;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly WinEventDelegate _foregroundCallback;
    private readonly IntPtr _hook;
    private bool _fullscreenReported;

    public TopmostKeeper(Window window, DockingController docking)
    {
        _window = window;
        _docking = docking;
        _timer.Tick += (_, _) => Tick();
        _foregroundCallback = OnForegroundChanged; // gardé en champ : le GC ne doit pas collecter le délégué natif
        _hook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero,
            _foregroundCallback, 0, 0, WINEVENT_OUTOFCONTEXT);
        if (_hook == IntPtr.Zero)
            Log.Warning("SetWinEventHook a échoué : seul le minuteur de 2 s réaffirme le premier plan");
    }

    public event Action? ExclusiveFullscreenDetected;

    public bool ExclusiveFullscreenActive => _fullscreenReported;

    public void Start() => _timer.Start();

    public void Dispose()
    {
        _timer.Stop();
        if (_hook != IntPtr.Zero) UnhookWinEvent(_hook);
    }

    private void OnForegroundChanged(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        try
        {
            if (GameWindowLocator.IsGameWindow(hwnd)) Tick();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erreur dans le suivi du premier plan");
        }
    }

    private void Tick()
    {
        if (_window.IsVisible && !_fullscreenReported) OverlayWindowHelper.ForceTopmost(_window);
        _docking.Reanchor();
        CheckExclusiveFullscreen();
    }

    private void CheckExclusiveFullscreen()
    {
        var foreground = GetForegroundWindow();
        if (!GameWindowLocator.IsGameWindow(foreground)) return;

        var exclusive = SHQueryUserNotificationState(out var state) == 0 && state == QUNS_RUNNING_D3D_FULL_SCREEN;
        if (exclusive && !_fullscreenReported)
        {
            _fullscreenReported = true;
            ExclusiveFullscreenDetected?.Invoke();
        }
        else if (!exclusive)
        {
            _fullscreenReported = false;
        }
    }
}
