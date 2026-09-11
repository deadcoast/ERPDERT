using Dert.Core;
using Dert.Cli.Motion;
using Dert.Mer;
using Spectre.Console;

namespace Dert.Cli.Results;

internal static class ResultPresentation
{
    internal static DocumentAnnotation Annotation(ResultNotePlan plan, int line) => new(line,
    [
        InkLine.Plain("RESULT TO NOTE", Ink.Signal),
        InkLine.Plain(plan.Observation.Outcome + " / recorded observation", Ink.Text),
        InkLine.Plain("Source / " + plan.Comparison.State, Ink.Copper),
        InkLine.Plain(plan.IsReady ? "Add one folded result note" : "Insertion needs attention", Ink.Text),
        InkLine.Plain("History / no current-state check", Ink.Muted),
        InkLine.Plain("Review exact text in Notes", Ink.Muted)
    ]) { CompactRowBudget = 6, Action = new("Result review", StageState.Waiting, "source unchanged") };

    internal static void Draw(ResultNotePlan plan, string path, IAnsiConsole console, bool plain)
    {
        var analysis = NoteScore.Analyze(() => plan.ProposedText, path, false,
            sourceState: plan.IsReady ? DocumentSourceState.Proposed : DocumentSourceState.Original);
        var journal = new MotionJournal(analysis.Score, DateTimeOffset.Now);
        foreach (var item in analysis.Events) journal.Apply(item, TimeSpan.Zero);
        var line = plan.IsReady ? new SourceText(plan.ProposedText).Lines.First(item => item.Start <= plan.AddedSpan.Start && item.End > plan.AddedSpan.Start).Number
            : Math.Clamp(plan.Comparison.Line ?? 1, 1, analysis.Document!.Source.Lines.Count);
        using var terminal = new InlineTerminal(console.Profile.Out.Writer, () => MerHost.Size(console), MerHost.Colors(console, plain));
        var size = MerHost.Size(console);
        terminal.Finish(MerCompositor.Document(journal.Snapshot(TimeSpan.Zero), size.Columns, size.Rows, line, Annotation(plan, line)).Lines);
    }
}
