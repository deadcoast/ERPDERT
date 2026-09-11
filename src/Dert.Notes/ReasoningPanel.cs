using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Dert.Core;

namespace Dert.Notes;

/// <summary>A temporary reading surface over the original, editable source.</summary>
public sealed partial class ReasoningPanel : Grid
{
    private readonly AuthoringSession session;
    private readonly TextBlock mode = NotesWindow.Label("", Palette.Muted, 12);
    private readonly StackPanel trace = new();
    private readonly StackPanel controls = new();
    private readonly TextBlock title = NotesWindow.Label("", Palette.Signal, 20);
    private readonly WrapPanel actions = new() { Margin = new(0, 8, 0, 12) };
    private readonly Button back;
    private readonly Button edit;
    private readonly Button fields;
    private readonly Button references;
    private readonly Button contextSelection;
    private DertNode? rendered;
    private bool rawFields, renderedRaw, pending, showReferences, renderedReferences;

    internal ReasoningPanel(AuthoringSession session, Action returnToNote, Action chooseContext)
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
        fields = NotesWindow.Action("Raw fields", () =>
        {
            if (pending || session.Reasoning.Owner is null) return;
            rawFields = !rawFields; showReferences = false; Refresh(pending);
        });
        fields.ToolTip = "Switch between the current reading and exact original fields. This changes only the view.";
        references = NotesWindow.Action("References", () =>
        {
            if (pending || session.Reasoning.Owner is null) return;
            showReferences = !showReferences; rawFields = false; Refresh(pending);
        });
        references.ToolTip = "Find whole written names in other reasoning notes in this document.";
        contextSelection = NotesWindow.Action("Context selection…", () => { if (!pending && session.Reasoning.Owner is not null) chooseContext(); });
        contextSelection.ToolTip = "Choose this note and its annotations for a reviewed copy/export. Other notes remain your choice.";
        actions.Children.Add(back); actions.Children.Add(edit); actions.Children.Add(fields); actions.Children.Add(references); actions.Children.Add(contextSelection); controls.Children.Add(actions);
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
        pending = pendingChoice;
        var owner = session.Reasoning.Owner;
        Visibility = !session.Reasoning.CanReturn || pendingChoice ? Visibility.Collapsed : Visibility.Visible;
        edit.Visibility = fields.Visibility = references.Visibility = contextSelection.Visibility = owner is null ? Visibility.Collapsed : Visibility.Visible;
        if (owner is null)
        {
            ResetReferences();
            rendered = null; rawFields = showReferences = false; trace.Children.Clear();
            if (session.Reasoning.ReferenceOriginLine is { } returnLine)
            {
                title.Text = "Visited note changed";
                mode.Text = $"Escape returns to line {returnLine}";
                trace.Children.Add(NotesWindow.Label("The visited note no longer has its original opener. Your starting place is retained; your edits remain in the source.", Palette.Copper, 14));
            }
            return;
        }
        mode.Text = session.Reasoning.ReferenceOriginLine is { } origin ? $"Visited a named match / Escape returns to line {origin}" :
            session.Reasoning.IsEditing ? "Editing source / Escape returns to your place" : "Reading / Escape returns to your place";
        if (ReferenceEquals(rendered, owner) && renderedRaw == rawFields && renderedReferences == showReferences) return;
        rendered = owner; renderedRaw = rawFields; renderedReferences = showReferences;
        fields.Content = rawFields ? "Read context" : "Raw fields";
        references.Content = showReferences ? "Read context" : "References";
        trace.Children.Clear();
        var header = owner is ReasoningBlockNode block ? block.Header : ((ReasoningNoteNode)owner).Header;
        var reading = ContextReading.Read(session.Parsed, owner)!;
        title.Text = header.Head.Text;
        trace.Children.Add(NotesWindow.Label($"Line {owner.LineNumber} / " + (showReferences ? "named places in this document" : rawFields ? "original fields" : reading.Reader == ContextReader.Written
            ? owner is ReasoningBlockNode counted ? $"{counted.Entries.Count} entries" : "explanation" : reading.Summary),
            reading.Issues.Count > 0 ? Palette.Copper : Palette.Signal));
        if (!rawFields && !showReferences && header.Target is { } target)
            trace.Children.Add(NotesWindow.Label($"Target as written / {target.Text}", Palette.Muted, 13));
        var closing = DertStructureIndex.Closing(owner);
        trace.Children.Add(NotesWindow.Label(closing is { } span ?
            $"Line {new DertStructureIndex(session.Parsed).LineOf(span.Start)} / its own //" : "Closing // is missing.",
            closing is null ? Palette.Copper : Palette.Muted, 13));
        var body = new StackPanel();
        trace.Children.Add(new Border
        {
            BorderBrush = Palette.Brush("#354148"), BorderThickness = new(1, 0, 0, 0),
            Padding = new(13, 4, 0, 4), Margin = new(2, 8, 0, 10), Child = body
        });
        if (showReferences) { ReferenceBody(session.Parsed, owner, body); return; }
        if (rawFields)
        {
            Field("Content", header.Head);
            if (header.Target is { } rawTarget) Field("; hook / target as written", rawTarget);
            if (header.Payload is { } rawPayload) Field(";; line / payload as written", rawPayload);
        }
        if (owner is ReasoningBlockNode || !rawFields && reading.Reader == ContextReader.Similarities)
        {
            if (reading.Items.Count == 0) body.Children.Add(NotesWindow.Label("No entries yet.", Palette.Muted));
            var items = rawFields && owner is ReasoningBlockNode original ? original.Entries.Select((entry, number) =>
                new ContextItem(number + 1, entry.Value, entry.Value.Span, null, entry.LineNumber, entry.Annotation)) : reading.Items;
            foreach (var item in items)
            {
                var button = NotesWindow.Action("", () => session.Reasoning.EditRange(owner, item.Value.Span), small: true);
                var column = item.Value.Span.Start - session.Parsed.Source.Lines[item.LineNumber - 1].Start + 1;
                var label = item.Value.Text.Length == 0 ? "words not written" : item.Value.Text;
                button.Content = NotesWindow.Label((!rawFields && reading.Reader == ContextReader.Similarities ? $"{item.Number:00}" : $"{item.LineNumber}") + $" / {label}",
                    item.Value.Text.Length == 0 ? Palette.Copper : Palette.Text, 14);
                button.Margin = new(0, 4, 0, 5);
                button.ToolTip = $"Edit source line {item.LineNumber}, column {column}";
                AutomationProperties.SetName(button, !rawFields && reading.Reader == ContextReader.Similarities ? $"Edit item {item.Number}, line {item.LineNumber}, column {column}: {label}"
                    : $"Edit line {item.LineNumber}: {item.Value.Text}");
                body.Children.Add(button);
                if (item.Annotation is { } annotation)
                {
                    var text = NotesWindow.Label(annotation.Text, Palette.Muted, 14);
                    text.Margin = new(8, 0, 0, 8);
                    body.Children.Add(text);
                }
            }
        }
        else if (!rawFields && header.Payload is { } payload) body.Children.Add(NotesWindow.Label(payload.Text, Palette.Text, 15));
        else if (!rawFields) body.Children.Add(NotesWindow.Label("No explanation yet.", Palette.Muted));
        if (!rawFields)
            foreach (var issue in reading.Issues.Where(issue => issue.Kind != ContextIssueKind.MissingClosing))
                trace.Children.Add(NotesWindow.Label(issue.Message, Palette.Copper, 13));
        if (reading.Reader == ContextReader.Similarities && !rawFields)
            trace.Children.Add(NotesWindow.Label("Listed together by this note. Select an item to edit its words.", Palette.Muted, 12));

        void Field(string label, TextField value)
        {
            body.Children.Add(NotesWindow.Label(label, Palette.Muted, 12));
            var button = NotesWindow.Action("", () => session.Reasoning.EditRange(owner, value.Span), small: true);
            button.Content = NotesWindow.Label(value.Text, Palette.Text, 14);
            AutomationProperties.SetName(button, "Edit " + label + ": " + value.Text);
            body.Children.Add(button);
        }
    }
}
