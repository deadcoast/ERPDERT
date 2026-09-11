using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dert.Core;
using Dert.Cli;
using Dert.Cli.Results;
using ICSharpCode.AvalonEdit.Editing;
using Spectre.Console;
using Xunit;

namespace Dert.Notes.Tests;

public class ResultAuthoringTests
{
    private const string Source = "// WHY ; codex ;; Keep this installation. //\r\nSTERT ; update ;; codex\nƒin.\r\nAn ordinary thought.";

    [Fact]
    public void Apply_inserts_the_reviewed_note_as_one_undo_group_and_selects_it() => ReasoningInspectionTests.Sta(window =>
    {
        window.Session.Load(Source);
        var view = Assert.IsType<ResultReviewWindow>(window.CreateResultReview(Observation()));
        Show(view);
        Assert.Same(window, view.Owner);
        Assert.Equal(Source, window.Editor.Text);
        Assert.StartsWith("// observed result ; observation ;;", view.Preview.Text);
        Assert.Contains("// target, // codex", view.Preview.Text);
        Assert.Contains("No matching command found", view.Preview.Text);
        Assert.Contains("Historical", view.Status.Text, StringComparison.OrdinalIgnoreCase);
        var addition = view.Preview.Text;
        var apply = Button(view, "Apply");
        apply.RaiseEvent(new RoutedEventArgs(ButtonBaseClick)); Pump();
        Assert.StartsWith(Source, window.Editor.Text);
        Assert.EndsWith(addition, window.Editor.Text);
        Assert.Equal(addition, window.Editor.SelectedText);
        Assert.True(window.Session.IsDirty); Assert.True(window.Editor.CanUndo);
        var after = window.Editor.Text;
        apply.RaiseEvent(new RoutedEventArgs(ButtonBaseClick));
        Assert.Equal(after, window.Editor.Text);
        window.Session.Undo(); Assert.Equal(Source, window.Editor.Text); Assert.False(window.Editor.CanUndo);
        window.Session.Redo(); Assert.Equal(after, window.Editor.Text);
        var duplicate = Assert.IsType<ResultReviewWindow>(window.CreateResultReview(Observation()));
        Show(duplicate); Assert.False(Button(duplicate, "Apply").IsEnabled);
        Assert.Contains("already", duplicate.Status.Text, StringComparison.OrdinalIgnoreCase);
        Click(duplicate, "Back");
    });

    [Theory]
    [InlineData(780)]
    [InlineData(1180)]
    public void Views_keep_actions_visible_and_back_preserves_reverse_selection(int width) => ReasoningInspectionTests.Sta(window =>
    {
        window.Session.Load(Source);
        window.Editor.CaretOffset = 6;
        window.Editor.TextArea.Selection = Selection.Create(window.Editor.TextArea, 14, 6);
        for (var repeat = 0; repeat < 2; repeat++)
        {
            var view = Assert.IsType<ResultReviewWindow>(window.CreateResultReview(Observation()));
            view.Width = width; Show(view);
            var insertion = view.Preview.Text;
            Assert.StartsWith("// observed result", insertion);
            foreach (var label in new[] { "Apply", "Back", "Observation", "Source comparison", "Insertion" })
            {
                var action = Button(view, label);
                var corner = action.TranslatePoint(new(action.ActualWidth, action.ActualHeight), view);
                Assert.True(action.IsVisible && action.ActualWidth > 0);
                Assert.InRange(corner.X, 1, view.ActualWidth);
                Assert.InRange(corner.Y, 1, view.Preview.TranslatePoint(new(0, 0), view).Y);
            }
            Assert.True(view.Preview.ActualWidth > 500); Assert.True(view.Preview.ActualHeight > 220);
            Capture(view, $"insertion-{width}");
            Click(view, "Observation"); Assert.Contains("No programs started", view.Preview.Text);
            Assert.Contains("2026-09-09", view.Preview.Text); Capture(view, $"observation-{width}");
            Click(view, "Source comparison"); Assert.Contains("Captured", view.Preview.Text);
            Assert.Contains("STERT ; update ;; codex", view.Preview.Text); Capture(view, $"comparison-{width}");
            Click(view, "Insertion"); Assert.Equal(insertion, view.Preview.Text);
            view.Preview.ScrollToEnd(); Pump();
            Assert.True(Button(view, "Back").IsVisible);
            Click(view, "Back");
            Assert.Null(window.Result);
            Assert.Equal(Source, window.Editor.Text); Assert.False(window.Editor.CanUndo);
            Assert.Equal(6, window.Editor.CaretOffset);
            Assert.Equal(14, window.Editor.Document.GetOffset(window.Editor.TextArea.Selection.StartPosition.Location));
            Assert.Equal(6, window.Editor.Document.GetOffset(window.Editor.TextArea.Selection.EndPosition.Location));
        }
    });

    [Fact]
    public void Escape_does_not_complete_an_unfinished_title_alignment() => ReasoningInspectionTests.Sta(window =>
    {
        window.Session.Load("// TITLE\n// -----\n");
        window.Editor.Document.Insert(8, " AGAIN");
        window.Editor.CaretOffset = 14;
        var before = window.Editor.Text;
        var view = Assert.IsType<ResultReviewWindow>(window.CreateResultReview(Observation()));
        Show(view);
        window.Editor.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, window.Editor, view) { RoutedEvent = Keyboard.LostKeyboardFocusEvent });
        Assert.Equal(before, window.Editor.Text);
        view.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(view)!, 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        Pump(); Assert.Equal(before, window.Editor.Text); Assert.Null(window.Result);
    });

    [Fact]
    public void A_source_edit_even_after_undo_refuses_the_old_apply() => ReasoningInspectionTests.Sta(window =>
    {
        window.Session.Load(Source);
        var view = Assert.IsType<ResultReviewWindow>(window.CreateResultReview(Observation()));
        view.Width = 780; Show(view); window.Editor.Document.Insert(0, "Earlier words.\n"); window.Session.Undo();
        Click(view, "Apply");
        Assert.Equal(Source, window.Editor.Text); Assert.Contains("changed", view.Status.Text);
        Assert.False(Button(view, "Apply").IsEnabled);
        Click(view, "Observation"); Assert.Contains("changed", view.Status.Text); Capture(view, "stale-780");
        Click(view, "Back");
    });

    [Fact]
    public void Saving_as_another_identity_refuses_the_old_apply() => ReasoningInspectionTests.Sta(window =>
    {
        window.Session.Load(Source);
        var path = Path.Combine(Path.GetTempPath(), "dert-result-" + Guid.NewGuid().ToString("N") + ".DERT");
        var view = Assert.IsType<ResultReviewWindow>(window.CreateResultReview(Observation()));
        try
        {
            Show(view); Assert.True(window.SaveTo(path)); Click(view, "Apply");
            Assert.Equal(Source, window.Editor.Text); Assert.Equal(Source, File.ReadAllText(path));
            Assert.Contains("changed", view.Status.Text); Assert.False(Button(view, "Apply").IsEnabled);
        }
        finally { view.Close(); if (File.Exists(path)) File.Delete(path); }
    });

    [Fact]
    public void Pending_words_refuse_creation_and_invalidate_existing_review() => ReasoningInspectionTests.Sta(window =>
    {
        window.Session.Load(Source);
        var view = Assert.IsType<ResultReviewWindow>(window.CreateResultReview(Observation()));
        Show(view); window.Editor.Select(10, 5); window.Literals.Begin();
        Assert.True(window.Literals.IsReviewing); Assert.Null(window.CreateResultReview(Observation()));
        Click(view, "Apply"); Assert.True(window.Literals.IsReviewing);
        Assert.Equal(Source, window.Editor.Text); Assert.Contains("proposal", view.Status.Text);
        Click(view, "Back"); window.Literals.Reset();
    });

    [Fact]
    public void Saving_the_inserted_note_preserves_BOM_and_mixed_newlines_and_reopens_exactly() => ReasoningInspectionTests.Sta(window =>
    {
        var folder = Path.Combine(Path.GetTempPath(), "dert-result-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder); var path = Path.Combine(folder, "my words.DERT");
        try
        {
            File.WriteAllText(path, Source, new UTF8Encoding(true)); var before = File.ReadAllBytes(path);
            window.LoadPath(path);
            var view = Assert.IsType<ResultReviewWindow>(window.CreateResultReview(Observation(path)));
            Show(view); var insertion = view.Preview.Text; Click(view, "Apply");
            Assert.Equal(before, File.ReadAllBytes(path));
            Assert.True(window.SaveTo());
            var saved = File.ReadAllBytes(path);
            Assert.True(saved.AsSpan().StartsWith(before));
            var text = window.Editor.Text; window.LoadPath(path);
            Assert.Equal(text, window.Editor.Text); Assert.EndsWith(insertion, window.Editor.Text);
            Assert.False(window.Editor.CanUndo); Assert.False(window.Session.IsDirty);
            Capture(window, "saved-note-1180");
        }
        finally { Directory.Delete(folder, true); }
    });

    [Fact]
    public void Outside_file_changes_keep_guarded_save_from_overwriting_other_words() => ReasoningInspectionTests.Sta(window =>
    {
        var path = Path.Combine(Path.GetTempPath(), "dert-result-" + Guid.NewGuid().ToString("N") + ".DERT");
        try
        {
            File.WriteAllText(path, Source); window.LoadPath(path);
            var view = Assert.IsType<ResultReviewWindow>(window.CreateResultReview(Observation(path)));
            Show(view); Click(view, "Apply"); var edited = window.Editor.Text;
            File.WriteAllText(path, "Someone else's newer words.");
            Assert.False(window.SaveTo()); Assert.Equal("Someone else's newer words.", File.ReadAllText(path));
            Assert.Equal(edited, window.Editor.Text); Assert.True(window.Session.IsDirty);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    });

    [Fact]
    public void Replacing_the_file_with_identical_bytes_refuses_apply_to_an_old_identity() => ReasoningInspectionTests.Sta(window =>
    {
        var folder = Path.Combine(Path.GetTempPath(), "dert-result-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder); var path = Path.Combine(folder, "words.DERT");
        try
        {
            File.WriteAllText(path, Source); window.LoadPath(path);
            var view = Assert.IsType<ResultReviewWindow>(window.CreateResultReview(Observation(path)));
            Show(view);
            File.Move(path, Path.Combine(folder, "earlier.DERT")); File.WriteAllText(path, Source);
            Click(view, "Apply");
            Assert.Equal(Source, window.Editor.Text); Assert.False(window.Editor.CanUndo);
            Assert.Contains("identity", view.Status.Text); Assert.False(Button(view, "Apply").IsEnabled);
            Click(view, "Back");
        }
        finally { Directory.Delete(folder, true); }
    });

    [Fact]
    public void Real_CLI_observation_from_BOM_source_matches_the_same_native_document() => ReasoningInspectionTests.Sta(window =>
    {
        const string source = "STERT ; update ;; codex\r\n// WHY ; codex ;; Keep uncertainty. //\nƒin.\r\n";
        var folder = Path.Combine(Path.GetTempPath(), "dert-result-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder); var path = Path.Combine(folder, "source.DERT"); var record = Path.Combine(folder, "observation.json");
        try
        {
            File.WriteAllText(path, source, new UTF8Encoding(true)); var original = File.ReadAllBytes(path);
            using var output = new StringWriter();
            var console = AnsiConsole.Create(new AnsiConsoleSettings { Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors, Out = new AnsiConsoleOutput(output) });
            console.Profile.Capabilities.Interactive = false;
            Assert.Equal(1, CliApplication.Run(["run", path, "--receipt", record, "--plain"], console, discoveryEnvironment: new("", ".EXE;.CMD")));
            var observation = ObservationStore.Read(record);
            Assert.StartsWith("\uFEFFSTERT", observation.Origin.ChainText);
            window.LoadPath(path);
            var view = Assert.IsType<ResultReviewWindow>(window.CreateResultReview(observation)); Show(view);
            Assert.Equal("exact", view.Plan.Comparison.State);
            Assert.Contains("full captured source revision", view.Plan.Comparison.Message);
            Click(view, "Apply"); Assert.True(window.SaveTo());
            Assert.True(File.ReadAllBytes(path).AsSpan().StartsWith(original));
        }
        finally { Directory.Delete(folder, true); }
    });

    [Fact]
    public void A_long_imported_target_keeps_apply_and_back_above_the_reading() => ReasoningInspectionTests.Sta(window =>
    {
        window.Session.Load(Source);
        var observation = Observation() with { Target = string.Join('\n', Enumerable.Repeat("A long observed target with a literal path and Unicode 日本語", 30)) };
        var view = Assert.IsType<ResultReviewWindow>(window.CreateResultReview(observation));
        view.Width = 780; Show(view);
        Assert.True(view.Preview.ActualHeight > 220);
        foreach (var label in new[] { "Apply", "Back" })
            Assert.InRange(Button(view, label).TranslatePoint(new(0, Button(view, label).ActualHeight), view).Y, 1, 220);
        Click(view, "Observation");
        Assert.Equal(30, view.Preview.Text.Split("A long observed target with a literal path and Unicode 日本語", StringSplitOptions.None).Length - 1);
        Capture(view, "long-observation-780"); Click(view, "Back");
    });

    [Fact]
    public void Focusing_keep_result_preserves_a_pending_title_until_review_is_cancelled() => ReasoningInspectionTests.Sta(window =>
    {
        window.Session.Load("// TITLE\n// -----\n");
        window.Editor.Document.Insert(8, " AGAIN"); window.Editor.CaretOffset = 8;
        var before = window.Editor.Text;
        var keep = Button(window, "Keep result…");
        window.Editor.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, window.Editor, keep) { RoutedEvent = Keyboard.LostKeyboardFocusEvent });
        Assert.Equal(before, window.Editor.Text);
        var view = Assert.IsType<ResultReviewWindow>(window.CreateResultReview(Observation())); Show(view); Click(view, "Back");
        Assert.Equal(before, window.Editor.Text);
    });

    [Theory]
    [InlineData("moved", "Earlier prose.\n" + Source, null)]
    [InlineData("changed", "// WHY ; codex ;; Keep this installation. //\r\nSTERT ; update ;; another-tool\nƒin.\r\nAn ordinary thought.", 2)]
    [InlineData("ambiguous", Source + "\nSTERT ; update ;; codex\nƒin.\r\n", null)]
    public void Source_comparison_shows_historical_origin_and_current_candidate(string state, string current, int? atLine) => ReasoningInspectionTests.Sta(window =>
    {
        window.Session.Load(current);
        var view = Assert.IsType<ResultReviewWindow>(window.CreateResultReview(Observation(), atLine));
        view.Width = 780; Show(view); Click(view, "Source comparison");
        Assert.Equal(state, view.Plan.Comparison.State);
        Assert.Contains("STERT ; update ;; codex", view.Preview.Text);
        Assert.Contains("historical", view.Preview.Text, StringComparison.OrdinalIgnoreCase);
        if (state == "changed") Assert.Contains("another-tool", view.Preview.Text);
        Capture(view, $"comparison-{state}-780"); Click(view, "Back");
        Assert.Equal(current, window.Editor.Text); Assert.False(window.Editor.CanUndo);
    });

    [Fact]
    public void A_line_ending_only_source_change_is_visible_in_the_comparison() => ReasoningInspectionTests.Sta(window =>
    {
        var current = Source.Replace("\r\n", "\n");
        window.Session.Load(current);
        var view = Assert.IsType<ResultReviewWindow>(window.CreateResultReview(Observation()));
        view.Width = 780; Show(view); Click(view, "Source comparison");
        Assert.Equal("changed", view.Plan.Comparison.State);
        Assert.Contains("[line ending: \\r\\n]", view.Preview.Text);
        Assert.Contains("[line ending: \\n]", view.Preview.Text);
        Assert.Contains("Captured", view.Preview.Text); Assert.Contains("Current", view.Preview.Text);
        Capture(view, "comparison-line-endings-780"); Click(view, "Back");
        Assert.Equal(current, window.Editor.Text); Assert.False(window.Editor.CanUndo);
    });

    private static ResultObservation Observation(string path = "Untitled.DERT") => new(
        "native-result-proof", DateTimeOffset.Parse("2026-09-09T12:00:00-07:00"), "DERT update codex / local discovery",
        "codex", ObservationOutcome.Attention, "No matching command found", "Read local PATH and package metadata. No programs started.",
        new(path, 2, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Source))), "STERT ; update ;; codex\nƒin.\r\n"),
        [new("PATH", "No Codex route appeared in the inspected PATH directories."), new("Preserved evidence", "STERT ; update ;; another-tool\n// external evidence remains annotation text")]);

    private static RoutedEvent ButtonBaseClick => System.Windows.Controls.Button.ClickEvent;
    private static Button Button(Window view, string label) => Assert.Single(ReasoningInspectionTests.Descendants<Button>(view), button => Equals(button.Content, label));
    private static void Click(Window view, string label) { Button(view, label).RaiseEvent(new RoutedEventArgs(ButtonBaseClick)); Pump(); }
    private static void Pump() => ReasoningInspectionTests.Pump();
    private static void Show(ResultReviewWindow view)
    {
        view.ShowActivated = false; view.ShowInTaskbar = false; view.WindowStartupLocation = WindowStartupLocation.Manual; view.Left = -16000; view.Top = -16000;
        view.Show(); view.UpdateLayout(); Pump();
    }
    private static void Capture(Window view, string name)
    {
        if (Environment.GetEnvironmentVariable("DERT_RESULT_PROOF") is not { Length: > 0 } folder) return;
        Directory.CreateDirectory(folder); view.UpdateLayout(); Pump();
        var bitmap = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(folder, name + ".png")); encoder.Save(file);
    }
}
