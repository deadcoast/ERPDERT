using System.Globalization;
using System.Text;
using Dert.Core;
using Spectre.Console;

namespace Erpdert.Cli;

internal sealed partial class DocumentRenderer(IAnsiConsole console, bool plain, string[]? invocation = null)
{
    public void Help()
    {
        Banner("TOOLS + NOTES / WORKBENCH");
        Line("ERPDERT codex", SemanticRole.Sinker);
        Line("    Discover Windows command routes, package evidence, and competing installations. Read-only.", SemanticRole.ContextNote);
        Line("ERPDERT run my-tools.DERT", SemanticRole.Sinker);
        Line("    Let STERT ; ERPDERT ;; codex request discovery from your DERT, with its notes alongside the result.", SemanticRole.ContextNote);
        Line("ERPDERT run my-tools.DERT --preview", SemanticRole.Sinker);
        Line("    See what your DERT requests before searching. Your file is not changed.", SemanticRole.ContextNote);
        Line("The published folder includes examples/my-tools.DERT, ready to preview and run.", SemanticRole.ContextNote);
        Line("");
        Line("Your notes already have structure. See what belongs where.", SemanticRole.Text);
        Line("");
        Line("ERPDERT inspect RERDMERR.DERT", SemanticRole.Sinker);
        Line("    Read the document with reasoning folded into handles.", SemanticRole.ContextNote);
        Line("ERPDERT inspect RERDMERR.DERT --at 37", SemanticRole.Sinker);
        Line("    Inspect an entry, its explanation, and the note it belongs to.", SemanticRole.ContextNote);
        Line("ERPDERT inspect RERDMERR.DERT --expand", SemanticRole.Sinker);
        Line("    Inspect every source line, preserving the written syntax.", SemanticRole.ContextNote);
        Line("ERPDERT lint RERDMERR.DERT", SemanticRole.Sinker);
        Line("    Find structural problems with source-cited explanations and copyable supported rehearsals.", SemanticRole.ContextNote);
        Line("ERPDERT repair 'my notes.DERT'", SemanticRole.Sinker);
        Line("    Rehearse an exact title-rule edit. Apply only when you choose; keep an undo copy.", SemanticRole.ContextNote);
        Line("ERPDERT repair 'my notes.DERT' --at 12", SemanticRole.Sinker);
        Line("    Choose where an unfinished reasoning block ends. See what stays inside and what changes ownership.", SemanticRole.ContextNote);
        Line("ERPDERT repair 'my notes.DERT' --at 12 --close-before 16", SemanticRole.Sinker);
        Line("    Rehearse one offered boundary; use eof only when offered. Selection is not approval.", SemanticRole.ContextNote);
        Line("ERPDERT undo 'my notes.DERT'", SemanticRole.Sinker);
        Line("    Rehearse restoring the original bytes from the latest title or note-boundary repair.", SemanticRole.ContextNote);
        Line("");
        Line("--plain removes color. inspect and lint never write your source.", SemanticRole.ContextNote);
        Line("Lint's --from-lint commands check the same source bytes and open previews without write prompts. They cannot be combined with --apply.", SemanticRole.ContextNote);
        Line("repair and undo wait for confirmation. Redirected output is preview-only unless you supply --apply <token> for a selected edit.", SemanticRole.ContextNote);
        Line("Title repair: ASCII width, UTF-8 documents, regular local Windows files. No DERT operations are executed.", SemanticRole.ContextNote);
        Line("Metadata interpretation and package operations are not implemented.", SemanticRole.ContextNote);
        Line("ƒin.", SemanticRole.Boundary);
    }

    public void Inspect(DertDocument document, CliOptions options)
    {
        Banner(options.At is null ? "SERNTERX / INSPECT" : $"SERNTERX / CONNECTION · LINE {options.At}");
        Line(options.Path, SemanticRole.ContextNote);
        var all = NodeWalk.All(document.Nodes).ToArray();
        Line($"{Count(all.Count(n => n.Node is ChainNode), "chain")} · " +
            $"{Count(all.Count(n => n.Node is ReasoningNoteNode), "closed-form reasoning note")} · " +
            $"{Count(all.Count(n => n.Node is ReasoningBlockNode), "reasoning block")} · " +
            Count(all.Count(n => n.Node is NoteEntryNode), "entry", "entries"), SemanticRole.Target);
        Line("");

        var focused = options.At is int at ? NodeWalk.At(document, at) : null;
        var scope = focused?.Parent is ReasoningBlockNode or StatementNode ? focused.Parent : focused?.Node;
        var folds = document.Folds.ToDictionary(f => f.StartLine);
        var skipThrough = 0;
        foreach (var sourceLine in document.Source.Lines)
        {
            if (sourceLine.Number <= skipThrough) continue;
            if (scope is not null && (sourceLine.End <= scope.Span.Start || sourceLine.Start >= scope.Span.End)) continue;
            if (!options.Expand && options.At is null && folds.TryGetValue(sourceLine.Number, out var fold))
            {
                var extent = fold.EndLine == fold.StartLine ? $"{fold.StartLine}" : $"{fold.StartLine}–{fold.EndLine}";
                var status = fold.EntryCount > 0 ? Count(fold.EntryCount, "entry", "entries") : "reason";
                if (!fold.IsClosed) status += " · missing //";
                var paragraph = new Paragraph()
                    .Append($"{extent,7} │ ", Style(SemanticRole.ContextNote))
                    .Append("// ", Style(SemanticRole.Reasoning))
                    .Append(Safe(fold.Label), Style(SemanticRole.Head))
                    .Append($"  ⟪ {status} ⟫", Style(fold.IsClosed ? SemanticRole.Reasoning : SemanticRole.Hook));
                console.Write(paragraph);
                console.WriteLine();
                skipThrough = fold.EndLine;
            }
            else
                SourceLine(document, sourceLine, options.At == sourceLine.Number);
        }
        if (focused is not null) Ownership(document, focused);
        else if (options.At is not null)
            Line("This line is spacing between structures; it has no entry value.", SemanticRole.ContextNote);
        Line("");
        Summary(document);
        if (document.Diagnostics.Count > 0)
            Line("Use ERPDERT lint with this file for source-cited explanations.", SemanticRole.ContextNote);
    }

    public void Failure(string message)
    {
        Banner("COULD NOT CONTINUE");
        Line(message, SemanticRole.Target);
        Line("ERPDERT -help shows the available commands.", SemanticRole.ContextNote);
    }

    private void SourceLine(DertDocument document, SourceLine line, bool selected)
    {
        var paragraph = new Paragraph();
        var position = line.Start;
        if (position == 0 && document.Source.Text.StartsWith('\uFEFF')) position++;
        var relevant = document.Highlights.Where(h =>
            h.Span.Start < line.ContentEnd && h.Span.End > position).ToArray();
        while (position < line.ContentEnd)
        {
            var role = relevant.LastOrDefault(h => h.Span.Contains(position))?.Role ?? SemanticRole.Text;
            var end = position + 1;
            while (end < line.ContentEnd &&
                (relevant.LastOrDefault(h => h.Span.Contains(end))?.Role ?? SemanticRole.Text) == role) end++;
            paragraph.Append(Safe(document.Source.Text[position..end]), Style(role));
            position = end;
        }
        var grid = new Grid().AddColumn(new GridColumn().Width(9).NoWrap().PadRight(1))
            .AddColumn(new GridColumn());
        grid.AddRow(new Text($"{(selected ? ">" : " ")}{line.Number,6} │",
            Style(selected ? SemanticRole.Hook : SemanticRole.ContextNote)), paragraph);
        console.Write(grid);
    }

    private void Ownership(DertDocument document, NodeLocation focused)
    {
        Line("");
        Line("BELONGS HERE", SemanticRole.Hook);
        var owner = focused.Parent switch
        {
            ReasoningBlockNode block => $"{block.Header.Head.Text} ; {block.Header.Target?.Text}",
            StatementNode statement => $"{statement.Header.Head.Text} ; {statement.Header.Target?.Text}",
            ChainNode chain => $"chain beginning on line {chain.LineNumber}",
            _ => "document"
        };
        Line($"Belongs to  {owner}", SemanticRole.Target);
        switch (focused.Node)
        {
            case NoteEntryNode entry:
                Line($"Value       {entry.Value.Text}", SemanticRole.Sinker);
                if (entry.Annotation is not null) Line($"Context     {entry.Annotation.Text}");
                Line(entry.HasComma ? "Comma       present in the source; preserved" : "Comma       absent", SemanticRole.ContextNote);
                break;
            case ReasoningNoteNode note:
                Fields(note.Header);
                Line(note.Closing is null ? "Closure     missing //" : "Closure     // on this line", SemanticRole.Reasoning);
                break;
            case ReasoningBlockNode block:
                Fields(block.Header);
                Line($"Entries     {block.Entries.Count}", SemanticRole.Sinker);
                break;
            case StatementNode statement:
                Fields(statement.Header);
                Line("Operation   source structure only; no command was executed", SemanticRole.ContextNote);
                break;
            case NoteNode note:
                Line($"Context     {note.Content.Text}");
                break;
        }
        if (focused.Parent is ReasoningBlockNode parent)
        {
            var closingLine = parent.Closing is TextSpan closer
                ? document.Source.Lines.First(l => closer.Start >= l.Start && closer.Start < l.End).Number
                : (int?)null;
            Line(closingLine is int number
                ? $"Boundary    standalone // on line {number} closes this reasoning note"
                : "Boundary    this reasoning note is missing its standalone //", SemanticRole.Reasoning);
        }
    }

    private void Fields(Header header)
    {
        Line($"Content     {header.Head.Text}", SemanticRole.Head);
        if (header.Target is not null) Line($"Target      {header.Target.Text}", SemanticRole.Target);
        if (header.Payload is { Text.Length: > 0 }) Line($"Payload     {header.Payload.Text}", SemanticRole.Sinker);
    }

    private void Summary(DertDocument document)
    {
        var errors = document.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
        var warnings = document.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);
        var notices = document.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Notice);
        Line($"{Count(errors, "error")} · {Count(warnings, "warning")} · {Count(notices, "note")}",
            errors == 0 ? SemanticRole.Target : SemanticRole.Hook);
        Line("Source preserved · supported structure checked · command meanings not validated", SemanticRole.ContextNote);
        Line("ƒin.", SemanticRole.Boundary);
    }

    private void Banner(string label)
    {
        Line("ERPDERT MER", SemanticRole.Title);
        Line(label, SemanticRole.Reasoning);
        Line(DateTimeOffset.Now.ToString("dd MMM yyyy · HH:mm:ss zzz", CultureInfo.InvariantCulture).ToUpperInvariant(),
            SemanticRole.ContextNote);
        Line(new string('─', Math.Clamp(console.Profile.Width, 20, 100)), SemanticRole.Rule);
    }

    private static string Count(int value, string singular, string? plural = null) =>
        $"{value} {(value == 1 ? singular : plural ?? singular + "s")}";

    private void Line(string text, SemanticRole role = SemanticRole.Text)
    {
        console.Write(new Text(Safe(text), Style(role)));
        console.WriteLine();
    }

    private Style Style(SemanticRole role)
    {
        if (plain) return Spectre.Console.Style.Plain;
        var color = role switch
        {
            SemanticRole.ContextNote or SemanticRole.Rule => new Color(143, 151, 163),
            SemanticRole.Reasoning or SemanticRole.Head => new Color(202, 158, 255),
            SemanticRole.Hook => new Color(255, 192, 94),
            SemanticRole.Line => new Color(111, 211, 245),
            SemanticRole.Target => new Color(239, 164, 151),
            SemanticRole.Boundary or SemanticRole.Title => new Color(247, 221, 164),
            _ => new Color(226, 231, 237)
        };
        return new(color, decoration: role is SemanticRole.Title or SemanticRole.Boundary ? Decoration.Bold : Decoration.None);
    }

    private static string Safe(string text)
    {
        var result = new StringBuilder();
        foreach (var rune in text.EnumerateRunes())
            if (Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control or UnicodeCategory.Format)
            {
                foreach (var codeUnit in rune.ToString()) result.Append($"\\u{(int)codeUnit:X4}");
            }
            else result.Append(rune.ToString());
        return result.ToString();
    }
}
