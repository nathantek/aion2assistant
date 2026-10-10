namespace Aion2Assistant.Core.Audio;

/// <summary>Génère les sons d'alerte intégrés par synthèse, pour ne livrer aucun fichier audio.</summary>
public static class WavGenerator
{
    /// <summary>Une note : fréquence (0 = silence), durée, décroissance exponentielle, attaque/relâche (le relâche est un fondu de fin, à régler pour les notes coupées avant d'être éteintes), partiels (rapport de fréquence, amplitude).</summary>
    internal sealed record Note(
        double Frequency,
        double Seconds,
        double Decay = 3,
        double Attack = 0.005,
        double Release = 0,
        (double Ratio, double Gain)[]? Partials = null);

    // Deux fois trois notes montantes (~1,4 s) : assez long pour percer par-dessus le son du jeu.
    private static readonly Note[] ChimeNotes =
    [
        new(880.0, 0.16), new(1108.7, 0.16), new(1318.5, 0.38),
        new(880.0, 0.16), new(1108.7, 0.16), new(1318.5, 0.38),
    ];

    public static byte[] CreateChime(int sampleRate = 44100) => Create(ChimeNotes, sampleRate, normalize: false);

    /// <summary>Synthétise les notes à la suite en PCM 16 bits mono ; <paramref name="normalize"/> ramène le pic à 0,98 du maximum.</summary>
    internal static byte[] Create(IReadOnlyList<Note> notes, int sampleRate = 44100, bool normalize = true)
    {
        var values = new List<double>();
        foreach (var note in notes)
        {
            var count = (int)(sampleRate * note.Seconds);
            var partials = note.Partials;
            var gainSum = partials?.Sum(p => p.Gain) ?? 1.0;
            for (var i = 0; i < count; i++)
            {
                var t = (double)i / sampleRate;
                var envelope = Math.Exp(-note.Decay * t / note.Seconds) * (note.Attack > 0 ? Math.Min(1.0, i / (sampleRate * note.Attack)) : 1.0);
                if (note.Release > 0) envelope *= Math.Min(1.0, (note.Seconds - t) / note.Release);

                double wave;
                if (note.Frequency <= 0) wave = 0;
                else if (partials is null) wave = Math.Sin(2 * Math.PI * note.Frequency * t);
                else
                {
                    wave = 0;
                    foreach (var (ratio, gain) in partials)
                        wave += gain * Math.Sin(2 * Math.PI * note.Frequency * ratio * t);
                    wave /= gainSum;
                }
                values.Add(wave * envelope);
            }
        }

        var scale = short.MaxValue * 0.98;
        if (normalize)
        {
            var peak = values.Count == 0 ? 0 : values.Max(Math.Abs);
            if (peak > 0) scale /= peak;
        }

        var samples = new short[values.Count];
        for (var i = 0; i < samples.Length; i++)
            samples[i] = (short)(values[i] * scale);
        return WriteWav(samples, sampleRate);
    }

    private static byte[] WriteWav(short[] samples, int sampleRate)
    {
        var dataLength = samples.Length * 2;
        using var stream = new MemoryStream(44 + dataLength);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + dataLength);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);          // PCM
        writer.Write((short)1);          // mono
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);    // octets par seconde
        writer.Write((short)2);          // alignement des blocs
        writer.Write((short)16);         // bits par échantillon
        writer.Write("data"u8);
        writer.Write(dataLength);
        foreach (var sample in samples) writer.Write(sample);
        writer.Flush();
        return stream.ToArray();
    }
}
