using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kotoba.Core;

namespace Kotoba.App;

public partial class MainWindow
{
    private async void StatisticDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || sender is not ListBoxItem { Tag: WordStatistic word }) return;
        e.Handled = true; await Safely(() => OpenStatistic(word));
    }
    private async void StatisticKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || StatisticsList.SelectedItem is not ListBoxItem { Tag: WordStatistic word }) return;
        e.Handled = true; await Safely(() => OpenStatistic(word));
    }
    internal async Task OpenStatistic(WordStatistic word)
    {
        if (operation is not null) return;
        if (engine is null) { Navigation.SelectedIndex = 3; Status.Text = "Install offline resources to reopen dictionary definitions."; return; }
        var entry = Resolve(word.EntryId);
        if (entry is null) { Status.Text = "This entry is no longer available. Its statistics are preserved."; return; }
        var previous = analysis;
        await Search(entry.Expression, false);
        if (ReferenceEquals(previous, analysis) || analysis is null) return;
        // This is a statistics revisit, not a new submission or counted lookup.
        // Never link subsequent clicks to the previously displayed passage.
        searchId = null; Input.Text = entry.Expression;
        var unit = analysis.Units.Length == 1 ? analysis.Units[0] : new Unit(0, entry.Expression.Length, entry.Expression, entry.Reading, [entry.Id], analysis.Units.SelectMany(u => u.Tokens).ToArray());
        var reading = entry.CompatibleReadings(entry.Expression).FirstOrDefault(k => TextRules.Hiragana(k.Text) == TextRules.Hiragana(word.Reading))?.Text ?? entry.Reading;
        unit = unit with { EntryIds = new[] { entry.Id }.Concat(unit.EntryIds).Distinct().ToArray(), Reading = reading, Personal = entry.IsPersonal };
        analysis = analysis with { Units = [unit] };
        lookupSelection.Open(entry.Id);
        Navigation.SelectedIndex = 0; WordsHint.Visibility = Visibility.Collapsed;
        OpenDefinition(entry, unit, false);
        Status.Text = "Definition reopened from statistics · " + entry.Expression + " · No new lookup recorded";
        WordsScroll.ScrollToTop();
    }
}
