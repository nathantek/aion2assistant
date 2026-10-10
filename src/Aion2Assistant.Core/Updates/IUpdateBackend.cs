namespace Aion2Assistant.Core.Updates;

/// <summary>
/// Accès au moteur de mise à jour (Velopack côté App, faux backend dans les tests).
/// L'implémentation mémorise l'information de la dernière vérification pour la réutiliser au téléchargement.
/// </summary>
public interface IUpdateBackend
{
    /// <summary>Vrai si l'application est installée par le moteur et peut donc se mettre à jour seule.</summary>
    bool IsInstalled { get; }

    /// <summary>
    /// Version déjà téléchargée sur le disque (lancement précédent) et pas encore installée, ou null.
    /// Lecture locale uniquement, sans réseau.
    /// </summary>
    AppVersion? DownloadedVersion { get; }

    /// <summary>Version plus récente disponible, ou null s'il n'y en a pas.</summary>
    Task<AppVersion?> CheckAsync(CancellationToken ct);

    /// <summary>Télécharge la version trouvée par le dernier <see cref="CheckAsync"/> (progression de 0 à 100).</summary>
    Task DownloadAsync(AppVersion version, Action<int>? progress, CancellationToken ct);

    /// <summary>Applique la mise à jour téléchargée puis relance l'application.</summary>
    void ApplyAndRestart();

    /// <summary>Applique la mise à jour téléchargée après la fermeture de l'application, sans relancer.</summary>
    void ApplyOnExit();
}
