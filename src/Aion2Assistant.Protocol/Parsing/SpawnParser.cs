using System.Buffers.Binary;
using Aion2Assistant.Core.Events;
using Aion2Assistant.Protocol.Wire;

namespace Aion2Assistant.Protocol.Parsing;

/// <summary>Apparition d'une entité (0x4136) : PNJ, invocation, esprit ou effet persistant.</summary>
public static class SpawnParser
{
    private const int MarkerSearch = 60;
    private const int HpSearch = 64;
    private const int SpiritSearch = 128;
    private static readonly byte[] SpiritAnchor = [0x80, 0x75, 0xD5, 0x2A, 0xBB, 0x03, 0x00, 0x00];
    private static readonly byte[] SummonKinds = [0x5F, 0x1C, 0x1F, 0x1D, 0x5D];
    private static readonly byte[] SpiritKinds = [0x5F, 0x1F, 0x1D, 0x5D];

    public static bool Parse(ReadOnlySpan<byte> body, long timeMs, Action<GameEvent> emit)
    {
        var r = new SpanReader(body);
        if (!r.TryVarint(out var raw)) return false;
        var id = EntityIds.FoldSpawnId(raw);
        if (!EntityIds.IsValid(id)) return false;
        var maskStart = r.Position;
        if (!r.TryU32(out var mask)) return false;
        var kind = (byte)(mask & 0xFF);

        string? inlineName = null;
        var cursor = maskStart + 4;
        if (maskStart + 6 <= body.Length && (body[maskStart + 4] & 0x01) != 0)
        {
            var length = body[maskStart + 5];
            if (length is >= 1 and <= NameText.MaxBytes && maskStart + 6 + length <= body.Length
                && NameText.TryDecode(body.Slice(maskStart + 6, length), out var name))
            {
                inlineName = name;
                cursor = maskStart + 6 + length;
            }
        }

        var npcCode = FindNpcCode(body, maskStart, out var markerEnd);
        float x = 0, y = 0, z = 0;
        long maxHp = 0;
        if (markerEnd >= 0)
        {
            ReadPosition(body, markerEnd, out x, out y, out z);
            maxHp = FindMaxHp(body, markerEnd);
        }

        var summonLike = SummonKinds.Contains(kind);
        if (kind == 0x5F && (mask & 0x10) != 0 && FindParentKey(body, cursor, id, out var parent))
        {
            emit(new SummonSeenEvent(timeMs, id, parent, null, npcCode));
            return true;
        }
        if (summonLike && inlineName is not null)
        {
            emit(new SummonSeenEvent(timeMs, id, 0, inlineName, npcCode));
            return true;
        }
        if (SpiritKinds.Contains(kind))
        {
            emit(new SummonSeenEvent(timeMs, id, FindSpiritOwner(body, maskStart), null, npcCode));
            return true;
        }
        if (kind is 0x0C or 0x0D || (!summonLike && npcCode != 0))
        {
            emit(new NpcSeenEvent(timeMs, id, npcCode, maxHp, x, y, z));
        }
        return true;
    }

    /// <summary>Code PNJ = u24 juste avant le marqueur 00 (00|40) 02.</summary>
    private static int FindNpcCode(ReadOnlySpan<byte> body, int maskStart, out int markerEnd)
    {
        markerEnd = -1;
        var last = Math.Min(maskStart + MarkerSearch, body.Length - 3);
        for (var i = maskStart + 3; i <= last; i++)
        {
            if (body[i] == 0x00 && (body[i + 1] & 0xBF) == 0x00 && body[i + 2] == 0x02)
            {
                markerEnd = i + 3;
                return body[i - 3] | body[i - 2] << 8 | body[i - 1] << 16;
            }
        }
        return 0;
    }

    private static void ReadPosition(ReadOnlySpan<byte> body, int at, out float x, out float y, out float z)
    {
        x = y = z = 0;
        if (at + 12 > body.Length) return;
        var px = BinaryPrimitives.ReadSingleLittleEndian(body[at..]);
        var py = BinaryPrimitives.ReadSingleLittleEndian(body[(at + 4)..]);
        var pz = BinaryPrimitives.ReadSingleLittleEndian(body[(at + 8)..]);
        if (Plausible(px) && Plausible(py) && Plausible(pz))
        {
            x = px;
            y = py;
            z = pz;
        }
    }

    private static bool Plausible(float v) => float.IsFinite(v) && Math.Abs(v) < 1e7f;

    private static long FindMaxHp(ReadOnlySpan<byte> body, int from)
    {
        var last = Math.Min(from + HpSearch, body.Length - 1);
        for (var i = from; i < last; i++)
        {
            if (body[i] != 0x01) continue;
            if (!Varint.TryRead(body, i + 1, out var current, out var l1)) continue;
            if (!Varint.TryRead(body, i + 1 + l1, out var max, out _)) continue;
            if (current != 0 && max >= current) return max;
        }
        return 0;
    }

    /// <summary>Propriétaire d'une invocation : 06 · parent u32 · légion u32 · 00 00 · serveur u16 · longueur u8 · nom de légion.</summary>
    private static bool FindParentKey(ReadOnlySpan<byte> body, int cursor, uint ownId, out uint parent)
    {
        parent = 0;
        for (var i = Math.Max(cursor, 1); i + 13 <= body.Length; i++)
        {
            if (body[i - 1] != 0x06) continue;
            var candidate = BinaryPrimitives.ReadUInt32LittleEndian(body[i..]);
            if (!EntityIds.IsValid(candidate) || candidate == ownId) continue;
            if (BinaryPrimitives.ReadUInt16LittleEndian(body[(i + 8)..]) != 0) continue;
            var server = BinaryPrimitives.ReadUInt16LittleEndian(body[(i + 10)..]);
            if (server is < 1000 or >= 3000) continue;
            var length = body[i + 12];
            if (length is < 1 or > 48 || i + 13 + length > body.Length) continue;
            if (!NameText.TryDecode(body.Slice(i + 13, Math.Min((int)length, NameText.MaxBytes)), out _)) continue;
            parent = candidate;
            return true;
        }
        return false;
    }

    private static uint FindSpiritOwner(ReadOnlySpan<byte> body, int maskStart)
    {
        var window = body.Slice(maskStart, Math.Min(SpiritSearch, body.Length - maskStart));
        var at = window.IndexOf(SpiritAnchor);
        if (at < 0) return 0;
        var after = maskStart + at + SpiritAnchor.Length;
        return Varint.TryRead(body, after, out var owner, out _) && EntityIds.IsValid(owner) ? owner : 0;
    }
}
