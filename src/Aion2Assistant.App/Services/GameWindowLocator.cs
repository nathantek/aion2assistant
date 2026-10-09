using System.Diagnostics;
using Aion2Assistant.App.Interop;
using Serilog;

namespace Aion2Assistant.App.Services;

internal static class GameWindowLocator
{
    public static readonly string[] ProcessNames = ["AION2", "AION2-Win64-Shipping"];
    private static int? _lastPid;

    public static IntPtr FindGameWindow()
    {
        foreach (var name in ProcessNames)
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                using (process)
                {
                    if (process.MainWindowHandle != IntPtr.Zero)
                    {
                        if (_lastPid != process.Id)
                        {
                            _lastPid = process.Id;
                            Log.Information("Jeu détecté : {Name} (PID {Pid})", process.ProcessName, process.Id);
                        }
                        return process.MainWindowHandle;
                    }
                }
            }
        }
        if (_lastPid is not null)
        {
            _lastPid = null;
            Log.Information("Jeu non détecté");
        }
        return IntPtr.Zero;
    }

    public static bool IsGameWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return ProcessNames.Contains(process.ProcessName, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return false; // processus terminé entre-temps
        }
    }
}
