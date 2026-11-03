using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Media;

namespace Kotoba.App;

/// <summary>Distribute natural-shaped text across the ruby base; never stretch glyphs.</summary>
internal sealed class NaturalRuby(string text, FontFamily fontFamily, double fontSize, Brush foreground) : FrameworkElement
{
    internal const string JapaneseFontStack = "Source Han Sans, 源ノ角ゴシック, Hiragino Sans, Noto Sans CJK JP, Meiryo, Yu Gothic, MS Gothic";
    internal string Text => text;
    internal FontFamily FontFamily => fontFamily;
    internal double FontSize => fontSize;
    internal Brush Foreground => foreground;
    internal double EffectiveFontSize { get; private set; }
    internal double[] GlyphWidths => glyphs.Select(g => g.WidthIncludingTrailingWhitespace).ToArray();
    private FormattedText[] glyphs = [];
    protected override Size MeasureOverride(Size availableSize)
    {
        var starts = StringInfo.ParseCombiningCharacters(text);
        var elements = starts.Select((start, index) => text[start..(index + 1 < starts.Length ? starts[index + 1] : text.Length)]).ToArray();
        FormattedText[] Format(double size) => elements.Select(element => new FormattedText(element, CultureInfo.GetCultureInfo("ja-JP"),
            FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), size,
            foreground, VisualTreeHelper.GetDpi(this).PixelsPerDip)).ToArray();
        EffectiveFontSize = fontSize; glyphs = Format(EffectiveFontSize);
        var naturalWidth = glyphs.Sum(g => g.WidthIncludingTrailingWhitespace);
        if (double.IsFinite(availableSize.Width) && availableSize.Width > 0 && naturalWidth > availableSize.Width)
        {
            // A very long reading may use a smaller font, retaining both proportions.
            EffectiveFontSize = fontSize * availableSize.Width / naturalWidth; glyphs = Format(EffectiveFontSize);
        }
        return new(glyphs.Sum(g => g.WidthIncludingTrailingWhitespace), glyphs.Select(g => g.Height).DefaultIfEmpty(0).Max());
    }
    protected override void OnRender(DrawingContext drawing)
    {
        var width = glyphs.Sum(g => g.WidthIncludingTrailingWhitespace);
        var gap = glyphs.Length > 1 ? Math.Max(0, ActualWidth - width) / (glyphs.Length - 1) : 0;
        var x = glyphs.Length == 1 ? Math.Max(0, (ActualWidth - width) / 2) : 0;
        foreach (var glyph in glyphs) { drawing.DrawText(glyph, new Point(x, 0)); x += glyph.WidthIncludingTrailingWhitespace + gap; }
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new RubyPeer(this);
    private sealed class RubyPeer(NaturalRuby owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetNameCore() => ((NaturalRuby)Owner).Text;
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;
    }
}
