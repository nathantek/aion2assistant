using Aion2Assistant.Core.Events;
using Aion2Assistant.Protocol.Wire;

namespace Aion2Assistant.Protocol.Parsing;

/// <summary>
/// Familiers et âmes (docs/research/familiers-ames-recherche.md). Une valeur hors bornes (identifiant inconnu,
/// longueur incohérente) rejette la trame : elle est comptée dans les rejets de l'onglet Capture, sans exception.
/// Un palier &gt; 3 n'est pas rejeté : il est ramené à 3 (affiché MAX), pour ne pas perdre le reste de l'état.
/// </summary>
public static class FamiliarParsers
{
    private const uint MinId = 1000, MaxId = 9999;
    private const uint MaxTier = 3;
    private const uint MaxSouls = 999;
    private const uint MaxSoulGain = 99;

    /// <summary>
    /// 0x0090 : u32 en-tête (inconnu) | u8 N1 | N1 × (u32 id, u32 id répété, u32 palier) | u8 N2 | N2 × (u32 id, u32 âmes),
    /// puis environ 1 500 octets non décodés (ignorés). Les deux listes sont fusionnées : palier 0 et âmes 0 par défaut.
    /// </summary>
    public static bool State(ReadOnlySpan<byte> body, long timeMs, Action<GameEvent> emit)
    {
        var r = new SpanReader(body);
        if (!r.TryU32(out _)) return false;

        // N ≤ 255 garanti par le u8 ; ordre de première apparition conservé.
        if (!r.TryU8(out var tierCount)) return false;
        var tiers = new Dictionary<int, int>(tierCount);
        var order = new List<int>(tierCount);
        for (var i = 0; i < tierCount; i++)
        {
            if (!r.TryU32(out var id) || !r.TryU32(out var repeated) || !r.TryU32(out var tier)) return false;
            if (!IsValidId(id) || repeated != id) return false;
            if (!tiers.TryAdd((int)id, ClampTier(tier))) return false;
            order.Add((int)id);
        }

        if (!r.TryU8(out var soulCount)) return false;
        var souls = new Dictionary<int, int>(soulCount);
        for (var i = 0; i < soulCount; i++)
        {
            if (!r.TryU32(out var id) || !r.TryU32(out var amount)) return false;
            if (!IsValidId(id) || amount > MaxSouls) return false;
            if (!souls.TryAdd((int)id, (int)amount)) return false;
            if (!tiers.ContainsKey((int)id)) order.Add((int)id);
        }

        var entries = order
            .Select(id => new FamiliarEntry(id, tiers.GetValueOrDefault(id), souls.GetValueOrDefault(id)))
            .ToArray();
        emit(new FamiliarStateEvent(timeMs, entries));
        return true;
    }

    /// <summary>0x0D90 : u8 type (01) | u32 id | u32 incrément (9 octets ; d'éventuels octets en plus sont ignorés).</summary>
    public static bool Soul(ReadOnlySpan<byte> body, long timeMs, Action<GameEvent> emit)
    {
        var r = new SpanReader(body);
        if (!r.TryU8(out var kind) || kind != 1) return false;
        if (!r.TryU32(out var id) || !IsValidId(id)) return false;
        if (!r.TryU32(out var amount) || amount is 0 or > MaxSoulGain) return false;
        emit(new FamiliarSoulEvent(timeMs, (int)id, (int)amount));
        return true;
    }

    /// <summary>
    /// 0x1490 : u16 (0) | u32 id | u32 nouveau palier | description du bonus débloqué (non décodée, ignorée).
    /// Le passage au palier 3 (MAX) n'a jamais été capturé : un palier &gt; 3 inattendu est ramené à 3 (MAX).
    /// </summary>
    public static bool Tier(ReadOnlySpan<byte> body, long timeMs, Action<GameEvent> emit)
    {
        var r = new SpanReader(body);
        if (!r.TryU16(out var zero) || zero != 0) return false;
        if (!r.TryU32(out var id) || !IsValidId(id)) return false;
        if (!r.TryU32(out var tier) || tier == 0) return false;
        emit(new FamiliarTierEvent(timeMs, (int)id, ClampTier(tier)));
        return true;
    }

    private static bool IsValidId(uint id) => id is >= MinId and <= MaxId;

    private static int ClampTier(uint tier) => (int)Math.Min(tier, MaxTier);
}
