using Dert.Cli.Applications;
using Dert.Core;
using Dert.Cli.Discovery;
using Dert.Cli.Operations;
using Dert.Cli.Repairs;
using Dert.Mer;
using Spectre.Console;

namespace Dert.Cli.Motion;

internal sealed partial class MerReview(NoteScore analysis, MotionSnapshot snapshot, byte[]? bytes, string path,
    IAnsiConsole console, DocumentRenderer renderer, TextReader input, bool plain, DiscoveryEnvironment? environment,
    RepairFileService? repairs = null, ICommandRunner? commandRunner = null, string? doctorStateDirectory = null)
{
    private DertOperationPlan? plan = analysis.Document is { } document ? DertOperationPlan.Build(document) : null;
    private readonly Dictionary<int, LocalInspection> inspections = [];
    private int? selected;
    private int copy;

    internal int Run(CancellationToken cancellation)
    {
        var evidence = snapshot.Stages.SelectMany(stage => stage.Evidence).ToArray();
        var stops = evidence.Where(item => item.SourceLine is not null).Select(item => item.SourceLine!.Value)
            .Concat(plan?.Steps.Select(step => step.RequestLine) ?? [])
            .Concat(plan?.Findings.Select(finding => finding.LineNumber) ?? []).Distinct().Order().ToArray();
        selected = evidence.FirstOrDefault(item => item.Kind == EvidenceKind.Error)?.SourceLine ??
            evidence.LastOrDefault(item => item.Focus is not null)?.SourceLine;
        while (true)
        {
            evidence = snapshot.Stages.SelectMany(stage => stage.Evidence).ToArray();
            stops = evidence.Where(item => item.SourceLine is not null).Select(item => item.SourceLine!.Value)
                .Concat(structureIndex?.Locations.Select(location => location.Node.LineNumber) ?? [])
                .Concat(plan?.Steps.Select(step => step.RequestLine) ?? [])
                .Concat(plan?.Findings.Select(finding => finding.LineNumber) ?? []).Distinct().Order().ToArray();
            var following = stops.FirstOrDefault(line => selected is null || line > selected);
            var next = following == 0 ? "next: end" : $"next: line {following} / {Destination(following)}";
            if (pendingRepair is null && noteChoices is null) Say("Connections / prev / " + next, Ink.Muted);
            Say("Enter done" + (pendingRepair is null && noteChoices is null ? " / at <line>" : "") + " / details" + RepairCommands() +
                (pendingRepair is null && noteChoices is null && Operation() is { Kind: ApplicationAction.UpdateDiscovery } ? " / probe" : "") +
                (pendingRepair is null && noteChoices is null ? UpdateCommands() : "") +
                (pendingRepair is null && noteChoices is null ? LocationCommands() : "") +
                (pendingRepair is null && noteChoices is null && Inspection() is { CopyCount: > 1 } found ? $" / copy 1-{found.CopyCount}" : ""), Ink.Muted);
            if (pendingRepair is null && noteChoices is null && structureIndex is not null)
                Say("Notes / find <text> / structure / owner / refs / context" + (referenceSet?.Choices.Count > 0 ? $" / ref 1-{referenceSet.Choices.Count} / ref back" : "") + (searchHits.Count > 0 ? $" / hit 1-{searchHits.Count} / hits" : ""), Ink.Muted);
            if (pendingRepair is null && noteChoices is null && Inspection() is not null && !snapshot.Score.Replay)
                Say("Keep this observation / keep <new.json path>", Ink.Signal);
            var answer = Task.Run(input.ReadLine, cancellation).WaitAsync(cancellation).GetAwaiter().GetResult()?.Trim();
            cancellation.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(answer) || answer.Equals("q", StringComparison.OrdinalIgnoreCase)) return repairFailed || UpdateNeedsAttention() || LocationNeedsAttention() ? Math.Max(1, analysis.Exit) : analysis.Exit;
            if (answer.Equals("details", StringComparison.OrdinalIgnoreCase))
            {
                Print(MerCompositor.Receipt(snapshot, MerHost.Size(console).Columns));
                if (plan is not null)
                    foreach (var finding in plan.Findings.Except(plan.Document.Diagnostics).Where(finding => InOperationContext(finding.LineNumber)))
                        Print([InkLine.Plain($"{finding.Code} / line {finding.LineNumber}", Ink.Copper), InkLine.Plain(finding.Message)]);
                foreach (var inspection in inspections.Values) Print(inspection.Details());
                RepairDetails();
                UpdateDetails();
                LocationDetails();
                StructureDetails();
                ReferenceDetails();
                if (contextAnnotation is not null && contextPacket is not null) renderer.ContextReview(contextPacket, true);
            }
            else if (HandleRepair(answer, cancellation)) { }
            else if (pendingRepair is not null || noteChoices is not null)
                Say("Finish this comparison with apply or cancel before navigating. Enter leaves without applying.", Ink.Muted);
            else if (HandleResult(answer)) { }
            else if (HandleContext(answer)) { }
            else if (HandleReferences(answer)) { }
            else if (HandleStructure(answer)) { }
            else if (HandleLocation(answer, cancellation)) { }
            else if (HandleUpdate(answer, cancellation)) { }
            else if (answer.Equals("next", StringComparison.OrdinalIgnoreCase) || answer.Equals("prev", StringComparison.OrdinalIgnoreCase))
            {
                var previous = answer.Equals("prev", StringComparison.OrdinalIgnoreCase);
                var target = previous ? stops.LastOrDefault(line => selected is null || line < selected) : following;
                if (target == 0) Say($"Already at the {(previous ? "first" : "last")} connection. Your selection is unchanged.", Ink.Muted);
                else Select(target);
            }
            else if (answer.Equals("back", StringComparison.OrdinalIgnoreCase))
            {
                selected = evidence.FirstOrDefault(item => item.Kind == EvidenceKind.Error)?.SourceLine ?? evidence.LastOrDefault(item => item.Focus is not null)?.SourceLine;
                copy = 0;
                repairAnnotation = null;
                showStructure = false;
                referenceAnnotation = null;
                contextAnnotation = null;
                Show();
            }
            else if (answer.StartsWith("at ", StringComparison.OrdinalIgnoreCase) && int.TryParse(answer.AsSpan(3), out var line) &&
                snapshot.Score.Source?.LineNumbers?.Contains(line) == true) Select(line);
            else if (answer.Equals("probe", StringComparison.OrdinalIgnoreCase)) Probe(cancellation);
            else if (answer.StartsWith("copy ", StringComparison.OrdinalIgnoreCase) && int.TryParse(answer.AsSpan(5), out var number))
            {
                if (Inspection() is { } inspection && number >= 1 && number <= inspection.CopyCount) { copy = number - 1; showStructure = false; Show(); }
                else Say("No inspected copy has that number. Your selection is unchanged.", Ink.Muted);
            }
            else Say("Use next, prev, at <line>, details, an offered action, back, or Enter to finish.", Ink.Muted);
        }

        string Destination(int line)
        {
            if (plan?.Steps.FirstOrDefault(step => step.RequestLine == line) is { } operation)
                return operation.Kind switch { ApplicationAction.UpdateDiscovery => "Codex discovery", ApplicationAction.Where => "application location", ApplicationAction.Doctor => "doctor proposal", _ => "update rehearsal" };
            var item = evidence.LastOrDefault(item => item.SourceLine == line);
            if (item?.Focus is NoteConnectionFocus connection) return connection.Value;
            if (item?.Focus is TitleRuleFocus) return "title rule";
            if (InOperationContext(line) && plan?.Findings.FirstOrDefault(finding => finding.LineNumber == line) is { } finding) return finding.Code;
            return structureIndex?.AtLine(line) is { } location ? DertStructureIndex.Label(location.Node) : "source";
        }
    }

    private void Select(int line, bool structure = false, DocumentAnnotation? reference = null, DocumentAnnotation? context = null)
    { selected = line; copy = 0; repairAnnotation = null; referenceAnnotation = reference; contextAnnotation = context; showStructure = structure; Show(); }
    private PlannedOperation? Operation() => plan?.Steps.FirstOrDefault(step => step.RequestLine == selected || step.Source.LineNumber == selected);
    private LocalInspection? Inspection() => selected is int line ? inspections.GetValueOrDefault(line) : null;

    private DocumentAnnotation? Annotation()
    {
        if (repairAnnotation is not null) return repairAnnotation;
        if (contextAnnotation is not null) return contextAnnotation;
        if (referenceAnnotation is not null) return referenceAnnotation;
        if (showStructure) return StructureAnnotation();
        if (plan is { CanRun: true } && LocationAnnotation() is { } location) return location;
        if (Update() is { } update) return update.Annotation();
        if (Inspection() is { } inspection) return inspection.Annotation(copy);
        if (selected is int at && InOperationContext(at) && plan?.Findings.Except(plan.Document.Diagnostics).FirstOrDefault(finding => finding.LineNumber == selected) is { } blocker)
            return new(blocker.LineNumber, [InkLine.Plain("Operation needs attention", Ink.Copper),
                InkLine.Plain($"{blocker.Code} / line {blocker.LineNumber}"), InkLine.Plain(blocker.Message)]);
        if (Operation() is not { } operation || selected is not int line)
            return analysis.Document?.Diagnostics.Any(finding => finding.LineNumber == selected) == true ? null : StructureAnnotation();
        if (plan is { CanRun: false })
        {
            var issue = plan.Findings.First(finding => finding.Severity == DiagnosticSeverity.Error);
            return new(line, [InkLine.Plain("Inspection is blocked", Ink.Copper), InkLine.Plain($"{issue.Code} / line {issue.LineNumber}"), InkLine.Plain(issue.Message)]);
        }
        if (operation.Kind != ApplicationAction.UpdateDiscovery)
            return new(line, [InkLine.Plain("This chain requests an update rehearsal", Ink.Copper),
                InkLine.Plain("rehearse finds local copies for this request.", Ink.Muted),
                InkLine.Plain("Then choose a copy and check its npm route.", Ink.Muted),
                InkLine.Plain("Queries need an explicit check; no update runs.", Ink.Muted)]);
        return new(line, [InkLine.Plain("Find Codex on this PC", Ink.Signal), InkLine.Plain("Source -> local launchers -> package"),
            InkLine.Plain("probe inspects local PATH and metadata", Ink.Muted), InkLine.Plain("No programs started", Ink.Muted)]);
    }

    private void Probe(CancellationToken cancellation)
    {
        showStructure = false;
        if (Operation() is not { Kind: ApplicationAction.UpdateDiscovery } || plan is not { CanRun: true })
        {
            if (Operation() is not null) Show();
            else Say("No discovery request at this line. Select a STERT ; update ;; codex request first.", Ink.Muted);
            return;
        }
        if (!OperatingSystem.IsWindows()) { Say("Local discovery currently follows Windows PowerShell paths.", Ink.Copper); return; }
        if (bytes is null || selected is not int line) return;
        try
        {
            var current = File.ReadAllBytesAsync(path, cancellation).GetAwaiter().GetResult();
            if (!current.AsSpan().SequenceEqual(bytes))
            {
                Say("Source changed since this analysis. Reopen mer on this file before probing. The recorded source is still available.", Ink.Copper);
                return;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { Say("Source could not be checked. No inspection started. " + error.Message, Ink.Copper); return; }

        using var terminal = new InlineTerminal(console.Profile.Out.Writer, () => MerHost.Size(console), MerHost.Colors(console, plain));
        if (MerHost.CanDrawLive(console, plain))
        {
            var size = MerHost.Size(console);
            var pending = new DocumentAnnotation(line, [InkLine.Plain("Reading local launchers", Ink.Signal),
                InkLine.Plain("Inspecting PATH and package metadata.", Ink.Muted), InkLine.Plain("No programs started", Ink.Muted)]);
            if (size.Columns >= 4 && size.Rows >= 4) terminal.Draw(MerCompositor.Document(snapshot, size.Columns, size.Rows, line, pending));
        }
        try
        {
            var report = Task.Run(() => CodexDiscovery.Scan(environment ?? DiscoveryEnvironment.Capture(), cancellation), cancellation)
                .WaitAsync(cancellation).GetAwaiter().GetResult();
            inspections[line] = new(line, DateTimeOffset.Now, report);
            copy = 0;
            var finalSize = MerHost.Size(console);
            terminal.Finish(MerCompositor.Document(snapshot, finalSize.Columns, finalSize.Rows, selected, Annotation()).Lines);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            var size = MerHost.Size(console);
            var stopped = new DocumentAnnotation(line, [InkLine.Plain("Local inspection stopped", Ink.Copper),
                InkLine.Plain("No new inspection result was captured.", Ink.Muted), InkLine.Plain("The source analysis is retained.", Ink.Muted), InkLine.Plain("No programs started", Ink.Muted)]);
            terminal.Finish(MerCompositor.Document(snapshot, size.Columns, size.Rows, line, stopped).Lines);
            throw;
        }
    }

    private void Show()
    {
        var size = MerHost.Size(console);
        Print(MerCompositor.Document(RepairView(), size.Columns, size.Rows, selected, Annotation()).Lines);
    }

    private void Say(string text, Ink ink) => Print(TerminalText.Wrap(text, Math.Max(1, MerHost.Size(console).Columns - 1))
        .Select(line => InkLine.Plain(line, ink)).ToArray());

    private void Print(IReadOnlyList<InkLine> lines)
    {
        using var terminal = new InlineTerminal(console.Profile.Out.Writer, () => MerHost.Size(console), MerHost.Colors(console, plain));
        terminal.Finish(lines);
    }
}
