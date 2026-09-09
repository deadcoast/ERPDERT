using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;
using Dert.Core;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;

namespace Dert.Notes;

/// <summary>Fit each authored mark to one measured title cell; never hide a wrong mark count.</summary>
public sealed class TitleRuleElementGenerator(TextEditor editor, Func<DertDocument> document) : VisualLineElementGenerator
{
    private object? key;
    private int first = -1, end;
    private double markWidth;
    private bool wraps;
    public bool TitleWraps { get { Measure(); return wraps; } }

    public override int GetFirstInterestedOffset(int startOffset)
    {
        Measure();
        var next = Math.Max(first, startOffset);
        return first >= 0 && next < end ? next : -1;
    }

    public override VisualLineElement? ConstructElement(int offset) =>
        offset >= first && offset < end && first >= 0 ? new Mark(CurrentContext.VisualLine, markWidth) : null;

    private void Measure()
    {
        var parsed = document();
        var dpi = VisualTreeHelper.GetDpi(editor).PixelsPerDip;
        var current = (parsed, editor.FontFamily, editor.FontSize, editor.FontStyle, editor.FontWeight,
            editor.FontStretch, editor.TextArea.TextView.ActualWidth, editor.WordWrap, dpi);
        if (Equals(key, current)) return;
        key = current; first = -1; wraps = false;
        var title = parsed.Nodes.OfType<NoteNode>().FirstOrDefault(note => note.IsTitle);
        if (title is null || title.Content.Text.All(character => character <= '~')) return;
        var plan = TitleRuleRepair.Plan(parsed);
        if (plan.Width is not > 0) return;
        var rule = parsed.Nodes.OfType<RuleNode>().FirstOrDefault(node => node.LineNumber == title.LineNumber + 1);
        if (rule is null) return;
        var line = parsed.Source.Lines[rule.LineNumber - 1];
        var raw = parsed.Source.Slice(line.ContentSpan);
        if (!raw.TrimStart().StartsWith("//", StringComparison.Ordinal)) return;
        var typeface = new Typeface(editor.FontFamily, editor.FontStyle, editor.FontWeight, editor.FontStretch);
        FormattedText Shape(string text) => new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            typeface, editor.FontSize, Palette.Text, dpi);
        var fullTitle = parsed.Source.Slice(parsed.Source.Lines[title.LineNumber - 1].ContentSpan);
        if (editor.WordWrap && Shape(fullTitle).WidthIncludingTrailingWhitespace > editor.TextArea.TextView.ActualWidth)
        { wraps = true; return; }
        markWidth = Shape(title.Content.Text).WidthIncludingTrailingWhitespace / plan.Width.Value;
        if (!double.IsFinite(markWidth) || markWidth <= 0) return;
        first = line.Start + raw.IndexOf('-');
        end = first + raw.TrimEnd().Length - raw.IndexOf('-');
    }

    private sealed class Mark(VisualLine line, double width) : VisualLineText(line, 1)
    {
        public override TextRun CreateTextRun(int startVisualColumn, ITextRunConstructionContext context) =>
            new MarkRun(width, TextRunProperties, VisualTreeHelper.GetDpi(context.TextView).PixelsPerDip);
    }

    private sealed class MarkRun : TextEmbeddedObject
    {
        private readonly double width;
        private readonly FormattedText glyph;
        public override TextRunProperties Properties { get; }
        public MarkRun(double width, TextRunProperties properties, double dpi)
        {
            this.width = width; Properties = properties;
            glyph = new("-", properties.CultureInfo, FlowDirection.LeftToRight, properties.Typeface,
                properties.FontRenderingEmSize, properties.ForegroundBrush, dpi);
        }
        public override int Length => 1;
        public override CharacterBufferReference CharacterBufferReference => new();
        public override LineBreakCondition BreakBefore => LineBreakCondition.BreakPossible;
        public override LineBreakCondition BreakAfter => LineBreakCondition.BreakPossible;
        public override bool HasFixedSize => true;
        public override TextEmbeddedObjectMetrics Format(double remainingParagraphWidth) => new(width, glyph.Height, glyph.Baseline);
        public override Rect ComputeBoundingBox(bool rightToLeft, bool sideways) => new(0, 0, width, glyph.Height);
        public override void Draw(DrawingContext context, Point origin, bool rightToLeft, bool sideways)
        {
            context.PushTransform(new TranslateTransform(origin.X, origin.Y - glyph.Baseline));
            context.PushTransform(new ScaleTransform(width / glyph.WidthIncludingTrailingWhitespace, 1));
            context.DrawText(glyph, new Point());
            context.Pop(); context.Pop();
        }
    }
}
