using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Kotoba.Core;
using Kotoba.Infrastructure;

namespace Kotoba.App;

public partial class MainWindow
{
    private AnkiKanjiOptions KanjiOptions => new(store.Setting("ankiDeck", "漢字"), store.Setting("ankiExpressionField", "Front"), store.Setting("ankiMeaningField", "Back"));
    private void ResetKanji()
    {
        kanjiOperation?.Cancel(); kanjiOperation?.Dispose(); kanjiOperation = null; kanjiPopup.IsOpen = false;
        KanjiPanel.Children.Clear(); KanjiPanel.Children.Add(ResultLabel("Choose a word to see its kanji and your Anki meanings.", 13, Muted));
    }
    private async void RefreshKanji(Unit unit)
    {
        kanjiOperation?.Cancel(); kanjiOperation?.Dispose(); kanjiOperation = new(); var token = kanjiOperation.Token;
        kanjiPopup.IsOpen = false; KanjiPanel.Children.Clear(); DetailsScroll.ScrollToTop();
        var characters = unit.Surface.EnumerateRunes().Where(r => TextRules.HasKanji(r.ToString())).Select(r => r.ToString()).Distinct().ToArray();
        if (characters.Length == 0) { KanjiPanel.Children.Add(ResultLabel("This word contains no kanji.", 13, Muted)); return; }
        // Create all cards immediately. Anki's local HTTP response never blocks definitions.
        var cards = characters.Select(character =>
        {
            var meaning = ResultLabel("Looking in Anki…", 15, Muted); var source = ResultLabel("ANKI · " + KanjiOptions.Deck, 10, Accent);
            var column = new StackPanel(); column.Children.Add(source); column.Children.Add(meaning);
            var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); row.ColumnDefinitions.Add(new ColumnDefinition());
            var glyph = ResultLabel(character, 46); glyph.Margin = new Thickness(0, 0, 12, 0); glyph.FontFamily = new FontFamily("Yu Gothic UI"); Grid.SetColumn(column, 1); row.Children.Add(glyph); row.Children.Add(column);
            var button = new Button { Content = row, HorizontalContentAlignment = HorizontalAlignment.Stretch, HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 12) };
            button.ToolTip = "Open dictionary meanings and On/Kun readings";
            button.Click += async (_, _) => await ShowKanjiPopup(character, meaning, source);
            KanjiPanel.Children.Add(button); return (Character: character, Meaning: meaning, Source: source);
        }).ToArray();
        foreach (var card in cards)
        {
            try
            {
                var result = await anki.Find(card.Character, KanjiOptions, token); token.ThrowIfCancellationRequested();
                card.Meaning.Text = result.DisplayMeaning;
                card.Meaning.Foreground = result.Notes.Length == 0 ? Muted : Brushes.WhiteSmoke;
                card.Source.Text = $"ANKI · {result.Profile} · {KanjiOptions.Deck}";
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                if (token.IsCancellationRequested) return;
                foreach (var pending in cards.Where(c => c.Meaning.Text == "Looking in Anki…"))
                {
                    pending.Meaning.Text = "Anki unavailable. Keep Anki open with AnkiConnect enabled.";
                    pending.Source.Text = "ANKI · Check mapping in Settings"; pending.Meaning.ToolTip = ex.Message;
                }
                break;
            }
        }
    }
    internal FrameworkElement? KanjiPopupContent => kanjiPopup.Child as FrameworkElement;
    internal async Task ShowKanjiPopup(string character, TextBlock meaning, TextBlock source)
    {
        var panel = new StackPanel { Width = Math.Min(460, SystemParameters.WorkArea.Width - 80) };
        panel.Children.Add(ResultLabel(character, 54));
        var attribution = ResultLabel("", 11, Accent); attribution.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = source });
        var gloss = ResultLabel("", 18); gloss.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = meaning });
        panel.Children.Add(attribution); panel.Children.Add(gloss);
        panel.Children.Add(new Border { BorderBrush = Muted, BorderThickness = new Thickness(0, 1, 0, 0), Margin = new Thickness(0, 16, 0, 12) });
        panel.Children.Add(ResultLabel("DICTIONARY · KANJIDIC2", 11, Accent));
        var details = new StackPanel(); details.Children.Add(ResultLabel("Loading offline character details…", 14, Muted)); panel.Children.Add(details);
        var scroll = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, MaxHeight = Math.Max(160, Math.Min(650, SystemParameters.WorkArea.Height - 160)) };
        var shell = new StackPanel(); shell.Children.Add(scroll); shell.Children.Add(ActionButton("Close", () => kanjiPopup.IsOpen = false));
        var card = Card(shell); kanjiPopup.Child = card; kanjiPopup.PlacementTarget = this; kanjiPopup.IsOpen = true;
        try
        {
            var entry = await KanjiDictionary.Find(character);
            if (!ReferenceEquals(kanjiPopup.Child, card)) return;
            details.Children.Clear();
            if (entry is null) { details.Children.Add(ResultLabel("No character dictionary entry is available for this kanji.", 15, Muted)); return; }
            var metadata = new List<string>();
            if (entry.StrokeCounts.Length > 0) metadata.Add($"{entry.StrokeCounts[0]} strokes" + (entry.StrokeCounts.Length > 1 ? $" (alternate counts: {string.Join(", ", entry.StrokeCounts.Skip(1))})" : ""));
            if (entry.Grade is >= 1 and <= 6) metadata.Add($"School grade {entry.Grade}");
            else if (entry.Grade == 8) metadata.Add("Jōyō · secondary school");
            else if (entry.Grade is 9 or 10) metadata.Add("Jinmeiyō · name-use kanji");
            details.Children.Add(ResultLabel(string.Join(" · ", metadata), 12, Muted));
            foreach (var group in entry.Groups)
            {
                details.Children.Add(ResultLabel(group.Meanings.Length == 0 ? "No English meaning recorded." : string.Join("; ", group.Meanings), 18, margin: new Thickness(0, 10, 0, 8)));
                AddReadings("Kun", group.Readings.Where(r => r.Type == "ja_kun"));
                AddReadings("On", group.Readings.Where(r => r.Type == "ja_on"));
            }
            if (entry.NameReadings.Length > 0) details.Children.Add(ResultLabel("Name readings (nanori): " + string.Join("、", entry.NameReadings), 15, margin: new Thickness(0, 8, 0, 0)));
            details.Children.Add(ResultLabel("Kun dots mark the start of okurigana; hyphens mark a prefix/suffix reading. Name readings apply in names, not every word.", 11, Muted, new Thickness(0, 12, 0, 0)));
            details.Children.Add(ResultLabel($"EDRDG · CC BY-SA 4.0 · {KanjiDictionary.Version}", 10, Muted, new Thickness(0, 8, 0, 0)));

            void AddReadings(string label, IEnumerable<KanjiReading> readings)
            {
                var values = readings.Select(r => r.Value + (r.Status is null && r.OnType is null ? "" : $" ({string.Join(", ", new[] { r.OnType, r.Status }.OfType<string>())})")).ToArray();
                details.Children.Add(ResultLabel(label + ": " + (values.Length == 0 ? "None recorded" : string.Join("、", values)), 16, margin: new Thickness(0, 4, 0, 0)));
            }
        }
        catch (Exception ex)
        {
            details.Children.Clear(); details.Children.Add(ResultLabel("Character dictionary unavailable. Reinstall the app package to restore its bundled data.", 14, Muted)); details.ToolTip = ex.Message;
        }
    }
    private void SaveAnkiClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var options = new AnkiKanjiOptions(AnkiDeckInput.Text.Trim(), AnkiExpressionInput.Text.Trim(), AnkiMeaningInput.Text.Trim()); AnkiKanjiClient.Validate(options);
            store.SetSetting("ankiDeck", options.Deck); store.SetSetting("ankiExpressionField", options.ExpressionField); store.SetSetting("ankiMeaningField", options.MeaningField);
            anki.ClearCache(); if (selectedUnit is not null) RefreshKanji(selectedUnit); Status.Text = "Anki kanji mapping saved. Exact single-character cards only.";
        }
        catch (ArgumentException ex) { Status.Text = ex.Message; }
    }
}
