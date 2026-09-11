using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using Dert.Notes;
using Xunit;

namespace Dert.Notes.Tests;

[Collection("Compact window chrome")]
public sealed class MinimalOperationTests
{
    [Fact]
    public void Real_CLI_execution_locks_every_source_edit_route_then_returns_output_and_editing() => MinimalWindowTests.Sta(window =>
    {
        window.Session.Load("packing ; weekend ;;\ncoffee,\nbook\n");
        window.Editor.AppendText("// A saved thought.\n");
        var path = Path.Combine(Path.GetDirectoryName(window.SettingsPath)!, "My Note.DERT");
        Assert.True(window.SaveTo(path));
        var source = window.Editor.Text;
        window.Editor.Select(source.IndexOf("book", StringComparison.Ordinal), 4);
        window.OpenCommandPanel("inspect '" + path + "'");
        var running = window.Dispatcher.Invoke(window.ExecuteDesktopCommand);
        try
        {
            Assert.True(window.IsOperationRunning);
            Assert.True(window.Editor.IsReadOnly);
            Assert.True(window.CloseWorkingPanel());
            Assert.Contains(ReasoningInspectionTests.Descendants<TextBlock>(window), text => text.IsVisible && text.Text == "DERT / command running");
            Assert.False(window.SaveTo());
            window.OpenMinimalMenu("edit"); window.UpdateLayout();
            MinimalWindowTests.FindButton(window, "Undo").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(source, window.Editor.Text);
            window.OpenMinimalMenu("note"); window.UpdateLayout();
            MinimalWindowTests.FindButton(window, "Align title…").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            window.UpdateLayout();
            var controls = ReasoningInspectionTests.Descendants<Button>(window.Lists).ToArray();
            Assert.NotEmpty(controls);
            foreach (var control in controls)
            {
                Assert.False(control.IsEnabled);
                var invoke = (IInvokeProvider)new ButtonAutomationPeer(control).GetPattern(PatternInterface.Invoke);
                Assert.Throws<ElementNotEnabledException>(() => invoke.Invoke());
            }
            Assert.Equal(source, window.Editor.Text);
        }
        finally { Finish(running); }
        Assert.False(window.IsOperationRunning); Assert.False(window.Editor.IsReadOnly);
        Assert.DoesNotContain(ReasoningInspectionTests.Descendants<TextBlock>(window), text => text.IsVisible && text.Text == "DERT / command running");
        Assert.Contains("DERT returned 0", window.OperationStatus);
        Assert.Contains("packing", window.OperationOutput);
        Assert.Equal(source, File.ReadAllText(path));
        window.OpenCommandPanel(); MinimalWindowTests.Capture(window, "minimal-cli-result");
        Assert.DoesNotContain(ReasoningInspectionTests.Descendants<TextBlock>(window), text => text.IsVisible && text.Text.Contains("Editing and file changes resume", StringComparison.Ordinal));
        window.CloseWorkingPanel(); window.Editor.AppendText("// Still my note.");
        Assert.True(window.Session.IsDirty);
    });

    [Fact]
    public void Help_is_a_separate_editable_workbench_and_preserves_a_failed_requested_file() => MinimalWindowTests.Sta(parent =>
    {
        var help = new NotesWindow(still: true) { ShowActivated = false, ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual, Left = -16000, Top = -16000 };
        try
        {
            help.EnableHelp(); help.Show(); MinimalWindowTests.Pump();
            Assert.False(help.IsCompact); Assert.Contains("DERT Help", help.Title);
            Assert.Contains("DERT HELP", help.Editor.Text); Assert.False(help.HasOpenedDocument);
            MinimalWindowTests.FindButton(help, "Hook, line, sinkers").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Contains("These items are sinkers", help.Editor.Text);
            MinimalWindowTests.Capture(help, "standalone-help");
            var missing = Path.Combine(Path.GetDirectoryName(parent.SettingsPath)!, "missing.DERT");
            help.LoadPath(missing); var source = help.Editor.Text;
            // Re-entry also checks that Help does not replace the open failure with a lesson.
            help.EnableHelp();
            Assert.Equal(source, help.Editor.Text);
            Assert.Contains(ReasoningInspectionTests.Descendants<TextBlock>(help), text => text.Text.Contains("missing.DERT", StringComparison.Ordinal));
        }
        finally { help.Session.MarkSaved(); help.Close(); }
    });

    private static void Finish(Task task)
    {
        var deadline = Stopwatch.StartNew();
        while (!task.IsCompleted && deadline.Elapsed < TimeSpan.FromSeconds(20))
        { MinimalWindowTests.Pump(); Thread.Sleep(10); }
        Assert.True(task.IsCompleted, "The real CLI command did not finish within 20 seconds.");
        task.GetAwaiter().GetResult();
    }
}
