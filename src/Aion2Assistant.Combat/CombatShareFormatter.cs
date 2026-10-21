using System.Text;
using Aion2Assistant.Core.Localization;

namespace Aion2Assistant.Combat;

/// <summary>
/// Résumé d'un meter sur une seule ligne, à coller dans le chat du jeu : pas de retour à la ligne,
/// pas d'émoji ni de séparateur exotique (le chat les afficherait en carrés).
/// Forme condensée : « Lakshmi 1:18 Tué | Kosis 1,2M 34% Pseudo2 900k 25% +4 », tenue dans <see cref="ChatBudget"/>.
/// </summary>
public static class CombatShareFormatter
{
    /// <summary>Plafond de joueurs, même si le budget en laisserait passer davantage.</summary>
    public const int MaxPlayers = 8;

    /// <summary>
    /// Longueur maximale du texte collé. Le chat d'AION 2 coupe à 111 caractères ASCII ; 105 garde une marge.
    /// </summary>
    public const int ChatBudget = 105;

    /// <summary>Longueur maximale d'un pseudo ou du nom court du boss, points de suspension compris.</summary>
    public const int MaxNameLength = 12;

    /// <summary>
    /// Mesure d'un texte pour le budget du chat. Octets UTF-8 par prudence (un « é » compte pour 2, on ne peut jamais
    /// dépasser) ; si le chat compte en caractères, remplacer par <c>s =&gt; s.Length</c>.
    /// </summary>
    public static readonly Func<string, int> ChatLength = s => Encoding.UTF8.GetByteCount(s);

    private const string Ellipsis = "…";

    public static string Format(EncounterSnapshot snapshot, MeterKind kind, bool bossOnly = false)
    {
        var boss = bossOnly && kind == MeterKind.Dps ? snapshot.BossDps : null;
        var meter = kind switch
        {
            MeterKind.Heal => snapshot.Heal,
            MeterKind.Taken => snapshot.Taken,
            _ => boss ?? snapshot.Dps,
        };

        var header = $"{ShortTitle(snapshot.Title)} {Duration(snapshot.DurationMs)} {StateLabel(snapshot)}";
        var actors = meter.Actors;
        if (actors.Count == 0) return header;

        var text = new StringBuilder(header).Append(" |");
        var shown = 0;
        foreach (var actor in actors.Take(MaxPlayers))
        {
            var candidate = text + " " + Player(actor, kind);
            if (ChatLength(candidate + Suffix(actors.Count - shown - 1)) > ChatBudget) break;
            text.Clear().Append(candidate);
            shown++;
        }

        var result = text + Suffix(actors.Count - shown);
        // Garde-fou : un en-tête si long que même « | +N » ne tient pas (titre et issue sont courts en pratique).
        return ChatLength(result) <= ChatBudget ? result : header;
    }

    private static string Suffix(int hidden) => hidden > 0 ? " +" + hidden : "";

    private static string Player(ActorRow actor, MeterKind kind)
    {
        var text = $"{Shorten(Clean(actor.Name))} {Compact(actor.Total)} {(actor.Share * 100).ToString("0", AppCulture.Current)}%";
        return kind == MeterKind.Taken && actor.Deaths > 0 ? $"{text} x{actor.Deaths}" : text;
    }

    /// <summary>
    /// Nom court du boss : dernier mot du titre (« Phantasmal Lakshmi » → « Lakshmi »). Titres par défaut gardés entiers :
    /// « Combat » et « PNJ 12345 » (PNJ absent de npcs.json, sinon il ne resterait que le code).
    /// </summary>
    public static string ShortTitle(string title)
    {
        var clean = Clean(title);
        if (clean.Length == 0 || clean == Strings.EncounterFallbackTitle || IsNpcFallback(clean)) return clean;
        var words = clean.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return Shorten(words[^1]);
    }

    /// <summary>Titre produit par le repli de GameData (« NPC {0} » / « PNJ {0} » dans la langue de l'app) : motif + chiffres.</summary>
    private static bool IsNpcFallback(string title)
    {
        var parts = Strings.NpcFallback.Split("{0}");
        if (parts.Length != 2) return false;
        var (prefix, suffix) = (parts[0], parts[1]);
        if (title.Length <= prefix.Length + suffix.Length || !title.StartsWith(prefix, StringComparison.Ordinal)
            || !title.EndsWith(suffix, StringComparison.Ordinal)) return false;
        return title[prefix.Length..^suffix.Length].All(char.IsAsciiDigit);
    }

    /// <summary>Tronque à <see cref="MaxNameLength"/> caractères, « … » compris, sans couper une paire de substitution.</summary>
    private static string Shorten(string text)
    {
        if (text.Length <= MaxNameLength) return text;
        var cut = MaxNameLength - Ellipsis.Length;
        if (char.IsHighSurrogate(text[cut - 1])) cut--;
        return text[..cut] + Ellipsis;
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
