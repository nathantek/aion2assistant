using System.Globalization;

namespace Aion2Assistant.Core.Localization;

/// <summary>
/// Culture de l'app (textes, nombres, dates), fixée une seule fois au démarrage par <see cref="Apply"/>.
/// Toutes les mises en forme passent par <see cref="Current"/> plutôt que par une culture en dur.
/// </summary>
public static class AppCulture
{
    private static CultureInfo _current = CultureFor(AppLanguage.English);
    private static readonly AsyncLocal<CultureInfo?> Scoped = new();

    public static CultureInfo Current => Scoped.Value ?? _current;

    /// <summary>Code de langue courant (« en » ou « fr »).</summary>
    public static string Language => AppLanguage.FromCulture(Current);

    /// <summary>Culture complète d'une langue : fr-FR ou en-US.</summary>
    public static CultureInfo CultureFor(string? language) =>
        CultureInfo.GetCultureInfo(AppLanguage.Normalize(language) == AppLanguage.French ? "fr-FR" : "en-US");

    /// <summary>
    /// Fixe la culture de tout le processus (thread courant et nouveaux threads). Appelé une fois au démarrage,
    /// avant toute UI : la langue ne change pas à chaud.
    /// </summary>
    public static void Apply(string? language)
    {
        var culture = CultureFor(language);
        _current = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    /// <summary>
    /// Culture locale au flux d'exécution courant, rétablie au Dispose (tests : n'affecte pas les autres threads).
    /// </summary>
    public static IDisposable Use(string language)
    {
        var previous = Scoped.Value;
        Scoped.Value = CultureFor(language);
        return new Restore(previous);
    }

    private sealed class Restore(CultureInfo? previous) : IDisposable
    {
        public void Dispose() => Scoped.Value = previous;
    }
}
