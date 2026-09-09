using Dert.Mer;
using Xunit;

namespace Dert.Tests;

public class MerSourceExcerptTests
{
    [Fact]
    public void A_highlight_boundary_cannot_insert_a_dotted_circle_or_split_a_grapheme_in_recorded_source()
    {
        var snapshot = Example("  6  //\u0301 cafe, // retained");
        var source = snapshot.Score.Source!;
        var lines = source.Lines.ToArray();
        lines[2] = new(new("  6  ", Ink.Muted), new("//", Ink.Signal), new("\u0301 cafe, // retained", Ink.Text));
        snapshot = snapshot with { Score = snapshot.Score with { Source = source with { Lines = lines } } };
        var frame = Assert.IsType<MerFrame>(MerCompositor.Source(snapshot, 6, 25));
        var shown = string.Concat(frame.Lines.Where(line => line.Runs.FirstOrDefault()?.Ink == Ink.Signal)
            .SelectMany(line => line.Runs.Skip(1)).Select(run => run.Text));
        Assert.Equal("  6  //\u0301 cafe, // retained", shown);
        Assert.Contains(frame.Lines.SelectMany(line => line.Runs), run => run.Text.Contains("/\u0301", StringComparison.Ordinal));
    }

    [Fact]
    public void Source_wrapping_prefers_whole_words_without_consuming_their_original_spaces()
    {
        const string raw = "  6  // food, // Keep  the booking number with this note.";
        var frame = Assert.IsType<MerFrame>(MerCompositor.Source(Example(raw), 6, 40));
        var selected = frame.Lines.Where(line => line.Runs.FirstOrDefault()?.Ink == Ink.Signal).ToArray();
        Assert.Contains(selected, line => line.Text.Contains("booking", StringComparison.Ordinal));
        Assert.Contains(selected, line => line.Text.Contains("number", StringComparison.Ordinal));
        Assert.Equal(raw, string.Concat(selected.SelectMany(line => line.Runs.Skip(1)).Select(run => run.Text)));
    }

    [Fact]
    public void Terminal_controls_are_visible_escapes_and_the_excerpt_discloses_that_display_treatment()
    {
        var frame = Assert.IsType<MerFrame>(MerCompositor.Source(Example("  6  // tab\tand\u001b[2J"), 6, 80));
        var text = string.Join('\n', frame.Lines.Select(line => line.Text));
        Assert.DoesNotContain('\u001b', text);
        Assert.Contains("\\tand\\u001B[2J", text);
        Assert.Contains("Controls shown as escapes", text);
    }

    [Theory]
    [InlineData(110)]
    [InlineData(80)]
    [InlineData(25)]
    public void Source_inspection_preserves_authored_spaces_punctuation_and_graphemes_with_bounded_rows(int columns)
    {
        const string raw = "  6  // café, // Two  spaces. 日本語 👩‍💻; keep, these // words.";
        var snapshot = Example(raw);
        var frame = Assert.IsType<MerFrame>(MerCompositor.Source(snapshot, 6, columns));
        Assert.Contains("Recorded source", string.Join('\n', frame.Lines.Select(line => line.Text)));
        // Authored rows are distinct from headings and omission notices; ignore only the display gutter.
        var shown = string.Concat(frame.Lines.Where(line => line.Runs.FirstOrDefault()?.Ink == Ink.Signal)
            .SelectMany(line => line.Runs.Skip(1)).Select(run => run.Text));
        Assert.Equal(raw, shown);
        Assert.All(frame.Lines, line => Assert.InRange(TerminalText.Width(line.Text), 0, columns - 1));
        Assert.DoesNotContain(frame.Lines.SelectMany(line => line.Runs), run => run.Text.Contains('�'));
    }

    [Fact]
    public void Excerpts_use_recorded_source_not_semantic_reconstruction_and_mark_omitted_lines()
    {
        var snapshot = Example("  6  // exact, // original punctuation");
        var frame = Assert.IsType<MerFrame>(MerCompositor.Source(snapshot, 6, 110));
        var text = string.Join('\n', frame.Lines.Select(line => line.Text));
        Assert.Contains("// exact, // original punctuation", text);
        Assert.DoesNotContain("// extracted", text);
        Assert.Contains("1 line between", text);
        Assert.Contains("  8  //", text);
        Assert.Contains("unchanged", text);
        Assert.DoesNotContain("omitted entry text", text);
    }

    [Fact]
    public void An_unclosed_note_has_an_explicit_unresolved_ending_not_a_fabricated_closer()
    {
        var frame = Assert.IsType<MerFrame>(MerCompositor.Source(Example("  6  // exact", close: null), 6, 80));
        var text = string.Join('\n', frame.Lines.Select(line => line.Text));
        Assert.Contains("Closing // missing", text);
        Assert.Contains("unresolved", text);
        Assert.DoesNotContain("  8  //", text);
    }

    [Fact]
    public void Source_inspection_does_not_hide_a_failed_record_or_invent_a_missing_mapping()
    {
        var snapshot = Example("  6  // exact");
        Assert.Null(MerCompositor.Source(snapshot with { Outcome = MotionOutcome.Failed }, 6, 80));
        Assert.Null(MerCompositor.Source(snapshot, 999, 80));
        Assert.Null(MerCompositor.Source(snapshot with { Score = snapshot.Score with { Source = new("absent", []) } }, 6, 80));
    }

    private static MotionSnapshot Example(string raw, int? close = 8)
    {
        var journal = new MotionJournal(new("Plans", "No writes", [new("context", "Context", "Read")],
            new("plans.DERT", [InkLine.Plain("  4  // plans ; friends ;;"), InkLine.Plain("  5  // omitted entry text"),
                InkLine.Plain(raw), InkLine.Plain("  7  // another entry"), InkLine.Plain("  8  //")], [4, 5, 6, 7, 8])), DateTimeOffset.UnixEpoch);
        journal.Apply(new StartStage("context"), TimeSpan.Zero);
        journal.Apply(new AddEvidence("context", new("Entry", "extracted", EvidenceKind.Source, 6, 4,
            new NoteConnectionFocus("plans", "extracted", "not the raw source", close))), TimeSpan.Zero);
        journal.Apply(new SettleStage("context", StageState.Passed, "Read."), TimeSpan.Zero);
        journal.Apply(new FinishMotion(MotionOutcome.Complete, "Done."), TimeSpan.Zero);
        return journal.Snapshot(TimeSpan.Zero);
    }
}
