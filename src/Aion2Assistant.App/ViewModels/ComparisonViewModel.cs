using Aion2Assistant.Combat;
using CommunityToolkit.Mvvm.Input;

namespace Aion2Assistant.App.ViewModels;

/// <summary>Une ligne de la comparaison ; <see cref="Sign"/> (-1, 0, 1) pilote la couleur du Δ dans le XAML (thème).</summary>
public sealed record ComparisonLine(string Label, string A, string B, string Delta, int Sign);

/// <summary>Comparaison côte à côte de deux essais (A = le plus ancien).</summary>
public sealed partial class ComparisonViewModel
{
    private readonly Action _close;

    /// <summary>Essais illisibles : message seul.</summary>
    public ComparisonViewModel(Action close)
    {
        _close = close;
        Message = "Combat illisible.";
    }

    public ComparisonViewModel(Action close, HistoryRowViewModel rowA, HistoryRowViewModel rowB, EncounterSnapshot a, EncounterSnapshot b)
    {
        _close = close;
        var c = TrainingComparer.Compare(a, b);
        HeaderA = Header(rowA, c.DurationA);
        HeaderB = Header(rowB, c.DurationB);
        Lines =
        [
            Line("DPS", c.DpsA, c.DpsB, c.DpsDeltaPct, v => MeterFormat.Compact(v) + "/s"),
            Line("Dégâts totaux", c.TotalA, c.TotalB, c.TotalDeltaPct, MeterFormat.Compact),
            CritLine(c.CritA, c.CritB),
        ];
        Skills = c.Skills.Select(s => Line(s.Name, s.TotalA, s.TotalB, s.DeltaPct, MeterFormat.Compact)).ToList();
    }

    public string? Message { get; }
    public bool HasData => Message is null;
    public string HeaderA { get; } = "";
    public string HeaderB { get; } = "";
    public IReadOnlyList<ComparisonLine> Lines { get; } = [];
    public IReadOnlyList<ComparisonLine> Skills { get; } = [];

    [RelayCommand]
    private void Back() => _close();

    private static string Header(HistoryRowViewModel row, long durationMs) =>
        row.StartedLocal.ToString("dd/MM/yyyy HH:mm", MeterFormat.French) + " · " + MeterFormat.Duration(durationMs);

    private static ComparisonLine Line(string label, double? a, double? b, double? delta, Func<double, string> format) =>
        new(label, a is { } x ? format(x) : MeterFormat.Missing, b is { } y ? format(y) : MeterFormat.Missing,
            delta is { } d ? DeltaText(d, " %", out var sign) : MeterFormat.Missing,
            delta is null ? 0 : Sign(delta.Value));

    /// <summary>Critique : le Δ est une différence en points (b - a), pas un pourcentage relatif.</summary>
    private static ComparisonLine CritLine(double? a, double? b)
    {
        string Fmt(double v) => v.ToString("0.0", MeterFormat.French) + " %";
        var delta = a is { } x && b is { } y ? b.Value - x : (double?)null;
        return new("Critique", a is { } p ? Fmt(p) : MeterFormat.Missing, b is { } q ? Fmt(q) : MeterFormat.Missing,
            delta is { } d ? DeltaText(d, " pts", out _) : MeterFormat.Missing,
            delta is null ? 0 : Sign(delta.Value));
    }

    private static int Sign(double d) => Math.Sign(Math.Round(d, 1));

    /// <summary>Arrondit d'abord, puis préfixe « + » : pas de « +0,0 % » ni de « -0,0 % ».</summary>
    private static string DeltaText(double d, string unit, out int sign)
    {
        var r = Math.Round(d, 1);
        sign = Math.Sign(r);
        return (r > 0 ? "+" : "") + r.ToString("0.0", MeterFormat.French) + unit;
    }

    private static ComparisonLine Line(string label, long? a, long? b, double? delta, Func<double, string> format) =>
        Line(label, a is { } x ? x : (double?)null, b is { } y ? y : (double?)null, delta, format);
}
