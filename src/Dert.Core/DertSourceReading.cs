namespace Dert.Core;

public sealed record SourceFormReading(string Explanation, string Summary);

/// <summary>Explains captured structure without assigning an application binding or rewriting source.</summary>
public static class DertSourceReading
{
    public static SourceFormReading? Form(DertNode node) => node switch
    {
        ChainNode { Header.Hook: not null, Header.Line: null, Header.Target.Text: "" } =>
            new("The hook is empty; its spelling is retained while you write.", "Empty hook / source retained"),
        ChainNode chain when string.Equals(chain.Header.Target?.Text, "DERCS", StringComparison.OrdinalIgnoreCase) =>
            new("DERCS names the original help form. Its command fields are preserved.", "DERCS / original help form"),
        StatementNode { Header.Head.Text: "START" } =>
            new("Historical START / read as a statement; no chain opened.", "Historical START / statement"),
        TextNode text when BeforeAnnotation(text.Content.Text) == "START" =>
            new("Historical START / retained as text; no chain opened.", "Historical START / text"),
        TextNode text when BeforeAnnotation(text.Content.Text) is "fin." or "ƒin." =>
            new("No STERT owns this goodbye; it remains source text.", "Loose goodbye / source text"),
        _ => null
    };

    public static string ChainExtent(DertStructureIndex index, ChainNode chain)
    {
        if (chain.Terminator is { } closing) return $"Chain ends on line {index.LineOf(closing.Start)}";
        var next = index.Document.Nodes.OfType<ChainNode>().FirstOrDefault(candidate => candidate.Span.Start == chain.Span.End);
        return next is null ? "No ƒin. written / captured to end of document."
            : $"No ƒin. written / next STERT on line {next.LineNumber}.";
    }

    private static string BeforeAnnotation(string text)
    {
        var annotation = HeaderReader.FindAnnotation(text, 0, text.Length);
        return (annotation < 0 ? text : text[..annotation]).Trim();
    }
}
