using System.Windows.Media;
using Aion2Assistant.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aion2Assistant.App.ViewModels;

/// <summary>Onglet pas encore implémenté (DPS, Reçus, Soins, Historique) : on affiche à quelle étape il arrive.</summary>
public sealed class PlaceholderViewModel : ObservableObject
{
    private readonly string _accentKey;

    public PlaceholderViewModel(string title, string message, string glyph, string accentKey)
    {
        Title = title;
        Message = message;
        Glyph = glyph;
        _accentKey = accentKey;
        // Les brosses de thème sont remplacées à chaque changement. Abonnement jamais retiré (vie = celle de l'app).
        ThemeManager.ThemeChanged += () => OnPropertyChanged(nameof(Accent));
    }

    public string Title { get; }
    public string Message { get; }
    public string Glyph { get; }
    public Brush Accent => AppHost.Resource(_accentKey);
}
