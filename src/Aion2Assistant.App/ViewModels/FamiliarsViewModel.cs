using System.Collections.ObjectModel;
using Aion2Assistant.App.Localization;
using Aion2Assistant.Combat;
using Aion2Assistant.Core.Localization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aion2Assistant.App.ViewModels;

/// <summary>Une ligne de l'onglet Familiers : nom, barre fine, puis « 11/75 » ou « MAX ».</summary>
public sealed partial class FamiliarRowViewModel : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _progressText = "";
    [ObservableProperty] private double _fraction;
    [ObservableProperty] private bool _isMax;

    public void Update(string name, FamiliarStatus status)
    {
        Name = name;
        ProgressText = status.Text;
        Fraction = status.Fraction;
        IsMax = status.IsMax;
    }
}

/// <summary>Onglet Familiers : derniers familiers ayant reçu une âme, et bandeau si l'état n'est pas synchronisé.</summary>
public sealed partial class FamiliarsViewModel : ObservableObject
{
    [ObservableProperty] private string? _bannerText;
    /// <summary>Vrai pour un avertissement (bordure d'alerte) ; faux pour l'attente de l'entrée en jeu (style discret).</summary>
    [ObservableProperty] private bool _bannerIsWarning;
    [ObservableProperty] private bool _isEmpty = true;

    public ObservableCollection<FamiliarRowViewModel> Rows { get; } = new();

    /// <summary>Remet la liste à jour en réutilisant les lignes existantes (pas de clignotement).</summary>
    /// <param name="nowMs">Heure actuelle (ms Unix), pour la période de grâce du bandeau.</param>
    internal void Refresh(FamiliarTracker tracker, Func<int, string> name, long nowMs)
    {
        var recent = tracker.Recent(FamiliarTracker.DefaultRecentLimit);
        while (Rows.Count > recent.Count) Rows.RemoveAt(Rows.Count - 1);
        while (Rows.Count < recent.Count) Rows.Add(new FamiliarRowViewModel());
        for (var i = 0; i < recent.Count; i++) Rows[i].Update(name(recent[i].Id), recent[i]);
        IsEmpty = recent.Count == 0;
        RefreshBanner(tracker, nowMs);
    }

    internal void RefreshBanner(FamiliarTracker tracker, long nowMs)
    {
        var kind = FamiliarBanner.Select(tracker.IsSynced, tracker.LastSyncMs, tracker.IdentifiedAtMs, tracker.StreamStartedAtMs, nowMs);
        BannerIsWarning = kind is FamiliarBannerKind.Stale or FamiliarBannerKind.NeverSynced;
        BannerText = kind switch
        {
            FamiliarBannerKind.Waiting => UiStrings.FamiliarsBannerWaiting,
            FamiliarBannerKind.Stale => UiStrings.Format(nameof(UiStrings.FamiliarsBannerStale),
                DateTimeOffset.FromUnixTimeMilliseconds(tracker.LastSyncMs!.Value).LocalDateTime.ToString(UiStrings.FormatDateTime, AppCulture.Current)),
            FamiliarBannerKind.NeverSynced => UiStrings.FamiliarsBannerNeverSynced,
            _ => null,
        };
    }
}
