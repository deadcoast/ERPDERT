using Dert.Cli;
using Dert.Cli.Motion;
using Dert.Core;
using Dert.Mer;
using Spectre.Console;
using Xunit;

namespace Dert.Tests;

public class ReferencePresentationTests
{
    private const string Source = "// HOW ; codex ;; Keep this reason, intact. //\n// similarities ; AI ;; codex, codex //";

    [Fact]
    public void Explicit_CLI_references_preserve_source_and_distinguish_named_items_from_parents()
    {
        using var file = new ScratchDocument(Source); var bytes = File.ReadAllBytes(file.Path);
        var result = Run(["inspect", file.Path, "--at", "1", "--references", "--plain"]);
        Assert.Equal(0, result.Exit);
        Assert.Contains("NAMED PLACES", result.Output);
        Assert.Contains("line 2:25 / listed item", result.Output);
        Assert.Contains("line 2:32 / listed item", result.Output);
        Assert.Contains("belongs to document", result.Output);
        Assert.Contains("no operation binding", result.Output);
        Assert.DoesNotContain("Find Codex on this PC", result.Output);
        Assert.Equal(bytes, File.ReadAllBytes(file.Path));
    }

    [Theory]
    [InlineData("inspect", "--references")]
    [InlineData("inspect", "--at", "1", "--references", "--references")]
    [InlineData("lint", "--references")]
    public void References_requires_an_explicit_inspection_location_and_rejects_misplaced_flags(string command, params string[] flags)
    {
        using var file = new ScratchDocument(Source);
        Assert.Equal(2, Run([command, file.Path, .. flags, "--plain"]).Exit);
    }

    [Fact]
    public void MER_follows_each_exact_occurrence_and_returns_to_the_reference_origin()
    {
        using var file = new ScratchDocument(Source); var bytes = File.ReadAllBytes(file.Path);
        var result = Review(file.Path, "at 1\nrefs\nref 2\ndetails\nref back\nstructure\n\n");
        Assert.Equal(0, result.Exit);
        Assert.Contains("Name match / codex", result.Output);
        Assert.Contains("line 2:32 / listed item", result.Output);
        Assert.Contains("Reference origin / line 1", result.Output);
        Assert.Equal(bytes, File.ReadAllBytes(file.Path));
    }

    [Theory]
    [InlineData(58)]
    [InlineData(110)]
    [InlineData(140)]
    public void Reference_frame_contains_every_destination_offered_on_the_page(int columns)
    {
        var analysis = NoteScore.Analyze(() => Source.Replace("codex, codex //", "codex, codex, codex, codex //", StringComparison.Ordinal), "references.DERT", true);
        var journal = new MotionJournal(analysis.Score, DateTimeOffset.UnixEpoch);
        foreach (var observed in analysis.Events) journal.Apply(observed, TimeSpan.Zero);
        var view = ReferenceConnection.Read(new DertStructureIndex(analysis.Document!), 1)!;
        var frame = MerCompositor.Document(journal.Snapshot(TimeSpan.Zero), columns, 30, 1, view.Describe());
        var text = string.Join('\n', frame.Lines.Select(line => line.Text));
        for (var number = 1; number <= 4; number++) Assert.Contains($"ref {number} /", text);
        Assert.Contains("Closed by //", text);
        Assert.All(frame.Lines, line => Assert.True(TerminalText.Width(line.Text) < columns));
    }

    [Fact]
    public void Canceling_a_repair_keeps_display_and_navigation_on_the_same_line()
    {
        using var file = new ScratchDocument("// TITLE\n// --\n" + Source);
        var result = Review(file.Path, "at 3\nrefs\nrepair title\ncancel\ndetails\nref back\n\n");
        var canceled = result.Output.Split("Preview left without applying.", StringSplitOptions.None)[1]
            .Split("Reference origin /", StringSplitOptions.None)[0];
        Assert.DoesNotContain("captured note 3", canceled);
        Assert.Contains("Title rule / line 2", canceled);
        Assert.Contains("Reference origin / line 3", result.Output);
    }

    [Fact]
    public void MER_pages_destinations_and_keeps_the_numbered_set_after_an_invalid_choice()
    {
        using var file = new ScratchDocument("// HOW ; music ;; Start here. //\n" + string.Join('\n', Enumerable.Range(1, 10).Select(i => $"// note {i} ; music ;; words //")));
        var result = Review(file.Path, "at 1\nrefs\nrefs 3\nrefs 9\nref 11\nref 10\ndetails\nref back\n\n", 58);
        Assert.Equal(0, result.Exit);
        Assert.Contains("References 9-10 of 10", result.Output);
        Assert.Contains("No reference has that number", result.Output);
        Assert.Contains("line 11:14 / target", result.Output);
        Assert.Contains("Reference origin / line 1", result.Output);
    }

    [Fact]
    public void Applying_a_repair_discards_old_reference_numbers_before_fresh_navigation()
    {
        using var file = new ScratchDocument("// TITLE\n" + Source);
        var result = Review(file.Path, "at 2\nrefs\nrepair title\napply\nref 1\nat 3\nrefs\nref 2\ndetails\n\n");
        Assert.Contains("References cleared after the source changed", result.Output);
        Assert.Contains("No reference has that number", result.Output);
        Assert.Contains("line 4:32 / listed item", result.Output);
    }

    [Fact]
    public void Pending_repair_blocks_reference_navigation_without_replacing_its_choices()
    {
        using var file = new ScratchDocument("// TITLE\n" + Source); var before = File.ReadAllBytes(file.Path);
        var result = Review(file.Path, "at 2\nrefs\nrepair title\nref 1\nrefs\ncancel\nref 1\ndetails\n\n");
        Assert.Contains("Finish this comparison", result.Output);
        Assert.Contains("Name match / codex", result.Output);
        Assert.Equal(before, File.ReadAllBytes(file.Path));
    }

    [Fact]
    public void No_match_and_ordinary_prose_remain_useful_without_invented_destinations()
    {
        using var file = new ScratchDocument("// HOW ; missing ;; Keep this explanation, whole. //\nordinary text missing");
        var result = Review(file.Path, "at 1\nrefs\nref 1\nat 2\nrefs\n\n");
        Assert.Contains("No other named place", result.Output);
        Assert.Contains("This line has no reasoning-note names", result.Output);
        Assert.Equal(0, result.Exit);
    }

    private static (int Exit, string Output) Review(string path, string input, int width = 140) => Run(["mer", path, "--plain", "--review"], input, width);
    private static (int Exit, string Output) Run(string[] args, string input = "", int width = 140)
    {
        using var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings { Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors, Out = new AnsiConsoleOutput(writer) });
        console.Profile.Width = width; console.Profile.Height = 40; console.Profile.Capabilities.Interactive = false;
        var exit = CliApplication.Run(args, console, merInput: new StringReader(input));
        return (exit, writer.ToString());
    }
}
