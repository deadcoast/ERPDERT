using System.Text;

namespace Dert.Mer;

public enum MerColorMode { None, Ansi16, TrueColor }
public readonly record struct TerminalSize(int Columns, int Rows);
public sealed class MerOutputException(Exception inner) : Exception("The terminal output could not be written.", inner);

/// <summary>Owns only the rows it appends; never an alternate screen or the user's prompt.</summary>
public sealed class InlineTerminal : IDisposable
{
    private readonly TextWriter output;
    private readonly Func<TerminalSize> measure;
    private readonly MerColorMode colors;
    private string[] previous = [];
    private TerminalSize previousSize;
    private bool hidden;
    private bool finished;

    public InlineTerminal(TextWriter output, Func<TerminalSize> measure, MerColorMode colors)
    {
        this.output = output;
        this.measure = measure;
        this.colors = colors;
    }

    public void Draw(MerFrame frame)
    {
        ObjectDisposedException.ThrowIf(finished, this);
        var size = measure();
        if (frame.Columns != size.Columns || frame.Rows is int rows && rows != size.Rows) return; // A resize won the race with composition. Try the next frame.
        if (frame.Lines.Count > size.Rows - 2 || frame.Lines.Any(line => TerminalText.Width(line.Text) >= size.Columns))
            throw new ArgumentException("A live frame must fit inside the current terminal with a spare column and two spare rows.", nameof(frame));
        var encoded = frame.Lines.Select(Encode).ToArray();
        var batch = new StringBuilder();
        if (!hidden) { batch.Append("\u001b[?25l"); hidden = true; }
        if (previous.Length == 0 || previousSize != size)
        {
            // Reflow destroys the old row-to-buffer mapping. Never guess upward.
            // Freeze that view in scrollback and establish a fresh owned region.
            batch.Append("\r\n");
            foreach (var row in encoded) batch.Append(row).Append("\r\n");
        }
        else if (previous.Length != encoded.Length)
        {
            // A different layout height is not terminal reflow. Its anchor is
            // still ours: repaint in place, clear retired rows, then leave the
            // cursor immediately below the new region. Never delete buffer rows.
            batch.Append($"\u001b[{previous.Length}A\r");
            var extent = Math.Max(previous.Length, encoded.Length);
            for (var i = 0; i < extent; i++)
                batch.Append("\u001b[2K").Append(i < encoded.Length ? encoded[i] : "").Append("\r\n");
            if (extent > encoded.Length) batch.Append($"\u001b[{extent - encoded.Length}A\r");
        }
        else
        {
            for (var i = 0; i < encoded.Length; i++)
                if (!string.Equals(previous[i], encoded[i], StringComparison.Ordinal))
                    batch.Append($"\u001b[{encoded.Length - i}A\r\u001b[2K")
                        .Append(encoded[i]).Append($"\r\u001b[{encoded.Length - i}B");
        }
        if (batch.Length > 0) Write(batch.ToString());
        previous = encoded;
        previousSize = size;
    }

    public void Finish(IReadOnlyList<InkLine> receipt)
    {
        if (finished) return;
        var batch = new StringBuilder();
        if (previous.Length > 0 && previousSize == measure())
        {
            batch.Append($"\u001b[{previous.Length}A\r");
            foreach (var _ in previous) batch.Append("\u001b[2K\r\n");
            batch.Append($"\u001b[{previous.Length}A\r");
        }
        else if (previous.Length > 0) batch.Append("\r\n");
        foreach (var line in receipt) batch.Append(Encode(line)).Append("\r\n");
        if (hidden) batch.Append("\u001b[0m\u001b[?25h");
        Write(batch.ToString());
        hidden = false;
        previous = [];
        finished = true;
    }

    public void Dispose()
    {
        if (hidden)
        {
            try { Write("\u001b[0m\u001b[?25h"); }
            catch (MerOutputException) { /* A closed pipe cannot receive terminal cleanup. */ }
            hidden = false;
        }
        finished = true;
    }

    private string Encode(InkLine line)
    {
        if (colors == MerColorMode.None) return TerminalText.Safe(line.Text);
        var buffer = new StringBuilder();
        foreach (var run in line.Runs)
        {
            buffer.Append("\u001b[0m");
            if (run.Bold) buffer.Append("\u001b[1m");
            if (run.Ink != Ink.Text)
            {
                var color = colors == MerColorMode.Ansi16 ? run.Ink switch
                {
                    Ink.Muted => "90",
                    Ink.Copper => "33",
                    Ink.Signal => "95",
                    Ink.Good => "94",
                    _ => "31"
                } : run.Ink switch
                {
                    // Match MIVIM minimal's amber, violet, blue and quiet type.
                    // The user's terminal still owns its background and base text.
                    Ink.Muted => "38;2;144;144;159",
                    Ink.Copper => "38;2;231;177;125",
                    Ink.Signal => "38;2;181;167;239",
                    Ink.Good => "38;2;144;179;235",
                    _ => "38;2;239;143;130"
                };
                buffer.Append("\u001b[").Append(color).Append('m');
            }
            buffer.Append(TerminalText.Safe(run.Text));
        }
        return buffer.Append("\u001b[0m").ToString();
    }

    private void Write(string text)
    {
        try { output.Write(text); output.Flush(); }
        catch (Exception error) when (error is IOException or ObjectDisposedException) { throw new MerOutputException(error); }
    }
}
