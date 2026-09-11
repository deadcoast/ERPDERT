using System.IO;
using System.Windows;
using System.Windows.Controls;
using Dert.Core;
using Xunit;

namespace Dert.Notes.Tests;

public class LiteralAuthoringTests
{
    [Fact]
    public void Reverse_source_selection_keeps_its_direction_after_applying_quotes() => ReasoningInspectionTests.Sta(window =>
    {
        window.Session.Load("items ; note ;;\nhello,");
        var start = window.Editor.Text.IndexOf("hello", StringComparison.Ordinal);
        window.Editor.CaretOffset = start;
        window.Editor.TextArea.Selection = ICSharpCode.AvalonEdit.Editing.Selection.Create(window.Editor.TextArea, start + 6, start);
        window.Literals.Begin(); Assert.True(window.Literals.Apply());
        Assert.Equal(start, window.Editor.CaretOffset);
        Assert.Equal(start + 8, window.Editor.Document.GetOffset(window.Editor.TextArea.Selection.StartPosition.Location));
        Assert.Equal(start, window.Editor.Document.GetOffset(window.Editor.TextArea.Selection.EndPosition.Location));
    });

    [Fact]
    public void Already_protected_text_has_no_edit_and_cannot_add_an_undo_entry() => ReasoningInspectionTests.Sta(window =>
    {
        const string source = "items ; note ;;\n\"already ; quoted,\"";
        window.Session.Load(source);
        window.Editor.Select(source.IndexOf('"'), source.Length - source.IndexOf('"'));
        window.Literals.Begin(); Assert.False(window.Literals.CurrentPlan!.IsReady);
        Assert.False(window.Literals.Apply()); Assert.False(window.Editor.CanUndo);
        Assert.Equal(source, window.Editor.Text); window.Literals.Leave();
    });

    [Fact]
    public void Rectangular_selection_does_not_open_a_source_edit() => ReasoningInspectionTests.Sta(window =>
    {
        window.Session.Load("items ; note ;;\na,\nb,");
        window.Editor.TextArea.Selection = new ICSharpCode.AvalonEdit.Editing.RectangleSelection(window.Editor.TextArea,
            new ICSharpCode.AvalonEdit.TextViewPosition(2, 1), new ICSharpCode.AvalonEdit.TextViewPosition(3, 3));
        window.Literals.Begin(); Assert.False(window.Literals.IsReviewing); Assert.False(window.Editor.CanUndo);
    });

    [Fact]
    public void Source_literal_choice_holds_other_previews_and_reasoning_return_until_left() => ReasoningInspectionTests.Sta(window =>
    {
        const string source = "// why ; note ;; keep ; and ;; here //\n\nitems ; note ;;\na,";
        window.Session.Load(source); window.Session.Reasoning.Begin(); window.Session.Reasoning.EditLine(1);
        window.Editor.Select(source.IndexOf("keep", StringComparison.Ordinal), 18);
        window.Literals.Begin(); Assert.True(window.Literals.CurrentPlan!.IsReady);
        window.Lists.BeginAdd(); Assert.False(window.Lists.IsReviewing);
        NotesWindow.ReturnToNoteCommand.Execute(null, window.Editor); Assert.NotNull(window.Session.Reasoning.Owner);
        Assert.False(window.SaveTo()); window.Literals.Leave();
        NotesWindow.ReturnToNoteCommand.Execute(null, window.Editor); Assert.Null(window.Session.Reasoning.Owner);
    });

    [Fact]
    public void Literal_source_preview_uses_selected_words_then_one_undo_and_guarded_save() => ReasoningInspectionTests.Sta(window =>
    {
        const string source = "items ; note ;;\r\nhello, // keep this,\r\n";
        window.Session.Load(source);
        window.Editor.Select(source.IndexOf("hello", StringComparison.Ordinal), 6);
        NotesWindow.LiteralTextCommand.Execute(null, window.Editor);
        Assert.Equal(source, window.Editor.Text);
        Assert.True(window.Literals.CurrentPlan!.IsReady);
        Assert.True(window.Literals.Apply());
        Assert.Equal("\"hello,\"", window.Editor.SelectedText);
        Assert.Contains("\"hello,\" // keep this,", window.Editor.Text);
        window.Session.Undo(); Assert.Equal(source, window.Editor.Text);
        window.Session.Redo();
        var folder = Path.Combine(Path.GetTempPath(), "dert-literal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "literal [words].DERT");
            Assert.True(window.SaveTo(path));
            var saved = File.ReadAllBytes(path);
            window.LoadPath(path); Assert.Equal(saved, File.ReadAllBytes(path));
            Assert.Contains("\"hello,\" // keep this,", window.Editor.Text);
        }
        finally { Directory.Delete(folder, true); }
    });

    [Fact]
    public void Source_change_invalidates_literal_preview_and_does_not_follow_a_new_selection() => ReasoningInspectionTests.Sta(window =>
    {
        const string source = "items ; note ;;\nhello,";
        window.Session.Load(source); window.Editor.Select(source.IndexOf("hello", StringComparison.Ordinal), 6);
        window.Literals.Begin(); var plan = window.Literals.CurrentPlan!;
        window.Editor.Document.Insert(0, "\n");
        Assert.False(window.Literals.Apply());
        Assert.False(window.Session.ApplyLiteralPreview(plan));
        Assert.Contains("hello,", window.Editor.Text);
        window.Literals.Leave();
    });

    [Theory]
    [InlineData("C:\\tools; old\\", "\"C:\\tools; old\"\\")]
    [InlineData("say \"hello\" // literally,", "say \"hello\"\" // literally,\"")]
    public void Existing_list_composer_keeps_typed_words_and_previews_only_added_quotes(string words, string expected) => ReasoningInspectionTests.Sta(window =>
    {
        const string source = "items ; note ;;\nfirst";
        window.Session.Load(source); window.Lists.BeginAdd(); window.Lists.ItemEditor.Text = words;
        window.Lists.KeepPunctuationAsText();
        Assert.Equal(words, window.Lists.ItemEditor.Text);
        Assert.Equal(source, window.Editor.Text);
        Assert.True(window.Lists.CurrentPlan!.IsReady, window.Lists.CurrentPlan.Explanation);
        Assert.True(window.Lists.Apply());
        var items = Assert.IsType<StatementNode>(Assert.Single(window.Session.Parsed.Nodes)).Values;
        Assert.Equal(expected, items[^1].Value.Text);
        Assert.Null(items[^1].Annotation);
        window.Session.Undo(); Assert.Equal(source, window.Editor.Text);
    });

    [Fact]
    public void Pending_words_block_source_literal_choice_and_save_while_literal_mode_survives_stale_refresh() => ReasoningInspectionTests.Sta(window =>
    {
        window.Session.Load("items ; note ;;\nfirst,");
        window.Lists.BeginAdd(); window.Lists.ItemEditor.Text = "literal //,";
        window.Lists.KeepPunctuationAsText();
        window.Editor.Select(window.Editor.Text.IndexOf("first", StringComparison.Ordinal), 6);
        window.Literals.Begin(); Assert.False(window.Literals.IsReviewing);
        Assert.False(window.SaveTo());
        window.Editor.Document.Insert(0, "\n");
        Assert.Null(window.Lists.CurrentPlan);
        window.Editor.CaretOffset = window.Editor.Document.GetLineByNumber(2).Offset;
        window.Lists.ReviewAgain();
        Assert.True(window.Lists.Apply());
        Assert.Contains("\"literal //,\"", window.Editor.Text);
    });

    [Theory]
    [InlineData(1180, false)]
    [InlineData(780, false)]
    [InlineData(1180, true)]
    [InlineData(780, true)]
    public void Render_literal_choice_with_visible_source_and_real_boundary_meaning(int width, bool adding) => ReasoningInspectionTests.Sta(window =>
    {
        window.Width = width;
        const string source = "// EVERY MARK IS MINE\n// ------------------\n\nkeepsakes ; weekend ;;\nhello, // This comma belongs to my words.\n\n// why ; notes ;; A little room for punctuation. //\n";
        window.Session.Load(source);
        window.Editor.Select(source.IndexOf("hello", StringComparison.Ordinal), 6);
        if (adding)
        {
            window.Lists.BeginAdd(); window.Lists.ItemEditor.Text = "C:\\notes; old\\ // keep this,";
            window.Lists.KeepPunctuationAsText();
            Assert.True(window.Lists.CurrentPlan!.IsReady);
        }
        else { window.Literals.Begin(); Assert.True(window.Literals.CurrentPlan!.IsReady); }
        window.UpdateLayout(); ReasoningInspectionTests.Pump();
        ReasoningInspectionTests.Capture(window, $"literal-{(adding ? "add" : "source")}-{width}");
        Assert.True(window.Editor.ActualHeight > 150);
        var apply = Assert.Single(ReasoningInspectionTests.Descendants<Button>(window), button => Equals(button.Content, "Apply") && button.IsVisible && button.IsEnabled);
        DependencyObject? ancestor = apply;
        while (ancestor is not null && ancestor is not ScrollViewer) ancestor = System.Windows.Media.VisualTreeHelper.GetParent(ancestor);
        var scroll = Assert.IsType<ScrollViewer>(ancestor);
        var buttonBottom = apply.TranslatePoint(new Point(0, apply.ActualHeight), window).Y;
        var viewportBottom = scroll.TranslatePoint(new Point(0, scroll.ActualHeight), window).Y;
        Assert.True(buttonBottom <= viewportBottom, $"Apply ends at {buttonBottom}, below the visible context at {viewportBottom}.");
        window.Lists.Reset(); window.Literals.Reset();
    });
}
