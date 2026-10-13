using System.Globalization;

namespace Aion2Assistant.Core.Localization;

/// <summary>Langues proposées par l'app : codes stockés dans <c>AppSettings.Language</c>.</summary>
public static class AppLanguage
{
    public const string English = "en";
    public const string French = "fr";

    public static IReadOnlyList<string> Supported { get; } = [English, French];

    /// <summary>Langue de l'app pour une culture Windows : français si la culture est française, anglais sinon.</summary>
    public static string FromCulture(CultureInfo culture) =>
        culture.TwoLetterISOLanguageName.Equals(French, StringComparison.OrdinalIgnoreCase) ? French : English;

    /// <summary>Ramène une valeur lue (fichier édité à la main) sur une langue connue ; inconnue = anglais.</summary>
    public static string Normalize(string? value)
    {
        var v = value?.Trim().ToLowerInvariant();
        return v == French ? French : English;
    }

    /// <summary>Nom de la langue dans cette langue (liste des Options) : jamais traduit.</summary>
    public static string NativeName(string? language) =>
        Normalize(language) == French ? "Français" : "English";
}
