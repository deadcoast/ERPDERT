using System.Security.Cryptography;
using System.Text;
using Erpdert.Cli;
using Spectre.Console;
using Xunit;

namespace Dert.Tests;

public class MerHandoffTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Analysis_offers_complete_commands_for_its_exact_bytes_and_actual_invocation(bool replay)
    {
        const string source = "\uFEFF// TITLE\r\n// --\r\n";
        using var file = new ScratchDocument(source);
        var relative = Path.GetRelativePath(Environment.CurrentDirectory, file.Path);
        var result = Run(["mer", relative, replay ? "--replay" : "--still", "--plain", "--details"],
            invocation: [@"C:\my app\ERPDERT.exe"]);
        Assert.Equal(0, result.Exit);
        Assert.Equal($"& 'C:\\my app\\ERPDERT.exe' repair '{file.Path}' --from-lint {Hash(source)} --plain", Assert.Single(Commands(result.Output)));
        Assert.Contains("REHEARSE / TITLE RULE", result.Output);
        Assert.DoesNotContain("--apply", result.Output);
        Assert.Equal(Encoding.UTF8.GetBytes(source), File.ReadAllBytes(file.Path));
        Assert.Single(Directory.EnumerateFileSystemEntries(file.DirectoryPath));
    }

    [Fact]
    public void Each_supported_finding_has_its_own_action_and_unsupported_repairs_remain_explicit()
    {
        using var file = new ScratchDocument("// TITLE\n// --\n// inline ; note ;; unfinished\n// plans ; weekend ;;\n// music");
        var result = Run(["mer", file.Path, "--plain", "--details"]);
        Assert.Equal(1, result.Exit);
        var commands = Commands(result.Output);
        Assert.Equal(2, commands.Length);
        Assert.DoesNotContain("--at", commands[0]);
        Assert.Contains("--at 4", commands[1]);
        Assert.Contains("Inline-note closure", result.Output);
        Assert.Contains("NO REPAIR OFFERED", result.Output);
        Assert.DoesNotContain("--close-before", result.Output);
        Assert.DoesNotContain("--apply", result.Output);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_file_changed_during_delivery_cannot_receive_a_repair_link_for_new_bytes(bool replay)
    {
        const string source = "// TITLE\n// --";
        using var file = new ScratchDocument(source);
        var writer = new ChangingOutput(() => File.WriteAllText(file.Path, "// DIFFERENT\n// --"));
        var result = Run(["mer", file.Path, replay ? "--replay" : "--still", "--plain", "--details"], writer);
        Assert.Equal(0, result.Exit);
        Assert.Contains("--from-lint " + Hash(source), Assert.Single(Commands(result.Output)));
        var refused = Run(["repair", file.Path, "--from-lint", Hash(source), "--plain"]);
        Assert.Equal(1, refused.Exit);
        Assert.Contains("DERT4002", refused.Output);
        Assert.Single(Directory.EnumerateFileSystemEntries(file.DirectoryPath));
    }

    [Theory]
    [InlineData("STERT\n// note\nƒin.", false)]
    [InlineData("// café", true)]
    public void Clear_or_unsupported_source_never_advertises_an_unavailable_repair(string source, bool finding)
    {
        using var file = new ScratchDocument(source);
        var result = Run(["mer", file.Path, "--plain", "--details"]);
        Assert.Empty(Commands(result.Output));
        Assert.Equal(finding, result.Output.Contains("NO REPAIR OFFERED", StringComparison.Ordinal));
        Assert.DoesNotContain("--from-lint", result.Output);
    }

    [Fact]
    public void An_incomplete_list_can_open_current_file_authoring_from_its_recorded_analysis()
    {
        const string source = "items ; list ;;\nsnacks,";
        using var file = new ScratchDocument(source);
        var result = Run(["mer", file.Path, "--plain", "--details"], invocation: [@"C:\my app\ERPDERT.exe"]);
        Assert.Contains("CHOOSE / IN NOTES", result.Output);
        Assert.Equal($"& 'C:\\my app\\ERPDERT.exe' write '{file.Path}'", Assert.Single(Commands(result.Output)));
        Assert.DoesNotContain("--from-lint", result.Output);
        Assert.DoesNotContain("--apply", result.Output);
        Assert.Equal(source, File.ReadAllText(file.Path));
    }

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static string[] Commands(string output) => output.Split('\n')
        .Where(line => line.StartsWith("ERPDERT repair ", StringComparison.Ordinal) || line.StartsWith("& ", StringComparison.Ordinal))
        .Select(line => line.TrimEnd('\r')).ToArray();

    private static (int Exit, string Output) Run(string[] args, StringWriter? writer = null, string[]? invocation = null)
    {
        writer ??= new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer)
        });
        console.Profile.Width = 32;
        var exit = CliApplication.Run(args, console,
            confirmRepair: _ => throw new InvalidOperationException("A preview must not ask to write."), invocation: invocation,
            chooseBoundary: _ => throw new InvalidOperationException("A handoff must not ask to choose."));
        return (exit, writer.ToString());
    }

    private sealed class ChangingOutput(Action change) : StringWriter
    {
        private bool changed;
        public override void Write(string? value)
        {
            if (!changed && value?.Contains("ERPDERT", StringComparison.Ordinal) == true)
            {
                changed = true;
                change();
            }
            base.Write(value);
        }
    }
}
