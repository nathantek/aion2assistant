using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Aion2Assistant.App.ViewModels;
using Aion2Assistant.App.Views;
using Serilog;

namespace Aion2Assistant.App.Services;

/// <summary>Rend la vue du meter hors écran (sans défilement) en image, pour la copie dans le presse-papiers.</summary>
internal static class ShareImageRenderer
{
    public const double Width = 360, Dpi = 192;

    /// <summary>Rend la carte (hors écran, sans défilement) liée à ce view-model ; null si le rendu échoue (logué).</summary>
    public static BitmapSource? Render(MeterViewModel vm)
    {
        try
        {
            var card = new ShareCardView { DataContext = vm, Width = Width };
            card.Measure(new Size(Width, double.PositiveInfinity));
            card.Arrange(new Rect(card.DesiredSize));
            card.UpdateLayout();

            var bitmap = new RenderTargetBitmap(
                (int)Math.Ceiling(card.DesiredSize.Width * Dpi / 96),
                (int)Math.Ceiling(card.DesiredSize.Height * Dpi / 96),
                Dpi, Dpi, PixelFormats.Pbgra32);
            bitmap.Render(card);
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Impossible de rendre l'image du tableau");
            return null;
        }
    }
}
