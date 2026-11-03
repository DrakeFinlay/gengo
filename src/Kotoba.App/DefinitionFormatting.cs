using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Kotoba.App;

internal static class DefinitionFormatting
{
    internal static string PlainText(RichTextBox editor) => new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text.TrimEnd('\r', '\n');
    internal static string Save(RichTextBox editor)
    {
        using var stream = new MemoryStream();
        new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Save(stream, DataFormats.Rtf);
        return Convert.ToBase64String(stream.ToArray());
    }
    internal static void Load(RichTextBox editor, string text, string rtf)
    {
        editor.Document = new FlowDocument { FontFamily = new FontFamily("Segoe UI"), FontSize = 14, Foreground = Brushes.WhiteSmoke, PagePadding = new Thickness(0) };
        if (rtf.Length > 0 && rtf.Length <= 400000)
        {
            try
            {
                using var stream = new MemoryStream(Convert.FromBase64String(rtf));
                new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Load(stream, DataFormats.Rtf); return;
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or IOException) { /* Keep the plain-text fallback usable. */ }
        }
        editor.Document.Blocks.Add(new Paragraph(new Run(text)) { Margin = new Thickness(0, 0, 0, 8) });
    }
}

public partial class MainWindow
{
    private void PersonalFilterChanged(object sender, RoutedEventArgs e) { if (PersonalList is not null) RefreshPersonal(); }
    private void SmallNoteClick(object sender, RoutedEventArgs e)
    {
        var small = DefinitionInput.Selection.GetPropertyValue(TextElement.FontSizeProperty) is double size && size < 14;
        DefinitionInput.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, small ? 14d : 11d);
        DefinitionInput.Focus();
    }
    private void SubscriptClick(object sender, RoutedEventArgs e)
    {
        var subscript = Equals(DefinitionInput.Selection.GetPropertyValue(Typography.VariantsProperty), FontVariants.Subscript);
        DefinitionInput.Selection.ApplyPropertyValue(Typography.VariantsProperty, subscript ? FontVariants.Normal : FontVariants.Subscript);
        DefinitionInput.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, subscript ? 14d : 11d);
        DefinitionInput.Focus();
    }
}
