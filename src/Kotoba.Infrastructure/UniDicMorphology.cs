using Kotoba.Core;
using NMeCab.Specialized;

namespace Kotoba.Infrastructure;

public sealed class UniDicMorphology : IMorphology
{
    private readonly MeCabUniDic21Tagger tagger;
    public string Version => "NMeCab-0.10.2/UniDic-lite-1.0.8";
    public UniDicMorphology(string dictionaryDirectory) => tagger = MeCabUniDic21Tagger.Create(dictionaryDirectory);
    public MorphToken[] Parse(string text, CancellationToken cancellationToken = default)
    {
        var result = new List<MorphToken>();
        // Bound native parsing work and preserve whitespace/newlines not emitted by MeCab.
        var cursor = 0;
        foreach (var line in text.Split('\n'))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var node in tagger.Parse(line))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var start = text.IndexOf(node.Surface, cursor, StringComparison.Ordinal);
                if (start < cursor) throw new InvalidDataException("Analyzer returned an invalid text span.");
                if (start > cursor) result.Add(new(cursor, start - cursor, text[cursor..start], "", "", "space", "", ""));
                // UniDic columns are documented in dicrc. CSV fields containing commas are quoted;
                // only the first 19 fields are needed here and are parsed without splitting quotes.
                var f = Csv(node.Feature);
                string Get(int index) => index < f.Length && f[index] != "*" ? f[index] : "";
                var lemma = Get(10); // orthographic base preserves 湧く rather than the lemma 沸く.
                if (lemma.Length == 0) lemma = Get(7).Split('-')[0];
                result.Add(new(start, node.Surface.Length, node.Surface, lemma, TextRules.Hiragana(Get(17)), Get(0), Get(4), Get(5), Get(7).Split('-')[0]));
                cursor = start + node.Surface.Length;
            }
            var end = cursor;
            var newline = text.IndexOf('\n', end);
            if (newline >= 0) { result.Add(new(end, newline + 1 - end, text[end..(newline + 1)], "", "", "space", "", "")); cursor = newline + 1; }
        }
        if (cursor < text.Length) result.Add(new(cursor, text.Length - cursor, text[cursor..], "", "", "space", "", ""));
        return result.ToArray();
    }
    private static string[] Csv(string value)
    {
        var fields = new List<string>(); var current = new System.Text.StringBuilder(); var quoted = false;
        foreach (var c in value) { if (c == '"') quoted = !quoted; else if (c == ',' && !quoted) { fields.Add(current.ToString()); current.Clear(); } else current.Append(c); }
        fields.Add(current.ToString()); return fields.ToArray();
    }
    public void Dispose() => tagger.Dispose();
}
