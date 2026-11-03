using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using Kotoba.Core;
using Kotoba.Infrastructure;
using Microsoft.Data.Sqlite;

if (args.FirstOrDefault() == "--anki-probe")
{
    using var client = new AnkiKanjiClient();
    foreach (var character in args.Skip(1))
    {
        var result = await client.Find(character, new()); Console.WriteLine(character + ": " + result.DisplayMeaning);
    }
    return;
}
if (args.FirstOrDefault() == "--install")
{
    var root = Path.Combine(Path.GetFullPath(args[1]), "reference");
    var installed = await ReferenceStore.Install(root, new Progress<string>(Console.WriteLine), CancellationToken.None);
    using var lexicon = new ReferenceStore(Path.Combine(installed, "reference.sqlite"));
    using var morphology = new UniDicMorphology(Path.Combine(installed, "UniDic"));
    if (lexicon.Find("日本語").Count == 0 || morphology.Parse("日本語").Length == 0) throw new Exception("Installed resource validation failed.");
    using var canceled = new CancellationTokenSource(); canceled.Cancel();
    try { await ReferenceStore.Install(root, null, canceled.Token); throw new Exception("Cancellation was ignored."); }
    catch (OperationCanceledException) { }
    if (ReferenceStore.InstalledDirectory(root) != installed) throw new Exception("Canceled reinstall changed the active resources.");
    Console.WriteLine("PASS end-to-end pinned downloads, SHA-256, indexing, native loading, and canceled reinstall preserves previous active resources."); return;
}
if (args.FirstOrDefault() == "--prepare")
{
    var target = Path.GetFullPath(args[3]); Directory.CreateDirectory(target);
    var referenceRoot = Path.Combine(target, "reference"); var installed = Path.Combine(referenceRoot, "resources-development"); Directory.CreateDirectory(installed);
    var db = Path.Combine(installed, "reference.sqlite"); if (!File.Exists(db)) ReferenceStore.Build(db, args[1], new Progress<string>(Console.WriteLine));
    var dic = Path.Combine(installed, "UniDic"); if (!Directory.Exists(dic)) ReferenceStore.ExtractUniDic(args[2], dic);
    File.WriteAllText(Path.Combine(referenceRoot, "current.txt"), "resources-development");
    Console.WriteLine($"Prepared isolated full-data fixture: {target}"); return;
}
var passed = 0; var failed = 0;
void Check(bool condition, string description) { if (condition) { passed++; Console.WriteLine("PASS " + description); } else { failed++; Console.WriteLine("FAIL " + description); } }
void Throws(Action action, string description) { try { action(); Check(false, description); } catch (Exception) { Check(true, description); } }
await AnkiChecks.Run(Check);
var ground = await KanjiDictionary.Find("地");
Check(ground is not null && ground.Groups.SelectMany(g => g.Meanings).Contains("earth") && ground.Groups.SelectMany(g => g.Readings).Where(r => r.Type == "ja_on").Select(r => r.Value).SequenceEqual(["チ", "ジ"]), "verified bundled KANJIDIC2 supplies 地 meanings and On readings offline");
var face = await KanjiDictionary.Find("面");
Check(face is not null && face.Groups.SelectMany(g => g.Readings).Where(r => r.Type == "ja_kun").Select(r => r.Value).SequenceEqual(["おも", "おもて", "つら"]) && face.NameReadings.Contains("ほおつき") && face.StrokeCounts[0] == 9 && face.Grade == 3, "KANJIDIC2 keeps Kun/name readings, strokes and school grade independently of Anki");
var part = await KanjiDictionary.Find("分");
Check(part is not null && part.Groups.SelectMany(g => g.Readings).Any(r => r.Value == "わ.かる"), "Kun readings preserve okurigana notation");
var noEnglish = await KanjiDictionary.Find("乁");
Check(noEnglish is not null && noEnglish.Groups.SelectMany(g => g.Meanings).Count() == 0 && noEnglish.Groups.SelectMany(g => g.Readings).Any(r => r.Value == "なが.れる"), "full kanji resource retains readings for characters without English meanings");
Check(await KanjiDictionary.Find("not-a-character") is null && ReferenceEquals(face, await KanjiDictionary.Find("面")), "missing character is explicit and repeated kanji lookup reuses indexed entries");
Check(EntryPresentation.AnkiFront("巨大", "きょだい") == "巨大[きょだい]", "Anki Front copy uses the selected dictionary spelling and correct reading");
Check(EntryPresentation.AnkiFront("それ", "それ") == "それ" && EntryPresentation.AnkiFront("衛宮士郎", "") == "衛宮士郎", "Anki copy omits redundant or unknown readings");
Check(EntryPresentation.Tag("n", t => t) == "Noun" && EntryPresentation.Tag("adj-na", t => t) == "Na-adjective", "results use concise English grammatical labels");
Check(EntryPresentation.Terms.Any(t => t.JapaneseTerm.Contains("futsuumeishi", StringComparison.Ordinal)) && EntryPresentation.Terms.Any(t => t.JapaneseTerm.Contains("keiyodoshi", StringComparison.Ordinal)), "terminology guide explains source grammar terms");
var formsFixture = new Entry("forms", [new("生", false, []), new("性", false, [])], [new("せい", false, ["生", "性"], []), new("しょう", false, ["生"], [])], [], []);
var alternativesFixture = EntryPresentation.OtherForms(formsFixture, "生", "せい");
Check(alternativesFixture.Length == 2 && alternativesFixture.All(f => !(f.Expression == "生" && f.Reading == "せい")) && alternativesFixture.All(f => !(f.Expression == "性" && f.Reading == "しょう")), "other forms exclude displayed pair and preserve reading restrictions");
var selection = new LookupSelection();
Check(selection.Open("A") && !selection.Open("A"), "clicking already-open entry does not count again");
Check(selection.Open("B") && selection.Open("A"), "A → B → A records a second deliberate A lookup");
selection.Clear(); Check(selection.Open("A"), "new search clears prior definition selection");
Check(!TextRules.NeedsApproval(new string('日', 5000)), "5,000 scalar values proceed normally");
Check(TextRules.NeedsApproval(new string('日', 5001)), "5,001 scalar values require approval");
Check(TextRules.CharacterCount("𠮷野\n😀") == 4, "Unicode guard counts scalars, including whitespace");
var zone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
var spring = TextRules.DateBounds(new DateTime(2026, 3, 8), new DateTime(2026, 3, 8), zone);
var autumn = TextRules.DateBounds(new DateTime(2026, 11, 1), new DateTime(2026, 11, 1), zone);
Check((spring.Until - spring.From)?.TotalHours == 23, "spring DST day has 23 hours"); Check((autumn.Until - autumn.From)?.TotalHours == 25, "autumn DST day has 25 hours");
var fixture = Path.Combine(Path.GetTempPath(), "Kotoba-checks-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(fixture);
try
{
    var legacyDirectory = Path.Combine(fixture, "legacy"); var legacy = new PersonalStore(legacyDirectory);
    var oldEntry = legacy.SaveEntry(null, "既存", "きそん", "existing", "preserved notes"); legacy.SetSetting("retained", "yes");
    using (var db = new SqliteConnection($"Data Source={legacy.DatabasePath};Pooling=False"))
    {
        db.Open(); using var command = db.CreateCommand(); command.CommandText = "ALTER TABLE personal DROP COLUMN category; ALTER TABLE personal DROP COLUMN definition_rtf; PRAGMA user_version=1;"; command.ExecuteNonQuery();
    }
    var legacyBackup = Path.Combine(fixture, "legacy-backup.zip"); legacy.Backup(legacyBackup, "fixture");
    var migrated = new PersonalStore(legacyDirectory); var migratedEntry = migrated.Entries().Single();
    Check(migratedEntry.Id == oldEntry.Id && migratedEntry.Category == "Personal words" && migratedEntry.DefinitionRtf == "" && migratedEntry.Notes == "preserved notes" && migrated.Setting("retained", "") == "yes" && Directory.GetFiles(legacyDirectory, "before-schema-2-*.sqlite").Length == 1, "schema-1 migration snapshots first and preserves entries/settings with default category");
    var categorized = migrated.SaveEntry(oldEntry.Id, "既存", "きそん", "existing", "preserved notes", "Names", "formatted payload");
    Check(migrated.Entries().Single() == categorized, "category and formatted definition round-trip without changing entry identity");
    Throws(() => migrated.SaveEntry(null, "bad", "", "bad", "", "invalid"), "unsupported personal category rejected");
    var formattedBackup = Path.Combine(fixture, "formatted-backup.zip"); migrated.Backup(formattedBackup, "fixture");
    migrated.SaveEntry(oldEntry.Id, "既存", "", "changed", ""); migrated.Restore(formattedBackup);
    Check(migrated.Entries().Single() == categorized, "backup restores category and formatted definition");
    migrated.Restore(legacyBackup);
    Check(migrated.Entries().Single().Category == "Personal words" && migrated.Entries().Single().Definition == "existing", "old schema-1 backup restores and migrates to current schema");
    var store = new PersonalStore(fixture);
    var a = store.SaveEntry(null, "聖杯", "せいはい", "Holy Grail", "");
    var b = store.SaveEntry(null, "聖杯戦争", "せいはいせんそう", "Holy Grail War", "");
    var name = store.SaveEntry(null, "衛宮士郎", "えみやしろう", "Shirou Emiya", "Name");
    Check(store.SaveEntry(null, "聖杯", "せいはい", "The Grail", "").Id == a.Id && store.Entries().Length == 3, "duplicate expression updates stable identity");
    Throws(() => store.SaveEntry(a.Id, b.Expression, "", "conflict", ""), "editing cannot collide with another override");
    Throws(() => store.SaveEntry(null, "", "", "empty", ""), "empty personal expressions rejected");
    using var fakeMorph = new IdentityMorphology(); var overrideEngine = new AnalysisEngine(new EmptyLexicon(), fakeMorph);
    var custom = overrideEngine.Analyze("𠮷 聖杯戦争、聖杯\n衛宮士郎！", store.Entries());
    Check(custom.Units.Any(u => u.Surface == b.Expression && u.EntryIds[0] == b.Id), "longest custom span wins at same position");
    Check(custom.Units.Count(u => u.Surface == a.Expression) == 1, "shorter occurrence stays short");
    Check(custom.Units.Any(u => u.Surface == name.Expression && u.Reading == name.Reading), "custom name ruby preserved");
    Check(string.Concat(custom.Units.Select(u => u.Surface)) == custom.Text, "custom boundaries preserve every original character");
    Check(custom.Units.All(u => custom.Text.Substring(u.Start, u.Length) == u.Surface), "custom offsets use original UTF-16 positions");
    using var canceled = new CancellationTokenSource(); canceled.Cancel(); Throws(() => overrideEngine.Analyze("聖杯", store.Entries(), canceled.Token), "analysis honors cancellation");
    Check(store.History().Length == 0, "analysis/edit/cancel create no search events");
    var searchId = store.SaveSearch(custom); var root = a.AsEntry();
    for (var i = 0; i < 5; i++) store.RecordClick(root, i % 2 == 0 ? "聖杯" : "聖杯だ", searchId, new DateTimeOffset(2026, 11, 1, 5 + i, 0, 0, TimeSpan.Zero));
    Check(store.Statistics().Single().Count == 5, "five events share stable entry identity across surfaces");
    Check(store.Statistics(new DateTimeOffset(2026, 11, 1, 6, 0, 0, TimeSpan.Zero)).Length == 0, "five-click threshold applies within selected dates");
    Check(store.DailyTotals(autumn.From, autumn.Until, zone).Sum(d => d.Count) == 5, "daily totals include DST range correctly");
    Check(store.Statistics().Single().Count == 5, "viewing statistics creates no clicks");
    Check(store.History().Single().Analysis.Text == custom.Text && store.History().Length == 1, "analysis snapshot round-trip/history read creates no duplicate");
    var backup = Path.Combine(fixture, "backup.zip"); store.Backup(backup, "fixture"); Check(PersonalStore.InspectBackup(backup).Clicks == 5, "backup manifest matches consistent snapshot");
    store.DeleteSearch(searchId); Check(store.Statistics().Single().Count == 5 && store.Clicks(root.Id).All(c => c.Context is null && c.SearchId is null), "deleting passage preserves clicks and unlinks text");
    store.DeleteEntry(a.Id); Check(store.Statistics().Single().Root == "聖杯", "deleted entry keeps historical snapshot");
    var safety = store.Restore(backup); Check(File.Exists(safety) && store.History().Length == 1 && store.Entries().Length == 3, "restore replaces data and creates safety backup");
    var invalid = Path.Combine(fixture, "invalid.zip"); File.WriteAllText(invalid, "not a backup"); Throws(() => store.Restore(invalid), "invalid backup rejected"); Check(store.Statistics().Single().Count == 5, "failed restore preserves data");
    store.SetSetting("fontSize", "28"); store.DeleteHistory(); Check(store.History().Length == 0 && store.Statistics(null, null, 1).Length == 0 && store.Entries().Length == 3 && store.Setting("fontSize", "24") == "28", "clear history retains entries/settings");
    var installed = args.Length > 0 && args[0].Length > 0 ? ReferenceStore.InstalledDirectory(Path.Combine(Path.GetFullPath(args[0]), "reference")) : null;
    if (installed is not null)
    {
        using var lexicon = new ReferenceStore(Path.Combine(installed, "reference.sqlite")); using var morphology = new UniDicMorphology(Path.Combine(installed, "UniDic")); var engine = new AnalysisEngine(lexicon, morphology);
        Analysis Analyze(string text) => engine.Analyze(text, []);
        void Root(string text, string expected, string layer)
        {
            var result = Analyze(text); var unit = result.Units.FirstOrDefault(u => u.Inflection?.Root == expected);
            Check(unit is not null && unit.Inflection!.Layers.Any(l => l.Contains(layer, StringComparison.Ordinal)), $"{text} → {expected} ({layer})");
            Console.WriteLine("  " + string.Join(" | ", result.Units.Select(u => $"{u.Surface}:{u.Inflection?.Root} {string.Join(",", u.Inflection?.Layers ?? [])}")));
        }
        const string sentence = "それが巨大な鉄槌であり、自分がゴミのように吹き飛ばされ、地面に落ちた事だけ、判る。";
        Console.WriteLine("SEGMENTS " + string.Join(" | ", Analyze(sentence).Units.Select(u => u.Surface)));
        var giant = Analyze("巨大な鉄槌").Units.FirstOrDefault(u => u.Surface == "巨大な");
        Check(giant is not null && giant.Inflection?.Root == "巨大" && giant.Tokens.Any(t => t.Surface == "な") && giant.EntryIds.All(id => lexicon.Get(id)?.Expression == "巨大"), "巨大な is one na-adjective unit opening 巨大, retaining internal な morphology");
        foreach (var sample in new[] { "静かな部屋", "有名な人" }) Check(Analyze(sample).Units.Any(u => u.Surface.EndsWith('な') && u.Inflection?.Layers.Any(l => l.StartsWith("attributive", StringComparison.Ordinal)) == true), "dictionary-backed na-adjective grouping: " + sample);
        Check(Analyze("巨大 な鉄槌").Units.Any(u => u.Surface == "巨大") && Analyze("行くな").Units.All(u => u.Inflection?.Layers.All(l => !l.StartsWith("attributive", StringComparison.Ordinal)) != false), "na grouping does not cross whitespace or reinterpret prohibitive な");
        var giantEntry = lexicon.Find("巨大").First(e => e.Reading == "きょだい");
        Check(EntryPresentation.OtherForms(giantEntry, "巨大", "きょだい").Length == 0, "巨大 has no duplicate current spelling/reading in Other Forms");
        Root("吹き飛ばされ", "吹き飛ばす", "passive"); Root("落ちた", "落ちる", "past"); Root("使えまい", "使う", "potential"); Root("読めない", "読む", "potential"); Root("書けなかった", "書く", "potential"); Root("食べさせられなかった", "食べる", "causative"); Root("読んでいる", "読む", "progressive"); Root("書きました", "書く", "polite");
        foreach (var expression in new[] { "巨大", "自分勝手", "一緒に", "嫌悪感", "焼け野原" }) Check(Analyze(expression).Units.Any(u => u.Surface == expression && u.EntryIds.Length > 0), "dictionary-backed grouping: " + expression);
        var phrase = Analyze("一緒に本を読むのが好き。"); Check(phrase.Units.Any(u => u.Surface == "を") && phrase.Units.Any(u => u.Surface == "の"), "standalone particles remain separate");
        foreach (var original in new[] { "𠮷野 😀\n  日本語\n\n次。", " 日本語  ", "\r\n日本語\r\n", "日\t本　語", "知らない架空語XYZ" }) { var result = Analyze(original); Check(string.Concat(result.Units.Select(u => u.Surface)) == original && result.Units.All(u => original.Substring(u.Start, u.Length) == u.Surface), "offsets/whitespace preserved: " + JsonSerializer.Serialize(original)); }
        foreach (var (text, pattern) in new[] { ("嫌悪感しか湧かない", "しか～ない"), ("水しか飲まない。", "しか～ない"), ("どうせ君に令呪は使えまい。", "～まい"), ("だっていうのに、", "っていうのに / というのに"), ("バカにするのでもなく、真剣に綾子は頷く。", "のでもなく / のではなく"), ("しゃらん、という華麗な音。", "という + noun"), ("気が付けば、焼け野原にいた。", "気が付けば"), ("記憶がないなりに…", "なりに") }) Check(GrammarEngine.Explain(Analyze(text)).Any(c => c.Pattern == pattern), $"grammar positive: {text} → {pattern}");
        foreach (var text in new[] { "鹿がいる。食べない。", "水しか飲む。食べない。", "水しか、彼は飲まない。", "しかと言った。飲まない。", "水しか飲む人がいない。" }) Check(GrammarEngine.Explain(Analyze(text)).All(c => c.Pattern != "しか～ない"), "grammar negative control: " + text);
        Check(GrammarEngine.Explain(Analyze("水しか飲まない人が来た。")).Any(c => c.Pattern == "しか～ない"), "local しか relation inside relative clause");
        Check(GrammarEngine.Explain(Analyze("だって、行きたい。")).All(c => c.Pattern != "っていうのに / というのに"), "ordinary だって is not contrastive contraction");
        Check(engine.Analyze("聖杯戦争と衛宮士郎。聖杯。", store.Entries()).Units.Any(u => u.Surface == "聖杯戦争" && u.Personal), "custom spans override actual morphology");
        Check(lexicon.Find("掛ける").Any(e => e.Senses.Length > 10), "full multi-sense structure retained");
        using (var db = new SqliteConnection($"Data Source={Path.Combine(installed, "reference.sqlite")};Mode=ReadOnly"))
        {
            db.Open(); using var command = db.CreateCommand(); command.CommandText = "SELECT COUNT(*) FROM entries"; Console.WriteLine("DICTIONARY ENTRIES " + command.ExecuteScalar()); command.CommandText = "SELECT value FROM metadata WHERE key='unresolved'"; Console.WriteLine("UNRESOLVED JLPT " + command.ExecuteScalar());
            if (args.Length > 1 && File.Exists(args[1]))
            {
                using var file = File.OpenRead(args[1]); using var gz = new GZipStream(file, CompressionMode.Decompress); using var baseline = JsonDocument.Parse(gz); var sourceEntries = baseline.RootElement.GetProperty("Entries").EnumerateArray().ToArray();
                foreach (var set in new[] { "Common", "N1", "N2", "N3", "N4", "N5" })
                {
                    var expected = sourceEntries.Where(e => set == "Common" ? e.GetProperty("Common").GetBoolean() : e.GetProperty("Levels").EnumerateArray().Any(l => l.GetString() == set)).Select(e => e.GetProperty("Id").GetString()!).ToHashSet();
                    command.CommandText = set == "Common" ? "SELECT id FROM entries WHERE common=1" : "SELECT entry_id FROM levels WHERE level=$level"; command.Parameters.Clear(); if (set != "Common") command.Parameters.AddWithValue("$level", set);
                    var actual = new HashSet<string>(); using (var rows = command.ExecuteReader()) while (rows.Read()) actual.Add(rows.GetString(0)); Check(actual.SetEquals(expected), $"FULL-SET PARITY {set}: {actual.Count:N0} IDs vs Anki Stats {expected.Count:N0}");
                }
            }
            else Console.WriteLine("SKIP parity: pass Anki Stats baseline gzip as second argument.");
        }
        foreach (var text in new[] { sentence, string.Concat(Enumerable.Repeat(sentence, 10)) }) { Analyze(text); var timer = Stopwatch.StartNew(); for (var i = 0; i < 10; i++) Analyze(text); timer.Stop(); Console.WriteLine($"WARM ANALYSIS {TextRules.CharacterCount(text)} chars: {timer.Elapsed.TotalMilliseconds / 10:F2} ms average (10 runs)"); }
        Console.WriteLine($"RESOURCE SIZE {Directory.EnumerateFiles(installed, "*", SearchOption.AllDirectories).Sum(p => new FileInfo(p).Length) / 1048576.0:F1} MiB");
    }
    else Console.WriteLine("SKIP full-data checks: pass isolated data directory as first argument.");
}
finally { Directory.Delete(fixture, true); }
Console.WriteLine($"{passed} passed; {failed} failed."); Environment.ExitCode = failed == 0 ? 0 : 1;

sealed class EmptyLexicon : ILexicon { public string Version => "fixture"; public IReadOnlyList<Entry> Find(string expression) => []; public Entry? Get(string id) => null; public string Label(string tag) => tag; }
sealed class IdentityMorphology : IMorphology { public string Version => "fixture"; public MorphToken[] Parse(string text, CancellationToken cancellationToken = default) => text.Length == 0 ? [] : [new(0, text.Length, text, "", "", "", "unknown", "")]; public void Dispose() { } }
