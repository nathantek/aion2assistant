using Aion2Assistant.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aion2Assistant.App.ViewModels;

/// <summary>Bascule « Boss seul / Tout » du meter DPS, partagée entre le direct et l'historique.</summary>
public partial class BossFilter : ObservableObject
{
    private readonly CombatSettings _settings;
    private readonly Action _requestSave;

    [ObservableProperty] private bool _bossOnly;

    public BossFilter(CombatSettings settings, Action requestSave)
    {
        _settings = settings;
        _requestSave = requestSave;
        _bossOnly = settings.BossOnly;
    }

    partial void OnBossOnlyChanged(bool value)
    {
        _settings.BossOnly = value;
        _requestSave();
    }
}
