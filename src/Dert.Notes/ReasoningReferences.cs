using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Dert.Core;

namespace Dert.Notes;

public sealed partial class ReasoningPanel
{
    private DertDocument? referenceRevision;
    private DertNode? referenceOwner;
    private IReadOnlyList<NoteReferenceGroup> referenceGroups = [];
    private int referencePage;
    private Button? referenceNext, referencePrevious;

    private void ResetReferences()
    {
        referenceRevision = null; referenceOwner = null; referenceGroups = []; referencePage = 0;
        referenceNext = referencePrevious = null;
    }

    private void ReferenceBody(DertDocument document, DertNode owner, StackPanel body)
    {
        if (!ReferenceEquals(referenceRevision, document) || !ReferenceEquals(referenceOwner, owner))
        {
            referenceRevision = document; referenceOwner = owner; referencePage = 0;
            referenceGroups = new NoteReferenceIndex(document).ForNote(owner);
        }
        const int pageSize = 6;
        var count = referenceGroups.Sum(group => Math.Max(1, group.Matches.Count));
        var pages = Math.Max(1, (count + pageSize - 1) / pageSize);
        body.Children.Add(NotesWindow.Label("Whole names / case ignored / quotes literal. No operation binding.", Palette.Muted, 12));
        if (pages > 1)
        {
            body.Children.Add(NotesWindow.Label($"Places / page {referencePage + 1} of {pages}", Palette.Signal, 13));
            var controls = new WrapPanel();
            referencePrevious = NotesWindow.Action("Previous places", () => MovePage(-1), small: true);
            referenceNext = NotesWindow.Action("Next places", () => MovePage(1), small: true);
            referencePrevious.IsEnabled = referencePage > 0; referenceNext.IsEnabled = referencePage + 1 < pages;
            controls.Children.Add(referencePrevious); controls.Children.Add(referenceNext); body.Children.Add(controls);
        }
        var rows = referenceGroups.SelectMany(group => group.Matches.Count == 0
            ? [(Group: group, Match: (NoteName?)null)] : group.Matches.Select(match => (Group: group, Match: (NoteName?)match)));
        NoteReferenceGroup? previous = null;
        foreach (var row in rows.Skip(referencePage * pageSize).Take(pageSize))
        {
            var group = row.Group;
            if (!ReferenceEquals(previous, group))
            {
                var heading = NotesWindow.Label($"{group.Origin.Value.Text} / {group.Origin.RoleLabel}", Palette.Signal, 14);
                heading.Margin = new(0, 8, 0, 0);
                body.Children.Add(heading);
                body.Children.Add(NotesWindow.Label(group.Summary, group.Origin.IsComplete ? Palette.Muted : Palette.Copper, 12));
                previous = group;
            }
            if (row.Match is { } match)
            {
                var column = match.Value.Span.Start - document.Source.Lines[match.LineNumber - 1].Start + 1;
                var parent = match.Parent is { } ownerNode ? $"belongs to line {ownerNode.LineNumber} / {DertStructureIndex.Label(ownerNode)}" : "belongs to document";
                var button = NotesWindow.Action("", () => session.Reasoning.FollowReference(document, group.Origin, match), small: true);
                var label = new StackPanel();
                label.Children.Add(NotesWindow.Label($"{match.LineNumber}:{column} / {match.RoleLabel}", Palette.Text, 14));
                label.Children.Add(NotesWindow.Label(DertStructureIndex.Label(match.Note) + " / " + parent, Palette.Muted, 12));
                button.Content = label; button.Margin = new(0, 5, 0, 3);
                button.ToolTip = "Name match / select these exact source words. Escape returns to your starting place.";
                AutomationProperties.SetName(button, $"Follow {match.Value.Text} / line {match.LineNumber}, column {column} / {match.RoleLabel}");
                AutomationProperties.SetHelpText(button, $"Name match in {DertStructureIndex.Label(match.Note)}; {parent}. Selects original source.");
                body.Children.Add(button);
            }
        }

        void MovePage(int delta)
        {
            if (pending || !ReferenceEquals(document, session.Parsed) || !ReferenceEquals(owner, session.Reasoning.Owner)) return;
            referencePage = Math.Clamp(referencePage + delta, 0, pages - 1);
            rendered = null; Refresh(pending);
            (delta > 0 && referenceNext?.IsEnabled == true ? referenceNext : referencePrevious?.IsEnabled == true ? referencePrevious : back).Focus();
        }
    }
}
