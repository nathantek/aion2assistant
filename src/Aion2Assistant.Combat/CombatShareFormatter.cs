using System.Text;
using Aion2Assistant.Core.Localization;

namespace Aion2Assistant.Combat;

/// <summary>
/// Résumé d'un meter sur une seule ligne, à coller dans le chat du jeu : pas de retour à la ligne,
/// pas d'émoji ni de séparateur exotique (le chat les afficherait en carrés).
/// </summary>
public static class CombatShareFormatter
{
    public const int MaxPlayers = 8;

    public static string Format(EncounterSnapshot snapshot, MeterKind kind, bool bossOnly = false)
    {
        var boss = bossOnly && kind == MeterKind.Dps ? snapshot.BossDps : null;
        var meter = kind switch
        {
            MeterKind.Heal => snapshot.Heal,
            MeterKind.Taken => snapshot.Taken,
            _ => boss ?? snapshot.Dps,
        };
        var label = kind switch
        {
            MeterKind.Heal => Strings.ShareHeal,
            MeterKind.Taken => Strings.ShareTaken,
            _ => boss is null ? Strings.ShareDps : Strings.ShareBossDps,
        };

        var sb = new StringBuilder();
        sb.Append('[').Append(label).Append("] ").Append(Clean(snapshot.Title)).Append(' ')
          .Append(Duration(snapshot.DurationMs)).Append(' ').Append(StateLabel(snapshot));

        var rank = 0;
        foreach (var actor in meter.Actors.Take(MaxPlayers))
        {
            sb.Append(++rank == 1 ? " | " : " ").Append(rank).Append(". ").Append(Clean(actor.Name)).Append(' ');
            sb.Append(Compact(actor.Total));
            sb.Append(" (").Append((actor.Share * 100).ToString("0", AppCulture.Current)).Append('%');
            if (kind == MeterKind.Taken && actor.Deaths > 0)
                sb.Append(", ").Append(Strings.Format(actor.Deaths == 1 ? nameof(Strings.ShareDeath) : nameof(Strings.ShareDeaths), actor.Deaths));
            sb.Append(')');
        }
        return sb.ToString();
    }

    /// <summary>État du combat tel qu'affiché dans l'en-tête des meters.</summary>
    public static string StateLabel(EncounterSnapshot snapshot) => snapshot.Active
        ? (snapshot.IsTraining ? Strings.StateTraining : Strings.StateLive)
        : OutcomeLabel(snapshot.Outcome);

    /// <summary>Issue d'un combat terminé (null = terminé sans cause connue).</summary>
    public static string OutcomeLabel(EncounterOutcome? outcome) => outcome switch
    {
        EncounterOutcome.Training => Strings.OutcomeTraining,
        EncounterOutcome.Kill => Strings.OutcomeKill,
        EncounterOutcome.Wipe => Strings.OutcomeWipe,
        EncounterOutcome.Manual => Strings.OutcomeReset,
        _ => Strings.OutcomeEnded,
    };

    /// <summary>Une ligne pour le dernier combat : « Vakron · 4:50 · Tué · 52,3k/s ».</summary>
    public static string SummaryLine(EncounterSummary s)
    {
        var name = !string.IsNullOrWhiteSpace(s.Boss) ? s.Boss : !string.IsNullOrWhiteSpace(s.Zone) ? s.Zone : Strings.EncounterFallbackTitle;
        return $"{Clean(name)} · {Duration(s.DurationMs)} · {OutcomeLabel(s.Outcome)} · {Compact(s.GroupDps)}/s";
    }

    private static string Compact(double v) => AppFormat.CompactTight(v);

    private static string Duration(long ms)
    {
        var seconds = Math.Max(0, ms) / 1000;
        return $"{seconds / 60}:{seconds % 60:00}";
    }

    private static string Clean(string text) =>
        string.Concat(text.Select(c => char.IsControl(c) ? ' ' : c)).Trim();
}
