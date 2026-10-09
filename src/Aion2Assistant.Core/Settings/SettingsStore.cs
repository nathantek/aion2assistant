using Aion2Assistant.Core.Storage;

namespace Aion2Assistant.Core.Settings;

public sealed class SettingsStore(string filePath)
{
    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Aion2Assistant");

    public string FilePath => filePath;

    public LoadResult<AppSettings> Load()
    {
        var result = JsonFileStore.Load(filePath, () => new AppSettings());
        result.Value.Normalize();
        return result;
    }

    public void Save(AppSettings settings) => JsonFileStore.Save(filePath, settings);
}
