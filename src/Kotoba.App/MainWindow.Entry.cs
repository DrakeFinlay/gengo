using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Kotoba.Core;

namespace Kotoba.App;

public partial class MainWindow
{
    private Entry? displayedEntry;
    internal string AnkiCopyText { get; private set; } = "";
    private double ResultScale => JapaneseSize / 24;
    private string EntryTag(string tag) => EntryPresentation.Tag(tag, t => reference?.Label(t) ?? t);
    private TextBlock ResultLabel(string text, double size = 14, Brush? foreground = null, Thickness? margin = null)
    {
        var label = Label(text, size * ResultScale, foreground, margin); label.Tag = size; return label;
    }
    private TextBox SelectableLabel(string text, double size = 16, Brush? foreground = null, Thickness? margin = null) => new()
    {
        Text = text, Tag = size, IsReadOnly = true, IsReadOnlyCaretVisible = false, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
        FontSize = size * ResultScale, Foreground = foreground ?? Brushes.WhiteSmoke, Background = Brushes.Transparent,
        BorderThickness = new Thickness(0), Padding = new Thickness(0), Margin = margin ?? new Thickness(0, 0, 0, 8),
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        Cursor = System.Windows.Input.Cursors.IBeam
    };
    private void OpenDefinition(Entry entry, Unit unit, bool count, bool refreshKanji = true)
    {
        if (count && lookupSelection.Open(entry.Id)) store.RecordClick(entry, unit.Surface, searchId);
        displayedEntry = entry; selectedUnit = unit; DefinitionPanel.Children.Clear(); AlternativesPanel.Children.Clear();
        var matchedSpelling = entry.Kanji.Any(k => k.Text == unit.Surface) || entry.Kana.Any(k => k.Text == unit.Surface)
            ? unit.Surface : unit.Inflection?.Root ?? entry.Expression;
        var reading = entry.CompatibleReadings(matchedSpelling).OrderByDescending(k => TextRules.Hiragana(k.Text) == unit.Reading)
            .ThenByDescending(k => k.Common).FirstOrDefault()?.Text ?? entry.Reading;
        var preferred = entry.Expression;
        if (!entry.CompatibleReadings(preferred).Any(k => k.Text == reading)) preferred = matchedSpelling;
        AnkiCopyText = EntryPresentation.AnkiFront(preferred, reading);

        var title = new StackPanel();
        var wordSize = 30 * ResultScale;
        var badgeWidth = new FormattedText("Common word", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            12 * ResultScale, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip).Width + 36;
        var measured = new FormattedText(preferred, CultureInfo.GetCultureInfo("ja-JP"), FlowDirection.LeftToRight,
            new Typeface(new FontFamily(NaturalRuby.JapaneseFontStack), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), wordSize, Brushes.WhiteSmoke, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        var rubyWidth = new FormattedText(reading, CultureInfo.GetCultureInfo("ja-JP"), FlowDirection.LeftToRight,
            new Typeface(new FontFamily(NaturalRuby.JapaneseFontStack), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), wordSize * .5, Brushes.WhiteSmoke, VisualTreeHelper.GetDpi(this).PixelsPerDip).WidthIncludingTrailingWhitespace;
        var available = Math.Max(220, WordsScroll.ActualWidth - 45);
        var wordWidth = Math.Min(Math.Max(40, Math.Max(measured.WidthIncludingTrailingWhitespace, rubyWidth) + 4), available);
        var headword = new StackPanel { Width = wordWidth, HorizontalAlignment = HorizontalAlignment.Left };
        if (reading != preferred && reading.Length > 0)
        {
            headword.Children.Add(new NaturalRuby(reading, new FontFamily(NaturalRuby.JapaneseFontStack), wordSize * .5, Brushes.WhiteSmoke)
                { Width = wordWidth, Margin = new Thickness(0, 0, 0, 2), HorizontalAlignment = HorizontalAlignment.Left });
        }
        var spelling = SelectableLabel(preferred, 30); spelling.Name = "EntrySpelling"; spelling.FontFamily = new FontFamily(NaturalRuby.JapaneseFontStack); spelling.TextAlignment = TextAlignment.Center;
        headword.Children.Add(spelling); title.Children.Add(headword);
        if (entry.IsPersonal) title.Children.Add(Badge("Personal", Color.FromRgb(104, 190, 172)));
        if (entry.Common) title.Children.Add(Badge("Common word", Color.FromRgb(139, 196, 127)));
        foreach (var level in entry.Levels) title.Children.Add(Badge("JLPT " + level, Color.FromRgb(140, 168, 221)));
        var formTags = entry.Kanji.Where(k => k.Text == preferred).SelectMany(k => k.Tags)
            .Concat(entry.Kana.Where(k => k.Text == reading).SelectMany(k => k.Tags)).Distinct().Select(EntryTag).ToArray();
        if (formTags.Length > 0) title.Children.Add(ResultLabel(string.Join(" · ", formTags), 11, Muted));
        var copy = ActionButton("ANKI card copy", () => { Clipboard.SetText(AnkiCopyText); Status.Text = "Copied for Anki: " + AnkiCopyText; });
        copy.Name = "AnkiCardCopy"; copy.ToolTip = AnkiCopyText; title.Children.Add(copy);

        var senses = new StackPanel();
        var personalDefinition = entry.IsPersonal ? store.Entries().FirstOrDefault(p => p.Id == entry.Id) : null;
        for (var i = 0; i < entry.Senses.Length; i++)
        {
            var sense = entry.Senses[i];
            var codes = sense.PartsOfSpeech.Concat(sense.Misc).Concat(sense.Fields).Concat(sense.Dialects).Distinct().ToArray();
            var tags = SelectableLabel(string.Join(" · ", codes.Select(EntryTag).Distinct()), 12, Muted, new Thickness(0, i == 0 ? 0 : 12, 0, 4));
            tags.ToolTip = string.Join(" · ", codes.Select(t => reference?.Label(t) ?? t)); senses.Children.Add(tags);
            if (personalDefinition is { DefinitionRtf.Length: > 0 })
            {
                var formatted = new RichTextBox { IsReadOnly = true, IsDocumentEnabled = false, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0), VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, LayoutTransform = new ScaleTransform(ResultScale, ResultScale) };
                DefinitionFormatting.Load(formatted, personalDefinition.Definition, personalDefinition.DefinitionRtf); senses.Children.Add(formatted);
            }
            else senses.Children.Add(SelectableLabel($"{i + 1}. {string.Join("; ", sense.Glosses)}"));
            var restrictions = new List<string>();
            if (!sense.AppliesToKanji.Contains("*")) restrictions.Add("Spellings: " + string.Join(", ", sense.AppliesToKanji));
            if (!sense.AppliesToKana.Contains("*")) restrictions.Add("Readings: " + string.Join(", ", sense.AppliesToKana));
            if (restrictions.Count > 0) senses.Children.Add(SelectableLabel((entry.CompatibleSenses(matchedSpelling, reading).Contains(sense) ? "" : "Other form only · ") + string.Join(" · ", restrictions), 12, Muted));
            foreach (var info in sense.Information) senses.Children.Add(SelectableLabel(info, 13, Muted));
        }
        var otherForms = EntryPresentation.OtherForms(entry, preferred, reading);
        if (otherForms.Length > 0)
        {
            senses.Children.Add(ResultLabel("OTHER FORMS", 11, Muted, new Thickness(0, 18, 0, 6)));
            senses.Children.Add(SelectableLabel(string.Join(" · ", otherForms.Select(f => f.Expression + (f.Reading == f.Expression ? "" : " 【" + f.Reading + "】") +
                (f.Tags.Length == 0 ? "" : " (" + string.Join(", ", f.Tags.Select(EntryTag)) + ")"))), 14));
        }
        var definition = new Grid { Name = "EntryDefinition" };
        // At large text sizes or narrow widths, keep readable lines by stacking the
        // form above the senses rather than squeezing both into tiny columns.
        var titleWidth = Math.Max(155, Math.Max(wordWidth + 20, badgeWidth));
        if (titleWidth + 32 + Math.Max(200, 220 * ResultScale) > available)
        {
            definition.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); definition.RowDefinitions.Add(new RowDefinition { Height = new GridLength(24) }); definition.RowDefinitions.Add(new RowDefinition());
            var divider = new Border { Name = "EntryDivider", Height = 1, Background = (Brush)FindResource("BorderBrush"), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(divider, 1); definition.Children.Add(divider); Grid.SetRow(senses, 2);
        }
        else
        {
            definition.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(titleWidth) });
            definition.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) }); definition.ColumnDefinitions.Add(new ColumnDefinition());
            var divider = new Border { Name = "EntryDivider", Width = 1, Margin = new Thickness(15, 0, 15, 0), Background = (Brush)FindResource("BorderBrush"), HorizontalAlignment = HorizontalAlignment.Center };
            Grid.SetColumn(divider, 1); definition.Children.Add(divider); Grid.SetColumn(senses, 2);
        }
        definition.Children.Add(title); definition.Children.Add(senses); DefinitionPanel.Children.Add(Card(definition));
        if (unit.Inflection is { } inflection)
        {
            var p = new StackPanel(); p.Children.Add(ResultLabel("WORD FORM", 11, Accent)); p.Children.Add(SelectableLabel($"{unit.Surface} → {inflection.Root}", 21));
            foreach (var layer in inflection.Layers) p.Children.Add(SelectableLabel("• " + layer, 14));
            p.Children.Add(SelectableLabel(inflection.Explanation, 12, Muted)); DefinitionPanel.Children.Add(Card(p));
        }
        var alternatives = unit.EntryIds.Where(id => id != entry.Id).Select(Resolve).OfType<Entry>().ToArray();
        if (alternatives.Length > 0) AlternativesPanel.Children.Add(ResultLabel("Other dictionary candidates", 19, null, new Thickness(0, 8, 0, 12)));
        foreach (var alternative in alternatives)
        {
            var p = new StackPanel(); p.Children.Add(SelectableLabel($"{alternative.Expression}  {alternative.Reading}", 20));
            p.Children.Add(SelectableLabel(string.Join("; ", alternative.Senses.FirstOrDefault()?.Glosses ?? []), 14, Muted));
            p.Children.Add(ActionButton("Open this entry", () => OpenDefinition(alternative, unit, true))); AlternativesPanel.Children.Add(Card(p));
        }
        RenderSentence(); if (refreshKanji) RefreshKanji(unit); WordsScroll.ScrollToTop(); Status.Text = "Definition opened · " + entry.Expression;
    }
    private Border Badge(string text, Color color) => new()
    {
        Name = text.StartsWith("JLPT", StringComparison.Ordinal) ? "JlptBadge" : text == "Common word" ? "CommonBadge" : "PersonalBadge",
        Background = new SolidColorBrush(color), CornerRadius = new CornerRadius(3), Padding = new Thickness(8, 3, 8, 3),
        Margin = new Thickness(0, 5, 0, 5), HorizontalAlignment = HorizontalAlignment.Left,
        Child = new TextBlock { Text = text, Foreground = Brushes.Black, FontSize = 12 * ResultScale, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.NoWrap }
    };
}
