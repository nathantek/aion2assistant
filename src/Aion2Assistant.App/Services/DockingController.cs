using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Shell;
using System.Windows.Threading;
using Aion2Assistant.App.Interop;
using Aion2Assistant.App.ViewModels;
using Aion2Assistant.Core.Settings;

namespace Aion2Assistant.App.Services;

/// <summary>
/// Un seul rectangle mémorisé (<see cref="AppSettings.FreeBounds"/>) sert aux deux modes.
/// Libre : déplaçable et redimensionnable, position et taille mémorisées.
/// Ancré : verrouillé là où il a été laissé (ni déplaçable ni redimensionnable). Sans rectangle mémorisé,
/// taille fixe (multipliée par l'échelle), collé en haut à gauche de l'écran du jeu.
/// Réduit : seule la barre d'onglets reste visible.
/// </summary>
internal sealed class DockingController
{
    private const double DockedWidth = 320;
    private const double DockedHeight = 260;
    private const double BarHeight = 38;
    private const double MinFreeWidth = 220;
    private const double MinFreeHeight = 140;
    private const int MarginPx = 8;

    private readonly Window _window;
    private readonly MainViewModel _vm;
    private readonly AppSettings _settings;
    private readonly SettingsSaver _saver;
    private bool _applying;
    private (int X, int Y)? _lastAnchor;
    private bool _moveToDefault;
    private double _lastScale;

    public DockingController(Window window, MainViewModel vm, AppSettings settings, SettingsSaver saver)
    {
        _window = window;
        _vm = vm;
        _settings = settings;
        _saver = saver;
        _lastScale = vm.UiScale;
        vm.PropertyChanged += OnViewModelChanged;
        window.LocationChanged += (_, _) => RememberFreeBounds();
        window.SizeChanged += (_, _) => RememberFreeBounds();
    }

    public void Apply()
    {
        _applying = true;
        try
        {
            var scale = _vm.UiScale;
            var free = !_vm.IsDocked;
            var resizable = free && !_vm.IsMinimized;

            _window.ResizeMode = resizable ? ResizeMode.CanResize : ResizeMode.NoResize;
            WindowChrome.SetWindowChrome(_window, new WindowChrome
            {
                CaptionHeight = 0,
                GlassFrameThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0),
                UseAeroCaptionButtons = false,
                ResizeBorderThickness = resizable ? new Thickness(6) : new Thickness(0),
            });
            _window.MinWidth = resizable ? MinFreeWidth * scale : 0;
            _window.MinHeight = resizable ? MinFreeHeight * scale : 0;

            if (_settings.FreeBounds is { } saved)
            {
                _window.Left = saved.Left;
                _window.Top = saved.Top;
                _window.Width = saved.Width;
                _window.Height = _vm.IsMinimized ? BarHeight * scale : saved.Height;
            }
            else
            {
                _window.Width = DockedWidth * scale;
                _window.Height = (_vm.IsMinimized ? BarHeight : DockedHeight) * scale;
                // Libre sans position mémorisée : on reste où l'on est, sauf après une réinitialisation (voir ResetPosition).
                _lastAnchor = null;
                if (!free || _moveToDefault) MoveToAnchor();
                _moveToDefault = false;
            }
        }
        finally
        {
            // LocationChanged/SizeChanged se déclenchent après le retour d'Apply() : on lève le garde plus tard.
            _window.Dispatcher.BeginInvoke(() => _applying = false, DispatcherPriority.Loaded);
        }
    }

    /// <summary>Oublie la position mémorisée : la fenêtre retrouve sa place et sa taille par défaut (haut gauche de l'écran du jeu).</summary>
    public void ResetPosition()
    {
        _settings.FreeBounds = null;
        _moveToDefault = true;
        _saver.RequestSave();
        Apply();
    }

    /// <summary>Ancré sans position mémorisée : suit l'écran du jeu. Ancré avec position mémorisée : ne bouge pas.</summary>
    public void Reanchor()
    {
        if (!_vm.IsDocked || _settings.FreeBounds is not null) return;
        MoveToAnchor();
    }

    /// <summary>Recolle la fenêtre en haut à gauche de l'écran où tourne le jeu (ou de l'écran courant).</summary>
    private void MoveToAnchor()
    {
        var hwnd = new WindowInteropHelper(_window).Handle;
        if (hwnd == IntPtr.Zero) return;

        var game = GameWindowLocator.FindGameWindow();
        var monitor = NativeMethods.GetMonitorRect(game != IntPtr.Zero ? game : hwnd);
        (int X, int Y) anchor = (monitor.Left + MarginPx, monitor.Top + MarginPx);
        if (_lastAnchor == anchor) return;

        _lastAnchor = anchor;
        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, anchor.X, anchor.Y, 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.UiScale)) RescaleSavedBounds();
        if (e.PropertyName is nameof(MainViewModel.IsDocked) or nameof(MainViewModel.IsMinimized) or nameof(MainViewModel.UiScale))
            Apply();
    }

    /// <summary>Verrouillée, la fenêtre ne peut plus être redimensionnée à la main : sa taille mémorisée suit l'échelle.</summary>
    private void RescaleSavedBounds()
    {
        var newScale = _vm.UiScale;
        var oldScale = _lastScale;
        _lastScale = newScale;
        if (_settings.FreeBounds is not { } b || oldScale <= 0 || oldScale == newScale) return;

        var ratio = newScale / oldScale;
        _settings.FreeBounds = b with
        {
            Width = Math.Max(MinFreeWidth * newScale, b.Width * ratio),
            Height = Math.Max(MinFreeHeight * newScale, b.Height * ratio),
        };
        _saver.RequestSave();
    }

    private void RememberFreeBounds()
    {
        if (_applying || _vm.IsDocked || !_window.IsLoaded) return;
        if (double.IsNaN(_window.Left) || double.IsNaN(_window.Top) || _window.ActualWidth <= 0) return;

        if (_vm.IsMinimized)
        {
            // Réduit : on ne mémorise que la position, la taille d'origine est conservée.
            var current = _settings.FreeBounds
                ?? new WindowBounds(_window.Left, _window.Top, DockedWidth * _vm.UiScale, DockedHeight * _vm.UiScale);
            _settings.FreeBounds = current with { Left = _window.Left, Top = _window.Top };
        }
        else
        {
            _settings.FreeBounds = new WindowBounds(_window.Left, _window.Top, _window.ActualWidth, _window.ActualHeight);
        }
        _saver.RequestSave();
    }
}
