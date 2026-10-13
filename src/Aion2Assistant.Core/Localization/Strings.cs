using System.Resources;
using System.Runtime.CompilerServices;

namespace Aion2Assistant.Core.Localization;

/// <summary>
/// Textes partagés générés par les bibliothèques (copie chat, issues, dates relatives…), dans Strings.resx
/// (anglais, neutre) et Strings.fr.resx. Accès écrit à la main : le nom de la propriété est la clé.
/// Ajouter une clé = une entrée dans les deux .resx + une propriété <c>public static string Cle =&gt; Get();</c>.
/// </summary>
public static class Strings
{
    public static ResourceManager ResourceManager { get; } =
        new("Aion2Assistant.Core.Localization.Strings", typeof(Strings).Assembly);

    /// <summary>Texte de la clé dans la culture de l'app ; clé absente = la clé elle-même (visible, jamais vide).</summary>
    public static string Get([CallerMemberName] string key = "") =>
        ResourceManager.GetString(key, AppCulture.Current) is { Length: > 0 } text ? text : key;

    /// <summary>Texte de la clé avec ses paramètres {0}, {1}… mis en forme dans la culture de l'app.</summary>
    public static string Format(string key, params object?[] args) =>
        string.Format(AppCulture.Current, Get(key), args);

    /// <summary>Milliers abrégés : « {0}k » / « {0} k ».</summary>
    public static string FormatKilo => Get();
    /// <summary>Millions abrégés : « {0}M » / « {0} M ».</summary>
    public static string FormatMega => Get();
    /// <summary>Pourcentage : « {0}% » / « {0} % ».</summary>
    public static string FormatPercent => Get();

    /// <summary>« Killed » / « Tué ».</summary>
    public static string OutcomeKill => Get();
    /// <summary>« Wipe » / « Wipe ».</summary>
    public static string OutcomeWipe => Get();
    /// <summary>« Wipe {0} » / « Wipe {0} ».</summary>
    public static string OutcomeWipeHp => Get();
    /// <summary>« Training » / « Entraînement ».</summary>
    public static string OutcomeTraining => Get();
    /// <summary>« Reset » / « Réinitialisé ».</summary>
    public static string OutcomeReset => Get();
    /// <summary>« Ended » / « Terminé ».</summary>
    public static string OutcomeEnded => Get();
    /// <summary>« in progress » / « en cours ».</summary>
    public static string StateLive => Get();
    /// <summary>« training » / « entraînement ».</summary>
    public static string StateTraining => Get();
    /// <summary>« DPS » / « DPS ».</summary>
    public static string ShareDps => Get();
    /// <summary>« Boss DPS » / « DPS boss ».</summary>
    public static string ShareBossDps => Get();
    /// <summary>« Healing » / « Soins ».</summary>
    public static string ShareHeal => Get();
    /// <summary>« Taken » / « Reçus ».</summary>
    public static string ShareTaken => Get();
    /// <summary>« {0} death » / « {0} mort ».</summary>
    public static string ShareDeath => Get();
    /// <summary>« {0} deaths » / « {0} morts ».</summary>
    public static string ShareDeaths => Get();
    /// <summary>« Combat » / « Combat ».</summary>
    public static string EncounterFallbackTitle => Get();
    /// <summary>« NPC {0} » / « PNJ {0} ».</summary>
    public static string NpcFallback => Get();
    /// <summary>« Map {0} » / « Carte {0} ».</summary>
    public static string MapFallback => Get();
    /// <summary>« Unknown zone » / « Zone inconnue ».</summary>
    public static string ZoneUnknown => Get();

    /// <summary>« {0}d {1:00}:{2:00} » / « {0} j {1:00}:{2:00} ».</summary>
    public static string CountdownDays => Get();
    /// <summary>« less than a minute » / « moins d'une minute ».</summary>
    public static string CountdownUnderMinute => Get();
    /// <summary>« {0} min » / « {0} min ».</summary>
    public static string CountdownMinutes => Get();
    /// <summary>« {0}h {1:00} » / « {0} h {1:00} ».</summary>
    public static string CountdownHours => Get();
    /// <summary>«  (assumed) » / «  (supposé) ».</summary>
    public static string FieldBossAssumedSuffix => Get();

    /// <summary>« Automatic updates off » / « Mises à jour automatiques désactivées ».</summary>
    public static string UpdateDisabled => Get();
    /// <summary>« Install Aion2Assistant with Setup.exe to get automatic updates » / « Installe Aion2Assistant via Setup.exe pour les mises à jour automatiques ».</summary>
    public static string UpdateNotInstalled => Get();
    /// <summary>« Version {0} available: install it with Setup.exe to get automatic updates » / « Version {0} disponible : installe-la via Setup.exe pour les mises à jour automatiques ».</summary>
    public static string UpdateLegacyAvailable => Get();
    /// <summary>« Update ready » / « Mise à jour prête ».</summary>
    public static string UpdateReady => Get();
    /// <summary>« Version {0} ready » / « Version {0} prête ».</summary>
    public static string UpdateVersionReady => Get();
    /// <summary>« {0}: installs on exit » / « {0} : installée à la fermeture ».</summary>
    public static string UpdateDeferred => Get();
    /// <summary>« Checking for updates… » / « Recherche d'une mise à jour… ».</summary>
    public static string UpdateChecking => Get();
    /// <summary>« Downloading update… » / « Téléchargement de la mise à jour… ».</summary>
    public static string UpdateDownloading => Get();
    /// <summary>« Up to date » / « À jour ».</summary>
    public static string UpdateUpToDate => Get();
    /// <summary>« Automatic updates on » / « Mises à jour automatiques actives ».</summary>
    public static string UpdateActive => Get();
    /// <summary>« Chime » / « Carillon ».</summary>
    public static string SoundChime => Get();
    /// <summary>« Bell » / « Cloche ».</summary>
    public static string SoundBell => Get();
    /// <summary>« Double beep » / « Double bip ».</summary>
    public static string SoundDoubleBeep => Get();
    /// <summary>« Gong » / « Gong ».</summary>
    public static string SoundGong => Get();
    /// <summary>« Harp » / « Harpe ».</summary>
    public static string SoundHarp => Get();
    /// <summary>« Alarm » / « Alarme ».</summary>
    public static string SoundAlarm => Get();
    /// <summary>« Custom file… » / « Fichier perso… ».</summary>
    public static string SoundCustomFile => Get();

    /// <summary>« Heartbeat » / « Heartbeat ».</summary>
    public static string OpcodeHeartbeat => Get();
    /// <summary>« Character » / « Personnage ».</summary>
    public static string OpcodeSelfInfo => Get();
    /// <summary>« Player » / « Joueur ».</summary>
    public static string OpcodePlayerInfo => Get();
    /// <summary>« Player (old) » / « Joueur (ancien) ».</summary>
    public static string OpcodePlayerInfoOld => Get();
    /// <summary>« Spawn » / « Apparition ».</summary>
    public static string OpcodeSpawn => Get();
    /// <summary>« Death » / « Mort ».</summary>
    public static string OpcodeDeath => Get();
    /// <summary>« Map » / « Carte ».</summary>
    public static string OpcodeMapLoad => Get();
    /// <summary>« Teleport » / « Téléportation ».</summary>
    public static string OpcodeTeleport => Get();
    /// <summary>« Cast » / « Lancement ».</summary>
    public static string OpcodeCast => Get();
    /// <summary>« Damage » / « Dégâts ».</summary>
    public static string OpcodeDamage => Get();
    /// <summary>« DoT/HoT » / « DoT/HoT ».</summary>
    public static string OpcodeDot => Get();
    /// <summary>« Remaining HP » / « PV restants ».</summary>
    public static string OpcodeRemainHp => Get();
    /// <summary>« Death + killer » / « Mort + tueur ».</summary>
    public static string OpcodeKillCredit => Get();
    /// <summary>« HP/MP » / « PV/PM ».</summary>
    public static string OpcodeHpMp => Get();
    /// <summary>« Party » / « Groupe ».</summary>
    public static string OpcodePartyRoster => Get();
    /// <summary>« Field bosses » / « Boss de terrain ».</summary>
    public static string OpcodeFieldBossList => Get();
    /// <summary>« Combat power » / « Puissance ».</summary>
    public static string OpcodeCombatPower => Get();
    /// <summary>« Inspect » / « Inspection ».</summary>
    public static string OpcodeInspect => Get();
    /// <summary>« Npcap is not installed » / « Npcap n'est pas installé ».</summary>
    public static string CaptureNpcapMissing => Get();
    /// <summary>« No network adapter carries the game connection » / « Aucune carte réseau ne porte la connexion du jeu ».</summary>
    public static string CaptureNoAdapter => Get();
    /// <summary>« Capture error » / « Erreur de capture ».</summary>
    public static string CaptureError => Get();
    /// <summary>« Game not detected » / « Jeu non détecté ».</summary>
    public static string CaptureGameNotDetected => Get();
    /// <summary>« Waiting for the game connection » / « En attente de la connexion du jeu ».</summary>
    public static string CaptureWaiting => Get();
    /// <summary>« No packets received yet » / « Aucun paquet reçu pour l'instant ».</summary>
    public static string CaptureNoPacketsYet => Get();
    /// <summary>« No packets for 10s » / « Aucun paquet depuis 10 s ».</summary>
    public static string CaptureNoPackets10s => Get();
    /// <summary>« Many unreadable packets (protocol changed?) » / « Beaucoup de paquets illisibles (protocole modifié ?) ».</summary>
    public static string CaptureManyRejected => Get();
    /// <summary>« Packets received but nothing decoded » / « Paquets reçus mais aucun événement décodé ».</summary>
    public static string CaptureNoEvents => Get();
    /// <summary>« Capture active » / « Capture active ».</summary>
    public static string CaptureActive => Get();
    /// <summary>« Starting… » / « Démarrage… ».</summary>
    public static string CaptureStatusStarting => Get();
    /// <summary>« Npcap is not installed: https://npcap.com (tick “WinPcap API-compatible Mode”). » / « Npcap n'est pas installé : https://npcap.com (cocher « WinPcap API-compatible Mode »). ».</summary>
    public static string CaptureStatusNpcapMissing => Get();
    /// <summary>« AION 2 is not running. » / « AION 2 n'est pas lancé. ».</summary>
    public static string CaptureStatusGameNotRunning => Get();
    /// <summary>« Waiting for the game connection. » / « En attente de la connexion du jeu. ».</summary>
    public static string CaptureStatusWaiting => Get();
    /// <summary>« No Npcap adapter carries the game connection (VPN?). » / « Aucune carte Npcap ne porte la connexion du jeu (VPN ?). ».</summary>
    public static string CaptureStatusNoAdapter => Get();
    /// <summary>« Capture active. » / « Capture active. ».</summary>
    public static string CaptureStatusActive => Get();
    /// <summary>« Capture access denied: reinstall Npcap without the “Restrict Npcap driver's access to Administrators only” option. {0} » / « Ouverture de la capture refusée : réinstalle Npcap sans l'option « réservé aux administrateurs ». {0} ».</summary>
    public static string CaptureStatusAccessDenied => Get();
    /// <summary>« Capture error: {0} » / « Erreur de capture : {0} ».</summary>
    public static string CaptureStatusError => Get();
}
