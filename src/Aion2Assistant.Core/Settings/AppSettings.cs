using Aion2Assistant.Core.Hotkeys;

namespace Aion2Assistant.Core.Settings;

public sealed record WindowBounds(double Left, double Top, double Width, double Height);

public sealed class TimerSettings
{
    public bool Visible { get; set; } = true;
    public bool Notify { get; set; } = true;
    public bool Sound { get; set; } = true;
    public int SoonThresholdMinutes { get; set; } = 5;
}

public sealed class CombatSettings
{
    public int IdleTimeoutSeconds { get; set; } = 8;
    public bool IncludeAllPlayers { get; set; } = true;
    /// <summary>Bascule uniquement la vue du compteur de DPS (en direct et dans l'historique) entre les dégâts sur les boss seuls et tous les dégâts ; rien n'est filtré à l'enregistrement.</summary>
    public bool BossOnly { get; set; }
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

    public const int CurrentVersion = 3;
    public const string DefaultThemeId = "classique";
    /// <summary>Version du CLUF en vigueur ; l'utilisateur doit l'accepter à nouveau quand elle change.</summary>
    public const string EulaVersion = "1";
    /// <summary>Le texte reçoit un contour quand Opacity est sous ce seuil.</summary>
    public const double OutlineThreshold = 0.9;

    /// <summary>0 = fichier antérieur au versionnage ; Normalize() le fait passer à <see cref="CurrentVersion"/>.</summary>
    public int SettingsVersion { get; set; }
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
    /// <summary>Version du CLUF acceptée (null = jamais acceptée).</summary>
    public string? EulaAcceptedVersion { get; set; }
    public string? CustomSoundPath { get; set; }
    public Dictionary<string, TimerSettings> Timers { get; set; } = new();
    public Dictionary<string, string> Hotkeys { get; set; } = HotkeyDefaults.Create();
    public List<string> EmittedAlerts { get; set; } = new();
    public CombatSettings Combat { get; set; } = new();
    public HistorySettings History { get; set; } = new();

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
        // v2 : raccourcis par défaut déplacés sur le pavé numérique ; les anciens défauts (F1…) sont remplacés.
        if (SettingsVersion < 2) Hotkeys = HotkeyDefaults.Create();
        // v3 : historique plafonné à 30 jours / 100 combats.
        if (SettingsVersion < 3) { History.RetentionDays = HistorySettings.MaxRetentionDays; History.MaxEncounters = HistorySettings.MaxEncountersCap; }
        SettingsVersion = CurrentVersion;
        Hotkeys ??= new();
        foreach (var (action, gesture) in HotkeyDefaults.Create())
            Hotkeys.TryAdd(action, gesture);
        foreach (var timer in Timers.Values)
            timer.SoonThresholdMinutes = Math.Clamp(timer.SoonThresholdMinutes, 1, 120);
    }
}
