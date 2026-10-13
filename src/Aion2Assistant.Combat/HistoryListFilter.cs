using System.Globalization;
using System.Text;
using Aion2Assistant.Core.Localization;

namespace Aion2Assistant.Combat;

/// <summary>Puce de filtre de la liste de l'historique (une seule active).</summary>
public enum HistoryFilter { All, Boss, Records, Training }

/// <summary>Filtre et recherche de la liste de l'historique : logique pure.</summary>
public static class HistoryListFilter
{
    /// <summary>
    /// Vrai si le combat passe la puce <paramref name="filter"/> et contient <paramref name="search"/> dans le nom du
    /// boss (insensible à la casse et aux accents ; vide = pas de recherche).
    /// </summary>
    public static bool Matches(EncounterSummary row, HistoryFilter filter, bool isRecord, string? search)
    {
        var training = row.Outcome == EncounterOutcome.Training;
        var chip = filter switch
        {
            // Vrais boss seulement (PV max ≥ seuil) ; drapeau inconnu : repli sur l'issue.
            HistoryFilter.Boss => !training && (row.IsBoss ?? EncounterSummary.GuessBoss(row.Outcome)),
            HistoryFilter.Records => isRecord,
            HistoryFilter.Training => training,
            _ => true,
        };
        if (!chip) return false;
        var needle = Normalize(search);
        return needle.Length == 0 || Normalize(row.Boss).Contains(needle, StringComparison.Ordinal);
    }

    /// <summary>Texte sans accents, en minuscules et sans espaces aux bords.</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var decomposed = text.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }
}

/// <summary>Couleur du badge d'issue (la vue la traduit en brosse de thème).</summary>
public enum OutcomeTone { Kill, Wipe, Training, Neutral }

/// <summary>Badge d'issue d'un combat : « Tué », « Wipe 23 % », « Entraînement », « Réinitialisé », « Terminé ».</summary>
public sealed record OutcomeBadge(OutcomeTone Tone, string Text)
{
    /// <param name="targetHpPct">PV restants du boss en %, affichés sur un wipe s'ils sont connus.</param>
    public static OutcomeBadge For(EncounterOutcome? outcome, double? targetHpPct) => outcome switch
    {
        EncounterOutcome.Kill => new(OutcomeTone.Kill, Strings.OutcomeKill),
        // Arrondi au-dessus : un boss encore en vie ne s'affiche jamais à 0 %.
        EncounterOutcome.Wipe => new(OutcomeTone.Wipe, targetHpPct is { } hp
            ? Strings.Format(nameof(Strings.OutcomeWipeHp), AppFormat.Percent(Math.Clamp(Math.Ceiling(hp), 1, 100)))
            : Strings.OutcomeWipe),
        EncounterOutcome.Training => new(OutcomeTone.Training, Strings.OutcomeTraining),
        EncounterOutcome.Manual => new(OutcomeTone.Neutral, Strings.OutcomeReset),
        _ => new(OutcomeTone.Neutral, Strings.OutcomeEnded),
    };
}
