using Dert.Cli.Authoring;
using Xunit;

namespace Dert.Tests;

public class AuthoringLaunchTests
{
    [Fact]
    public void Launch_uses_the_packaged_host_and_literal_absolute_arguments()
    {
        var folder = Path.Combine(Path.GetTempPath(), "DERT with spaces");
        var source = "my [notes] ' & $(not a command).DERT";
        var start = AuthoringCommand.LaunchInfo([source, "--still"], folder);
        Assert.Equal(Path.Combine(folder, "notes", "DERT.Notes.exe"), start.FileName);
        Assert.False(start.UseShellExecute);
        Assert.Equal(new[] { Path.GetFullPath(source), "--still" }, start.ArgumentList);
        Assert.Empty(start.Arguments);
    }

    [Theory]
    [InlineData("--apply")]
    [InlineData("--still", "--still")]
    [InlineData("first.DERT", "second.DERT")]
    public void Unsupported_arguments_cannot_be_forwarded(params string[] args)
        => Assert.Throws<ArgumentException>(() => AuthoringCommand.LaunchInfo(args, AppContext.BaseDirectory));
}
