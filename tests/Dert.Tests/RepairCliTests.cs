using Erpdert.Cli;
using Erpdert.Cli.Repairs;
using Spectre.Console;
using Xunit;

namespace Dert.Tests;

public class RepairCliTests
{
    private static (int Exit, string Output) Run(string[] args, Func<string, bool>? confirm = null, int width = 160)
    {
        var output = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(output)
        });
        console.Profile.Width = width;
        return (CliApplication.Run(args, console, confirm), output.ToString());
    }

    [Fact]
    public void Redirected_preview_explains_the_actual_diff_without_writing()
    {
        using var file = new ScratchDocument("// TITLE\n// --\nwords");
        var (exit, output) = Run(["repair", file.Path, "--plain"]);
        Assert.Equal(0, exit);
        Assert.Contains("REPAIR REHEARSAL", output);
        Assert.Contains("DERT2003", output);
        Assert.Contains("3 marks short", output);
        Assert.Contains("// --", output);
        Assert.Contains("// -----", output);
        Assert.Contains("--apply", output);
        Assert.Contains("PREVIEW ONLY", output);
        Assert.Equal("// TITLE\n// --\nwords", File.ReadAllText(file.Path));
        Assert.Single(Directory.EnumerateFileSystemEntries(file.DirectoryPath));
    }

    [Fact]
    public void Confirmed_repair_reports_verified_application_and_an_undo_command()
    {
        using var file = new ScratchDocument("// TITLE\n// --");
        var (exit, output) = Run(["repair", file.Path, "--plain"], _ => true);
        Assert.Equal(0, exit);
        Assert.Equal("// TITLE\n// -----", File.ReadAllText(file.Path));
        Assert.Contains("TITLE ALIGNED", output);
        Assert.Contains("before.DERT", output);
        Assert.Contains("ERPDERT undo", output);
    }

    [Fact]
    public void Leaving_a_preview_creates_neither_an_edit_nor_a_recovery_directory()
    {
        using var file = new ScratchDocument("// TITLE");
        var (exit, output) = Run(["repair", file.Path, "--plain"], _ => false);
        Assert.Equal(0, exit);
        Assert.Contains("LEFT UNCHANGED", output);
        Assert.Equal("// TITLE", File.ReadAllText(file.Path));
        Assert.Single(Directory.EnumerateFileSystemEntries(file.DirectoryPath));
    }

    [Fact]
    public void A_change_while_the_confirmation_is_open_withdraws_the_edit()
    {
        using var file = new ScratchDocument("// TITLE");
        var (exit, output) = Run(["repair", file.Path, "--plain"], _ =>
        {
            File.WriteAllText(file.Path, "// NEW WORDS");
            return true;
        });
        Assert.Equal(1, exit);
        Assert.Contains("DERT4002", output);
        Assert.DoesNotContain("TITLE ALIGNED", output);
        Assert.Equal("// NEW WORDS", File.ReadAllText(file.Path));
    }

    [Fact]
    public void Explicit_revision_approval_applies_without_interactive_input()
    {
        using var file = new ScratchDocument("// TITLE\n// --");
        var revision = new RepairFileService().Preview(file.Path).ApprovalToken;
        var (exit, _) = Run(["repair", file.Path, "--apply", revision, "--plain"]);
        Assert.Equal(0, exit);
        Assert.Equal("// TITLE\n// -----", File.ReadAllText(file.Path));
    }

    [Fact]
    public void A_stale_revision_never_approves_a_new_preview_implicitly()
    {
        using var file = new ScratchDocument("// TITLE");
        var (exit, output) = Run(["repair", file.Path, "--apply", new string('0', 64), "--plain"]);
        Assert.Equal(1, exit);
        Assert.Contains("DERT4002", output);
        Assert.Equal("// TITLE", File.ReadAllText(file.Path));
    }

    [Fact]
    public void An_old_undo_approval_cannot_select_a_different_original_with_identical_repaired_bytes()
    {
        using var file = new ScratchDocument("// TITLE\n// --");
        var service = new RepairFileService();
        service.Apply(service.Preview(file.Path));
        var output = Run(["undo", file.Path, "--plain"]).Output;
        var approval = System.Text.RegularExpressions.Regex.Match(output.Replace("\r", "").Replace("\n", ""), @"--apply\s+([A-F0-9]{64})").Groups[1].Value;
        Assert.True(approval.Length == 64, output);
        service.Apply(service.PreviewUndo(file.Path));
        File.WriteAllText(file.Path, "// TITLE\n// ---");
        service.Apply(service.Preview(file.Path));
        var (exit, _) = Run(["undo", file.Path, "--apply", approval, "--plain"]);
        Assert.Equal(1, exit);
        Assert.Equal("// TITLE\n// -----", File.ReadAllText(file.Path));
    }

    [Fact]
    public void A_copied_approval_is_withdrawn_for_a_replacement_file_with_identical_bytes()
    {
        using var file = new ScratchDocument("// TITLE");
        var output = Run(["repair", file.Path, "--plain"]).Output;
        var approval = System.Text.RegularExpressions.Regex.Match(output.Replace("\r", "").Replace("\n", ""), @"--apply\s+([A-F0-9]{64})").Groups[1].Value;
        Assert.True(approval.Length == 64, output);
        var replacement = System.IO.Path.Combine(file.DirectoryPath, "replacement.DERT");
        File.WriteAllText(replacement, "// TITLE");
        File.Move(replacement, file.Path, overwrite: true);
        Assert.Equal(1, Run(["repair", file.Path, "--apply", approval, "--plain"]).Exit);
        Assert.Equal("// TITLE", File.ReadAllText(file.Path));
    }

    [Fact]
    public void Undo_is_previewed_and_restores_only_after_explicit_approval()
    {
        using var file = new ScratchDocument("// TITLE\n// --");
        var service = new RepairFileService();
        service.Apply(service.Preview(file.Path));
        var (previewExit, previewText) = Run(["undo", file.Path, "--plain"]);
        Assert.Equal(0, previewExit);
        Assert.Contains("UNDO REHEARSAL", previewText);
        Assert.Equal("// TITLE\n// -----", File.ReadAllText(file.Path));
        var (exit, output) = Run(["undo", file.Path, "--plain"], _ => true);
        Assert.Equal(0, exit);
        Assert.Contains("ORIGINAL RESTORED", output);
        Assert.Equal("// TITLE\n// --", File.ReadAllText(file.Path));
    }

    [Theory]
    [InlineData("// TITLE\n// -----", 0)]
    [InlineData("STERT\n// a note\nƒin.", 0)]
    [InlineData("// café", 1)]
    public void No_candidate_does_not_create_a_recovery_record(string text, int expectedExit)
    {
        using var file = new ScratchDocument(text);
        var (exit, output) = Run(["repair", file.Path, "--plain"]);
        Assert.Equal(expectedExit, exit);
        Assert.Contains("UNCHANGED", output);
        Assert.Equal(text, File.ReadAllText(file.Path));
        Assert.Single(Directory.EnumerateFileSystemEntries(file.DirectoryPath));
    }

    [Fact]
    public void A_title_repair_does_not_claim_to_resolve_unrelated_errors()
    {
        using var file = new ScratchDocument("// TITLE\n// --\n// HOW ; notes ;; unfinished");
        var (exit, output) = Run(["repair", file.Path, "--plain"], _ => true);
        Assert.Equal(0, exit);
        Assert.Contains("TITLE ALIGNED", output);
        Assert.Contains("DERT1002", output);
        Assert.Contains("still present", output);
        Assert.EndsWith("// HOW ; notes ;; unfinished", File.ReadAllText(file.Path));
    }

    [Fact]
    public void Markup_in_a_title_is_shown_as_the_authors_literal_words()
    {
        using var file = new ScratchDocument("// [red]TITLE[/]\n// --");
        var (exit, output) = Run(["repair", file.Path, "--plain"]);
        Assert.Equal(0, exit);
        Assert.Contains("[red]TITLE[/]", output);
    }

    [Fact]
    public void A_narrow_console_does_not_hard_wrap_the_copyable_approval_command()
    {
        using var file = new ScratchDocument("// TITLE");
        var (exit, output) = Run(["repair", file.Path, "--plain"], width: 32);
        Assert.Equal(0, exit);
        var command = Assert.Single(output.Split('\n'), line => line.StartsWith("ERPDERT repair '"));
        Assert.Contains(file.Path, command);
        Assert.Matches(@"--apply [A-F0-9]{64}", command);
        Assert.DoesNotContain("\u001b", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Copy_commands_preserve_apostrophes_and_express_hidden_path_characters_visibly()
    {
        using var file = new ScratchDocument("// TITLE");
        var path = System.IO.Path.Combine(file.DirectoryPath, "it's a\u200Bnote.DERT");
        File.Move(file.Path, path);
        var (exit, output) = Run(["repair", path, "--plain"]);
        Assert.Equal(0, exit);
        var command = Assert.Single(output.Split('\n'), line => line.StartsWith("ERPDERT repair ("));
        Assert.Contains("it''s a' + [char]0x200B + 'note.DERT')", command);
        Assert.DoesNotContain("\u200B", command, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u200B", command, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData('\u2018')]
    [InlineData('\u2019')]
    [InlineData('\u201A')]
    [InlineData('\u201B')]
    public void PowerShell_smart_quotes_in_filenames_are_expressed_as_characters(char quote)
    {
        using var file = new ScratchDocument("// TITLE");
        var path = System.IO.Path.Combine(file.DirectoryPath, $"Ryan{quote}s notes.DERT");
        File.Move(file.Path, path);
        var output = Run(["repair", path, "--plain"]).Output;
        var command = Assert.Single(output.Split('\n'), line => line.StartsWith("ERPDERT repair "));
        Assert.Contains($"[char]0x{(int)quote:X4}", command);
    }

    [Theory]
    [InlineData("repair", "--apply", "")]
    [InlineData("repair", "--apply", "yes")]
    [InlineData("undo", "--at", "1")]
    [InlineData("inspect", "--apply", "yes")]
    public void Mutation_options_cannot_be_mistaken_for_unconditional_permission(string command, string option, string value)
    {
        using var file = new ScratchDocument("// TITLE");
        Assert.Equal(2, Run([command, file.Path, option, value, "--plain"]).Exit);
        Assert.Equal("// TITLE", File.ReadAllText(file.Path));
    }
}
