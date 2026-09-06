namespace Dert.Core;

internal static class HeaderReader
{
    public static TextField Field(SourceText source, int start, int end)
    {
        while (start < end && char.IsWhiteSpace(source.Text[start])) start++;
        while (end > start && char.IsWhiteSpace(source.Text[end - 1])) end--;
        var span = new TextSpan(start, end - start);
        return new(source.Slice(span), span);
    }

    public static Header Read(SourceText source, int start, int end)
    {
        int? hook = null, line = null;
        var quoted = false;
        for (var i = start; i < end; i++)
        {
            if (source.Text[i] == '"' && !IsEscaped(source.Text, i))
                quoted = !quoted;
            if (quoted || source.Text[i] != ';') continue;
            if (i + 1 < end && source.Text[i + 1] == ';')
            {
                line = i;
                break;
            }
            hook ??= i;
        }
        return new(
            Field(source, start, hook ?? line ?? end),
            hook is int h ? Field(source, h + 1, line ?? end) : null,
            line is int l ? Field(source, l + 2, end) : null,
            hook is int hs ? new(hs, 1) : null,
            line is int ls ? new(ls, 2) : null);
    }

    public static int FindAnnotation(string text, int start, int end)
    {
        var quoted = false;
        for (var i = start; i + 1 < end; i++)
        {
            if (text[i] == '"' && !IsEscaped(text, i)) quoted = !quoted;
            if (!quoted && text[i] == '/' && text[i + 1] == '/' &&
                (i == start || char.IsWhiteSpace(text[i - 1]))) return i;
        }
        return -1;
    }

    public static bool HasClosingMarker(string text, int start, int end)
    {
        if (end - start < 3 || !text.AsSpan(end - 2, 2).SequenceEqual("//") ||
            !char.IsWhiteSpace(text[end - 3])) return false;
        var quoted = false;
        for (var i = start; i < end - 2; i++)
            if (text[i] == '"' && !IsEscaped(text, i)) quoted = !quoted;
        return !quoted;
    }

    private static bool IsEscaped(string text, int position)
    {
        var count = 0;
        for (var i = position - 1; i >= 0 && text[i] == '\\'; i--) count++;
        return count % 2 != 0;
    }
}
