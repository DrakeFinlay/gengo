using System.Collections.Frozen;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace Kotoba.Infrastructure;

public sealed record KanjiReading(string Value, string Type, string? Status, string? OnType);
public sealed record KanjiSense(KanjiReading[] Readings, string[] Meanings);
public sealed record KanjiEntry(string Character, KanjiSense[] Groups, string[] NameReadings, int[] StrokeCounts, int? Grade);

/// <summary>Verified, bundled KANJIDIC2. Independent of Anki and the word dictionary.</summary>
public static class KanjiDictionary
{
    public const string Version = "3.6.2+20261026175559";
    public const string Sha256 = "4C5969C69A5A3B21F87254A440D3BBC5E92420793BB22D78E4CCAA603AEC2501";
    public const string SourceUrl = "https://github.com/scriptin/jmdict-simplified/releases/download/3.6.2%2B20261026175559/kanjidic2-all-3.6.2%2B20261026175559.json.zip";
    // First use loads off the UI thread; subsequent clicks do one indexed lookup.
    private static readonly Lazy<Task<FrozenDictionary<string, KanjiEntry>>> entries = new(() => Task.Run(Load));
    public static async Task<KanjiEntry?> Find(string character) => (await entries.Value.ConfigureAwait(false)).GetValueOrDefault(character);

    private static FrozenDictionary<string, KanjiEntry> Load()
    {
        using var stream = typeof(KanjiDictionary).Assembly.GetManifestResourceStream("Kotoba.Infrastructure.Resources.kanjidic2-all.json.zip")
            ?? throw new InvalidDataException("Bundled KANJIDIC2 resource is missing.");
        if (Convert.ToHexString(SHA256.HashData(stream)) != Sha256) throw new InvalidDataException("KANJIDIC2 checksum mismatch.");
        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        using var input = archive.Entries.Single(e => e.FullName.EndsWith(".json", StringComparison.Ordinal)).Open();
        using var document = JsonDocument.Parse(input); var root = document.RootElement;
        if (root.GetProperty("fileVersion").GetInt32() != 4 || root.GetProperty("dictDate").GetString() != "2026-10-26") throw new InvalidDataException("Unexpected KANJIDIC2 schema/date.");
        var result = new Dictionary<string, KanjiEntry>(StringComparer.Ordinal);
        foreach (var item in root.GetProperty("characters").EnumerateArray())
        {
            var character = item.GetProperty("literal").GetString()!;
            var misc = item.GetProperty("misc"); var rm = item.GetProperty("readingMeaning");
            var groups = rm.ValueKind == JsonValueKind.Null ? [] : rm.GetProperty("groups").EnumerateArray().Select(g => new KanjiSense(
                g.GetProperty("readings").EnumerateArray().Where(r => r.GetProperty("type").GetString() is "ja_on" or "ja_kun")
                    .Select(r => new KanjiReading(r.GetProperty("value").GetString()!, r.GetProperty("type").GetString()!, r.GetProperty("status").GetString(), r.GetProperty("onType").GetString())).ToArray(),
                g.GetProperty("meanings").EnumerateArray().Where(m => m.GetProperty("lang").GetString() == "en").Select(m => m.GetProperty("value").GetString()!).ToArray())).ToArray();
            result.Add(character, new(character, groups,
                rm.ValueKind == JsonValueKind.Null ? [] : rm.GetProperty("nanori").EnumerateArray().Select(n => n.GetString()!).ToArray(),
                misc.GetProperty("strokeCounts").EnumerateArray().Select(n => n.GetInt32()).ToArray(),
                misc.GetProperty("grade").ValueKind == JsonValueKind.Null ? null : misc.GetProperty("grade").GetInt32()));
        }
        return result.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
