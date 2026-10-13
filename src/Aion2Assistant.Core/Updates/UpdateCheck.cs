using System.Text.Json;

namespace Aion2Assistant.Core.Updates;

public sealed record ReleaseInfo(string Tag, string Url, bool Prerelease, bool Draft);

/// <summary>Logique pure de détection de mise à jour à partir de l'API releases de GitHub.</summary>
public static class UpdateCheck
{
    public static IReadOnlyList<ReleaseInfo> ParseReleases(string githubJson)
    {
        var result = new List<ReleaseInfo>();
        try
        {
            using var doc = JsonDocument.Parse(githubJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return result;
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.Object) continue;
                var tag = Str(e, "tag_name");
                if (tag is null || !AppVersion.TryParse(tag, out _)) continue;
                result.Add(new ReleaseInfo(tag, Str(e, "html_url") ?? "", Bool(e, "prerelease"), Bool(e, "draft")));
            }
        }
        catch (JsonException)
        {
            return new List<ReleaseInfo>();
        }
        return result;
    }

    private static string? Str(JsonElement e, string name)
        => e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static bool Bool(JsonElement e, string name)
        => e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.True;

    /// <summary>
    /// Release la plus récente au-delà de <paramref name="current"/>. Canal stable
    /// (<paramref name="includePrereleases"/> faux) : pré-versions ignorées, qu'elles soient marquées
    /// comme telles sur GitHub ou qu'elles portent un suffixe. Jamais de version inférieure.
    /// </summary>
    public static ReleaseInfo? Newer(IEnumerable<ReleaseInfo> releases, AppVersion current, bool includePrereleases)
    {
        ReleaseInfo? best = null;
        AppVersion? bestVersion = null;
        foreach (var r in releases)
        {
            if (r.Draft) continue;
            if (r.Prerelease && !includePrereleases) continue;
            if (!AppVersion.TryParse(r.Tag, out var v)) continue;
            if (v.IsPrerelease && !includePrereleases) continue;
            if (v.CompareTo(current) <= 0) continue;
            if (bestVersion is null || v.CompareTo(bestVersion) > 0) { best = r; bestVersion = v; }
        }
        return best;
    }
}
