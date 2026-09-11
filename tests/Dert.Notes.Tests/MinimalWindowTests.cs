using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Dert.Notes;
using Xunit;

namespace Dert.Notes.Tests;

// WindowChrome uses a shared WPF property-descriptor listener cache. Keep its
// windows on one test collection instead of initializing it on competing STAs.
[Collection("Compact window chrome")]
public sealed class MinimalWindowTests
{
    [Fact]
    public void Compact_surface_gives_the_document_the_window_and_opens_without_a_lesson() => Sta(window =>
    {
        Assert.True(window.IsCompact);
        Assert.Equal(640, window.Width);
        Assert.Equal(420, window.Height);
        Assert.True(window.Editor.ActualHeight > 290);
        Assert.Empty(window.Editor.Text);
        Assert.False(window.IsWorkingPanelOpen);
        Assert.Equal(Color.FromRgb(9, 10, 14), Palette.Background.Color);
        Capture(window, "minimal-empty");
    });

    [Fact]
    public void Minimal_editor_keeps_title_undo_and_source_through_the_real_save_menu() => Sta(window =>
    {
        var path = Path.Combine(Path.GetDirectoryName(window.SettingsPath)!, "my note.DERT");
        window.Editor.AppendText("// WEEKEND");
        window.Editor.CaretOffset = window.Editor.Text.Length;
        window.Session.CompleteTitleLine();
        Assert.Equal("// WEEKEND\r\n// -------\r\n", window.Editor.Text);
        window.Session.Undo();
        Assert.Equal("// WEEKEND", window.Editor.Text);
        Assert.True(window.SaveTo(path));
        window.Editor.AppendText("\r\n// second visit");
        window.OpenMinimalMenu("file"); Pump();
        var save = FindButton(window, "Save");
        save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
        Assert.Equal(window.Editor.Text, File.ReadAllText(path));
        Assert.False(window.Session.IsDirty);
    });

    [Fact]
    public void Compact_menu_does_not_discard_a_literal_preview_or_its_selected_words() => Sta(window =>
    {
        window.Session.Load("packing ; overnight ;;\nnotebook\n");
        window.Editor.Select(window.Editor.Text.IndexOf("notebook", StringComparison.Ordinal), 8);
        window.OpenMinimalMenu("note"); Pump();
        FindButton(window, "Literal text…").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
        Assert.True(window.Literals.IsReviewing);
        Assert.True(window.IsWorkingPanelOpen);
        Assert.False(window.CloseWorkingPanel());
        Assert.Equal("packing ; overnight ;;\nnotebook\n", window.Editor.Text);
        Capture(window, "minimal-literal-preview");
        window.Literals.Reset();
    });

    internal static Button FindButton(DependencyObject view, string content) =>
        ReasoningInspectionTests.Descendants<Button>(view).First(button => Equals(button.Content, content));

    internal static void Sta(Action<NotesWindow> test)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var folder = Path.Combine(Path.GetTempPath(), "dert-minimal-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            NotesWindow? window = null;
            try
            {
                window = new NotesWindow(still: true, compact: true, settingsPath: Path.Combine(folder, "settings.json"), commandLinePath: Path.Combine(AppContext.BaseDirectory, "DERT.exe"))
                { ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -16000, Top = -16000 };
                window.Show(); Pump(); window.UpdateLayout();
                test(window);
            }
            catch (Exception error) { failure = error; }
            finally
            {
                if (window is not null) { window.Literals.Reset(); window.Lists.Reset(); window.Session.MarkSaved(); window.Close(); }
                Directory.Delete(folder, true);
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    internal static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    internal static void Capture(Window view, string name)
    {
        if (Environment.GetEnvironmentVariable("DERT_MINIMAL_PROOF") is not { Length: > 0 } folder) return;
        Directory.CreateDirectory(folder); view.UpdateLayout(); Pump();
        var bitmap = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(folder, name + ".png")); encoder.Save(stream);
    }
}
