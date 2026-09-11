using Dert.Cli.Authoring;
using Xunit;

namespace Dert.Tests;

public sealed class DesktopCommandRunnerTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "dert-desktop-runner-" + Guid.NewGuid().ToString("N"));
    private static string CliPath => Path.Combine(AppContext.BaseDirectory, "DERT.exe");

    [Fact]
    public void Literal_quotes_and_optional_prefix_preserve_exact_argument_boundaries()
    {
        Assert.Equal(new[] { "where", "Visual Studio Code", "--path", "C:\\Ryan's tools\\codex.ps1" },
            DesktopCommandRunner.ParseArguments("DERT where \"Visual Studio Code\" --path 'C:\\Ryan''s tools\\codex.ps1'", CliPath));
        Assert.Equal(new[] { "context", "a file.DERT", "--output", "literal $name; & (text).txt" },
            DesktopCommandRunner.ParseArguments("context 'a file.DERT' --output \"literal $name; & (text).txt\"", CliPath));
        Assert.Equal(new[] { "where", "" }, DesktopCommandRunner.ParseArguments("dert.exe where ''", CliPath));
    }

    [Fact]
    public void A_bare_explicit_dert_invocation_keeps_the_clis_no_argument_help_behavior()
    {
        Assert.Empty(DesktopCommandRunner.ParseArguments("DERT", CliPath));
        Assert.Empty(DesktopCommandRunner.ParseArguments("& '" + CliPath.Replace("'", "''") + "'", CliPath));
    }

    [Fact]
    public void A_complete_approval_handoff_keeps_the_saved_token_and_target_exactly()
    {
        var command = "& '" + CliPath.Replace("'", "''") + "' where codex --doctor --path 'C:\\Tools\\codex.ps1' --apply 'saved-token'";
        Assert.Equal(new[] { "where", "codex", "--doctor", "--path", "C:\\Tools\\codex.ps1", "--apply", "saved-token" },
            DesktopCommandRunner.ParseArguments(command, CliPath));
    }

    [Theory]
    [InlineData("where codex; settings word-wrap off")]
    [InlineData("where codex | another")]
    [InlineData("where $(Get-Item x)")]
    [InlineData("where $env:HOME")]
    [InlineData("where %USERPROFILE%")]
    [InlineData("where `" )]
    [InlineData("where codex > output.txt")]
    [InlineData("where codex & other")]
    [InlineData("where codex\nsettings word-wrap off")]
    [InlineData("where 'unfinished")]
    [InlineData("& 'C:\\other\\DERT.exe' -help")]
    [InlineData("& powershell.exe -Command x")]
    [InlineData("")]
    public void Shell_expressions_unfinished_quotes_and_other_executable_handoffs_are_refused(string command)
    {
        Assert.Throws<ArgumentException>(() => DesktopCommandRunner.ParseArguments(command, CliPath));
    }

    [Theory]
    [InlineData(true, "inspect", "note.DERT")]
    [InlineData(true, "where", "codex")]
    [InlineData(true, "update", "codex")]
    [InlineData(true, "settings")]
    [InlineData(false, "settings", "word-wrap", "off")]
    [InlineData(false, "where", "codex", "--doctor")]
    [InlineData(false, "update", "codex", "updateme")]
    [InlineData(false, "update", "codex", "updateme", "--apply", "reviewed-token")]
    [InlineData(false, "repair", "note.DERT", "--apply", "reviewed-token")]
    [InlineData(false, "run", "note.DERT")]
    public void Cancellation_is_offered_only_for_explicit_read_only_commands(bool expected, params string[] arguments)
    {
        Assert.Equal(expected, DesktopCommandRunner.SupportsCancellation(arguments));
    }

    [Fact]
    public async Task The_actual_cli_reads_literal_unicode_paths_and_preserves_source_with_streamed_output()
    {
        var path = Write("Ryan's café; $name [note].DERT", "// ordinary café 日本語 👩‍💻\n");
        var before = File.ReadAllBytes(path);
        var chunks = new List<string>();
        var result = await DesktopCommandRunner.RunAsync(CliPath, ["inspect", path, "--expand", "--plain"], output: chunks.Add);
        Assert.Equal(0, result.ExitCode);
        Assert.False(result.Cancelled);
        Assert.Contains("café 日本語 👩‍💻", result.Output);
        Assert.Equal(result.Output, string.Concat(chunks));
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public async Task A_cli_refusal_retains_its_nonzero_exit_and_explanation()
    {
        var result = await DesktopCommandRunner.RunAsync(CliPath, ["where", "codex", "--made-up"]);
        Assert.Equal(2, result.ExitCode);
        Assert.False(result.Cancelled);
        Assert.Contains("--made-up", result.Output);
    }

    [Fact]
    public async Task Cancellation_before_start_has_no_file_effect_even_for_an_apply_command()
    {
        var path = Write("untouched.DERT", "// TITLE\n// -\n");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var result = await DesktopCommandRunner.RunAsync(CliPath, ["repair", path, "--apply", "anything"], cancellation.Token);
        Assert.True(result.Cancelled);
        Assert.Equal(130, result.ExitCode);
        Assert.Equal("// TITLE\n// -\n", File.ReadAllText(path));
        Assert.Single(Directory.EnumerateFileSystemEntries(directory));
    }

    [Fact]
    public async Task A_running_read_only_inspection_can_be_cancelled_after_output_begins()
    {
        var path = Write("large.DERT", string.Concat(Enumerable.Repeat("// ordinary writing with a long enough line to keep output busy\n", 20_000)));
        using var cancellation = new CancellationTokenSource();
        var result = await DesktopCommandRunner.RunAsync(CliPath, ["inspect", path, "--expand", "--plain"], cancellation.Token,
            _ => cancellation.Cancel());
        Assert.True(result.Cancelled);
        Assert.NotEmpty(result.Output);
    }

    [Fact]
    public async Task A_started_approved_source_repair_finishes_even_if_the_token_is_cancelled()
    {
        var path = Write("repair.DERT", "// TITLE\n// -\n");
        var preview = await DesktopCommandRunner.RunAsync(CliPath, ["repair", path, "--plain"]);
        var handoff = preview.Output.Split('\n').Select(line => line.Trim()).First(line => line.StartsWith("& '", StringComparison.Ordinal) && line.Contains("--apply", StringComparison.Ordinal));
        var arguments = DesktopCommandRunner.ParseArguments(handoff, CliPath);
        using var cancellation = new CancellationTokenSource();
        var result = await DesktopCommandRunner.RunAsync(CliPath, arguments, cancellation.Token, _ => cancellation.Cancel());
        Assert.True(cancellation.IsCancellationRequested);
        Assert.False(result.Cancelled);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("// TITLE\n// -----\n", File.ReadAllText(path));
        Assert.Contains("VERIFIED RESULT", result.Output);
    }

    [Theory]
    [InlineData("DERT.exe")]
    [InlineData("C:\\Windows\\System32\\cmd.exe")]
    public async Task The_runner_requires_an_absolute_bundled_dert_executable(string path)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => DesktopCommandRunner.RunAsync(path, ["-help"]));
    }

    private string Write(string name, string source)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, source);
        return path;
    }

    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
