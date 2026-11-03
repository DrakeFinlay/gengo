using System.Text.RegularExpressions;

namespace Kotoba.Core;

/// <summary>Small versioned, project-authored catalog. No generated text or external service.</summary>
public static class GrammarEngine
{
    public const string Version = "builtin-1";
    public static GrammarCard[] Explain(Analysis analysis, CancellationToken cancellationToken = default)
    {
        var cards = new List<GrammarCard>(); var text = analysis.Text;
        var tokens = analysis.Units.SelectMany(u => u.Tokens).OrderBy(t => t.Start).ToArray();
        foreach (var token in tokens)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (token.Surface != "しか" || token.PartOfSpeech != "助詞") continue;
            var next = tokens.Where(t => t.Start > token.Start).TakeWhile(t => !Boundary(t.Surface)).ToArray();
            var predicate = Array.FindIndex(next, t => t.PartOfSpeech is "動詞" or "形容詞");
            if (predicate < 0 || next.Take(predicate).Any(t => t.Surface is "は" or "が" or "けど" or "のに")) continue;
            // Stop before a second predicate, relative-clause head noun, or quoted material.
            var chain = next.Skip(predicate).TakeWhile((t, index) => index == 0 || t.PartOfSpeech is "助動詞" || t.Surface is "て" or "で" or "ば").ToArray();
            var negative = chain.FirstOrDefault(t => t.Lemma is "ない" or "ぬ" or "ず");
            if (negative is null) continue;
            var verb = chain[0];
            cards.Add(new("しか～ない", "only / nothing but",
                $"しか marks the sole remaining possibility; the negative predicate {text[verb.Start..(negative.Start + negative.Length)]} excludes everything else. The combination expresses ‘only’, although the verb is grammatically negative.",
                [new(token.Start, token.Length), new(verb.Start, negative.Start + negative.Length - verb.Start)]));
        }
        Add("(?:だ)?っていうのに|というのに", "っていうのに / というのに", "and yet / despite the fact that",
            "って contracts the quotation marker と. いうのに packages the preceding statement as a fact and contrasts it with what follows. A fragment needs prior context to identify that fact.");
        Add("のでもなく|のではなく|んでもなく|んではなく", "のでもなく / のではなく", "without it being / rather than",
            "の nominalizes the preceding characterization. で is the copula's connective form; も or は adds contrast, and なく negates it while connecting to the next clause. The construction does not supply an unstated object.");
        Add("なりに", "なりに", "in a way suited to one's circumstances / within those limits",
            "なりに presents the preceding state or capacity as the frame within which something is done. With a negative state, the sense is often ‘given that limitation’; it is not invariably a simple ‘despite’.",
            m => tokens.Any(t => t.Start == m.Index && t.Surface == "なり"), "The precise frame depends on the following clause; an unfinished fragment may leave it open.");
        foreach (Match m in Regex.Matches(text, "という", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
        {
            var after = tokens.Where(t => t.Start >= m.Index + m.Length).TakeWhile(t => !Boundary(t.Surface)).Take(4).ToArray();
            if (!after.Any(t => t.PartOfSpeech == "名詞")) continue;
            cards.Add(new("という + noun", "called / described as / the expression …",
                "と marks quoted content; いう links that content to the following noun. A sound followed by という音 identifies the sound. The quoted content can also be a statement, name, or description.", [new(m.Index, m.Length)], "Quotation and naming uses overlap; the surrounding phrase determines the relationship."));
        }
        foreach (var unit in analysis.Units.Where(u => u.Inflection?.Layers.Any(l => l.StartsWith("conditional", StringComparison.Ordinal)) == true))
        {
            var preceding = text[..unit.Start]; var awareness = unit.Inflection!.Root == "付く" && preceding.EndsWith("気が", StringComparison.Ordinal);
            cards.Add(new(awareness ? "気が付けば" : "～ば", awareness ? "on noticing / when one becomes aware" : "if / when",
                awareness ? "気が付く means ‘notice / become aware’; ば is conditional. In narrative, the following clause can describe the situation already present when awareness returns. ‘Before I knew it’ is an idiomatic English rendering, not the lexical meaning of ば."
                : "The conditional form connects a condition with its consequence. In narrative it can also introduce the situation discovered when the condition is met.", [new(awareness ? unit.Start - 2 : unit.Start, unit.Length + (awareness ? 2 : 0))]));
        }
        foreach (var unit in analysis.Units.Where(u => u.Tokens.Any(t => t.Lemma == "まい")))
            cards.Add(new("～まい", "probably will not / intend not to",
                "まい expresses negative conjecture or negative intention. When attached to a potential form, the conjecture concerns ability. Speaker intent and context distinguish these readings.", [new(unit.Start, unit.Length)], "Negative conjecture and negative intention can remain ambiguous in isolation."));
        return cards.DistinctBy(c => (c.Pattern, c.Spans[0].Start)).OrderBy(c => c.Spans[0].Start).ToArray();

        void Add(string regex, string pattern, string meaning, string explanation, Func<Match, bool>? predicate = null, string? uncertainty = null)
        {
            foreach (Match m in Regex.Matches(text, regex, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
                if (predicate?.Invoke(m) != false) cards.Add(new(pattern, meaning, explanation, [new(m.Index, m.Length)], uncertainty));
        }
    }
    private static bool Boundary(string surface) => surface.IndexOfAny(['。', '！', '？', '!', '?', '\n', '、', ',', '「', '」', '『', '』', ';', '；']) >= 0;
}
