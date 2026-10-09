using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;

namespace Aion2Assistant.Combat;

public sealed record EncounterSummary(long Id, DateTimeOffset StartedAt, long DurationMs, string Zone, string Boss,
    EncounterOutcome Outcome, double GroupDps);

/// <summary>Historique des combats dans une base SQLite. Not thread-safe: thread UI uniquement.</summary>
public sealed class HistoryStore : IDisposable
{
    private static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly SqliteConnection _db;

    public HistoryStore(string databasePath)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        _db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString());
        _db.Open();
        Exec("""
            CREATE TABLE IF NOT EXISTS encounters (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                started_at TEXT NOT NULL,
                duration_ms INTEGER NOT NULL,
                zone TEXT NOT NULL,
                boss TEXT NOT NULL,
                outcome TEXT NOT NULL,
                group_dps REAL NOT NULL,
                snapshot_json TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_encounters_started_at ON encounters(started_at);
            """);
    }

    public static bool ShouldSave(EncounterSnapshot s, HistoryOptions o) => s.DurationMs >= o.MinDurationMs || s.IsBoss || s.IsTraining;

    public long Save(EncounterSnapshot snapshot, string zone)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO encounters (started_at, duration_ms, zone, boss, outcome, group_dps, snapshot_json)
            VALUES ($started, $duration, $zone, $boss, $outcome, $dps, $json);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$started", DateTimeOffset.FromUnixTimeMilliseconds(snapshot.StartMs).UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$duration", snapshot.DurationMs);
        cmd.Parameters.AddWithValue("$zone", zone);
        cmd.Parameters.AddWithValue("$boss", snapshot.Title);
        cmd.Parameters.AddWithValue("$outcome", (snapshot.Outcome ?? EncounterOutcome.Manual).ToString());
        cmd.Parameters.AddWithValue("$dps", snapshot.Dps.PerSecond);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(snapshot, Json));
        return (long)cmd.ExecuteScalar()!;
    }

    public IReadOnlyList<EncounterSummary> List(int limit = 1000)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            SELECT id, started_at, duration_ms, zone, boss, outcome, group_dps
            FROM encounters ORDER BY started_at DESC, id DESC LIMIT $limit;
            """;
        cmd.Parameters.AddWithValue("$limit", limit);
        var list = new List<EncounterSummary>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            if (!Enum.TryParse<EncounterOutcome>(r.GetString(5), out var outcome)
                || !DateTimeOffset.TryParse(r.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var startedAt))
                continue;
            list.Add(new EncounterSummary(r.GetInt64(0), startedAt,
                r.GetInt64(2), r.GetString(3), r.GetString(4), outcome, r.GetDouble(6)));
        }
        return list;
    }

    public EncounterSnapshot? Load(long id)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT snapshot_json FROM encounters WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        if (cmd.ExecuteScalar() is not string json) return null;
        try
        {
            return JsonSerializer.Deserialize<EncounterSnapshot>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public int Purge(DateTimeOffset nowUtc, HistoryOptions o)
    {
        var removed = 0;
        using (var cmd = _db.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM encounters WHERE started_at < $cutoff;";
            cmd.Parameters.AddWithValue("$cutoff", nowUtc.UtcDateTime.AddDays(-o.RetentionDays).ToString("O", CultureInfo.InvariantCulture));
            removed += cmd.ExecuteNonQuery();
        }
        using (var cmd = _db.CreateCommand())
        {
            cmd.CommandText = """
                DELETE FROM encounters WHERE id IN (
                    SELECT id FROM encounters ORDER BY started_at DESC, id DESC LIMIT -1 OFFSET $max);
                """;
            cmd.Parameters.AddWithValue("$max", Math.Max(0, o.MaxEncounters));
            removed += cmd.ExecuteNonQuery();
        }
        return removed;
    }

    public void Dispose()
    {
        _db.Dispose();
        SqliteConnection.ClearAllPools();
    }

    private void Exec(string sql)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
