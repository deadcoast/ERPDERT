using Erpdert.Cli;
using Erpdert.Cli.Discovery;
using Erpdert.Cli.Operations;
using Erpdert.Mer;
using System.Reflection;
using System.Text;
using Spectre.Console;
using Xunit;

namespace Dert.Tests;

public class MerUpdateReviewTests
{
    private const string Source = "STERT ; ERPDERT ;; codex\n// why ; codex ;; Keep the right copy. //\ncommands ; updateme ;;\nfin.";

    [Fact]
    public void Selecting_and_comparing_a_copy_does_not_start_npm()
    {
        using var fixture = new UpdateFixture();
        using var file = new ScratchDocument(Source);
        var result = Run(file.Path, "at 3\nrehearse\ncopy 1\n\n", fixture);
        Assert.Contains("Copy 1 of 1", result.Output);
        Assert.Contains("check queries npm and the registry", result.Output);
        Assert.Empty(fixture.Calls);
        Assert.Equal(Source, File.ReadAllText(file.Path));
    }

    [Fact]
    public void Explicit_check_connects_source_inventory_registry_and_dry_run()
    {
        using var fixture = new UpdateFixture { Fault = "dry-run-preamble" };
        using var file = new ScratchDocument(Source);
        var result = Run(file.Path, "at 1\nrehearse\ncheck\ndetails\napply\n\n", fixture);
        Assert.Equal(0, result.Exit);
        Assert.Contains("3 | commands ; updateme ;;", result.Output);
        Assert.Contains("1.0.0 -> 2.0.0", result.Output);
        Assert.Contains("Rehearsed / not applied", result.Output);
        Assert.Contains("npm inventory confirmed", result.Output);
        Assert.Contains("@openai/codex-win32-x64", result.Output);
        Assert.Contains("Update rehearsal recorded", result.Output);
        Assert.Equal(new[] { "root", "ls", "view", "install" }, fixture.Calls.Select(c => c.Arguments[1]));
        Assert.Contains("--dry-run", fixture.Calls[^1].Arguments);
        Assert.Contains("--ignore-scripts", fixture.Calls[^1].Arguments);
        Assert.Equal(fixture.Original, File.ReadAllText(fixture.Metadata));
        Assert.Equal(Source, File.ReadAllText(file.Path));
    }

    [Theory]
    [InlineData("1.0.0", "Already current", 0)]
    [InlineData("0.9.0", "Ahead of latest", 1)]
    public void Current_and_ahead_versions_do_not_claim_a_completed_dry_run(string latest, string label, int exit)
    {
        using var fixture = new UpdateFixture { Latest = latest };
        using var file = new ScratchDocument(Source);
        var result = Run(file.Path, "at 3\nrehearse\ncheck\n\n", fixture);
        Assert.Equal(exit, result.Exit);
        Assert.Contains(label, result.Output);
        Assert.DoesNotContain("Rehearsed / not applied", result.Output);
        Assert.DoesNotContain(fixture.Calls, call => call.Arguments.Contains("install"));
    }

    [Theory]
    [InlineData("inventory-version", "ERP5204")]
    [InlineData("engine-failure", "ERP5203")]
    public void Failed_checks_remain_attached_to_the_request(string fault, string code)
    {
        using var fixture = new UpdateFixture { Fault = fault };
        using var file = new ScratchDocument(Source);
        var result = Run(file.Path, "at 3\nrehearse\ncheck\ndetails\n\n", fixture);
        Assert.Equal(1, result.Exit);
        Assert.Contains("Rehearsal stopped", result.Output);
        Assert.Contains(code, result.Output);
        Assert.DoesNotContain("Rehearsed / not applied", result.Output);
    }

    [Theory]
    [InlineData("// commands ; updateme ;; This stays a note. //")]
    [InlineData("STERT ; ERPDERT ;; codex\ncommands ; unknown ;;\nfin.")]
    [InlineData("STERT ; ERPDERT ;; codex\ncommands ; updateme ;;\n// unfinished ; note ;;")]
    public void Notes_and_ineligible_source_do_not_start_rehearsals(string source)
    {
        using var fixture = new UpdateFixture();
        using var file = new ScratchDocument(source);
        Run(file.Path, "at 1\nrehearse\ncheck\n\n", fixture);
        Assert.Empty(fixture.Calls);
    }

    [Fact]
    public void Replay_never_starts_npm_queries()
    {
        using var fixture = new UpdateFixture();
        using var file = new ScratchDocument(Source);
        var result = Run(file.Path, "at 3\nrehearse\ncheck\n\n", fixture, replay: true);
        Assert.Contains("Replay cannot start npm queries", result.Output);
        Assert.Empty(fixture.Calls);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void Source_changes_before_discovery_or_check_refuse_the_new_work(int atRead)
    {
        using var fixture = new UpdateFixture();
        using var file = new ScratchDocument(Source);
        var result = Run(file.Path, new ChangingInput("at 3\nrehearse\ncheck\n\n", atRead,
            () => File.WriteAllText(file.Path, "// different notes")), fixture);
        Assert.Contains("Source changed", result.Output);
        Assert.Empty(fixture.Calls);
    }

    [Fact]
    public void Source_changes_during_queries_discard_the_proposed_update()
    {
        using var fixture = new UpdateFixture();
        using var file = new ScratchDocument(Source);
        var runner = new ChangingRunner(fixture, () => File.WriteAllText(file.Path, "// edited during query"));
        var result = Run(file.Path, new StringReader("at 3\nrehearse\ncheck\ndetails\n\n"), fixture, runner: runner);
        Assert.Equal(1, result.Exit);
        Assert.Contains("Source changed", result.Output);
        Assert.DoesNotContain("Rehearsed / not applied", result.Output);
        Assert.DoesNotContain("MANUAL UPDATE / CHANGES PACKAGES", result.Output);
    }

    private sealed class ChangingRunner(UpdateFixture fixture, Action change) : ICommandRunner
    {
        public CommandResult Run(CommandRequest request) { var result = fixture.Run(request); if (request.Arguments.Contains("view")) change(); return result; }
    }

    [Fact]
    public void Multiple_copies_require_a_choice_and_revisiting_a_result_never_requeries()
    {
        using var first = new UpdateFixture();
        using var second = new UpdateFixture();
        using var file = new ScratchDocument(Source);
        var route = new RoutingRunner(first, second);
        var result = Run(file.Path, new StringReader("at 3\nrehearse\ncheck\ncopy 2\ncheck\ncopy 1\ncopy 2\ndetails\n\n"), first,
            runner: route, searchPath: first.Prefix + ";" + second.Prefix);
        Assert.Equal(0, result.Exit);
        Assert.Contains("copy <number> to choose", result.Output);
        Assert.Contains("Copy 2 of 2", result.Output);
        Assert.Empty(first.Calls);
        Assert.Equal(4, second.Calls.Count);
        Assert.Contains(second.Prefix.Replace("'", "''"), result.Output);
    }

    [Fact]
    public void A_failed_repeat_replaces_the_previous_ready_result()
    {
        using var fixture = new UpdateFixture();
        using var file = new ScratchDocument(Source);
        var result = Run(file.Path, new StringReader("at 3\nrehearse\ncheck\ncheck\ndetails\n\n"), fixture,
            runner: new FailRepeat(fixture));
        var failed = result.Output[result.Output.IndexOf("Rehearsal stopped", StringComparison.Ordinal)..];
        Assert.Equal(1, result.Exit);
        Assert.DoesNotContain("MANUAL UPDATE / CHANGES PACKAGES", failed);
        Assert.DoesNotContain("Rehearsed / not applied", failed);
    }

    [Fact]
    public void Source_repair_invalidates_rehearsal_selection_and_results()
    {
        using var fixture = new UpdateFixture();
        using var file = new ScratchDocument("// TITLE\n// --\n" + Source);
        var result = Run(file.Path, "at 5\nrehearse\ncheck\nrepair title\napply\nat 5\ncheck\ndetails\n\n", fixture);
        var saved = result.Output[result.Output.IndexOf("Saved / bytes verified", StringComparison.Ordinal)..];
        Assert.DoesNotContain("Update rehearsal recorded", saved);
        Assert.Equal(4, fixture.Calls.Count);
        Assert.Contains("Use rehearse to find copies", saved);
    }

    [Fact]
    public void A_changed_installation_must_be_selected_again_before_queries()
    {
        using var fixture = new UpdateFixture();
        using var file = new ScratchDocument(Source);
        var result = Run(file.Path, new ChangingInput("at 3\nrehearse\ncheck\n\n", 3,
            () => File.WriteAllText(fixture.Metadata, fixture.Original.Replace("1.0.0", "1.0.1"))), fixture);
        Assert.Contains("selected installation changed", result.Output);
        Assert.Equal(1, result.Exit);
        Assert.Empty(fixture.Calls);
    }

    [Fact]
    public void A_stale_repair_cannot_leave_an_update_proposal_attached_to_unverified_source()
    {
        using var fixture = new UpdateFixture();
        using var file = new ScratchDocument("// TITLE\n// --\n" + Source);
        var result = Run(file.Path, new ChangingInput("at 5\nrehearse\ncheck\nrepair title\napply\nat 5\ndetails\n\n", 5,
            () => File.AppendAllText(file.Path, "\n// Later note")), fixture);
        var stopped = result.Output[result.Output.IndexOf("Recorded source / current file not verified", StringComparison.Ordinal)..];
        Assert.DoesNotContain("Rehearsed / not applied", stopped);
        Assert.DoesNotContain("MANUAL UPDATE / CHANGES PACKAGES", stopped);
        Assert.DoesNotContain("Update rehearsal / complete", stopped);
    }

    [Fact]
    public void Warnings_from_fresh_discovery_are_visible_in_the_rehearsed_result()
    {
        using var fixture = new UpdateFixture();
        using var file = new ScratchDocument(Source);
        var entry = Path.Combine(fixture.Prefix, "node_modules", "@openai", "codex", "bin", "codex.js");
        var result = Run(file.Path, new ChangingInput("at 3\nrehearse\ncheck\n\n", 3, () => File.Delete(entry)), fixture);
        Assert.Equal(1, result.Exit);
        var finished = result.Output[result.Output.IndexOf("Rehearsed / not applied", StringComparison.Ordinal)..];
        Assert.Contains("1 issue / details", finished);
        Assert.Contains("needs attention", finished);
    }

    [Fact]
    public void Cancelling_discovery_clears_a_previous_ready_result_and_settles_the_window()
    {
        using var fixture = new UpdateFixture();
        using var file = new ScratchDocument(Source);
        var review = new ReviewHarness(file.Path, fixture, fixture);
        review.Command("rehearse"); review.Command("check");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var failure = Assert.Throws<TargetInvocationException>(() => review.Command("check", cancellation.Token));
        Assert.IsType<OperationCanceledException>(failure.InnerException);
        review.Details();
        var stopped = review.Output[review.Output.IndexOf("Rehearsal stopped", StringComparison.Ordinal)..];
        Assert.Contains("Local discovery stopped", stopped);
        Assert.DoesNotContain("MANUAL UPDATE / CHANGES PACKAGES", stopped);
        Assert.Equal(4, fixture.Calls.Count);
    }

    [Fact]
    public void Cancelling_a_query_preserves_completed_evidence_and_prevents_the_next_process()
    {
        using var fixture = new UpdateFixture();
        using var file = new ScratchDocument(Source);
        using var cancellation = new CancellationTokenSource();
        var review = new ReviewHarness(file.Path, fixture, new ChangingRunner(fixture, cancellation.Cancel));
        review.Command("rehearse");
        var failure = Assert.Throws<TargetInvocationException>(() => review.Command("check", cancellation.Token));
        Assert.IsType<OperationCanceledException>(failure.InnerException);
        review.Details();
        Assert.Contains("Rehearsal stopped / interrupted", review.Output);
        Assert.Contains("MANAGER INVENTORY", review.Output);
        Assert.DoesNotContain("MANUAL UPDATE / CHANGES PACKAGES", review.Output);
        Assert.DoesNotContain(fixture.Calls, call => call.Arguments.Contains("install"));
    }

    private sealed class ReviewHarness
    {
        private readonly object review;
        private readonly Type type;
        private readonly StringWriter output = new();
        internal string Output => output.ToString();
        internal ReviewHarness(string path, UpdateFixture fixture, ICommandRunner runner)
        {
            var assembly = typeof(CliApplication).Assembly;
            var adapter = assembly.GetType("Erpdert.Cli.Motion.NoteScore")!;
            var bytes = File.ReadAllBytes(path);
            var analysis = adapter.GetMethod("Analyze", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null,
                [new Func<string>(() => Encoding.UTF8.GetString(bytes)), path, false, null, CancellationToken.None, DocumentSourceState.Original])!;
            var score = (MotionScore)adapter.GetProperty("Score")!.GetValue(analysis)!;
            var journal = new MotionJournal(score with { View = MotionView.Document }, DateTimeOffset.Now);
            foreach (var change in (IReadOnlyList<MotionEvent>)adapter.GetProperty("Events")!.GetValue(analysis)!) journal.Apply(change, TimeSpan.Zero);
            var console = AnsiConsole.Create(new AnsiConsoleSettings { Ansi = AnsiSupport.No, Interactive = InteractionSupport.No, Out = new AnsiConsoleOutput(output) });
            console.Profile.Width = 132; console.Profile.Height = 40;
            var renderer = Activator.CreateInstance(assembly.GetType("Erpdert.Cli.DocumentRenderer")!, console, true, null, true);
            type = assembly.GetType("Erpdert.Cli.Motion.MerReview")!;
            review = Activator.CreateInstance(type, analysis, journal.Snapshot(TimeSpan.Zero), bytes, path, console, renderer,
                new StringReader(""), true, new DiscoveryEnvironment(fixture.Prefix, ".CMD;.EXE"), null, runner)!;
            type.GetField("selected", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(review, 3);
        }
        internal void Command(string command, CancellationToken cancellation = default) =>
            type.GetMethod("HandleUpdate", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(review, [command, cancellation]);
        internal void Details() => type.GetMethod("UpdateDetails", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(review, []);
    }

    private sealed class RoutingRunner(UpdateFixture first, UpdateFixture second) : ICommandRunner
    {
        public CommandResult Run(CommandRequest request) => request.WorkingDirectory == first.Prefix ? first.Run(request) : second.Run(request);
    }
    private sealed class FailRepeat(UpdateFixture fixture) : ICommandRunner
    {
        private int calls;
        public CommandResult Run(CommandRequest request) => ++calls > 4 ? new(1, "", "second attempt failed") : fixture.Run(request);
    }

    private static (int Exit, string Output) Run(string path, string commands, UpdateFixture fixture, bool replay = false) =>
        Run(path, new StringReader(commands), fixture, replay);
    private static (int Exit, string Output) Run(string path, TextReader commands, UpdateFixture fixture, bool replay = false, ICommandRunner? runner = null, string? searchPath = null)
    {
        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings { Ansi = AnsiSupport.No, Interactive = InteractionSupport.No, Out = new AnsiConsoleOutput(writer) });
        console.Profile.Width = 132; console.Profile.Height = 40;
        string[] args = replay ? ["mer", path, "--review", "--plain", "--replay"] : ["mer", path, "--review", "--plain"];
        return (CliApplication.Run(args, console, merInput: commands, commandRunner: runner ?? fixture,
            discoveryEnvironment: new DiscoveryEnvironment(searchPath ?? fixture.Prefix, ".CMD;.EXE")), writer.ToString());
    }
    private sealed class ChangingInput(string text, int at, Action change) : StringReader(text)
    {
        private int reads;
        public override string? ReadLine() { if (++reads == at) change(); return base.ReadLine(); }
    }
}
