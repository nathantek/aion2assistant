using Aion2Assistant.Core.Events;

namespace Aion2Assistant.Combat;

public sealed class SkillStats
{
    public SkillStats(int skillCode) => SkillCode = skillCode;

    public int SkillCode { get; }
    public int Hits { get; private set; }
    public int Records { get; private set; }
    public long Total { get; private set; }
    public long Max { get; private set; }
    public int Crits { get; private set; }
    public int Backs { get; private set; }
    public int Fronts { get; private set; }
    public int Perfects { get; private set; }
    public int Multis { get; private set; }
    /// <summary>Enregistrements portant HitFlags.Dot : le parseur de DoT ne transmet aucun autre drapeau.</summary>
    public int DotRecords { get; private set; }
    /// <summary>Enregistrements dont le surplus était mesurable (PV de la cible connus).</summary>
    public int OverhealKnownRecords { get; private set; }
    public long Overheal { get; private set; }

    /// <summary>Additionne tous les compteurs de <paramref name="other"/> ; Max = max des deux.</summary>
    public void MergeFrom(SkillStats other)
    {
        Hits += other.Hits;
        Records += other.Records;
        Total += other.Total;
        if (other.Max > Max) Max = other.Max;
        Crits += other.Crits;
        Backs += other.Backs;
        Fronts += other.Fronts;
        Perfects += other.Perfects;
        Multis += other.Multis;
        DotRecords += other.DotRecords;
        OverhealKnownRecords += other.OverhealKnownRecords;
        Overheal += other.Overheal;
    }

    /// <summary>overheal = null quand les PV de la cible sont inconnus.</summary>
    public void Add(long amount, HitFlags flags, int hitCount, long? overheal = null)
    {
        Hits += hitCount;
        Records++;
        Total += amount;
        if (amount > Max) Max = amount;
        if ((flags & HitFlags.Critical) != 0) Crits++;
        if ((flags & HitFlags.Back) != 0) Backs++;
        if ((flags & HitFlags.Front) != 0) Fronts++;
        if ((flags & HitFlags.Perfect) != 0) Perfects++;
        if ((flags & HitFlags.Multi) != 0) Multis++;
        if ((flags & HitFlags.Dot) != 0) DotRecords++;
        if (overheal is { } o)
        {
            OverhealKnownRecords++;
            Overheal += o;
        }
    }
}
