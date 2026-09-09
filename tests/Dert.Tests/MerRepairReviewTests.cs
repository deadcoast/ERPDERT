using System.Text;
using System.Reflection;
using Erpdert.Cli;
using Erpdert.Cli.Discovery;
using Erpdert.Cli.Repairs;
using Erpdert.Mer;
using Spectre.Console;
using Xunit;

namespace Dert.Tests;

public class MerRepairReviewTests
{
    [Theory]
    [InlineData("repair\nbefore\nafter\n\n")]
    [InlineData("repair\ncancel\napply\n\n")]
    public void Comparing_or_leaving_a_proposal_never_writes(string commands)
    {
        using var file = new ScratchDocument("// WEEKEND\n// --");
        var result = Run(file.Path, commands);
        Assert.Contains("Proposed / not saved", result.Output);
        Assert.Contains("2 | // -------", result.Output);
        Assert.Equal("// WEEKEND\n// --", File.ReadAllText(file.Path));
        Assert.Single(Directory.EnumerateFileSystemEntries(file.DirectoryPath));
    }

    [Fact]
    public void Apply_and_undo_return_verified_bytes_and_reanalyze_the_source()
    {
        const string source = "\uFEFF// WEEKEND\r\n// --\nSTERT\r\n// music\nfin.";
        using var file = new ScratchDocument(source);
        var repaired = Run(file.Path, "repair\napply\nnext\ndetails\n\n");
        Assert.Equal(0, repaired.Exit);
        Assert.Contains("Saved / bytes verified", repaired.Output);
        Assert.Contains("Title aligned", repaired.Output);
        var afterSave = repaired.Output[repaired.Output.IndexOf("Saved / bytes verified", StringComparison.Ordinal)..];
        Assert.DoesNotContain("every proposed edit is still only a preview", afterSave);
        Assert.Contains("This subsequent analysis makes no further edits", afterSave);
        Assert.Equal(Encoding.UTF8.GetBytes(source.Replace("// --\n", "// -------\n")), File.ReadAllBytes(file.Path));
        var restored = Run(file.Path, "undo\nbefore\nafter\napply\n\n");
        Assert.Equal(0, restored.Exit);
        Assert.Contains("Restored / bytes verified", restored.Output);
        Assert.Equal(Encoding.UTF8.GetBytes(source), File.ReadAllBytes(file.Path));
    }

    [Fact]
    public void Note_repair_requires_a_boundary_and_shows_the_resulting_ownership()
    {
        using var file = new ScratchDocument("// plans ; weekend ;;\n// music,\n// food");
        var result = Run(file.Path, "repair\napply\nclose 3\ndetails\napply\n\n");
        Assert.Contains("Choose where this note ends", result.Output);
        Assert.Contains("No preview is ready to apply", result.Output);
        Assert.Contains("1 kept / 1 released", result.Output);
        Assert.Contains("ordinary note", result.Output);
        Assert.Contains("Note boundary closed", result.Output);
        Assert.Equal(0, result.Exit);
        Assert.Equal("// plans ; weekend ;;\n// music,\n//\n// food", File.ReadAllText(file.Path));
        var undo = Run(file.Path, "undo\napply\n\n");
        Assert.Equal(1, undo.Exit);
        Assert.Contains("Restored / bytes verified", undo.Output);
        Assert.Contains("needs attention", undo.Output);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Changed_source_refuses_a_preview_or_application(int changeAt)
    {
        using var file = new ScratchDocument("// TITLE\n// --");
        var result = Run(file.Path, new ChangingInput("repair\napply\n\n", changeAt,
            () => File.WriteAllText(file.Path, "// LATER WORDS\n// --")));
        Assert.Contains("DERT4002", result.Output);
        Assert.DoesNotContain("Saved / bytes verified", result.Output);
        Assert.Equal("// LATER WORDS\n// --", File.ReadAllText(file.Path));
        Assert.Single(Directory.EnumerateFileSystemEntries(file.DirectoryPath));
    }

    [Fact]
    public void Replay_can_compare_but_cannot_apply_a_proposal()
    {
        using var file = new ScratchDocument("// TITLE\n// --");
        var result = Run(file.Path, "repair\napply\n\n", replay: true);
        Assert.Contains("Replay is read-only", result.Output);
        Assert.Equal("// TITLE\n// --", File.ReadAllText(file.Path));
        Assert.Single(Directory.EnumerateFileSystemEntries(file.DirectoryPath));
    }

    [Fact]
    public void Successful_title_edit_keeps_other_errors_and_clears_old_probe_results()
    {
        using var file = new ScratchDocument("// TITLE\n// --\nSTERT ; ERPDERT ;; codex\nfin.");
        var result = Run(file.Path, "at 3\nprobe\nrepair title\napply\nat 3\ndetails\n\n");
        var afterSave = result.Output[result.Output.IndexOf("Saved / bytes verified", StringComparison.Ordinal)..];
        Assert.DoesNotContain("Local inspection recorded", afterSave);
        Assert.Contains("Find Codex on this PC", afterSave);
        using var unfinished = new ScratchDocument("// TITLE\n// --\n// why ; music ;; unfinished");
        var kept = Run(unfinished.Path, "repair title\napply\ndetails\n\n");
        Assert.Equal(1, kept.Exit);
        Assert.Contains("DERT1002", kept.Output);
        Assert.Contains("needs attention", kept.Output);
    }

    [Theory]
    [InlineData("// TITLE\nFollowing prose", "// TITLE\n// -----\nFollowing prose")]
    [InlineData("// TITLE", "// TITLE\r\n// -----")]
    public void Inserted_rules_keep_following_prose_and_undo_exactly(string source, string expected)
    {
        using var file = new ScratchDocument(source);
        Run(file.Path, "repair title\napply\n\n");
        Assert.Equal(expected, File.ReadAllText(file.Path));
        Run(file.Path, "undo\napply\n\n");
        Assert.Equal(source, File.ReadAllText(file.Path));
    }

    [Fact]
    public void Whitespace_on_an_existing_rule_is_shown_as_removed_source()
    {
        using var file = new ScratchDocument("// TITLE\n// -- \t");
        var result = Run(file.Path, "repair\n\n");
        var comparison = result.Output[result.Output.IndexOf("Proposed / not saved", StringComparison.Ordinal)..];
        Assert.Contains("- // --", comparison);
        Assert.DoesNotContain("(no rule)", comparison);
    }

    [Fact]
    public void Undo_explains_ownership_in_the_direction_of_restoration()
    {
        using var file = new ScratchDocument("// plans ; weekend ;;\n// music,\n// food");
        Run(file.Path, "repair\nclose 3\napply\n\n");
        var result = Run(file.Path, "undo\n\n");
        Assert.Contains("Line 4 -> 3: entry in note 1", result.Output);
        Assert.DoesNotContain("Line 3 -> 4: ordinary note", result.Output);
    }

    [Fact]
    public void Comparing_an_inserted_eof_closer_keeps_the_end_of_the_document_visible()
    {
        var source = "// plans ; weekend ;;\n" + string.Join('\n', Enumerable.Range(1, 50).Select(i => $"// entry {i},"));
        using var file = new ScratchDocument(source);
        var result = Run(file.Path, "at 1\nrepair\nclose eof\nbefore\n\n");
        var comparison = result.Output[result.Output.IndexOf("Before edit / captured bytes", StringComparison.Ordinal)..];
        Assert.Contains("51 | // entry 50,", comparison);
        Assert.DoesNotContain("1 | // plans", comparison);
    }

    [Fact]
    public void Leaving_an_eof_insertion_keeps_the_last_source_line_selected()
    {
        var source = "// plans ; weekend ;;\n" + string.Join('\n', Enumerable.Range(1, 50).Select(i => $"// entry {i},"));
        using var file = new ScratchDocument(source);
        var result = Run(file.Path, "at 1\nrepair\nclose eof\ncancel\n\n");
        var left = result.Output[result.Output.IndexOf("Preview left without applying", StringComparison.Ordinal)..];
        Assert.Contains(";  51 | // entry 50,", left);
        Assert.Equal(source, File.ReadAllText(file.Path));
    }

    [Fact]
    public void Missing_undo_history_does_not_block_a_valid_repair()
    {
        using var file = new ScratchDocument("// TITLE\n// --");
        var result = Run(file.Path, "undo\nrepair title\napply\n\n");
        Assert.Contains("DERT4003", result.Output);
        Assert.Contains("Saved / bytes verified", result.Output);
        Assert.Equal(0, result.Exit);
        Assert.Equal("// TITLE\n// -----", File.ReadAllText(file.Path));
    }

    [Fact]
    public void Copied_note_choices_remain_bound_to_the_captured_bytes()
    {
        using var file = new ScratchDocument("// plans ; weekend ;;\n// music,\n// food");
        var result = Run(file.Path, "repair\ndetails\n\n");
        var revision = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file.Path)));
        var commands = result.Output.Split('\n').Where(line => line.StartsWith("ERPDERT repair", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(commands);
        Assert.All(commands, command => Assert.Contains("--from-lint " + revision + " --plain", command));
    }

    [Theory]
    [InlineData(false, "DERT4102", "previous bytes were restored and verified")]
    [InlineData(true, "DERT4103", "file needs recovery")]
    public void Write_failure_keeps_recovery_evidence_without_claiming_the_display_is_current(bool always, string code, string explanation)
    {
        const string source = "// TITLE\n// --";
        using var file = new ScratchDocument(source);
        var result = RunWithWriter(file.Path, new FailingWriter(always));
        Assert.Equal(1, result.Exit);
        var stopped = result.Output[result.Output.IndexOf("Recorded source / current file not verified", StringComparison.Ordinal)..];
        Assert.Contains(code, stopped);
        Assert.Contains(explanation, stopped);
        Assert.Contains("Saved original /", stopped);
        Assert.DoesNotContain("no file edits", stopped);
        Assert.DoesNotContain("Saved / bytes verified", stopped);
        Assert.Equal(!always, File.ReadAllText(file.Path) == source);
        Assert.Equal(source, File.ReadAllText(Assert.Single(Directory.EnumerateFiles(file.DirectoryPath, "before.DERT", SearchOption.AllDirectories))));
    }

    private static (int Exit, string Output) RunWithWriter(string path, IRepairWriter fault)
    {
        // Use the actual review adapter and service; inject only a failed disk
        // writer to exercise a failure that cannot reliably be scheduled on disk.
        var assembly = typeof(CliApplication).Assembly;
        var flags = BindingFlags.NonPublic | BindingFlags.Static;
        var adapter = assembly.GetType("Erpdert.Cli.Motion.NoteScore")!;
        var bytes = File.ReadAllBytes(path);
        var analysis = adapter.GetMethod("Analyze", flags)!.Invoke(null,
            [new Func<string>(() => Encoding.UTF8.GetString(bytes)), path, false, null, CancellationToken.None, DocumentSourceState.Original])!;
        var score = (MotionScore)adapter.GetProperty("Score")!.GetValue(analysis)!;
        var events = (IReadOnlyList<MotionEvent>)adapter.GetProperty("Events")!.GetValue(analysis)!;
        var journal = new MotionJournal(score with { View = MotionView.Document }, DateTimeOffset.Now);
        foreach (var change in events) journal.Apply(change, TimeSpan.Zero);
        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings { Ansi = AnsiSupport.No, Interactive = InteractionSupport.No, Out = new AnsiConsoleOutput(writer) });
        console.Profile.Width = 132;
        console.Profile.Height = 32;
        var renderer = Activator.CreateInstance(assembly.GetType("Erpdert.Cli.DocumentRenderer")!, console, true, null, true);
        var reviewType = assembly.GetType("Erpdert.Cli.Motion.MerReview")!;
        var review = Activator.CreateInstance(reviewType, analysis, journal.Snapshot(TimeSpan.Zero), bytes, path, console,
            renderer, new StringReader("repair title\napply\ndetails\n\n"), true, null, new RepairFileService(fault), null);
        var exit = (int)reviewType.GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(review, [CancellationToken.None])!;
        return (exit, writer.ToString());
    }

    private sealed class FailingWriter(bool always) : IRepairWriter
    {
        private int writes;
        public void Rewrite(FileStream stream, byte[] bytes)
        {
            if (++writes == 1 || always)
            {
                stream.Position = 0; stream.WriteByte(0); stream.Flush(true);
                throw new IOException("Injected partial write failure");
            }
            new FileStreamRepairWriter().Rewrite(stream, bytes);
        }
    }

    private static (int Exit, string Output) Run(string path, string commands, bool replay = false) => Run(path, new StringReader(commands), replay);
    private static (int Exit, string Output) Run(string path, TextReader commands, bool replay = false)
    {
        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings { Ansi = AnsiSupport.No, Interactive = InteractionSupport.No, Out = new AnsiConsoleOutput(writer) });
        console.Profile.Width = 132;
        console.Profile.Height = 40;
        string[] args = replay ? ["mer", path, "--plain", "--review", "--replay"] : ["mer", path, "--plain", "--review"];
        return (CliApplication.Run(args, console, merInput: commands, discoveryEnvironment: new DiscoveryEnvironment("", ".CMD")), writer.ToString());
    }
    private sealed class ChangingInput(string text, int at, Action change) : StringReader(text)
    {
        private int reads;
        public override string? ReadLine() { if (++reads == at) change(); return base.ReadLine(); }
    }
}
