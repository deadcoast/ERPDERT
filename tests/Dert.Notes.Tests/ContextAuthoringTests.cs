using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Dert.Core;
using Xunit;

namespace Dert.Notes.Tests;

public class ContextAuthoringTests
{
    private const string Source = "// KEEP THE CONTEXT\r\n// ----------------\r\n\r\n// similarities ; tools ;; codex, \"ACME, local\", codex //\r\n// HOW ; codex, claude ;; Keep this explanation, exactly as written. //\r\n";

    [Fact]
    public void Item_selection_edits_exact_duplicate_occurrence_and_returns_with_undo_available() => ReasoningInspectionTests.Sta(window =>
    {
        window.Session.Load(Source);
        window.Editor.CaretOffset = window.Editor.Document.GetLineByNumber(4).Offset;
        var origin = window.Editor.CaretOffset;
        NotesWindow.InspectReasoningCommand.Execute(null, window.Editor);
        var button = ItemButton(window, 3);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal("codex", window.Editor.SelectedText);
        Assert.Equal(Source.IndexOf(", codex //", StringComparison.Ordinal) + 2, window.Editor.SelectionStart);
        window.Editor.TextArea.PerformTextInput("codex-local");
        NotesWindow.ReturnToNoteCommand.Execute(null, window.Editor); ReasoningInspectionTests.Pump();
        Assert.Equal(origin, window.Editor.CaretOffset);
        Assert.Contains("codex, \"ACME, local\", codex-local //", window.Editor.Text);
        window.Session.Undo(); Assert.Equal(Source, window.Editor.Text);
    });

    [Fact]
    public void Raw_fields_keep_targets_and_payload_exact_and_switching_does_not_edit_source() => ReasoningInspectionTests.Sta(window =>
    {
        window.Session.Load(Source); window.Editor.CaretOffset = window.Editor.Document.GetLineByNumber(5).Offset;
        window.Session.Reasoning.Begin();
        Click(window, "Raw fields");
        Assert.Contains(ReasoningInspectionTests.Descendants<TextBlock>(window.ReasoningView), block => block.Text == "codex, claude");
        Assert.Contains(ReasoningInspectionTests.Descendants<TextBlock>(window.ReasoningView), block => block.Text == "Keep this explanation, exactly as written.");
        Click(window, "Read context");
        Assert.Contains(ReasoningInspectionTests.Descendants<TextBlock>(window.ReasoningView), block => block.Text == "Keep this explanation, exactly as written.");
        Assert.False(window.Session.IsDirty); Assert.False(window.Editor.CanUndo);
        Assert.Equal(Source, window.Editor.Text);
    });

    [Fact]
    public void Stale_item_button_cannot_select_replacement_words() => ReasoningInspectionTests.Sta(window =>
    {
        window.Session.Load(Source); window.Editor.CaretOffset = window.Editor.Document.GetLineByNumber(4).Offset;
        window.Session.Reasoning.Begin(); var old = ItemButton(window, 1);
        window.Editor.Document.Replace(Source.IndexOf("codex", StringComparison.Ordinal), 5, "other");
        var before = window.Editor.SelectionStart;
        old.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(before, window.Editor.SelectionStart);
        Assert.Equal("", window.Editor.SelectedText);
    });

    [Fact]
    public void An_empty_item_selects_its_exact_insertion_point_and_native_typing_fills_it() => ReasoningInspectionTests.Sta(window =>
    {
        const string source = "// similarities ; names ;; first,,last //";
        window.Session.Load(source); window.Session.Reasoning.Begin();
        ItemButton(window, 2).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(source.IndexOf(",,", StringComparison.Ordinal) + 1, window.Editor.CaretOffset);
        window.Editor.TextArea.PerformTextInput("middle");
        Assert.Equal(source.Replace(",,", ",middle,", StringComparison.Ordinal), window.Editor.Text);
        Assert.Empty(ContextReading.Read(window.Session.Parsed, window.Session.Reasoning.Owner!)!.Issues);
    });

    [Theory]
    [InlineData("Raw fields")]
    [InlineData("Edit source")]
    public void Old_inspection_controls_cannot_interrupt_a_pending_literal_proposal(string action) => ReasoningInspectionTests.Sta(window =>
    {
        window.Session.Load(Source); window.Editor.CaretOffset = window.Editor.Document.GetLineByNumber(4).Offset;
        window.Session.Reasoning.Begin();
        var old = Assert.Single(ReasoningInspectionTests.Descendants<Button>(window.ReasoningView), button => Equals(button.Content, action));
        ItemButton(window, 2).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.Literals.Begin();
        Assert.True(window.Literals.IsReviewing);
        Assert.Equal(Visibility.Collapsed, window.ReasoningView.Visibility);
        var start = window.Editor.SelectionStart; var length = window.Editor.SelectionLength;
        old.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(Visibility.Collapsed, window.ReasoningView.Visibility);
        Assert.True(window.Literals.IsReviewing);
        Assert.Equal(start, window.Editor.SelectionStart); Assert.Equal(length, window.Editor.SelectionLength);
        Assert.Equal(Source, window.Editor.Text);
    });

    [Fact]
    public void Expanded_unfinished_quote_selects_its_comma_while_raw_fields_retain_parser_fields() => ReasoningInspectionTests.Sta(window =>
    {
        const string source = "// similarities ; names ;;\n// \"still writing,\n//";
        window.Session.Load(source); window.Session.Reasoning.Begin();
        ItemButton(window, 1).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal("\"still writing,", window.Editor.SelectedText);
        Click(window, "Raw fields");
        Assert.Contains(ReasoningInspectionTests.Descendants<TextBlock>(window.ReasoningView), text => text.Text == "2 / \"still writing");
        Click(window, "Read context");
        ItemButton(window, 1).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.Editor.TextArea.PerformTextInput("\"now finished,\"");
        Assert.Equal(source.Replace("\"still writing,", "\"now finished,\"", StringComparison.Ordinal), window.Editor.Text);
        window.Session.Undo(); Assert.Equal(source, window.Editor.Text);
    });

    [Theory]
    [InlineData(1180, false)]
    [InlineData(780, false)]
    [InlineData(1180, true)]
    [InlineData(780, true)]
    public void Render_context_and_raw_fields_with_return_visible(int width, bool raw) => ReasoningInspectionTests.Sta(window =>
    {
        window.Width = width;
        window.Session.Load(Source); window.Editor.CaretOffset = window.Editor.Document.GetLineByNumber(4).Offset;
        window.Session.Reasoning.Begin();
        if (raw) Click(window, "Raw fields");
        window.UpdateLayout(); ReasoningInspectionTests.Pump();
        ReasoningInspectionTests.Capture(window, $"context-{(raw ? "fields" : "items")}-{width}");
        var back = Assert.Single(ReasoningInspectionTests.Descendants<Button>(window.ReasoningView), button => Equals(button.Content, "Return to note"));
        Assert.True(back.IsVisible && back.IsEnabled);
        DependencyObject? ancestor = back;
        while (ancestor is not null && ancestor is not ScrollViewer) ancestor = System.Windows.Media.VisualTreeHelper.GetParent(ancestor);
        var scroll = Assert.IsType<ScrollViewer>(ancestor);
        Assert.True(back.TranslatePoint(new(0, back.ActualHeight), window).Y <= scroll.TranslatePoint(new(0, scroll.ActualHeight), window).Y);
        Assert.True(window.Editor.ActualHeight > 150);
    });

    internal static Button ItemButton(NotesWindow window, int number) => Assert.Single(ReasoningInspectionTests.Descendants<Button>(window.ReasoningView),
        button => AutomationProperties.GetName(button).StartsWith($"Edit item {number},", StringComparison.Ordinal));

    [Theory]
    [InlineData(780, false)]
    [InlineData(1180, false)]
    [InlineData(780, true)]
    public void Missing_boundary_stays_above_long_content_in_the_visible_inspector(int width, bool raw) => ReasoningInspectionTests.Sta(window =>
    {
        window.Width = width;
        window.Session.Load("// similarities ; notes ;;\n" + string.Concat(Enumerable.Range(1, 20).Select(i => $"// entry {i}, // Keep the explanation for this entry.\n")));
        window.Session.Reasoning.Begin(); if (raw) Click(window, "Raw fields");
        window.UpdateLayout(); ReasoningInspectionTests.Pump();
        var missing = Assert.Single(ReasoningInspectionTests.Descendants<TextBlock>(window.ReasoningView), text => text.Text == "Closing // is missing.");
        AssertInViewport(missing, window);
        ReasoningInspectionTests.Capture(window, $"context-unfinished-{(raw ? "fields" : "items")}-{width}");
        Assert.False(window.Session.IsDirty); Assert.False(window.Editor.CanUndo);
    });

    [Fact]
    public void Unknown_kind_keeps_prose_and_raw_fields_without_a_schema_warning() => ReasoningInspectionTests.Sta(window =>
    {
        const string source = "// tomorrow maybe ; my desk ;; coffee, a clear window, enough time //";
        window.Width = 780; window.Session.Load(source); window.Session.Reasoning.Begin();
        window.UpdateLayout(); ReasoningInspectionTests.Pump();
        Assert.Contains(ReasoningInspectionTests.Descendants<TextBlock>(window.ReasoningView), text => text.Text == "coffee, a clear window, enough time");
        Assert.Empty(ContextReading.Read(window.Session.Parsed, window.Session.Reasoning.Owner!)!.Issues);
        Assert.DoesNotContain(ReasoningInspectionTests.Descendants<Button>(window.ReasoningView), button => AutomationProperties.GetName(button).StartsWith("Edit item", StringComparison.Ordinal));
        ReasoningInspectionTests.Capture(window, "context-personal-780");
        Click(window, "Raw fields"); Click(window, "Read context");
        Assert.Equal(source, window.Editor.Text); Assert.False(window.Editor.CanUndo);
    });

    [Fact]
    public void Repeated_reading_and_targeted_edit_survive_save_reopen_with_original_encoding() => ReasoningInspectionTests.Sta(window =>
    {
        var folder = Path.Combine(Path.GetTempPath(), "dert-context-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "context [日本語].DERT");
            var source = Source.Replace("// HOW", "\n// HOW", StringComparison.Ordinal);
            var encoding = new System.Text.UTF8Encoding(true);
            File.WriteAllText(path, source, encoding); var before = File.ReadAllBytes(path);
            window.LoadPath(path);
            for (var visit = 0; visit < 3; visit++)
            {
                window.Editor.CaretOffset = window.Editor.Document.GetLineByNumber(4).Offset;
                window.Session.Reasoning.Begin(); Click(window, "Raw fields"); Click(window, "Read context");
                Assert.True(window.Session.Reasoning.Return()); ReasoningInspectionTests.Pump();
            }
            Assert.False(window.Session.IsDirty); Assert.False(window.Editor.CanUndo);
            Assert.True(window.SaveTo()); Assert.Equal(before, File.ReadAllBytes(path));
            window.Session.Reasoning.Begin(); ItemButton(window, 3).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            window.Editor.TextArea.PerformTextInput("local copy");
            Assert.True(window.SaveTo());
            var expected = source.Replace(", codex //", ", local copy //", StringComparison.Ordinal);
            Assert.Equal(encoding.GetPreamble().Concat(encoding.GetBytes(expected)), File.ReadAllBytes(path));
            window.LoadPath(path); Assert.Null(window.Session.Reasoning.Owner);
            Assert.Equal(expected, window.Editor.Text);
        }
        finally { Directory.Delete(folder, true); }
    });

    private static void AssertInViewport(FrameworkElement element, NotesWindow window)
    {
        DependencyObject? ancestor = element;
        while (ancestor is not null && ancestor is not ScrollViewer) ancestor = System.Windows.Media.VisualTreeHelper.GetParent(ancestor);
        var scroll = Assert.IsType<ScrollViewer>(ancestor);
        Assert.True(element.TranslatePoint(new(0, element.ActualHeight), window).Y <= scroll.TranslatePoint(new(0, scroll.ActualHeight), window).Y);
    }

    private static void Click(NotesWindow window, string label)
    {
        Assert.Single(ReasoningInspectionTests.Descendants<Button>(window.ReasoningView), button => Equals(button.Content, label))
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.UpdateLayout(); ReasoningInspectionTests.Pump();
    }
}
