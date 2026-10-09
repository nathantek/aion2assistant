using Aion2Assistant.Core.Events;

namespace Aion2Assistant.Combat;

public enum MeterKind { Dps, Heal, Taken }
public enum EncounterOutcome { Kill, Wipe, Timeout, Manual, Training }

public sealed record SkillRow(int SkillCode, string Name, int Hits, long Total, long Max, double Average,
    double? CritPct, double? BackPct, double? FrontPct, double? PerfectPct, double? MultiPct, double Share, long? Overheal);

public sealed record ActorRow(uint ActorId, string Name, GameClass Class, bool IsSelf, long Total, double PerSecond,
    double Share, int Hits, int Deaths, IReadOnlyList<SkillRow> Skills, long? CombatPower = null);

public sealed record MeterSnapshot(MeterKind Kind, long Total, double PerSecond, IReadOnlyList<ActorRow> Actors);

public sealed record RecapLine(long AtMs, string Source, string Skill, long Amount, bool IsHeal);
public sealed record DeathRecap(uint ActorId, string Name, long AtMs, IReadOnlyList<RecapLine> Lines);

public sealed record EncounterSnapshot(string Title, long StartMs, long DurationMs, bool Active,
    EncounterOutcome? Outcome, string? TargetName, double? TargetHpPct,
    MeterSnapshot Dps, MeterSnapshot Heal, MeterSnapshot Taken, IReadOnlyList<DeathRecap> Deaths,
    int MapId = 0, bool IsBoss = false, MeterSnapshot? BossDps = null,
    IReadOnlyList<ActorSeries>? DpsTimeline = null, IReadOnlyList<ActorSeries>? BossDpsTimeline = null,
    int TargetNpcCode = 0, bool IsTraining = false, long TrainingDurationMs = 0);

public static class MeterBuilder
{
    public static MeterSnapshot Build(MeterKind kind, IEnumerable<ActorMeter> actors, long durationMs,
        Func<uint, string> nameOf, Func<uint, GameClass> classOf, Func<int, string> skillName, uint selfId,
        Func<uint, long?>? combatPowerOf = null)
    {
        var seconds = Math.Max(1, durationMs / 1000.0);
        var list = actors.OrderByDescending(a => a.Total).ThenBy(a => a.ActorId).ToList();
        var sum = list.Sum(a => a.Total);
        var rows = new List<ActorRow>(list.Count);
        foreach (var a in list)
        {
            var skills = a.Skills.Values.OrderByDescending(s => s.Total).Select(s => new SkillRow(
                s.SkillCode, skillName(s.SkillCode), s.Hits, s.Total, s.Max, s.Total / (double)Math.Max(1, s.Hits),
                Pct(s.Crits, s), Pct(s.Backs, s), Pct(s.Fronts, s), Pct(s.Perfects, s), Pct(s.Multis, s),
                a.Total == 0 ? 0 : s.Total / (double)a.Total,
                s.OverhealKnownRecords == 0 ? null : s.Overheal)).ToList();
            rows.Add(new ActorRow(a.ActorId, nameOf(a.ActorId), classOf(a.ActorId), a.ActorId == selfId, a.Total,
                a.Total / seconds, sum == 0 ? 0 : a.Total / (double)sum, skills.Sum(s => s.Hits), a.Deaths, skills, combatPowerOf?.Invoke(a.ActorId)));
        }
        return new MeterSnapshot(kind, sum, sum / seconds, rows);
    }

    // Les ticks de DoT ne portent aucun drapeau : on les retire du dénominateur, et sans coup direct le taux est inconnu.
    private static double? Pct(int count, SkillStats s)
    {
        var direct = s.Records - s.DotRecords;
        return direct <= 0 ? null : count * 100.0 / direct;
    }
}
