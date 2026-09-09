namespace Dert.Mer;

public sealed class ReplaySequence
{
    private readonly MotionScore score;
    private readonly MotionEvent[] events;
    private readonly DateTimeOffset created = DateTimeOffset.Now;
    private readonly MotionSnapshot complete;
    private MotionJournal journal;
    public int Position { get; private set; }
    public int Count => events.Length;

    public ReplaySequence(MotionScore score, IReadOnlyList<MotionEvent> events)
    {
        var validation = new MotionJournal(score with { Replay = true }, created);
        this.score = validation.Snapshot(TimeSpan.Zero).Score;
        this.events = events.Select(change => change is SetSource source
            ? new SetSource(source.Source with
            {
                Lines = Array.AsReadOnly(source.Source.Lines.ToArray()),
                ContentLines = source.Source.ContentLines is { } raw ? Array.AsReadOnly(raw.ToArray()) : null,
                LineNumbers = source.Source.LineNumbers is { } numbers ? Array.AsReadOnly(numbers.ToArray()) : null
            }) : change).ToArray();
        foreach (var change in this.events) validation.Apply(change, TimeSpan.Zero);
        complete = validation.Snapshot(TimeSpan.Zero);
        if (complete.Outcome is null) throw new ArgumentException("A replay needs a completed recorded result.", nameof(events));
        journal = new(this.score, created);
    }

    public void Seek(int position, TimeSpan elapsed)
    {
        if (position < 0 || position > Count) throw new ArgumentOutOfRangeException(nameof(position));
        if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
        if (position < Position) { journal = new(score, created); Position = 0; }
        while (Position < position)
        {
            journal.Apply(events[Position], elapsed);
            Position++;
        }
    }

    public MotionSnapshot Snapshot(TimeSpan elapsed) => journal.Snapshot(elapsed);
    public MotionSnapshot Recorded => complete;
    public MerFrame Hold(int columns, int rows) => MerCompositor.Hold(complete, columns, rows);
    public int? InitialInspectionLine => complete.Outcome is MotionOutcome.Failed or MotionOutcome.Interrupted ||
        complete.Stages.Any(stage => stage.State is StageState.Failed or StageState.Interrupted || stage.Evidence.Any(evidence => evidence.Kind == EvidenceKind.Error))
        ? null : complete.Stages.Reverse().SelectMany(stage => stage.Evidence.Reverse()).FirstOrDefault(evidence => evidence.Focus is not null)
            is { Focus: NoteConnectionFocus, SourceLine: int line } && InspectionLines.Contains(line) ? line : null;
    public IReadOnlyList<int> InspectionLines => Array.AsReadOnly(complete.Stages.SelectMany(stage => stage.Evidence)
        .Where(evidence => evidence.Focus is NoteConnectionFocus && evidence.SourceLine is not null)
        .Select(evidence => evidence.SourceLine!.Value).Distinct().Order().ToArray());
    public MerFrame? Inspect(int sourceLine, int columns, int rows) => MerCompositor.Inspect(complete, sourceLine, columns, rows);
    public MerFrame? Source(int sourceLine, int columns) => MerCompositor.Source(complete, sourceLine, columns);
    public IReadOnlyList<InkLine> Receipt(bool stopped = false, int columns = 110) => stopped
        ? [InkLine.Plain("Replay stopped / the analysis already finished. Its full recorded result follows.", Ink.Copper), .. MerCompositor.Receipt(complete, columns)]
        : MerCompositor.Receipt(complete, columns);
}
