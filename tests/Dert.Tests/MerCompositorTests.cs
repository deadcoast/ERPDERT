using Erpdert.Mer;
using Xunit;

namespace Dert.Tests;

public class MerCompositorTests
{
    [Fact]
    public void A_prioritized_finding_is_shown_once_without_claiming_its_duplicate_is_missing_detail()
    {
        var journal = new MotionJournal(new("Notes", "No writes", [new("lint", "Check boundaries", "Check")]), DateTimeOffset.UnixEpoch);
        journal.Apply(new StartStage("lint"), TimeSpan.Zero);
        journal.Apply(new AddEvidence("lint", new("DERT1001 / line 4", "The ending is still unresolved.", EvidenceKind.Error)), TimeSpan.Zero);
        journal.Apply(new SettleStage("lint", StageState.Attention, "One finding."), TimeSpan.Zero);
        var output = Flatten(MerCompositor.Live(journal.Snapshot(TimeSpan.Zero), 80, 24, true));
        Assert.Equal(1, output.Split("The ending is still unresolved.", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("more in the full receipt", output);
    }

    [Fact]
    public void A_single_evidence_row_preserves_the_opening_negation_not_a_misleading_suffix()
    {
        var journal = new MotionJournal(new("Notes", "Read-only", [
            new("read", "Read", "Read"), new("context", "Context", "Context"),
            new("lint", "Lint", "Lint"), new("repair", "Repair", "Repair")],
            new("notes.DERT", [InkLine.Plain("source")], [1])), DateTimeOffset.UnixEpoch);
        journal.Apply(new StartStage("context"), TimeSpan.Zero);
        journal.Apply(new AddEvidence("context", new("Observed",
            "No check has established that the installed program will open successfully or that any proposed repair was applied.")), TimeSpan.Zero);
        var output = Flatten(MerCompositor.Live(journal.Snapshot(TimeSpan.Zero), 80, 14));
        Assert.Contains("No check has established", output);
        Assert.Contains("…", output);
        Assert.DoesNotContain("successfully or that any proposed repair was applied.", output);
    }

    [Theory]
    [InlineData(24)]
    [InlineData(20)]
    public void A_crowded_view_keeps_newest_evidence_and_discloses_the_excerpt(int rows)
    {
        var journal = new MotionJournal(new("Notes", "Read-only", [
            new("read", "Read", "Read"), new("context", "Context", "Context"),
            new("lint", "Lint", "Lint"), new("repair", "Repair", "Repair")],
            new("notes.DERT", [InkLine.Plain("old entry"), InkLine.Plain("latest entry")], [2, 3])), DateTimeOffset.UnixEpoch);
        journal.Apply(new StartStage("context"), TimeSpan.Zero);
        journal.Apply(new AddEvidence("context", new("Earlier", new string('x', 180), SourceLine: 2)), TimeSpan.Zero);
        journal.Apply(new AddEvidence("context", new("Newest", "Keep this evidence", SourceLine: 3)), TimeSpan.Zero);
        var output = Flatten(MerCompositor.Live(journal.Snapshot(TimeSpan.Zero), 80, rows));
        Assert.Contains("Keep this evidence", output);
        Assert.Contains("receipt", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void English_wraps_at_words_without_chopping_the_next_word_in_half()
    {
        var journal = new MotionJournal(new("Words", "Read-only", [new("one", "Read", "Check this package before changing anything.")]), DateTimeOffset.UnixEpoch);
        journal.Apply(new StartStage("one"), TimeSpan.Zero);
        var frame = MerCompositor.Live(journal.Snapshot(TimeSpan.Zero), 24, 12);
        Assert.Contains(frame.Lines, line => line.Text == "Check this package");
        Assert.Contains(frame.Lines, line => line.Text == "before changing");
        Assert.Contains(frame.Lines, line => line.Text == "anything.");
    }

    [Fact]
    public void The_receipt_does_not_repeat_evidence_used_as_the_settlement_explanation()
    {
        var journal = Journal();
        const string detail = "The observed package and version agree.";
        journal.Apply(new AddEvidence("shape", new("Observed", detail)), TimeSpan.FromSeconds(2));
        journal.Apply(new SettleStage("shape", StageState.Passed, detail), TimeSpan.FromSeconds(2));
        var receipt = MerCompositor.Receipt(journal.Snapshot(TimeSpan.FromSeconds(2)));
        Assert.Single(receipt, line => line.Text.Contains(detail, StringComparison.Ordinal));
        Assert.Contains(receipt, line => line.Text.Contains("Observed", StringComparison.Ordinal));
        Assert.Contains(receipt, line => line.Text.Contains("Trace the context", StringComparison.Ordinal));
    }

    [Fact]
    public void Focused_evidence_reveals_its_actual_source_owner_in_the_live_excerpt()
    {
        var journal = new MotionJournal(new("Notes", "Read-only", [new("context", "Trace the context", "Reading")],
            new("notes.DERT", [InkLine.Plain("// TITLE"), InkLine.Plain("// -----"), InkLine.Plain("STERT"),
                InkLine.Plain("// music ; context ;;"), InkLine.Plain("// jazz // keep this"), InkLine.Plain("//")], [1, 2, 4, 9, 10, 11])), DateTimeOffset.UnixEpoch);
        journal.Apply(new StartStage("context"), TimeSpan.Zero);
        journal.Apply(new AddEvidence("context", new("belongs to line 9", "jazz", EvidenceKind.Source, 10, 9)), TimeSpan.Zero);
        var frame = MerCompositor.Live(journal.Snapshot(TimeSpan.FromSeconds(1)), 80, 24);
        Assert.Contains("// music ; context ;;", Flatten(frame));
        Assert.Contains("// jazz // keep this", Flatten(frame));
        Assert.DoesNotContain("// TITLE", Flatten(frame));
    }
    [Fact]
    public void A_failure_explanation_takes_priority_over_existing_evidence()
    {
        var journal = Journal();
        journal.Apply(new SettleStage("shape", StageState.Failed, "The file changed while reading."), TimeSpan.FromSeconds(2));
        var frame = MerCompositor.Live(journal.Snapshot(TimeSpan.FromSeconds(2)), 80, 24);
        Assert.Contains("The file changed while reading.", Flatten(frame));
    }
    private static MotionJournal Journal()
    {
        var journal = new MotionJournal(new("Look inside these notes", "Read-only / no file edits", [
            new("read", "Read the source", "Reading UTF-8"),
            new("shape", "Trace the context", "Finding who owns each entry"),
            new("lint", "Check the boundaries", "Checking supported rules")],
            new("weekend.DERT", [InkLine.Plain("// MY WEEKEND"), InkLine.Plain("// music ; mood ;; 日本語 👩‍💻 //")])), DateTimeOffset.UnixEpoch);
        journal.Apply(new StartStage("read"), TimeSpan.Zero);
        journal.Apply(new SettleStage("read", StageState.Passed, "Read 42 bytes."), TimeSpan.FromSeconds(1));
        journal.Apply(new StartStage("shape"), TimeSpan.FromSeconds(1));
        journal.Apply(new AddEvidence("shape", new("Entry 5 belongs to line 4", "// music ; mood ;; 日本語 👩‍💻 //", EvidenceKind.Source)), TimeSpan.FromSeconds(1));
        return journal;
    }

    [Theory]
    [InlineData(120, 30)]
    [InlineData(80, 24)]
    [InlineData(45, 15)]
    [InlineData(20, 8)]
    public void Live_layout_fits_its_region_without_splitting_wide_characters(int columns, int rows)
    {
        var frame = MerCompositor.Live(Journal().Snapshot(TimeSpan.FromSeconds(2)), columns, rows);
        Assert.InRange(frame.Lines.Count, 1, rows - 2);
        Assert.All(frame.Lines, line => Assert.InRange(TerminalText.Width(line.Text), 0, columns - 1));
        Assert.Contains("Trace", string.Join('\n', frame.Lines.Select(line => line.Text)));
    }

    [Fact]
    public void New_evidence_traces_once_without_scrambling_source_or_meaning()
    {
        var journal = Journal();
        var early = MerCompositor.Live(journal.Snapshot(TimeSpan.FromSeconds(1)), 110, 28);
        var later = MerCompositor.Live(journal.Snapshot(TimeSpan.FromSeconds(1.15)), 110, 28);
        Assert.NotEqual(Flatten(early), Flatten(later));
        Assert.Contains("// MY WEEKEND", Flatten(early));
        Assert.Contains("// MY WEEKEND", Flatten(later));
        Assert.Contains("Entry 5 belongs to line 4", Flatten(early));
        Assert.Contains("Entry 5 belongs to line 4", Flatten(later));
    }

    [Fact]
    public void Reduced_motion_has_no_time_driven_frame_changes()
    {
        var journal = Journal();
        Assert.Equal(Flatten(MerCompositor.Live(journal.Snapshot(TimeSpan.FromSeconds(2)), 110, 28, true)),
            Flatten(MerCompositor.Live(journal.Snapshot(TimeSpan.FromSeconds(20)), 110, 28, true)));
    }

    [Fact]
    public void Final_receipt_retains_all_evidence_and_the_unfinished_state()
    {
        var journal = Journal();
        for (var i = 0; i < 20; i++)
            journal.Apply(new AddEvidence("shape", new("Evidence " + i, "full value " + i)), TimeSpan.FromSeconds(2));
        journal.Apply(new FinishMotion(MotionOutcome.Interrupted, "Stopped. Source unchanged."), TimeSpan.FromSeconds(3));
        var receipt = string.Join('\n', MerCompositor.Receipt(journal.Snapshot(TimeSpan.FromSeconds(3))).Select(line => line.Text));
        Assert.Contains("Evidence 0", receipt);
        Assert.Contains("full value 19", receipt);
        Assert.Contains("interrupted", receipt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not run", receipt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("100%", receipt);
    }

    private static string Flatten(MerFrame frame) => string.Join('\n', frame.Lines.Select(line => line.Text));
}
