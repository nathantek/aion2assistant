namespace Aion2Assistant.Core.Updates;

/// <summary>Ligne d'état des mises à jour affichée dans Options › À propos.</summary>
public static class UpdateStatusText
{
    public const string Disabled = "Mises à jour automatiques désactivées";
    public const string NotInstalled = "Installe Aion2Assistant via Setup.exe pour les mises à jour automatiques";

    /// <param name="enabled">Réglage « Mises à jour automatiques ».</param>
    /// <param name="installed">Application installée par Setup.exe (mise à jour automatique possible).</param>
    /// <param name="state">État de l'orchestrateur (ignoré hors installation).</param>
    /// <param name="version">Version téléchargée (Ready / Deferred).</param>
    /// <param name="upToDate">La dernière vérification a réussi sans trouver de version plus récente.</param>
    /// <param name="legacyAvailable">Hors installation : version plus récente signalée par l'ancienne vérification.</param>
    public static string Describe(bool enabled, bool installed, UpdateState state, AppVersion? version, bool upToDate, string? legacyAvailable)
    {
        if (!enabled) return Disabled;
        if (!installed)
        {
            return legacyAvailable is null
                ? NotInstalled
                : $"Version {legacyAvailable} disponible : installe-la via Setup.exe pour les mises à jour automatiques";
        }

        var ready = version is null ? "Mise à jour prête" : $"Version {version} prête";
        return state switch
        {
            UpdateState.Checking => "Recherche d'une mise à jour…",
            UpdateState.Downloading => "Téléchargement de la mise à jour…",
            UpdateState.Ready => ready,
            UpdateState.Deferred => ready + " : installée à la fermeture",
            _ => upToDate ? "À jour" : "Mises à jour automatiques actives",
        };
    }
}
