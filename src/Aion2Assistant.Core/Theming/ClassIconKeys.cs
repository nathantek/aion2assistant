using Aion2Assistant.Core.Events;

namespace Aion2Assistant.Core.Theming;

/// <summary>Clé de ressource de l'icône d'une classe (Theme/ClassIcons.xaml de l'App).</summary>
public static class ClassIconKeys
{
    public const string Unknown = "ClassIcon.Unknown";

    /// <summary>« ClassIcon.&lt;Classe&gt; » ; <see cref="Unknown"/> pour Unknown, Brawler (absent du jeu) et toute valeur hors liste.</summary>
    public static string For(GameClass c) => c switch
    {
        GameClass.Gladiator => "ClassIcon.Gladiator",
        GameClass.Templar => "ClassIcon.Templar",
        GameClass.Ranger => "ClassIcon.Ranger",
        GameClass.Assassin => "ClassIcon.Assassin",
        GameClass.Elementalist => "ClassIcon.Elementalist",
        GameClass.Sorcerer => "ClassIcon.Sorcerer",
        GameClass.Cleric => "ClassIcon.Cleric",
        GameClass.Chanter => "ClassIcon.Chanter",
        _ => Unknown,
    };
}
