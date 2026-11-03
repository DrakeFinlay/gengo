namespace Kotoba.Core;

public sealed class AnalysisEngine(ILexicon lexicon, IMorphology morphology)
{
    public Analysis Analyze(string text, IReadOnlyList<PersonalEntry> personal, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Enter Japanese text first.");
        var units = new List<Unit>(); var cursor = 0; var ordinaryStart = 0;
        var overrides = personal.Where(e => e.Expression.Length > 0).OrderByDescending(e => e.Expression.Length).ToArray();
        while (cursor < text.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var match = overrides.FirstOrDefault(e => text.AsSpan(cursor).StartsWith(e.Expression, StringComparison.Ordinal));
            if (match is null) { cursor += char.IsHighSurrogate(text[cursor]) && cursor + 1 < text.Length && char.IsLowSurrogate(text[cursor + 1]) ? 2 : 1; continue; }
            AddOrdinary(ordinaryStart, cursor);
            units.Add(new(cursor, match.Expression.Length, match.Expression, match.Reading,
                new[] { match.Id }.Concat(lexicon.Find(match.Expression).Select(e => e.Id)).ToArray(), [], null, true));
            cursor += match.Expression.Length; ordinaryStart = cursor;
        }
        AddOrdinary(ordinaryStart, text.Length);
        return new(text, units.ToArray(), morphology.Version + "/grouping-2", lexicon.Version);

        void AddOrdinary(int start, int end)
        {
            if (start == end) return;
            var tokens = morphology.Parse(text[start..end], cancellationToken).Select(t => t with { Start = t.Start + start }).ToArray();
            for (var i = 0; i < tokens.Length;)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var first = tokens[i]; var chosenEnd = i; var chosen = Array.Empty<Entry>(); string root = first.Lemma;
                // Merge a span only with dictionary and reading evidence. Particle-crossing spans
                // must be lexical expressions/adverbs, rather than arbitrary longest substrings.
                for (var j = i; j < Math.Min(tokens.Length, i + 8); j++)
                {
                    if (tokens[j].PartOfSpeech is "space" or "補助記号" or "記号") break;
                    var span = text[first.Start..(tokens[j].Start + tokens[j].Length)];
                    var reading = string.Concat(tokens[i..(j + 1)].Select(t => t.Reading));
                    var candidates = lexicon.Find(span).Where(e => j == i ||
                        (e.Kana.Any(k => TextRules.Hiragana(k.Text) == reading) &&
                        (tokens[i..(j + 1)].All(t => t.PartOfSpeech is "名詞" or "形状詞" or "接頭辞" or "接尾辞") ||
                         (first.PartOfSpeech == "動詞" && first.ConjugationForm.StartsWith("連用形", StringComparison.Ordinal) && tokens[(i + 1)..(j + 1)].All(t => t.PartOfSpeech is "名詞" or "接尾辞") && e.Senses.Any(s => s.PartsOfSpeech.Contains("n"))) ||
                         e.Senses.Any(s => s.PartsOfSpeech.Any(p => p is "exp" or "adv" or "adv-to"))))).ToArray();
                    if (candidates.Length == 0) continue;
                    chosenEnd = j; chosen = candidates; root = span;
                }
                if (chosenEnd == i && first.PartOfSpeech is "動詞" or "形容詞" or "助動詞")
                {
                    // Prefer the morphology's root, validated against the dictionary, to a
                    // coincidentally matching noun/short surface form.
                    var rooted = lexicon.Find(first.Lemma).Where(e => e.Senses.Any(s => s.PartsOfSpeech.Any(p => p.StartsWith('v') || p.StartsWith("adj", StringComparison.Ordinal) || p.StartsWith("aux", StringComparison.Ordinal)))).ToArray();
                    if (rooted.Length > 0) { chosen = rooted; root = first.Lemma; }
                    if (IsPotential(first))
                    {
                        var underlying = lexicon.Find(first.DictionaryLemma).Where(e => e.Senses.Any(s => s.PartsOfSpeech.Any(p => p.StartsWith('v')))).ToArray();
                        if (underlying.Length > 0) { chosen = underlying; root = first.DictionaryLemma; }
                    }
                    var j = i + 1;
                    if (chosen.Length > 0)
                    {
                        while (j < tokens.Length && tokens[j].Start == tokens[j - 1].Start + tokens[j - 1].Length)
                        {
                            var next = tokens[j];
                            var auxiliary = next.PartOfSpeech == "助動詞" && next.Lemma != "だ";
                            var connective = next.PartOfSpeech == "助詞" && next.Surface is "て" or "で" or "ば";
                            var helping = next.PartOfSpeech is "動詞" or "形容詞" && next.Lemma is "いる" or "居る" or "おく" or "置く" or "しまう" or "仕舞う" or "ない";
                            if (!auxiliary && !connective && !helping) break;
                            chosenEnd = j++;
                        }
                    }
                }
                // な is the attributive ending of a dictionary-validated na-adjective.
                // Keep its morphology for grammar, but open the adjective's root entry.
                var attributiveNa = chosenEnd + 1 < tokens.Length && chosen.Length > 0 &&
                    chosen.Any(e => e.Senses.Any(s => s.PartsOfSpeech.Contains("adj-na"))) &&
                    tokens[chosenEnd + 1] is { Surface: "な", Lemma: "だ", PartOfSpeech: "助動詞" } ending &&
                    ending.ConjugationForm.StartsWith("連体形", StringComparison.Ordinal) &&
                    ending.Start == tokens[chosenEnd].Start + tokens[chosenEnd].Length;
                if (attributiveNa) chosenEnd++;
                var group = tokens[i..(chosenEnd + 1)];
                var length = group[^1].Start + group[^1].Length - first.Start;
                var surface = text.Substring(first.Start, length);
                var joinedReading = string.Concat(group.Select(t => t.Reading));
                // Unknown text has no fabricated ruby. Morphology may still supply useful
                // readings for recognized names even without a JMdict entry.
                var ruby = first.PartOfSpeech is "space" or "補助記号" ? "" : joinedReading;
                if (chosen.Length > 0)
                {
                    chosen = chosen.OrderByDescending(e => e.Kana.Any(k => TextRules.Hiragana(k.Text) == (chosenEnd == i ? first.Reading : joinedReading)))
                        .ThenByDescending(e => e.Common).ToArray();
                    if (ruby.Length == 0) ruby = chosen[0].CompatibleReadings(surface).FirstOrDefault()?.Text ?? "";
                }
                var inflection = attributiveNa
                    ? new Inflection(root, ["attributive な: connects a na-adjective to the noun it describes"], "The dictionary form is " + root + ". な marks its use before a noun (or nominalized phrase).")
                    : ExplainInflection(group, root);
                units.Add(new(first.Start, length, surface, ruby, chosen.Select(e => e.Id).Distinct().ToArray(), group, inflection));
                i = chosenEnd + 1;
            }
        }
    }
    private static Inflection? ExplainInflection(MorphToken[] tokens, string root)
    {
        var first = tokens[0]; var layers = new List<string>();
        if (first.DictionaryLemma == root && IsPotential(first)) layers.Add("potential: can / be able to");
        foreach (var t in tokens.Skip(1))
        {
            var layer = t.Lemma switch
            {
                "れる" => "passive: be acted upon (potential/honorific uses can also occur)",
                "られる" => "passive / potential / honorific: context determines the reading",
                "せる" or "させる" => "causative: make / let someone do",
                "ない" or "ぬ" or "ず" => "negative",
                "た" => "past / completed",
                "ます" => "polite",
                "です" => "polite copula",
                "まい" => "negative conjecture / negative intention (context dependent)",
                "いる" or "居る" => "progressive / resulting state",
                "しまう" or "仕舞う" => "completion / unintended result",
                "おく" or "置く" => "preparation / leaving a state",
                _ => t.Surface switch { "て" or "で" => "connective te-form", "ば" => "conditional: if / when", _ => "" }
            };
            if (layer.Length > 0) layers.Add(layer);
        }
        if (tokens[^1].ConjugationForm.StartsWith("連用形", StringComparison.Ordinal) && first.PartOfSpeech == "動詞") layers.Add("continuative form: connects to what follows");
        if (layers.Count == 0 && first.Surface != root && first.PartOfSpeech == "動詞" && first.ConjugationForm.Length > 0) layers.Add(first.ConjugationForm);
        return layers.Count == 0 ? null : new(root, layers.Distinct().ToArray(), "Read the endings in order. Some endings have multiple uses; the surrounding clause determines their interpretation.");
    }
    private static bool IsPotential(MorphToken token)
    {
        var root = token.DictionaryLemma;
        if (token.PartOfSpeech != "動詞" || root.Length == 0 || !token.ConjugationType.Contains("一段", StringComparison.Ordinal)) return false;
        var ending = root[^1] switch { 'う' => 'え', 'く' => 'け', 'ぐ' => 'げ', 'す' => 'せ', 'つ' => 'て', 'ぬ' => 'ね', 'ぶ' => 'べ', 'む' => 'め', 'る' => 'れ', _ => '\0' };
        return ending != '\0' && token.Lemma == root[..^1] + ending + "る";
    }
}
