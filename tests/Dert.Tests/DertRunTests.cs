using System.Text;
using Dert.Cli;
using Dert.Cli.Discovery;
using Spectre.Console;
using Xunit;

namespace Dert.Tests;

public class DertRunTests
{
    [Theory]
    [InlineData("STERT ; update ;; codex")]
    [InlineData("STERT ; update ;; codex\nƒin.")]
    [InlineData("STERT ; update ;; codex\nfin.")]
    [InlineData("STERT;update;;codex // keep my annotation")]
    [InlineData("\uFEFFSTERT ; update ;; CODEX\r\nƒin.\r\n")]
    public void Written_operation_reaches_the_actual_discovery_engine(string source)
    {
        using var fixture = new DiscoveryFixture();
        var prefix = fixture.Package("node", "8.7.6");
        using var file = new ScratchDocument(source);
        var original = File.ReadAllBytes(file.Path);
        var (exit, output) = Run(file.Path, prefix);
        Assert.Equal(0, exit);
        Assert.Contains("DERT / RUN", output);
        Assert.Contains("STERT", output);
        Assert.Contains("CODEX / DISCOVERY", output);
        Assert.Contains("8.7.6", output);
        Assert.Contains(Path.Combine(prefix, "codex.ps1"), output);
        Assert.Contains("1 operation completed", output);
        Assert.Equal(original, File.ReadAllBytes(file.Path));
        Assert.Single(Directory.EnumerateFiles(file.DirectoryPath));
    }

    [Fact]
    public void Preview_shows_the_binding_and_context_but_no_machine_result()
    {
        using var fixture = new DiscoveryFixture();
        var prefix = fixture.Package("node", "8.7.6");
        using var file = new ScratchDocument("""
            STERT ; update ;; codex
            // Why this tool matters to me
            // support doctoring ; conflicts ;;
            // fixme, // ask before changing anything
            //
            ƒin.
            """);
        var (exit, output) = Run(file.Path, prefix, "--preview");
        Assert.Equal(0, exit);
        Assert.Contains("DERT / PREVIEW", output);
        Assert.Contains("Why this tool matters to me", output);
        Assert.Contains("support doctoring", output);
        Assert.Contains("fixme", output);
        Assert.Contains("ask before changing anything", output);
        Assert.Contains("DERT update codex", output);
        Assert.Contains("No discovery was performed", output);
        Assert.DoesNotContain("CODEX / DISCOVERY", output);
        Assert.DoesNotContain("8.7.6", output);
    }

    [Fact]
    public void A_title_warning_is_visible_but_does_not_block_a_read_only_operation()
    {
        using var fixture = new DiscoveryFixture();
        var prefix = fixture.Package("node", "1.0.0");
        using var file = new ScratchDocument("// MY TOOLS\nSTERT ; update ;; codex");
        var (exit, output) = Run(file.Path, prefix);
        Assert.Equal(0, exit);
        Assert.Contains("DERT2003", output);
        Assert.Contains("CODEX / DISCOVERY", output);
        Assert.Contains("MY TOOLS", output);
    }

    [Theory]
    [InlineData("// STERT ; update ;; codex //")]
    [InlineData("// context ; codex ;;\n// STERT ; update ;; codex, // this is an example\n//")]
    [InlineData("STERT\n// just keeping notes\nƒin.")]
    [InlineData("ordinary prose without an operation")]
    public void Notes_and_prose_are_not_promoted_into_operations(string source)
    {
        using var file = new ScratchDocument(source);
        var (exit, output) = Run(file.Path, "");
        Assert.Equal(0, exit);
        Assert.Contains("No operations to run", output);
        Assert.DoesNotContain("CODEX / DISCOVERY", output);
    }

    [Theory]
    [InlineData("STERT\n// keep this context even without a command\nƒin.", 0)]
    [InlineData("STERT ; update ;; claude\n// keep this context even without a command\nƒin.", 1)]
    public void Non_runnable_chains_still_show_the_authors_context(string source, int expectedExit)
    {
        using var file = new ScratchDocument(source);
        var (exit, output) = Run(file.Path, "", "--preview");
        Assert.Equal(expectedExit, exit);
        Assert.Contains("keep this context even without a command", output);
        Assert.Contains("STERT", output);
        Assert.DoesNotContain("CODEX / DISCOVERY", output);
    }

    [Fact]
    public void Document_context_between_chains_keeps_its_source_position()
    {
        using var file = new ScratchDocument("STERT ; update ;; codex\n// first chain context\nƒin.\n// between chains context\nSTERT ; update ;; codex\n// second chain context");
        var (_, output) = Run(file.Path, "", "--preview");
        Assert.True(output.IndexOf("first chain context", StringComparison.Ordinal) < output.IndexOf("between chains context", StringComparison.Ordinal));
        Assert.True(output.IndexOf("between chains context", StringComparison.Ordinal) < output.IndexOf("second chain context", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("STERT ; update ;; claude", "claude")]
    [InlineData("STERT ; update ;; codex --update", "codex --update")]
    [InlineData("STERT ; update ;; codex; whoami", "codex; whoami")]
    [InlineData("STERT ; SOMETHING ;; codex", "SOMETHING")]
    [InlineData("STERT ; update", "STERT")]
    [InlineData("STERT ; update ;;", "STERT")]
    [InlineData("STERT ; update ;; codex\ncommands ; uninstallme ;;", "uninstallme")]
    [InlineData("STERT ; update ;; codex\nrun this please", "run this please")]
    [InlineData("DERT ;; codex", "DERT")]
    public void Unimplemented_or_incomplete_content_is_cited_instead_of_partially_run(string source, string offendingText)
    {
        using var fixture = new DiscoveryFixture();
        var prefix = fixture.Package("node", "8.7.6");
        using var file = new ScratchDocument(source);
        var (exit, output) = Run(file.Path, prefix);
        Assert.Equal(1, exit);
        Assert.Contains(offendingText, output);
        Assert.Contains(file.Path + ":", output);
        Assert.Contains("No operations ran", output);
        Assert.DoesNotContain("CODEX / DISCOVERY", output);
        Assert.DoesNotContain("8.7.6", output);
    }

    [Fact]
    public void An_unsupported_later_chain_prevents_silent_partial_completion()
    {
        using var fixture = new DiscoveryFixture();
        var prefix = fixture.Package("node", "8.7.6");
        using var file = new ScratchDocument("STERT ; update ;; codex\nƒin.\nSTERT ; update ;; claude");
        var (exit, output) = Run(file.Path, prefix);
        Assert.Equal(1, exit);
        Assert.Contains(file.Path + ":3", output);
        Assert.DoesNotContain("CODEX / DISCOVERY", output);
        Assert.Contains("No operations ran", output);
    }

    [Fact]
    public void Multiple_supported_chains_keep_source_order_and_separate_results()
    {
        using var fixture = new DiscoveryFixture();
        var prefix = fixture.Package("node", "8.7.6");
        using var file = new ScratchDocument("STERT ; update ;; codex\n// first reason\nƒin.\nSTERT ; update ;; codex\n// second reason");
        var (exit, output) = Run(file.Path, prefix);
        Assert.Equal(0, exit);
        Assert.Contains("STEP 01 / RESULT", output);
        Assert.Contains("STEP 02 / RESULT", output);
        Assert.Contains(file.Path + ":1", output);
        Assert.Contains(file.Path + ":4", output);
        Assert.True(output.IndexOf("first reason", StringComparison.Ordinal) < output.IndexOf("second reason", StringComparison.Ordinal));
        Assert.Contains("2 operations completed", output);
    }

    [Fact]
    public void Discovery_attention_is_not_misreported_as_a_failed_DERT_parse()
    {
        using var file = new ScratchDocument("STERT ; update ;; codex");
        var (exit, output) = Run(file.Path, "");
        Assert.Equal(1, exit);
        Assert.Contains("1 operation completed", output);
        Assert.Contains("1 result needs attention", output);
        Assert.Contains("No codex command was found", output);
        Assert.DoesNotContain("No operations ran", output);
    }

    [Fact]
    public void Incomplete_reasoning_is_source_cited_before_any_dispatch()
    {
        using var file = new ScratchDocument("STERT ; update ;; codex\n// support ; conflicts ;;\n// one entry");
        var (exit, output) = Run(file.Path, "");
        Assert.Equal(1, exit);
        Assert.Contains("DERT1001", output);
        Assert.Contains(file.Path + ":2", output);
        Assert.DoesNotContain("CODEX / DISCOVERY", output);
    }

    [Fact]
    public void Invalid_UTF8_is_not_repaired_or_interpreted_for_execution()
    {
        using var file = new ScratchDocument("");
        File.WriteAllBytes(file.Path, [0xff, 0xfe]);
        var (exit, output) = Run(file.Path, "");
        Assert.Equal(2, exit);
        Assert.Contains("UTF-8", output);
        Assert.Equal(new byte[] { 0xff, 0xfe }, File.ReadAllBytes(file.Path));
    }

    [Fact]
    public void Source_comments_cannot_send_terminal_control_sequences()
    {
        using var file = new ScratchDocument("STERT ; update ;; codex\n// [red]context\u001b[2J\u202E");
        var (_, output) = Run(file.Path, "", "--preview");
        Assert.Contains("[red]context\\u001B[2J\\u202E", output);
        Assert.DoesNotContain("\u001b", output, StringComparison.Ordinal);
        Assert.DoesNotContain("\u202E", output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--apply")]
    [InlineData("--expand")]
    [InlineData("--at")]
    public void File_run_does_not_accept_unrelated_repair_or_inspection_flags(string option)
    {
        using var file = new ScratchDocument("STERT ; update ;; codex");
        var (exit, output) = Run(file.Path, "", option);
        Assert.Equal(2, exit);
        Assert.Contains(option, output);
        Assert.DoesNotContain("CODEX / DISCOVERY", output);
    }

    private static (int Exit, string Output) Run(string path, string searchPath, params string[] options)
    {
        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer)
        });
        console.Profile.Width = 200;
        var exit = CliApplication.Run(["run", path, "--plain", .. options], console,
            discoveryEnvironment: new(searchPath, ".EXE;.CMD"));
        return (exit, writer.ToString());
    }
}
