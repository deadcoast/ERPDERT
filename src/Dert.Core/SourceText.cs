namespace Dert.Core;

public sealed class SourceText(string text)
{
    public string Text { get; } = text;
    public IReadOnlyList<SourceLine> Lines { get; } = ReadLines(text);
    public string Slice(TextSpan span) => Text.Substring(span.Start, span.Length);

    private static SourceLine[] ReadLines(string text)
    {
        var lines = new List<SourceLine>();
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] is not ('\r' or '\n')) continue;
            var contentEnd = i;
            if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
            lines.Add(new(lines.Count + 1, start, contentEnd, i + 1));
            start = i + 1;
        }
        lines.Add(new(lines.Count + 1, start, text.Length, text.Length));
        return lines.ToArray();
    }
}

public sealed record SourceLine(int Number, int Start, int ContentEnd, int End)
{
    public TextSpan Span => new(Start, End - Start);
    public TextSpan ContentSpan => new(Start, ContentEnd - Start);
}
