using Dert.Cli.Authoring;
using Dert.Cli.Motion;
using Dert.Core;
using Dert.Mer;
using Xunit;

namespace Dert.Tests;

public class MerContextTests
{
    private const string Source = "// TITLE\n// --\n// HOW ; codex ;; Keep this install. //\n// WHY ; codex ;; This note disagrees. //\nSTERT ; update ;; codex\nƒin.";

    [Fact]
    public void MER_keeps_other_notes_out_until_explicitly_included_and_retains_a_review_handoff()
    {
        using var file = new ScratchDocument(Source); var bytes = File.ReadAllBytes(file.Path);
        var result = Run(file.Path, "at 3\ncontext\ncontext left\ncontext include 4\ncontext include 5\ncontext details\ncontext exclude 3\ncontext exclude 4\ncontext details\n\n");
        Assert.Equal(0, result.Exit); Assert.Contains("SELECTED CONTEXT", result.Output);
        Assert.Contains("Included notes: 2", result.Output); Assert.Contains("Included notes: 1", result.Output);
        Assert.Contains("This note disagrees.", result.Output);
        Assert.Contains("starting note", result.Output); Assert.Contains("not a reasoning note opener", result.Output);
        Assert.Contains("--include 4", result.Output); Assert.Contains("--from-preview", result.Output);
        Assert.Equal(bytes, File.ReadAllBytes(file.Path));
    }

    [Fact]
    public void Saved_repairs_clear_the_context_set_and_pending_repair_keeps_its_choices()
    {
        using var file = new ScratchDocument(Source);
        var result = Run(file.Path, "at 3\ncontext\nrepair title\ncontext include 4\napply\ncontext details\n\n");
        Assert.Contains("Finish this comparison", result.Output);
        Assert.Contains("Context selection cleared after the source changed", result.Output);
        Assert.Contains("Use context on a reasoning note first", result.Output);
    }

    [Fact]
    public void Canceling_repair_does_not_restore_a_context_annotation_on_the_wrong_line()
    {
        using var file = new ScratchDocument(Source);
        var result = Run(file.Path, "at 3\ncontext\nrepair title\ncancel\ndetails\n\n");
        var canceled = result.Output.Split("Preview left without applying.", StringSplitOptions.None)[1];
        Assert.DoesNotContain("SELECTED CONTEXT", canceled); Assert.Contains("Title rule / line 2", canceled);
    }

    [Fact]
    public void MER_note_choices_are_paged_and_large_sets_remain_explicit()
    {
        using var file = new ScratchDocument(string.Join('\n', Enumerable.Range(1, 12).Select(i => $"// note {i} ; music ;; words //")));
        var result = Run(file.Path, "at 1\ncontext\ncontext notes 2\ncontext include 12\ncontext details\n\n", 58);
        Assert.Contains("Notes 7-12 of 12", result.Output); Assert.Contains("12 /", result.Output);
        Assert.Contains("Reasoning note at line 12", result.Output); Assert.Contains("Included notes: 2", result.Output);
    }

    [Theory]
    [InlineData(58)]
    [InlineData(110)]
    public void Context_frames_keep_missing_boundary_and_omissions_visible(int width)
    {
        const string source = "// HOW ; codex ;; keep //\n// support ; conflicts ;;\n// missing, // Keep the caution.";
        var analysis = NoteScore.Analyze(() => source, "selection.DERT", true);
        var packet = ContextSelection.Capture(analysis.Document!, 2, "selection.DERT", ContextSourceKind.FileSnapshot).Build();
        var journal = new MotionJournal(analysis.Score, DateTimeOffset.UnixEpoch);
        foreach (var observed in analysis.Events) journal.Apply(observed, TimeSpan.Zero);
        var frame = MerCompositor.Document(journal.Snapshot(TimeSpan.Zero), width, 30, 2, ContextPresentation.Annotation(packet));
        var text = string.Join('\n', frame.Lines.Select(line => line.Text));
        Assert.Contains("Closing // missing", text); Assert.Contains("Left out", text); Assert.Contains("SELECTED CONTEXT", text);
        Assert.Contains("Selected / lines 2", text);
        Assert.Contains("source unchanged", frame.Lines[^1].Text);
        Assert.All(frame.Lines, line => Assert.True(TerminalText.Width(line.Text) < width));
    }

    private static (int Exit, string Output) Run(string path, string input, int width = 110) => ContextCommandTests.Run(["mer", path, "--review", "--plain"], input, width);
}
