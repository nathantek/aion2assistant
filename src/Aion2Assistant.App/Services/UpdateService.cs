using System.Net.Http;
using System.Reflection;
using System.Windows.Threading;
using Aion2Assistant.Core.Updates;
using Serilog;

namespace Aion2Assistant.App.Services;

/// <summary>
/// Application non installée (build de dev, ancien zip) : une seule vérification de nouvelle version, 10 s après
/// le démarrage, en arrière-plan, pour guider vers Setup.exe. Installée : voir <see cref="UpdateController"/>.
/// </summary>
internal sealed class UpdateService
{
    private const string ReleasesUrl = "https://api.github.com/repos/nathantek/aion2assistant/releases?per_page=10";
    private const string FallbackUrl = "https://github.com/nathantek/aion2assistant/releases";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(10) };
    private readonly Func<bool> _isEnabled;
    private readonly Func<bool> _betaChannel;
    private bool _started;

    /// <param name="betaChannel">Canal bêta : pré-versions comprises ; lu au moment de la vérification.</param>
    public UpdateService(Func<bool>? isEnabled = null, Func<bool>? betaChannel = null)
    {
        _isEnabled = isEnabled ?? (() => true);
        _betaChannel = betaChannel ?? (() => false);
        CurrentVersionText = ReadVersionText();
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            if (!_isEnabled()) return;
            _ = CheckAsync();
        };
    }

    /// <summary>Version courante affichée (sans le suffixe +hash éventuel).</summary>
    public string CurrentVersionText { get; }
    public string? AvailableVersion { get; private set; }
    public string? AvailableUrl { get; private set; }
    /// <summary>Vrai une fois la vérification terminée sans erreur.</summary>
    public bool Checked { get; private set; }

    public event Action? Changed;
    /// <summary>Levé sur le thread UI quand une version plus récente est trouvée : version, URL.</summary>
    public event Action<string, string>? UpdateFound;

    public void Start()
    {
        if (_started) return;
        _started = true;
        _timer.Start();
    }

    /// <summary>InformationalVersion de l'application, sans le suffixe +hash.</summary>
    internal static string ReadVersionText()
    {
        var assembly = typeof(UpdateService).Assembly;
        var info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                   ?? assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        var plus = info.IndexOf('+');
        return plus >= 0 ? info[..plus] : info;
    }

    private async Task CheckAsync()
    {
        try
        {
            if (!AppVersion.TryParse(CurrentVersionText, out var current)) return;
            var beta = _betaChannel();
            var found = await Task.Run(async () =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, ReleasesUrl);
                request.Headers.UserAgent.ParseAdd("Aion2Assistant/" + CurrentVersionText);
                request.Headers.Accept.ParseAdd("application/vnd.github+json");
                using var response = await Http.SendAsync(request).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return UpdateCheck.Newer(UpdateCheck.ParseReleases(json), current, beta);
            });
            Checked = true;
            if (found is not null)
            {
                AvailableVersion = found.Tag.TrimStart('v', 'V');
                AvailableUrl = string.IsNullOrWhiteSpace(found.Url) ? FallbackUrl : found.Url;
                UpdateFound?.Invoke(AvailableVersion, AvailableUrl);
            }
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Vérification de mise à jour échouée");
        }
    }
}
