using Dert.Cli;
using Dert.Cli.Discovery;
using Spectre.Console;
using Xunit;

namespace Dert.Tests;

public class DiscoveryCliTests
{
    [Fact]
    public void Codex_is_an_operation_not_a_document_path()
    {
        using var fixture = new DiscoveryFixture();
        var (exit, output) = RunIn(fixture, [], "update", "codex", "--plain");
        Assert.Contains("CODEX / DISCOVERY", output);
        Assert.InRange(exit, 0, 1);
        Assert.Contains("Nothing was executed, installed, updated, or changed.", output);
    }

    [Fact]
    public void Identified_launchers_render_one_group_with_evidence_and_no_guessed_updater()
    {
        using var fixture = new DiscoveryFixture();
        var prefix = fixture.Package("node tools [red]", "9.8.7");
        var (exit, output) = RunIn(fixture, [prefix], "update", "codex", "--plain");
        Assert.Equal(0, exit);
        Assert.Contains("One installation group", output);
        Assert.Contains("9.8.7", output);
        Assert.Contains("3 launcher paths", output);
        Assert.Contains("original installer is not proven", output);
        Assert.DoesNotContain("pnpm", output);
        Assert.DoesNotContain("npm install", output);
        Assert.Contains(prefix, output);
        Assert.DoesNotContain("\u001b", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_first_match_stays_first_in_the_report_and_returns_attention()
    {
        using var fixture = new DiscoveryFixture();
        var first = fixture.Write("manual/codex.exe", "unidentified");
        var prefix = fixture.Package("node", "1.2.3");
        var (exit, output) = RunIn(fixture, [Path.GetDirectoryName(first)!, prefix], "update", "codex", "--plain");
        Assert.Equal(1, exit);
        Assert.Contains("2 installation groups", output);
        Assert.True(output.IndexOf(first, StringComparison.Ordinal) < output.IndexOf(prefix, StringComparison.Ordinal));
        Assert.Contains("ERP5103", output);
        Assert.Contains("no updater selected", output);
    }

    [Fact]
    public void Hostile_metadata_is_visible_text_not_terminal_control_or_markup()
    {
        using var fixture = new DiscoveryFixture();
        var prefix = fixture.Package("node", "1.0.0\\u001b[2J[red]\\u202E");
        var (_, output) = RunIn(fixture, [prefix], "update", "codex", "--plain");
        Assert.DoesNotContain("\u001b", output, StringComparison.Ordinal);
        Assert.DoesNotContain("\u202E", output, StringComparison.Ordinal);
        Assert.Contains("\\u001B[2J[red]\\u202E", output);
    }

    [Fact]
    public void Empty_search_does_not_claim_uninstalled_or_trigger_an_install_prompt()
    {
        using var fixture = new DiscoveryFixture();
        var (exit, output) = RunIn(fixture, [], "update", "codex", "--plain");
        Assert.Equal(1, exit);
        Assert.Contains("does not prove Codex is uninstalled", output);
        Assert.Contains("0 command matches", output);
        Assert.DoesNotContain("npm install", output);
    }

    [Fact]
    public void Broken_metadata_is_a_cited_plain_English_finding_not_a_stack_trace()
    {
        using var fixture = new DiscoveryFixture();
        var prefix = fixture.Package("node", "1.0.0");
        File.WriteAllText(Path.Combine(prefix, "node_modules", "@openai", "codex", "package.json"), "null");
        var (exit, output) = RunIn(fixture, [prefix], "update", "codex", "--plain");
        Assert.Equal(1, exit);
        Assert.Contains("ERP5104", output);
        Assert.Contains("package.json", output);
        Assert.DoesNotContain("System.", output);
    }

    [Fact]
    public void Long_evidence_paths_remain_exact_copyable_logical_lines_in_a_narrow_terminal()
    {
        using var fixture = new DiscoveryFixture();
        var prefix = fixture.Package("long folder with spaces [literal]", "1.2.3");
        var (_, output) = RunWith(new(prefix, ".EXE;.CMD"), ["update", "codex", "--plain"], width: 40);
        var lines = output.Split('\n').Select(line => line.TrimEnd('\r')).ToArray();
        Assert.Contains(Path.Combine(prefix, "codex.ps1"), lines);
        Assert.Contains(lines, line => line.Contains(Path.Combine(prefix, "node.exe"), StringComparison.Ordinal));
        Assert.Contains("Get-Command codex -All | Select-Object CommandType, Name, Source, Definition", lines);
    }

    private static (int Exit, string Output) RunIn(DiscoveryFixture fixture, string[] directories, params string[] args) =>
        RunWith(new(string.Join(';', directories), ".COM;.EXE;.BAT;.CMD"), args);

    [Fact]
    public void Help_exposes_the_operational_entry_point_without_a_document()
    {
        var (_, output) = Run("-help");
        Assert.Contains("DERT update codex", output);
    }

    [Theory]
    [InlineData("--apply")]
    [InlineData("update")]
    [InlineData("notes.DERT")]
    public void Discovery_rejects_unimplemented_operations_and_document_options(string option)
    {
        var (exit, output) = Run("update", "codex", option);
        Assert.Equal(2, exit);
        Assert.Contains("read-only", output);
        Assert.Contains(option, output);
    }

    private static (int Exit, string Output) Run(params string[] args) => RunWith(null, args);

    private static (int Exit, string Output) RunWith(DiscoveryEnvironment? environment, string[] args, int width = 180)
    {
        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer)
        });
        console.Profile.Width = width;
        var exit = CliApplication.Run(args, console, discoveryEnvironment: environment);
        return (exit, writer.ToString());
    }
}
