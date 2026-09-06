using Dert.Core;
using Xunit;

namespace Dert.Tests;

public class SourceTests
{
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void Line_endings_are_retained_and_line_numbers_match_source(string newline)
    {
        var text = "STERT" + newline + "// WHY ; X ;; first, second //" + newline + "ƒin.";
        var document = DertParser.Parse(text);
        var chain = Assert.IsType<ChainNode>(Assert.Single(document.Nodes));
        var note = Assert.IsType<ReasoningNoteNode>(Assert.Single(chain.Children));
        Assert.Equal(2, note.LineNumber);
        Assert.Equal(5 + newline.Length + 3, note.Header.Head.Span.Start);
        Assert.Equal(text, string.Concat(document.Tokens.Select(t => document.Source.Slice(t.Span))));
    }

    [Fact]
    public void Bom_is_preserved_without_hiding_the_first_chain()
    {
        const string text = "\uFEFFSTERT\nƒin.";
        var document = DertParser.Parse(text);
        Assert.IsType<ChainNode>(Assert.Single(document.Nodes));
        Assert.Equal(text, string.Concat(document.Tokens.Select(t => document.Source.Slice(t.Span))));
    }

    [Fact]
    public void Title_and_rule_have_distinct_presentation_roles()
    {
        var document = DertParser.Parse("// TITLE\n// -----\n// a note");
        Assert.True(Assert.IsType<NoteNode>(document.Nodes[0]).IsTitle);
        Assert.IsType<RuleNode>(document.Nodes[1]);
        Assert.False(Assert.IsType<NoteNode>(document.Nodes[2]).IsTitle);
        Assert.Contains(document.Highlights, h => h.Role == SemanticRole.Title);
        Assert.Empty(document.Diagnostics);
    }

    [Fact]
    public void Bare_or_wrong_width_title_rule_produces_a_local_lint_diagnostic()
    {
        var document = DertParser.Parse("// TITLE\n---");
        Assert.Contains(document.Diagnostics, d => d.Code == "DERT2003" && d.LineNumber == 2);
        Assert.Equal("// TITLE\n---", document.Source.Text);
    }

    [Fact]
    public void Fold_region_keeps_the_exact_original_note_extent()
    {
        var document = DertParser.Parse("STERT\n// content ; target ;;\n// value // reason\n//\nƒin.");
        var fold = Assert.Single(document.Folds);
        Assert.Equal(2, fold.StartLine);
        Assert.Equal(4, fold.EndLine);
        Assert.Equal(1, fold.EntryCount);
        Assert.Equal("// content ; target ;;\n// value // reason\n//\n", document.Source.Slice(fold.Span));
    }

    [Fact]
    public void Semantic_roles_distinguish_hook_line_sinker_and_context_note()
    {
        var document = DertParser.Parse("// content ; target ;;\n// value // reason\n//");
        foreach (var role in new[] { SemanticRole.Hook, SemanticRole.Line, SemanticRole.Sinker, SemanticRole.ContextNote })
            Assert.Contains(document.Highlights, h => h.Role == role);
        var sinker = Assert.Single(document.Highlights, h => h.Role == SemanticRole.Sinker);
        Assert.Equal("value", document.Source.Slice(sinker.Span));
    }

    [Fact]
    public void Entry_opening_slashes_and_its_annotation_have_different_roles()
    {
        const string text = "// content ; target ;;\n// value // explanation\n//";
        var document = DertParser.Parse(text);
        var opening = text.IndexOf("// value", StringComparison.Ordinal);
        var annotation = text.IndexOf("// explanation", StringComparison.Ordinal);
        Assert.Equal(SemanticRole.Reasoning,
            document.Highlights.LastOrDefault(h => h.Span.Contains(opening))?.Role);
        Assert.Equal(SemanticRole.ContextNote,
            document.Highlights.LastOrDefault(h => h.Span.Contains(annotation))?.Role);
    }

    [Fact]
    public void Large_expanded_list_does_not_allocate_copies_of_every_previous_entry()
    {
        var text = "items ; values ;;\n" + string.Concat(Enumerable.Repeat("value,\n", 9999)) + "last";
        _ = DertParser.Parse("items ; values ;;\nvalue");
        var before = GC.GetAllocatedBytesForCurrentThread();
        var document = DertParser.Parse(text);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        var statement = Assert.IsType<StatementNode>(Assert.Single(document.Nodes));
        Assert.Equal(10000, statement.Values.Count);
        Assert.Equal("last", statement.Values[^1].Value.Text);
        Assert.Equal(text.Length, statement.Span.End);
        Assert.True(allocated < 64L * 1024 * 1024,
            $"A 70 KiB expanded list allocated {allocated:N0} bytes; avoid copying all prior entries per value.");
    }

    [Fact]
    public void Incomplete_random_fragments_preserve_source_and_never_produce_out_of_bounds_spans()
    {
        var random = new Random(731);
        const string alphabet = "/; ,\r\n\"abcƒ\uFEFF";
        for (var sample = 0; sample < 300; sample++)
        {
            var text = new string(Enumerable.Range(0, random.Next(0, 160))
                .Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray());
            var document = DertParser.Parse(text);
            Assert.Equal(text, string.Concat(document.Tokens.Select(t => document.Source.Slice(t.Span))));
            foreach (var span in document.Highlights.Select(h => h.Span))
            {
                Assert.InRange(span.Start, 0, text.Length);
                Assert.InRange(span.End, span.Start, text.Length);
            }
        }
    }
}
