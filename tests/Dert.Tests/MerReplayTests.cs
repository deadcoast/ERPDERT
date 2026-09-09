using Erpdert.Mer;
using Xunit;

namespace Dert.Tests;

public class MerReplayTests
{
    [Fact]
    public void Restarting_step_playback_resets_the_old_clock_even_when_its_first_beat_was_already_selected()
    {
        var score = new MotionScore("Notes", "No writes", [new("one", "Read", "Reading")]);
        MotionEvent[] events = [new StartStage("one"), new AddEvidence("one", new("Value", "Kept")),
            new SettleStage("one", StageState.Passed, "Read."), new FinishMotion(MotionOutcome.Complete, "Done.")];
        var replay = new ReplaySequence(score, events);
        replay.Seek(2, TimeSpan.FromSeconds(10));
        var before = replay.Receipt().Select(line => line.Text).ToArray();
        var assembly = typeof(Erpdert.Cli.CliApplication).Assembly;
        var analysis = Activator.CreateInstance(assembly.GetType("Erpdert.Cli.Motion.NoteScore")!, score, events, 0);
        var writer = new StringWriter();
        var console = Spectre.Console.AnsiConsole.Create(new Spectre.Console.AnsiConsoleSettings
        {
            Ansi = Spectre.Console.AnsiSupport.No,
            Interactive = Spectre.Console.InteractionSupport.No,
            Out = new Spectre.Console.AnsiConsoleOutput(writer)
        });
        var play = assembly.GetType("Erpdert.Cli.Motion.ReplayPlayer")!.GetMethod("Play", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        Assert.True(Assert.IsType<bool>(play.Invoke(null, [analysis, console, true, true, CancellationToken.None,
            (Func<string?>)(() => ""), null, true, replay, 3, null])));
        Assert.Equal(before, replay.Receipt().Select(line => line.Text));
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, null)]
    public void Initial_selection_matches_the_held_connection_but_does_not_preselect_over_an_error(bool error, int? selected)
    {
        var replay = new ReplaySequence(new("Notes", "No writes", [new("one", "Read", "Reading")],
            new("note.DERT", [InkLine.Plain("  1  // plans ; friends ;;"), InkLine.Plain("  2  // food")], [1, 2])), [
            new StartStage("one"), new AddEvidence("one", new("Entry", "food", EvidenceKind.Source, 2, 1,
                new NoteConnectionFocus("plans", "food", null, null))),
            new AddEvidence("one", new("Check", "This ending needs a choice.", error ? EvidenceKind.Error : EvidenceKind.Text)),
            new SettleStage("one", error ? StageState.Attention : StageState.Passed, "Read."),
            new FinishMotion(error ? MotionOutcome.Attention : MotionOutcome.Complete, "Finished.")]);
        Assert.Equal(selected, replay.InitialInspectionLine);
    }

    [Fact]
    public void A_rejected_seek_does_not_consume_the_next_event()
    {
        var replay = new ReplaySequence(new("Example", "Read-only", [new("one", "Read", "Reading")]), [
            new StartStage("one"), new AddEvidence("one", new("Value", "Retain this evidence")),
            new SettleStage("one", StageState.Passed, "Checked."), new FinishMotion(MotionOutcome.Complete, "Finished.")]);
        replay.Seek(1, TimeSpan.FromSeconds(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => replay.Seek(2, TimeSpan.Zero));
        Assert.Equal(1, replay.Position);
        replay.Seek(2, TimeSpan.FromSeconds(2));
        Assert.Single(replay.Snapshot(TimeSpan.FromSeconds(2)).Stages[0].Evidence);
    }

    [Fact]
    public void Stopping_playback_retains_all_completed_analysis_and_later_findings()
    {
        var replay = new ReplaySequence(new("Example", "No writes", [new("one", "Read", "Reading")]), [
            new StartStage("one"), new AddEvidence("one", new("Finding", "This recorded finding matters.")),
            new SettleStage("one", StageState.Attention, "Needs a choice."),
            new FinishMotion(MotionOutcome.Attention, "Analysis finished.")]);
        replay.Seek(1, TimeSpan.Zero);
        var receipt = string.Join('\n', replay.Receipt(stopped: true).Select(line => line.Text));
        Assert.Contains("Replay stopped", receipt);
        Assert.Contains("This recorded finding matters.", receipt);
        Assert.Contains("Read / attention", receipt);
        Assert.DoesNotContain("Read / interrupted", receipt);
        Assert.Contains("REPLAY", receipt);
    }

    [Fact]
    public void Stepping_back_restores_the_recorded_view_without_repeating_the_operation()
    {
        var replay = new ReplaySequence(new("Example", "Read-only", [new("one", "Read", "Reading")]), [
            new StartStage("one"), new AddEvidence("one", new("Value", "Recorded value")),
            new SettleStage("one", StageState.Passed, "Checked."), new FinishMotion(MotionOutcome.Complete, "Finished.")]);
        replay.Seek(3, TimeSpan.FromSeconds(1));
        Assert.Equal(StageState.Passed, replay.Snapshot(TimeSpan.FromSeconds(1)).Stages[0].State);
        replay.Seek(1, TimeSpan.FromSeconds(2));
        Assert.Equal(StageState.Working, replay.Snapshot(TimeSpan.FromSeconds(2)).Stages[0].State);
        Assert.Empty(replay.Snapshot(TimeSpan.FromSeconds(2)).Stages[0].Evidence);
        Assert.Contains(replay.Receipt(), line => line.Text.Contains("Recorded value", StringComparison.Ordinal));
    }
}
