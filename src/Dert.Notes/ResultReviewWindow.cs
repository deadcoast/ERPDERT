using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Input;
using Dert.Core;
using ICSharpCode.AvalonEdit;

namespace Dert.Notes;

public sealed class ResultReviewWindow : Window
{
    private readonly Func<bool> current;
    private readonly Func<bool> apply;
    private readonly Button applyButton;
    private readonly TextBlock reading = NotesWindow.Label("", Palette.Signal, 15);
    private readonly Dictionary<string, Button> views = [];
    private readonly DertColorizer colorizer;
    private bool stale, finished;
    public ResultNotePlan Plan { get; }
    public TextEditor Preview { get; }
    public TextBlock Status { get; } = NotesWindow.Label("", Palette.Muted, 13);

    internal ResultReviewWindow(ResultNotePlan plan, Func<bool> current, Func<bool> apply, bool compact = false)
    {
        Plan = plan; this.current = current; this.apply = apply;
        Title = "Keep result / DERT"; Width = 1180; Height = 780; MinWidth = 720; MinHeight = 580;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Palette.Background; Foreground = Palette.Text; FontFamily = new("Segoe UI"); FontSize = 14;
        if (compact) CompactReviewPresentation.Configure(this);
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/DERT.Notes;component/Theme.xaml", UriKind.Relative) });
        var frame = new Grid { Margin = new(compact ? 12 : 24) };
        frame.RowDefinitions.Add(new() { Height = GridLength.Auto });
        frame.RowDefinitions.Add(new() { Height = GridLength.Auto });
        frame.RowDefinitions.Add(new() { Height = GridLength.Auto });
        frame.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        frame.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var heading = new StackPanel();
        var title = new DockPanel();
        applyButton = Action("Apply", Apply, true);
        applyButton.ToolTip = "Append the reviewed note to the editor. Undo is available; Save is separate.";
        if (compact)
        {
            var primaryActions = new StackPanel { Orientation = Orientation.Horizontal };
            primaryActions.Children.Add(applyButton); primaryActions.Children.Add(Action("Back", Close));
            DockPanel.SetDock(primaryActions, Dock.Right); title.Children.Add(primaryActions);
        }
        title.Children.Add(NotesWindow.Label("DERT / keep this result", Palette.Text, compact ? 16 : 24));
        heading.Children.Add(title);
        if (!compact) heading.Children.Add(NotesWindow.Label("Bring an observed result back to your words.", Palette.Muted, 14));
        var summary = NotesWindow.Label($"{plan.Observation.Target.ReplaceLineEndings(" ")} / {plan.Observation.Outcome} / observed {plan.Observation.ObservedAt:yyyy-MM-dd HH:mm:ss zzz}", Palette.Signal, compact ? 11 : 14);
        summary.TextWrapping = TextWrapping.NoWrap; summary.TextTrimming = TextTrimming.CharacterEllipsis;
        summary.ToolTip = summary.Text;
        heading.Children.Add(summary);
        frame.Children.Add(heading);
        var actions = new WrapPanel { Margin = new(0, compact ? 3 : 12, 0, 0) };
        if (!compact) { actions.Children.Add(applyButton); actions.Children.Add(NotesWindow.Action("Back", Close)); }
        foreach (var name in new[] { "Observation", "Source comparison", "Insertion" })
        {
            var button = Action(name, () => ShowReading(name)); views.Add(name, button); actions.Children.Add(button);
        }
        frame.Children.Add(actions); Grid.SetRow(actions, 1);
        if (compact)
        {
            reading.FontSize = 11; reading.Margin = new(0);
            var explanation = new ScrollViewer { Content = reading, MaxHeight = 32, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new(0, 3, 0, 6), Focusable = true };
            AutomationProperties.SetName(explanation, "Current reading explanation");
            frame.Children.Add(explanation); Grid.SetRow(explanation, 2);
        }
        else { reading.Margin = new(0, 7, 0, 10); frame.Children.Add(reading); Grid.SetRow(reading, 2); }
        Preview = new TextEditor
        {
            IsReadOnly = true, WordWrap = true, FontFamily = new(compact ? "Cascadia Mono, Consolas" : "Consolas"), FontSize = compact ? 12 : 15,
            Foreground = Palette.Text, Background = Palette.Background,
            BorderBrush = Palette.Brush("#403B4D"), BorderThickness = new(1), Padding = new(compact ? 9 : 14),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Preview.Options.EnableHyperlinks = false; Preview.Options.EnableEmailHyperlinks = false;
        Preview.TextArea.SelectionBrush = Palette.Brush("#39334F");
        colorizer = new(() => DertParser.Parse(Plan.NoteText));
        frame.Children.Add(Preview); Grid.SetRow(Preview, 3);
        if (compact) CompactReviewPresentation.AddStatus(frame, Status, 4);
        else { Status.Margin = new(0, 12, 0, 0); frame.Children.Add(Status); Grid.SetRow(Status, 4); }
        Content = compact ? CompactReviewPresentation.Frame(frame) : frame;
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
        Closed += (_, _) => { finished = true; applyButton.IsEnabled = false; };
        ShowReading("Insertion");

        Button Action(string text, Action click, bool primary = false) => compact
            ? CompactReviewPresentation.Action(text, click, primary) : NotesWindow.Action(text, click, primary);
    }

    private void ShowReading(string name)
    {
        Preview.TextArea.TextView.LineTransformers.Remove(colorizer);
        if (name == "Insertion")
        {
            Preview.Text = Plan.NoteText;
            Preview.TextArea.TextView.LineTransformers.Add(colorizer);
            reading.Text = Plan.IsReady
                ? "Exact note to append / " + (Plan.Source.Length == 0 ? "this will be the first note in your document."
                    : "your existing words stay in place. A separating blank line keeps this note apart.")
                : "Insertion unavailable / " + Plan.Explanation;
        }
        else if (name == "Observation")
        {
            Preview.Text = ResultNotes.Describe(Plan.Observation);
            reading.Text = "The saved observation / what its producer reported at that time.";
        }
        else
        {
            var comparison = Plan.Comparison;
            reading.Text = "Source comparison / " + comparison.Message;
            Preview.Text = $"Historical source comparison / {comparison.State}\n" +
                "This compares authored words. It does not check today's installations or repeat the operation.\n\n" +
                ResultNotes.DescribeComparison(comparison) +
                "\nThe full captured path and source revision are available in Observation. This comparison does not replace that historical origin.";
        }
        foreach (var (label, button) in views)
        {
            button.BorderBrush = label == name ? Palette.Signal : Palette.Brush("#403B4D");
            AutomationProperties.SetHelpText(button, label == name ? "Current reading" : "Show " + label.ToLowerInvariant());
        }
        AutomationProperties.SetName(Preview, name == "Insertion" ? "Exact result note to append" : name);
        Preview.ScrollToHome();
        Status.Foreground = Plan.IsReady ? Palette.Muted : Palette.Copper;
        Status.Text = Plan.IsReady
            ? "Historical observation / Apply adds this note to your buffer. Undo reverses it; Save keeps it on disk."
            : Plan.Explanation + " The historical observation and source comparison remain available.";
        applyButton.IsEnabled = Plan.IsReady && !finished && !stale;
        if (stale) CheckCurrent();
    }

    internal bool CheckCurrent()
    {
        if (finished) return false;
        if (stale || !current())
        {
            stale = true; applyButton.IsEnabled = false;
            Status.Text = "The source, its document identity or an open proposal changed. Go back and open a fresh result review.";
            Status.Foreground = Palette.Copper; return false;
        }
        return true;
    }

    private void Apply()
    {
        if (!CheckCurrent() || !Plan.IsReady) return;
        // Latch before the callback: native change events can re-enter the view,
        // and an old button reference must never apply this observation twice.
        finished = true; applyButton.IsEnabled = false;
        if (apply()) Close();
        else
        {
            finished = false; stale = true; CheckCurrent();
        }
    }
}
