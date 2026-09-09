using Dert.Core;
using Dert.Mer;

namespace Dert.Cli.Motion;

/// <summary>Observed analysis events can be delivered live or replayed without repeating work.</summary>
internal sealed record NoteScore(MotionScore Score, IReadOnlyList<MotionEvent> Events, int Exit)
{
    internal DertDocument? Document { get; init; }

    internal static MotionScore Describe(string label, bool replay) => new("Your notes, connected.",
        "Read source · reveal context · rehearse one exact edit. No file writes.", [
            new("read", "Read the source", "Reading the exact UTF-8 text."),
            new("context", "Trace the context", "Follow the // that connect each entry to its note."),
            new("lint", "Check the boundaries", "Check supported structure without rewriting your words."),
            new("repair", "Rehearse the title", "See the exact rule edit before choosing any change.")], new(label, []), replay);

    internal static NoteScore Analyze(Func<string> read, string label, bool replay, Action<MotionEvent>? observe = null, CancellationToken cancellation = default,
        DocumentSourceState sourceState = DocumentSourceState.Original)
    {
        var events = new List<MotionEvent>();
        var score = Describe(label, replay);
        var verified = sourceState is DocumentSourceState.Saved or DocumentSourceState.Restored;
        if (verified) score = score with { Scope = "Check the verified " + (sourceState == DocumentSourceState.Restored ? "restored" : "saved") + " bytes. This subsequent analysis makes no further edits." };
        var active = "read";
        try
        {
            Emit(new StartStage(active));
            var source = read();
            Emit(new AddEvidence(active, new("Source", $"{System.Text.Encoding.UTF8.GetByteCount(source)} bytes / UTF-8 · original text retained")));
            Emit(new SettleStage(active, StageState.Passed, "Read the UTF-8 source. Punctuation and line endings are retained."));

            active = "context";
            Emit(new StartStage(active));
            var document = DertParser.Parse(source);
            score = score with { Source = Source(document, label, cancellation) with { State = sourceState } };
            Emit(new SetSource(score.Source!));
            var nodes = NodeWalk.All(document.Nodes).ToArray();
            var structure = new DertStructureIndex(document);
            var ownership = nodes.Select(location => location.Node).OfType<ReasoningBlockNode>().ToDictionary(block => block.LineNumber,
                block => new DocumentOwnership(block.LineNumber, block.Closing is { } closing ? structure.LineOf(closing.Start) : null,
                    block.Entries.Select(entry => entry.LineNumber).ToArray(), structure.LineOf(Math.Max(block.Span.Start, block.Span.End - 1))));
            foreach (var location in nodes)
                switch (location.Node)
                {
                    case ReasoningBlockNode block:
                        Emit(new AddEvidence(active, new($"Line {block.LineNumber} / {block.Header.Head.Text}",
                            $"{block.Entries.Count} entries · " + (block.Closing is null ? "closing // is missing" : "closed by its own //"), SourceLine: block.LineNumber)));
                        break;
                    case NoteEntryNode entry when location.Parent is ReasoningBlockNode parent:
                        Emit(new AddEvidence(active, new($"Line {entry.LineNumber} belongs to line {parent.LineNumber}",
                            entry.Value.Text + (entry.Annotation is null ? "" : " // " + entry.Annotation.Text), EvidenceKind.Source, entry.LineNumber, parent.LineNumber,
                            new NoteConnectionFocus(parent.Header.Head.Text, entry.Value.Text, entry.Annotation?.Text,
                                ownership[parent.LineNumber].ClosingLine)
                            { Ownership = ownership[parent.LineNumber] })));
                        break;
                    case ReasoningNoteNode note:
                        Emit(new AddEvidence(active, new($"Line {note.LineNumber} / {note.Header.Head.Text}",
                            note.Header.Payload?.Text ?? "", EvidenceKind.Source, note.LineNumber)));
                        break;
                }
            Emit(new SettleStage(active, StageState.Passed, $"{nodes.Length} source structures traced. Named targets are not invented operation bindings."));

            active = "lint";
            Emit(new StartStage(active));
            foreach (var finding in document.Diagnostics)
                Emit(new AddEvidence(active, new($"{finding.Code} / line {finding.LineNumber} / {finding.Severity}", finding.Message,
                    finding.Severity == DiagnosticSeverity.Error ? EvidenceKind.Error : EvidenceKind.Text, SourceLine: finding.LineNumber)));
            var attention = document.Diagnostics.Any(finding => finding.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning);
            Emit(new SettleStage(active, attention ? StageState.Attention : StageState.Passed,
                document.Diagnostics.Count == 0 ? "Supported structural checks are clear. This is not full-language validation." :
                $"{document.Diagnostics.Count} {(document.Diagnostics.Count == 1 ? "finding" : "findings")}. " + (verified ? "These remain in the verified source." : "Your text is unchanged.")));

            active = "repair";
            Emit(new StartStage(active));
            var repair = TitleRuleRepair.Plan(document);
            if (repair.Edit is { } edit)
            {
                var insertion = edit.Span.Length == 0;
                var focusLine = insertion ? repair.TitleLine : repair.RuleLine;
                Emit(new AddEvidence(active, new(insertion ? $"After line {repair.TitleLine} / before" : $"Line {repair.RuleLine} / before",
                    edit.Before.Length == 0 ? "(no rule here)" : edit.Before, EvidenceKind.Removed, focusLine, repair.TitleLine)));
                foreach (var line in edit.After.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Where(line => line.Length > 0))
                    Emit(new AddEvidence(active, new(insertion ? $"Insert after line {repair.TitleLine} / proposed" : $"Line {repair.RuleLine} / proposed",
                        line, EvidenceKind.Added, focusLine, repair.TitleLine,
                        new TitleRuleFocus(document.Source.Slice(document.Source.Lines[repair.TitleLine - 1].ContentSpan).TrimStart('\uFEFF'),
                            edit.Before, line, repair.Width!.Value, edit.Before.Count(character => character == '-'), repair.Explanation))));
            }
            Emit(new SettleStage(active, repair.State == TitleRepairState.Unavailable ? StageState.Attention : StageState.Passed,
                repair.Explanation + (repair.IsReady ? " Preview only / not applied." : " No edit applied.")));
            Emit(new FinishMotion(attention || repair.State == TitleRepairState.Unavailable ? MotionOutcome.Attention : MotionOutcome.Complete,
                verified ? "Analysis of verified bytes finished. Any remaining proposal needs its own approval." :
                "Analysis finished. Source unchanged; every proposed edit is still only a preview."));
            return new(score, events, document.Diagnostics.Any(finding => finding.Severity == DiagnosticSeverity.Error) ? 1 : 0) { Document = document };
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            var invalidEncoding = active == "read" && error is System.Text.DecoderFallbackException;
            Emit(new SettleStage(active, StageState.Failed, invalidEncoding
                ? "This source is not valid UTF-8 text. No later check ran. Decoder detail / " + error.Message
                : "The source could not be read or analyzed. " + error.Message));
            Emit(new FinishMotion(MotionOutcome.Failed, invalidEncoding
                ? "No file was changed. Check its encoding in your editor; keep the original before saving a UTF-8 copy."
                : "No file was changed. Check the source path, read permission and UTF-8 encoding."));
            return new(score, events, 2);
        }

        void Emit(MotionEvent change)
        {
            cancellation.ThrowIfCancellationRequested();
            events.Add(change);
            observe?.Invoke(change);
        }
    }

    internal static MotionSource Source(DertDocument document, string label, CancellationToken cancellation = default)
    {
        // Sweep span boundaries once, preserving last-highlight-wins precedence.
        var points = document.Highlights.SelectMany((highlight, index) => new[]
        {
            (At: highlight.Span.Start, Index: index, Add: true),
            (At: highlight.Span.End, Index: index, Add: false)
        }).OrderBy(point => point.At).ThenBy(point => point.Add).ToArray();
        var active = new SortedSet<int>();
        var pointIndex = 0;
        var lines = new List<InkLine>();
        var content = new List<InkLine>();
        var numbers = new List<int>();
        foreach (var line in document.Source.Lines)
        {
            cancellation.ThrowIfCancellationRequested();
            if (line.ContentSpan.Length == 0 && line.Start == document.Source.Text.Length) continue;
            var runs = new List<InkRun> { new($"{line.Number,3}  ", Ink.Muted) };
            var offset = line.Start;
            if (offset == 0 && document.Source.Text.StartsWith('\uFEFF')) offset++;
            while (offset < line.ContentEnd)
            {
                while (pointIndex < points.Length && points[pointIndex].At <= offset)
                {
                    var point = points[pointIndex++];
                    if (point.Add) active.Add(point.Index); else active.Remove(point.Index);
                }
                var role = active.Count == 0 ? SemanticRole.Text : document.Highlights[active.Max].Role;
                var end = pointIndex < points.Length ? Math.Min(line.ContentEnd, points[pointIndex].At) : line.ContentEnd;
                var ink = role switch
                {
                    SemanticRole.Hook or SemanticRole.Boundary => Ink.Copper,
                    SemanticRole.Line or SemanticRole.Reasoning or SemanticRole.Target => Ink.Signal,
                    SemanticRole.ContextNote or SemanticRole.Rule => Ink.Muted,
                    _ => Ink.Text
                };
                runs.Add(new(document.Source.Text[offset..end], ink, role is SemanticRole.Title or SemanticRole.Head));
                offset = end;
            }
            lines.Add(new(runs.ToArray()));
            content.Add(new(runs.Skip(1).ToArray()));
            numbers.Add(line.Number);
        }
        return new(label, lines, numbers) { ContentLines = content };
    }
}
