using System.Globalization;
using Aion2Assistant.Core.Localization;
using Aion2Assistant.Core.Storage;

namespace Aion2Assistant.Core.Settings;

/// <param name="systemUiCulture">
/// Langue d'interface de Windows, lue au premier lancement pour choisir la langue de l'app
/// (par défaut <see cref="CultureInfo.CurrentUICulture"/>, à lire avant que l'app n'impose la sienne).
/// </param>
public sealed class SettingsStore(string filePath, Func<CultureInfo>? systemUiCulture = null)
{
    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Aion2Assistant");

    public string FilePath => filePath;

    public LoadResult<AppSettings> Load()
    {
        var firstLaunch = !File.Exists(filePath) && !File.Exists(JsonFileStore.BackupPath(filePath));
        var result = JsonFileStore.Load(filePath, () => new AppSettings());
        // Premier lancement seulement : un fichier illisible appartient à un utilisateur existant (migration v6).
        if (firstLaunch && result.Source == LoadSource.Defaults)
            result.Value.Language = AppLanguage.FromCulture((systemUiCulture ?? (() => CultureInfo.CurrentUICulture))());
        result.Value.Normalize();
        return result;
    }

    public void Save(AppSettings settings) => JsonFileStore.Save(filePath, settings);
}
