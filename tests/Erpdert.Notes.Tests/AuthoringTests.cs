using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Dert.Core;
using Erpdert.Notes;
using Xunit;

namespace Erpdert.Notes.Tests;

public partial class AuthoringTests
{
    [Fact]
    public void Enter_creates_one_undoable_title_edit_and_places_the_caret_after_the_rule() => Sta(window =>
    {
        var session = window.Session;
        window.Editor.AppendText("// MY FIRST TITLE");
        window.Editor.CaretOffset = window.Editor.Text.Length;
        Assert.True(session.CompleteTitleLine());
        Assert.Equal("// MY FIRST TITLE\r\n// --------------\r\n", window.Editor.Text);
        Assert.Equal(window.Editor.Text.Length, window.Editor.CaretOffset);
        session.Undo();
        Assert.Equal("// MY FIRST TITLE", window.Editor.Text);
        session.CompleteBoundary();
        Assert.Equal("// MY FIRST TITLE", window.Editor.Text);
        session.Redo();
        Assert.Contains("// --------------", window.Editor.Text);
    });

    [Fact]
    public void Rename_adjusts_only_the_rule_and_preserves_the_selected_words() => Sta(window =>
    {
        window.Session.Load("// TITLE\n// -----\n\nmy prose");
        window.Editor.Document.Replace(3, 5, "NEW TITLE");
        window.Editor.Select(window.Editor.Text.IndexOf("my prose", StringComparison.Ordinal), 8);
        window.Session.CompleteBoundary();
        Assert.Equal("// NEW TITLE\n// ---------\n\nmy prose", window.Editor.Text);
        Assert.Equal("my prose", window.Editor.SelectedText);
    });

    [Fact]
    public void Enter_after_rename_keeps_one_rule_and_skips_to_the_following_words() => Sta(window =>
    {
        window.Session.Load("// TITLE\n// -----\nmy words");
        window.Editor.Document.Replace(3, 5, "NEW TITLE");
        window.Editor.CaretOffset = window.Editor.Document.GetLineByNumber(1).EndOffset;
        Assert.True(window.Session.CompleteTitleLine());
        Assert.Equal("// NEW TITLE\n// ---------\nmy words", window.Editor.Text);
        Assert.Equal(window.Editor.Document.GetLineByNumber(3).Offset, window.Editor.CaretOffset);
        window.Session.Undo();
        Assert.Equal("// NEW TITLE\n// -----\nmy words", window.Editor.Text);
        window.Session.CompleteBoundary();
        Assert.Equal("// NEW TITLE\n// -----\nmy words", window.Editor.Text);
    });

    [Fact]
    public void Undoing_body_prose_does_not_disable_later_title_edits() => Sta(window =>
    {
        window.Session.Load("// TITLE\n// -----\nmy words");
        window.Editor.Document.Insert(window.Editor.Text.Length, " more");
        window.Session.Undo();
        window.Editor.Document.Replace(3, 5, "NEW TITLE");
        window.Session.CompleteBoundary();
        Assert.Equal("// NEW TITLE\n// ---------\nmy words", window.Editor.Text);
    });

    [Fact]
    public void Pasted_title_and_notes_are_treated_after_the_whole_paste() => Sta(window =>
    {
        var text = "// WEEKEND\n// why ; rest ;; Keep it simple. //\n";
        window.Editor.Document.Insert(0, text);
        window.Editor.CaretOffset = text.Length;
        window.Editor.RaiseEvent(new DataObjectPastingEventArgs(new DataObject(DataFormats.UnicodeText, text), false, DataFormats.UnicodeText));
        Pump();
        Assert.Equal("// WEEKEND\n// -------\n// why ; rest ;; Keep it simple. //\n", window.Editor.Text);
        Assert.Equal(window.Editor.Text.Length, window.Editor.CaretOffset);
    });

    [Fact]
    public void Deliberate_rule_deletion_and_imported_mismatch_do_not_auto_reinsert() => Sta(window =>
    {
        window.Session.Load("// TITLE\n// -----\n");
        window.Editor.Document.Remove(9, 9);
        window.Session.CompleteBoundary();
        Assert.Equal("// TITLE\n", window.Editor.Text);
        Assert.True(TitleRuleRepair.Plan(window.Session.Parsed).IsReady);
        window.Session.Load("// TITLE\n// --\n");
        window.Session.CompleteBoundary();
        Assert.Equal("// TITLE\n// --\n", window.Editor.Text);
        window.PreviewTitle();
        Assert.Equal("// TITLE\n// --\n", window.Editor.Text);
        window.ApplyTitle();
        Assert.Equal("// TITLE\n// -----\n", window.Editor.Text);
    });

    [Fact]
    public void Unicode_title_is_retained_and_its_width_limit_is_explained() => Sta(window =>
    {
        string? explanation = null;
        window.Session.TitleAlignmentUnavailable += message => explanation = message;
        window.Editor.AppendText("// café 日本語");
        window.Editor.CaretOffset = window.Editor.Text.Length;
        Assert.True(window.Session.CompleteTitleLine());
        Assert.Equal("// café 日本語\r\n", window.Editor.Text);
        Assert.Equal(TitleRepairState.Unavailable, TitleRuleRepair.Plan(window.Session.Parsed).State);
        Assert.Contains("visible width", explanation);
    });

    [Fact]
    public void Reasoning_folds_but_active_values_and_exact_source_remain_available() => Sta(window =>
    {
        var text = "// why ; music ;; Keep every word. //\nitems ; weekend ;;\ncoffee,\nbook\n// plans ; Saturday ;;\n// morning, // Bring coffee\n// evening // Bring music\n//\n";
        window.Session.Load(text);
        Assert.Equal(2, window.Session.Folds.AllFoldings.Count());
        Assert.All(window.Session.Folds.AllFoldings, fold => Assert.True(fold.IsFolded));
        Assert.Equal(text, window.Editor.Text);
        window.Editor.CaretOffset = window.Editor.Document.GetLineByNumber(5).Offset;
        window.Session.ToggleReasoning();
        Assert.False(window.Session.Folds.AllFoldings.Last().IsFolded);
        window.Editor.Document.Insert(window.Editor.Text.IndexOf("morning", StringComparison.Ordinal), "early ");
        Assert.Contains("// early morning, // Bring coffee", window.Editor.Text);
        Assert.All(window.Editor.Text.Split('\n'), line => Assert.False(line.StartsWith(' ')));
    });

    [Fact]
    public void Save_reopen_and_second_edit_use_the_same_native_buffer_and_guarded_file() => Sta(window =>
    {
        var folder = Path.Combine(Path.GetTempPath(), "erpdert-authoring-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "my notes.DERT");
        try
        {
            window.Editor.AppendText("// WEEKEND"); window.Editor.CaretOffset = window.Editor.Text.Length;
            window.Session.CompleteTitleLine();
            Assert.True(window.SaveTo(path));
            Assert.False(window.Session.IsDirty);
            window.LoadPath(path);
            window.Editor.Document.Insert(window.Editor.Text.Length, "// second visit\r\n");
            Assert.True(window.SaveTo());
            Assert.Contains("second visit", File.ReadAllText(path));
            File.WriteAllText(path, "external edit");
            window.Editor.Document.Insert(window.Editor.Text.Length, "my third thought");
            Assert.False(window.SaveTo());
            Assert.True(window.Session.IsDirty);
            Assert.Equal("external edit", File.ReadAllText(path));
            Assert.Contains("my third thought", window.Editor.Text);
        }
        finally { Directory.Delete(folder, true); }
    });

    [Fact]
    public void Native_keyboard_routing_reaches_title_enter_and_undo() => Sta(window =>
    {
        var composition = new TextComposition(InputManager.Current, window.Editor.TextArea, "// HELLO");
        window.Editor.TextArea.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice, composition) { RoutedEvent = TextCompositionManager.TextInputEvent });
        Assert.Equal("// HELLO", window.Editor.Text);
        var source = PresentationSource.FromVisual(window.Editor)!;
        window.Editor.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, Key.Enter) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        Assert.Contains("// -----", window.Editor.Text);
        ApplicationCommands.Undo.Execute(null, window.Editor);
        Assert.Equal("// HELLO", window.Editor.Text);
    });

    [Fact]
    public void Adjacent_and_unfinished_reasoning_remains_exact_when_selected_and_unfolded() => Sta(window =>
    {
        var source = "// why ; one ;; First explanation. //\n// how ; two ;; Another explanation. //\n// plans ; later ;;\n// maybe, // still typing\n";
        window.Session.Load(source);
        Assert.Equal(3, window.Session.Folds.AllFoldings.Count());
        Assert.Contains(window.Session.Folds.AllFoldings, fold => fold.Title.Contains("missing //", StringComparison.Ordinal));
        window.Editor.SelectAll();
        Assert.Equal(source, window.Editor.SelectedText);
        foreach (var fold in window.Session.Folds.AllFoldings) fold.IsFolded = false;
        Assert.Equal(source, window.Editor.SelectedText);
        Assert.Equal(source, window.Editor.Text);
    });

    [Theory]
    [InlineData(1180, 780)]
    [InlineData(780, 720)]
    public void Render_the_actual_host_with_folded_reasoning(int width, int height) => Sta(window =>
    {
        window.Width = width; window.Height = height;
        window.Session.Load("// A LITTLE ROOM FOR THE WEEKEND\n// -----------------------------\n\npacking ; overnight ;;\nnotebook, // Leave work at home.\nheadphones, // The quiet playlist.\ncoffee\n\n// little plans ; Saturday ;;\n// morning, // Coffee by the water.\n// afternoon, // Find the record shop.\n// evening // Play the quiet record again.\n//\n");
        window.Editor.CaretOffset = window.Editor.Document.GetLineByNumber(9).Offset;
        window.UpdateLayout(); Pump();
        if (Environment.GetEnvironmentVariable("ERPDERT_AUTHORING_PROOF") is { Length: > 0 } proof)
        {
            Directory.CreateDirectory(proof);
            var surface = (FrameworkElement)window.Content;
            // The root has an outer margin; render the full client area so its
            // intended spacing and background are represented as well.
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)(surface.ActualHeight + surface.Margin.Top + surface.Margin.Bottom), 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(proof, $"notes-{width}.png")); encoder.Save(file);
        }
        Assert.True(window.Editor.ActualWidth > 350);
        Assert.True(window.Editor.ActualHeight > 150);
    });

    private static void Sta(Action<NotesWindow> action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            NotesWindow? window = null;
            try
            {
                window = new NotesWindow(still: true) { ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -16000, Top = -16000 };
                window.Show(); Pump(); action(window);
            }
            catch (Exception error) { failure = error; }
            finally { if (window is not null) { window.Lists.Reset(); window.Session.MarkSaved(); window.Close(); } Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "Native authoring test did not finish.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}
