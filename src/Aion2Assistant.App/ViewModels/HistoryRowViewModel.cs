using Aion2Assistant.App.Localization;
using Aion2Assistant.Combat;
using Aion2Assistant.Core.Localization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aion2Assistant.App.ViewModels;

/// <summary>Titre d'un jour dans la liste de l'historique, avec le nombre de combats visibles ce jour-là.</summary>
public sealed class HistoryDayHeaderViewModel : ObservableObject
{
    private int _count;

    public HistoryDayHeaderViewModel(DateOnly day, string text)
    {
        Day = day;
        Text = text;
    }

    public DateOnly Day { get; }
    public string Text { get; }

    /// <summary>Combats de ce jour qui passent le filtre ; l'en-tête est masqué à zéro.</summary>
    public int Count
    {
        get => _count;
        set
        {
            if (!SetProperty(ref _count, value)) return;
            OnPropertyChanged(nameof(CountText));
            OnPropertyChanged(nameof(IsVisible));
        }
    }

    public string CountText => _count == 1 ? UiStrings.HistoryFightCountOne : UiStrings.Format(nameof(UiStrings.HistoryFightCount), _count);
    public bool IsVisible => _count > 0;
}

/// <summary>Une carte de la liste de l'historique.</summary>
public sealed class HistoryRowViewModel : ObservableObject
{
    private bool _isRecord;
    private bool _isCompareChecked;
    private bool _isVisible = true;

    public HistoryRowViewModel(EncounterSummary summary, bool isRecord = false, double? trainingDeltaPct = null)
    {
        Summary = summary;
        _isRecord = isRecord;
        Id = summary.Id;
        var local = summary.StartedAt.ToLocalTime();
        StartedLocal = local.DateTime;
        Day = DateOnly.FromDateTime(local.DateTime);
        IsTraining = summary.Outcome == EncounterOutcome.Training;
        Boss = summary.Boss;
        Badge = OutcomeBadge.For(summary.Outcome, summary.TargetHpPct);
        TimeDurationText = local.ToString("HH:mm", AppCulture.Current) + " · " + MeterFormat.Duration(summary.DurationMs);
        GroupText = UiStrings.Format(nameof(UiStrings.HistoryGroup), MeterFormat.PerSecond(summary.GroupDps, summary.DurationMs));

        HasSelf = summary.SelfDps is not null;
        SelfText = summary.SelfDps is { } self
            ? UiStrings.Format(nameof(UiStrings.HistorySelf), MeterFormat.PerSecond(self, summary.DurationMs))
            : "";
        ShareFraction = Math.Clamp(summary.SelfShare ?? 0, 0, 1);
        if (IsTraining)
        {
            // Entraînement : pas de rang, écart avec l'essai précédent de même durée cible.
            var sign = 0;
            RightText = trainingDeltaPct is { } d
                ? UiStrings.Format(nameof(UiStrings.HistoryVsPrevious), TrainingDelta.Format(d, out sign))
                : "";
            DeltaSign = sign;
        }
        else
        {
            var rank = summary.SelfRank is { } r
                ? summary.PartySize is { } n ? $"#{r} / {n}" : $"#{r}"
                : null;
            var share = summary.SelfShare is { } s ? MeterFormat.Percent(s * 100) : null;
            RightText = string.Join(" · ", new[] { rank, share }.Where(t => t is not null));
        }
    }

    public EncounterSummary Summary { get; }
    public long Id { get; }
    public DateTime StartedLocal { get; }
    public DateOnly Day { get; }

    /// <summary>Vrai pour un essai d'entraînement : seule cette carte propose la case « Comparer ».</summary>
    public bool IsTraining { get; }
    public bool IsNotTraining => !IsTraining;

    /// <summary>Case « Comparer » cochée (la sélection est gérée par <see cref="HistoryViewModel"/>).</summary>
    public bool IsCompareChecked
    {
        get => _isCompareChecked;
        set => SetProperty(ref _isCompareChecked, value);
    }

    /// <summary>Faux si la carte est écartée par le filtre ou la recherche.</summary>
    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public string Boss { get; }

    /// <summary>Vrai si ce combat porte le record actuel du personnage sur ce boss.</summary>
    public bool IsRecord
    {
        get => _isRecord;
        set => SetProperty(ref _isRecord, value);
    }

    public OutcomeBadge Badge { get; }

    /// <summary>« 22:26 · 0:52 ».</summary>
    public string TimeDurationText { get; }

    /// <summary>« Groupe 45,6 k/s ».</summary>
    public string GroupText { get; }

    /// <summary>Faux si le personnage n'a pas été détecté dans ce combat : ligne « Toi » et barre masquées.</summary>
    public bool HasSelf { get; }

    /// <summary>« Toi 13,6 k/s ».</summary>
    public string SelfText { get; }

    /// <summary>« #1 / 5 · 30 % » hors entraînement, « +4 % vs essai préc. » en entraînement.</summary>
    public string RightText { get; }

    /// <summary>Signe de l'écart d'entraînement : 1 mieux (Heal), -1 moins bien (Alert), 0 neutre (muted).</summary>
    public int DeltaSign { get; }

    /// <summary>Barre de part du groupe : hors entraînement (en solo elle serait toujours pleine).</summary>
    public bool ShowShareBar => HasSelf && !IsTraining;

    /// <summary>Part du DPS du groupe, de 0 à 1.</summary>
    public double ShareFraction { get; }
}
