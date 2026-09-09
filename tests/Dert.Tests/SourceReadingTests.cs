using Dert.Cli;
using Dert.Cli.Motion;
using Dert.Core;
using Dert.Mer;
using Spectre.Console;
using Xunit;

namespace Dert.Tests;

public class SourceReadingTests
{
    [Theory]
    [InlineData("STERT ; ERPDERT ;; codex", "No ƒin. written / captured to end of document.")]
    [InlineData("STERT ; update ;; codex\n// context\nSTERT ; where ;; codex\nfin.", "No ƒin. written / next STERT on line 3.")]
    [InlineData("STERT ;\nfin.", "The hook is empty; its spelling is retained while you write.")]
    [InlineData("START ;\ncommand option ; variable", "Historical START / read as a statement; no chain opened.")]
    [InlineData("START\ncommand option variable ;;\nstring,", "Historical START / retained as text; no chain opened.")]
    [InlineData("START // preserved context", "Historical START / retained as text; no chain opened.")]
    [InlineData("STERT ; DERCS ;;\ncommand option ; variable\nfin.", "DERCS names the original help form. Its command fields are preserved.")]
    [InlineData("fin. // a loose goodbye", "No STERT owns this goodbye; it remains source text.")]
    public void Inspect_and_MER_explain_the_same_captured_source_form(string source, string explanation)
    {
        using var file = new ScratchDocument(source);
        var before = File.ReadAllBytes(file.Path);
        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings { Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors, Out = new AnsiConsoleOutput(writer) });
        console.Profile.Width = 180;
        Assert.Equal(0, CliApplication.Run(["inspect", file.Path, "--at", "1", "--plain"], console));
        Assert.Contains(explanation, writer.ToString());
        var annotation = StructureConnection.Describe(new(DertParser.Parse(source)), 1);
        Assert.Contains(explanation, annotation.Lines.Select(line => line.Text));
        Assert.DoesNotContain("Chain ending missing", annotation.Lines.Select(line => line.Text));
        Assert.Equal(before, File.ReadAllBytes(file.Path));
    }

    [Theory]
    [InlineData("fin.")]
    [InlineData("ƒin. // loose fragment")]
    public void A_loose_goodbye_is_not_highlighted_as_an_owned_boundary(string source)
    {
        var document = DertParser.Parse(source);
        Assert.IsType<TextNode>(Assert.Single(document.Nodes));
        Assert.DoesNotContain(document.Highlights, highlight => highlight.Role == SemanticRole.Boundary);
        Assert.Empty(FlorinGoodbye.Endings(document));
    }

    [Fact]
    public void Narrow_DERCS_context_keeps_the_source_extent_and_full_fields_remain_available()
    {
        var annotation = StructureConnection.Describe(new(DertParser.Parse("STERT ; DERCS ;; codex")), 1);
        Assert.Contains(annotation.CompactLines!, line => line.Text == "No ƒin. written / captured to end of document.");
        Assert.Contains(annotation.Lines, line => line.Text == ";; line / codex");
        Assert.Contains(annotation.CompactLines!, line => line.Text.Contains("original help form", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(58)]
    [InlineData(80)]
    public void Rendered_narrow_DERCS_keeps_help_intent_and_extent_after_wrapping(int columns)
    {
        const string source = "STERT ; DERCS ;; codex";
        var analysis = NoteScore.Analyze(() => source, "help.DERT", true);
        var journal = new MotionJournal(analysis.Score, DateTimeOffset.UnixEpoch);
        foreach (var observed in analysis.Events) journal.Apply(observed, TimeSpan.Zero);
        var annotation = StructureConnection.Describe(new(analysis.Document!), 1);
        var frame = MerCompositor.Document(journal.Snapshot(TimeSpan.Zero), columns, 30, 1, annotation);
        var output = string.Join('\n', frame.Lines.Select(line => line.Text));
        Assert.Contains("No ƒin. written", output);
        Assert.Matches(@"captured to end of\s+document\.", output);
        Assert.Matches(@"original help form", output);
        Assert.All(frame.Lines, line => Assert.True(TerminalText.Width(line.Text) < columns));
    }

    [Theory]
    [InlineData("\"START\"")]
    [InlineData("\"START\" // preserved context")]
    [InlineData("START your day here. // preserved context")]
    [InlineData("// START ; this is a free note")]
    [InlineData("// why ; START ;; A named target. //")]
    [InlineData("START your day here.")]
    [InlineData("\"fin.\"")]
    public void Prose_quotes_and_named_targets_do_not_acquire_historical_command_advice(string source)
    {
        var annotation = StructureConnection.Describe(new(DertParser.Parse(source)), 1);
        Assert.DoesNotContain(annotation.Lines, line => line.Text.Contains("Historical START", StringComparison.Ordinal));
        Assert.DoesNotContain(annotation.Lines, line => line.Text.Contains("No STERT owns", StringComparison.Ordinal));
    }
}
