using Dert.Cli;
using Dert.Cli.Applications;
using Dert.Cli.Operations;
using Dert.Cli.Updates;
using Dert.Cli.Transactions;
using Spectre.Console;
using System.Text.RegularExpressions;
using Xunit;

namespace Dert.Tests;

public class UpdateTransactionCliTests
{
    [Theory]
    [InlineData("update")]
    [InlineData("where")]
    public void Approval_requires_an_operation_and_selected_path(string command)
    {
        Assert.False(ApplicationRequest.TryRead([command, "codex", "--apply", new string('A', 64)], out _, out _));
        Assert.False(ApplicationRequest.TryRead([command, "codex", command == "update" ? "updateme" : "--doctor",
            "--apply", new string('A', 64)], out _, out _));
    }

    [Theory]
    [InlineData("cli")]
    [InlineData("run")]
    [InlineData("mer")]
    public void Each_surface_saves_a_proposal_and_the_exact_approval_updates_only_that_copy(string surface)
    {
        using var fixture = new DoctorFixture { Latest = "2.0.0" };
        using var source = new ScratchDocument("STERT ; update ;; codex\n// Keep the selected copy.\ncommands ; updateme ;;\nƒin.");
        var state = Path.Combine(fixture.Files.Root, "state");
        fixture.Receipts = Path.Combine(state, "update-receipts");
        var runner = new InstalledInventory(fixture);
        string[] args = surface switch
        {
            "run" => ["run", source.Path],
            "mer" => ["mer", source.Path, "--review"],
            _ => ["update", "codex", "updateme", "--path", fixture.Launcher]
        };
        var preview = Invoke(args, fixture, state, runner, "at 3\nrehearse\ncheck\ndetails\n\n");
        Assert.True(preview.Exit == 0, preview.Output);
        Assert.Contains("UPDATE PROPOSAL / awaiting your choice", preview.Output);
        Assert.DoesNotContain(fixture.Calls, DoctorFixture.IsMutation);
        var token = Regex.Match(preview.Output, @"--apply ([A-F0-9]{64})").Groups[1].Value;
        Assert.Equal(64, token.Length);
        var saved = UpdateTransaction.LoadProposal(token, Path.Combine(state, "update-proposals"));
        Assert.Equal(surface == "cli" ? null : source.Path, saved.Proposal!.Source?.Path);
        var apply = Approval(saved.Proposal);
        fixture.Calls.Clear();
        var result = Invoke(apply, fixture, state, runner);
        Assert.True(result.Exit == 0, result.Output);
        Assert.Contains("UPDATE / VERIFIED", result.Output);
        var mutation = Assert.Single(fixture.Calls, DoctorFixture.IsMutation);
        Assert.Equal("install", mutation.Arguments[1]);
        Assert.Contains("@openai/codex@2.0.0", mutation.Arguments);
        Assert.DoesNotContain(fixture.Calls, call => call.Arguments[1] == "view");
        Assert.Single(Directory.GetFiles(fixture.Receipts, "*.jsonl"));
        Assert.Contains("Keep the selected copy.", File.ReadAllText(source.Path));
        fixture.Calls.Clear();
        Assert.Equal(1, Invoke(apply, fixture, state, runner).Exit);
        Assert.Empty(fixture.Calls);
    }

    [Fact]
    public void Cancellation_between_discovery_and_rehearsal_starts_no_manager_query()
    {
        using var fixture = new DoctorFixture { Latest = "2.0.0" };
        using var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings { Ansi = AnsiSupport.No, Out = new AnsiConsoleOutput(writer) });
        var renderer = new DocumentRenderer(console, true, ["DERT.exe"], true);
        var discovery = Dert.Cli.Discovery.CodexDiscovery.Scan(new(fixture.Prefix, ".CMD;.EXE"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => renderer.RehearseUpdate(discovery, fixture.Launcher, fixture,
            stateDirectory: Path.Combine(fixture.Files.Root, "state"), cancellationToken: cancellation.Token));
        Assert.Empty(fixture.Calls);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("source-path")]
    [InlineData("route")]
    [InlineData("action")]
    public void Changed_or_mismatched_approval_stops_before_any_manager_process(string change)
    {
        using var fixture = new DoctorFixture { Latest = "2.0.0" };
        using var file = new ScratchDocument("STERT ; update ;; codex\ncommands ; updateme ;;\nƒin.");
        var state = Path.Combine(fixture.Files.Root, "state");
        var report = UpdateTransaction.Inspect(Dert.Cli.Discovery.CodexDiscovery.Scan(new(fixture.Prefix, ".CMD;.EXE")),
            fixture.Launcher, fixture, source: NpmSource.Capture(file.Path));
        UpdateTransaction.SaveProposal(report, Path.Combine(state, "update-proposals"));
        var proposal = report.Proposal!;
        var args = Approval(proposal);
        if (change == "source") File.AppendAllText(file.Path, "\n// Edited after rehearsal");
        if (change == "source-path") args[Array.IndexOf(args, "--from-source") + 1] = Path.Combine(file.DirectoryPath, "different.DERT");
        if (change == "route") args[Array.IndexOf(args, "--path") + 1] = Path.Combine(fixture.Prefix, "codex.cmd");
        if (change == "action") { args[0] = "where"; args[2] = "--doctor"; }
        fixture.Calls.Clear();
        Assert.Equal(1, Invoke(args, fixture, state, fixture).Exit);
        Assert.Empty(fixture.Calls);
    }

    [Fact]
    public void Choosing_between_copies_retains_the_origin_in_every_offered_handoff()
    {
        using var fixture = new DoctorFixture { Latest = "2.0.0" };
        using var source = new ScratchDocument("STERT ; update ;; codex\ncommands ; updateme ;;\nƒin.");
        var other = fixture.Files.Package("other", "1.0.0");
        using var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings { Ansi = AnsiSupport.No, Out = new AnsiConsoleOutput(writer) });
        console.Profile.Width = 80;
        var exit = CliApplication.Run(["run", source.Path, "--plain"], console, invocation: ["DERT.exe"],
            discoveryEnvironment: new(fixture.Prefix + ";" + other, ".CMD;.EXE"), commandRunner: fixture,
            doctorStateDirectory: Path.Combine(fixture.Files.Root, "state"));
        Assert.Equal(1, exit);
        var choices = writer.ToString().Split('\n').Where(line => line.Contains("--path")).ToArray();
        Assert.Equal(2, choices.Length);
        Assert.All(choices, command => Assert.Contains("--from-source '" + source.Path.Replace("'", "''") + "'", command));
        Assert.Empty(fixture.Calls);
    }

    [Fact]
    public void Failed_proposal_storage_never_offers_an_unusable_approval()
    {
        using var fixture = new DoctorFixture { Latest = "2.0.0" };
        var state = Path.Combine(fixture.Files.Root, "state");
        Directory.CreateDirectory(state);
        File.WriteAllText(Path.Combine(state, "update-proposals"), "occupied");
        var result = Invoke(["update", "codex", "updateme"], fixture, state, fixture);
        Assert.Equal(1, result.Exit);
        Assert.Contains("could not be saved", result.Output);
        Assert.DoesNotContain("APPLY THIS REVIEWED UPDATE", result.Output);
        Assert.DoesNotContain(fixture.Calls, DoctorFixture.IsMutation);
    }

    private static string[] Approval(NpmProposal proposal) => ["update", "codex", "updateme", "--path", proposal.SelectedPath,
        "--apply", proposal.Token, .. (proposal.Source is { } source ? new[] { "--from-source", source.Path } : [])];

    private static (int Exit, string Output) Invoke(string[] args, DoctorFixture fixture, string state, ICommandRunner runner, string input = "")
    {
        using var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings { Ansi = AnsiSupport.No, Interactive = InteractionSupport.No, Out = new AnsiConsoleOutput(writer) });
        console.Profile.Width = 100; console.Profile.Height = 40;
        var exit = CliApplication.Run([.. args, "--plain"], console, invocation: ["DERT.exe"],
            discoveryEnvironment: new(fixture.Prefix, ".CMD;.EXE"), commandRunner: runner,
            doctorStateDirectory: state, merInput: new StringReader(input));
        return (exit, writer.ToString());
    }

    private sealed class InstalledInventory(DoctorFixture fixture) : ICommandRunner
    {
        public CommandResult Run(CommandRequest request)
        {
            var result = fixture.Run(request);
            if (DoctorFixture.IsMutation(request) && result.Succeeded) fixture.InventoryVersion = "2.0.0";
            return result;
        }
    }
}
