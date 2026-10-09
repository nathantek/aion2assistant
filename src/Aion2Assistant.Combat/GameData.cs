using System.Text.Json;

namespace Aion2Assistant.Combat;

/// <summary>Noms de skills et de PNJ chargés depuis des fichiers JSON optionnels.</summary>
public sealed class GameData
{
    private readonly IReadOnlyDictionary<int, string> _skills;
    private readonly IReadOnlyDictionary<int, string> _npcs;

    public int SkillCount => _skills.Count;

    public int NpcCount => _npcs.Count;

    public static GameData Empty { get; } = new(new Dictionary<int, string>(), new Dictionary<int, string>());

    public GameData(IReadOnlyDictionary<int, string> skills, IReadOnlyDictionary<int, string> npcs)
    {
        _skills = skills;
        _npcs = npcs;
    }

    public static GameData Load(string directory)
    {
        try
        {
            return new GameData(ReadTable(Path.Combine(directory, "skills.json")), ReadTable(Path.Combine(directory, "npcs.json")));
        }
        catch
        {
            return Empty;
        }
    }

    internal static Dictionary<int, string> ReadTable(string path)
    {
        var table = new Dictionary<int, string>();
        try
        {
            if (!File.Exists(path)) return table;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return table;
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                if (p.Value.ValueKind == JsonValueKind.String && int.TryParse(p.Name, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var key))
                    table[key] = p.Value.GetString() ?? "";
            }
        }
        catch
        {
            return new Dictionary<int, string>();
        }
        return table;
    }

    public string SkillName(int normalizedCode) => _skills.TryGetValue(normalizedCode, out var n) ? n : $"Skill {normalizedCode}";

    public string NpcName(int npcCode) => _npcs.TryGetValue(npcCode, out var n) ? n : $"PNJ {npcCode}";
}

/// <summary>Noms de cartes chargés depuis <c>maps.json</c> (facultatif).</summary>
public static class MapNames
{
    public static Func<int, string> Load(string directory)
    {
        var table = GameData.ReadTable(Path.Combine(directory, "maps.json"));
        return id => id == 0 ? "Zone inconnue" : table.TryGetValue(id, out var n) ? n : $"Carte {id}";
    }
}
