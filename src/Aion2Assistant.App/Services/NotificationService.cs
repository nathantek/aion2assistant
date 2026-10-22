using System.Windows;
using System.Windows.Media;
using Aion2Assistant.App.ViewModels;
using Aion2Assistant.App.Windows;

namespace Aion2Assistant.App.Services;

/// <summary>Pop-ups discrètes empilées sous l'overlay, 3 au maximum (la plus ancienne part en premier), dont au plus une compacte.</summary>
internal sealed class NotificationService(Window owner, MainViewModel main)
{
    private const int MaxToasts = 3;
    private const double Gap = 6;
    private readonly List<ToastWindow> _open = new();

    private static readonly TimeSpan CompactLifetime = TimeSpan.FromSeconds(3);
    private ToastWindow? _compact;

    public void Show(string title, string message, Brush accent, string glyph) =>
        Add(new ToastWindow(title, message, accent, glyph, main.UiScale));

    /// <summary>
    /// Pop-up compacte d'une ligne (3 s, sans son). Une seule à la fois : si elle est encore visible, son texte est
    /// remplacé et sa durée relancée au lieu d'empiler.
    /// </summary>
    public void ShowCompact(string text, Brush accent, string glyph)
    {
        if (_compact is { } current && current.TryUpdate(text))
        {
            Relayout();
            return;
        }
        var toast = ToastWindow.Compact(text, accent, glyph, main.UiScale, CompactLifetime);
        toast.Closed += (_, _) =>
        {
            if (_compact == toast) _compact = null;
        };
        _compact = toast;
        Add(toast);
    }

    private void Add(ToastWindow toast)
    {
        if (_open.Count >= MaxToasts) _open[0].Close();

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
