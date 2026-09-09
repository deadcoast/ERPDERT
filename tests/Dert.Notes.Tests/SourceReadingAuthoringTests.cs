using System.Windows.Controls;
using Dert.Notes;
using Xunit;

namespace Dert.Notes.Tests;

public class SourceReadingAuthoringTests
{
    [Theory]
    [InlineData("STERT ; ERPDERT ;; codex", "No ƒin. written / captured to end of document.")]
    [InlineData("STERT\n// earlier context\nSTERT\nfin.", "No ƒin. written / next STERT on line 3.")]
    [InlineData("START ;", "Historical START / read as a statement; no chain opened.")]
    [InlineData("START", "Historical START / retained as text; no chain opened.")]
    [InlineData("START // preserved context", "Historical START / retained as text; no chain opened.")]
    [InlineData("STERT ; DERCS ;;", "DERCS names the original help form. Its command fields are preserved.")]
    public void Native_context_explains_original_forms_without_an_edit(string source, string explanation) => ReasoningInspectionTests.Sta(window =>
    {
        window.Session.Load(source); window.UpdateLayout(); ReasoningInspectionTests.Pump();
        Assert.Contains(ReasoningInspectionTests.Descendants<TextBlock>(window), label => label.IsVisible && label.Text.Contains(explanation, StringComparison.Ordinal));
        Assert.Equal(source, window.Editor.Text);
        Assert.False(window.Session.IsDirty);
        Assert.False(window.Editor.CanUndo);
    });

    [Fact]
    public void A_hook_still_being_typed_keeps_its_source_and_updates_the_reading() => ReasoningInspectionTests.Sta(window =>
    {
        const string source = "STERT ;";
        window.Session.Load(source);
        window.Editor.CaretOffset = source.Length;
        window.Session.CompleteBoundary();
        ReasoningInspectionTests.Pump();
        Assert.Equal(source, window.Editor.Text);
        Assert.Contains(ReasoningInspectionTests.Descendants<TextBlock>(window), label => label.IsVisible && label.Text.Contains("The hook is empty", StringComparison.Ordinal));
        window.Editor.TextArea.PerformTextInput(" update ;; codex");
        ReasoningInspectionTests.Pump();
        Assert.Equal("STERT ; update ;; codex", window.Editor.Text);
        Assert.DoesNotContain(ReasoningInspectionTests.Descendants<TextBlock>(window), label => label.IsVisible && label.Text.Contains("The hook is empty", StringComparison.Ordinal));
        window.Session.Undo();
        Assert.Equal(source, window.Editor.Text);
    });

    [Theory]
    [InlineData(1180)]
    [InlineData(780)]
    public void Original_form_context_is_readable_in_wide_and_narrow_native_views(int width) => ReasoningInspectionTests.Sta(window =>
    {
        window.Width = width;
        window.Session.Load("// AS WRITTEN\n// ----------\n\nSTERT ; ERPDERT ;; codex\n// Find the recorded meaning before changing words.\n\nSTERT ;\n// Leave the hook available while writing.\nfin.\n\nSTART ;\ncommand option ; variable\n\nSTERT ; DERCS ;;\ncommand option ; variable\nfin.\n");
        window.Editor.CaretOffset = window.Editor.Document.GetLineByNumber(4).Offset;
        window.UpdateLayout(); ReasoningInspectionTests.Pump();
        ReasoningInspectionTests.Capture(window, $"source-reading-{width}");
        Assert.Contains(ReasoningInspectionTests.Descendants<TextBlock>(window), label => label.IsVisible && label.Text.Contains("next STERT on line 7", StringComparison.Ordinal));
    });
}
