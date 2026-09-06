namespace Dert.Core;

public readonly record struct TextSpan(int Start, int Length)
{
    public int End => Start + Length;
    public bool Contains(int offset) => offset >= Start && offset < End;
}

public sealed record TextField(string Text, TextSpan Span);
public sealed record Header(TextField Head, TextField? Target, TextField? Payload,
    TextSpan? Hook, TextSpan? Line);

public abstract record DertNode(TextSpan Span, int LineNumber);
public sealed record ChainNode(TextSpan Span, int LineNumber, Header Header,
    IReadOnlyList<DertNode> Children, TextSpan? Terminator) : DertNode(Span, LineNumber);
public sealed record NoteNode(TextSpan Span, int LineNumber, TextField Content,
    bool IsTitle = false) : DertNode(Span, LineNumber);
public sealed record ReasoningNoteNode(TextSpan Span, int LineNumber, Header Header,
    TextSpan? Closing) : DertNode(Span, LineNumber);
public sealed record ReasoningBlockNode(TextSpan Span, int LineNumber, Header Header,
    IReadOnlyList<NoteEntryNode> Entries, TextSpan? Closing) : DertNode(Span, LineNumber);
public sealed record NoteEntryNode(TextSpan Span, int LineNumber, TextField Value,
    bool HasComma, TextField? Annotation) : DertNode(Span, LineNumber);
public sealed record StatementNode(TextSpan Span, int LineNumber, Header Header,
    IReadOnlyList<NoteEntryNode> Values) : DertNode(Span, LineNumber);
public sealed record TextNode(TextSpan Span, int LineNumber, TextField Content) : DertNode(Span, LineNumber);
public sealed record RuleNode(TextSpan Span, int LineNumber) : DertNode(Span, LineNumber);

public enum TokenKind { Text, Whitespace, NewLine, Slashes, Hook, Line, Comma, Bom }
public sealed record LexToken(TokenKind Kind, TextSpan Span);
public enum DiagnosticSeverity { Notice, Warning, Error }
public sealed record Diagnostic(string Code, DiagnosticSeverity Severity, string Message,
    TextSpan Span, int LineNumber);
public enum SemanticRole { Text, ContextNote, Reasoning, Head, Target, Hook, Line, Sinker, Boundary, Title, Rule }
public sealed record Highlight(TextSpan Span, SemanticRole Role);
public sealed record FoldRegion(TextSpan Span, int StartLine, int EndLine, string Label, int EntryCount,
    bool IsClosed);
public sealed record DertDocument(SourceText Source, IReadOnlyList<LexToken> Tokens,
    IReadOnlyList<DertNode> Nodes, IReadOnlyList<Diagnostic> Diagnostics,
    IReadOnlyList<Highlight> Highlights, IReadOnlyList<FoldRegion> Folds);
