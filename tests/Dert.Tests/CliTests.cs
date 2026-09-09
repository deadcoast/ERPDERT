using System.Text;
using Dert.Cli;
using Spectre.Console;
using Xunit;

namespace Dert.Tests;

public class CliTests
{
    private static (int Exit, string Output) Run(params string[] args)
    {
        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer)
        });
        console.Profile.Width = 140;
        var exit = CliApplication.Run(args, console);
        return (exit, writer.ToString());
    }

    [Theory]
    [InlineData()]
    [InlineData("-help")]
    [InlineData("--help")]
    public void Basic_help_is_available_without_setup_or_a_document(params string[] args)
    {
        var (exit, output) = Run(args);
        Assert.Equal(0, exit);
        Assert.Contains("inspect", output);
        Assert.Contains("lint", output);
        Assert.Contains("--at", output);
    }

    [Fact]
    public void Focused_entry_shows_its_own_value_explanation_and_owner()
    {
        using var file = new InputFile("// support doctoring ; conflicts ;;\n// fixme, // the repair command\n//");
        var (exit, output) = Run("inspect", file.Path, "--at", "2", "--plain");
        Assert.Equal(0, exit);
        Assert.Contains("support doctoring", output);
        Assert.Contains("conflicts", output);
        Assert.Contains("fixme", output);
        Assert.Contains("the repair command", output);
        Assert.Contains("Belongs to", output);
    }

    [Fact]
    public void A_filename_named_help_does_not_bypass_argument_validation()
    {
        var (exit, output) = Run("lint", "help", "--made-up");
        Assert.Equal(2, exit);
        Assert.Contains("--made-up", output);
    }

    [Fact]
    public void Supplementary_hidden_format_characters_are_visible_but_emoji_remain_intact()
    {
        const string tag = "\uDB40\uDC01";
        const string emoji = "\uD83D\uDE00";
        using var file = new InputFile($"STERT\n// hello{tag}world {emoji}\nƒin.");
        var (exit, output) = Run("inspect", file.Path, "--expand", "--plain");
        Assert.Equal(0, exit);
        Assert.DoesNotContain(tag, output, StringComparison.Ordinal);
        Assert.Contains("\\U000E0001", output);
        Assert.Contains(emoji, output, StringComparison.Ordinal);
    }

    [Fact]
    public void Inspector_counts_single_structures_and_diagnostics_in_readable_English()
    {
        using var file = new InputFile("// TITLE\nSTERT\n// content ; target ;;\n// value // explanation\nƒin.");
        var (exit, output) = Run("inspect", file.Path, "--plain");
        Assert.Equal(1, exit);
        Assert.Contains("1 chain · 0 closed-form reasoning notes · 1 reasoning block · 1 entry", output);
        Assert.Contains("1 error · 1 warning · 0 notes", output);
    }

    [Fact]
    public void Folded_incomplete_block_identifies_its_missing_closer_on_the_handle()
    {
        using var file = new InputFile("// content ; target ;;\n// value // explanation");
        var (exit, output) = Run("inspect", file.Path, "--plain");
        Assert.Equal(1, exit);
        Assert.Matches(@"⟪[^\r\n]*missing //[^\r\n]*⟫", output);
    }

    [Fact]
    public void Folded_incomplete_inline_note_identifies_its_missing_closer_on_the_handle()
    {
        using var file = new InputFile("// content ; target ;; explanation");
        var (exit, output) = Run("inspect", file.Path, "--plain");
        Assert.Equal(1, exit);
        Assert.Matches(@"⟪[^\r\n]*missing //[^\r\n]*⟫", output);
    }

    [Fact]
    public void Inspector_treats_markup_as_source_text()
    {
        using var file = new InputFile("STERT\n// [red]ordinary[/]\nƒin.");
        var (exit, output) = Run("inspect", file.Path, "--expand", "--plain");
        Assert.Equal(0, exit);
        Assert.Contains("[red]ordinary[/]", output);
    }

    [Fact]
    public void Source_control_sequences_cannot_clear_or_recolor_the_terminal()
    {
        using var file = new InputFile("STERT\n// hello\u001b[2Jworld\nƒin.");
        var (exit, output) = Run("inspect", file.Path, "--expand", "--plain");
        Assert.Equal(0, exit);
        Assert.DoesNotContain("\u001b", output, StringComparison.Ordinal);
        Assert.Contains("\\u001B", output);
    }

    [Fact]
    public void Lint_reports_plain_English_source_location_and_returns_failure_for_an_unclosed_note()
    {
        using var file = new InputFile("// content ; target ;;\n// value // explanation");
        var (exit, output) = Run("lint", file.Path, "--plain");
        Assert.Equal(1, exit);
        Assert.Contains("DERT1001", output);
        Assert.Contains(":1", output);
        Assert.Contains("standalone //", output);
    }

    [Fact]
    public void Lint_and_inspect_never_write_the_source_even_with_a_bom_and_diagnostics()
    {
        using var file = new InputFile("\uFEFF// TITLE\r\n---\r\n");
        var before = File.ReadAllBytes(file.Path);
        Run("lint", file.Path, "--plain");
        Run("inspect", file.Path, "--plain");
        Assert.Equal(before, File.ReadAllBytes(file.Path));
    }

    [Theory]
    [InlineData("inspect", "--at", "0")]
    [InlineData("inspect", "--at", "999")]
    [InlineData("inspect", "--at", "text")]
    [InlineData("lint", "--made-up", "")]
    public void Invalid_options_provide_a_usable_error_without_throwing(string command, string option, string value)
    {
        using var file = new InputFile("//");
        var (exit, output) = Run(command, file.Path, option, value);
        Assert.Equal(2, exit);
        Assert.Contains("DERT", output);
        Assert.NotEmpty(output);
    }

    [Fact]
    public void Missing_file_is_explained_without_a_stack_trace()
    {
        var (exit, output) = Run("inspect", "missing-file-for-erpdert-test.DERT", "--plain");
        Assert.Equal(2, exit);
        Assert.Contains("could not", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("System.IO.", output);
    }

    [Fact]
    public void Invalid_utf8_is_reported_instead_of_replacing_the_authors_bytes()
    {
        using var file = new InputFile("");
        File.WriteAllBytes(file.Path, [0xff, 0xfe, 0x41]);
        var (exit, output) = Run("inspect", file.Path, "--plain");
        Assert.Equal(2, exit);
        Assert.Contains("UTF-8", output);
        Assert.Equal(new byte[] { 0xff, 0xfe, 0x41 }, File.ReadAllBytes(file.Path));
    }

    private sealed class InputFile : IDisposable
    {
        private readonly string directory = System.IO.Path.Combine(AppContext.BaseDirectory,
            "scratch", Guid.NewGuid().ToString("N"));
        public string Path { get; }

        public InputFile(string content)
        {
            Directory.CreateDirectory(directory);
            Path = System.IO.Path.Combine(directory, "my notes.DERT");
            File.WriteAllText(Path, content, new UTF8Encoding(false));
        }

        public void Dispose()
        {
            File.Delete(Path);
            Directory.Delete(directory);
        }
    }
}
