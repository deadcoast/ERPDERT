using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.TextFormatting;
using Dert.Core;
using Xunit;

namespace Dert.Notes.Tests;

public partial class AuthoringTests
{
    [Theory]
    [InlineData("cafe\u0301", 4)]
    [InlineData("日本語", 6)]
    [InlineData("café 日本語 👩‍💻", 14)]
    [InlineData("👨‍👩‍👧‍👦 🇨🇦", 5)]
    [InlineData("שלום", 4)]
    [InlineData("مرحبا", 5)]
    public void Unicode_title_enter_preserves_spelling_and_aligns_actual_fallback_glyphs(string title, int marks) => Sta(window =>
    {
        window.Editor.AppendText("// " + title); window.Editor.CaretOffset = window.Editor.Text.Length;
        Assert.True(window.Session.CompleteTitleLine());
        var expected = "// " + title + "\r\n// " + new string('-', marks) + "\r\n";
        Assert.Equal(expected, window.Editor.Text);
        AssertTitleAdvances(window, 1, 2, marks);
        window.Session.Undo();
        Assert.Equal("// " + title, window.Editor.Text);
        window.Session.CompleteBoundary();
        Assert.Equal("// " + title, window.Editor.Text);
        window.Session.Redo(); Assert.Equal(expected, window.Editor.Text);
    });

    private static void AssertTitleAdvances(NotesWindow window, int titleLine, int ruleLine, int requiredMarks, double ratio = 1)
    {
        window.UpdateLayout(); Pump();
        var view = window.Editor.TextArea.TextView; view.EnsureVisualLines();
        var title = view.GetVisualLine(titleLine)!; var rule = view.GetVisualLine(ruleLine)!;
        Assert.Single(title.TextLines); Assert.Single(rule.TextLines);
        var titleRun = title.TextLines[0]; var ruleRun = rule.TextLines[0];
        var titleWidth = titleRun.GetTextBounds(3, title.VisualLength - 3).Sum(bound => bound.Rectangle.Width);
        var ruleWidth = ruleRun.GetDistanceFromCharacterHit(new CharacterHit(rule.VisualLength, 0)) - ruleRun.GetDistanceFromCharacterHit(new CharacterHit(3, 0));
        Assert.InRange(Math.Abs(titleWidth * ratio - ruleWidth), 0, 0.25);
        Assert.Equal(requiredMarks, TitleRuleRepair.Plan(window.Session.Parsed).Width);
    }

    [Fact]
    public void Unicode_rule_preview_keeps_a_wrong_rule_visibly_short_and_preserves_partial_selection() => Sta(window =>
    {
        const string source = "// 日本語\n// ---\n// Keep every word.";
        window.Session.Load(source); window.Editor.Select(10, 2);
        AssertTitleAdvances(window, 1, 2, 6, 0.5);
        window.PreviewTitle(); Assert.Equal(source, window.Editor.Text);
        window.ApplyTitle();
        Assert.Equal("// 日本語\n// ------\n// Keep every word.", window.Editor.Text);
        AssertTitleAdvances(window, 1, 2, 6);
        window.Editor.Select(10, 2);
        Assert.Equal("--", window.Editor.SelectedText);
        window.Editor.SelectedText = "";
        Assert.Equal("// 日本語\n// ----\n// Keep every word.", window.Editor.Text);
        window.Session.Undo(); Assert.Contains("// ------", window.Editor.Text);
    });

    [Theory]
    [InlineData("Consolas", 24)]
    [InlineData("Cascadia Mono", 17)]
    [InlineData("Segoe UI", 19)]
    public void Unicode_rule_remeasures_font_changes_without_creating_source_edits(string family, double size) => Sta(window =>
    {
        const string source = "// café 日本語 👩‍💻\n// --------------\n";
        window.Session.Load(source);
        window.Editor.FontFamily = new FontFamily(family); window.Editor.FontSize = size;
        AssertTitleAdvances(window, 1, 2, 14);
        Assert.Equal(source, window.Editor.Text);
        Assert.False(window.Session.IsDirty);
        Assert.False(window.Editor.Document.UndoStack.CanUndo);
    });

    [Fact]
    public void Unicode_long_title_wrap_is_explained_without_inserting_extra_rules() => Sta(window =>
    {
        window.Width = 780;
        var title = string.Join(' ', Enumerable.Repeat("日本語", 24));
        var source = "// " + title + "\n// " + new string('-', 167) + "\n";
        window.Session.Load(source); window.UpdateLayout(); Pump();
        Assert.Equal(source, window.Editor.Text);
        Assert.Contains(Descendants<TextBlock>(window), text => text.IsVisible && text.Text.Contains("wraps", StringComparison.Ordinal));
        Assert.False(window.Session.IsDirty);
    });

    [Theory]
    [InlineData(1180)]
    [InlineData(780)]
    public void Unicode_rendered_title_and_saved_reopened_source_share_the_same_rule(int width) => Sta(window =>
    {
        window.Width = width; window.Height = 780;
        var folder = Path.Combine(Path.GetTempPath(), "erpdert-unicode-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "日本語 café [notes].DERT");
            const string source = "// café 日本語 👩‍💻\r\n// --------------\n\n// Different words. The same care.\n\npacking ; weekend ;;\nnotebook, // Bring your own words.\ntea\n";
            File.WriteAllText(path, source, new System.Text.UTF8Encoding(true));
            var bytes = File.ReadAllBytes(path);
            window.LoadPath(path); AssertTitleAdvances(window, 1, 2, 14);
            Assert.True(window.SaveTo()); Assert.Equal(bytes, File.ReadAllBytes(path));
            window.LoadPath(path); AssertTitleAdvances(window, 1, 2, 14);
            if (Environment.GetEnvironmentVariable("DERT_AUTHORING_PROOF") is { Length: > 0 } proof)
            {
                Directory.CreateDirectory(proof);
                var surface = (FrameworkElement)window.Content;
                var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)(surface.ActualHeight + surface.Margin.Top + surface.Margin.Bottom), 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(window);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(Path.Combine(proof, $"unicode-title-{width}.png")); encoder.Save(file);
            }
        }
        finally { Directory.Delete(folder, true); }
    });
}
