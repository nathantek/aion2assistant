using Aion2Assistant.Combat;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aion2Assistant.App.ViewModels;

/// <summary>Titre d'un jour dans la liste de l'historique.</summary>
public sealed record HistoryDayHeaderViewModel(DateOnly Day, string Text);

/// <summary>Une ligne de la liste de l'historique.</summary>
public sealed class HistoryRowViewModel : ObservableObject
{
    private bool _isRecord;
    private bool _isCompareChecked;

    public HistoryRowViewModel(EncounterSummary summary, bool isRecord = false)
    {
        _isRecord = isRecord;
        Id = summary.Id;
        var local = summary.StartedAt.ToLocalTime();
        StartedLocal = local.DateTime;
        Day = DateOnly.FromDateTime(local.DateTime);
        IsTraining = summary.Outcome == EncounterOutcome.Training;
        TimeText = local.ToString("HH:mm", MeterFormat.French);
        Boss = summary.Boss;
        Zone = string.IsNullOrWhiteSpace(summary.Zone) ? MeterFormat.Missing : summary.Zone;
        DurationText = MeterFormat.Duration(summary.DurationMs);
        OutcomeText = summary.Outcome switch
        {
            EncounterOutcome.Kill => "Tué",
            EncounterOutcome.Wipe => "Wipe",
            EncounterOutcome.Manual => "Réinitialisé",
            EncounterOutcome.Training => "Entraînement",
            _ => "Terminé",
        };
        DpsText = MeterFormat.PerSecond(summary.GroupDps, summary.DurationMs);
    }

    public long Id { get; }
    public DateTime StartedLocal { get; }
    public DateOnly Day { get; }

    /// <summary>Vrai pour un essai d'entraînement : seule cette ligne propose la case « Comparer ».</summary>
    public bool IsTraining { get; }

    /// <summary>Case « Comparer » cochée (la sélection est gérée par <see cref="HistoryViewModel"/>).</summary>
    public bool IsCompareChecked
    {
        get => _isCompareChecked;
        set => SetProperty(ref _isCompareChecked, value);
    }

    public string TimeText { get; }
    public string Boss { get; }

    /// <summary>Vrai si ce combat porte le record actuel du personnage sur ce boss.</summary>
    public bool IsRecord
    {
        get => _isRecord;
        set
        {
            if (SetProperty(ref _isRecord, value)) OnPropertyChanged(nameof(BossText));
        }
    }

    public string BossText => _isRecord ? "🏆 " + Boss : Boss;
    public string Zone { get; }
    public string DurationText { get; }
    public string OutcomeText { get; }
    public string DpsText { get; }
}
