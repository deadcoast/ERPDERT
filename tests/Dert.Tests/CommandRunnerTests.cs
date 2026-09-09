using Erpdert.Cli.Operations;
using Xunit;

namespace Dert.Tests;

public class CommandRunnerTests
{
    [Fact]
    public void Cancellation_does_not_start_a_command_that_was_already_cancelled()
    {
        using var fixture = new DiscoveryFixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var result = new SystemCommandRunner(cancellation.Token).Run(new(Shell,
            ["-NoProfile", "-NonInteractive", "-Command", "Write-Output 'should not run'"], fixture.Root, TimeSpan.FromSeconds(10)));
        Assert.Equal(130, result.ExitCode);
        Assert.Empty(result.Output);
        Assert.Contains("cancelled", result.Failure);
    }

    [Fact]
    public async Task Cancellation_stops_the_owned_running_child()
    {
        using var fixture = new DiscoveryFixture();
        using var cancellation = new CancellationTokenSource();
        var marker = Path.Combine(fixture.Root, "pid.txt");
        // Publish readiness only after the PID has been written and closed.
        // File.Exists can otherwise win the race against WriteAllText's content.
        var quotedMarker = marker.Replace("'", "''");
        var script = fixture.Write("wait.ps1", "[IO.File]::WriteAllText('" + quotedMarker + ".pending', $PID.ToString()); [IO.File]::Move('" + quotedMarker + ".pending', '" + quotedMarker + "'); Start-Sleep -Seconds 30");
        var task = Task.Run(() => new SystemCommandRunner(cancellation.Token).Run(new(Shell,
            ["-NoProfile", "-NonInteractive", "-File", script], fixture.Root, TimeSpan.FromSeconds(5))));
        try
        {
            var id = 0;
            Assert.True(SpinWait.SpinUntil(() =>
            {
                try { return int.TryParse(File.ReadAllText(marker), out id) && id > 0; }
                catch (IOException) { return false; } // Windows can still hold the renamed marker briefly.
            }, TimeSpan.FromSeconds(3)), "The owned child did not publish a readable PID.");
            using var process = System.Diagnostics.Process.GetProcessById(id);
            cancellation.Cancel();
            var result = await task;
            Assert.Equal(130, result.ExitCode);
            Assert.True(process.HasExited);
        }
        finally { cancellation.Cancel(); await task; }
    }
    private static string Shell => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");

    [Fact]
    public void Runs_the_named_program_with_literal_arguments_and_separate_output_streams()
    {
        using var fixture = new DiscoveryFixture();
        var script = fixture.Write("Ryan's script.ps1", "[Console]::Out.WriteLine($args[0]); [Console]::Error.WriteLine('evidence on stderr'); exit 7");
        var literal = "a '; Write-Output BAD; $thing [red]";
        var result = new SystemCommandRunner().Run(new(Shell, ["-NoProfile", "-NonInteractive", "-File", script, literal], fixture.Root, TimeSpan.FromSeconds(10)));
        Assert.Equal(7, result.ExitCode);
        Assert.Equal(literal, result.Output.Trim());
        Assert.Equal("evidence on stderr", result.Error.Trim());
        Assert.Null(result.Failure);
    }

    [Fact]
    public void Timeout_ends_the_owned_process_instead_of_hanging_the_rehearsal()
    {
        using var fixture = new DiscoveryFixture();
        var result = new SystemCommandRunner().Run(new(Shell, ["-NoProfile", "-NonInteractive", "-Command", "Start-Sleep -Seconds 30"], fixture.Root, TimeSpan.FromMilliseconds(200)));
        Assert.False(result.Succeeded);
        Assert.Contains("time limit", result.Failure);
    }

    [Fact]
    public void Excessive_output_is_bounded_and_is_not_accepted_as_complete_evidence()
    {
        using var fixture = new DiscoveryFixture();
        var result = new SystemCommandRunner().Run(new(Shell, ["-NoProfile", "-NonInteractive", "-Command", "[Console]::Out.Write(('x' * 600000))"], fixture.Root, TimeSpan.FromSeconds(10)));
        Assert.False(result.Succeeded);
        Assert.Contains("output limit", result.Failure);
        Assert.True(result.Output.Length <= 262144);
    }

    [Fact]
    public void Failure_to_start_returns_an_explained_result()
    {
        using var fixture = new DiscoveryFixture();
        var result = new SystemCommandRunner().Run(new(Path.Combine(fixture.Root, "absent.exe"), [], fixture.Root, TimeSpan.FromSeconds(1)));
        Assert.False(result.Succeeded);
        Assert.Contains("could not start", result.Failure);
    }
}
