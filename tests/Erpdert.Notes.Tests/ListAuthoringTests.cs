using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dert.Core;
using Xunit;

namespace Erpdert.Notes.Tests;

public partial class AuthoringTests
{
    [Fact]
    public void List_finish_is_a_buffer_preview_then_one_native_undo_preserving_selection() => Sta(window =>
    {
        const string source = "items ; weekend ;;\r\nsnacks, // Apples, pears,\r\n";
        window.Session.Load(source);
        window.Editor.Select(source.IndexOf("Apples", StringComparison.Ordinal), 14);
        window.Lists.PreviewFinish();
        Assert.Equal(source, window.Editor.Text);
        Assert.True(window.Lists.CurrentPlan!.IsReady);
        Assert.True(window.Lists.Apply());
        Assert.Equal(source.Replace("snacks,", "snacks", StringComparison.Ordinal), window.Editor.Text);
        Assert.Equal("Apples, pears,", window.Editor.SelectedText);
        Assert.True(window.Session.IsDirty);
        window.Session.Undo();
        Assert.Equal(source, window.Editor.Text);
        Assert.False(window.Session.IsDirty);
        window.Session.Redo();
        Assert.DoesNotContain("snacks,", window.Editor.Text);
    });

    [Fact]
    public void List_addition_uses_literal_draft_and_groups_separator_and_new_item_in_one_undo() => Sta(window =>
    {
        const string source = "items ; weekend ;;\ncoffee // Keep it hot.\n\nmy separate prose";
        window.Session.Load(source); window.Editor.CaretOffset = window.Editor.Document.GetLineByNumber(2).Offset;
        window.Lists.BeginAdd(); window.Lists.ItemEditor.Text = "book // Leave work at home.";
        Assert.Equal(source, window.Editor.Text);
        Assert.True(window.Lists.Apply());
        Assert.Equal("items ; weekend ;;\ncoffee, // Keep it hot.\nbook // Leave work at home.\n\nmy separate prose", window.Editor.Text);
        Assert.Equal(window.Editor.Text.IndexOf("book", StringComparison.Ordinal) + 4, window.Editor.CaretOffset);
        window.Session.Undo();
        Assert.Equal(source, window.Editor.Text);
        window.Session.Redo();
        window.Lists.BeginAdd(); window.Lists.ItemEditor.Text = "thermos";
        Assert.True(window.Lists.Apply());
        Assert.Equal(3, ListCompletion.StatementAt(window.Session.Parsed, 1)!.Values.Count);
    });

    [Fact]
    public void Adding_from_selected_words_clears_that_selection_before_the_next_native_input() => Sta(window =>
    {
        window.Session.Load("items ; weekend ;;\ncoffee");
        window.Editor.Select(window.Editor.Text.IndexOf("coffee", StringComparison.Ordinal), 6);
        window.Lists.BeginAdd(); window.Lists.ItemEditor.Text = "book";
        Assert.True(window.Lists.Apply());
        Assert.Equal("", window.Editor.SelectedText);
        var composition = new System.Windows.Input.TextComposition(System.Windows.Input.InputManager.Current, window.Editor.TextArea, "s");
        window.Editor.TextArea.RaiseEvent(new System.Windows.Input.TextCompositionEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, composition)
            { RoutedEvent = System.Windows.Input.TextCompositionManager.TextInputEvent });
        Assert.Equal("items ; weekend ;;\ncoffee,\nbooks", window.Editor.Text);
    });

    [Fact]
    public void Source_changes_withdraw_the_proposal_and_retain_typed_words_until_reviewed() => Sta(window =>
    {
        window.Session.Load("items ; weekend ;;\nsnacks,");
        window.Lists.BeginAdd(); window.Lists.ItemEditor.Text = "thermos";
        var stale = window.Lists.CurrentPlan!;
        window.Editor.Document.Insert(0, "// MY LIST\n// -------\n");
        Assert.True(window.Lists.IsStale);
        Assert.Equal("thermos", window.Lists.ItemEditor.Text);
        Assert.False(window.Lists.Apply());
        Assert.False(window.Session.ApplyListPreview(stale));
        window.Editor.CaretOffset = window.Editor.Document.GetLineByNumber(4).Offset;
        window.Lists.ReviewAgain();
        Assert.True(window.Lists.Apply());
        Assert.Contains("snacks,\nthermos", window.Editor.Text);
    });

    [Fact]
    public void Save_and_open_cannot_silently_discard_an_unapplied_item() => Sta(window =>
    {
        var folder = Path.Combine(Path.GetTempPath(), "erpdert-list-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "notes.DERT");
            const string source = "items ; weekend ;;\nsnacks,";
            File.WriteAllText(path, source);
            window.LoadPath(path); window.Lists.BeginAdd(); window.Lists.ItemEditor.Text = "thermos";
            Assert.False(window.SaveTo());
            window.LoadPath(path);
            Assert.Equal("thermos", window.Lists.ItemEditor.Text);
            Assert.Equal(source, File.ReadAllText(path));
            window.Lists.Leave();
            Assert.Equal(source, window.Editor.Text);
            window.Lists.PreviewFinish(); Assert.True(window.Lists.Apply());
            Assert.True(window.SaveTo()); window.LoadPath(path);
            Assert.Equal("items ; weekend ;;\nsnacks", window.Editor.Text);
            window.Lists.BeginAdd(); window.Lists.ItemEditor.Text = "tea";
            Assert.True(window.Lists.Apply()); Assert.True(window.SaveTo());
            Assert.Equal("items ; weekend ;;\nsnacks,\ntea", File.ReadAllText(path));
        }
        finally { Directory.Delete(folder, true); }
    });

    [Fact]
    public void Native_list_buttons_open_their_own_proposal_and_apply_it() => Sta(window =>
    {
        window.Session.Load("items ; weekend ;;\nsnacks,");
        var add = Descendants<Button>(window.Lists).Single(button => Equals(button.Content, "Add another item…"));
        add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.Lists.ItemEditor.Text = "tea";
        var apply = Descendants<Button>(window.Lists).Single(button => Equals(button.Content, "Apply"));
        Assert.True(apply.IsEnabled);
        apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal("items ; weekend ;;\nsnacks,\ntea", window.Editor.Text);
    });

    [Theory]
    [InlineData(1180, 780, false)]
    [InlineData(1180, 780, true)]
    [InlineData(780, 780, false)]
    [InlineData(780, 780, true)]
    public void Render_actual_list_choices_and_their_source_bound_previews(int width, int height, bool add) => Sta(window =>
    {
        window.Width = width; window.Height = height;
        window.Session.Load("// LEAVE WORK AT HOME\n// ------------------\n\npacking ; weekend ;;\nnotebook, // Only the small one.\nsnacks, // Apples, pears,\n\n// why ; rest ;; Leave a little room. //\n");
        window.Editor.CaretOffset = window.Editor.Document.GetLineByNumber(6).Offset;
        if (add) { window.Lists.BeginAdd(); window.Lists.ItemEditor.Text = "thermos // Bring tea."; }
        else window.Lists.PreviewFinish();
        window.UpdateLayout(); Pump();
        Assert.True(window.Lists.CurrentPlan!.IsReady);
        if (Environment.GetEnvironmentVariable("ERPDERT_AUTHORING_PROOF") is { Length: > 0 } proof)
        {
            Directory.CreateDirectory(proof);
            var surface = (FrameworkElement)window.Content;
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)(surface.ActualHeight + surface.Margin.Top + surface.Margin.Bottom), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(proof, $"list-{(add ? "add" : "finish")}-{width}.png")); encoder.Save(file);
        }
        Assert.True(window.Editor.ActualHeight > 150);
    });

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
}
