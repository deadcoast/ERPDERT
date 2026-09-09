using System.Globalization;

namespace Erpdert.Mer;

/// <summary>An on-demand scrollback excerpt. Source rows wrap, never truncate.</summary>
internal static class SourceExcerptCompositor
{
    internal static MerFrame? Compose(MotionSnapshot snapshot, int sourceLine, int columns)
    {
        if (snapshot.Outcome is MotionOutcome.Failed or MotionOutcome.Interrupted ||
            snapshot.Stages.Any(stage => stage.State is StageState.Failed or StageState.Interrupted) ||
            snapshot.Score.Source is not { LineNumbers: { } numbers } source) return null;
        var evidence = snapshot.Stages.Reverse().SelectMany(stage => stage.Evidence.Reverse())
            .FirstOrDefault(item => item.SourceLine == sourceLine && item.Focus is NoteConnectionFocus);
        if (evidence is not { OwnerLine: int owner, Focus: NoteConnectionFocus note } ||
            Line(owner) is null || Line(sourceLine) is null || note.ClosingLine is int close && Line(close) is null) return null;
        var width = Math.Clamp(columns - 1, 1, 240);
        var result = new List<InkLine>();
        var escaped = false;
        if (width < 20)
        {
            Label("Widen the terminal or use details for the recorded source.", Ink.Copper);
            return new(columns, result);
        }
        Label($";  Recorded source / line {sourceLine}", Ink.Copper);
        Label("   " + source.Label.Replace('\\', '/').Split('/').Last() + " / " +
            snapshot.Created.ToString("dd MMM yyyy  HH:mm:ss zzz", CultureInfo.InvariantCulture), Ink.Muted);
        result.Add(InkLine.Plain(""));
        WriteSource(owner, "  ;   ", Ink.Copper);
        Gap(owner, sourceLine);
        WriteSource(sourceLine, "  ;;  ", Ink.Signal);
        if (note.ClosingLine is int ending)
        {
            Gap(sourceLine, ending);
            WriteSource(ending, "  ╰   ", Ink.Copper);
        }
        else Label("   ?  Closing // missing / this grouping is unresolved", Ink.Copper);
        result.Add(InkLine.Plain(""));
        Label("   Recorded text / unchanged. Wrapped rows continue at │.", Ink.Muted);
        if (escaped) Label("   Controls shown as escapes; the recorded text is not changed.", Ink.Muted);
        Label("   source returns to the connection / details opens every check", Ink.Muted);
        return new(columns, result);

        InkLine? Line(int number)
        {
            var index = numbers.ToList().IndexOf(number);
            return index >= 0 && index < source.Lines.Count ? source.Lines[index] : null;
        }
        void Label(string text, Ink ink)
        {
            result.AddRange(TerminalText.Wrap(text, width).Select(line => InkLine.Plain(line, ink)));
        }
        void Gap(int from, int to)
        {
            var count = to - from - 1;
            if (count > 0) Label($"   ·  {count} {(count == 1 ? "line" : "lines")} between / not shown", Ink.Muted);
        }
        void WriteSource(int number, string marker, Ink tone)
        {
            escaped |= TerminalText.Safe(Line(number)!.Text) != Line(number)!.Text;
            var tokens = Tokens(Line(number)!).ToArray();
            var at = 0;
            do
            {
                // Only the first row carries the recorded line number. Continuation
                // gutters are presentation, not new source lines or inserted spaces.
                var gutter = at == 0 ? marker : "   │       ";
                var runs = new List<InkRun> { new(gutter, tone) };
                var used = gutter.Length;
                var end = at;
                var space = -1;
                while (end < tokens.Length && used + tokens[end].Width <= width)
                {
                    if (tokens[end].Text == " ") space = end;
                    used += tokens[end++].Width;
                }
                // Keep the break's spaces in the recorded row; prose wrapping
                // would trim them. Only a word too wide for a row is split.
                if (end < tokens.Length && space > at && tokens[end].Text != " ") end = space + 1;
                while (at < end)
                {
                    var token = tokens[at++];
                    if (runs.Count > 1 && runs[^1].Ink == token.Ink && runs[^1].Bold == token.Bold)
                        runs[^1] = runs[^1] with { Text = runs[^1].Text + token.Text };
                    else runs.Add(new(token.Text, token.Ink, token.Bold));
                }
                result.Add(new(runs.ToArray()));
            } while (at < tokens.Length);
        }
    }

    internal static IEnumerable<(string Text, int Width, Ink Ink, bool Bold)> Tokens(InkLine line)
    {
        var runs = line.Runs.Select(run => run with { Text = TerminalText.Safe(run.Text) }).ToArray();
        if (runs.Length == 0) yield break;
        var elements = StringInfo.GetTextElementEnumerator(string.Concat(runs.Select(run => run.Text)));
        var runIndex = 0;
        var end = runs[0].Text.Length;
        while (elements.MoveNext())
        {
            while (elements.ElementIndex >= end && runIndex + 1 < runs.Length) end += runs[++runIndex].Text.Length;
            // A grapheme is indivisible even when semantic highlights meet inside
            // it. The base character supplies the ink for the whole cluster.
            var run = runs[runIndex];
            foreach (var element in TerminalText.Elements(elements.GetTextElement()))
                yield return (element.Text, element.Width, run.Ink, run.Bold);
        }
    }
}
