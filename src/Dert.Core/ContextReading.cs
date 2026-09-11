namespace Dert.Core;

public enum ContextReader { Written, How, Similarities }
public enum ContextIssueKind { MissingClosing, EmptyItem, OpenQuote, MissingContent }
public sealed record ContextItem(int Number, TextField Value, TextSpan SourceSpan, TextSpan? Separator,
    int LineNumber, TextField? Annotation);
public sealed record ContextIssue(ContextIssueKind Kind, string Message, TextSpan Span);

/// <summary>A named inspection over retained source fields; never an operation binding.</summary>
public sealed record ContextReading(DertDocument Document, DertNode Owner, Header RawHeader, ContextReader Reader,
    IReadOnlyList<ContextItem> Items, IReadOnlyList<ContextIssue> Issues)
{
    public string Summary
    {
        get
        {
            if (Reader == ContextReader.How) return "HOW / whole explanation";
            if (Reader != ContextReader.Similarities) return "Written context / original fields";
            var count = Items.Count(item => item.Value.Text.Length > 0);
            var empty = Items.Count - count;
            return $"similarities / {count} written {(count == 1 ? "item" : "items")}" +
                (empty > 0 ? $" / {empty} empty {(empty == 1 ? "place" : "places")}" : "") +
                (Issues.Any(issue => issue.Kind == ContextIssueKind.OpenQuote) ? " / quote still open" : "");
        }
    }

    public static ContextReading? Read(DertDocument document, DertNode owner) => Read(new DertStructureIndex(document), owner);

    public static ContextReading? Read(DertStructureIndex index, DertNode owner)
    {
        var document = index.Document;
        var header = owner switch { ReasoningNoteNode note => note.Header, ReasoningBlockNode block => block.Header, _ => null };
        if (header is null || !index.Contains(owner)) return null;
        var reader = header.Head.Text.Equals("similarities", StringComparison.OrdinalIgnoreCase) ? ContextReader.Similarities
            : owner is ReasoningNoteNode && header.Head.Text.Equals("HOW", StringComparison.OrdinalIgnoreCase) ? ContextReader.How : ContextReader.Written;
        var items = new List<ContextItem>();
        var issues = new List<ContextIssue>();
        if (DertStructureIndex.Closing(owner) is null)
            issues.Add(new(ContextIssueKind.MissingClosing, "The note still needs its closing //.", new(owner.Span.End, 0)));
        if (owner is ReasoningBlockNode nested)
        {
            foreach (var entry in nested.Entries)
            {
                TextSpan? separator = null;
                if (entry.HasComma)
                {
                    var comma = entry.Value.Span.End;
                    while (comma < document.Source.Lines[entry.LineNumber - 1].ContentEnd && char.IsWhiteSpace(document.Source.Text[comma])) comma++;
                    if (comma < document.Source.Text.Length && document.Source.Text[comma] == ',') separator = new(comma, 1);
                }
                var value = entry.Value;
                // Preserve the parser's fields on the owner. The reading keeps
                // punctuation inside an unfinished quote with those words.
                if (reader == ContextReader.Similarities && separator is { } trailing &&
                    HeaderReader.HasUnclosedQuote(value.Text, 0, value.Text.Length))
                {
                    value = HeaderReader.Field(document.Source, value.Span.Start, trailing.End);
                    separator = null;
                }
                items.Add(new(items.Count + 1, value, value.Span, separator, entry.LineNumber, entry.Annotation));
                if (reader == ContextReader.Similarities) CheckValue(value, items.Count);
            }
        }
        else if (reader == ContextReader.Similarities && header.Payload is { Text.Length: > 0 } payload)
        {
            foreach (var part in SourceFieldParts.SplitCommas(document.Source, payload))
            {
                items.Add(new(items.Count + 1, part.Value, part.SourceSpan, part.Separator, owner.LineNumber, null));
                CheckValue(part.Value, items.Count);
            }
        }
        if (reader == ContextReader.Similarities && items.Count == 0)
            issues.Add(new(ContextIssueKind.MissingContent, "No items have been written yet.", header.Payload?.Span ?? header.Head.Span));
        if (reader == ContextReader.How && header.Payload is { } prose)
        {
            if (prose.Text.Length == 0) issues.Add(new(ContextIssueKind.MissingContent, "No explanation has been written yet.", prose.Span));
            if (HeaderReader.HasUnclosedQuote(prose.Text, 0, prose.Text.Length))
                issues.Add(new(ContextIssueKind.OpenQuote, "A quote in the explanation is still open; its words are retained.", prose.Span));
        }
        return new(document, owner, header, reader, items.AsReadOnly(), issues.AsReadOnly());

        void CheckValue(TextField value, int number)
        {
            if (value.Text.Length == 0)
                issues.Add(new(ContextIssueKind.EmptyItem, $"Item {number} has not been written yet; its place is retained.", value.Span));
            if (HeaderReader.HasUnclosedQuote(value.Text, 0, value.Text.Length))
                issues.Add(new(ContextIssueKind.OpenQuote, $"Item {number} has a quote still open; its remaining text stays together.", value.Span));
        }
    }
}
