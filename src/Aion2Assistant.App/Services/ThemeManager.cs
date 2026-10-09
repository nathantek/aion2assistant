using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Aion2Assistant.Core.Events;
using Aion2Assistant.Core.Settings;
using Aion2Assistant.Core.Theming;
using Serilog;

namespace Aion2Assistant.App.Services;

/// <summary>
/// Applique le thème et les réglages visuels. Les brosses des ressources sont REMPLACÉES à chaque thème (WPF scelle
/// les Freezables ajoutés aux ressources de l'application : leur Color n'est plus modifiable). Les DynamicResource
/// se mettent à jour seuls ; le code qui conserve une brosse doit la relire sur <see cref="ThemeChanged"/>.
/// </summary>
internal static class ThemeManager
{
    private static readonly (string Key, double Size)[] FontSizes =
    [
        ("FontSizeSmall", 11),
        ("FontSizeNormal", 13),
        ("FontSizeLarge", 16),
        ("FontSizeHuge", 22),
    ];

    private const string BackgroundKey = "OverlayBackgroundBrush";
    private const string SolidBackgroundKey = "SolidBackgroundBrush";
    private const string OutlineKey = "TextOutlineEffect";

    // Clé de ressource -> couleur du thème.
    private static readonly (string Key, Func<ThemeColors, Rgb> Pick)[] ColorKeys =
    [
        ("TextBrush", c => c.Text),
        ("TextMutedBrush", c => c.TextMuted),
        ("SurfaceBrush", c => c.Surface),
        ("SurfaceHoverBrush", c => c.SurfaceHover),
        ("DividerBrush", c => c.Divider),
        ("DpsBrush", c => c.Dps),
        ("HealBrush", c => c.Heal),
        ("TakenBrush", c => c.Taken),
        ("TimerBrush", c => c.Timer),
        ("AlertBrush", c => c.Alert),
        ("CritBrush", c => c.Crit),
    ];

    /// <summary>Clé de la brosse d'une classe (« ClassBrush.Cleric ») ; TextBrush pour une classe inconnue.</summary>
    public static string ClassBrushKey(GameClass c) => c == GameClass.Unknown ? "TextBrush" : "ClassBrush." + c;

    public static IReadOnlyList<ThemeDefinition> Themes { get; private set; } = [];

    /// <summary>Levé à la fin de chaque <see cref="Apply"/> : le code qui garde une brosse doit la relire.</summary>
    public static event Action? ThemeChanged;

    /// <summary>Charge les thèmes ; les brosses sont installées par <see cref="Apply"/>.</summary>
    public static void Initialize(string themesDirectory)
    {
        Themes = ThemeLoader.LoadAll(themesDirectory,
            (file, error) => Log.Warning("Thème ignoré {File} : {Error}", file, error.Message));
        Application.Current.Resources[OutlineKey] = null;
    }

    public static void Apply(AppSettings settings)
    {
        var resources = Application.Current.Resources;
        foreach (var (key, size) in FontSizes)
            resources[key] = size * settings.FontScale;

        var theme = Themes.FirstOrDefault(t => t.Id == settings.ThemeId)
                    ?? Themes.FirstOrDefault(t => t.Id == AppSettings.DefaultThemeId);
        // Sans thème on garde les couleurs actuelles ; dans tous les cas on remplace la brosse (jamais de mutation).
        foreach (var (key, pick) in ColorKeys)
        {
            var color = theme is not null ? ToColor(pick(theme.Colors)) : ((SolidColorBrush)resources[key]).Color;
            resources[key] = NewBrush(color);
        }

        // Une brosse par classe ; sans thème on retombe sur la couleur du texte (déjà remplacée ci-dessus).
        var textColor = ((SolidColorBrush)resources["TextBrush"]).Color;
        foreach (var gameClass in Enum.GetValues<GameClass>())
        {
            if (gameClass == GameClass.Unknown) continue;
            resources[ClassBrushKey(gameClass)] = NewBrush(theme is not null ? ToColor(theme.Classes.For(gameClass)) : textColor);
        }

        // Alpha 0 rendrait le fond transparent aux clics (fenêtre layered) : on garde 1 pour pouvoir déplacer l'overlay à 0 %.
        var alpha = (byte)Math.Max(1, Math.Round(settings.Opacity * 255));
        var baseColor = theme is not null ? ToColor(theme.Colors.Background) : ((SolidColorBrush)resources[BackgroundKey]).Color;
        resources[BackgroundKey] = NewBrush(Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B));
        // Fond opaque pour l'image copiée : même couleur que le fond, sans l'opacité de l'overlay.
        resources[SolidBackgroundKey] = NewBrush(Color.FromRgb(baseColor.R, baseColor.G, baseColor.B));

        // Priorité : lisibilité d'abord (contour sombre sous le seuil d'opacité), puis lueur du thème si activée.
        Effect? textEffect = null;
        if (theme is not null && settings.Opacity < AppSettings.OutlineThreshold)
        {
            textEffect = new DropShadowEffect
            {
                ShadowDepth = 0, BlurRadius = 3, Opacity = 1, Color = ToColor(theme.Colors.Outline),
            };
        }
        else if (theme?.Glow is { } glow && settings.GlowEnabled)
        {
            textEffect = new DropShadowEffect
            {
                ShadowDepth = 0, BlurRadius = glow.Blur, Opacity = glow.Opacity, Color = ToColor(glow.Color),
            };
        }
        textEffect?.Freeze();
        resources[OutlineKey] = textEffect;

        ThemeChanged?.Invoke();
    }

    private static SolidColorBrush NewBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Color ToColor(Rgb rgb) => Color.FromRgb(rgb.R, rgb.G, rgb.B);
}
