using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Dert.Core;
using Xunit;

namespace Dert.Notes.Tests;

public class ReferenceAuthoringTests
{
    private const string Source = "// KEEP THE CONNECTION\r\n// -------------------\r\n\r\n// WHY ; codex ;; Start from these words. //\r\n// similarities ; tools ;; codex, \"ACME, local\", codex //\r\n// HOW ; codex ;; Keep this explanation, whole. //\r\n";

    [Fact]
    public void Following_the_second_duplicate_selects_that_occurrence_and_escape_returns_to_origin() => ReasoningInspectionTests.Sta(window =>
    {
        Start(window); var caret = window.Editor.CaretOffset;
        Follow(window, 5, 50);
        Assert.Equal("codex", window.Editor.SelectedText);
        Assert.Equal(Source.IndexOf(", codex //", StringComparison.Ordinal) + 2, window.Editor.SelectionStart);
        Assert.Equal(5, window.Session.Reasoning.Owner!.LineNumber);
        NotesWindow.ReturnToNoteCommand.Execute(null, window.Editor); ReasoningInspectionTests.Pump();
        Assert.Equal(caret, window.Editor.CaretOffset); Assert.Null(window.Session.Reasoning.Owner);
        Assert.Equal(Source, window.Editor.Text); Assert.False(window.Editor.CanUndo);
    });

    [Fact]
    public void Multi_hop_visit_keeps_original_return_anchor_after_editing_another_note() => ReasoningInspectionTests.Sta(window =>
    {
        Start(window); var origin = window.Editor.CaretOffset;
        Follow(window, 5, 50); Follow(window, 6, 10);
        window.Editor.TextArea.PerformTextInput("codex-local");
        NotesWindow.ReturnToNoteCommand.Execute(null, window.Editor); ReasoningInspectionTests.Pump();
        Assert.Equal(origin, window.Editor.CaretOffset);
        Assert.Contains("// HOW ; codex-local ;;", window.Editor.Text);
        window.Session.Undo(); Assert.Equal(Source, window.Editor.Text);
    });

    [Fact]
    public void A_reference_from_an_old_revision_cannot_visit_replacement_text() => ReasoningInspectionTests.Sta(window =>
    {
        Start(window); var button = Match(window, 5, 50);
        window.Editor.Document.Insert(0, "\n");
        var before = window.Editor.SelectionStart;
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(before, window.Editor.SelectionStart);
        Assert.Equal(5, window.Session.Reasoning.Owner!.LineNumber);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Removing_a_visited_note_retains_return_without_adopting_its_neighbor(bool replaceOpener) => ReasoningInspectionTests.Sta(window =>
    {
        Start(window); var origin = window.Editor.CaretOffset;
        Follow(window, 5, 50);
        var visited = window.Editor.Document.GetLineByNumber(5);
        if (replaceOpener) window.Editor.Document.Replace(visited.Offset, 2, "words");
        else window.Editor.Document.Remove(visited.Offset, visited.TotalLength);
        window.UpdateLayout(); ReasoningInspectionTests.Pump();
        Assert.Null(window.Session.Reasoning.Owner);
        Assert.Equal(Visibility.Visible, window.ReasoningView.Visibility);
        ReasoningInspectionTests.Capture(window, $"references-removed-{replaceOpener}");
        FindButton(window, "Return to note").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        ReasoningInspectionTests.Pump();
        Assert.Equal(origin, window.Editor.CaretOffset);
        window.Session.Undo(); Assert.Equal(Source, window.Editor.Text);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Pending_literal_proposal_holds_reference_buttons_and_view_switch(bool toggle) => ReasoningInspectionTests.Sta(window =>
    {
        Start(window); var old = toggle ? FindButton(window, "Read context") : Match(window, 5, 50);
        window.Session.Reasoning.EditLine(4);
        window.Literals.Begin(); Assert.True(window.Literals.IsReviewing);
        var selection = window.Editor.SelectionStart; var length = window.Editor.SelectionLength;
        old.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(Visibility.Collapsed, window.ReasoningView.Visibility);
        Assert.Equal(selection, window.Editor.SelectionStart); Assert.Equal(length, window.Editor.SelectionLength);
        Assert.True(window.Literals.IsReviewing); Assert.Equal(Source, window.Editor.Text);
    });

    [Theory]
    [InlineData(1180)]
    [InlineData(780)]
    public void References_render_with_parent_meaning_and_return_within_the_viewport(int width) => ReasoningInspectionTests.Sta(window =>
    {
        window.Width = width; Start(window); window.UpdateLayout(); ReasoningInspectionTests.Pump();
        var back = FindButton(window, "Return to note");
        DependencyObject? ancestor = back;
        while (ancestor is not null && ancestor is not ScrollViewer) ancestor = System.Windows.Media.VisualTreeHelper.GetParent(ancestor);
        var scroll = Assert.IsType<ScrollViewer>(ancestor);
        Assert.True(back.TranslatePoint(new(0, back.ActualHeight), window).Y <= scroll.TranslatePoint(new(0, scroll.ActualHeight), window).Y);
        Assert.True(window.Editor.ActualHeight > 150);
        Assert.Equal(3, ReasoningInspectionTests.Descendants<Button>(window.ReasoningView).Count(button => AutomationProperties.GetName(button).StartsWith("Follow codex /", StringComparison.Ordinal)));
        ReasoningInspectionTests.Capture(window, $"references-{width}");
    });

    [Fact]
    public void Many_destinations_are_paged_and_the_last_match_remains_reachable() => ReasoningInspectionTests.Sta(window =>
    {
        window.Session.Load("// HOW ; codex ;; Start here. //\n" + string.Join('\n', Enumerable.Range(1, 20).Select(i => $"// note {i} ; codex ;; words //")));
        window.Session.Reasoning.Begin(); FindButton(window, "References").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.UpdateLayout(); ReasoningInspectionTests.Pump();
        Assert.InRange(ReasoningInspectionTests.Descendants<Button>(window.ReasoningView).Count(button => AutomationProperties.GetName(button).StartsWith("Follow ", StringComparison.Ordinal)), 1, 6);
        for (var page = 0; page < 3; page++)
        {
            FindButton(window, "Next places").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            window.UpdateLayout(); ReasoningInspectionTests.Pump();
        }
        var final = ReasoningInspectionTests.Descendants<Button>(window.ReasoningView).Single(button => AutomationProperties.GetName(button).StartsWith("Follow codex / line 21,", StringComparison.Ordinal));
        final.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(21, window.Session.Reasoning.Owner!.LineNumber); Assert.Equal("codex", window.Editor.SelectedText);
        NotesWindow.ReturnToNoteCommand.Execute(null, window.Editor); ReasoningInspectionTests.Pump();
        Assert.Equal(1, window.Editor.TextArea.Caret.Line); Assert.False(window.Editor.CanUndo);
    });

    [Fact]
    public void Repeated_visits_and_exact_edit_survive_save_reopen_without_stale_destinations() => ReasoningInspectionTests.Sta(window =>
    {
        var folder = Path.Combine(Path.GetTempPath(), "dert-references-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "references [日本語].DERT");
            var source = Source.Replace("// HOW", "\n// HOW", StringComparison.Ordinal);
            var encoding = new System.Text.UTF8Encoding(true);
            File.WriteAllText(path, source, encoding); var before = File.ReadAllBytes(path);
            window.LoadPath(path);
            for (var visit = 0; visit < 3; visit++)
            {
                OpenReferences(window, 4); Follow(window, 5, 50);
                Assert.True(window.Session.Reasoning.Return()); ReasoningInspectionTests.Pump();
                Assert.Equal(4, window.Editor.TextArea.Caret.Line);
            }
            Assert.False(window.Session.IsDirty); Assert.False(window.Editor.CanUndo);
            Assert.True(window.SaveTo()); Assert.Equal(before, File.ReadAllBytes(path));
            OpenReferences(window, 4); var old = Match(window, 5, 50); Follow(window, 5, 50);
            window.Editor.TextArea.PerformTextInput("local copy");
            Assert.True(window.SaveTo());
            var expected = source.Replace(", codex //", ", local copy //", StringComparison.Ordinal);
            Assert.Equal(encoding.GetPreamble().Concat(encoding.GetBytes(expected)), File.ReadAllBytes(path));
            window.LoadPath(path); Assert.False(window.Session.Reasoning.CanReturn);
            var caret = window.Editor.CaretOffset;
            old.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(caret, window.Editor.CaretOffset); Assert.Equal(expected, window.Editor.Text);
            OpenReferences(window, 4);
            Assert.DoesNotContain(ReasoningInspectionTests.Descendants<Button>(window.ReasoningView), button =>
                AutomationProperties.GetName(button).StartsWith("Follow codex / line 5, column 50 /", StringComparison.Ordinal));
            Follow(window, 5, 28); Assert.Equal("codex", window.Editor.SelectedText);
        }
        finally { Directory.Delete(folder, true); }
    });

    [Fact]
    public void Unmatched_personal_names_keep_readable_source_without_a_required_schema() => ReasoningInspectionTests.Sta(window =>
    {
        const string source = "// tomorrow maybe ; my desk ;; coffee, a clear window, enough time //";
        window.Width = 780; window.Session.Load(source); OpenReferences(window, 1);
        Assert.DoesNotContain(ReasoningInspectionTests.Descendants<Button>(window.ReasoningView), button =>
            AutomationProperties.GetName(button).StartsWith("Follow ", StringComparison.Ordinal));
        Assert.Contains(ReasoningInspectionTests.Descendants<TextBlock>(window.ReasoningView), text => text.Text.Contains("No other named place", StringComparison.Ordinal));
        Assert.True(window.Session.Reasoning.Return()); Assert.Equal(source, window.Editor.Text);
        Assert.False(window.Editor.CanUndo); Assert.False(window.Session.IsDirty);
        OpenReferences(window, 1); ReasoningInspectionTests.Capture(window, "references-personal-780");
    });

    [Theory]
    [InlineData(780)]
    [InlineData(1180)]
    public void Visiting_an_unfinished_note_keeps_its_missing_boundary_visible(int width) => ReasoningInspectionTests.Sta(window =>
    {
        const string source = "// WHY ; codex ;; kept //\n// similarities ; tools ;;\n// codex,\n// unfinished";
        window.Width = width; window.Session.Load(source); OpenReferences(window, 1); Follow(window, 3, 4);
        Assert.Equal(2, window.Session.Reasoning.Owner!.LineNumber);
        Assert.Equal("codex", window.Editor.SelectedText);
        var missing = Assert.Single(ReasoningInspectionTests.Descendants<TextBlock>(window.ReasoningView), text => text.Text == "Closing // is missing.");
        DependencyObject? ancestor = missing;
        while (ancestor is not null && ancestor is not ScrollViewer) ancestor = System.Windows.Media.VisualTreeHelper.GetParent(ancestor);
        var scroll = Assert.IsType<ScrollViewer>(ancestor);
        Assert.True(missing.TranslatePoint(new(0, missing.ActualHeight), window).Y <= scroll.TranslatePoint(new(0, scroll.ActualHeight), window).Y);
        ReasoningInspectionTests.Capture(window, $"references-unfinished-{width}");
        Assert.True(window.Session.Reasoning.Return()); Assert.Equal(1, window.Editor.TextArea.Caret.Line);
        Assert.Equal(source, window.Editor.Text); Assert.False(window.Editor.CanUndo);
    });

    private static void OpenReferences(NotesWindow window, int line)
    {
        window.Editor.CaretOffset = window.Editor.Document.GetLineByNumber(line).Offset;
        window.Session.Reasoning.Begin();
        var toggle = ReasoningInspectionTests.Descendants<Button>(window.ReasoningView).FirstOrDefault(button => Equals(button.Content, "References"));
        toggle?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.UpdateLayout(); ReasoningInspectionTests.Pump();
    }

    private static void Start(NotesWindow window)
    {
        window.Session.Load(Source); window.Editor.CaretOffset = window.Editor.Document.GetLineByNumber(4).Offset;
        window.Session.Reasoning.Begin(); FindButton(window, "References").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.UpdateLayout(); ReasoningInspectionTests.Pump();
    }
    private static Button FindButton(NotesWindow window, string text) => Assert.Single(ReasoningInspectionTests.Descendants<Button>(window.ReasoningView), button => Equals(button.Content, text));
    private static Button Match(NotesWindow window, int line, int column) => ReasoningInspectionTests.Descendants<Button>(window.ReasoningView).First(button =>
        AutomationProperties.GetName(button).StartsWith($"Follow codex / line {line}, column {column} /", StringComparison.Ordinal));
    private static void Follow(NotesWindow window, int line, int column)
    {
        Match(window, line, column).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout(); ReasoningInspectionTests.Pump();
    }
}
