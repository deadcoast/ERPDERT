using Dert.Cli;
using Dert.Cli.Authoring;
using Spectre.Console;
using Xunit;

namespace Dert.Tests;

public sealed class DesktopGuideTests
{
    [Fact]
    public void Guide_launch_selects_the_help_surface_and_keeps_literal_paths_and_motion_preference()
    {
        var folder = Path.Combine(Path.GetTempPath(), "DERT with spaces");
        const string source = "my [notes] ' & $(literal).DERT";
        var guide = AuthoringCommand.LaunchInfo([source, "--still"], folder, guide: true);
        Assert.Equal(Path.Combine(folder, "notes", "DERT.Notes.exe"), guide.FileName);
        Assert.False(guide.UseShellExecute);
        Assert.Equal(new[] { "--guide", Path.GetFullPath(source), "--still" }, guide.ArgumentList);
        Assert.Empty(guide.Arguments);
        Assert.DoesNotContain("--guide", AuthoringCommand.LaunchInfo([source], folder).ArgumentList);
    }

    [Fact]
    public void Guide_arguments_are_checked_by_the_native_host_route_before_launching()
    {
        using var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No, Out = new AnsiConsoleOutput(writer)
        });
        console.Profile.Width = 200;
        Assert.Equal(2, CliApplication.Run(["guide", "--unknown"], console));
        Assert.Contains("--still", writer.ToString());
    }
}
