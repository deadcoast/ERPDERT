using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace Dert.Notes.Tests;

public class ContextSelectionAuthoringTests
{
    private const string Source = "// HOW ; codex ;; Keep this installation. //\r\n// WHY ; codex ;; This other note disagrees. //\nSTERT ; update ;; codex\nƒin.";

    [Fact]
    public void A_deliberate_note_choice_changes_the_exact_copy_and_left_out_reading() => ReasoningInspectionTests.Sta(window =>
    {
        Start(window); string? copied = null;
        var view = Assert.IsType<ContextSelectionWindow>(window.CreateContextSelection(value => copied = value));
        try
        {
            Show(view); Assert.Single(view.Packet.Included);
            Click(view, "Left out"); Assert.Contains("This other note disagrees.", view.Preview.Text);
            var choice = Assert.Single(ReasoningInspectionTests.Descendants<CheckBox>(view), box => box.Tag is int line && line == 2);
            choice.IsChecked = true;
            Assert.Equal(2, view.Packet.Included.Count);
            Click(view, "Preview"); Click(view, "Copy preview");
            Assert.Equal(view.Packet.Report, copied); Assert.Equal(view.Packet.Report, view.Preview.Text);
            Assert.Equal(Source, window.Editor.Text); Assert.False(window.Editor.CanUndo);
        }
        finally { view.Close(); }
    });

    [Fact]
    public void Clipboard_failure_does_not_report_a_successful_copy() => ReasoningInspectionTests.Sta(window =>
    {
        Start(window); var view = Assert.IsType<ContextSelectionWindow>(window.CreateContextSelection(_ => throw new ExternalException("Clipboard busy")));
        try
        {
            Show(view); Click(view, "Copy preview");
            Assert.Contains("could not", view.Status.Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Copied", view.Status.Text); Assert.Equal(Source, window.Editor.Text);
        }
        finally { view.Close(); }
    });

    [Fact]
    public void Changing_source_invalidates_old_copy_and_export_actions() => ReasoningInspectionTests.Sta(window =>
    {
        Start(window); var copied = false; var path = Path.Combine(Path.GetTempPath(), "dert-context-" + Guid.NewGuid().ToString("N") + ".txt");
        var view = Assert.IsType<ContextSelectionWindow>(window.CreateContextSelection(_ => copied = true));
        try
        {
            Show(view); window.Editor.Document.Insert(0, "\n"); Click(view, "Copy preview");
            Assert.False(copied); Assert.False(view.ExportTo(path)); Assert.False(File.Exists(path));
            Assert.Contains("changed", view.Status.Text);
            Click(view, "Left out"); Click(view, "Preview");
            Assert.Contains("changed", view.Status.Text); Assert.False(Button(view, "Copy preview").IsEnabled);
        }
        finally { view.Close(); }
    });

    [Fact]
    public void Saving_under_another_identity_invalidates_the_old_context() => ReasoningInspectionTests.Sta(window =>
    {
        Start(window); var copied = false;
        var path = Path.Combine(Path.GetTempPath(), "dert-context-" + Guid.NewGuid().ToString("N") + ".DERT");
        var view = Assert.IsType<ContextSelectionWindow>(window.CreateContextSelection(_ => copied = true));
        try
        {
            Show(view); Assert.True(window.SaveTo(path)); Click(view, "Copy preview");
            Assert.False(copied); Assert.Contains("changed", view.Status.Text);
        }
        finally { view.Close(); if (File.Exists(path)) File.Delete(path); }
    });

    [Fact]
    public void Pending_proposals_keep_their_choices_and_refuse_context_creation() => ReasoningInspectionTests.Sta(window =>
    {
        Start(window); window.Session.Reasoning.EditLine(1); window.Literals.Begin();
        Assert.True(window.Literals.IsReviewing); Assert.Null(window.CreateContextSelection(_ => { }));
        Assert.True(window.Literals.IsReviewing); Assert.Equal(Source, window.Editor.Text);
    });

    [Fact]
    public void Export_creates_the_reviewed_text_and_never_replaces_an_existing_file() => ReasoningInspectionTests.Sta(window =>
    {
        Start(window); var path = Path.Combine(Path.GetTempPath(), "dert-context-" + Guid.NewGuid().ToString("N") + ".txt");
        var view = Assert.IsType<ContextSelectionWindow>(window.CreateContextSelection(_ => { }));
        try
        {
            Show(view); Assert.True(view.ExportTo(path)); Assert.Equal(view.Packet.Report, File.ReadAllText(path));
            var bytes = File.ReadAllBytes(path); Assert.False(view.ExportTo(path)); Assert.Equal(bytes, File.ReadAllBytes(path));
            Assert.Equal(Source, window.Editor.Text); Assert.False(window.Session.IsDirty);
        }
        finally { view.Close(); if (File.Exists(path)) File.Delete(path); }
    });

    [Theory]
    [InlineData(1180)]
    [InlineData(780)]
    public void Review_actions_and_full_preview_remain_usable_at_both_widths(int width) => ReasoningInspectionTests.Sta(window =>
    {
        Start(window); var origin = window.Editor.CaretOffset;
        for (var repeat = 0; repeat < 2; repeat++)
        {
            var view = Assert.IsType<ContextSelectionWindow>(window.CreateContextSelection(_ => { }));
            try
            {
                view.Width = width; Show(view);
                foreach (var label in new[] { "Copy preview", "Export…", "Back to note" })
                {
                    var button = Button(view, label);
                    Assert.True(button.IsVisible && button.ActualWidth > 0);
                    Assert.InRange(button.TranslatePoint(new(0, button.ActualHeight), view).Y, 0, view.ActualHeight);
                }
                Assert.True(view.Preview.ActualWidth > 300); Assert.True(view.Preview.ActualHeight > 180);
                Capture(view, $"context-selection-{width}"); Click(view, "Back to note");
            }
            finally { view.Close(); }
            Assert.Equal(origin, window.Editor.CaretOffset); Assert.Equal(Source, window.Editor.Text); Assert.False(window.Editor.CanUndo);
        }
    });

    [Fact]
    public void Many_notes_are_paged_without_auto_including_name_matches() => ReasoningInspectionTests.Sta(window =>
    {
        window.Session.Load(string.Join('\n', Enumerable.Range(1, 20).Select(i => $"// note {i} ; music ;; words //")));
        window.Session.Reasoning.Begin(1);
        var view = Assert.IsType<ContextSelectionWindow>(window.CreateContextSelection(_ => { }));
        try
        {
            view.Width = 780; Show(view); Assert.Single(view.Packet.Included);
            Assert.Equal(6, ReasoningInspectionTests.Descendants<CheckBox>(view).Count());
            for (var page = 0; page < 3; page++) Click(view, "Next notes");
            var last = Assert.Single(ReasoningInspectionTests.Descendants<CheckBox>(view), box => box.Tag is int line && line == 20);
            last.IsChecked = true;
            Assert.Equal(new[] { 1, 20 }, view.Packet.Included.Select(note => note.LineNumber));
            Assert.Contains("// note 20 ; music ;; words //", view.Packet.Report);
            Capture(view, "context-selection-paged-780");
        }
        finally { view.Close(); }
    });

    [Fact]
    public void Left_out_cannot_be_copied_as_if_it_were_the_selected_preview() => ReasoningInspectionTests.Sta(window =>
    {
        Start(window); string? copied = null;
        var view = Assert.IsType<ContextSelectionWindow>(window.CreateContextSelection(value => copied = value));
        try
        {
            Show(view); Click(view, "Left out"); Click(view, "Copy preview"); Assert.Null(copied);
            view.Preview.Select(0, 4);
            Assert.False(System.Windows.Input.ApplicationCommands.Copy.CanExecute(null, view.Preview.TextArea));
            Assert.False(Button(view, "Export…").IsEnabled);
            Capture(view, "context-selection-left-out");
            Click(view, "Preview"); view.Preview.Select(0, 4);
            var nativeCopy = false;
            DataObject.AddCopyingHandler(view.Preview, (_, e) => { nativeCopy = true; e.CancelCommand(); });
            System.Windows.Input.ApplicationCommands.Copy.Execute(null, view.Preview.TextArea);
            Assert.False(nativeCopy);
            Assert.Equal(view.Packet.Report, copied);
        }
        finally { view.Close(); }
    });

    [Fact]
    public void Pending_proposal_after_capture_keeps_old_actions_from_delivering() => ReasoningInspectionTests.Sta(window =>
    {
        Start(window); var copied = false;
        var view = Assert.IsType<ContextSelectionWindow>(window.CreateContextSelection(_ => copied = true));
        try
        {
            Show(view); window.Session.Reasoning.EditLine(1); window.Literals.Begin();
            Assert.True(window.Literals.IsReviewing); Click(view, "Copy preview");
            Assert.False(copied); Assert.True(window.Literals.IsReviewing); Assert.Equal(Source, window.Editor.Text);
        }
        finally { view.Close(); }
    });

    [Fact]
    public void Unfinished_context_preserves_warning_and_backward_source_selection() => ReasoningInspectionTests.Sta(window =>
    {
        const string source = "// HOW ; music ;;\n// Keep this, // unfinished caution.";
        window.Session.Load(source);
        window.Editor.TextArea.Selection = ICSharpCode.AvalonEdit.Editing.Selection.Create(window.Editor.TextArea, 20, 17);
        window.Editor.CaretOffset = 17; window.Session.Reasoning.Begin(1);
        var caret = window.Editor.CaretOffset; var start = window.Editor.SelectionStart; var length = window.Editor.SelectionLength;
        var view = Assert.IsType<ContextSelectionWindow>(window.CreateContextSelection(_ => { }));
        try
        {
            Show(view); Assert.Contains("Closing //: missing", view.Preview.Text);
            Assert.Contains("// Keep this, // unfinished caution.", view.Preview.Text);
            Capture(view, "context-selection-unfinished"); Click(view, "Back to note");
            Assert.Equal(caret, window.Editor.CaretOffset); Assert.Equal(start, window.Editor.SelectionStart); Assert.Equal(length, window.Editor.SelectionLength);
            Assert.False(window.Editor.CanUndo);
        }
        finally { view.Close(); }
    });

    [Fact]
    public void Nul_text_refuses_clipboard_truncation_and_retains_exact_file_export() => ReasoningInspectionTests.Sta(window =>
    {
        window.Session.Load("// HOW ; note ;; before\0AFTER //"); window.Session.Reasoning.Begin(1);
        var copied = false; var path = Path.Combine(Path.GetTempPath(), "dert-context-nul-" + Guid.NewGuid().ToString("N") + ".txt");
        var view = Assert.IsType<ContextSelectionWindow>(window.CreateContextSelection(_ => copied = true));
        try
        {
            Show(view); Click(view, "Copy preview"); Assert.False(copied);
            Assert.Contains("NUL", view.Status.Text); Assert.Contains("Export", view.Status.Text);
            Assert.True(view.ExportTo(path)); Assert.Equal(view.Packet.Report, File.ReadAllText(path));
            Assert.Contains("before\0AFTER", File.ReadAllText(path), StringComparison.Ordinal);
        }
        finally { view.Close(); if (File.Exists(path)) File.Delete(path); }
    });

    private static void Start(NotesWindow window) { window.Session.Load(Source); window.Session.Reasoning.Begin(1); }
    private static void Show(ContextSelectionWindow view)
    {
        view.ShowActivated = false; view.ShowInTaskbar = false; view.WindowStartupLocation = WindowStartupLocation.Manual; view.Left = -16000; view.Top = -16000;
        view.Show(); view.UpdateLayout(); ReasoningInspectionTests.Pump();
    }
    private static Button Button(Window view, string label) => Assert.Single(ReasoningInspectionTests.Descendants<Button>(view), button => Equals(button.Content, label));
    private static void Click(Window view, string label) { Button(view, label).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); view.UpdateLayout(); ReasoningInspectionTests.Pump(); }
    internal static void Capture(Window view, string name)
    {
        if (Environment.GetEnvironmentVariable("DERT_CONTEXT_PROOF") is not { Length: > 0 } folder) return;
        Directory.CreateDirectory(folder);
        var bitmap = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(folder, name + ".png")); encoder.Save(file);
    }
}
