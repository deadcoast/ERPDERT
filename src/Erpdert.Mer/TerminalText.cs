using System.Globalization;
using System.Text;

namespace Erpdert.Mer;

public static class TerminalText
{
    public static string Safe(string text)
    {
        var output = new StringBuilder(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            if (rune.Value is '\r' or '\n' or '\t')
                output.Append(rune.Value switch { '\r' => "\\r", '\n' => "\\n", _ => "\\t" });
            else if (category is UnicodeCategory.Control or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator ||
                category == UnicodeCategory.Format && rune.Value is not (0x200C or 0x200D))
                output.Append(rune.Value <= 0xFFFF ? $"\\u{rune.Value:X4}" : $"\\U{rune.Value:X8}");
            else output.Append(rune.ToString());
        }
        return output.ToString();
    }

    public static int Width(string text) => Elements(text).Sum(element => element.Width);

    public static IEnumerable<string> Wrap(string text, int columns)
    {
        if (columns < 1) yield break;
        var elements = Elements(text).ToArray();
        for (var start = 0; start < elements.Length;)
        {
            var end = start;
            var cells = 0;
            var space = -1;
            while (end < elements.Length && cells + elements[end].Width <= columns)
            {
                if (elements[end].Text == " ") space = end;
                cells += elements[end++].Width;
            }
            if (end == start) { yield return "…"; start++; continue; }
            if (end < elements.Length && space > start && elements[end].Text != " ") end = space;
            yield return string.Concat(elements[start..end].Select(element => element.Text)).TrimEnd(' ');
            start = end;
            while (start < elements.Length && elements[start].Text == " ") start++;
        }
    }

    public static string Clip(string text, int columns)
    {
        if (columns <= 0) return "";
        var elements = Elements(text).ToArray();
        if (elements.Sum(element => element.Width) <= columns) return string.Concat(elements.Select(element => element.Text));
        var output = new StringBuilder();
        var used = 0;
        foreach (var element in elements)
        {
            if (used + element.Width > columns - 1) break;
            output.Append(element.Text);
            used += element.Width;
        }
        return output.Append('…').ToString();
    }

    // A display policy, not a source-width or Unicode conformance claim:
    // grapheme clusters stay intact; common CJK/emoji occupy two cells;
    // ambiguous-width symbols use one cell on our Windows-first surface.
    internal static IEnumerable<(string Text, int Width)> Elements(string text)
    {
        var enumerator = StringInfo.GetTextElementEnumerator(Safe(text));
        while (enumerator.MoveNext())
        {
            var element = enumerator.GetTextElement();
            var runes = element.EnumerateRunes().ToArray();
            var first = runes[0];
            if (Rune.GetUnicodeCategory(first) is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark || first.Value is 0x200C or 0x200D)
            {
                yield return ("◌" + element, 1);
                continue;
            }
            var wide = runes.Any(rune => Wide(rune.Value)) || element.Contains('\uFE0F') || element.Contains('\u20E3');
            yield return (element, wide ? 2 : 1);
        }
    }

    private static bool Wide(int value) => value is
        >= 0x1100 and <= 0x115F or 0x2329 or 0x232A or
        >= 0x2E80 and <= 0xA4CF and not 0x303F or
        >= 0xAC00 and <= 0xD7A3 or >= 0xF900 and <= 0xFAFF or
        >= 0xFE10 and <= 0xFE19 or >= 0xFE30 and <= 0xFE6F or
        >= 0xFF01 and <= 0xFF60 or >= 0xFFE0 and <= 0xFFE6 or
        >= 0x1F000 and <= 0x1FAFF or >= 0x20000 and <= 0x3FFFD;
}
