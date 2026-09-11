using Dert.Cli.Applications;
using System.Diagnostics;
using Dert.Cli.Discovery;
using Dert.Cli.Operations;
using Dert.Cli.Updates;
using Dert.Cli.Transactions;
using Dert.Mer;

namespace Dert.Cli.Motion;

internal sealed partial class MerReview
{
    private readonly Dictionary<int, UpdateConnection> updates = [];
    private UpdateConnection? Update() => Operation() is { Kind: ApplicationAction.UpdateRehearsal } operation ? updates.GetValueOrDefault(operation.RequestLine) : null;
    private bool UpdateNeedsAttention() => updates.Values.Any(update => update.FailedAttempt || update.CopyCount == 0 || update.Results.Values.Any(result =>
        result.Report.NeedsAttention || result.Report.Discovery.Issues.Count > 0));
    private string UpdateCommands() => Operation() is not { Kind: ApplicationAction.UpdateRehearsal } ? "" :
        " / rehearse" + (Update() is { } update ? (update.CopyCount > 0 ? $" / copy 1-{update.CopyCount}" : "") +
        (update.Chosen && update.Route is not null && !snapshot.Score.Replay ? " / check" : "") : "");

    private bool HandleUpdate(string answer, CancellationToken cancellation)
    {
        if (answer.StartsWith("copy ", StringComparison.OrdinalIgnoreCase) && Update() is { } selection)
        {
            if (int.TryParse(answer.AsSpan(5), out var number) && selection.Select(number)) { showStructure = false; Show(); }
            else Say("No discovered copy has that number. Your selection is unchanged.", Ink.Muted);
            return true;
        }
        var rehearse = answer.Equals("rehearse", StringComparison.OrdinalIgnoreCase);
        if (!rehearse && !answer.Equals("check", StringComparison.OrdinalIgnoreCase)) return false;
        showStructure = false;
        if (Operation() is not { Kind: ApplicationAction.UpdateRehearsal } operation || plan is not { CanRun: true })
        { Say("Select an eligible commands ; updateme ;; request. Notes and blocked chains cannot start an update rehearsal.", Ink.Copper); return true; }
        if (!OperatingSystem.IsWindows()) { Say("This update rehearsal currently follows Windows npm routes.", Ink.Copper); return true; }
        if (bytes is null) return true;
        selected = operation.RequestLine; repairAnnotation = null;
        if (!rehearse && snapshot.Score.Replay)
        { Say("Replay cannot start npm queries. Open this file without --replay to check its request.", Ink.Copper); return true; }
        if (!MatchingSource(out var problem))
        { SetProblem(problem); return true; }
        if (rehearse)
        {
            var discovery = Scan();
            updates[selected.Value] = new(selected.Value, discovery, DateTimeOffset.Now);
            Show();
            Say("check queries the selected npm and its configured registry, then calculates an install dry run. npm may write cache/logs; package scripts and package updates do not run.", Ink.Muted);
            return true;
        }
        if (Update() is not { Chosen: true, Route: not null } update)
        { Say("Use rehearse to find copies, then copy <number> to choose when several routes or an incomplete search remain.", Ink.Muted); return true; }
        var previous = update.Route;
        var fresh = Scan();
        var route = fresh.Commands.FirstOrDefault(candidate => candidate.Path.Equals(previous.Path, StringComparison.OrdinalIgnoreCase));
        if (route is null || !SameRoute(previous, route))
        {
            update.Results.Remove(previous.Path);
            SetProblem("The selected installation changed since discovery. Use rehearse to inspect its current route before checking.");
            return true;
        }
        update.Problem = null;
        update.Results.Remove(previous.Path); // A new attempt cannot leave old success over a failure.
        var journal = new MotionJournal(UpdateMotion.Score(null), DateTimeOffset.Now);
        var timer = Stopwatch.StartNew();
        using var terminal = new InlineTerminal(console.Profile.Out.Writer, () => MerHost.Size(console), MerHost.Colors(console, plain));
        using var motion = new UpdateMotion(change =>
        {
            journal.Apply(change, timer.Elapsed);
            if (change is FinishMotion || !MerHost.CanDrawLive(console, plain)) return;
            var size = MerHost.Size(console);
            if (size.Columns >= 4 && size.Rows >= 4)
                terminal.Draw(MerCompositor.Document(snapshot, size.Columns, size.Rows, selected, update.Annotation(journal.Snapshot(timer.Elapsed))));
        });
        var guarded = new SourceCheckedRunner(commandRunner ?? new SystemCommandRunner(cancellation),
            () => MatchingSource(out var issue) ? null : issue, cancellation);
        UpdateReport report;
        try
        {
            report = UpdateTransaction.Inspect(fresh, route.Path, guarded, activity: motion.Observe,
                cancellation: cancellation, source: NpmSource.Capture(path));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            report = new(fresh);
            report.Issues.Add(new("DERT5402", path, "The update source could not be captured. " + error.Message));
        }
        if (cancellation.IsCancellationRequested) report.State = UpdateState.Interrupted;
        else if (!MatchingSource(out problem))
        {
            report.State = UpdateState.Unavailable; report.Install = null; report.DryRun = null; report.Proposal = null;
            report.Issues.Add(new("ERP5210", path, problem));
        }
        if (report.State == UpdateState.Ready)
        {
            try { UpdateTransaction.SaveProposal(report, UpdateCommand.ProposalDirectory(doctorStateDirectory)); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
            {
                report.State = UpdateState.Unavailable; report.Proposal = null;
                report.Issues.Add(new("DERT5402", path, "The reviewed update proposal could not be saved. " + error.Message));
            }
        }
        motion.Finish(report);
        update.Results[previous.Path] = new(journal.Snapshot(timer.Elapsed).Created, report, journal.Snapshot(timer.Elapsed));
        update.FailedAttempt = false;
        var finalSize = MerHost.Size(console);
        terminal.Finish(MerCompositor.Document(snapshot, finalSize.Columns, finalSize.Rows, selected, update.Annotation()).Lines);
        if (cancellation.IsCancellationRequested)
        {
            foreach (var issue in report.Issues) Say(issue.Code + " / " + issue.Explanation, Ink.Copper);
            throw new OperationCanceledException(cancellation);
        }
        return true;

        DiscoveryReport Scan()
        {
            try { return CodexDiscovery.Scan(environment ?? DiscoveryEnvironment.Capture(), cancellation); }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                SetProblem("Local discovery stopped. No new rehearsal result was accepted; the previous proposal was cleared.", StageState.Interrupted);
                throw;
            }
        }

        void SetProblem(string message, StageState state = StageState.Failed)
        {
            var annotation = new DocumentAnnotation(operation.RequestLine, [InkLine.Plain("Rehearsal stopped", Ink.Copper), InkLine.Plain(message)])
            { Action = new("Update rehearsal", state, "no new update result") };
            if (!updates.TryGetValue(operation.RequestLine, out var current))
                updates[operation.RequestLine] = current = new(operation.RequestLine, new([], [], 0), DateTimeOffset.Now);
            current.Problem = annotation; current.FailedAttempt = true; current.Results.Clear();
            Show(); Say(message, Ink.Copper);
        }
    }

    private bool MatchingSource(out string problem)
    {
        try
        {
            if (File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes)) { problem = ""; return true; }
            problem = "Source changed since this analysis. Reopen mer before checking this request. The recorded source remains available.";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { problem = "Source could not be checked. No new rehearsal result is accepted. " + error.Message; }
        return false;
    }

    private static bool SameRoute(CommandMatch first, CommandMatch second) =>
        first.Identity.Equals(second.Identity, StringComparison.OrdinalIgnoreCase) && first.Package == second.Package;

    private void UpdateDetails()
    {
        foreach (var update in updates.Values)
        {
            Print(new LocalInspection(update.SourceLine, update.Discovered, update.Discovery).Details());
            if (update.Problem is { } problem) foreach (var line in problem.Lines) Say(line.Text, line.Runs[0].Ink);
            foreach (var result in update.Results.Values)
            {
                Say($"Update rehearsal recorded {result.Created:dd MMM yyyy HH:mm:ss zzz} / source line {update.SourceLine}", Ink.Signal);
                renderer.UpdateRehearsal(result.Report, embedded: true, sourcePath: path);
            }
        }
    }

    private sealed class SourceCheckedRunner(ICommandRunner inner, Func<string?> check, CancellationToken cancellation) : ICommandRunner
    {
        public CommandResult Run(CommandRequest request)
        {
            if (cancellation.IsCancellationRequested) return new(130, "", "", "Stopped before starting the next query.");
            if (check() is { } problem) return new(1, "", "", problem);
            return inner.Run(request);
        }
    }
}
