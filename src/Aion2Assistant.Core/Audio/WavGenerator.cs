namespace Aion2Assistant.Core.Audio;

/// <summary>Génère le « ding » de notification par défaut, pour ne livrer aucun fichier audio.</summary>
public static class WavGenerator
{
    // Deux fois trois notes montantes (~1,4 s) : assez long pour percer par-dessus le son du jeu.
    private static readonly (double Frequency, double Seconds)[] Notes =
        [(880.0, 0.16), (1108.7, 0.16), (1318.5, 0.38), (880.0, 0.16), (1108.7, 0.16), (1318.5, 0.38)];

    public static byte[] CreateChime(int sampleRate = 44100)
    {
        var samples = new List<short>();
        foreach (var (frequency, seconds) in Notes)
        {
            var count = (int)(sampleRate * seconds);
            for (var i = 0; i < count; i++)
            {
                var t = (double)i / sampleRate;
                var attack = Math.Min(1.0, i / (sampleRate * 0.005));
                var envelope = Math.Exp(-3 * t / seconds) * attack;
                samples.Add((short)(Math.Sin(2 * Math.PI * frequency * t) * envelope * short.MaxValue * 0.98));
            }
        }

        var dataLength = samples.Count * 2;
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
