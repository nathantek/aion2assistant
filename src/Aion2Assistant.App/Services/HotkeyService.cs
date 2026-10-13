using System.Windows;
using System.Windows.Interop;
using Aion2Assistant.App.Interop;
using Aion2Assistant.App.Localization;
using Aion2Assistant.Core.Hotkeys;
using Serilog;

namespace Aion2Assistant.App.Services;

/// <summary>Raccourcis globaux via RegisterHotKey : ils marchent même quand le jeu a le focus.</summary>
internal sealed class HotkeyService : IDisposable
{
    private const int FirstId = 0xA000;

    private readonly IntPtr _hwnd;
    private readonly HwndSource _source;
    private readonly Dictionary<int, HotkeyAction> _registered = new();
    private readonly Dictionary<HotkeyAction, string> _failures = new();

    public HotkeyService(Window window)
    {
        _hwnd = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(_hwnd);
        _source.AddHook(WndProc);
    }

    public event Action<HotkeyAction>? Pressed;

    /// <summary>Raccourcis qui n'ont pas pu être enregistrés, avec la raison (affichée dans les Réglages).</summary>
    public IReadOnlyDictionary<HotkeyAction, string> Failures => _failures;

    public void RegisterAll(IReadOnlyDictionary<string, string> bindings)
    {
        UnregisterAll();
        var id = FirstId;
        foreach (var action in Enum.GetValues<HotkeyAction>())
        {
            if (!bindings.TryGetValue(action.ToString(), out var text) || !HotkeyGesture.TryParse(text, out var gesture))
            {
                _failures[action] = UiStrings.HotkeyInvalid;
                continue;
            }

            var modifiers = (uint)gesture.Modifiers | NativeMethods.MOD_NOREPEAT;
            if (NativeMethods.RegisterHotKey(_hwnd, id, modifiers, (uint)gesture.VirtualKey))
            {
                _registered[id] = action;
            }
            else
            {
                _failures[action] = UiStrings.HotkeyTakenByOtherProgram;
                Log.Warning("Raccourci {Gesture} ({Action}) déjà utilisé", gesture, action);
            }
            id++;
        }
    }

    public void UnregisterAll()
    {
        foreach (var id in _registered.Keys) NativeMethods.UnregisterHotKey(_hwnd, id);
        _registered.Clear();
        _failures.Clear();
    }

    public void Dispose()
    {
        UnregisterAll();
        _source.RemoveHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && _registered.TryGetValue(wParam.ToInt32(), out var action))
        {
            handled = true;
            Pressed?.Invoke(action);
        }
        return IntPtr.Zero;
    }
}
