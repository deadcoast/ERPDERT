using System.Globalization;
using System.Text;

namespace Dert.Mer;

public static class TerminalText
{
    public static string Safe(string text) => Dert.Text.DisplayCells.Safe(text);

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

    internal static IEnumerable<(string Text, int Width)> Elements(string text) => Dert.Text.DisplayCells.Elements(text);
}
