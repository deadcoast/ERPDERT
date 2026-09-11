using Dert.Cli.Discovery;
using Dert.Cli.Updates;
using Dert.Mer;

namespace Dert.Cli.Motion;

internal sealed record RecordedRehearsal(DateTimeOffset Created, UpdateReport Report, MotionSnapshot Motion);

internal sealed class UpdateConnection(int sourceLine, DiscoveryReport discovery, DateTimeOffset discovered)
{
    internal int SourceLine { get; } = sourceLine;
    internal DiscoveryReport Discovery { get; } = discovery;
    internal DateTimeOffset Discovered { get; } = discovered;
    internal int Copy { get; private set; }
    internal int CopyCount => Discovery.Installations.Count;
    internal bool Chosen { get; private set; } = discovery.Installations.Count == 1 && discovery.Issues.Count == 0;
    internal CommandMatch? Route => CopyCount == 0 ? null : Discovery.Installations[Copy][0];
    internal Dictionary<string, RecordedRehearsal> Results { get; } = new(StringComparer.OrdinalIgnoreCase);
    internal DocumentAnnotation? Problem { get; set; }
    internal bool FailedAttempt { get; set; }
    internal RecordedRehearsal? Result => Route is { } route ? Results.GetValueOrDefault(route.Path) : null;

    internal bool Select(int number)
    {
        if (number < 1 || number > CopyCount) return false;
        Copy = number - 1; Chosen = true; Problem = null;
        return true;
    }

    internal DocumentAnnotation Annotation(MotionSnapshot? running = null)
    {
        if (Problem is not null && running is null) return Problem;
        if (Route is not { } route)
            return new(SourceLine, [InkLine.Plain("No Codex copy found", Ink.Copper),
                InkLine.Plain("An update needs an installed copy."), InkLine.Plain("Discovery issues and paths in details.", Ink.Muted)])
            { Action = new("Update rehearsal", StageState.Attention, "no npm queries") };
        var result = running is null ? Result : null;
        var motion = running ?? result?.Motion;
        var report = result?.Report;
        var reportedDiscovery = report?.Discovery ?? Discovery;
        var reportedRoute = report?.Selected ?? route;
        var active = motion?.Stages.FirstOrDefault(stage => stage.State == StageState.Working);
        var state = running is not null ? StageState.Working : report is null ? StageState.Waiting : report.State switch
        {
            UpdateState.Ready or UpdateState.Current => report.NeedsAttention || reportedDiscovery.Issues.Count > 0 ? StageState.Attention : StageState.Passed,
            UpdateState.Interrupted => StageState.Interrupted,
            UpdateState.Ahead or UpdateState.ChooseCopy => StageState.Attention,
            _ => StageState.Failed
        };
        var heading = active?.Stage.Title ?? (running is not null ? "Reading rehearsal evidence" : report?.State switch
        {
            UpdateState.Ready => "Rehearsed / not applied",
            UpdateState.Current => "Already current",
            UpdateState.Ahead => "Ahead of latest / no downgrade",
            UpdateState.Interrupted => "Rehearsal stopped / interrupted",
            UpdateState.Unavailable => "Rehearsal stopped",
            _ => "Choose the copy to rehearse"
        });
        var ink = state is StageState.Attention or StageState.Failed or StageState.Interrupted ? Ink.Copper : state == StageState.Passed ? Ink.Good : Ink.Signal;
        var time = motion?.Created ?? Discovered;
        var copy = $"Copy {Copy + 1} of {CopyCount} / {Path.GetFileName(route.Path)}";
        var version = (route.Package?.Version ?? "unidentified") + (report?.Available is { } available ? " -> " + available : " / installed metadata");
        var lines = new List<InkLine> { InkLine.Plain(heading, ink), InkLine.Plain($"Recorded {time:HH:mm:ss zzz}", Ink.Muted),
            InkLine.Plain(copy), InkLine.Plain("@openai/codex / " + version, Ink.Signal) };
        if (motion is null)
        {
            lines.Add(InkLine.Plain("  |-- " + (route.Package?.NodePath is null ? "Node not identified" : "Node found"), Ink.Muted));
            lines.Add(InkLine.Plain("  '-- " + (route.Package?.NpmPath is null ? "npm not identified" : "npm nearby / inventory unverified"), Ink.Muted));
            lines.Add(InkLine.Plain("Location / " + (Path.GetDirectoryName(route.Path) ?? route.Path), Ink.Muted));
            lines.Add(InkLine.Plain(Chosen ? "check queries npm and the registry" : "copy <number> selects this exact route", Ink.Copper));
            lines.Add(InkLine.Plain("npm may write cache and logs.", Ink.Muted));
            lines.Add(InkLine.Plain("No package update or scripts run.", Ink.Muted));
        }
        else
        {
            if (Passed("Prefix")) lines.Add(InkLine.Plain("  |-- exact target folder matched", Ink.Muted));
            if (Passed("Inventory")) lines.Add(InkLine.Plain("  |-- npm inventory confirmed", Ink.Signal));
            if (Passed("Release")) lines.Add(InkLine.Plain("  |-- registry latest resolved", Ink.Muted));
            if (Passed("DryRun")) lines.Add(InkLine.Plain("  '-- dependency dry run accepted", Ink.Signal));
            if (active is not null) lines.Add(InkLine.Plain("  '-- " + active.Stage.Purpose, Ink.Copper));
            if (report?.State is UpdateState.Current or UpdateState.Ahead) lines.Add(InkLine.Plain("No install dry run was needed.", Ink.Muted));
            if (report?.State == UpdateState.Ready)
            {
                lines.Add(InkLine.Plain(report.Checks.LastOrDefault()?.Detail ?? "Dry run accepted.", Ink.Muted));
            }
            if (report?.Issues.FirstOrDefault() is { } issue)
            { lines.Add(InkLine.Plain(issue.Code, Ink.Copper)); lines.Add(InkLine.Plain(issue.Explanation)); }
            lines.Add(InkLine.Plain(report?.ProposalPath is not null ? "details: evidence and saved approval." : "details keeps the full evidence.",
                report?.ProposalPath is not null ? Ink.Signal : Ink.Muted));
            lines.Add(InkLine.Plain("No update applied / cache may change", Ink.Muted));
        }
        var issues = reportedDiscovery.Issues.Count + reportedRoute.Issues.Count + (report?.Issues.Count ?? 0);
        if (issues > 0) lines.Insert(3, InkLine.Plain($"{issues} " + (issues == 1 ? "issue" : "issues") + " / details", Ink.Copper));
        var compact = new[] { InkLine.Plain(heading, ink), InkLine.Plain(copy), InkLine.Plain(version, Ink.Signal),
            InkLine.Plain(issues > 0 ? $"{issues} issues / details" : motion is null ? Chosen ? "check queries npm and the registry" : "Choose with copy <number>" :
                Passed("Inventory") ? "npm inventory confirmed" : "Inventory not confirmed", issues > 0 ? Ink.Copper : Ink.Muted),
            InkLine.Plain(motion is null ? "Cache/logs may change; no update runs." : report?.ProposalPath is not null
                ? "details: saved approval / not applied" : "details: evidence / no update applied", Ink.Muted) };
        return new(SourceLine, lines, compact) { Action = new("Update rehearsal", state, motion is null ? "no npm queries" : "no update applied") };

        bool Passed(string id) => motion?.Stages.Any(stage => stage.Stage.Id == id && stage.State == StageState.Passed) == true;
    }
}
