using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kotoba.App;

public partial class MainWindow
{
    internal const double TextSizeStep = 8.0 / 3; // Two typographic points in WPF device-independent units.
    internal void ChangeTextSize(int direction) => FontSlider.Value = Math.Clamp(FontSlider.Value + direction * TextSizeStep, FontSlider.Minimum, FontSlider.Maximum);
    private void UpdateReadingBounds()
    {
        AppHeader.Visibility = ActualHeight < 720 ? Visibility.Collapsed : Visibility.Visible;
        Sentence.MaxHeight = Math.Max(ActualHeight < 720 ? 55 : 155, JapaneseSize * 2.4);
        Input.MaxHeight = Math.Max(ActualHeight < 720 ? 55 : 92, JapaneseSize * .85 + 24);
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
