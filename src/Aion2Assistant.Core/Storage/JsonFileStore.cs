using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aion2Assistant.Core.Storage;

public enum LoadSource { File, Backup, Defaults }

public sealed record LoadResult<T>(T Value, string? Error, LoadSource Source);

/// <summary>
/// Lit et écrit des fichiers JSON. Chaque lecture réussie copie le fichier en <c>.bak</c> ;
/// si le fichier est illisible, on se rabat sur la copie, puis sur les valeurs par défaut.
/// </summary>
public static class JsonFileStore
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string BackupPath(string path) => path + ".bak";

    public static LoadResult<T> Load<T>(string path, Func<T> defaults) where T : class
    {
        string? error = null;
        if (File.Exists(path))
        {
            if (TryRead<T>(path, out var value, out error))
            {
                TryCopy(path, BackupPath(path));
                return new LoadResult<T>(value!, null, LoadSource.File);
            }
        }

        var backup = BackupPath(path);
        if (File.Exists(backup) && TryRead<T>(backup, out var fromBackup, out _))
            return new LoadResult<T>(fromBackup!, error, LoadSource.Backup);

        return new LoadResult<T>(defaults(), error, LoadSource.Defaults);
    }

    public static void Save<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, Options));
        File.Move(temp, path, overwrite: true);
    }

    private static bool TryRead<T>(string path, out T? value, out string? error) where T : class
    {
        try
        {
            value = JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options);
            error = value is null ? $"{Path.GetFileName(path)} : contenu vide" : null;
            return value is not null;
        }
        catch (JsonException ex)
        {
            value = null;
            error = $"{Path.GetFileName(path)} ligne {ex.LineNumber + 1} : {ex.Message}";
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            value = null;
            error = $"{Path.GetFileName(path)} illisible : {ex.Message}";
            return false;
        }
    }

    private static void TryCopy(string from, string to)
    {
        try { File.Copy(from, to, overwrite: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
