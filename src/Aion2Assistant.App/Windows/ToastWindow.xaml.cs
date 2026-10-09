using System.Windows;
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
