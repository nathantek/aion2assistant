using Aion2Assistant.Core.Audio;
using Aion2Assistant.Core.Hotkeys;
using Aion2Assistant.Core.Localization;

namespace Aion2Assistant.Core.Settings;

public sealed record WindowBounds(double Left, double Top, double Width, double Height);

public sealed class TimerSettings
{
    public bool Visible { get; set; } = true;
    public bool Notify { get; set; } = true;
    public bool Sound { get; set; } = true;
    public int SoonThresholdMinutes { get; set; } = 5;
    /// <summary>Son propre au timer (intégré ou <see cref="AlertSounds.CustomId"/>) ; null = celui du réglage global.</summary>
    public string? SoundId { get; set; }
    public string? CustomSoundPath { get; set; }
}

public sealed class CombatSettings
{
    public int IdleTimeoutSeconds { get; set; } = 8;
    public bool IncludeAllPlayers { get; set; } = true;
    /// <summary>Bascule uniquement la vue du compteur de DPS (en direct et dans l'historique) entre les dégâts sur les boss seuls et tous les dégâts ; rien n'est filtré à l'enregistrement.</summary>
    public bool BossOnly { get; set; }
    /// <summary>Affiche la puissance de combat après le nom des joueurs (meters, historique, image copiée).</summary>
    public bool ShowCombatPower { get; set; }
    /// <summary>PV du boss au-dessus de sa barre en pourcentage ; false = en chiffres (restants / max).</summary>
    public bool BossHpAsPercent { get; set; }
}

/// <summary>Pop-up et son d'un événement de familier (palier atteint, niveau MAX), réglables comme ceux des timers.</summary>
public sealed class FamiliarAlertSettings
{
    public bool Notify { get; set; } = true;
    public bool Sound { get; set; } = true;
    /// <summary>Son intégré (voir <see cref="AlertSounds"/>) ou <see cref="AlertSounds.CustomId"/> ; inconnu = défaut de l'événement.</summary>
    public string SoundId { get; set; } = AlertSounds.DefaultId;
    public string? CustomSoundPath { get; set; }
}

public sealed class FamiliarSettings
{
    /// <summary>Sons par défaut : harpe pour un palier (distincte du carillon des timers), cloche pour le MAX.</summary>
    public const string DefaultTierSoundId = "harp", DefaultMaxSoundId = "bell";

    public FamiliarAlertSettings Tier { get; set; } = new() { SoundId = DefaultTierSoundId };
    public FamiliarAlertSettings Max { get; set; } = new() { SoundId = DefaultMaxSoundId };
}

public sealed class HistorySettings
{
    public const int MaxRetentionDays = 30, MaxEncountersCap = 100;

    public int RetentionDays { get; set; } = 30;
    public int MaxEncounters { get; set; } = 100;
    public int MinDurationSeconds { get; set; } = 20;
}

public sealed class AppSettings
{
    public const double MinUiScale = 0.8, MaxUiScale = 2.0;
    public const double MinFontScale = 0.8, MaxFontScale = 1.5;
    public const double MinOpacity = 0.0, MaxOpacity = 1.0;

    public const int CurrentVersion = 7;
    public const string DefaultThemeId = "classique";
    /// <summary>Version du CLUF en vigueur ; l'utilisateur doit l'accepter à nouveau quand elle change.</summary>
    public const string EulaVersion = "2";
    /// <summary>Le texte reçoit un contour quand Opacity est sous ce seuil.</summary>
    public const double OutlineThreshold = 0.9;

    /// <summary>0 = fichier antérieur au versionnage ; Normalize() le fait passer à <see cref="CurrentVersion"/>.</summary>
    public int SettingsVersion { get; set; }
    /// <summary>
    /// Langue de l'app (« en » | « fr », voir <see cref="AppLanguage"/>), appliquée au démarrage.
    /// null = pas encore déterminée : SettingsStore la déduit de Windows au premier lancement, Normalize la complète.
    /// </summary>
    public string? Language { get; set; }
    public string ThemeId { get; set; } = DefaultThemeId;
    public double UiScale { get; set; } = 1.0;
    public double FontScale { get; set; } = 1.0;
    public double Opacity { get; set; } = 0.9;
    public double Volume { get; set; } = 0.8;
    public bool Docked { get; set; } = true;
    public bool Minimized { get; set; }
    /// <summary>Lueur du texte pour les thèmes qui en définissent une (absent du fichier = activée).</summary>
    public bool GlowEnabled { get; set; } = true;
    public WindowBounds? FreeBounds { get; set; }
    /// <summary>Vérifie au démarrage si une version plus récente est publiée sur GitHub.</summary>
    public bool CheckForUpdates { get; set; } = true;
    /// <summary>
    /// Canal bêta : reçoit aussi les pré-versions (-beta.N). Faux = canal stable.
    /// Nouvelle installation : stable (SettingsStore) ; réglages antérieurs à la v7 : bêta (Normalize).
    /// </summary>
    public bool BetaUpdates { get; set; }
    /// <summary>Version du CLUF acceptée (null = jamais acceptée).</summary>
    public string? EulaAcceptedVersion { get; set; }
    /// <summary>Son d'alerte : identifiant intégré (voir <see cref="AlertSounds"/>) ou <see cref="AlertSounds.CustomId"/> pour le fichier perso.</summary>
    public string SoundId { get; set; } = AlertSounds.DefaultId;
    public string? CustomSoundPath { get; set; }
    public Dictionary<string, TimerSettings> Timers { get; set; } = new();
    public Dictionary<string, string> Hotkeys { get; set; } = HotkeyDefaults.Create();
    public List<string> EmittedAlerts { get; set; } = new();
    public CombatSettings Combat { get; set; } = new();
    public HistorySettings History { get; set; } = new();
    /// <summary>Onglet « Familiers » et ses pop-ups (absent du fichier = désactivé : aucune migration).</summary>
    public bool FamiliarsEnabled { get; set; }
    public FamiliarSettings Familiars { get; set; } = new();

    /// <summary>Réglage commun à tous les boss de terrain (leurs timers naissent de la capture, pas de la config).</summary>
    public const string FieldBossTimersKey = "fieldboss";
    private const string FieldBossTimerPrefix = FieldBossTimersKey + ":";

    public TimerSettings GetTimer(string id)
    {
        var key = IsFieldBossTimer(id) ? FieldBossTimersKey : id;
        if (!Timers.TryGetValue(key, out var settings))
            // Boss : pas d'alerte tant que l'utilisateur ne l'a pas activée dans les options.
            Timers[key] = settings = new TimerSettings { Notify = key != FieldBossTimersKey };
        return settings;
    }

    private static bool IsFieldBossTimer(string id) => id.StartsWith(FieldBossTimerPrefix, StringComparison.Ordinal);

    public string GetHotkey(HotkeyAction action) => Hotkeys[action.ToString()];

    /// <summary>Ramène chaque valeur dans sa plage et complète ce qui manque (fichier ancien ou édité à la main).</summary>
    public void Normalize()
    {
        UiScale = Math.Round(Math.Clamp(UiScale, MinUiScale, MaxUiScale), 2);
        FontScale = Math.Round(Math.Clamp(FontScale, MinFontScale, MaxFontScale), 2);
        Opacity = Math.Clamp(Opacity, MinOpacity, MaxOpacity);
        Volume = Math.Clamp(Volume, 0, 1);
        Timers ??= new();
        // Anciennes entrées par boss (créées avec l'alerte activée) : remplacées par le réglage commun.
        foreach (var id in Timers.Keys.Where(IsFieldBossTimer).ToList())
            Timers.Remove(id);
        EmittedAlerts ??= new();
        Combat ??= new();
        Combat.IdleTimeoutSeconds = Math.Clamp(Combat.IdleTimeoutSeconds, 3, 60);
        History ??= new();
        History.RetentionDays = Math.Clamp(History.RetentionDays, 1, HistorySettings.MaxRetentionDays);
        History.MaxEncounters = Math.Clamp(History.MaxEncounters, 10, HistorySettings.MaxEncountersCap);
        History.MinDurationSeconds = Math.Clamp(History.MinDurationSeconds, 0, 600);
        if (string.IsNullOrWhiteSpace(ThemeId)) ThemeId = DefaultThemeId;
        Familiars ??= new();
        Familiars.Tier = NormalizeAlert(Familiars.Tier, FamiliarSettings.DefaultTierSoundId);
        Familiars.Max = NormalizeAlert(Familiars.Max, FamiliarSettings.DefaultMaxSoundId);
        Hotkeys ??= new();
        // v2 : raccourcis par défaut déplacés sur le pavé numérique ; les anciens défauts (F1…) sont remplacés.
        if (SettingsVersion < 2) Hotkeys = HotkeyDefaults.Create();
        // v3 : historique plafonné à 30 jours / 100 combats.
        if (SettingsVersion < 3) { History.RetentionDays = HistorySettings.MaxRetentionDays; History.MaxEncounters = HistorySettings.MaxEncountersCap; }
        // v4 : raccourcis d'onglet réordonnés comme les onglets (DPS, Reçus, Soins, Historique, Timers).
        if (SettingsVersion < 4) MigrateTabHotkeysV4(Hotkeys);
        // v5 : choix du son d'alerte ; un fichier perso déjà configuré reste celui utilisé.
        if (SettingsVersion < 5 && !string.IsNullOrWhiteSpace(CustomSoundPath)) SoundId = AlertSounds.CustomId;
        if (SoundId != AlertSounds.CustomId && !AlertSounds.IsBuiltIn(SoundId)) SoundId = AlertSounds.DefaultId;
        // v6 : choix de la langue ; les utilisateurs existants (réglages antérieurs) restent en français.
        if (SettingsVersion < 6 && string.IsNullOrWhiteSpace(Language)) Language = AppLanguage.French;
        Language = AppLanguage.Normalize(Language);
        // v7 : canaux stable / bêta ; les utilisateurs existants (bêta-testeurs) restent sur le canal bêta.
        if (SettingsVersion < 7) BetaUpdates = true;
        SettingsVersion = CurrentVersion;
        foreach (var (action, gesture) in HotkeyDefaults.Create())
            Hotkeys.TryAdd(action, gesture);
        foreach (var timer in Timers.Values)
        {
            timer.SoonThresholdMinutes = Math.Clamp(timer.SoonThresholdMinutes, 1, 120);
            // Son propre inconnu (fichier édité à la main) : retour à l'héritage du réglage global.
            if (timer.SoundId is { } id && id != AlertSounds.CustomId && !AlertSounds.IsBuiltIn(id)) timer.SoundId = null;
        }
    }

    private static FamiliarAlertSettings NormalizeAlert(FamiliarAlertSettings? alert, string defaultSoundId)
    {
        alert ??= new FamiliarAlertSettings { SoundId = defaultSoundId };
        if (alert.SoundId != AlertSounds.CustomId && !AlertSounds.IsBuiltIn(alert.SoundId)) alert.SoundId = defaultSoundId;
        return alert;
    }

    /// <summary>
    /// Passe les raccourcis d'onglet restés à leur ancien défaut sur le nouveau, sans toucher aux valeurs personnalisées
    /// ni créer de doublon (un candidat dont le nouveau défaut est déjà pris par une autre action reste inchangé).
    /// </summary>
    internal static void MigrateTabHotkeysV4(Dictionary<string, string> hotkeys)
    {
        var newDefaults = HotkeyDefaults.Create();
        var candidates = new HashSet<string>();
        foreach (var (action, oldDefault) in HotkeyDefaults.TabDefaultsV3)
            if (hotkeys.TryGetValue(action, out var value)
                && HotkeyGesture.TryParse(value, out var current)
                && HotkeyGesture.TryParse(oldDefault, out var old) && current == old)
                candidates.Add(action);

        bool removed;
        do
        {
            removed = false;
            var taken = new HashSet<HotkeyGesture>();
            foreach (var (action, value) in hotkeys)
                if (!candidates.Contains(action) && HotkeyGesture.TryParse(value, out var g)) taken.Add(g);
            foreach (var action in candidates.ToList())
                if (HotkeyGesture.TryParse(newDefaults[action], out var target) && taken.Contains(target))
                    removed |= candidates.Remove(action);
        } while (removed);

        foreach (var action in candidates)
            hotkeys[action] = newDefaults[action];
    }
}
