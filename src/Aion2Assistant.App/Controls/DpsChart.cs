using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Aion2Assistant.App.ViewModels;

namespace Aion2Assistant.App.Controls;

/// <summary>Courbe des DPS lissés du groupe dans le temps, dessinée à la main (une polyligne par acteur).</summary>
public sealed class DpsChart : FrameworkElement
{
    public static readonly DependencyProperty SeriesProperty = DependencyProperty.Register(
        nameof(Series), typeof(IReadOnlyList<ChartSeries>), typeof(DpsChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty DurationMsProperty = DependencyProperty.Register(
        nameof(DurationMs), typeof(long), typeof(DpsChart),
        new FrameworkPropertyMetadata(0L, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<ChartSeries>? Series
    {
        get => (IReadOnlyList<ChartSeries>?)GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    public long DurationMs
    {
        get => (long)GetValue(DurationMsProperty);
        set => SetValue(DurationMsProperty, value);
    }

    private const double MarginTop = 6, MarginRight = 4, MarginBottom = 16, LabelGap = 4;

    /// <summary>Plus petit pas 1/2/5 × 10^k (au moins 1) donnant au plus 5 intervalles jusqu'au maximum ; le maximum arrondi en découle.</summary>
    internal static (double Step, double Max) NiceAxis(double rawMax)
    {
        if (rawMax <= 0) return (1, 1);
        for (var pow = 1.0; ; pow *= 10)
        {
            foreach (var m in new[] { 1.0, 2.0, 5.0 })
            {
                var step = m * pow;
                var n = Math.Ceiling(rawMax / step - 1e-9);
                if (n <= 5) return (step, Math.Max(1, n) * step);
            }
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        var all = Series;
        if (all is null || all.Count == 0 || ActualWidth < 20 || ActualHeight < 20) return;

        var muted = AppHost.Resource("TextMutedBrush");
        var divider = AppHost.Resource("DividerBrush");
        var typeface = new Typeface((FontFamily)Application.Current.Resources["UiFont"],
            FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var size = (double)Application.Current.Resources["FontSizeSmall"];
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        FormattedText Text(string s) => new(s, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, size, muted, dpi);

        // Seules les courbes visibles comptent, y compris pour l'échelle de l'axe Y
        var series = all.Where(s => s.IsVisible).ToList();
        var smoothedMax = series.Count == 0 ? 0 : series.Max(s => s.Smoothed.Length == 0 ? 0 : s.Smoothed.Max());
        var (step, max) = NiceAxis(smoothedMax);
        var intervals = (int)Math.Round(max / step);

        // Graduations : « /s » uniquement sur la plus haute
        var ticks = new List<(double Value, FormattedText Label)>();
        for (var i = 0; i <= intervals; i++)
        {
            var value = i == intervals ? max : i * step;
            ticks.Add((value, Text(MeterFormat.Compact(value) + (i == intervals ? "/s" : ""))));
        }

        var left = ticks.Max(t => t.Label.Width) + LabelGap + 2;
        var plotW = ActualWidth - left - MarginRight;
        var plotH = ActualHeight - MarginTop - MarginBottom;
        if (plotW < 10 || plotH < 10) return;
        var bottom = MarginTop + plotH;

        // Grille horizontale (pointillée, sauf la ligne de base) et libellés de l'axe Y
        var gridPen = new Pen(divider, 1) { DashStyle = DashStyles.Dash };
        foreach (var (value, label) in ticks)
        {
            var y = bottom - value / max * plotH;
            var py = Math.Round(y) + 0.5; // alignement sur les pixels
            dc.DrawLine(value == 0 ? new Pen(divider, 1) : gridPen, new Point(left, py), new Point(left + plotW, py));
            var ly = Math.Clamp(y - label.Height / 2, 0, ActualHeight - MarginBottom - label.Height + MarginTop);
            dc.DrawText(label, new Point(left - LabelGap - label.Width, ly));
        }

        // Libellés de l'axe X : 0, moitié, fin
        var textTop = bottom + 2;
        var t0 = Text(MeterFormat.Duration(0));
        var tm = Text(MeterFormat.Duration(DurationMs / 2));
        var te = Text(MeterFormat.Duration(DurationMs));
        dc.DrawText(t0, new Point(left, textTop));
        dc.DrawText(tm, new Point(left + plotW / 2 - tm.Width / 2, textTop));
        dc.DrawText(te, new Point(left + plotW - te.Width, textTop));

        // Le joueur est dessiné en dernier, au-dessus des autres
        foreach (var s in series.Where(s => !s.IsSelf).Concat(series.Where(s => s.IsSelf)))
            DrawSeries(dc, s, left, plotW, bottom, plotH, max);
    }

    private static void DrawSeries(DrawingContext dc, ChartSeries s, double left, double plotW, double bottom, double plotH, double max)
    {
        var data = Reduce(s.Smoothed, (int)plotW);
        if (data.Length == 0) return;
        var pen = new Pen(s.Brush, s.IsSelf ? 2.5 : 1.5)
        {
            LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round,
        };
        // Même classe qu'une autre courbe : tirets puis pointillés (les points ont besoin d'un bout rond pour être visibles).
        if (s.DashIndex == 1) pen.DashStyle = DashStyles.Dash;
        else if (s.DashIndex >= 2)
        {
            pen.DashStyle = DashStyles.Dot;
            pen.DashCap = PenLineCap.Round;
        }
        double Y(double v) => bottom - Math.Clamp(v / max, 0, 1) * plotH;

        if (data.Length == 1)
        {
            // Un seul point : courte ligne horizontale
            dc.DrawLine(pen, new Point(left, Y(data[0])), new Point(left + Math.Min(plotW, 24), Y(data[0])));
            return;
        }

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            for (var i = 0; i < data.Length; i++)
            {
                var p = new Point(left + plotW * i / (data.Length - 1), Y(data[i]));
                if (i == 0) ctx.BeginFigure(p, false, false);
                else ctx.LineTo(p, true, true);
            }
        }
        geo.Freeze();
        dc.DrawGeometry(null, pen, geo);
    }

    /// <summary>Limite la série à ~maxPoints valeurs : une case par colonne de pixels, on garde le maximum pour préserver les pics.</summary>
    private static double[] Reduce(double[] data, int maxPoints)
    {
        maxPoints = Math.Max(2, maxPoints);
        if (data.Length <= maxPoints) return data;
        var result = new double[maxPoints];
        for (var k = 0; k < maxPoints; k++)
        {
            var from = (int)((long)k * data.Length / maxPoints);
            var to = Math.Max(from + 1, (int)((long)(k + 1) * data.Length / maxPoints));
            var max = data[from];
            for (var i = from + 1; i < to; i++) if (data[i] > max) max = data[i];
            result[k] = max;
        }
        result[^1] = data[^1]; // le dernier point reste exact (fin de courbe)
        return result;
    }
}
