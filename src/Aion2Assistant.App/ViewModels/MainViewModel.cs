using System.Windows.Media;
using Aion2Assistant.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aion2Assistant.App.ViewModels;

public enum OverlayTab { Dps, Taken, Heal, History, Timers, Settings }

public partial class MainViewModel : ObservableObject
{
    private const double ScaleStep = 0.1;
    private static readonly int TabCount = Enum.GetValues<OverlayTab>().Length;

    private readonly AppSettings _settings;
    private readonly Action _requestSave;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPage))]
    private OverlayTab _currentTab = OverlayTab.Timers;

    [ObservableProperty] private double _uiScale;
    [ObservableProperty] private bool _isDocked;
    [ObservableProperty] private bool _isMinimized;
    [ObservableProperty] private string? _warningMessage;

    // Valeur clé de la barre réduite (DPS en combat, sinon prochain timer) ; null hors réduction. Alimentée par MiniBarController.
    [ObservableProperty] private string? _miniValueText;
    [ObservableProperty] private Brush? _miniValueBrush;
    [ObservableProperty] private string? _miniValueTip;

    public MainViewModel(AppSettings settings, Action requestSave)
    {
        _settings = settings;
        _requestSave = requestSave;
        _uiScale = settings.UiScale;
        _isDocked = settings.Docked;
        _isMinimized = settings.Minimized;
    }

    public Dictionary<OverlayTab, object> Pages { get; } = new();

    /// <summary>Diagnostic de capture (voyant de la barre d'onglets), affecté par AppHost avant l'affichage.</summary>
    [ObservableProperty] private CaptureViewModel? _capture;

    public object? CurrentPage => Pages.TryGetValue(CurrentTab, out var page) ? page : null;

    public static double ClampScale(double value) =>
        Math.Round(Math.Clamp(value, AppSettings.MinUiScale, AppSettings.MaxUiScale), 2);

    [RelayCommand]
    private void SelectTab(OverlayTab tab)
    {
        CurrentTab = tab;
        IsMinimized = false;
    }

    [RelayCommand]
    private void NextTab() => CurrentTab = (OverlayTab)(((int)CurrentTab + 1) % TabCount);

    [RelayCommand]
    private void PreviousTab() => CurrentTab = (OverlayTab)(((int)CurrentTab + TabCount - 1) % TabCount);

    [RelayCommand]
    private void ToggleDock() => IsDocked = !IsDocked;

    [RelayCommand]
    private void ToggleMinimize() => IsMinimized = !IsMinimized;

    [RelayCommand]
    private void ScaleUp() => UiScale = ClampScale(UiScale + ScaleStep);

    [RelayCommand]
    private void ScaleDown() => UiScale = ClampScale(UiScale - ScaleStep);

    [RelayCommand]
    private void DismissWarning() => WarningMessage = null;

    partial void OnUiScaleChanged(double value)
    {
        _settings.UiScale = value;
        _requestSave();
    }

    partial void OnIsDockedChanged(bool value)
    {
        _settings.Docked = value;
        _requestSave();
    }

    partial void OnIsMinimizedChanged(bool value)
    {
        _settings.Minimized = value;
        _requestSave();
    }
}
