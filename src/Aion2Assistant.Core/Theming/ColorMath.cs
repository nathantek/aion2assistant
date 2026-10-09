namespace Aion2Assistant.Core.Theming;

public static class ColorMath
{
    private static double Linear(byte channel)
    {
        var c = channel / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    /// <summary>Luminance relative WCAG 2.x.</summary>
    public static double RelativeLuminance(Rgb c) =>
        0.2126 * Linear(c.R) + 0.7152 * Linear(c.G) + 0.0722 * Linear(c.B);

    /// <summary>Rapport de contraste WCAG, indépendant de l'ordre des couleurs.</summary>
    public static double ContrastRatio(Rgb a, Rgb b)
    {
        var la = RelativeLuminance(a);
        var lb = RelativeLuminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    /// <summary>Superpose <paramref name="top"/> à l'opacité <paramref name="alpha"/> sur <paramref name="backdrop"/>.</summary>
    public static Rgb Composite(Rgb top, double alpha, Rgb backdrop)
    {
        static byte Mix(byte t, double a, byte b) =>
            // Arrondi préalable à 9 décimales : 1 - 0,9 n'est pas exact en binaire et ferait basculer les demi-valeurs (43,4999…).
            (byte)Math.Clamp(Math.Round(Math.Round(a * t + (1 - a) * b, 9), MidpointRounding.AwayFromZero), 0, 255);
        return new Rgb(Mix(top.R, alpha, backdrop.R), Mix(top.G, alpha, backdrop.G), Mix(top.B, alpha, backdrop.B));
    }

    /// <summary>Écart CIE76 (distance euclidienne dans Lab, blanc D65).</summary>
    public static double DeltaE76(Rgb a, Rgb b)
    {
        var (l1, a1, b1) = ToLab(a);
        var (l2, a2, b2) = ToLab(b);
        return Math.Sqrt((l1 - l2) * (l1 - l2) + (a1 - a2) * (a1 - a2) + (b1 - b2) * (b1 - b2));
    }

    private static (double L, double A, double B) ToLab(Rgb c)
    {
        var r = Linear(c.R);
        var g = Linear(c.G);
        var b = Linear(c.B);
        var x = (0.4124564 * r + 0.3575761 * g + 0.1804375 * b) / 0.95047;
        var y = 0.2126729 * r + 0.7151522 * g + 0.0721750 * b;
        var z = (0.0193339 * r + 0.1191920 * g + 0.9503041 * b) / 1.08883;
        static double F(double t) => t > 0.008856 ? Math.Cbrt(t) : 7.787 * t + 16.0 / 116.0;
        var fx = F(x);
        var fy = F(y);
        var fz = F(z);
        return (116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz));
    }
}
