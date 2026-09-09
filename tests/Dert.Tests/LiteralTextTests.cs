using Dert.Core;
using Xunit;

namespace Dert.Tests;

public class LiteralTextTests
{
    [Theory]
    [InlineData("use ; and ;; // literally,", "\"use ; and ;; // literally,\"")]
    [InlineData("C:\\tools; old\\", "\"C:\\tools; old\"\\")]
    [InlineData("\\\\server\\share\\", "\\\\server\\share\\")]
    [InlineData("C:\\Users\\Ryan\\", "C:\\Users\\Ryan\\")]
    [InlineData("https://example.test/a//b", "https://example.test/a//b")]
    [InlineData("a literal comma,", "\"a literal comma,\"")]
    [InlineData("STERT", "\"STERT\"")]
    [InlineData("fin.", "\"fin.\"")]
    [InlineData("say \"hello\" // literally,", "say \"hello\"\" // literally,\"")]
    [InlineData("\"already ; // quoted,\"", "\"already ; // quoted,\"")]
    [InlineData("  keep my spaces  ", "\"  keep my spaces  \"")]
    public void Literal_words_become_one_sinker_with_only_visible_quote_insertions(string words, string expected)
    {
        var quote = LiteralText.Protect(words);
        Assert.True(quote.IsAvailable, quote.Explanation);
        Assert.Equal(expected, quote.Text);
        var restored = quote.Text;
        foreach (var insertion in quote.InsertedQuotes.Reverse()) restored = restored.Remove(insertion, 1);
        Assert.Equal(words, restored);
        var parsed = DertParser.Parse("items ; note ;;\n" + quote.Text);
        var item = Assert.Single(Assert.IsType<StatementNode>(Assert.Single(parsed.Nodes)).Values);
        Assert.Equal(quote.Text, item.Value.Text);
        Assert.False(item.HasComma);
        Assert.Null(item.Annotation);
        Assert.Equal(quote.Text, LiteralText.Protect(quote.Text).Text);
    }

    [Theory]
    [InlineData("unfinished \"words ; //,")]
    [InlineData("one\ntwo")]
    [InlineData("one\0two")]
    public void Unfinished_or_multiline_words_are_retained_without_a_guessed_edit(string words)
    {
        var quote = LiteralText.Protect(words);
        Assert.False(quote.IsAvailable);
        Assert.Equal(words, quote.Text);
        Assert.Empty(quote.InsertedQuotes);
    }

    [Fact]
    public void Selected_terminal_comma_can_become_literal_without_rewriting_annotation_or_newlines()
    {
        const string source = "items ; note ;;\r\nhello, // keep this,\r\n\nSTERT\rfin.";
        var plan = LiteralText.Plan(DertParser.Parse(source), new(source.IndexOf("hello", StringComparison.Ordinal), 6));
        Assert.True(plan.IsReady, plan.Explanation);
        Assert.Equal(source.Replace("hello,", "\"hello,\"", StringComparison.Ordinal), plan.ProposedText);
        Assert.Contains("ends this list", plan.AfterReading);
    }

    [Theory]
    [InlineData("items ; note ;;\na,\nb", "a,", "ownership")]
    [InlineData("STERT ; where ;; codex", "codex", "application")]
    [InlineData("// why ; codex ;; use ; and ;; here //", "why ; codex", "field")]
    [InlineData("items ; note ;;\na // meaning", "a // meaning", "annotation")]
    public void Source_convenience_cannot_change_other_ownership_or_application_fields(string source, string selected, string reason)
    {
        var plan = LiteralText.Plan(DertParser.Parse(source), new(source.IndexOf(selected, StringComparison.Ordinal), selected.Length));
        Assert.False(plan.IsReady);
        Assert.Equal(source, plan.ProposedText);
        Assert.Contains(reason, plan.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Selection_in_reasoning_preserves_its_owner_and_closing_marker()
    {
        const string source = "// why ; note ;; use ; and ;; here //";
        var plan = LiteralText.Plan(DertParser.Parse(source), new(source.IndexOf("use", StringComparison.Ordinal), 17));
        Assert.True(plan.IsReady, plan.Explanation);
        var note = Assert.IsType<ReasoningNoteNode>(Assert.Single(DertParser.Parse(plan.ProposedText).Nodes));
        Assert.Equal("note", note.Header.Target!.Text);
        Assert.NotNull(note.Closing);
    }

    [Theory]
    [InlineData("e\u0301,", 0, 1)]
    [InlineData("👩‍💻,", 0, 2)]
    [InlineData("👩‍💻,", 1, 1)]
    public void Partial_visible_characters_cannot_be_split_by_a_quote(string source, int start, int length)
    {
        var plan = LiteralText.Plan(DertParser.Parse(source), new(start, length));
        Assert.False(plan.IsReady);
        Assert.Contains("whole characters", plan.Explanation);
        Assert.Equal(source, plan.ProposedText);
    }

    [Fact]
    public void Punctuation_combinations_preserve_every_input_character_and_have_a_stable_reading()
    {
        string[] parts = ["word", " ; ", " ;; ", " // ", "https://a/b", "\"quoted ; //,\"", "\\", "\\\\", "café 👩‍💻", ",", "\"", "\\\""];
        foreach (var first in parts)
        foreach (var second in parts)
        foreach (var last in new[] { "", ",", "\\", "\\\\" })
        {
            var words = first + second + last;
            var quote = LiteralText.Protect(words);
            var restored = quote.Text;
            foreach (var insertion in quote.InsertedQuotes.Reverse())
            {
                Assert.Equal('"', restored[insertion]);
                restored = restored.Remove(insertion, 1);
            }
            Assert.Equal(words, restored);
            if (!quote.IsAvailable) { Assert.Empty(quote.InsertedQuotes); continue; }
            var document = DertParser.Parse("items ; note ;;\n" + quote.Text);
            var value = Assert.Single(Assert.IsType<StatementNode>(Assert.Single(document.Nodes)).Values);
            Assert.Equal(quote.Text, value.Value.Text);
            Assert.Null(value.Annotation); Assert.False(value.HasComma);
            Assert.Equal(quote.Text, LiteralText.Protect(quote.Text).Text);
        }
    }
}
