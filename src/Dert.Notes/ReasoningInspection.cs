using Dert.Core;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;
using System.Windows.Threading;

namespace Dert.Notes;

public sealed class ReasoningInspection
{
    private readonly AuthoringSession session;
    private readonly Action changed;
    private TextAnchor? marker, originCaret, viewport;
    private Selection? originSelection;
    private Type? ownerType;
    private double horizontal, vertical, withinTopLine;
    private bool moving, holdOpen, followingReference;
    private int epoch;
    public DertNode? Owner { get; private set; }
    public bool IsEditing { get; private set; }
    public int? ReferenceOriginLine => followingReference && originCaret is { IsDeleted: false } ? originCaret.Location.Line : null;
    public bool CanReturn => Owner is not null || ReferenceOriginLine is not null;

    internal ReasoningInspection(AuthoringSession session, Action changed)
    {
        this.session = session; this.changed = changed;
        session.Editor.Document.Changing += (_, change) =>
        {
            // An anchor at a removal's boundary survives in AvalonEdit. The
            // original two slashes must survive too; adjacent reasoning cannot
            // inherit an inspection when a deletion joins their openers.
            if (marker is not { IsDeleted: false } || Owner is null) return;
            if (change.RemovalLength > 0 && change.Offset < marker.Offset + 1 &&
                change.Offset + change.RemovalLength > marker.Offset - 1 ||
                change.InsertionLength > 0 && change.Offset == marker.Offset) WithdrawOwner();
        };
        session.Editor.Document.Changed += (_, change) =>
        {
            if (originSelection is not null) originSelection = originSelection.UpdateOnDocumentChange(change);
        };
    }

    public bool Begin(int? line = null) => BeginAt(line, false);

    private bool BeginAt(int? line, bool keepOrigin)
    {
        var editor = session.Editor;
        var owner = AtLine(line ?? editor.TextArea.Caret.Line);
        if (owner is null) return false;
        if (Owner == owner) return true;
        epoch++;
        if (!keepOrigin || Owner is null)
        {
            followingReference = false;
            originCaret = Anchor(editor.CaretOffset);
            originSelection = editor.TextArea.Selection;
            horizontal = editor.HorizontalOffset; vertical = editor.VerticalOffset;
            var top = editor.TextArea.TextView.VisualLinesValid ? editor.TextArea.TextView.VisualLines.FirstOrDefault() : null;
            viewport = Anchor(top?.FirstDocumentLine.Offset ?? 0);
            withinTopLine = vertical - (top?.VisualTop ?? 0);
        }
        Owner = owner; ownerType = owner.GetType();
        marker = Anchor(owner.Span.Start + 1, false);
        IsEditing = Fold(owner)?.IsFolded == false;
        changed();
        return true;
    }

    public bool FollowReference(DertDocument revision, NoteName origin, NoteName destination)
    {
        RefreshSource();
        if (holdOpen || !ReferenceEquals(revision, session.Parsed) || !ReferenceEquals(Owner, origin.Note)) return false;
        var group = new NoteReferenceIndex(revision).ForNote(Owner!).FirstOrDefault(group => group.Origin == origin);
        var match = group?.Matches.FirstOrDefault(match => ReferenceEquals(match.Note, destination.Note) && match == destination);
        if (match is null) return false;
        followingReference = true;
        if (!BeginAt(match.Note.LineNumber, true)) return false;
        return EditRange(match.Note, match.Value.Span);
    }

    public bool EditLine(int line)
    {
        RefreshSource();
        if (Owner is null || holdOpen) return false;
        var entry = Owner is ReasoningBlockNode block ? block.Entries.FirstOrDefault(entry => entry.LineNumber == line) : null;
        var span = entry?.Value.Span ?? (line == Owner.LineNumber ? Owner switch
        {
            ReasoningNoteNode note => note.Header.Payload?.Span ?? note.Header.Head.Span,
            ReasoningBlockNode nested => nested.Header.Head.Span,
            _ => Owner.Span
        } : (TextSpan?)null);
        if (span is null) return false;
        return SelectRange(span.Value, line);
    }

    public bool EditRange(DertNode expectedOwner, TextSpan range)
    {
        RefreshSource();
        if (holdOpen || !ReferenceEquals(Owner, expectedOwner) || range.Start < 0 || range.Length < 0 ||
            range.Start > session.Parsed.Source.Text.Length - range.Length) return false;
        var header = Owner switch { ReasoningNoteNode note => note.Header, ReasoningBlockNode block => block.Header, _ => null };
        if (header is null) return false;
        var fields = new[] { header.Head, header.Target, header.Payload }.OfType<TextField>();
        if (Owner is ReasoningBlockNode nested) fields = fields.Concat(nested.Entries.Select(entry => entry.Value));
        if (!fields.Any(field => range.Start >= field.Span.Start && range.End <= field.Span.End) &&
            ContextReading.Read(session.Parsed, Owner!)?.Items.Any(item => item.Value.Span == range) != true) return false;
        return SelectRange(range, new DertStructureIndex(session.Parsed).LineOf(range.Start));
    }

    private bool SelectRange(TextSpan span, int line)
    {
        moving = true; epoch++;
        try
        {
            IsEditing = true;
            if (Fold(Owner!) is { } fold) fold.IsFolded = false;
            session.Editor.Select(span.Start, span.Length);
            session.Editor.ScrollToLine(line);
            session.Editor.Focus();
        }
        finally { moving = false; }
        QueueNavigation();
        changed();
        return true;
    }

    public bool Return()
    {
        RefreshSource();
        if (!CanReturn || holdOpen) return false;
        var owner = Owner;
        var top = viewport;
        var x = horizontal; var y = vertical; var delta = withinTopLine;
        moving = true;
        try
        {
            var editor = session.Editor;
            if (originCaret is { IsDeleted: false } && originSelection is not null)
            {
                editor.CaretOffset = originCaret.Offset;
                editor.TextArea.Selection = originSelection;
            }
            // A return that began inside an expanded edit keeps that original
            // selection visible. Navigation away will restore the resting fold.
            if (owner is not null && Fold(owner) is { } fold && !SelectionIntersects(fold.StartOffset, fold.EndOffset) &&
                !(editor.CaretOffset > fold.StartOffset && editor.CaretOffset < fold.EndOffset)) fold.IsFolded = true;
            Reset();
            editor.Focus();
            // Let the host restore its resting layout before the viewport. Its
            // compact layout may queue a caret reveal while the panel resizes.
            changed();
            var stamp = epoch;
            editor.Dispatcher.BeginInvoke(() =>
            {
                if (epoch != stamp) return;
                editor.UpdateLayout();
                var offset = top is { IsDeleted: false } ? editor.TextArea.TextView.GetVisualPosition(
                    new ICSharpCode.AvalonEdit.TextViewPosition(top.Location), VisualYPosition.LineTop).Y + delta : y;
                editor.ScrollToHorizontalOffset(x); editor.ScrollToVerticalOffset(Math.Max(0, offset));
            }, DispatcherPriority.Background);
        }
        finally { moving = false; }
        return true;
    }

    internal void Reset()
    {
        epoch++; Owner = null; IsEditing = followingReference = false; marker = originCaret = viewport = null; originSelection = null;
    }

    private void WithdrawOwner()
    {
        if (!followingReference) { Reset(); return; }
        // A removed destination cannot acquire its neighbor's identity. Keep
        // only the independent return anchors from before the first visit.
        epoch++; Owner = null; marker = null; ownerType = null; IsEditing = false;
    }

    internal void RefreshSource()
    {
        if (Owner is null) return;
        var node = marker is { IsDeleted: false } ? new DertStructureIndex(session.Parsed).Locations
            .Select(location => location.Node).FirstOrDefault(node => node.GetType() == ownerType &&
                node.Span.Start < marker.Offset && marker.Offset <= session.Parsed.Source.Lines[node.LineNumber - 1].ContentEnd) : null;
        if (node is null) WithdrawOwner();
        else { Owner = node; IsEditing |= Fold(node)?.IsFolded == false; }
    }

    internal void HoldForPendingEdit(bool hold)
    {
        if (holdOpen == hold) return;
        holdOpen = hold;
        if (!hold) QueueNavigation();
    }

    internal void QueueNavigation()
    {
        if (moving) return;
        var stamp = ++epoch;
        session.Editor.Dispatcher.BeginInvoke(() =>
        {
            if (stamp != epoch || moving || session.Editor.Document.IsInUpdate) return;
            ReconcileNavigation();
        }, DispatcherPriority.Background);
    }

    private void ReconcileNavigation()
    {
        var caret = session.Editor.CaretOffset;
        if (holdOpen) return;
        RefreshSource();
        if (Owner is { } current && !ContainsCaret(current, caret) && !SelectionIntersects(current.Span.Start, current.Span.End)) Reset();
        var changes = false;
        var nodes = new DertStructureIndex(session.Parsed).Locations.Select(location => location.Node)
            .Where(node => node is ReasoningNoteNode or ReasoningBlockNode).ToArray();
        foreach (var node in nodes)
            if (Fold(node) is { IsFolded: false } fold && !ContainsCaret(node, caret) && !SelectionIntersects(fold.StartOffset, fold.EndOffset))
            { fold.IsFolded = true; changes = true; }
        // Reading is an explicit action. Moving anchors after an edit must not
        // open a new inspection just because another source fold is expanded.
        if (changes || Owner is null) changed();
    }

    private DertNode? AtLine(int line)
    {
        var at = new DertStructureIndex(session.Parsed).AtLine(line);
        return at?.Node is ReasoningNoteNode or ReasoningBlockNode ? at.Node : at?.Parent as ReasoningBlockNode;
    }
    internal bool HasReasoningAt(int line) => AtLine(line) is not null;
    private ICSharpCode.AvalonEdit.Folding.FoldingSection? Fold(DertNode node) => session.Folds.AllFoldings.FirstOrDefault(fold =>
        fold.StartOffset >= node.Span.Start && fold.EndOffset <= node.Span.End);
    private bool ContainsCaret(DertNode owner, int caret) => caret >= owner.Span.Start &&
        (caret < owner.Span.End || caret == owner.Span.End && caret == session.Editor.Document.TextLength);
    private bool SelectionIntersects(int start, int end) => session.Editor.SelectionLength > 0 &&
        session.Editor.SelectionStart < end && session.Editor.SelectionStart + session.Editor.SelectionLength > start;
    private TextAnchor Anchor(int offset, bool survive = true)
    {
        var anchor = session.Editor.Document.CreateAnchor(offset);
        anchor.MovementType = AnchorMovementType.AfterInsertion;
        anchor.SurviveDeletion = survive;
        return anchor;
    }
}
