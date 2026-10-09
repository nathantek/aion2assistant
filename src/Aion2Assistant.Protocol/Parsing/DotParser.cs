using Aion2Assistant.Core.Events;
using Aion2Assistant.Protocol.Wire;

namespace Aion2Assistant.Protocol.Parsing;

/// <summary>Effets sur la durée (0x0538). Le code de skill est transmis ×100.</summary>
public static class DotParser
{
    public static bool Parse(ReadOnlySpan<byte> body, long timeMs, Action<GameEvent> emit)
    {
        var r = new SpanReader(body);
        if (!r.TryVarint(out var target) || !EntityIds.IsValid(target)) return false;
        if (!r.TryU8(out var effect)) return false;
        if (!r.TryVarint(out var actor) || !EntityIds.IsValid(actor)) return false;
        if (!r.TryVarint(out _)) return false;
        if (!r.TryU32(out var rawSkill)) return false;
        var skill = rawSkill / 100;
        if (skill is < 1 or > 299_999_999) return false;
        if (!r.TryVarint(out var amount) || amount is < 1 or > 99_999_999) return false;

        switch (effect)
        {
            case 0x02 or 0x0A:
                if (actor != target) emit(new DamageEvent(timeMs, actor, target, (int)skill, amount, HitFlags.Dot, 1, 0));
                return true;
            case 0x01 or 0x09 or 0x0B:
                emit(new HealEvent(timeMs, actor, target, (int)skill, amount, HitFlags.Dot));
                return true;
            default:
                return true; // tick de buff : ignoré volontairement
        }
    }
}
