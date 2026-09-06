using Dert.Core;
using Xunit;

namespace Dert.Tests;

public class TitleRuleRepairTests
{
    [Theory]
    [InlineData("// TITLE\n// --\nwords", "// TITLE\n// -----\nwords")]
    [InlineData("// TITLE\n// --------", "// TITLE\n// -----")]
    [InlineData("// TITLE\r\n---\r\nwords", "// TITLE\r\n// -----\r\nwords")]
    [InlineData("// TITLE\nwords", "// TITLE\n// -----\nwords")]
    [InlineData("// TITLE\n\nwords", "// TITLE\n// -----\n\nwords")]
    [InlineData("// TITLE", "// TITLE\r\n// -----")]
    [InlineData("// TITLE\r", "// TITLE\r// -----\r")]
    [InlineData("\uFEFF// TITLE\r\nwords\n", "\uFEFF// TITLE\r\n// -----\r\nwords\n")]
    public void One_exact_edit_preserves_everything_outside_the_title_rule(string before, string after)
    {
        var plan = TitleRuleRepair.Plan(DertParser.Parse(before));
        Assert.Equal(TitleRepairState.Ready, plan.State);
        Assert.Equal(after, plan.ProposedText);
        Assert.Equal(after, plan.ApplyTo(before));
        Assert.Equal(before, plan.Source);
        Assert.Equal(before.Substring(plan.Edit!.Span.Start, plan.Edit.Span.Length), plan.Edit.Before);
        Assert.Equal(TitleRepairState.Aligned, TitleRuleRepair.Plan(DertParser.Parse(after)).State);
    }

    [Fact]
    public void A_short_rule_provides_a_local_width_explanation_and_exact_old_text()
    {
        var plan = TitleRuleRepair.Plan(DertParser.Parse("// TITLE\n// --\nwords"));
        Assert.Equal(1, plan.TitleLine);
        Assert.Equal(2, plan.RuleLine);
        Assert.Equal(5, plan.Width);
        Assert.Equal("// --", plan.Edit!.Before);
        Assert.Equal("// -----", plan.Edit.After);
        Assert.Contains("3", plan.Explanation);
        Assert.Contains("short", plan.Explanation);
    }

    [Theory]
    [InlineData("// TITLE\n// -----", TitleRepairState.Aligned)]
    [InlineData("STERT\n// a note\nƒin.", TitleRepairState.NoTitle)]
    [InlineData("// HOW ; title ;; reason //", TitleRepairState.NoTitle)]
    [InlineData("", TitleRepairState.NoTitle)]
    [InlineData("// café\n// --", TitleRepairState.Unavailable)]
    [InlineData("// first\tsecond", TitleRepairState.Unavailable)]
    public void Non_candidates_remain_unchanged_and_explain_their_state(string text, TitleRepairState state)
    {
        var plan = TitleRuleRepair.Plan(DertParser.Parse(text));
        Assert.Equal(state, plan.State);
        Assert.Equal(text, plan.ProposedText);
        Assert.Null(plan.Edit);
        Assert.NotEmpty(plan.Explanation);
    }

    [Fact]
    public void Applying_a_preview_to_different_source_is_rejected()
    {
        var plan = TitleRuleRepair.Plan(DertParser.Parse("// TITLE"));
        Assert.Throws<InvalidOperationException>(() => plan.ApplyTo("// OTHER"));
    }

    [Fact]
    public void Original_reasoning_is_untouched_when_a_title_rule_is_inserted_above_it()
    {
        var original = OriginalSyntaxTests.OriginalExample();
        var plan = TitleRuleRepair.Plan(DertParser.Parse("// TITLE\n" + original));
        Assert.Equal("// TITLE\n// -----\n" + original, plan.ProposedText);
    }
}
