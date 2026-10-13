using Aion2Assistant.Core.Localization;

namespace Aion2Assistant.Core.Audio;

public sealed record AlertSound(string Id, string Label);

/// <summary>Catalogue des sons d'alerte intégrés (générés, aucun fichier livré).</summary>
public static class AlertSounds
{
    public const string DefaultId = "chime";
    /// <summary>Identifiant réservé au fichier choisi par l'utilisateur.</summary>
    public const string CustomId = "custom";

    private static readonly string[] BuiltInIds = [DefaultId, "bell", "double-beep", "gong", "harp", "alarm"];

    /// <summary>Sons intégrés, libellés dans la langue de l'app.</summary>
    public static IReadOnlyList<AlertSound> BuiltIn =>
    [
        new(DefaultId, Strings.SoundChime),
        new("bell", Strings.SoundBell),
        new("double-beep", Strings.SoundDoubleBeep),
        new("gong", Strings.SoundGong),
        new("harp", Strings.SoundHarp),
        new("alarm", Strings.SoundAlarm),
    ];

    /// <summary>Entrée « Fichier perso… » des listes de sons.</summary>
    public static AlertSound Custom => new(CustomId, Strings.SoundCustomFile);

    public static bool IsBuiltIn(string? id) => id is not null && BuiltInIds.Contains(id);

    /// <summary>WAV du son intégré ; un identifiant inconnu donne le carillon.</summary>
    public static byte[] Create(string id) => id switch
    {
        "bell" => WavGenerator.Create([new(660, 1.5, Decay: 4, Release: FadeOut, Partials: [(1, 1), (2.76, 0.5), (5.4, 0.3), (8.93, 0.15)])]),
        "double-beep" => WavGenerator.Create(
            [Beep(1000, 0.12), Silence(0.08), Beep(1000, 0.12)]),
        "gong" => WavGenerator.Create([new(110, 2.5, Decay: 3, Attack: 0.01, Release: FadeOut, Partials: [(1, 1), (1.5, 0.6), (2.0, 0.5), (2.74, 0.4), (4.1, 0.25)])]),
        "harp" => WavGenerator.Create(
            [Pluck(523.25), Pluck(659.25), Pluck(783.99), Pluck(1046.5), Pluck(1318.5, 0.5)]),
        "alarm" => WavGenerator.Create(
            [.. AlarmBurst(), Silence(0.15), .. AlarmBurst()]),
        _ => WavGenerator.CreateChime(),
    };

    // Fondu de fin de quelques ms : évite le clic quand une note est coupée avant d'être éteinte (le carillon n'en a pas, octets figés).
    private const double FadeOut = 0.005;

    // Bip à enveloppe plate, attaque et relâche adoucies pour éviter les clics.
    private static WavGenerator.Note Beep(double frequency, double seconds) =>
        new(frequency, seconds, Decay: 0, Attack: 0.01, Release: 0.015);

    private static WavGenerator.Note Silence(double seconds) => new(0, seconds);

    // Note pincée : attaque brève, décroissance rapide, un partiel à l'octave pour le timbre.
    private static WavGenerator.Note Pluck(double frequency, double seconds = 0.18) =>
        new(frequency, seconds, Decay: 4, Release: FadeOut, Partials: [(1, 1), (2, 0.3)]);

    private static WavGenerator.Note[] AlarmBurst() =>
        [Beep(1400, 0.1), Silence(0.05), Beep(1400, 0.1), Silence(0.05), Beep(1400, 0.1)];
}
