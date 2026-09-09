namespace Dert.Core;

public sealed record StructureLocation(DertNode Node, DertNode? Parent);
public sealed record SourceHit(int LineNumber, string Text, IReadOnlyList<TextSpan> Matches);

/// <summary>A projection of one parsed revision, with no application bindings or source edits.</summary>
public sealed class DertStructureIndex
{
    private readonly Dictionary<int, StructureLocation> byLine = [];
    public DertDocument Document { get; }
    public IReadOnlyList<StructureLocation> Locations { get; }

    public DertStructureIndex(DertDocument document)
    {
        Document = document;
        var locations = new List<StructureLocation>();
        Visit(document.Nodes, null);
        Locations = locations.AsReadOnly();

        void Visit(IEnumerable<DertNode> nodes, DertNode? parent)
        {
            foreach (var node in nodes)
            {
                var location = new StructureLocation(node, parent);
                locations.Add(location);
                byLine[node.LineNumber] = location;
                if (Closing(node) is { } closing) byLine[LineOf(closing.Start)] = location;
                Visit(Children(node), node);
            }
        }
    }

    public StructureLocation? AtLine(int line) => byLine.GetValueOrDefault(line);

    public int? OwnerLine(int line) => AtLine(line) is { } location
        ? location.Node.LineNumber != line ? location.Node.LineNumber : location.Parent?.LineNumber : null;

    public string DescribeLine(int line) => AtLine(line) is { } location
        ? location.Node.LineNumber != line && Closing(location.Node) is not null
            ? location.Node is ChainNode ? "Chain ending" : "Reasoning ending"
            : Kind(location) : "Spacing";

    public IReadOnlyList<SourceHit> Find(string text)
    {
        if (string.IsNullOrEmpty(text)) return [];
        var hits = new List<SourceHit>();
        foreach (var line in Document.Source.Lines)
        {
            var content = Document.Source.Slice(line.ContentSpan);
            var matches = new List<TextSpan>();
            var offset = 0;
            while (offset <= content.Length - text.Length)
            {
                var found = content.IndexOf(text, offset, StringComparison.OrdinalIgnoreCase);
                if (found < 0) break;
                matches.Add(new(line.Start + found, text.Length));
                offset = found + text.Length;
            }
            if (matches.Count > 0) hits.Add(new(line.Number, content, matches.AsReadOnly()));
        }
        return hits.AsReadOnly();
    }

    public int LineOf(int offset)
    {
        var lines = Document.Source.Lines;
        var low = 0;
        var high = lines.Count - 1;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (lines[middle].Start <= offset) low = middle;
            else high = middle - 1;
        }
        return lines[low].Number;
    }

    public static TextSpan? Closing(DertNode node) => node switch
    {
        ChainNode chain => chain.Terminator,
        ReasoningBlockNode block => block.Closing,
        ReasoningNoteNode note => note.Closing,
        _ => null
    };

    public static IEnumerable<DertNode> Children(DertNode node) => node switch
    {
        ChainNode chain => chain.Children,
        StatementNode statement => statement.Values,
        ReasoningBlockNode block => block.Entries,
        _ => []
    };

    public static string Kind(StructureLocation location) => location.Node switch
    {
        ChainNode => "Chain",
        NoteNode { IsTitle: true } => "Title",
        NoteNode => "Free note",
        ReasoningNoteNode => "Reasoning note",
        ReasoningBlockNode => "Reasoning block",
        NoteEntryNode => location.Parent is ReasoningBlockNode ? "Reasoning entry" : "Sinker",
        StatementNode => "Statement",
        RuleNode => "Title rule",
        _ => "Text"
    };

    public static string Label(DertNode node) => node switch
    {
        ChainNode chain => chain.Header.Head.Text,
        ReasoningNoteNode note => note.Header.Head.Text,
        ReasoningBlockNode block => block.Header.Head.Text,
        StatementNode statement => statement.Header.Head.Text,
        NoteEntryNode entry => entry.Value.Text,
        NoteNode note => note.Content.Text,
        TextNode text => text.Content.Text,
        _ => "title rule"
    };
}
