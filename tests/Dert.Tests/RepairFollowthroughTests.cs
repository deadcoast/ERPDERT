using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Dert.Core;
using Erpdert.Cli;
using Erpdert.Cli.Repairs;
using Spectre.Console;
using Xunit;

namespace Dert.Tests;

public class RepairFollowthroughTests
{
    private static (int Exit, string Output) Run(string[] args, Func<string, bool>? confirm = null,
        int width = 400, string[]? invocation = null, StringWriter? output = null,
        bool interactive = false, Func<IReadOnlyList<NoteBoundary>, string?>? choose = null)
    {
        output ??= new StringWriter();
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

    private static string Hash(string source) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
    private static string[] Links(string output) => output.Split('\n')
        .Where(l => (l.StartsWith("ERPDERT repair ", StringComparison.Ordinal) || l.StartsWith("& ", StringComparison.Ordinal))
            && l.Contains("--from-lint", StringComparison.Ordinal))
        .Select(l => l.TrimEnd('\r')).ToArray();
    private static string Verified(string output)
    {
        var start = output.IndexOf("VERIFIED RESULT", StringComparison.Ordinal);
        Assert.True(start >= 0, output);
        return output[start..];
    }

    [Theory]
    [InlineData("", "\n")]
    [InlineData("\uFEFF", "\r\n")]
    public void Inserting_a_title_rule_links_the_remaining_block_at_its_new_line_and_byte_revision(string bom, string newline)
    {
        var source = bom + string.Join(newline, "// TITLE", "STERT", "// why ; notes ;;", "// entry", "ƒin.");
        var expected = source.Replace("// TITLE" + newline, "// TITLE" + newline + "// -----" + newline, StringComparison.Ordinal);
        using var file = new ScratchDocument(source);
        var result = Run(["repair", file.Path, "--plain"], _ => true);
        Assert.Equal(0, result.Exit); // Success belongs to this edit, not all lint findings.
        Assert.Equal(Encoding.UTF8.GetBytes(expected), File.ReadAllBytes(file.Path));
        Assert.Equal($"ERPDERT repair '{file.Path}' --at 4 --from-lint {Hash(expected)} --plain", Assert.Single(Links(result.Output)));
        var verified = Verified(result.Output);
        Assert.Contains($"DERT1001 · ERROR · {file.Path}:4", verified);
        Assert.Contains("1 finding still present", verified);
        Assert.DoesNotContain("DERT2003", verified);
        Assert.DoesNotContain("--apply", verified);
        Assert.Contains($"ERPDERT undo '{file.Path}' --plain", result.Output);
    }

    [Fact]
    public void Closing_one_note_does_not_hide_a_newly_released_unfinished_block()
    {
        const string source = "commands ; list ;;\n// outer ; notes ;;\n// inner ; notes ;;\n// item\nvalue";
        const string expected = "commands ; list ;;\n// outer ; notes ;;\n//\n// inner ; notes ;;\n// item\nvalue";
        using var file = new ScratchDocument(source);
        var result = Run(["repair", file.Path, "--at", "2", "--close-before", "3", "--plain"], _ => true);
        Assert.Equal(0, result.Exit);
        Assert.Contains("NOTE BOUNDARY CLOSED", result.Output);
        Assert.Equal(expected, File.ReadAllText(file.Path));
        Assert.Equal($"ERPDERT repair '{file.Path}' --at 4 --from-lint {Hash(expected)} --plain", Assert.Single(Links(result.Output)));
        Assert.Contains("the selected note boundary", Verified(result.Output));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Undo_offers_actions_for_the_restored_bytes_without_approving_them(bool noteRepair)
    {
        const string source = "\uFEFF// TITLE\r\n// why ; notes ;;\r\n// entry";
        using var file = new ScratchDocument(source);
        string[] scope = noteRepair ? ["--at", "2", "--close-before", "eof"] : [];
        Assert.Equal(0, Run(["repair", file.Path, .. scope, "--plain"], _ => true).Exit);
        var result = Run(["undo", file.Path, "--plain"], _ => true);
        Assert.Equal(0, result.Exit);
        Assert.Equal(Encoding.UTF8.GetBytes(source), File.ReadAllBytes(file.Path));
        var verified = Verified(result.Output);
        Assert.Contains("in the restored original", verified);
        Assert.Equal(new[]
        {
            $"ERPDERT repair '{file.Path}' --from-lint {Hash(source)} --plain",
            $"ERPDERT repair '{file.Path}' --at 2 --from-lint {Hash(source)} --plain"
        }, Links(verified));
        Assert.DoesNotContain("--apply", verified);
    }

    [Fact]
    public void Remaining_findings_keep_source_order_and_explain_unsupported_repairs_beside_their_lines()
    {
        using var file = new ScratchDocument("// TITLE\n// why ; notes ;; unfinished\nitems ; list ;;\nvalue,\n// block ; notes ;;\n// entry");
        var result = Run(["repair", file.Path, "--plain"], _ => true);
        var verified = Verified(result.Output);
        Assert.Equal(new[] { "DERT1002", "DERT3003", "DERT1001" },
            Regex.Matches(verified, @"(?m)^DERT\d{4}").Select(m => m.Value));
        var sections = Regex.Split(verified, @"(?m)(?=^DERT\d{4} ·)");
        var inline = Assert.Single(sections, s => s.StartsWith("DERT1002", StringComparison.Ordinal));
        var list = Assert.Single(sections, s => s.StartsWith("DERT3003", StringComparison.Ordinal));
        Assert.Contains("Inline-note closure is not available", inline);
        Assert.Contains("CHOOSE / IN NOTES", list);
        Assert.Contains($"ERPDERT write '{file.Path}'", list);
        Assert.Empty(Links(inline));
        Assert.Empty(Links(list));
        Assert.Contains("--at 6", Assert.Single(Links(verified)));
    }

    [Fact]
    public void Unsupported_title_width_is_not_advertised_as_a_followup_repair()
    {
        using var file = new ScratchDocument("// café\n// why ; notes ;;\n// entry");
        var result = Run(["repair", file.Path, "--at", "2", "--close-before", "eof", "--plain"], _ => true);
        Assert.Equal(0, result.Exit);
        var verified = Verified(result.Output);
        Assert.Contains("DERT2003", verified);
        Assert.Contains("NO REPAIR OFFERED", verified);
        Assert.Empty(Links(verified));
    }

    [Fact]
    public void No_remaining_findings_means_supported_checks_only_not_full_language_validation()
    {
        using var file = new ScratchDocument("// TITLE");
        var result = Run(["repair", file.Path, "--plain"], _ => true);
        Assert.Equal(0, result.Exit);
        var verified = Verified(result.Output);
        Assert.Contains("No remaining findings in the supported structural checks", verified);
        Assert.Contains("Command meanings were not validated", verified);
        Assert.Empty(Links(verified));
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("preview")]
    [InlineData("noop")]
    [InlineData("stale")]
    public void An_action_that_did_not_complete_offers_no_verified_result_or_followup_links(string state)
    {
        using var file = new ScratchDocument(state == "noop" ? "// TITLE\n// -----" : "// TITLE");
        Func<string, bool>? confirm = state switch
        {
            "cancel" => _ => false,
            "stale" => _ => { File.WriteAllText(file.Path, "// CHANGED"); return true; }
            ,
            "noop" => _ => throw new InvalidOperationException("No-op requested approval."),
            _ => null
        };
        var result = Run(["repair", file.Path, "--plain"], confirm);
        Assert.Equal(state == "stale" ? 1 : 0, result.Exit);
        Assert.DoesNotContain("VERIFIED RESULT", result.Output);
        Assert.Empty(Links(result.Output));
        Assert.Single(Directory.EnumerateFileSystemEntries(file.DirectoryPath));
    }

    [Fact]
    public void Followup_commands_keep_literal_paths_plain_mode_and_the_actual_host_on_one_logical_line()
    {
        using var file = new ScratchDocument("// TITLE\n// why ; notes ;;\n// entry");
        var path = Path.Combine(file.DirectoryPath, "Ryan’s 'note\u200B[red]$`.DERT");
        File.Move(file.Path, path);
        var result = Run(["repair", Path.GetRelativePath(Environment.CurrentDirectory, path), "--plain"], _ => true,
            width: 32, invocation: [@"C:\Program Files\dotnet\dotnet.exe", @"C:\my app\ERPDERT.dll"]);
        var command = Assert.Single(Links(result.Output));
        Assert.StartsWith("& 'C:\\Program Files\\dotnet\\dotnet.exe' 'C:\\my app\\ERPDERT.dll' repair ", command);
        Assert.Contains("[char]0x2019", command);
        Assert.Contains("[char]0x200B", command);
        Assert.Contains("''note", command);
        Assert.Contains("[red]$`", command);
        Assert.Contains(file.DirectoryPath, command);
        Assert.EndsWith(" --plain", command);
        Assert.DoesNotContain('\u001b', result.Output);
    }

    [Fact]
    public void A_followup_source_revision_opens_choices_without_inheriting_approval()
    {
        using var file = new ScratchDocument("// TITLE\n// why ; notes ;;\n// entry");
        var complete = Run(["repair", file.Path, "--plain"], _ => true);
        var link = Assert.Single(Links(complete.Output));
        var revision = Regex.Match(link, @"--from-lint ([A-F0-9]{64})").Groups[1].Value;
        var before = File.ReadAllBytes(file.Path);
        var receipts = Directory.GetFiles(file.DirectoryPath, "receipt.json", SearchOption.AllDirectories);
        var preview = Run(["repair", file.Path, "--at", "3", "--from-lint", revision, "--plain"],
            _ => throw new InvalidOperationException("Followup asked to write."), interactive: true,
            choose: _ => throw new InvalidOperationException("Followup chose a boundary."));
        Assert.Equal(0, preview.Exit);
        Assert.Contains("CHOICES ONLY", preview.Output);
        Assert.Equal(before, File.ReadAllBytes(file.Path));
        Assert.Equal(receipts, Directory.GetFiles(file.DirectoryPath, "receipt.json", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData("title")]
    [InlineData("note")]
    [InlineData("undo")]
    public void Copied_approval_keeps_plain_mode_for_its_result_actions(string kind)
    {
        using var file = new ScratchDocument("// TITLE\n// why ; notes ;;\n// entry");
        var service = new RepairFileService();
        if (kind == "undo") service.Apply(service.Preview(file.Path));
        string[] scope = kind == "note" ? ["--at", "2", "--close-before", "eof"] : [];
        var preview = Run([kind == "undo" ? "undo" : "repair", file.Path, .. scope, "--plain"]);
        var command = Assert.Single(preview.Output.Split('\n'), l => l.StartsWith("ERPDERT ", StringComparison.Ordinal) && l.Contains("--apply", StringComparison.Ordinal));
        Assert.Contains(" --plain --apply ", command);
    }

    [Fact]
    public void A_result_reports_verified_bytes_even_if_the_file_changes_while_success_is_being_printed()
    {
        const string expected = "// TITLE\n// -----\n// why ; notes ;;\n// entry";
        using var file = new ScratchDocument("// TITLE\n// why ; notes ;;\n// entry");
        var output = new ChangeOnSuccess(() => File.WriteAllText(file.Path, "// LATER WORDS"));
        var result = Run(["repair", file.Path, "--plain"], _ => true, output: output);
        Assert.True(output.Changed);
        Assert.Equal(0, result.Exit);
        Assert.Equal($"ERPDERT repair '{file.Path}' --at 3 --from-lint {Hash(expected)} --plain", Assert.Single(Links(result.Output)));
        Assert.Equal("// LATER WORDS", File.ReadAllText(file.Path));
        var stale = Run(["repair", file.Path, "--at", "3", "--from-lint", Hash(expected), "--plain"]);
        Assert.Equal(1, stale.Exit);
        Assert.Contains("DERT4002", stale.Output);
        Assert.DoesNotContain("CHOOSE A NOTE BOUNDARY", stale.Output);
    }

    private sealed class ChangeOnSuccess(Action change) : StringWriter
    {
        public bool Changed { get; private set; }
        public override void Write(string? value) { base.Write(value); Check(); }
        public override void Write(char value) { base.Write(value); Check(); }
        public override void Write(char[] buffer, int index, int count) { base.Write(buffer, index, count); Check(); }
        public override void Write(ReadOnlySpan<char> buffer) { base.Write(buffer); Check(); }
        private void Check()
        {
            if (Changed || !ToString().Contains("TITLE ALIGNED", StringComparison.Ordinal)) return;
            Changed = true;
            change();
        }
    }
}
