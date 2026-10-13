using System.Text.Json;
using System.Text.RegularExpressions;
using Aion2Assistant.Core.Localization;

namespace Aion2Assistant.Core.Theming;

public static partial class ThemeLoader
{
    [GeneratedRegex("^[a-z0-9-]+$")]
    private static partial Regex IdPattern();

    /// <summary>
    /// Lit un thème JSON (clés camelCase, toutes obligatoires sauf glow, highContrast et nameEn).
    /// Name = nameEn en anglais s'il est renseigné, sinon name (thèmes d'avant la 0.4, thèmes perso).
    /// </summary>
    public static ThemeDefinition Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new ThemeFormatException("Le thème doit être un objet JSON.");

            var id = ReadString(root, "id");
            if (!IdPattern().IsMatch(id))
                throw new ThemeFormatException($"Identifiant de thème invalide : « {id} » (attendu : a-z, 0-9 et tirets).");
            var name = ReadString(root, "name");
            // Lu dans toutes les langues : un nameEn mal formé est signalé même en français.
            var nameEn = ReadOptionalString(root, "nameEn");
            if (nameEn is not null && AppCulture.Language == AppLanguage.English) name = nameEn;

            if (!root.TryGetProperty("colors", out var c) || c.ValueKind != JsonValueKind.Object)
                throw new ThemeFormatException("Clé manquante : « colors ».");

            var colors = new ThemeColors(
                Color(c, "text"), Color(c, "textMuted"), Color(c, "surface"), Color(c, "surfaceHover"),
                Color(c, "divider"), Color(c, "background"), Color(c, "outline"), Color(c, "dps"),
                Color(c, "heal"), Color(c, "taken"), Color(c, "timer"), Color(c, "alert"), Color(c, "crit"));

            if (!root.TryGetProperty("classes", out var k) || k.ValueKind != JsonValueKind.Object)
                throw new ThemeFormatException("Clé manquante : « classes ».");
            var classes = new ClassColors(
                Color(k, "gladiator"), Color(k, "templar"), Color(k, "ranger"), Color(k, "assassin"),
                Color(k, "elementalist"), Color(k, "sorcerer"), Color(k, "cleric"), Color(k, "chanter"), Color(k, "brawler"));
            return new ThemeDefinition(id, name, colors, classes, ReadGlow(root), ReadHighContrast(root));
        }
        catch (JsonException ex)
        {
            throw new ThemeFormatException($"JSON invalide : {ex.Message}");
        }
    }

    /// <summary>Lueur facultative : absente = null ; présente, elle doit être complète et dans les plages.</summary>
    private static ThemeGlow? ReadGlow(JsonElement root)
    {
        if (!root.TryGetProperty("glow", out var g)) return null;
        if (g.ValueKind != JsonValueKind.Object) throw new ThemeFormatException("« glow » doit être un objet.");
        var color = Color(g, "color");
        var blur = ReadNumber(g, "blur");
        var opacity = ReadNumber(g, "opacity");
        if (blur < ThemeGlow.MinBlur || blur > ThemeGlow.MaxBlur)
            throw new ThemeFormatException($"« glow.blur » doit être compris entre {ThemeGlow.MinBlur} et {ThemeGlow.MaxBlur} (reçu : {blur}).");
        if (opacity < 0 || opacity > 1)
            throw new ThemeFormatException($"« glow.opacity » doit être compris entre 0 et 1 (reçu : {opacity}).");
        return new ThemeGlow(color, blur, opacity);
    }

    /// <summary>Indicateur facultatif : absent = false ; présent, il doit être un booléen.</summary>
    private static bool ReadHighContrast(JsonElement root)
    {
        if (!root.TryGetProperty("highContrast", out var v)) return false;
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new ThemeFormatException("« highContrast » doit être un booléen."),
        };
    }

    private static double ReadNumber(JsonElement obj, string key)
    {
        if (!obj.TryGetProperty(key, out var v) || v.ValueKind != JsonValueKind.Number)
            throw new ThemeFormatException($"Clé manquante ou non numérique : « glow.{key} ».");
        return v.GetDouble();
    }

    private static string ReadString(JsonElement obj, string key)
    {
        if (!obj.TryGetProperty(key, out var v) || v.ValueKind != JsonValueKind.String)
            throw new ThemeFormatException($"Clé manquante : « {key} ».");
        return v.GetString() ?? "";
    }

    /// <summary>Texte facultatif : absent ou vide = null ; présent, il doit être une chaîne.</summary>
    private static string? ReadOptionalString(JsonElement obj, string key)
    {
        if (!obj.TryGetProperty(key, out var v)) return null;
        if (v.ValueKind != JsonValueKind.String) throw new ThemeFormatException($"« {key} » doit être une chaîne.");
        return string.IsNullOrWhiteSpace(v.GetString()) ? null : v.GetString()!.Trim();
    }

    private static Rgb Color(JsonElement colors, string key)
    {
        var hex = ReadString(colors, key);
        return Rgb.TryParse(hex, out var rgb)
            ? rgb
            : throw new ThemeFormatException($"Couleur invalide pour « {key} » : {hex}");
    }

    /// <summary>Charge les *.json d'un dossier (triés par nom) ; ignore les fichiers invalides et les id en double.</summary>
    public static IReadOnlyList<ThemeDefinition> LoadAll(string directory, Action<string, Exception>? onInvalid = null)
    {
        var result = new List<ThemeDefinition>();
        if (!Directory.Exists(directory)) return result;

        var files = Directory.GetFiles(directory, "*.json");
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            try
            {
                var theme = Parse(File.ReadAllText(file));
                if (result.Any(t => t.Id == theme.Id))
                    throw new ThemeFormatException($"Identifiant de thème en double : « {theme.Id} ».");
                result.Add(theme);
            }
            catch (Exception ex) when (ex is ThemeFormatException or IOException or UnauthorizedAccessException)
            {
                onInvalid?.Invoke(file, ex);
            }
        }
        return Order(result);
    }

    /// <summary>Thème par défaut en tête, puis les autres par nom d'affichage (ordre stable).</summary>
    public static IReadOnlyList<ThemeDefinition> Order(IEnumerable<ThemeDefinition> themes) =>
        themes
            .OrderBy(t => t.Id == Settings.AppSettings.DefaultThemeId ? 0 : 1)
            .ThenBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(t => t.Id, StringComparer.Ordinal)
            .ToList();
}
