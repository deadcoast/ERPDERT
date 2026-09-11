using Dert.Core;
using Dert.Cli.Operations;
using Xunit;

namespace Dert.Tests;

public class NoteReferenceTests
{
    [Fact]
    public void Targets_find_distinct_named_items_with_their_actual_parent_and_exact_ranges()
    {
        const string source = "// HOW ; codex, claude ;; Keep either explanation, intact. //\nSTERT\n// similarities ; AI ;; codex, claude, codex //\nfin.";
        var document = DertParser.Parse(source);
        var index = new NoteReferenceIndex(document);
        var groups = index.ForNote(document.Nodes[0]);
        var codex = Assert.Single(groups, group => group.Origin.Value.Text == "codex");
        Assert.Equal(2, codex.Matches.Count);
        Assert.All(codex.Matches, match =>
        {
            Assert.Equal(NoteNameRole.ListedItem, match.Role);
            Assert.Equal(3, match.LineNumber);
            Assert.IsType<ChainNode>(match.Parent);
            Assert.Equal("codex", document.Source.Slice(match.Value.Span));
        });
        Assert.NotEqual(codex.Matches[0].Value.Span, codex.Matches[1].Value.Span);
        Assert.Equal("codex, claude", codex.Origin.RawField.Text);
        Assert.Equal(source.IndexOf("codex", StringComparison.Ordinal), codex.Origin.Value.Span.Start);
        Assert.Equal(source, document.Source.Text);
    }

    [Fact]
    public void Matching_excludes_same_note_substrings_prose_annotations_and_operation_arguments()
    {
        var document = DertParser.Parse("// HOW ; codex ;; codex stays in prose. //\n" +
            "// similarities ; AI ;; codex-local, other //\n// notes ; desk ;;\n// codex // codex\n//\n" +
            "// codex is ordinary prose\nSTERT ; update ;; codex\nfin.");
        var group = new NoteReferenceIndex(document).ForNote(document.Nodes[0]).Single(group => group.Origin.Value.Text == "codex");
        Assert.Empty(group.Matches);
        Assert.Empty(new NoteReferenceIndex(document).ForNote(document.Nodes[^1]));
    }

    [Fact]
    public void Unknown_content_names_and_targets_are_usable_without_registered_schemas()
    {
        var document = DertParser.Parse("// tomorrow maybe ; music ;; Keep the evening free. //\n// music ; tomorrow maybe ;; jazz //");
        var index = new NoteReferenceIndex(document);
        var groups = index.ForNote(document.Nodes[0]);
        Assert.Equal(2, groups.Count);
        Assert.Equal(NoteNameRole.Target, Assert.Single(groups[0].Matches).Role);
        Assert.Equal(NoteNameRole.Content, Assert.Single(groups[1].Matches).Role);
        Assert.Empty(DertOperationPlan.Build(document).Steps);
        Assert.Empty(document.Diagnostics);
    }

    [Theory]
    [InlineData("codex", "CODEX", true)]
    [InlineData("codex", "codex-local", false)]
    [InlineData("\"codex\"", "codex", false)]
    [InlineData("\"ACME, local\"", "\"ACME, LOCAL\"", true)]
    [InlineData("café", "cafe\u0301", false)]
    public void Matching_is_whole_written_name_without_decoding_or_normalizing(string name, string candidate, bool matches)
    {
        var document = DertParser.Parse($"// HOW ; {name} ;; reason //\n// why ; {candidate} ;; reason //");
        var groups = new NoteReferenceIndex(document).ForNote(document.Nodes[0]);
        var target = Assert.Single(groups, group => group.Origin.Role == NoteNameRole.Target);
        Assert.Equal(matches ? 1 : 0, target.Matches.Count);
    }

    [Fact]
    public void Empty_names_keep_their_positions_and_never_acquire_destinations()
    {
        var document = DertParser.Parse("// HOW ; a,,b ;; intact, prose //\n// similarities ; group ;; a,,b, //");
        var index = new NoteReferenceIndex(document);
        var empty = Assert.Single(index.ForNote(document.Nodes[0]), group => group.Origin.Value.Text.Length == 0);
        Assert.False(empty.Origin.IsComplete); Assert.Empty(empty.Matches);
        Assert.Equal(new TextSpan(11, 0), empty.Origin.Value.Span);
        Assert.Empty(document.Diagnostics);
    }

    [Fact]
    public void Unfinished_quoted_items_remain_visible_without_matching_each_other()
    {
        var document = DertParser.Parse("// similarities ; first ;; \"open\n// similarities ; second ;; \"open");
        var group = new NoteReferenceIndex(document).ForNote(document.Nodes[0]).Single(group => group.Origin.Role == NoteNameRole.ListedItem);
        Assert.Equal("\"open", group.Origin.Value.Text);
        Assert.False(group.Origin.IsComplete); Assert.Empty(group.Matches);
    }

    [Fact]
    public void A_large_note_collection_builds_one_usable_reference_index_without_a_long_pause()
    {
        var source = string.Join('\n', Enumerable.Range(1, 10000).Select(i => $"// HOW ; name {i} ;; My words. //"));
        var document = DertParser.Parse(source);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var index = new NoteReferenceIndex(document);
        watch.Stop();
        Assert.Equal(20000, index.Names.Count);
        Assert.Equal(9999, index.ForNote(document.Nodes[0]).First().Matches.Count);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"Building references paused for {watch.Elapsed.TotalSeconds:F1} seconds.");
    }

    [Fact]
    public void Foreign_equal_looking_notes_cannot_borrow_a_revision()
    {
        const string source = "// HOW ; music ;; Keep the note. //\n// why ; music ;; Keep another. //";
        var document = DertParser.Parse(source);
        Assert.Empty(new NoteReferenceIndex(document).ForNote(DertParser.Parse(source).Nodes[0]));
    }

    [Fact]
    public void Expanded_similarity_items_keep_annotations_and_inner_commas_out_of_name_splitting()
    {
        var document = DertParser.Parse("// HOW ; \"ACME, local\" ;; Keep the name. //\n// similarities ; groups ;;\n// \"ACME, local\", // codex is only annotation\n// ACME, local\n//");
        var groups = new NoteReferenceIndex(document).ForNote(document.Nodes[0]);
        var match = Assert.Single(groups.Single(group => group.Origin.Role == NoteNameRole.Target).Matches);
        Assert.Equal(3, match.LineNumber);
        Assert.Equal("\"ACME, local\"", match.Value.Text);
    }
}
