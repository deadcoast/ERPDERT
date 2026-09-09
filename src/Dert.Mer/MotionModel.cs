namespace Dert.Mer;

public enum StageState { Waiting, Working, Passed, Attention, Failed, Skipped, Interrupted }
public enum MotionOutcome { Complete, Attention, Failed, Interrupted }
public enum EvidenceKind { Text, Source, Path, Removed, Added, Error }
public enum MotionView { Evidence, Document }
public enum DocumentSourceState { Original, BeforeEdit, Proposed, Saved, Restored, Unverified }

public sealed record MotionStage(string Id, string Title, string Purpose);
public sealed record MotionSource(string Label, IReadOnlyList<InkLine> Lines, IReadOnlyList<int>? LineNumbers = null)
{
    public IReadOnlyList<InkLine>? ContentLines { get; init; }
    public DocumentSourceState State { get; init; }
}
public sealed record MotionScore(string Title, string Scope, IReadOnlyList<MotionStage> Stages,
    MotionSource? Source = null, bool Replay = false)
{
    public MotionView View { get; init; }
}
// These describe observed meaning, not strings the renderer has to reverse-parse.
public abstract record EvidenceFocus;
public sealed record NoteConnectionFocus(string Head, string Value, string? Annotation, int? ClosingLine) : EvidenceFocus
{
    public DocumentOwnership? Ownership { get; init; }
}
public sealed record TitleRuleFocus(string TitleSource, string Before, string After, int Width, int PreviousWidth, string Explanation) : EvidenceFocus;
public sealed record MotionEvidence(string Label, string Text, EvidenceKind Kind = EvidenceKind.Text, int? SourceLine = null, int? OwnerLine = null,
    EvidenceFocus? Focus = null);

// A separate recorded inspection can annotate authored source without rewriting
// its completed analysis journal or pretending it happened during replay.
public sealed record DocumentAnnotation(int SourceLine, IReadOnlyList<InkLine> Lines,
    IReadOnlyList<InkLine>? CompactLines = null)
{
    public DocumentAction? Action { get; init; }
    public DocumentOwnership? Ownership { get; init; }
}
public sealed record DocumentOwnership(int OpeningLine, int? ClosingLine,
    IReadOnlyList<int>? MemberLines = null, int? EndLine = null);
public sealed record DocumentAction(string Name, StageState State, string Effect);

public abstract record MotionEvent;
public sealed record SetSource(MotionSource Source) : MotionEvent;
public sealed record StartStage(string Id) : MotionEvent;
public sealed record AddEvidence(string Id, MotionEvidence Evidence) : MotionEvent;
public sealed record SettleStage(string Id, StageState State, string Detail) : MotionEvent;
public sealed record FinishMotion(MotionOutcome Outcome, string Detail) : MotionEvent;

public sealed record StageSnapshot(MotionStage Stage, StageState State, string Detail,
    TimeSpan? Started, TimeSpan? Settled, IReadOnlyList<MotionEvidence> Evidence, TimeSpan? EvidenceAt = null, TimeSpan? FocusAt = null);
public sealed record MotionSnapshot(MotionScore Score, DateTimeOffset Created,
    IReadOnlyList<StageSnapshot> Stages, TimeSpan Elapsed, MotionOutcome? Outcome, string Detail);

/// <summary>Reduces observed events. A clock cannot turn a check green.</summary>
public sealed class MotionJournal
{
    private MotionScore score;
    private readonly DateTimeOffset created;
    private readonly List<StageSnapshot> stages;
    private MotionOutcome? outcome;
    private string detail = "";
    private TimeSpan last;

    public MotionJournal(MotionScore score, DateTimeOffset created)
    {
        if (score.Stages.Count == 0 || score.Stages.Any(stage => string.IsNullOrWhiteSpace(stage.Id)) ||
            score.Stages.Select(stage => stage.Id).Distinct(StringComparer.Ordinal).Count() != score.Stages.Count)
            throw new ArgumentException("A score needs uniquely named stages.", nameof(score));
        this.score = score with
        {
            Stages = Array.AsReadOnly(score.Stages.ToArray()),
            Source = score.Source is { } source ? CopySource(source) : null
        };
        this.created = created;
        stages = score.Stages.Select(stage => new StageSnapshot(stage, StageState.Waiting, stage.Purpose, null, null, [])).ToList();
    }

    public void Apply(MotionEvent change, TimeSpan elapsed)
    {
        if (outcome is not null) throw new InvalidOperationException("This receipt has already settled.");
        if (elapsed < last) throw new ArgumentOutOfRangeException(nameof(elapsed), "Event time must not move backwards.");
        if (change is SetSource source)
            score = score with { Source = CopySource(source.Source) };
        else if (change is FinishMotion finish)
        {
            var abandoned = stages.Any(stage => stage.State == StageState.Waiting);
            for (var i = 0; i < stages.Count; i++)
                if (stages[i].State is StageState.Waiting or StageState.Working)
                    stages[i] = stages[i] with
                    {
                        State = stages[i].State == StageState.Waiting ? StageState.Skipped : StageState.Interrupted,
                        Detail = stages[i].State == StageState.Waiting ? "Not run." : "Stopped before this check could finish.",
                        Settled = elapsed
                    };
            outcome = stages.Any(stage => stage.State == StageState.Interrupted) ? MotionOutcome.Interrupted :
                stages.Any(stage => stage.State == StageState.Failed) ? MotionOutcome.Failed :
                (abandoned || stages.Any(stage => stage.State == StageState.Attention)) && finish.Outcome == MotionOutcome.Complete ? MotionOutcome.Attention : finish.Outcome;
            detail = finish.Detail;
        }
        else
        {
            var id = change switch
            {
                StartStage start => start.Id,
                SettleStage settle => settle.Id,
                AddEvidence evidence => evidence.Id,
                _ => throw new ArgumentException("Unknown motion event.", nameof(change))
            };
            var index = stages.FindIndex(stage => stage.Stage.Id == id);
            if (index < 0) throw new ArgumentException("Unknown stage: " + id, nameof(change));
            var current = stages[index];
            stages[index] = change switch
            {
                StartStage when current.State == StageState.Waiting => current with { State = StageState.Working, Started = elapsed },
                AddEvidence evidence when current.State == StageState.Working => current with
                {
                    Evidence = Array.AsReadOnly<MotionEvidence>([.. current.Evidence, evidence.Evidence]),
                    EvidenceAt = elapsed,
                    FocusAt = evidence.Evidence.Focus is null ? current.FocusAt : elapsed
                },
                SettleStage settle when current.State == StageState.Working && settle.State is StageState.Passed or StageState.Attention or StageState.Failed or StageState.Interrupted =>
                    current with { State = settle.State, Detail = settle.Detail, Settled = elapsed },
                SettleStage { State: StageState.Skipped } settle when current.State == StageState.Waiting =>
                    current with { State = StageState.Skipped, Detail = settle.Detail, Settled = elapsed },
                _ => throw new InvalidOperationException("This event would rewrite or bypass a stage's observed state.")
            };
        }
        last = elapsed;
    }

    public MotionSnapshot Snapshot(TimeSpan elapsed) => new(score, created, stages.AsReadOnly().ToArray(),
        outcome is null ? elapsed > last ? elapsed : last : last, outcome, detail);

    private static MotionSource CopySource(MotionSource source)
    {
        if (source.ContentLines is { } content && content.Count != source.Lines.Count)
            throw new ArgumentException("Source content must match its displayed lines.", nameof(source));
        if (source.LineNumbers is { } numbers && (numbers.Count != source.Lines.Count || numbers.Any(number => number < 1) || numbers.Distinct().Count() != numbers.Count))
            throw new ArgumentException("Source line numbers must identify each supplied line once.", nameof(source));
        return source with
        {
            Lines = Array.AsReadOnly(source.Lines.ToArray()),
            ContentLines = source.ContentLines is { } raw ? Array.AsReadOnly(raw.ToArray()) : null,
            LineNumbers = source.LineNumbers is { } lines ? Array.AsReadOnly(lines.ToArray()) : null
        };
    }
}
