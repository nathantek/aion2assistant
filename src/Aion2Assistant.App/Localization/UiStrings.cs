using System.Resources;
using System.Runtime.CompilerServices;
using Aion2Assistant.Core.Localization;

namespace Aion2Assistant.App.Localization;

/// <summary>
/// Textes de l'interface, dans UiStrings.resx (anglais, neutre) et UiStrings.fr.resx. Accès écrit à la main
/// (le générateur ResX de Visual Studio ne tourne pas avec dotnet build) : le nom de la propriété est la clé.
/// Depuis le XAML : <c>{loc:Tr Cle}</c> (voir <see cref="TrExtension"/>).
/// </summary>
public static class UiStrings
{
    public static ResourceManager ResourceManager { get; } =
        new("Aion2Assistant.App.Localization.UiStrings", typeof(UiStrings).Assembly);

    /// <summary>Texte de la clé dans la culture de l'app ; clé absente = la clé elle-même (visible, jamais vide).</summary>
    public static string Get([CallerMemberName] string key = "") =>
        ResourceManager.GetString(key, AppCulture.Current) is { Length: > 0 } text ? text : key;

    /// <summary>Texte de la clé avec ses paramètres {0}, {1}… mis en forme dans la culture de l'app.</summary>
    public static string Format(string key, params object?[] args) =>
        string.Format(AppCulture.Current, Get(key), args);

    /// <summary>Texte de la clé dans une autre langue que celle de l'app (message de redémarrage après un changement).</summary>
    public static string GetIn(string language, string key) =>
        ResourceManager.GetString(key, AppCulture.CultureFor(language)) is { Length: > 0 } text ? text : key;

    /// <summary>« Close » / « Fermer ».</summary>
    public static string EulaClose => Get();
    /// <summary>Motif de date et heure complètes (en-tête de la comparaison).</summary>
    public static string FormatDateTime => Get();
    /// <summary>Motif du séparateur de jour de l'historique (au-delà d'hier).</summary>
    public static string FormatDayHeader => Get();
    /// <summary>« Aion2Assistant could not start. Details are in the logs. » / « Aion2Assistant n'a pas pu démarrer. Détails dans les logs. ».</summary>
    public static string StartupFailed => Get();
    /// <summary>« Quit » / « Quitter ».</summary>
    public static string Quit => Get();
    /// <summary>« Settings » / « Réglages ».</summary>
    public static string Settings => Get();
    /// <summary>« DPS » / « DPS ».</summary>
    public static string TabDps => Get();
    /// <summary>« Damage taken » / « Dégâts reçus ».</summary>
    public static string TabTaken => Get();
    /// <summary>« Healing » / « Soins ».</summary>
    public static string TabHeal => Get();
    /// <summary>« History » / « Historique ».</summary>
    public static string TabHistory => Get();
    /// <summary>« Loading… » / « Chargement… ».</summary>
    public static string PlaceholderLoading => Get();
    /// <summary>« Npcap is not installed: download it from npcap.com and tick “WinPcap API-compatible Mode”. » / « Npcap n'est pas installé : télécharge-le sur npcap.com et coche « WinPcap API-compatible Mode ». ».</summary>
    public static string NpcapMissing => Get();
    /// <summary>« Version {0} available » / « Version {0} disponible ».</summary>
    public static string UpdateAvailableTitle => Get();
    /// <summary>« Install it with Setup.exe from the project page (Options › About). » / « Installe-la avec Setup.exe depuis la page du projet (Options › À propos). ».</summary>
    public static string UpdateAvailableMessage => Get();
    /// <summary>« Update {0} ready » / « Mise à jour {0} prête ».</summary>
    public static string UpdateReadyTitle => Get();
    /// <summary>« Restart from the overlay to install it. » / « Redémarre depuis l'overlay pour l'installer. ».</summary>
    public static string UpdateReadyMessage => Get();
    /// <summary>« AION 2 is in exclusive fullscreen: the overlay can't show on top of it. Switch to “borderless windowed” in the game's graphics options. » / « AION 2 est en plein écran exclusif : l'overlay ne peut pas s'afficher par-dessus. Passe en « fenêtré sans bordure » dans les options graphiques du jeu. ».</summary>
    public static string ExclusiveFullscreenMessage => Get();
    /// <summary>« Exclusive fullscreen detected » / « Plein écran exclusif détecté ».</summary>
    public static string ExclusiveFullscreenDetected => Get();
    /// <summary>« Exclusive fullscreen » / « Plein écran exclusif ».</summary>
    public static string ExclusiveFullscreenTitle => Get();
    /// <summary>« Switch to borderless windowed. » / « Passe en fenêtré sans bordure. ».</summary>
    public static string ExclusiveFullscreenShort => Get();
    /// <summary>« Unexpected error » / « Erreur inattendue ».</summary>
    public static string UnexpectedErrorTitle => Get();
    /// <summary>« Details are in the logs. » / « Les détails sont dans les logs. ».</summary>
    public static string UnexpectedErrorMessage => Get();
    /// <summary>« In {0} » / « Dans {0} ».</summary>
    public static string TimerAlertIn => Get();
    /// <summary>« Now » / « Maintenant ».</summary>
    public static string TimerAlertNow => Get();
    /// <summary>« New record! » / « Nouveau record ! ».</summary>
    public static string RecordNewTitle => Get();
    /// <summary>« {0}: {1}/s (previous {2}/s, +{3}%) » / « {0} : {1}/s (ancien {2}/s, +{3} %) ».</summary>
    public static string RecordNewMessage => Get();
    /// <summary>« Your DPS (fight in progress) » / « Ton DPS (combat en cours) ».</summary>
    public static string MiniSelfDps => Get();
    /// <summary>« Your DPS on the last boss (fight in progress) » / « Ton DPS sur le dernier boss (combat en cours) ».</summary>
    public static string MiniLastBossDps => Get();
    /// <summary>« Next timer » / « Prochain timer ».</summary>
    public static string MiniNextTimer => Get();
    /// <summary>« Show / hide » / « Afficher / masquer ».</summary>
    public static string TrayToggleVisible => Get();
    /// <summary>« Dock / undock » / « Ancrer / libérer ».</summary>
    public static string TrayToggleDock => Get();
    /// <summary>« Reset position » / « Réinitialiser la position ».</summary>
    public static string ResetPosition => Get();
    /// <summary>« Death at {0} » / « Mort à {0} ».</summary>
    public static string MeterDeathAt => Get();
    /// <summary>« Group: {0} · {1} » / « Groupe : {0} · {1} ».</summary>
    public static string MeterGroupRate => Get();
    /// <summary>« Group: {0} » / « Groupe : {0} ».</summary>
    public static string MeterGroupTotal => Get();
    /// <summary>«  · no boss, everything counts » / «  · aucun boss, tout est compté ».</summary>
    public static string MeterNoBossSuffix => Get();
    /// <summary>« Last boss · {0} min ago » / « Dernier boss · il y a {0} min ».</summary>
    public static string MeterLastBossAgo => Get();
    /// <summary>« Last boss · &lt; 1 min ago » / « Dernier boss · il y a &lt; 1 min ».</summary>
    public static string MeterLastBossJustNow => Get();
    /// <summary>« Training: {0} left » / « Entraînement : {0} restantes ».</summary>
    public static string TrainingRemaining => Get();
    /// <summary>« Free training: {0} » / « Entraînement libre : {0} ».</summary>
    public static string TrainingFreeElapsed => Get();
    /// <summary>« Training ready: attack to start. » / « Entraînement prêt : frappe pour démarrer. ».</summary>
    public static string TrainingArmed => Get();
    /// <summary>« Copy text » / « Copier texte ».</summary>
    public static string CopyText => Get();
    /// <summary>« Copy image » / « Copier image ».</summary>
    public static string CopyImage => Get();
    /// <summary>« ✓ Copied » / « ✓ Copié ».</summary>
    public static string CopyTextDone => Get();
    /// <summary>« ✓ Image copied » / « ✓ Image copiée ».</summary>
    public static string CopyImageDone => Get();
    /// <summary>« Copy failed » / « Échec de la copie ».</summary>
    public static string CopyFailed => Get();
    /// <summary>« History unavailable: {0} » / « Historique indisponible : {0} ».</summary>
    public static string HistoryUnavailable => Get();
    /// <summary>« 1 fight » / « 1 combat ».</summary>
    public static string HistoryFightCountOne => Get();
    /// <summary>« {0} fights » / « {0} combats ».</summary>
    public static string HistoryFightCount => Get();
    /// <summary>« Group {0} » / « Groupe {0} ».</summary>
    public static string HistoryGroup => Get();
    /// <summary>« You {0} » / « Toi {0} ».</summary>
    public static string HistorySelf => Get();
    /// <summary>« {0} vs prev. try » / « {0} vs essai préc. ».</summary>
    public static string HistoryVsPrevious => Get();
    /// <summary>« Today » / « Aujourd'hui ».</summary>
    public static string HistoryToday => Get();
    /// <summary>« Yesterday » / « Hier ».</summary>
    public static string HistoryYesterday => Get();
    /// <summary>« Unreadable fight. » / « Combat illisible. ».</summary>
    public static string FightUnreadable => Get();
    /// <summary>« Total damage » / « Dégâts totaux ».</summary>
    public static string ComparisonTotalDamage => Get();
    /// <summary>« Crit » / « Critique ».</summary>
    public static string ComparisonCrit => Get();
    /// <summary>« {0} pts » / « {0} pts ».</summary>
    public static string ComparisonPoints => Get();
    /// <summary>« Notification sound » / « Son de notification ».</summary>
    public static string TimerSoundTip => Get();
    /// <summary>« Open » / « Ouvert ».</summary>
    public static string TimerOpen => Get();
    /// <summary>« at {0} » / « à {0} ».</summary>
    public static string TimerAt => Get();
    /// <summary>« {0} · schedule unverified » / « {0} · horaire à vérifier ».</summary>
    public static string TimerUnverified => Get();
    /// <summary>« Alive » / « Vivant ».</summary>
    public static string FieldBossAlive => Get();
    /// <summary>« Alive? » / « Vivant ? ».</summary>
    public static string FieldBossAliveAssumed => Get();
    /// <summary>« Boss {0} — map {1} » / « Boss {0} — carte {1} ».</summary>
    public static string FieldBossFallback => Get();
    /// <summary>« Field bosses » / « Boss de terrain ».</summary>
    public static string FieldBosses => Get();
    /// <summary>« Restart required to apply the language. » / « Redémarrage nécessaire pour appliquer la langue. ».</summary>
    public static string LanguageRestartRequired => Get();
    /// <summary>« Restart now » / « Redémarrer maintenant ».</summary>
    public static string LanguageRestartNow => Get();
    /// <summary>« Record a capture » / « Enregistrer une capture ».</summary>
    public static string CaptureRecordStart => Get();
    /// <summary>« Stop recording » / « Arrêter l'enregistrement ».</summary>
    public static string CaptureRecordStop => Get();
    /// <summary>« Character not detected » / « Personnage non détecté ».</summary>
    public static string CaptureSelfUnknown => Get();
    /// <summary>« (no name) » / « (sans nom) ».</summary>
    public static string CaptureNoName => Get();
    /// <summary>«  — server {0} — {1} lv. {2} » / «  — serveur {0} — {1} niv. {2} ».</summary>
    public static string CaptureSelfDetails => Get();
    /// <summary>«  — combat power {0} » / «  — puissance {0} ».</summary>
    public static string CaptureSelfPower => Get();
    /// <summary>« {0:N0} segments · {1:N0} frames · {2:N0} events\n{3} resyncs · {4} gaps · {5:0.#}% rejected · {6} dropped » / « {0:N0} segments · {1:N0} trames · {2:N0} événements\n{3} resync. · {4} trous · {5:0.#} % rejetés · {6} perdus ».</summary>
    public static string CaptureCounters => Get();
    /// <summary>« {0}: {1:N0} » / « {0} : {1:N0} ».</summary>
    public static string CaptureOpcodeLine => Get();
    /// <summary>«  ({0} rejected) » / «  ({0} rejetés) ».</summary>
    public static string CaptureOpcodeRejected => Get();
    /// <summary>« Press a key combination… (Esc: cancel) » / « Appuie sur une combinaison… (Échap : annuler) ».</summary>
    public static string HotkeyPressCombination => Get();
    /// <summary>« Unsupported combination (needs Ctrl, Alt, Shift or Win + a key) » / « Combinaison non prise en charge (il faut Ctrl, Alt, Shift ou Win + une touche) ».</summary>
    public static string HotkeyUnsupported => Get();
    /// <summary>« Already used by “{0}” » / « Déjà utilisé par « {0} » ».</summary>
    public static string HotkeyAlreadyUsedBy => Get();
    /// <summary>« Invalid hotkey » / « Raccourci invalide ».</summary>
    public static string HotkeyInvalid => Get();
    /// <summary>« Already used by another program » / « Déjà utilisé par un autre programme ».</summary>
    public static string HotkeyTakenByOtherProgram => Get();
    /// <summary>« Timers tab » / « Onglet Timers ».</summary>
    public static string HotkeyTabTimers => Get();
    /// <summary>« DPS tab » / « Onglet DPS ».</summary>
    public static string HotkeyTabDps => Get();
    /// <summary>« Damage taken tab » / « Onglet Dégâts reçus ».</summary>
    public static string HotkeyTabTaken => Get();
    /// <summary>« Healing tab » / « Onglet Soins ».</summary>
    public static string HotkeyTabHeal => Get();
    /// <summary>« History tab » / « Onglet Historique ».</summary>
    public static string HotkeyTabHistory => Get();
    /// <summary>« Previous tab » / « Onglet précédent ».</summary>
    public static string HotkeyPreviousTab => Get();
    /// <summary>« Next tab » / « Onglet suivant ».</summary>
    public static string HotkeyNextTab => Get();
    /// <summary>« Hide / show » / « Masquer / afficher ».</summary>
    public static string HotkeyToggleVisible => Get();
    /// <summary>« Dock / undock » / « Ancrer / libérer ».</summary>
    public static string HotkeyToggleDock => Get();
    /// <summary>« Collapse / expand » / « Réduire / agrandir ».</summary>
    public static string HotkeyToggleMinimize => Get();
    /// <summary>« Reset fight » / « Réinitialiser le combat ».</summary>
    public static string HotkeyResetEncounter => Get();
    /// <summary>« Scale interface up » / « Agrandir l'interface ».</summary>
    public static string HotkeyScaleUp => Get();
    /// <summary>« Scale interface down » / « Rétrécir l'interface ».</summary>
    public static string HotkeyScaleDown => Get();
    /// <summary>« Version {0} » / « Version {0} ».</summary>
    public static string AboutVersion => Get();
    /// <summary>« File not found: {0} — {1} plays instead » / « Fichier introuvable : {0} — {1} joué à la place ».</summary>
    public static string SoundFileMissing => Get();
    /// <summary>« Sounds (*.wav;*.mp3)|*.wav;*.mp3 » / « Sons (*.wav;*.mp3)|*.wav;*.mp3 ».</summary>
    public static string SoundFileFilter => Get();
    /// <summary>« EULA not found: reinstall the application. » / « CLUF introuvable : réinstalle l'application. ».</summary>
    public static string EulaMissing => Get();
    /// <summary>« Familiars » / « Familiers ».</summary>
    public static string TabFamiliars => Get();
    /// <summary>« Tier reached » / « Palier atteint ».</summary>
    public static string FamiliarsTierAlert => Get();
    /// <summary>« MAX level » / « Niveau MAX ».</summary>
    public static string FamiliarsMaxAlert => Get();
    /// <summary>« Waiting for you to enter the game… » / « En attente de l'entrée en jeu… ».</summary>
    public static string FamiliarsBannerWaiting => Get();
    /// <summary>« State from {0}: start the app before the game to sync. » / « État du {0} : lance l'app avant le jeu pour synchroniser. ».</summary>
    public static string FamiliarsBannerStale => Get();
    /// <summary>« State not synced: start the app before the game. » / « État non synchronisé : lance l'app avant le jeu. ».</summary>
    public static string FamiliarsBannerNeverSynced => Get();
    /// <summary>« {0}: tier {1} reached » / « {0} : palier {1} atteint ».</summary>
    public static string FamiliarTierReached => Get();
    /// <summary>« {0}: MAX level! » / « {0} : niveau MAX ! ».</summary>
    public static string FamiliarMaxReached => Get();
}
