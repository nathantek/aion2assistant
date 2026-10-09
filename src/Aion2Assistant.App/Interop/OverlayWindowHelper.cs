using System.Windows;
using System.Windows.Interop;
using static Aion2Assistant.App.Interop.NativeMethods;

namespace Aion2Assistant.App.Interop;

/// <summary>Rend une fenêtre WPF « overlay » : hors Alt+Tab, toujours au premier plan, cliquable sans prendre le focus.</summary>
internal static class OverlayWindowHelper
{
    public static void MakeOverlay(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        var style = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        style = (style | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE) & ~WS_EX_APPWINDOW;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(style));
        HwndSource.FromHwnd(hwnd)?.AddHook(NoActivateHook);
        ForceTopmost(window);
    }

    /// <summary>Désactive temporairement le « sans focus » pour pouvoir saisir au clavier (capture d'un raccourci).</summary>
    public static void SetNoActivate(Window window, bool enabled)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        var style = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        style = enabled ? style | WS_EX_NOACTIVATE : style & ~WS_EX_NOACTIVATE;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(style));
    }

    public static void ForceTopmost(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

        // Repasser au premier plan place l'overlay au-dessus de ses propres bulles d'aide et pop-ups :
        // on les remonte juste après pour qu'elles restent visibles.
        foreach (var source in PresentationSource.CurrentSources.OfType<HwndSource>())
        {
            if (source.IsDisposed || source.Handle == hwnd || source.RootVisual is not UIElement { IsVisible: true }) continue;
            SetWindowPos(source.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
    }

    private static IntPtr NoActivateHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WM_MOUSEACTIVATE) return IntPtr.Zero;
        var style = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        if ((style & WS_EX_NOACTIVATE) == 0) return IntPtr.Zero;
        handled = true;
        return new IntPtr(MA_NOACTIVATE);
    }
}
