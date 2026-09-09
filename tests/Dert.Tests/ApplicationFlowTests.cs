using Dert.Cli;
using Dert.Cli.Applications;
using Dert.Cli.Discovery;
using Dert.Cli.Doctoring;
using Spectre.Console;
using Xunit;

namespace Dert.Tests;

public class ApplicationFlowTests
{
    private const string Source = "STERT ; where ;; codex\ncommands ; doctor ;;\nƒin.";

    [Fact]
    public void Where_connects_exact_package_paths_without_starting_a_program()
    {
        using var fixture = new DoctorFixture();
        var result = Run(fixture, ["where", "codex", "--plain"]);
        Assert.Equal(0, result.Exit);
        Assert.Contains("codex.ps1", result.Output);
        Assert.Contains("node_modules", result.Output);
        Assert.Contains("DERT / where", result.Output);
        Assert.Empty(fixture.Calls);
    }

    [Theory]
    [InlineData("--doctor")]
    [InlineData("-dr")]
    public void Cli_doctor_saves_review_then_applies_that_exact_proposal(string flag)
    {
        using var fixture = new DoctorFixture();
        var result = Run(fixture, ["where", "codex", flag, "--plain"]);
        Assert.Equal(0, result.Exit);
        Assert.DoesNotContain(fixture.Calls, DoctorFixture.IsMutation);
        var token = SavedToken(fixture);
        Assert.Contains(token, result.Output);
        var applied = Run(fixture, ["where", "codex", flag, "--path", fixture.Launcher, "--apply", token, "--plain"]);
        Assert.Equal(0, applied.Exit);
        Assert.Equal(2, fixture.Calls.Count(DoctorFixture.IsMutation));
        Assert.Contains("Codex itself was not launched", applied.Output);
        Assert.Single(Directory.GetFiles(fixture.Receipts, "*.jsonl"));
    }

    [Theory]
    [InlineData("node")]
    [InlineData("source")]
    [InlineData("selection")]
    public void Saved_cli_approval_refuses_changed_inputs_before_queries(string change)
    {
        using var fixture = new DoctorFixture();
        using var source = new ScratchDocument(Source);
        Assert.Equal(0, Run(fixture, ["where", "codex", "--doctor", "--from-source", source.Path, "--plain"]).Exit);
        var token = SavedToken(fixture);
        fixture.Calls.Clear();
        if (change == "node") File.AppendAllText(Path.Combine(fixture.Prefix, "node.exe"), "changed");
        if (change == "source") File.AppendAllText(source.Path, "\n// changed");
        var selected = change == "selection" ? Path.Combine(fixture.Prefix, "different.cmd") : fixture.Launcher;
        Assert.Equal(1, Run(fixture, ["where", "codex", "--doctor", "--path", selected, "--apply", token, "--plain"]).Exit);
        Assert.Empty(fixture.Calls);
    }

    [Fact]
    public void Udl_doctor_retains_source_and_saves_a_source_bound_review()
    {
        using var fixture = new DoctorFixture();
        using var source = new ScratchDocument(Source);
        var bytes = File.ReadAllBytes(source.Path);
        var result = Run(fixture, ["run", source.Path, "--plain"]);
        Assert.Equal(0, result.Exit);
        var saved = DoctorService.LoadProposal(SavedToken(fixture), Path.Combine(State(fixture), "doctor-proposals"));
        Assert.Equal(source.Path, saved.Proposal!.Source!.Path);
        Assert.Equal(bytes, File.ReadAllBytes(source.Path));
        Assert.DoesNotContain(fixture.Calls, DoctorFixture.IsMutation);
    }

    [Fact]
    public void Mer_locates_and_reviews_the_source_selected_operation()
    {
        using var fixture = new DoctorFixture();
        using var source = new ScratchDocument(Source);
        var result = Run(fixture, ["mer", source.Path, "--review", "--plain"], new StringReader("at 2\nprobe\ndoctor\ndetails\n\n"));
        Assert.Equal(0, result.Exit);
        Assert.Contains("commands ; doctor ;;", result.Output);
        Assert.Contains("awaiting choice", result.Output);
        Assert.Contains(SavedToken(fixture), result.Output);
        Assert.DoesNotContain(fixture.Calls, DoctorFixture.IsMutation);
        Assert.Equal(Source, File.ReadAllText(source.Path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Mer_does_not_silently_replace_the_single_located_copy(bool addCopy)
    {
        using var fixture = new DoctorFixture();
        using var source = new ScratchDocument(Source);
        var extra = Path.Combine(fixture.Prefix, "another");
        var input = new ChangingInput("at 2\nprobe\ndoctor\n\n", 3, () =>
        {
            if (addCopy) fixture.Files.Package("doctor's prefix/another", "1.0.0");
            else File.WriteAllText(fixture.Metadata, File.ReadAllText(fixture.Metadata).Replace("1.0.0", "2.0.0"));
        });
        var result = Run(fixture, ["mer", source.Path, "--review", "--plain"], input, fixture.Prefix + ";" + extra);
        Assert.Equal(1, result.Exit);
        Assert.Contains("Probe again", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(fixture.Calls);
    }

    [Theory]
    [InlineData("Visual Studio Code", "'visual studio code'")]
    [InlineData("Tool(Preview)", "'tool(preview)'")]
    public void Displayed_application_arguments_are_copyable_in_PowerShell(string name, string argument)
    {
        Assert.True(ApplicationRequest.TryRead(["where", name], out var request, out _));
        Assert.Equal("DERT where " + argument, request!.Command);
    }

    [Fact]
    public void Cli_preserves_interrupted_doctor_exit_code()
    {
        using var fixture = new DoctorFixture { Fault = "root-cancelled" };
        var result = Run(fixture, ["where", "codex", "-dr", "--plain"]);
        Assert.Equal(130, result.Exit);
        Assert.Single(fixture.Calls);
    }

    [Fact]
    public void Mer_requires_an_explicit_copy_when_the_path_search_is_incomplete()
    {
        using var fixture = new DoctorFixture();
        using var source = new ScratchDocument(Source);
        var result = Run(fixture, ["mer", source.Path, "--review", "--plain"],
            new StringReader("at 2\nprobe\ndoctor\n\n"), fixture.Prefix + ";relative-folder");
        Assert.Equal(1, result.Exit);
        Assert.Contains("explicitly choose a copy", result.Output);
        Assert.Empty(fixture.Calls);
    }

    private static string State(DoctorFixture fixture) => Path.Combine(fixture.Prefix, "dert-state");
    private static string SavedToken(DoctorFixture fixture) => Path.GetFileNameWithoutExtension(Assert.Single(Directory.GetFiles(Path.Combine(State(fixture), "doctor-proposals"), "*.json")));
    private static (int Exit, string Output) Run(DoctorFixture fixture, string[] args, TextReader? input = null, string? searchPath = null)
    {
        fixture.Receipts = Path.Combine(State(fixture), "doctor-receipts");
        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings { Ansi = AnsiSupport.No, Interactive = InteractionSupport.No, Out = new AnsiConsoleOutput(writer) });
        console.Profile.Width = 160; console.Profile.Height = 40;
        var exit = CliApplication.Run(args, console, commandRunner: fixture, merInput: input,
            discoveryEnvironment: new(searchPath ?? fixture.Prefix, ".CMD;.EXE"), doctorStateDirectory: State(fixture));
        return (exit, writer.ToString());
    }
    private sealed class ChangingInput(string text, int at, Action change) : StringReader(text)
    {
        private int reads;
        public override string? ReadLine() { if (++reads == at) change(); return base.ReadLine(); }
    }
}
