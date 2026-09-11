using Dert.Cli;
using Dert.Cli.Doctoring;
using Spectre.Console;
using Xunit;

namespace Dert.Tests;

public class DoctorConflictFlowTests
{
    private const string Source = "STERT ; where ;; codex\n// support doctoring ; conflicts ;;\n// possible_conflicts, // Several copies deserve a closer look.\n// conflicts, // Show the route before calling it broken.\n// fixme // Keep the reason beside the correction I choose.\n//\ncommands ; doctor ;;\nƒin.";

    [Theory]
    [InlineData("cli")]
    [InlineData("udl")]
    [InlineData("mer")]
    public void Every_entry_point_diagnoses_before_copy_selection_without_npm_queries(string entry)
    {
        using var fixture = new ConflictFixture();
        using var source = new ScratchDocument(Source);
        var original = File.ReadAllBytes(source.Path);
        var args = entry == "cli" ? new[] { "where", "codex", "--doctor", "--from-source", source.Path, "--plain" } :
            entry == "udl" ? ["run", source.Path, "--plain"] : ["mer", source.Path, "--review", "--plain"];
        var result = Run(fixture, args, entry == "mer" ? "at 7\nprobe\ndoctor\ndetails\n\n" : null);
        Assert.Equal(1, result.Exit);
        Assert.Contains("CONFLICT CONFIRMED / external PATH", result.Output);
        Assert.Contains("FIXME / review the broken first copy", result.Output);
        Assert.Contains("--path '" + fixture.Doctor.Launcher.Replace("'", "''") + "'", result.Output);
        Assert.Contains("--from-source '" + source.Path.Replace("'", "''") + "'", result.Output);
        Assert.Empty(fixture.Doctor.Calls);
        Assert.Equal(original, File.ReadAllBytes(source.Path));
        if (entry == "mer") Assert.Contains("commands ; doctor ;;", result.Output);
    }

    [Theory]
    [InlineData("--plain")]
    [InlineData("--still")]
    public void Chosen_source_bound_repair_records_the_external_route_then_rediagnoses_without_stale_conflict(string mode)
    {
        using var fixture = new ConflictFixture();
        using var source = new ScratchDocument(Source);
        var review = Run(fixture, ["where", "codex", "--doctor", "--path", fixture.Doctor.Launcher, "--from-source", source.Path, mode]);
        Assert.Equal(0, review.Exit);
        var token = SavedToken(fixture);
        Assert.DoesNotContain(fixture.Doctor.Calls, DoctorFixture.IsMutation);
        var result = Run(fixture, ["where", "codex", "--doctor", "--path", fixture.Doctor.Launcher, "--from-source", source.Path, "--apply", token, mode]);
        Assert.True(result.Exit == 0, result.Output);
        Assert.Contains("EXTERNAL ROUTE / REPAIR VERIFIED", result.Output);
        Assert.Equal(2, fixture.Doctor.Calls.Count(DoctorFixture.IsMutation));
        Assert.Equal(Source, File.ReadAllText(source.Path));
        fixture.Doctor.Calls.Clear();
        var repeated = Run(fixture, ["where", "codex", "--doctor", mode]);
        Assert.Contains("POSSIBLE CONFLICT / evidence only", repeated.Output);
        Assert.DoesNotContain("CONFLICT CONFIRMED", repeated.Output);
        Assert.Empty(fixture.Doctor.Calls);
    }

    [Fact]
    public void Mer_selected_conflict_proposal_keeps_its_source_and_pending_approval()
    {
        using var fixture = new ConflictFixture();
        using var source = new ScratchDocument(Source);
        var result = Run(fixture, ["mer", source.Path, "--review", "--plain"], "at 7\nprobe\ncopy 1\ndoctor\ndetails\n\n");
        Assert.Contains("awaiting choice", result.Output);
        Assert.Contains(SavedToken(fixture), result.Output);
        Assert.Contains("--from-source '" + source.Path.Replace("'", "''") + "'", result.Output);
        Assert.Contains("CONFLICT CONFIRMED / external PATH", result.Output);
        Assert.DoesNotContain(fixture.Doctor.Calls, DoctorFixture.IsMutation);
        Assert.Equal(Source, File.ReadAllText(source.Path));
    }

    [Theory]
    [InlineData("environment")]
    [InlineData("earlier-route")]
    [InlineData("later-entrypoint")]
    [InlineData("source")]
    public void Persisted_cli_conflict_approval_refuses_changed_evidence_before_queries(string change)
    {
        using var fixture = new ConflictFixture();
        using var source = new ScratchDocument(Source);
        Assert.Equal(0, Run(fixture, ["where", "codex", "--doctor", "--path", fixture.Doctor.Launcher, "--from-source", source.Path, "--plain"]).Exit);
        var token = SavedToken(fixture);
        fixture.Doctor.Calls.Clear();
        if (change == "environment") fixture.Environment = fixture.Environment with { SearchPath = fixture.Later + ";" + fixture.Environment.SearchPath };
        if (change == "earlier-route") File.WriteAllText(Path.Combine(fixture.Earlier, "codex.exe"), "new route");
        if (change == "later-entrypoint") File.AppendAllText(Path.Combine(fixture.Later, "node_modules", "@openai", "codex", "bin", "codex.js"), "changed");
        if (change == "source") File.AppendAllText(source.Path, "\n// new reason");
        var applied = Run(fixture, ["where", "codex", "--doctor", "--path", fixture.Doctor.Launcher, "--from-source", source.Path, "--apply", token, "--plain"]);
        Assert.Equal(1, applied.Exit);
        Assert.DoesNotContain("REPAIR VERIFIED", applied.Output);
        Assert.Empty(fixture.Doctor.Calls);
        Assert.False(File.Exists(fixture.Entrypoint));
    }

    private static string State(ConflictFixture fixture) => Path.Combine(fixture.Doctor.Files.Root, "state");
    private static string SavedToken(ConflictFixture fixture) => Path.GetFileNameWithoutExtension(Assert.Single(Directory.GetFiles(Path.Combine(State(fixture), "doctor-proposals"), "*.json")));
    private static (int Exit, string Output) Run(ConflictFixture fixture, string[] args, string? input = null)
    {
        fixture.Doctor.Receipts = Path.Combine(State(fixture), "doctor-receipts");
        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings { Ansi = AnsiSupport.No, Interactive = InteractionSupport.No, Out = new AnsiConsoleOutput(writer) });
        console.Profile.Width = 180; console.Profile.Height = 40;
        var exit = CliApplication.Run(args, console, commandRunner: fixture, merInput: input is null ? null : new StringReader(input),
            discoveryEnvironment: fixture.Environment, doctorStateDirectory: State(fixture));
        return (exit, writer.ToString());
    }
}
