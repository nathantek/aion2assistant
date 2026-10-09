namespace Aion2Assistant.Capture;

public static class HeartbeatScanner
{
    /// <summary>Heartbeats « crédibles » d'un segment : 0E 00 36 (11 octets) ou 06 00 36 (3 octets), non suivis de 0xFF.</summary>
    public static int Count(ReadOnlySpan<byte> payload)
    {
        var count = 0;
        for (var i = 0; i + 2 < payload.Length; i++)
        {
            int size;
            if (payload[i] == 0x0E) size = 11;
            else if (payload[i] == 0x06) size = 3;
            else continue;
            if (payload[i + 1] != 0x00 || payload[i + 2] != 0x36) continue;
            var end = i + size;
            if (end == payload.Length || (end < payload.Length && payload[end] != 0xFF)) count++;
        }
        return count;
    }

    /// <summary>Enregistrement TLS (le même serveur sert aussi le lanceur et le web) : à ignorer.</summary>
    public static bool LooksLikeTls(ReadOnlySpan<byte> payload) =>
        payload.Length >= 3 && payload[0] is >= 0x14 and <= 0x17 && payload[1] == 0x03 && payload[2] <= 0x04;
}
