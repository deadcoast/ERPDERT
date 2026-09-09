using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dert.Core;
using Xunit;

namespace Dert.Notes.Tests;

public partial class AuthoringTests
{
    [Fact]
    public void Florin_is_rendered_automatically_without_changing_source_selection_or_undo() => Sta(window =>
    {
        const string source = "STERT\n// fin. is just a note.\nfin. // Until tomorrow.";
        window.Session.Load(source);
        window.Editor.Select(source.IndexOf("fin. //", StringComparison.Ordinal), 4);
        Assert.Equal("ƒin. // Until tomorrow.", DrawnLine(window, 3));
        Assert.Equal("// fin. is just a note.", DrawnLine(window, 2));
        Assert.Equal(source, window.Editor.Text);
        Assert.Equal("fin.", window.Editor.SelectedText);
        Assert.False(window.Session.IsDirty);
        Assert.False(window.Editor.Document.UndoStack.CanUndo);
        string? copied = null;
        DataObject.AddCopyingHandler(window.Editor, (_, args) =>
        {
            copied = args.DataObject.GetData(DataFormats.UnicodeText) as string;
            args.CancelCommand(); // Exercise native copy data without changing the OS clipboard.
        });
        ApplicationCommands.Copy.Execute(null, window.Editor.TextArea);
        Assert.Equal("fin.", copied);
    });

    private static string DrawnLine(NotesWindow window, int number)
    {
        window.UpdateLayout(); Pump();
        var view = window.Editor.TextArea.TextView;
        view.EnsureVisualLines();
        var line = view.GetVisualLine(number);
        Assert.NotNull(line);
        return string.Concat(line.TextLines.SelectMany(text => text.GetIndexedGlyphRuns())
            .Select(run => new string(run.GlyphRun.Characters.ToArray())));
    }

    [Fact]
    public void Florin_native_choices_reveal_typed_spelling_and_apply_one_undoable_character() => Sta(window =>
    {
        const string source = "STERT\r\nfin. // Keep these words.";
        window.Session.Load(source);
        window.Editor.Select(7, 4);
        ClickGoodbye(window, "Show typed spelling");
        Assert.Equal("fin. // Keep these words.", DrawnLine(window, 2));
        Assert.False(window.Session.IsDirty);
        Assert.Equal("fin.", window.Editor.SelectedText);
        ClickGoodbye(window, "Write ƒ in source…");
        Assert.Equal(source, window.Editor.Text);
        var visible = string.Join("\n", Descendants<TextBlock>(window.Goodbye).Select(text =>
            new System.Windows.Documents.TextRange(text.ContentStart, text.ContentEnd).Text));
        Assert.True(visible.Contains("- 2 | fin. // Keep these words.", StringComparison.Ordinal), visible);
        ClickGoodbye(window, "Apply");
        Assert.Equal("STERT\r\nƒin. // Keep these words.", window.Editor.Text);
        Assert.Equal("ƒin.", window.Editor.SelectedText);
        window.Session.Undo();
        Assert.Equal(source, window.Editor.Text);
        Assert.False(window.Session.IsDirty);
        window.Session.Redo();
        Assert.Contains("ƒin.", window.Editor.Text);
    });

    [Fact]
    public void Florin_choice_stays_with_its_ending_and_source_changes_withdraw_it() => Sta(window =>
    {
        const string source = "STERT\nfin.\nSTERT\nfin.";
        window.Session.Load(source); window.Editor.CaretOffset = 6;
        ClickGoodbye(window, "Write ƒ in source…");
        window.Editor.CaretOffset = source.Length;
        ClickGoodbye(window, "Apply");
        Assert.Equal("STERT\nƒin.\nSTERT\nfin.", window.Editor.Text);
        ClickGoodbye(window, "Write ƒ in source…");
        window.Editor.Document.Insert(0, "// note\n");
        Assert.DoesNotContain(Descendants<Button>(window), button => button.IsVisible && Equals(button.Content, "Apply"));
        Assert.EndsWith("fin.", window.Editor.Text);
        Assert.Contains(Descendants<TextBlock>(window), text => text.Text.Contains("source changed", StringComparison.Ordinal));
    });

    [Fact]
    public void Florin_rendering_follows_native_typing_deletion_and_source_undo() => Sta(window =>
    {
        window.Session.Load("STERT\nfin"); window.Editor.CaretOffset = window.Editor.Text.Length;
        Type(".");
        Assert.Equal("ƒin.", DrawnLine(window, 2));
        Assert.EndsWith("fin.", window.Editor.Text);
        window.Session.Undo();
        Assert.Equal("fin", DrawnLine(window, 2));
        window.Session.Redo();
        Assert.Equal("ƒin.", DrawnLine(window, 2));
        window.Editor.Select(6, 1); Type("s");
        Assert.Equal("sin.", DrawnLine(window, 2));
        window.Session.Undo();
        window.Editor.Document.Remove(0, 6);
        Assert.Equal("fin.", DrawnLine(window, 1));

        void Type(string text)
        {
            var composition = new TextComposition(InputManager.Current, window.Editor.TextArea, text);
            window.Editor.TextArea.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice, composition)
                { RoutedEvent = TextCompositionManager.TextInputEvent });
        }
    });

    [Fact]
    public void Florin_presentation_save_and_explicit_spelling_survive_separate_reopens() => Sta(window =>
    {
        var folder = Path.Combine(Path.GetTempPath(), "erpdert-florin-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "goodbye [one] ' & note.DERT");
            const string source = "STERT\r\n// fin. stays here\nfin. // café";
            File.WriteAllText(path, source, new UTF8Encoding(true));
            var original = File.ReadAllBytes(path);
            window.LoadPath(path); window.Editor.CaretOffset = source.Length;
            Assert.StartsWith("ƒin.", DrawnLine(window, 3));
            Assert.True(window.SaveTo());
            Assert.Equal(original, File.ReadAllBytes(path));
            ClickGoodbye(window, "Write ƒ in source…"); ClickGoodbye(window, "Leave");
            Assert.False(window.Session.IsDirty);
            ClickGoodbye(window, "Write ƒ in source…"); ClickGoodbye(window, "Apply");
            Assert.Equal(original, File.ReadAllBytes(path));
            Assert.True(window.SaveTo()); window.LoadPath(path);
            Assert.Equal(source.Replace("fin. //", "ƒin. //", StringComparison.Ordinal), window.Editor.Text);
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, File.ReadAllBytes(path)[..3]);
            Assert.False(window.Editor.Document.UndoStack.CanUndo);
        }
        finally { Directory.Delete(folder, true); }
    });

    private static void ClickGoodbye(NotesWindow window, string content)
    {
        window.UpdateLayout(); Pump();
        var button = Descendants<Button>(window).Single(button => button.IsVisible && Equals(button.Content, content));
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.UpdateLayout(); Pump();
    }

    [Fact]
    public void Florin_apply_preserves_a_backward_selections_active_end() => Sta(window =>
    {
        window.Session.Load("STERT\nfin. // tomorrow");
        window.Editor.TextArea.Selection = ICSharpCode.AvalonEdit.Editing.Selection.Create(window.Editor.TextArea, 10, 6);
        window.Editor.CaretOffset = 6;
        window.Goodbye.Preview();
        Assert.True(window.Goodbye.Apply());
        Assert.Equal(6, window.Editor.CaretOffset);
        Assert.Equal(10, window.Editor.Document.GetOffset(window.Editor.TextArea.Selection.StartPosition.Location));
        Assert.Equal(6, window.Editor.Document.GetOffset(window.Editor.TextArea.Selection.EndPosition.Location));
        Assert.Equal("ƒin.", window.Editor.SelectedText);
    });

    [Fact]
    public void Florin_accepting_a_preview_does_not_report_it_as_a_stale_edit() => Sta(window =>
    {
        window.Session.Load("STERT\nfin."); window.Editor.CaretOffset = 6;
        var messages = new List<string>();
        var panel = new GoodbyeAuthoringPanel(window.Session, messages.Add, () => { }, () => true);
        window.Session.Changed += panel.Refresh;
        panel.Preview();
        Assert.True(panel.Apply());
        Assert.DoesNotContain(messages, message => message.Contains("source changed", StringComparison.Ordinal));
    });

    [Fact]
    public void Florin_stale_plan_and_list_draft_cannot_be_applied_over_each_other() => Sta(window =>
    {
        const string source = "STERT\nitems ; weekend ;;\ncoffee,\nfin.";
        window.Session.Load(source); window.Editor.CaretOffset = source.Length;
        window.Goodbye.Preview();
        var plan = window.Goodbye.CurrentPlan!;
        window.Editor.CaretOffset = 6;
        Assert.Equal(Visibility.Collapsed, window.Lists.Visibility);
        window.Lists.BeginAdd(); window.Lists.ItemEditor.Text = "tea";
        Assert.Null(window.Goodbye.CurrentPlan);
        window.Editor.CaretOffset = source.Length;
        window.Goodbye.Preview();
        Assert.Null(window.Goodbye.CurrentPlan);
        Assert.Equal("tea", window.Lists.ItemEditor.Text);
        window.Editor.Document.Insert(0, "// room\n");
        Assert.False(window.Session.ApplyGoodbyePreview(plan));
        Assert.Equal("tea", window.Lists.ItemEditor.Text);
        Assert.EndsWith("fin.", window.Editor.Text);
    });

    [Theory]
    [InlineData(1180, false)]
    [InlineData(1180, true)]
    [InlineData(780, false)]
    [InlineData(780, true)]
    public void Florin_actual_context_and_preview_fit_beside_the_source(int width, bool preview) => Sta(window =>
    {
        window.Width = width; window.Height = 780;
        window.Session.Load("// UNTIL TOMORROW\n// --------------\n\nSTERT\n// why ; tonight ;; Leave a little room. //\n// fin. is still an ordinary note.\n\"fin.\"\nfin. // Keep the quiet ending.\n");
        window.Editor.CaretOffset = window.Editor.Document.GetLineByNumber(8).Offset;
        if (preview) window.Goodbye.Preview();
        window.UpdateLayout(); Pump();
        Assert.Equal("ƒin. // Keep the quiet ending.", DrawnLine(window, 8));
        if (Environment.GetEnvironmentVariable("DERT_AUTHORING_PROOF") is { Length: > 0 } proof)
        {
            Directory.CreateDirectory(proof);
            var surface = (FrameworkElement)window.Content;
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)(surface.ActualHeight + surface.Margin.Top + surface.Margin.Bottom), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(proof, $"florin-{(preview ? "preview" : "appearance")}-{width}.png")); encoder.Save(file);
        }
        Assert.True(window.Editor.ActualHeight > 150);
        var viewport = Descendants<ScrollViewer>(window).Single(scroll => Descendants<GoodbyeAuthoringPanel>(scroll).Any());
        foreach (var button in Descendants<Button>(window.Goodbye).Where(button => button.IsVisible))
        {
            var bounds = button.TransformToAncestor(viewport).TransformBounds(new Rect(button.RenderSize));
            Assert.True(bounds.Top >= 0 && bounds.Bottom <= viewport.ActualHeight, $"{button.Content}: {bounds}; viewport {viewport.ActualHeight}");
        }
    });
}
