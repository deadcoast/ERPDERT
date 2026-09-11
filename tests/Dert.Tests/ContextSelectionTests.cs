using System.Security.Cryptography;
using System.Text;
using Dert.Core;
using Xunit;

namespace Dert.Tests;

public class ContextSelectionTests
{
    private const string Source = "\uFEFFSTERT ; update ;; codex\r\n" +
        "// support doctoring ; conflicts ;;  \n" +
        "// possible, // first explanation\r\n\n" +
        "// fixme // keep this explanation\r" +
        "//  \n" +
        "\t\n" +
        "// HOW ; codex ;; Keep either installer, if correct. //\r\n" +
        "// uncommon words ; unknown name ;; Contradict the earlier note. //\n" +
        "fin.\nordinary prose\n";

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(6)]
    public void Opener_entry_and_closer_capture_the_actual_whole_note(int line)
    {
        var document = DertParser.Parse(Source);
        var selection = ContextSelection.Capture(document, line, "notes.DERT", ContextSourceKind.FileSnapshot);
        var primary = selection.Primary;
        Assert.Same(document, selection.Document);
        Assert.Equal(2, primary.LineNumber);
        var chain = Assert.IsType<ChainNode>(Assert.Single(document.Nodes.OfType<ChainNode>()));
        Assert.Same(chain, primary.Parent);
        Assert.Same(chain.Children[0], primary.Node);
        Assert.True(primary.IsClosed);
        Assert.Equal("// support doctoring ; conflicts ;;  \n// possible, // first explanation\r\n\n// fixme // keep this explanation\r//  \n",
            document.Source.Slice(primary.Span));
        Assert.Same(primary, Assert.Single(selection.Build().Included));
        Assert.Equal(Source, document.Source.Text);
    }

    [Fact]
    public void Omitted_ranges_cover_every_unselected_character_including_operations_and_spacing()
    {
        var selection = ContextSelection.Capture(DertParser.Parse(Source), 2, "notes.DERT", ContextSourceKind.FileSnapshot);
        var packet = selection.Build([8]);
        Assert.Equal(new[] { 2, 8 }, packet.Included.Select(note => note.LineNumber));
        Assert.Equal(3, packet.Excluded.Count);
        var parts = packet.Included.Select(note => (note.Span, Text: selection.Document.Source.Slice(note.Span)))
            .Concat(packet.Excluded.Select(range => (range.Span, range.Text))).OrderBy(part => part.Span.Start).ToArray();
        Assert.Equal(Source, string.Concat(parts.Select(part => part.Text)));
        var offset = 0;
        foreach (var part in parts) { Assert.Equal(offset, part.Span.Start); offset = part.Span.End; }
        Assert.Equal(Source.Length, offset);
        Assert.Equal((1, 1), (packet.Excluded[0].FirstLine, packet.Excluded[0].LastLine));
        Assert.Equal((7, 7), (packet.Excluded[1].FirstLine, packet.Excluded[1].LastLine));
        Assert.Equal((9, 11), (packet.Excluded[2].FirstLine, packet.Excluded[2].LastLine));
        Assert.DoesNotContain("STERT ; update ;; codex", packet.Report);
        Assert.DoesNotContain("Contradict the earlier note.", packet.Report);
        Assert.Contains("STERT ; update ;; codex", packet.LeftOutReport);
        Assert.Contains("Contradict the earlier note.", packet.LeftOutReport);
        Assert.All(packet.Excluded, range => Assert.Contains(range.Text, packet.LeftOutReport));
        Assert.DoesNotContain("first explanation", packet.LeftOutReport);
    }

    [Fact]
    public void Deliberate_choices_are_deduplicated_in_source_order_and_always_retain_the_primary()
    {
        var selection = ContextSelection.Capture(DertParser.Parse(Source), 8, "notes.DERT", ContextSourceKind.FileSnapshot);
        var packet = selection.Build([9, 2, 9]);
        Assert.Equal(new[] { 2, 8, 9 }, packet.Included.Select(note => note.LineNumber));
        Assert.Equal("uncommon words", packet.Included[2].Label);
        Assert.Equal(new[] { 2, 8, 9 }, selection.Notes.Select(note => note.LineNumber));
        Assert.Equal(8, Assert.Single(selection.Build().Included).LineNumber);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(11)]
    [InlineData(999)]
    public void Additional_choices_must_be_actual_note_openers_in_the_captured_revision(int line)
    {
        var selection = ContextSelection.Capture(DertParser.Parse(Source), 2, "notes.DERT", ContextSourceKind.FileSnapshot);
        var error = Assert.Throws<ArgumentException>(() => selection.Build([line]));
        Assert.Contains("opener", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, Assert.Single(selection.Build().Included).LineNumber);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(999)]
    public void Ordinary_prose_spacing_and_operations_do_not_gain_an_invented_reasoning_owner(int line)
    {
        var error = Assert.Throws<ArgumentException>(() => ContextSelection.Capture(DertParser.Parse(Source), line,
            "notes.DERT", ContextSourceKind.FileSnapshot));
        Assert.Contains("reasoning note", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Shared_names_mark_choices_without_automatically_including_their_words()
    {
        var document = DertParser.Parse("// HOW ; codex ;; First position. //\n// why ; CODEX ;; Opposite position. //\n// unrelated ; other ;; Separate. //");
        var selection = ContextSelection.Capture(document, 1, "Unsaved notes", ContextSourceKind.EditorBuffer);
        Assert.False(selection.Primary.IsRelated);
        Assert.True(selection.Notes[1].IsRelated);
        Assert.False(selection.Notes[2].IsRelated);
        Assert.Single(selection.Build().Included);
        Assert.DoesNotContain("Opposite position.", selection.Build().Report);
    }

    [Fact]
    public void Report_carries_revision_provenance_exact_ranges_parent_and_real_boundary()
    {
        var document = DertParser.Parse(Source);
        var selection = ContextSelection.Capture(document, 3, "notes.DERT", ContextSourceKind.FileSnapshot);
        var packet = selection.Build();
        Assert.Equal(Convert.ToHexString(SHA256.HashData(new UTF8Encoding(false, true).GetBytes(Source))).ToLowerInvariant(), selection.SourceRevision);
        Assert.Contains("Source: \"notes.DERT\"", packet.Report);
        Assert.Contains("Source kind: file snapshot", packet.Report);
        Assert.Contains("Captured UTF-8 SHA-256: " + selection.SourceRevision, packet.Report);
        Assert.Contains("UTF-16 [" + selection.Primary.Span.Start + ", " + selection.Primary.Span.End + ")", packet.Report);
        Assert.Contains("line 2, column 1 to line 7, column 1 (end exclusive)", packet.Report);
        Assert.Contains("Parent: Chain", packet.Report);
        Assert.Contains("at line 1", packet.Report);
        Assert.Contains("Closing //: line 6, column 1", packet.Report);
        Assert.Contains("Omitted ranges", packet.Report);
        Assert.Contains("Nothing was executed", packet.Report);
        Assert.Contains(document.Source.Slice(selection.Primary.Span), packet.Report);
    }

    [Theory]
    [InlineData("// HOW ; music ;; Keep these words close. //", "Closing //: line 1")]
    [InlineData("// HOW ; music ;; Keep these unfinished words close.", "Closing //: missing")]
    public void Review_shows_the_authors_words_before_detailed_provenance_and_keeps_the_boundary_above_them(string source, string boundary)
    {
        var packet = ContextSelection.Capture(DertParser.Parse(source), 1, "Unsaved notes", ContextSourceKind.EditorBuffer).Build();
        var excerpt = packet.Report.IndexOf(source, StringComparison.Ordinal);
        Assert.True(excerpt >= 0);
        Assert.True(packet.Report[..excerpt].Count(character => character == '\n') <= 8,
            "The author should reach the first source excerpt within nine report lines.");
        Assert.True(packet.Report.IndexOf(boundary, StringComparison.Ordinal) < excerpt);
        foreach (var detail in new[] { "Captured UTF-8 SHA-256:", "Coordinates use", "Source range:", "Parent:", "Parser findings:" })
            Assert.True(packet.Report.IndexOf(detail, StringComparison.Ordinal) > excerpt, detail + " should follow the author's words.");
        Assert.Contains("Effects of this context creation: Nothing was executed or sent to an agent. No agent check was requested.", packet.Report);
    }

    [Fact]
    public void Left_out_reading_also_shows_omitted_words_before_full_revision_and_coordinate_details()
    {
        const string source = "ordinary prose kept out\n// HOW ; music ;; Selected note. //";
        var packet = ContextSelection.Capture(DertParser.Parse(source), 2, "notes.DERT", ContextSourceKind.FileSnapshot).Build();
        var excerpt = packet.LeftOutReport.IndexOf("ordinary prose kept out\n", StringComparison.Ordinal);
        Assert.True(excerpt >= 0);
        foreach (var detail in new[] { "Captured UTF-8 SHA-256:", "Review SHA-256:", "Coordinates use" })
            Assert.True(packet.LeftOutReport.IndexOf(detail, StringComparison.Ordinal) > excerpt, detail + " should follow the omitted words.");
        Assert.Contains("UTF-16 [0, 24)", packet.LeftOutReport);
    }

    [Theory]
    [InlineData("// HOW ; café ;; Keep the incomplete note", "DERT1002")]
    [InlineData("// unknown ; café ;;\r\n// one, // a real annotation\r\n", "DERT1001")]
    public void Unfinished_notes_keep_the_missing_closer_and_actual_parser_findings(string source, string code)
    {
        var selection = ContextSelection.Capture(DertParser.Parse(source), 1, "Unsaved notes", ContextSourceKind.EditorBuffer);
        var packet = selection.Build();
        Assert.False(selection.Primary.IsClosed);
        Assert.Contains("Source kind: editor buffer", packet.Report);
        Assert.Contains("Parent: document", packet.Report);
        Assert.Contains("Closing //: missing", packet.Report);
        Assert.Contains(code, packet.Report);
        Assert.Contains(source, packet.Report);
        Assert.Empty(packet.Excluded);
        Assert.Contains("No source was left out.", packet.LeftOutReport);
    }

    [Fact]
    public void Dynamic_fences_keep_backticks_literal_and_metadata_cannot_insert_report_sections()
    {
        const string source = "\uFEFF// `````title ; names ;;\r\n// ```\n// ````` // keep the fence\r//\n";
        var selection = ContextSelection.Capture(DertParser.Parse(source), 1, "note\n# Injected\u001b[31m.DERT", ContextSourceKind.EditorBuffer);
        var packet = selection.Build();
        Assert.Equal(0, selection.Primary.Span.Start);
        Assert.Contains("``````dert\n" + source + "``````\n", packet.Report);
        Assert.DoesNotContain("\n# Injected", packet.Report);
        Assert.DoesNotContain('\u001b', packet.Report);
        Assert.Contains("\\n# Injected", packet.Report);
        Assert.Contains("\\u001b", packet.Report, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Review_identity_is_stable_but_changes_with_revision_identity_kind_or_explicit_selection()
    {
        var document = DertParser.Parse(Source);
        var original = ContextSelection.Capture(document, 2, "notes.DERT", ContextSourceKind.FileSnapshot).Build([8]);
        Assert.Matches("^[a-f0-9]{64}$", original.ReviewId);
        Assert.Equal(original.ReviewId, ContextSelection.Capture(document, 3, "notes.DERT", ContextSourceKind.FileSnapshot).Build([8, 8]).ReviewId);
        Assert.NotEqual(original.ReviewId, original.Selection.Build().ReviewId);
        Assert.NotEqual(original.ReviewId, ContextSelection.Capture(document, 2, "other.DERT", ContextSourceKind.FileSnapshot).Build([8]).ReviewId);
        Assert.NotEqual(original.ReviewId, ContextSelection.Capture(document, 2, "notes.DERT", ContextSourceKind.EditorBuffer).Build([8]).ReviewId);
        var changed = ContextSelection.Capture(DertParser.Parse(Source + "left out, but changed"), 2, "notes.DERT", ContextSourceKind.FileSnapshot).Build([8]);
        Assert.NotEqual(original.ReviewId, changed.ReviewId);
        Assert.NotEqual(original.Selection.SourceRevision, changed.Selection.SourceRevision);
    }

    [Fact]
    public void Incomplete_Unicode_anywhere_in_the_revision_refuses_a_lossy_capture_without_editing_source()
    {
        const string source = "// HOW ; music ;; Complete note. //\n\ud800";
        var document = DertParser.Parse(source);
        var error = Assert.Throws<ArgumentException>(() => ContextSelection.Capture(document, 1, "Unsaved notes", ContextSourceKind.EditorBuffer));
        Assert.Contains("incomplete Unicode", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(source, document.Source.Text);
    }
}
