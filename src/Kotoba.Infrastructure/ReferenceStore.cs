using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using Kotoba.Core;
using Microsoft.Data.Sqlite;

namespace Kotoba.Infrastructure;

public sealed class ReferenceStore : ILexicon, IDisposable
{
    public const string DictionaryVersion = "3.6.2+20261026175559";
    public const string JlptRevision = "0d1d3410bec90bd4098a7c72de820543cb4f707c";
    public const string UniDicUrl = "https://files.pythonhosted.org/packages/55/2b/8cf7514cb57d028abcef625afa847d60ff1ffbf0049c36b78faa7c35046f/unidic-lite-1.0.8.tar.gz";
    public const string UniDicHash = "DB9D4572D9FDD4D00A97949D4B0741EC480EE05A7E7E2E32F547500DAE27B245";
    public static readonly string[] Hashes = [
        "C25F1594482FCB55D02F66609584367A04136AF9E4CFAA15C207EB41500BC0D3",
        "09325EB676EC26044E9019AFB5DDD460A394A0D8CB51B0B34D30D03124C131FF",
        "4C7053D1B61F71CA4004252CAFDF89439C65531900E7BAB6C53078802ACF3C5B",
        "17CA6BCBBA9D212C3C9D38C38CECC2ADEF0FD8718CB7157BB48DD9D3A1724948",
        "B38512A6DC1E06A3615F377B3E58184C4A85B6F769C2E3F58F2A4EF0E1810927",
        "CBB221C9A3A92108FCD02FB088D9AFACC70BC8C96570D5730425DE0A49DAD5F3"];
    private readonly SqliteConnection connection;
    private readonly Dictionary<string, Entry> entries = [];
    private readonly Dictionary<string, Entry[]> forms = [];
    private readonly Dictionary<string, string> tags;
    private readonly object gate = new();
    public string Version => DictionaryVersion + "/" + JlptRevision;
    public ReferenceStore(string database)
    {
        connection = new(new SqliteConnectionStringBuilder { DataSource = database, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand(); command.CommandText = "SELECT value FROM metadata WHERE key='tags'";
        tags = JsonSerializer.Deserialize<Dictionary<string, string>>((string)command.ExecuteScalar()!)!;
    }
    public string Label(string tag) => tags.GetValueOrDefault(tag, tag);
    public Entry? Get(string id)
    {
        lock (gate)
        {
            if (entries.TryGetValue(id, out var cached)) return cached;
            using var command = connection.CreateCommand(); command.CommandText = "SELECT json FROM entries WHERE id=$id"; command.Parameters.AddWithValue("$id", id);
            var json = command.ExecuteScalar() as string;
            if (json is null) return null;
            var entry = JsonSerializer.Deserialize<Entry>(json)!;
            if (entries.Count >= 2048) entries.Clear(); entries[id] = entry; return entry;
        }
    }
    public IReadOnlyList<Entry> Find(string expression)
    {
        lock (gate)
        {
            if (forms.TryGetValue(expression, out var cached)) return cached;
            using var command = connection.CreateCommand(); command.CommandText = "SELECT entry_id FROM forms WHERE text=$text ORDER BY entry_id"; command.Parameters.AddWithValue("$text", expression);
            var ids = new List<string>(); using (var reader = command.ExecuteReader()) while (reader.Read()) ids.Add(reader.GetString(0));
            var found = ids.Select(Get).OfType<Entry>().OrderByDescending(e => e.Common).ToArray();
            if (forms.Count >= 4096) forms.Clear(); forms[expression] = found; return found;
        }
    }
    public void Dispose() => connection.Dispose();

    public static async Task<string> Install(string root, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(root);
        var staging = Path.Combine(root, "staging-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(staging);
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Kotoba/0.1");
            var version = Uri.EscapeDataString(DictionaryVersion);
            await Download(client, $"https://github.com/scriptin/jmdict-simplified/releases/download/{version}/jmdict-eng-{version}.json.zip", Path.Combine(staging, "jmdict.zip"), Hashes[0], "JMdict", progress, cancellationToken);
            for (var n = 1; n <= 5; n++) await Download(client, $"https://raw.githubusercontent.com/evanclan/OpenJLPT/{JlptRevision}/data/json/vocab/n{n}.json", Path.Combine(staging, $"n{n}.json"), Hashes[n], $"JLPT N{n}", progress, cancellationToken);
            await Download(client, UniDicUrl, Path.Combine(staging, "unidic.tar.gz"), UniDicHash, "UniDic (45.2 MiB)", progress, cancellationToken);
            progress?.Report("Extracting morphology dictionary…");
            ExtractUniDic(Path.Combine(staging, "unidic.tar.gz"), Path.Combine(staging, "UniDic"), cancellationToken);
            progress?.Report("Building full dictionary index. This may take a minute…");
            var db = Path.Combine(staging, "reference.sqlite"); Build(db, staging, progress, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            // A version directory is immutable. One small pointer is replaced only after verification.
            var installed = "resources-" + Guid.NewGuid().ToString("N"); Directory.Move(staging, Path.Combine(root, installed));
            var temporary = Path.Combine(root, "current.tmp"); await File.WriteAllTextAsync(temporary, installed, cancellationToken);
            File.Move(temporary, Path.Combine(root, "current.txt"), true);
            return Path.Combine(root, installed);
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
    }
    public static string? InstalledDirectory(string root)
    {
        var pointer = Path.Combine(root, "current.txt"); if (!File.Exists(pointer)) return null;
        var name = File.ReadAllText(pointer).Trim();
        if (!name.StartsWith("resources-", StringComparison.Ordinal) || name.IndexOfAny(['/', '\\', ':']) >= 0) throw new InvalidDataException("Invalid resource pointer.");
        var path = Path.Combine(root, name);
        return File.Exists(Path.Combine(path, "reference.sqlite")) && File.Exists(Path.Combine(path, "UniDic", "sys.dic")) ? path : null;
    }
    private static async Task Download(HttpClient client, string url, string path, string hash, string label, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken); response.EnsureSuccessStatusCode();
        using var input = await response.Content.ReadAsStreamAsync(cancellationToken); using var output = File.Create(path);
        var buffer = new byte[128 * 1024]; long total = 0; int count; var last = Environment.TickCount64;
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        while ((count = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += count; if (total > 150_000_000) throw new InvalidDataException("Resource exceeds size limit.");
            await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken); sha.AppendData(buffer, 0, count);
            if (Environment.TickCount64 - last > 150) { progress?.Report($"Downloading {label}: {total / 1048576.0:F1} MiB…"); last = Environment.TickCount64; }
        }
        if (Convert.ToHexString(sha.GetHashAndReset()) != hash) throw new InvalidDataException($"{label} checksum differs from the pinned resource. Previous installation preserved.");
    }
    public static void ExtractUniDic(string archive, string target, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(target);
        using var file = File.OpenRead(archive); using var gzip = new GZipStream(file, CompressionMode.Decompress); using var tar = new TarReader(gzip);
        TarEntry? member;
        var allowed = new HashSet<string> { "char.bin", "matrix.bin", "sys.dic", "unk.dic", "dicrc", "COPYING", "BSD", "AUTHORS", "README.md", "version" };
        while ((member = tar.GetNextEntry()) is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!member.Name.Contains("/dicdir/", StringComparison.Ordinal) || !allowed.Contains(Path.GetFileName(member.Name)) || member.EntryType != TarEntryType.RegularFile) continue;
            using var output = File.Create(Path.Combine(target, Path.GetFileName(member.Name))); member.DataStream!.CopyTo(output);
        }
        foreach (var name in new[] { "char.bin", "matrix.bin", "sys.dic", "unk.dic" }) if (!File.Exists(Path.Combine(target, name))) throw new InvalidDataException("Missing analyzer data.");
        using var probe = new UniDicMorphology(target); if (probe.Parse("日本語").Length == 0) throw new InvalidDataException("Analyzer validation failed.");
    }
    public static void Build(string database, string sourceDirectory, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        // Validate every source even for local developer imports.
        for (var n = 0; n <= 5; n++)
        {
            using var source = File.OpenRead(Path.Combine(sourceDirectory, n == 0 ? "jmdict.zip" : $"n{n}.json"));
            if (Convert.ToHexString(SHA256.HashData(source)) != Hashes[n]) throw new InvalidDataException("Source checksum mismatch.");
        }
        var levels = new Dictionary<string, HashSet<string>>(); var unresolved = 0;
        for (var n = 1; n <= 5; n++)
        {
            using var list = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(sourceDirectory, $"n{n}.json")));
            foreach (var row in list.RootElement.EnumerateArray())
            {
                if (!row.TryGetProperty("jmdict_id", out var id) || id.ValueKind == JsonValueKind.Null) { unresolved++; continue; }
                if (!levels.TryGetValue(id.ToString(), out var set)) levels[id.ToString()] = set = [];
                set.Add(row.GetProperty("level").GetString()!);
            }
        }
        using var zip = ZipFile.OpenRead(Path.Combine(sourceDirectory, "jmdict.zip"));
        using var stream = zip.Entries.Single(e => e.Name == "jmdict-eng-3.6.2.json").Open(); using var json = JsonDocument.Parse(stream);
        using var db = new SqliteConnection($"Data Source={database};Pooling=False"); db.Open();
        using (var command = db.CreateCommand()) { command.CommandText = "PRAGMA journal_mode=DELETE; CREATE TABLE entries(id TEXT PRIMARY KEY,json TEXT NOT NULL,common INTEGER NOT NULL); CREATE TABLE forms(text TEXT NOT NULL,entry_id TEXT NOT NULL,PRIMARY KEY(text,entry_id)) WITHOUT ROWID; CREATE TABLE levels(entry_id TEXT NOT NULL,level TEXT NOT NULL,PRIMARY KEY(entry_id,level)); CREATE TABLE metadata(key TEXT PRIMARY KEY,value TEXT NOT NULL);"; command.ExecuteNonQuery(); }
        using var transaction = db.BeginTransaction();
        using var insert = db.CreateCommand(); insert.Transaction = transaction; insert.CommandText = "INSERT INTO entries VALUES($id,$json,$common)";
        insert.Parameters.Add("$id", SqliteType.Text); insert.Parameters.Add("$json", SqliteType.Text); insert.Parameters.Add("$common", SqliteType.Integer); insert.Prepare();
        using var form = db.CreateCommand(); form.Transaction = transaction; form.CommandText = "INSERT OR IGNORE INTO forms VALUES($text,$id)"; form.Parameters.Add("$text", SqliteType.Text); form.Parameters.Add("$id", SqliteType.Text); form.Prepare();
        using var level = db.CreateCommand(); level.Transaction = transaction; level.CommandText = "INSERT INTO levels VALUES($id,$level)"; level.Parameters.Add("$id", SqliteType.Text); level.Parameters.Add("$level", SqliteType.Text); level.Prepare();
        string[] Strings(JsonElement o, string key) => o.TryGetProperty(key, out var a) ? a.EnumerateArray().Select(v => v.GetString()!).ToArray() : [];
        var ids = new HashSet<string>(); var count = 0;
        foreach (var word in json.RootElement.GetProperty("words").EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var id = word.GetProperty("id").GetString()!; ids.Add(id);
            var entry = new Entry(id,
                word.GetProperty("kanji").EnumerateArray().Select(k => new WrittenForm(k.GetProperty("text").GetString()!, k.GetProperty("common").GetBoolean(), Strings(k, "tags"))).ToArray(),
                word.GetProperty("kana").EnumerateArray().Select(k => new ReadingForm(k.GetProperty("text").GetString()!, k.GetProperty("common").GetBoolean(), Strings(k, "appliesToKanji"), Strings(k, "tags"))).ToArray(),
                word.GetProperty("sense").EnumerateArray().Select(s => new Sense(s.GetProperty("gloss").EnumerateArray().Where(g => g.GetProperty("lang").GetString() == "eng").Select(g => g.GetProperty("text").GetString()!).ToArray(), Strings(s, "partOfSpeech"), Strings(s, "misc"), Strings(s, "field"), Strings(s, "dialect"), Strings(s, "appliesToKanji"), Strings(s, "appliesToKana"), Strings(s, "info"))).ToArray(),
                levels.TryGetValue(id, out var memberships) ? memberships.Order().ToArray() : []);
            insert.Parameters[0].Value = id; insert.Parameters[1].Value = JsonSerializer.Serialize(entry); insert.Parameters[2].Value = entry.Common ? 1 : 0; insert.ExecuteNonQuery();
            foreach (var text in entry.Kanji.Select(k => k.Text).Concat(entry.Kana.Select(k => k.Text))) { form.Parameters[0].Value = text; form.Parameters[1].Value = id; form.ExecuteNonQuery(); }
            foreach (var membership in entry.Levels) { level.Parameters[0].Value = id; level.Parameters[1].Value = membership; level.ExecuteNonQuery(); }
            if (++count % 10000 == 0) progress?.Report($"Indexed {count:N0} entries…");
        }
        unresolved += levels.Where(k => !ids.Contains(k.Key)).Sum(k => k.Value.Count);
        using var meta = db.CreateCommand(); meta.Transaction = transaction; meta.CommandText = "INSERT INTO metadata VALUES($key,$value)"; meta.Parameters.Add("$key", SqliteType.Text); meta.Parameters.Add("$value", SqliteType.Text);
        foreach (var pair in new Dictionary<string,string> { ["version"] = DictionaryVersion, ["jlpt"] = JlptRevision, ["tags"] = json.RootElement.GetProperty("tags").GetRawText(), ["unresolved"] = unresolved.ToString(), ["schema"] = "1" }) { meta.Parameters[0].Value = pair.Key; meta.Parameters[1].Value = pair.Value; meta.ExecuteNonQuery(); }
        transaction.Commit();
    }
}
