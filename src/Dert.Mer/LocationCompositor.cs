namespace Dert.Mer;

public sealed record LocationBranch(string Name, IReadOnlyList<InkLine> Evidence);
public sealed record LocationView(string Application, DateTimeOffset RecordedAt,
    IReadOnlyList<InkLine> Summary, IReadOnlyList<LocationBranch> Branches, string Status, string Effect);

// A single connected location window. Complete paths wrap into scrollback;
// viewport rendering is reserved for in-progress activity, never a clipped result.
public static class LocationCompositor
{
    public static IReadOnlyList<InkLine> Receipt(LocationView view, int columns)
    {
        var width = Math.Max(1, Math.Min(columns - 1, 132));
        var lines = new List<InkLine>();
        if (width < 38)
        {
            Add("DERT / where / " + view.Application, Ink.Signal);
            foreach (var summary in view.Summary) Add(summary.Text, summary.Runs.FirstOrDefault()?.Ink ?? Ink.Text);
            foreach (var branch in view.Branches)
            {
                Add(branch.Name, Ink.Signal);
                foreach (var evidence in branch.Evidence) Add(evidence.Text, evidence.Runs.FirstOrDefault()?.Ink ?? Ink.Text);
            }
            Add(view.Status, Ink.Copper);
            Add(view.Effect, Ink.Muted);
            return lines;
        }
        var contentWidth = width - 6;
        Add("  DERT / where" + new string(' ', Math.Max(1, width - 39)) + view.RecordedAt.ToLocalTime().ToString("dd MMM  HH:mm:ss"), Ink.Muted);
        var tab = TerminalText.Clip(view.Application, contentWidth - 12);
        var left = " .-- " + tab + " --";
        lines.Add(new(new(left, Ink.Signal, true), new(new string('-', width - TerminalText.Width(left) - 1) + ".", Ink.Muted)));
        foreach (var summary in view.Summary) Row(summary);
        foreach (var branch in view.Branches)
        {
            Row(InkLine.Plain(""));
            Row(InkLine.Plain(branch.Name, Ink.Signal));
            foreach (var evidence in branch.Evidence) Row(evidence);
        }
        lines.Add(InkLine.Plain(" '" + new string('-', width - 9) + " ;; --'", Ink.Muted));
        Add("  " + view.Status, Ink.Text);
        Add("  " + view.Effect, Ink.Muted);
        return lines;

        void Row(InkLine line)
        {
            var indent = line.Text.StartsWith("  |   ", StringComparison.Ordinal) ? "  |   " : line.Text.StartsWith("  '-- ", StringComparison.Ordinal) ? "  '-- " : "";
            var continuation = indent.Contains('\'') ? "      " : indent;
            var wrapped = WrapEvidence(line.Text[indent.Length..], contentWidth - indent.Length).Select((part, index) => (index == 0 ? indent : continuation) + part).ToArray();
            if (wrapped.Length == 0) wrapped = [""];
            foreach (var part in wrapped)
                lines.Add(new(new(" |  ", Ink.Muted),
                    new(part, line.Runs.FirstOrDefault()?.Ink ?? Ink.Text),
                    new(new string(' ', contentWidth - TerminalText.Width(part)) + " |", Ink.Muted)));
        }
        void Add(string text, Ink ink) => lines.AddRange(WrapEvidence(text, width).Select(part => InkLine.Plain(part, ink)));
    }

    private static IEnumerable<string> WrapEvidence(string text, int width) =>
        text.Contains(":\\", StringComparison.Ordinal) || text.StartsWith('/') ? WrapExact(text, width) : TerminalText.Wrap(text, width);

    // Paths may contain consecutive spaces. Word wrapping would silently discard
    // them, so location evidence splits only at grapheme boundaries.
    private static IEnumerable<string> WrapExact(string text, int width)
    {
        var part = new System.Text.StringBuilder();
        var used = 0;
        foreach (var element in TerminalText.Elements(text))
        {
            if (used + element.Width > width && part.Length > 0)
            { yield return part.ToString(); part.Clear(); used = 0; }
            part.Append(element.Width > width ? "…" : element.Text);
            used += Math.Min(element.Width, width);
        }
        if (part.Length > 0) yield return part.ToString();
    }

    public static MerFrame Pending(string application, string detail, int columns, int rows)
    {
        var width = Math.Max(1, columns - 1);
        var lines = new[] { "DERT / where", " .-- " + application + " --.", " |", " +-- ;; " + detail, " |   Reading local evidence", " '-- No programs started or files changed" }
            .SelectMany(line => TerminalText.Wrap(line, width)).Take(Math.Max(1, rows - 2)).Select(line => InkLine.Plain(line, Ink.Muted)).ToArray();
        return new(columns, lines, rows);
    }
}
