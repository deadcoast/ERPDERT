using Dert.Mer;
using Xunit;

namespace Dert.Tests;

public class MerSourceFocusTests
{
    [Theory]
    [InlineData(110, 28)]
    [InlineData(80, 24)]
    [InlineData(80, 18)]
    public void Inspecting_an_entry_keeps_its_neighbors_and_emphasizes_only_its_own_connection(int columns, int rows)
    {
        var source = new MotionSource("plans.DERT", [InkLine.Plain("  4  // plans ; friends ;;"),
            InkLine.Plain("  5  // music, // Bring the playlist."), InkLine.Plain("  6  // food, // A meal together."),
            InkLine.Plain("  7  // home // Last train at 23:40."), InkLine.Plain("  8  //")], [4, 5, 6, 7, 8]);
        var journal = new MotionJournal(new("Our plans", "No writes", [new("context", "Context", "Read")], source), DateTimeOffset.UnixEpoch);
        journal.Apply(new StartStage("context"), TimeSpan.Zero);
        foreach (var (line, value, annotation) in new[] { (5, "music", "Bring the playlist."), (6, "food", "A meal together."), (7, "home", "Last train at 23:40.") })
            journal.Apply(new AddEvidence("context", new("Entry", annotation, EvidenceKind.Source, line, 4,
                new NoteConnectionFocus("plans", value, annotation, 8))), TimeSpan.Zero);
        journal.Apply(new SettleStage("context", StageState.Passed, "Read."), TimeSpan.Zero);
        journal.Apply(new FinishMotion(MotionOutcome.Complete, "Done."), TimeSpan.Zero);
        var frame = Assert.IsType<MerFrame>(MerCompositor.Inspect(journal.Snapshot(TimeSpan.Zero), 6, columns, rows));
        Assert.Contains("music", Text(frame));
        Assert.Contains("home", Text(frame));
        Assert.Contains("A meal together.", Text(frame));
        Assert.Contains("Closing // at line 8", Text(frame));
        Assert.Contains(frame.Lines.SelectMany(line => line.Runs), run => run.Text.Contains("food", StringComparison.Ordinal) && run.Bold && run.Ink == Ink.Signal);
        Assert.DoesNotContain(frame.Lines.SelectMany(line => line.Runs), run => run.Text.Contains("music", StringComparison.Ordinal) && run.Bold);
        Assert.DoesNotContain("Bring the playlist.", Text(frame));
        if (columns >= 100)
        {
            var connection = Assert.Single(frame.Lines, line => line.Text.Contains('─') && line.Text.Contains("food", StringComparison.Ordinal)).Text;
            Assert.Equal(2, connection.IndexOf('─') - (connection.IndexOf("food", StringComparison.Ordinal) + 4));
        }
    }

    [Fact]
    public void Routine_checks_continue_beneath_the_observed_note_instead_of_replacing_it()
    {
        var journal = new MotionJournal(new("Notes", "No writes", [new("context", "Context", "Read"), new("lint", "Check boundaries", "Checking")],
            new("note.DERT", [InkLine.Plain("  1  // plans ; weekend ;;"), InkLine.Plain("  2  // home // Last train at 23:40."), InkLine.Plain("  3  //")], [1, 2, 3])), DateTimeOffset.UnixEpoch);
        journal.Apply(new StartStage("context"), TimeSpan.Zero);
        journal.Apply(new AddEvidence("context", new("Owner", "Last train at 23:40.", EvidenceKind.Source, 2, 1,
            new NoteConnectionFocus("plans", "home", "Last train at 23:40.", 3))), TimeSpan.Zero);
        var before = MerCompositor.Live(journal.Snapshot(TimeSpan.FromSeconds(1)), 110, 28, true);
        journal.Apply(new SettleStage("context", StageState.Passed, "Read."), TimeSpan.FromSeconds(1));
        journal.Apply(new StartStage("lint"), TimeSpan.FromSeconds(1));
        var during = MerCompositor.Live(journal.Snapshot(TimeSpan.FromSeconds(2)), 110, 28, true);
        Assert.Contains("Belongs to plans", Text(during));
        Assert.Contains("Check boundaries", Text(during));
        Assert.Equal(before.Lines.ToList().FindIndex(line => line.Text.Contains("Last train", StringComparison.Ordinal)),
            during.Lines.ToList().FindIndex(line => line.Text.Contains("Last train", StringComparison.Ordinal)));
        journal.Apply(new AddEvidence("lint", new("DERT1001", "This boundary needs your choice.", EvidenceKind.Error, 1)), TimeSpan.FromSeconds(2));
        Assert.Contains("This boundary needs your choice.", Text(MerCompositor.Live(journal.Snapshot(TimeSpan.FromSeconds(2)), 110, 28, true)));
        Assert.DoesNotContain("Belongs to plans", Text(MerCompositor.Live(journal.Snapshot(TimeSpan.FromSeconds(2)), 110, 28, true)));
    }

    [Theory]
    [InlineData(StageState.Failed, "failed")]
    [InlineData(StageState.Interrupted, "interrupted")]
    public void A_failed_stage_is_visible_before_the_overall_completion_event(StageState state, string expected)
    {
        var journal = Title();
        journal.Apply(new SettleStage("repair", state, "The check did not finish."), TimeSpan.Zero);
        var text = Text(MerCompositor.Live(journal.Snapshot(TimeSpan.Zero), 80, 24, true));
        Assert.Contains("overall " + expected, text);
        Assert.Contains("The check did not finish.", text);
        Assert.DoesNotContain("Measured against your title", text);
        Assert.Null(MerCompositor.Inspect(journal.Snapshot(TimeSpan.Zero), 2, 80, 24));
    }

    [Fact]
    public void An_unrelated_working_stage_cannot_cover_a_failed_check()
    {
        var journal = new MotionJournal(new("Checks", "Observed", [new("read", "Read", "Reading"), new("verify", "Verify", "Verifying")]), DateTimeOffset.UnixEpoch);
        journal.Apply(new StartStage("read"), TimeSpan.Zero);
        journal.Apply(new StartStage("verify"), TimeSpan.Zero);
        journal.Apply(new SettleStage("read", StageState.Failed, "The source could not be read."), TimeSpan.Zero);
        Assert.Contains("The source could not be read.", Text(MerCompositor.Live(journal.Snapshot(TimeSpan.Zero), 110, 28, true)));
    }

    [Fact]
    public void A_title_extension_distinguishes_retained_marks_from_new_marks()
    {
        var frame = MerCompositor.Live(Title().Snapshot(TimeSpan.Zero), 80, 24, true);
        var after = Assert.Single(frame.Lines, line => line.Text.Contains("+ // -----", StringComparison.Ordinal));
        Assert.Contains(after.Runs, run => run.Text == "--" && run.Ink == Ink.Signal);
        Assert.Contains(after.Runs, run => run.Text.Contains("// ---", StringComparison.Ordinal) && run.Ink != Ink.Signal);
    }

    [Theory]
    [InlineData(80)]
    [InlineData(110)]
    public void A_missing_note_boundary_takes_priority_over_a_cosmetic_title_preview(int columns)
    {
        var journal = new MotionJournal(new("Notes", "No writes", [new("lint", "Boundaries", "Check"), new("repair", "Title", "Preview")],
            new("note.DERT", [InkLine.Plain("  1  // TITLE"), InkLine.Plain("  2  // ---"), InkLine.Plain("  4  // plans ; next ;;")], [1, 2, 4])), DateTimeOffset.UnixEpoch);
        journal.Apply(new StartStage("lint"), TimeSpan.Zero);
        // Error kind is supplied by the parser adapter, not inferred from prose.
        journal.Apply(new AddEvidence("lint", new("DERT1001", "This note still needs its closing //.", EvidenceKind.Error, 4)), TimeSpan.Zero);
        journal.Apply(new SettleStage("lint", StageState.Attention, "1 finding."), TimeSpan.Zero);
        journal.Apply(new StartStage("repair"), TimeSpan.Zero);
        journal.Apply(new AddEvidence("repair", new("Proposed", "// -----", EvidenceKind.Added, 2, 1,
            new TitleRuleFocus("// TITLE", "// ---", "// -----", 5, 3, "The line is 2 marks short."))), TimeSpan.Zero);
        journal.Apply(new SettleStage("repair", StageState.Passed, "Preview ready."), TimeSpan.Zero);
        journal.Apply(new FinishMotion(MotionOutcome.Attention, "Source unchanged."), TimeSpan.Zero);
        var frame = MerCompositor.Hold(journal.Snapshot(TimeSpan.Zero), columns, 24);
        var text = Text(frame);
        Assert.Contains("This note still needs its closing //.", text);
        Assert.DoesNotContain("Measured against your title", text);
        Assert.Contains(frame.Lines.SelectMany(line => line.Runs), run =>
            run.Text.Contains("Boundaries", StringComparison.Ordinal) && run.Bold);
    }

    [Fact]
    public void The_title_and_proposed_underline_share_the_same_visible_end_column()
    {
        var frame = MerCompositor.Live(Title().Snapshot(TimeSpan.FromSeconds(1)), 80, 24, true);
        var title = Assert.Single(frame.Lines, line => line.Text.Contains("// TITLE", StringComparison.Ordinal)).Text;
        var after = Assert.Single(frame.Lines, line => line.Text.Contains("+ // -----", StringComparison.Ordinal)).Text;
        Assert.Equal(title.IndexOf("TITLE", StringComparison.Ordinal) + 5, after.LastIndexOf('-') + 1);
    }

    [Fact]
    public void A_short_title_view_keeps_both_the_reason_and_the_no_write_status()
    {
        var frame = MerCompositor.Live(Title().Snapshot(TimeSpan.Zero), 80, 18, true);
        var text = Text(frame);
        Assert.Contains("2 marks short", text);
        Assert.Contains("Preview only / not applied", text);
    }

    [Fact]
    public void A_short_unfinished_note_keeps_the_annotation_and_unresolved_boundary_separate()
    {
        var frame = MerCompositor.Live(Note().Snapshot(TimeSpan.Zero), 80, 18, true);
        var text = Text(frame);
        Assert.Contains(frame.Lines, line => line.Text.Trim() == "No installation was changed.");
        Assert.Contains("Closing // missing", text);
        Assert.Contains("ending is unresolved", text);
        Assert.DoesNotContain("Closing // at line", text);
    }

    [Theory]
    [InlineData(MotionOutcome.Failed)]
    [InlineData(MotionOutcome.Interrupted)]
    public void A_failed_final_result_cannot_be_hidden_by_a_previously_successful_focus(MotionOutcome outcome)
    {
        var journal = Title();
        journal.Apply(new SettleStage("repair", StageState.Passed, "Preview checked."), TimeSpan.Zero);
        journal.Apply(new FinishMotion(outcome, "Final verification did not finish."), TimeSpan.Zero);
        var text = Text(MerCompositor.Hold(journal.Snapshot(TimeSpan.Zero), 80, 24));
        Assert.Contains("Final verification did not finish.", text);
        Assert.DoesNotContain("Measured against your title", text);
    }

    [Fact]
    public void The_trace_settles_without_repeating_or_changing_the_evidence()
    {
        var journal = Note();
        var early = Text(MerCompositor.Live(journal.Snapshot(TimeSpan.Zero), 110, 28));
        var travelling = Text(MerCompositor.Live(journal.Snapshot(TimeSpan.FromSeconds(.15)), 110, 28));
        var settled = Text(MerCompositor.Live(journal.Snapshot(TimeSpan.FromSeconds(1)), 110, 28));
        var later = Text(MerCompositor.Live(journal.Snapshot(TimeSpan.FromSeconds(5)), 110, 28));
        Assert.NotEqual(early, travelling);
        Assert.Equal(settled, later);
        Assert.Contains("No installation was changed.", early);
        Assert.Contains("No installation was changed.", settled);
    }

    [Fact]
    public void Unrelated_evidence_does_not_reanimate_a_retained_connection()
    {
        var journal = Note();
        var settled = Text(MerCompositor.Live(journal.Snapshot(TimeSpan.FromSeconds(1)), 110, 28));
        journal.Apply(new AddEvidence("context", new("A separate note", "Unrelated inline context.")), TimeSpan.FromSeconds(1));
        Assert.Equal(settled, Text(MerCompositor.Live(journal.Snapshot(TimeSpan.FromSeconds(1)), 110, 28)));
        Assert.Equal(settled, Text(MerCompositor.Live(journal.Snapshot(TimeSpan.FromSeconds(1.15)), 110, 28)));
    }

    [Theory]
    [InlineData(46, 18)]
    [InlineData(25, 10)]
    public void Compact_direct_inspection_retains_the_selected_explanation(int columns, int rows)
    {
        var frame = Assert.IsType<MerFrame>(MerCompositor.Inspect(Note().Snapshot(TimeSpan.Zero), 2, columns, rows));
        Assert.Contains("No installation", Text(frame));
        Assert.InRange(frame.Lines.Count, 1, rows - 2);
        Assert.All(frame.Lines, line => Assert.InRange(TerminalText.Width(line.Text), 0, columns - 1));
    }

    [Theory]
    [InlineData(96)]
    [InlineData(97)]
    [InlineData(110)]
    public void Long_unicode_entries_keep_their_annotation_and_terminal_bounds(int columns)
    {
        var value = string.Concat(Enumerable.Repeat("日本語👩‍💻", 20));
        var journal = new MotionJournal(new("Places", "No writes", [new("context", "Context", "Reading")],
            new("places.DERT", [InkLine.Plain("  1  // places ; next ;;"), InkLine.Plain("  2  // " + value)], [1, 2])), DateTimeOffset.UnixEpoch);
        journal.Apply(new StartStage("context"), TimeSpan.Zero);
        journal.Apply(new AddEvidence("context", new("Entry", "Keep this explanation.", EvidenceKind.Source, 2, 1,
            new NoteConnectionFocus("places", value, "Keep this explanation.", null))), TimeSpan.Zero);
        var frame = Assert.IsType<MerFrame>(MerCompositor.Inspect(journal.Snapshot(TimeSpan.Zero), 2, columns, 24));
        Assert.Contains("Keep this explanation.", Text(frame));
        Assert.Contains("Closing // missing", Text(frame));
        Assert.All(frame.Lines, line => Assert.InRange(TerminalText.Width(line.Text), 0, columns - 1));
        Assert.DoesNotContain("�", Text(frame));
    }

    [Theory]
    [InlineData(110, 28)]
    [InlineData(80, 24)]
    [InlineData(80, 18)]
    [InlineData(46, 18)]
    [InlineData(25, 10)]
    public void Focus_frames_stay_inside_their_terminal_region(int columns, int rows)
    {
        foreach (var journal in new[] { Title(), Note() })
        {
            var frame = MerCompositor.Live(journal.Snapshot(TimeSpan.Zero), columns, rows);
            Assert.InRange(frame.Lines.Count, 1, rows - 2);
            Assert.All(frame.Lines, line => Assert.InRange(TerminalText.Width(line.Text), 0, columns - 1));
        }
    }

    private static MotionJournal Title()
    {
        var journal = new MotionJournal(new("A title", "No writes", [new("repair", "Title", "Preview")],
            new("note.DERT", [InkLine.Plain("  1  // TITLE"), InkLine.Plain("  2  // ---")], [1, 2])), DateTimeOffset.UnixEpoch);
        journal.Apply(new StartStage("repair"), TimeSpan.Zero);
        journal.Apply(new AddEvidence("repair", new("Proposed", "// -----", EvidenceKind.Added, 2, 1,
            new TitleRuleFocus("// TITLE", "// ---", "// -----", 5, 3, "The line is 2 marks short."))), TimeSpan.Zero);
        return journal;
    }

    private static MotionJournal Note()
    {
        var journal = new MotionJournal(new("A note", "No writes", [new("context", "Context", "Read")],
            new("note.DERT", [InkLine.Plain("  1  // plans ; weekend ;;"), InkLine.Plain("  2  // status // No installation was changed.")], [1, 2])), DateTimeOffset.UnixEpoch);
        journal.Apply(new StartStage("context"), TimeSpan.Zero);
        journal.Apply(new AddEvidence("context", new("Owner", "No installation was changed.", EvidenceKind.Source, 2, 1,
            new NoteConnectionFocus("plans", "status", "No installation was changed.", null))), TimeSpan.Zero);
        return journal;
    }

    private static string Text(MerFrame frame) => string.Join('\n', frame.Lines.Select(line => line.Text));
}
