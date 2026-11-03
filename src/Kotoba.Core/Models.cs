using System.Text;

namespace Kotoba.Core;

public sealed record WrittenForm(string Text, bool Common, string[] Tags);
public sealed record ReadingForm(string Text, bool Common, string[] AppliesToKanji, string[] Tags);
public sealed record Sense(string[] Glosses, string[] PartsOfSpeech, string[] Misc, string[] Fields,
    string[] Dialects, string[] AppliesToKanji, string[] AppliesToKana, string[] Information);
public sealed record Entry(string Id, WrittenForm[] Kanji, ReadingForm[] Kana, Sense[] Senses, string[] Levels, bool IsPersonal = false)
{
    public bool Common => Kanji.Any(k => k.Common) || Kana.Any(k => k.Common);
    public string Expression => Senses.FirstOrDefault()?.Misc.Contains("uk") == true || Kanji.Length == 0
        ? Kana.OrderByDescending(k => k.Common).First().Text
        : Kanji.OrderByDescending(k => k.Common).First().Text;
    public string Reading => CompatibleReadings(Expression).OrderByDescending(k => k.Common).FirstOrDefault()?.Text ?? "";
    public IEnumerable<ReadingForm> CompatibleReadings(string spelling) => Kana.Where(k =>
        k.Text == spelling || k.AppliesToKanji.Contains("*") || k.AppliesToKanji.Contains(spelling));
    public IEnumerable<Sense> CompatibleSenses(string spelling, string reading) => Senses.Where(s =>
        (s.AppliesToKanji.Contains("*") || s.AppliesToKanji.Contains(spelling) || Kanji.All(k => k.Text != spelling)) &&
        (s.AppliesToKana.Contains("*") || s.AppliesToKana.Contains(reading)));
}
public sealed record PersonalEntry(string Id, string Expression, string Reading, string Definition, string Notes, string Category = "Personal words", string DefinitionRtf = "")
{
    public Entry AsEntry() => new(Id, [new(Expression, false, [])], [new(Reading.Length == 0 ? Expression : Reading, false, ["*"], [])],
        [new([Definition], ["personal entry"], [], [], [], ["*"], ["*"], Notes.Length == 0 ? [] : [Notes])], [], true);
}
// Offsets are UTF-16 code units, matching .NET/WPF; character guards use Unicode scalars.
public sealed record MorphToken(int Start, int Length, string Surface, string Lemma, string Reading,
    string PartOfSpeech, string ConjugationType, string ConjugationForm, string DictionaryLemma = "");
public sealed record Inflection(string Root, string[] Layers, string Explanation);
public sealed record Unit(int Start, int Length, string Surface, string Reading, string[] EntryIds,
    MorphToken[] Tokens, Inflection? Inflection = null, bool Personal = false);
public sealed record Analysis(string Text, Unit[] Units, string AnalyzerVersion, string ResourceVersion);
public sealed record TextSpan(int Start, int Length);
public sealed record GrammarCard(string Pattern, string Meaning, string Explanation, TextSpan[] Spans, string? Uncertainty = null);
public sealed record SearchRecord(long Id, string Text, DateTimeOffset Created, Analysis Analysis);
public sealed record LookupEvent(long Id, string EntryId, string Root, string Reading, string Surface, DateTimeOffset Created, long? SearchId, string? Context);
public sealed record WordStatistic(string EntryId, string Root, string Reading, int Count, DateTimeOffset First, DateTimeOffset Last);
public sealed record BackupManifest(int Version, DateTimeOffset Created, int PersonalEntries, int Searches, int Clicks, string ResourceVersion);
public interface ILexicon
{
    string Version { get; }
    IReadOnlyList<Entry> Find(string expression);
    Entry? Get(string id);
    string Label(string tag);
}
public interface IMorphology : IDisposable
{
    string Version { get; }
    MorphToken[] Parse(string text, CancellationToken cancellationToken = default);
}
public static class TextRules
{
    public static int CharacterCount(string text) => text.EnumerateRunes().Count();
    public static bool NeedsApproval(string text) => CharacterCount(text) > 5000;
    public static string Hiragana(string text) => string.Concat(text.Select(c => c is >= 'ァ' and <= 'ヶ' ? (char)(c - 0x60) : c));
    public static bool HasKanji(string text) => text.EnumerateRunes().Any(r => r.Value is >= 0x3400 and <= 0x9fff or >= 0x20000 and <= 0x3134f || r.Value == '々');
    public static (DateTimeOffset? From, DateTimeOffset? Until) DateBounds(DateTime? from, DateTime? through, TimeZoneInfo zone)
    {
        DateTimeOffset? Convert(DateTime? day) => day is null ? null : new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(day.Value.Date, DateTimeKind.Unspecified), zone));
        return (Convert(from), Convert(through?.AddDays(1)));
    }
}
