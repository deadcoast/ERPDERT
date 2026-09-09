using Dert.Cli;
using Dert.Cli.Motion;
using Dert.Core;
using Spectre.Console;
using Xunit;

namespace Dert.Tests;

public class LiteralReadingTests
{
    [Theory]
    [InlineData("\"keep ; ;; // and comma,\"")]
    [InlineData("C:\\Users\\Ryan\\")]
    [InlineData("\"still writing, // words")]
    public void CLI_and_MER_use_the_same_literal_explanation_and_keep_file_bytes(string item)
    {
        using var file = new ScratchDocument("items ; note ;;\n" + item);
        var bytes = File.ReadAllBytes(file.Path);
        var parsed = DertParser.Parse(File.ReadAllText(file.Path));
        var index = new DertStructureIndex(parsed);
        var explanation = DertPunctuation.Detail(index.AtLine(2)!.Node);
        Assert.NotNull(explanation);
        var output = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings { Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors, Out = new AnsiConsoleOutput(output) });
        console.Profile.Width = 220;
        Assert.Equal(0, CliApplication.Run(["inspect", file.Path, "--at", "2", "--plain"], console));
        Assert.Contains(explanation, output.ToString());
        Assert.Contains(StructureConnection.Describe(index, 2).Lines, line => line.Text == explanation);
        Assert.Equal(bytes, File.ReadAllBytes(file.Path));
    }

    [Fact]
    public void Annotation_quotes_do_not_describe_the_value_as_unfinished()
    {
        var parsed = DertParser.Parse("ordinary words // a \"still writing annotation");
        Assert.Null(DertPunctuation.Detail(Assert.Single(parsed.Nodes)));
        var plan = LiteralText.Plan(parsed, new(0, parsed.Source.Text.Length));
        Assert.False(plan.IsReady);
        Assert.Contains("annotation", plan.Explanation);
    }

    [Theory]
    [InlineData("a \"still writing annotation")]
    [InlineData("the \"quoted\" explanation")]
    [InlineData("backslash \\")]
    public void A_plain_value_can_be_quoted_independently_of_its_annotation(string annotation)
    {
        var source = "hello, // " + annotation;
        var plan = LiteralText.Plan(DertParser.Parse(source), new(0, 6));
        Assert.True(plan.IsReady, plan.Explanation);
        Assert.Equal("\"hello,\" // " + annotation, plan.ProposedText);
        var before = Assert.Single(DertParser.Parse(source).Highlights, highlight => highlight.Role == SemanticRole.ContextNote);
        var after = Assert.Single(DertParser.Parse(plan.ProposedText).Highlights, highlight => highlight.Role == SemanticRole.ContextNote);
        Assert.Equal(source.Substring(before.Span.Start, before.Span.Length), plan.ProposedText.Substring(after.Span.Start, after.Span.Length));
    }
}
