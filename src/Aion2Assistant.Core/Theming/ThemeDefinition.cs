using System.Globalization;
using Aion2Assistant.Core.Events;

namespace Aion2Assistant.Core.Theming;

/// <summary>Couleur sRGB 8 bits, écrite « #RRGGBB ».</summary>
public readonly record struct Rgb(byte R, byte G, byte B)
{
    /// <summary>Accepte exactement « #RRGGBB », sans tenir compte de la casse.</summary>
    public static bool TryParse(string? hex, out Rgb rgb)
    {
        rgb = default;
        if (hex is not { Length: 7 } || hex[0] != '#') return false;
        if (!byte.TryParse(hex.AsSpan(1, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var r)
            || !byte.TryParse(hex.AsSpan(3, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var g)
            || !byte.TryParse(hex.AsSpan(5, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var b))
            return false;
        rgb = new Rgb(r, g, b);
        return true;
    }

    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";
}

public sealed record ThemeColors(Rgb Text, Rgb TextMuted, Rgb Surface, Rgb SurfaceHover, Rgb Divider, Rgb Background,
    Rgb Outline, Rgb Dps, Rgb Heal, Rgb Taken, Rgb Timer, Rgb Alert, Rgb Crit);

/// <summary>Une couleur par classe de jeu (Unknown n'a pas de couleur propre).</summary>
public sealed record ClassColors(Rgb Gladiator, Rgb Templar, Rgb Ranger, Rgb Assassin, Rgb Elementalist, Rgb Sorcerer,
    Rgb Cleric, Rgb Chanter, Rgb Brawler)
{
    /// <summary>Couleur de la classe ; <see cref="GameClass.Unknown"/> est refusée (l'appelant choisit son repli).</summary>
    public Rgb For(GameClass c) => c switch
    {
        GameClass.Gladiator => Gladiator,
        GameClass.Templar => Templar,
        GameClass.Ranger => Ranger,
        GameClass.Assassin => Assassin,
        GameClass.Elementalist => Elementalist,
        GameClass.Sorcerer => Sorcerer,
        GameClass.Cleric => Cleric,
        GameClass.Chanter => Chanter,
        GameClass.Brawler => Brawler,
        _ => throw new ArgumentOutOfRangeException(nameof(c), c, "Aucune couleur pour cette classe."),
    };

    /// <summary>Les neuf classes colorées avec leur clé JSON, dans l'ordre de l'énumération.</summary>
    public IEnumerable<(string Name, Rgb Color)> Entries()
    {
        yield return ("gladiator", Gladiator);
        yield return ("templar", Templar);
        yield return ("ranger", Ranger);
        yield return ("assassin", Assassin);
        yield return ("elementalist", Elementalist);
        yield return ("sorcerer", Sorcerer);
        yield return ("cleric", Cleric);
        yield return ("chanter", Chanter);
        yield return ("brawler", Brawler);
    }
}

/// <summary>Lueur optionnelle du texte : couleur, rayon de flou (1 à 20) et opacité (0 à 1).</summary>
public sealed record ThemeGlow(Rgb Color, double Blur, double Opacity)
{
    public const double MinBlur = 1, MaxBlur = 20;
}

public sealed record ThemeDefinition(string Id, string Name, ThemeColors Colors, ClassColors Classes, ThemeGlow? Glow = null);

public sealed class ThemeFormatException(string message) : Exception(message);
