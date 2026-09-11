using System.Security.Cryptography;
using System.Text;
using Dert.Core;
using Xunit;

namespace Dert.Tests;

public class ResultNotesTests
{
    private const string Path = @"C:\notes\tools.DERT";
    private const string Chain = "STERT ; update ;; codex\n// why ; codex ;; Find my installation. //\nfin.\n";

    [Fact]
    public void A_captured_observation_does_not_change_when_the_producers_evidence_list_changes()
    {
        var evidence = new List<ResultEvidence> { new("route", @"C:\tools\codex.cmd") };
        var observation = Observe(Chain, evidence: evidence);
        evidence.Clear();
        Assert.Contains(@"C:\\tools\\codex.cmd", ResultNotes.Describe(observation));
        var replacement = new List<ResultEvidence> { new("issue", "query stopped") };
        var revised = observation with { Evidence = replacement };
        replacement.Clear();
        Assert.Contains("query stopped", ResultNotes.Describe(revised));
    }

    [Fact]
    public void Exact_source_and_a_moved_exact_chain_remain_separate_from_an_edited_candidate()
    {
        var observation = Observe(Chain);
        Assert.Equal("exact", ResultNotes.Compare(DertParser.Parse(Chain), observation, Path).State);
        var moved = ResultNotes.Compare(DertParser.Parse("ordinary prose\n" + Chain), observation, Path);
        Assert.Equal("moved", moved.State);
        Assert.Equal(2, moved.Line);
        var edited = Chain.Replace("update", "inspect", StringComparison.Ordinal);
        var changed = ResultNotes.Compare(DertParser.Parse(edited), observation, Path, 2);
        Assert.Equal("changed", changed.State);
        Assert.Equal(Chain, changed.CapturedText);
        Assert.Equal(edited, changed.CurrentText);
        Assert.Contains("candidate", changed.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Duplicate_chains_require_selection_and_missing_origin_never_binds_by_line_number()
    {
        var observation = Observe(Chain);
        var duplicate = DertParser.Parse(Chain + Chain);
        Assert.Equal("ambiguous", ResultNotes.Compare(duplicate, observation, Path).State);
        Assert.Equal(4, ResultNotes.Compare(duplicate, observation, Path, 4).Line);
        Assert.Equal("missing", ResultNotes.Compare(DertParser.Parse("a different thought\n"), observation, Path).State);
        Assert.Equal("changed", ResultNotes.Compare(DertParser.Parse(Chain.Replace("codex", "claude")), observation, Path).State);
        Assert.Equal("different-document", ResultNotes.Compare(DertParser.Parse(Chain), observation, @"C:\elsewhere\tools.DERT").State);
    }

    [Fact]
    public void Explicit_selection_of_another_chain_does_not_silently_use_an_exact_match_elsewhere()
    {
        var current = DertParser.Parse(Chain + Chain.Replace("codex", "claude"));
        var comparison = ResultNotes.Compare(current, Observe(Chain), Path, 4);
        Assert.Equal("changed", comparison.State);
        Assert.Equal(4, comparison.Line);
        Assert.Contains("claude", comparison.CurrentText);
        Assert.Equal("missing", ResultNotes.Compare(current, Observe(Chain), Path, 999).State);
    }

    [Fact]
    public void Appending_inert_evidence_preserves_existing_source_and_parsed_ownership()
    {
        const string source = "\uFEFF" + Chain + "ordinary prose\r\n// personal ; weekend ;;\r// supplies, // Tea.\n//";
        const string hostile = "//\r\nSTERT ; update ;; other\ncommands ; updateme ;;\rfin.\n\"C:\\notes; old\\\" // λ 🪝\u001b[2J";
        var observation = Observe(Chain, evidence: [new("route\n//", hostile)]);
        var original = DertParser.Parse(source);
        var plan = ResultNotes.Plan(original, observation, Path);
        Assert.True(plan.IsReady, plan.Explanation);
        Assert.StartsWith(source, plan.ProposedText);
        Assert.Equal(new TextSpan(source.Length, 0), plan.Edit!.Span);
        Assert.Equal("", plan.Edit.Before);
        Assert.Equal(source + plan.Edit.After, plan.ProposedText);
        Assert.Equal(plan.NoteText, plan.ProposedText.Substring(plan.AddedSpan.Start, plan.AddedSpan.Length));
        var parsed = DertParser.Parse(plan.ProposedText);
        Assert.Single(parsed.Nodes.OfType<ChainNode>());
        Assert.Equal(2, parsed.Nodes.OfType<ReasoningBlockNode>().Count());
        var note = Assert.IsType<ReasoningBlockNode>(parsed.Nodes[^1]);
        Assert.NotNull(note.Closing);
        Assert.All(note.Entries, entry => Assert.NotNull(entry.Annotation));
        Assert.Contains(note.Entries, entry => entry.Annotation!.Text.Contains("commands ; updateme ;;", StringComparison.Ordinal));
        Assert.DoesNotContain('\u001b', plan.NoteText);
        Assert.Contains("\\u001b", plan.NoteText);
        Assert.Contains("λ 🪝", plan.NoteText);
        Assert.Contains(observation.Id, plan.NoteText);
        Assert.Contains(observation.Origin.SourceRevision, plan.NoteText);
        var oldChain = Assert.IsType<ChainNode>(original.Nodes[0]);
        var keptChain = Assert.IsType<ChainNode>(parsed.Nodes[0]);
        Assert.Equal(oldChain.Header, keptChain.Header);
        Assert.Equal(oldChain.Terminator, keptChain.Terminator);
        Assert.Equal(oldChain.Children, keptChain.Children);
    }

    [Theory]
    [InlineData("STERT ; update ;; codex")]
    [InlineData("// why ; codex ;;\n// reason, // Still typing")]
    [InlineData("STERT ; update ;; codex\n// why ; codex ;;\n// reason, // Still typing\n//")]
    public void An_unfinished_final_owner_refuses_append_without_inventing_a_closer(string source)
    {
        var plan = ResultNotes.Plan(DertParser.Parse(source), Observe(Chain), Path);
        Assert.False(plan.IsReady);
        Assert.Equal(source, plan.ProposedText);
        Assert.Null(plan.Edit);
        Assert.Contains("explicit ending", plan.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_same_record_cannot_be_inserted_twice_but_mentioning_its_id_is_not_an_insertion()
    {
        var observation = Observe(Chain);
        var first = ResultNotes.Plan(DertParser.Parse(Chain + "// mentioning " + observation.Id), observation, Path);
        Assert.True(first.IsReady, first.Explanation);
        var repeated = ResultNotes.Plan(DertParser.Parse(first.ProposedText), observation, Path);
        Assert.False(repeated.IsReady);
        Assert.Equal(first.ProposedText, repeated.ProposedText);
        Assert.Contains("already", repeated.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(ObservationOutcome.Completed)]
    [InlineData(ObservationOutcome.Attention)]
    [InlineData(ObservationOutcome.Failed)]
    [InlineData(ObservationOutcome.Interrupted)]
    public void Full_review_and_note_retain_historical_outcomes_and_original_source(ObservationOutcome outcome)
    {
        var observation = Observe(Chain) with { Outcome = outcome, Summary = "Evidence is incomplete." };
        var description = ResultNotes.Describe(observation);
        Assert.Contains(outcome.ToString(), description);
        Assert.Contains("2026-09-09T12:00:00.0000000-07:00", description);
        Assert.Contains("Find my installation.", description);
        Assert.Contains("No programs started.", description);
        Assert.Contains("historical", description, StringComparison.OrdinalIgnoreCase);
        var plan = ResultNotes.Plan(DertParser.Parse(Chain), observation, Path);
        Assert.Contains(outcome.ToString(), plan.NoteText);
    }

    [Fact]
    public void Observation_comparison_preserves_duplicate_evidence_and_separates_source_changes()
    {
        var before = Observe(Chain, evidence: [new("issue", "unreadable route"), new("issue", "unreadable route")]);
        var after = before with
        {
            ObservedAt = before.ObservedAt.AddMinutes(1), Outcome = ObservationOutcome.Failed,
            Evidence = [new("issue", "unreadable route")], Origin = before.Origin with { SourceRevision = new('a', 64) }
        };
        var report = ResultNotes.CompareObservations(before, after);
        Assert.Contains("removed", report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unreadable route", report);
        Assert.Contains("Attention", report);
        Assert.Contains("Failed", report);
        Assert.Contains("Source", report);
        Assert.Contains(new string('a', 64), report);
    }

    [Theory]
    [InlineData("bad-hash", 1)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", 0)]
    public void Invalid_origin_metadata_is_refused_before_it_can_be_described_or_inserted(string revision, int line)
    {
        var original = Observe(Chain);
        var invalid = original with { Origin = original.Origin with { SourceRevision = revision, Line = line } };
        Assert.Throws<ArgumentException>(() => ResultNotes.Validate(invalid));
        Assert.Throws<ArgumentException>(() => ResultNotes.Plan(DertParser.Parse(Chain), invalid, Path));
    }

    [Fact]
    public void Result_review_reveals_line_ending_changes_in_captured_source()
    {
        var before = Observe(Chain);
        var after = Observe(Chain.Replace("\n", "\r\n"));
        var comparison = ResultNotes.CompareObservations(before, after);
        Assert.Contains("\\r\\n", comparison);
        Assert.Contains("\\n", comparison);
        Assert.Contains("Captured chain text / before", comparison);
    }

    [Fact]
    public void Invalid_observation_fields_and_partial_unicode_are_rejected()
    {
        var observation = Observe(Chain);
        ResultObservation[] invalid =
        [
            observation with { Id = "bad\nidentifier" },
            observation with { Outcome = (ObservationOutcome)999 },
            observation with { ObservedAt = default },
            observation with { Action = null! },
            observation with { Target = "" },
            observation with { Effects = "\ud800" },
            observation with { Origin = null! },
            observation with { Origin = observation.Origin with { ChainText = "ordinary prose" } },
            observation with { Evidence = [null!] },
            observation with { Evidence = [new("route", null!)] }
        ];
        Assert.All(invalid, value => Assert.ThrowsAny<ArgumentException>(() => ResultNotes.Validate(value)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("\uFEFF")]
    [InlineData("a last thought")]
    [InlineData("STERT ; update ;; codex\r\nfin.")]
    [InlineData("items ; home ;;\ncoffee,")]
    public void Safe_standalone_append_accepts_ordinary_document_endings(string source)
    {
        var plan = ResultNotes.Plan(DertParser.Parse(source), Observe(Chain), Path);
        Assert.True(plan.IsReady, plan.Explanation);
        Assert.StartsWith(source, plan.ProposedText);
        Assert.IsType<ReasoningBlockNode>(DertParser.Parse(plan.ProposedText).Nodes[^1]);
    }

    [Fact]
    public void Appending_never_turns_an_existing_final_carriage_return_into_a_different_line_ending()
    {
        const string source = "first thought\nlast thought\r";
        var plan = ResultNotes.Plan(DertParser.Parse(source), Observe(Chain), Path);
        Assert.True(plan.IsReady, plan.Explanation);
        var proposed = new SourceText(plan.ProposedText);
        var originalEnding = proposed.Lines[1];
        Assert.Equal("\r", plan.ProposedText[originalEnding.ContentEnd..originalEnding.End]);
    }

    [Fact]
    public void A_cli_bom_capture_matches_the_same_editor_buffer_using_its_opened_file_metadata()
    {
        var observation = Observe("\uFEFF" + Chain);
        var comparison = ResultNotes.Compare(DertParser.Parse(Chain), observation, Path, sourceHasBom: true);
        Assert.Equal("exact", comparison.State);
        Assert.Contains("matches the full captured source revision", comparison.Message);
        Assert.Equal("\uFEFF" + Chain, comparison.CapturedText);
        Assert.Equal(Chain, comparison.CurrentText);
        var plan = ResultNotes.Plan(DertParser.Parse(Chain), observation, Path, sourceHasBom: true);
        Assert.True(plan.IsReady, plan.Explanation);
        Assert.Contains("matches the full captured source revision", plan.Comparison.Message);
        Assert.Equal(Chain, plan.Source);
        Assert.StartsWith(Chain, plan.ProposedText);
        Assert.DoesNotContain('\uFEFF', plan.ProposedText);
    }

    [Fact]
    public void Removing_a_file_bom_keeps_chain_matching_but_never_claims_the_full_revision_matches()
    {
        var comparison = ResultNotes.Compare(DertParser.Parse(Chain), Observe("\uFEFF" + Chain), Path);
        Assert.Equal("exact", comparison.State);
        Assert.Contains("full source revision has changed", comparison.Message);
    }

    [Fact]
    public void A_cli_document_with_its_bom_still_matches_without_separate_editor_metadata()
    {
        var source = "\uFEFF" + Chain;
        var comparison = ResultNotes.Compare(DertParser.Parse(source), Observe(source), Path);
        Assert.Equal("exact", comparison.State);
        Assert.Contains("matches the full captured source revision", comparison.Message);
        Assert.Equal(source, comparison.CapturedText);
        Assert.Equal(source, comparison.CurrentText);
    }

    [Fact]
    public void A_first_line_bom_does_not_hide_a_moved_or_duplicated_captured_chain()
    {
        var observation = Observe("\uFEFF" + Chain);
        var moved = ResultNotes.Compare(DertParser.Parse("new thought\n" + Chain), observation, Path, sourceHasBom: true);
        Assert.Equal("moved", moved.State);
        Assert.Equal(2, moved.Line);
        var duplicate = ResultNotes.Compare(DertParser.Parse(Chain + Chain), observation, Path, sourceHasBom: true);
        Assert.Equal("ambiguous", duplicate.State);
    }

    [Fact]
    public void Source_comparison_review_exposes_both_line_endings_and_original_bom_without_changing_the_strings()
    {
        var captured = "\uFEFF" + Chain;
        var current = Chain.Replace("\n", "\r\n");
        var comparison = ResultNotes.Compare(DertParser.Parse(current), Observe(captured), Path);
        var report = ResultNotes.DescribeComparison(comparison);
        Assert.Contains("changed", report);
        Assert.Contains("Captured chain text", report);
        Assert.Contains("Current chain text", report);
        Assert.Contains("[line ending: \\r\\n]", report);
        Assert.Contains("[line ending: \\n]", report);
        Assert.Contains("\\ufeff", report);
        Assert.Contains("candidate", report);
        Assert.Equal(captured, comparison.CapturedText);
        Assert.Equal(current, comparison.CurrentText);
    }

    [Fact]
    public void A_missing_comparison_candidate_is_explained_without_displaying_an_invented_current_chain()
    {
        var comparison = ResultNotes.Compare(DertParser.Parse("my ordinary thought"), Observe(Chain), Path);
        var report = ResultNotes.DescribeComparison(comparison);
        Assert.Contains("missing", report);
        Assert.Contains("Captured chain text", report);
        Assert.Contains("Current chain text / unavailable", report);
        Assert.Contains("No current chain", report);
        Assert.Null(comparison.CurrentText);
        Assert.Equal(1, report.Split("STERT", StringSplitOptions.None).Length - 1);
    }

    [Theory]
    [InlineData("\0", "\\u0000")]
    [InlineData("\uFEFF", "\\ufeff")]
    public void Display_distinguishes_actual_controls_from_literal_unicode_escape_words(string character, string literal)
    {
        var captured = Chain.Replace("Find my installation.", character, StringComparison.Ordinal);
        var current = Chain.Replace("Find my installation.", literal, StringComparison.Ordinal);
        var comparison = ResultNotes.Compare(DertParser.Parse(current), Observe(captured), Path);
        var report = ResultNotes.DescribeComparison(comparison);
        Assert.Contains("// why ; codex ;; " + literal + " // [line ending: \\n]", report);
        Assert.Contains("// why ; codex ;; \\" + literal + " // [line ending: \\n]", report);
        Assert.Equal(captured, comparison.CapturedText);
        Assert.Equal(current, comparison.CurrentText);

        var description = ResultNotes.Describe(Observe(Chain) with { Summary = character, Effects = literal });
        Assert.Contains("Summary:\n  | " + literal + "\n", description);
        Assert.Contains("Effects:\n  | \\" + literal + "\n", description);
    }

    [Fact]
    public void Display_backslash_escaping_leaves_inserted_path_annotations_and_observation_strings_unchanged()
    {
        const string route = @"C:\tools\codex.cmd";
        var observation = Observe(Chain, evidence: [new("route", route)]);
        var before = ResultNotes.Plan(DertParser.Parse(Chain), observation, Path);
        var description = ResultNotes.Describe(observation);
        Assert.Contains(@"C:\\tools\\codex.cmd", description);
        var after = ResultNotes.Plan(DertParser.Parse(Chain), observation, Path);
        Assert.Equal(before.NoteText, after.NoteText);
        Assert.Contains("// evidence, // " + route, after.NoteText);
        Assert.DoesNotContain(@"C:\\tools\\codex.cmd", after.NoteText);
        Assert.Equal(route, observation.Evidence[0].Detail);
    }

    private static ResultObservation Observe(string source, IReadOnlyList<ResultEvidence>? evidence = null) =>
        new("observation-123", DateTimeOffset.Parse("2026-09-09T12:00:00-07:00"), "DERT update codex / local discovery",
            "codex", ObservationOutcome.Attention, "No confirmed installation.", "Read local PATH. No programs started.",
            new(Path, 1, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant(), source),
            evidence ?? [new("discovery", "No route found.")]);
}
