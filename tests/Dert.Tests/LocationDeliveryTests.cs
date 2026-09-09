using Dert.Mer;
using Xunit;

namespace Dert.Tests;

public class LocationDeliveryTests
{
    [Fact]
    public void Wrapped_paths_keep_internal_spaces_and_their_connection_margin()
    {
        var path = "C:\\Apps\\" + new string('x', 100) + "\\two  spaces\\codex.exe";
        var view = new LocationView("Codex", DateTimeOffset.UnixEpoch, [],
            [new("Paths", [InkLine.Plain("  |   " + path)])], "Located", "No programs started");
        var parts = LocationCompositor.Receipt(view, 60).Where(line => line.Text.StartsWith(" |    |   ")).ToArray();
        Assert.True(parts.Length > 1);
        var reconstructed = string.Concat(parts.Select(line => line.Text[10..^2].TrimEnd(' ')));
        Assert.Equal(path, reconstructed);
    }
    [Theory]
    [InlineData(32)]
    [InlineData(60)]
    [InlineData(80)]
    [InlineData(140)]
    public void Location_receipts_retain_path_ends_and_leave_the_terminal_spare_column(int columns)
    {
        var path = "C:\\" + new string('x', 180) + "\\日本語\\codex.exe";
        var view = new LocationView("Codex", DateTimeOffset.UnixEpoch,
            [InkLine.Plain("Two distinct installation groups")],
            [new("COMMAND -> PACKAGE", [InkLine.Plain(path), InkLine.Plain("7.2 MiB / partial: unreadable folder")])],
            "Located / needs attention", "No programs started");
        var receipt = LocationCompositor.Receipt(view, columns);
        Assert.All(receipt, line => Assert.True(TerminalText.Width(line.Text) < columns, line.Text));
        Assert.Contains(receipt, line => line.Text.Contains("codex.exe", StringComparison.Ordinal));
        Assert.Contains(receipt, line => line.Text.Contains("partial", StringComparison.Ordinal));
        Assert.Contains(receipt, line => line.Text.Contains("No programs started", StringComparison.Ordinal));
    }
}
