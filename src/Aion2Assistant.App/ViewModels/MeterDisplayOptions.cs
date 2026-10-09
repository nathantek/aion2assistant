using Aion2Assistant.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aion2Assistant.App.ViewModels;

/// <summary>Options d'affichage des lignes de meter, partagées entre le direct, l'historique et les Réglages.</summary>
public partial class MeterDisplayOptions : ObservableObject
{
    private readonly CombatSettings _settings;
    private readonly Action _requestSave;

    [ObservableProperty] private bool _showCombatPower;

    public MeterDisplayOptions(CombatSettings settings, Action requestSave)
    {
        _settings = settings;
        _requestSave = requestSave;
        _showCombatPower = settings.ShowCombatPower;
    }

    partial void OnShowCombatPowerChanged(bool value)
    {
        _settings.ShowCombatPower = value;
        _requestSave();
    }
}
