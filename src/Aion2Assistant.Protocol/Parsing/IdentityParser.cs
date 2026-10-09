using Aion2Assistant.Core.Events;
using Aion2Assistant.Protocol.Wire;

namespace Aion2Assistant.Protocol.Parsing;

public static class IdentityParser
{
    public static bool Parse(ReadOnlySpan<byte> body, bool isSelf, long timeMs, Action<GameEvent> emit)
    {
        var r = new SpanReader(body);
        if (!r.TryVarint(out var id) || !EntityIds.IsValid(id)) return false;
        if (!r.TrySkip(4) || !r.TryU8(out var mask) || (mask & 0x01) == 0) return false;
        if (!r.TryU8(out var nameLength) || nameLength is < 1 or > NameText.MaxBytes) return false;
        if (!r.TryBytes(nameLength, out var nameBytes)) return false;

        if (nameBytes[0] == (byte)'$' && nameLength > 1)
        {
            // Personnage de tutoriel pas encore nommé.
            if (isSelf) emit(new SelfIdentifiedEvent(timeMs, id, "", 0, GameClass.Unknown, 0));
            return true;
        }

        if (!NameText.TryDecode(nameBytes, out var name)) return false;

        if (isSelf)
        {
            var tail = r;
            if (tail.TryU16(out var server) && server is >= 1000 and < 3000
                && tail.TryU32(out var wireClass) && GameClasses.FromWire(wireClass) is var gameClass && gameClass != GameClass.Unknown
                && tail.TrySkip(1)
                && tail.TryU32(out var level) && level is >= 1 and <= 99)
            {
                emit(new SelfIdentifiedEvent(timeMs, id, name, server, gameClass, (int)level));
            }
            else
            {
                emit(new SelfIdentifiedEvent(timeMs, id, name, 0, GameClass.Unknown, 0));
            }
            return true;
        }

        var otherClass = r.TryU32(out var wire) ? GameClasses.FromWire(wire) : GameClass.Unknown;
        emit(new PlayerSeenEvent(timeMs, id, name, 0, otherClass));
        return true;
    }
}
