using Dert.Cli;
using Dert.Cli.Discovery;
using Dert.Cli.Operations;
using Spectre.Console;
using Xunit;

namespace Dert.Tests;

public class UpdateCliTests
{
    [Fact]
    public void Help_exposes_the_update_request_and_its_rehearsal_effect()
    {
        var (exit, output) = Invoke(["-help", "--plain"], "");
        Assert.Equal(0, exit);
        Assert.Contains("DERT update codex updateme", output);
        Assert.Contains("commands ; updateme ;;", output);
        Assert.Contains("rehearsal", output);
        Assert.DoesNotContain("package operations are not implemented", output);
    }

    [Fact]
    public void Missing_tool_explains_why_no_update_can_be_rehearsed()
    {
        var (exit, output) = Run([], "");
        Assert.Equal(1, exit);
        Assert.Contains("UPDATE / REHEARSAL", output);
        Assert.Contains("No codex command", output);
        Assert.Contains("update command was not executed", output);
    }

    [Fact]
    public void Competing_copies_offer_literal_target_choices_without_running_either()
    {
        using var fixture = new DiscoveryFixture();
        var first = fixture.Package("Ryan's first", "1.0.0");
        var second = fixture.Package("second", "2.0.0");
        var (exit, output) = Run([], first + ";" + second);
        Assert.Equal(1, exit);
        Assert.Contains("CHOOSE THE COPY", output);
        Assert.Contains("--path", output);
        Assert.Contains("Ryan''s first", output);
        Assert.DoesNotContain(" install ", output);
    }

    [Fact]
    public void Missing_manager_is_explained_without_borrowing_a_different_npm()
    {
        using var fixture = new DiscoveryFixture();
        var prefix = fixture.Package("node", "1.0.0");
        File.Delete(Path.Combine(prefix, "npm.cmd"));
        var (exit, output) = Run([], prefix);
        Assert.Equal(1, exit);
        Assert.Contains("npm", output);
        Assert.Contains("ERP5202", output);
        Assert.DoesNotContain(" install ", output);
    }

    [Theory]
    [InlineData("commands ; updateme ;;")]
    [InlineData("commands;updateme;; // keep this annotation")]
    public void Dert_update_preview_binds_the_statement_without_querying_managers(string statement)
    {
        using var file = new ScratchDocument("STERT ; update ;; codex\n// why ; codex ;; Keep this copy current. //\n" + statement + "\nƒin.");
        var (exit, output) = Invoke(["run", file.Path, "--preview", "--plain"], "");
        Assert.Equal(0, exit);
        Assert.Contains("DERT update codex updateme", output);
        Assert.Contains("Keep this copy current", output);
        Assert.Contains("update rehearsal", output);
        Assert.DoesNotContain("ERP6004", output);
        Assert.Single(Directory.GetFiles(file.DirectoryPath));
    }

    [Theory]
    [InlineData("commands ; updateme ;; extra")]
    [InlineData("commands ; updateme ;;\nextra")]
    [InlineData("commands ; updateme ;;\ncommands ; updateme ;;")]
    public void Unhandled_update_arguments_are_cited_and_never_dropped(string statement)
    {
        using var file = new ScratchDocument("STERT ; update ;; codex\n" + statement);
        var (exit, output) = Invoke(["run", file.Path, "--preview", "--plain"], "");
        Assert.Equal(1, exit);
        Assert.Contains("ERP600", output);
        Assert.Contains("No operations ran", output);
    }

    [Fact]
    public void A_note_describing_updateme_does_not_request_it()
    {
        using var file = new ScratchDocument("STERT ; update ;; codex\n// commands ; updateme ;; later //");
        var (exit, output) = Invoke(["run", file.Path, "--preview", "--plain"], "");
        Assert.Equal(0, exit);
        Assert.DoesNotContain("STEP 01 / DERT update codex updateme", output);
    }

    [Fact]
    public void An_explicit_target_must_be_one_of_the_discovered_routes()
    {
        using var fixture = new DiscoveryFixture();
        var prefix = fixture.Package("node", "1.0.0");
        var (exit, output) = Run(["--path", Path.Combine(fixture.Root, "not-discovered.exe")], prefix);
        Assert.Equal(1, exit);
        Assert.Contains("ERP5201", output);
        Assert.DoesNotContain(" install ", output);
    }

    private static (int Exit, string Output) Run(string[] extra, string path) => Invoke(["update", "codex", "updateme", "--plain", .. extra], path);

    internal static (int Exit, string Output) Invoke(string[] args, string path, ICommandRunner? runner = null, int width = 200)
    {
        using var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings { Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors, Out = new AnsiConsoleOutput(writer) });
        console.Profile.Width = width;
        var exit = CliApplication.Run(args, console, invocation: ["DERT.exe"], discoveryEnvironment: new(path, ".EXE;.CMD"), commandRunner: runner);
        return (exit, writer.ToString());
    }
}
