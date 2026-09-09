using System.Buffers;
using System.Globalization;
using System.Text;

namespace Dert.Text;

/// <summary>DERT's cell policy: Unicode 17 width data, .NET grapheme boundaries, ambiguous width one.</summary>
public static class DisplayCells
{
    public static bool TryMeasureTitle(string text, out int width, out string reason)
    {
        width = 0; reason = "";
        for (var offset = 0; offset < text.Length;)
        {
            if (Rune.DecodeFromUtf16(text.AsSpan(offset), out var rune, out var consumed) != OperationStatus.Done)
            { reason = "This title contains an unfinished Unicode character. Its width will be checked when the character is complete."; return false; }
            offset += consumed;
            if (rune.Value == '\t')
            { reason = "A tab has no fixed title width across these views. Your tab and words stay unchanged; use spaces if you want an aligned rule."; return false; }
            if (MustEscape(rune))
            { reason = "This title contains a control or formatting character with no portable visible width. Your source stays unchanged."; return false; }
        }
        foreach (var element in RawElements(text))
        {
            if (Orphan(element))
            { reason = "A combining mark or joiner has no base character here, so its title width is not checked. Your words stay unchanged."; return false; }
            width += ClusterWidth(element);
        }
        return true;
    }

    public static string Safe(string text)
    {
        var output = new StringBuilder(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value is '\r' or '\n' or '\t')
                output.Append(rune.Value switch { '\r' => "\\r", '\n' => "\\n", _ => "\\t" });
            else if (MustEscape(rune))
                output.Append(rune.Value <= 0xFFFF ? $"\\u{rune.Value:X4}" : $"\\U{rune.Value:X8}");
            else output.Append(rune.ToString());
        }
        return output.ToString();
    }

    public static IEnumerable<(string Text, int Width)> Elements(string text)
    {
        foreach (var element in RawElements(Safe(text)))
            yield return Orphan(element) ? ("◌" + element, 1) : (element, ClusterWidth(element));
    }

    private static IEnumerable<string> RawElements(string text)
    {
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext()) yield return enumerator.GetTextElement();
    }

    private static bool MustEscape(Rune rune) => Rune.GetUnicodeCategory(rune) is
        UnicodeCategory.Control or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator ||
        Rune.GetUnicodeCategory(rune) == UnicodeCategory.Format && rune.Value is not (0x200C or 0x200D);

    private static bool Orphan(string cluster)
    {
        var first = cluster.EnumerateRunes().First();
        return Rune.GetUnicodeCategory(first) is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.SpacingCombiningMark ||
            first.Value is 0x200C or 0x200D;
    }

    private static int ClusterWidth(string cluster)
    {
        var runes = cluster.EnumerateRunes().ToArray();
        if (runes.Any(rune => Contains(UnicodeWidthData.Wide, rune.Value))) return 2;
        var textPresentation = cluster.Contains('\uFE0E');
        if (!textPresentation && runes.Any(rune => Contains(UnicodeWidthData.EmojiPresentation, rune.Value))) return 2;
        if (cluster.Contains('\uFE0F') && Contains(UnicodeWidthData.Emoji, runes[0].Value)) return 2;
        if (runes[^1].Value == 0x20E3 && runes[0].Value is >= '0' and <= '9' or '#' or '*') return 2;
        return 1;
    }

    private static bool Contains((int First, int Last)[] ranges, int value)
    {
        var low = 0; var high = ranges.Length - 1;
        while (low <= high)
        {
            var middle = (low + high) / 2;
            var range = ranges[middle];
            if (value < range.First) high = middle - 1;
            else if (value > range.Last) low = middle + 1;
            else return true;
        }
        return false;
    }
}
