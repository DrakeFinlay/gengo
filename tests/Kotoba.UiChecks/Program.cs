using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Controls;
using System.Windows.Media;
using Kotoba.App;
using Kotoba.Infrastructure;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length != 1 || !Path.GetFullPath(args[0]).Contains(Path.DirectorySeparatorChar + ".tools" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Pass an isolated .tools/ data directory.");
        Environment.SetEnvironmentVariable("KOTOBA_DATA_DIR", Path.GetFullPath(args[0]));
        var personal = new PersonalStore(Path.GetFullPath(args[0])); personal.SetSetting("fontSize", "24");
        foreach (var entry in personal.Entries().Where(p => p.Expression == "Qzxvplm Rqzxvplm")) personal.DeleteEntry(entry.Id);
        personal.SaveEntry(null, "衛宮士郎", "えみやしろう", "Shirou Emiya — isolated UI test entry", "");
        var application = new App(); application.InitializeComponent();
        var window = new MainWindow { ShowActivated = false, ShowInTaskbar = false };
        var result = 0;
        void Check(bool condition, string description) { Console.WriteLine((condition ? "PASS " : "FAIL ") + description); if (!condition) result = 1; }
        window.Loaded += async (_, _) =>
        {
            try
            {
                var deadline = Stopwatch.StartNew(); while (!window.ResourcesReady && deadline.ElapsedMilliseconds < 10000) await Task.Delay(25);
                Check(window.ResourcesReady, "full-data resources initialize for UI checks");
                const string reportedPassage = "あれだけ愉快そうだった男の哄笑が止まる。";
                foreach (var size in new[] { 24d, 40d, 48d })
                {
                    window.FontSlider.Value = size;
                    await window.Search(reportedPassage, false); await Task.Delay(150);
                    var passageRuby = window.SentenceRuby!;
                    var firstKanji = ((Paragraph)window.Sentence.Document.Blocks.FirstBlock).Inlines.OfType<Run>().First(r => Kotoba.Core.TextRules.HasKanji(r.Text));
                    var baseTop = firstKanji.ContentStart.GetCharacterRect(LogicalDirection.Forward).Top;
                    var bounds = VisualTreeHelper.GetDrawing(passageRuby)?.Bounds ?? Rect.Empty;
                    Check(!bounds.IsEmpty && bounds.Width > 0 && bounds.Top >= 0 && bounds.Top < baseTop,
                        $"reported passage paints first-line furigana inside the viewport at size {size}");
                }
                Capture(window, Path.Combine(args[0], "furigana-fixed-preview.png"));
                window.FontSlider.Value = 24;
                const string text = "それが巨大な鉄槌であり、自分がゴミのように吹き飛ばされ、地面に落ちた事だけ、判る。";
                await window.Search(text, false); await Task.Delay(400);
                var ruby = window.SentenceRuby ?? throw new Exception("Missing ruby layer"); var rebuilds = ruby.RebuildCount; var renders = ruby.RenderCount;
                Check(rebuilds > 0 && renders > 0, "visible passage builds and paints ruby initially");
                var initialRubyBounds = VisualTreeHelper.GetDrawing(ruby)?.Bounds ?? Rect.Empty;
                Check(!initialRubyBounds.IsEmpty && initialRubyBounds.Width > 0 && initialRubyBounds.Height > 0, "initial passage paints actual furigana glyphs");
                using var process = Process.GetCurrentProcess(); var cpu = process.TotalProcessorTime; var clock = Stopwatch.StartNew();
                await Task.Delay(700); process.Refresh();
                var cpuMs = (process.TotalProcessorTime - cpu).TotalMilliseconds;
                Console.WriteLine($"IDLE CPU {cpuMs:F2} ms / {clock.Elapsed.TotalMilliseconds:F2} ms; ruby rebuilds {ruby.RebuildCount - rebuilds}; renders {ruby.RenderCount - renders}");
                Check(ruby.RebuildCount == rebuilds && ruby.RenderCount - renders <= 2, "idle passage schedules no repeated layout/render loop");
                Check(cpuMs < 250, "idle post-search CPU stays below continuous-core regression threshold");
                var document = window.Sentence.Document;
                window.OpenUnit(window.ActiveAnalysis!.Units.First(u => u.Surface == "巨大な")); await Task.Delay(100);
                Check(ReferenceEquals(window.Sentence.Document, document) && ReferenceEquals(window.SentenceRuby, ruby), "definition opening updates selection without rebuilding passage/ruby");
                var spelling = Descendants<TextBox>(window.DefinitionPanel).First(t => t.Name == "EntrySpelling");
                Check(spelling.IsReadOnly && spelling.Text == "巨大" && window.AnkiCopyText == "巨大[きょだい]", "selectable dictionary root and Anki copy omit attributive な");
                spelling.SelectAll(); Check(spelling.SelectedText == "巨大", "dictionary spelling supports native text selection");
                Check(Descendants<TextBox>(window.DefinitionPanel).Any(t => t.IsReadOnly && t.Text.Contains("huge", StringComparison.Ordinal)), "English senses are selectable for copying");
                Check(!Descendants<TextBlock>(window.DefinitionPanel).Any(t => t.Text == "OTHER FORMS"), "duplicate Other Forms section is hidden");
                var common = Descendants<Border>(window.DefinitionPanel).First(b => b.Name == "CommonBadge"); var jlpt = Descendants<Border>(window.DefinitionPanel).First(b => b.Name == "JlptBadge");
                Check(common.Background is SolidColorBrush green && green.Color.G > green.Color.R && jlpt.Background is SolidColorBrush blue && blue.Color.B > blue.Color.R, "Common is green and JLPT is blue");
                Check(common.TranslatePoint(new Point(), window.DefinitionPanel).Y >= spelling.TranslatePoint(new Point(), window.DefinitionPanel).Y + spelling.ActualHeight, "classification badges are beneath the dictionary spelling");
                var reading = Descendants<NaturalRuby>(window.DefinitionPanel).First();
                Check(Math.Abs(reading.ActualWidth - spelling.ActualWidth) < 1 && reading.Foreground == spelling.Foreground && reading.FontSize < spelling.FontSize, "natural dictionary ruby shares base width/color at a smaller font");
                var glyphWidths = reading.GlyphWidths; var oldRubyWidth = reading.Width; reading.Width += 30; window.UpdateLayout();
                Check(reading.GlyphWidths.SequenceEqual(glyphWidths) && reading.RenderTransform.Value.IsIdentity, "wider ruby changes spacing without distorting glyph proportions");
                reading.Width = oldRubyWidth; window.UpdateLayout();
                Check(window.ExplainGrammarButton.TranslatePoint(new Point(), window.SentenceCard).Y >= window.PassageLegend.TranslatePoint(new Point(), window.SentenceCard).Y + window.PassageLegend.ActualHeight, "Explain grammar sits below passage legend without overlapping ruby");
                Check(Descendants<Border>(window.DefinitionPanel).Any(b => b.Name == "EntryDivider"), "headword and senses have a visible divider and padding");
                Check(Descendants<TextBlock>(window.TerminologyPanel).Any(t => t.Text.Contains("futsuumeishi", StringComparison.Ordinal)), "terminology guide is available in Settings");
                var beforeZoomClicks = personal.Clicks(null).Length; var originalWordSize = spelling.FontSize;
                window.ChangeTextSize(1); await Task.Delay(100);
                var enlargedSpelling = Descendants<TextBox>(window.DefinitionPanel).First(t => t.Name == "EntrySpelling");
                Check(Math.Abs((window.FontSlider.Value - 24) * .75 - 2) < .001 && enlargedSpelling.FontSize > originalWordSize && window.Sentence.Document.FontSize > 24 && window.Input.FontSize > 20.4, "2-point zoom enlarges existing passage, dictionary and input text");
                var enlargedBadge = Descendants<Border>(window.DefinitionPanel).First(b => b.Name == "CommonBadge");
                Check(enlargedBadge.Child is TextBlock badgeLabel && enlargedBadge.ActualHeight <= badgeLabel.FontSize * 1.6 + 8, "Common badge remains on one readable line when enlarged");
                Check(personal.Clicks(null).Length == beforeZoomClicks && window.AnkiCopyText == "巨大[きょだい]", "text resizing creates no lookup event and preserves entry/copy text");
                window.FontSlider.Value = 40; Check(window.FontSlider.Value > 36 && double.Parse(personal.Setting("fontSize", "0"), System.Globalization.CultureInfo.InvariantCulture) == window.FontSlider.Value, "larger text sizes work beyond the old 36-unit ceiling and persist");
                window.ChangeTextSize(-1); window.ChangeTextSize(-1); window.FontSlider.Value = 24; await Task.Delay(100);
                document = window.Sentence.Document;
                var displayed = new TextRange(document.ContentStart, document.ContentEnd).Text;
                Check(displayed.Contains(' ') && displayed.Replace(" ", "").TrimEnd('\r', '\n') == text, "visual word gaps preserve original Japanese characters");
                window.Sentence.SelectAll(); Check(window.SelectedText() == text, "selection lookup/copy omits display-only spaces");
                var aligned = RubyAdorner.RubyParts("吹き飛ばされ", "ふきとばされ").ToArray();
                Check(aligned.Length == 2 && aligned[0].Reading == "ふ" && aligned[1].Reading == "と", "ruby aligns separated kanji portions");
                const string multiline = "一緒に 本を読む。\r\n床に落ちた。";
                await window.Search(multiline, false); window.Sentence.SelectAll();
                Check(window.SelectedText() == multiline, "selection retains intentional spaces and paragraph breaks");
                var longInput = string.Join("\r\n\r\n", Enumerable.Repeat(text + " 本を読む。", 12));
                window.Height = 1040; window.Input.Text = longInput;
                await window.Search(longInput, false); await Task.Delay(300); window.UpdateLayout();
                Console.WriteLine($"EXPANDED input={window.Input.ActualHeight:F1}/{window.Input.MaxHeight:F1}, passage={window.Sentence.ActualHeight:F1}/{window.Sentence.MaxHeight:F1}, words={window.WordsScroll.ActualHeight:F1}");
                Check(window.Input.ActualHeight >= 183.5 && window.Sentence.ActualHeight >= 309.5 && window.Sentence.ActualHeight <= 310.1 && window.WordsScroll.ActualHeight >= 90,
                    "long multiline input and passage grow to doubled height limits while definitions remain usable");
                window.Sentence.SelectAll(); Check(window.Input.Text == longInput && window.SelectedText() == longInput, "larger reading areas preserve pasted spaces and paragraph breaks");
                window.Sentence.Selection.Select(window.Sentence.Document.ContentStart, window.Sentence.Document.ContentStart);
                var longRuby = window.SentenceRuby!; var beforeScroll = longRuby.RebuildCount;
                window.Sentence.ScrollToVerticalOffset(160); await Task.Delay(150); window.UpdateLayout();
                Check(longRuby.RebuildCount > beforeScroll, "ruby refreshes for inner passage scrolling");
                var contentViewport = Descendants<ScrollContentPresenter>(window.Sentence).Single();
                var viewportBounds = contentViewport.TransformToAncestor(window.Sentence).TransformBounds(new Rect(new Point(), contentViewport.RenderSize));
                var rubyBounds = VisualTreeHelper.GetDrawing(longRuby)?.Bounds ?? Rect.Empty;
                Check(!rubyBounds.IsEmpty && viewportBounds.Contains(rubyBounds), "scrolled furigana is clipped inside the same viewport as its base text");
                await Task.Delay(80); window.UpdateLayout(); Capture(window, Path.Combine(args[0], "reading-scroll-preview.png"));
                var settled = longRuby.RebuildCount; await Task.Delay(200); Check(longRuby.RebuildCount == settled, "ruby returns to idle after scrolling");
                window.Width = 800; window.Height = 620; await Task.Delay(150);
                Console.WriteLine($"COMPACT input={window.Input.ActualHeight:F0}, words={window.WordsScroll.ActualHeight:F0}, details-width={window.DetailsScroll.ActualWidth:F0}");
                Check(window.Input.IsVisible && window.Input.ActualHeight > 0 && window.WordsScroll.ActualHeight >= 90 && window.DetailsScroll.ActualWidth >= 180, "compact window retains pinned search and side-by-side results");
                window.Input.Clear();
                var unknown = new TextBlock { Text = "unknown" }; var ankiSource = new TextBlock { Text = "ANKI · test deck" };
                var kanjiClock = Stopwatch.StartNew();
                await window.ShowKanjiPopup("面", unknown, ankiSource); window.UpdateLayout();
                Console.WriteLine($"COLD KANJI POPUP {kanjiClock.Elapsed.TotalMilliseconds:F0} ms");
                var popupText = string.Join("\n", Descendants<TextBlock>(window.KanjiPopupContent!).Select(t => t.Text));
                Check(popupText.Contains("unknown") && popupText.Contains("mask; face; features; surface") && popupText.Contains("Kun: おも、おもて、つら") && popupText.Contains("On: メン、ベン") && popupText.Contains("Name readings"), "kanji popup shows full offline readings/meanings even for unknown Anki cards");
                await window.ShowKanjiPopup("not-a-character", unknown, ankiSource); window.UpdateLayout();
                Check(Descendants<TextBlock>(window.KanjiPopupContent!).Any(t => t.Text.Contains("No character dictionary entry")), "missing dictionary character has its own fallback");
                foreach (var expression in new[] { "程度", "意識" })
                {
                    await window.Search(expression, false); window.OpenUnit(window.ActiveAnalysis!.Units.Single()); window.UpdateLayout();
                    var definitions = Descendants<TextBox>(window.DefinitionPanel).Where(t => System.Text.RegularExpressions.Regex.IsMatch(t.Text, @"^\d+\. ")).ToArray();
                    var expected = definitions.Length == 1 ? definitions[0].Text[3..] : string.Join("\r\n", definitions.Select(t => t.Text));
                    var copyDefinitions = Descendants<Button>(window.DefinitionPanel).Single(b => b.Name == "CopyAllDefinitions");
                    var clicks = personal.Clicks(null).Length;
                    copyDefinitions.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Check(definitions.Length == (expression == "程度" ? 1 : 3) && Clipboard.GetText() == expected,
                        expression + " copies all glosses with numbering only for multiple definitions");
                    Check(Clipboard.ContainsData(DataFormats.Html) && Clipboard.GetText(TextDataFormat.Html).Contains(expression == "程度" ? "degree;" : "1. consciousness<br>2."),
                        expression + " supplies HTML with explicit line breaks for rich-text paste");
                    Check(copyDefinitions.TranslatePoint(new Point(), window.DefinitionPanel).Y >= definitions.Last().TranslatePoint(new Point(), window.DefinitionPanel).Y + definitions.Last().ActualHeight && personal.Clicks(null).Length == clicks,
                        "copy button sits below definitions and does not count another lookup");
                }
                await window.Search("悪", false); window.OpenUnit(window.ActiveAnalysis!.Units.Single()); window.UpdateLayout();
                Check(new[] { "【あく】", "【わる】" }.All(reading => Descendants<Button>(window.DefinitionPanel).Any(b => b.Name == "OpenAlternativeEntry" && b.Content is string label && label.Contains(reading))),
                    "悪 exposes あく and わる choices above the selected contextual-root definitions");
                var choices = Descendants<StackPanel>(window.DefinitionPanel).Single(p => p.Name == "EntryChoices");
                Check(choices.TranslatePoint(new Point(), window.DefinitionPanel).Y < Descendants<TextBox>(window.DefinitionPanel).Single(t => t.Name == "EntrySpelling").TranslatePoint(new Point(), window.DefinitionPanel).Y,
                    "alternative readings can be found without scrolling through the selected entry's senses");
                var alternativeClicks = personal.Clicks(null).Length;
                window.UpdateLayout(); await Task.Delay(50);
                Check(personal.Clicks(null).Length == alternativeClicks, "rendering and previewing expanded lookup choices creates no lookup events");
                using (var lookupSource = new ReferenceStore(Path.Combine(ReferenceStore.InstalledDirectory(Path.Combine(Path.GetFullPath(args[0]), "reference"))!, "reference.sqlite")))
                {
                    foreach (var candidateReading in new[] { "あく", "わる" })
                    {
                        var expected = lookupSource.Find("悪").First(e => e.CompatibleReadings("悪").Any(k => k.Text == candidateReading));
                        Descendants<Button>(window.DefinitionPanel).Single(b => b.Name == "OpenAlternativeEntry" && Equals(b.Tag, expected.Id)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout();
                        var actualSenses = Descendants<TextBox>(window.DefinitionPanel).Where(t => System.Text.RegularExpressions.Regex.IsMatch(t.Text, @"^\d+\. ")).Select(t => t.Text).ToArray();
                        Check(window.AnkiCopyText == "悪[" + candidateReading + "]" && expected.Senses.Select((sense, i) => $"{i + 1}. {string.Join("; ", sense.Glosses)}").SequenceEqual(actualSenses),
                            "opening 悪【" + candidateReading + "】 displays every source sense and the correct reading for copying");
                    }
                }
                Check(personal.Clicks(null).Length == alternativeClicks + 2, "explicitly opening two alternative entries records exactly two lookups");
                var encodedEntry = new Kotoba.Core.PersonalEntry("test", "test", "", "日本語 <note> & meaning\r\nnext line", "").AsEntry();
                var encodedData = DefinitionClipboard.Create(encodedEntry);
                var encodedHtml = encodedData.GetData(DataFormats.Html) as string ?? throw new Exception("Missing clipboard HTML");
                var offsets = System.Text.RegularExpressions.Regex.Matches(encodedHtml, @"(?:Start|End)(?:HTML|Fragment):(\d+)").Select(m => int.Parse(m.Groups[1].Value)).ToArray();
                var htmlBytes = System.Text.Encoding.UTF8.GetBytes(encodedHtml);
                Check(System.Text.Encoding.UTF8.GetString(htmlBytes[offsets[2]..offsets[3]]) == "日本語 &lt;note&gt; &amp; meaning<br>next line" && offsets[1] == htmlBytes.Length,
                    "HTML clipboard escapes definitions, retains internal newlines, and uses UTF-8 byte offsets");
                window.Navigation.SelectedIndex = 1; window.Width = 1710; window.Height = 1400; await Task.Delay(150); window.UpdateLayout();
                var personalItem = window.PersonalList.Items.OfType<ListBoxItem>().First(i => i.Content is TextBlock t && t.Text == "衛宮士郎");
                window.PersonalList.SelectedItem = personalItem;
                Check(personalItem.Content is TextBlock entryLabel && entryLabel.Text == "衛宮士郎" && window.ReadingInput.Text == "えみやしろう", "personal list shows expression only while editor retains the reading");
                Check(personalItem.TranslatePoint(new Point(), window.PersonalWorkspace).Y < 100 && window.PersonalEditor.TranslatePoint(new Point(), window.PersonalWorkspace).Y < 2 && window.PersonalList.ActualHeight > 550, "tall half-screen personal list and editor start together without a height-cap gap");
                window.Width = 800; window.Height = 620; await Task.Delay(100); window.UpdateLayout();
                var editorScroll = Descendants<ScrollViewer>(window.PersonalWorkspace).First(s => ReferenceEquals(s.Content, window.PersonalEditor));
                Check(window.PersonalList.ActualHeight >= 100 && editorScroll.ScrollableHeight > 0 && window.ExpressionInput.ActualWidth >= 200, "compact personal workspace keeps a usable list and independently scrollable editor");
                var rich = window.DefinitionInput;
                rich.Document = new FlowDocument(new Paragraph(new Run("Holy Grail War")));
                rich.SelectAll(); EditingCommands.ToggleBold.Execute(null, rich); EditingCommands.ToggleItalic.Execute(null, rich);
                Check(Equals(rich.Selection.GetPropertyValue(TextElement.FontWeightProperty), FontWeights.Bold) && Equals(rich.Selection.GetPropertyValue(TextElement.FontStyleProperty), FontStyles.Italic), "definition editor applies bold and italic to selection");
                rich.Selection.ApplyPropertyValue(Typography.VariantsProperty, FontVariants.Subscript); rich.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, 11d);
                var payload = DefinitionFormatting.Save(rich); DefinitionFormatting.Load(rich, "fallback", payload); rich.SelectAll();
                Check(DefinitionFormatting.PlainText(rich) == "Holy Grail War" && Equals(rich.Selection.GetPropertyValue(Typography.VariantsProperty), FontVariants.Subscript), "definition formatting round-trip retains subscript and text");
                rich.Document = new FlowDocument(new Paragraph(new Run("First meaning"))); rich.SelectAll(); EditingCommands.ToggleNumbering.Execute(null, rich);
                payload = DefinitionFormatting.Save(rich); DefinitionFormatting.Load(rich, "fallback", payload);
                Check(rich.Document.Blocks.FirstBlock is System.Windows.Documents.List { MarkerStyle: TextMarkerStyle.Decimal }, "numbered list survives formatted definition save/load");
                rich.SelectAll(); EditingCommands.ToggleBullets.Execute(null, rich); payload = DefinitionFormatting.Save(rich); DefinitionFormatting.Load(rich, "fallback", payload);
                Check(rich.Document.Blocks.FirstBlock is System.Windows.Documents.List { MarkerStyle: TextMarkerStyle.Disc }, "bulleted list survives formatted definition save/load");
                var named = personal.SaveEntry(null, "衛宮士郎", "えみやしろう", "First meaning", "", "Names", payload);
                window.NamesPersonalFilter.IsChecked = true;
                var nameItem = window.PersonalList.Items.OfType<ListBoxItem>().Single(); window.PersonalList.SelectedItem = nameItem;
                Check(nameItem.Tag is Kotoba.Core.PersonalEntry { Category: "Names" } && window.NamesCategory.IsChecked == true && rich.Document.Blocks.FirstBlock is System.Windows.Documents.List, "Names filter and editor restore saved category and formatting");
                window.WordsPersonalFilter.IsChecked = true;
                Check(!window.PersonalList.Items.OfType<ListBoxItem>().Any(i => i.Tag is Kotoba.Core.PersonalEntry p && p.Id == named.Id), "Personal words filter excludes Names without changing stored entries");
                await window.Search("衛宮士郎", false); window.OpenUnit(window.ActiveAnalysis!.Units.Single());
                Check(Descendants<RichTextBox>(window.DefinitionPanel).Any(r => r.IsReadOnly && r.Document.Blocks.FirstBlock is System.Windows.Documents.List), "search displays the personal definition with its saved list formatting");
                const string unmatched = "Qzxvplm Rqzxvplm";
                foreach (var entry in personal.Entries().Where(p => p.Expression == unmatched)) personal.DeleteEntry(entry.Id);
                window.Navigation.SelectedIndex = 0;
                var searchesBefore = personal.History().Length; var clicksBefore = personal.Clicks(null).Length;
                await window.Search(unmatched);
                Check(window.ActiveAnalysis!.Units.All(u => u.EntryIds.Length == 0) && window.WordsHint.Visibility == Visibility.Collapsed,
                    "unmatched search shows an empty-result action immediately without clicking a fragment");
                var addUnmatched = Descendants<Button>(window.DefinitionPanel).Single(b => b.Name == "AddUnmatchedPersonalEntry");
                addUnmatched.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                TextBox InlineField(string name) => Descendants<TextBox>(window.DefinitionPanel).Single(t => t.Name == name);
                Check(window.Navigation.SelectedIndex == 0 && InlineField("InlineExpression").Text == unmatched && InlineField("InlineReading").Text == "" && InlineField("InlineDefinition").Text == "" && !personal.Entries().Any(p => p.Expression == unmatched),
                    "empty-result add button opens a prefilled form within Search without persisting a draft");
                var saveEntry = Descendants<Button>(window.DefinitionPanel).Single(b => b.Name == "SaveInlinePersonalEntry");
                saveEntry.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(Descendants<TextBlock>(window.DefinitionPanel).Any(t => t.Name == "InlineEntryStatus" && t.Text.Contains("Enter an expression")) && !personal.Entries().Any(p => p.Expression == unmatched),
                    "inline required-field validation keeps the draft in Search and writes nothing");
                Descendants<Button>(window.DefinitionPanel).Single(b => b.Name == "CancelInlinePersonalEntry").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(addUnmatched.IsVisible && !InlineField("InlineDefinition").IsVisible && window.Navigation.SelectedIndex == 0 && !personal.Entries().Any(p => p.Expression == unmatched),
                    "cancel closes the inline form without navigation or persistence");
                addUnmatched.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                InlineField("InlineDefinition").Text = "Isolated empty-result test definition\r\nSecond line";
                InlineField("InlineReading").Text = "てすと"; InlineField("InlineNotes").Text = "Inline test note";
                Descendants<ComboBox>(window.DefinitionPanel).Single(c => c.Name == "InlineCategory").SelectedItem = "Names";
                saveEntry = Descendants<Button>(window.DefinitionPanel).Single(b => b.Name == "SaveInlinePersonalEntry");
                saveEntry.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var saveDeadline = Stopwatch.StartNew();
                while (!window.Status.Text.StartsWith("Saved personal dictionary item", StringComparison.Ordinal) && saveDeadline.ElapsedMilliseconds < 10000) await Task.Delay(25);
                var inlineSaved = personal.Entries().Single(p => p.Expression == unmatched);
                Check(window.Navigation.SelectedIndex == 0 && window.Status.Text.StartsWith("Saved personal dictionary item", StringComparison.Ordinal) && window.ActiveAnalysis!.Units is [{ Personal: true, Surface: unmatched }] && inlineSaved is { Reading: "てすと", Category: "Names", Notes: "Inline test note", Definition: "Isolated empty-result test definition\r\nSecond line" },
                    "inline save persists all fields and reanalyzes the passage while staying in Search");
                Check(personal.History().Length == searchesBefore + 1 && personal.Clicks(null).Length == clicksBefore,
                    "add and save create no extra search or lookup events");
                window.Navigation.SelectedIndex = 0; window.OpenUnit(window.ActiveAnalysis!.Units.Single());
                Check(!Descendants<Button>(window.DefinitionPanel).Any(b => b.Name == "AddUnmatchedPersonalEntry") && Descendants<TextBox>(window.DefinitionPanel).Any(t => t.Text.Contains("Isolated empty-result test definition\r\nSecond line")),
                    "the saved personal definition replaces the empty-result action on deliberate lookup");
                await window.Search("程度", false);
                Check(!Descendants<Button>(window.DefinitionPanel).Any(b => b.Name == "AddUnmatchedPersonalEntry"), "successful searches clear the previous empty-result action");
                personal.DeleteEntry(inlineSaved.Id);
                var assistant = new AiFixture(); window.AssistantOverride = _ => assistant;
                window.AiProviderPicker.SelectedItem = "Ollama";
                await Task.Delay(50);
                Check(window.AiModelPicker.Items.OfType<Kotoba.Core.AiModel>().Select(m => m.Id).SequenceEqual(["fixture-model", "second-model"]), "AI Settings refreshes model choices through the selected provider");
                window.AiModelPicker.SelectedIndex = 1;
                Check(personal.Setting("aiModel:Ollama", "") == "second-model", "AI model selection persists per provider");
                window.OpenUnit(window.ActiveAnalysis!.Units.Single());
                Descendants<Button>(window.DefinitionPanel).Single(b => b.Name == "CheckDefinition").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var aiQuestion = Descendants<TextBox>(window.DefinitionPanel).Single(t => t.Name == "AiQuestion");
                aiQuestion.Text = "Which meaning fits this context?";
                var aiContext = Descendants<TextBox>(window.DefinitionPanel).Single(t => t.Name == "AiContext"); aiContext.Text = "程度が分かる。";
                var aiClicks = personal.Clicks(null).Length; var aiHistory = personal.History().Length; var aiEntries = personal.Entries().Length;
                Descendants<Button>(window.DefinitionPanel).Single(b => b.Name == "AskAi").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Task.Delay(50);
                Check(assistant.Last is { Expression: "程度", Context: "程度が分かる。", Entry: not null } && assistant.Last.Question == aiQuestion.Text && assistant.LastModel == "second-model", "check definition sends selected senses, editable context and the user's question to the selected model");
                Check(window.Navigation.SelectedIndex == 0 && Descendants<TextBox>(window.DefinitionPanel).Any(t => t.Text == "Synthetic AI explanation"), "AI answer appears inline and remains separate from the dictionary senses");
                window.Width = 1200; window.Height = 1000; await Task.Delay(100); window.UpdateLayout(); window.WordsScroll.ScrollToEnd(); await Task.Delay(50);
                Capture(window, Path.Combine(args[0], "ai-search-preview.png"));
                window.Navigation.SelectedIndex = 3; await Task.Delay(100); window.UpdateLayout(); Capture(window, Path.Combine(args[0], "ai-settings-preview.png"));
                window.AiModelPicker.IsDropDownOpen = true; window.UpdateLayout(); await Task.Delay(50);
                var modelPopup = (System.Windows.Controls.Primitives.Popup)window.AiModelPicker.Template.FindName("PART_Popup", window.AiModelPicker);
                Check(Descendants<TextBlock>(window.AiModelPicker).Any(t => t.Text == "Second model") && Descendants<TextBlock>(modelPopup.Child).Any(t => t.Text == "Second model") && window.AiModelPicker.Foreground is SolidColorBrush pickerText && pickerText.Color.R > 150 && window.AiModelPicker.Background is SolidColorBrush pickerBackground && pickerBackground.Color.R < 80,
                    "AI model picker and dropdown show readable model names in the dark theme");
                window.AiModelPicker.IsDropDownOpen = false; window.Navigation.SelectedIndex = 0;
                aiQuestion.Text = "Could it mean something else?";
                Descendants<Button>(window.DefinitionPanel).Single(b => b.Name == "AskAi").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Task.Delay(50);
                Check(assistant.Last!.Conversation.Length == 1, "AI follow-up includes only this panel's prior exchange");
                Check(personal.Clicks(null).Length == aiClicks && personal.History().Length == aiHistory && personal.Entries().Length == aiEntries, "AI requests and rendering change no counts, history or personal entries");
                Descendants<Button>(window.DefinitionPanel).First(b => b.Name == "UseAiPersonalEntry").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(InlineField("InlineDefinition").Text == "Synthetic suggested meaning" && InlineField("InlineReading").Text == "ていど" && window.Navigation.SelectedIndex == 0 && personal.Entries().Length == aiEntries, "using an AI suggestion prefills a reviewable inline draft without saving it");
                await window.Search(unmatched, false);
                Descendants<Button>(window.DefinitionPanel).Single(b => b.Name == "HelpIdentify").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Descendants<Button>(window.DefinitionPanel).Single(b => b.Name == "AskAi").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Task.Delay(50);
                Check(assistant.Last is { Entry: null, Expression: unmatched, Conversation.Length: 0 }, "help identify sends the unmatched expression with context and no invented dictionary entry");
                window.AiProviderPicker.SelectedItem = "Off"; var previousCalls = assistant.Calls;
                Descendants<Button>(window.DefinitionPanel).Single(b => b.Name == "AskAi").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Task.Delay(25);
                Check(assistant.Calls == previousCalls, "Off prevents AI inference");
                window.AiProviderPicker.SelectedItem = "Ollama"; await Task.Delay(25);
                assistant.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
                Descendants<Button>(window.DefinitionPanel).Single(b => b.Name == "AskAi").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var pending = assistant.Pending;
                await window.Search("程度", false); window.OpenUnit(window.ActiveAnalysis!.Units.Single());
                pending.SetResult(AiFixture.Answer); await Task.Delay(50);
                Check(assistant.LastCancellation.IsCancellationRequested && !Descendants<TextBox>(window.DefinitionPanel).Any(t => t.Text == "Synthetic AI explanation"), "changing the search cancels AI and discards even a provider response that ignores cancellation");
                window.AiProviderPicker.SelectedItem = "ChatGPT";
                using (var statisticsSource = new ReferenceStore(Path.Combine(ReferenceStore.InstalledDirectory(Path.Combine(Path.GetFullPath(args[0]), "reference"))!, "reference.sqlite")))
                {
                    var statisticsEntry = statisticsSource.Find("悪").First(e => e.CompatibleReadings("悪").Any(k => k.Text == "あく"));
                    for (var i = 0; i < 5; i++) personal.RecordClick(statisticsEntry, "悪", null);
                    await window.Search("今日は悪い天気だ。");
                    var historyBeforeRevisit = personal.History().Select(h => h.Id).ToArray();
                    int TotalClicks() => personal.DailyTotals(null, null, TimeZoneInfo.Local).Sum(d => d.Count);
                    var clicksBeforeRevisit = TotalClicks();
                    window.Navigation.SelectedIndex = 2;
                    var statisticsDeadline = Stopwatch.StartNew();
                    while (!window.StatisticsList.Items.OfType<ListBoxItem>().Any(i => i.Tag is Kotoba.Core.WordStatistic s && s.EntryId == statisticsEntry.Id) && statisticsDeadline.ElapsedMilliseconds < 10000) await Task.Delay(25);
                    var statisticsRow = window.StatisticsList.Items.OfType<ListBoxItem>().Single(i => i.Tag is Kotoba.Core.WordStatistic s && s.EntryId == statisticsEntry.Id);
                    window.StatisticsList.SelectedItem = statisticsRow;
                    var doubleClick = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, System.Windows.Input.MouseButton.Left)
                    { RoutedEvent = Control.MouseDoubleClickEvent, Source = statisticsRow };
                    statisticsRow.RaiseEvent(doubleClick);
                    var revisitDeadline = Stopwatch.StartNew();
                    while (!window.Status.Text.StartsWith("Definition reopened from statistics", StringComparison.Ordinal) && revisitDeadline.ElapsedMilliseconds < 10000) await Task.Delay(25);
                    window.UpdateLayout(); await Task.Delay(50);
                    Check(window.Navigation.SelectedIndex == 0 && window.Input.Text == "悪" && window.AnkiCopyText == "悪[あく]" && window.WordsScroll.VerticalOffset == 0,
                        "double-clicking a five-count statistics row immediately navigates to its exact definition and reading in Search");
                    Check(TotalClicks() == clicksBeforeRevisit && personal.History().Select(h => h.Id).SequenceEqual(historyBeforeRevisit), "statistics definition revisit adds neither a lookup nor a submitted search");
                    window.OpenUnit(window.ActiveAnalysis!.Units.Single()); window.UpdateLayout();
                    Check(window.AnkiCopyText == "悪[あく]" && TotalClicks() == clicksBeforeRevisit, "clicking the already-open statistics word does not count it again or switch homographs");
                    var alternativeButton = Descendants<Button>(window.DefinitionPanel).First(b => b.Name == "OpenAlternativeEntry");
                    alternativeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Check(TotalClicks() == clicksBeforeRevisit + 1 && personal.Clicks((string)alternativeButton.Tag).First().SearchId is null,
                        "a later deliberate alternative counts normally without linking to the previously displayed unrelated passage");
                    var deleted = personal.SaveEntry(null, "統計用の削除済み項目", "", "Synthetic deleted entry", "");
                    for (var i = 0; i < 5; i++) personal.RecordClick(deleted.AsEntry(), deleted.Expression, null);
                    var deletedStatistic = personal.Statistics().Single(s => s.EntryId == deleted.Id); personal.DeleteEntry(deleted.Id);
                    var preservedAnalysis = window.ActiveAnalysis; var countsBeforeMissing = TotalClicks();
                    await window.OpenStatistic(deletedStatistic);
                    Check(ReferenceEquals(window.ActiveAnalysis, preservedAnalysis) && window.Status.Text.Contains("no longer available", StringComparison.Ordinal) && TotalClicks() == countsBeforeMissing,
                        "a removed statistics entry reports unavailability without opening a different identity or changing counts");
                    foreach (var click in personal.Clicks(deleted.Id)) personal.DeleteClick(click.Id);
                }
            }
            catch (Exception ex) { Console.WriteLine(ex); result = 1; }
            finally { window.Close(); application.Shutdown(result); }
        };
        application.Run(window); return result;
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
    private static void Capture(Window window, string path)
    {
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window); var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var output = File.Create(path); encoder.Save(output);
    }
    private sealed class AiFixture : Kotoba.Core.IWordAssistant
    {
        internal static Kotoba.Core.WordAnswer Answer => new("程度", "ていど", "Synthetic suggested meaning", "Synthetic AI explanation", ["Synthetic alternative"], "Synthetic uncertainty", "Personal words");
        internal Kotoba.Core.WordQuestion? Last; internal string? LastModel; internal int Calls;
        internal CancellationToken LastCancellation; internal TaskCompletionSource<Kotoba.Core.WordAnswer>? Pending;
        public Task<Kotoba.Core.AiModel[]> Models(CancellationToken token) => Task.FromResult<Kotoba.Core.AiModel[]>([new("fixture-model", "Fixture model"), new("second-model", "Second model")]);
        public Task<Kotoba.Core.WordAnswer> Ask(string model, Kotoba.Core.WordQuestion question, CancellationToken token)
        {
            Last = question; LastModel = model; LastCancellation = token; Calls++;
            return Pending?.Task ?? Task.FromResult(Answer);
        }
    }
}
