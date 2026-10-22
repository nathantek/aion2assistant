using System.Collections.ObjectModel;
using System.ComponentModel;
using Aion2Assistant.App.Localization;
using Aion2Assistant.App.Services;
using Aion2Assistant.Core.Audio;
using Aion2Assistant.Core.Hotkeys;
using Aion2Assistant.Core.Localization;
using Aion2Assistant.Core.Settings;
using Aion2Assistant.Core.Theming;
using Aion2Assistant.Timers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aion2Assistant.App.ViewModels;

/// <summary>Une langue de la liste des Options : code stocké dans les réglages et nom natif affiché.</summary>
public sealed record LanguageOption(string Code, string NativeName);

public partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly MainViewModel _main;
    private readonly Action _requestSave;
    private readonly Action<SoundRef> _playSound;
    private readonly HotkeyService _hotkeys;
    private readonly Action<bool> _setKeyboardCapture;
    private readonly Action _resetPosition;
    private readonly UpdateController _updates;
    private readonly Action _showEula;
    private readonly Action _restartApp;
    private readonly MeterDisplayOptions _display;

    /// <summary>Puissance de combat après le nom, dans les meters et l'image copiée.</summary>
    public bool ShowCombatPower
    {
        get => _display.ShowCombatPower;
        set => _display.ShowCombatPower = value;
    }

    /// <summary>PV du boss au-dessus de sa barre : pourcentage plutôt que chiffres.</summary>
    public bool BossHpAsPercent
    {
        get => _display.BossHpAsPercent;
        set => _display.BossHpAsPercent = value;
    }

    [ObservableProperty] private double _fontScale;
    [ObservableProperty] private double _opacity;
    [ObservableProperty] private ThemeDefinition? _selectedTheme;
    [ObservableProperty] private double _volume;
    [ObservableProperty] private double _combatIdleSeconds;
    [ObservableProperty] private bool _includeAllPlayers;
    [ObservableProperty] private bool _glowEnabled;
    [ObservableProperty] private bool _checkForUpdates;
    [ObservableProperty] private bool _betaUpdates;

    internal SettingsViewModel(AppSettings settings, MainViewModel main, Action requestSave, Action<SoundRef> playSound,
        HotkeyService hotkeys, IReadOnlyList<TimerDefinition> timers, Action<bool> setKeyboardCapture, Action resetPosition,
        UpdateController updates, Action showEula, MeterDisplayOptions display, Action restartApp)
    {
        _restartApp = restartApp;
        _display = display;
        display.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MeterDisplayOptions.ShowCombatPower)) OnPropertyChanged(nameof(ShowCombatPower));
            if (e.PropertyName == nameof(MeterDisplayOptions.BossHpAsPercent)) OnPropertyChanged(nameof(BossHpAsPercent));
        };
        _updates = updates;
        _showEula = showEula;
        _checkForUpdates = settings.CheckForUpdates;
        _betaUpdates = settings.BetaUpdates;
        updates.Changed += RefreshUpdateStatus;
        _settings = settings;
        _main = main;
        _requestSave = requestSave;
        _playSound = playSound;
        _hotkeys = hotkeys;
        _setKeyboardCapture = setKeyboardCapture;
        _resetPosition = resetPosition;
        _fontScale = settings.FontScale;
        _opacity = settings.Opacity;
        _selectedTheme = Themes.FirstOrDefault(t => t.Id == settings.ThemeId)
                         ?? Themes.FirstOrDefault(t => t.Id == AppSettings.DefaultThemeId);
        _volume = settings.Volume;
        _combatIdleSeconds = settings.Combat.IdleTimeoutSeconds;
        _includeAllPlayers = settings.Combat.IncludeAllPlayers;
        _glowEnabled = settings.GlowEnabled;
        _selectedLanguage = Languages.FirstOrDefault(l => l.Code == AppLanguage.Normalize(settings.Language)) ?? Languages[0];

        foreach (var action in Enum.GetValues<HotkeyAction>())
            Hotkeys.Add(new HotkeyRowViewModel(action, HotkeyLabels.For(action), BeginCapture));
        RefreshHotkeys();

        foreach (var timer in timers)
            Timers.Add(new TimerSettingRowViewModel(timer.Id, timer.Name, settings.GetTimer(timer.Id), requestSave, settings, playSound));
        Timers.Add(new TimerSettingRowViewModel(AppSettings.FieldBossTimersKey, UiStrings.FieldBosses,
            settings.GetTimer(AppSettings.FieldBossTimersKey), requestSave, settings, playSound));

        FamiliarAlerts =
        [
            new FamiliarAlertRowViewModel(UiStrings.FamiliarsTierAlert, settings.Familiars.Tier, requestSave, playSound),
            new FamiliarAlertRowViewModel(UiStrings.FamiliarsMaxAlert, settings.Familiars.Max, requestSave, playSound),
        ];

        main.PropertyChanged += OnMainChanged;
    }

    /// <summary>Option « Familiers » : onglet et pop-ups. Portée par MainViewModel (la barre d'onglets suit en direct).</summary>
    public bool FamiliarsEnabled
    {
        get => _main.FamiliarsEnabled;
        set => _main.FamiliarsEnabled = value;
    }

    /// <summary>Pop-up et son « palier atteint » puis « niveau MAX ».</summary>
    public IReadOnlyList<FamiliarAlertRowViewModel> FamiliarAlerts { get; }

    /// <summary>La case « Effets lumineux » n'a de sens que pour un thème qui définit une lueur.</summary>
    public bool SelectedThemeHasGlow => SelectedTheme?.Glow is not null;

    public string VersionText => UiStrings.Format(nameof(UiStrings.AboutVersion), _updates.CurrentVersionText);

    /// <summary>État des mises à jour (désactivées, à jour, téléchargement, prête, installation via Setup.exe…).</summary>
    public string UpdateStatusText => _updates.StatusText;

    /// <summary>Hors installation : lien vers la page de la nouvelle version (Setup.exe).</summary>
    public bool HasUpdate => _updates.DownloadUrl is not null;
    public string? UpdateUrl => _updates.DownloadUrl;

    /// <summary>Une version téléchargée attend : bouton « Redémarrer » (utile après « Plus tard »).</summary>
    public bool CanRestartForUpdate => _updates.CanRestart;

    private void RefreshUpdateStatus()
    {
        OnPropertyChanged(nameof(UpdateStatusText));
        OnPropertyChanged(nameof(HasUpdate));
        OnPropertyChanged(nameof(UpdateUrl));
        OnPropertyChanged(nameof(CanRestartForUpdate));
    }

    [RelayCommand]
    private void RestartForUpdate() => _updates.RequestRestart();

    public IReadOnlyList<ThemeDefinition> Themes => ThemeManager.Themes;

    /// <summary>Le texte est détouré sous le seuil d'opacité : on le signale sous le curseur.</summary>
    public bool ShowOutlineHint => ThemeManager.EffectiveOpacity(SelectedTheme, Opacity) < AppSettings.OutlineThreshold;

    public ObservableCollection<HotkeyRowViewModel> Hotkeys { get; } = new();
    public ObservableCollection<TimerSettingRowViewModel> Timers { get; } = new();
    public HotkeyRowViewModel? CapturingRow { get; private set; }

    /// <summary>Diagnostic de capture (section CAPTURE), affecté par AppHost.</summary>
    public CaptureViewModel? Capture { get; set; }

    public double UiScale
    {
        get => _main.UiScale;
        set => _main.UiScale = MainViewModel.ClampScale(value);
    }

    partial void OnFontScaleChanged(double value)
    {
        _settings.FontScale = Math.Round(value, 2);
        ThemeManager.Apply(_settings);
        _requestSave();
    }

    partial void OnOpacityChanged(double value)
    {
        _settings.Opacity = value;
        ThemeManager.Apply(_settings);
        OnPropertyChanged(nameof(ShowOutlineHint));
        _requestSave();
    }

    partial void OnSelectedThemeChanged(ThemeDefinition? value)
    {
        if (value is null) return;
        _settings.ThemeId = value.Id;
        ThemeManager.Apply(_settings);
        OnPropertyChanged(nameof(SelectedThemeHasGlow));
        OnPropertyChanged(nameof(ShowOutlineHint));
        _requestSave();
    }

    partial void OnGlowEnabledChanged(bool value)
    {
        _settings.GlowEnabled = value;
        ThemeManager.Apply(_settings);
        _requestSave();
    }

    partial void OnCheckForUpdatesChanged(bool value)
    {
        _settings.CheckForUpdates = value;
        _updates.SetEnabled(value);
        RefreshUpdateStatus();
        _requestSave();
    }

    /// <summary>Canal bêta / stable : pris en compte à la prochaine vérification, sans redémarrage.</summary>
    partial void OnBetaUpdatesChanged(bool value)
    {
        _settings.BetaUpdates = value;
        _requestSave();
    }

    [RelayCommand]
    private void OpenLink(string? url)
    {
        if (string.IsNullOrEmpty(url)) return;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
            || (uri.Host != "github.com" && uri.Host != "nathantek.github.io"))
        {
            Serilog.Log.Debug("Lien ignoré (hôte non autorisé) : {Url}", url);
            return;
        }
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { Serilog.Log.Debug(ex, "Ouverture du lien impossible : {Url}", url); }
    }

    [RelayCommand]
    private void ReadEula() => _showEula();

    /// <summary>Langues proposées, sous leur nom natif (jamais traduit).</summary>
    public IReadOnlyList<LanguageOption> Languages { get; } =
        AppLanguage.Supported.Select(l => new LanguageOption(l, AppLanguage.NativeName(l))).ToList();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LanguageRestartPending), nameof(LanguageRestartText), nameof(LanguageRestartButtonText))]
    private LanguageOption? _selectedLanguage;

    /// <summary>La langue choisie diffère de celle de l'interface : elle s'appliquera au redémarrage.</summary>
    public bool LanguageRestartPending => SelectedLanguage is { } l && l.Code != AppCulture.Language;

    // Message et bouton dans la langue choisie : c'est celle que l'utilisateur lit.
    public string LanguageRestartText => UiStrings.GetIn(TargetLanguage, nameof(UiStrings.LanguageRestartRequired));
    public string LanguageRestartButtonText => UiStrings.GetIn(TargetLanguage, nameof(UiStrings.LanguageRestartNow));
    private string TargetLanguage => SelectedLanguage?.Code ?? AppCulture.Language;

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (value is null) return;
        _settings.Language = value.Code;
        _requestSave();
    }

    /// <summary>« Redémarrer maintenant » : fermeture normale (réglages enregistrés) puis relance de l'app.</summary>
    [RelayCommand]
    private void RestartForLanguage() => _restartApp();

    partial void OnCombatIdleSecondsChanged(double value)
    {
        _settings.Combat.IdleTimeoutSeconds = (int)Math.Clamp(Math.Round(value), 3, 60);
        _requestSave();
    }

    partial void OnIncludeAllPlayersChanged(bool value)
    {
        _settings.Combat.IncludeAllPlayers = value;
        _requestSave();
    }

    partial void OnVolumeChanged(double value)
    {
        _settings.Volume = value;
        _requestSave();
    }

    [RelayCommand]
    private void ResetPosition() => _resetPosition();

    [RelayCommand]
    private void TestSound() => _playSound(new SoundRef(AlertSounds.DefaultId, null));

    /// <summary>Termine la capture d'un raccourci. <paramref name="gesture"/> null = annulé (Échap).</summary>
    public void CompleteCapture(HotkeyGesture? gesture)
    {
        if (CapturingRow is not { } row) return;

        if (gesture is { } g)
        {
            if (!g.IsValid)
            {
                row.Error = UiStrings.HotkeyUnsupported;
                return; // on reste en capture
            }
            var text = g.ToString();
            var taken = Hotkeys.FirstOrDefault(h => h != row && h.Gesture == text);
            if (taken is not null)
            {
                row.Error = UiStrings.Format(nameof(UiStrings.HotkeyAlreadyUsedBy), taken.Label);
                return;
            }
            _settings.Hotkeys[row.Action.ToString()] = text;
            _requestSave();
        }

        row.IsCapturing = false;
        CapturingRow = null;
        try { _hotkeys.RegisterAll(_settings.Hotkeys); RefreshHotkeys(); }
        finally { _setKeyboardCapture(false); }
    }

    public void CancelCapture() => CompleteCapture(null);

    private void BeginCapture(HotkeyRowViewModel row)
    {
        if (CapturingRow is { } previous) previous.IsCapturing = false;
        CapturingRow = row;
        row.Error = null;
        row.IsCapturing = true;
        _hotkeys.UnregisterAll(); // sinon la combinaison serait interceptée comme raccourci au lieu d'être saisie
        _setKeyboardCapture(true);
    }

    private void RefreshHotkeys()
    {
        foreach (var row in Hotkeys)
        {
            row.Gesture = _settings.GetHotkey(row.Action);
            row.Error = _hotkeys.Failures.TryGetValue(row.Action, out var failure) ? failure : null;
        }
    }

    private void OnMainChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.UiScale)) OnPropertyChanged(nameof(UiScale));
        if (e.PropertyName == nameof(MainViewModel.FamiliarsEnabled)) OnPropertyChanged(nameof(FamiliarsEnabled));
        if (e.PropertyName != nameof(MainViewModel.CurrentTab)) return;

        if (_main.CurrentTab == OverlayTab.Settings)
        {
            foreach (var timer in Timers) timer.Reload();
        }
        else if (CapturingRow is not null)
        {
            CompleteCapture(null);
        }
    }
}
