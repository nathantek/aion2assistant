using Aion2Assistant.Core.Events;
using Aion2Assistant.Protocol.Wire;

namespace Aion2Assistant.Protocol.Parsing;

public static class SimpleParsers
{
    private const ulong MaxHp = 10_000_000_000;

    /// <summary>0x008D : PV restants d'un PNJ (sorte 2 ; 1 = joueur, ignoré). Pas de PV max dans ce paquet.</summary>
    public static bool RemainHp(ReadOnlySpan<byte> body, long timeMs, Action<GameEvent> emit)
    {
        var r = new SpanReader(body);
        if (!r.TryVarint(out var entity) || !EntityIds.IsValid(entity)) return false;
        if (!r.TryVarint(out var kind) || !r.TryVarint(out _) || !r.TryVarint(out _)) return false;
        if (!r.TryU64(out var hp) || hp > MaxHp) return false;
        if (kind == 2) emit(new HpEvent(timeMs, entity, (long)hp, 0));
        return true;
    }

    /// <summary>0x1B92 : PV courants et max (joueurs et PNJ).</summary>
    public static bool HpMp(ReadOnlySpan<byte> body, long timeMs, Action<GameEvent> emit)
    {
        var r = new SpanReader(body);
        if (!r.TryVarint(out var actor) || !EntityIds.IsValid(actor)) return false;
        if (!r.TryVarint(out var hp) || !r.TryVarint(out var max) || max == 0 || hp > max) return false;
        emit(new HpEvent(timeMs, actor, hp, max));
        return true;
    }

    /// <summary>0x4236 : drapeau 1 = chargé déjà mort, 3 = mort au combat, autres valeurs ignorées.</summary>
    public static bool Death(ReadOnlySpan<byte> body, long timeMs, Action<GameEvent> emit)
    {
        var r = new SpanReader(body);
        if (!r.TryVarint(out var entity) || !EntityIds.IsValid(entity)) return false;
        if (!r.TryVarint(out _) || !r.TryVarint(out var flag)) return false;
        if (flag == 1) emit(new DeathEvent(timeMs, entity, true));
        else if (flag == 3) emit(new DeathEvent(timeMs, entity, false));
        return true;
    }

    /// <summary>0x048D : entité retirée + tueur (avec son nom). Tout à zéro = une invocation qui disparaît.</summary>
    public static bool KillCredit(ReadOnlySpan<byte> body, long timeMs, Action<GameEvent> emit)
    {
        var r = new SpanReader(body);
        if (!r.TryVarint(out var entity) || !EntityIds.IsValid(entity)) return false;
        if (!r.TryU32(out var middle) || !r.TryVarint(out var killer)) return false;
        if (middle == 0 && killer == 0) return true;

        emit(new DeathEvent(timeMs, entity, false));
        if (EntityIds.IsValid(killer)
            && r.TryU16(out var server) && server is >= 1000 and < 3000
            && r.TryU8(out var length) && length is >= 1 and <= NameText.MaxBytes
            && r.TryBytes(length, out var nameBytes) && NameText.TryDecode(nameBytes, out var name))
        {
            emit(new PlayerSeenEvent(timeMs, killer, name, server, GameClass.Unknown));
        }
        return true;
    }

    /// <summary>0x2136 : chargement de carte. La même carte deux fois de suite = téléportation dans la carte.</summary>
    public static bool MapLoad(ReadOnlySpan<byte> body, long timeMs, ref int lastMapId, Action<GameEvent> emit)
    {
        var r = new SpanReader(body);
        if (!r.TryU32(out _) || !r.TryU32(out var map) || map is 0 or > 9_999_999) return false;
        var mapId = (int)map;
        var teleport = mapId == lastMapId;
        lastMapId = mapId;
        emit(new ZoneChangedEvent(timeMs, mapId, mapId is >= 600_000 and <= 699_999, teleport));
        return true;
    }

    /// <summary>0x0238 : annonce de lancement (code de skill brut).</summary>
    public static bool Cast(ReadOnlySpan<byte> body, long timeMs, Action<GameEvent> emit)
    {
        var r = new SpanReader(body);
        if (!r.TryVarint(out var actor) || !EntityIds.IsValid(actor)) return false;
        if (!r.TryVarint(out _)) return false;
        if (!r.TryU32(out var skill) || skill is < 1 or > 299_999_999) return false;
        if (!r.TrySkip(2)) return false;
        if (!r.TryVarint(out var target) || !EntityIds.IsValid(target)) return false;
        emit(new CastEvent(timeMs, actor, target, (int)skill));
        return true;
    }
}
