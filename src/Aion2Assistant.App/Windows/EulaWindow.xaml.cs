using System.Windows;
using Aion2Assistant.App.Localization;
using Aion2Assistant.Core.Localization;
using Serilog;

namespace Aion2Assistant.App.Windows;

/// <summary>Fenêtre du CLUF : mode acceptation (J'accepte / Quitter) ou lecture seule (Fermer).</summary>
public partial class EulaWindow : Window
{
    public static string MissingText => UiStrings.EulaMissing;

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
            QuitButton.Content = UiStrings.EulaClose;
            return;
        }
        AcceptButton.IsEnabled = text is not null;
    }

    private static string? Load()
    {
        try
        {
            // Texte dans la langue de l'app ; la version française fait foi.
            var file = AppCulture.Language == AppLanguage.French ? "CLUF.txt" : "EULA.txt";
            var path = Path.Combine(AppContext.BaseDirectory, "legal", file);
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
