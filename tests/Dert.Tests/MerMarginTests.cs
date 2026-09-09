using Dert.Mer;
using Xunit;

namespace Dert.Tests;

public class MerMarginTests
{
    [Theory]
    [InlineData(110, 28)]
    [InlineData(80, 24)]
    public void Settled_work_retains_its_result_beneath_the_current_evidence(int columns, int rows)
    {
        var journal = Example();
        var frame = MerCompositor.Live(journal.Snapshot(TimeSpan.FromSeconds(2)), columns, rows, true);
        Assert.Contains("436 bytes retained.", Text(frame));
        Assert.Contains("Three entries have context.", Text(frame));
        Assert.Contains("Check boundaries", Text(frame));
        Assert.Contains("Two marks short.", Text(frame));
        Assert.Contains("Settled work", Text(frame));
    }

    [Fact]
    public void Waiting_does_not_reanimate_old_evidence_or_manufacture_progress()
    {
        var journal = Example();
        var settled = MerCompositor.Live(journal.Snapshot(TimeSpan.FromSeconds(3)), 110, 28);
        Assert.Equal(Text(settled), Text(MerCompositor.Live(journal.Snapshot(TimeSpan.FromSeconds(20)), 110, 28)));
        Assert.DoesNotContain("100%", Text(settled));
        Assert.Contains("working", Text(settled));
    }

    [Fact]
    public void A_new_stage_takes_the_same_margin_position_without_moving_the_source()
    {
        var journal = Example();
        var before = MerCompositor.Live(journal.Snapshot(TimeSpan.FromSeconds(3)), 110, 28, true);
        journal.Apply(new SettleStage("lint", StageState.Attention, "Two marks short."), TimeSpan.FromSeconds(3));
        journal.Apply(new StartStage("repair"), TimeSpan.FromSeconds(3));
        var after = MerCompositor.Live(journal.Snapshot(TimeSpan.FromSeconds(3)), 110, 28, true);
        Assert.Equal(Row(before, "Check boundaries"), Row(after, "Rehearse title"));
        Assert.Equal(Row(before, "// WEEKEND"), Row(after, "// WEEKEND"));
        Assert.Contains("Two marks short.", Text(after));
    }

    [Fact]
    public void Failed_work_keeps_its_cause_and_stops_decorative_motion_immediately()
    {
        var journal = Example();
        journal.Apply(new SettleStage("lint", StageState.Failed, "The source changed. No repair was applied."), TimeSpan.FromSeconds(2));
        var early = MerCompositor.Live(journal.Snapshot(TimeSpan.FromSeconds(2)), 80, 24);
        var late = MerCompositor.Live(journal.Snapshot(TimeSpan.FromSeconds(20)), 80, 24);
        Assert.Equal(Text(early), Text(late));
        Assert.Contains("The source changed. No repair was applied.", Text(early));
        Assert.Contains("overall failed", Text(early));
    }

    [Fact]
    public void Long_settled_explanations_keep_their_beginning_and_disclose_omissions()
    {
        var journal = Example();
        const string detail = "No repair was applied. " +
            "The source changed before the selected boundary could be checked against the bytes read from the file.";
        journal.Apply(new SettleStage("lint", StageState.Attention, detail), TimeSpan.FromSeconds(2));
        journal.Apply(new StartStage("repair"), TimeSpan.FromSeconds(2));
        var frame = MerCompositor.Live(journal.Snapshot(TimeSpan.FromSeconds(3)), 80, 24, true);
        Assert.Contains("No repair was applied.", Text(frame));
        Assert.Contains("…", Text(frame));
        Assert.Contains(detail, string.Join('\n', MerCompositor.Receipt(journal.Snapshot(TimeSpan.FromSeconds(3))).Select(line => line.Text)));
    }

    [Fact]
    public void Starting_the_next_check_keeps_the_last_observed_source_in_view()
    {
        var journal = Example();
        journal.Apply(new SetSource(new("weekend.DERT", [InkLine.Plain("title"), InkLine.Plain("opener"),
            InkLine.Plain("selected entry"), InkLine.Plain("closer")], [1, 8, 9, 10])), TimeSpan.FromSeconds(2));
        journal.Apply(new AddEvidence("lint", new("Selected", "Current grouping", SourceLine: 9, OwnerLine: 8)), TimeSpan.FromSeconds(2));
        var before = MerCompositor.Live(journal.Snapshot(TimeSpan.FromSeconds(3)), 110, 28, true);
        journal.Apply(new SettleStage("lint", StageState.Attention, "Ending unresolved."), TimeSpan.FromSeconds(3));
        journal.Apply(new StartStage("repair"), TimeSpan.FromSeconds(3));
        var after = MerCompositor.Live(journal.Snapshot(TimeSpan.FromSeconds(3)), 110, 28, true);
        Assert.True(Row(before, "selected entry") >= 0);
        Assert.Equal(Row(before, "selected entry"), Row(after, "selected entry"));
        Assert.Equal(Row(before, "opener"), Row(after, "opener"));
    }

    private static MotionJournal Example()
    {
        var journal = new MotionJournal(new("Look inside your notes", "Read only / no source edits", [
            new("read", "Read source", "Read the source text."), new("context", "Trace context", "Find each entry's note."),
            new("lint", "Check boundaries", "Check the supported rules."), new("repair", "Rehearse title", "Measure the title.")],
            new("weekend.DERT", [InkLine.Plain("  1  // WEEKEND"), InkLine.Plain("  2  // -----")], [1, 2])), DateTimeOffset.UnixEpoch);
        journal.Apply(new StartStage("read"), TimeSpan.Zero);
        journal.Apply(new SettleStage("read", StageState.Passed, "436 bytes retained."), TimeSpan.Zero);
        journal.Apply(new StartStage("context"), TimeSpan.Zero);
        journal.Apply(new SettleStage("context", StageState.Passed, "Three entries have context."), TimeSpan.FromSeconds(1));
        journal.Apply(new StartStage("lint"), TimeSpan.FromSeconds(1));
        journal.Apply(new AddEvidence("lint", new("Title rule / line 2", "Two marks short.", SourceLine: 2)), TimeSpan.FromSeconds(1));
        return journal;
    }

    private static int Row(MerFrame frame, string value) => frame.Lines.ToList().FindIndex(line => line.Text.Contains(value, StringComparison.Ordinal));
    private static string Text(MerFrame frame) => string.Join('\n', frame.Lines.Select(line => line.Text));
}
