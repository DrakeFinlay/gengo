using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using Kotoba.Core;
using Microsoft.Data.Sqlite;

namespace Kotoba.Infrastructure;

public sealed class PersonalStore
{
    public string DatabasePath { get; }
    public PersonalStore(string directory)
    {
        Directory.CreateDirectory(directory); DatabasePath = Path.Combine(directory, "personal.sqlite");
        using var db = Open();
        using var version = db.CreateCommand(); version.CommandText = "PRAGMA user_version"; var schema = Convert.ToInt32(version.ExecuteScalar());
        if (schema > 2) throw new InvalidDataException("This personal database needs a newer version of Gengo.");
        if (schema == 2) return;
        if (schema == 1)
        {
            // Keep an exact SQLite snapshot before changing an existing personal database.
            var snapshot = Path.Combine(directory, "before-schema-2-" + Guid.NewGuid().ToString("N") + ".sqlite");
            using (var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = snapshot, Pooling = false }.ToString())) { target.Open(); db.BackupDatabase(target); }
            using var migration = db.BeginTransaction(); using var alter = db.CreateCommand(); alter.Transaction = migration;
            alter.CommandText = "ALTER TABLE personal ADD COLUMN category TEXT NOT NULL DEFAULT 'Personal words'; ALTER TABLE personal ADD COLUMN definition_rtf TEXT NOT NULL DEFAULT ''; PRAGMA user_version=2;";
            alter.ExecuteNonQuery(); migration.Commit(); return;
        }
        // Fresh databases start directly at the current schema.
        using var transaction = db.BeginTransaction();
        using var command = db.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE personal(id TEXT PRIMARY KEY,expression TEXT NOT NULL UNIQUE COLLATE BINARY,reading TEXT NOT NULL,definition TEXT NOT NULL,notes TEXT NOT NULL,category TEXT NOT NULL DEFAULT 'Personal words',definition_rtf TEXT NOT NULL DEFAULT '');
            CREATE TABLE searches(id INTEGER PRIMARY KEY,text TEXT NOT NULL,created INTEGER NOT NULL,analysis TEXT NOT NULL);
            CREATE TABLE clicks(id INTEGER PRIMARY KEY,entry_id TEXT NOT NULL,root TEXT NOT NULL,reading TEXT NOT NULL,surface TEXT NOT NULL,created INTEGER NOT NULL,search_id INTEGER REFERENCES searches(id) ON DELETE SET NULL);
            CREATE INDEX clicks_identity_time ON clicks(entry_id,created);
            CREATE INDEX clicks_time ON clicks(created);
            CREATE INDEX searches_time ON searches(created);
            CREATE TABLE settings(key TEXT PRIMARY KEY,value TEXT NOT NULL);
            PRAGMA user_version=2;
            """;
        command.ExecuteNonQuery(); transaction.Commit();
    }
    private SqliteConnection Open()
    {
        var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = DatabasePath, Pooling = false, ForeignKeys = true }.ToString()); db.Open(); return db;
    }
    private static SqliteCommand Command(SqliteConnection db, string sql, params (string Name, object? Value)[] values)
    {
        var command = db.CreateCommand(); command.CommandText = sql;
        foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return command;
    }
    public PersonalEntry[] Entries()
    {
        using var db = Open(); using var command = Command(db, "SELECT id,expression,reading,definition,notes,category,definition_rtf FROM personal ORDER BY expression"); using var reader = command.ExecuteReader();
        var result = new List<PersonalEntry>(); while (reader.Read()) result.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6))); return result.ToArray();
    }
    public PersonalEntry SaveEntry(string? id, string expression, string reading, string definition, string notes, string category = "Personal words", string definitionRtf = "")
    {
        if (string.IsNullOrWhiteSpace(expression) || string.IsNullOrWhiteSpace(definition)) throw new ArgumentException("Expression and definition are required.");
        if (expression.Length > 2000 || definition.Length > 20000 || reading.Length > 2000 || notes.Length > 20000) throw new ArgumentException("This entry is too long.");
        if (category is not ("Personal words" or "Names")) throw new ArgumentException("Choose Personal words or Names.");
        if (definitionRtf.Length > 400000) throw new ArgumentException("Formatted definition is too large.");
        using var db = Open(); using var transaction = db.BeginTransaction();
        using var duplicate = Command(db, "SELECT id FROM personal WHERE expression=$e", ("$e", expression)); duplicate.Transaction = transaction;
        var existing = duplicate.ExecuteScalar() as string;
        if (id is not null && existing is not null && existing != id) throw new ArgumentException("That expression already exists. Edit its existing entry.");
        id ??= existing ?? "personal:" + Guid.NewGuid().ToString("N");
        using var command = Command(db, "INSERT INTO personal(id,expression,reading,definition,notes,category,definition_rtf) VALUES($id,$e,$r,$d,$n,$c,$f) ON CONFLICT(id) DO UPDATE SET expression=$e,reading=$r,definition=$d,notes=$n,category=$c,definition_rtf=$f", ("$id", id), ("$e", expression), ("$r", reading), ("$d", definition), ("$n", notes), ("$c", category), ("$f", definitionRtf)); command.Transaction = transaction; command.ExecuteNonQuery(); transaction.Commit();
        return new(id, expression, reading, definition, notes, category, definitionRtf);
    }
    public void DeleteEntry(string id) { using var db = Open(); using var c = Command(db, "DELETE FROM personal WHERE id=$id", ("$id", id)); c.ExecuteNonQuery(); }
    public long SaveSearch(Analysis analysis)
    {
        using var db = Open(); using var c = Command(db, "INSERT INTO searches(text,created,analysis) VALUES($text,$time,$analysis) RETURNING id", ("$text", analysis.Text), ("$time", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), ("$analysis", JsonSerializer.Serialize(analysis))); return (long)c.ExecuteScalar()!;
    }
    public void RecordClick(Entry entry, string surface, long? searchId, DateTimeOffset? at = null)
    {
        using var db = Open(); using var c = Command(db, "INSERT INTO clicks(entry_id,root,reading,surface,created,search_id) VALUES($id,$root,$reading,$surface,$time,$search)", ("$id", entry.Id), ("$root", entry.Expression), ("$reading", entry.Reading), ("$surface", surface), ("$time", (at ?? DateTimeOffset.UtcNow).ToUnixTimeMilliseconds()), ("$search", searchId)); c.ExecuteNonQuery();
    }
    public SearchRecord[] History(int offset = 0, int limit = 30, DateTimeOffset? from = null, DateTimeOffset? until = null)
    {
        using var db = Open(); using var c = Command(db, "SELECT id,text,created,analysis FROM searches WHERE ($from IS NULL OR created >= $from) AND ($until IS NULL OR created < $until) ORDER BY created DESC,id DESC LIMIT $limit OFFSET $offset", ("$from", from?.ToUnixTimeMilliseconds()), ("$until", until?.ToUnixTimeMilliseconds()), ("$limit", Math.Clamp(limit, 1, 100)), ("$offset", Math.Max(0, offset))); using var r = c.ExecuteReader();
        var records = new List<SearchRecord>(); while (r.Read()) records.Add(new(r.GetInt64(0), r.GetString(1), DateTimeOffset.FromUnixTimeMilliseconds(r.GetInt64(2)), JsonSerializer.Deserialize<Analysis>(r.GetString(3))!)); return records.ToArray();
    }
    public WordStatistic[] Statistics(DateTimeOffset? from = null, DateTimeOffset? until = null, int threshold = 5)
    {
        using var db = Open(); using var c = Command(db, "SELECT entry_id,root,reading,COUNT(*),MIN(created),MAX(created) FROM clicks WHERE ($from IS NULL OR created >= $from) AND ($until IS NULL OR created < $until) GROUP BY entry_id HAVING COUNT(*) >= $threshold ORDER BY COUNT(*) DESC,MAX(created) DESC LIMIT 1000", ("$from", from?.ToUnixTimeMilliseconds()), ("$until", until?.ToUnixTimeMilliseconds()), ("$threshold", threshold)); using var r = c.ExecuteReader();
        var result = new List<WordStatistic>(); while (r.Read()) result.Add(new(r.GetString(0), r.GetString(1), r.GetString(2), r.GetInt32(3), DateTimeOffset.FromUnixTimeMilliseconds(r.GetInt64(4)), DateTimeOffset.FromUnixTimeMilliseconds(r.GetInt64(5)))); return result.ToArray();
    }
    public LookupEvent[] Clicks(string? entryId, DateTimeOffset? from = null, DateTimeOffset? until = null)
    {
        using var db = Open(); using var c = Command(db, "SELECT c.id,c.entry_id,c.root,c.reading,c.surface,c.created,c.search_id,s.text FROM clicks c LEFT JOIN searches s ON s.id=c.search_id WHERE ($id IS NULL OR entry_id=$id) AND ($from IS NULL OR c.created >= $from) AND ($until IS NULL OR c.created < $until) ORDER BY c.created DESC LIMIT 200", ("$id", entryId), ("$from", from?.ToUnixTimeMilliseconds()), ("$until", until?.ToUnixTimeMilliseconds())); using var r = c.ExecuteReader();
        var result = new List<LookupEvent>(); while (r.Read()) result.Add(new(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), DateTimeOffset.FromUnixTimeMilliseconds(r.GetInt64(5)), r.IsDBNull(6) ? null : r.GetInt64(6), r.IsDBNull(7) ? null : r.GetString(7))); return result.ToArray();
    }
    public (DateOnly Day, int Count)[] DailyTotals(DateTimeOffset? from, DateTimeOffset? until, TimeZoneInfo zone)
    {
        // Aggregate by local dates in minute groups, including fractional-hour zones,
        // so indefinite history does not require loading every click into memory.
        using var db = Open(); using var c = Command(db, "SELECT (created/60000)*60000,COUNT(*) FROM clicks WHERE ($from IS NULL OR created >= $from) AND ($until IS NULL OR created < $until) GROUP BY created/60000", ("$from", from?.ToUnixTimeMilliseconds()), ("$until", until?.ToUnixTimeMilliseconds())); using var r = c.ExecuteReader();
        var totals = new Dictionary<DateOnly, int>(); while (r.Read()) { var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(r.GetInt64(0)), zone).DateTime); totals[day] = totals.GetValueOrDefault(day) + r.GetInt32(1); }
        return totals.OrderByDescending(p => p.Key).Select(p => (p.Key, p.Value)).ToArray();
    }
    public void DeleteSearch(long id) { using var db = Open(); using var c = Command(db, "DELETE FROM searches WHERE id=$id", ("$id", id)); c.ExecuteNonQuery(); }
    public void DeleteHistory(DateTimeOffset? from = null, DateTimeOffset? until = null)
    {
        using var db = Open(); using var transaction = db.BeginTransaction();
        foreach (var table in new[] { "clicks", "searches" }) { using var c = Command(db, $"DELETE FROM {table} WHERE ($from IS NULL OR created >= $from) AND ($until IS NULL OR created < $until)", ("$from", from?.ToUnixTimeMilliseconds()), ("$until", until?.ToUnixTimeMilliseconds())); c.Transaction = transaction; c.ExecuteNonQuery(); }
        transaction.Commit();
    }
    public void DeleteClick(long id) { using var db = Open(); using var c = Command(db, "DELETE FROM clicks WHERE id=$id", ("$id", id)); c.ExecuteNonQuery(); }
    public string Setting(string key, string fallback) { using var db = Open(); using var c = Command(db, "SELECT value FROM settings WHERE key=$key", ("$key", key)); return c.ExecuteScalar() as string ?? fallback; }
    public void SetSetting(string key, string value) { using var db = Open(); using var c = Command(db, "INSERT INTO settings VALUES($key,$value) ON CONFLICT(key) DO UPDATE SET value=$value", ("$key", key), ("$value", value)); c.ExecuteNonQuery(); }
    public void Backup(string archive, string resources)
    {
        var temp = Path.Combine(Path.GetDirectoryName(DatabasePath)!, "snapshot-" + Guid.NewGuid().ToString("N") + ".sqlite");
        try
        {
            using (var source = Open()) using (var target = new SqliteConnection($"Data Source={temp};Pooling=False")) { target.Open(); source.BackupDatabase(target); }
            using var snapshot = new SqliteConnection($"Data Source={temp};Mode=ReadOnly;Pooling=False"); snapshot.Open();
            int Count(string table) { using var c = Command(snapshot, $"SELECT COUNT(*) FROM {table}"); return Convert.ToInt32(c.ExecuteScalar(), CultureInfo.InvariantCulture); }
            using var zip = ZipFile.Open(archive, ZipArchiveMode.Create); zip.CreateEntryFromFile(temp, "personal.sqlite", CompressionLevel.Optimal);
            using var json = zip.CreateEntry("manifest.json").Open(); JsonSerializer.Serialize(json, new BackupManifest(1, DateTimeOffset.UtcNow, Count("personal"), Count("searches"), Count("clicks"), resources));
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public static BackupManifest InspectBackup(string archive)
    {
        using var zip = ZipFile.OpenRead(archive);
        if (zip.Entries.Count != 2 || zip.GetEntry("personal.sqlite") is not { Length: <= 1_000_000_000 } || zip.GetEntry("manifest.json") is not { Length: < 10000 } manifest) throw new InvalidDataException("Invalid backup archive.");
        using var stream = manifest.Open(); var info = JsonSerializer.Deserialize<BackupManifest>(stream) ?? throw new InvalidDataException("Empty backup manifest.");
        if (info.Version != 1 || info.PersonalEntries < 0 || info.Searches < 0 || info.Clicks < 0) throw new InvalidDataException("Unsupported backup."); return info;
    }
    public string Restore(string archive)
    {
        var manifest = InspectBackup(archive); var temp = DatabasePath + ".restore-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var zip = ZipFile.OpenRead(archive)) zip.GetEntry("personal.sqlite")!.ExtractToFile(temp);
            using (var db = new SqliteConnection($"Data Source={temp};Mode=ReadOnly;Pooling=False"))
            {
                db.Open(); using var check = Command(db, "PRAGMA integrity_check"); if ((string?)check.ExecuteScalar() != "ok") throw new InvalidDataException("Backup database is damaged.");
                using var schema = Command(db, "PRAGMA user_version"); var backupSchema = Convert.ToInt32(schema.ExecuteScalar()); if (backupSchema is not (1 or 2)) throw new InvalidDataException("Unsupported database schema.");
                if (backupSchema == 2) { using var extra = Command(db, "SELECT category,definition_rtf FROM personal LIMIT 0"); extra.ExecuteNonQuery(); }
                foreach (var pair in new[] { ("personal", manifest.PersonalEntries), ("searches", manifest.Searches), ("clicks", manifest.Clicks) }) { using var c = Command(db, $"SELECT COUNT(*) FROM {pair.Item1}"); if (Convert.ToInt32(c.ExecuteScalar()) != pair.Item2) throw new InvalidDataException("Backup counts do not match manifest."); }
                // Confirm required columns and serialized records before replacing anything.
                using var columns = Command(db, "SELECT p.id,p.expression,p.reading,p.definition,p.notes,s.text,s.created,s.analysis,c.entry_id,c.root,c.reading,c.surface,c.created,c.search_id,k.key,k.value FROM personal p,searches s,clicks c,settings k LIMIT 0"); columns.ExecuteNonQuery();
                using var foreign = Command(db, "PRAGMA foreign_key_check"); using var rows = foreign.ExecuteReader(); if (rows.Read()) throw new InvalidDataException("Broken sentence links in backup.");
                using var personal = Command(db, "SELECT id,expression,reading,definition,notes FROM personal");
                using (var entries = personal.ExecuteReader()) while (entries.Read())
                    if (string.IsNullOrWhiteSpace(entries.GetString(1)) || string.IsNullOrWhiteSpace(entries.GetString(3)) || !entries.GetString(0).StartsWith("personal:", StringComparison.Ordinal)) throw new InvalidDataException("Invalid personal entry in backup.");
                using var analyses = Command(db, "SELECT text,analysis FROM searches");
                using (var searches = analyses.ExecuteReader()) while (searches.Read())
                {
                    var value = JsonSerializer.Deserialize<Analysis>(searches.GetString(1));
                    if (value is null || value.Text != searches.GetString(0) || value.Units is null) throw new InvalidDataException("Invalid saved analysis in backup.");
                    var offset = 0;
                    foreach (var unit in value.Units)
                    {
                        if (unit.Start != offset || unit.Length <= 0 || unit.Length > value.Text.Length - offset || value.Text.Substring(offset, unit.Length) != unit.Surface || unit.EntryIds is null || unit.Tokens is null) throw new InvalidDataException("Invalid saved text spans in backup.");
                        offset += unit.Length;
                    }
                    if (offset != value.Text.Length) throw new InvalidDataException("Incomplete saved analysis in backup.");
                }
            }
            var safety = Path.Combine(Path.GetDirectoryName(DatabasePath)!, $"before-restore-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.zip"); Backup(safety, manifest.ResourceVersion);
            File.Move(temp, DatabasePath, true); _ = new PersonalStore(Path.GetDirectoryName(DatabasePath)!); return safety;
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
