using Dert.Core;
using Xunit;

namespace Dert.Tests;

public class StructureIndexTests
{
    [Fact]
    public void Active_entries_and_interleaved_notes_retain_their_actual_parents()
    {
        var document = DertParser.Parse("STERT\nitems ; weekend ;;\nsnacks, // for everyone\n// keep this note\n\nmusic\nfin.");
        var index = new DertStructureIndex(document);
        Assert.IsType<StatementNode>(index.AtLine(3)!.Parent);
        Assert.IsType<ChainNode>(index.AtLine(4)!.Parent);
        Assert.Null(index.AtLine(5));
        Assert.Equal(2, index.OwnerLine(6));
        Assert.Equal(1, index.OwnerLine(2));
        Assert.Equal(1, index.OwnerLine(7));
        Assert.Null(index.OwnerLine(1));
        Assert.Equal("Chain ending", index.DescribeLine(7));
    }

    [Fact]
    public void Reasoning_closers_and_recovered_endings_are_distinct()
    {
        var index = new DertStructureIndex(DertParser.Parse("// why ; music ;;\n// jazz, // quieter\n//\n// why ; music ;;\n// folk\nitems ; next ;;\nbook"));
        Assert.Equal(1, index.OwnerLine(2));
        Assert.Equal(1, index.OwnerLine(3));
        Assert.Equal("Reasoning ending", index.DescribeLine(3));
        Assert.Null(Assert.IsType<ReasoningBlockNode>(index.AtLine(5)!.Parent).Closing);
        Assert.IsType<StatementNode>(index.AtLine(6)!.Node);
        Assert.Equal(6, index.OwnerLine(7));
    }

    [Fact]
    public void Literal_search_keeps_exact_spans_and_does_not_split_prose_or_bind_targets()
    {
        var source = "\uFEFF// TRIP\r\n// ----\r\n// HOW ; codex, claude ;; Codex, keep CODEX unchanged. //\r\nhttps://example.test/a[b]\r\n";
        var document = DertParser.Parse(source);
        var index = new DertStructureIndex(document);
        var hit = Assert.Single(index.Find("codex"));
        Assert.Equal(3, hit.LineNumber);
        Assert.Equal(3, hit.Matches.Count);
        Assert.Equal(new[] { "codex", "Codex", "CODEX" }, hit.Matches.Select(document.Source.Slice));
        Assert.Equal(4, Assert.Single(index.Find("a[b]")).LineNumber);
        Assert.Empty(index.Find("a.*"));
        Assert.Empty(index.Find(""));
        Assert.Null(index.AtLine(0));
        Assert.Null(index.AtLine(500));
        Assert.Equal(source, document.Source.Text);
    }

    [Fact]
    public void Bare_prose_and_unfinished_notes_remain_searchable_without_a_chain()
    {
        var index = new DertStructureIndex(DertParser.Parse("my weekend plans\n// why ; rest ;; Take a break\n"));
        Assert.Equal("Text", index.DescribeLine(1));
        Assert.Equal("Reasoning note", index.DescribeLine(2));
        Assert.Null(index.OwnerLine(2));
        Assert.Equal(2, Assert.Single(index.Find("break")).LineNumber);
    }
}
