using System.Globalization;

namespace Dert.Mer;

/// <summary>The authored document is the persistent surface; checks annotate it.</summary>
internal static class DocumentWindow
{
    private sealed record SourceRow(int Number, bool Continuation, InkLine Content);

    internal static MerFrame Compose(MotionSnapshot snapshot, int columns, int rows, bool still, int? atLine = null, DocumentAnnotation? annotation = null)
    {
        var width = Math.Clamp(columns - 1, 1, 132);
        var capacity = Math.Clamp(rows - 4, 1, 38);
        var source = snapshot.Score.Source;
        var failed = snapshot.Stages.LastOrDefault(stage => stage.State is StageState.Failed or StageState.Interrupted);
        var terminalProblem = failed is not null || snapshot.Outcome is MotionOutcome.Failed or MotionOutcome.Interrupted;
        var findings = snapshot.Stages.SelectMany(stage => stage.Evidence.Select(evidence => (Stage: stage, Evidence: evidence))).ToArray();
        var chosen = !terminalProblem && atLine is int requested
            ? findings.LastOrDefault(item => item.Evidence.SourceLine == requested)
            : findings.FirstOrDefault(item => item.Evidence.Kind == EvidenceKind.Error);
        if (chosen.Evidence is null && atLine is null && !terminalProblem)
            chosen = findings.LastOrDefault(item => item.Evidence.Focus is not null);
        var working = snapshot.Stages.LastOrDefault(stage => stage.State == StageState.Working);
        var focus = annotation?.SourceLine ?? atLine ?? chosen.Evidence?.SourceLine;
        var finished = snapshot.Outcome is not null;
        var settled = snapshot.Stages.LastOrDefault(stage => stage.State is not StageState.Waiting and not StageState.Working);
        var next = snapshot.Stages.FirstOrDefault(stage => stage.State == StageState.Waiting);
        var detail = Detail();
        var status = terminalProblem ? (snapshot.Outcome == MotionOutcome.Interrupted || failed?.State == StageState.Interrupted ? "Stopped" : "Could not finish")
            : finished ? snapshot.Outcome == MotionOutcome.Attention ? "Analysis complete / needs attention" : "Analysis complete"
            : working?.Stage.Title ?? (next is not null ? "Next: " + next.Stage.Title : "Finishing analysis");
        var caption = (snapshot.Score.Replay ? "Replay" : "Local analysis") + " / original text";
        var effect = "no file edits";
        switch (source?.State)
        {
            case DocumentSourceState.BeforeEdit: caption = "Before edit / captured bytes"; status = "Comparing an exact preview"; break;
            case DocumentSourceState.Proposed: caption = "Proposed / not saved"; status = "Preview only"; break;
            case DocumentSourceState.Saved: caption = "Saved / bytes verified"; effect = "saved repair"; break;
            case DocumentSourceState.Restored: caption = "Restored / bytes verified"; effect = "original bytes restored"; break;
            case DocumentSourceState.Unverified: caption = "Recorded source / current file not verified"; status = "Repair stopped"; effect = "see recovery detail"; break;
        }
        if (!terminalProblem && source?.State != DocumentSourceState.Unverified && annotation?.Action is { } action)
        {
            status = action.Name + " / " + (action.State switch
            {
                StageState.Working => "checking",
                StageState.Passed => "complete",
                StageState.Waiting => "awaiting choice",
                StageState.Interrupted => "stopped",
                StageState.Failed => "could not finish",
                _ => "needs attention"
            });
            effect = action.Effect;
        }
        if (width < 35 || capacity < 16)
        {
            var compact = new List<InkLine>();
            Add("DERT / mer", Ink.Text);
            Add(source?.Label.Replace('\\', '/').Split('/').Last() ?? "Source", Ink.Text);
            Add(caption, Ink.Muted);
            Add(status, terminalProblem ? Ink.Bad : Ink.Muted);
            foreach (var line in detail) Add(line.Text, line.Runs.FirstOrDefault()?.Ink ?? Ink.Text);
            Add("Source window needs more room. --details shows the full record.", Ink.Muted);
            return new(columns, compact.Take(capacity).ToArray(), rows);

            void Add(string text, Ink ink) => compact.AddRange(TerminalText.Wrap(text, width).Select(line => InkLine.Plain(line, ink)));
        }

        var wide = width >= 108;
        var panelWidth = wide ? 78 : width - 2;
        var digits = GutterDigits(source);
        var textX = 8 + digits;
        var textWidth = panelWidth - textX - 3;
        var annotationWidth = wide ? width - panelWidth - 6 : panelWidth - 8;
        var notes = detail.SelectMany(line => Wrap(line, annotationWidth)).ToArray();
        var annotationRows = wide ? 0 : Math.Min(6, notes.Length + 1);
        var bodyBudget = Math.Max(2, capacity - 7 - annotationRows);
        var sourceIndex = focus is int targetLine && source is not null
            ? Enumerable.Range(0, source.Lines.Count).FirstOrDefault(index => (source.LineNumbers?[index] ?? index + 1) == targetLine) : 0;
        // Only wrap the lines needed around the focus. A 5,000-line document
        // must not be laid out again for every observed entry or motion tick.
        var preceding = Source(source, textWidth, Math.Max(0, sourceIndex - bodyBudget), sourceIndex).TakeLast(bodyBudget);
        var following = Source(source, textWidth, sourceIndex).Take(bodyBudget + 1);
        var numbered = preceding.Concat(following).ToArray();
        var bodyRows = Math.Clamp(numbered.Length, 3, bodyBudget);
        var selected = focus is int lineNumber ? Array.FindIndex(numbered, row => row.Number == lineNumber) : 0;
        if (selected < 0) selected = 0;
        var start = numbered.Length <= bodyRows ? 0 : Math.Clamp(selected - bodyRows / 3, 0, numbered.Length - bodyRows);
        var visible = numbered.Skip(start).Take(bodyRows).ToArray();
        var bottom = 4 + bodyRows;
        var height = Math.Min(capacity, Math.Max(bottom + 3 + annotationRows, wide ? 6 + Math.Min(notes.Length, capacity - 6) : 0));
        var canvas = new CellCanvas(width, height);

        canvas.Put(2, 0, "DERT", Ink.Text, true);
        canvas.Put(11, 0, "/ mer", Ink.Muted);
        var stamp = snapshot.Created.ToString("dd MMM  HH:mm:ss", CultureInfo.InvariantCulture);
        canvas.Put(Math.Max(20, panelWidth - stamp.Length), 0, stamp, Ink.Muted);

        var name = source?.Label.Replace('\\', '/').Split('/').Last() ?? "Source";
        name = TerminalText.Clip(name, panelWidth - 24);
        var tabEnd = 11 + TerminalText.Width(name);
        canvas.Put(4, 2, ".-- " + name + " --.", Ink.Muted);
        canvas.Put(8, 2, name, Ink.Text, true);
        canvas.Put(1, 3, ".--'" + new string(' ', tabEnd - 5) + "'" + new string('-', panelWidth - tabEnd - 2) + ".", Ink.Muted);

        for (var i = 0; i < bodyRows; i++)
        {
            canvas.Put(1, 4 + i, "|", Ink.Muted);
            canvas.Put(panelWidth - 1, 4 + i, "|", Ink.Muted);
            if (i >= visible.Length) continue;
            var row = visible[i];
            var isSelected = row.Number == focus;
            canvas.Put(3, 4 + i, isSelected && !row.Continuation ? ";" : " ", Ink.Signal, true);
            var gutter = (row.Continuation ? new string(' ', digits) : row.Number.ToString(CultureInfo.InvariantCulture).PadLeft(digits)) + " | ";
            canvas.Put(5, 4 + i, gutter, isSelected ? Ink.Text : Ink.Muted);
            canvas.Put(textX, 4 + i, row.Content, textWidth);
        }
        var relationship = annotation?.Ownership ?? (annotation is null && chosen.Evidence is { Focus: NoteConnectionFocus connection, OwnerLine: int ownerLine }
            ? connection.Ownership ?? new DocumentOwnership(ownerLine, connection.ClosingLine) : null);
        if (relationship is not null)
        {
            // Trace actual ownership in the display margin. The source column
            // stays literal; an offscreen or missing ending stays open.
            var opening = Array.FindIndex(visible, row => row.Number == relationship.OpeningLine && !row.Continuation);
            var closing = Array.FindIndex(visible, row => row.Number == relationship.ClosingLine && !row.Continuation);
            var from = opening >= 0 ? opening : 0;
            var to = closing >= 0 ? closing : visible.Length - 1;
            for (var i = from; i <= to; i++)
            {
                if (relationship.EndLine is int end && visible[i].Number > end ||
                    relationship.MemberLines is { } members && visible[i].Number != relationship.OpeningLine &&
                    visible[i].Number != relationship.ClosingLine && !members.Contains(visible[i].Number)) continue;
                canvas.Put(3, 4 + i, i == opening ? "+" : i == closing ? "'" : "|", i == opening || i == closing ? Ink.Signal : Ink.Muted);
            }
            if (opening < 0 && visible.Length > 0)
            {
                var head = Array.FindIndex(visible, row => (relationship.EndLine is null || row.Number <= relationship.EndLine) &&
                    (relationship.MemberLines is null || relationship.MemberLines.Contains(row.Number) || row.Number == relationship.ClosingLine));
                if (head >= 0) canvas.Put(3, 4 + head, ":", Ink.Muted);
            }
            if (closing < 0 && visible.Length > 0)
            {
                var tail = Array.FindLastIndex(visible, row => (relationship.EndLine is null || row.Number <= relationship.EndLine) &&
                    (relationship.MemberLines is null || relationship.MemberLines.Contains(row.Number) || row.Number == relationship.OpeningLine));
                if (tail >= 0) canvas.Put(3, 4 + tail, ":", Ink.Muted);
            }
            for (var i = 0; i < visible.Length; i++)
                if (visible[i].Number == focus && !visible[i].Continuation) canvas.Put(3, 4 + i, ";", Ink.Signal, true);
        }
        if (numbered.Length == 0)
            canvas.Put(5, 5, terminalProblem ? "Source unavailable" : finished ? "Empty document" : "Reading source...", Ink.Muted);

        var excerpt = visible.Length > 0 && (visible[0].Continuation ||
            visible[0].Number != (source?.LineNumbers?.FirstOrDefault() ?? 1) ||
            start + bodyRows < numbered.Length || visible[^1].Number != (source?.LineNumbers?.LastOrDefault() ?? source?.Lines.Count));
        var extent = visible.Length == 0 ? "source" : $"lines {visible[0].Number}-{visible[^1].Number}";
        if (excerpt) extent += " / excerpt";
        canvas.Put(1, bottom, "'" + new string('-', panelWidth - 3) + "'", Ink.Muted);
        var extentLabel = "[ " + extent + " ]";
        var extentX = TerminalText.Width(extentLabel) <= panelWidth - 7 ? 5 : 2;
        canvas.Put(extentX, bottom, extentLabel, Ink.Muted, limit: panelWidth - extentX - 1);
        if (extentX + TerminalText.Width(extentLabel) < panelWidth - 8)
            canvas.Put(panelWidth - 7, bottom, " ;; ", Ink.Signal);

        // The one animated mark belongs to the document's edge. Source glyphs,
        // punctuation, and line numbers remain stable throughout the trace.
        var observed = chosen.Stage?.FocusAt ?? chosen.Stage?.EvidenceAt;
        if (!still && !finished && !terminalProblem && observed is { } time && focus is not null)
        {
            var age = (snapshot.Elapsed - time).TotalSeconds;
            if (age >= 0 && age < .48)
            {
                var target = 4 + Math.Clamp(selected - start, 0, bodyRows - 1);
                var y = 3 + (int)((target - 3) * (1 - Math.Pow(1 - age / .48, 3)));
                canvas.Put(1, y, ";", Ink.Signal, true);
            }
        }

        var noteX = wide ? panelWidth + 5 : 5;
        var anchor = 4 + Math.Clamp(selected - start, 0, bodyRows - 1);
        var noteY = wide ? Math.Clamp(anchor, 4, Math.Max(4, height - notes.Length - 2)) : bottom + 2;
        var noteBudget = wide ? height - noteY - 2 : annotationRows - 1;
        if (wide && focus is not null)
        {
            canvas.Put(panelWidth - 1, anchor, "+-", Ink.Signal);
            var routeX = panelWidth + 1;
            for (var y = Math.Min(anchor, noteY); y <= Math.Max(anchor, noteY); y++) canvas.Put(routeX, y, "|", Ink.Muted);
            canvas.Put(routeX, anchor, anchor == noteY ? "---" : noteY < anchor ? "'" : ".", Ink.Muted);
            canvas.Put(routeX, noteY, anchor == noteY ? "---" : anchor < noteY ? "'--" : ".--", Ink.Muted);
        }
        for (var i = 0; i < Math.Min(notes.Length, noteBudget); i++)
            canvas.Put(noteX, noteY + i, notes.Length > noteBudget && i == noteBudget - 1
                ? InkLine.Plain("... full explanation in details", Ink.Muted) : notes[i], annotationWidth);
        canvas.Put(2, height - 1, status + " / " + effect, terminalProblem ? Ink.Bad : Ink.Muted);
        canvas.Put(2, 1, caption, Ink.Muted);
        return new(columns, canvas.Lines(), rows);

        IReadOnlyList<InkLine> Detail()
        {
            if (terminalProblem)
                return [InkLine.Plain("Could not finish", Ink.Bad), InkLine.Plain((failed?.Detail ?? snapshot.Detail).Split("Decoder detail / ", StringSplitOptions.None)[0].Trim())];
            if (annotation is not null) return width < 108 ? annotation.CompactLines ?? annotation.Lines : annotation.Lines;
            if (chosen.Evidence is { Kind: EvidenceKind.Error } error)
                return [InkLine.Plain($"Line {error.SourceLine} / needs attention", Ink.Copper), InkLine.Plain(error.Text)];
            if (chosen.Evidence is { Focus: TitleRuleFocus title } proposed)
                return [InkLine.Plain($"Line {proposed.SourceLine} / title rule", Ink.Copper), InkLine.Plain(title.Width == title.PreviousWidth ? title.Explanation :
                    title.Width > title.PreviousWidth ? $"{title.Width - title.PreviousWidth} marks short." : $"{title.PreviousWidth - title.Width} marks too long."),
                    InkLine.Plain("Preview only / not applied", Ink.Muted), RuleDiff(title, false), RuleDiff(title, true)];
            if (chosen.Evidence is { Focus: NoteConnectionFocus note, OwnerLine: int owner } connection)
                return [InkLine.Plain($"Line {connection.SourceLine} / {note.Value}", Ink.Text),
                    InkLine.Plain($"Belongs to line {owner}: {note.Head}", Ink.Muted),
                    InkLine.Plain(note.Annotation ?? "No explanation on this entry."),
                    InkLine.Plain(note.ClosingLine is int close ? $"Closed by // on line {close}" : "Closing // missing", Ink.Muted)];
            if (atLine is not null && chosen.Evidence is { } observed)
                return [InkLine.Plain(observed.Label, Ink.Text), InkLine.Plain(observed.Text, Ink.Muted)];
            if (atLine is not null) return [InkLine.Plain($"Line {atLine} / original source"), InkLine.Plain("No recorded connection at this line.", Ink.Muted)];
            var attention = snapshot.Stages.LastOrDefault(stage => stage.State == StageState.Attention);
            if (attention is not null) return [InkLine.Plain(attention.Stage.Title, Ink.Copper), InkLine.Plain(attention.Detail)];
            if (working is not null) return [InkLine.Plain(working.Stage.Title), InkLine.Plain(working.Stage.Purpose, Ink.Muted)];
            if (snapshot.Outcome is not null)
                return [InkLine.Plain("Supported checks complete"), InkLine.Plain("Details keeps the complete analysis record.", Ink.Muted)];
            return [InkLine.Plain(settled?.Stage.Title ?? "Preparing the source"),
                InkLine.Plain(settled?.Detail ?? "Waiting for source evidence.", Ink.Muted)];
        }
    }

    private static InkLine RuleDiff(TitleRuleFocus title, bool after)
    {
        var common = 0;
        while (common < Math.Min(title.Before.Length, title.After.Length) && title.Before[common] == title.After[common]) common++;
        var text = after ? title.After : title.Before;
        if (text.Length == 0) return InkLine.Plain("- (no rule)", Ink.Muted);
        return new(new InkRun(after ? "+ " : "- ", Ink.Muted), new InkRun(text[..common], Ink.Muted),
            new InkRun(text[common..], after ? Ink.Signal : Ink.Bad, true));
    }

    private static IEnumerable<SourceRow> Source(MotionSource? source, int width, int from = 0, int? until = null)
    {
        if (source is null) yield break;
        for (var i = from; i < (until ?? source.Lines.Count); i++)
        {
            var number = source.LineNumbers?[i] ?? i + 1;
            var content = source.ContentLines?[i] ?? source.Lines[i];
            var continuation = false;
            foreach (var row in Wrap(content, width))
            {
                yield return new(number, continuation, row);
                continuation = true;
            }
        }
    }

    internal static IReadOnlyList<InkLine> FullSource(MotionSource source, int columns)
    {
        var width = Math.Clamp(columns - 2, 20, 100);
        var digits = GutterDigits(source);
        var textX = 8 + digits;
        var textWidth = Math.Max(2, width - textX - 3);
        var body = Source(source, textWidth).ToArray();
        var canvas = new CellCanvas(width, body.Length + 4);
        var name = TerminalText.Clip(source.Label.Replace('\\', '/').Split('/').Last(), width - 14);
        canvas.Put(1, 0, ".-- " + name + " " + new string('-', Math.Max(0, width - TerminalText.Width(name) - 7)) + ".", Ink.Muted);
        for (var i = 0; i < body.Length; i++)
        {
            canvas.Put(1, i + 1, "|", Ink.Muted);
            canvas.Put(width - 1, i + 1, "|", Ink.Muted);
            canvas.Put(5, i + 1, (body[i].Continuation ? new string(' ', digits) : body[i].Number.ToString(CultureInfo.InvariantCulture).PadLeft(digits)) + " | ", Ink.Muted);
            canvas.Put(textX, i + 1, body[i].Content, textWidth);
        }
        canvas.Put(1, body.Length + 1, "'" + new string('-', width - 3) + "'", Ink.Muted);
        return canvas.Lines();
    }

    internal static IEnumerable<InkLine> Wrap(InkLine line, int width)
    {
        var tokens = SourceExcerptCompositor.Tokens(line).ToArray();
        var start = 0;
        do
        {
            var end = start;
            var used = 0;
            var space = -1;
            while (end < tokens.Length && used + tokens[end].Width <= width)
            {
                if (tokens[end].Text == " ") space = end;
                used += tokens[end++].Width;
            }
            if (end < tokens.Length && space > start && tokens[end].Text != " ") end = space + 1;
            if (end == start && start < tokens.Length) { yield return InkLine.Plain("…"); start++; continue; }
            yield return new InkLine(tokens[start..end].Select(token => new InkRun(token.Text, token.Ink, token.Bold)).ToArray());
            start = end;
        } while (start < tokens.Length);
    }

    private static int GutterDigits(MotionSource? source) => Math.Max(3,
        (source?.LineNumbers is { Count: > 0 } numbers ? numbers.Max() : Math.Max(1, source?.Lines.Count ?? 0))
        .ToString(CultureInfo.InvariantCulture).Length);
}
