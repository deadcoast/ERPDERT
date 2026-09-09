using System.Security.Cryptography;
using System.Text;
using Dert.Core;
using Erpdert.Cli.Operations;
using Erpdert.Cli.Repairs;
using Erpdert.Mer;

namespace Erpdert.Cli.Motion;

internal sealed partial class MerReview
{
    private readonly RepairFileService repairService = repairs ?? new();
    private readonly List<InkLine> repairRecord = [];
    private RepairSnapshot? pendingRepair;
    private MotionSource? beforeRepair;
    private MotionSource? afterRepair;
    private NoteRepairInspection? noteChoices;
    private DocumentAnnotation? repairAnnotation;
    private bool showBefore;
    private bool repairFailed;
    private bool repairBlocked;

    private string RepairCommands()
    {
        if (pendingRepair is not null) return " / before / after" + (snapshot.Score.Replay ? "" : " / apply") + " / cancel";
        if (noteChoices is not null) return " / close <line|eof> / cancel";
        if (bytes is null || analysis.Document is null || repairBlocked) return "";
        var title = TitleRuleRepair.Plan(analysis.Document);
        var titleHere = title.IsReady && (selected == title.TitleLine || selected == title.RuleLine);
        var noteHere = false;
        if (selected is int line)
            try { NoteClosureRepair.Analyze(analysis.Document, line); noteHere = true; }
            catch (ArgumentException) { }
        return (noteHere || titleHere ? " / repair" : "") + (title.IsReady && !titleHere ? " / repair title" : "") + " / undo";
    }

    private MotionSnapshot RepairView()
    {
        if (pendingRepair is null) return snapshot;
        var source = showBefore ? beforeRepair : afterRepair;
        // This is a comparison projection, never a replacement for the settled
        // source-analysis journal. Only Apply's verified result starts new analysis.
        return snapshot with { Score = snapshot.Score with { Source = source } };
    }

    private bool HandleRepair(string answer, CancellationToken cancellation)
    {
        var command = answer.ToLowerInvariant();
        if (command is not ("repair" or "repair title" or "undo" or "before" or "after" or "apply" or "cancel") &&
            !command.StartsWith("close ", StringComparison.Ordinal)) return false;
        if (bytes is null || analysis.Document is null) { Say("A readable source is needed before repairing it.", Ink.Copper); return true; }
        try
        {
            if (command == "cancel")
            {
                if (pendingRepair is null && noteChoices is null)
                { Say("There is no open preview to cancel.", Ink.Muted); return true; }
                pendingRepair = null; noteChoices = null; repairAnnotation = null;
                ClampSelection();
                Say("Preview left without applying. No source or recovery copy was written by this preview.", Ink.Muted);
                Show();
            }
            else if (command is "before" or "after")
            {
                if (pendingRepair is null) Say("Open a repair or undo preview first.", Ink.Muted);
                else { showBefore = command == "before"; PreviewAnnotation(); Show(); }
            }
            else if (command == "apply")
            {
                if (snapshot.Score.Replay) Say("Replay is read-only. Open this file without --replay to choose a saved edit.", Ink.Copper);
                else if (pendingRepair is null) Say("No preview is ready to apply. Choose a repair and its exact edit first.", Ink.Muted);
                else
                {
                    cancellation.ThrowIfCancellationRequested();
                    var applied = pendingRepair;
                    var result = repairService.Apply(applied);
                    pendingRepair = null; noteChoices = null; repairFailed = false; repairBlocked = false;
                    bytes = Encoding.UTF8.GetBytes(result.VerifiedSource);
                    // Do not reread the path: these bytes were checked under the
                    // service's exclusive handle, even if an editor changes it next.
                    analysis = NoteScore.Analyze(() => result.VerifiedSource, path, false,
                        sourceState: applied.IsUndo ? DocumentSourceState.Restored : DocumentSourceState.Saved);
                    var journal = new MotionJournal(analysis.Score with { View = MotionView.Document }, DateTimeOffset.Now);
                    foreach (var change in analysis.Events) journal.Apply(change, TimeSpan.Zero);
                    snapshot = journal.Snapshot(TimeSpan.Zero);
                    plan = DertOperationPlan.Build(analysis.Document!);
                    ResetStructure();
                    inspections.Clear(); copy = 0;
                    updates.Clear();
                    selected = Math.Clamp(RepairLine(applied), 1, snapshot.Score.Source!.Lines.Count);
                    var title = applied.IsUndo ? "Original restored" : applied.Plan is NoteClosurePlan ? "Note boundary closed" : "Title aligned";
                    var remaining = analysis.Document!.Diagnostics.Count;
                    repairAnnotation = new(selected.Value, [InkLine.Plain(title, Ink.Good),
                        InkLine.Plain("Written bytes read back and verified."),
                        InkLine.Plain(remaining == 0 ? "Supported structural checks are clear." : $"{remaining} " + (remaining == 1 ? "finding remains" : "findings remain") + " / needs attention", remaining == 0 ? Ink.Muted : Ink.Copper),
                        InkLine.Plain(applied.IsUndo ? "The saved original remains available." : "undo compares the saved original.", Ink.Muted),
                        InkLine.Plain("Recovery location in details.", Ink.Muted)]);
                    repairRecord.AddRange([InkLine.Plain(title + " / " + snapshot.Created.ToString("O"), Ink.Good),
                        InkLine.Plain(result.Message), InkLine.Plain("Verified revision / " + result.Revision, Ink.Muted),
                        InkLine.Plain("Saved original / " + Path.Combine(result.ReceiptDirectory, "before.DERT"), Ink.Muted)]);
                    Show();
                }
            }
            else if (repairBlocked)
                Say("Reopen mer to check the current file before another repair. Details retains the stopped repair and recovery location.", Ink.Copper);
            else if (command.StartsWith("close ", StringComparison.Ordinal))
            {
                if (noteChoices is null) Say("Select an unfinished note and use repair before choosing its ending.", Ink.Muted);
                else
                {
                    var key = answer[6..].Trim();
                    if (!noteChoices.Analysis.Boundaries.Any(boundary => boundary.Key.Equals(key, StringComparison.OrdinalIgnoreCase)))
                        Say("Choose one of the offered source lines, or eof when offered. Your selection is unchanged.", Ink.Muted);
                    else OpenPreview(repairService.PreviewNote(noteChoices, key));
                }
            }
            else if (pendingRepair is not null || noteChoices is not null)
                Say("Use cancel to leave this proposal before choosing another repair.", Ink.Muted);
            else if (command == "undo")
            {
                var preview = repairService.PreviewUndo(path);
                if (preview.Revision != Revision())
                    throw new RepairException("DERT4002", "Source changed since this analysis. Reopen mer before comparing undo. Nothing was written.");
                OpenPreview(preview);
            }
            else
            {
                NoteClosureAnalysis? note = null;
                if (command == "repair" && selected is int at)
                    try { note = NoteClosureRepair.Analyze(analysis.Document, at); }
                    catch (ArgumentException) { /* This line is not an unfinished block. */ }
                if (note is not null)
                {
                    noteChoices = repairService.InspectNote(path, note.Block.LineNumber, Revision());
                    selected = note.Block.LineNumber;
                    var choices = noteChoices.Analysis.Boundaries.Select(boundary => InkLine.Plain(
                        $"close {boundary.Key} / {boundary.KeptCount} kept / {boundary.ReleasedCount} released", Ink.Muted)).ToArray();
                    repairAnnotation = new(selected.Value, [InkLine.Plain("Choose where this note ends", Ink.Copper),
                        InkLine.Plain("No closing // has been chosen."), .. choices],
                        [InkLine.Plain("Choose where this note ends", Ink.Copper), InkLine.Plain("close <line|eof> selects a preview."),
                        InkLine.Plain($"{choices.Length} possible endings / details lists all", Ink.Muted)]);
                    Show();
                    // Choices are actions, so keep the complete list reachable
                    // without requiring a wide or tall terminal.
                    Print(choices.SelectMany(line => TerminalText.Wrap(line.Text, Math.Max(1, MerHost.Size(console).Columns - 1))
                        .Select(text => InkLine.Plain(text, Ink.Muted))).ToArray());
                }
                else
                {
                    var title = TitleRuleRepair.Plan(analysis.Document);
                    if (command == "repair" && selected != title.TitleLine && selected != title.RuleLine)
                    { Say("No supported repair at this line. Select an unfinished note or use repair title for the title rule.", Ink.Muted); return true; }
                    OpenPreview(repairService.Preview(path, Revision()));
                }
            }
        }
        catch (RepairException error)
        {
            pendingRepair = null; noteChoices = null; repairFailed = true;
            repairBlocked = error.Code is "DERT4002" or "DERT4100" or "DERT4102" or "DERT4103";
            if (repairBlocked) updates.Clear();
            ClampSelection();
            if (repairBlocked && snapshot.Score.Source is { } source)
                snapshot = snapshot with { Score = snapshot.Score with { Source = source with { State = DocumentSourceState.Unverified } } };
            var lines = new List<InkLine> { InkLine.Plain(error.Code + " / repair stopped", Ink.Bad), InkLine.Plain(error.Message) };
            if (error.RecoveryPath is not null) lines.Add(InkLine.Plain("Saved original / " + error.RecoveryPath, Ink.Copper));
            repairRecord.AddRange(lines);
            repairAnnotation = new(selected ?? 1, lines);
            Show();
            // Recovery guidance must never depend on the viewport's truncation.
            foreach (var line in lines) Say(line.Text, line.Runs[0].Ink);
        }
        return true;
    }

    private string Revision() => Convert.ToHexString(SHA256.HashData(bytes!));
    private void ClampSelection() => selected = Math.Clamp(selected ?? 1, 1, Math.Max(1, snapshot.Score.Source?.Lines.Count ?? 1));

    private void OpenPreview(RepairSnapshot preview)
    {
        if (!preview.CanApply)
        {
            Say(preview.IsUndo ? "These original bytes are already restored. No edit is needed." : preview.Plan.Explanation, Ink.Muted);
            return;
        }
        pendingRepair = preview;
        beforeRepair = NoteScore.Source(DertParser.Parse(preview.Before), path) with { State = DocumentSourceState.BeforeEdit };
        afterRepair = NoteScore.Source(DertParser.Parse(preview.After), path) with { State = DocumentSourceState.Proposed };
        showBefore = false;
        selected = RepairLine(preview);
        PreviewAnnotation();
        Show();
        Say(preview.IsUndo ? "apply restores these exact original bytes. Later edits block undo." :
            "apply saves this exact edit and a full, unencrypted local undo copy in .erpdert/repairs beside this file.", Ink.Muted);
        if (snapshot.Score.Replay) Say("Replay is read-only. Comparison is available; saving requires opening the file without --replay.", Ink.Copper);
    }

    private void PreviewAnnotation()
    {
        var preview = pendingRepair!;
        selected = Math.Clamp(RepairLine(preview), 1, (showBefore ? beforeRepair : afterRepair)!.Lines.Count);
        var lines = new List<InkLine> { InkLine.Plain(preview.IsUndo ? "Restore the saved original" :
            preview.Plan is NoteClosurePlan ? "Close the chosen note" : "Align the title rule", Ink.Copper),
            InkLine.Plain(showBefore ? "Before / captured source" : "After / proposed source", Ink.Muted) };
        if (preview.Plan is TitleRepairPlan title)
        {
            var original = title.Edit!.Before;
            var proposed = title.Edit.After.Trim('\r', '\n');
            var before = preview.IsUndo ? proposed : original;
            var after = preview.IsUndo ? original : proposed;
            lines.Add(Diff(before, after, false));
            lines.Add(Diff(after, before, true));
            lines.Add(InkLine.Plain(preview.IsUndo ? "Original rule and line endings return." : "Title words stay exactly as written.", Ink.Muted));
        }
        else if (preview.Plan is NoteClosurePlan note)
        {
            lines.Add(InkLine.Plain(preview.IsUndo ? "Remove the selected closing //." : $"Add // on result line {note.ClosingLine}."));
            lines.Add(InkLine.Plain(preview.IsUndo ? $"{note.Analysis.Block.Entries.Count} entries return to the unfinished note." :
                $"{note.Boundary.KeptCount} kept / {note.Boundary.ReleasedCount} released", Ink.Muted));
            foreach (var change in note.OwnershipChanges)
                lines.Add(InkLine.Plain(preview.IsUndo ? $"Line {change.ResultLine} -> {change.OriginalLine}: entry in note {note.Analysis.Block.LineNumber}" :
                    $"Line {change.OriginalLine} -> {change.ResultLine}: {Role(change.Role)} / {change.OwnerLabel}", Ink.Muted));
            if (preview.IsUndo) lines.Add(InkLine.Plain("Undo restores the unfinished note and its original ownership.", Ink.Copper));
        }
        lines.Add(InkLine.Plain("before / after compare; apply saves", Ink.Muted));
        repairAnnotation = new(selected!.Value, lines, [lines[0], lines[1], lines[2], lines[3], InkLine.Plain("before / after / apply / cancel; details", Ink.Muted)]);

        static InkLine Diff(string text, string other, bool added)
        {
            if (text.Length == 0) return InkLine.Plain((added ? "+ " : "- ") + "(no rule)", Ink.Muted);
            var common = 0;
            while (common < Math.Min(text.Length, other.Length) && text[common] == other[common]) common++;
            return new(new InkRun(added ? "+ " : "- ", Ink.Muted), new InkRun(text[..common], Ink.Muted),
                new InkRun(text[common..], added ? Ink.Signal : Ink.Bad, true));
        }
        static string Role(NoteResultRole role) => role switch
        {
            NoteResultRole.OrdinaryNote => "ordinary note",
            NoteResultRole.ReasoningNote => "inline reasoning note",
            NoteResultRole.ReasoningBlock => "reasoning block",
            NoteResultRole.ReasoningEntry => "reasoning entry",
            _ => "other source structure"
        };
    }

    private static int RepairLine(RepairSnapshot preview) => preview.Plan switch
    {
        TitleRepairPlan title => title.RuleLine,
        NoteClosurePlan note => note.ClosingLine,
        _ => 1
    };

    private void RepairDetails()
    {
        if (pendingRepair is not null) renderer.RepairPreview(pendingRepair);
        else if (noteChoices is not null) renderer.NoteChoices(noteChoices, copyCommands: true, fromLint: true);
        else if (bytes is not null && analysis.Document is not null) renderer.MerRehearsals(analysis.Document, path, Revision());
        foreach (var line in repairRecord) Say(line.Text, line.Runs[0].Ink);
    }
}
