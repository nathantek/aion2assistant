using System.Collections.ObjectModel;
using System.Windows.Media;
using Aion2Assistant.App.Localization;
using Aion2Assistant.Combat;
using Aion2Assistant.Core.Localization;
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
    [ObservableProperty] private HistoryFilter _filter;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private bool _hasNoMatch;

    public HistoryViewModel(Func<long, EncounterSnapshot?> load, string dps, string heal, string taken, BossFilter bossFilter,
        MeterDisplayOptions display)
    {
        _load = load;
        Dps = new MeterViewModel(MeterKind.Dps, UiStrings.TabDps, dps, bossFilter, display) { ShowOutcomeBadge = true };
        Heal = new MeterViewModel(MeterKind.Heal, UiStrings.TabHeal, heal, display: display) { ShowOutcomeBadge = true };
        Taken = new MeterViewModel(MeterKind.Taken, UiStrings.TabTaken, taken, display: display) { ShowOutcomeBadge = true };
        _selectedMeter = Dps;
        Items.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(ShowFilters));
        };
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

    /// <summary>Puces et recherche : seulement s'il y a des combats et que l'historique est lisible.</summary>
    public bool ShowFilters => Items.Count > 0 && ErrorMessage is null;

    partial void OnErrorMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(ShowFilters));
        if (value is not null) SetLatest(null);
    }

    /// <summary>Résumé du combat le plus récent (null si la liste est vide ou l'historique en erreur).</summary>
    public EncounterSummary? Latest { get; private set; }

    /// <summary>Levé quand <see cref="Latest"/> change.</summary>
    public event Action? LatestChanged;

    private void SetLatest(EncounterSummary? summary)
    {
        if (ReferenceEquals(Latest, summary)) return;
        Latest = summary;
        LatestChanged?.Invoke();
    }

    /// <summary>Ouvre le détail du combat le plus récent (ferme d'abord comparaison ou détail) ; rien si la liste est vide.</summary>
    public void OpenLatest()
    {
        var row = Items.OfType<HistoryRowViewModel>().FirstOrDefault();
        if (row is null) return;
        CloseComparison();
        Back();
        OpenCommand.Execute(row);
    }

    /// <summary>Dit si un combat porte un record actuel ; renseigné par le contrôleur de records.</summary>
    public Func<long, bool>? IsRecord { get; set; }

    /// <summary>Recalcule le trophée de chaque ligne (après un nouveau record) et le filtre « Records ».</summary>
    public void RefreshRecords()
    {
        foreach (var row in Items.OfType<HistoryRowViewModel>()) row.IsRecord = IsRecord?.Invoke(row.Id) ?? false;
        ApplyFilter();
    }

    /// <summary>Autorise (true) ou retire (false) le focus clavier de l'overlay ; fourni par l'hôte.</summary>
    public Action<bool>? SetKeyboardInput { get; set; }

    private bool _typing;

    /// <summary>Début de saisie dans la recherche : l'overlay peut prendre le focus clavier.</summary>
    public void BeginTyping()
    {
        if (_typing || SetKeyboardInput is null) return;
        _typing = true;
        SetKeyboardInput(true);
    }

    /// <summary>Fin de saisie : l'overlay redevient sans focus et le jeu retrouve le clavier.</summary>
    public void EndTyping()
    {
        if (!_typing) return;
        _typing = false;
        SetKeyboardInput?.Invoke(false);
    }

    partial void OnFilterChanged(HistoryFilter value) => ApplyFilter();
    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private void SetFilter(HistoryFilter filter) => Filter = filter;

    private bool Passes(HistoryRowViewModel row) =>
        HistoryListFilter.Matches(row.Summary, Filter, row.IsRecord, SearchText);

    /// <summary>Applique puce et recherche à chaque carte, puis recompte les en-têtes de jour.</summary>
    private void ApplyFilter()
    {
        foreach (var row in Items.OfType<HistoryRowViewModel>())
        {
            row.IsVisible = Passes(row);
            // Une carte masquée ne garde pas sa case cochée : « Comparer (2) » ne porte que sur des cartes visibles.
            if (!row.IsVisible) row.IsCompareChecked = false;
        }
        Recount();
    }

    /// <summary>Compte les cartes visibles sous chaque en-tête (masqué à zéro).</summary>
    private void Recount()
    {
        HistoryDayHeaderViewModel? header = null;
        var count = 0;
        var any = false;
        foreach (var item in Items)
        {
            if (item is HistoryDayHeaderViewModel h)
            {
                if (header is not null) header.Count = count;
                header = h;
                count = 0;
            }
            else if (item is HistoryRowViewModel { IsVisible: true })
            {
                count++;
                any = true;
            }
        }
        if (header is not null) header.Count = count;
        HasNoMatch = Items.Count > 0 && !any;
    }

    /// <summary>Remplace la liste ; <paramref name="summaries"/> est trié du plus récent au plus ancien.</summary>
    public void SetItems(IEnumerable<EncounterSummary> summaries, DateTime now)
    {
        foreach (var r in Items.OfType<HistoryRowViewModel>()) r.PropertyChanged -= OnRowChanged;
        Items.Clear();
        _checked.Clear();
        OnPropertyChanged(nameof(CanCompare));
        EncounterSummary? newest = null;
        var all = summaries.ToList();
        foreach (var s in Enumerable.Reverse(all)) { Add(s, now, TrainingDelta.PercentVsPrevious(s, all)); newest = s; }
        Recount();
        SetLatest(ErrorMessage is null ? newest : null);
    }

    /// <summary>Ajoute un combat en tête (le plus récent), en créant l'en-tête de son jour au besoin.</summary>
    public void Insert(EncounterSummary summary, DateTime now)
    {
        if (ErrorMessage is null) SetLatest(summary);
        var previous = Items.OfType<HistoryRowViewModel>().Select(r => r.Summary);
        Add(summary, now, TrainingDelta.PercentVsPrevious(summary, previous));
        Recount();
    }

    private void Add(EncounterSummary summary, DateTime now, double? trainingDelta)
    {
        var row = new HistoryRowViewModel(summary, IsRecord?.Invoke(summary.Id) ?? false, trainingDelta);
        row.IsVisible = Passes(row);
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
        if (day == today) return UiStrings.HistoryToday;
        if (day == today.AddDays(-1)) return UiStrings.HistoryYesterday;
        return day.ToString(UiStrings.FormatDayHeader, AppCulture.Current);
    }

    [RelayCommand]
    private void Open(HistoryRowViewModel? row)
    {
        if (row is null) return;
        var snapshot = _load(row.Id);
        if (snapshot is null)
        {
            DetailMessage = UiStrings.FightUnreadable;
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
