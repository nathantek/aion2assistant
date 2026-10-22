using System.Windows.Media;
using Aion2Assistant.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aion2Assistant.App.ViewModels;

/// <summary>Ordre des onglets (Suivant / Précédent). Familiars n'existe que si l'option est activée.</summary>
public enum OverlayTab { Dps, Taken, Heal, History, Timers, Familiars, Settings }

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
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowUpdateBanner))]
    private bool _isMinimized;
    [ObservableProperty] private string? _warningMessage;

    /// <summary>Option « Familiers » : affiche l'onglet (et autorise ses pop-ups). Modifiée depuis les Réglages, sans redémarrage.</summary>
    [ObservableProperty] private bool _familiarsEnabled;

    // Bandeau « Version X prête » ; null = masqué. Alimenté par UpdateController.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowUpdateBanner))]
    private string? _updateBannerText;

    /// <summary>Le bandeau est masqué en mode réduit (la notification a déjà prévenu).</summary>
    public bool ShowUpdateBanner => UpdateBannerText is not null && !IsMinimized;

    /// <summary>Actions du bandeau, affectées par UpdateController.</summary>
    public Action? RestartUpdate { get; set; }
    public Action? DeferUpdate { get; set; }

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
        _familiarsEnabled = settings.FamiliarsEnabled;
    }

    public Dictionary<OverlayTab, object> Pages { get; } = new();

    /// <summary>Diagnostic de capture (voyant de la barre d'onglets), affecté par AppHost avant l'affichage.</summary>
    [ObservableProperty] private CaptureViewModel? _capture;

    public object? CurrentPage => Pages.TryGetValue(CurrentTab, out var page) ? page : null;

    public static double ClampScale(double value) =>
        Math.Round(Math.Clamp(value, AppSettings.MinUiScale, AppSettings.MaxUiScale), 2);

    /// <summary>Onglet présent dans la barre (Familiers seulement si l'option est activée).</summary>
    public bool IsTabAvailable(OverlayTab tab) => tab != OverlayTab.Familiars || FamiliarsEnabled;

    [RelayCommand]
    private void SelectTab(OverlayTab tab)
    {
        if (!IsTabAvailable(tab)) return;
        CurrentTab = tab;
        IsMinimized = false;
    }

    [RelayCommand]
    private void NextTab() => CurrentTab = Step(+1);

    [RelayCommand]
    private void PreviousTab() => CurrentTab = Step(-1);

    /// <summary>Onglet voisin dans le sens donné, en sautant les onglets absents.</summary>
    private OverlayTab Step(int direction)
    {
        var tab = CurrentTab;
        for (var i = 0; i < TabCount; i++)
        {
            tab = (OverlayTab)(((int)tab + direction + TabCount) % TabCount);
            if (IsTabAvailable(tab)) return tab;
        }
        return CurrentTab;
    }

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

    [RelayCommand]
    private void RestartForUpdate() => RestartUpdate?.Invoke();

    [RelayCommand]
    private void DeferUpdateBanner() => DeferUpdate?.Invoke();

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

    partial void OnFamiliarsEnabledChanged(bool value)
    {
        _settings.FamiliarsEnabled = value;
        _requestSave();
        // Onglet retiré pendant qu'il est affiché : retour à l'onglet voisin (Timers).
        if (!value && CurrentTab == OverlayTab.Familiars) CurrentTab = OverlayTab.Timers;
    }

    partial void OnIsMinimizedChanged(bool value)
    {
        _settings.Minimized = value;
        _requestSave();
    }
}
