using System.Windows.Media;
using Dert.Core;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace Dert.Notes;

public sealed class DertColorizer(Func<DertDocument> document) : DocumentColorizingTransformer
{
    protected override void ColorizeLine(DocumentLine line)
    {
        foreach (var highlight in document().Highlights)
        {
            var start = Math.Max(line.Offset, highlight.Span.Start);
            var end = Math.Min(line.EndOffset, highlight.Span.End);
            if (end <= start) continue;
            ChangeLinePart(start, end, part => part.TextRunProperties.SetForegroundBrush(highlight.Role switch
            {
                SemanticRole.ContextNote or SemanticRole.Rule => Palette.Muted,
                SemanticRole.Hook or SemanticRole.Boundary => Palette.Copper,
                SemanticRole.Line or SemanticRole.Reasoning or SemanticRole.Target => Palette.Signal,
                _ => Palette.Text
            }));
        }
    }
}

public static class Palette
{
    public static readonly SolidColorBrush Background = Brush("#090A0E");
    public static readonly SolidColorBrush Panel = Brush("#14151E");
    public static readonly SolidColorBrush Text = Brush("#E4E2EA");
    public static readonly SolidColorBrush Muted = Brush("#90909F");
    public static readonly SolidColorBrush Signal = Brush("#B5A7EF");
    public static readonly SolidColorBrush Copper = Brush("#E7B17D");
    public static readonly SolidColorBrush Good = Brush("#90B3EB");
    public static SolidColorBrush Brush(string color) { var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); brush.Freeze(); return brush; }
}
