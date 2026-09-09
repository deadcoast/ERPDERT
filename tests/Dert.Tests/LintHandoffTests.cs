using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Dert.Core;
using Dert.Cli;
using Spectre.Console;
using Xunit;

namespace Dert.Tests;

public class LintHandoffTests
{
    private static (int Exit, string Output) Run(string[] args, int width = 160, string[]? invocation = null,
        bool interactive = false, Func<string, bool>? confirm = null,
        Func<IReadOnlyList<NoteBoundary>, string?>? choose = null)
    {
        var output = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = interactive ? InteractionSupport.Yes : InteractionSupport.No,
            Out = new AnsiConsoleOutput(output)
        });
        console.Profile.Width = width;
        return (CliApplication.Run(args, console, confirm, invocation, choose), output.ToString());
    }

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static string[] Commands(string output) => output.Split('\n')
        .Where(l => l.StartsWith("DERT repair ", StringComparison.Ordinal) || l.StartsWith("& ", StringComparison.Ordinal))
        .Select(l => l.TrimEnd('\r')).ToArray();

    [Theory]
    [InlineData("// TITLE", 1)]
    [InlineData("// TITLE\n// --", 2)]
    [InlineData("\uFEFF// TITLE\r\n--\r\n", 2)]
    public void Title_finding_offers_a_source_bound_rehearsal_not_a_note_selection(string source, int findingLine)
    {
        using var file = new ScratchDocument(source);
        // This assertion checks the location, not terminal wrapping. A source-only
        // build can live under a longer path; narrow rendering is tested separately.
        var result = Run(["lint", file.Path, "--plain"], width: Math.Max(160, file.Path.Length + 80));
        Assert.Equal(0, result.Exit);
        Assert.Contains($"{file.Path}:{findingLine}", result.Output);
        Assert.Contains("REHEARSE / TITLE RULE", result.Output);
        Assert.Equal($"DERT repair '{file.Path}' --from-lint {Hash(source)} --plain", Assert.Single(Commands(result.Output)));
        Assert.DoesNotContain("--apply", result.Output);
        Assert.Equal(Encoding.UTF8.GetBytes(source), File.ReadAllBytes(file.Path));
        Assert.Single(Directory.EnumerateFileSystemEntries(file.DirectoryPath));
    }

    [Fact]
    public void Every_unfinished_block_links_to_its_own_opener_without_choosing_a_cut()
    {
        const string source = "STERT\n// first ; t ;;\n// entry\nwords\n// second ; t ;;\n// entry\nƒin.";
        using var file = new ScratchDocument(source);
        var result = Run(["lint", file.Path, "--plain"]);
        Assert.Equal(1, result.Exit);
        Assert.Equal(new[]
        {
            $"DERT repair '{file.Path}' --at 2 --from-lint {Hash(source)} --plain",
            $"DERT repair '{file.Path}' --at 5 --from-lint {Hash(source)} --plain"
        }, Commands(result.Output));
        Assert.DoesNotContain("--close-before", result.Output);
        Assert.DoesNotContain("--apply", result.Output);
    }

    [Theory]
    [InlineData("// first\tsecond", "DERT2003", "width")]
    [InlineData("// first\tsecond\n// ----", "DERT3004", "width")]
    [InlineData("// why ; t ;; unfinished", "DERT1002", "Inline-note")]
    public void Unsupported_findings_explain_the_missing_capability_without_a_dead_end_repair(string source, string code, string reason)
    {
        using var file = new ScratchDocument(source);
        var result = Run(["lint", file.Path, "--plain"]);
        Assert.Contains(code, result.Output);
        Assert.Contains("NO REPAIR OFFERED", result.Output);
        Assert.Contains(reason, result.Output);
        Assert.Empty(Commands(result.Output));
        Assert.Equal(source, File.ReadAllText(file.Path));
    }

    [Fact]
    public void List_notice_offers_current_file_authoring_without_a_repair_or_write_approval()
    {
        const string source = "items ; list ;;\nsnacks,";
        using var file = new ScratchDocument(source);
        var result = Run(["lint", file.Path, "--plain"]);
        Assert.Contains("CHOOSE / IN NOTES", result.Output);
        Assert.Contains("Add another item and Finish this list", result.Output);
        Assert.Contains("current file, which may differ", result.Output);
        Assert.Contains($"DERT write '{file.Path}'", result.Output);
        Assert.DoesNotContain("--from-lint", result.Output);
        Assert.DoesNotContain("--apply", result.Output);
        Assert.Equal(source, File.ReadAllText(file.Path));
    }

    [Fact]
    public void Mixed_findings_keep_each_action_beside_its_own_source_and_keep_unsupported_cases_explicit()
    {
        using var file = new ScratchDocument("// TITLE\n// --\n// why ; t ;; unfinished\n// block ; t ;;\n// entry");
        var result = Run(["lint", file.Path, "--plain"]);
        Assert.Equal(1, result.Exit);
        var sections = Regex.Split(result.Output, @"(?m)(?=^DERT\d{4} ·)");
        var title = Assert.Single(sections, s => s.StartsWith("DERT2003", StringComparison.Ordinal));
        var inline = Assert.Single(sections, s => s.StartsWith("DERT1002", StringComparison.Ordinal));
        var block = Assert.Single(sections, s => s.StartsWith("DERT1001", StringComparison.Ordinal));
        Assert.Single(Commands(title));
        Assert.Empty(Commands(inline));
        Assert.Contains("--at 4", Assert.Single(Commands(block)));
    }

    [Fact]
    public void A_clean_document_does_not_advertise_work_or_repair_commands()
    {
        using var file = new ScratchDocument("STERT\n// note\nƒin.");
        var result = Run(["lint", file.Path, "--plain"]);
        Assert.Equal(0, result.Exit);
        Assert.Empty(Commands(result.Output));
        Assert.DoesNotContain("NO REPAIR OFFERED", result.Output);
        Assert.DoesNotContain("--from-lint", result.Output);
    }

    [Fact]
    public void Lint_walks_the_source_in_order_instead_of_exposing_parser_check_order()
    {
        using var file = new ScratchDocument("// TITLE\n// --\n// why ; t ;; unfinished\n// block ; t ;;\n// entry");
        var result = Run(["lint", file.Path, "--plain"]);
        Assert.Equal(new[] { "DERT2003 · WARNING", "DERT1002 · ERROR", "DERT1001 · ERROR" },
            Regex.Matches(result.Output, @"(?m)^DERT\d{4} · (?:WARNING|ERROR|NOTICE)").Select(m => m.Value));
        Assert.Equal(1, result.Exit);
    }

    [Fact]
    public void Relative_input_is_pinned_to_an_absolute_path_and_the_actual_dotnet_host_invocation()
    {
        using var file = new ScratchDocument("// TITLE");
        var relative = Path.GetRelativePath(Environment.CurrentDirectory, file.Path);
        var result = Run(["lint", relative, "--plain"], invocation: [@"C:\Program Files\dotnet\dotnet.exe", @"C:\my app\DERT.dll"]);
        var command = Assert.Single(Commands(result.Output));
        Assert.StartsWith("& 'C:\\Program Files\\dotnet\\dotnet.exe' 'C:\\my app\\DERT.dll' repair '", command);
        Assert.Contains(file.Path, command);
        Assert.DoesNotContain($"repair '{relative}'", command);
    }

    [Fact]
    public void Narrow_commands_preserve_literal_paths_plain_mode_and_source_revision_on_one_logical_line()
    {
        using var file = new ScratchDocument("// TITLE");
        var path = Path.Combine(file.DirectoryPath, "Ryan’s 'note\u200B[red]$`.DERT");
        File.Move(file.Path, path);
        var result = Run(["lint", path, "--plain"], width: 32);
        var command = Assert.Single(Commands(result.Output));
        Assert.Contains("[char]0x2019", command);
        Assert.Contains("[char]0x200B", command);
        Assert.Contains("''note", command);
        Assert.Contains("[red]$`", command);
        Assert.EndsWith($"--from-lint {Hash("// TITLE")} --plain", command);
        Assert.DoesNotContain('\u001b', result.Output);
    }

    [Theory]
    [InlineData("// TITLE", null)]
    [InlineData("\uFEFF// TITLE\r\n// --", null)]
    [InlineData("// block ; t ;;\n// entry", "1")]
    public void A_lint_handoff_never_prompts_or_writes_even_in_an_interactive_terminal(string source, string? at)
    {
        using var file = new ScratchDocument(source);
        string[] scope = at is null ? [] : ["--at", at];
        var result = Run(["repair", file.Path, .. scope, "--from-lint", Hash(source), "--plain"],
            interactive: true, confirm: _ => throw new InvalidOperationException("Handoff asked permission to write."),
            choose: _ => throw new InvalidOperationException("Handoff implicitly asked for a cut."));
        Assert.Equal(0, result.Exit);
        Assert.Contains(at is null ? "REPAIR REHEARSAL" : "CHOICES ONLY", result.Output);
        Assert.Contains("FROM LINT", result.Output);
        Assert.Equal(Encoding.UTF8.GetBytes(source), File.ReadAllBytes(file.Path));
        Assert.Single(Directory.EnumerateFileSystemEntries(file.DirectoryPath));
    }

    [Theory]
    [InlineData("// TITLE", "// NEW TITLE", null)]
    [InlineData("// old ; t ;;\n// entry", "STERT\n// new ; t ;;\n// other", "1")]
    public void Stale_lint_sources_are_refused_before_a_different_title_or_block_can_be_shown(string original, string changed, string? at)
    {
        using var file = new ScratchDocument(changed);
        string[] scope = at is null ? [] : ["--at", at];
        var result = Run(["repair", file.Path, .. scope, "--from-lint", Hash(original), "--plain"]);
        Assert.Equal(1, result.Exit);
        Assert.Contains("DERT4002", result.Output);
        Assert.Contains("changed since lint", result.Output);
        Assert.DoesNotContain("REPAIR REHEARSAL", result.Output);
        Assert.DoesNotContain("CHOOSE A NOTE BOUNDARY", result.Output);
        Assert.Equal(changed, File.ReadAllText(file.Path));
        Assert.Single(Directory.EnumerateFileSystemEntries(file.DirectoryPath));
    }

    [Fact]
    public void A_copied_boundary_choice_keeps_the_lint_revision_and_plain_preference_until_selected_preview()
    {
        const string source = "// block ; t ;;\n// entry";
        using var file = new ScratchDocument(source);
        var choices = Run(["repair", file.Path, "--at", "1", "--from-lint", Hash(source), "--plain"]);
        Assert.Equal(new[]
        {
            $"DERT repair '{file.Path}' --at 1 --close-before 2 --from-lint {Hash(source)} --plain",
            $"DERT repair '{file.Path}' --at 1 --close-before eof --from-lint {Hash(source)} --plain"
        }, Commands(choices.Output));
        var selected = Run(["repair", file.Path, "--at", "1", "--close-before", "eof", "--from-lint", Hash(source), "--plain"],
            interactive: true, confirm: _ => throw new InvalidOperationException("Selection from lint asked to write."));
        Assert.Equal(0, selected.Exit);
        Assert.Contains("BOUNDARY REHEARSAL", selected.Output);
        Assert.Contains("PREVIEW ONLY", selected.Output);
        var approval = Assert.Single(Commands(selected.Output));
        Assert.Contains("--plain", approval);
        Assert.Contains("--apply", approval);
        Assert.DoesNotContain("--from-lint", approval);
        Assert.Equal(source, File.ReadAllText(file.Path));
        Assert.Single(Directory.EnumerateFileSystemEntries(file.DirectoryPath));
    }

    [Fact]
    public void Title_approval_after_lint_preserves_plain_mode_but_is_a_separate_action()
    {
        using var file = new ScratchDocument("// TITLE");
        var preview = Run(["repair", file.Path, "--from-lint", Hash("// TITLE"), "--plain"]);
        var command = Assert.Single(Commands(preview.Output));
        Assert.Contains("--plain", command);
        Assert.Contains("--apply", command);
        Assert.DoesNotContain("--from-lint", command);
    }

    [Theory]
    [InlineData("--from-lint", "yes")]
    [InlineData("--from-lint")]
    public void Malformed_lint_links_are_not_permission_to_enter_normal_repair(params string[] options)
    {
        using var file = new ScratchDocument("// TITLE");
        Assert.Equal(2, Run(["repair", file.Path, .. options], confirm: _ => true).Exit);
        Assert.Equal("// TITLE", File.ReadAllText(file.Path));
    }

    [Theory]
    [InlineData("undo")]
    [InlineData("inspect")]
    [InlineData("lint")]
    public void Lint_handoff_mode_is_only_accepted_on_repair(string command)
    {
        using var file = new ScratchDocument("// TITLE");
        Assert.Equal(2, Run([command, file.Path, "--from-lint", Hash("// TITLE")]).Exit);
    }

    [Fact]
    public void Source_bound_handoff_cannot_be_combined_with_approval_or_duplicate_revisions()
    {
        using var file = new ScratchDocument("// TITLE");
        var hash = Hash("// TITLE");
        Assert.Equal(2, Run(["repair", file.Path, "--from-lint", hash, "--apply", hash], confirm: _ => true).Exit);
        Assert.Equal(2, Run(["repair", file.Path, "--from-lint", hash, "--from-lint", hash]).Exit);
        Assert.Equal("// TITLE", File.ReadAllText(file.Path));
    }
}
