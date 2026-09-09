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
    public static readonly SolidColorBrush Background = Brush("#141A1E");
    public static readonly SolidColorBrush Panel = Brush("#192126");
    public static readonly SolidColorBrush Text = Brush("#E9E7DF");
    public static readonly SolidColorBrush Muted = Brush("#8C97A3");
    public static readonly SolidColorBrush Signal = Brush("#76C9C1");
    public static readonly SolidColorBrush Copper = Brush("#E6A57E");
    public static readonly SolidColorBrush Good = Brush("#A5C685");
    public static SolidColorBrush Brush(string color) { var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); brush.Freeze(); return brush; }
}
