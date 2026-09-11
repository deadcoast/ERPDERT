using System.Text;
using Dert.Cli;
using Dert.Core;
using Spectre.Console;
using Xunit;

namespace Dert.Tests;

public class ContextCommandTests
{
    private const string Source = "// HOW ; codex ;; Keep the existing install. //\r\n// WHY ; codex ;; Replace it only after evidence. //\nSTERT ; update ;; codex\nƒin.";

    [Fact]
    public void Default_context_previews_only_the_requested_note_and_offers_full_review()
    {
        using var file = new ScratchDocument(Source); var before = File.ReadAllBytes(file.Path);
        var result = Run(["context", file.Path, "--at", "1", "--plain"]);
        Assert.Equal(0, result.Exit); Assert.Contains("SELECTED CONTEXT", result.Output);
        Assert.Contains("1 note", result.Output); Assert.Contains("Left out", result.Output);
        Assert.Contains("--details", result.Output); Assert.Contains("--from-preview", result.Output);
        Assert.DoesNotContain("Find Codex on this PC", result.Output);
        Assert.Equal(before, File.ReadAllBytes(file.Path));
    }

    [Theory]
    [InlineData("--at", "0")]
    [InlineData("--at", "3")]
    [InlineData("--include", "2")]
    [InlineData("--at", "1", "--include", "3")]
    [InlineData("--at", "1", "--text", "--details")]
    [InlineData("--at", "1", "--output", "copy.txt")]
    public void Invalid_or_ambiguous_context_requests_are_explained(params string[] flags)
    {
        using var file = new ScratchDocument(Source);
        Assert.Equal(2, Run(["context", file.Path, .. flags, "--plain"]).Exit);
    }

    [Fact]
    public void Explicit_text_and_new_file_export_use_the_exact_reviewed_packet()
    {
        using var file = new ScratchDocument(Source);
        var path = file.Path + ".context.txt";
        try
        {
            var packet = ContextSelection.Capture(DertParser.Parse(Source), 1, Path.GetFullPath(file.Path), ContextSourceKind.FileSnapshot).Build([2]);
            var text = Run(["context", file.Path, "--at", "1", "--include", "2", "--text"]);
            Assert.Equal(0, text.Exit); Assert.Equal(packet.Report, text.Output);
            Assert.DoesNotContain("\u001b", text.Output, StringComparison.Ordinal);
            var saved = Run(["context", file.Path, "--at", "1", "--include", "2", "--output", path, "--from-preview", packet.ReviewId, "--plain"]);
            Assert.Equal(0, saved.Exit); Assert.Equal(new UTF8Encoding(false, true).GetBytes(packet.Report), File.ReadAllBytes(path));
            Assert.Equal(2, Run(["context", file.Path, "--at", "1", "--include", "2", "--output", path, "--from-preview", packet.ReviewId, "--plain"]).Exit);
            Assert.Equal(Source, File.ReadAllText(file.Path));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Changed_source_or_selection_refuses_the_previous_export(bool changeSource)
    {
        using var file = new ScratchDocument(Source); var path = file.Path + ".context.txt";
        var packet = ContextSelection.Capture(DertParser.Parse(Source), 1, Path.GetFullPath(file.Path), ContextSourceKind.FileSnapshot).Build();
        if (changeSource) File.AppendAllText(file.Path, "\n// New disagreement.");
        var result = Run(["context", file.Path, "--at", "1", .. (changeSource ? Array.Empty<string>() : ["--include", "2"]),
            "--output", path, "--from-preview", packet.ReviewId, "--plain"]);
        Assert.Equal(2, result.Exit); Assert.Contains("changed", result.Output); Assert.False(File.Exists(path));
    }

    [Fact]
    public void An_unfinished_note_can_be_shared_with_its_real_boundary_status()
    {
        using var file = new ScratchDocument("// HOW ; music ;;\n// Keep this, // with its annotation.");
        var result = Run(["context", file.Path, "--at", "2", "--details", "--plain"]);
        Assert.Equal(0, result.Exit); Assert.Contains("missing", result.Output);
        Assert.Contains("Keep this, // with its annotation.", result.Output);
    }

    internal static (int Exit, string Output) Run(string[] args, string? input = null, int columns = 110)
    {
        using var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings { Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors, Out = new AnsiConsoleOutput(writer) });
        console.Profile.Width = columns; console.Profile.Height = 30; console.Profile.Capabilities.Interactive = false;
        return (CliApplication.Run(args, console, merInput: new StringReader(input ?? "")), writer.ToString());
    }
}
