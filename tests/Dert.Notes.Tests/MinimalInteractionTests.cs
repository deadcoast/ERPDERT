using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dert.Cli.Authoring;
using Dert.Notes;
using Xunit;

namespace Dert.Notes.Tests;

[Collection("Compact window chrome")]
public sealed class MinimalInteractionTests
{
    private const string Source = "// LITTLE PLANS\n// ------------\n\npacking ; weekend ;;\ncoffee,\nbook\n\n// plans ; Saturday ;;\n// morning, // Bring coffee.\n// evening // Bring music.\n//\n";

    [Fact]
    public void Menu_arrow_keys_stay_in_the_current_level_and_Escape_returns_to_the_opener() => MinimalWindowTests.Sta(window =>
    {
        SendKey(Key.F10); SendKey(Key.Down);
        Assert.Equal("New", Focused().Content);
        SendKey(Key.Up); Assert.Equal("Close", Focused().Content);
        SendKey(Key.Down); Assert.Equal("New", Focused().Content);
        SendKey(Key.Escape);
        SendKey(Key.F10); SendKey(Key.Right); SendKey(Key.Right); SendKey(Key.Down);
        Assert.Equal("Inspect this line", Focused().Content);
        SendKey(Key.Down); SendKey(Key.Down); Assert.Equal("List  >", Focused().Content);
        SendKey(Key.Right); Assert.Equal("Add another item…", Focused().Content);
        SendKey(Key.Escape); Assert.Equal("List  >", Focused().Content);
        Button Focused() => Assert.IsType<Button>(FocusManager.GetFocusedElement(window));
        void SendKey(Key key)
        {
            window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, key)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            MinimalWindowTests.Pump();
        }
    });

    [Fact]
    public void Escape_releases_F10_root_anchor_without_needing_to_open_a_menu() => MinimalWindowTests.Sta(window =>
    {
        var menu = window.MinimalMenu!;
        SendKey(Key.F10); menu.State.ClickWindow(TimeSpan.FromSeconds(-10)); menu.Refresh();
        Assert.Equal(1, menu.RootChrome.Opacity);
        SendKey(Key.Escape); menu.Refresh();
        Assert.Equal(0, menu.RootChrome.Opacity);
        void SendKey(Key key) => window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, key)
        { RoutedEvent = Keyboard.PreviewKeyDownEvent });
    });

    [Theory]
    [InlineData("literal")]
    [InlineData("add")]
    [InlineData("finish")]
    public void Starting_a_proposal_from_the_editor_reveals_its_controls_without_a_menu(string proposal) => MinimalWindowTests.Sta(window =>
    {
        window.Session.Load("packing ; weekend ;;\ncoffee,\nbook\n");
        window.Editor.Select(window.Editor.Text.IndexOf("book", StringComparison.Ordinal), 4); MinimalWindowTests.Pump();
        Assert.False(window.IsWorkingPanelOpen);
        if (proposal == "literal") NotesWindow.LiteralTextCommand.Execute(null, window.Editor);
        else if (proposal == "add") window.Lists.BeginAdd();
        else window.Lists.PreviewFinish();
        MinimalWindowTests.Pump();
        Assert.True(window.IsWorkingPanelOpen);
        if (proposal == "literal") window.Literals.Leave(); else window.Lists.Leave();
        Assert.False(window.IsWorkingPanelOpen);
    });

    [Fact]
    public void An_initial_open_failure_survives_compact_composition() => MinimalWindowTests.Sta(parent =>
    {
        var path = Path.Combine(Path.GetDirectoryName(parent.SettingsPath)!, "missing.DERT");
        var window = new NotesWindow(path, still: true, compact: true, settingsPath: parent.SettingsPath)
        { ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -16000, Top = -16000 };
        try
        {
            window.Show(); MinimalWindowTests.Pump();
            Assert.Contains(ReasoningInspectionTests.Descendants<TextBlock>(window), text => text.IsVisible && text.Text.Contains("missing.DERT", StringComparison.Ordinal));
            Assert.False(window.HasOpenedDocument);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void The_whole_root_anchors_but_editor_hover_and_typing_do_not_reveal_it() => MinimalWindowTests.Sta(window =>
    {
        window.Session.Load(Source);
        var menu = window.MinimalMenu!;
        menu.State.ClickWindow(TimeSpan.FromSeconds(-10)); menu.Refresh();
        Assert.Equal(0, menu.RootChrome.Opacity);
        Assert.Equal(1, window.Editor.Opacity);
        MinimalWindowTests.Capture(window, "minimal-writing-faded");
        foreach (var point in new[] { new Point(12, 12), new Point(menu.ActualWidth - 8, 47) })
        {
            Assert.Equal("root", menu.RegionAt(point)); menu.PointerAt(point);
            Assert.Equal(1, menu.RootChrome.Opacity);
            Assert.False(menu.State.IsOpen("file"));
        }
        menu.PointerAt(new Point(300, 150));
        window.Editor.AppendText("// Still writing."); MinimalWindowTests.Pump();
        Assert.Equal(0, menu.RootChrome.Opacity);
        Assert.Null(menu.RegionAt(new Point(300, 150)));
        menu.PointerAt(new Point(100, 10));
        MinimalWindowTests.Capture(window, "minimal-writing-root");
    });

    [Fact]
    public void A_child_opens_on_click_and_anchors_only_its_own_rendered_rectangle() => MinimalWindowTests.Sta(window =>
    {
        window.Session.Load(Source);
        window.OpenMinimalMenu("note"); MinimalWindowTests.Pump();
        var menu = window.MinimalMenu!;
        var list = MinimalWindowTests.FindButton(window, "List  >");
        var listPoint = list.TranslatePoint(new Point(20, 10), menu);
        menu.PointerAt(listPoint);
        Assert.False(menu.State.IsOpen("note/2"));
        list.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); MinimalWindowTests.Pump();
        var child = ReasoningInspectionTests.Descendants<Border>(menu).Single(border => Equals(border.Tag, "note/2"));
        var childPoint = child.TranslatePoint(new Point(30, 20), menu);
        menu.PointerAt(childPoint);
        Assert.Equal("note/2", menu.RegionAt(childPoint));
        Assert.Equal(1, child.Opacity);
        Assert.Equal(0, menu.RootChrome.Opacity);
        Assert.Equal(0, ReasoningInspectionTests.Descendants<Border>(menu).Single(border => Equals(border.Tag, "note")).Opacity);
        MinimalWindowTests.Capture(window, "minimal-child-anchored");
        menu.State.ClickWindow(TimeSpan.FromSeconds(-10)); menu.PointerAt(childPoint);
        Assert.Equal(1, child.Opacity);
        menu.PointerAt(new Point(menu.ActualWidth - 2, menu.ActualHeight - 30));
        Assert.False(menu.State.IsOpen("note/2"));
        Assert.Equal(0, menu.RootChrome.Opacity);
    });

    [Fact]
    public void Fade_setting_is_shared_and_persists_without_opening_a_branch() => MinimalWindowTests.Sta(window =>
    {
        window.OpenMinimalMenu("settings"); MinimalWindowTests.Pump();
        MinimalWindowTests.FindButton(window, "[x] Windows menu fade").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.False(new DesktopSettingsStore(window.SettingsPath).Read().WindowsMenuFade);
        var menu = window.MinimalMenu!;
        menu.State.ClickWindow(TimeSpan.FromSeconds(-10)); menu.PointerAt(new Point(300, 180));
        Assert.Equal(1, menu.RootChrome.Opacity);
        Assert.False(menu.State.IsOpen("settings"));
        // An external CLI settings change is read when Settings is opened.
        new DesktopSettingsStore(window.SettingsPath).Save(new(WindowsMenuFade: true, WordWrap: false, FontSize: 18));
        window.OpenMinimalMenu("settings");
        Assert.True(menu.FadeEnabled); Assert.False(window.Editor.WordWrap); Assert.Equal(18, window.Editor.FontSize);
    });

    [Fact]
    public void Reasoning_keyboard_route_and_edit_source_keep_the_same_small_window() => MinimalWindowTests.Sta(window =>
    {
        window.Session.Load(Source); window.Editor.CaretOffset = window.Editor.Document.GetLineByNumber(8).Offset;
        NotesWindow.InspectReasoningCommand.Execute(null, window.Editor); MinimalWindowTests.Pump();
        Assert.True(window.IsWorkingPanelOpen);
        MinimalWindowTests.Capture(window, "minimal-reasoning");
        MinimalWindowTests.FindButton(window.ReasoningView, "Edit source").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        MinimalWindowTests.Pump();
        Assert.False(window.IsWorkingPanelOpen);
        Assert.Equal(Source, window.Editor.Text);
        Assert.Equal(640, window.Width);
    });

    [Theory]
    [InlineData("where codex --doctor --apply old-token --from-source ")]
    [InlineData("update codex updateme --apply old-token --from-source ")]
    [InlineData("result record.json --write --into ")]
    public void Unsaved_buffer_refuses_previous_saved_handoff_before_starting_a_process(string command) => MinimalWindowTests.Sta(window =>
    {
        window.Session.Load(Source);
        var path = Path.Combine(Path.GetDirectoryName(window.SettingsPath)!, "My Note.DERT");
        Assert.True(window.SaveTo(path)); window.Editor.AppendText("\n// Changed intention.");
        window.OpenCommandPanel(command + "'" + path.ToUpperInvariant() + "'");
        window.ExecuteDesktopCommand().GetAwaiter().GetResult();
        Assert.Contains("unsaved changes", window.OperationStatus);
        Assert.False(window.IsOperationRunning);
        Assert.Equal(Source, File.ReadAllText(path));
        Assert.Contains("Changed intention", window.Editor.Text);
    });

    [Fact]
    public void Changed_disk_source_refuses_a_handoff_while_retaining_the_buffer() => MinimalWindowTests.Sta(window =>
    {
        window.Session.Load(Source);
        var path = Path.Combine(Path.GetDirectoryName(window.SettingsPath)!, "My Note.DERT");
        Assert.True(window.SaveTo(path)); File.WriteAllText(path, "// Someone changed this file.");
        window.OpenCommandPanel("where codex --doctor --apply old-token --from-source '" + path + "'");
        window.ExecuteDesktopCommand().GetAwaiter().GetResult();
        Assert.Contains("changed on disk", window.OperationStatus);
        Assert.False(window.IsOperationRunning); Assert.Equal(Source, window.Editor.Text);
    });
}
