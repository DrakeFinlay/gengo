namespace Kotoba.Core;

public static class LookupCandidates
{
    public static Entry[] For(Unit unit, ILexicon lexicon, IReadOnlyList<PersonalEntry> personal)
    {
        // Context ranks the likely root; it must not erase other entries for the
        // literal spelling. Resolve at lookup time so saved analyses benefit too.
        Entry? Resolve(string id) => id.StartsWith("personal:", StringComparison.Ordinal)
            ? personal.FirstOrDefault(p => p.Id == id)?.AsEntry() : lexicon.Get(id);
        return personal.Where(p => p.Expression == unit.Surface).Select(p => p.AsEntry())
            .Concat(unit.EntryIds.Select(Resolve).OfType<Entry>())
            .Concat(lexicon.Find(unit.Surface))
            .Concat(unit.Inflection is { } inflection ? lexicon.Find(inflection.Root) : [])
            .DistinctBy(e => e.Id).ToArray();
    }
}
