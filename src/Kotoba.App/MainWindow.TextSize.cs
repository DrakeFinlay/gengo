using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Kotoba.App;

public partial class MainWindow
{
    internal const double TextSizeStep = 8.0 / 3; // Two typographic points in WPF device-independent units.
    private bool readingBoundsQueued;
    internal void ChangeTextSize(int direction) => FontSlider.Value = Math.Clamp(FontSlider.Value + direction * TextSizeStep, FontSlider.Minimum, FontSlider.Maximum);
    private void UpdateReadingBounds()
    {
        AppHeader.Visibility = ActualHeight < 720 ? Visibility.Collapsed : Visibility.Visible;
        var sentenceLimit = 2 * Math.Max(ActualHeight < 720 ? 55 : 155, JapaneseSize * 2.4);
        var inputLimit = 2 * Math.Max(ActualHeight < 720 ? 55 : 92, JapaneseSize * .85 + 24);
        // Grow naturally with content. On short windows, share the available space
        // instead of allowing two long editors to push the dictionary panes away.
        if (SearchWorkspace.ActualHeight > 0 && SearchInputCard.ActualHeight > 0)
        {
            var overhead = SearchInputCard.ActualHeight - Input.ActualHeight + SearchInputCard.Margin.Top + SearchInputCard.Margin.Bottom;
            var sentenceVisible = SentenceCard.Visibility == Visibility.Visible;
            if (sentenceVisible) overhead += SentenceCard.ActualHeight - Sentence.ActualHeight + SentenceCard.Margin.Top + SentenceCard.Margin.Bottom;
            if (SetupBanner.Visibility == Visibility.Visible) overhead += SetupBanner.ActualHeight + SetupBanner.Margin.Top + SetupBanner.Margin.Bottom;
            var budget = Math.Max(128, SearchWorkspace.ActualHeight - overhead - 140);
            var inputNeed = Math.Min(inputLimit, Math.Max(48, Input.ActualHeight + Math.Max(0, Input.ExtentHeight - Input.ViewportHeight)));
            var sentenceNeed = sentenceVisible ? Math.Min(sentenceLimit, Sentence.ActualHeight + Math.Max(0, Sentence.ExtentHeight - Sentence.ViewportHeight)) : 0;
            if (inputNeed + sentenceNeed > budget)
            {
                var ratio = budget / (inputNeed + sentenceNeed);
                inputLimit = Math.Max(48, inputNeed * ratio);
                sentenceLimit = Math.Max(JapaneseSize * 2.4, sentenceNeed * ratio);
            }
        }
        if (Math.Abs(Sentence.MaxHeight - sentenceLimit) > .5) Sentence.MaxHeight = sentenceLimit;
        if (Math.Abs(Input.MaxHeight - inputLimit) > .5) Input.MaxHeight = inputLimit;
    }
    private void ReadingWorkspaceSizeChanged(object sender, SizeChangedEventArgs e) => QueueReadingBounds();
    private void QueueReadingBounds()
    {
        if (!initialized || readingBoundsQueued) return;
        readingBoundsQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => { readingBoundsQueued = false; UpdateReadingBounds(); });
    }
    private void ReadingExtentChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.ExtentHeightChange != 0 || e.ViewportHeightChange != 0) QueueReadingBounds();
    }
    private void ApplyTextSize()
    {
        Input.FontSize = JapaneseSize * .85;
        FontSizeLabel.Text = $"{JapaneseSize * .75:0.#} pt · Ctrl+Plus / Ctrl+Minus changes 2 pt";
        UpdateReadingBounds(); RenderSentence();
        if (displayedEntry is not null && selectedUnit is not null) OpenDefinition(displayedEntry, selectedUnit, false, false);
        else ScaleResultLabels(DefinitionPanel);
        foreach (var root in new[] { GrammarPanel, KanjiPanel }) ScaleResultLabels(root);
    }
    private void ScaleResultLabels(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is TextBlock { Tag: double originalSize } text) text.FontSize = originalSize * ResultScale;
            if (child is TextBox { Tag: double originalTextSize } box) box.FontSize = originalTextSize * ResultScale;
            ScaleResultLabels(child);
        }
    }
    private void PopulateTerminology()
    {
        TerminologyPanel.Children.Clear();
        foreach (var term in Kotoba.Core.EntryPresentation.Terms)
        {
            TerminologyPanel.Children.Add(Label(term.Label + " — " + term.JapaneseTerm, 16, Accent, new Thickness(0, 12, 0, 4)));
            TerminologyPanel.Children.Add(Label(term.Explanation, 14));
        }
        TerminologyPanel.Children.Add(Label("Definitions use short English labels. Hover a label to see the original dictionary wording. Terminology follows JMdict / EDRDG; the explanations here are project-authored.", 12, Muted, new Thickness(0, 16, 0, 0)));
    }
}
