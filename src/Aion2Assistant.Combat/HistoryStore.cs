using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;

namespace Aion2Assistant.Combat;

/// <summary>
/// Ligne de l'historique. Les champs « Self* » viennent du meter DPS du combat (même base que GroupDps) ;
/// ils sont nuls si le personnage n'a pas été détecté ou si le combat est illisible.
/// TrainingTargetMs : durée cible d'un entraînement (0 = libre), nul hors entraînement.
/// IsBoss : cible principale avec PV max ≥ seuil boss à l'enregistrement ; nul si inconnu (voir <see cref="GuessBoss"/>).
/// </summary>
public sealed record EncounterSummary(long Id, DateTimeOffset StartedAt, long DurationMs, string Zone, string Boss,
    EncounterOutcome Outcome, double GroupDps,
    double? SelfDps = null, int? SelfRank = null, int? PartySize = null, double? SelfShare = null,
    double? TargetHpPct = null, long? TrainingTargetMs = null, bool? IsBoss = null)
{
    /// <summary>
    /// Repli pour un ancien combat sans l'information : avant la 0.3.7, seul un boss pouvait finir en Kill
    /// et seul un combat de boss était typiquement suivi jusqu'au Wipe.
    /// </summary>
    public static bool GuessBoss(EncounterOutcome outcome) => outcome is EncounterOutcome.Kill or EncounterOutcome.Wipe;

    /// <summary>Ligne d'historique d'un combat qui vient d'être enregistré.</summary>
    public static EncounterSummary From(long id, EncounterSnapshot s, string zone)
    {
        var st = SelfStats.Compute(s);
        return new EncounterSummary(id, DateTimeOffset.FromUnixTimeMilliseconds(s.StartMs), s.DurationMs, zone, s.Title,
            s.Outcome ?? EncounterOutcome.Manual, s.Dps.PerSecond,
            st.SelfDps, st.SelfRank, st.PartySize, st.SelfShare, st.TargetHpPct, st.TrainingTargetMs, s.IsBoss);
    }
}

/// <summary>Chiffres personnels d'un combat, dérivés du meter DPS.</summary>
public readonly record struct SelfStats(double? SelfDps, int? SelfRank, int? PartySize, double? SelfShare,
    double? TargetHpPct, long? TrainingTargetMs)
{
    public static SelfStats Compute(EncounterSnapshot s)
    {
        var actors = s.Dps.Actors;
        int? party = actors.Count > 0 ? actors.Count : null;
        var training = s.IsTraining || s.Outcome == EncounterOutcome.Training;
        long? target = training ? s.TrainingDurationMs : null;
        var self = actors.FirstOrDefault(a => a.IsSelf);
        if (self is null) return new SelfStats(null, null, party, null, s.TargetHpPct, target);
        // Rang 1-based par DPS décroissant ; à égalité, le meilleur rang est partagé.
        var rank = 1 + actors.Count(a => a.PerSecond > self.PerSecond);
        return new SelfStats(self.PerSecond, rank, party, self.Share, s.TargetHpPct, target);
    }
}

/// <summary>Écart d'entraînement : logique pure, sans accès à la base.</summary>
public static class TrainingDelta
{
    /// <summary>
    /// Écart en % du DPS personnel de <paramref name="row"/> par rapport à l'essai d'entraînement précédent (plus ancien)
    /// de même durée cible (0 = libre). Nul hors entraînement ou sans précédent exploitable.
    /// </summary>
    public static double? PercentVsPrevious(EncounterSummary row, IEnumerable<EncounterSummary> all)
    {
        if (row.Outcome != EncounterOutcome.Training || row.SelfDps is not { } now) return null;
        var target = row.TrainingTargetMs ?? 0;
        var previous = all
            .Where(o => o.Id != row.Id && o.Outcome == EncounterOutcome.Training && (o.TrainingTargetMs ?? 0) == target
                        && o.SelfDps is > 0 && (o.StartedAt < row.StartedAt || (o.StartedAt == row.StartedAt && o.Id < row.Id)))
            .OrderByDescending(o => o.StartedAt).ThenByDescending(o => o.Id)
            .FirstOrDefault();
        return previous is null ? null : (now - previous.SelfDps!.Value) / previous.SelfDps.Value * 100.0;
    }

    /// <summary>
    /// Écart arrondi à l'entier : « +4 % », « −4 % », ou « ±0 % » quand |écart| &lt; 0,5 (jamais « −0 % »).
    /// <paramref name="sign"/> vaut 1, -1 ou 0 (neutre).
    /// </summary>
    public static string Format(double pct, out int sign)
    {
        var rounded = Math.Round(Math.Abs(pct), MidpointRounding.AwayFromZero);
        if (rounded == 0)
        {
            sign = 0;
            return "±0 %";
        }
        sign = pct > 0 ? 1 : -1;
        return (sign > 0 ? "+" : "−") + rounded.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " %";
    }
}

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
        try
        {
            _db.Open();
            Initialize();
        }
        catch
        {
            // Pas de connexion orpheline si l'ouverture échoue.
            _db.Dispose();
            throw;
        }
    }

    /// <summary>Erreur du remplissage des anciens combats (null si tout s'est bien passé) ; l'historique reste utilisable.</summary>
    public Exception? BackfillError { get; private set; }

    /// <summary>Combats dont le snapshot n'a pas pu être relu au remplissage (colonnes laissées nulles).</summary>
    public int BackfillSkipped { get; private set; }

    private void Initialize()
    {
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
        AddMissingColumns();
        try
        {
            Backfill();
        }
        catch (Exception ex)
        {
            // Un remplissage raté ne doit jamais rendre l'historique indisponible : les lignes « Toi » restent vides.
            BackfillError = ex;
        }
        try
        {
            BackfillBoss();
        }
        catch (Exception ex)
        {
            // Même règle : sans remplissage, le filtre Boss se rabat sur l'issue (EncounterSummary.GuessBoss).
            BackfillError ??= ex;
        }
    }

    private static readonly (string Name, string Type)[] ExtraColumns =
    [
        ("self_dps", "REAL"), ("self_rank", "INTEGER"), ("party_size", "INTEGER"), ("self_share", "REAL"),
        ("target_hp_pct", "REAL"), ("training_target_ms", "INTEGER"), ("is_boss", "INTEGER"),
    ];

    // Anciennes bases : on ajoute les colonnes absentes sans toucher aux données existantes.
    private void AddMissingColumns()
    {
        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = _db.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(encounters);";
            using var r = cmd.ExecuteReader();
            while (r.Read()) present.Add(r.GetString(1));
        }
        foreach (var (name, type) in ExtraColumns)
            if (!present.Contains(name)) Exec($"ALTER TABLE encounters ADD COLUMN {name} {type};");
    }

    // Remplissage des combats sans chiffres personnels (anciennes bases, ou écrits par une version antérieure) depuis
    // snapshot_json, à chaque ouverture : idempotent, une seule transaction, au plus quelques centaines de lignes.
    // Un snapshot illisible ou inattendu est ignoré (colonnes nulles) et sera retenté à l'ouverture suivante.
    private void Backfill()
    {
        var rows = new List<(long Id, string Json)>();
        using (var cmd = _db.CreateCommand())
        {
            cmd.CommandText = "SELECT id, snapshot_json FROM encounters WHERE party_size IS NULL;";
            using var r = cmd.ExecuteReader();
            while (r.Read()) rows.Add((r.GetInt64(0), r.GetString(1)));
        }
        if (rows.Count == 0) return;
        var updates = new List<(long Id, SelfStats Stats)>();
        foreach (var (id, json) in rows)
        {
            try
            {
                var snap = JsonSerializer.Deserialize<EncounterSnapshot>(json, Json);
                if (snap?.Dps is null) { BackfillSkipped++; continue; }
                var stats = SelfStats.Compute(snap);
                if (stats.PartySize is null) continue; // aucun acteur DPS : rien à écrire
                updates.Add((id, stats));
            }
            catch (Exception)
            {
                BackfillSkipped++;
            }
        }
        if (updates.Count == 0) return;
        using var tx = _db.BeginTransaction();
        using var upd = _db.CreateCommand();
        upd.Transaction = tx;
        upd.CommandText = """
            UPDATE encounters SET self_dps = $sd, self_rank = $sr, party_size = $ps, self_share = $ss,
                target_hp_pct = $hp, training_target_ms = $tt WHERE id = $id;
            """;
        foreach (var (id, stats) in updates)
        {
            upd.Parameters.Clear();
            Bind(upd, stats);
            upd.Parameters.AddWithValue("$id", id);
            upd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    // Remplissage de is_boss : drapeau IsBoss du snapshot s'il y figure (présent depuis la première version),
    // sinon issue Kill/Wipe ⇒ boss. Toujours une valeur écrite : la ligne n'est traitée qu'une fois.
    private void BackfillBoss()
    {
        var rows = new List<(long Id, string Outcome, string Json)>();
        using (var cmd = _db.CreateCommand())
        {
            cmd.CommandText = "SELECT id, outcome, snapshot_json FROM encounters WHERE is_boss IS NULL;";
            using var r = cmd.ExecuteReader();
            while (r.Read()) rows.Add((r.GetInt64(0), r.GetString(1), r.GetString(2)));
        }
        if (rows.Count == 0) return;
        using var tx = _db.BeginTransaction();
        using var upd = _db.CreateCommand();
        upd.Transaction = tx;
        upd.CommandText = "UPDATE encounters SET is_boss = $b WHERE id = $id;";
        foreach (var (id, outcome, json) in rows)
        {
            upd.Parameters.Clear();
            upd.Parameters.AddWithValue("$b", BossFlagOf(outcome, json) ? 1 : 0);
            upd.Parameters.AddWithValue("$id", id);
            upd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    private static bool BossFlagOf(string outcome, string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("IsBoss", out var flag)
                && flag.ValueKind is JsonValueKind.True or JsonValueKind.False)
                return flag.GetBoolean();
        }
        catch (JsonException)
        {
            // Snapshot illisible : heuristique sur l'issue.
        }
        return Enum.TryParse<EncounterOutcome>(outcome, out var o) && EncounterSummary.GuessBoss(o);
    }

    private static void Bind(SqliteCommand cmd, SelfStats st)
    {
        cmd.Parameters.AddWithValue("$sd", (object?)st.SelfDps ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$sr", (object?)st.SelfRank ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ps", (object?)st.PartySize ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ss", (object?)st.SelfShare ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$hp", (object?)st.TargetHpPct ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tt", (object?)st.TrainingTargetMs ?? DBNull.Value);
    }

    private static double? NullableDouble(SqliteDataReader r, int i) => r.IsDBNull(i) ? null : r.GetDouble(i);

    public static bool ShouldSave(EncounterSnapshot s, HistoryOptions o) => s.DurationMs >= o.MinDurationMs || s.IsBoss || s.IsTraining;

    public long Save(EncounterSnapshot snapshot, string zone)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO encounters (started_at, duration_ms, zone, boss, outcome, group_dps, snapshot_json,
                self_dps, self_rank, party_size, self_share, target_hp_pct, training_target_ms, is_boss)
            VALUES ($started, $duration, $zone, $boss, $outcome, $dps, $json,
                $sd, $sr, $ps, $ss, $hp, $tt, $isBoss);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$started", DateTimeOffset.FromUnixTimeMilliseconds(snapshot.StartMs).UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$duration", snapshot.DurationMs);
        cmd.Parameters.AddWithValue("$zone", zone);
        cmd.Parameters.AddWithValue("$boss", snapshot.Title);
        cmd.Parameters.AddWithValue("$outcome", (snapshot.Outcome ?? EncounterOutcome.Manual).ToString());
        cmd.Parameters.AddWithValue("$dps", snapshot.Dps.PerSecond);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(snapshot, Json));
        Bind(cmd, SelfStats.Compute(snapshot));
        cmd.Parameters.AddWithValue("$isBoss", snapshot.IsBoss ? 1 : 0);
        return (long)cmd.ExecuteScalar()!;
    }

    public IReadOnlyList<EncounterSummary> List(int limit = 1000)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            SELECT id, started_at, duration_ms, zone, boss, outcome, group_dps,
                self_dps, self_rank, party_size, self_share, target_hp_pct, training_target_ms, is_boss
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
                r.GetInt64(2), r.GetString(3), r.GetString(4), outcome, r.GetDouble(6),
                NullableDouble(r, 7), r.IsDBNull(8) ? null : r.GetInt32(8), r.IsDBNull(9) ? null : r.GetInt32(9),
                NullableDouble(r, 10), NullableDouble(r, 11), r.IsDBNull(12) ? null : r.GetInt64(12),
                r.IsDBNull(13) ? null : r.GetInt64(13) != 0));
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
