using Erpdert.Mer;
using Xunit;

namespace Dert.Tests;

public class MerSessionTests
{
    [Fact]
    public void Still_live_output_reports_new_events_without_a_timer()
    {
        var output = new StringWriter();
        using var session = new MotionSession(new("Operation", "Read-only", [new("one", "Check package", "Waiting for the registry")]),
            output, () => new(80, 24), animate: false, live: true);
        session.Publish(new StartStage("one"));
        Assert.Contains("Waiting for the registry", output.ToString());
        session.Publish(new AddEvidence("one", new("Observed", "Package confirmed")));
        Assert.Contains("Package confirmed", output.ToString());
        Assert.DoesNotContain("0.0s", output.ToString());
    }
    private static MotionScore Score() => new("A real check", "No edits", [new("read", "Read my notes", "Reading this file")]);

    [Fact]
    public void A_static_session_emits_one_complete_receipt_without_cursor_controls()
    {
        var output = new StringWriter();
        using (var session = new MotionSession(Score(), output, () => new(80, 24)))
        {
            session.Publish(new StartStage("read"));
            session.Publish(new AddEvidence("read", new("observed", "42 bytes")));
            session.Publish(new SettleStage("read", StageState.Passed, "Read the file."));
            session.Publish(new FinishMotion(MotionOutcome.Complete, "Source unchanged."));
        }
        Assert.Contains("42 bytes", output.ToString());
        Assert.Equal(1, output.ToString().Split("ERPDERT").Length - 1);
        Assert.DoesNotContain('\u001b', output.ToString());
    }

    [Fact]
    public void Disposing_unfinished_work_leaves_an_interrupted_receipt_not_a_spinning_lie()
    {
        var output = new StringWriter();
        using (var session = new MotionSession(Score(), output, () => new(80, 24)))
            session.Publish(new StartStage("read"));
        Assert.Contains("INTERRUPTED", output.ToString());
        Assert.DoesNotContain("/ checked", output.ToString());
    }

    [Fact]
    public void Fast_live_work_does_not_wait_for_an_animation_to_finish()
    {
        var output = new StringWriter();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        using (var session = new MotionSession(Score(), output, () => new(80, 24), animate: true))
        {
            session.Publish(new StartStage("read"));
            session.Publish(new SettleStage("read", StageState.Passed, "Read."));
            session.Publish(new FinishMotion(MotionOutcome.Complete, "Done."));
        }
        Assert.Contains("Done.", output.ToString());
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), "No minimum display duration or ceremonial wait is allowed.");
        var screen = new MerTestScreen();
        screen.Feed(output.ToString());
        Assert.True(screen.CursorVisible);
        Assert.Contains("COMPLETE", screen.Text);
        Assert.DoesNotContain("unsettled", screen.Text);
    }
}
