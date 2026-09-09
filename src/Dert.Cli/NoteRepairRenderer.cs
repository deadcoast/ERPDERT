using Dert.Core;
using Dert.Cli.Repairs;

namespace Dert.Cli;

internal sealed partial class DocumentRenderer
{
    public void NoteChoices(NoteRepairInspection inspection, bool copyCommands, bool fromLint = false)
    {
        var analysis = inspection.Analysis;
        var block = analysis.Block;
        Banner("SERNTERX / CHOOSE A NOTE BOUNDARY");
        Line(inspection.Path, SemanticRole.ContextNote);
        if (fromLint) LintSourceMatched();
        Line("");
        Line($"DERT1001 / OPEN NOTE · LINE {block.LineNumber}", SemanticRole.Hook);
        Line("This note has no closing // on its own line. Where should it end?");
        Line("CURRENT / recovered grouping, not a confirmed boundary", SemanticRole.Reasoning);
        var stop = analysis.Boundaries[^1];
        foreach (var line in analysis.Document.Source.Lines.Where(l =>
            l.Number >= block.LineNumber && (stop.BeforeLine is null || l.Number <= stop.BeforeLine)))
            SourceLine(analysis.Document, line, line.Number == block.LineNumber);
        if (stop.BeforeLine is null) Line("        · EOF", SemanticRole.ContextNote);
        else Line($"Recovery stops before source line {stop.BeforeLine}; this is a choice, not a guessed ending.", SemanticRole.ContextNote);
        Line("");
        Line("CHOOSE THE CUT / words and commas stay exactly as written", SemanticRole.Hook);
        foreach (var boundary in analysis.Boundaries)
        {
            Line($"{boundary.Key}  │  {BoundaryLabel(boundary)}", SemanticRole.Target);
            Line($"     {boundary.KeptCount} kept in this note · {boundary.ReleasedCount} released for a new interpretation", SemanticRole.ContextNote);
        }
        Line("leave  │  Keep the source unchanged", SemanticRole.ContextNote);
        Line("A choice opens an ownership-and-edit preview. It does not write the file.");
        if (!copyCommands) return;
        Line("");
        Line("CHOICES ONLY · nothing written", SemanticRole.Hook);
        Line("Copy one complete PowerShell command to rehearse that boundary:");
        foreach (var boundary in analysis.Boundaries)
            CopyCommand(NoteSelectionCommand(inspection.Path, block.LineNumber, boundary.Key) +
                (fromLint ? $" --from-lint {inspection.Revision}" + (plain ? " --plain" : "") : ""));
        Line("ƒin.", SemanticRole.Boundary);
    }

    private void NoteRepairPreview(RepairSnapshot snapshot, NoteClosurePlan plan, bool fromLint)
    {
        Banner(snapshot.IsUndo ? "SERNTERX / UNDO REHEARSAL" : "SERNTERX / BOUNDARY REHEARSAL");
        Line(snapshot.Path, SemanticRole.ContextNote);
        if (fromLint) LintSourceMatched();
        Line("");
        if (!snapshot.CanApply)
        {
            Line("UNCHANGED", SemanticRole.Title);
            Line("These original bytes are already restored. No source bytes were written.");
            return;
        }
        var analysis = plan.Analysis;
        var source = analysis.Document;
        var block = analysis.Block;
        Line(snapshot.IsUndo ? "RESTORE / EXACT ORIGINAL" : $"DERT1001 / SELECTED BOUNDARY · NOTE AT LINE {block.LineNumber}", SemanticRole.Hook);
        Line(snapshot.IsUndo
            ? "Remove the selected closing marker and restore the original unfinished grouping, including its exact line endings."
            : plan.Explanation);
        Line("");
        SourceLine(source, source.Source.Lines[block.LineNumber - 1], true);
        var prior = source.Source.Lines.LastOrDefault(l => l.Start < plan.Boundary.Offset && l.ContentEnd > l.Start);
        if (prior is not null && prior.Number != block.LineNumber)
        {
            if (prior.Number > block.LineNumber + 1) Line("        ⋮ unchanged source", SemanticRole.ContextNote);
            SourceLine(source, prior, false);
        }
        DiffRow(snapshot.IsUndo ? "−" : "+", plan.ClosingLine, "//", snapshot.IsUndo ? RemovedStyle() : AddedStyle());
        if (plan.Boundary.BeforeLine is int next)
            SourceLine(snapshot.IsUndo ? source : plan.ResultDocument,
                (snapshot.IsUndo ? source : plan.ResultDocument).Source.Lines[next - (snapshot.IsUndo ? 1 : 0)], false);
        else Line("        · EOF", SemanticRole.ContextNote);
        Line("− removed   + added   ·   signs and line numbers are display-only", SemanticRole.ContextNote);
        Line("");
        Line(snapshot.IsUndo ? "OWNERSHIP / RESTORED" : "OWNERSHIP / AFTER THIS CUT", SemanticRole.Hook);
        Line(snapshot.IsUndo
            ? $"All {block.Entries.Count} original entries return to this note's recovered grouping. The missing-boundary finding returns too."
            : $"{plan.Boundary.KeptCount} kept in this note · {plan.Boundary.ReleasedCount} released from it", SemanticRole.Line);
        if (!snapshot.IsUndo)
        {
            if (plan.Boundary.KeptCount > 0)
            {
                Line("STAYS INSIDE / this note", SemanticRole.Target);
                foreach (var entry in block.Entries.Take(plan.Boundary.KeptCount))
                    SourceLine(source, source.Source.Lines[entry.LineNumber - 1], false);
            }
            if (plan.OwnershipChanges.Count > 0) Line("OUTSIDE THIS NOTE / reparsed ownership", SemanticRole.Target);
            foreach (var change in plan.OwnershipChanges)
            {
                Line($"source {change.OriginalLine} → result {change.ResultLine} · {RoleName(change.Role)}", SemanticRole.Reasoning);
                Line("Belongs to  " + change.OwnerLabel + (change.OwnerLine is int owner ? $" · result line {owner}" : ""), SemanticRole.Target);
                SourceLine(plan.ResultDocument, plan.ResultDocument.Source.Lines[change.ResultLine - 1], false);
            }
            if (plan.OwnershipChanges.Count == 0)
                Line("Every current entry stays in this note. Its closing boundary becomes explicit.", SemanticRole.ContextNote);
            Line("This changes note ownership, not just decoration. No DERT command is executed.");
            if (plan.UsesDefaultNewline)
                Line("No existing line ending is available. The added line uses Windows CRLF; no final newline is added.", SemanticRole.ContextNote);
            else
                Line("The added line reuses the nearest preceding line ending. Existing bytes, BOM, and final-newline style stay unchanged.", SemanticRole.ContextNote);
            var remaining = plan.ResultDocument.Diagnostics;
            Line($"Reparsed proposal / {Count(remaining.Count, "finding")} would remain", SemanticRole.Reasoning);
            foreach (var finding in remaining)
                Line($"{finding.Code} · result line {finding.LineNumber} · {finding.Message}");
            Line("Before writing: save and verify a full, unencrypted local undo copy beside this file in .erpdert/repairs.", SemanticRole.ContextNote);
        }
        else Line("The saved original stays available after undo. Later edits are never silently replaced.", SemanticRole.ContextNote);
        Line("Your choice belongs to this file and this exact edit. Changed source, file identity, or undo selection cancels it.", SemanticRole.ContextNote);
        Line("");
    }

    private string NoteSelectionCommand(string path, int atLine, string boundary) =>
        $"{Invocation()} repair {PowerShellLiteral(path)} --at {atLine} --close-before {boundary}";
    private static string BoundaryLabel(NoteBoundary boundary) => boundary.BeforeLine is int line
        ? $"Close before source line {line}" : "Close at the end of the file";
    private static string RoleName(NoteResultRole role) => role switch
    {
        NoteResultRole.OrdinaryNote => "ordinary note",
        NoteResultRole.ReasoningNote => "inline reasoning note",
        NoteResultRole.ReasoningBlock => "reasoning block",
        NoteResultRole.ReasoningEntry => "reasoning entry",
        _ => "source structure"
    };
}
