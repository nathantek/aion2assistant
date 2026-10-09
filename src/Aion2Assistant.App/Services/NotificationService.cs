using System.Windows;
using System.Windows.Media;
using Aion2Assistant.App.ViewModels;
using Aion2Assistant.App.Windows;

namespace Aion2Assistant.App.Services;

/// <summary>Pop-ups discrètes empilées sous l'overlay, 3 au maximum (la plus ancienne part en premier).</summary>
internal sealed class NotificationService(Window owner, MainViewModel main)
{
    private const int MaxToasts = 3;
    private const double Gap = 6;
    private readonly List<ToastWindow> _open = new();

    public void Show(string title, string message, Brush accent, string glyph)
    {
        if (_open.Count >= MaxToasts) _open[0].Close();

        var toast = new ToastWindow(title, message, accent, glyph, main.UiScale);
        toast.Closed += (_, _) =>
        {
            _open.Remove(toast);
            Relayout();
        };
        toast.SizeChanged += (_, _) => Relayout();
        _open.Add(toast);
        Relayout(); // positionne la nouvelle pop-up avant son affichage
        toast.Show();
    }

    private void Relayout()
    {
        var left = owner.Left;
        var top = owner.IsVisible ? owner.Top + owner.ActualHeight + Gap : owner.Top;
        foreach (var toast in _open)
        {
            toast.Left = left;
            toast.Top = top;
            top += toast.ActualHeight + Gap;
        }
    }
}
