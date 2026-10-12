using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Aion2Assistant.App.Services;
using Aion2Assistant.Combat;
using Aion2Assistant.Core.Events;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace Aion2Assistant.App.ViewModels;

/// <summary>
/// Une courbe du graphique : DPS lissé par seconde d'un acteur, avec sa brosse de thème partagée.
/// DashIndex distingue les joueurs de même classe : 0 = trait plein, 1 = tirets, 2+ = pointillés.
/// </summary>
public sealed record ChartSeries(uint ActorId, string Name, bool IsSelf, bool IsVisible, double[] Smoothed, Brush Brush,
    int DashIndex = 0);

/// <summary>Actions d'entraînement fournies au meter DPS live (absentes de l'historique).</summary>
public sealed record TrainingControls(Action<long> Start, Action Stop);

/// <summary>État de l'essai relu à chaque rafraîchissement : armé, en cours, durée fixée (0 = libre) et temps écoulé.</summary>
public readonly record struct TrainingState(bool Armed, bool Running, long DurationMs, long ElapsedMs);

public partial class MeterViewModel : ObservableObject
{
    private readonly string _accentKey;
    private Brush _textBrush;
    private Brush _healBrush;
    private readonly BossFilter? _bossFilter;
    private readonly MeterDisplayOptions? _display;
    private uint? _selectedId;
    // Courbes masquées par clic sur la légende ; remis à zéro à chaque nouveau combat.
    private readonly HashSet<uint> _hiddenSeries = new();
    private long? _chartStartMs;

    [ObservableProperty] private bool _hasSnapshot;
    [ObservableProperty] private string _headerText = "";
    // Badge d'issue à droite du titre (historique seulement) ; null = état en texte dans le titre.
    [ObservableProperty] private OutcomeBadge? _badge;
    [ObservableProperty] private double? _targetHpPct;
    [ObservableProperty] private string _groupText = "";
    [ObservableProperty] private bool _isDetail;
    [ObservableProperty] private string _detailName = "";
    [ObservableProperty] private IReadOnlyList<SkillLineViewModel> _detailSkills = [];
    [ObservableProperty] private IReadOnlyList<object> _detailRecaps = [];
    [ObservableProperty] private string _copyTextLabel = CopyTextIdle;
    [ObservableProperty] private string _copyImageLabel = CopyImageIdle;

    [ObservableProperty] private bool _isChart;
    [ObservableProperty] private IReadOnlyList<ChartSeries> _chartSeries = [];
    [ObservableProperty] private long _chartDurationMs;

    // Couleurs des séries dont la classe est inconnue (sans classe, on garde l'ancien ordre de repli)
    private static readonly string[] SeriesBrushKeys =
        ["TextBrush", "TextMutedBrush", "DpsBrush", "HealBrush", "TimerBrush", "AlertBrush", "TakenBrush", "CritBrush"];

    private const string CopyTextIdle = "Copier texte";
    private const string CopyImageIdle = "Copier image";
    private DispatcherTimer? _copyReset;

    private readonly Dictionary<uint, MeterRowViewModel> _rowsById = new();

    public MeterViewModel(MeterKind kind, string title, string accentKey, BossFilter? bossFilter = null,
        MeterDisplayOptions? display = null)
    {
        _bossFilter = bossFilter;
        _display = display;
        if (display is not null) display.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MeterDisplayOptions.ShowCombatPower)) Update(_lastSnapshot);
        };
        if (bossFilter is not null) bossFilter.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsBossOnly));
            OnPropertyChanged(nameof(IsAll));
            Update(_lastSnapshot);
        };
        Kind = kind;
        Title = title;
        _accentKey = accentKey;
        _textBrush = AppHost.Resource("TextBrush");
        _healBrush = AppHost.Resource("HealBrush");
        // Les brosses de thème sont remplacées à chaque changement : on les relit. Abonnement jamais retiré (vie = celle de l'app).
        ThemeManager.ThemeChanged += OnThemeChanged;
    }

    private void OnThemeChanged()
    {
        _textBrush = AppHost.Resource("TextBrush");
        _healBrush = AppHost.Resource("HealBrush");
        OnPropertyChanged(nameof(Accent));
        // Recalcule les séries du graphique et les recaps du détail avec les nouvelles brosses.
        if (_lastSnapshot is not null) Update(_lastSnapshot);
    }

    /// <summary>Commandes d'entraînement ; null = pas de bouton (historique, soins, dégâts reçus).</summary>
    public TrainingControls? Training { get; init; }
    public bool ShowTrainingButton => Training is not null;
    /// <summary>Barre d'actions sous la liste : Entraînement (live) et/ou Copier (dès qu'il y a un combat).</summary>
    public bool ShowActionBar => ShowTrainingButton || HasSnapshot;
    [ObservableProperty] private bool _isTrainingStripOpen;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasTrainingText))] private string _trainingText = "";
    public bool HasTrainingText => TrainingText.Length > 0;

    /// <summary>Met à jour la ligne d'état de l'essai (appelé à chaque rafraîchissement).</summary>
    public void ApplyTraining(TrainingState state)
    {
        TrainingText = state switch
        {
            { Running: true, DurationMs: > 0 } => $"Entraînement : {MeterFormat.Duration(Math.Max(0, state.DurationMs - state.ElapsedMs))} restantes",
            { Running: true } => $"Entraînement libre : {MeterFormat.Duration(state.ElapsedMs)}",
            { Armed: true } => "Entraînement prêt : frappe pour démarrer.",
            _ => "",
        };
    }

    [RelayCommand]
    private void ToggleTrainingStrip() => IsTrainingStripOpen = !IsTrainingStripOpen;

    [RelayCommand]
    private void StartTraining(string? durationMs)
    {
        IsTrainingStripOpen = false;
        if (Training is not null && long.TryParse(durationMs, out var ms)) Training.Start(ms);
    }

    [RelayCommand]
    private void StopTraining() => Training?.Stop();

    /// <summary>Ouvre le dernier combat dans l'historique (renseigné par AppHost, absent de l'historique lui-même).</summary>
    public Action? OpenLastFight { get; set; }

    /// <summary>Résumé du dernier combat pour l'état vide ; null = section masquée.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasLastFight))] private string? _lastFightText;

    public bool HasLastFight => LastFightText is not null;

    [RelayCommand]
    private void OpenLastFightClick() => OpenLastFight?.Invoke();

    public MeterKind Kind { get; }
    public string Title { get; }

    /// <summary>Historique : l'issue d'un combat terminé s'affiche en badge plutôt qu'en texte dans le titre.</summary>
    public bool ShowOutcomeBadge { get; init; }
    public Brush Accent => AppHost.Resource(_accentKey);
    public ObservableCollection<MeterRowViewModel> Rows { get; } = new();
    public bool ShowPerSecond => Kind == MeterKind.Dps;
    public bool ShowOverheal => Kind == MeterKind.Heal;
    public bool ShowRecaps => Kind == MeterKind.Taken;
    public bool ShowBossToggle => _bossFilter is not null && Kind == MeterKind.Dps;
    public bool IsBossOnly => _bossFilter?.BossOnly == true;
    public bool IsAll => !IsBossOnly;
    public bool ShowChartToggle => Kind == MeterKind.Dps;
    public bool HasChartData => ChartSeries.Count > 0;
    public bool ShowList => !IsChart && !IsDetail;
    public bool ShowDetail => !IsChart && IsDetail;
    public bool HasTargetHp => TargetHpPct is not null;

    partial void OnHasSnapshotChanged(bool value) => OnPropertyChanged(nameof(ShowActionBar));
    partial void OnTargetHpPctChanged(double? value) => OnPropertyChanged(nameof(HasTargetHp));
    partial void OnChartSeriesChanged(IReadOnlyList<ChartSeries> value) => OnPropertyChanged(nameof(HasChartData));

    partial void OnIsChartChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowList));
        OnPropertyChanged(nameof(ShowDetail));
        if (value && _lastSnapshot is not null) UpdateChart(_lastSnapshot);
    }

    partial void OnIsDetailChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowList));
        OnPropertyChanged(nameof(ShowDetail));
    }

    [RelayCommand]
    private void ToggleSeries(ChartSeries? series)
    {
        if (series is null) return;
        if (!_hiddenSeries.Remove(series.ActorId)) _hiddenSeries.Add(series.ActorId);
        if (_lastSnapshot is not null) UpdateChart(_lastSnapshot);
    }

    [RelayCommand]
    private void ToggleChart()
    {
        if (!IsChart) Back();
        IsChart = !IsChart;
    }

    /// <summary>Sélecteur [Boss | Tout] : « True » = boss seul, « False » = tous les dégâts.</summary>
    [RelayCommand]
    private void SetBossOnly(string? value)
    {
        if (_bossFilter is not null) _bossFilter.BossOnly = string.Equals(value, "True", StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand]
    private void Select(MeterRowViewModel? row)
    {
        if (row is null) return;
        _selectedId = row.ActorId;
        DetailName = row.Name;
        IsDetail = true;
        if (_lastSnapshot is not null) UpdateDetail(_lastSnapshot);
    }

    [RelayCommand]
    private void Back()
    {
        _selectedId = null;
        IsDetail = false;
    }

    private EncounterSnapshot? _lastSnapshot;

    /// <summary>Copie le classement de cet onglet sur une ligne, prête à coller dans le chat du jeu.</summary>
    [RelayCommand]
    private void Copy()
    {
        if (_lastSnapshot is null) return;
        var text = CombatShareFormatter.Format(_lastSnapshot, Kind, bossOnly: _bossFilter?.BossOnly == true);
        var data = new DataObject();
        data.SetText(text);
        CopyTextLabel = TrySetClipboard(data) ? "✓ Copié" : "Échec de la copie";
        StartCopyReset();
    }

    /// <summary>Copie une image du tableau (toutes les lignes, sans défilement) dans le presse-papiers.</summary>
    [RelayCommand]
    private void CopyImage()
    {
        var image = ShareImageRenderer.Render(this);
        var ok = false;
        if (image is not null)
        {
            var data = new DataObject();
            data.SetImage(image);
            ok = TrySetClipboard(data);
        }
        CopyImageLabel = ok ? "✓ Image copiée" : "Échec de la copie";
        StartCopyReset();
    }

    private static bool TrySetClipboard(DataObject data)
    {
        // Sans copie forcée (copy: false) : pas d'OleFlushClipboard, qui échoue avec CLIPBRD_E_CANT_OPEN quand
        // l'historique du presse-papiers de Windows lit les données au même instant. Les données restent servies
        // par l'app tant qu'elle tourne.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Clipboard.SetDataObject(data, copy: false);
                return true;
            }
            catch (COMException ex)
            {
                // L'erreur peut survenir alors que nos données sont bien en place.
                if (IsOnClipboard(data)) return true;
                if (attempt == 4)
                {
                    Log.Warning(ex, "Impossible de copier dans le presse-papiers");
                    return false;
                }
                // Le presse-papiers peut être verrouillé un instant par une autre application.
                Log.Debug(ex, "Presse-papiers occupé, nouvel essai");
                Thread.Sleep(50);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Impossible de copier dans le presse-papiers");
                return false;
            }
        }
        return false;
    }

    private static bool IsOnClipboard(DataObject data)
    {
        try { return Clipboard.IsCurrent(data); }
        catch (Exception) { return false; }
    }

    /// <summary>Un seul minuteur remet les deux libellés de copie à leur état de repos après 1,5 s.</summary>
    private void StartCopyReset()
    {
        _copyReset ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _copyReset.Stop();
        _copyReset.Tick -= OnCopyReset;
        _copyReset.Tick += OnCopyReset;
        _copyReset.Start();
    }

    private void OnCopyReset(object? sender, EventArgs e)
    {
        _copyReset?.Stop();
        CopyTextLabel = CopyTextIdle;
        CopyImageLabel = CopyImageIdle;
    }

    public void Update(EncounterSnapshot? snapshot)
    {
        _lastSnapshot = snapshot;
        if (snapshot is null)
        {
            HasSnapshot = false;
            Badge = null;
            if (Rows.Count > 0) Rows.Clear();
            _rowsById.Clear();
            ChartSeries = [];
            _hiddenSeries.Clear();
            _chartStartMs = null;
            Back();
            return;
        }

        HasSnapshot = true;
        if (_chartStartMs != snapshot.StartMs)
        {
            _hiddenSeries.Clear();
            _chartStartMs = snapshot.StartMs;
        }
        Badge = ShowOutcomeBadge && !snapshot.Active ? OutcomeBadge.For(snapshot.Outcome, snapshot.TargetHpPct) : null;
        HeaderText = Badge is null
            ? $"{snapshot.Title} · {MeterFormat.Duration(snapshot.DurationMs)} · {CombatShareFormatter.StateLabel(snapshot)}"
            : $"{snapshot.Title} · {MeterFormat.Duration(snapshot.DurationMs)}";
        TargetHpPct = snapshot.TargetHpPct is { } pct ? Math.Clamp(pct, 0, 100) : null;

        var meter = MeterOf(snapshot);
        GroupText = ShowPerSecond
            ? $"Groupe : {MeterFormat.PerSecond(meter.PerSecond, snapshot.DurationMs)} · {MeterFormat.Compact(meter.Total)}"
            : $"Groupe : {MeterFormat.Compact(meter.Total)}";
        if (Kind == MeterKind.Dps && _bossFilter?.BossOnly == true && snapshot.BossDps is null)
            GroupText += " · aucun boss, tout est compté";
        SyncRows(meter, snapshot.DurationMs);
        UpdateChart(snapshot);

        if (_selectedId is not null) UpdateDetail(snapshot);
    }

    private void UpdateChart(EncounterSnapshot snapshot)
    {
        if (!IsChart || Kind != MeterKind.Dps) return;
        var source = _bossFilter?.BossOnly == true && snapshot.BossDpsTimeline is { } boss ? boss : snapshot.DpsTimeline;
        ChartDurationMs = snapshot.DurationMs;
        if (source is null)
        {
            ChartSeries = [];
            return;
        }
        // Couleur de la classe ; à classe égale (courbes masquées comprises) la 2e est en tirets, les suivantes en pointillés.
        var perClass = new Dictionary<GameClass, int>();
        var unknownCount = 0;
        var list = new List<ChartSeries>(source.Count);
        foreach (var s in source)
        {
            string key;
            var dash = 0;
            if (s.Class == GameClass.Unknown)
            {
                key = SeriesBrushKeys[unknownCount++ % SeriesBrushKeys.Length];
            }
            else
            {
                key = ThemeManager.ClassBrushKey(s.Class);
                perClass.TryGetValue(s.Class, out dash);
                perClass[s.Class] = dash + 1;
            }
            list.Add(new ChartSeries(s.ActorId, s.Name, s.IsSelf, !_hiddenSeries.Contains(s.ActorId), Timeline.Rolling(s.DamagePerSecond),
                AppHost.Resource(key), dash));
        }
        ChartSeries = list;
    }

    private MeterSnapshot MeterOf(EncounterSnapshot s) => Kind switch
    {
        MeterKind.Heal => s.Heal,
        MeterKind.Taken => s.Taken,
        _ => _bossFilter?.BossOnly == true && s.BossDps is { } boss ? boss : s.Dps,
    };

    private void SyncRows(MeterSnapshot meter, long durationMs)
    {
        var actors = meter.Actors;
        var present = new HashSet<uint>(actors.Count);
        foreach (var a in actors) present.Add(a.ActorId);
        for (var i = Rows.Count - 1; i >= 0; i--)
        {
            if (present.Contains(Rows[i].ActorId)) continue;
            _rowsById.Remove(Rows[i].ActorId);
            Rows.RemoveAt(i);
        }

        var top = actors.Count == 0 ? 0 : actors[0].Total;
        for (var i = 0; i < actors.Count; i++)
        {
            var actor = actors[i];
            if (!_rowsById.TryGetValue(actor.ActorId, out var row))
            {
                row = new MeterRowViewModel(actor.ActorId);
                _rowsById[actor.ActorId] = row;
                Rows.Insert(i, row);
            }
            else if (!ReferenceEquals(Rows[i], row))
            {
                Rows.Move(Rows.IndexOf(row), i);
            }
            row.Update(actor, top, Kind == MeterKind.Taken, ShowPerSecond, durationMs, meter.Total,
                _display?.ShowCombatPower == true);
        }
    }

    private void UpdateDetail(EncounterSnapshot snapshot)
    {
        var actor = MeterOf(snapshot).Actors.FirstOrDefault(a => a.ActorId == _selectedId);
        if (actor is null)
        {
            Back();
            return;
        }

        DetailName = actor.Name;
        var skills = actor.Skills.Select(s => new SkillLineViewModel(
            s.Name,
            MeterFormat.Compact(s.Total),
            MeterFormat.Share(s.Share, actor.Total),
            s.Hits.ToString("N0", MeterFormat.French),
            MeterFormat.Percent(s.CritPct),
            MeterFormat.Compact(s.Max),
            s.Overheal is { } oh ? MeterFormat.Compact(oh) : MeterFormat.Missing)).ToList();
        if (!skills.SequenceEqual(DetailSkills)) DetailSkills = skills;

        if (!ShowRecaps) return;
        var recaps = new List<object>();
        foreach (var d in snapshot.Deaths.Where(d => d.ActorId == actor.ActorId))
        {
            recaps.Add(new RecapHeaderViewModel("Mort à " + MeterFormat.Duration(Math.Max(0, d.AtMs - snapshot.StartMs))));
            recaps.AddRange(d.Lines.Select(l => new RecapLineViewModel(
                "-" + (Math.Abs(l.AtMs - d.AtMs) / 1000.0).ToString("0.0", MeterFormat.French) + " s",
                l.Source, l.Skill, MeterFormat.Compact(l.Amount), l.IsHeal ? _healBrush : _textBrush)));
        }
        if (!recaps.SequenceEqual(DetailRecaps)) DetailRecaps = recaps;
    }
}
