using Dert.Cli;
using Dert.Cli.Discovery;
using Spectre.Console;
using Xunit;

namespace Dert.Tests;

public class EcosystemCommandTests
{
    [Fact]
    public void Help_names_the_ecosystem_and_both_application_commands()
    {
        var (_, output) = Run("--help");
        Assert.Contains("DERT update codex", output);
        Assert.Contains("DERT where", output);
    }

    [Fact]
    public void Update_retains_the_existing_local_discovery_entry()
    {
        var (exit, output) = Run("update", "codex", "--plain");
        Assert.Equal(1, exit);
        Assert.Contains("No codex command", output);
        Assert.DoesNotContain("Unknown command", output);
    }

    [Theory]
    [InlineData("STERT ; update ;; codex", "DERT update codex")]
    [InlineData("STERT ; ERPDERT ;; codex", "DERT update codex")]
    [InlineData("STERT ; where ;; codex", "DERT where codex")]
    [InlineData("STERT ; where ;; node", "DERT where node")]
    [InlineData("STERT ; where ;; codex\ncommands ; doctor ;;", "DERT where codex --doctor")]
    [InlineData("STERT ; where ;; codex\n// commands ; doctor ;; never run this example //", "DERT where codex")]
    public void Source_binding_preserves_spelling_and_explains_the_normalized_request(string source, string command)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".DERT");
        try
        {
            File.WriteAllText(path, source);
            var original = File.ReadAllBytes(path);
            var (exit, output) = Run("run", path, "--preview", "--plain");
            Assert.Equal(0, exit);
            Assert.Contains("STEP 01 / " + command, output);
            Assert.Contains(source.Split('\n')[0], output);
            Assert.Contains("No discovery was performed", output);
            Assert.Equal(original, File.ReadAllBytes(path));
            if (source.Contains("// commands")) Assert.DoesNotContain("STEP 01 / DERT where codex --doctor", output);
        }
        finally { File.Delete(path); }
    }

    private static (int Exit, string Output) Run(params string[] args)
    {
        var output = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No, Out = new AnsiConsoleOutput(output)
        });
        console.Profile.Width = 140;
        var exit = CliApplication.Run(args, console, discoveryEnvironment: new("", ".EXE;.CMD"));
        return (exit, output.ToString());
    }
}
