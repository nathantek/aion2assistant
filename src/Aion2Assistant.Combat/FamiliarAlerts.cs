using Aion2Assistant.Core.Audio;
using Aion2Assistant.Core.Settings;

namespace Aion2Assistant.Combat;

/// <summary>
/// Bandeau de l'onglet Familiers : aucun (synchronisé), attente de l'entrée en jeu (neutre), état daté ou jamais synchronisé
/// (avertissements : l'app a été lancée après l'entrée en jeu).
/// </summary>
public enum FamiliarBannerKind { None, Waiting, Stale, NeverSynced }

public static class FamiliarBanner
{
    /// <summary>
    /// Délai après l'identification du personnage avant d'avertir : l'état complet (0x0090) arrive environ 9 s après
    /// le chargement du personnage, l'avertissement ne doit pas clignoter pendant ce temps.
    /// </summary>
    public const long GraceMs = 30_000;

    /// <summary>
    /// Délai après le début du flux de jeu avant d'avertir, même sans identification (app lancée en cours de partie : le
    /// personnage n'est pas renvoyé). À la connexion, l'identification arrive 5 à 7 s après le flux et le 0x0090 ~9 s plus tard.
    /// </summary>
    public const long StreamGraceMs = 60_000;

    /// <summary>
    /// Synchronisé pendant cette session = pas de bandeau. Sinon, avertissement (daté s'il existe un ancien état complet, sinon
    /// jamais synchronisé) si le flux a commencé depuis au moins <see cref="StreamGraceMs"/> ou le personnage est identifié
    /// depuis au moins <see cref="GraceMs"/> ; attente dans les autres cas (y compris jeu pas lancé, aucun flux).
    /// </summary>
    /// <param name="identifiedAtMs">Identification du serveur courant (ms Unix) ; null s'il n'est pas encore identifié.</param>
    /// <param name="streamStartedAtMs">Début du flux de jeu (ms Unix) ; null si aucun flux n'a été verrouillé.</param>
    /// <param name="nowMs">Heure actuelle (ms Unix).</param>
    public static FamiliarBannerKind Select(bool isSynced, long? lastSyncMs, long? identifiedAtMs, long? streamStartedAtMs, long nowMs) =>
        isSynced ? FamiliarBannerKind.None
        : !Elapsed(streamStartedAtMs, StreamGraceMs, nowMs) && !Elapsed(identifiedAtMs, GraceMs, nowMs) ? FamiliarBannerKind.Waiting
        : lastSyncMs is > 0 ? FamiliarBannerKind.Stale
        : FamiliarBannerKind.NeverSynced;

    /// <summary>
    /// Vrai tant qu'un avertissement peut encore apparaître sans nouvel événement : attente avec un flux ou une identification
    /// en cours de grâce. Faux une fois synchronisé, l'avertissement affiché, ou sans flux ni identification.
    /// </summary>
    public static bool NeedsTimedRefresh(bool isSynced, long? identifiedAtMs, long? streamStartedAtMs, long nowMs) =>
        (identifiedAtMs is not null || streamStartedAtMs is not null)
        && Select(isSynced, null, identifiedAtMs, streamStartedAtMs, nowMs) == FamiliarBannerKind.Waiting;

    private static bool Elapsed(long? sinceMs, long delayMs, long nowMs) => sinceMs is { } since && nowMs - since >= delayMs;
}

/// <summary>Ce qu'il faut faire pour une montée de palier : pop-up, son (null = aucun).</summary>
public sealed record FamiliarAlertDecision(bool ShowPopup, SoundRef? Sound)
{
    public static readonly FamiliarAlertDecision Nothing = new(false, null);
}

/// <summary>Règles des pop-ups et sons de familiers (option désactivée = rien du tout).</summary>
public static class FamiliarAlerts
{
    /// <summary>Réglages de l'événement : « MAX » pour le palier 3, « palier » sinon.</summary>
    public static FamiliarAlertSettings For(FamiliarSettings settings, FamiliarMilestone milestone) =>
        milestone.IsMax ? settings.Max : settings.Tier;

    /// <summary>
    /// Option désactivée : ni pop-up ni son. Plein écran exclusif : pas de pop-up (elle passerait sous le jeu), le son reste,
    /// comme pour les timers.
    /// </summary>
    public static FamiliarAlertDecision Decide(bool enabled, bool exclusiveFullscreen, FamiliarSettings settings, FamiliarMilestone milestone)
    {
        if (!enabled) return FamiliarAlertDecision.Nothing;
        var alert = For(settings, milestone);
        return new FamiliarAlertDecision(alert.Notify && !exclusiveFullscreen,
            alert.Sound ? new SoundRef(alert.SoundId, alert.CustomSoundPath) : null);
    }

    /// <summary>
    /// Pop-up compacte d'âme reçue (sans son) : seulement option activée, overlay réduit à la barre et hors plein écran
    /// exclusif. Pas de pop-up d'âme si une pop-up de palier est affichée pour le même lot (pas d'empilement bruyant).
    /// </summary>
    public static bool ShowSoulPopup(bool enabled, bool minimized, bool exclusiveFullscreen, bool milestonePopupShown) =>
        enabled && minimized && !exclusiveFullscreen && !milestonePopupShown;

    /// <summary>Texte de la pop-up d'âme : « nom progression », par ex. « Kalgolem 12/25 » ou « Kalgolem MAX ».</summary>
    public static string SoulPopupText(string name, FamiliarStatus status) => $"{name} {status.Text}";
}
