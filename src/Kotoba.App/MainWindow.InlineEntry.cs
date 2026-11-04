using System.Windows;
using System.Windows.Controls;
using Kotoba.Core;

namespace Kotoba.App;

public partial class MainWindow
{
    private void BuildInlinePersonalEntry(StackPanel form, string expression, Action cancel, WordAnswer? suggestion = null)
    {
        TextBox Field(string label, string name, string text = "", bool multiline = false)
        {
            form.Children.Add(ResultLabel(label, 14));
            var input = new TextBox
            {
                Name = name, Text = text, FontSize = 16 * ResultScale,
                AcceptsReturn = multiline, TextWrapping = TextWrapping.Wrap,
                MinHeight = multiline ? 90 : 0, MaxHeight = multiline ? 180 : double.PositiveInfinity,
                VerticalScrollBarVisibility = multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled
            };
            form.Children.Add(input); return input;
        }
        var expressionInput = Field("Expression *", "InlineExpression", expression);
        var readingInput = Field("Reading (optional)", "InlineReading", suggestion?.Reading ?? "");
        form.Children.Add(ResultLabel("Category", 14));
        var category = new ComboBox { Name = "InlineCategory", ItemsSource = new[] { "Personal words", "Names" }, SelectedIndex = 0, Margin = new Thickness(0, 0, 0, 10) };
        form.Children.Add(category);
        if (suggestion is not null) category.SelectedItem = suggestion.Category;
        var definitionInput = Field("English definition *", "InlineDefinition", suggestion?.Meaning ?? "", multiline: true);
        var notesInput = Field("Notes (optional)", "InlineNotes", multiline: true);
        var feedback = ResultLabel("", 14, Accent); feedback.Name = "InlineEntryStatus";
        var actions = new WrapPanel();
        var save = new Button { Content = "Save entry", Name = "SaveInlinePersonalEntry", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 10, 8, 8) };
        var cancelButton = ActionButton("Cancel", cancel); cancelButton.Name = "CancelInlinePersonalEntry";
        save.Click += async (_, _) => await Safely(async () =>
        {
            if (operation is not null) { feedback.Text = "Wait for the current operation or cancel it first."; return; }
            if (string.IsNullOrWhiteSpace(expressionInput.Text) || string.IsNullOrWhiteSpace(definitionInput.Text))
            {
                feedback.Text = "Enter an expression and an English definition.";
                (string.IsNullOrWhiteSpace(expressionInput.Text) ? expressionInput : definitionInput).Focus(); return;
            }
            save.IsEnabled = false; cancelButton.IsEnabled = false;
            try
            {
                var entry = store.SaveEntry(null, expressionInput.Text, readingInput.Text, definitionInput.Text, notesInput.Text, (string)category.SelectedItem);
                RefreshPersonal();
                if (analysis is not null) await Reanalyze();
                var unit = analysis?.Units.FirstOrDefault(u => u.EntryIds.Contains(entry.Id));
                if (unit is not null) { WordsHint.Visibility = Visibility.Collapsed; OpenDefinition(entry.AsEntry(), unit, false); }
                Status.Text = "Saved personal dictionary item · " + entry.Expression;
            }
            catch (ArgumentException ex) { feedback.Text = ex.Message; }
            finally { save.IsEnabled = true; cancelButton.IsEnabled = true; }
        });
        actions.Children.Add(save); actions.Children.Add(cancelButton); form.Children.Add(actions); form.Children.Add(feedback);
        definitionInput.Focus();
        definitionInput.BringIntoView();
    }
}
