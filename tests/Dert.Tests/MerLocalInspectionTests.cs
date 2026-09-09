using Dert.Cli;
using Dert.Cli.Discovery;
using Spectre.Console;
using Xunit;

namespace Dert.Tests;

public class MerLocalInspectionTests
{
    [Fact]
    public void Navigation_names_real_destinations_and_keeps_inline_context_readable()
    {
        using var file = new ScratchDocument("// TOOLS\n// --\nSTERT ; update ;; codex\n// why ; codex ;; Find my own copy. //\nfin.");
        var result = Run(file.Path, "next\nnext\nnext\nprev\n\n");
        Assert.Equal(0, result.Exit);
        Assert.Contains("next: line 3", result.Output);
        Assert.Contains("Find Codex on this PC", result.Output);
        Assert.Contains("Find my own copy.", result.Output);
        Assert.Contains("next: line 4 / why", result.Output);
        Assert.DoesNotContain("line 4 / Line 4", result.Output);
        Assert.Contains("Already at the last connection", result.Output);
        Assert.DoesNotContain("No recorded connection at this line", result.Output);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void An_explicit_probe_attaches_actual_package_evidence_and_preserves_source(bool replay)
    {
        using var fixture = new DiscoveryFixture();
        var prefix = fixture.Package("Ryan's [tools]", "1.2.3");
        using var file = new ScratchDocument("STERT ; update ;; codex\n// why ; codex ;; Keep the right copy. //\nfin.");
        var original = File.ReadAllBytes(file.Path);
        var result = Run(file.Path, "at 1\nprobe\ndetails\n\n", new(prefix, ".CMD;.EXE"), replay);
        Assert.Equal(0, result.Exit);
        Assert.Contains("Local inspection", result.Output);
        Assert.Contains("Copy 1 of 1", result.Output);
        Assert.Contains("@openai/codex 1.2.3", result.Output);
        Assert.Contains("No programs started", result.Output);
        Assert.Contains(Path.Combine(prefix, "node.exe"), result.Output);
        Assert.Contains(Path.Combine(prefix, "npm.cmd"), result.Output);
        Assert.Contains("ownership is not verified", result.Output);
        Assert.Equal(original, File.ReadAllBytes(file.Path));
    }

    [Fact]
    public void Copy_selection_reuses_the_inspection_even_after_installed_metadata_changes()
    {
        using var fixture = new DiscoveryFixture();
        var first = fixture.Package("first", "1.2.3");
        var second = fixture.Package("second", "2.0.0");
        using var file = new ScratchDocument("STERT ; update ;; codex\nfin.");
        using var input = new ChangingInput("at 1\nprobe\ncopy 2\ndetails\n\n", 3,
            () => File.WriteAllText(Path.Combine(second, "node_modules", "@openai", "codex", "package.json"), "null"));
        var result = Run(file.Path, input, new(first + ";" + second, ".CMD;.EXE"));
        Assert.Contains("Copy 2 of 2", result.Output);
        Assert.Contains("@openai/codex 2.0.0", result.Output);
        Assert.DoesNotContain("could not be identified", result.Output);
    }

    [Fact]
    public void A_changed_source_refuses_new_inspection_and_retains_the_recorded_document()
    {
        using var fixture = new DiscoveryFixture();
        var prefix = fixture.Package("installed", "9.8.7");
        using var file = new ScratchDocument("STERT ; update ;; codex\nfin.");
        using var input = new ChangingInput("at 1\nprobe\n\n", 2, () => File.WriteAllText(file.Path, "// changed"));
        var result = Run(file.Path, input, new(prefix, ".CMD"));
        Assert.Contains("Source changed since this analysis", result.Output);
        Assert.DoesNotContain("9.8.7", result.Output);
        Assert.Equal("// changed", File.ReadAllText(file.Path));
    }

    [Theory]
    [InlineData("// why ; codex ;; Just a note. //", "No discovery request at this line")]
    [InlineData("STERT ; update ;; codex\ncommands ; unknown ;;\nfin.", "ERP6004")]
    [InlineData("STERT ; update ;; codex\ncommands ; updateme ;;\nfin.", "update rehearsal")]
    public void Context_or_ineligible_operations_do_not_acquire_discovery_effects(string source, string explanation)
    {
        using var fixture = new DiscoveryFixture();
        var prefix = fixture.Package("installed", "9.8.7");
        using var file = new ScratchDocument(source);
        var result = Run(file.Path, "at 1\nprobe\n\n", new(prefix, ".CMD"));
        Assert.Contains(explanation, result.Output);
        Assert.DoesNotContain("9.8.7", result.Output);
        Assert.DoesNotContain("Local inspection recorded", result.Output);
    }

    [Fact]
    public void An_incomplete_search_does_not_claim_the_first_external_match_is_known()
    {
        using var fixture = new DiscoveryFixture();
        var prefix = fixture.Package("installed", "1.2.3");
        using var file = new ScratchDocument("STERT ; update ;; codex\nfin.");
        var result = Run(file.Path, "at 1\nprobe\ndetails\n\n", new("relative;" + prefix, ".CMD;.EXE"));
        Assert.Contains("First observed match / incomplete search", result.Output);
        Assert.DoesNotContain("First external PATH match", result.Output);
        Assert.Contains("ERP5101", result.Output);
    }

    [Fact]
    public void Details_keeps_every_application_blocker_in_the_captured_plan()
    {
        using var file = new ScratchDocument("STERT ; update ;; codex\ncommands ; unknown ;;\nfin.\nSTERT ; other ;; codex\nfin.");
        var result = Run(file.Path, "at 1\ndetails\n\n");
        Assert.Contains("ERP6004 / line 2", result.Output);
        Assert.Contains("ERP6002 / line 4", result.Output);
    }

    [Fact]
    public void Binding_findings_are_reachable_through_source_navigation()
    {
        using var file = new ScratchDocument("STERT ; update ;; codex\ncommands ; unknown ;;\nfin.");
        var result = Run(file.Path, "at 1\nnext\n\n");
        Assert.Contains("next: line 2 / ERP6004", result.Output);
        Assert.Contains("Operation needs attention", result.Output);
        Assert.Contains("ERP6004 / line 2", result.Output);
    }

    [Fact]
    public void Redirected_input_does_not_start_inspection_without_explicit_review()
    {
        using var file = new ScratchDocument("STERT ; update ;; codex\nfin.");
        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer)
        });
        using var input = new UnexpectedInput();
        var exit = CliApplication.Run(["mer", file.Path, "--plain"], console, merInput: input);
        Assert.Equal(0, exit);
        Assert.DoesNotContain("Local inspection", writer.ToString());
    }

    private sealed class UnexpectedInput : StringReader
    {
        internal UnexpectedInput() : base("probe\n") { }
        public override string? ReadLine() => throw new InvalidOperationException("Redirected input must not be read without --review.");
    }

    [Fact]
    public void Cancelled_discovery_does_not_read_the_search_environment()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => CodexDiscovery.Scan(new(null!, null!), cancelled.Token));
    }

    [Fact]
    public void Empty_discovery_and_invalid_copy_choices_keep_their_actual_meaning()
    {
        using var file = new ScratchDocument("STERT ; update ;; codex\nfin.");
        var result = Run(file.Path, "at 1\nprobe\ncopy 2\n\n");
        Assert.Contains("No Codex launcher found", result.Output);
        Assert.Contains("No inspected copy has that number", result.Output);
        Assert.DoesNotContain("Copy 1 of 1", result.Output);
    }

    private static (int Exit, string Output) Run(string path, string input, DiscoveryEnvironment? environment = null, bool replay = false) =>
        Run(path, new StringReader(input), environment, replay);

    private static (int Exit, string Output) Run(string path, TextReader input, DiscoveryEnvironment? environment = null, bool replay = false)
    {
        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer)
        });
        console.Profile.Width = 132;
        console.Profile.Height = 40;
        var args = new List<string> { "mer", path, "--plain", "--review" };
        if (replay) args.Add("--replay");
        return (CliApplication.Run(args.ToArray(), console, merInput: input, discoveryEnvironment: environment ?? new("", ".CMD;.EXE")), writer.ToString());
    }

    private sealed class ChangingInput(string text, int atRead, Action change) : StringReader(text)
    {
        private int reads;
        public override string? ReadLine()
        {
            if (++reads == atRead) change();
            return base.ReadLine();
        }
    }
}
