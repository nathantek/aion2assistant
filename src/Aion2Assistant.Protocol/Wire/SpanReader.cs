using System.Buffers.Binary;

namespace Aion2Assistant.Protocol.Wire;

/// <summary>Lecteur séquentiel sans exception : chaque Try* renvoie false s'il manque des octets, sans avancer.</summary>
public ref struct SpanReader
{
    private readonly ReadOnlySpan<byte> _data;

    public SpanReader(ReadOnlySpan<byte> data, int position = 0)
    {
        _data = data;
        Position = position;
    }

    public int Position { get; set; }
    public readonly int Remaining => _data.Length - Position;
    public readonly ReadOnlySpan<byte> Data => _data;

    public bool TryVarint(out uint value)
    {
        if (!Varint.TryRead(_data, Position, out value, out var length)) return false;
        Position += length;
        return true;
    }

    public bool TryU8(out byte value)
    {
        value = 0;
        if (Remaining < 1) return false;
        value = _data[Position++];
        return true;
    }

    public bool TryU16(out ushort value)
    {
        value = 0;
        if (Remaining < 2) return false;
        value = BinaryPrimitives.ReadUInt16LittleEndian(_data[Position..]);
        Position += 2;
        return true;
    }

    public bool TryU32(out uint value)
    {
        value = 0;
        if (Remaining < 4) return false;
        value = BinaryPrimitives.ReadUInt32LittleEndian(_data[Position..]);
        Position += 4;
        return true;
    }

    public bool TryU64(out ulong value)
    {
        value = 0;
        if (Remaining < 8) return false;
        value = BinaryPrimitives.ReadUInt64LittleEndian(_data[Position..]);
        Position += 8;
        return true;
    }

    public bool TryF32(out float value)
    {
        value = 0;
        if (Remaining < 4) return false;
        value = BinaryPrimitives.ReadSingleLittleEndian(_data[Position..]);
        Position += 4;
        return true;
    }

    public bool TrySkip(int count)
    {
        if (count < 0 || Remaining < count) return false;
        Position += count;
        return true;
    }

    public bool TryBytes(int count, out ReadOnlySpan<byte> bytes)
    {
        bytes = default;
        if (count < 0 || Remaining < count) return false;
        bytes = _data.Slice(Position, count);
        Position += count;
        return true;
    }
}
