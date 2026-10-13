using Aion2Assistant.Core.Localization;

namespace Aion2Assistant.Core.Theming;

/// <summary>Contrôles de lisibilité d'un thème à une opacité de fond donnée.</summary>
public static class ThemeReadability
{
    public const double MainText = 7.0, Secondary = 4.5, MinStatDeltaE = 15.0;

    private static string Num(double v) => v.ToString("0.00", AppCulture.Current);
    private static string Num1(double v) => v.ToString("0.0", AppCulture.Current);

    /// <summary>Liste des problèmes de lisibilité ; vide = thème lisible.</summary>
    public static IReadOnlyList<string> Check(ThemeDefinition theme, double opacity)
    {
        var c = theme.Colors;
        var problems = new List<string>();

        // Pire cas : le fond semi-transparent posé sur du noir puis sur du blanc.
        var backdrops = new (string Label, Rgb Color)[]
        {
            ("fond sur noir", ColorMath.Composite(c.Background, opacity, new Rgb(0, 0, 0))),
            ("fond sur blanc", ColorMath.Composite(c.Background, opacity, new Rgb(255, 255, 255))),
        };

        void OnBackground(string name, Rgb fg, double min)
        {
            foreach (var (label, bg) in backdrops)
            {
                var ratio = ColorMath.ContrastRatio(fg, bg);
                if (ratio < min)
                    problems.Add($"{name} : contraste {Num(ratio)}:1 < {Num1(min)}:1 ({label})");
            }
        }

        OnBackground("text", c.Text, MainText);
        // Contraste élevé : le texte secondaire, les stats et les classes passent aussi à 7:1.
        var secondary = theme.HighContrast ? MainText : Secondary;
        OnBackground("textMuted", c.TextMuted, secondary);

        var stats = new (string Name, Rgb Color)[]
        {
            ("dps", c.Dps), ("heal", c.Heal), ("taken", c.Taken), ("timer", c.Timer), ("alert", c.Alert), ("crit", c.Crit),
        };
        foreach (var (name, color) in stats) OnBackground(name, color, secondary);

        var classes = theme.Classes.Entries().ToArray();
        foreach (var (name, color) in classes) OnBackground(name, color, secondary);

        foreach (var (name, surface) in new[] { ("surface", c.Surface), ("surfaceHover", c.SurfaceHover) })
        {
            var ratio = ColorMath.ContrastRatio(c.Text, surface);
            if (ratio < MainText)
                problems.Add($"text sur {name} : contraste {Num(ratio)}:1 < {Num1(MainText)}:1");
        }

        for (var i = 0; i < stats.Length; i++)
        for (var j = i + 1; j < stats.Length; j++)
        {
            var de = ColorMath.DeltaE76(stats[i].Color, stats[j].Color);
            if (de < MinStatDeltaE)
                problems.Add($"{stats[i].Name}/{stats[j].Name} : écart de couleur {Num1(de)} < {Num1(MinStatDeltaE)}");
        }

        for (var i = 0; i < classes.Length; i++)
        for (var j = i + 1; j < classes.Length; j++)
        {
            var de = ColorMath.DeltaE76(classes[i].Color, classes[j].Color);
            if (de < MinStatDeltaE)
                problems.Add($"{classes[i].Name}/{classes[j].Name} : écart de couleur {Num1(de)} < {Num1(MinStatDeltaE)}");
        }

        return problems;
    }
}
