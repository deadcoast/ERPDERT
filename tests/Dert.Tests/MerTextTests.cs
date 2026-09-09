using Erpdert.Mer;
using Xunit;

namespace Dert.Tests;

public class MerTextTests
{
    [Theory]
    [InlineData("hello", 5)]
    [InlineData("cafe\u0301", 4)]
    [InlineData("日本語", 6)]
    [InlineData("👩‍💻", 2)]
    [InlineData("🇨🇦", 2)]
    [InlineData("1️⃣", 2)]
    public void Layout_counts_display_cells_not_utf16_units(string value, int width) =>
        Assert.Equal(width, TerminalText.Width(value));

    [Theory]
    [InlineData("A日本Z", 4, "A日…")]
    [InlineData("e\u0301ab", 2, "e\u0301…")]
    [InlineData("👩‍💻ab", 3, "👩‍💻…")]
    [InlineData("abc", 0, "")]
    [InlineData("abc", 1, "…")]
    [InlineData("abc", 3, "abc")]
    public void Clipping_keeps_whole_graphemes_and_fits_the_requested_cells(string value, int width, string expected) =>
        Assert.Equal(expected, TerminalText.Clip(value, width));

    [Fact]
    public void Terminal_and_direction_controls_cannot_execute_through_evidence()
    {
        var safe = TerminalText.Safe("a\u001b[2J\r\n\t\u009b0m\u202Eevil\u2028next");
        Assert.Equal("a\\u001B[2J\\r\\n\\t\\u009B0m\\u202Eevil\\u2028next", safe);
        Assert.DoesNotContain('\u001b', safe);
    }
}
