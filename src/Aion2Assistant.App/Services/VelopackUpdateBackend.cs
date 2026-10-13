using Aion2Assistant.Core.Updates;
using Serilog;
using Velopack;
using Velopack.Sources;

namespace Aion2Assistant.App.Services;

/// <summary>
/// Adaptateur fin entre l'orchestrateur (Core) et Velopack : releases GitHub (pré-versions incluses).
/// Aucun appel réseau tant que l'orchestrateur ne lance pas de cycle.
/// </summary>
internal sealed class VelopackUpdateBackend : IUpdateBackend
{
    private const string RepoUrl = "https://github.com/nathantek/aion2assistant";

    private readonly UpdateManager? _manager;
    private readonly object _gate = new();
    // Dernière information trouvée (jamais effacée par une vérification vide ou en échec).
    private UpdateInfo? _info;
    // Dernière version entièrement téléchargée : c'est elle qu'on applique.
    private VelopackAsset? _downloaded;
    private volatile bool _upToDate;
    // Version déjà annoncée en Information : les cycles suivants ne la répètent qu'en Debug.
    private AppVersion? _announced;

    public VelopackUpdateBackend()
    {
        try
        {
            _manager = new UpdateManager(new GithubSource(RepoUrl, null, true));
            IsInstalled = _manager.IsInstalled;
        }
        catch (Exception ex)
        {
            // Exécution hors installation (build de dev, ancien zip) : simplement pas de mise à jour intégrée.
            Log.Debug(ex, "Velopack indisponible");
            _manager = null;
            IsInstalled = false;
        }
    }

    public bool IsInstalled { get; }

    /// <summary>La dernière vérification a réussi sans trouver de version plus récente.</summary>
    public bool UpToDate => _upToDate;

    public AppVersion? DownloadedVersion
    {
        get
        {
            if (_manager is null || !IsInstalled) return null;
            var asset = PendingAsset(_manager);
            return asset is not null && AppVersion.TryParse(asset.Version.ToString(), out var v) ? v : null;
        }
    }

    /// <summary>À la fermeture : relancer l'application après l'installation (choix « Redémarrer »).</summary>
    public bool RestartAfterApply { get; set; }

    /// <summary>L'outil de mise à jour a été lancé : il installera la version à la fin du processus.</summary>
    public bool ApplyStarted { get; private set; }

    public async Task<AppVersion?> CheckAsync(CancellationToken ct)
    {
        var manager = Manager();
        ct.ThrowIfCancellationRequested();
        // « À jour » seulement après une vérification réussie : un échec efface l'indication.
        _upToDate = false;
        // Velopack ne prend pas de jeton ici : on abandonne l'attente si le cycle est annulé.
        var info = await manager.CheckForUpdatesAsync().WaitAsync(ct).ConfigureAwait(false);
        if (info is null)
        {
            _upToDate = true;
            Log.Debug("Mises à jour : aucune nouvelle version");
            return null;
        }
        var text = info.TargetFullRelease.Version.ToString();
        if (!AppVersion.TryParse(text, out var version))
        {
            Log.Debug("Mises à jour : version {Version} illisible, ignorée", text);
            return null;
        }
        bool first;
        lock (_gate)
        {
            _info = info;
            first = !Equals(_announced, version);
            _announced = version;
        }
        if (first) Log.Information("Mise à jour disponible : {Version}", version);
        else Log.Debug("Mise à jour toujours disponible : {Version}", version);
        return version;
    }

    public async Task DownloadAsync(AppVersion version, Action<int>? progress, CancellationToken ct)
    {
        var manager = Manager();
        UpdateInfo? info;
        lock (_gate) info = _info;
        if (info is null) throw new InvalidOperationException("Aucune mise à jour à télécharger.");
        Log.Information("Téléchargement de la mise à jour {Version}", version);
        await manager.DownloadUpdatesAsync(info, progress, ct).ConfigureAwait(false);
        lock (_gate) _downloaded = info.TargetFullRelease;
        Log.Information("Mise à jour {Version} téléchargée", version);
    }

    /// <summary>
    /// Prépare l'installation avec relance : l'outil de mise à jour attend la fin du processus (60 s au plus),
    /// l'appelant doit donc quitter l'application juste après.
    /// </summary>
    public void ApplyAndRestart() => Apply(restart: true);

    /// <summary>Installation silencieuse à la fermeture, avec relance seulement si l'utilisateur l'a demandée.</summary>
    public void ApplyOnExit() => Apply(RestartAfterApply);

    private void Apply(bool restart)
    {
        var manager = Manager();
        var asset = PendingAsset(manager);
        if (asset is null) throw new InvalidOperationException("Aucune mise à jour téléchargée.");
        manager.WaitExitThenApplyUpdates(asset, silent: true, restart: restart);
        ApplyStarted = true;
    }

    // Un cycle annulé en plein téléchargement ne doit pas faire perdre la version déjà prête.
    private VelopackAsset? PendingAsset(UpdateManager manager)
    {
        try
        {
            if (manager.UpdatePendingRestart is { } pending) return pending;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Mises à jour : lecture du paquet en attente impossible");
        }
        lock (_gate) return _downloaded;
    }

    private UpdateManager Manager()
        => _manager is not null && IsInstalled ? _manager : throw new InvalidOperationException("Application non installée.");
}
