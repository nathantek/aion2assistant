using Aion2Assistant.Core.Events;
using Aion2Assistant.Protocol.Wire;

namespace Aion2Assistant.Protocol.Parsing;

/// <summary>Liste des boss de terrain d'une carte (0x0191), telle que la carte en jeu l'affiche.</summary>
public static class FieldBossListParser
{
    private const ulong MinTimeMs = 1_600_000_000_000;
    private const ulong MaxTimeMs = 2_600_000_000_000;

    public static bool Parse(ReadOnlySpan<byte> body, long timeMs, Action<GameEvent> emit)
    {
        var r = new SpanReader(body);
        if (!r.TryU16(out var zero) || zero != 0) return false;
        if (!r.TryU32(out var map) || map is 0 or > 9_999_999) return false;
        if (!r.TryU8(out var count)) return false;

        var slots = new List<FieldBossSlot>(count);
        var position = r.Position;
        for (var k = 0; k < count; k++)
        {
            var isLast = k == count - 1;
            if ((TrySlot(body, position, map, withExtra: false, out var slot, out var next) && Fits(body, next, map, isLast))
                || (TrySlot(body, position, map, withExtra: true, out slot, out next) && Fits(body, next, map, isLast)))
            {
                slots.Add(slot!);
                position = next;
            }
            else
            {
                return false;
            }
        }

        emit(new FieldBossListEvent(timeMs, (int)map, count, slots));
        return true;
    }

    private static bool TrySlot(ReadOnlySpan<byte> body, int at, uint map, bool withExtra, out FieldBossSlot? slot, out int next)
    {
        slot = null;
        next = at;
        var r = new SpanReader(body, at);
        if (!r.TryU8(out var alive) || alive > 1) return false;
        if (!r.TryVarint(out var id) || !InRange(id, map)) return false;
        float x = 0, y = 0, z = 0;
        if (alive == 1 && (!r.TryF32(out x) || !r.TryF32(out y) || !r.TryF32(out z))) return false;
        if (withExtra && !r.TrySkip(1)) return false;
        if (!r.TryU64(out var time)) return false;
        if (time != 0 && (time <= MinTimeMs || time >= MaxTimeMs)) return false;
        slot = new FieldBossSlot((int)id, alive == 1, (long)time, x, y, z);
        next = r.Position;
        return true;
    }

    /// <summary>Après un emplacement : soit l'en-tête du suivant est cohérent, soit (dernier) il ne reste que ≤ 8 zéros.</summary>
    private static bool Fits(ReadOnlySpan<byte> body, int next, uint map, bool isLast)
    {
        if (isLast)
        {
            var rest = body[next..];
            return rest.Length <= 8 && !rest.ContainsAnyExcept((byte)0);
        }
        return next < body.Length && body[next] <= 1
            && Varint.TryRead(body, next + 1, out var id, out _) && InRange(id, map);
    }

    private static bool InRange(uint slotId, uint map) => slotId > map * 100L && slotId < map * 100L + 100;
}
