namespace Aion2Assistant.Core.Localization;

/// <summary>
/// Formats de nombres communs, selon <see cref="AppCulture.Current"/>.
/// <code>
///                     fr          en
/// Compact            950         950
///                    1 782       1,782      (N0 : séparateur de milliers de la culture)
///                    45,6 k      45.6k      (≥ 10 000, une décimale)
///                    1,25 M      1.25M      (≥ 1 000 000, deux décimales)
/// CompactTight       52,3k       52.3k      (copie pour le chat : jamais d'espace ni de séparateur de milliers)
/// Percent            23 %        23%
/// </code>
/// Les suffixes et le motif du pourcentage viennent des ressources (FormatKilo, FormatMega, FormatPercent).
/// </summary>
public static class AppFormat
{
    /// <summary>Format compact des meters (débits, totaux, puissance de combat).</summary>
    public static string Compact(double v)
    {
        var c = AppCulture.Current;
        if (v >= 1_000_000) return string.Format(c, Strings.FormatMega, (v / 1_000_000).ToString("0.00", c));
        if (v >= 10_000) return string.Format(c, Strings.FormatKilo, (v / 1_000).ToString("0.0", c));
        return v.ToString("N0", c);
    }

    /// <summary>Format compact sans espace, pour le texte collé dans le chat du jeu.</summary>
    public static string CompactTight(double v)
    {
        var c = AppCulture.Current;
        if (v >= 1_000_000) return (v / 1_000_000).ToString("0.00", c) + "M";
        if (v >= 10_000) return (v / 1_000).ToString("0.0", c) + "k";
        return v.ToString("0", c);
    }

    /// <summary>Pourcentage déjà multiplié par 100 : « 23 % » / « 23% ».</summary>
    public static string Percent(double value, string format = "0") =>
        PercentText(value.ToString(format, AppCulture.Current));

    /// <summary>Applique le motif de pourcentage à un nombre déjà mis en forme (signe compris).</summary>
    public static string PercentText(string number) =>
        string.Format(AppCulture.Current, Strings.FormatPercent, number);
}
