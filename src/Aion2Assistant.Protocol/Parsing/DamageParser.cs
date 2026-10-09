using Aion2Assistant.Core.Events;
using Aion2Assistant.Protocol.Wire;

namespace Aion2Assistant.Protocol.Parsing;

/// <summary>Dégâts directs (0x0438). Valide chaque champ et préfère rejeter plutôt que d'inventer un chiffre.</summary>
public static class DamageParser
{
    private const int MaxHitsField = 25;
    private const uint MaxDamage = 99_999_999;

    private enum Outcome { Emitted, CastMarker, Invalid }

    public static bool Parse(ReadOnlySpan<byte> body, long timeMs, Action<GameEvent> emit)
    {
        var position = 0;
        var emitted = false;
        while (true)
        {
            var outcome = ParseRecord(body, position, timeMs, emit, out var end, out var clean);
            if (outcome == Outcome.CastMarker) return true;
            if (outcome == Outcome.Invalid) return emitted;
            emitted = true;
            if (!clean) return true;

            if (end + 1 < body.Length && body[end] == 0x01 && body[end + 1] == 0x00)
            {
                position = end + 2;
                if (position >= body.Length) return true;
                continue;
            }
            return true;
        }
    }

    private static Outcome ParseRecord(ReadOnlySpan<byte> body, int start, long timeMs, Action<GameEvent> emit, out int end, out bool clean)
    {
        end = body.Length;
        clean = false;
        var r = new SpanReader(body, start);
        if (!r.TryVarint(out var target) || !r.TryVarint(out var sw)) return Outcome.Invalid;

        var layout = (int)(sw & 0x0F);
        if (layout == 0) return Outcome.CastMarker;
        if (layout is < 4 or > 7 || !EntityIds.IsValid(target)) return Outcome.Invalid;

        if (!r.TryVarint(out _)) return Outcome.Invalid;
        if (!r.TryVarint(out var actor) || !EntityIds.IsValid(actor)) return Outcome.Invalid;
        if (!r.TryU32(out var skill) || skill is < 1 or > 299_999_999) return Outcome.Invalid;
        if (!r.TrySkip(1)) return Outcome.Invalid;
        if (!r.TryVarint(out var damageType)) return Outcome.Invalid;

        var critical = damageType == 3;
        var flags = critical ? HitFlags.Critical : HitFlags.None;
        var blockStart = r.Position;
        if (layout >= 5)
        {
            if (body.Length < blockStart + 3) return Outcome.Invalid;
            flags |= ModifierFlags(body[blockStart]) | DirectionFlags(body[blockStart + 2]);
        }

        var found = false;
        long damage = 0;
        var scalar = 0;
        var afterValues = 0;
        foreach (var length in BlockLengths(layout))
        {
            if (TryValues(body, blockStart + length, layout == 6 && critical, out damage, out scalar, out afterValues))
            {
                found = true;
                break;
            }
        }
        if (!found) return Outcome.Invalid;

        int hits;
        if (TryTail(body, afterValues, withField: layout == 4, sw, damage, out hits, out end)
            || (layout != 6 && TryTail(body, afterValues, withField: false, sw, damage, out hits, out end)))
        {
            clean = true;
            end = SkipLifeSteal(body, end);
        }
        else
        {
            hits = 1;
            var lenient = new SpanReader(body, afterValues);
            if ((sw & 0x20) != 0 && lenient.TryVarint(out var count) && count is >= 1 and <= MaxHitsField) hits = 1 + (int)count;
            end = body.Length;
        }

        if (hits > 1) flags |= HitFlags.Multi;

        if (actor == target) emit(new HealEvent(timeMs, actor, target, (int)skill, damage, flags));
        else emit(new DamageEvent(timeMs, actor, target, (int)skill, damage, flags, hits, scalar));
        return Outcome.Emitted;
    }

    private static int[] BlockLengths(int layout) => layout switch
    {
        4 => [8],
        5 => [11, 12],
        6 => [11, 10],
        _ => [11, 14],
    };

    private static bool TryValues(ReadOnlySpan<byte> body, int at, bool critOnLayout6, out long damage, out int scalar, out int end)
    {
        damage = 0;
        scalar = 0;
        end = 0;
        if (at > body.Length) return false;
        var r = new SpanReader(body, at);
        if (!r.TryVarint(out var v1)) return false;
        if (v1 == 0 && !r.TryVarint(out v1)) return false; // zéro de bourrage
        var afterV1 = r.Position;
        if (!r.TryVarint(out var v2)) return false;

        if (critOnLayout6 && v1 is >= 1000 and <= 5_000_000 && v2 <= MaxHitsField)
        {
            // Critique sur disposition 6 : les dégâts viennent en premier, sans scalaire.
            damage = v1;
            end = afterV1;
            return true;
        }

        if (v2 is < 1 or > MaxDamage) return false;
        if (v1 != 0 && v1 is < 1000 or > 200_000) return false;
        damage = v2;
        scalar = (int)v1;
        end = r.Position;
        return true;
    }

    private static bool TryTail(ReadOnlySpan<byte> body, int at, bool withField, uint sw, long damage, out int hits, out int end)
    {
        hits = 1;
        end = at;
        var r = new SpanReader(body, at);
        if (withField && (!r.TryVarint(out var field) || field is < 1 or > MaxHitsField)) return false;

        if ((sw & 0x20) != 0)
        {
            if (!r.TryVarint(out var count) || count is < 1 or > MaxHitsField) return false;
            long sum = 0;
            for (var i = 0; i < count; i++)
            {
                if (!r.TryVarint(out var hit)) return false;
                sum += hit;
            }
            if (sum >= damage) return false;
            hits = 1 + (int)count;
        }

        end = r.Position;
        return end == body.Length || IsMarker(body, end);
    }

    private static bool IsMarker(ReadOnlySpan<byte> body, int at) =>
        at + 1 < body.Length
        && ((body[at] is >= 0x01 and <= 0x07 && body[at + 1] == 0x00) || (body[at] == 0x04 && body[at + 1] == 0x38));

    /// <summary>Suffixe de vol de vie : 03 00 puis un montant (ignoré).</summary>
    private static int SkipLifeSteal(ReadOnlySpan<byte> body, int end)
    {
        if (end + 2 < body.Length && body[end] == 0x03 && body[end + 1] == 0x00
            && Varint.TryRead(body, end + 2, out _, out var length))
        {
            return end + 2 + length;
        }
        return end;
    }

    private static HitFlags ModifierFlags(byte b)
    {
        var flags = HitFlags.None;
        if ((b & 0x02) != 0) flags |= HitFlags.Parry;
        if ((b & 0x04) != 0) flags |= HitFlags.Perfect;
        if ((b & 0x08) != 0) flags |= HitFlags.Double;
        if ((b & 0x20) != 0) flags |= HitFlags.Heavy;
        return flags;
    }

    private static HitFlags DirectionFlags(byte b) => b switch
    {
        0x01 => HitFlags.Back,
        0x02 => HitFlags.Front,
        _ => HitFlags.None,
    };
}
