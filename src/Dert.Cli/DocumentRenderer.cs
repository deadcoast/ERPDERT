using System.Globalization;
using System.Text;
using Dert.Core;
using Spectre.Console;
using ConsoleText = Spectre.Console.Text;

namespace Dert.Cli;

internal sealed partial class DocumentRenderer(IAnsiConsole console, bool plain, string[]? invocation = null, bool still = false)
{
    public void Help()
    {
        Banner("TOOLS + NOTES / WORKBENCH");
        Line("DERT demo", SemanticRole.Sinker);
        Line("    Try five offline cases. Repeat, inspect step by step, compare motion/still/plain, and keep a personal review card. No file writes or installs.", SemanticRole.ContextNote);
        Line("    --case title|context|unfinished|unicode|failure selects a case; --all runs without prompts; --step pauses at recorded evidence.", SemanticRole.ContextNote);
        Line("");
        Line("DERT write [file.DERT] [--still]", SemanticRole.Sinker);
        Line("    Write in the compact Windows notepad terminal. Titles align on Enter; source colors and reasoning folds follow your words. Ctrl+S saves; Ctrl+Z undoes buffer edits.", SemanticRole.ContextNote);
        Line("    Opened mismatched titles offer a preview. Save retains original encoding/newlines and checks for external changes; Save as creates a new copy.", SemanticRole.ContextNote);
        Line("    Select an expanded list to Add another item or Finish this list. Exact previews keep annotations and ownership; Apply edits the buffer, Ctrl+Z undoes, Ctrl+S saves.", SemanticRole.ContextNote);
        Line("    A chain-ending fin. appears as ƒin. in Notes; Copy and Save retain typed source. Select its line to show typed spelling or preview writing ƒ into the source.", SemanticRole.ContextNote);
        Line("    Titles keep accents, CJK and emoji. Their rule shares MER's cell count and follows the actual font width in Notes. Tabs and unsupported controls stay unchanged with an explanation.", SemanticRole.ContextNote);
        Line("    Inspect reasoning / Ctrl+Space reads similarities as written items and HOW as whole prose. Raw fields retains the original words; selecting an item edits that exact occurrence. Try examples/context-notes.DERT.", SemanticRole.ContextNote);
        Line("    References follows whole names to other reasoning notes in this document. Duplicate destinations stay separate; Escape returns to your starting place across visits. Try examples/referenced-notes.DERT.", SemanticRole.ContextNote);
        Line("    Context selection opens an exact copy/export review from the reasoning inspector. Include other notes deliberately; Left out shows excluded source. Source words, annotations, ranges and revision stay connected. Try examples/context-selection.DERT.", SemanticRole.ContextNote);
        Line("");
        Line("DERT guide [file.DERT] [--still]", SemanticRole.Sinker);
        Line("    Open the separate syntax, Help and troubleshooting workbench. The compact notes window remains available through DERT write.", SemanticRole.ContextNote);
        Line("");
        Line("DERT settings", SemanticRole.Sinker);
        Line("    Read the preferences shared by the Windows window and CLI, with their settings file path.", SemanticRole.ContextNote);
        Line("DERT settings windows-menu-fade on|off", SemanticRole.Sinker);
        Line("    Also accepts word-wrap, show-line-numbers and reduced-motion with on|off; font-size accepts 8 to 48. Preferences persist under LocalAppData/DERT/settings.json. Windows reloads them when opened or when its Settings menu is opened.", SemanticRole.ContextNote);
        Line("");
        Line("DERT context file.DERT --at <line> [--include <note line>] [--details | --text]", SemanticRole.Sinker);
        Line("    Preview one reasoning note with its own annotations. Other notes remain explicit choices; --include can be repeated. --details shows the full review; --text emits the exact text for deliberate copying/redirection.", SemanticRole.ContextNote);
        Line("    Export to a new .txt file with --output <path> --from-preview <review ID>. Changed source or selection refuses the old review. No source edit or agent upload occurs. Portable schemas remain deferred.", SemanticRole.ContextNote);
        Line("DERT run file.DERT --receipt new.json", SemanticRole.Sinker);
        Line("    Retain one real Codex discovery observation in a new local record. This capture currently supports one STERT ; update ;; codex request. Source remains unchanged.", SemanticRole.ContextNote);
        Line("DERT result record.json [--into notes.DERT] [--at N] [--details] [--compare earlier.json]", SemanticRole.Sinker);
        Line("    Review observed action, time, outcome, evidence and captured/current source. --compare shows differences between two saved observations without running an operation.", SemanticRole.ContextNote);
        Line("    --write opens the insertion review in Notes. Or use DERT write notes.DERT --result record.json. Apply adds a folded historical note; Undo and Save remain separate choices.", SemanticRole.ContextNote);
        Line("    In MER, probe then keep <new.json path> retains that already shown observation. Portable schemas and automatic agent delivery remain deferred.", SemanticRole.ContextNote);
        Line("");
        Line("DERT mer", SemanticRole.Sinker);
        Line("    Open the bundled note in MER's ASCII source window, with a short edge trace as recorded connections appear.", SemanticRole.ContextNote);
        Line("DERT mer 'my notes.DERT' [--replay] [--still] [--plain] [--details | --review]", SemanticRole.Sinker);
        Line("    Keep your numbered source beside its explanation. --replay is read-only; --still removes motion. Reading never changes your file.", SemanticRole.ContextNote);
        Line("    After analysis: next/prev follows connections, at <line> inspects source, details opens the full record, repair shows previews, Enter finishes.", SemanticRole.ContextNote);
        Line("    Notes: find <text> searches captured source literally; hit <number> selects a result; hits <page> lists matches. owner follows the actual parent; structure reveals fields and entries.", SemanticRole.ContextNote);
        Line("    refs shows named places in other notes; refs <page> pages them, ref <number> visits one and ref back returns. Name matches do not bind applications; details retains full names and roles.", SemanticRole.ContextNote);
        Line("    context reviews the selected reasoning note; context include/exclude <line> changes its note choices. context notes <page> lists choices; context left reads excluded source; context details offers the exact reviewed CLI export; context clear leaves the selection.", SemanticRole.ContextNote);
        Line("    examples/weekend.DERT is ready for notes-only review. Try find quiet, hit 2, then owner. Search and inspection never execute text or change your file.", SemanticRole.ContextNote);
        Line("    repair title previews the title rule. An unfinished note offers close <line|eof>. before/after compares; apply explicitly saves with verified undo storage; cancel leaves. undo previews the saved original.", SemanticRole.ContextNote);
        Line("    At a supported Codex discovery request, probe reads local launchers and package metadata. copy <number> compares recorded installations. No programs are started.", SemanticRole.ContextNote);
        Line("    At an updateme request, rehearse finds local copies; copy <number> chooses one. check starts npm inventory, registry and dependency dry-run queries, which may write npm cache/logs. No package update is applied.", SemanticRole.ContextNote);
        Line("    At a where request, probe connects application paths and folder sizes. doctor checks the selected Codex copy and saves an exact reinstall proposal; details offers its separate CLI apply command.", SemanticRole.ContextNote);
        Line("    --review accepts review commands even with redirected input, including explicit apply. Probing, update checks and repairs check the captured source revision.", SemanticRole.ContextNote);
        Line("    --details prints the full record and source-bound PowerShell previews directly, including when output is redirected.", SemanticRole.ContextNote);
        Line("");
        Line("DERT update codex", SemanticRole.Sinker);
        Line("    Discover Windows command routes, package evidence, and competing installations. Read-only.", SemanticRole.ContextNote);
        Line("DERT update codex updateme", SemanticRole.Sinker);
        Line("    Rehearse this copy's update and save its exact proposal. No package update is applied. The offered --path and --apply <token> command rechecks the proposal, installs the pinned version, and records verification.", SemanticRole.ContextNote);
        Line("    Multiple copies? Follow an offered --path command to select the exact one.", SemanticRole.ContextNote);
        Line("DERT where <application>", SemanticRole.Sinker);
        Line("DERT where codex --doctor [--path <launcher>]  (-dr also means --doctor)", SemanticRole.Sinker);
        Line("    Find command routes, installations and adjacent folders with their observed size. Doctor checks the selected copy through its update adapter and previews an exact reinstall.", SemanticRole.ContextNote);
        Line("    A doctor preview never uninstalls anything. A supported proposal offers --apply <token>; changed evidence refuses the old approval. Unsupported managers remain explained.", SemanticRole.ContextNote);
        Line("DERT run my-tools.DERT", SemanticRole.Sinker);
        Line("    Let STERT ; update ;; codex request discovery from your DERT, with its notes alongside the result.", SemanticRole.ContextNote);
        Line("DERT run my-tools.DERT --preview", SemanticRole.Sinker);
        Line("    See what your DERT requests before searching. Your file is not changed.", SemanticRole.ContextNote);
        Line("The published folder includes examples/my-tools.DERT, ready to preview and run.", SemanticRole.ContextNote);
        Line("Add commands ; updateme ;; inside that chain to request the same update rehearsal. examples/update-my-tools.DERT is ready to try.", SemanticRole.ContextNote);
        Line("STERT ; where ;; codex requests locations; commands ; doctor ;; adds a source-bound repair review. Try examples/where-my-tools.DERT.", SemanticRole.ContextNote);
        Line("");
        Line("Your notes already have structure. See what belongs where.", SemanticRole.Text);
        Line("");
        Line("DERT inspect RERDMERR.DERT", SemanticRole.Sinker);
        Line("    Read the document with reasoning folded into handles.", SemanticRole.ContextNote);
        Line("DERT inspect RERDMERR.DERT --at 37", SemanticRole.Sinker);
        Line("    Inspect an entry, its explanation, and the note it belongs to.", SemanticRole.ContextNote);
        Line("DERT inspect RERDMERR.DERT --expand", SemanticRole.Sinker);
        Line("    Inspect every source line, preserving the written syntax.", SemanticRole.ContextNote);
        Line("DERT inspect 'my notes.DERT' --at 12 --references", SemanticRole.Sinker);
        Line("    Show whole written name matches, exact occurrences and actual parents in this document. Case is ignored; quotes remain literal. No operation binding is inferred.", SemanticRole.ContextNote);
        Line("DERT lint RERDMERR.DERT", SemanticRole.Sinker);
        Line("    Find structural problems with source-cited explanations and copyable supported rehearsals.", SemanticRole.ContextNote);
        Line("DERT repair 'my notes.DERT'", SemanticRole.Sinker);
        Line("    Rehearse an exact title-rule edit. Apply only when you choose; keep an undo copy.", SemanticRole.ContextNote);
        Line("DERT repair 'my notes.DERT' --at 12", SemanticRole.Sinker);
        Line("    Choose where an unfinished reasoning block ends. See what stays inside and what changes ownership.", SemanticRole.ContextNote);
        Line("DERT repair 'my notes.DERT' --at 12 --close-before 16", SemanticRole.Sinker);
        Line("    Rehearse one offered boundary; use eof only when offered. Selection is not approval.", SemanticRole.ContextNote);
        Line("DERT undo 'my notes.DERT'", SemanticRole.Sinker);
        Line("    Rehearse restoring the original bytes from the latest title or note-boundary repair.", SemanticRole.ContextNote);
        Line("");
        Line("--plain removes color. inspect and lint never write your source.", SemanticRole.ContextNote);
        Line("--still removes motion from mer and update rehearsals (also through run). DERT_MOTION=off disables motion for this environment.", SemanticRole.ContextNote);
        Line("Lint's --from-lint commands check the same source bytes and open previews without write prompts. They cannot be combined with --apply.", SemanticRole.ContextNote);
        Line("repair and undo wait for confirmation. Redirected output is preview-only unless you supply --apply <token> for a selected edit.", SemanticRole.ContextNote);
        Line("Title repair: shared Unicode width, UTF-8 documents, regular local Windows files. Source repairs never execute application operations.", SemanticRole.ContextNote);
        Line("updateme rehearses an update; adding its exact --apply approval performs it without uninstalling first. Doctor --apply performs a reinstall. Both recheck the reviewed npm target and record the transaction. npm queries may write cache/logs.", SemanticRole.ContextNote);
        Line("Doctor proposals and receipts live under LocalAppData/DERT (or absolute DERT_STATE_HOME). Existing .erpdert note recovery remains compatible.", SemanticRole.ContextNote);
        Line("ƒin.", SemanticRole.Boundary);
    }

    public void DesktopPreferences(Authoring.DesktopSettings settings, string path, bool saved)
    {
        Banner(saved ? "SETTINGS / SAVED" : "SETTINGS");
        Line(path, SemanticRole.ContextNote);
        Line($"windows-menu-fade: {(settings.WindowsMenuFade ? "on" : "off")}", SemanticRole.Text);
        Line($"word-wrap: {(settings.WordWrap ? "on" : "off")}", SemanticRole.Text);
        Line($"show-line-numbers: {(settings.ShowLineNumbers ? "on" : "off")}", SemanticRole.Text);
        Line($"reduced-motion: {(settings.ReducedMotion ? "on" : "off")}", SemanticRole.Text);
        Line($"font-size: {settings.FontSize.ToString(CultureInfo.InvariantCulture)}", SemanticRole.Text);
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
        if (options.References && options.At is { } referenceLine)
        {
            if (Motion.ReferenceConnection.Read(new(document), referenceLine) is { } references)
                foreach (var line in references.Describe(all: true).Lines) Line(line.Text, SemanticRole.Text);
            else Line("This line has no reasoning-note names. MER find <text> searches literal source.", SemanticRole.ContextNote);
        }
        else if (focused is not null) Ownership(document, focused);
        else if (options.At is not null)
            Line("This line is spacing between structures; it has no entry value.", SemanticRole.ContextNote);
        Line("");
        Summary(document);
        if (document.Diagnostics.Count > 0)
            Line("Use DERT lint with this file for source-cited explanations.", SemanticRole.ContextNote);
    }

    public void Failure(string message)
    {
        Banner("COULD NOT CONTINUE");
        Line(message, SemanticRole.Target);
        Line("DERT -help shows the available commands.", SemanticRole.ContextNote);
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
        grid.AddRow(new ConsoleText($"{(selected ? ">" : " ")}{line.Number,6} │",
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
        if (DertSourceReading.Form(focused.Node) is { } reading) Line(reading.Explanation, SemanticRole.ContextNote);
        if (DertPunctuation.Detail(focused.Node) is { } punctuation) Line(punctuation, SemanticRole.ContextNote);
        switch (focused.Node)
        {
            case ChainNode chain:
                Fields(chain.Header);
                Line(DertSourceReading.ChainExtent(new(document), chain), SemanticRole.ContextNote);
                break;
            case NoteEntryNode entry:
                Line($"Value       {entry.Value.Text}", SemanticRole.Sinker);
                if (entry.Annotation is not null) Line($"Context     {entry.Annotation.Text}");
                Line(entry.HasComma ? "Comma       present in the source; preserved" : "Comma       absent", SemanticRole.ContextNote);
                break;
            case ReasoningNoteNode note:
                Fields(note.Header);
                ContextFields(document, note);
                Line(note.Closing is null ? "Closure     missing //" : "Closure     // on this line", SemanticRole.Reasoning);
                break;
            case ReasoningBlockNode block:
                Fields(block.Header);
                ContextFields(document, block);
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

    private void ContextFields(DertDocument document, DertNode node)
    {
        if (ContextReading.Read(document, node) is not { Reader: not ContextReader.Written } reading) return;
        Line("READ AS / " + reading.Summary, SemanticRole.Reasoning);
        foreach (var item in reading.Items)
        {
            var column = item.Value.Span.Start - document.Source.Lines[item.LineNumber - 1].Start + 1;
            Line($"{item.Number:00} / {item.LineNumber}:{column} / " + (item.Value.Text.Length == 0 ? "words not written" : item.Value.Text), SemanticRole.Sinker);
            if (item.Annotation is { } annotation) Line("     // " + annotation.Text, SemanticRole.ContextNote);
        }
        foreach (var issue in reading.Issues.Where(issue => issue.Kind != ContextIssueKind.MissingClosing)) Line(issue.Message, SemanticRole.ContextNote);
        if (reading.Reader == ContextReader.Similarities) Line("Listed together by this note; named targets remain unresolved.", SemanticRole.ContextNote);
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
        Line("DERT MER", SemanticRole.Title);
        Line(label, SemanticRole.Reasoning);
        Line(DateTimeOffset.Now.ToString("dd MMM yyyy · HH:mm:ss zzz", CultureInfo.InvariantCulture).ToUpperInvariant(),
            SemanticRole.ContextNote);
        Line(new string('─', Math.Clamp(console.Profile.Width, 20, 100)), SemanticRole.Rule);
    }

    private static string Count(int value, string singular, string? plural = null) =>
        $"{value} {(value == 1 ? singular : plural ?? singular + "s")}";

    private void Line(string text, SemanticRole role = SemanticRole.Text)
    {
        console.Write(new ConsoleText(Safe(text), Style(role)));
        console.WriteLine();
    }

    private Style Style(SemanticRole role)
    {
        if (plain) return Spectre.Console.Style.Plain;
        var color = role switch
        {
            SemanticRole.ContextNote or SemanticRole.Rule => new Color(144, 144, 159),
            SemanticRole.Reasoning or SemanticRole.Head => new Color(181, 167, 239),
            SemanticRole.Hook => new Color(231, 177, 125),
            SemanticRole.Line => new Color(144, 179, 235),
            SemanticRole.Target => new Color(239, 164, 151),
            SemanticRole.Boundary or SemanticRole.Title => new Color(231, 177, 125),
            _ => new Color(228, 226, 234)
        };
        return new(color, decoration: role is SemanticRole.Title or SemanticRole.Boundary ? Decoration.Bold : Decoration.None);
    }

    private static string Safe(string text) => Dert.Text.DisplayCells.Safe(text);
}
