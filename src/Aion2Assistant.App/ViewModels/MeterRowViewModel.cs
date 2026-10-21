using System.Windows.Media;
using Aion2Assistant.App.Services;
using Aion2Assistant.Combat;
using Aion2Assistant.Core.Events;
using Aion2Assistant.Core.Localization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aion2Assistant.App.ViewModels;

/// <summary>Mises en forme des meters, dans la culture de l'app (<see cref="AppFormat"/>).</summary>
public static class MeterFormat
{
    public static string Compact(double v) => AppFormat.Compact(v);

    public const string Missing = "—";

    /// <summary>Débit par seconde ; en dessous d'une seconde de combat la valeur n'est pas fiable.</summary>
    public static string PerSecond(double value, long durationMs) =>
        durationMs < 1000 ? Missing : Compact(value) + "/s";

    public static string Share(double share, long meterTotal) =>
        meterTotal <= 0 ? Missing : share.ToString("P0", AppCulture.Current);

    public static string Percent(double? pct) =>
        pct is { } p ? AppFormat.Percent(p) : Missing;

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
    // Suffixe « · puissance » après le nom ; vide si l'option est off ou la valeur inconnue.
    [ObservableProperty] private string _combatPowerText = "";
    // Brosse de la classe du joueur (nom et barre) ; relue à chaque Update car le thème remplace les brosses.
    [ObservableProperty] private Brush _classBrush = AppHost.Resource("TextBrush");
    // Classe du joueur : choisit l'icône devant le nom (ClassIconConverter).
    [ObservableProperty] private GameClass _class;

    public MeterRowViewModel(uint actorId)
    {
        ActorId = actorId;
    }

    public uint ActorId { get; }

    public void Update(ActorRow row, long topTotal, bool showDeaths, bool showPerSecond,
        long durationMs, long meterTotal, bool showCombatPower = false)
    {
        Name = row.Name;
        ClassBrush = AppHost.Resource(ThemeManager.ClassBrushKey(row.Class));
        Class = row.Class;
        IsSelf = row.IsSelf;
        PerSecondText = showPerSecond ? MeterFormat.PerSecond(row.PerSecond, durationMs) : "";
        TotalText = MeterFormat.Compact(row.Total);
        ShareText = MeterFormat.Share(row.Share, meterTotal);
        BarFraction = topTotal <= 0 ? 0 : Math.Clamp(row.Total / (double)topTotal, 0, 1);
        CombatPowerText = CombatPowerFormat.Suffix(row.CombatPower, showCombatPower);
        DeathsText = showDeaths && row.Deaths > 0 ? $"☠ {row.Deaths}" : "";
    }
}

public sealed record SkillLineViewModel(string Name, string TotalText, string ShareText, string HitsText, string CritText,
    string MaxText, string OverhealText);

public sealed record RecapLineViewModel(string TimeText, string Source, string Skill, string AmountText, Brush Brush);

public sealed record RecapHeaderViewModel(string Text);
