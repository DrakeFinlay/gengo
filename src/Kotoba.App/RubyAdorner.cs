using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using Kotoba.Core;

namespace Kotoba.App;

/// <summary>Ruby geometry is rebuilt only on content/size/scroll changes.
/// Never invalidate layout from LayoutUpdated: InvalidateVisual itself queues layout.</summary>
internal sealed class RubyAdorner : Adorner, IDisposable
{
    private readonly RichTextBox document;
    private readonly (Run Run, int Offset, int Length, string Reading)[] parts;
    private readonly double size;
    private readonly Brush brush;
    private DrawingGroup? cached;
    private Vector cachedScrollOffset;
    private bool queued;
    private bool disposed;
    internal int RebuildCount { get; private set; }
    internal int RenderCount { get; private set; }
    public RubyAdorner(RichTextBox document, IReadOnlyList<(Unit Unit, Run Run)> runs, double size, Brush brush) : base(document)
    {
        this.document = document; this.size = size; this.brush = brush; IsHitTestVisible = false;
        parts = runs.Where(w => TextRules.HasKanji(w.Unit.Surface) && w.Unit.Reading.Length > 0)
            .SelectMany(w => RubyParts(w.Unit.Surface, w.Unit.Reading).Select(p => (w.Run, p.Offset, p.Length, p.Reading))).ToArray();
        document.SizeChanged += DocumentSizeChanged;
        document.Loaded += LoadedDocument;
        document.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(Scrolled));
        QueueRebuild();
    }
    private void LoadedDocument(object sender, RoutedEventArgs e) => QueueRebuild();
    private void DocumentSizeChanged(object sender, SizeChangedEventArgs e) => InvalidateGeometry();
    private void Scrolled(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalChange != 0 || e.HorizontalChange != 0 || e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0 || e.ExtentHeightChange != 0) InvalidateGeometry();
    }
    private void InvalidateGeometry() { cached = null; InvalidateVisual(); QueueRebuild(); }
    private void QueueRebuild()
    {
        if (disposed || queued) return; queued = true;
        document.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            queued = false; if (disposed || !document.IsLoaded) return;
            Rebuild();
        });
    }
    private void Rebuild()
    {
        var scrollOffset = new Vector(document.HorizontalOffset, document.VerticalOffset);
        var drawing = new DrawingGroup();
        using (var context = drawing.Open())
        {
            var viewport = ContentViewport();
            context.PushClip(new RectangleGeometry(viewport));
            var dpi = VisualTreeHelper.GetDpi(document).PixelsPerDip;
            foreach (var (run, offset, length, reading) in parts)
            {
                var start = run.ContentStart.GetPositionAtOffset(offset); var end = start?.GetPositionAtOffset(length); if (start is null || end is null) continue;
                var a = start.GetCharacterRect(LogicalDirection.Forward); var b = end.GetCharacterRect(LogicalDirection.Backward);
                // An adorner is outside the editor's scroll-content clipping. Require
                // the base glyphs to be visible too, rather than drawing orphan ruby.
                if (a.IsEmpty || b.IsEmpty || a.Top < viewport.Top || b.Bottom > viewport.Bottom || a.Bottom <= viewport.Top || a.Top >= viewport.Bottom) continue;
                var width = Math.Abs(a.Y - b.Y) < 2 ? b.Right - a.Left : Math.Min(document.ActualWidth - a.Left, length * size);
                if (width <= 0) continue;
                var formatted = new FormattedText(reading, CultureInfo.GetCultureInfo("ja-JP"), FlowDirection.LeftToRight, new Typeface("Yu Gothic UI"), size * .43, brush, dpi);
                if (formatted.Width > width)
                    formatted = new FormattedText(reading, CultureInfo.GetCultureInfo("ja-JP"), FlowDirection.LeftToRight, new Typeface("Yu Gothic UI"), size * .43 * width / formatted.Width, brush, dpi);
                var position = new Point(a.Left + Math.Max(0, (width - formatted.Width) / 2), a.Top - size * .55);
                if (position.Y < viewport.Top || position.Y + formatted.Height > viewport.Bottom) continue;
                context.DrawText(formatted, position);
            }
            context.Pop();
        }
        drawing.Freeze(); cached = drawing; cachedScrollOffset = scrollOffset; RebuildCount++; InvalidateVisual();
    }
    protected override void OnRender(DrawingContext drawing)
    {
        RenderCount++;
        if (cached is not null && cachedScrollOffset == new Vector(document.HorizontalOffset, document.VerticalOffset))
        {
            drawing.PushClip(new RectangleGeometry(ContentViewport())); drawing.DrawDrawing(cached); drawing.Pop();
        }
    }
    private Rect ContentViewport()
    {
        static ScrollContentPresenter? Find(DependencyObject parent)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is ScrollContentPresenter presenter) return presenter;
                if (Find(child) is { } found) return found;
            }
            return null;
        }
        var viewport = new Rect(new Point(), document.RenderSize);
        if (Find(document) is { } presenter)
            viewport.Intersect(presenter.TransformToAncestor(document).TransformBounds(new Rect(new Point(), presenter.RenderSize)));
        return viewport;
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        document.SizeChanged -= DocumentSizeChanged; document.Loaded -= LoadedDocument;
        document.RemoveHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(Scrolled));
        AdornerLayer.GetAdornerLayer(document)?.Remove(this);
    }
    internal static IEnumerable<(int Offset, int Length, string Reading)> RubyParts(string surface, string reading)
    {
        var pieces = Regex.Matches(surface, @"[\p{IsCJKUnifiedIdeographs}々]+|[^\p{IsCJKUnifiedIdeographs}々]+").Cast<Match>().ToArray();
        var result = new List<(int, int, string)>(); var cursor = 0;
        for (var i = 0; i < pieces.Length; i++)
        {
            var piece = pieces[i];
            if (!TextRules.HasKanji(piece.Value))
            {
                var kana = TextRules.Hiragana(piece.Value);
                if (!reading.AsSpan(cursor).StartsWith(kana, StringComparison.Ordinal)) return [(0, surface.Length, reading)]; cursor += kana.Length; continue;
            }
            var end = reading.Length;
            if (i + 1 < pieces.Length)
            {
                var anchor = TextRules.Hiragana(pieces[i + 1].Value); end = reading.IndexOf(anchor, cursor, StringComparison.Ordinal);
                if (end <= cursor || reading.IndexOf(anchor, end + 1, StringComparison.Ordinal) >= 0) return [(0, surface.Length, reading)];
            }
            if (end <= cursor) return [(0, surface.Length, reading)]; result.Add((piece.Index, piece.Length, reading[cursor..end])); cursor = end;
        }
        return cursor == reading.Length ? result : [(0, surface.Length, reading)];
    }
}
