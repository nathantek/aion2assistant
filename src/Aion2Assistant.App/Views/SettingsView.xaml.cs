using System.Windows;

namespace Aion2Assistant.App.Views;

public partial class SettingsView
{
    public SettingsView() => InitializeComponent();

    /// <summary>Puce de section : fait défiler jusqu'au titre ciblé (Tag).</summary>
    private void OnSectionChip(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not FrameworkElement target) return;
        Scroller.ScrollToVerticalOffset(Scroller.VerticalOffset + target.TranslatePoint(new Point(0, 0), Scroller).Y);
    }
}
