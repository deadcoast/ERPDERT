using Dert.Text;

namespace Dert.Core;

public static class DertParser
{
    public static DertDocument Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new Parser(text).Parse();
    }

    private sealed class Parser(string text)
    {
        private readonly SourceText source = new(text);
        private readonly List<DertNode> nodes = [];
        private readonly List<Diagnostic> diagnostics = [];
        private readonly List<Highlight> highlights = [];
        private readonly List<FoldRegion> folds = [];
        private List<DertNode>? chainChildren;
        private Header? chainHeader;
        private SourceLine? chainStart;
        private Header? blockHeader;
        private SourceLine? blockStart;
        private readonly List<NoteEntryNode> entries = [];
        private StatementNode? pendingStatement;
        private readonly List<NoteEntryNode> pendingValues = [];
        private int pendingEnd;
        private List<DertNode>? pendingContainer;
        private int pendingIndex;
        private NoteNode? title;

        public DertDocument Parse()
        {
            foreach (var line in source.Lines)
            {
                var start = line.Start == 0 && text.StartsWith('\uFEFF') ? 1 : line.Start;
                var field = HeaderReader.Field(source, start, line.ContentEnd);
                if (field.Text.Length == 0) continue;
                if (blockStart is not null)
                {
                    if (field.Text == "//")
                    {
                        EndBlock(line.End, new(field.Span.Start, 2), line.Number);
                        continue;
                    }
                    if (field.Text.StartsWith("//", StringComparison.Ordinal))
                    {
                        Paint(new(field.Span.Start, 2), SemanticRole.Reasoning);
                        entries.Add(ReadEntry(line, field.Span.Start + 2));
                        continue;
                    }
                    EndBlock(line.Start, null, line.Number - 1);
                }

                if (title is not null && line.Number == title.LineNumber + 1 && IsRule(field.Text))
                {
                    Add(new RuleNode(line.Span, line.Number));
                    Paint(field.Span, SemanticRole.Rule);
                    continue;
                }
                if (field.Text.StartsWith("//", StringComparison.Ordinal))
                {
                    ReadNote(line, field);
                    continue;
                }

                var annotation = HeaderReader.FindAnnotation(text, field.Span.Start, field.Span.End);
                var contentEnd = annotation < 0 ? field.Span.End : annotation;
                var header = HeaderReader.Read(source, field.Span.Start, contentEnd);
                if (header.Head.Text == "STERT")
                {
                    EndPayload();
                    EndChain(line.Start, null);
                    chainHeader = header;
                    chainStart = line;
                    chainChildren = [];
                    PaintHeader(header);
                    Paint(header.Head.Span, SemanticRole.Boundary);
                }
                else if (header.Head.Text is "ƒin." or "fin." && header.Hook is null && header.Line is null)
                {
                    EndPayload();
                    Paint(header.Head.Span, SemanticRole.Boundary);
                    if (chainStart is null)
                        Add(new TextNode(line.Span, line.Number, field));
                    else
                        EndChain(line.End, header.Head.Span);
                }
                else if (header.Hook is not null || header.Line is not null)
                {
                    EndPayload();
                    var statement = new StatementNode(line.Span, line.Number, header, []);
                    Add(statement);
                    if (header.Line is not null && header.Payload!.Text.Length == 0)
                    {
                        pendingStatement = statement;
                        pendingEnd = line.End;
                        pendingContainer = chainChildren ?? nodes;
                        pendingIndex = pendingContainer.Count - 1;
                    }
                    PaintHeader(header);
                }
                else if (pendingStatement is not null)
                {
                    var value = ReadEntry(line, field.Span.Start, reasoning: false);
                    pendingValues.Add(value);
                    pendingEnd = line.End;
                    if (!value.HasComma) EndPayload();
                }
                else
                {
                    Add(new TextNode(line.Span, line.Number, field));
                }
                if (annotation >= 0)
                    Paint(new(annotation, line.ContentEnd - annotation), SemanticRole.ContextNote);
            }
            if (blockStart is not null) EndBlock(text.Length, null, source.Lines.Count);
            EndPayload();
            EndChain(text.Length, null);
            ValidateTitle();
            return new(source, DertLexer.Lex(text), nodes.ToArray(), diagnostics.ToArray(),
                highlights.ToArray(), folds.ToArray());
        }

        private void ReadNote(SourceLine line, TextField field)
        {
            var start = field.Span.Start + 2;
            var end = field.Span.End;
            var closed = HeaderReader.HasClosingMarker(text, start, end);
            var header = HeaderReader.Read(source, start, closed ? end - 2 : end);
            if (header.Hook is not null && header.Line is not null)
            {
                Paint(field.Span, SemanticRole.Reasoning);
                PaintHeader(header);
                if (!closed && header.Payload!.Text.Length == 0)
                {
                    blockStart = line;
                    blockHeader = header;
                    entries.Clear();
                    return;
                }
                TextSpan? closer = closed ? new(end - 2, 2) : null;
                Add(new ReasoningNoteNode(line.Span, line.Number, header, closer));
                folds.Add(new(line.Span, line.Number, line.Number, Label(header), 0, closed));
                if (!closed)
                    diagnostics.Add(new("DERT1002", DiagnosticSeverity.Error,
                        "This reasoning note needs its closing //.", field.Span, line.Number));
                return;
            }
            var content = HeaderReader.Field(source, start, end);
            var note = new NoteNode(line.Span, line.Number, content,
                nodes.Count == 0 && chainStart is null && content.Text.Length > 0);
            if (note.IsTitle) title = note;
            Add(note);
            Paint(field.Span, note.IsTitle ? SemanticRole.Title : SemanticRole.ContextNote);
        }

        private NoteEntryNode ReadEntry(SourceLine line, int start, bool reasoning = true)
        {
            var annotation = HeaderReader.FindAnnotation(text, start, line.ContentEnd);
            var value = HeaderReader.Field(source, start, annotation < 0 ? line.ContentEnd : annotation);
            var hasComma = value.Text.EndsWith(',');
            if (hasComma) value = HeaderReader.Field(source, value.Span.Start, value.Span.End - 1);
            var note = annotation < 0 ? null : HeaderReader.Field(source, annotation + 2, line.ContentEnd);
            Paint(new(start, line.ContentEnd - start), reasoning ? SemanticRole.Reasoning : SemanticRole.Text);
            Paint(value.Span, SemanticRole.Sinker);
            if (annotation >= 0)
                Paint(new(annotation, line.ContentEnd - annotation), SemanticRole.ContextNote);
            return new(line.Span, line.Number, value, hasComma, note);
        }

        private void EndBlock(int end, TextSpan? closing, int endLine)
        {
            var span = new TextSpan(blockStart!.Start, end - blockStart.Start);
            Add(new ReasoningBlockNode(span, blockStart.Number, blockHeader!, entries.ToArray(), closing));
            folds.Add(new(span, blockStart.Number, endLine, Label(blockHeader!), entries.Count, closing is not null));
            if (closing is TextSpan closer) Paint(closer, SemanticRole.Reasoning);
            else diagnostics.Add(new("DERT1001", DiagnosticSeverity.Error,
                "This reasoning block needs a standalone // to close it. Its entries are still preserved.",
                blockStart.ContentSpan, blockStart.Number));
            blockStart = null;
            blockHeader = null;
            entries.Clear();
        }

        private void EndChain(int end, TextSpan? closing)
        {
            if (chainStart is null) return;
            nodes.Add(new ChainNode(new(chainStart.Start, end - chainStart.Start), chainStart.Number,
                chainHeader!, chainChildren!.ToArray(), closing));
            chainStart = null;
            chainHeader = null;
            chainChildren = null;
        }

        private void EndPayload()
        {
            if (pendingStatement is { } statement)
            {
                pendingContainer![pendingIndex] = statement with
                {
                    Span = new(statement.Span.Start, pendingEnd - statement.Span.Start),
                    Values = pendingValues.ToArray()
                };
                if (pendingValues.Count > 0 && pendingValues[^1].HasComma)
                {
                    var last = pendingValues[^1];
                    diagnostics.Add(new("DERT3003", DiagnosticSeverity.Notice,
                        "This comma leaves room for another item. Add an item or choose to finish the list; nothing was removed.",
                        last.Value.Span, last.LineNumber));
                }
            }
            pendingValues.Clear();
            pendingStatement = null;
            pendingContainer = null;
        }

        private static bool IsRule(string text)
        {
            var marks = text.StartsWith("//", StringComparison.Ordinal) ? text[2..].Trim() : text;
            return marks.Length > 0 && marks.All(c => c == '-');
        }

        private void ValidateTitle()
        {
            if (title is null) return;
            var next = source.Lines.ElementAtOrDefault(title.LineNumber);
            var rule = next is null ? "" : source.Slice(next.ContentSpan).Trim();
            var hasRule = IsRule(rule);
            if (!hasRule || !rule.StartsWith("//", StringComparison.Ordinal))
            {
                diagnostics.Add(new("DERT2003", DiagnosticSeverity.Warning,
                    "The title needs its matching // underline directly below it.",
                    hasRule ? next!.ContentSpan : title.Span, hasRule ? next!.Number : title.LineNumber));
                return;
            }
            if (!DisplayCells.TryMeasureTitle(title.Content.Text, out var width, out var reason))
            {
                diagnostics.Add(new("DERT3004", DiagnosticSeverity.Notice,
                    reason,
                    title.Content.Span, title.LineNumber));
                return;
            }
            if (rule[2..].Trim().Length != width)
                diagnostics.Add(new("DERT2003", DiagnosticSeverity.Warning,
                    $"The title underline needs {width} marks to match the title.",
                    next!.ContentSpan, next.Number));
        }

        private void Add(DertNode node) => (chainChildren ?? nodes).Add(node);
        private static string Label(Header header) => $"{header.Head.Text} ; {header.Target?.Text}";
        private void Paint(TextSpan span, SemanticRole role)
        {
            if (span.Length > 0) highlights.Add(new(span, role));
        }
        private void PaintHeader(Header header)
        {
            Paint(header.Head.Span, SemanticRole.Head);
            if (header.Target is TextField target) Paint(target.Span, SemanticRole.Target);
            if (header.Payload is TextField payload) Paint(payload.Span, SemanticRole.Sinker);
            if (header.Hook is TextSpan hook) Paint(hook, SemanticRole.Hook);
            if (header.Line is TextSpan line) Paint(line, SemanticRole.Line);
        }
    }
}
