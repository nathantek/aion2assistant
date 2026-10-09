using System.Globalization;
using System.Windows.Media;
using Aion2Assistant.App.Services;
using Aion2Assistant.Combat;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aion2Assistant.App.ViewModels;

public static class MeterFormat
{
    public static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    public static string Compact(double v)
    {
        if (v >= 1_000_000) return (v / 1_000_000).ToString("0.00", French) + " M";
        if (v >= 10_000) return (v / 1_000).ToString("0.0", French) + " k";
        return v.ToString("N0", French);
    }

    public const string Missing = "—";

    /// <summary>Débit par seconde ; en dessous d'une seconde de combat la valeur n'est pas fiable.</summary>
    public static string PerSecond(double value, long durationMs) =>
        durationMs < 1000 ? Missing : Compact(value) + "/s";

    public static string Share(double share, long meterTotal) =>
        meterTotal <= 0 ? Missing : share.ToString("P0", French);

    public static string Percent(double? pct) =>
        pct is { } p ? p.ToString("0", French) + " %" : Missing;

    public static string Duration(long ms)
    {
        var seconds = Math.Max(0, ms) / 1000;
        return $"{seconds / 60}:{seconds % 60:00}";
    }
}

public partial class MeterRowViewModel : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private bool _isSelf;
    [ObservableProperty] private string _perSecondText = "";
    [ObservableProperty] private string _totalText = "";
    [ObservableProperty] private string _shareText = "";
    [ObservableProperty] private double _barFraction;
    [ObservableProperty] private string _deathsText = "";
    // Brosse de la classe du joueur (nom et barre) ; relue à chaque Update car le thème remplace les brosses.
    [ObservableProperty] private Brush _classBrush = AppHost.Resource("TextBrush");

    public MeterRowViewModel(uint actorId)
    {
        ActorId = actorId;
    }

    public uint ActorId { get; }

    public void Update(ActorRow row, long topTotal, bool showDeaths, bool showPerSecond,
        long durationMs, long meterTotal)
    {
        Name = row.Name;
        ClassBrush = AppHost.Resource(ThemeManager.ClassBrushKey(row.Class));
        IsSelf = row.IsSelf;
        PerSecondText = showPerSecond ? MeterFormat.PerSecond(row.PerSecond, durationMs) : "";
        TotalText = MeterFormat.Compact(row.Total);
        ShareText = MeterFormat.Share(row.Share, meterTotal);
        BarFraction = topTotal <= 0 ? 0 : Math.Clamp(row.Total / (double)topTotal, 0, 1);
        DeathsText = showDeaths && row.Deaths > 0 ? $"☠ {row.Deaths}" : "";
    }
}

public sealed record SkillLineViewModel(string Name, string TotalText, string ShareText, string HitsText, string CritText,
    string MaxText, string OverhealText);

public sealed record RecapLineViewModel(string TimeText, string Source, string Skill, string AmountText, Brush Brush);

public sealed record RecapHeaderViewModel(string Text);
