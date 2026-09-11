using Dert.Cli.Operations;
using Dert.Core;
using Xunit;

namespace Dert.Tests;

public class ContextReadingTests
{
    [Fact]
    public void HOW_retains_the_entire_original_shaped_explanation_and_raw_named_targets()
    {
        const string source = "// HOW ; codex, claude ;; I want either installer, if it is correct. //";
        var document = DertParser.Parse(source);
        var note = Assert.IsType<ReasoningNoteNode>(Assert.Single(document.Nodes));
        var reading = ContextReading.Read(document, note);
        Assert.NotNull(reading);
        Assert.Equal(ContextReader.How, reading.Reader);
        Assert.Same(note.Header, reading.RawHeader);
        Assert.Equal("codex, claude", reading.RawHeader.Target!.Text);
        Assert.Equal("I want either installer, if it is correct.", reading.RawHeader.Payload!.Text);
        Assert.Empty(reading.Items); Assert.Empty(reading.Issues);
        Assert.Equal(source, document.Source.Text);
    }

    [Fact]
    public void Similarities_has_exact_quote_aware_items_separators_and_duplicate_occurrences()
    {
        const string source = "// similarities ; AI ;;  codex, \"ACME, local\", codex , \"use \\\"quotes\\\", too\" //";
        var document = DertParser.Parse(source);
        var note = Assert.IsType<ReasoningNoteNode>(Assert.Single(document.Nodes));
        var reading = ContextReading.Read(document, note);
        Assert.NotNull(reading);
        Assert.Equal(ContextReader.Similarities, reading.Reader);
        Assert.Equal(new[] { "codex", "\"ACME, local\"", "codex", "\"use \\\"quotes\\\", too\"" }, reading.Items.Select(item => item.Value.Text));
        Assert.NotEqual(reading.Items[0].Value.Span, reading.Items[2].Value.Span);
        Assert.All(reading.Items, item => Assert.Equal(item.Value.Text, document.Source.Slice(item.Value.Span)));
        Assert.Equal(note.Header.Payload!.Text, string.Concat(reading.Items.Select(item => document.Source.Slice(item.SourceSpan) +
            (item.Separator is { } separator ? document.Source.Slice(separator) : ""))));
        Assert.Empty(reading.Issues);
    }

    [Theory]
    [InlineData("similarities", ContextReader.Similarities)]
    [InlineData("SIMILARITIES", ContextReader.Similarities)]
    [InlineData("how", ContextReader.How)]
    [InlineData("why", ContextReader.Written)]
    [InlineData("similarities maybe", ContextReader.Written)]
    [InlineData("\"similarities\"", ContextReader.Written)]
    public void Only_explicit_note_heads_select_a_reader(string head, ContextReader expected)
    {
        var document = DertParser.Parse($"// {head} ; target ;; a, b, c //");
        var reading = ContextReading.Read(document, Assert.Single(document.Nodes));
        Assert.NotNull(reading); Assert.Equal(expected, reading.Reader);
        if (expected != ContextReader.Similarities) Assert.Empty(reading.Items);
        Assert.Equal("a, b, c", reading.RawHeader.Payload!.Text);
        Assert.Empty(document.Diagnostics);
    }

    [Fact]
    public void Expanded_similarities_keeps_each_authored_entry_and_its_own_annotation()
    {
        const string source = "// similarities ; names ;;\r\n// ACME, local, // this whole name, together\r\n// codex // an unfinished \"annotation\r\n//\r\n";
        var document = DertParser.Parse(source);
        var block = Assert.IsType<ReasoningBlockNode>(Assert.Single(document.Nodes));
        var reading = ContextReading.Read(document, block);
        Assert.NotNull(reading);
        Assert.Equal(ContextReader.Similarities, reading.Reader);
        Assert.Equal(new[] { "ACME, local", "codex" }, reading.Items.Select(item => item.Value.Text));
        Assert.Same(block.Entries[0].Value, reading.Items[0].Value);
        Assert.Same(block.Entries[1].Annotation, reading.Items[1].Annotation);
        Assert.Empty(reading.Issues);
    }

    [Fact]
    public void An_unfinished_expanded_quote_keeps_its_trailing_comma_in_the_item()
    {
        const string source = "// similarities ; names ;;\n// \"still writing,\n//";
        var document = DertParser.Parse(source);
        var block = Assert.IsType<ReasoningBlockNode>(Assert.Single(document.Nodes));
        var reading = ContextReading.Read(document, block)!;
        var item = Assert.Single(reading.Items);
        Assert.Equal("\"still writing,", item.Value.Text);
        Assert.Equal(item.Value.Text, document.Source.Slice(item.Value.Span));
        Assert.Null(item.Separator);
        Assert.Contains(reading.Issues, issue => issue.Kind == ContextIssueKind.OpenQuote);
        Assert.Equal("\"still writing", block.Entries[0].Value.Text);
        Assert.True(block.Entries[0].HasComma);
        Assert.Equal(source, document.Source.Text);
    }

    [Fact]
    public void HOW_nests_remain_written_entries_instead_of_becoming_a_joined_sentence()
    {
        var document = DertParser.Parse("// HOW ; note ;;\n// first, // context\n// second\n//");
        var reading = ContextReading.Read(document, Assert.Single(document.Nodes));
        Assert.NotNull(reading); Assert.Equal(ContextReader.Written, reading.Reader);
        Assert.Equal(new[] { "first", "second" }, reading.Items.Select(item => item.Value.Text));
    }

    [Fact]
    public void Empty_places_are_retained_without_promoting_reader_issues_to_parser_errors()
    {
        const string source = "// similarities ; names ;; a,,b, //";
        var document = DertParser.Parse(source);
        var reading = ContextReading.Read(document, Assert.Single(document.Nodes));
        Assert.NotNull(reading);
        Assert.Equal(new[] { "a", "", "b", "" }, reading.Items.Select(item => item.Value.Text));
        Assert.Equal(2, reading.Issues.Count);
        Assert.Contains("2 empty", reading.Summary);
        Assert.Empty(document.Diagnostics);
        Assert.All(reading.Items, item => Assert.Equal(item.Value.Text, document.Source.Slice(item.Value.Span)));
    }

    [Theory]
    [InlineData("// similarities ; names ;; a, \"still, writing //")]
    [InlineData("// similarities ; names ;; a, b")]
    [InlineData("// similarities ; names ;; //")]
    public void Incomplete_context_keeps_its_source_and_explains_what_is_still_open(string source)
    {
        var document = DertParser.Parse(source);
        var reading = ContextReading.Read(document, Assert.Single(document.Nodes));
        Assert.NotNull(reading); Assert.NotEmpty(reading.Issues);
        Assert.Equal(source, document.Source.Text);
        Assert.Equal(source, string.Concat(document.Tokens.Select(token => document.Source.Slice(token.Span))));
    }

    [Theory]
    [InlineData("similarities ; AI ;; codex, claude")]
    [InlineData("// ordinary prose, has, commas")]
    public void Statements_and_free_notes_do_not_acquire_a_context_reader(string source)
    {
        var document = DertParser.Parse(source);
        Assert.Null(ContextReading.Read(document, Assert.Single(document.Nodes)));
    }

    [Fact]
    public void Named_context_readers_do_not_choose_or_execute_application_operations()
    {
        const string source = "// similarities ; AI ;; codex, claude //\n// HOW ; codex ;; run anything, whatever //";
        var document = DertParser.Parse(source);
        foreach (var note in document.Nodes) Assert.NotNull(ContextReading.Read(document, note));
        var plan = DertOperationPlan.Build(document);
        Assert.Empty(plan.Steps); Assert.Empty(plan.Findings);
        Assert.Equal(source, document.Source.Text);
    }
}
