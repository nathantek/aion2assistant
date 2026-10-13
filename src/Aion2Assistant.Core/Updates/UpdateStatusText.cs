using Aion2Assistant.Core.Localization;

namespace Aion2Assistant.Core.Updates;

/// <summary>Ligne d'état des mises à jour affichée dans Options › À propos.</summary>
public static class UpdateStatusText
{
    /// <param name="enabled">Réglage « Mises à jour automatiques ».</param>
    /// <param name="installed">Application installée par Setup.exe (mise à jour automatique possible).</param>
    /// <param name="state">État de l'orchestrateur (ignoré hors installation).</param>
    /// <param name="version">Version téléchargée (Ready / Deferred).</param>
    /// <param name="upToDate">La dernière vérification a réussi sans trouver de version plus récente.</param>
    /// <param name="legacyAvailable">Hors installation : version plus récente signalée par l'ancienne vérification.</param>
    public static string Describe(bool enabled, bool installed, UpdateState state, AppVersion? version, bool upToDate, string? legacyAvailable)
    {
        if (!enabled) return Strings.UpdateDisabled;
        if (!installed)
        {
            return legacyAvailable is null
                ? Strings.UpdateNotInstalled
                : Strings.Format(nameof(Strings.UpdateLegacyAvailable), legacyAvailable);
        }

        var ready = version is null ? Strings.UpdateReady : Strings.Format(nameof(Strings.UpdateVersionReady), version);
        return state switch
        {
            UpdateState.Checking => Strings.UpdateChecking,
            UpdateState.Downloading => Strings.UpdateDownloading,
            UpdateState.Ready => ready,
            UpdateState.Deferred => Strings.Format(nameof(Strings.UpdateDeferred), ready),
            _ => upToDate ? Strings.UpdateUpToDate : Strings.UpdateActive,
        };
    }
}
