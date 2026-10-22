using System.Text.Json;
using Aion2Assistant.Core.Events;
using Aion2Assistant.Core.Localization;

namespace Aion2Assistant.Combat;

/// <summary>Noms de skills, de PNJ et de familiers chargés depuis des fichiers JSON optionnels.</summary>
public sealed class GameData
{
    private readonly IReadOnlyDictionary<int, string> _skills;
    private readonly IReadOnlyDictionary<int, string> _npcs;
    private readonly IReadOnlyDictionary<int, string> _aliases;
    private readonly IReadOnlyDictionary<int, PetName> _pets;

    public int SkillCount => _skills.Count;

    public int NpcCount => _npcs.Count;

    public int FamiliarCount => _pets.Count;

    public static GameData Empty { get; } = new(new Dictionary<int, string>(), new Dictionary<int, string>());

    public GameData(IReadOnlyDictionary<int, string> skills, IReadOnlyDictionary<int, string> npcs,
        IReadOnlyDictionary<int, string>? aliases = null, IReadOnlyDictionary<int, PetName>? pets = null)
    {
        _skills = skills;
        _npcs = npcs;
        _aliases = aliases ?? new Dictionary<int, string>();
        _pets = pets ?? new Dictionary<int, PetName>();
    }

    public static GameData Load(string directory)
    {
        try
        {
            return new GameData(ReadTable(Path.Combine(directory, "skills.json")), ReadTable(Path.Combine(directory, "npcs.json")),
                ReadTable(Path.Combine(directory, "skill-aliases.json")), ReadPets(Path.Combine(directory, "pets.json")));
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

    /// <summary>
    /// pets.json : { "1084": { "name": "…", "category": "…" } }, catégorie facultative ; une valeur texte seule
    /// est aussi acceptée comme nom. Fichier absent ou illisible → table vide (noms de repli).
    /// </summary>
    internal static Dictionary<int, PetName> ReadPets(string path)
    {
        var table = new Dictionary<int, PetName>();
        try
        {
            if (!File.Exists(path)) return table;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return table;
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                if (!int.TryParse(p.Name, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var key)) continue;
                if (p.Value.ValueKind == JsonValueKind.String)
                {
                    var text = p.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(text)) table[key] = new PetName(text, null);
                }
                else if (p.Value.ValueKind == JsonValueKind.Object
                    && p.Value.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(name.GetString()))
                {
                    var category = p.Value.TryGetProperty("category", out var c) && c.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(c.GetString()) ? c.GetString() : null;
                    table[key] = new PetName(name.GetString()!, category);
                }
            }
        }
        catch
        {
            return new Dictionary<int, PetName>();
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
        Deaths = s.Deaths.Select(Rename).ToList(),
    };

    // Les récaps ne gardent que le texte : seul « Skill N » contient encore le code de façon fiable.
    private DeathRecap Rename(DeathRecap d) => d with
    {
        Lines = d.Lines.Select(l => TryParseSkillN(l.Skill, out var code) ? l with { Skill = SkillName(code) } : l).ToList(),
    };

    private static bool TryParseSkillN(string text, out int code)
    {
        code = 0;
        return text.StartsWith("Skill ", StringComparison.Ordinal)
            && int.TryParse(text.AsSpan(6), System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out code);
    }

    private MeterSnapshot Rename(MeterSnapshot m) => m with
    {
        Actors = m.Actors.Select(a => a with { Skills = a.Skills.Select(Rename).ToList() }).ToList(),
    };

    private SkillRow Rename(SkillRow r) =>
        r.Name == $"Skill {r.SkillCode}" || r.Name == FallbackName(r.SkillCode) ? r with { Name = SkillName(r.SkillCode) } : r;

    public string NpcName(int npcCode) => _npcs.TryGetValue(npcCode, out var n) ? n : Strings.Format(nameof(Strings.NpcFallback), npcCode);

    /// <summary>Nom anglais de pets.json, sinon « Familier #1234 » / « Familiar #1234 ».</summary>
    public string FamiliarName(int id) =>
        _pets.TryGetValue(id, out var p) ? p.Name : Strings.Format(nameof(Strings.FamiliarFallback), id);

    /// <summary>Catégorie de pets.json, ou null si inconnue.</summary>
    public string? FamiliarCategory(int id) => _pets.TryGetValue(id, out var p) ? p.Category : null;
}

/// <summary>Entrée de pets.json : nom anglais et catégorie (facultative).</summary>
public sealed record PetName(string Name, string? Category);

/// <summary>Noms de cartes chargés depuis <c>maps.json</c> (facultatif).</summary>
public static class MapNames
{
    public static Func<int, string> Load(string directory)
    {
        var table = GameData.ReadTable(Path.Combine(directory, "maps.json"));
        return id => id == 0 ? Strings.ZoneUnknown : table.TryGetValue(id, out var n) ? n : Strings.Format(nameof(Strings.MapFallback), id);
    }
}
