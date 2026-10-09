using Aion2Assistant.Core.Events;

namespace Aion2Assistant.Protocol;

public static class GameClasses
{
    /// <summary>Classe sur le câble : 4 valeurs consécutives par classe.</summary>
    public static GameClass FromWire(uint value) => value switch
    {
        >= 5 and <= 8 => GameClass.Gladiator,
        >= 9 and <= 12 => GameClass.Templar,
        >= 13 and <= 16 => GameClass.Ranger,
        >= 17 and <= 20 => GameClass.Assassin,
        >= 21 and <= 24 => GameClass.Elementalist,
        >= 25 and <= 28 => GameClass.Sorcerer,
        >= 29 and <= 32 => GameClass.Cleric,
        >= 33 and <= 36 => GameClass.Chanter,
        >= 45 and <= 48 => GameClass.Brawler,
        _ => GameClass.Unknown,
    };
}
