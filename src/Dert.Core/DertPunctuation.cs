namespace Dert.Core;

/// <summary>Visible punctuation meaning from a parsed source location; no binding or decoding.</summary>
public static class DertPunctuation
{
    public static string Reading(DertStructureIndex index, int line)
    {
        var at = index.AtLine(line);
        return at?.Node switch
        {
            NoteEntryNode entry when at.Parent is StatementNode =>
                (entry.HasComma ? "The final comma continues this list." : "This item ends this list.") +
                (entry.Annotation is null ? " No annotation is separated." : " Its // annotation stays separate."),
            NoteEntryNode entry => "This entry stays in its reasoning block." +
                (entry.HasComma ? " Its comma is retained." : "") + (entry.Annotation is null ? "" : " Its // annotation stays separate."),
            ReasoningNoteNode note => note.Closing is null ? "The reason still needs its closing //." : "The final // closes this reason.",
            _ => "These words remain source text."
        };
    }

    public static string? Detail(DertNode node)
    {
        var text = node switch
        {
            NoteEntryNode entry => entry.Value.Text,
            ReasoningNoteNode note => note.Header.Payload?.Text,
            TextNode plain => BeforeAnnotation(plain.Content.Text),
            _ => null
        };
        if (text is null) return null;
        if (HeaderReader.HasUnclosedQuote(text, 0, text.Length))
            return "A quote is still open / literal convenience waits for its ending.";
        var quoted = Enumerable.Range(0, text.Length).Any(i => text[i] == '"' && !HeaderReader.IsEscaped(text, i));
        if (quoted) return "Quoted spans keep their punctuation as text. Quote marks remain in source." +
            (text.Contains('\\') ? " Backslashes stay exactly as written." : "");
        return text.Contains('\\') ? "Backslashes stay exactly as written; this value needs no path escaping." : null;
    }

    private static string BeforeAnnotation(string text)
    {
        var annotation = HeaderReader.FindAnnotation(text, 0, text.Length);
        return annotation < 0 ? text : text[..annotation];
    }
}
