using Aion2Assistant.Combat;
using Aion2Assistant.Core.Settings;

namespace Aion2Assistant.App;

internal static class AppPaths
{
    public static string DataDirectory => SettingsStore.DefaultDirectory;
    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");
    public static string SoundsDirectory => Path.Combine(DataDirectory, "sounds");
    public static string CapturesDirectory => Path.Combine(DataDirectory, "captures");
    public static string LogsDirectory => Path.Combine(DataDirectory, "logs");
    public static string HistoryDatabase => Path.Combine(DataDirectory, "history.db");
    public static string Bosses => Path.Combine(DataDirectory, "bosses.json");
    public static string Records => Path.Combine(DataDirectory, "records.json");
    public static string Familiars => Path.Combine(DataDirectory, FamiliarStore.FileName);
    public static string TimersFile => Path.Combine(AppContext.BaseDirectory, "timers.json");
}
