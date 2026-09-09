using Dert.Cli.Operations;
using Dert.Core;
using Xunit;

namespace Dert.Tests;

public class SourceInterpretationTests
{
    [Fact]
    public void T14_duplicate_named_targets_keep_separate_sources_without_resolving_objects()
    {
        const string source = "// HOW ; codex ;; First reason, kept whole. //\n// HOW ; codex ;; A different reason. //";
        var document = DertParser.Parse(source);
        var notes = document.Nodes.OfType<ReasoningNoteNode>().ToArray();
        Assert.Equal(2, notes.Length);
        Assert.Equal(notes[0].Header.Target!.Text, notes[1].Header.Target!.Text);
        Assert.NotEqual(notes[0].Header.Target!.Span, notes[1].Header.Target!.Span);
        Assert.All(new DertStructureIndex(document).Locations, location => Assert.Null(location.Parent));
        Assert.Empty(DertOperationPlan.Build(document).Steps);
    }

    [Fact]
    public void T24_proposed_receipt_shape_is_inert_raw_reasoning_not_a_replayed_operation()
    {
        const string source = "// checked ; codex ;;\n// source ; chain ;; chain-weekend-01\n// result ; command lookup ;; one matching command\n// changed ; this computer ;; nothing\n// observed ; local time ;; 06 SEP 2026 14:37:08\n//";
        var document = DertParser.Parse(source);
        var block = Assert.IsType<ReasoningBlockNode>(Assert.Single(document.Nodes));
        Assert.Equal(4, block.Entries.Count);
        Assert.Equal("source ; chain ;; chain-weekend-01", block.Entries[0].Value.Text);
        Assert.Equal("result ; command lookup ;; one matching command", block.Entries[1].Value.Text);
        var plan = DertOperationPlan.Build(document);
        Assert.Empty(plan.Steps);
        Assert.True(plan.CanRun);
        Assert.Equal(source, document.Source.Text);
    }

    [Fact]
    public void T21_title_repair_does_not_reformat_an_already_valid_annotated_nest()
    {
        const string nest = "// plans ; weekend ;;\n// first, // a reason, retained\n// second // another reason\n//\n";
        var source = "// TITLE\n// --\n" + nest;
        var plan = TitleRuleRepair.Plan(DertParser.Parse(source));
        Assert.True(plan.IsReady);
        Assert.EndsWith(nest, plan.ProposedText);
        var result = DertParser.Parse(plan.ProposedText);
        var block = Assert.Single(result.Nodes.OfType<ReasoningBlockNode>());
        Assert.Equal(["first", "second"], block.Entries.Select(entry => entry.Value.Text));
        Assert.Equal(["a reason, retained", "another reason"], block.Entries.Select(entry => entry.Annotation!.Text));
        Assert.NotNull(block.Closing);
        Assert.DoesNotContain(result.Source.Lines, line => result.Source.Slice(line.ContentSpan).StartsWith(' '));
    }
}
