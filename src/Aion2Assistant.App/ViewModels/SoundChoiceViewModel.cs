using Aion2Assistant.App.Localization;
using Aion2Assistant.Core.Audio;
using Aion2Assistant.Core.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aion2Assistant.App.ViewModels;

/// <summary>
/// Sélecteur de son réutilisable (une ligne de timer) : liste, choix du fichier perso, aperçu, libellé.
/// Le son effectif est lu et écrit par l'appelant ; la sélection n'a d'effet (sauvegarde, aperçu) que sur action de l'utilisateur.
/// </summary>
public sealed partial class SoundChoiceViewModel : ObservableObject
{
    /// <summary>Sons intégrés puis « Fichier perso… » (libellés dans la langue de l'app).</summary>
    public static IReadOnlyList<AlertSound> Choices { get; } = [.. AlertSounds.BuiltIn, AlertSounds.Custom];

    private readonly Func<SoundRef> _get;
    private readonly Action<SoundRef> _set;
    private readonly Action _requestSave;
    private readonly Action<SoundRef> _play;
    private readonly Func<string?> _pickFile;

    [ObservableProperty] private AlertSound? _selectedChoice;
    [ObservableProperty] private string? _label;
    [ObservableProperty] private bool _isCustom;

    public SoundChoiceViewModel(Func<SoundRef> get, Action<SoundRef> set, Action requestSave, Action<SoundRef> play,
        Func<string?>? pickFile = null)
    {
        _get = get;
        _set = set;
        _requestSave = requestSave;
        _play = play;
        _pickFile = pickFile ?? PickFileDialog;
        Reload();
    }

    /// <summary>Relit le son effectif sans rien sauvegarder ni jouer.</summary>
    public void Reload()
    {
        var current = _get();
        SelectedChoice = Find(current.Id);
        Label = LabelFor(current);
        IsCustom = current.Id == AlertSounds.CustomId;
    }

    partial void OnSelectedChoiceChanged(AlertSound? value)
    {
        var current = _get();
        if (value is null || value.Id == current.Id) return;

        SoundRef chosen;
        if (value.Id == AlertSounds.CustomId)
        {
            // Aucun fichier retenu pour ce choix : on ouvre le sélecteur, l'annulation revient au choix précédent.
            if (_pickFile() is not { } path)
            {
                // Différé : réaffecter pendant la mise à jour de la liaison laisse la ComboBox sur le choix annulé.
                System.Windows.Application.Current.Dispatcher.BeginInvoke(() => SelectedChoice = Find(_get().Id));
                return;
            }
            chosen = new SoundRef(AlertSounds.CustomId, path);
        }
        else
        {
            chosen = new SoundRef(value.Id, null);
        }
        Commit(chosen);
    }

    /// <summary>Choisit un autre fichier perso quand « perso » est déjà actif ; l'annulation ne change rien.</summary>
    [RelayCommand]
    private void ChooseFile()
    {
        if (_pickFile() is { } path) Commit(new SoundRef(AlertSounds.CustomId, path));
    }

    private void Commit(SoundRef sound)
    {
        _set(sound);
        Label = LabelFor(sound);
        IsCustom = sound.Id == AlertSounds.CustomId;
        _requestSave();
        _play(sound);
    }

    private static AlertSound Find(string id) => Choices.FirstOrDefault(c => c.Id == id) ?? Choices[0];

    /// <summary>Nom du fichier perso quand il est actif, sinon null (ligne masquée).</summary>
    private static string? LabelFor(SoundRef sound) =>
        sound.Id == AlertSounds.CustomId && sound.CustomPath is { } path
            ? File.Exists(path) ? Path.GetFileName(path)
                : UiStrings.Format(nameof(UiStrings.SoundFileMissing), Path.GetFileName(path), Strings.SoundChime)
            : null;

    private static string? PickFileDialog()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = UiStrings.SoundFileFilter, Title = UiStrings.TimerSoundTip };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
