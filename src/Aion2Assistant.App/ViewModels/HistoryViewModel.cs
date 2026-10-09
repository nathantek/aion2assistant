using System.Collections.ObjectModel;
using System.Windows.Media;
using Aion2Assistant.Combat;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aion2Assistant.App.ViewModels;

/// <summary>Onglet Historique : liste des combats groupés par jour, puis détail en lecture seule.</summary>
public partial class HistoryViewModel : ObservableObject
{
    private readonly Func<long, EncounterSnapshot?> _load;

    private readonly List<HistoryRowViewModel> _checked = new();

    [ObservableProperty, NotifyPropertyChangedFor(nameof(ShowList))] private bool _isDetail;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ShowList))] private bool _isComparing;
    [ObservableProperty] private ComparisonViewModel? _comparison;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _detailMessage;
    [ObservableProperty] private MeterViewModel _selectedMeter;

    public HistoryViewModel(Func<long, EncounterSnapshot?> load, string dps, string heal, string taken, BossFilter bossFilter)
    {
        _load = load;
        Dps = new MeterViewModel(MeterKind.Dps, "DPS", dps, bossFilter);
        Heal = new MeterViewModel(MeterKind.Heal, "Soins", heal);
        Taken = new MeterViewModel(MeterKind.Taken, "Dégâts reçus", taken);
        _selectedMeter = Dps;
        Items.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));
    }

    public MeterViewModel Dps { get; }
    public MeterViewModel Heal { get; }
    public MeterViewModel Taken { get; }

    /// <summary>En-têtes de jour (<see cref="HistoryDayHeaderViewModel"/>) et lignes (<see cref="HistoryRowViewModel"/>).</summary>
    public ObservableCollection<object> Items { get; } = new();

    /// <summary>La liste n'est visible ni dans le détail d'un combat ni dans la comparaison.</summary>
    public bool ShowList => !IsDetail && !IsComparing;

    /// <summary>Vrai quand exactement deux essais sont cochés : le bouton « Comparer (2) » apparaît.</summary>
    public bool CanCompare => _checked.Count == 2;

    public bool IsEmpty => Items.Count == 0 && ErrorMessage is null;

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(IsEmpty));

    /// <summary>Dit si un combat porte un record actuel ; renseigné par le contrôleur de records.</summary>
    public Func<long, bool>? IsRecord { get; set; }

    /// <summary>Recalcule le trophée de chaque ligne (après un nouveau record).</summary>
    public void RefreshRecords()
    {
        foreach (var row in Items.OfType<HistoryRowViewModel>()) row.IsRecord = IsRecord?.Invoke(row.Id) ?? false;
    }

    /// <summary>Remplace la liste ; <paramref name="summaries"/> est trié du plus récent au plus ancien.</summary>
    public void SetItems(IEnumerable<EncounterSummary> summaries, DateTime now)
    {
        foreach (var r in Items.OfType<HistoryRowViewModel>()) r.PropertyChanged -= OnRowChanged;
        Items.Clear();
        _checked.Clear();
        OnPropertyChanged(nameof(CanCompare));
        foreach (var s in summaries.Reverse()) Insert(s, now);
    }

    /// <summary>Ajoute un combat en tête (le plus récent), en créant l'en-tête de son jour au besoin.</summary>
    public void Insert(EncounterSummary summary, DateTime now)
    {
        var row = new HistoryRowViewModel(summary, IsRecord?.Invoke(summary.Id) ?? false);
        row.PropertyChanged += OnRowChanged;
        if (Items.Count > 0 && Items[0] is HistoryDayHeaderViewModel head && head.Day == row.Day)
        {
            Items.Insert(1, row);
            return;
        }
        Items.Insert(0, row);
        Items.Insert(0, new HistoryDayHeaderViewModel(row.Day, DayLabel(row.Day, DateOnly.FromDateTime(now))));
    }

    /// <summary>Tient la sélection des essais à comparer : au plus deux, la plus ancienne case cochée se décoche.</summary>
    private void OnRowChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(HistoryRowViewModel.IsCompareChecked) || sender is not HistoryRowViewModel row) return;
        if (row.IsCompareChecked)
        {
            if (!_checked.Contains(row)) _checked.Add(row);
            if (_checked.Count > 2) _checked[0].IsCompareChecked = false;
        }
        else _checked.Remove(row);
        OnPropertyChanged(nameof(CanCompare));
    }

    public static string DayLabel(DateOnly day, DateOnly today)
    {
        if (day == today) return "Aujourd'hui";
        if (day == today.AddDays(-1)) return "Hier";
        return day.ToString("dddd d MMMM", MeterFormat.French);
    }

    [RelayCommand]
    private void Open(HistoryRowViewModel? row)
    {
        if (row is null) return;
        var snapshot = _load(row.Id);
        if (snapshot is null)
        {
            DetailMessage = "Combat illisible.";
        }
        else
        {
            DetailMessage = null;
            Dps.Update(snapshot);
            Heal.Update(snapshot);
            Taken.Update(snapshot);
            SelectedMeter = Dps;
        }
        IsDetail = true;
    }

    [RelayCommand]
    private void Back()
    {
        IsDetail = false;
        DetailMessage = null;
    }

    [RelayCommand]
    private void ShowMeter(MeterViewModel? meter)
    {
        if (meter is not null) SelectedMeter = meter;
    }

    [RelayCommand]
    private void Compare()
    {
        if (_checked.Count != 2) return;
        var first = _checked[0];
        var second = _checked[1];
        var snapA = _load(first.Id);
        var snapB = _load(second.Id);
        if (snapA is null || snapB is null)
        {
            Comparison = new ComparisonViewModel(CloseComparison);
        }
        else
        {
            // A = le plus ancien, comme dans TrainingComparer.
            if (snapA.StartMs > snapB.StartMs) (first, second, snapA, snapB) = (second, first, snapB, snapA);
            Comparison = new ComparisonViewModel(CloseComparison, first, second, snapA, snapB);
        }
        IsComparing = true;
    }

    private void CloseComparison()
    {
        IsComparing = false;
        Comparison = null;
    }
}
