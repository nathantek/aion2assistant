using Aion2Assistant.App.Localization;
using Aion2Assistant.Core.Hotkeys;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aion2Assistant.App.ViewModels;

public partial class HotkeyRowViewModel(HotkeyAction action, string label, Action<HotkeyRowViewModel> beginCapture)
    : ObservableObject
{
    public HotkeyAction Action { get; } = action;
    public string Label { get; } = label;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    private string _gesture = "";

    [ObservableProperty] private string? _error;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    private bool _isCapturing;

    public string DisplayText => IsCapturing ? UiStrings.HotkeyPressCombination : Gesture;

    [RelayCommand]
    private void BeginCapture() => beginCapture(this);
}
