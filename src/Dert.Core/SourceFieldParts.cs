namespace Dert.Core;

internal readonly record struct SourceFieldPart(TextField Value, TextSpan SourceSpan, TextSpan? Separator);

internal static class SourceFieldParts
{
    internal static IReadOnlyList<SourceFieldPart> SplitCommas(SourceText source, TextField field)
    {
        var parts = new List<SourceFieldPart>();
        var start = 0;
        var quoted = false;
        for (var i = 0; i < field.Text.Length; i++)
        {
            if (field.Text[i] == '"' && !HeaderReader.IsEscaped(field.Text, i)) quoted = !quoted;
            if (quoted || field.Text[i] != ',') continue;
            Add(i, new(field.Span.Start + i, 1));
            start = i + 1;
        }
        Add(field.Text.Length, null);
        return parts.AsReadOnly();

        void Add(int end, TextSpan? separator)
        {
            var raw = new TextSpan(field.Span.Start + start, end - start);
            parts.Add(new(HeaderReader.Field(source, raw.Start, raw.End), raw, separator));
        }
    }
}
