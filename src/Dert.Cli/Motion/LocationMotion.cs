using System.Diagnostics;
using Dert.Cli.Applications;
using Dert.Cli.Discovery;
using Dert.Mer;
using Spectre.Console;

namespace Dert.Cli.Motion;

internal static class LocationMotion
{
    internal static ApplicationLocationReport Scan(ApplicationDefinition app, DiscoveryEnvironment environment,
        IAnsiConsole console, bool plain, bool still, CancellationToken cancellation)
    {
        var watch = Stopwatch.StartNew();
        var score = new MotionScore(app.DisplayName + " / where", "Local locations and observed file lengths", [
            new("Locate", "Locate the application", "Read command routes, registration and related folders.")]);
        var journal = new MotionJournal(score, DateTimeOffset.Now);
        journal.Apply(new StartStage("Locate"), watch.Elapsed);
        using var terminal = new InlineTerminal(console.Profile.Out.Writer, () => MerHost.Size(console), MerHost.Colors(console, plain));
        try
        {
            var report = ApplicationLocator.Scan(app, environment, cancellation, activity =>
            {
                journal.Apply(new AddEvidence("Locate", new(activity.Phase.ToString(), activity.Detail,
                    activity.Path is null ? EvidenceKind.Text : EvidenceKind.Path)), watch.Elapsed);
                if (activity.Completed || !MerHost.CanAnimate(console, plain, still)) return;
                var size = MerHost.Size(console);
                if (size.Columns < 4 || size.Rows < 4) return;
                terminal.Draw(LocationCompositor.Pending(app.DisplayName,
                    activity.Detail + (activity.Path is null ? "" : " / " + activity.Path), size.Columns, size.Rows));
            });
            var attention = new LocationConnection(0, report).NeedsAttention;
            journal.Apply(new SettleStage("Locate", attention ? StageState.Attention : StageState.Passed,
                report.Locations.Count + " locations observed"), watch.Elapsed);
            journal.Apply(new FinishMotion(attention ? MotionOutcome.Attention : MotionOutcome.Complete, "Local evidence recorded"), watch.Elapsed);
            terminal.Finish(LocationCompositor.Receipt(new LocationConnection(0, report).View(), MerHost.Size(console).Columns));
            return report;
        }
        catch (Exception failure) when (failure is not MerOutputException)
        {
            var stopped = cancellation.IsCancellationRequested;
            journal.Apply(new SettleStage("Locate", stopped ? StageState.Interrupted : StageState.Failed, "No completed location result"), watch.Elapsed);
            journal.Apply(new FinishMotion(stopped ? MotionOutcome.Interrupted : MotionOutcome.Failed, "Application location stopped"), watch.Elapsed);
            terminal.Finish([InkLine.Plain("Application location stopped / no completed result claimed", Ink.Copper)]);
            throw;
        }
    }
}
