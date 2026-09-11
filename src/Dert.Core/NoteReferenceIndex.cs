namespace Dert.Core;

public enum NoteNameRole { Content, Target, ListedItem }
public sealed record NoteName(DertNode Note, DertNode? Parent, TextField RawField, TextField Value,
    NoteNameRole Role, int LineNumber)
{
    public bool IsComplete => Value.Text.Length > 0 && !HeaderReader.HasUnclosedQuote(Value.Text, 0, Value.Text.Length);
    public string RoleLabel => Role switch { NoteNameRole.Content => "content name", NoteNameRole.Target => "target", _ => "listed item" };
}
public sealed record NoteReferenceGroup(NoteName Origin, IReadOnlyList<NoteName> Matches)
{
    public string Summary => Origin.Value.Text.Length == 0 ? "Name not written" : !Origin.IsComplete ? "Name unfinished / no matches chosen"
        : Matches.Count == 0 ? "No other named place" : $"{Matches.Count} other named {(Matches.Count == 1 ? "place" : "places")}";
}

/// <summary>Written name occurrences between reasoning notes in one parsed revision.</summary>
public sealed class NoteReferenceIndex
{
    private readonly Dictionary<string, List<NoteName>> byName = new(StringComparer.OrdinalIgnoreCase);
    public DertDocument Document { get; }
    public IReadOnlyList<NoteName> Names { get; }

    public NoteReferenceIndex(DertDocument document)
    {
        Document = document;
        var names = new List<NoteName>();
        var structure = new DertStructureIndex(document);
        foreach (var location in structure.Locations)
        {
            var header = location.Node switch { ReasoningNoteNode note => note.Header, ReasoningBlockNode block => block.Header, _ => null };
            if (header is null) continue;
            Add(header.Head, header.Head, NoteNameRole.Content, location.Node.LineNumber);
            if (header.Target is { } target)
                foreach (var part in SourceFieldParts.SplitCommas(document.Source, target)) Add(target, part.Value, NoteNameRole.Target, location.Node.LineNumber);
            if (ContextReading.Read(structure, location.Node) is { Reader: ContextReader.Similarities } reading)
                foreach (var item in reading.Items)
                    Add(header.Payload is { Text.Length: > 0 } payload ? payload : item.Value, item.Value, NoteNameRole.ListedItem, item.LineNumber);

            void Add(TextField raw, TextField value, NoteNameRole role, int line)
            {
                var name = new NoteName(location.Node, location.Parent, raw, value, role, line);
                names.Add(name);
                if (!name.IsComplete) return;
                if (!byName.TryGetValue(value.Text, out var matches)) byName[value.Text] = matches = [];
                matches.Add(name);
            }
        }
        Names = names.AsReadOnly();
    }

    public IReadOnlyList<NoteReferenceGroup> ForNote(DertNode note) => Names.Where(name => ReferenceEquals(name.Note, note))
        .Select(name => new NoteReferenceGroup(name, name.IsComplete && byName.TryGetValue(name.Value.Text, out var matches)
            ? Array.AsReadOnly(matches.Where(match => !ReferenceEquals(match.Note, note)).ToArray()) : []))
        .ToArray();
}
