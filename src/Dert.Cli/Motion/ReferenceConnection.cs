using Dert.Core;
using Dert.Mer;

namespace Dert.Cli.Motion;

internal sealed record ReferenceChoice(int Number, NoteName Origin, NoteName Match);

internal sealed class ReferenceConnection
{
    private const int PageSize = 4;
    private readonly DertStructureIndex index;
    private readonly DertNode note;
    private readonly IReadOnlyList<NoteReferenceGroup> groups;
    internal int SourceLine { get; }
    internal IReadOnlyList<ReferenceChoice> Choices { get; }
    internal int Pages => Math.Max(1, (Choices.Count + PageSize - 1) / PageSize);

    private ReferenceConnection(DertStructureIndex index, int line, DertNode note)
    {
        this.index = index; this.note = note; SourceLine = line;
        groups = new NoteReferenceIndex(index.Document).ForNote(note);
        Choices = groups.SelectMany(group => group.Matches.Select(match => (group.Origin, match)))
            .Select((pair, number) => new ReferenceChoice(number + 1, pair.Origin, pair.match)).ToArray();
    }

    internal static ReferenceConnection? Read(DertStructureIndex index, int line)
    {
        var location = index.AtLine(line);
        var owner = location?.Node is ReasoningNoteNode or ReasoningBlockNode ? location.Node : location?.Parent as ReasoningBlockNode;
        return owner is null ? null : new(index, line, owner);
    }

    internal DocumentAnnotation Describe(int page = 1, bool all = false)
    {
        var lines = new List<InkLine>();
        var structure = StructureConnection.Describe(index, SourceLine);
        var first = all ? 0 : (page - 1) * PageSize;
        var last = all ? Choices.Count : Math.Min(first + PageSize, Choices.Count);
        var heading = InkLine.Plain($"NAMED PLACES / captured note {note.LineNumber}", Ink.Signal);
        var boundary = Boundary(note);
        var count = Choices.Count == 0 ? "No other named places" : $"References {first + 1}-{last} of {Choices.Count}";
        lines.Add(heading); lines.Add(boundary);
        lines.Add(InkLine.Plain("Whole names / case ignored / quotes literal", Ink.Muted));
        lines.Add(InkLine.Plain("Name matches / no operation binding", Ink.Muted));
        foreach (var group in groups)
            lines.Add(InkLine.Plain($"{group.Origin.Value.Text} / {group.Origin.RoleLabel} / {group.Summary}", group.Origin.IsComplete ? Ink.Muted : Ink.Copper));
        lines.Add(InkLine.Plain(count, Ink.Signal));
        foreach (var choice in Choices.Skip(first).Take(last - first))
        {
            lines.Add(InkLine.Plain($"ref {choice.Number} / {choice.Match.Value.Text}", Ink.Signal));
            lines.Add(InkLine.Plain(Position(choice.Match)));
            lines.Add(InkLine.Plain(DertStructureIndex.Label(choice.Match.Note) + " / " + Parent(choice.Match), Ink.Muted));
            lines.Add(InkLine.Plain($"from {Position(choice.Origin)}", Ink.Muted));
        }
        var navigation = Choices.Count == 0 ? "Names retained / no destination chosen" : $"ref 1-{Choices.Count} / refs 1-{Pages} / ref back";
        lines.Add(InkLine.Plain(navigation, Ink.Muted));
        var compact = new List<InkLine> { heading, boundary, InkLine.Plain(count, Ink.Signal) };
        foreach (var choice in Choices.Skip(first).Take(Math.Min(PageSize, last - first)))
            compact.Add(InkLine.Plain($"ref {choice.Number} / {choice.Match.LineNumber}:{Column(choice.Match)} / " + TerminalText.Clip(choice.Match.Value.Text, 18), Ink.Signal));
        if (Choices.Count == 0)
            foreach (var group in groups.Take(2)) compact.Add(InkLine.Plain(TerminalText.Clip(group.Origin.Value.Text, 18) + " / " + group.Summary, Ink.Muted));
        compact.Add(InkLine.Plain($"refs 1-{Pages} / ref back / details", Ink.Muted));
        return new(SourceLine, lines, compact) { WindowLines = compact, Ownership = structure.Ownership, CompactRowBudget = 8 };
    }

    internal DocumentAnnotation Visit(ReferenceChoice choice)
    {
        var structure = StructureConnection.Describe(index, choice.Match.LineNumber);
        var heading = InkLine.Plain("Name match / " + choice.Match.Value.Text, Ink.Signal);
        var position = InkLine.Plain(Position(choice.Match));
        var origin = InkLine.Plain($"From line {choice.Origin.LineNumber} / {choice.Origin.RoleLabel}; ref back returns", Ink.Muted);
        var boundary = Boundary(choice.Match.Note);
        return new(choice.Match.LineNumber, [heading, boundary, position, origin,
            InkLine.Plain(Parent(choice.Match) + " / no operation binding", Ink.Muted), .. structure.Lines],
            [heading, boundary, position, origin, InkLine.Plain("Name match / no operation binding", Ink.Muted)]) { Ownership = structure.Ownership };
    }

    private InkLine Boundary(DertNode owner) => DertStructureIndex.Closing(owner) is { } end
        ? InkLine.Plain($"Closed by // on line {index.LineOf(end.Start)}", Ink.Muted) : InkLine.Plain("Closing // missing", Ink.Copper);
    private int Column(NoteName name) => name.Value.Span.Start - index.Document.Source.Lines[name.LineNumber - 1].Start + 1;
    private string Position(NoteName name) => $"line {name.LineNumber}:{Column(name)} / {name.RoleLabel}";
    private static string Parent(NoteName name) => name.Parent is { } parent ? $"belongs to line {parent.LineNumber} / {DertStructureIndex.Label(parent)}" : "belongs to document";
}
