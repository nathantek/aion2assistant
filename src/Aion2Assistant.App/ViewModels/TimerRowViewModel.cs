using System.Windows;
using System.Windows.Media;
using Aion2Assistant.App.Localization;
using Aion2Assistant.App.Services;
using Aion2Assistant.Core.Localization;
using Aion2Assistant.Core.Settings;
using Aion2Assistant.Timers;
using Aion2Assistant.Timers.FieldBosses;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aion2Assistant.App.ViewModels;

public partial class TimerRowViewModel : ObservableObject
{
    private readonly Action<string> _toggleSound;

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _shortName = "";
    [ObservableProperty] private string _glyph = "";
    [ObservableProperty] private string _subText = "";
    [ObservableProperty] private string _remainingText = "";
    [ObservableProperty] private double _progress;
    [ObservableProperty] private bool _isSoon;
    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private Brush _accentBrush = Brushes.White;
    [ObservableProperty] private string _soundGlyph = "";

    public TimerRowViewModel(string id, Action<string> toggleSound)
    {
        Id = id;
        _toggleSound = toggleSound;
    }

    public string Id { get; }

    public void Update(TimerState state, TimerSettings settings, DateTimeOffset now)
    {
        var assumed = state.Category == FieldBossTracker.Category && state.Name.EndsWith(FieldBossTracker.AssumedSuffix, StringComparison.Ordinal);
        Name = assumed ? state.Name[..^FieldBossTracker.AssumedSuffix.Length] : state.Name;
        // Nom court (barre minimisée) ; repli sur le nom complet, notamment pour les boss de terrain.
        ShortName = state.ShortName ?? Name;
        Glyph = CategoryGlyphs.For(state.Category);
        IsOpen = state.Phase == TimerPhase.Open;
        IsSoon = !IsOpen && state.Remaining <= TimeSpan.FromMinutes(settings.SoonThresholdMinutes);
        RemainingText = IsOpen && state.OpenUntil is { } until
            ? CountdownFormatter.Format(until - now)
            : IsOpen && state.Category == FieldBossTracker.Category
                ? (assumed ? UiStrings.FieldBossAliveAssumed : UiStrings.FieldBossAlive)
                : CountdownFormatter.Format(state.Remaining);
        Progress = state.Progress;
        AccentBrush = Resource(IsSoon ? "AlertBrush" : "TimerBrush");
        SoundGlyph = settings.Sound ? "" : "";

        var local = state.NextAt.ToLocalTime();
        var when = state.Remaining >= TimeSpan.FromDays(1)
            ? local.ToString("ddd HH:mm", AppCulture.Current)
            : local.ToString("HH:mm", AppCulture.Current);
        var status = IsOpen ? UiStrings.TimerOpen : UiStrings.Format(nameof(UiStrings.TimerAt), when);
        SubText = state.Verified ? status : UiStrings.Format(nameof(UiStrings.TimerUnverified), status);
    }

    [RelayCommand]
    private void ToggleSound() => _toggleSound(Id);

    private static Brush Resource(string key) => (Brush)Application.Current.Resources[key];
}
