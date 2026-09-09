using Dert.Cli.Applications;
using Dert.Cli.Operations;
using Dert.Core;
using Xunit;

namespace Dert.Tests;

// The family IDs trace the design audit. These fixtures belong to the tests;
// no original note or documentation file is read during verification.
public class OriginalExampleCoverageTests
{
    [Theory]
    [InlineData("C01-title-rule.DERT")]
    [InlineData("contextual-notes.DERT")]
    [InlineData("C03-compact.DERT")]
    [InlineData("C04-decorated-opener.DERT")]
    [InlineData("C05-dercs-help.DERT")]
    [InlineData("C06-historical-hook.DERT")]
    [InlineData("C07-historical-line.DERT")]
    [InlineData("C08-loose-fragments.DERT")]
    [InlineData("C09-empty-action.DERT")]
    [InlineData("C10-active-sinker.DERT")]
    [InlineData("C11-free-hook.DERT")]
    [InlineData("C12-identity-prose.DERT")]
    [InlineData("mixed-structures.DERT")]
    public void Every_family_retains_exact_spelling_tokens_and_field_ranges(string fixture)
    {
        var source = SyntaxFixtures.Read(fixture);
        var document = DertParser.Parse(source);
        Assert.Equal(source, string.Concat(document.Tokens.Select(token => document.Source.Slice(token.Span))));
        foreach (var location in new DertStructureIndex(document).Locations)
        {
            var node = location.Node;
            Assert.InRange(node.Span.Start, 0, source.Length);
            Assert.InRange(node.Span.End, node.Span.Start, source.Length);
            var fields = node switch
            {
                ChainNode chain => Fields(chain.Header),
                StatementNode statement => Fields(statement.Header),
                ReasoningNoteNode note => Fields(note.Header),
                ReasoningBlockNode block => Fields(block.Header),
                NoteEntryNode entry => new[] { entry.Value, entry.Annotation }.OfType<TextField>(),
                NoteNode note => [note.Content],
                TextNode text => [text.Content],
                _ => []
            };
            foreach (var field in fields)
            {
                Assert.Equal(field.Text, document.Source.Slice(field.Span));
                Assert.InRange(field.Span.Start, node.Span.Start, node.Span.End);
                Assert.InRange(field.Span.End, field.Span.Start, node.Span.End);
            }
        }
        Assert.Equal(source, DertOperationPlan.Build(document).Document.Source.Text);
    }

    [Fact]
    public void C01_title_rule_uses_the_original_visible_width()
    {
        var document = Read("C01-title-rule.DERT");
        Assert.True(Assert.IsType<NoteNode>(document.Nodes[0]).IsTitle);
        Assert.IsType<RuleNode>(document.Nodes[1]);
        Assert.Empty(document.Diagnostics);
        Assert.Equal(TitleRepairState.Aligned, TitleRuleRepair.Plan(document).State);
        Assert.Equal(TitleRepairState.Aligned, TitleRuleRepair.Plan(DertParser.Parse("// TITLE\n// -----")).State);
    }

    [Fact]
    public void C02_original_reasoning_keeps_prose_and_its_own_nested_annotations()
    {
        var chain = Assert.IsType<ChainNode>(Assert.Single(Read("contextual-notes.DERT").Nodes));
        var notes = chain.Children.OfType<ReasoningNoteNode>().ToArray();
        Assert.Equal(2, notes.Length);
        Assert.Equal("codex, claude", notes[1].Header.Target!.Text);
        Assert.Equal("I dont care if it calls claudes installer, i just want it to be correct.", notes[1].Header.Payload!.Text);
        var block = Assert.Single(chain.Children.OfType<ReasoningBlockNode>());
        Assert.Equal(["possible_conflicts", "conflicts", "fixme"], block.Entries.Select(entry => entry.Value.Text));
        Assert.All(block.Entries, entry => { Assert.True(entry.HasComma); Assert.NotNull(entry.Annotation); });
        Assert.NotNull(block.Closing);
        Assert.Empty(DertOperationPlan.Build(Read("contextual-notes.DERT")).Steps);
    }

    [Fact]
    public void C03_original_compact_invocation_binds_update_without_inventing_a_closer()
    {
        var document = Read("C03-compact.DERT");
        var chain = Assert.IsType<ChainNode>(Assert.Single(document.Nodes));
        Assert.Null(chain.Terminator);
        Assert.Empty(chain.Children);
        Assert.Equal("ERPDERT", chain.Header.Target!.Text);
        var plan = DertOperationPlan.Build(document);
        Assert.True(plan.CanRun);
        Assert.Equal("DERT update codex", Assert.Single(plan.Steps).Command);
    }

    [Fact]
    public void C04_decorated_opener_retains_its_empty_hook_active_list_and_typed_ending()
    {
        var document = Read("C04-decorated-opener.DERT");
        var chain = Assert.IsType<ChainNode>(Assert.Single(document.Nodes));
        Assert.Equal(";", document.Source.Slice(chain.Header.Hook!.Value));
        Assert.Equal("", chain.Header.Target!.Text);
        Assert.Null(chain.Header.Line);
        Assert.Equal("fin.", document.Source.Slice(chain.Terminator!.Value));
        var list = Assert.Single(chain.Children.OfType<StatementNode>());
        Assert.Equal(3, list.Values.Count);
        Assert.True(list.Values[^1].HasComma);
        Assert.Contains(document.Diagnostics, finding => finding.Code == "DERT3003");
        Assert.False(DertOperationPlan.Build(document).CanRun);
        Assert.Equal("ƒ", FlorinGoodbye.Plan(document, chain.LineNumber + 8).Edit?.After);
    }

    [Fact]
    public void C05_DERCS_preserves_help_fields_and_explains_its_unimplemented_binding()
    {
        var document = Read("C05-dercs-help.DERT");
        var chain = Assert.IsType<ChainNode>(Assert.Single(document.Nodes));
        Assert.Equal("DERCS", chain.Header.Target!.Text);
        var command = Assert.Single(chain.Children.OfType<StatementNode>());
        Assert.Equal("command option", command.Header.Head.Text);
        Assert.Equal("variable", command.Header.Target!.Text);
        Assert.Null(command.Header.Line);
        var plan = DertOperationPlan.Build(document);
        Assert.False(plan.CanRun);
        Assert.Empty(plan.Steps);
        var finding = Assert.Single(plan.Findings, finding => finding.Code == "ERP6002");
        Assert.Contains("help", finding.Message);
        Assert.Contains("DERT --help", finding.Message);
        Assert.DoesNotContain("Use update or where", finding.Message);
    }

    [Fact]
    public void C06_historical_START_hook_stays_a_statement_without_a_silent_alias()
    {
        var document = Read("C06-historical-hook.DERT");
        Assert.Empty(document.Nodes.OfType<ChainNode>());
        Assert.Equal(["START", "command option"], document.Nodes.OfType<StatementNode>().Select(node => node.Header.Head.Text));
        Assert.False(DertOperationPlan.Build(document).CanRun);
    }

    [Fact]
    public void C07_historical_START_and_line_only_header_keep_their_distinct_reading()
    {
        var document = Read("C07-historical-line.DERT");
        Assert.Equal("START", Assert.IsType<TextNode>(document.Nodes[0]).Content.Text);
        var statement = Assert.IsType<StatementNode>(document.Nodes[1]);
        Assert.Equal("command option variable", statement.Header.Head.Text);
        Assert.Null(statement.Header.Hook);
        Assert.Null(statement.Header.Target);
        Assert.NotNull(statement.Header.Line);
        var entry = Assert.Single(statement.Values);
        Assert.Equal("string", entry.Value.Text);
        Assert.True(entry.HasComma);
        Assert.Contains("no comma on last string", entry.Annotation!.Text);
        Assert.Contains(document.Diagnostics, finding => finding.Code == "DERT3003");
    }

    [Fact]
    public void C08_loose_names_do_not_become_reasoning_payloads_or_application_requests()
    {
        var document = Read("C08-loose-fragments.DERT");
        var index = new DertStructureIndex(document);
        Assert.Equal(4, document.Nodes.OfType<TextNode>().Count());
        foreach (var line in new[] { 2, 3, 5, 6 }) Assert.Null(index.AtLine(line)!.Parent);
        var list = Assert.Single(document.Nodes.OfType<StatementNode>());
        Assert.Equal(3, list.Values.Count);
        Assert.Equal("support doctoring", list.Header.Head.Text);
        Assert.Empty(DertOperationPlan.Build(document).Steps);
    }

    [Fact]
    public void C09_empty_action_is_valid_only_for_an_implemented_enclosing_request()
    {
        var original = Read("C09-empty-action.DERT");
        var action = original.Nodes.OfType<StatementNode>().Last();
        Assert.Equal("commands", action.Header.Head.Text);
        Assert.Equal("updateme", action.Header.Target!.Text);
        Assert.Empty(action.Values);
        Assert.False(DertOperationPlan.Build(original).CanRun);
        var supported = DertOperationPlan.Build(DertParser.Parse("STERT ; update ;; codex\ncommands ; updateme ;;\nƒin."));
        Assert.True(supported.CanRun);
        Assert.Equal(ApplicationAction.UpdateRehearsal, Assert.Single(supported.Steps).Kind);
    }

    [Fact]
    public void C10_hook_line_and_expanded_sinker_keep_separate_fields()
    {
        var document = Read("C10-active-sinker.DERT");
        var chain = Assert.IsType<ChainNode>(Assert.Single(document.Nodes));
        var statement = Assert.IsType<StatementNode>(Assert.Single(chain.Children));
        Assert.Equal("command option", statement.Header.Head.Text);
        Assert.Equal("variable", statement.Header.Target!.Text);
        Assert.NotNull(statement.Header.Line);
        Assert.Equal("", statement.Header.Payload!.Text);
        Assert.Equal("string", Assert.Single(statement.Values).Value.Text);
        Assert.False(statement.Values[0].HasComma);
        Assert.Empty(document.Folds);
    }

    [Fact]
    public void C11_a_hook_alone_does_not_promote_a_free_note()
    {
        var document = Read("C11-free-hook.DERT");
        Assert.Equal("STERT ; starts the chain of command", Assert.IsType<NoteNode>(document.Nodes[0]).Content.Text);
        var reasoning = Assert.IsType<ReasoningNoteNode>(document.Nodes[1]);
        Assert.Equal("ERKER .dert", reasoning.Header.Payload!.Text);
        Assert.NotNull(reasoning.Closing);
        Assert.Empty(DertOperationPlan.Build(document).Steps);
    }

    [Fact]
    public void C12_original_identity_phrases_are_source_fields_and_prose()
    {
        var document = Read("C12-identity-prose.DERT");
        Assert.StartsWith("PRERPRYERT ERY", Assert.IsType<ReasoningNoteNode>(document.Nodes[0]).Header.Head.Text);
        Assert.Contains("TONGUE IN CHEEK", Assert.IsType<NoteNode>(document.Nodes[1]).Content.Text);
        var plan = DertOperationPlan.Build(document);
        Assert.Empty(plan.Steps);
        Assert.True(plan.CanRun);
    }

    [Fact]
    public void C13_mixed_family_composition_retains_complete_chains_and_detached_fragments()
    {
        var document = Read("mixed-structures.DERT");
        var chains = document.Nodes.OfType<ChainNode>().ToArray();
        Assert.Equal(3, chains.Length);
        Assert.All(chains, chain => Assert.NotNull(chain.Terminator));
        Assert.Equal(4, document.Nodes.OfType<TextNode>().Count());
        Assert.Equal(3, Assert.Single(document.Nodes.OfType<StatementNode>()).Values.Count);
        Assert.False(DertOperationPlan.Build(document).CanRun);
        Assert.Empty(DertOperationPlan.Build(document).Steps);
    }

    private static DertDocument Read(string fixture) => DertParser.Parse(SyntaxFixtures.Read(fixture));
    private static IEnumerable<TextField> Fields(Header header) => new[] { header.Head, header.Target, header.Payload }.OfType<TextField>();
}
