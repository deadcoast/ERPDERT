using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Dert.Notes;

public sealed record MinimalMenuItem(string Label, Action? Invoke = null,
    IReadOnlyList<MinimalMenuItem>? Children = null, Func<bool>? IsChecked = null, string? Shortcut = null);

/// <summary>Click opens a branch. Pointer geometry anchors exactly one menu surface.</summary>
public sealed class MinimalMenuHost : Grid
{
    private sealed record Branch(string Id, Border Surface, Button Opener);
    private readonly Window owner;
    private readonly Func<bool> still;
    private readonly Action beginInteraction;
    private readonly Action finishInteraction;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly DispatcherTimer timer;
    private readonly Canvas flyouts = new();
    private readonly StackPanel headings = new() { Orientation = Orientation.Horizontal, Margin = new(10, 0, 0, 0) };
    private readonly Dictionary<string, (Button Button, Func<IReadOnlyList<MinimalMenuItem>> Items)> menus = new();
    private readonly List<Branch> branches = [];
    private bool keyboardMode;
    private IInputElement? previousFocus;
    public MenuFadeState State { get; } = new();
    public Border RootChrome { get; }
    public TextBlock Caption { get; } = new() { FontSize = 11, Foreground = Palette.Muted, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    public bool FadeEnabled { get => State.FadeEnabled; set { State.FadeEnabled = value; Refresh(); } }

    public MinimalMenuHost(Window owner, Func<bool> still, Action beginInteraction, Action finishInteraction)
    {
        this.owner = owner; this.still = still; this.beginInteraction = beginInteraction; this.finishInteraction = finishInteraction;
        var rows = new Grid(); rows.RowDefinitions.Add(new() { Height = new(26) }); rows.RowDefinitions.Add(new() { Height = new(25) });
        var captionRow = new DockPanel { Margin = new(14, 0, 8, 0) };
        var controls = new StackPanel { Orientation = Orientation.Horizontal };
        controls.Children.Add(WindowControl("−", "Minimize", () => SystemCommands.MinimizeWindow(owner)));
        controls.Children.Add(WindowControl("□", "Maximize or restore", ToggleMaximize));
        controls.Children.Add(WindowControl("×", "Close", owner.Close));
        DockPanel.SetDock(controls, Dock.Right); captionRow.Children.Add(controls);
        captionRow.Children.Add(Caption);
        Caption.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2) ToggleMaximize();
            else if (e.LeftButton == MouseButtonState.Pressed) owner.DragMove();
            e.Handled = true;
        };
        rows.Children.Add(captionRow); Grid.SetRow(headings, 1); rows.Children.Add(headings);
        RootChrome = new Border { Height = 51, VerticalAlignment = VerticalAlignment.Top, Background = Brushes.Transparent,
            CornerRadius = new(8, 8, 0, 0), Child = rows, Tag = MenuFadeState.RootRegionId };
        Children.Add(RootChrome); Children.Add(flyouts);
        owner.PreviewMouseDown += OnMouseDown;
        owner.PreviewMouseMove += (_, e) => PointerAt(e.GetPosition(this));
        owner.MouseLeave += (_, _) => { State.HoverRegion(null); Refresh(); };
        owner.PreviewKeyDown += OnKeyDown;
        owner.PreviewGotKeyboardFocus += (_, e) =>
        {
            State.SetKeyboardRegion(keyboardMode ? RegionOf(e.NewFocus as DependencyObject) : null);
            Refresh();
        };
        owner.Deactivated += (_, _) => { State.HoverRegion(null); State.SetKeyboardRegion(null); Refresh(); };
        timer = new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Background, (_, _) => Refresh(), Dispatcher);
        Loaded += (_, _) => { State.ClickWindow(clock.Elapsed); timer.Start(); Refresh(); };
        Unloaded += (_, _) => timer.Stop();
    }

    private Button WindowControl(string symbol, string label, Action action)
    {
        var button = FlatButton(symbol); button.Width = 30; button.FontSize = 14; button.Padding = new(0);
        button.HorizontalContentAlignment = HorizontalAlignment.Center; button.ToolTip = label;
        System.Windows.Automation.AutomationProperties.SetName(button, label);
        button.Click += (_, _) => action(); return button;
    }

    private void ToggleMaximize()
    {
        if (owner.WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(owner);
        else SystemCommands.MaximizeWindow(owner);
    }

    public void AddMenu(string id, string label, Func<IReadOnlyList<MinimalMenuItem>> items)
    {
        var button = FlatButton(label); button.Padding = new(8, 2, 8, 3); button.FontSize = 11;
        button.Click += (_, _) => OpenMenu(id);
        menus.Add(id, (button, items)); headings.Children.Add(button);
    }

    public void OpenMenu(string id)
    {
        if (!menus.TryGetValue(id, out var menu)) return;
        beginInteraction(); ClearBranches();
        var point = menu.Button.TranslatePoint(new Point(0, menu.Button.ActualHeight + 4), this);
        AddBranch(id, menu.Items(), point, menu.Button);
        State.OpenPath([MenuFadeState.RootRegionId, id], clock.Elapsed);
        if (keyboardMode) FocusFirst(branches[0]);
        Refresh();
    }

    private void AddBranch(string id, IReadOnlyList<MinimalMenuItem> items, Point at, Button opener)
    {
        var content = new StackPanel { Margin = new(5) };
        var surface = new Border { Width = 222, CornerRadius = new(7), BorderThickness = new(1),
            BorderBrush = Palette.Brush("#514B63"), Background = Palette.Brush("#F00F1017"), Tag = id,
            Child = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = Math.Max(100, owner.ActualHeight - 72) } };
        var branch = new Branch(id, surface, opener);
        foreach (var item in items)
        {
            if (item.Label == "-") { content.Children.Add(new Border { Height = 1, Background = Palette.Brush("#343140"), Margin = new(7, 5, 7, 5) }); continue; }
            var button = FlatButton(item.Label);
            button.Tag = item;
            button.FontSize = 12; button.Padding = new(10, 7, 10, 7);
            button.ToolTip = item.Shortcut;
            // The complete accessible action name remains the button's content.
            if (item.IsChecked is not null) button.Content = (item.IsChecked() ? "[x] " : "[ ] ") + item.Label;
            if (item.Children is not null) button.Content = item.Label + "  >";
            button.Click += (_, _) =>
            {
                if (item.Children is not null)
                {
                    var depth = branches.IndexOf(branch);
                    for (var i = branches.Count - 1; i > depth; i--) { flyouts.Children.Remove(branches[i].Surface); branches.RemoveAt(i); }
                    var childPoint = button.TranslatePoint(new Point(surface.Width - 10, 0), this);
                    if (childPoint.X + 222 > ActualWidth - 8) childPoint.X = Math.Max(8, Canvas.GetLeft(surface) - 224);
                    var childId = id + "/" + content.Children.IndexOf(button);
                    AddBranch(childId, item.Children, childPoint, button);
                    State.OpenPath(new[] { MenuFadeState.RootRegionId }.Concat(branches.Select(b => b.Id)), clock.Elapsed);
                    if (keyboardMode) FocusFirst(branches[^1]);
                    Refresh();
                }
                else
                {
                    CloseMenus(false);
                    try { item.Invoke?.Invoke(); }
                    finally { finishInteraction(); }
                }
            };
            content.Children.Add(button);
        }
        branches.Add(branch); flyouts.Children.Add(surface);
        surface.Measure(new Size(222, Math.Max(100, owner.ActualHeight - 60)));
        Canvas.SetLeft(surface, Math.Clamp(at.X, 6, Math.Max(6, owner.ActualWidth - 230)));
        Canvas.SetTop(surface, Math.Clamp(at.Y, 51, Math.Max(51, owner.ActualHeight - surface.DesiredSize.Height - 8)));
    }

    public string? RegionAt(Point point)
    {
        for (var i = branches.Count - 1; i >= 0; i--)
            if (Contains(branches[i].Surface, point)) return branches[i].Id;
        return Contains(RootChrome, point) ? MenuFadeState.RootRegionId : null;
    }

    private bool Contains(FrameworkElement element, Point point)
    {
        var origin = element.TranslatePoint(new Point(), this);
        return new Rect(origin, element.RenderSize).Contains(point);
    }

    public void PointerAt(Point point)
    {
        keyboardMode = false; State.SetKeyboardRegion(null);
        var id = RegionAt(point); State.HoverRegion(id);
        RootChrome.Background = id == MenuFadeState.RootRegionId ? Palette.Brush("#CC161620") : Brushes.Transparent;
        Refresh();
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        keyboardMode = false; State.SetKeyboardRegion(null);
        State.ClickWindow(clock.Elapsed);
        var region = RegionAt(e.GetPosition(this));
        if (region is not null) beginInteraction();
        else CloseMenus();
        State.HoverRegion(region); Refresh();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if ((Keyboard.Modifiers == ModifierKeys.Alt && key is Key.F or Key.E or Key.N or Key.O or Key.S or Key.H) || key == Key.F10)
        {
            if (!keyboardMode) previousFocus = Keyboard.FocusedElement;
            keyboardMode = true; beginInteraction(); State.ClickWindow(clock.Elapsed);
            if (key == Key.F10) { menus.First().Value.Button.Focus(); State.SetKeyboardRegion(MenuFadeState.RootRegionId); }
            else OpenMenu(key switch { Key.F => "file", Key.E => "edit", Key.N => "note", Key.O => "operations", Key.S => "settings", _ => "help" });
            e.Handled = true; Refresh(); return;
        }
        var focused = FocusedMenuButton();
        var rootItem = menus.FirstOrDefault(pair => ReferenceEquals(pair.Value.Button, focused));
        if (rootItem.Key is not null && key is Key.Down or Key.Up)
        { keyboardMode = true; OpenMenu(rootItem.Key); e.Handled = true; return; }
        if (rootItem.Key is not null && key is Key.Left or Key.Right)
        {
            var buttons = menus.Values.Select(menu => menu.Button).ToArray();
            var index = Array.IndexOf(buttons, focused);
            keyboardMode = true; FocusButton(buttons[(index + buttons.Length + (key == Key.Right ? 1 : -1)) % buttons.Length]);
            e.Handled = true; Refresh(); return;
        }
        if (branches.Count == 0)
        {
            if (key == Key.Escape && keyboardMode) { CloseMenus(); e.Handled = true; }
            return;
        }
        if (key == Key.Escape)
        {
            BackOneLevel(); e.Handled = true;
        }
        else if (key == Key.Right && focused is { Tag: MinimalMenuItem { Children: not null } })
        {
            keyboardMode = true; focused.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); e.Handled = true;
        }
        else if (key == Key.Left && focused is not null)
        {
            BackOneLevel(); e.Handled = true;
        }
        else if (key is Key.Down or Key.Up && focused is not null)
        {
            keyboardMode = true;
            var branch = branches.FirstOrDefault(branch => branch.Id == RegionOf(focused));
            if (branch?.Surface.Child is ScrollViewer { Content: StackPanel content })
            {
                var buttons = content.Children.OfType<Button>().Where(button => button.IsEnabled && button.IsVisible).ToArray();
                var index = Array.IndexOf(buttons, focused);
                if (buttons.Length > 0) FocusButton(buttons[(index + buttons.Length + (key == Key.Down ? 1 : -1)) % buttons.Length]);
            }
            e.Handled = true; Refresh();
        }
    }

    private void BackOneLevel()
    {
        if (branches.Count <= 1) { CloseMenus(); return; }
        var opener = branches[^1].Opener;
        flyouts.Children.Remove(branches[^1].Surface); branches.RemoveAt(branches.Count - 1);
        keyboardMode = true;
        State.OpenPath(new[] { MenuFadeState.RootRegionId }.Concat(branches.Select(b => b.Id)), clock.Elapsed);
        State.HoverRegion(null); FocusButton(opener); Refresh();
    }

    private string? RegionOf(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is Border { Tag: string id } && State.IsOpen(id)) return id;
            element = element is Visual ? VisualTreeHelper.GetParent(element) : null;
        }
        return null;
    }

    private void FocusFirst(Branch branch)
    {
        branch.Surface.UpdateLayout();
        if (branch.Surface.Child is ScrollViewer { Content: StackPanel content })
            if (content.Children.OfType<Button>().FirstOrDefault(button => button.IsEnabled) is { } first) FocusButton(first);
        State.SetKeyboardRegion(branch.Id);
    }

    private Button? FocusedMenuButton()
    {
        if (Keyboard.FocusedElement is Button active && RegionOf(active) is not null) return active;
        return FocusManager.GetFocusedElement(owner) is Button logical && RegionOf(logical) is not null ? logical : null;
    }

    private void FocusButton(Button button)
    {
        FocusManager.SetFocusedElement(owner, button); button.Focus(); button.BringIntoView();
        State.SetKeyboardRegion(RegionOf(button));
    }

    public void CloseMenus(bool finish = true)
    {
        ClearBranches(); State.CloseToRoot(); State.SetKeyboardRegion(null);
        if (finish)
        {
            finishInteraction();
            if (keyboardMode && previousFocus is UIElement { IsVisible: true, IsEnabled: true } target) target.Focus();
        }
        keyboardMode = false; previousFocus = null;
        Refresh();
    }

    private void ClearBranches() { branches.Clear(); flyouts.Children.Clear(); }

    public void Refresh()
    {
        SetOpacity(RootChrome, State.SurfaceOpacity(MenuFadeState.RootRegionId, clock.Elapsed));
        foreach (var branch in branches)
        {
            var opacity = State.SurfaceOpacity(branch.Id, clock.Elapsed);
            SetOpacity(branch.Surface, opacity);
            branch.Surface.IsHitTestVisible = opacity > 0;
        }
        if (branches.Count > 0 && branches.All(b => State.SurfaceOpacity(b.Id, clock.Elapsed) == 0 && b.Surface.Opacity < 0.01))
        { ClearBranches(); State.CloseToRoot(); finishInteraction(); }
    }

    private void SetOpacity(UIElement element, double value)
    {
        if (Equals(element.GetValue(TargetOpacityProperty), value)) return;
        element.SetValue(TargetOpacityProperty, value);
        var previous = element.Opacity;
        element.BeginAnimation(OpacityProperty, null);
        if (still()) element.Opacity = value;
        else element.BeginAnimation(OpacityProperty, new DoubleAnimation(previous, value, TimeSpan.FromMilliseconds(value == 0 ? 220 : 90)));
    }

    private static readonly DependencyProperty TargetOpacityProperty = DependencyProperty.RegisterAttached("TargetOpacity", typeof(double), typeof(MinimalMenuHost), new PropertyMetadata(double.NaN));

    private static Button FlatButton(string text) => new()
    {
        Content = text, Foreground = Palette.Text, Background = Brushes.Transparent, BorderBrush = Brushes.Transparent,
        BorderThickness = new(1), HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new(0),
        FontFamily = new FontFamily("Cascadia Mono, Consolas"), Padding = new(6, 2, 6, 2), Focusable = true
    };
}
