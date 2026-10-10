using Aion2Assistant.Core.Settings;

namespace Aion2Assistant.Core.Audio;

/// <summary>Identité d'un son à jouer : identifiant et, pour « perso », le chemin du fichier.</summary>
public sealed record SoundRef(string Id, string? CustomPath);

/// <summary>Détermine le fichier WAV à jouer pour un réglage de son.</summary>
public static class SoundResolver
{
    /// <summary>Son d'un timer : le sien s'il en a un, sinon le réglage global (ancien son perso inclus).</summary>
    public static SoundRef ForTimer(AppSettings settings, TimerSettings timer) =>
        timer.SoundId is { } id ? new SoundRef(id, timer.CustomSoundPath) : new SoundRef(settings.SoundId, settings.CustomSoundPath);

    public static string FileName(string id) => id + ".wav";

    /// <summary>Son personnalisé s'il existe, sinon le son intégré choisi ; tout le reste retombe sur le carillon.</summary>
    public static string Resolve(string soundId, string? customPath, string soundsDirectory, Func<string, bool> fileExists)
    {
        if (soundId == AlertSounds.CustomId && !string.IsNullOrWhiteSpace(customPath) && fileExists(customPath))
            return customPath;
        var id = AlertSounds.IsBuiltIn(soundId) ? soundId : AlertSounds.DefaultId;
        return Path.Combine(soundsDirectory, FileName(id));
    }
}
