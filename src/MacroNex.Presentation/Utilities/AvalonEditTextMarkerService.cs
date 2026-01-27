using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace MacroNex.Presentation.Utilities;

/// <summary>
/// Minimal text marker service for AvalonEdit to underline and tooltip error spans.
/// </summary>
public sealed class AvalonEditTextMarkerService : IBackgroundRenderer
{
    private readonly TextSegmentCollection<TextMarker> _markers;

    public AvalonEditTextMarkerService(TextDocument document)
    {
        _markers = new TextSegmentCollection<TextMarker>(document);
    }

    // Render above text/background in a consistent layer.
    public KnownLayer Layer => KnownLayer.Text;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (textView.Document == null)
            return;

        textView.EnsureVisualLines();

        foreach (var marker in _markers)
        {
            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, marker))
            {
                var underlinePen = new Pen(marker.UnderlineColor, 1);
                underlinePen.Freeze();

                // Draw slightly above the bottom edge to avoid clipping.
                var y = rect.Bottom - 1;
                var start = new System.Windows.Point(rect.Left, y);
                var end = new System.Windows.Point(rect.Right, y);
                drawingContext.DrawLine(underlinePen, start, end);
            }
        }
    }

    public void Clear()
    {
        _markers.Clear();
    }

    public void SetSingleMarker(int offset, int length, string message)
    {
        Clear();
        if (length <= 0) length = 1;
        var m = new TextMarker
        {
            StartOffset = offset,
            Length = length,
            Message = message,
            UnderlineColor = Brushes.IndianRed
        };
        _markers.Add(m);
    }

    private sealed class TextMarker : TextSegment
    {
        public Brush UnderlineColor { get; init; } = Brushes.IndianRed;
        public string Message { get; init; } = string.Empty;
    }
}

