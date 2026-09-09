using Dert.Cli;
using Spectre.Console;
using Xunit;

namespace Dert.Tests;

public class MerCliTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_closed_plain_progress_output_is_not_mistaken_for_failed_npm_evidence(bool disposed)
    {
        using var fixture = new UpdateFixture();
        var writer = new BrokenProgressOutput(disposed);
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer)
        });
        var exit = CliApplication.Run(["update", "codex", "updateme", "--plain"], console,
            discoveryEnvironment: new(fixture.Prefix, ".EXE;.CMD"), commandRunner: fixture);
        Assert.Equal(2, exit);
        Assert.Empty(fixture.Calls);
        Assert.DoesNotContain("ERP5207", writer.ToString());
        Assert.DoesNotContain("evidence could not be read", writer.ToString());
    }

    [Fact]
    public void A_closed_receipt_output_is_not_reclassified_as_failed_source_analysis()
    {
        using var file = new ScratchDocument("// TITLE\n// -----\n");
        var writer = new BrokenMerOutput();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer)
        });
        Assert.Equal(2, CliApplication.Run(["mer", file.Path, "--plain"], console));
        Assert.DoesNotContain("could not be read or analyzed", writer.ToString());
    }

    [Fact]
    public void A_new_title_rule_is_anchored_after_the_title_not_to_the_following_prose()
    {
        using var file = new ScratchDocument("// TITLE\nKeep these words.");
        var (exit, output) = Invoke(["mer", file.Path, "--plain", "--details"]);
        Assert.Equal(0, exit);
        Assert.Contains("Insert after line 1", output);
        Assert.Contains("Keep these words.", output);
        Assert.DoesNotContain("Line 2 / before", output);
    }

    [Fact]
    public void Copied_DERT_preview_commands_keep_reduced_motion()
    {
        using var file = new ScratchDocument("STERT ; update ;; codex\ncommands ; updateme ;;\nƒin.");
        var (exit, output) = Invoke(["run", file.Path, "--preview", "--still", "--plain"]);
        Assert.Equal(0, exit);
        var command = Assert.Single(output.Split('\n'), line => line.StartsWith("DERT run ", StringComparison.Ordinal));
        Assert.Contains("--still", command);
    }
    [Fact]
    public void A_failed_live_read_is_explained_inside_the_engine_receipt()
    {
        var (exit, output) = Invoke(["mer", Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".DERT"), "--plain", "--details"]);
        Assert.Equal(2, exit);
        Assert.Contains("Read the source / failed", output);
        Assert.DoesNotContain("Check the boundaries / checked", output);
        Assert.Contains("FAILED", output);
    }
    [Fact]
    public void The_motion_surface_analyzes_real_source_without_editing_it()
    {
        const string source = "// WEEKEND\n// --\nSTERT\n// because ; music ;; Keep the good stuff. //\nƒin.\n";
        using var file = new ScratchDocument(source);
        var (exit, output) = Invoke(["mer", file.Path, "--still", "--plain"]);
        Assert.Equal(0, exit);
        Assert.Contains("WEEKEND", output);
        Assert.Contains("// -------", output);
        Assert.Contains("Keep the good stuff.", output);
        Assert.Contains("not applied", output, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(source, File.ReadAllText(file.Path));
        Assert.Single(Directory.GetFiles(file.DirectoryPath));
        Assert.DoesNotContain('\u001b', output);
    }

    [Fact]
    public void Damaged_notes_remain_visible_and_do_not_receive_a_clean_receipt()
    {
        using var file = new ScratchDocument("STERT\n// because ; music ;;\n// unfinished");
        var (exit, output) = Invoke(["mer", file.Path, "--plain"]);
        Assert.Equal(1, exit);
        Assert.Contains("unfinished", output);
        Assert.Contains("//", output);
        Assert.Contains("attention", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Reduced_motion_is_available_on_the_real_update_workflow()
    {
        using var fixture = new UpdateFixture();
        var (exit, output) = UpdateCliTests.Invoke(["update", "codex", "updateme", "--still", "--plain"], fixture.Prefix, fixture);
        Assert.Equal(0, exit);
        Assert.Contains("REHEARSED / NOT APPLIED", output);
        Assert.DoesNotContain('\u001b', output);
        Assert.All(fixture.Calls.Where(call => call.Arguments.Contains("install")), call => Assert.Contains("--dry-run", call.Arguments));
    }

    [Theory]
    [InlineData("", 0, "Rehearse the change / checked")]
    [InlineData("invalid-json", 1, "Read the registry / failed")]
    [InlineData("engine-failure", 1, "Rehearse the change / failed")]
    public void The_live_operation_adapter_only_confirms_validated_npm_evidence(string fault, int expectedExit, string expectedStage)
    {
        using var fixture = new UpdateFixture { Fault = fault };
        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            Interactive = InteractionSupport.Yes,
            Out = new AnsiConsoleOutput(writer)
        });
        var exit = CliApplication.Run(["update", "codex", "updateme", "--still"], console,
            discoveryEnvironment: new(fixture.Prefix, ".EXE;.CMD"), commandRunner: fixture);
        var output = System.Text.RegularExpressions.Regex.Replace(writer.ToString(), "\u001b\\[[0-9;]*m", "");
        Assert.Equal(expectedExit, exit);
        Assert.Contains(expectedStage, output);
        Assert.Contains("/ mer", output);
        Assert.DoesNotContain("✓ TARGET FOLDER", output);
        // Event-driven still frames may show this evidence before the receipt.
        // The final retained receipt must contain it exactly once.
        var receiptAt = output.LastIndexOf("DERT / mer", StringComparison.Ordinal);
        Assert.True(receiptAt >= 0);
        Assert.Equal(1, output[receiptAt..].Split("npm sees @openai/codex", StringSplitOptions.None).Length - 1);
        if (fault == "invalid-json") Assert.DoesNotContain("Read the registry / checked", output);
        Assert.All(fixture.Calls.Where(call => call.Arguments.Contains("install")), call => Assert.Contains("--dry-run", call.Arguments));
    }

    private static (int Exit, string Output) Invoke(string[] args)
    {
        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer)
        });
        return (CliApplication.Run(args, console), writer.ToString());
    }

    private sealed class BrokenMerOutput : StringWriter
    {
        private bool failed;
        public override void Write(string? value)
        {
            if (!failed && value?.Contains("/ mer", StringComparison.Ordinal) == true)
            {
                failed = true;
                throw new IOException("Fixture pipe closed.");
            }
            base.Write(value);
        }
    }

    private sealed class BrokenProgressOutput(bool disposed) : StringWriter
    {
        private bool failed;
        public override void Write(string? value)
        {
            if (!failed && value?.Contains("CHECK /", StringComparison.Ordinal) == true)
            {
                failed = true;
                if (disposed) throw new ObjectDisposedException("Fixture pipe");
                throw new IOException("Fixture pipe closed.");
            }
            base.Write(value);
        }
    }
}
