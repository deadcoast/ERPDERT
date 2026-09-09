using Dert.Core;
using Dert.Mer;
using Dert.Cli;
using Spectre.Console;
using Xunit;

namespace Dert.Tests;

public sealed class UnicodeTitleTests
{
    [Theory]
    [InlineData("café", 4)]
    [InlineData("cafe\u0301", 4)]
    [InlineData("日本語", 6)]
    [InlineData("한글", 4)]
    [InlineData("שלום", 4)]
    [InlineData("مرحبا", 5)]
    [InlineData("ＡＢ", 4)]
    [InlineData("中文 𠀀", 7)]
    [InlineData("👩‍💻", 2)]
    [InlineData("👨‍👩‍👧‍👦", 2)]
    [InlineData("👍🏽", 2)]
    [InlineData("🇨🇦", 2)]
    [InlineData("1️⃣", 2)]
    [InlineData("☀", 1)]
    [InlineData("☀️", 2)]
    [InlineData("♥︎", 1)]
    [InlineData("♥️", 2)]
    [InlineData("Ω · café", 8)]
    [InlineData("café 日本語 👩‍💻", 14)]
    public void Title_lint_repair_and_mer_agree_without_rewriting_the_words(string title, int width)
    {
        var source = "\uFEFF// " + title + "\r\n// -------------------\n// Keep my spelling.\r";
        var document = DertParser.Parse(source);
        var plan = TitleRuleRepair.Plan(document);
        Assert.Equal(TitleRepairState.Ready, plan.State);
        Assert.Equal(width, plan.Width);
        Assert.Equal(width, TerminalText.Width(title));
        Assert.Equal("\uFEFF// " + title + "\r\n// " + new string('-', width) + "\n// Keep my spelling.\r", plan.ProposedText);
        Assert.DoesNotContain(document.Diagnostics, diagnostic => diagnostic.Code == "DERT3004");
        Assert.Equal(TitleRepairState.Aligned, TitleRuleRepair.Plan(DertParser.Parse(plan.ProposedText)).State);
        Assert.Empty(DertParser.Parse(plan.ProposedText).Diagnostics);
    }

    [Fact]
    public void Unicode_title_cli_preview_saved_repair_and_undo_keep_the_actual_graphemes()
    {
        const string source = "\uFEFF// cafe\u0301 日本語 👩‍💻\r\n// --\n// My words stay.";
        using var file = new ScratchDocument(source);
        var before = File.ReadAllBytes(file.Path);
        var preview = Run(["repair", file.Path, "--plain"]);
        Assert.Equal(0, preview.Exit);
        Assert.Contains("cafe\u0301 日本語 👩‍💻", preview.Output);
        Assert.Contains("// --------------", preview.Output);
        Assert.Equal(before, File.ReadAllBytes(file.Path));
        Assert.Equal(0, Run(["repair", file.Path, "--plain"], true).Exit);
        Assert.Equal(source.Replace("// --\n", "// --------------\n", StringComparison.Ordinal), File.ReadAllText(file.Path, System.Text.Encoding.UTF8).Insert(0, "\uFEFF"));
        Assert.Equal(0, Run(["undo", file.Path, "--plain"], true).Exit);
        Assert.Equal(before, File.ReadAllBytes(file.Path));
    }

    [Theory]
    [InlineData("a\tb")]
    [InlineData("a\u001bb")]
    [InlineData("a\u202Eb")]
    [InlineData("\u0301")]
    [InlineData("\u200D")]
    public void Unmeasurable_title_text_is_retained_with_a_specific_reason(string title)
    {
        var source = "// " + title + "\n// --";
        var plan = TitleRuleRepair.Plan(DertParser.Parse(source));
        Assert.Equal(TitleRepairState.Unavailable, plan.State);
        Assert.Equal(source, plan.ProposedText);
        Assert.Contains("width", plan.Explanation);
    }

    [Fact]
    public void Unfinished_surrogate_in_an_editor_buffer_is_not_replaced_by_a_width_guess() =>
        Unmeasurable_title_text_is_retained_with_a_specific_reason(new string((char)0xD800, 1));

    private static (int Exit, string Output) Run(string[] args, bool apply = false)
    {
        var output = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings { Ansi = AnsiSupport.No,
            Out = new AnsiConsoleOutput(output), Interactive = InteractionSupport.No });
        console.Profile.Width = 240;
        return (CliApplication.Run(args, console, apply ? _ => true : null), output.ToString());
    }
}
