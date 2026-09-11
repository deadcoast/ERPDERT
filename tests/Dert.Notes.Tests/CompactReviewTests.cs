using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dert.Core;
using Xunit;

namespace Dert.Notes.Tests;

[Collection("Compact window chrome")]
public sealed class CompactReviewTests
{
    private const string Source = "// HOW ; codex ;; Keep this installation. //\r\n// WHY ; codex ;; Another perspective. //\nSTERT ; update ;; codex\nƒin.\r\nAn ordinary thought.";

    [Theory]
    [InlineData(640, 420)]
    [InlineData(460, 320)]
    public void Compact_context_keeps_the_preview_and_note_choices_readable_in_the_small_window(int width, int height) => MinimalWindowTests.Sta(window =>
    {
        window.Session.Load(Source); window.Session.Reasoning.Begin(1);
        var view = Assert.IsType<ContextSelectionWindow>(window.CreateContextSelection(_ => { }));
        try
        {
            Assert.Equal(640, view.Width); Assert.Equal(420, view.Height);
            view.Width = width; view.Height = height; Show(view);
            Assert.Equal(width, view.ActualWidth); Assert.Equal(height, view.ActualHeight);
            foreach (var label in new[] { "Choose notes", "Copy preview", "Export…", "Left out", "Back to note" }) Reachable(view, Button(view, label));
            Assert.True(view.Preview.ActualWidth > width - 90);
            Assert.True(view.Preview.ActualHeight > height - 225);
            MinimalWindowTests.Capture(view, $"compact-context-preview-{width}");
            Click(view, "Choose notes");
            var choice = Note(view, 2); Reachable(view, choice);
            Assert.True(choice.ActualWidth > width - 110);
            MinimalWindowTests.Capture(view, $"compact-context-choices-{width}");
            Click(view, "Show preview"); Assert.True(view.Preview.IsVisible);
            Assert.Equal(Source, window.Editor.Text); Assert.False(window.Editor.CanUndo);
        }
        finally { view.Close(); }
    });

    [Fact]
    public void Compact_context_preserves_deliberate_selection_full_copy_and_verified_export() => MinimalWindowTests.Sta(window =>
    {
        window.Session.Load(Source); window.Session.Reasoning.Begin(1);
        string? copied = null;
        var path = Path.Combine(Path.GetDirectoryName(window.SettingsPath)!, "selected.txt");
        var view = Assert.IsType<ContextSelectionWindow>(window.CreateContextSelection(text => copied = text));
        try
        {
            Show(view); Click(view, "Choose notes");
            Assert.Single(view.Packet.Included); Assert.False(Note(view, 1).IsEnabled);
            Note(view, 2).IsChecked = true; Assert.Equal(2, view.Packet.Included.Count);
            Click(view, "Show preview"); Click(view, "Copy preview");
            Assert.Contains("Keep this installation.", copied); Assert.Contains("Another perspective.", copied);
            Assert.Equal(view.Preview.Text, copied);
            Assert.True(view.ExportTo(path)); Assert.Equal(copied, File.ReadAllText(path));
            var bytes = File.ReadAllBytes(path); Assert.False(view.ExportTo(path)); Assert.Equal(bytes, File.ReadAllBytes(path));
            Click(view, "Choose notes"); Note(view, 2).IsChecked = false; Click(view, "Show preview");
            Assert.Single(view.Packet.Included);
            Click(view, "Left out"); Assert.Contains("Another perspective.", view.Preview.Text);
            Assert.False(Button(view, "Copy preview").IsEnabled); Assert.False(view.ExportTo(path + ".txt"));
            Click(view, "Preview"); Assert.Equal(view.Packet.Report, view.Preview.Text);
            Assert.Equal(Source, window.Editor.Text); Assert.False(window.Editor.CanUndo);
        }
        finally { view.Close(); }
    });

    [Fact]
    public void Compact_context_pages_keep_later_note_choices_and_keyboard_toggle_accessible() => MinimalWindowTests.Sta(window =>
    {
        var source = string.Join('\n', Enumerable.Range(1, 20).Select(i => $"// note {i} ; music ;; Words {i}. //"));
        window.Session.Load(source); window.Session.Reasoning.Begin(1);
        string? copied = null;
        var view = Assert.IsType<ContextSelectionWindow>(window.CreateContextSelection(value => copied = value));
        try
        {
            view.Width = 460; view.Height = 320; Show(view); Click(view, "Choose notes");
            Reachable(view, Button(view, "Next notes")); Click(view, "Next notes");
            var choice = Note(view, 12); choice.BringIntoView(); MinimalWindowTests.Pump(); view.UpdateLayout();
            Reachable(view, choice); choice.Focus(); Assert.True(choice.IsKeyboardFocused);
            choice.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(view)!, 0, Key.Space) { RoutedEvent = Keyboard.KeyDownEvent });
            choice.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(view)!, 0, Key.Space) { RoutedEvent = Keyboard.KeyUpEvent });
            Assert.True(choice.IsChecked); Assert.Equal(2, view.Packet.Included.Count);
            MinimalWindowTests.Capture(view, "compact-context-later-notes-460");
            Click(view, "Show preview"); Click(view, "Copy preview");
            Assert.Contains("Words 12.", copied); Assert.Contains("Words 1.", copied);
            Click(view, "Choose notes"); Click(view, "Previous notes"); Click(view, "Next notes");
            Assert.True(Note(view, 12).IsChecked);
            Assert.Equal(source, window.Editor.Text); Assert.False(window.Editor.CanUndo);
        }
        finally { view.Close(); }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Compact_reviews_refuse_old_document_identity_after_save_as(bool result) => MinimalWindowTests.Sta(window =>
    {
        window.Session.Load(Source); window.Session.Reasoning.Begin(1);
        var sourcePath = Path.Combine(Path.GetDirectoryName(window.SettingsPath)!, "renamed.DERT");
        var exportedPath = Path.Combine(Path.GetDirectoryName(window.SettingsPath)!, "old-context.txt");
        var copied = false;
        Window view = result ? Assert.IsType<ResultReviewWindow>(window.CreateResultReview(Observation()))
            : Assert.IsType<ContextSelectionWindow>(window.CreateContextSelection(_ => copied = true));
        try
        {
            Show(view); Assert.True(window.SaveTo(sourcePath));
            if (view is ResultReviewWindow resultView)
            {
                Click(view, "Apply"); Assert.Contains("identity", resultView.Status.Text); Assert.False(Button(view, "Apply").IsEnabled);
            }
            else
            {
                var contextView = (ContextSelectionWindow)view;
                Click(view, "Copy preview"); Assert.False(copied); Assert.Contains("identity", contextView.Status.Text);
                Assert.False(contextView.ExportTo(exportedPath)); Assert.False(File.Exists(exportedPath));
            }
            Assert.Equal(Source, File.ReadAllText(sourcePath)); Assert.Equal(Source, window.Editor.Text); Assert.False(window.Editor.CanUndo);
        }
        finally { view.Close(); }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Compact_context_refuses_delivery_after_source_or_pending_proposal_changes(bool proposal) => MinimalWindowTests.Sta(window =>
    {
        window.Session.Load(Source); window.Session.Reasoning.Begin(1);
        var copied = false; var path = Path.Combine(Path.GetDirectoryName(window.SettingsPath)!, "stale.txt");
        var view = Assert.IsType<ContextSelectionWindow>(window.CreateContextSelection(_ => copied = true));
        try
        {
            Show(view);
            if (proposal) { window.Editor.Select(Source.IndexOf("codex", StringComparison.Ordinal), 5); window.Literals.Begin(); Assert.True(window.Literals.IsReviewing); }
            else window.Editor.Document.Insert(0, "Earlier words.\n");
            Click(view, "Copy preview"); Assert.False(copied); Assert.False(view.ExportTo(path)); Assert.False(File.Exists(path));
            Assert.Contains("changed", view.Status.Text);
            Click(view, "Choose notes"); Click(view, "Show preview"); Click(view, "Left out"); Click(view, "Preview");
            Assert.Contains("changed", view.Status.Text); Assert.False(Button(view, "Copy preview").IsEnabled);
            Reachable(view, Button(view, "Back to note"));
            MinimalWindowTests.Capture(view, "compact-context-stale-" + proposal);
        }
        finally { view.Close(); }
    });

    [Theory]
    [InlineData(640, 420)]
    [InlineData(460, 320)]
    public void Compact_result_keeps_every_reading_and_its_actions_reachable(int width, int height) => MinimalWindowTests.Sta(window =>
    {
        window.Session.Load(Source);
        var view = Assert.IsType<ResultReviewWindow>(window.CreateResultReview(Observation()));
        try
        {
            Assert.Equal(640, view.Width); Assert.Equal(420, view.Height);
            view.Width = width; view.Height = height; Show(view);
            Assert.Equal(width, view.ActualWidth); Assert.Equal(height, view.ActualHeight);
            var insertion = view.Preview.Text;
            foreach (var label in new[] { "Apply", "Back", "Observation", "Source comparison", "Insertion" }) Reachable(view, Button(view, label));
            Assert.True(view.Preview.ActualWidth > width - 90); Assert.True(view.Preview.ActualHeight > height - 235);
            MinimalWindowTests.Capture(view, $"compact-result-insertion-{width}");
            Click(view, "Observation"); Assert.Contains("No programs started", view.Preview.Text);
            MinimalWindowTests.Capture(view, $"compact-result-observation-{width}");
            Click(view, "Source comparison"); Assert.Contains("STERT ; update ;; codex", view.Preview.Text);
            MinimalWindowTests.Capture(view, $"compact-result-comparison-{width}");
            Click(view, "Insertion"); Assert.Equal(insertion, view.Preview.Text);
        }
        finally { view.Close(); }
    });

    [Fact]
    public void Compact_result_apply_preserves_one_undo_group_and_blocks_duplicate_apply() => MinimalWindowTests.Sta(window =>
    {
        window.Session.Load(Source);
        var view = Assert.IsType<ResultReviewWindow>(window.CreateResultReview(Observation()));
        Show(view); var insertion = view.Preview.Text; var apply = Button(view, "Apply"); Click(view, "Apply");
        Assert.StartsWith(Source, window.Editor.Text); Assert.EndsWith(insertion, window.Editor.Text);
        Assert.Equal(insertion, window.Editor.SelectedText);
        var after = window.Editor.Text; apply.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        Assert.Equal(after, window.Editor.Text); window.Session.Undo();
        Assert.Equal(Source, window.Editor.Text); Assert.False(window.Editor.CanUndo);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Compact_result_keeps_stale_refusal_visible_after_reading_changes(bool proposal) => MinimalWindowTests.Sta(window =>
    {
        window.Session.Load(Source);
        var view = Assert.IsType<ResultReviewWindow>(window.CreateResultReview(Observation()));
        try
        {
            view.Width = 460; view.Height = 320; Show(view);
            if (proposal) { window.Editor.Select(Source.IndexOf("codex", StringComparison.Ordinal), 5); window.Literals.Begin(); Assert.True(window.Literals.IsReviewing); }
            else { window.Editor.Document.Insert(0, "Earlier words.\n"); window.Session.Undo(); }
            Click(view, "Apply"); Assert.Equal(Source, window.Editor.Text);
            foreach (var reading in new[] { "Observation", "Source comparison", "Insertion" })
            {
                Click(view, reading); Assert.Contains("changed", view.Status.Text); Assert.False(Button(view, "Apply").IsEnabled);
                Reachable(view, Button(view, "Back"));
            }
            MinimalWindowTests.Capture(view, "compact-result-stale-" + proposal);
        }
        finally { view.Close(); }
    });

    private static ResultObservation Observation() => new(
        "compact-native-result", DateTimeOffset.Parse("2026-09-10T12:00:00-07:00"), "DERT update codex / local discovery",
        "codex", ObservationOutcome.Attention, "No matching command found", "Read local PATH and package metadata. No programs started.",
        new("Untitled.DERT", 3, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Source))), "STERT ; update ;; codex\nƒin.\r\n"),
        [new("PATH", "No Codex route appeared in the inspected PATH directories.")]);

    private static CheckBox Note(Window view, int line) => Assert.Single(ReasoningInspectionTests.Descendants<CheckBox>(view), box => box.Tag is int value && value == line);
    private static Button Button(Window view, string label) => Assert.Single(ReasoningInspectionTests.Descendants<Button>(view), button => Equals(button.Content, label));
    private static void Click(Window view, string label) { Button(view, label).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); MinimalWindowTests.Pump(); view.UpdateLayout(); }
    private static void Reachable(Window view, FrameworkElement element)
    {
        Assert.True(element.IsVisible && element.ActualWidth > 0 && element.ActualHeight > 0);
        var top = element.TranslatePoint(new(0, 0), view);
        var bottom = element.TranslatePoint(new(element.ActualWidth, element.ActualHeight), view);
        Assert.InRange(top.X, 0, view.ActualWidth); Assert.InRange(top.Y, 0, view.ActualHeight);
        Assert.InRange(bottom.X, 1, view.ActualWidth); Assert.InRange(bottom.Y, 1, view.ActualHeight);
    }
    private static void Show(Window view)
    {
        view.ShowActivated = false; view.ShowInTaskbar = false; view.WindowStartupLocation = WindowStartupLocation.Manual; view.Left = -16000; view.Top = -16000;
        view.Show(); MinimalWindowTests.Pump(); view.UpdateLayout();
    }
}
