using System.Globalization;
using Aion2Assistant.Core.Events;

namespace Aion2Assistant.Combat;

/// <summary>
/// Pseudos arrivés après coup : un instantané figé (ou enregistré) garde « #id » pour un joueur pas encore annoncé.
/// Ces helpers purs complètent le nom (et la classe inconnue) d'un acteur, sans toucher aux noms déjà réels.
/// </summary>
public static class SnapshotNames
{
    /// <summary>
    /// Renomme l'acteur <paramref name="actorId"/> partout où il s'appelle encore « #id » : lignes des meters, séries
    /// des courbes, récaps de mort et sources des lignes de récap. Renvoie la même instance si rien ne change.
    /// </summary>
    public static EncounterSnapshot Rename(EncounterSnapshot s, uint actorId, string name, GameClass cls)
    {
        var placeholder = Placeholder(actorId);
        if (name == placeholder) return s;
        var changed = false;

        MeterSnapshot Meter(MeterSnapshot m)
        {
            if (!m.Actors.Any(a => a.ActorId == actorId && a.Name == placeholder)) return m;
            changed = true;
            return m with
            {
                Actors = m.Actors.Select(a => a.ActorId == actorId && a.Name == placeholder
                    ? a with { Name = name, Class = a.Class == GameClass.Unknown ? cls : a.Class }
                    : a).ToList(),
            };
        }

        IReadOnlyList<ActorSeries>? Series(IReadOnlyList<ActorSeries>? list)
        {
            if (list is null || !list.Any(x => x.ActorId == actorId && x.Name == placeholder)) return list;
            changed = true;
            return list.Select(x => x.ActorId == actorId && x.Name == placeholder
                ? x with { Name = name, Class = x.Class == GameClass.Unknown ? cls : x.Class }
                : x).ToList();
        }

        DeathRecap Death(DeathRecap d)
        {
            var renameSelf = d.ActorId == actorId && d.Name == placeholder;
            if (!renameSelf && !d.Lines.Any(l => l.Source == placeholder)) return d;
            changed = true;
            return d with
            {
                Name = renameSelf ? name : d.Name,
                Lines = d.Lines.Select(l => l.Source == placeholder ? l with { Source = name } : l).ToList(),
            };
        }

        var dps = Meter(s.Dps);
        var heal = Meter(s.Heal);
        var taken = Meter(s.Taken);
        var bossDps = s.BossDps is null ? null : Meter(s.BossDps);
        var dpsTimeline = Series(s.DpsTimeline);
        var bossTimeline = Series(s.BossDpsTimeline);
        var deaths = s.Deaths.Select(Death).ToList();
        if (!changed) return s;
        return s with
        {
            Dps = dps, Heal = heal, Taken = taken, BossDps = bossDps,
            DpsTimeline = dpsTimeline, BossDpsTimeline = bossTimeline, Deaths = deaths,
        };
    }

    /// <summary>Ids encore nommés « #id » : lignes des meters, récaps de mort et sources des lignes de récap.</summary>
    public static HashSet<uint> UnnamedIds(EncounterSnapshot s)
    {
        var ids = new HashSet<uint>();
        foreach (var m in new[] { s.Dps, s.Heal, s.Taken, s.BossDps })
            if (m is not null)
                foreach (var a in m.Actors)
                    if (a.Name == Placeholder(a.ActorId)) ids.Add(a.ActorId);
        foreach (var d in s.Deaths)
        {
            if (d.Name == Placeholder(d.ActorId)) ids.Add(d.ActorId);
            // Les lignes de récap ne gardent que le texte : « #id » y est relu.
            foreach (var l in d.Lines)
                if (l.Source.StartsWith('#')
                    && uint.TryParse(l.Source.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var id))
                    ids.Add(id);
        }
        return ids;
    }

    /// <summary>Vrai si un acteur des meters Dps ou Soins s'appelle encore « #id » (message « change de zone »).</summary>
    public static bool HasUnnamedPlayers(EncounterSnapshot? s) =>
        s is not null && (s.Dps.Actors.Any(IsUnnamed) || s.Heal.Actors.Any(IsUnnamed));

    private static bool IsUnnamed(ActorRow a) => a.Name == Placeholder(a.ActorId);

    private static string Placeholder(uint id) => $"#{id}";
}
