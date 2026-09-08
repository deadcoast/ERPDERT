using Dert.Core;
using Xunit;

namespace Dert.Tests;

public class RecoveryTests
{
    [Fact]
    public void Missing_block_closer_reports_the_opener_and_preserves_the_following_statement()
    {
        const string text = "// support doctoring ; conflicts ;;\n// fixme, // repair\ncommands ; updateme ;;";
        var document = DertParser.Parse(text);
        var error = Assert.Single(document.Diagnostics, d => d.Code == "DERT1001");
        Assert.Equal(1, error.LineNumber);
        var block = Assert.IsType<ReasoningBlockNode>(document.Nodes[0]);
        Assert.Null(block.Closing);
        Assert.Equal("repair", Assert.Single(block.Entries).Annotation!.Text);
        Assert.IsType<StatementNode>(document.Nodes[1]);
        Assert.Equal(text, document.Source.Text);
    }

    [Fact]
    public void Missing_same_line_closer_preserves_reason_and_reports_its_exact_line()
    {
        var document = DertParser.Parse("// HOW ; codex ;; first, then second");
        var note = Assert.IsType<ReasoningNoteNode>(Assert.Single(document.Nodes));
        Assert.Equal("first, then second", note.Header.Payload!.Text);
        Assert.Null(note.Closing);
        Assert.Contains(document.Diagnostics, d => d.Code == "DERT1002" && d.LineNumber == 1);
    }

    [Fact]
    public void Url_slashes_do_not_become_context_annotations()
    {
        var document = DertParser.Parse("// links ; docs ;; https://example.test/a //");
        var note = Assert.IsType<ReasoningNoteNode>(Assert.Single(document.Nodes));
        Assert.Equal("https://example.test/a", note.Header.Payload!.Text);
        Assert.NotNull(note.Closing);
        Assert.Empty(document.Diagnostics);
    }

    [Fact]
    public void Semicolon_in_a_free_note_does_not_open_structured_reasoning()
    {
        var document = DertParser.Parse("STERT\n// STERT ; starts the chain of command\nƒin.");
        var chain = Assert.IsType<ChainNode>(Assert.Single(document.Nodes));
        Assert.Equal("STERT ; starts the chain of command",
            Assert.IsType<NoteNode>(Assert.Single(chain.Children)).Content.Text);
    }

    [Fact]
    public void Whitespace_never_creates_an_extra_nesting_level()
    {
        var document = DertParser.Parse("// content ; target ;;\n  // first, // one\n// second // two\n//");
        var block = Assert.IsType<ReasoningBlockNode>(Assert.Single(document.Nodes));
        Assert.Equal(["first", "second"], block.Entries.Select(e => e.Value.Text));
        Assert.NotNull(block.Closing);
        Assert.Equal("  // first, // one", document.Source.Slice(document.Source.Lines[1].ContentSpan));
    }

    [Fact]
    public void Expanded_sinker_values_keep_order_and_inline_annotations()
    {
        var document = DertParser.Parse(
            "STERT\ncommand option ; variable ;;\nfirst, // one\n\n// a free note\nsecond // two\nƒin.");
        var chain = Assert.IsType<ChainNode>(Assert.Single(document.Nodes));
        var statement = Assert.Single(chain.Children.OfType<StatementNode>());
        Assert.Equal(["first", "second"], statement.Values.Select(v => v.Value.Text));
        Assert.Equal("two", statement.Values[1].Annotation!.Text);
        Assert.Single(chain.Children.OfType<NoteNode>());
    }

    [Fact]
    public void New_header_after_continuation_comma_is_not_consumed_as_a_sinker()
    {
        var document = DertParser.Parse("items ; one ;;\nvalue,\nitems ; two ;;\nnext");
        var statements = document.Nodes.OfType<StatementNode>().ToArray();
        Assert.Equal(2, statements.Length);
        Assert.Equal("value", Assert.Single(statements[0].Values).Value.Text);
        Assert.Equal("next", Assert.Single(statements[1].Values).Value.Text);
        Assert.Contains(document.Diagnostics, d => d.Code == "DERT3003");
    }

    [Fact]
    public void Quoted_marker_text_is_preserved_and_does_not_begin_an_annotation()
    {
        var document = DertParser.Parse("items ; literals ;;\n\"fin. // ; ;;\" // literal");
        var value = Assert.Single(Assert.IsType<StatementNode>(Assert.Single(document.Nodes)).Values);
        Assert.Equal("\"fin. // ; ;;\"", value.Value.Text);
        Assert.Equal("literal", value.Annotation!.Text);
    }

    [Fact]
    public void Slashes_inside_an_unfinished_quote_do_not_falsely_close_a_reasoning_note()
    {
        var document = DertParser.Parse("// HOW ; notes ;; \"literal //");
        var note = Assert.IsType<ReasoningNoteNode>(Assert.Single(document.Nodes));
        Assert.Null(note.Closing);
        Assert.Equal("\"literal //", note.Header.Payload!.Text);
        Assert.Contains(document.Diagnostics, d => d.Code == "DERT1002");
    }

    [Fact]
    public void Original_final_note_entry_comma_is_retained_without_an_automatic_fix()
    {
        var document = DertParser.Parse("// notes ; entries ;;\n// last, // explanation\n//");
        var block = Assert.IsType<ReasoningBlockNode>(Assert.Single(document.Nodes));
        Assert.True(Assert.Single(block.Entries).HasComma);
        Assert.DoesNotContain(document.Diagnostics, d => d.Code == "DERT3003");
        Assert.Equal("// notes ; entries ;;\n// last, // explanation\n//", document.Source.Text);
    }

    [Fact]
    public void Standalone_note_marker_outside_a_block_remains_an_empty_note()
    {
        var document = DertParser.Parse("//");
        Assert.IsType<NoteNode>(Assert.Single(document.Nodes));
        Assert.Empty(document.Diagnostics);
    }

    [Fact]
    public void A_mixed_document_keeps_all_three_explicit_chains_and_trailing_source()
    {
        var text = SyntaxFixtures.Read("mixed-structures.DERT");
        var document = DertParser.Parse(text);
        Assert.Equal(3, document.Nodes.OfType<ChainNode>().Count());
        Assert.All(document.Nodes.OfType<ChainNode>(), c => Assert.NotNull(c.Terminator));
        Assert.Contains(document.Nodes, n => n is StatementNode);
        Assert.Equal(text, string.Concat(document.Tokens.Select(t => document.Source.Slice(t.Span))));
    }
}
