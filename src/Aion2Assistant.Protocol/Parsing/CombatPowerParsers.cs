using System.Buffers.Binary;
using Aion2Assistant.Core.Events;
using Aion2Assistant.Protocol.Wire;

namespace Aion2Assistant.Protocol.Parsing;

/// <summary>Puissance de combat : mise à jour 0x1C56, inspection 0x5036, roster de groupe 0x0297 et balayage du 0x4536. Aucun ne lève d'exception.</summary>
public static class CombatPowerParsers
{
    /// <summary>0x1C56 : entité (varint) puis puissance u32 ; le reste du corps est ignoré.</summary>
    public static bool Update(ReadOnlySpan<byte> body, long timeMs, Action<GameEvent> emit)
    {
        var r = new SpanReader(body);
        if (!r.TryVarint(out var id) || !EntityIds.IsValid(id)) return false;
        if (!r.TryU32(out var power) || !CombatPowerRange.IsValid(power)) return false;
        emit(new CombatPowerSeenEvent(timeMs, id, null, power));
        return true;
    }

    /// <summary>0x5036 : inspection d'un joueur, identifié par son nom seulement.</summary>
    public static bool Inspect(ReadOnlySpan<byte> body, long timeMs, Action<GameEvent> emit)
    {
        var r = new SpanReader(body);
        if (!r.TrySkip(2) || !r.TryU8(out var flag) || (flag & 0x01) == 0) return false;
        if (!r.TryU8(out var nameLength) || nameLength is < 1 or > NameText.MaxBytes) return false;
        if (!r.TryBytes(nameLength, out var nameBytes) || !NameText.TryDecode(nameBytes, out var name)) return false;
        // classe u32, 2 × u8, niveau u32, u32 ignoré, puissance u32.
        if (!r.TrySkip(4) || !r.TrySkip(2) || !r.TryU32(out var level) || level is < 1 or > 99) return false;
        if (!r.TrySkip(4) || !r.TryU32(out var power) || !CombatPowerRange.IsValid(power)) return false;
        emit(new CombatPowerSeenEvent(timeMs, 0, name, power));
        return true;
    }

    /// <summary>0x0297 : roster de groupe. Un membre dont la puissance vaut 0 (groupe juste créé) ne donne rien ; un corps tronqué garde les membres déjà lus.</summary>
    public static bool PartyRoster(ReadOnlySpan<byte> body, long timeMs, Action<GameEvent> emit)
    {
        var r = new SpanReader(body);
        if (!r.TrySkip(4) || !r.TryU8(out var groupNameLength) || !r.TrySkip(groupNameLength)) return false;
        // u8, u32, u8, u8, u64, 4 × u8.
        if (!r.TrySkip(1 + 4 + 1 + 1 + 8 + 4)) return false;

        while (r.TryU8(out var mask) && mask == 0x0C)
        {
            if (!r.TrySkip(1 + 8)) break;                                  // slot, dbid
            if (!r.TryU8(out var nameLength) || !r.TryBytes(nameLength, out var nameBytes)) break;
            if (!r.TrySkip(4) || !r.TrySkip(4) || !r.TryU32(out var power)) break; // classe, niveau, puissance
            if (!r.TrySkip(2 + 2 + 1 + 9 + 2)) break;                       // reste du membre (39 + nameLength octets au total)
            if (CombatPowerRange.IsValid(power) && NameText.TryDecode(nameBytes, out var name))
                emit(new CombatPowerSeenEvent(timeMs, 0, name, power));
        }
        return true;
    }

    /// <summary>Balayage du bloc « dbid ; n ; n × (u16, i32) ; niveau ; 0 ; puissance » ; null si 0 ou plusieurs correspondances.</summary>
    internal static long? ScanPlayerRecord(ReadOnlySpan<byte> body, int from)
    {
        long? found = null;
        for (var s = Math.Max(from, 0); s <= body.Length - 21; s++)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(body[s..]) == 0) continue;
            if (BinaryPrimitives.ReadUInt16LittleEndian(body[(s + 4)..]) != 0) continue;
            var server = BinaryPrimitives.ReadUInt16LittleEndian(body[(s + 6)..]);
            if (server is < 1000 or > 2999) continue;
            int n = body[s + 8];
            if (n is < 1 or > 16) continue;
            var p = s + 9 + 6 * n;
            if (p + 12 > body.Length) continue;
            var level = BinaryPrimitives.ReadUInt32LittleEndian(body[p..]);
            if (level is < 1 or > 99 || BinaryPrimitives.ReadUInt32LittleEndian(body[(p + 4)..]) != 0) continue;
            var power = BinaryPrimitives.ReadUInt32LittleEndian(body[(p + 8)..]);
            if (!CombatPowerRange.IsValid(power)) continue;
            if (found is not null) return null;
            found = power;
        }
        return found;
    }
}
