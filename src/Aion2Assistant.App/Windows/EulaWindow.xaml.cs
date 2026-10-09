using System.Windows;
using Serilog;

namespace Aion2Assistant.App.Windows;

/// <summary>Fenêtre du CLUF : mode acceptation (J'accepte / Quitter) ou lecture seule (Fermer).</summary>
public partial class EulaWindow : Window
{
    public const string MissingText = "CLUF introuvable : réinstalle l'application.";

    private readonly bool _readOnly;

    public EulaWindow(bool readOnly)
    {
        InitializeComponent();
        _readOnly = readOnly;
        var text = Load();
        EulaText.Text = text ?? MissingText;
        if (readOnly)
        {
            Topmost = false;
            AcceptButton.Visibility = Visibility.Collapsed;
            QuitButton.Content = "Fermer";
            return;
        }
        AcceptButton.IsEnabled = text is not null;
    }

    private static string? Load()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "legal", "CLUF.txt");
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning(ex, "CLUF illisible");
            return null;
        }
    }

    private void OnAccept(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnQuit(object sender, RoutedEventArgs e)
    {
        if (_readOnly) Close();
        else DialogResult = false;
    }
}
