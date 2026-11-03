namespace Kotoba.Core;

public sealed record AlternativeForm(string Expression, string Reading, string[] Tags);
public sealed record DictionaryTerm(string Label, string JapaneseTerm, string Explanation);

public static class EntryPresentation
{
    public static string AnkiFront(string expression, string reading) => TextRules.HasKanji(expression) &&
        !string.IsNullOrWhiteSpace(reading) && reading != expression ? expression + "[" + TextRules.Hiragana(reading) + "]" : expression;

    public static AlternativeForm[] OtherForms(Entry entry, string displayedExpression, string displayedReading)
    {
        var forms = new List<AlternativeForm>();
        foreach (var written in entry.Kanji)
            foreach (var reading in entry.CompatibleReadings(written.Text))
                if (written.Text != displayedExpression || reading.Text != displayedReading)
                    forms.Add(new(written.Text, reading.Text, written.Tags.Concat(reading.Tags).Distinct().ToArray()));
        foreach (var reading in entry.Kana)
        {
            var hasWritten = entry.Kanji.Any(k => entry.CompatibleReadings(k.Text).Contains(reading));
            if (!hasWritten && !(reading.Text == displayedExpression || reading.Text == displayedReading)) forms.Add(new(reading.Text, reading.Text, reading.Tags));
        }
        return forms.DistinctBy(f => (f.Expression, f.Reading)).ToArray();
    }
    public static string Tag(string tag, Func<string, string> fallback) => tag switch
    {
        "n" => "Noun", "n-pr" => "Proper noun", "adj-na" => "Na-adjective", "adj-i" => "I-adjective",
        "adj-ix" => "I-adjective (いい class)", "adj-no" => "Noun used with の", "adj-pn" => "Pre-noun adjective",
        "adv" => "Adverb", "adv-to" => "Adverb with と", "pn" => "Pronoun", "prt" => "Particle",
        "aux" => "Auxiliary", "aux-v" => "Auxiliary verb", "aux-adj" => "Auxiliary adjective", "cop" => "Copula",
        "v1" or "v1-s" => "Ichidan verb", "vs" or "vs-i" or "vs-s" => "Suru verb", "vk" => "Kuru verb",
        "vi" => "Intransitive", "vt" => "Transitive", "exp" => "Expression", "int" => "Interjection",
        "conj" => "Conjunction", "ctr" => "Counter", "num" => "Number", "pref" => "Prefix", "suf" => "Suffix",
        _ when tag.StartsWith("v5", StringComparison.Ordinal) => "Godan verb",
        _ => fallback(tag)
    };
    public static readonly DictionaryTerm[] Terms = [
        new("Noun", "普通名詞 · futsuumeishi", "An ordinary noun naming a thing, person, place or idea. The dictionary's ‘noun (common)’ label is a grammatical category; it is separate from the green Common vocabulary badge."),
        new("Na-adjective", "形容動詞 · keiyōdōshi / keiyodoshi", "A describing word used with な before a noun: 巨大な鉄槌, ‘a huge hammer’. Dictionary entries normally omit the な ending, so its root is 巨大."),
        new("I-adjective", "形容詞 · keiyōshi", "An adjective such as 大きい, ‘big’. It inflects directly, for example 大きかった, ‘was big’. A spelling ending in い alone does not prove this category."),
        new("Pronoun", "代名詞 · daimeishi", "A word standing for a person or thing, such as それ, ‘that’."),
        new("Adverb", "副詞 · fukushi", "A word modifying a verb, adjective or the statement as a whole."),
        new("Particle", "助詞 · joshi", "A grammatical marker such as が, を or に that shows a word's role or connects parts of a phrase."),
        new("Auxiliary / copula", "助動詞 · jodōshi; だ / です", "A grammatical ending or helper expressing features such as tense, negation or politeness. だ and です can express that something is a particular thing or state."),
        new("Godan verb", "五段動詞 · godan dōshi", "A verb class whose final kana changes across forms, for example 書く → 書かない → 書きます. The label describes conjugation, not a difficulty level."),
        new("Ichidan verb", "一段動詞 · ichidan dōshi", "A verb class generally formed by replacing final る, for example 食べる → 食べない → 食べます. Not every verb ending in る is ichidan."),
        new("Transitive / intransitive", "他動詞 / 自動詞 · tadōshi / jidōshi", "Transitive verbs act on an object; intransitive verbs describe an action or change without a direct object. Context still determines which participants are stated."),
        new("Common / JLPT", "Vocabulary badges", "Green Common uses the pinned dictionary's common flags. Blue N5–N1 badges are community JLPT estimates. An absent badge does not mean a word is advanced. These classifications match the pinned Anki Stats baseline.")
    ];
}
