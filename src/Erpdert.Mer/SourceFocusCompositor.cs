using System.Globalization;

namespace Erpdert.Mer;

/// <summary>Source-first views of observed relationships and proposed edits.</summary>
internal static class SourceFocusCompositor
{
    internal static MerFrame? Compose(MotionSnapshot snapshot, StageSnapshot stage, MotionEvidence evidence,
        int columns, int rows, bool still, bool held = false)
    {
        var width = Math.Clamp(columns - 1, 1, 240);
        var height = Math.Min(rows - 2, evidence.Focus is TitleRuleFocus || width >= 96 ? 18 : 20);
        if (width < 46 || height < 16 || snapshot.Score.Source is not { } source ||
            evidence.OwnerLine is not int owner || Line(owner) is null ||
            evidence.SourceLine is not int entry || Line(entry) is null) return null;
        if (evidence.Focus is not (NoteConnectionFocus or TitleRuleFocus)) return null;
        var canvas = new CellCanvas(width, height);
        canvas.Put(2, 0, ";", Ink.Copper, true);
        canvas.Put(6, 0, "ERPDERT / mer", Ink.Muted);
        var stamp = snapshot.Created.ToString("dd MMM yyyy  HH:mm:ss zzz", CultureInfo.InvariantCulture);
        if (width >= 74) canvas.Put(width - TerminalText.Width(stamp) - 2, 0, stamp, Ink.Muted);
        canvas.Put(2, 1, ";;", Ink.Signal, true);
        canvas.Put(6, 1, snapshot.Score.Title, Ink.Text, true);
        canvas.Put(6, 2, source.Label.Replace('\\', '/').Split('/').Last(), Ink.Muted);
        if (evidence.Focus is TitleRuleFocus title) Title(title);
        else if (evidence.Focus is NoteConnectionFocus note)
        {
            if (note.ClosingLine is int close && Line(close) is null) return null;
            Note(note);
        }

        var passed = snapshot.Stages.Count(item => item.State == StageState.Passed);
        var attention = snapshot.Stages.Count(item => item.State is StageState.Attention or StageState.Failed or StageState.Interrupted);
        var unfinished = snapshot.Stages.Count(item => item.State is StageState.Working or StageState.Waiting);
        var skipped = snapshot.Stages.Count(item => item.State == StageState.Skipped);
        if (snapshot.Stages.LastOrDefault(item => item.State == StageState.Working) is { } working)
            canvas.Put(6, height - 3, working.Stage.Title + " / in progress", Ink.Muted);
        var summary = $"{passed} checked / {attention} need attention" + (unfinished > 0 ? $" / {unfinished} unsettled" : skipped > 0 ? $" / {skipped} not run" : "");
        canvas.Put(6, height - 2, summary, Ink.Muted);
        canvas.Put(6, height - 1, (snapshot.Score.Replay ? "REPLAY" : "LIVE") +
            (held ? " / details opens the full record" : " / full receipt follows"), Ink.Muted);
        return new(columns, canvas.Lines(), rows);

        InkLine? Line(int number)
        {
            var index = source.LineNumbers?.ToList().IndexOf(number) ?? -1;
            return index < 0 ? null : source.Lines[index];
        }

        void Title(TitleRuleFocus title)
        {
            canvas.Put(6, 4, "Measured against your title", Ink.Copper);
            canvas.Put(4, 6, $"{owner,3}", Ink.Muted);
            canvas.Put(11, 6, title.TitleSource, Ink.Text, true);
            canvas.Put(4, 8, title.Before.Length == 0 ? "" : $"{entry,3}", Ink.Muted);
            var common = 0;
            while (common < title.Before.Length && common < title.After.Length && title.Before[common] == title.After[common]) common++;
            canvas.Put(9, 8, title.Before.Length == 0 ? InkLine.Plain("− (no rule here)", Ink.Muted) :
                new InkLine(new("− ", Ink.Bad), new(title.Before[..common], Ink.Muted), new(title.Before[common..], Ink.Bad, true)));
            canvas.Put(9, 9, new InkLine(new("+ ", Ink.Signal), new(title.After[..common], Ink.Muted), new(title.After[common..], Ink.Signal, true)));
            var difference = Math.Abs(title.Width - title.PreviousWidth);
            // The guide measures known marks only; it never animates source text
            // or pretends a partial preview has been applied.
            var start = 14 + Math.Min(title.Width, title.PreviousWidth);
            var end = 14 + Math.Max(title.Width, title.PreviousWidth) - 1;
            if (difference > 1 && end < width - 2)
            {
                canvas.Put(start, 10, "╰" + new string('─', difference - 2) + "╯", Ink.Muted);
                Trace(Enumerable.Range(start, difference).Select(x => (x, 10)).ToArray());
            }
            var explanationY = height < 18 ? 11 : 12;
            Prose(title.Explanation, 6, explanationY, width - 12, height - 4 - explanationY);
            canvas.Put(6, height - 4, "Preview only / not applied", Ink.Copper);
        }

        void Note(NoteConnectionFocus note)
        {
            var wide = width >= 96;
            var sourceWidth = wide ? width / 2 - 8 : width - 10;
            var detailX = wide ? width / 2 + 5 : 6;
            var detailWidth = width - detailX - 4;
            var siblings = stage.Evidence.Where(item => item.OwnerLine == owner && item.Focus is NoteConnectionFocus &&
                item.SourceLine is int number && Line(number) is not null)
                .GroupBy(item => item.SourceLine).Select(group => group.Last()).OrderBy(item => item.SourceLine).ToArray();
            var selected = Array.FindIndex(siblings, item => item.SourceLine == entry);
            canvas.Put(2, 4, ";", Ink.Copper, true);
            canvas.Put(4, 4, Line(owner)!, sourceWidth);
            canvas.Put(6, 5, "entries", Ink.Muted);
            for (var y = 5; y <= 8; y++) canvas.Put(2, y, "│", Ink.Muted);
            for (var offset = -1; offset <= 1; offset++)
            {
                var index = selected + offset;
                if (index < 0 || index >= siblings.Length) continue;
                var item = siblings[index];
                var value = ((NoteConnectionFocus)item.Focus!).Value;
                canvas.Put(4, 7 + offset, new InkLine(new($"{item.SourceLine,3}  ", Ink.Muted),
                    new(value, offset == 0 ? Ink.Signal : Ink.Muted, offset == 0)), sourceWidth);
            }
            canvas.Put(2, 7, "├", Ink.Signal);
            canvas.Put(2, 9, note.ClosingLine is null ? "?" : "╰", Ink.Copper);
            canvas.Put(6, 9, note.ClosingLine is int close ? $"Closing // at line {close}" : "Closing // missing", Ink.Copper, limit: sourceWidth);
            var headingY = wide ? 4 : 10;
            canvas.Put(detailX, headingY, "Belongs to " + note.Head, Ink.Copper, limit: detailWidth);
            if (wide) canvas.Put(detailX, 7, note.Value, Ink.Signal, true, detailWidth);
            var proseY = wide ? 8 : 11;
            Prose(note.Annotation ?? "No context note on this entry.", detailX, proseY, detailWidth, height - 4 - proseY);
            if (wide)
            {
                // A deliberate gap separates the source index from the connector;
                // no line ever grows out of a clipped piece of authored prose.
                var start = 6 + Math.Min(sourceWidth, TerminalText.Width($"{entry,3}  " + note.Value));
                canvas.Put(start, 7, new string('─', detailX - start - 2) + "╴", Ink.Muted);
                Trace(Enumerable.Range(start, detailX - start - 1).Select(x => (x, 7)).ToArray());
                canvas.Put(6, 11, $"Entry {selected + 1} / {siblings.Length} observed", Ink.Muted, limit: sourceWidth);
            }
            else Trace([(2, 4), (2, 5), (2, 6), (2, 7)]);
            if (note.ClosingLine is null) canvas.Put(6, height - 4, "Current grouping / its ending is unresolved", Ink.Copper);
        }

        void Prose(string text, int x, int y, int availableWidth, int availableRows)
        {
            if (availableRows <= 0) return;
            var lines = TerminalText.Wrap(text, availableWidth).ToArray();
            var clipped = lines.Length > availableRows;
            var count = clipped && availableRows > 1 ? availableRows - 1 : Math.Min(lines.Length, availableRows);
            for (var i = 0; i < count; i++)
                canvas.Put(x, y + i, lines[i] + (clipped && availableRows == 1 ? "…" : ""));
            if (clipped && availableRows > 1) canvas.Put(x, y + count, "… full text in the receipt", Ink.Muted, limit: availableWidth);
        }

        void Trace((int X, int Y)[] path)
        {
            if (still || held || snapshot.Outcome is not null || path.Length == 0 || stage.FocusAt is not { } at) return;
            var age = (snapshot.Elapsed - at).TotalSeconds;
            if (age < 0 || age >= .36) return;
            var progress = 1 - Math.Pow(1 - age / .36, 3);
            var position = path[Math.Min(path.Length - 1, (int)(progress * path.Length))];
            canvas.Put(position.X, position.Y, ";", Ink.Signal, true);
        }
    }
}
