using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Aion2Assistant.App.Interop;

namespace Aion2Assistant.App.Windows;

public partial class ToastWindow : Window
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(8);
    private readonly DispatcherTimer _life = new() { Interval = Lifetime };
    private bool _closing;
    private bool _closed;

    /// <summary>
    /// Variante compacte : une seule ligne, plus petite, durée de vie courte. Réutilisable tant qu'elle est visible
    /// (<see cref="TryUpdate"/>) pour remplacer le texte au lieu d'empiler.
    /// </summary>
    public static ToastWindow Compact(string text, Brush accent, string glyph, double scale, TimeSpan lifetime)
    {
        var toast = new ToastWindow(text, "", accent, glyph, scale);
        toast._life.Interval = lifetime;
        toast.MessageText.Visibility = Visibility.Collapsed;
        toast.TitleText.SetResourceReference(TextBlock.FontSizeProperty, "FontSizeSmall");
        toast.TextPanel.Margin = new Thickness(0, 4, 10, 4);
        toast.GlyphText.SetResourceReference(TextBlock.FontSizeProperty, "FontSizeSmall");
        toast.GlyphText.Margin = new Thickness(8, 0, 6, 0);
        if (string.IsNullOrEmpty(glyph))
        {
            toast.GlyphText.Visibility = Visibility.Collapsed;
            toast.TextPanel.Margin = new Thickness(8, 4, 10, 4);
        }
        toast.RootBorder.Width = double.NaN;
        toast.RootBorder.MaxWidth = 260;
        return toast;
    }

    public ToastWindow(string title, string message, Brush accent, string glyph, double scale)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        GlyphText.Text = glyph;
        GlyphText.Foreground = accent;
        AccentBar.Background = accent;
        RootScale.ScaleX = scale;
        RootScale.ScaleY = scale;

        SourceInitialized += (_, _) => OverlayWindowHelper.MakeOverlay(this);
        Loaded += (_, _) =>
        {
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
            _life.Start();
        };
        Closed += (_, _) =>
        {
            _closed = true;
            _closing = true;
            _life.Stop();
        };
        _life.Tick += (_, _) => FadeOutAndClose();
        MouseLeftButtonUp += (_, _) => FadeOutAndClose();
    }

    /// <summary>Remplace le texte et relance la durée de vie ; faux si la pop-up est déjà en train de disparaître.</summary>
    public bool TryUpdate(string title)
    {
        if (_closed || _closing) return false;
        TitleText.Text = title;
        if (IsLoaded)
        {
            _life.Stop();
            _life.Start();
        }
        return true;
    }

    private void FadeOutAndClose()
    {
        if (_closed || _closing) return;
        _closing = true;
        _life.Stop();
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(220));
        fade.Completed += (_, _) =>
        {
            if (!_closed) Close();
        };
        BeginAnimation(OpacityProperty, fade);
    }
}
