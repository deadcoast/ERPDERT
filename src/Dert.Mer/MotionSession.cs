using System.Diagnostics;

namespace Dert.Mer;

public sealed class MotionSession : IDisposable
{
    private readonly object gate = new();
    private readonly MotionJournal journal;
    private readonly InlineTerminal terminal;
    private readonly Func<TerminalSize> measure;
    private readonly bool animate;
    private readonly bool live;
    private readonly bool detailed;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly CancellationTokenSource stopping = new();
    private readonly Task pump;
    private bool finished;
    private bool disposed;
    private MerOutputException? outputFailure;

    public MotionSession(MotionScore score, TextWriter output, Func<TerminalSize> measure,
        MerColorMode colors = MerColorMode.None, bool animate = false, bool? live = null, bool detailed = false)
    {
        journal = new(score, DateTimeOffset.Now);
        terminal = new(output, measure, colors);
        this.measure = measure;
        this.animate = animate;
        this.live = live ?? animate;
        this.detailed = detailed;
        pump = animate ? Task.Run(Pump) : Task.CompletedTask;
    }

    public void Publish(MotionEvent change)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (outputFailure is not null) throw outputFailure;
            journal.Apply(change, clock.Elapsed);
            try
            {
                if (change is FinishMotion)
                {
                    finished = true;
                    stopping.Cancel();
                    var snapshot = journal.Snapshot(clock.Elapsed);
                    var size = measure();
                    terminal.Finish(!detailed && snapshot.Score.View == MotionView.Document
                        ? MerCompositor.Document(snapshot, size.Columns, size.Rows).Lines : MerCompositor.Receipt(snapshot, size.Columns));
                }
                else if (live) Draw();
            }
            catch (MerOutputException error)
            {
                outputFailure = error;
                stopping.Cancel();
                throw;
            }
        }
    }

    private async Task Pump()
    {
        try
        {
            while (true)
            {
                await Task.Delay(40, stopping.Token).ConfigureAwait(false);
                lock (gate)
                {
                    if (finished || disposed) return;
                    if (journal.Snapshot(clock.Elapsed).Stages.Any(stage => stage.State == StageState.Working)) Draw();
                }
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
        catch (MerOutputException error) { lock (gate) outputFailure = error; }
    }

    public MotionSnapshot Snapshot() { lock (gate) return journal.Snapshot(clock.Elapsed); }

    private void Draw()
    {
        var size = measure();
        if (size.Columns < 4 || size.Rows < 4) return;
        terminal.Draw(MerCompositor.Live(journal.Snapshot(clock.Elapsed), size.Columns, size.Rows, still: !animate));
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            stopping.Cancel();
            try
            {
                if (!finished && outputFailure is null)
                {
                    journal.Apply(new FinishMotion(MotionOutcome.Interrupted, "Stopped before completion. No success is assumed."), clock.Elapsed);
                    terminal.Finish(MerCompositor.Receipt(journal.Snapshot(clock.Elapsed)));
                }
            }
            catch (MerOutputException) { /* Do not mask the operation's exception during best-effort teardown. */ }
            finally { terminal.Dispose(); }
        }
        try { pump.GetAwaiter().GetResult(); }
        finally { stopping.Dispose(); }
    }
}
