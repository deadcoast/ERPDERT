using Erpdert.Mer;
using Xunit;

namespace Dert.Tests;

public class MerTerminalTests
{
    [Theory]
    [InlineData(4, 2)]
    [InlineData(2, 4)]
    [InlineData(20, 16)]
    [InlineData(16, 20)]
    public void A_layout_height_change_reuses_owned_rows_without_leaving_an_obsolete_view(int before, int after)
    {
        var output = new StringWriter();
        var screen = new MerTestScreen();
        screen.Feed("PS> command\r\n");
        using var terminal = new InlineTerminal(output, () => new(80, 24), MerColorMode.None);
        terminal.Draw(new(80, Enumerable.Range(0, before).Select(i => InkLine.Plain($"obsolete {i}")).ToArray()));
        var frame = new MerFrame(80, Enumerable.Range(0, after).Select(i => InkLine.Plain($"source focus {i}")).ToArray());
        terminal.Draw(frame);
        var drawn = output.ToString();
        screen.Feed(drawn);
        Assert.Contains("PS> command", screen.Text);
        Assert.DoesNotContain("obsolete", screen.Text);
        Assert.Contains($"source focus {after - 1}", screen.Text);
        terminal.Draw(frame);
        Assert.Equal(drawn, output.ToString());
        terminal.Finish([InkLine.Plain("final receipt")]);
        screen.Feed(output.ToString()[drawn.Length..]);
        Assert.Contains("PS> command", screen.Text);
        Assert.Contains("final receipt", screen.Text);
        Assert.DoesNotContain("source focus", screen.Text);
        Assert.True(screen.CursorVisible);
    }

    [Fact]
    public void A_height_resize_between_composition_and_write_discards_the_stale_frame()
    {
        var journal = new MotionJournal(new("Check", "Read-only", [new("read", "Read", "Reading")]), DateTimeOffset.UnixEpoch);
        var frame = MerCompositor.Live(journal.Snapshot(TimeSpan.Zero), 80, 24);
        var output = new StringWriter();
        using var terminal = new InlineTerminal(output, () => new(80, 8), MerColorMode.None);
        terminal.Draw(frame);
        Assert.Equal("", output.ToString());
    }
    [Fact]
    public void An_unchanged_frame_writes_nothing_and_a_change_repaints_only_its_row()
    {
        var output = new StringWriter();
        using var terminal = new InlineTerminal(output, () => new(80, 24), MerColorMode.TrueColor);
        var frame = new MerFrame(80, [InkLine.Plain("stable request"), InkLine.Plain("working", Ink.Signal)]);
        terminal.Draw(frame);
        var first = output.ToString();
        terminal.Draw(frame);
        Assert.Equal(first, output.ToString());
        terminal.Draw(new(80, [InkLine.Plain("stable request"), InkLine.Plain("checked", Ink.Good)]));
        var delta = output.ToString()[first.Length..];
        Assert.Contains("checked", delta);
        Assert.DoesNotContain("stable request", delta);
        Assert.DoesNotContain("\u001b[2J", output.ToString());
        Assert.DoesNotContain("1049", output.ToString());
    }

    [Fact]
    public void Resizing_abandons_old_coordinates_instead_of_erasing_reflowed_history()
    {
        var output = new StringWriter();
        var size = new TerminalSize(80, 24);
        using var terminal = new InlineTerminal(output, () => size, MerColorMode.None);
        terminal.Draw(new(80, [InkLine.Plain("source"), InkLine.Plain("working")]));
        var length = output.ToString().Length;
        size = new(45, 12);
        terminal.Draw(new(45, [InkLine.Plain("continued")]));
        var afterResize = output.ToString()[length..];
        Assert.Contains("continued", afterResize);
        Assert.DoesNotContain("A", afterResize); // This fixture has no letter A; CSI n A would move into old rows.
        Assert.DoesNotContain("\u001b[2K", afterResize);
    }

    [Fact]
    public void Finalization_erases_stale_live_rows_keeps_the_prompt_and_restores_the_cursor()
    {
        var output = new StringWriter();
        var screen = new MerTestScreen();
        screen.Feed("PS> command\r\n");
        using (var terminal = new InlineTerminal(output, () => new(80, 24), MerColorMode.None))
        {
            terminal.Draw(new(80, [InkLine.Plain("long obsolete row"), InkLine.Plain("old detail")]));
            terminal.Draw(new(80, [InkLine.Plain("short"), InkLine.Plain("")]));
            terminal.Finish([InkLine.Plain("receipt / checked")]);
        }
        screen.Feed(output.ToString());
        Assert.Contains("PS> command", screen.Text);
        Assert.Contains("receipt / checked", screen.Text);
        Assert.DoesNotContain("obsolete", screen.Text);
        Assert.DoesNotContain("old detail", screen.Text);
        Assert.True(screen.CursorVisible);
    }

    [Fact]
    public void Data_cannot_inject_terminal_commands_even_into_a_final_receipt()
    {
        var output = new StringWriter();
        using var terminal = new InlineTerminal(output, () => new(80, 24), MerColorMode.None);
        terminal.Finish([InkLine.Plain("hostile\u001b]52;c;clipboard\u0007\nforged")]);
        Assert.Contains("\\u001B]52", output.ToString());
        Assert.Contains("\\nforged", output.ToString());
        Assert.DoesNotContain('\u001b', output.ToString());
    }

    [Fact]
    public void Oversized_frames_are_refused_before_touching_the_terminal()
    {
        var output = new StringWriter();
        using var terminal = new InlineTerminal(output, () => new(10, 5), MerColorMode.None);
        Assert.Throws<ArgumentException>(() => terminal.Draw(new(10, [InkLine.Plain("too wide to fit")])));
        Assert.Equal("", output.ToString());
    }
}

// Tiny independently implemented screen interpreter, not a production renderer mock.
internal sealed class MerTestScreen
{
    private readonly List<List<char>> lines = [[]];
    private int row;
    private int column;
    public bool CursorVisible { get; private set; } = true;
    public string Text => string.Join('\n', lines.Select(line => new string(line.ToArray())));

    public void Feed(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '\u001b')
            {
                Assert.Equal('[', text[++i]);
                var parameters = "";
                while (++i < text.Length && text[i] is not (>= '@' and <= '~')) parameters += text[i];
                var command = text[i];
                if (command == 'm') continue;
                if (parameters == "?25") { CursorVisible = command == 'h'; continue; }
                var number = int.TryParse(parameters, out var value) ? value : 1;
                switch (command)
                {
                    case 'A': row -= number; Assert.True(row >= 0, "Renderer moved above its own region."); break;
                    case 'B': row += number; EnsureRow(); break;
                    case 'K': Assert.Equal(2, number); lines[row].Clear(); break;
                    default: Assert.Fail("Unexpected terminal operation: " + command); break;
                }
            }
            else if (ch == '\r') column = 0;
            else if (ch == '\n') { row++; EnsureRow(); }
            else
            {
                while (lines[row].Count <= column) lines[row].Add(' ');
                lines[row][column++] = ch;
            }
        }
    }
    private void EnsureRow() { while (lines.Count <= row) lines.Add([]); }
}
