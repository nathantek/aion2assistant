namespace Aion2Assistant.Timers;

/// <summary>Convertit les heures murales du serveur en instants UTC.</summary>
public sealed class ServerTime(TimeZoneInfo zone)
{
    public TimeZoneInfo Zone { get; } = zone;

    public static TimeZoneInfo ResolveZone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException) when (TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var windowsId))
        {
            return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
        }
    }

    public DateOnly ServerDate(DateTimeOffset utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utc, Zone).DateTime);

    public DateTimeOffset ToUtc(DateOnly day, TimeOnly time)
    {
        var wall = day.ToDateTime(time, DateTimeKind.Unspecified);
        if (Zone.IsInvalidTime(wall))
            wall = wall.AddHours(1); // heure sautée au passage à l'heure d'été : l'événement a lieu juste après
        var offset = Zone.IsAmbiguousTime(wall)
            ? Zone.GetAmbiguousTimeOffsets(wall).Max() // première occurrence = décalage d'été, le plus grand
            : Zone.GetUtcOffset(wall);
        return new DateTimeOffset(wall, offset).ToUniversalTime();
    }
}
