using Dert.Cli;
using Spectre.Console;
using Xunit;

namespace Dert.Tests;

[CollectionDefinition("Doctor process environment", DisableParallelization = true)]
public sealed class DoctorProcessEnvironmentCollection;

[Collection("Doctor process environment")]
public class DoctorLiveEnvironmentTests
{
    [Fact]
    public void Production_cli_reader_detects_environment_drift_during_a_manager_query()
    {
        using var fixture = new ConflictFixture();
        WithEnvironment(fixture.Environment.SearchPath, fixture.Environment.PathExtensions, () =>
        {
            fixture.AfterRequest = _ => Environment.SetEnvironmentVariable("PATH", fixture.Later + ";" + fixture.Doctor.Prefix);
            var result = Run(fixture, ["where", "codex", "--doctor", "--path", fixture.Doctor.Launcher, "--plain"]);
            Assert.NotEqual(0, result.Exit);
            Assert.Contains("changed", result.Output, StringComparison.OrdinalIgnoreCase);
            Assert.Single(fixture.Doctor.Calls);
            Assert.DoesNotContain("CONFLICT CONFIRMED", result.Output);
            Assert.False(Directory.Exists(Path.Combine(fixture.Doctor.Files.Root, "state", "doctor-proposals")));
        });
    }

    [Fact]
    public void Udl_doctor_reads_the_current_environment_for_each_operation()
    {
        using var fixture = new ConflictFixture();
        using var source = new ScratchDocument("STERT ; where ;; codex\ncommands ; doctor ;;\nƒin.\nSTERT ; where ;; codex\ncommands ; doctor ;;\nƒin.");
        File.WriteAllText(fixture.Entrypoint, "initially intact");
        WithEnvironment(fixture.Doctor.Prefix, fixture.Environment.PathExtensions, () =>
        {
            fixture.AfterRequest = _ =>
            {
                fixture.AfterRequest = null;
                File.Delete(fixture.Entrypoint);
                Environment.SetEnvironmentVariable("PATH", fixture.Environment.SearchPath);
            };
            var result = Run(fixture, ["run", source.Path, "--plain"]);
            Assert.Contains("CONFLICT CONFIRMED / external PATH", result.Output);
            Assert.DoesNotContain(fixture.Doctor.Calls, DoctorFixture.IsMutation);
        });
    }

    private static void WithEnvironment(string path, string extensions, Action action)
    {
        var previousPath = Environment.GetEnvironmentVariable("PATH");
        var previousExtensions = Environment.GetEnvironmentVariable("PATHEXT");
        try
        {
            Environment.SetEnvironmentVariable("PATH", path);
            Environment.SetEnvironmentVariable("PATHEXT", extensions);
            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", previousPath);
            Environment.SetEnvironmentVariable("PATHEXT", previousExtensions);
        }
    }
    private static (int Exit, string Output) Run(ConflictFixture fixture, string[] args)
    {
        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings { Ansi = AnsiSupport.No, Interactive = InteractionSupport.No, Out = new AnsiConsoleOutput(writer) });
        console.Profile.Width = 180;
        var exit = CliApplication.Run(args, console, commandRunner: fixture,
            doctorStateDirectory: Path.Combine(fixture.Doctor.Files.Root, "state"));
        return (exit, writer.ToString());
    }
}
