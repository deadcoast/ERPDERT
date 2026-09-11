using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ICSharpCode.AvalonEdit.Editing;
using System.Windows.Threading;
using Dert.Notes;
using Xunit;

namespace Dert.Notes.Tests;

public class ReasoningInspectionTests
{
    private const string Source = "packing ; weekend ;;\ncoffee,\nbook\n// plans ; Saturday ;;\n// morning, // Bring coffee.\n// evening // Bring music.\n//\nBack to the note.\n";

    [Fact]
    public void Temporary_reading_keeps_source_folded_and_returns_without_an_edit() => Sta(window =>
    {
        window.Session.Load(Source);
        window.Editor.CaretOffset = window.Editor.Document.GetLineByNumber(4).Offset;
        var origin = window.Editor.CaretOffset;
        Assert.True(window.Session.Reasoning.Begin());
        Assert.True(window.Session.Folds.AllFoldings.Single().IsFolded);
        Assert.Equal(4, window.Session.Reasoning.Owner!.LineNumber);
        Assert.True(window.Session.Reasoning.Return());
        Assert.Null(window.Session.Reasoning.Owner);
        Assert.Equal(origin, window.Editor.CaretOffset);
        Assert.Equal(Source, window.Editor.Text);
        Assert.False(window.Session.IsDirty);
        Assert.False(window.Editor.CanUndo);
    });

    [Fact]
    public void Editing_an_entry_uses_the_original_buffer_and_return_keeps_the_change() => Sta(window =>
    {
        window.Session.Load(Source);
        window.Editor.CaretOffset = window.Editor.Document.GetLineByNumber(4).Offset;
        Assert.True(window.Session.Reasoning.Begin());
        Assert.True(window.Session.Reasoning.EditLine(5));
        Assert.Equal("morning", window.Editor.SelectedText);
        Assert.False(window.Session.Folds.AllFoldings.Single().IsFolded);
        window.Editor.SelectedText = "early morning";
        Pump();
        Assert.True(window.Session.Reasoning.Return());
        Assert.True(window.Session.Folds.AllFoldings.Single().IsFolded);
        Assert.Contains("// early morning, // Bring coffee.", window.Editor.Text);
        Assert.Equal(4, window.Editor.TextArea.Caret.Line);
        window.Session.Undo();
        Assert.Equal(Source, window.Editor.Text);
    });

    [Fact]
    public void Leaving_the_reasoning_refolds_it_without_moving_the_new_caret() => Sta(window =>
    {
        window.Session.Load(Source);
        window.Editor.CaretOffset = window.Editor.Document.GetLineByNumber(4).Offset;
        window.Session.ToggleReasoning();
        Assert.False(window.Session.Folds.AllFoldings.Single().IsFolded);
        window.Editor.CaretOffset = window.Editor.Document.GetLineByNumber(8).Offset + 5;
        Pump();
        Assert.True(window.Session.Folds.AllFoldings.Single().IsFolded);
        Assert.Equal(8, window.Editor.TextArea.Caret.Line);
        Assert.Equal(6, window.Editor.TextArea.Caret.Column);
        Assert.Equal(Source, window.Editor.Text);
    });

    [Fact]
    public void Active_selection_is_never_hidden_when_its_caret_leaves_the_reasoning() => Sta(window =>
    {
        window.Session.Load(Source);
        window.Editor.CaretOffset = window.Editor.Document.GetLineByNumber(4).Offset;
        window.Session.ToggleReasoning();
        var start = window.Editor.Document.GetLineByNumber(5).Offset;
        var end = window.Editor.Document.GetLineByNumber(8).EndOffset;
        window.Editor.Select(start, end - start);
        window.Editor.CaretOffset = end;
        Pump();
        Assert.False(window.Session.Folds.AllFoldings.Single().IsFolded);
        Assert.Contains("Bring music.", window.Editor.SelectedText);
        window.Editor.Select(end, 0);
        Pump();
        Assert.True(window.Session.Folds.AllFoldings.Single().IsFolded);
    });

    [Fact]
    public void Inspection_command_exposes_ordered_annotations_and_keyboard_return() => Sta(window =>
    {
        window.Session.Load(Source);
        window.Editor.CaretOffset = window.Editor.Document.GetLineByNumber(4).Offset;
        NotesWindow.InspectReasoningCommand.Execute(null, window.Editor);
        Pump();
        var labels = Descendants<TextBlock>(window.ReasoningView).Select(label => label.Text).ToArray();
        Assert.Contains("Target as written / Saturday", labels);
        Assert.Contains("Bring coffee.", labels);
        Assert.Contains("Bring music.", labels);
        Assert.True(Array.IndexOf(labels, "Bring coffee.") < Array.IndexOf(labels, "Bring music."));
        Assert.Contains("Line 7 / its own //", labels);
        Assert.True(window.Session.Folds.AllFoldings.Single().IsFolded);
        NotesWindow.ReturnToNoteCommand.Execute(null, window.ReasoningView);
        Pump();
        Assert.Null(window.Session.Reasoning.Owner);
        Assert.Equal(Source, window.Editor.Text);
    });

    [Theory]
    [InlineData("Ordinary prose.\n")]
    [InlineData("// An ordinary note.\n")]
    [InlineData("packing ; weekend ;;\ncoffee,\nbook\n")]
    public void Ordinary_prose_notes_and_active_sinkers_do_not_gain_inspection(string text) => Sta(window =>
    {
        window.Session.Load(text);
        Assert.False(window.Session.Reasoning.Begin());
        Assert.Empty(window.Session.Folds.AllFoldings);
        Assert.Equal(text, window.Editor.Text);
    });

    [Fact]
    public void Return_anchors_follow_edits_before_the_origin() => Sta(window =>
    {
        window.Session.Load(Source);
        window.Editor.CaretOffset = window.Editor.Document.GetLineByNumber(4).Offset;
        window.Session.Reasoning.Begin();
        window.Session.Reasoning.EditLine(5);
        window.Editor.Document.Insert(0, "An earlier thought.\n");
        Pump();
        Assert.Equal(5, window.Session.Reasoning.Owner!.LineNumber);
        Assert.True(window.Session.Reasoning.Return());
        Pump();
        Assert.Equal(5, window.Editor.TextArea.Caret.Line);
        Assert.True(window.Session.Folds.AllFoldings.Single().IsFolded);
        Assert.StartsWith("An earlier thought.\n", window.Editor.Text);
    });

    [Fact]
    public void Deleting_the_opener_withdraws_inspection_instead_of_adopting_an_identical_neighbor() => Sta(window =>
    {
        const string block = "// plans ; Saturday ;;\n// morning // Bring coffee.\n//\n";
        window.Session.Load(block + block);
        window.Session.Reasoning.Begin();
        window.Editor.Document.Remove(0, block.Length);
        Pump();
        Assert.Null(window.Session.Reasoning.Owner);
        Assert.False(window.Session.Reasoning.Return());
        Assert.Equal(block, window.Editor.Text);
    });

    [Theory]
    [InlineData("// why ; Saturday ;; Bring coffee. //\n", "Bring coffee.", "Line 1 / its own //")]
    [InlineData("// plans ; Saturday ;;\n// morning // Bring coffee.\n", "Bring coffee.", "Closing // is missing.")]
    [InlineData("// plans ; Saturday ;;\n//\n", "No entries yet.", "Line 2 / its own //")]
    public void Small_and_unfinished_reasoning_remain_readable(string source, string contents, string boundary) => Sta(window =>
    {
        window.Session.Load(source);
        NotesWindow.InspectReasoningCommand.Execute(null, window.Editor);
        Pump();
        var labels = Descendants<TextBlock>(window.ReasoningView).Select(label => label.Text).ToArray();
        Assert.Contains(contents, labels);
        Assert.Contains(boundary, labels);
        Assert.Equal(source, window.Editor.Text);
        Assert.False(window.Session.IsDirty);
    });

    [Fact]
    public void Pending_list_words_survive_a_reasoning_return_request() => Sta(window =>
    {
        window.Session.Load(Source);
        window.Session.Reasoning.Begin(4);
        window.Lists.BeginAdd();
        window.Lists.ItemEditor.Text = "map";
        NotesWindow.ReturnToNoteCommand.Execute(null, window.Lists.ItemEditor);
        Pump();
        Assert.True(window.Lists.HasDraft);
        Assert.Equal("map", window.Lists.ItemEditor.Text);
        Assert.Equal(Source, window.Editor.Text);
    });

    [Fact]
    public void Entry_button_edits_its_exact_source_and_focus_does_not_replace_the_reading_controls() => Sta(window =>
    {
        window.Session.Load(Source);
        window.Editor.CaretOffset = window.Editor.Document.GetLineByNumber(4).Offset;
        NotesWindow.InspectReasoningCommand.Execute(null, window.Editor);
        Pump();
        var entry = Descendants<Button>(window.ReasoningView).Single(button =>
            System.Windows.Automation.AutomationProperties.GetName(button) == "Edit line 6: evening");
        entry.Focus(); Pump();
        Assert.Contains(entry, Descendants<Button>(window.ReasoningView));
        entry.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Pump();
        Assert.Equal("evening", window.Editor.SelectedText);
        Assert.False(window.Session.Folds.AllFoldings.Single().IsFolded);
        Assert.Equal(4, window.Session.Reasoning.Owner!.LineNumber);
        NotesWindow.ReturnToNoteCommand.Execute(null, window.Editor);
        Pump();
        Assert.True(window.Session.Folds.AllFoldings.Single().IsFolded);
        Assert.Equal(4, window.Editor.TextArea.Caret.Line);
    });

    [Fact]
    public void Return_restores_a_reverse_selection_without_hiding_it() => Sta(window =>
    {
        window.Session.Load(Source);
        window.Editor.CaretOffset = window.Editor.Document.GetLineByNumber(4).Offset;
        window.Session.ToggleReasoning();
        var start = window.Editor.Document.GetLineByNumber(5).Offset;
        var end = window.Editor.Document.GetLineByNumber(6).EndOffset;
        window.Editor.CaretOffset = start;
        window.Editor.TextArea.Selection = Selection.Create(window.Editor.TextArea, end, start);
        window.Session.Reasoning.Begin();
        window.Session.Reasoning.EditLine(6);
        window.Session.Reasoning.Return(); Pump();
        Assert.Equal(start, window.Editor.CaretOffset);
        Assert.Equal(end, window.Editor.Document.GetOffset(window.Editor.TextArea.Selection.StartPosition.Location));
        Assert.Equal(start, window.Editor.Document.GetOffset(window.Editor.TextArea.Selection.EndPosition.Location));
        Assert.False(window.Session.Folds.AllFoldings.Single().IsFolded);
    });

    [Fact]
    public void Return_preserves_native_rectangular_selection_shape() => Sta(window =>
    {
        window.Session.Load(Source);
        window.Editor.CaretOffset = window.Editor.Document.GetLineByNumber(4).Offset;
        window.Session.ToggleReasoning();
        window.Editor.CaretOffset = window.Editor.Document.GetLineByNumber(6).Offset + 7;
        window.Editor.TextArea.Selection = new RectangleSelection(window.Editor.TextArea,
            new ICSharpCode.AvalonEdit.TextViewPosition(5, 4), new ICSharpCode.AvalonEdit.TextViewPosition(6, 8));
        var original = window.Editor.TextArea.Selection.GetText();
        window.Session.Reasoning.Begin();
        window.Session.Reasoning.EditLine(6);
        window.Session.Reasoning.Return(); Pump();
        Assert.IsType<RectangleSelection>(window.Editor.TextArea.Selection);
        Assert.Equal(original, window.Editor.TextArea.Selection.GetText());
        Assert.False(window.Session.Folds.AllFoldings.Single().IsFolded);
    });

    [Fact]
    public void Renaming_the_header_preserves_ownership_but_reclassifying_it_withdraws_inspection() => Sta(window =>
    {
        window.Session.Load(Source);
        window.Session.Reasoning.Begin(4);
        var header = window.Editor.Document.GetLineByNumber(4);
        window.Editor.Document.Replace(header.Offset + 3, 5, "another plan");
        Assert.NotNull(window.Session.Reasoning.Owner);
        window.Editor.Document.Replace(header.Offset, header.Length, "This is ordinary prose now.");
        Pump();
        Assert.Null(window.Session.Reasoning.Owner);
        Assert.False(window.Session.Reasoning.Return());
    });

    [Theory]
    [InlineData(1180)]
    [InlineData(780)]
    public void Long_note_inspection_restores_viewport_and_repeats_without_source_or_save_changes(int width) => Sta(window =>
    {
        window.Width = width;
        var folder = Path.Combine(Path.GetTempPath(), "dert-reasoning-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var before = string.Join('\n', Enumerable.Range(1, 35).Select(i => $"Thought {i} / keep a little space for this.")) + "\n";
            var after = string.Join('\n', Enumerable.Range(1, 40).Select(i => $"Later thought {i}.")) + "\n";
            var source = before + Source.Replace("\n", "\r\n") + after;
            var path = Path.Combine(folder, "a longer note.DERT");
            File.WriteAllText(path, source, new System.Text.UTF8Encoding(true));
            var bytes = File.ReadAllBytes(path);
            window.LoadPath(path);
            window.Editor.CaretOffset = window.Editor.Document.GetLineByNumber(39).Offset;
            window.Editor.ScrollToLine(39); window.UpdateLayout(); Pump();
            window.Editor.ScrollToVerticalOffset(window.Editor.VerticalOffset - 65); Pump();
            var y = window.Editor.VerticalOffset;
            var caret = window.Editor.CaretOffset;
            for (var visit = 0; visit < 3; visit++)
            {
                NotesWindow.InspectReasoningCommand.Execute(null, window.Editor); Pump(); window.UpdateLayout();
                Assert.True(window.Session.Folds.AllFoldings.Single().IsFolded);
                if (visit == 0) Capture(window, $"reasoning-read-{width}");
                window.Session.Reasoning.EditLine(40); Pump();
                window.Editor.ScrollToLine(70); Pump();
                NotesWindow.ReturnToNoteCommand.Execute(null, window.Editor); Pump(); window.UpdateLayout(); Pump();
                Assert.Equal(caret, window.Editor.CaretOffset);
                Assert.InRange(Math.Abs(y - window.Editor.VerticalOffset), 0, 1);
                Assert.True(window.Session.Folds.AllFoldings.Single().IsFolded);
                Assert.Null(window.Session.Reasoning.Owner);
            }
            Capture(window, $"reasoning-return-{width}");
            Assert.Equal(source, window.Editor.Text);
            Assert.False(window.Session.IsDirty);
            Assert.False(window.Editor.CanUndo);
            Assert.True(window.SaveTo()); Assert.Equal(bytes, File.ReadAllBytes(path));
            window.LoadPath(path); Assert.Null(window.Session.Reasoning.Owner);
            Assert.True(window.Session.Folds.AllFoldings.Single().IsFolded);
        }
        finally { Directory.Delete(folder, true); }
    });

    internal static void Capture(NotesWindow window, string name)
    {
        if (Environment.GetEnvironmentVariable("DERT_REASONING_PROOF") is not { Length: > 0 } proof) return;
        Directory.CreateDirectory(proof);
        var surface = (FrameworkElement)window.Content;
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)(surface.ActualHeight + surface.Margin.Top + surface.Margin.Bottom), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(proof, name + ".png")); encoder.Save(file);
    }

    internal static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T item) yield return item;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    internal static void Sta(Action<NotesWindow> action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            NotesWindow? window = null;
            try
            {
                window = new(still: true) { ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -16000, Top = -16000 };
                window.Show(); Pump(); action(window);
            }
            catch (Exception error) { failure = error; }
            finally { if (window is not null) { window.Lists.Reset(); window.Session.MarkSaved(); window.Close(); } Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(25)), "Reasoning interaction did not finish.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    internal static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}
