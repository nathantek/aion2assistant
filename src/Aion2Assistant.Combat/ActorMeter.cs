using Aion2Assistant.Core.Events;

namespace Aion2Assistant.Combat;

public sealed class ActorMeter
{
    private readonly Dictionary<int, SkillStats> _skills = new();

    public ActorMeter(uint actorId) => ActorId = actorId;

    public uint ActorId { get; }
    public long Total { get; private set; }
    public int Deaths { get; set; }
    public IReadOnlyDictionary<int, SkillStats> Skills => _skills;

    /// <summary>Fusionne compétence par compétence ; Total additionné, Deaths inchangé.</summary>
    public void MergeFrom(ActorMeter other)
    {
        foreach (var (code, stats) in other._skills)
        {
            if (!_skills.TryGetValue(code, out var s)) _skills[code] = s = new SkillStats(code);
            s.MergeFrom(stats);
        }
        Total += other.Total;
    }

    public void Add(int skillCode, long amount, HitFlags flags, int hitCount, long? overheal = null)
    {
        if (!_skills.TryGetValue(skillCode, out var s)) _skills[skillCode] = s = new SkillStats(skillCode);
        s.Add(amount, flags, hitCount, overheal);
        Total += amount;
    }
}
