using System.Windows.Media.TextFormatting;
using Dert.Core;
using ICSharpCode.AvalonEdit.Rendering;

namespace Erpdert.Notes;

/// <summary>A single displayed glyph with the source character's native positions.</summary>
public sealed class FlorinElementGenerator(Func<DertDocument> document, Func<bool> showTyped) : VisualLineElementGenerator
{
    private DertDocument? captured;
    private int[] offsets = [];

    public override int GetFirstInterestedOffset(int startOffset)
    {
        if (showTyped()) return -1;
        var current = document();
        if (!ReferenceEquals(captured, current))
        {
            captured = current;
            offsets = FlorinGoodbye.Endings(current).Where(ending => ending.Spelling == "fin.")
                .Select(ending => ending.Span.Start).ToArray();
        }
        return offsets.FirstOrDefault(offset => offset >= startOffset, -1);
    }

    public override VisualLineElement? ConstructElement(int offset) =>
        !showTyped() && Array.BinarySearch(offsets, offset) >= 0 ? new FlorinGlyph(CurrentContext.VisualLine) : null;

    private sealed class FlorinGlyph(VisualLine line) : VisualLineText(line, 1)
    {
        public override TextRun CreateTextRun(int startVisualColumn, ITextRunConstructionContext context) =>
            new TextCharacters("ƒ", TextRunProperties);
    }
}
