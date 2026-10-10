using Aion2Assistant.Core.Audio;
using Aion2Assistant.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aion2Assistant.App.ViewModels;

public partial class TimerSettingRowViewModel : ObservableObject
{
    private static readonly int[] Thresholds = [1, 2, 3, 5, 10, 15, 30, 60];
    private readonly TimerSettings _settings;
    private readonly Action _requestSave;

    [ObservableProperty] private bool _visible;
    [ObservableProperty] private bool _notify;
    [ObservableProperty] private bool _sound;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThresholdText))]
    private int _threshold;

    public TimerSettingRowViewModel(string id, string name, TimerSettings settings, Action requestSave,
        AppSettings app, Action<SoundRef> playSound)
    {
        Id = id;
        Name = name;
        _settings = settings;
        _requestSave = requestSave;
        SoundChoice = new SoundChoiceViewModel(
            () => SoundResolver.ForTimer(app, settings),
            sound => { settings.SoundId = sound.Id; settings.CustomSoundPath = sound.CustomPath; },
            requestSave, playSound);
        Reload();
    }

    /// <summary>Son propre à la ligne (hérite du réglage global tant qu'il n'est pas choisi).</summary>
    public SoundChoiceViewModel SoundChoice { get; }

    public string Id { get; }
    public string Name { get; }
    public string ThresholdText => $"{Threshold} min";

    /// <summary>Relit les réglages (le son a pu être changé depuis l'onglet Timers).</summary>
    public void Reload()
    {
        Visible = _settings.Visible;
        Notify = _settings.Notify;
        Sound = _settings.Sound;
        Threshold = _settings.SoonThresholdMinutes;
        SoundChoice?.Reload();
    }

    [RelayCommand]
    private void IncreaseThreshold() => Threshold = Thresholds.FirstOrDefault(t => t > Threshold, Thresholds[^1]);

    [RelayCommand]
    private void DecreaseThreshold() => Threshold = Thresholds.LastOrDefault(t => t < Threshold, Thresholds[0]);

    partial void OnVisibleChanged(bool value) { _settings.Visible = value; _requestSave(); }
    partial void OnNotifyChanged(bool value) { _settings.Notify = value; _requestSave(); }
    partial void OnSoundChanged(bool value) { _settings.Sound = value; _requestSave(); }
    partial void OnThresholdChanged(int value) { _settings.SoonThresholdMinutes = value; _requestSave(); }
}
