using System.Windows;
using Dert.Notes;
using Xunit;

namespace Dert.Notes.Tests;

public class ReasoningBoundaryTests
{
    [Fact]
    public void Deleting_the_owner_should_not_start_inspecting_an_expanded_neighbor() => ReasoningInspectionTests.Sta(window =>
    {
        const string first = "// old plans ; Saturday ;;\n// morning // Bring coffee.\n//\n";
        const string second = "// new plans ; Sunday ;;\n// evening // Bring music.\n//\n";
        window.Session.Load(first + second);
        Assert.True(window.Session.Reasoning.Begin());
        foreach (var fold in window.Session.Folds.AllFoldings) fold.IsFolded = false;
        window.Editor.Document.Remove(0, first.Length);
        ReasoningInspectionTests.Pump();
        Assert.Equal(second, window.Editor.Text);
        Assert.Null(window.Session.Reasoning.Owner);
        Assert.False(window.Session.Reasoning.Return());
    });

    [Fact]
    public void Deleting_from_inside_the_opener_should_not_adopt_the_adjacent_note() => ReasoningInspectionTests.Sta(window =>
    {
        const string first = "// old plans ; Saturday ;;\n// morning // Bring coffee.\n//\n";
        const string second = "// new plans ; Sunday ;;\n// evening // Bring music.\n//\n";
        window.Session.Load(first + second);
        Assert.True(window.Session.Reasoning.Begin());
        window.Editor.Document.Remove(1, first.Length);
        ReasoningInspectionTests.Pump();
        Assert.Equal(second, window.Editor.Text);
        Assert.Null(window.Session.Reasoning.Owner);
        Assert.False(window.Session.Reasoning.Return());
    });

    [Fact]
    public void Navigation_to_first_column_after_reasoning_should_end_inspection() => ReasoningInspectionTests.Sta(window =>
    {
        window.Session.Load("// plans ; Saturday ;;\n// morning // Bring coffee.\n//\nBack to the note.\n");
        window.Session.Reasoning.Begin();
        window.Session.Reasoning.EditLine(2);
        window.Editor.Select(window.Editor.Document.GetLineByNumber(4).Offset, 0);
        ReasoningInspectionTests.Pump();
        Assert.Null(window.Session.Reasoning.Owner);
        Assert.True(window.Session.Folds.AllFoldings.Single().IsFolded);
    });

    [Fact]
    public void Gutter_inspection_from_goodbye_should_show_reasoning_only() => ReasoningInspectionTests.Sta(window =>
    {
        window.Session.Load("STERT\nfin.\n// plans ; Saturday ;;\n// morning // Bring coffee.\n//\n");
        window.Editor.CaretOffset = window.Editor.Document.GetLineByNumber(2).Offset;
        ReasoningInspectionTests.Pump();
        Assert.Equal(Visibility.Visible, window.Goodbye.Visibility);
        Assert.True(window.Session.Reasoning.Begin(3));
        ReasoningInspectionTests.Pump();
        Assert.Equal(Visibility.Visible, window.ReasoningView.Visibility);
        Assert.Equal(Visibility.Collapsed, window.Goodbye.Visibility);
    });
}
