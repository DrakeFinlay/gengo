using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Kotoba.Core;
using Kotoba.Infrastructure;
using Microsoft.Win32;

namespace Kotoba.App;

public partial class MainWindow : Window
{
    private readonly string dataDirectory;
    private readonly PersonalStore store;
    private ReferenceStore? reference;
    private IMorphology? morphology;
    private AnalysisEngine? engine;
    private Analysis? analysis;
    private long? searchId;
    private readonly LookupSelection lookupSelection = new();
    private Unit? selectedUnit;
    private string? editingId;
    private CancellationTokenSource? operation;
    private int generation;
    private int historyOffset;
    private int historyRevision;
    private bool initialized;
    private bool composing;
    private Point pointerDown;
    private readonly List<(Unit Unit, Run Run)> wordRuns = [];
    private RubyAdorner? ruby;
    private Analysis? renderedAnalysis;
    private double renderedSize;
    private readonly AnkiKanjiClient anki = new();
    private CancellationTokenSource? kanjiOperation;
    private readonly Popup kanjiPopup = new() { StaysOpen = false, AllowsTransparency = true, Placement = PlacementMode.MousePoint };
    private GrammarCard[] grammar = [];
    private double JapaneseSize => FontSlider?.Value ?? 24;
    private Brush Accent => (Brush)FindResource("AccentBrush");
    private Brush Muted => (Brush)FindResource("MutedBrush");
    public MainWindow()
    {
        dataDirectory = Environment.GetEnvironmentVariable("KOTOBA_DATA_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kotoba");
        store = new(dataDirectory);
        InitializeComponent();
        Input.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(ReadingExtentChanged));
        Sentence.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(ReadingExtentChanged));
        AnkiDeckInput.Text = KanjiOptions.Deck; AnkiExpressionInput.Text = KanjiOptions.ExpressionField; AnkiMeaningInput.Text = KanjiOptions.MeaningField;
        SizeChanged += (_, _) => UpdateReadingBounds();
        FontSlider.Value = double.TryParse(store.Setting("fontSize", "24"), CultureInfo.InvariantCulture, out var size) ? Math.Clamp(size, FontSlider.Minimum, FontSlider.Maximum) : 24;
        TextCompositionManager.AddPreviewTextInputStartHandler(Input, (_, _) => composing = true);
        TextCompositionManager.AddPreviewTextInputHandler(Input, (_, _) => composing = false);
        Sentence.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, (_, e) =>
        {
            var text = SelectedText(); if (text.Length > 0) Clipboard.SetText(text); e.Handled = true;
        }, (_, e) => { e.CanExecute = !Sentence.Selection.IsEmpty; e.Handled = true; }));
        Sentence.PreviewMouseLeftButtonDown += (_, e) => pointerDown = e.GetPosition(Sentence);
        Sentence.AddHandler(Mouse.PreviewMouseUpEvent, new MouseButtonEventHandler((_, e) =>
        {
            if (e.ChangedButton != MouseButton.Left || (e.GetPosition(Sentence) - pointerDown).Length > 5 || !Sentence.Selection.IsEmpty) return;
            var position = Sentence.GetPositionFromPoint(e.GetPosition(Sentence), false);
            if (position is null) return;
            var word = wordRuns.FirstOrDefault(w => position.CompareTo(w.Run.ContentStart) >= 0 && position.CompareTo(w.Run.ContentEnd) <= 0);
            if (word.Unit is not null && !string.IsNullOrWhiteSpace(word.Unit.Surface)) Dispatcher.BeginInvoke(() => OpenUnit(word.Unit));
        }), true);
        Sentence.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter || !Sentence.Selection.IsEmpty) return;
            var position = Sentence.CaretPosition;
            var word = wordRuns.FirstOrDefault(w => position.CompareTo(w.Run.ContentStart) >= 0 && position.CompareTo(w.Run.ContentEnd) <= 0);
            if (word.Unit is not null) { OpenUnit(word.Unit); e.Handled = true; }
        };
        initialized = true;
        ApplyTextSize(); PopulateTerminology();
        Loaded += async (_, _) => await Safely(async () => { await Task.Run(LoadResources); RefreshResources(); RefreshPersonal(); await InitializeAi(); Input.Focus(); });
        Closed += (_, _) => { CancelAiRequests(); aiSignIn?.Cancel(); aiHttp.Dispose(); ollamaHttp.Dispose(); };
    }
    private void LoadResources()
    {
        var installed = ReferenceStore.InstalledDirectory(Path.Combine(dataDirectory, "reference"));
        if (installed is null) return;
        var nextReference = new ReferenceStore(Path.Combine(installed, "reference.sqlite"));
        IMorphology nextMorphology;
        try { nextMorphology = new UniDicMorphology(Path.Combine(installed, "UniDic")); }
        catch { nextReference.Dispose(); throw; }
        reference?.Dispose(); morphology?.Dispose(); reference = nextReference; morphology = nextMorphology; engine = new(reference, morphology);
    }
    private async Task Safely(Func<Task> action)
    {
        try { await action(); }
        catch (OperationCanceledException) { Status.Text = "Canceled. Your last successful passage is preserved."; }
        catch (Exception ex) { Status.Text = ex.Message; MessageBox.Show(this, ex.Message, "Gengo", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    private CancellationToken BeginOperation(string message)
    {
        CancelAiRequests();
        operation?.Cancel(); operation?.Dispose(); operation = new(); generation++;
        SearchButton.IsEnabled = false; InstallButton.IsEnabled = false; CancelButton.IsEnabled = true; CancelInstallButton.IsEnabled = true; Status.Text = message;
        return operation.Token;
    }
    private void FinishOperation()
    {
        SearchButton.IsEnabled = true; InstallButton.IsEnabled = true; CancelButton.IsEnabled = false; CancelInstallButton.IsEnabled = false;
        operation?.Dispose(); operation = null;
    }
    internal bool ResourcesReady => engine is not null;
    internal Analysis? ActiveAnalysis => analysis;
    internal RubyAdorner? SentenceRuby => ruby;
    internal async Task Search(string text, bool save = true)
    {
        if (engine is null) { Navigation.SelectedIndex = 3; Status.Text = "Install offline resources to begin dictionary lookup."; return; }
        if (operation is not null) return;
        if (string.IsNullOrWhiteSpace(text)) { Status.Text = "Enter Japanese text first."; return; }
        if (TextRules.NeedsApproval(text) && !Confirm($"This passage contains {TextRules.CharacterCount(text):N0} characters, exceeding 5,000. Analysis can take longer. Analyze anyway?", "Large passage")) return;
        var token = BeginOperation("Analyzing Japanese…"); var revision = generation;
        var personal = store.Entries();
        try
        {
            var result = await Task.Run(() => engine.Analyze(text, personal, token), token);
            token.ThrowIfCancellationRequested(); if (revision != generation) return;
            // Commit only after cancellation/staleness checks on the UI thread.
            var id = save ? store.SaveSearch(result) : searchId;
            analysis = result; searchId = id; lookupSelection.Clear(); displayedEntry = null; AnkiCopyText = ""; selectedUnit = null; grammar = [];
            ResetKanji(); WordsHint.Visibility = Visibility.Visible;
            DefinitionPanel.Children.Clear(); AlternativesPanel.Children.Clear(); GrammarPanel.Children.Clear(); RenderSentence();
            Status.Text = $"{result.Units.Count(u => u.EntryIds.Length > 0):N0} dictionary units · Click a word for its definition";
            if (!result.Units.Any(u => u.EntryIds.Length > 0)) ShowNoMatch(text);
        }
        finally { FinishOperation(); }
    }
    private async void SearchClick(object sender, RoutedEventArgs e) => await Safely(() => Search(Input.Text));
    private void CancelClick(object sender, RoutedEventArgs e) { operation?.Cancel(); generation++; Status.Text = "Canceling…"; }
    private void InputChanged(object sender, TextChangedEventArgs e) { if (CharacterLabel is not null) CharacterLabel.Text = $"{TextRules.CharacterCount(Input.Text):N0} characters"; QueueReadingBounds(); }
    private async void WindowKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && !Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            if (e.Key is Key.OemPlus or Key.Add) { ChangeTextSize(1); e.Handled = true; return; }
            if (e.Key is Key.OemMinus or Key.Subtract) { ChangeTextSize(-1); e.Handled = true; return; }
        }
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.L) { Navigation.SelectedIndex = 0; Input.Focus(); Input.SelectAll(); e.Handled = true; }
        if (Input.IsKeyboardFocusWithin && Keyboard.Modifiers is ModifierKeys.None or ModifierKeys.Control && e.Key == Key.Enter && !composing && e.ImeProcessedKey == Key.None) { e.Handled = true; await Safely(() => Search(Input.Text)); }
    }
    private void RenderSentence(GrammarCard? highlight = null)
    {
        if (analysis is null) return;
        SentenceCard.Visibility = Visibility.Visible;
        if (renderedAnalysis == analysis && renderedSize == JapaneseSize) { ApplyWordStyles(highlight); return; }
        ruby?.Dispose(); ruby = null; wordRuns.Clear();
        // FlowDocument collapses the first paragraph's top margin. Padding keeps
        // the first line's ruby space inside the editor's clipped viewport, with
        // a small rounding allowance at different font sizes and display scales.
        var paragraph = new Paragraph { Margin = new Thickness(0), Padding = new Thickness(0, Math.Ceiling(JapaneseSize * 0.55) + 1, 0, 0), LineHeight = JapaneseSize * 2.0 };
        Unit? previous = null;
        foreach (var unit in analysis.Units)
        {
            if (previous is not null && IsLexical(previous) && IsLexical(unit)) paragraph.Inlines.Add(new Run(" ") { FontSize = JapaneseSize * .65 });
            var run = new Run(unit.Surface);
            run.Cursor = Cursors.Hand;
            paragraph.Inlines.Add(run); wordRuns.Add((unit, run));
            previous = unit;
        }
        Sentence.Document = new FlowDocument(paragraph) { FontFamily = new FontFamily("Yu Gothic UI"), FontSize = JapaneseSize, PagePadding = new Thickness(0), ColumnWidth = double.PositiveInfinity };
        renderedAnalysis = analysis; renderedSize = JapaneseSize; ApplyWordStyles(highlight);
        var layer = AdornerLayer.GetAdornerLayer(Sentence);
        if (layer is not null)
        {
            ruby = new RubyAdorner(Sentence, wordRuns, JapaneseSize, Muted); layer.Add(ruby);
        }
    }
    private static bool IsLexical(Unit unit) => !string.IsNullOrWhiteSpace(unit.Surface) && unit.Surface.EnumerateRunes().Any(r => System.Text.Rune.IsLetterOrDigit(r));
    private void ApplyWordStyles(GrammarCard? highlight)
    {
        foreach (var (unit, run) in wordRuns)
        {
            run.Foreground = unit.Personal ? Accent : unit.Tokens.FirstOrDefault()?.PartOfSpeech switch
            {
                "名詞" or "代名詞" => Brushes.DarkSalmon,
                "動詞" => Brushes.LightSteelBlue,
                "助詞" or "助動詞" => Brushes.LightCoral,
                "形容詞" or "形状詞" => Brushes.Plum,
                "副詞" => Brushes.Khaki,
                _ => Muted
            };
            run.TextDecorations = IsLexical(unit) ? TextDecorations.Underline : null;
            run.Background = unit == selectedUnit ? new SolidColorBrush(Color.FromRgb(48, 87, 82)) : Brushes.Transparent;
            run.TextEffects = new TextEffectCollection();
            if (highlight is null) continue;
            foreach (var span in highlight.Spans)
            {
                var start = Math.Max(unit.Start, span.Start); var end = Math.Min(unit.Start + unit.Length, span.Start + span.Length);
                if (end > start) run.TextEffects.Add(new TextEffect { PositionStart = start - unit.Start, PositionCount = end - start, Foreground = Brushes.Gold });
            }
        }
    }
    internal void OpenUnit(Unit unit)
    {
        CancelAiRequests();
        selectedUnit = unit;
        WordsHint.Visibility = Visibility.Collapsed;
        var entries = EntriesFor(unit);
        if (entries.Length == 0)
        {
            displayedEntry = null; AnkiCopyText = "";
            lookupSelection.Clear(); DefinitionPanel.Children.Clear(); AlternativesPanel.Children.Clear();
            ShowNoMatch(unit.Surface); RenderSentence(); RefreshKanji(unit); return;
        }
        OpenDefinition(entries[0], unit, true);
    }
    private void ShowNoMatch(string expression)
    {
        WordsHint.Visibility = Visibility.Collapsed;
        var panel = new StackPanel(); panel.Children.Add(SelectableLabel($"{expression} · No dictionary match", 20));
        panel.Children.Add(ResultLabel("Add this expression to your personal dictionary with your own definition and optional reading.", 14, Muted));
        var form = new StackPanel { Visibility = Visibility.Collapsed };
        Button? add = null;
        add = ActionButton("Add personal dictionary item", () =>
        {
            form.Children.Clear();
            add!.Visibility = Visibility.Collapsed; form.Visibility = Visibility.Visible;
            BuildInlinePersonalEntry(form, expression, () => { form.Visibility = Visibility.Collapsed; add!.Visibility = Visibility.Visible; add.Focus(); });
        });
        add.Name = "AddUnmatchedPersonalEntry"; panel.Children.Add(add);
        panel.Children.Add(form);
        AddAiAssistance(panel, expression, null, selectedUnit?.Surface == expression ? selectedUnit : null);
        DefinitionPanel.Children.Add(Card(panel)); WordsScroll.ScrollToTop(); Status.Text = expression + " · No dictionary match";
    }
    private Entry? Resolve(string id) => id.StartsWith("personal:", StringComparison.Ordinal) ? store.Entries().FirstOrDefault(e => e.Id == id)?.AsEntry() : reference?.Get(id);
    private Entry[] EntriesFor(Unit unit) => reference is null ? unit.EntryIds.Select(Resolve).OfType<Entry>().ToArray() : LookupCandidates.For(unit, reference, store.Entries());
    private async void GrammarClick(object sender, RoutedEventArgs e) => await Safely(async () =>
    {
        if (analysis is null || operation is not null) return;
        var snapshot = analysis; var token = BeginOperation("Finding supported grammar patterns…");
        try
        {
            var result = await Task.Run(() => GrammarEngine.Explain(snapshot, token), token); token.ThrowIfCancellationRequested(); if (analysis != snapshot) return;
            grammar = result; GrammarPanel.Children.Clear();
            GrammarPanel.Children.Add(ResultLabel("Sentence grammar · Built-in", 21, null, new Thickness(0, 8, 0, 12)));
            if (grammar.Length == 0) GrammarPanel.Children.Add(Card(ResultLabel("None of the built-in grammar rules matched this passage. This is not an error: the current rule set is limited, and other constructions may be present. See Settings for supported patterns.", 15, Muted)));
            foreach (var card in grammar)
            {
                var p = new StackPanel(); p.Children.Add(ResultLabel($"{card.Pattern} — {card.Meaning}", 19));
                p.Children.Add(ResultLabel(string.Join(" … ", card.Spans.Select(s => snapshot.Text.Substring(s.Start, s.Length))), 19, Accent, new Thickness(0, 10, 0, 8)));
                p.Children.Add(ResultLabel(card.Explanation, 14)); if (card.Uncertainty is not null) p.Children.Add(ResultLabel(card.Uncertainty, 12, Muted, new Thickness(0, 10, 0, 0)));
                p.Children.Add(ActionButton("Highlight in passage", () => RenderSentence(card))); GrammarPanel.Children.Add(Card(p));
            }
            Status.Text = $"{grammar.Length} supported grammar pattern(s) · No lookup clicks recorded";
        }
        finally { FinishOperation(); }
    });
    internal string SelectedText()
    {
        // Display separators are not source characters. Selection lookup/copy must use
        // original unit text, retaining intentional whitespace but omitting added gaps.
        if (Sentence.Selection.IsEmpty) return "";
        var result = new System.Text.StringBuilder();
        foreach (var (unit, run) in wordRuns)
        {
            var start = Sentence.Selection.Start.CompareTo(run.ContentStart) > 0 ? Sentence.Selection.Start : run.ContentStart;
            var end = Sentence.Selection.End.CompareTo(run.ContentEnd) < 0 ? Sentence.Selection.End : run.ContentEnd;
            if (start.CompareTo(end) < 0) result.Append(new TextRange(start, end).Text);
        }
        return result.ToString();
    }
    private async void LookupSelectionClick(object sender, RoutedEventArgs e) => await Safely(async () =>
    {
        var text = SelectedText(); if (string.IsNullOrWhiteSpace(text) || engine is null) return;
        if (TextRules.NeedsApproval(text) && !Confirm($"Analyze this selection of {TextRules.CharacterCount(text):N0} characters?", "Large selection")) return;
        var personal = store.Entries().FirstOrDefault(p => p.Expression == text);
        var direct = (personal is null ? Array.Empty<Entry>() : [personal.AsEntry()]).Concat(reference!.Find(text)).ToArray();
        if (direct.Length > 0) { OpenUnit(new(0, text.Length, text, "", direct.Select(d => d.Id).ToArray(), [])); return; }
        var overrides = store.Entries(); var result = await Task.Run(() => engine.Analyze(text, overrides));
        var combined = result.Units.Length == 1 ? result.Units[0] : new Unit(0, text.Length, text, "", [], []);
        OpenUnit(combined);
    });
    private void AddSelectionClick(object sender, RoutedEventArgs e) { var text = SelectedText(); if (!string.IsNullOrWhiteSpace(text)) StartEntry(text); }
    private void StartEntry(string expression)
    {
        Navigation.SelectedIndex = 1; editingId = null; PersonalList.SelectedIndex = -1; ExpressionInput.Text = expression; ReadingInput.Clear(); DefinitionFormatting.Load(DefinitionInput, "", ""); NotesInput.Clear(); PersonalStatus.Text = "";
        NamesCategory.IsChecked = NamesPersonalFilter.IsChecked == true; WordsCategory.IsChecked = NamesCategory.IsChecked != true;
        var existing = store.Entries().FirstOrDefault(p => p.Expression == expression); if (existing is not null) FillEntry(existing); DefinitionInput.Focus();
    }
    private void RefreshPersonal()
    {
        PersonalList.Items.Clear(); foreach (var p in store.Entries().Where(p => NamesPersonalFilter?.IsChecked == true ? p.Category == "Names" : WordsPersonalFilter?.IsChecked != true || p.Category == "Personal words")) PersonalList.Items.Add(new ListBoxItem { Content = Label(p.Expression, 17, margin: new Thickness(0)), Padding = new Thickness(10), Tag = p });
    }
    private void FillEntry(PersonalEntry p) { editingId = p.Id; ExpressionInput.Text = p.Expression; ReadingInput.Text = p.Reading; DefinitionFormatting.Load(DefinitionInput, p.Definition, p.DefinitionRtf); NotesInput.Text = p.Notes; NamesCategory.IsChecked = p.Category == "Names"; WordsCategory.IsChecked = p.Category != "Names"; }
    private void PersonalSelected(object sender, SelectionChangedEventArgs e) { if (PersonalList.SelectedItem is ListBoxItem { Tag: PersonalEntry p }) FillEntry(p); }
    private void NewEntryClick(object sender, RoutedEventArgs e) => StartEntry("");
    private async void SaveEntryClick(object sender, RoutedEventArgs e) => await Safely(async () =>
    {
        if (operation is not null) { PersonalStatus.Text = "Wait for the current operation or cancel it first."; return; }
        var p = store.SaveEntry(editingId, ExpressionInput.Text, ReadingInput.Text, DefinitionFormatting.PlainText(DefinitionInput), NotesInput.Text, NamesCategory.IsChecked == true ? "Names" : "Personal words", DefinitionFormatting.Save(DefinitionInput)); editingId = p.Id;
        if (p.Category == "Names" && WordsPersonalFilter.IsChecked == true || p.Category == "Personal words" && NamesPersonalFilter.IsChecked == true) AllPersonalFilter.IsChecked = true;
        RefreshPersonal(); PersonalList.SelectedItem = PersonalList.Items.OfType<ListBoxItem>().FirstOrDefault(i => i.Tag is PersonalEntry item && item.Id == p.Id);
        if (analysis is not null) await Reanalyze(); PersonalStatus.Text = "Saved. This expression now takes priority in every passage.";
    });
    private async void DeleteEntryClick(object sender, RoutedEventArgs e) => await Safely(async () =>
    {
        if (editingId is null || operation is not null || !Confirm("Delete this personal entry? Ordinary dictionary matching will resume. Historical clicks keep their identity and counts.", "Delete entry")) return;
        store.DeleteEntry(editingId); StartEntry(""); RefreshPersonal(); if (analysis is not null) await Reanalyze();
    });
    private async Task Reanalyze()
    {
        if (analysis is null || engine is null) return;
        var token = BeginOperation("Applying personal dictionary…"); var text = analysis.Text; var entries = store.Entries();
        try
        {
            var result = await Task.Run(() => engine.Analyze(text, entries, token), token); token.ThrowIfCancellationRequested(); analysis = result; lookupSelection.Clear(); displayedEntry = null; AnkiCopyText = ""; selectedUnit = null; grammar = [];
            DefinitionPanel.Children.Clear(); GrammarPanel.Children.Clear(); AlternativesPanel.Children.Clear(); RenderSentence();
            ResetKanji(); WordsHint.Visibility = Visibility.Visible;
            Status.Text = "Personal dictionary applied · No new search or lookup recorded";
            if (!result.Units.Any(u => u.EntryIds.Length > 0)) ShowNoMatch(text);
        }
        finally { FinishOperation(); }
    }
    private async void NavigationChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!initialized || e.Source != Navigation) return;
        if (Navigation.SelectedIndex == 2) await Safely(RefreshHistory);
        if (Navigation.SelectedIndex == 3) RefreshResources();
    }
    private (DateTimeOffset? From, DateTimeOffset? Until) Bounds()
    {
        if (FromDate.SelectedDate > ThroughDate.SelectedDate) throw new ArgumentException("The start date must precede the end date.");
        return TextRules.DateBounds(FromDate.SelectedDate, ThroughDate.SelectedDate, TimeZoneInfo.Local);
    }
    private async Task RefreshHistory()
    {
        var bounds = Bounds(); var offset = historyOffset; var revision = ++historyRevision;
        var result = await Task.Run(() => (Words: store.Statistics(bounds.From, bounds.Until), History: store.History(offset, 30, bounds.From, bounds.Until), Daily: store.DailyTotals(bounds.From, bounds.Until, TimeZoneInfo.Local), Clicks: store.Clicks(null, bounds.From, bounds.Until)));
        if (revision != historyRevision) return;
        StatisticsList.Items.Clear(); ClickDetails.Text = "";
        foreach (var word in result.Words)
        {
            var item = new ListBoxItem { Content = Label($"{word.Root}   {word.Reading}     {word.Count} lookups\nFirst: {word.First.LocalDateTime:g}   ·   Last: {word.Last.LocalDateTime:g}", 15), Tag = word };
            item.MouseDoubleClick += StatisticDoubleClick; StatisticsList.Items.Add(item);
        }
        TotalsText.Text = $"{result.Daily.Sum(d => d.Count):N0} deliberate lookups · {result.Words.Length:N0} words opened at least five times";
        DailyText.Text = result.Daily.Length == 0 ? "No lookups yet." : string.Join("   ·   ", result.Daily.Take(90).Select(d => $"{d.Day:yyyy-MM-dd}: {d.Count}")) + (result.Daily.Length > 90 ? "\nShowing the most recent 90 days; choose dates to see earlier totals." : "");
        HistoryPanel.Children.Clear();
        if (result.History.Length == 0) HistoryPanel.Children.Add(Label("No submitted passages in this page/date range.", 14, Muted));
        foreach (var record in result.History)
        {
            var p = new StackPanel(); p.Children.Add(Label(record.Created.LocalDateTime.ToString("g"), 12, Muted)); p.Children.Add(Label(record.Text.Length > 240 ? record.Text[..240] + "…" : record.Text, 17));
            var actions = new WrapPanel(); actions.Children.Add(ActionButton("Reopen", () => Reopen(record)));
            actions.Children.Add(ActionButton("Delete passage…", async () => { if (!Confirm("Delete this submitted text and saved analysis? Lookup counts are preserved; their sentence links are removed.", "Delete passage")) return; store.DeleteSearch(record.Id); if (searchId == record.Id) searchId = null; await Safely(RefreshHistory); })); p.Children.Add(actions); HistoryPanel.Children.Add(Card(p));
        }
        RecentClicksPanel.Children.Clear();
        foreach (var click in result.Clicks)
        {
            var p = new DockPanel(); var delete = ActionButton("Delete click", async () => { if (Confirm("Delete this lookup event? Its count will be removed.", "Delete lookup")) { store.DeleteClick(click.Id); await Safely(RefreshHistory); } }); DockPanel.SetDock(delete, Dock.Right); p.Children.Add(delete); p.Children.Add(Label($"{click.Surface} → {click.Root}  ·  {click.Created.LocalDateTime:g}", 14)); RecentClicksPanel.Children.Add(p);
        }
    }
    private void Reopen(SearchRecord record)
    {
        if (operation is not null) return;
        CancelAiRequests();
        analysis = record.Analysis; searchId = record.Id; lookupSelection.Clear(); displayedEntry = null; AnkiCopyText = ""; selectedUnit = null; grammar = [];
        ResetKanji(); WordsHint.Visibility = Visibility.Visible;
        Input.Text = record.Text; DefinitionPanel.Children.Clear(); GrammarPanel.Children.Clear(); AlternativesPanel.Children.Clear(); Navigation.SelectedIndex = 0; RenderSentence();
        Status.Text = "Saved analysis reopened · No new search or click recorded";
        if (!record.Analysis.Units.Any(u => u.EntryIds.Length > 0)) ShowNoMatch(record.Text);
    }
    private async void StatisticSelected(object sender, SelectionChangedEventArgs e) => await Safely(async () =>
    {
        if (StatisticsList.SelectedItem is not ListBoxItem { Tag: WordStatistic word }) return;
        var bounds = Bounds(); var events = await Task.Run(() => store.Clicks(word.EntryId, bounds.From, bounds.Until));
        ClickDetails.Text = string.Join("\n\n", events.Take(30).Select(c => $"{c.Created.LocalDateTime:g} · {c.Surface}" + (c.Context is null ? " · Sentence removed" : "\n" + (c.Context.Length > 350 ? c.Context[..350] + "…" : c.Context))));
    });
    private async void ApplyDatesClick(object sender, RoutedEventArgs e) { historyOffset = 0; await Safely(RefreshHistory); }
    private async void AllDatesClick(object sender, RoutedEventArgs e) { FromDate.SelectedDate = null; ThroughDate.SelectedDate = null; historyOffset = 0; await Safely(RefreshHistory); }
    private async void PreviousHistoryClick(object sender, RoutedEventArgs e) { historyOffset = Math.Max(0, historyOffset - 30); await Safely(RefreshHistory); }
    private async void NextHistoryClick(object sender, RoutedEventArgs e) { historyOffset += 30; await Safely(RefreshHistory); }
    private async void DeleteRangeClick(object sender, RoutedEventArgs e) => await Safely(async () => { var bounds = Bounds(); if (!Confirm("Delete submitted passages AND lookup events within the selected dates? With no dates selected, this clears all history. Personal entries and settings remain. Existing backups are independent copies.", "Delete history")) return; store.DeleteHistory(bounds.From, bounds.Until); searchId = null; historyOffset = 0; await RefreshHistory(); });
    private async void ClearHistoryClick(object sender, RoutedEventArgs e) => await Safely(async () => { if (!Confirm("Clear ALL submitted passages and lookup counts permanently? Personal entries and settings remain. Existing backups remain independent copies.", "Clear all history")) return; store.DeleteHistory(); searchId = null; historyOffset = 0; await RefreshHistory(); RefreshResources(); });
    private void OpenSettingsClick(object sender, RoutedEventArgs e) => Navigation.SelectedIndex = 3;
    private void RefreshResources()
    {
        SetupBanner.Visibility = reference is null ? Visibility.Visible : Visibility.Collapsed;
        InstallButton.Content = reference is null ? "Install offline resources" : "Reinstall pinned resources";
        ResourceInfo.Text = reference is null ? "No offline resources installed. Dictionary lookup requires the initial installation." : $"Installed · JMdict {ReferenceStore.DictionaryVersion}\nOpenJLPT {ReferenceStore.JlptRevision}\n{morphology?.Version}\nBuilt-in grammar {GrammarEngine.Version}";
        DataInfo.Text = $"Personal data: {dataDirectory}\nDatabase: {(File.Exists(store.DatabasePath) ? new FileInfo(store.DatabasePath).Length / 1048576.0 : 0):F2} MiB · History retained until you delete it";
    }
    private async void InstallClick(object sender, RoutedEventArgs e) => await Safely(async () =>
    {
        if (operation is not null) return;
        if (!Confirm("Download approximately 62 MiB of public dictionary and morphology resources? Installation needs about 500 MiB of disk space. Personal text is never included in requests.", "Install offline resources")) return;
        var token = BeginOperation("Starting resource installation…");
        try
        {
            await Task.Run(() => ReferenceStore.Install(Path.Combine(dataDirectory, "reference"), new Progress<string>(message => Dispatcher.InvokeAsync(() => Status.Text = message)), token), token);
            await Task.Run(LoadResources); RefreshResources(); Status.Text = "Offline resources installed. Ready to search.";
        }
        finally { FinishOperation(); }
    });
    private async void CheckUpdatesClick(object sender, RoutedEventArgs e) => await Safely(async () =>
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) }; client.DefaultRequestHeaders.UserAgent.ParseAdd("Gengo/0.1");
        // Check the supported pinned release, not upstream latest: independently advancing
        // Common/JLPT sources would break the required parity with Anki Stats.
        var version = Uri.EscapeDataString(ReferenceStore.DictionaryVersion);
        using var request = new HttpRequestMessage(HttpMethod.Head, $"https://github.com/scriptin/jmdict-simplified/releases/download/{version}/jmdict-eng-{version}.json.zip");
        using var response = await client.SendAsync(request); response.EnsureSuccessStatusCode();
        Status.Text = "Supported pinned resource release is available. This app build has no newer coordinated baseline. Reinstall to repair resources.";
    });
    private void FontChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (!initialized) return; store.SetSetting("fontSize", e.NewValue.ToString(CultureInfo.InvariantCulture)); ApplyTextSize(); }
    private async void BackupClick(object sender, RoutedEventArgs e) => await Safely(async () =>
    {
        var dialog = new SaveFileDialog { Title = "Create personal dictionary backup", Filter = "Gengo backup (*.zip)|*.zip", FileName = $"Gengo-{DateTime.Now:yyyyMMdd-HHmmss}.zip", OverwritePrompt = true };
        if (dialog.ShowDialog(this) != true) return;
        var temporary = dialog.FileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { await Task.Run(() => store.Backup(temporary, reference?.Version ?? "not installed")); File.Move(temporary, dialog.FileName, true); Status.Text = "Backup saved: " + dialog.FileName; }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    });
    private async void RestoreClick(object sender, RoutedEventArgs e) => await Safely(async () =>
    {
        if (operation is not null) return;
        var dialog = new OpenFileDialog { Title = "Restore personal dictionary backup", Filter = "Gengo backup (*.zip)|*.zip" }; if (dialog.ShowDialog(this) != true) return;
        var manifest = await Task.Run(() => PersonalStore.InspectBackup(dialog.FileName));
        if (!Confirm($"Backup from {manifest.Created.LocalDateTime:g}\n{manifest.PersonalEntries:N0} entries, {manifest.Searches:N0} passages, {manifest.Clicks:N0} lookup events.\n\nReplace current personal data? A safety backup is created first. Reference resources are not included; another machine may need installation.", "Restore backup")) return;
        BeginOperation("Restoring backup…"); Navigation.IsEnabled = false; string safety;
        try { safety = await Task.Run(() => store.Restore(dialog.FileName)); }
        finally { Navigation.IsEnabled = true; FinishOperation(); }
        analysis = null; searchId = null; lookupSelection.Clear(); displayedEntry = null; AnkiCopyText = ""; selectedUnit = null; SentenceCard.Visibility = Visibility.Collapsed; DefinitionPanel.Children.Clear(); GrammarPanel.Children.Clear(); AlternativesPanel.Children.Clear();
        ResetKanji(); WordsHint.Visibility = Visibility.Visible; ruby?.Dispose(); ruby = null; renderedAnalysis = null;
        RefreshPersonal(); StartEntry(""); Navigation.SelectedIndex = 3; RefreshResources();
        FontSlider.Value = double.TryParse(store.Setting("fontSize", "24"), CultureInfo.InvariantCulture, out var size) ? Math.Clamp(size, FontSlider.Minimum, FontSlider.Maximum) : 24;
        await InitializeAi();
        Status.Text = "Restored. Safety backup: " + safety;
    });
    private void WindowClosing(object? sender, CancelEventArgs e)
    {
        if (operation is not null) { operation.Cancel(); e.Cancel = true; Status.Text = "Canceling the current operation. Close the window once cancellation finishes."; return; }
        kanjiOperation?.Cancel(); kanjiOperation?.Dispose(); kanjiPopup.IsOpen = false; anki.Dispose(); ruby?.Dispose();
        reference?.Dispose(); morphology?.Dispose();
    }
    private bool Confirm(string text, string title) => MessageBox.Show(this, text, title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
    private static TextBlock Label(string text, double size = 14, Brush? foreground = null, Thickness? margin = null) => new() { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap, Foreground = foreground ?? Brushes.WhiteSmoke, Margin = margin ?? new Thickness(0, 0, 0, 6) };
    private Border Card(UIElement child) => new() { Style = (Style)FindResource("Card"), Child = child };
    private static Button ActionButton(string label, Action action) { var button = new Button { Content = label, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 10, 8, 8) }; button.Click += (_, _) => action(); return button; }
}
