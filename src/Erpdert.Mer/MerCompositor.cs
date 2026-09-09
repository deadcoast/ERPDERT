using System.Globalization;

namespace Erpdert.Mer;

public sealed record MerFrame(int Columns, IReadOnlyList<InkLine> Lines, int? Rows = null);

public static class MerCompositor
{
    public static MerFrame? Source(MotionSnapshot snapshot, int sourceLine, int columns) => SourceExcerptCompositor.Compose(snapshot, sourceLine, columns);

    public static MerFrame Hold(MotionSnapshot snapshot, int columns, int rows) => Render(snapshot, columns, rows, true, held: true);

    public static MerFrame? Inspect(MotionSnapshot snapshot, int sourceLine, int columns, int rows)
    {
        if (snapshot.Outcome is MotionOutcome.Failed or MotionOutcome.Interrupted ||
            snapshot.Stages.Any(stage => stage.State is StageState.Failed or StageState.Interrupted)) return null;
        foreach (var stage in snapshot.Stages.Reverse())
            if (stage.Evidence.LastOrDefault(evidence => evidence.SourceLine == sourceLine && evidence.Focus is not null) is { } selected)
                return SourceFocusCompositor.Compose(snapshot, stage, selected, columns, rows, true, held: true) ??
                    Render(snapshot, columns, rows, true, stage with { Detail = selected.Text, Evidence = [selected] }, held: true);
        return null;
    }

    public static MerFrame Live(MotionSnapshot snapshot, int columns, int rows, bool still = false) => Render(snapshot, columns, rows, still);

    public static MerFrame Document(MotionSnapshot snapshot, int columns, int rows, int? atLine = null, DocumentAnnotation? annotation = null) =>
        DocumentWindow.Compose(snapshot, columns, rows, true, atLine, annotation);

    private static MerFrame Render(MotionSnapshot snapshot, int columns, int rows, bool still, StageSnapshot? selection = null, bool held = false)
    {
        if (snapshot.Score.View == MotionView.Document)
            return DocumentWindow.Compose(snapshot, columns, rows, still || held);
        var width = Math.Clamp(columns - 1, 1, 240);
        var capacity = Math.Clamp(rows - 2, 1, 26);
        var sourceRows = capacity >= 18 ? 3 : 1;
        var naturalBody = width >= 92 ? Math.Clamp(snapshot.Stages.Count * 2 - 1, 5, 11) : Math.Clamp(snapshot.Stages.Count, 1, 7) + 5;
        var height = Math.Min(capacity, (snapshot.Score.Source is null ? 5 : 6 + sourceRows) + naturalBody + 3);
        var canvas = new CellCanvas(width, height);
        var failedStage = snapshot.Stages.LastOrDefault(stage => stage.State == StageState.Interrupted) ??
            snapshot.Stages.LastOrDefault(stage => stage.State == StageState.Failed);
        var active = selection ?? failedStage ?? snapshot.Stages.LastOrDefault(stage => stage.State == StageState.Working) ??
            snapshot.Stages.LastOrDefault(stage => stage.State is not (StageState.Waiting or StageState.Skipped)) ?? snapshot.Stages[0];
        var terminalProblem = snapshot.Outcome is MotionOutcome.Failed or MotionOutcome.Interrupted ||
            snapshot.Stages.Any(stage => stage.State is StageState.Failed or StageState.Interrupted);
        if (selection is null && !terminalProblem)
        {
            var errorStage = snapshot.Stages.FirstOrDefault(stage => stage.Evidence.Any(evidence => evidence.Kind == EvidenceKind.Error));
            if (errorStage is not null)
            {
                var problem = errorStage.Evidence.First(evidence => evidence.Kind == EvidenceKind.Error);
                active = errorStage with { Detail = problem.Text, Evidence = [problem] };
            }
            else
                foreach (var stage in snapshot.Stages.Reverse())
                    foreach (var evidence in stage.Evidence.Reverse())
                        if (SourceFocusCompositor.Compose(snapshot, stage, evidence, columns, rows, still, held) is { } retained) return retained;
        }
        if (!terminalProblem && active.State is not (StageState.Failed or StageState.Interrupted) && active.Evidence.LastOrDefault() is { Focus: not null } focused &&
            SourceFocusCompositor.Compose(snapshot, active, focused, columns, rows, still, held) is { } sourceFrame) return sourceFrame;
        var detailStage = terminalProblem && active.State is not (StageState.Failed or StageState.Interrupted)
            ? active with { State = StageState.Failed, Detail = snapshot.Detail, Evidence = [] } : active;
        var status = snapshot.Outcome?.ToString().ToLowerInvariant() ?? failedStage?.State.ToString().ToLowerInvariant() ?? (snapshot.Score.Replay ? "replay" : "live");
        if (height < 12 || width < 40)
        {
            canvas.Put(0, 0, ";; ERPDERT / MER", Ink.Copper, true);
            canvas.Put(0, 1, status, Ink.Signal);
            canvas.Put(0, 2, active.Stage.Title, Tone(active.State), true);
            var detailRows = Wrap(detailStage.Detail, width).Take(Math.Max(0, height - 4)).ToArray();
            for (var i = 0; i < detailRows.Length; i++) canvas.Put(0, 3 + i, detailRows[i]);
            canvas.Put(0, height - 1, snapshot.Outcome is null ? "Full receipt follows." : snapshot.Detail, Ink.Muted);
            return new(columns, canvas.Lines(), rows);
        }

        canvas.Put(2, 0, ";", Ink.Copper, true);
        canvas.Put(6, 0, "ERPDERT", Ink.Text, true);
        canvas.Put(15, 0, "/ mer", Ink.Muted);
        var stamp = snapshot.Created.ToString("dd MMM yyyy · HH:mm:ss zzz", CultureInfo.InvariantCulture);
        if (width >= 74) canvas.Put(width - TerminalText.Width(stamp), 0, stamp, Ink.Muted);
        canvas.Put(2, 1, ";;", Ink.Signal, true);
        canvas.Put(6, 1, snapshot.Score.Title, Ink.Text, true, width - 7);
        canvas.Put(6, 2, snapshot.Score.Scope, Ink.Muted);

        var bodyTop = 5;
        if (snapshot.Score.Source is { } source)
        {
            var focus = active.Evidence.LastOrDefault(evidence => evidence.SourceLine is not null) ??
                snapshot.Stages.SelectMany(stage => stage.Evidence).LastOrDefault(evidence => evidence.SourceLine is not null);
            var excerpt = Excerpt(source, focus, sourceRows);
            canvas.Put(2, 4, "//", Ink.Copper);
            var name = source.Label.Replace('\\', '/').Split('/').LastOrDefault() ?? source.Label;
            canvas.Put(6, 4, name + (focus?.SourceLine is int at ? $" / source at {at}" : " / source"), Ink.Muted);
            for (var i = 0; i < excerpt.Count; i++)
            {
                var index = excerpt[i];
                if (source.LineNumbers?[index] == focus?.SourceLine && focus is not null) canvas.Put(2, 5 + i, ";;", Ink.Signal, true);
                else if (source.LineNumbers?[index] == focus?.OwnerLine && focus?.OwnerLine is not null) canvas.Put(2, 5 + i, ";", Ink.Copper, true);
                canvas.Put(6, 5 + i, source.Lines[index]);
            }
            bodyTop = 6 + sourceRows;
        }

        var bodyHeight = height - bodyTop - 3;
        // Keep current work in one place. Observed results settle below it;
        // waiting stages remain pending in the count and complete receipt.
        canvas.Put(2, bodyTop, Glyph(detailStage.State), Tone(detailStage.State), true);
        canvas.Put(6, bodyTop, active.Stage.Title + " / " + StateName(detailStage.State), Tone(detailStage.State), true);
        var settledStages = snapshot.Stages.Where(stage => stage.Stage.Id != active.Stage.Id &&
            stage.State is not (StageState.Waiting or StageState.Working)).ToArray();
        var ledgerHeight = settledStages.Length > 0 && bodyHeight >= 6 ? Math.Min(4, bodyHeight - 3) : 0;
        var detailHeight = bodyHeight - 1 - ledgerHeight;
        DrawDetail(canvas, detailStage, 6, bodyTop + 1, width - 8, detailHeight);
        if (ledgerHeight > 0)
            DrawSettled(canvas, settledStages, 6, bodyTop + 1 + detailHeight, width - 8, ledgerHeight);

        // An observation gets one short trace. Waiting does not loop old evidence,
        // and failure or interruption settles immediately, even before FinishMotion.
        var observed = active.EvidenceAt ?? active.Started;
        if (!still && !held && !terminalProblem && snapshot.Outcome is null &&
            active.State == StageState.Working && observed is { } observedAt)
        {
            var age = (snapshot.Elapsed - observedAt).TotalSeconds;
            if (age >= 0 && age < .36)
            {
                var offset = Math.Min(detailHeight, (int)((1 - Math.Pow(1 - age / .36, 3)) * (detailHeight + 1)));
                canvas.Put(2, bodyTop + offset, ";", Ink.Signal, true);
            }
        }
        canvas.Put(2, height - 3, "╰" + new string('─', Math.Max(0, width - 7)) + ";;", Ink.Copper);
        var settled = snapshot.Stages.Count(stage => stage.State is not (StageState.Waiting or StageState.Working));
        var passed = snapshot.Stages.Count(stage => stage.State == StageState.Passed);
        var attention = snapshot.Stages.Count(stage => stage.State is StageState.Attention or StageState.Failed or StageState.Interrupted);
        canvas.Put(6, height - 2, terminalProblem ? $"{passed} checks passed / overall {status}" :
            $"{passed} checked · {attention} attention · {snapshot.Stages.Count - settled} unsettled", Ink.Muted);
        canvas.Put(6, height - 1, status + (snapshot.Score.Replay ? " / recorded local analysis" : " / observed events") +
            (held ? " / details opens the full record" : " / full receipt follows"), Ink.Signal);
        return new(columns, canvas.Lines(), rows);
    }

    private static void DrawSettled(CellCanvas canvas, IReadOnlyList<StageSnapshot> stages, int x, int y, int width, int height)
    {
        var count = Math.Min(stages.Count, height - 1);
        canvas.Put(x, y, "Settled work" + (stages.Count > count ? $" / {stages.Count - count} more in receipt" : ""), Ink.Muted);
        var recent = stages.TakeLast(count).ToArray();
        for (var i = 0; i < recent.Length; i++)
        {
            var stage = recent[i];
            // Preserve the start of each result, including negation. Full text
            // remains in the receipt; never silently present a shortened result.
            var detail = stage.Stage.Title + " / " + StateName(stage.State) + ": " + stage.Detail;
            canvas.Put(2, y + 1 + i, Glyph(stage.State), Tone(stage.State));
            canvas.Put(x, y + 1 + i, TerminalText.Clip(detail, width), Ink.Muted);
        }
    }

    private static IReadOnlyList<int> Excerpt(MotionSource source, MotionEvidence? focus, int count)
    {
        var selected = focus?.SourceLine is int at && source.LineNumbers is { } numbers ? numbers.ToList().IndexOf(at) : -1;
        if (selected < 0) return Enumerable.Range(0, Math.Min(count, source.Lines.Count)).ToArray();
        if (count == 1) return [selected];
        var owner = focus?.OwnerLine is int parent && source.LineNumbers is { } owners ? owners.ToList().IndexOf(parent) : -1;
        if (owner >= 0 && owner != selected)
        {
            var result = new List<int> { owner, selected };
            if (count > 2 && selected + 1 < source.Lines.Count && selected + 1 != owner) result.Add(selected + 1);
            return result.Order().ToArray();
        }
        var start = Math.Max(0, selected - 1);
        return Enumerable.Range(start, Math.Min(count, source.Lines.Count - start)).ToArray();
    }

    private static void DrawDetail(CellCanvas canvas, StageSnapshot stage, int x, int y, int width, int height)
    {
        if (height <= 0) return;
        var priority = stage.State is StageState.Attention or StageState.Failed or StageState.Interrupted || stage.Evidence.Count == 0
            ? Wrapped(stage.Detail, stage.State == StageState.Failed ? Ink.Bad : Ink.Text) : [];
        var remainingEvidence = stage.Evidence.Where(evidence => priority.Count == 0 || evidence.Text != stage.Detail).ToArray();
        var groups = remainingEvidence.TakeLast(2).Select(evidence =>
        {
            var label = Wrapped(evidence.Label, Ink.Copper);
            var content = Wrapped(Prefix(evidence.Kind) + evidence.Text, EvidenceTone(evidence.Kind));
            return (Label: label, Content: content, All: label.Concat(content).ToList());
        }).ToArray();
        var omitted = remainingEvidence.Length > groups.Length || priority.Count + groups.Sum(group => group.All.Count) > height;
        var budget = Math.Max(1, height - (omitted && height > 1 ? 1 : 0));
        var lines = priority.Take(budget).ToList();
        // Fit the newest evidence first, then retain its predecessor only when
        // the whole group fits. Source focus and visible evidence must agree.
        var available = budget - lines.Count;
        var chosen = new List<InkLine>();
        for (var i = groups.Length - 1; i >= 0 && available > 0; i--)
        {
            if (groups[i].All.Count <= available)
            {
                chosen.InsertRange(0, groups[i].All);
                available -= groups[i].All.Count;
            }
            else if (chosen.Count == 0)
            {
                // Preserve the beginning of the explanation: a suffix can lose
                // a negation. Never spend the entire budget on its label.
                chosen.AddRange((available <= groups[i].Label.Count ? groups[i].Content : groups[i].All).Take(available));
                if (chosen.Count > 0)
                {
                    var last = chosen[^1];
                    chosen[^1] = new InkLine([.. last.Runs, new("…", Ink.Muted)]);
                }
                break;
            }
        }
        lines.AddRange(chosen);
        for (var row = 0; row < lines.Count; row++) canvas.Put(x, y + row, lines[row]);
        if (omitted && height > 1) canvas.Put(x, y + height - 1, "… more in the full receipt", Ink.Muted);

        List<InkLine> Wrapped(string text, Ink ink) => Wrap(text, width).Select(line => InkLine.Plain(line, ink)).ToList();
    }

    public static IReadOnlyList<InkLine> Receipt(MotionSnapshot snapshot) => Receipt(snapshot, 110);

    public static IReadOnlyList<InkLine> Receipt(MotionSnapshot snapshot, int columns)
    {
        var lines = new List<InkLine>
        {
            new(new("  ;   ", Ink.Copper, true), new("ERPDERT", Ink.Text, true), new(" / mer", Ink.Muted)),
            new(new("  ;;  ", Ink.Signal, true), new(snapshot.Score.Title, Ink.Text, true)),
            InkLine.Plain("      " + snapshot.Created.ToString("dd MMM yyyy · HH:mm:ss zzz", CultureInfo.InvariantCulture), Ink.Muted),
            InkLine.Plain("      " + snapshot.Score.Scope, Ink.Muted)
        };
        if (snapshot.Score.Replay) lines.Add(InkLine.Plain("      REPLAY / recorded local analysis, not a second execution", Ink.Copper));
        if (snapshot.Score.Source is { } source)
        {
            lines.Add(InkLine.Plain(""));
            if (source.State != DocumentSourceState.Original)
                lines.Add(InkLine.Plain("      " + (source.State switch
                {
                    DocumentSourceState.Saved => "Saved / bytes verified",
                    DocumentSourceState.Restored => "Restored / bytes verified",
                    DocumentSourceState.Proposed => "Proposed / not saved",
                    DocumentSourceState.BeforeEdit => "Before edit / captured bytes",
                    _ => "Recorded source / current file not verified"
                }), Ink.Muted));
            lines.Add(InkLine.Plain("  //  " + source.Label, Ink.Copper));
            if (snapshot.Score.View == MotionView.Document) lines.AddRange(DocumentWindow.FullSource(source, columns));
            else lines.AddRange(source.Lines.Select(line => new InkLine([new("      "), .. line.Runs])));
        }
        foreach (var stage in snapshot.Stages)
        {
            lines.Add(InkLine.Plain("  ┆", Ink.Muted));
            lines.Add(new(new("  " + Glyph(stage.State) + "   ", Tone(stage.State), true),
                new(stage.Stage.Title, Ink.Text, true), new(" / " + StateName(stage.State), Tone(stage.State))));
            // A passed check may settle on its exact evidence text. Keep that
            // evidence once, with its label; failure explanations still lead.
            if (stage.State != StageState.Passed || !stage.Evidence.Any(evidence => evidence.Text == stage.Detail))
                lines.Add(InkLine.Plain("      " + stage.Detail));
            foreach (var evidence in stage.Evidence)
            {
                if (evidence.Label.Length > 0) lines.Add(InkLine.Plain("      " + evidence.Label, Ink.Copper));
                lines.Add(InkLine.Plain("      " + Prefix(evidence.Kind) + evidence.Text, EvidenceTone(evidence.Kind)));
            }
        }
        lines.Add(InkLine.Plain("  ╰──────────────────────────────────── ;;", Ink.Copper));
        lines.Add(new(new("      " + (snapshot.Outcome?.ToString().ToUpperInvariant() ?? "UNFINISHED"),
            snapshot.Outcome == MotionOutcome.Complete ? Ink.Good : Ink.Copper, true),
            new(" / " + snapshot.Detail)));
        lines.Add(InkLine.Plain("      ƒin.", Ink.Copper));
        return lines;
    }

    internal static IEnumerable<string> Wrap(string text, int columns) => TerminalText.Wrap(text, columns);

    private static string Prefix(EvidenceKind kind) => kind switch { EvidenceKind.Removed => "− ", EvidenceKind.Added => "+ ", _ => "" };
    private static Ink EvidenceTone(EvidenceKind kind) => kind switch { EvidenceKind.Removed or EvidenceKind.Error => Ink.Bad, EvidenceKind.Added => Ink.Good, EvidenceKind.Source => Ink.Signal, _ => Ink.Text };
    private static string Glyph(StageState state) => state switch
    {
        StageState.Working => ";",
        StageState.Passed => "✓",
        StageState.Attention => "!",
        StageState.Failed => "×",
        StageState.Skipped => "·",
        StageState.Interrupted => "!",
        _ => "○"
    };
    private static string StateName(StageState state) => state switch { StageState.Waiting or StageState.Skipped => "not run", StageState.Passed => "checked", _ => state.ToString().ToLowerInvariant() };
    private static Ink Tone(StageState state) => state switch
    {
        StageState.Passed => Ink.Good,
        StageState.Working => Ink.Signal,
        StageState.Attention or StageState.Interrupted => Ink.Copper,
        StageState.Failed => Ink.Bad,
        _ => Ink.Muted
    };
}
