using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Dert.Core;

namespace Dert.Notes;

/// <summary>A temporary reading surface over the original, editable source.</summary>
public sealed class ReasoningPanel : Grid
{
    private readonly AuthoringSession session;
    private readonly TextBlock mode = NotesWindow.Label("", Palette.Muted, 12);
    private readonly StackPanel trace = new();
    private readonly StackPanel controls = new();
    private readonly TextBlock title = NotesWindow.Label("", Palette.Signal, 20);
    private readonly WrapPanel actions = new() { Margin = new(0, 8, 0, 12) };
    private readonly Button back;
    private readonly Button edit;
    private DertNode? rendered;

    internal ReasoningPanel(AuthoringSession session, Action returnToNote)
    {
        this.session = session;
        AutomationProperties.SetName(this, "Reasoning inspection");
        ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new() { Width = new(0) });
        RowDefinitions.Add(new() { Height = GridLength.Auto });
        RowDefinitions.Add(new() { Height = GridLength.Auto });
        controls.Children.Add(title); controls.Children.Add(mode);
        back = NotesWindow.Action("Return to note", returnToNote, true);
        back.ToolTip = "Escape / return to your original place. Source edits remain in your note.";
        edit = NotesWindow.Action("Edit source", () =>
        {
            if (session.Reasoning.Owner is { } owner) session.Reasoning.EditLine(owner.LineNumber);
        });
        actions.Children.Add(back); actions.Children.Add(edit); controls.Children.Add(actions);
        Children.Add(controls); Children.Add(trace); SetRow(trace, 1);
        Visibility = Visibility.Collapsed;
    }

    internal void FocusReturn() => back.Focus();

    internal void Layout(bool compact)
    {
        ColumnDefinitions[0].Width = compact ? new(220) : new(1, GridUnitType.Star);
        ColumnDefinitions[1].Width = compact ? new(1, GridUnitType.Star) : new(0);
        SetRow(trace, compact ? 0 : 1); SetColumn(trace, compact ? 1 : 0);
        trace.Margin = compact ? new(24, 0, 0, 0) : new(0);
    }

    internal void Refresh(bool pendingChoice)
    {
        var owner = session.Reasoning.Owner;
        Visibility = owner is null || pendingChoice ? Visibility.Collapsed : Visibility.Visible;
        if (owner is null) { rendered = null; trace.Children.Clear(); return; }
        mode.Text = session.Reasoning.IsEditing ? "Editing source / Escape returns to your place" : "Reading / Escape returns to your place";
        if (ReferenceEquals(rendered, owner)) return;
        rendered = owner;
        trace.Children.Clear();
        var header = owner is ReasoningBlockNode block ? block.Header : ((ReasoningNoteNode)owner).Header;
        title.Text = header.Head.Text;
        trace.Children.Add(NotesWindow.Label($"Line {owner.LineNumber}" + (owner is ReasoningBlockNode counted ? $" / {counted.Entries.Count} entries" : " / explanation"), Palette.Signal));
        if (header.Target is { } target)
            trace.Children.Add(NotesWindow.Label($"Named target / {target.Text}", Palette.Muted, 13));
        var body = new StackPanel();
        trace.Children.Add(new Border
        {
            BorderBrush = Palette.Brush("#354148"), BorderThickness = new(1, 0, 0, 0),
            Padding = new(13, 4, 0, 4), Margin = new(2, 8, 0, 10), Child = body
        });
        if (owner is ReasoningBlockNode nested)
        {
            if (nested.Entries.Count == 0) body.Children.Add(NotesWindow.Label("No entries yet.", Palette.Muted));
            foreach (var entry in nested.Entries)
            {
                var button = NotesWindow.Action("", () => EditCurrent(owner, entry.LineNumber), small: true);
                button.Content = NotesWindow.Label($"{entry.LineNumber} / {entry.Value.Text}", Palette.Text, 14);
                button.Margin = new(0, 4, 0, 5);
                button.ToolTip = $"Edit this entry on source line {entry.LineNumber}";
                AutomationProperties.SetName(button, $"Edit line {entry.LineNumber}: {entry.Value.Text}");
                body.Children.Add(button);
                if (entry.Annotation is { } annotation)
                {
                    var text = NotesWindow.Label(annotation.Text, Palette.Muted, 14);
                    text.Margin = new(8, 0, 0, 8);
                    body.Children.Add(text);
                }
            }
        }
        else if (header.Payload is { } payload) body.Children.Add(NotesWindow.Label(payload.Text, Palette.Text, 15));
        else body.Children.Add(NotesWindow.Label("No explanation yet.", Palette.Muted));
        var closing = DertStructureIndex.Closing(owner);
        trace.Children.Add(NotesWindow.Label(closing is { } span ?
            $"Line {new DertStructureIndex(session.Parsed).LineOf(span.Start)} / its own //" : "Closing // is missing.",
            closing is null ? Palette.Copper : Palette.Muted, 13));
        if (owner is ReasoningBlockNode { Entries.Count: > 0 })
            trace.Children.Add(NotesWindow.Label("Select an entry to edit its words.", Palette.Muted, 12));
    }

    private void EditCurrent(DertNode owner, int line)
    {
        // A queued click from an old reading surface cannot select a replacement.
        if (ReferenceEquals(session.Reasoning.Owner, owner)) session.Reasoning.EditLine(line);
    }
}
