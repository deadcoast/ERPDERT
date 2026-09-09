using Dert.Cli;
using Dert.Mer;
using Spectre.Console;
using Xunit;

namespace Dert.Tests;

public class MerDocumentWindowTests
{
    [Theory]
    [InlineData(80, 24)]
    [InlineData(120, 32)]
    [InlineData(200, 50)]
    public void The_default_command_leaves_a_document_window_instead_of_the_internal_check_log(int columns, int rows)
    {
        using var file = new ScratchDocument("// WEEKEND\n// --\n\nSTERT\n// why ; music ;; Keep the playlist. //\nƒin.");
        var result = Run(["mer", file.Path, "--plain"], columns, rows);
        Assert.Equal(0, result.Exit);
        Assert.Contains(".-- ", result.Output);
        Assert.Contains("1 | // WEEKEND", result.Output);
        Assert.Contains("2 | // --", result.Output);
        Assert.Contains("3 |", result.Output);
        Assert.Matches("5 marks\\s+short", result.Output);
        Assert.Contains("Preview only", result.Output);
        Assert.DoesNotContain("source structures traced", result.Output);
        Assert.DoesNotContain("original text retained", result.Output);
        Assert.DoesNotContain("FROM THIS ANALYSIS", result.Output);
        Assert.DoesNotContain("--from-lint", result.Output);
        Assert.Contains("--details", result.Output);
        Assert.DoesNotContain('\u001b', result.Output);
        Assert.All(result.Output.Split('\n'), line => Assert.True(TerminalText.Width(line.TrimEnd('\r')) < columns));
        Assert.True(result.Output.Split('\n').Length <= rows + 2);
    }

    [Fact]
    public void Full_detail_is_explicit_and_retains_the_exact_source_bound_repair_command()
    {
        using var file = new ScratchDocument("// TITLE\n// --");
        var result = Run(["mer", file.Path, "--details", "--plain"], 100, 30);
        Assert.Equal(0, result.Exit);
        Assert.Contains("Read the source / checked", result.Output);
        Assert.Contains("--from-lint", result.Output);
        Assert.Equal("// TITLE\n// --", File.ReadAllText(file.Path));
    }

    [Theory]
    [InlineData(35, 18)]
    [InlineData(46, 20)]
    [InlineData(80, 24)]
    [InlineData(120, 32)]
    public void The_window_fits_small_viewports_and_keeps_large_line_numbers_separate_from_source(int columns, int rows)
    {
        var journal = Journal();
        var frame = MerCompositor.Live(journal.Snapshot(TimeSpan.FromSeconds(2)), columns, rows, true);
        Assert.InRange(frame.Lines.Count, 1, rows - 2);
        Assert.All(frame.Lines, line => Assert.InRange(TerminalText.Width(line.Text), 0, columns - 1));
        if (rows >= 20) Assert.Contains("10001 | // café", string.Join('\n', frame.Lines.Select(line => line.Text)));
    }

    [Fact]
    public void The_document_edge_traces_new_evidence_then_settles_without_changing_the_source()
    {
        var journal = Journal();
        var early = MerCompositor.Live(journal.Snapshot(TimeSpan.FromSeconds(1)), 120, 32);
        var moving = MerCompositor.Live(journal.Snapshot(TimeSpan.FromSeconds(1.2)), 120, 32);
        Assert.NotEqual(Text(early), Text(moving));
        Assert.Contains("10001 | // café", Text(early));
        Assert.Contains("10001 | // café", Text(moving));
        Assert.Equal(Text(MerCompositor.Live(journal.Snapshot(TimeSpan.FromSeconds(2)), 120, 32)),
            Text(MerCompositor.Live(journal.Snapshot(TimeSpan.FromSeconds(20)), 120, 32)));
        Assert.Equal(Text(MerCompositor.Live(journal.Snapshot(TimeSpan.FromSeconds(1)), 120, 32, true)),
            Text(MerCompositor.Live(journal.Snapshot(TimeSpan.FromSeconds(20)), 120, 32, true)));
    }

    [Fact]
    public void A_settled_read_does_not_claim_the_waiting_checks_have_completed()
    {
        var journal = new MotionJournal(new("Notes", "No writes",
            [new("read", "Read source", "Read the original"), new("context", "Trace context", "Find owners")])
        { View = MotionView.Document }, DateTimeOffset.UnixEpoch);
        journal.Apply(new StartStage("read"), TimeSpan.Zero);
        journal.Apply(new SettleStage("read", StageState.Passed, "Read the original text."), TimeSpan.FromSeconds(1));
        var output = Text(MerCompositor.Live(journal.Snapshot(TimeSpan.FromSeconds(1.1)), 120, 32));
        Assert.DoesNotContain("complete", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Read the original text.", output);
        Assert.Contains("Next: Trace context", output);
    }

    [Fact]
    public void A_narrow_window_keeps_the_extent_label_intact()
    {
        var source = new MotionSource("note.DERT", [InkLine.Plain("// one"), InkLine.Plain("// two"), InkLine.Plain("// three"), InkLine.Plain("// four")]);
        var journal = new MotionJournal(new("Notes", "No writes", [new("read", "Read", "This source is still being read. No later check has completed. Keep the document visible while this check works.")], source)
        { View = MotionView.Document }, DateTimeOffset.UnixEpoch);
        journal.Apply(new StartStage("read"), TimeSpan.Zero);
        var output = Text(MerCompositor.Live(journal.Snapshot(TimeSpan.FromSeconds(2)), 36, 20, true));
        Assert.Contains("[ lines 1-3 / excerpt ]", output);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2500)]
    [InlineData(5000)]
    public void A_large_document_reads_only_a_window_around_the_selected_source_line(int line)
    {
        var content = new CountedLines(5000);
        var score = new MotionScore("Notes", "No writes", [new("read", "Read", "Read")], new("large.DERT", content))
        { View = MotionView.Document };
        var snapshot = new MotionSnapshot(score, DateTimeOffset.UnixEpoch, [], TimeSpan.Zero, MotionOutcome.Complete, "Done");
        var frame = MerCompositor.Document(snapshot, 120, 32, line);
        Assert.Contains($"{line} | // source {line}", Text(frame));
        Assert.Contains("/ excerpt ]", Text(frame));
        Assert.InRange(content.Reads, 1, 65);
    }

    private sealed class CountedLines(int count) : IReadOnlyList<InkLine>
    {
        public int Count => count;
        public int Reads { get; private set; }
        public InkLine this[int index] { get { Reads++; return InkLine.Plain($"// source {index + 1}"); } }
        public IEnumerator<InkLine> GetEnumerator() => Enumerable.Range(0, Count).Select(index => this[index]).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Fact]
    public void An_empty_document_keeps_a_usable_window_without_inventing_source_lines()
    {
        using var file = new ScratchDocument("");
        var result = Run(["mer", file.Path, "--plain"], 80, 24);
        Assert.Equal(0, result.Exit);
        Assert.Contains("Empty document", result.Output);
        Assert.DoesNotContain("1 |", result.Output);
    }

    [Fact]
    public void Reviewing_a_line_details_and_repair_uses_one_record_even_after_the_file_changes()
    {
        const string original = "// TITLE\n// --\n// plans ; weekend ;;\n// music // Keep this explanation.\n//";
        using var file = new ScratchDocument(original);
        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            Interactive = InteractionSupport.Yes,
            Out = new AnsiConsoleOutput(writer)
        });
        var read = new ChangedFileInput(() => File.WriteAllText(file.Path, "new words"));
        var exit = CliApplication.Run(["mer", file.Path, "--plain"], console, merInput: read);
        var output = writer.ToString();
        Assert.Equal(0, exit);
        Assert.Contains("Belongs to line 3", output);
        Assert.Contains("Keep this explanation.", output);
        Assert.Contains("Read the source / checked", output);
        var revision = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(original)));
        Assert.Contains("--from-lint " + revision, output);
        Assert.DoesNotContain("new words", output);
        Assert.Equal("new words", File.ReadAllText(file.Path));
    }

    private sealed class ChangedFileInput(Action change) : StringReader("at 4\ndetails\nrepair\n\n")
    {
        private bool changed;
        public override string? ReadLine()
        {
            if (!changed) { changed = true; change(); }
            return base.ReadLine();
        }
    }

    [Theory]
    [InlineData(80, 24)]
    [InlineData(132, 24)]
    public void An_overflow_hint_replaces_the_whole_annotation_row(int columns, int rows)
    {
        var journal = Journal();
        var annotation = new DocumentAnnotation(10001, Enumerable.Repeat(InkLine.Plain("A long explanation that would leave stale words at the right edge"), 30).ToArray());
        var frame = MerCompositor.Document(journal.Snapshot(TimeSpan.FromSeconds(2)), columns, rows, 10001, annotation);
        var hint = Assert.Single(frame.Lines, line => line.Text.Contains("... full explanation", StringComparison.Ordinal));
        Assert.EndsWith("details", hint.Text);
        Assert.All(frame.Lines, line => Assert.True(TerminalText.Width(line.Text) < columns));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ownership_margin_skips_nonmembers_and_stops_at_recovered_end(bool offscreenOpening)
    {
        var numbers = offscreenOpening ? new[] { 8, 9, 10, 11 } : new[] { 6, 7, 8, 9, 10, 11 };
        var content = numbers.Select(number => InkLine.Plain($"original {number}")).ToArray();
        var source = new MotionSource("notes.DERT", content, numbers) { ContentLines = content };
        var journal = new MotionJournal(new("Notes", "Read only", [new("read", "Read", "Read source")], source), DateTimeOffset.UnixEpoch);
        var annotation = new DocumentAnnotation(9, [InkLine.Plain("Entry / selected")])
        { Ownership = new(6, null, [7, 9], 9) };
        var frame = MerCompositor.Document(journal.Snapshot(TimeSpan.Zero), 132, 36, 9, annotation);
        foreach (var number in new[] { 8, 10, 11 })
        {
            var row = Assert.Single(frame.Lines, line => line.Text.Contains($"{number} | original", StringComparison.Ordinal));
            Assert.Equal(' ', row.Text[3]);
        }
    }

    private static MotionJournal Journal()
    {
        var source = new MotionSource("own words.DERT", [InkLine.Plain("first"), InkLine.Plain("second")], [10000, 10001])
        {
            ContentLines = [InkLine.Plain("// plans ; weekend ;;"), InkLine.Plain("// café 👩‍💻 // Keep  these spaces.")]
        };
        var journal = new MotionJournal(new("Notes", "No writes", [new("context", "Trace context", "Read context")], source)
        { View = MotionView.Document }, DateTimeOffset.UnixEpoch);
        journal.Apply(new StartStage("context"), TimeSpan.Zero);
        journal.Apply(new AddEvidence("context", new("Connection", "Keep  these spaces.", EvidenceKind.Source, 10001, 10000,
            new NoteConnectionFocus("plans", "café 👩‍💻", "Keep  these spaces.", null))), TimeSpan.FromSeconds(1));
        return journal;
    }

    private static string Text(MerFrame frame) => string.Join('\n', frame.Lines.Select(line => line.Text));

    private static (int Exit, string Output) Run(string[] args, int columns, int rows)
    {
        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer)
        });
        console.Profile.Width = columns;
        console.Profile.Height = rows;
        return (CliApplication.Run(args, console), writer.ToString());
    }
}
