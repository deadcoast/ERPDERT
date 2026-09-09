using Dert.Core;
using Xunit;

namespace Dert.Tests;

public class ListCompletionTests
{
    [Theory]
    [InlineData("\"apples,", ListAction.Finish, null)]
    [InlineData("\"apples", ListAction.Add, "pear")]
    [InlineData("apples,", ListAction.Add, "\"pear,")]
    [InlineData("\"apples\\\",", ListAction.Finish, null)]
    public void An_unfinished_quote_never_authorizes_a_list_punctuation_edit(string last, ListAction action, string? item)
    {
        var source = "items ; weekend ;;\n" + last;
        var plan = ListCompletion.Plan(DertParser.Parse(source), 2, action, item);
        Assert.False(plan.IsReady);
        Assert.Contains("quoted", plan.Explanation);
        Assert.Equal(source, plan.ProposedText);
    }

    [Fact]
    public void Ambiguous_unquoted_double_comma_is_preserved_for_a_deliberate_literal_edit()
    {
        const string source = "items ; weekend ;;\nsnacks,,";
        var plan = ListCompletion.Plan(DertParser.Parse(source), 2, ListAction.Finish);
        Assert.False(plan.IsReady);
        Assert.Equal(source, plan.ProposedText);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void Finish_removes_only_the_active_comma_and_preserves_the_rest(string newline)
    {
        var source = "items ; weekend ;;" + newline + "snacks \t,  // Apples, pears," + newline + newline + "// why ; rest ;; Keep commas, here. //";
        var plan = ListCompletion.Plan(DertParser.Parse(source), 2, ListAction.Finish);
        Assert.True(plan.IsReady, plan.Explanation);
        Assert.Equal(",", plan.Edit!.Before);
        Assert.Equal("", plan.Edit.After);
        Assert.Equal(source.Replace("snacks \t,", "snacks \t", StringComparison.Ordinal), plan.ProposedText);
        Assert.DoesNotContain(DertParser.Parse(plan.ProposedText).Diagnostics, finding => finding.Code == "DERT3003");
    }

    [Theory]
    [InlineData("items ; weekend ;;\nsnacks,", 1)]
    [InlineData("items ; weekend ;;\nsnacks,", 2)]
    [InlineData("STERT\nitems ; weekend ;;\nsnacks,\nfin.", 3)]
    public void Add_is_bound_to_the_selected_active_owner(string source, int at)
    {
        var plan = ListCompletion.Plan(DertParser.Parse(source), at, ListAction.Add, "thermos // Bring tea.");
        Assert.True(plan.IsReady, plan.Explanation);
        var index = new DertStructureIndex(DertParser.Parse(plan.ProposedText));
        var statement = Assert.IsType<StatementNode>(index.AtLine(plan.StatementLine)!.Node);
        Assert.Equal(2, statement.Values.Count);
        Assert.Equal("thermos", statement.Values[1].Value.Text);
        Assert.Equal("Bring tea.", statement.Values[1].Annotation!.Text);
        Assert.Equal(source, plan.Source);
    }

    [Fact]
    public void Repeated_addition_previews_the_required_comma_without_moving_annotations_or_spacing()
    {
        const string source = "items ; weekend ;;\ncoffee // Keep it hot.\n\n// note\nnext ; stop ;; later";
        var plan = ListCompletion.Plan(DertParser.Parse(source), 2, ListAction.Add, "book");
        Assert.True(plan.IsReady, plan.Explanation);
        Assert.Equal("items ; weekend ;;\ncoffee, // Keep it hot.\nbook\n\n// note\nnext ; stop ;; later", plan.ProposedText);
        Assert.Equal(2, plan.Edits.Count);
    }

    [Theory]
    [InlineData("// note,", 1)]
    [InlineData("ordinary prose,", 1)]
    [InlineData("// plans ; weekend ;;\n// maybe,\n//", 2)]
    [InlineData("items ; weekend ;; \"inline,\"", 1)]
    [InlineData("items ; weekend ;;\nsnacks,\n\n// free note", 4)]
    public void Notes_prose_inline_payloads_and_unowned_lines_are_not_list_repairs(string source, int at)
    {
        var plan = ListCompletion.Plan(DertParser.Parse(source), at, ListAction.Finish);
        Assert.False(plan.IsReady);
        Assert.Equal(source, plan.ProposedText);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("one\ntwo")]
    [InlineData("// a note")]
    [InlineData("next ; owner ;;")]
    [InlineData("STERT")]
    [InlineData("fin.")]
    [InlineData(", // missing value")]
    public void Addition_requires_one_actual_item_without_inventing_grammar(string item)
    {
        const string source = "items ; weekend ;;\nsnacks,";
        var plan = ListCompletion.Plan(DertParser.Parse(source), 2, ListAction.Add, item);
        Assert.False(plan.IsReady);
        Assert.Equal(source, plan.ProposedText);
    }

    [Fact]
    public void Quoted_punctuation_and_literal_commas_survive_completion()
    {
        const string source = "items ; weekend ;;\n\"a ;; b // c,\", // keep, this,";
        var plan = ListCompletion.Plan(DertParser.Parse(source), 2, ListAction.Finish);
        Assert.True(plan.IsReady, plan.Explanation);
        Assert.Equal("items ; weekend ;;\n\"a ;; b // c,\" // keep, this,", plan.ProposedText);
        Assert.False(ListCompletion.Plan(DertParser.Parse(plan.ProposedText), 2, ListAction.Finish).IsReady);
    }

    [Fact]
    public void A_trailing_comma_on_the_new_item_cannot_absorb_existing_prose()
    {
        const string source = "items ; weekend ;;\nsnacks\nmy separate thought";
        var plan = ListCompletion.Plan(DertParser.Parse(source), 2, ListAction.Add, "thermos,");
        Assert.False(plan.IsReady);
        Assert.Contains("following", plan.Explanation);
        Assert.Equal(source, plan.ProposedText);
    }

    [Fact]
    public void A_new_continuation_can_remain_unfinished_before_a_separate_header()
    {
        const string source = "items ; weekend ;;\nsnacks,\nnext ; heading ;; value";
        var plan = ListCompletion.Plan(DertParser.Parse(source), 2, ListAction.Add, "thermos,");
        Assert.True(plan.IsReady, plan.Explanation);
        var parsed = DertParser.Parse(plan.ProposedText);
        Assert.Contains(parsed.Diagnostics, finding => finding.Code == "DERT3003" && finding.LineNumber == 3);
        Assert.Equal("next", Assert.IsType<StatementNode>(new DertStructureIndex(parsed).AtLine(4)!.Node).Header.Head.Text);
    }
}
