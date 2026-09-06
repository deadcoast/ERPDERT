using System.Text.RegularExpressions;
using Dert.Core;
using Xunit;

namespace Dert.Tests;

public class OriginalSyntaxTests
{
    internal static string OriginalExample()
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "original.md"));
        return Regex.Match(source, @"(?ms)^```DERT\r?\n(.*?)^```").Groups[1].Value;
    }

    [Fact]
    public void Original_block_preserves_its_three_entries_and_their_individual_notes()
    {
        var document = DertParser.Parse(OriginalExample());
        var chain = Assert.IsType<ChainNode>(Assert.Single(document.Nodes));
        var block = Assert.Single(chain.Children.OfType<ReasoningBlockNode>());
        Assert.Equal("support doctoring", block.Header.Head.Text);
        Assert.Equal("conflicts", block.Header.Target!.Text);
        Assert.Equal(["possible_conflicts", "conflicts", "fixme"], block.Entries.Select(e => e.Value.Text));
        Assert.Equal("something thats known to override it, block it, or break it.",
            block.Entries[0].Annotation!.Text);
        Assert.Equal("something that is almost surely conflicting", block.Entries[1].Annotation!.Text);
        Assert.Equal("the confirmation command to fix the conflict", block.Entries[2].Annotation!.Text);
        Assert.All(block.Entries, e => Assert.True(e.HasComma));
        Assert.NotNull(block.Closing);
    }

    [Fact]
    public void Closed_reasoning_retains_English_commas_and_multiple_named_targets()
    {
        var document = DertParser.Parse(OriginalExample());
        var chain = Assert.IsType<ChainNode>(Assert.Single(document.Nodes));
        var notes = chain.Children.OfType<ReasoningNoteNode>().ToArray();
        Assert.Equal(2, notes.Length);
        Assert.Equal("similarities", notes[0].Header.Head.Text);
        Assert.Equal("AI", notes[0].Header.Target!.Text);
        Assert.Equal("claudecode, claude, codex, openai-desktop-app", notes[0].Header.Payload!.Text);
        Assert.Equal("codex, claude", notes[1].Header.Target!.Text);
        Assert.Equal("I dont care if it calls claudes installer, i just want it to be correct.",
            notes[1].Header.Payload!.Text);
        Assert.All(notes, n => Assert.NotNull(n.Closing));
    }

    [Fact]
    public void Closing_slashes_return_to_the_chain_and_keep_the_command_outside_the_note()
    {
        var chain = Assert.IsType<ChainNode>(Assert.Single(DertParser.Parse(OriginalExample()).Nodes));
        Assert.Equal(2, chain.Children.OfType<NoteNode>().Count());
        var command = Assert.Single(chain.Children.OfType<StatementNode>());
        Assert.Equal("commands", command.Header.Head.Text);
        Assert.Equal("updateme", command.Header.Target!.Text);
        Assert.Empty(command.Values);
        Assert.NotNull(chain.Terminator);
    }

    [Fact]
    public void Every_original_character_is_available_through_the_lexical_stream()
    {
        var text = OriginalExample();
        var document = DertParser.Parse(text);
        Assert.Equal(text, string.Concat(document.Tokens.Select(t => document.Source.Slice(t.Span))));
        Assert.Equal("fixme", document.Source.Slice(
            Assert.Single(Assert.IsType<ChainNode>(Assert.Single(document.Nodes))
                .Children.OfType<ReasoningBlockNode>()).Entries[2].Value.Span));
        Assert.DoesNotContain(document.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }
}
