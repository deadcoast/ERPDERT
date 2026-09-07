using Dert.Core;
using Xunit;

namespace Dert.Tests;

public class NoteClosureRepairTests
{
    private const string Note = "// context ; notes ;;\n// first, // its reason\n// later\ncommands ; updateme ;;";

    [Fact]
    public void Boundary_choices_are_source_positions_with_explicit_ownership_counts()
    {
        var analysis = NoteClosureRepair.Analyze(DertParser.Parse(Note), 2);
        Assert.Equal(["2", "3", "4"], analysis.Boundaries.Select(b => b.Key));
        Assert.Equal([0, 1, 2], analysis.Boundaries.Select(b => b.KeptCount));
        Assert.Equal([2, 1, 0], analysis.Boundaries.Select(b => b.ReleasedCount));
        Assert.Equal(Note, analysis.Document.Source.Text);
    }

    [Fact]
    public void An_earlier_boundary_releases_only_the_selected_following_note()
    {
        var plan = NoteClosureRepair.Plan(NoteClosureRepair.Analyze(DertParser.Parse(Note), 1), "3");
        Assert.Equal("// context ; notes ;;\n// first, // its reason\n//\n// later\ncommands ; updateme ;;", plan.ProposedText);
        Assert.Equal("", plan.Edit.Before);
        Assert.Equal("//\n", plan.Edit.After);
        Assert.Equal(1, plan.Boundary.KeptCount);
        var change = Assert.Single(plan.OwnershipChanges);
        Assert.Equal(3, change.OriginalLine);
        Assert.Equal(4, change.ResultLine);
        Assert.Equal(NoteResultRole.OrdinaryNote, change.Role);
        var block = Assert.Single(plan.ResultDocument.Nodes.OfType<ReasoningBlockNode>());
        Assert.NotNull(block.Closing);
        Assert.Equal("its reason", Assert.Single(block.Entries).Annotation!.Text);
        Assert.True(block.Entries[0].HasComma);
    }

    [Fact]
    public void Released_structured_content_is_described_by_its_real_new_owner()
    {
        const string text = "commands ; list ;;\n// outer ; notes ;;\n// inner ; notes ;;\n// item\nvalue";
        var plan = NoteClosureRepair.Plan(NoteClosureRepair.Analyze(DertParser.Parse(text), 2), "3");
        Assert.Equal(NoteResultRole.ReasoningBlock, plan.OwnershipChanges[0].Role);
        Assert.Equal(NoteResultRole.ReasoningEntry, plan.OwnershipChanges[1].Role);
        Assert.Equal(4, plan.OwnershipChanges[1].OwnerLine);
        Assert.Equal("inner ; notes", plan.OwnershipChanges[1].OwnerLabel);
        Assert.Contains(plan.ResultDocument.Diagnostics, d => d.Code == "DERT1001" && d.LineNumber == 4);
        Assert.Equal("value", Assert.Single(Assert.Single(plan.ResultDocument.Nodes.OfType<StatementNode>()).Values).Value.Text);
    }

    [Fact]
    public void Released_same_line_reasoning_does_not_become_an_ordinary_note()
    {
        const string text = "STERT\n// outer ; notes ;;\n// HOW ; codex ;; first, then second //\nƒin.";
        var plan = NoteClosureRepair.Plan(NoteClosureRepair.Analyze(DertParser.Parse(text), 3), "3");
        Assert.Equal(NoteResultRole.ReasoningNote, Assert.Single(plan.OwnershipChanges).Role);
        Assert.Equal(1, plan.OwnershipChanges[0].OwnerLine);
    }

    [Theory]
    [InlineData("// n ; t ;;", "// n ; t ;;\r\n//")]
    [InlineData("// n ; t ;;\n", "// n ; t ;;\n//\n")]
    [InlineData("// n ; t ;;\r", "// n ; t ;;\r//\r")]
    [InlineData("// n ; t ;;\r\n", "// n ; t ;;\r\n//\r\n")]
    [InlineData("// n ; t ;;\n// café", "// n ; t ;;\n// café\n//")]
    [InlineData("\uFEFF// n ; t ;;\r\n// one\n// two", "\uFEFF// n ; t ;;\r\n// one\n// two\n//")]
    [InlineData("// n ; t ;;\r\n\r\n", "// n ; t ;;\r\n\r\n//\r\n")]
    public void Eof_insertion_preserves_bom_local_newlines_and_final_newline_presence(string before, string after)
    {
        var plan = NoteClosureRepair.Plan(NoteClosureRepair.Analyze(DertParser.Parse(before), 1), "eof");
        Assert.Equal(after, plan.ProposedText);
        Assert.Equal(after, plan.ApplyTo(before));
        Assert.Equal(before, plan.Source);
        Assert.DoesNotContain(plan.ResultDocument.Diagnostics, d => d.Code == "DERT1001");
    }

    [Fact]
    public void Eof_empty_source_line_selects_the_open_block_without_duplicate_choices()
    {
        var analysis = NoteClosureRepair.Analyze(DertParser.Parse("// n ; t ;;\n// item\n"), 3);
        Assert.Equal(["2", "eof"], analysis.Boundaries.Select(b => b.Key));
    }

    [Theory]
    [InlineData("// n ; t ;;\n// item\n//", 1)]
    [InlineData("// HOW ; t ;; unfinished", 1)]
    [InlineData("STERT\n// ordinary", 2)]
    [InlineData("// n ; t ;;\ncommands ; x ;;", 2)]
    [InlineData("// n ; t ;;", 0)]
    [InlineData("// n ; t ;;", 99)]
    public void Non_block_or_outside_selections_are_refused(string text, int line)
    {
        Assert.Throws<ArgumentException>(() => NoteClosureRepair.Analyze(DertParser.Parse(text), line));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("eof")]
    [InlineData("999")]
    [InlineData("3.0")]
    public void An_unoffered_cut_cannot_write_a_guessed_boundary(string key)
    {
        var analysis = NoteClosureRepair.Analyze(DertParser.Parse(Note), 1);
        Assert.Throws<ArgumentException>(() => NoteClosureRepair.Plan(analysis, key));
    }

    [Fact]
    public void Blank_lines_are_preserved_without_duplicate_ownership_choices()
    {
        const string text = "// n ; t ;;\r\n\n// item\r\n\r\ncommands ; x ;;";
        var analysis = NoteClosureRepair.Analyze(DertParser.Parse(text), 2);
        Assert.Equal(["3", "5"], analysis.Boundaries.Select(b => b.Key));
        Assert.Equal("// n ; t ;;\r\n\n//\n// item\r\n\r\ncommands ; x ;;",
            NoteClosureRepair.Plan(analysis, "3").ProposedText);
    }

    [Fact]
    public void The_original_missing_closer_case_keeps_every_entry_and_its_English()
    {
        var original = OriginalSyntaxTests.OriginalExample();
        var document = DertParser.Parse(original);
        var chain = Assert.IsType<ChainNode>(Assert.Single(document.Nodes));
        var block = Assert.Single(chain.Children.OfType<ReasoningBlockNode>());
        var closerLine = document.Source.Lines.Single(l => l.ContentSpan.Contains(block.Closing!.Value.Start));
        var damaged = original[..closerLine.Start] + original[closerLine.End..];
        var analysis = NoteClosureRepair.Analyze(DertParser.Parse(damaged), block.LineNumber);
        var plan = NoteClosureRepair.Plan(analysis, analysis.Boundaries[^1].Key);
        Assert.Equal(3, plan.Boundary.KeptCount);
        Assert.Empty(plan.OwnershipChanges);
        var repairedChain = Assert.IsType<ChainNode>(Assert.Single(plan.ResultDocument.Nodes));
        var repairedBlock = Assert.Single(repairedChain.Children.OfType<ReasoningBlockNode>());
        Assert.Equal(["possible_conflicts", "conflicts", "fixme"], repairedBlock.Entries.Select(e => e.Value.Text));
        Assert.All(repairedBlock.Entries, e => Assert.True(e.HasComma));
        Assert.Equal("the confirmation command to fix the conflict", repairedBlock.Entries[2].Annotation!.Text);
        Assert.NotNull(repairedBlock.Closing);
        Assert.Single(repairedChain.Children.OfType<StatementNode>());
        Assert.Equal(damaged, plan.Source);
    }

    [Fact]
    public void Only_the_selected_unfinished_block_gets_a_closer()
    {
        const string text = "// first ; notes ;;\nwords\n// second ; notes ;;\n// item";
        var plan = NoteClosureRepair.Plan(NoteClosureRepair.Analyze(DertParser.Parse(text), 3), "eof");
        Assert.Equal(text + "\n//", plan.ProposedText);
        Assert.Contains(plan.ResultDocument.Diagnostics, d => d.Code == "DERT1001" && d.LineNumber == 1);
        Assert.Throws<InvalidOperationException>(() => plan.ApplyTo(text + "changed"));
    }
}
