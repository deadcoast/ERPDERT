using Dert.Cli;
using Dert.Cli.Motion;
using Dert.Core;
using Dert.Mer;
using Spectre.Console;
using Xunit;

namespace Dert.Tests;

public class ContextPresentationTests
{
    [Theory]
    [InlineData("// similarities ; AI ;; codex, \"ACME, local\", codex //", "similarities / 3 written items")]
    [InlineData("// HOW ; codex, claude ;; Keep this sentence, intact. //", "HOW / whole explanation")]
    [InlineData("// similarities ; AI ;; a,,b, //", "2 empty places")]
    [InlineData("// similarities ; names ;;\n// \"still writing,\n//", "quote still open")]
    public void Inspect_and_MER_show_the_same_named_reading_without_changing_source(string source, string summary)
    {
        using var file = new ScratchDocument(source);
        var bytes = File.ReadAllBytes(file.Path);
        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings { Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors, Out = new AnsiConsoleOutput(writer) });
        console.Profile.Width = 220;
        Assert.Equal(0, CliApplication.Run(["inspect", file.Path, "--at", "1", "--plain"], console));
        Assert.Contains(summary, writer.ToString());
        var annotation = StructureConnection.Describe(new(DertParser.Parse(source)), 1);
        Assert.Contains(annotation.Lines, line => line.Text.Contains(summary, StringComparison.Ordinal));
        Assert.Contains(annotation.CompactLines!, line => line.Text.Contains(summary, StringComparison.Ordinal));
        Assert.Equal(bytes, File.ReadAllBytes(file.Path));
    }

    [Theory]
    [InlineData(58)]
    [InlineData(110)]
    public void Long_expanded_context_keeps_its_missing_boundary_in_the_actual_frame(int columns)
    {
        var source = "// similarities ; notes ;;\n" + string.Concat(Enumerable.Range(1, 20).Select(i => $"// entry {i}, // Keep the explanation for this entry.\n"));
        var analysis = NoteScore.Analyze(() => source, "unfinished-context.DERT", true);
        var journal = new MotionJournal(analysis.Score, DateTimeOffset.UnixEpoch);
        foreach (var observed in analysis.Events) journal.Apply(observed, TimeSpan.Zero);
        var annotation = StructureConnection.Describe(new(analysis.Document!), 1);
        var frame = MerCompositor.Document(journal.Snapshot(TimeSpan.Zero), columns, 30, 1, annotation);
        var text = string.Join('\n', frame.Lines.Select(line => line.Text));
        Assert.Contains("Closing // missing", text);
        var summaryRow = Array.FindIndex(frame.Lines.ToArray(), line => line.Text.Contains("similarities / 20 written", StringComparison.Ordinal));
        Assert.True(summaryRow >= 0);
        Assert.Contains("items", string.Join('\n', frame.Lines.Skip(summaryRow).Take(2).Select(line => line.Text)));
        if (columns == 110) Assert.Contains("More in details", text);
    }

    [Theory]
    [InlineData(58)]
    [InlineData(80)]
    public void Narrow_MER_keeps_the_named_reading_and_missing_note_boundary_visible(int columns)
    {
        const string source = "// similarities ; AI ;; codex, claude";
        var analysis = NoteScore.Analyze(() => source, "context.DERT", true);
        var journal = new MotionJournal(analysis.Score, DateTimeOffset.UnixEpoch);
        foreach (var observed in analysis.Events) journal.Apply(observed, TimeSpan.Zero);
        var annotation = StructureConnection.Describe(new(analysis.Document!), 1);
        var frame = MerCompositor.Document(journal.Snapshot(TimeSpan.Zero), columns, 30, 1, annotation);
        var text = string.Join('\n', frame.Lines.Select(line => line.Text));
        Assert.Matches(@"similarities / 2 written\s+items", text);
        Assert.Contains("Closing // missing", text);
        Assert.All(frame.Lines, line => Assert.True(TerminalText.Width(line.Text) < columns));
    }
}
