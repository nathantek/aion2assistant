namespace Aion2Assistant.Protocol.Wire;

/// <summary>Entier variable LEB128 non signé, 7 bits par octet, poids faible d'abord, 5 octets maximum.</summary>
public static class Varint
{
    public const int MaxBytes = 5;

    public static bool TryRead(ReadOnlySpan<byte> data, int offset, out uint value, out int length)
    {
        value = 0;
        length = 0;
        var shift = 0;
        for (var i = 0; i < MaxBytes; i++)
        {
            if (offset + i >= data.Length) { value = 0; return false; }
            var b = data[offset + i];
            value |= (uint)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
            {
                length = i + 1;
                return true;
            }
            shift += 7;
        }
        value = 0;
        return false;
    }

    public static byte[] Encode(uint value)
    {
        var bytes = new List<byte>(MaxBytes);
        while (value >= 0x80)
        {
            bytes.Add((byte)(value | 0x80));
            value >>= 7;
        }
        bytes.Add((byte)value);
        return bytes.ToArray();
    }
}
