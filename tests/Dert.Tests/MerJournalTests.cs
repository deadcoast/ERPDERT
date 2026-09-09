using Dert.Mer;
using Xunit;

namespace Dert.Tests;

public class MerJournalTests
{
    private static MotionJournal Journal() => new(new("Read my notes", "No edits", [new("read", "Read", "Read this file"), new("lint", "Lint", "Check its structure")]), DateTimeOffset.UnixEpoch);

    [Fact]
    public void Abandoning_every_waiting_check_cannot_produce_a_clean_receipt()
    {
        var journal = Journal();
        journal.Apply(new FinishMotion(MotionOutcome.Complete, "Done."), TimeSpan.Zero);
        Assert.NotEqual(MotionOutcome.Complete, journal.Snapshot(TimeSpan.Zero).Outcome);
    }

    [Fact]
    public void Source_arriving_after_a_read_is_snapshotted_without_changing_earlier_frames()
    {
        var journal = Journal();
        var before = journal.Snapshot(TimeSpan.Zero);
        journal.Apply(new SetSource(new("notes.DERT", [InkLine.Plain("// hello")], [1])), TimeSpan.Zero);
        Assert.Null(before.Score.Source);
        Assert.Equal("// hello", Assert.Single(journal.Snapshot(TimeSpan.Zero).Score.Source!.Lines).Text);
    }

    [Fact]
    public void Time_alone_cannot_complete_a_check()
    {
        var journal = Journal();
        journal.Apply(new StartStage("read"), TimeSpan.Zero);
        var later = journal.Snapshot(TimeSpan.FromHours(1));
        Assert.Equal(StageState.Working, later.Stages[0].State);
        Assert.Equal(StageState.Waiting, later.Stages[1].State);
        Assert.Null(later.Outcome);
    }

    [Fact]
    public void Evidence_remains_attached_to_its_observed_stage_and_prior_snapshots_do_not_mutate()
    {
        var journal = Journal();
        journal.Apply(new StartStage("read"), TimeSpan.Zero);
        var before = journal.Snapshot(TimeSpan.Zero);
        journal.Apply(new AddEvidence("read", new("bytes", "42")), TimeSpan.FromSeconds(1));
        journal.Apply(new SettleStage("read", StageState.Passed, "Read 42 bytes."), TimeSpan.FromSeconds(2));
        var after = journal.Snapshot(TimeSpan.FromSeconds(3));
        Assert.Empty(before.Stages[0].Evidence);
        Assert.Equal("42", Assert.Single(after.Stages[0].Evidence).Text);
        Assert.Empty(after.Stages[1].Evidence);
        Assert.Equal(TimeSpan.FromSeconds(2), after.Stages[0].Settled);
    }

    [Fact]
    public void Finishing_does_not_promote_unfinished_work_to_success()
    {
        var journal = Journal();
        journal.Apply(new StartStage("read"), TimeSpan.Zero);
        journal.Apply(new FinishMotion(MotionOutcome.Complete, "Finished."), TimeSpan.FromSeconds(1));
        var snapshot = journal.Snapshot(TimeSpan.FromSeconds(50));
        Assert.Equal(MotionOutcome.Interrupted, snapshot.Outcome);
        Assert.Equal(StageState.Interrupted, snapshot.Stages[0].State);
        Assert.Equal(StageState.Skipped, snapshot.Stages[1].State);
        Assert.Equal(TimeSpan.FromSeconds(1), snapshot.Elapsed);
    }

    [Fact]
    public void A_failed_check_prevents_a_clean_overall_receipt()
    {
        var journal = Journal();
        journal.Apply(new StartStage("read"), TimeSpan.Zero);
        journal.Apply(new SettleStage("read", StageState.Failed, "Access denied."), TimeSpan.FromSeconds(1));
        journal.Apply(new FinishMotion(MotionOutcome.Complete, "File unchanged."), TimeSpan.FromSeconds(1));
        Assert.Equal(MotionOutcome.Failed, journal.Snapshot(TimeSpan.FromSeconds(1)).Outcome);
    }

    [Fact]
    public void Unknown_stages_and_rewriting_a_settled_result_are_programming_errors()
    {
        var journal = Journal();
        Assert.Throws<ArgumentException>(() => journal.Apply(new StartStage("missing"), TimeSpan.Zero));
        journal.Apply(new StartStage("read"), TimeSpan.Zero);
        journal.Apply(new SettleStage("read", StageState.Passed, "Read."), TimeSpan.FromSeconds(1));
        Assert.Throws<InvalidOperationException>(() => journal.Apply(new StartStage("read"), TimeSpan.FromSeconds(2)));
    }
}
