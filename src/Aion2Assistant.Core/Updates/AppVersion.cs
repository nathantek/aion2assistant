using System.Globalization;

namespace Aion2Assistant.Core.Updates;

/// <summary>Version SemVer 2 (majeure.mineure.correctif[-pré-version]).</summary>
public sealed record AppVersion(int Major, int Minor, int Patch, string? Pre) : IComparable<AppVersion>
{
    public bool IsPrerelease => Pre is not null;

    /// <summary>Accepte "0.3.0-beta.1", "v0.3.0", "1.2.3". Tout le reste donne false.</summary>
    public static bool TryParse(string? text, out AppVersion version)
    {
        version = new AppVersion(0, 0, 0, null);
        if (string.IsNullOrWhiteSpace(text)) return false;
        var s = text.Trim();
        if (s[0] is 'v' or 'V') s = s[1..];

        string? pre = null;
        var dash = s.IndexOf('-');
        if (dash >= 0)
        {
            pre = s[(dash + 1)..];
            s = s[..dash];
            if (!ValidPre(pre)) return false;
        }

        var parts = s.Split('.');
        if (parts.Length != 3) return false;
        if (!TryNum(parts[0], out var maj) || !TryNum(parts[1], out var min) || !TryNum(parts[2], out var pat)) return false;
        version = new AppVersion(maj, min, pat, pre);
        return true;
    }

    private static bool TryNum(string s, out int n)
    {
        n = 0;
        return s.Length > 0 && s.All(char.IsAsciiDigit) && int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out n);
    }

    private static bool ValidPre(string pre)
        => pre.Length > 0 && pre.Split('.').All(id => id.Length > 0 && id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'));

    public int CompareTo(AppVersion? other)
    {
        if (other is null) return 1;
        var c = Major.CompareTo(other.Major);
        if (c != 0) return c;
        c = Minor.CompareTo(other.Minor);
        if (c != 0) return c;
        c = Patch.CompareTo(other.Patch);
        if (c != 0) return c;
        if (Pre is null) return other.Pre is null ? 0 : 1;
        if (other.Pre is null) return -1;

        var a = Pre.Split('.');
        var b = other.Pre.Split('.');
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            var an = a[i].All(char.IsAsciiDigit);
            var bn = b[i].All(char.IsAsciiDigit);
            if (an && bn)
            {
                c = ComparePureNumbers(a[i], b[i]);
            }
            else if (an) c = -1;
            else if (bn) c = 1;
            else c = string.CompareOrdinal(a[i], b[i]);
            if (c != 0) return Math.Sign(c);
        }
        return a.Length.CompareTo(b.Length);
    }

    // Comparaison numérique sans débordement (zéros de tête ignorés).
    private static int ComparePureNumbers(string x, string y)
    {
        x = x.TrimStart('0');
        y = y.TrimStart('0');
        return x.Length != y.Length ? x.Length.CompareTo(y.Length) : string.CompareOrdinal(x, y);
    }

    public bool Equals(AppVersion? other)
        => other is not null && Major == other.Major && Minor == other.Minor && Patch == other.Patch && Pre == other.Pre;

    public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch, Pre);

    public override string ToString() => $"{Major}.{Minor}.{Patch}" + (Pre is null ? "" : "-" + Pre);
}
