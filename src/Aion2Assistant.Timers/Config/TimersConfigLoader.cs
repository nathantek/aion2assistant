using System.Globalization;
using Aion2Assistant.Core.Localization;
using Aion2Assistant.Core.Storage;
using Aion2Assistant.Timers.Scheduling;

namespace Aion2Assistant.Timers.Config;

public sealed record TimersConfig(ServerTime ServerTime, IReadOnlyList<TimerDefinition> Definitions);

public sealed record TimersConfigLoadResult(TimersConfig Config, IReadOnlyList<string> Errors);

public static class TimersConfigLoader
{
    public static TimersConfigLoadResult Load(string path)
    {
        var loaded = JsonFileStore.Load(path, DefaultTimers.Create);
        var built = Build(loaded.Value);
        if (loaded.Error is null) return built;
        var errors = new List<string> { loaded.Error };
        errors.AddRange(built.Errors);
        return built with { Errors = errors };
    }

    public static TimersConfigLoadResult Build(TimersConfigDto dto)
    {
        var errors = new List<string>();

        TimeZoneInfo zone;
        try
        {
            if (string.IsNullOrWhiteSpace(dto.ServerTimeZone)) throw new FormatException($"Fuseau « {dto.ServerTimeZone} » inconnu");
            zone = ServerTime.ResolveZone(dto.ServerTimeZone);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException or FormatException)
        {
            errors.Add($"Fuseau « {dto.ServerTimeZone} » inconnu : {DefaultTimers.ServerTimeZone} utilisé.");
            zone = ServerTime.ResolveZone(DefaultTimers.ServerTimeZone);
        }

        var english = AppCulture.Language == AppLanguage.English;
        var definitions = new List<TimerDefinition>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var timers = dto.Timers ?? [];
        if (dto.Timers is null)
        {
            errors.Add("Liste « timers » manquante");
        }

        foreach (var timer in timers)
        {
            try
            {
                if (timer is null) throw new FormatException("Timer vide ignoré");
                if (string.IsNullOrWhiteSpace(timer.Id)) throw new FormatException("identifiant manquant");
                if (!ids.Add(timer.Id)) throw new FormatException("identifiant en double");
                definitions.Add(new TimerDefinition(
                    timer.Id,
                    (english ? NonBlank(timer.NameEn) : null) ?? timer.Name ?? timer.Id,
                    timer.Category ?? "other",
                    BuildRule(timer),
                    timer.OpenFor is null ? null : ParseDuration(timer.OpenFor, "openFor"),
                    timer.Verified,
                    (english ? NonBlank(timer.ShortNameEn) : null) ?? NonBlank(timer.ShortName),
                    TimerZone(timer, errors)));
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException)
            {
                var timerId = timer?.Id ?? "?";
                errors.Add($"Timer « {timerId} » ignoré : {ex.Message}");
            }
        }

        return new TimersConfigLoadResult(new TimersConfig(new ServerTime(zone), definitions), errors);
    }

    private static string? NonBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Fuseau propre au timer ; null (fuseau global) s'il est absent ou inconnu, avec une erreur dans ce cas.</summary>
    private static ServerTime? TimerZone(TimerDto timer, List<string> errors)
    {
        if (NonBlank(timer.TimeZone) is not { } id) return null;
        try
        {
            return new ServerTime(ServerTime.ResolveZone(id));
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        {
            errors.Add($"Timer « {timer.Id} » : fuseau « {id} » inconnu, fuseau global utilisé.");
            return null;
        }
    }

    private static IScheduleRule BuildRule(TimerDto timer) => timer.Kind switch
    {
        TimerKind.Recurring => new RecurringRule(ParseTime(timer.Anchor, "anchor"), ParseDuration(timer.Interval, "interval")),
        TimerKind.Daily => new DailyRule(ParseTime(timer.Time, "time")),
        TimerKind.Weekly => new WeeklyRule(
            timer.Day ?? throw new FormatException("champ « day » manquant"),
            ParseTime(timer.Time, "time")),
        _ => throw new FormatException($"type « {timer.Kind} » non pris en charge dans timers.json"),
    };

    private static TimeOnly ParseTime(string? value, string field)
    {
        if (value is null) throw new FormatException($"champ « {field} » manquant");
        if (!TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            throw new FormatException($"« {field} » invalide : {value} (format HH:mm)");
        return time;
    }

    private static TimeSpan ParseDuration(string? value, string field)
    {
        if (value is null) throw new FormatException($"champ « {field} » manquant");
        if (!TimeSpan.TryParseExact(value, @"hh\:mm", CultureInfo.InvariantCulture, out var duration) || duration <= TimeSpan.Zero)
            throw new FormatException($"« {field} » invalide : {value} (format HH:mm)");
        return duration;
    }
}
