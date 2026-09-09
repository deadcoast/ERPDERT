using Dert.Cli;
using Spectre.Console;
using Xunit;

namespace Dert.Tests;

public class MerStructureTests
{
    [Fact]
    public void Ordinary_notes_and_active_sinkers_have_source_connected_inspection()
    {
        using var file = new ScratchDocument("STERT\nitems ; weekend ;;\nsnacks, // for everyone\n// Keep the playlist\nmusic\nfin.");
        var original = File.ReadAllBytes(file.Path);
        var result = Run(file.Path, "at 3\nstructure\nowner\nstructure\nat 4\nstructure\nat 6\nowner\n\n");
        Assert.Contains("Sinker / line 3", result.Output);
        Assert.Contains("Entry 1 of 2", result.Output);
        Assert.Contains("for everyone", result.Output);
        Assert.Contains("Statement / line 2", result.Output);
        Assert.Contains("; hook / weekend", result.Output);
        Assert.Contains("Free note / line 4", result.Output);
        Assert.Contains("Belongs to line 1 / STERT", result.Output);
        Assert.Contains("Chain / line 1", result.Output);
        Assert.DoesNotContain("No recorded connection", result.Output);
        Assert.Equal(original, File.ReadAllBytes(file.Path));
    }

    [Fact]
    public void Finding_text_in_explanations_returns_literal_source_and_its_owner()
    {
        using var file = new ScratchDocument("// IDEAS\n// -----\n// why ; codex, claude ;; Keep codex, too. //\n// support doctoring ; conflicts ;;\n// fixme // Keep it local\n//\n");
        var result = Run(file.Path, "find keep\nhit 2\nowner\nstructure\ndetails\nfind [.*]\nhit 0\n\n");
        Assert.Contains("2 matching lines", result.Output);
        Assert.Contains("Hit 2 of 2 / line 5", result.Output);
        Assert.Contains("Belongs to line 4 / support doctoring", result.Output);
        Assert.Contains("Reasoning block / line 4", result.Output);
        Assert.Contains("Closed by // on line 6", result.Output);
        Assert.Contains("No literal matches", result.Output);
        Assert.Contains("No search hit has that number", result.Output);
        Assert.DoesNotContain("No recorded connection", result.Output);
    }

    [Fact]
    public void Structure_can_inspect_a_claimed_target_without_an_operation_binding()
    {
        using var file = new ScratchDocument("// HOW ; codex, claude ;; Keep both, without guessing. //");
        var result = Run(file.Path, "at 1\nstructure\ndetails\n\n");
        Assert.Contains("; hook / codex, claude", result.Output);
        Assert.Contains("Target names as written", result.Output);
        Assert.Contains("Keep both, without guessing.", result.Output);
        Assert.DoesNotContain("Find Codex on this PC", result.Output);
    }

    [Fact]
    public void Search_is_paged_and_invalid_choices_preserve_the_selection()
    {
        using var file = new ScratchDocument(string.Join('\n', Enumerable.Range(1, 22).Select(i => $"line {i}: jazz [red]")));
        var result = Run(file.Path, "find jazz\nhits 3\nhit 22\nhits 0\nhit 23\nfind\nstructure\n\n");
        Assert.Contains("22 matching lines", result.Output);
        Assert.Contains("Hits 17-22 of 22", result.Output);
        Assert.Contains("Hit 22 of 22 / line 22", result.Output);
        Assert.Contains("Text / line 22", result.Output);
        Assert.Contains("[red]", result.Output);
        Assert.Contains("find <text>", result.Output);
    }

    [Fact]
    public void Repair_rebuilds_the_index_and_discards_search_hit_numbers()
    {
        using var file = new ScratchDocument("// TITLE\n// why ; music ;; Keep music. //");
        var result = Run(file.Path, "find music\nrepair title\napply\nhit 1\nfind music\nstructure\nundo\napply\nfind music\n\n");
        Assert.Contains("Search cleared after the source changed", result.Output);
        Assert.Contains("No search hit has that number", result.Output);
        Assert.Contains("Hit 1 of 1 / line 3", result.Output);
        Assert.Contains("Hit 1 of 1 / line 2", result.Output);
        Assert.Equal("// TITLE\n// why ; music ;; Keep music. //", File.ReadAllText(file.Path));
    }

    [Fact]
    public void Pending_repair_blocks_search_navigation_and_plain_search_is_read_only()
    {
        using var file = new ScratchDocument("// TITLE\n// why ; music ;; Keep music. //");
        var original = File.ReadAllBytes(file.Path);
        var result = Run(file.Path, "repair title\nfind music\nowner\ncancel\nfind music\n\n", 80);
        Assert.Contains("Finish this comparison", result.Output);
        Assert.Contains("Hit 1 of 1 / line 2", result.Output);
        Assert.DoesNotContain('\u001b', result.Output);
        Assert.Equal(original, File.ReadAllBytes(file.Path));
    }

    [Fact]
    public void Missing_closures_remain_unresolved_when_viewing_the_structure()
    {
        using var file = new ScratchDocument("// why ; music ;;\n// jazz, // quieter\nitems ; next ;;\nbook");
        var result = Run(file.Path, "at 2\nstructure\nowner\nstructure\ndetails\n\n");
        Assert.Contains("Closing // missing", result.Output);
        Assert.Contains("Reasoning entry / line 2", result.Output);
        Assert.DoesNotContain("Closed by //", result.Output);
    }

    [Fact]
    public void Notes_only_statements_do_not_receive_unsolicited_operation_errors()
    {
        using var file = new ScratchDocument("packing ; weekend ;;\ncoffee\n");
        var result = Run(file.Path, "at 1\ndetails\n\n");
        Assert.Equal(0, result.Exit);
        Assert.Contains("Statement / line 1", result.Output);
        Assert.Contains("; hook / weekend", result.Output);
        Assert.DoesNotContain("ERP6004", result.Output);
        Assert.DoesNotContain("Operation needs attention", result.Output);
    }

    [Fact]
    public void Title_inspection_does_not_invent_an_adjacent_rule()
    {
        using var file = new ScratchDocument("// TITLE\n// why ; music ;; Keep music. //");
        var result = Run(file.Path, "at 1\nstructure\ndetails\n\n");
        Assert.Contains("No adjacent title rule is present", result.Output);
        Assert.DoesNotContain("The adjacent rule belongs", result.Output);
    }

    private static (int Exit, string Output) Run(string path, string input, int width = 140)
    {
        using var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings { Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors, Out = new AnsiConsoleOutput(writer) });
        console.Profile.Width = width;
        console.Profile.Height = 40;
        console.Profile.Capabilities.Interactive = false;
        var exit = CliApplication.Run(["mer", path, "--plain", "--review"], console, merInput: new StringReader(input));
        return (exit, writer.ToString());
    }
}
