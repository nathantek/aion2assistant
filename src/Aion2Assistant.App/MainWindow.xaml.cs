using System.Windows;
using System.Windows.Input;
using Aion2Assistant.App.Interop;
using Aion2Assistant.App.ViewModels;
using Aion2Assistant.Core.Hotkeys;

namespace Aion2Assistant.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => OverlayWindowHelper.MakeOverlay(this);
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel { CurrentPage: SettingsViewModel { CapturingRow: not null } settings }) return;
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape)
        {
            settings.CompleteCapture(null);
            return;
        }
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            return; // on attend la touche principale
        }

        var modifiers = HotkeyModifiers.None;
        var pressed = Keyboard.Modifiers;
        if (pressed.HasFlag(ModifierKeys.Control)) modifiers |= HotkeyModifiers.Ctrl;
        if (pressed.HasFlag(ModifierKeys.Alt)) modifiers |= HotkeyModifiers.Alt;
        if (pressed.HasFlag(ModifierKeys.Shift)) modifiers |= HotkeyModifiers.Shift;
        if (pressed.HasFlag(ModifierKeys.Windows)) modifiers |= HotkeyModifiers.Win;

        settings.CompleteCapture(new HotkeyGesture(modifiers, KeyInterop.VirtualKeyFromKey(key)));
    }

    private void OnTabBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Les boutons marquent l'événement comme traité : on n'arrive ici qu'en cliquant sur le fond de la barre.
        if (DataContext is MainViewModel { IsDocked: false } && e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }
}
