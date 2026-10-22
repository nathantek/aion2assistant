using Aion2Assistant.Core.Audio;
using Aion2Assistant.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aion2Assistant.App.ViewModels;

/// <summary>Réglage d'un événement de familier (palier, MAX) dans les Réglages : pop-up, son et choix du son, comme une ligne de timer.</summary>
public sealed partial class FamiliarAlertRowViewModel : ObservableObject
{
    private readonly FamiliarAlertSettings _settings;
    private readonly Action _requestSave;

    [ObservableProperty] private bool _notify;
    [ObservableProperty] private bool _sound;

    public FamiliarAlertRowViewModel(string name, FamiliarAlertSettings settings, Action requestSave, Action<SoundRef> playSound)
    {
        Name = name;
        _settings = settings;
        _requestSave = requestSave;
        _notify = settings.Notify;
        _sound = settings.Sound;
        SoundChoice = new SoundChoiceViewModel(
            () => new SoundRef(settings.SoundId, settings.CustomSoundPath),
            sound => { settings.SoundId = sound.Id; settings.CustomSoundPath = sound.CustomPath; },
            requestSave, playSound);
    }

    public string Name { get; }
    public SoundChoiceViewModel SoundChoice { get; }

    partial void OnNotifyChanged(bool value) { _settings.Notify = value; _requestSave(); }
    partial void OnSoundChanged(bool value) { _settings.Sound = value; _requestSave(); }
}
