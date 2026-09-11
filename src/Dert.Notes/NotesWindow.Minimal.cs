using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using Dert.Cli.Authoring;
using ICSharpCode.AvalonEdit.Search;

namespace Dert.Notes;

public sealed partial class NotesWindow
{
    private DesktopSettingsStore? settingsStore;
    private DesktopSettings preferences = new();
    private Grid? minimalRoot;
    private Border? workingPanel;
    private ContentControl? workingContent;
    private TextBlock? panelTitle;
    private TextBlock? minimalStatus;
    private TextBlock? minimalPosition;
    private MinimalMenuHost? minimalMenu;
    private bool settingsFailed;
    private bool minimalReviewWasOpen;
    public bool IsCompact { get; private set; }
    public bool IsWorkingPanelOpen => workingPanel?.Visibility == Visibility.Visible;
    public string SettingsPath => settingsStore?.Path ?? "";
    public MinimalMenuHost? MinimalMenu => minimalMenu;

    private void ConfigureMinimal(string? settingsPath)
    {
        IsCompact = true; settingsStore = new(settingsPath);
        Width = 640; Height = 420; MinWidth = 460; MinHeight = 320;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.CanResize;
        Background = Palette.Background;
        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 0, ResizeBorderThickness = new(5), GlassFrameThickness = new(0),
            CornerRadius = new(8), UseAeroCaptionButtons = false
        });
        SourceInitialized += (_, _) =>
        {
            var preference = 2;
            _ = DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 33, ref preference, sizeof(int));
        };
        FontFamily = new("Cascadia Mono, Consolas"); FontSize = 12;
        // Recompose the same editor and controller panels. There is no second
        // editing buffer, file service, repair implementation or grammar here.
        ((DockPanel)Editor.Parent).Children.Remove(Editor);
        body.Children.Remove(contextScroll);
        inspector.Margin = new(0); context.Margin = new(0, 8, 0, 12);
        heading.FontSize = 16; context.FontSize = 13;
        minimalRoot = new Grid();
        minimalRoot.RowDefinitions.Add(new() { Height = new(51) });
        minimalRoot.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        minimalRoot.RowDefinitions.Add(new() { Height = new(24) });
        var outer = new Border
        {
            // Windows owns the perimeter. An inset ring here creates a second
            // silhouette, especially where its radius differs from DWM's.
            CornerRadius = new(8), Background = Palette.Background,
            Child = minimalRoot
        };
        Content = outer;
        Editor.Background = Brushes.Transparent; Editor.FontFamily = FontFamily;
        Editor.Padding = new(10, 9, 14, 8); Editor.LineNumbersForeground = Palette.Brush("#666372");
        Editor.TextArea.SelectionBrush = Palette.Brush("#4F423E64");
        Grid.SetRow(Editor, 1); minimalRoot.Children.Add(Editor);
        var search = SearchPanel.Install(Editor);
        search.MarkerBrush = Palette.Brush("#70544478");
        search.Background = Palette.Panel; search.Foreground = Palette.Text;

        var footer = new DockPanel { Margin = new(14, 0, 14, 4) };
        minimalPosition = new TextBlock { Foreground = Palette.Brush("#77717F"), FontSize = 10, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(minimalPosition, Dock.Right); footer.Children.Add(minimalPosition);
        minimalStatus = new TextBlock { Text = "DERT", FontSize = 10, Foreground = Palette.Muted,
            TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        minimalStatus.MouseLeftButtonUp += (_, _) => ShowWorkingPanel("Document status", new TextBlock
        { Text = notice.Text + "\n\n" + state.Text, Foreground = Palette.Text, TextWrapping = TextWrapping.Wrap, Margin = new(10) });
        footer.Children.Add(minimalStatus); Grid.SetRow(footer, 2); minimalRoot.Children.Add(footer);

        workingContent = new ContentControl { Margin = new(13, 7, 13, 12), HorizontalContentAlignment = HorizontalAlignment.Stretch };
        panelTitle = new TextBlock { Foreground = Palette.Signal, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        var panelBody = new DockPanel();
        var panelHeader = new DockPanel { Margin = new(14, 10, 9, 0) };
        var back = Action("Back to note", () => CloseWorkingPanel(), small: true); back.FontSize = 11;
        DockPanel.SetDock(back, Dock.Right); panelHeader.Children.Add(back); panelHeader.Children.Add(panelTitle);
        DockPanel.SetDock(panelHeader, Dock.Top); panelBody.Children.Add(panelHeader); panelBody.Children.Add(workingContent);
        ((Panel)notice.Parent).Children.Remove(notice);
        notice.FontSize = 11; notice.Margin = new(14, 5, 14, 3);
        DockPanel.SetDock(notice, Dock.Top); panelBody.Children.Insert(1, notice);
        workingPanel = new Border { Background = Palette.Panel, BorderBrush = Palette.Brush("#494253"), BorderThickness = new(1), CornerRadius = new(7),
            Margin = new(8, 0, 8, 3), Child = panelBody, Visibility = Visibility.Collapsed };
        Grid.SetRow(workingPanel, 1); Panel.SetZIndex(workingPanel, 10); minimalRoot.Children.Add(workingPanel);

        minimalMenu = new MinimalMenuHost(this, () => still || preferences.ReducedMotion,
            () => Session.HoldAutomaticBoundary = true,
            () => { Session.HoldAutomaticBoundary = Result is not null || sourceOperationRunning; if (!IsWorkingPanelOpen) Editor.Focus(); });
        minimalMenu.AddMenu("file", "File", FileMenu);
        minimalMenu.AddMenu("edit", "Edit", EditMenu);
        minimalMenu.AddMenu("note", "Note", NoteMenu);
        minimalMenu.AddMenu("operations", "Tools", OperationsMenu);
        minimalMenu.AddMenu("settings", "Settings", SettingsMenu);
        minimalMenu.AddMenu("help", "Help", HelpMenu);
        Grid.SetRowSpan(minimalMenu, 3); Panel.SetZIndex(minimalMenu, 30); minimalRoot.Children.Add(minimalMenu);

        // Keep the system outline and meet it with short neutral highlights.
        // The user's screenshots show a #39393A outer edge. The curve sits
        // directly beside it, with no dark gap or separate purple/blue contour.
        var edgeLight = new LinearGradientBrush
        {
            StartPoint = new(0, 1), EndPoint = new(1, 0),
            GradientStops = new()
            {
                new(Colors.Transparent, 0), new(Palette.Brush("#39393A").Color, 0.42),
                new(Palette.Brush("#39393A").Color, 0.52), new(Colors.Transparent, 1)
            }
        };
        edgeLight.Freeze();
        var upperLight = new System.Windows.Shapes.Path { Data = Geometry.Parse("M 1.25,38 L 1.25,8 A 6.75,6.75 0 0 1 8,1.25 L 90,1.25"),
            Stroke = edgeLight, StrokeThickness = 0.75,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
        Grid.SetRowSpan(upperLight, 3); Panel.SetZIndex(upperLight, 50); minimalRoot.Children.Add(upperLight);
        var lowerLight = new System.Windows.Shapes.Path { Data = Geometry.Parse("M 0,22.75 L 44,22.75 A 6.75,6.75 0 0 0 50.75,16 L 50.75,0"),
            Stroke = new LinearGradientBrush(Colors.Transparent, Palette.Brush("#39393A").Color, 0), StrokeThickness = 0.6,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new(0, 0, 0.875, 0.95), IsHitTestVisible = false };
        Grid.SetRowSpan(lowerLight, 3); Panel.SetZIndex(lowerLight, 50); minimalRoot.Children.Add(lowerLight);
        Activated += (_, _) => ReloadPreferences();
        Closing += (_, e) => { if (IsOperationRunning) { e.Cancel = true; ShowOperationNotice("The DERT command is still running. Keep this window open until it finishes."); } };
        ReasoningView.SourceRequested += () => { workingPanel.Visibility = Visibility.Collapsed; Editor.Focus(); };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.P && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            { OpenOperations(); e.Handled = true; }
            else if (e.Key == Key.Escape && IsWorkingPanelOpen && !ContextPending()) { CloseWorkingPanel(); e.Handled = true; }
        };
        ReloadPreferences();
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    public void OpenMinimalMenu(string id) => minimalMenu?.OpenMenu(id);

    private IReadOnlyList<MinimalMenuItem> FileMenu() =>
    [new("New", NewDocument, Shortcut: "Ctrl+N"), new("Open…", ChooseOpen, Shortcut: "Ctrl+O"),
     new("Save", () => Save(), Shortcut: "Ctrl+S"), new("Save as…", () => Save(true), Shortcut: "Ctrl+Shift+S"),
     new("-"), new("Close", Close, Shortcut: "Alt+F4")];

    private IReadOnlyList<MinimalMenuItem> EditMenu() =>
    [new("Undo", () => { if (!DocumentBusy()) Session.Undo(); }, Shortcut: "Ctrl+Z"), new("Redo", () => { if (!DocumentBusy()) Session.Redo(); }, Shortcut: "Ctrl+Y"), new("-"),
     new("Cut", Editor.Cut), new("Copy", Editor.Copy), new("Paste", Editor.Paste), new("Select all", Editor.SelectAll),
     new("Find…", () => ApplicationCommands.Find.Execute(null, Editor), Shortcut: "Ctrl+F")];

    private IReadOnlyList<MinimalMenuItem> NoteMenu() =>
    [new("Inspect this line", () => { InspectReasoning(); ShowWorkingPanel("This line", contextScroll); }, Shortcut: "Ctrl+Space"),
     new("Align title…", () => { PreviewTitle(); ShowWorkingPanel("Review title", contextScroll); }),
     new("List", Children: [new("Add another item…", () => { if (DocumentBusy()) return; Lists.BeginAdd(); ShowWorkingPanel("List item", contextScroll); }),
         new("Finish this list…", () => { if (DocumentBusy()) return; Lists.PreviewFinish(); ShowWorkingPanel("List ending", contextScroll); })]),
     new("Literal text…", () => { if (DocumentBusy()) return; Literals.Begin(); ShowWorkingPanel("Literal text", contextScroll); }, Shortcut: "Ctrl+Shift+L"),
     new("Choose context…", () => { InspectReasoning(); CreateContextSelection()?.ShowDialog(); }),
     new("Keep result…", ChooseResult), new("Return to note", () => { ReturnToNote(); CloseWorkingPanel(); })];

    private IReadOnlyList<MinimalMenuItem> OperationsMenu() =>
    [new("Command…", () => OpenOperations(), Shortcut: "Ctrl+Shift+P"),
     new("This document", Children: [new("Inspect", () => OpenFileOperation("inspect")), new("Lint", () => OpenFileOperation("lint")),
         new("Preview operations", () => OpenFileOperation("run", "--preview")), new("Run operations", () => OpenFileOperation("run"))]),
     new("Find an application…", () => OpenOperations("where codex")),
     new("Doctor Codex…", () => OpenOperations("where codex --doctor")),
     new("Review Codex update…", () => OpenOperations("update codex updateme"))];

    private IReadOnlyList<MinimalMenuItem> SettingsMenu()
    {
        ReloadPreferences();
        return [new("Windows menu fade", () => ChangePreferences(preferences with { WindowsMenuFade = !preferences.WindowsMenuFade }), IsChecked: () => preferences.WindowsMenuFade),
            new("Word wrap", () => ChangePreferences(preferences with { WordWrap = !preferences.WordWrap }), IsChecked: () => preferences.WordWrap),
            new("Line numbers", () => ChangePreferences(preferences with { ShowLineNumbers = !preferences.ShowLineNumbers }), IsChecked: () => preferences.ShowLineNumbers),
            new("Reduced motion", () => ChangePreferences(preferences with { ReducedMotion = !preferences.ReducedMotion }), IsChecked: () => preferences.ReducedMotion),
            new("Text size", Children: [new("Larger", () => ChangePreferences(preferences with { FontSize = Math.Min(48, preferences.FontSize + 1) })),
                new("Smaller", () => ChangePreferences(preferences with { FontSize = Math.Max(8, preferences.FontSize - 1) })),
                new("Reset to 14", () => ChangePreferences(preferences with { FontSize = 14 }))]),
            new("Settings details", () => ShowWorkingPanel("Shared settings", new TextBlock { Text = "These preferences are also available through DERT settings.\n\n" + SettingsPath,
                Foreground = Palette.Text, TextWrapping = TextWrapping.Wrap }))];
    }

    private IReadOnlyList<MinimalMenuItem> HelpMenu() =>
    [new("DERT Help…", () => OpenHelp()), new("Troubleshoot…", () => OpenHelp(true)),
     new("CLI reference", () => OpenOperations("help")), new("-"),
     new("About MIVIM", () => ShowWorkingPanel("MIVIM / minimal", new TextBlock { Text = "DERT\n\nExpressive notes. Useful operations.\n\nM I V I M — minimal",
         Foreground = Palette.Text, TextWrapping = TextWrapping.Wrap, FontSize = 14, Margin = new(12) }))];

    private void ReloadPreferences()
    {
        if (settingsStore is null) return;
        try { preferences = settingsStore.Read(); settingsFailed = false; ApplyPreferences(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        { settingsFailed = true; notice.Text = error.Message; notice.Foreground = Palette.Copper; UpdateMinimalStatus(); }
    }

    private void ChangePreferences(DesktopSettings changed)
    {
        if (settingsStore is null) return;
        try { settingsStore.Save(changed); preferences = changed; settingsFailed = false; ApplyPreferences(); notice.Text = "Settings saved for Notes and the CLI."; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        { settingsFailed = true; notice.Text = error.Message; notice.Foreground = Palette.Copper; ShowWorkingPanel("Settings need attention", new TextBlock { Text = error.Message, Foreground = Palette.Copper, TextWrapping = TextWrapping.Wrap }); }
        UpdateMinimalStatus();
    }

    private void ApplyPreferences()
    {
        Editor.FontSize = preferences.FontSize; Editor.WordWrap = preferences.WordWrap; Editor.ShowLineNumbers = preferences.ShowLineNumbers;
        if (minimalMenu is not null) minimalMenu.FadeEnabled = preferences.WindowsMenuFade;
    }

    private void UpdateMinimalStatus()
    {
        if (!IsCompact || minimalStatus is null || minimalPosition is null || minimalMenu is null) return;
        minimalMenu.Caption.Text = name.Text + (Session.IsDirty ? "  *" : "") + "    / DERT";
        minimalPosition.Text = position.Text;
        minimalStatus.Text = IsOperationRunning ? "DERT / command running" : needsAttention || settingsFailed ? notice.Text : Session.IsDirty ? "●  unsaved" : opened is null ? "DERT" : "saved";
        minimalStatus.Foreground = needsAttention || settingsFailed ? Palette.Copper : Palette.Muted;
        minimalStatus.ToolTip = notice.Text;
    }

    private void ShowWorkingPanel(string title, object content)
    {
        if (workingPanel is null || workingContent is null || panelTitle is null) return;
        if (ContextPending() && !ReferenceEquals(content, contextScroll))
        { notice.Text = "Apply or leave the current proposal before opening another panel."; content = contextScroll; title = "Your pending edit"; }
        notice.Visibility = ReferenceEquals(content, operations) ? Visibility.Collapsed : Visibility.Visible;
        panelTitle.Text = title; workingContent.Content = content; workingPanel.Visibility = Visibility.Visible;
        LayoutProposal();
    }

    public bool CloseWorkingPanel()
    {
        if (ContextPending()) { notice.Text = "Apply or leave the current proposal before returning to your note."; return false; }
        if (workingPanel is null) return true;
        workingPanel.Visibility = Visibility.Collapsed;
        ReturnToNote(); Editor.Focus(); return true;
    }
}
