using Dert.Core;
using Xunit;

namespace Dert.Tests;

public sealed class FlorinGoodbyeTests
{
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void Only_the_owned_goodbyes_first_character_changes(string newline)
    {
        var source = "\uFEFFSTERT" + newline + "// fin. stays a note" + newline + "fin.  // fin. Until tomorrow." + newline;
        var document = DertParser.Parse(source);
        var ending = FlorinGoodbye.EndingAt(document, 3);
        Assert.NotNull(ending);
        Assert.Equal(1, ending.ChainLine);
        Assert.Equal("fin.", ending.Spelling);
        var plan = FlorinGoodbye.Plan(document, 3);
        Assert.True(plan.IsReady);
        Assert.Equal("f", plan.Edit!.Before);
        Assert.Equal("ƒ", plan.Edit.After);
        Assert.Equal(source[..ending.Span.Start] + "ƒ" + source[(ending.Span.Start + 1)..], plan.ProposedText);
        Assert.Equal("fin.  // fin. Until tomorrow.", plan.BeforeLine);
        Assert.Equal("ƒin.  // fin. Until tomorrow.", plan.AfterLine);
        Assert.Equal(document.Diagnostics, DertParser.Parse(plan.ProposedText).Diagnostics);
        Assert.Equal(source, document.Source.Text);
    }

    [Theory]
    [InlineData("fin.")]
    [InlineData("// fin.")]
    [InlineData("STERT\n// fin.")]
    [InlineData("STERT\n// why ; goodbye ;; fin. //")]
    [InlineData("STERT\n\"fin.\"")]
    [InlineData("STERT\n'fin.'")]
    [InlineData("STERT\nhttps://example.test/fin.")]
    [InlineData("STERT\nC:\\notes\\fin.")]
    [InlineData("STERT\nI wrote fin. today.")]
    [InlineData("STERT\nfin")]
    [InlineData("STERT\nFIN.")]
    [InlineData("STERT\nfin. ; words")]
    [InlineData("STERT\nwords ; goodbye ;; fin.")]
    [InlineData("STERT\n// why ; goodbye ;;\n// fin.")]
    public void Ordinary_or_unfinished_words_are_not_goodbye_edits(string source)
    {
        var document = DertParser.Parse(source);
        Assert.Empty(FlorinGoodbye.Endings(document));
        var plan = FlorinGoodbye.Plan(document, document.Source.Lines.Count);
        Assert.False(plan.IsReady);
        Assert.Equal(source, plan.ProposedText);
    }

    [Fact]
    public void Each_chain_has_its_own_ending_and_existing_florin_is_not_an_edit()
    {
        const string source = "STERT\nfin.\nSTERT\nƒin.\nfin.";
        var document = DertParser.Parse(source);
        Assert.Equal(new[] { 2, 4 }, FlorinGoodbye.Endings(document).Select(ending => ending.LineNumber));
        Assert.Equal(3, FlorinGoodbye.EndingAt(document, 4)!.ChainLine);
        Assert.False(FlorinGoodbye.Plan(document, 4).IsReady);
        Assert.Null(FlorinGoodbye.EndingAt(document, 5));
        Assert.Null(FlorinGoodbye.EndingAt(document, 1));
        Assert.Null(FlorinGoodbye.EndingAt(document, 0));
    }
}
