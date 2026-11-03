using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kotoba.Core;

namespace Kotoba.Infrastructure;

public sealed record AnkiKanjiMeaning(long NoteId, string Kanji, string Meaning);
public sealed record AnkiKanjiResult(string Profile, AnkiKanjiMeaning[] Notes)
{
    public string DisplayMeaning => Notes.Length == 0 ? "unknown" : string.Join("\n", Notes.Select(n => n.Meaning).Distinct());
}
public sealed record AnkiKanjiOptions(string Deck = "漢字", string ExpressionField = "Front", string MeaningField = "Back");

/// <summary>Only read operations against the loopback AnkiConnect service. No collection writes.</summary>
public sealed class AnkiKanjiClient : IDisposable
{
    private readonly HttpClient http;
    private readonly Dictionary<(string Profile, AnkiKanjiOptions Options, string Kanji), AnkiKanjiResult> cache = [];
    public AnkiKanjiClient(HttpMessageHandler? handler = null)
    {
        http = new(handler ?? new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false })
            { BaseAddress = new Uri("http://127.0.0.1:8765/"), Timeout = TimeSpan.FromSeconds(8) };
    }
    public async Task<AnkiKanjiResult> Find(string kanji, AnkiKanjiOptions options, CancellationToken cancellationToken = default)
    {
        if (TextRules.CharacterCount(kanji) != 1 || !TextRules.HasKanji(kanji)) throw new ArgumentException("Choose one kanji.");
        Validate(options);
        var profile = await Read<string>("getActiveProfile", new { }, cancellationToken);
        var key = (profile, options, kanji); if (cache.TryGetValue(key, out var result)) return result;
        // Anki field searches match stored HTML, so Front:分 misses <div>分</div>.
        // Widen candidate discovery only; cleaned text must still equal the single kanji.
        var query = Quote("deck:" + options.Deck) + " " + Quote(options.ExpressionField + ":*" + kanji + "*");
        var ids = await Read<long[]>("findNotes", new { query }, cancellationToken);
        if (ids.Length > 500) throw new InvalidDataException("More than 500 candidate notes. Narrow the Anki deck/field mapping in Settings.");
        var notes = ids.Length == 0 ? [] : await Read<JsonElement[]>("notesInfo", new { notes = ids }, cancellationToken);
        var meanings = new List<AnkiKanjiMeaning>();
        foreach (var note in notes)
        {
            if (!note.TryGetProperty("fields", out var fields) || !fields.TryGetProperty(options.ExpressionField, out var expression) || !fields.TryGetProperty(options.MeaningField, out var meaning)) continue;
            var written = CleanField(expression.GetProperty("value").GetString() ?? "");
            // A vocabulary card containing the character must never become its kanji keyword.
            if (written != kanji) continue;
            var gloss = CleanField(meaning.GetProperty("value").GetString() ?? "");
            if (gloss.Length > 0) meanings.Add(new(note.GetProperty("noteId").GetInt64(), kanji, gloss));
        }
        if (await Read<string>("getActiveProfile", new { }, cancellationToken) != profile) throw new IOException("Anki profile changed during lookup. Try again.");
        result = new(profile, meanings.ToArray()); if (cache.Count >= 256) cache.Clear();
        // Do not retain misses: adding a previously absent card in Anki should be
        // visible on the next deliberate selection without restarting Kotoba.
        if (result.Notes.Length > 0) cache[key] = result; return result;
    }
    public void ClearCache() => cache.Clear();
    public static void Validate(AnkiKanjiOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Deck) || options.Deck.Length > 200) throw new ArgumentException("Choose an Anki deck in Settings.");
        foreach (var field in new[] { options.ExpressionField, options.MeaningField })
            if (string.IsNullOrWhiteSpace(field) || field.Length > 100 || field.IndexOfAny([':', '"', '\\', '*', '?', '\r', '\n']) >= 0) throw new ArgumentException("Use the exact Anki field names, without search operators.");
    }
    private static string Quote(string term) => "\"" + term.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    public static string CleanField(string html)
    {
        if (html.Length > 100000) throw new InvalidDataException("Anki field is too large.");
        const RegexOptions flags = RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant;
        var text = Regex.Replace(html, @"<(script|style|rt)\b[^>]*>.*?</\1\s*>", "", flags, TimeSpan.FromSeconds(1));
        text = Regex.Replace(text, @"<br\s*/?>|</(?:div|p|li)\s*>", "\n", flags, TimeSpan.FromSeconds(1));
        text = Regex.Replace(text, @"<[^>]*>|\[sound:[^\]]*\]", "", flags, TimeSpan.FromSeconds(1));
        return WebUtility.HtmlDecode(text).Trim();
    }
    private async Task<T> Read<T>(string action, object parameters, CancellationToken cancellationToken)
    {
        // Keep the allowlist here even though no caller can supply an action outside this class.
        if (action is not ("getActiveProfile" or "findNotes" or "notesInfo")) throw new InvalidOperationException("Only read-only Anki operations are supported.");
        using var content = new StringContent(JsonSerializer.Serialize(new { action, version = 6, @params = parameters }), Encoding.UTF8, "application/json");
        using var response = await http.PostAsync("", content, cancellationToken); response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (!json.RootElement.TryGetProperty("result", out var result) || !json.RootElement.TryGetProperty("error", out var error)) throw new InvalidDataException("Invalid AnkiConnect response.");
        if (error.ValueKind != JsonValueKind.Null) throw new IOException("AnkiConnect: " + error.ToString());
        return result.Deserialize<T>() ?? throw new InvalidDataException("Empty AnkiConnect response.");
    }
    public void Dispose() => http.Dispose();
}
