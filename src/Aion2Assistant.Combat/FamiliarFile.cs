using Aion2Assistant.Core.Storage;

namespace Aion2Assistant.Combat;

/// <summary>Un familier connu : palier 0..3, âmes du palier en cours, dernière âme ou montée de palier (ms Unix, 0 = jamais).</summary>
public sealed class FamiliarRecord
{
    public int Tier { get; set; }
    public int Souls { get; set; }
    public long LastUpdateMs { get; set; }
}

/// <summary>Familiers d'un serveur (partagés par tous ses personnages).</summary>
public sealed class FamiliarServerState
{
    /// <summary>Dernier état complet (0x0090) reçu, ms Unix ; 0 = jamais.</summary>
    public long LastSyncMs { get; set; }

    /// <summary>Identifiant du familier → état.</summary>
    public Dictionary<int, FamiliarRecord> Entries { get; set; } = new();
}

/// <summary>Contenu de familiars.json.</summary>
public sealed class FamiliarFile
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>Dernier serveur identifié : affiché au démarrage, avant que le personnage soit détecté.</summary>
    public int LastServerId { get; set; }

    /// <summary>Serveur → familiers.</summary>
    public Dictionary<int, FamiliarServerState> Servers { get; set; } = new();
}

/// <summary>Lecture et écriture de familiars.json (écriture atomique, repli sur le .bak, puis fichier vide).</summary>
public static class FamiliarStore
{
    public const string FileName = "familiars.json";

    /// <summary>Ne lève jamais : une erreur (fichier corrompu, illisible) est renvoyée dans <see cref="LoadResult{T}.Error"/>, à journaliser.</summary>
    public static LoadResult<FamiliarFile> Load(string path)
    {
        try
        {
            var loaded = JsonFileStore.Load(path, () => new FamiliarFile());
            return loaded.Value is null ? loaded with { Value = new FamiliarFile() } : loaded;
        }
        catch (Exception ex)
        {
            return new LoadResult<FamiliarFile>(new FamiliarFile(), $"{FileName} illisible : {ex.Message}", LoadSource.Defaults);
        }
    }

    public static void Save(string path, FamiliarFile file)
    {
        file.Version = FamiliarFile.CurrentVersion;
        JsonFileStore.Save(path, file);
    }
}
