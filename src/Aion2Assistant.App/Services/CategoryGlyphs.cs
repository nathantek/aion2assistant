namespace Aion2Assistant.App.Services;

/// <summary>Glyphes Segoe MDL2 par catégorie de timer.</summary>
internal static class CategoryGlyphs
{
    public static string For(string category) => category switch
    {
        "rift" => "",    // éclair
        "reset" => "",   // rafraîchir
        "dungeon" => "", // repère
        "boss" => "",    // avertissement
        "fieldboss" => "", // drapeau
        _ => "",         // chronomètre
    };
}
