using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Dert.Core;
using Dert.Cli.Authoring;
using ICSharpCode.AvalonEdit;
using Microsoft.Win32;

namespace Dert.Notes;

public sealed class ContextSelectionWindow : Window
{
    private readonly ContextSelection selection;
    private readonly Func<bool> current;
    private readonly Action<string> copyText;
    private readonly HashSet<int> included = [];
    private readonly Grid body = new();
    private readonly StackPanel choices = new();
    private readonly ScrollViewer choiceScroll;
    private readonly Button copy, export, toggle;
    private readonly TextBlock summary = NotesWindow.Label("", Palette.Signal, 14);
    private int page;
    private bool leftOut, stale;
    public ContextPacket Packet { get; private set; }
    public TextEditor Preview { get; }
    public TextBlock Status { get; } = NotesWindow.Label("Review the exact text. Copy or export when it carries what you mean.", Palette.Muted, 13);

    internal ContextSelectionWindow(ContextSelection selection, Func<bool> current, Action<string>? copyText = null)
    {
        this.selection = selection; this.current = current; this.copyText = copyText ?? Clipboard.SetText;
        Packet = selection.Build();
        Title = "Context selection / DERT"; Width = 1180; Height = 780; MinWidth = 720; MinHeight = 580;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Palette.Background; Foreground = Palette.Text; FontFamily = new("Segoe UI"); FontSize = 14;
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/DERT.Notes;component/Theme.xaml", UriKind.Relative) });
        var frame = new Grid { Margin = new(24) };
        frame.RowDefinitions.Add(new() { Height = GridLength.Auto });
        frame.RowDefinitions.Add(new() { Height = GridLength.Auto });
        frame.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        frame.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var heading = new StackPanel();
        heading.Children.Add(NotesWindow.Label("DERT / context", Palette.Text, 24));
        heading.Children.Add(NotesWindow.Label("Choose the notes that travel together.", Palette.Muted, 14));
        heading.Children.Add(summary); frame.Children.Add(heading);
        var actions = new WrapPanel { Margin = new(0, 12, 0, 12) };
        copy = NotesWindow.Action("Copy preview", CopyPreview, true);
        copy.ToolTip = "Ctrl+C / copy the complete reviewed text. Nothing is sent to an agent.";
        export = NotesWindow.Action("Export…", ChooseExport);
        toggle = NotesWindow.Action("Left out", () => { leftOut = !leftOut; ShowText(); });
        actions.Children.Add(copy); actions.Children.Add(export); actions.Children.Add(toggle);
        actions.Children.Add(NotesWindow.Action("Back to note", Close));
        frame.Children.Add(actions); Grid.SetRow(actions, 1);
        body.ColumnDefinitions.Add(new() { Width = new(270) }); body.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        body.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) }); body.RowDefinitions.Add(new() { Height = new(0) });
        choiceScroll = new() { Content = choices, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new(0, 0, 18, 0) };
        body.Children.Add(choiceScroll);
        Preview = new TextEditor
        {
            IsReadOnly = true, WordWrap = true, FontFamily = new("Consolas"), FontSize = 14,
            Foreground = Palette.Text, Background = Palette.Background, BorderBrush = Palette.Brush("#354148"), BorderThickness = new(1),
            Padding = new(14), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Preview.Options.EnableHyperlinks = false; Preview.Options.EnableEmailHyperlinks = false;
        Preview.TextArea.SelectionBrush = Palette.Brush("#31504E");
        AutomationProperties.SetName(Preview, "Exact context preview");
        body.Children.Add(Preview); Grid.SetColumn(Preview, 1);
        frame.Children.Add(body); Grid.SetRow(body, 2);
        Status.Margin = new(0, 12, 0, 0); frame.Children.Add(Status); Grid.SetRow(Status, 3);
        Content = frame;
        // Intercept before AvalonEdit's focused TextArea handles its own Copy
        // binding. The complete preview uses one guarded clipboard path.
        CommandManager.AddPreviewCanExecuteHandler(Preview, (_, e) =>
        {
            if (e.Command != ApplicationCommands.Copy) return;
            e.CanExecute = !leftOut && CheckCurrent(); e.Handled = true;
        });
        CommandManager.AddPreviewExecutedHandler(Preview, (_, e) =>
        {
            if (e.Command != ApplicationCommands.Copy) return;
            CopyPreview(); e.Handled = true;
        });
        CommandBindings.Add(new(ApplicationCommands.Copy, (_, e) => { CopyPreview(); e.Handled = true; }));
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
        SizeChanged += (_, _) => Layout();
        RenderChoices(); ShowText();
    }

    private void Layout()
    {
        var narrow = ActualWidth < 950;
        body.ColumnDefinitions[0].Width = narrow ? new(1, GridUnitType.Star) : new(270);
        body.ColumnDefinitions[1].Width = narrow ? new(0) : new(1, GridUnitType.Star);
        body.RowDefinitions[0].Height = narrow ? new(148) : new(1, GridUnitType.Star);
        body.RowDefinitions[1].Height = narrow ? new(1, GridUnitType.Star) : new(0);
        Grid.SetColumn(Preview, narrow ? 0 : 1); Grid.SetRow(Preview, narrow ? 1 : 0);
        choiceScroll.Margin = narrow ? new(0, 0, 0, 12) : new(0, 0, 18, 0);
    }

    private void RenderChoices()
    {
        choices.Children.Clear();
        choices.Children.Add(NotesWindow.Label("Notes in this document", Palette.Signal, 15));
        choices.Children.Add(NotesWindow.Label("Other notes stay out until you include them. A name match does not establish agreement.", Palette.Muted, 12));
        const int size = 6;
        var pages = Math.Max(1, (selection.Notes.Count + size - 1) / size);
        var navigation = new WrapPanel();
        var previous = NotesWindow.Action("Previous notes", () => MovePage(-1), small: true); previous.IsEnabled = page > 0;
        var next = NotesWindow.Action("Next notes", () => MovePage(1), small: true); next.IsEnabled = page + 1 < pages;
        if (pages > 1)
        {
            choices.Children.Add(NotesWindow.Label($"Notes / page {page + 1} of {pages}", Palette.Muted, 12));
            navigation.Children.Add(previous); navigation.Children.Add(next); choices.Children.Add(navigation);
        }
        foreach (var note in selection.Notes.Skip(page * size).Take(size))
        {
            var primary = note.LineNumber == selection.Primary.LineNumber;
            var label = new StackPanel();
            label.Children.Add(NotesWindow.Label($"{note.LineNumber} / {note.Label}", Palette.Text, 14));
            label.Children.Add(NotesWindow.Label(primary ? "Starting note / included" : note.IsRelated ? "Name match / your choice" : "Other reasoning / your choice", Palette.Muted, 12));
            if (!note.IsClosed) label.Children.Add(NotesWindow.Label("Closing // missing", Palette.Copper, 12));
            var box = new CheckBox { Tag = note.LineNumber, Content = label, IsChecked = primary || included.Contains(note.LineNumber), IsEnabled = !primary && !stale, Foreground = Palette.Text, Margin = new(0, 8, 0, 6) };
            AutomationProperties.SetName(box, $"Include note {note.LineNumber}: {note.Label}");
            box.Checked += (_, _) => Choose(note.LineNumber, true);
            box.Unchecked += (_, _) => Choose(note.LineNumber, false);
            choices.Children.Add(box);
        }

        void MovePage(int delta)
        {
            if (!CheckCurrent()) return;
            page = Math.Clamp(page + delta, 0, pages - 1); RenderChoices();
            var focus = PageButtons().FirstOrDefault(button => Equals(button.Content, delta > 0 && page + 1 < pages ? "Next notes" : "Previous notes"));
            focus?.Focus(); choiceScroll.ScrollToTop();
        }
        IEnumerable<Button> PageButtons() => choices.Children.OfType<WrapPanel>().SelectMany(panel => panel.Children.OfType<Button>());
    }

    private void Choose(int line, bool selected)
    {
        if (!CheckCurrent()) { RenderChoices(); return; }
        if (selected) included.Add(line); else included.Remove(line);
        Packet = selection.Build(included); ShowText();
        Status.Text = "Selection changed. Review its exact text before copying or exporting."; Status.Foreground = Palette.Muted;
    }

    private void ShowText()
    {
        toggle.Content = leftOut ? "Preview" : "Left out";
        Preview.Text = leftOut ? Packet.LeftOutReport : Packet.Report;
        Preview.ScrollToHome();
        AutomationProperties.SetName(Preview, leftOut ? "Source left out of the export" : "Exact context preview");
        copy.IsEnabled = export.IsEnabled = !leftOut && !stale;
        summary.Text = $"{Packet.Included.Count} selected {(Packet.Included.Count == 1 ? "note" : "notes")} / {Packet.Excluded.Count} excluded {(Packet.Excluded.Count == 1 ? "range" : "ranges")} / " + (leftOut ? "left out of export" : "exact text to copy or export");
        if (leftOut) { Status.Text = "This source is left out. Include its note or return to Preview before copying/exporting."; Status.Foreground = Palette.Copper; }
        else { Status.Text = "Ctrl+C copies the complete preview. Source stays in your note; nothing is uploaded."; Status.Foreground = Palette.Muted; }
        if (stale) CheckCurrent();
    }

    private bool CheckCurrent()
    {
        if (stale || !current())
        {
            stale = true; copy.IsEnabled = export.IsEnabled = false;
            Status.Text = "The source, its identity or an open proposal changed. Go back and open a fresh context selection."; Status.Foreground = Palette.Copper;
            return false;
        }
        return !stale;
    }

    private bool CanDeliver()
    {
        if (!CheckCurrent()) return false;
        if (!leftOut) return true;
        Status.Text = "Return to Preview to review the text before copying or exporting."; Status.Foreground = Palette.Copper; return false;
    }

    private void CopyPreview()
    {
        if (!CanDeliver()) return;
        if (Packet.Report.Contains('\0'))
        {
            Status.Text = "This preview contains a NUL character. Windows clipboard text would stop there. Use Export to preserve the complete text in a new .txt file.";
            Status.Foreground = Palette.Copper;
            return;
        }
        try { copyText(Packet.Report); Status.Text = "Copied the reviewed text. Paste it where you choose; nothing was uploaded."; Status.Foreground = Palette.Good; }
        catch (Exception error) when (error is ExternalException or InvalidOperationException)
        { Status.Text = "The clipboard could not receive this preview. Your selection is retained; try Copy again. " + error.Message; Status.Foreground = Palette.Copper; }
    }

    private void ChooseExport()
    {
        if (!CanDeliver()) return;
        var dialog = new SaveFileDialog { Filter = "Context text (*.txt)|*.txt", DefaultExt = ".txt", FileName = "Selected context.txt", OverwritePrompt = false };
        if (dialog.ShowDialog(this) == true) ExportTo(dialog.FileName);
    }

    public bool ExportTo(string path)
    {
        if (!CanDeliver()) return false;
        try { var saved = ContextExport.WriteNew(Packet, path); Status.Text = "Export saved / bytes verified. " + saved; Status.Foreground = Palette.Good; return true; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        { Status.Text = error.Message; Status.Foreground = Palette.Copper; return false; }
    }
}
