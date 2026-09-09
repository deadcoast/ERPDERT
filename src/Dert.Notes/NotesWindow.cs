using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Dert.Core;
using Dert.Cli.Authoring;
using Dert.Cli.Repairs;
using ICSharpCode.AvalonEdit;
using Microsoft.Win32;

namespace Dert.Notes;

public sealed class NotesWindow : Window
{
    public TextEditor Editor { get; }
    public AuthoringSession Session { get; }
    public ListAuthoringPanel Lists { get; }
    public GoodbyeAuthoringPanel Goodbye { get; }
    private readonly DraftFileService files;
    private DraftFileSnapshot? opened;
    private TitleRepairPlan? preview;
    private bool needsAttention;
    private readonly TextBlock name = Label("Untitled.DERT", Palette.Text, 16);
    private readonly TextBlock state = Label("New note / not saved", Palette.Copper);
    private readonly TextBlock position = Label("Line 1", Palette.Muted);
    private readonly TextBlock notice = Label("Your words stay here until you choose Save.", Palette.Muted);
    private readonly TextBlock heading = Label("A little room for your words", Palette.Signal, 20);
    private readonly TextBlock context = Label("Start with // MY TITLE and press Enter. The matching rule follows your title.", Palette.Text, 15);
    private readonly StackPanel entries = new() { Margin = new(0, 14, 0, 0) };
    private readonly Button align;
    private readonly Button inspectReasoning;
    private readonly StackPanel previewActions = new() { Orientation = Orientation.Horizontal, Visibility = Visibility.Collapsed };
    private readonly Border accent = new() { Background = Palette.Signal, Height = 2, Opacity = 0.45 };
    private readonly bool still;
    private readonly Grid body = new();
    private readonly Border writing;
    private readonly StackPanel inspector = new() { Margin = new(28, 8, 0, 0) };
    private readonly Border contextBreath = new() { Height = 24 };
    private readonly ScrollViewer contextScroll;
    private readonly TitleRuleElementGenerator titleRules;

    public NotesWindow(string? path = null, bool still = false, DraftFileService? files = null)
    {
        this.still = still || !SystemParameters.ClientAreaAnimation ||
            (Environment.GetEnvironmentVariable("DERT_MOTION") ?? Environment.GetEnvironmentVariable("ERPDERT_MOTION")) == "off";
        this.files = files ?? new();
        Title = "DERT / notes";
        Width = 1180; Height = 780; MinWidth = 720; MinHeight = 540;
        Background = Palette.Background; Foreground = Palette.Text;
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/DERT.Notes;component/Theme.xaml", UriKind.Relative) });
        FontFamily = new("Segoe UI"); FontSize = 14;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Editor = new TextEditor
        {
            FontFamily = new("Consolas"), FontSize = 17, Foreground = Palette.Text,
            Background = Palette.Background, ShowLineNumbers = true, LineNumbersForeground = Palette.Muted,
            BorderThickness = new(0), Padding = new(12, 16, 18, 18), WordWrap = true,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Editor.Options.ConvertTabsToSpaces = false;
        Editor.Options.EnableHyperlinks = false;
        Editor.Options.EnableEmailHyperlinks = false;
        // No indentation is introduced by Enter, including when imported text
        // happens to contain whitespace. Source ownership comes from DERT.
        Editor.TextArea.IndentationStrategy = new FlushLeftIndentation();
        Editor.TextArea.SelectionBrush = Palette.Brush("#31504E");
        System.Windows.Automation.AutomationProperties.SetName(Editor, "DERT document source");
        Session = new(Editor);
        ICSharpCode.AvalonEdit.Folding.FoldingElementGenerator.TextBrush = Palette.Signal;
        ICSharpCode.AvalonEdit.Folding.FoldingMargin.SetFoldingMarkerBrush(Editor, Palette.Muted);
        ICSharpCode.AvalonEdit.Folding.FoldingMargin.SetFoldingMarkerBackgroundBrush(Editor, Palette.Background);
        ICSharpCode.AvalonEdit.Folding.FoldingMargin.SetSelectedFoldingMarkerBrush(Editor, Palette.Signal);
        ICSharpCode.AvalonEdit.Folding.FoldingMargin.SetSelectedFoldingMarkerBackgroundBrush(Editor, Palette.Panel);
        Editor.TextArea.TextView.LineTransformers.Add(new DertColorizer(() => Session.Parsed));
        Editor.TextArea.TextView.ElementGenerators.Add(new FlorinElementGenerator(() => Session.Parsed, () => Goodbye?.ShowTypedSpelling == true));
        titleRules = new(Editor, () => Session.Parsed);
        Editor.TextArea.TextView.ElementGenerators.Add(titleRules);
        Editor.TextArea.TextView.VisualLinesChanged += (_, _) => Update();
        Session.Changed += Update;
        Session.TitleAlignmentUnavailable += explanation => { notice.Text = explanation; notice.Foreground = Palette.Copper; };
        Session.TitleAligned += () =>
        {
            notice.Text = "Title aligned / Undo is available. Save keeps these words on disk.";
            notice.Foreground = Palette.Signal;
            if (!this.still) accent.BeginAnimation(OpacityProperty, new DoubleAnimation(0.15, 1, TimeSpan.FromMilliseconds(260)) { AutoReverse = true });
        };
        Session.ListEdited += message =>
        {
            notice.Text = message; notice.Foreground = Palette.Signal; notice.ToolTip = null;
            if (!this.still) accent.BeginAnimation(OpacityProperty, new DoubleAnimation(0.15, 1, TimeSpan.FromMilliseconds(260)) { AutoReverse = true });
        };
        Session.GoodbyeEdited += message =>
        {
            notice.Text = message; notice.Foreground = Palette.Signal; notice.ToolTip = null;
            if (!this.still) accent.BeginAnimation(OpacityProperty, new DoubleAnimation(0.15, 1, TimeSpan.FromMilliseconds(260)) { AutoReverse = true });
        };

        var root = new DockPanel { Margin = new(30, 22, 30, 20), LastChildFill = true };
        Content = root;
        var top = new DockPanel { Margin = new(0, 0, 0, 20) };
        top.Children.Add(Label("DERT", Palette.Text, 24));
        top.Children.Add(new TextBlock { Text = "  /  notes", Foreground = Palette.Muted, VerticalAlignment = VerticalAlignment.Center, FontSize = 18 });
        DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        var actions = new WrapPanel { Margin = new(0, 0, 0, 20) };
        actions.Children.Add(Action("_New", NewDocument));
        actions.Children.Add(Action("_Open…", ChooseOpen));
        actions.Children.Add(Action("_Save", () => Save(), true));
        actions.Children.Add(Action("Save _as…", () => Save(true)));
        actions.Children.Add(Action("_Undo", Session.Undo));
        actions.Children.Add(Action("_Redo", Session.Redo));
        DockPanel.SetDock(actions, Dock.Top); root.Children.Add(actions);

        var bottom = new StackPanel { Margin = new(0, 14, 0, 0) };
        bottom.Children.Add(notice);
        bottom.Children.Add(Label("Ctrl+S save    Ctrl+Z undo    Ctrl+Y redo    Ctrl+Space inspect reasoning", Palette.Muted, 12));
        DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);
        root.Children.Add(body);
        body.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new() { Width = new(292) });
        body.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        body.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var document = new DockPanel();
        var tab = new DockPanel { Margin = new(16, 12, 16, 10) };
        DockPanel.SetDock(position, Dock.Right); tab.Children.Add(position); tab.Children.Add(name);
        DockPanel.SetDock(tab, Dock.Top); document.Children.Add(tab);
        DockPanel.SetDock(accent, Dock.Top); document.Children.Add(accent);
        document.Children.Add(Editor);
        writing = new Border { BorderBrush = Palette.Brush("#354148"), BorderThickness = new(1), Child = document };
        body.Children.Add(writing);
        inspector.Children.Add(state);
        inspector.Children.Add(contextBreath);
        inspector.Children.Add(heading);
        context.Margin = new(0, 12, 0, 14); inspector.Children.Add(context);
        align = Action("Align title…", PreviewTitle);
        inspector.Children.Add(align);
        inspectReasoning = Action("Inspect / fold reasoning", Session.ToggleReasoning);
        inspector.Children.Add(inspectReasoning);
        Lists = new(Session, message => { notice.Text = message; notice.Foreground = Palette.Muted; notice.ToolTip = null; },
            () => { if (Lists?.IsReviewing == true) { preview = null; if (Goodbye?.IsReviewing == true) Goodbye.Reset(); } Update(); });
        inspector.Children.Add(Lists);
        Goodbye = new(Session, message => { notice.Text = message; notice.Foreground = Palette.Muted; notice.ToolTip = null; },
            () => { if (Goodbye?.IsReviewing == true) preview = null; Update(); },
            () =>
            {
                if (!Lists.IsReviewing) return true;
                notice.Text = "Apply or leave the list proposal before opening another choice.";
                return false;
            });
        inspector.Children.Add(Goodbye);
        previewActions.Children.Add(Action("Apply", ApplyTitle, true));
        previewActions.Children.Add(Action("Leave", () => { preview = null; Update(); }));
        inspector.Children.Add(previewActions);
        inspector.Children.Add(entries);
        contextScroll = new ScrollViewer { Content = inspector, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetColumn(contextScroll, 1); body.Children.Add(contextScroll);
        SizeChanged += (_, _) =>
        {
            var compact = ActualWidth < 950;
            body.ColumnDefinitions[1].Width = compact ? new(0) : new(292);
            Grid.SetColumn(contextScroll, compact ? 0 : 1);
            Grid.SetRow(contextScroll, compact ? 1 : 0);
            LayoutProposal();
            inspector.Margin = compact ? new(0, 16, 0, 0) : new(28, 8, 0, 0);
            Dispatcher.BeginInvoke(() => Editor.ScrollToLine(Editor.TextArea.Caret.Line), DispatcherPriority.Background);
        };
        Command(ApplicationCommands.Save, () => Save());
        Command(ApplicationCommands.SaveAs, () => Save(true));
        Command(ApplicationCommands.Open, ChooseOpen);
        Command(ApplicationCommands.New, NewDocument);
        Editor.CommandBindings.Add(new(ApplicationCommands.Undo, (_, e) => { Session.Undo(); e.Handled = true; }));
        Editor.CommandBindings.Add(new(ApplicationCommands.Redo, (_, e) => { Session.Redo(); e.Handled = true; }));
        Editor.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None && Session.CompleteTitleLine()) e.Handled = true;
            else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) { Lists.BeginAdd(); e.Handled = true; }
            else if (e.Key == Key.Enter && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)) { Lists.PreviewFinish(); e.Handled = true; }
            else if (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.Control) { Session.ToggleReasoning(); e.Handled = true; }
        };
        Editor.LostKeyboardFocus += (_, _) => Session.CompleteBoundary();
        DataObject.AddPastingHandler(Editor, (_, _) => Dispatcher.BeginInvoke(Session.CompleteBoundary, DispatcherPriority.Background));
        Closing += (_, e) => { if (!ConfirmDeparture()) e.Cancel = true; };
        if (path is not null) LoadPath(path);
        Update();
        Loaded += (_, _) => Editor.Focus();

        void Command(RoutedUICommand command, Action action) => CommandBindings.Add(new(command, (_, e) => { action(); e.Handled = true; }));
    }

    public void LoadPath(string path)
    {
        if (!ReadyToSave()) return;
        try
        {
            var captured = files.Open(path);
            opened = captured;
            Lists.Reset();
            Goodbye.Reset();
            needsAttention = false; preview = null;
            Session.Load(captured.Text);
            notice.Text = "Opened without changing the file. Corrections are available as previews.";
            notice.Foreground = Palette.Muted;
            Update();
        }
        catch (System.Text.DecoderFallbackException)
        { Problem(new IOException("This file could not be read as UTF-8. Choose a UTF-8 copy; the original was not changed.")); }
        catch (Exception error) when (Expected(error)) { Problem(error); }
    }

    public bool SaveTo(string? newPath = null)
    {
        if (!ReadyToSave()) return false;
        try
        {
            Session.CompleteBoundary();
            string? recovery = null;
            if (newPath is not null) opened = files.Create(newPath, Editor.Text, opened?.HasBom ?? false);
            else if (opened is not null)
            {
                var result = files.Save(opened, Editor.Text);
                opened = result.Snapshot; recovery = result.RecoveryPath;
            }
            else return false;
            Session.MarkSaved();
            needsAttention = false;
            notice.Foreground = Palette.Good;
            notice.Text = $"Saved and verified at {DateTimeOffset.Now:HH:mm:ss}." + (recovery is null ? "" : " Previous bytes kept in .erpdert/drafts.");
            notice.ToolTip = recovery ?? opened.Path;
            Update();
            return true;
        }
        catch (Exception error) when (Expected(error)) { Problem(error); return false; }
    }

    private bool Save(bool saveAs = false)
    {
        if (!ReadyToSave()) return false;
        if (!saveAs && opened is not null) return SaveTo();
        var dialog = new SaveFileDialog { Filter = "DERT notes (*.DERT)|*.DERT|All files (*.*)|*.*", DefaultExt = ".DERT", FileName = opened is null ? "My notes.DERT" : Path.GetFileName(opened.Path), OverwritePrompt = false };
        return dialog.ShowDialog(this) == true && SaveTo(dialog.FileName);
    }

    private void ChooseOpen()
    {
        if (!ConfirmDeparture()) return;
        var dialog = new OpenFileDialog { Filter = "DERT notes (*.DERT)|*.DERT|All files (*.*)|*.*" };
        if (dialog.ShowDialog(this) == true) LoadPath(dialog.FileName);
    }

    private void NewDocument()
    {
        if (!ConfirmDeparture()) return;
        opened = null; preview = null; needsAttention = false; Lists.Reset(); Goodbye.Reset(); Session.Load("", true);
        notice.Text = "Your words stay here until you choose Save."; Editor.Focus(); Update();
    }

    private bool ConfirmDeparture()
    {
        if (Lists.HasDraft)
        {
            if (MessageBox.Show(this, "Your new list item is still a preview. Discard that proposal and continue leaving this note?\nNo keeps the window open so you can Apply it.",
                "Keep your proposed words", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return false;
            Lists.Reset();
        }
        if (!Session.IsDirty) return true;
        return MessageBox.Show(this, "Save your changes before leaving this note?\nYes saves; No discards this edited buffer; Cancel keeps it open.", "Keep your words", MessageBoxButton.YesNoCancel, MessageBoxImage.Question) switch
        { MessageBoxResult.Yes => Save(), MessageBoxResult.No => true, _ => false };
    }

    public void PreviewTitle()
    {
        if (Lists.HasDraft) { ReadyToSave(); return; }
        Lists.Reset();
        Goodbye.Reset();
        preview = TitleRuleRepair.Plan(Session.Parsed);
        if (!preview.IsReady) { notice.Text = preview.Explanation; preview = null; return; }
        Update();
    }

    public void ApplyTitle()
    {
        if (preview is not null && !Session.ApplyPreview(preview)) notice.Text = "The source changed after the preview. Review Align title again.";
        preview = null; Update(); Editor.Focus();
    }

    private void Update()
    {
        if (align is null || Lists is null || Goodbye is null) return; // Controller can publish while the visual tree is built.
        name.Text = opened is null ? "Untitled.DERT" : Path.GetFileName(opened.Path);
        name.ToolTip = opened?.Path;
        state.Text = needsAttention ? "File needs attention" : Session.IsDirty ? "Unsaved changes" : opened is null ? "New note / not saved" : "Saved source";
        state.Foreground = needsAttention || Session.IsDirty || opened is null ? Palette.Copper : Palette.Good;
        Title = (Session.IsDirty ? "* " : "") + name.Text + " / DERT";
        position.Text = $"{Editor.TextArea.Caret.Line}:{Editor.TextArea.Caret.Column}";
        if (preview is not null && preview.Source != Editor.Text) preview = null;
        entries.Children.Clear();
        previewActions.Visibility = preview is null ? Visibility.Collapsed : Visibility.Visible;
        Lists.Refresh();
        Goodbye.Refresh();
        if (preview is not null || Lists.IsReviewing) Goodbye.Visibility = Visibility.Collapsed;
        var goodbyeVisible = Goodbye.Visibility == Visibility.Visible;
        entries.Visibility = Lists.IsReviewing || goodbyeVisible ? Visibility.Collapsed : Visibility.Visible;
        if (preview is not null || Goodbye.IsReviewing) Lists.Visibility = Visibility.Collapsed;
        var plan = TitleRuleRepair.Plan(Session.Parsed);
        align.Visibility = preview is null && !Lists.IsReviewing && !goodbyeVisible && plan.IsReady ? Visibility.Visible : Visibility.Collapsed;
        context.Visibility = Lists.IsReviewing || goodbyeVisible ? Visibility.Collapsed : Visibility.Visible;
        inspectReasoning.Visibility = Visibility.Collapsed;
        if (preview is { Edit: { } edit })
        {
            heading.Text = "Align your title";
            context.Text = preview.Explanation + "\n\n- " + (edit.Before.Length == 0 ? "(no rule)" : edit.Before) + "\n+ " + edit.After.TrimEnd('\r', '\n') + "\n\nPreview only / not saved";
            return;
        }
        var index = new DertStructureIndex(Session.Parsed);
        var at = index.AtLine(Editor.TextArea.Caret.Line);
        LayoutProposal();
        if (Lists.IsReviewing) { heading.Text = Lists.OwnerName; return; }
        if (goodbyeVisible) { heading.Text = "A proper goodbye"; return; }
        if (!Lists.IsReviewing && (at?.Node is ReasoningNoteNode or ReasoningBlockNode || at?.Parent is ReasoningBlockNode))
            inspectReasoning.Visibility = Visibility.Visible;
        heading.Text = at is null ? Editor.Text.Length == 0 ? "A little room for your words" : "Room for the next thought" : DertStructureIndex.Label(at.Node);
        if (Editor.Text.Length == 0) context.Text = "Start with // MY TITLE and press Enter. The matching rule follows your title.";
        else if (at is null) context.Text = "Write a free note with //, or keep your ordinary prose. Save when you want to keep this version.";
        else
        {
            var parent = at.Parent is null ? "Part of this document" : $"Belongs to line {at.Parent.LineNumber}: {DertStructureIndex.Label(at.Parent)}";
            context.Text = DertStructureIndex.Kind(at) + " / " + parent;
            if (at.Node is NoteNode { IsTitle: true }) context.Text += "\n\n" + plan.Explanation +
                (titleRules.TitleWraps ? "\n\nYour title wraps in this window. The rule keeps its source marks; widen the view to compare the full width." : "");
            if (at.Node is ReasoningNoteNode note) context.Text += "\n\n" + note.Header.Payload?.Text + (note.Closing is null ? "\n\nClosing // missing." : "");
            if (at.Node is NoteEntryNode entry) context.Text += "\n\n" + entry.Annotation?.Text;
            var block = at.Node as ReasoningBlockNode ?? at.Parent as ReasoningBlockNode;
            if (block is not null)
            {
                context.Text += block.Closing is null ? "\n\nClosing // missing." : "\n\nIts own // closes this note.";
                foreach (var child in block.Entries)
                    entries.Children.Add(Action($"{child.LineNumber} / {child.Value.Text}", () => GoTo(child.Value.Span.Start), small: true));
            }
        }
        foreach (var finding in Session.Parsed.Diagnostics.Where(finding => finding.LineNumber == Editor.TextArea.Caret.Line &&
            !(finding.Code == "DERT3003" && Lists.Visibility == Visibility.Visible)))
            entries.Children.Add(Label(finding.Message, Palette.Copper, 13));
    }

    private void GoTo(int offset)
    {
        foreach (var fold in Session.Folds.GetFoldingsContaining(offset)) fold.IsFolded = false;
        Editor.CaretOffset = offset; Editor.ScrollToLine(Editor.Document.GetLineByOffset(offset).LineNumber); Editor.Focus();
    }

    private void Problem(Exception error)
    {
        needsAttention = true;
        Update();
        notice.Foreground = Palette.Copper;
        notice.Text = error.Message + (error is RepairException { RecoveryPath: { } path } ? "\nSaved original: " + path : "");
        notice.ToolTip = error.ToString();
    }

    private static bool Expected(Exception error) => error is IOException or UnauthorizedAccessException or ArgumentException or RepairException;
    private bool ReadyToSave()
    {
        if (!Lists.HasDraft) return true;
        notice.Text = "The new item is still a preview. Apply it to your note or choose Leave before saving.";
        notice.Foreground = Palette.Copper;
        return false;
    }

    private void LayoutProposal()
    {
        if (contextScroll is null) return;
        var reviewing = Lists.IsReviewing || Goodbye.Visibility == Visibility.Visible;
        contextBreath.Height = ActualWidth < 950 && reviewing ? 8 : 24;
        var height = ActualWidth < 950 ? reviewing ? Math.Clamp(ActualHeight - 480, 180, 320) : 220 : double.PositiveInfinity;
        if (contextScroll.MaxHeight == height) return;
        contextScroll.MaxHeight = height;
        Dispatcher.BeginInvoke(() => Editor.ScrollToLine(Editor.TextArea.Caret.Line), DispatcherPriority.Background);
    }

    internal static TextBlock Label(string text, Brush brush, double size = 14) => new() { Text = text, Foreground = brush, FontSize = size, TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 5) };
    internal static Button Action(string text, Action click, bool primary = false, bool small = false)
    {
        var button = new Button
        {
            Content = text, Foreground = primary ? Palette.Background : Palette.Text, Background = primary ? Palette.Signal : Palette.Panel,
            BorderBrush = primary ? Palette.Signal : Palette.Brush("#354148"), BorderThickness = small ? new(0, 0, 0, 1) : new(1),
            Padding = small ? new(8, 6, 8, 6) : new(13, 8, 13, 8), Margin = new(0, 0, 8, 8),
            HorizontalContentAlignment = HorizontalAlignment.Left, FontSize = small ? 13 : 14
        };
        button.Click += (_, _) => click();
        return button;
    }
}

internal sealed class FlushLeftIndentation : ICSharpCode.AvalonEdit.Indentation.IIndentationStrategy
{
    public void IndentLine(ICSharpCode.AvalonEdit.Document.TextDocument document, ICSharpCode.AvalonEdit.Document.DocumentLine line) { }
    public void IndentLines(ICSharpCode.AvalonEdit.Document.TextDocument document, int beginLine, int endLine) { }
}
