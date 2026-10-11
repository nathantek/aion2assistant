using System.Text.Json;
using Aion2Assistant.Core.Events;

namespace Aion2Assistant.Combat;

/// <summary>Noms de skills et de PNJ chargés depuis des fichiers JSON optionnels.</summary>
public sealed class GameData
{
    private readonly IReadOnlyDictionary<int, string> _skills;
    private readonly IReadOnlyDictionary<int, string> _npcs;
    private readonly IReadOnlyDictionary<int, string> _aliases;

    public int SkillCount => _skills.Count;

    public int NpcCount => _npcs.Count;

    public static GameData Empty { get; } = new(new Dictionary<int, string>(), new Dictionary<int, string>());

    public GameData(IReadOnlyDictionary<int, string> skills, IReadOnlyDictionary<int, string> npcs,
        IReadOnlyDictionary<int, string>? aliases = null)
    {
        _skills = skills;
        _npcs = npcs;
        _aliases = aliases ?? new Dictionary<int, string>();
    }

    public static GameData Load(string directory)
    {
        try
        {
            return new GameData(ReadTable(Path.Combine(directory, "skills.json")), ReadTable(Path.Combine(directory, "npcs.json")),
                ReadTable(Path.Combine(directory, "skill-aliases.json")));
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

    /// <summary>Ordre : skills.json, puis alias maintenus à la main, puis repli lisible selon la plage du code.</summary>
    public string SkillName(int normalizedCode)
    {
        if (_skills.TryGetValue(normalizedCode, out var n)) return n;
        if (_aliases.TryGetValue(normalizedCode, out var a)) return a;
        return FallbackName(normalizedCode);
    }

    internal static string FallbackName(int code)
    {
        var cls = SkillCodes.ClassOf(code);
        if (cls != GameClass.Unknown) return $"{cls} skill {(code / 10_000) % 100:00}";
        if (code is >= 30_000_000 and <= 30_999_999) return $"Divine Stone {code / 10}";
        if (code is >= 1_000_000 and <= 1_999_999) return $"NPC skill {code}";
        if (code is >= 2_000_000 and <= 2_999_999 or >= 4_000_000 and <= 4_999_999) return $"Item {code}";
        return $"Skill {code}";
    }

    /// <summary>
    /// Les anciens combats stockent « Skill N » ou le libellé de repli de leur version : on ré-résout ces noms depuis
    /// SkillCode (sans migration de la base), pour qu'un alias ajouté plus tard s'applique aussi à eux.
    /// Les noms déjà utiles (connus au moment de l'enregistrement) sont conservés.
    /// </summary>
    public EncounterSnapshot Rename(EncounterSnapshot s) => s with
    {
        Dps = Rename(s.Dps),
        Heal = Rename(s.Heal),
        Taken = Rename(s.Taken),
        BossDps = s.BossDps is null ? null : Rename(s.BossDps),
    };

    private MeterSnapshot Rename(MeterSnapshot m) => m with
    {
        Actors = m.Actors.Select(a => a with { Skills = a.Skills.Select(Rename).ToList() }).ToList(),
    };

    private SkillRow Rename(SkillRow r) =>
        r.Name == $"Skill {r.SkillCode}" || r.Name == FallbackName(r.SkillCode) ? r with { Name = SkillName(r.SkillCode) } : r;

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
