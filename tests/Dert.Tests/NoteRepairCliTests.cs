using Dert.Core;
using Erpdert.Cli;
using Erpdert.Cli.Repairs;
using Spectre.Console;
using Xunit;

namespace Dert.Tests;

public class NoteRepairCliTests
{
    private const string Source = "// context ; notes ;;\n// first, // its reason\n// later\ncommands ; updateme ;;";
    private static (int Exit, string Output) Run(string[] args, Func<string, bool>? confirm = null,
        Func<IReadOnlyList<NoteBoundary>, string?>? choose = null, int width = 160)
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
        return (CliApplication.Run(args, console, confirm, chooseBoundary: choose), output.ToString());
    }

    [Fact]
    public void Choice_report_is_read_only_and_cannot_approve_an_unselected_boundary()
    {
        using var file = new ScratchDocument(Source);
        var (exit, output) = Run(["repair", file.Path, "--at", "2", "--plain"]);
        Assert.Equal(0, exit);
        Assert.Contains("BOUNDARY", output);
        Assert.Contains("recovered grouping", output);
        Assert.Contains("--close-before 2", output);
        Assert.Contains("--close-before 3", output);
        Assert.Contains("--close-before 4", output);
        Assert.DoesNotContain("--apply", output);
        Assert.DoesNotContain("--close-before eof", output);
        Assert.Equal(Source, File.ReadAllText(file.Path));
        Assert.Single(Directory.EnumerateFileSystemEntries(file.DirectoryPath));
    }

    [Fact]
    public void Selected_cut_shows_exact_diff_and_actual_released_ownership_without_writing()
    {
        using var file = new ScratchDocument(Source);
        var (exit, output) = Run(["repair", file.Path, "--at", "1", "--close-before", "3", "--plain"]);
        Assert.Equal(0, exit);
        Assert.Contains("BOUNDARY REHEARSAL", output);
        Assert.Contains("1 kept", output);
        Assert.Contains("1 released", output);
        Assert.Contains("source 3 → result 4", output);
        Assert.Contains("ordinary note", output);
        Assert.Contains("document", output);
        Assert.Contains("// first, // its reason", output);
        Assert.Contains("// later", output);
        Assert.Matches(@"\+\s+3 │ //", output);
        Assert.Contains("--at 1 --close-before 3 --plain --apply", output);
        Assert.Equal(Source, File.ReadAllText(file.Path));
    }

    [Fact]
    public void Choice_and_permission_are_separate_and_both_can_leave_unchanged()
    {
        using var file = new ScratchDocument(Source);
        var confirmed = false;
        var left = Run(["repair", file.Path, "--at", "1", "--plain"], _ => confirmed = true, _ => null);
        Assert.Equal(0, left.Exit);
        Assert.False(confirmed);
        Assert.Contains("LEFT UNCHANGED", left.Output);
        var declined = Run(["repair", file.Path, "--at", "1", "--plain"], _ => false, choices =>
        {
            Assert.Equal(new[] { "2", "3", "4" }, choices.Select(c => c.Key));
            return "3";
        });
        Assert.Equal(0, declined.Exit);
        Assert.Contains("BOUNDARY REHEARSAL", declined.Output);
        Assert.Contains("LEFT UNCHANGED", declined.Output);
        Assert.Equal(Source, File.ReadAllText(file.Path));
        Assert.Single(Directory.EnumerateFileSystemEntries(file.DirectoryPath));
    }

    [Fact]
    public void Confirmed_selection_applies_and_generic_undo_previews_and_restores()
    {
        using var file = new ScratchDocument(Source);
        var applied = Run(["repair", file.Path, "--at", "2", "--plain"], question =>
        {
            Assert.Contains("note-boundary", question);
            return true;
        }, _ => "3");
        Assert.Equal(0, applied.Exit);
        Assert.Contains("NOTE BOUNDARY CLOSED", applied.Output);
        Assert.Equal(Source.Replace("// later", "//\n// later", StringComparison.Ordinal), File.ReadAllText(file.Path));
        var preview = Run(["undo", file.Path, "--plain"]);
        Assert.Equal(0, preview.Exit);
        Assert.Contains("UNDO REHEARSAL", preview.Output);
        Assert.Contains("original unfinished grouping", preview.Output);
        Assert.Matches(@"−\s+3 │ //", preview.Output);
        var undoCommand = Assert.Single(preview.Output.Split('\n'), l => l.StartsWith("ERPDERT undo '"));
        Assert.DoesNotContain("--close-before", undoCommand);
        Assert.Equal(0, Run(["undo", file.Path, "--plain"], _ => true).Exit);
        Assert.Equal(Source, File.ReadAllText(file.Path));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Changed_source_during_choice_or_confirmation_withdraws_the_captured_plan(bool duringChoice)
    {
        using var file = new ScratchDocument(Source);
        var result = Run(["repair", file.Path, "--at", "1", "--plain"], _ =>
        {
            if (!duringChoice) File.AppendAllText(file.Path, "\nnew words");
            return true;
        }, _ =>
        {
            if (duringChoice) File.AppendAllText(file.Path, "\nnew words");
            return "3";
        });
        Assert.Equal(1, result.Exit);
        Assert.Contains("DERT4002", result.Output);
        Assert.DoesNotContain("NOTE BOUNDARY CLOSED", result.Output);
        Assert.Equal(Source + "\nnew words", File.ReadAllText(file.Path));
    }

    [Fact]
    public void Approval_is_bound_to_the_selected_cut_not_just_the_source()
    {
        using var file = new ScratchDocument(Source);
        var service = new RepairFileService();
        var token = service.PreviewNote(service.InspectNote(file.Path, 1), "3").ApprovalToken;
        var wrong = Run(["repair", file.Path, "--at", "1", "--close-before", "4", "--apply", token, "--plain"]);
        Assert.Equal(1, wrong.Exit);
        Assert.Contains("DERT4002", wrong.Output);
        Assert.Equal(Source, File.ReadAllText(file.Path));
        Assert.Equal(0, Run(["repair", file.Path, "--at", "2", "--close-before", "3", "--apply", token, "--plain"]).Exit);
    }

    [Fact]
    public void Released_structured_opener_owns_its_actual_entries_and_remaining_errors_are_visible()
    {
        const string text = "commands ; list ;;\n// outer ; notes ;;\n// inner ; notes ;;\n// item\nvalue";
        using var file = new ScratchDocument(text);
        var result = Run(["repair", file.Path, "--at", "2", "--close-before", "3", "--plain"], _ => true);
        Assert.Equal(0, result.Exit);
        Assert.Contains("reasoning block", result.Output);
        Assert.Contains("reasoning entry", result.Output);
        Assert.Contains("inner ; notes · result line 4", result.Output);
        Assert.Contains("DERT1001", result.Output);
        Assert.Contains("still present", result.Output);
        Assert.DoesNotContain("only changed the title", result.Output);
    }

    [Fact]
    public void An_unfinished_released_inline_note_is_not_described_as_closed()
    {
        using var file = new ScratchDocument("// outer ; t ;;\n// HOW ; t ;; unfinished\nwords");
        var result = Run(["repair", file.Path, "--at", "1", "--close-before", "2", "--plain"]);
        Assert.Equal(0, result.Exit);
        Assert.Contains("inline reasoning note", result.Output);
        Assert.Contains("DERT1002", result.Output);
        Assert.DoesNotContain("closed-form", result.Output);
    }

    [Fact]
    public void Direct_selected_rehearsal_shows_every_retained_entry_and_annotation()
    {
        using var file = new ScratchDocument("// outer ; t ;;\n// first, // first reason\n// second, // second reason\n// last, // last reason\nwords");
        var result = Run(["repair", file.Path, "--at", "1", "--close-before", "5", "--plain"]);
        Assert.Equal(0, result.Exit);
        Assert.Contains("// first, // first reason", result.Output);
        Assert.Contains("// second, // second reason", result.Output);
        Assert.Contains("// last, // last reason", result.Output);
        Assert.Contains("STAYS INSIDE", result.Output);
    }

    [Theory]
    [InlineData("// n ; t ;;", "eof")]
    [InlineData("// n ; t ;;\n// item\n", "EOF")]
    public void Eof_choice_uses_one_canonical_selection_and_undo_preserves_final_newline(string text, string key)
    {
        using var file = new ScratchDocument(text);
        var options = Run(["repair", file.Path, "--at", "1", "--plain"]);
        Assert.Equal(0, options.Exit);
        Assert.Single(options.Output.Split('\n'), l => l.Contains("--close-before eof"));
        var result = Run(["repair", file.Path, "--at", "1", "--close-before", key, "--plain"], _ => true);
        Assert.Equal(0, result.Exit);
        if (!text.Contains('\n')) Assert.Contains("Windows CRLF", result.Output);
        Assert.Equal(0, Run(["undo", file.Path, "--plain"], _ => true).Exit);
        Assert.Equal(text, File.ReadAllText(file.Path));
    }

    [Fact]
    public void Narrow_plain_output_keeps_complete_literal_safe_selection_and_approval_commands()
    {
        using var file = new ScratchDocument(Source);
        var path = System.IO.Path.Combine(file.DirectoryPath, "Ryan's [red]notes.DERT");
        File.Move(file.Path, path);
        foreach (var selected in new[] { false, true })
        {
            string[] args = selected ? ["repair", path, "--at", "1", "--close-before", "3", "--plain"]
                : ["repair", path, "--at", "1", "--plain"];
            var result = Run(args, width: 32);
            Assert.Equal(0, result.Exit);
            Assert.DoesNotContain('\u001b', result.Output);
            var commands = result.Output.Split('\n').Where(l => l.StartsWith("ERPDERT repair '"));
            Assert.Equal(selected ? 1 : 3, commands.Count());
            Assert.All(commands, c => Assert.Contains("Ryan''s [red]notes.DERT' --at 1 --close-before", c));
        }
    }

    [Theory]
    [InlineData("// n ; t ;; words", "1", "eof")]
    [InlineData("// n ; t ;;\n// item\n//", "1", "3")]
    [InlineData(Source, "1", "1")]
    [InlineData(Source, "4", "eof")]
    public void Unsupported_or_unoffered_selections_are_explained_without_writing(string text, string at, string boundary)
    {
        using var file = new ScratchDocument(text);
        var result = Run(["repair", file.Path, "--at", at, "--close-before", boundary, "--plain"], _ => true);
        Assert.Equal(1, result.Exit);
        Assert.Contains("DERT4005", result.Output);
        Assert.Equal(text, File.ReadAllText(file.Path));
    }

    [Theory]
    [InlineData("--close-before", "3")]
    [InlineData("--at", "1", "--at", "2")]
    [InlineData("--at", "1", "--close-before", "3", "--close-before", "4")]
    [InlineData("--at", "1", "--close-before", "0")]
    [InlineData("--at", "1", "--close-before", "guess")]
    public void Ambiguous_or_incomplete_arguments_never_select_a_boundary(params string[] options)
    {
        using var file = new ScratchDocument(Source);
        Assert.Equal(2, Run(["repair", file.Path, .. options, "--plain"]).Exit);
        Assert.Equal(Source, File.ReadAllText(file.Path));
    }

    [Fact]
    public void Apply_without_a_boundary_cannot_bypass_selection()
    {
        using var file = new ScratchDocument(Source);
        Assert.Equal(2, Run(["repair", file.Path, "--at", "1", "--apply", new string('A', 64)]).Exit);
    }
}
