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
                const string text = "それが巨大な鉄槌であり、自分がゴミのように吹き飛ばされ、地面に落ちた事だけ、判る。";
                await window.Search(text, false); await Task.Delay(400);
                var ruby = window.SentenceRuby ?? throw new Exception("Missing ruby layer"); var rebuilds = ruby.RebuildCount; var renders = ruby.RenderCount;
                Check(rebuilds > 0 && renders > 0, "visible passage builds and paints ruby initially");
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
                await window.Search(string.Concat(Enumerable.Repeat(text, 12)), false); await Task.Delay(300);
                Check(window.Sentence.ActualHeight <= 155.1 && window.WordsScroll.ActualHeight > 0, "paragraph height is bounded while definitions remain visible");
                var longRuby = window.SentenceRuby!; var beforeScroll = longRuby.RebuildCount;
                window.Sentence.ScrollToVerticalOffset(80); await Task.Delay(150);
                Check(longRuby.RebuildCount > beforeScroll, "ruby refreshes for inner passage scrolling");
                var settled = longRuby.RebuildCount; await Task.Delay(200); Check(longRuby.RebuildCount == settled, "ruby returns to idle after scrolling");
                window.Width = 800; window.Height = 620; await Task.Delay(150);
                Console.WriteLine($"COMPACT input={window.Input.ActualHeight:F0}, words={window.WordsScroll.ActualHeight:F0}, details-width={window.DetailsScroll.ActualWidth:F0}");
                Check(window.Input.IsVisible && window.Input.ActualHeight > 0 && window.WordsScroll.ActualHeight >= 90 && window.DetailsScroll.ActualWidth >= 180, "compact window retains pinned search and side-by-side results");
                var unknown = new TextBlock { Text = "unknown" }; var ankiSource = new TextBlock { Text = "ANKI · test deck" };
                var kanjiClock = Stopwatch.StartNew();
                await window.ShowKanjiPopup("面", unknown, ankiSource); window.UpdateLayout();
                Console.WriteLine($"COLD KANJI POPUP {kanjiClock.Elapsed.TotalMilliseconds:F0} ms");
                var popupText = string.Join("\n", Descendants<TextBlock>(window.KanjiPopupContent!).Select(t => t.Text));
                Check(popupText.Contains("unknown") && popupText.Contains("mask; face; features; surface") && popupText.Contains("Kun: おも、おもて、つら") && popupText.Contains("On: メン、ベン") && popupText.Contains("Name readings"), "kanji popup shows full offline readings/meanings even for unknown Anki cards");
                await window.ShowKanjiPopup("not-a-character", unknown, ankiSource); window.UpdateLayout();
                Check(Descendants<TextBlock>(window.KanjiPopupContent!).Any(t => t.Text.Contains("No character dictionary entry")), "missing dictionary character has its own fallback");
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
}
