using Dert.Core;
using Erpdert.Mer;

namespace Erpdert.Cli.Motion;

internal static class StructureConnection
{
    internal static DocumentAnnotation Describe(DertStructureIndex index, int line)
    {
        var lines = new List<InkLine>();
        var compact = new List<InkLine>();
        var location = index.AtLine(line);
        Add(index.DescribeLine(line) + $" / line {line}", Ink.Signal);
        if (location is null)
        {
            Add("Spacing between written structures.", Ink.Muted);
            return new(line, lines);
        }
        var node = location.Node;
        var owner = location.Parent;
        var ownerText = owner is null ? "Belongs to this document" : $"Belongs to line {owner.LineNumber} / {DertStructureIndex.Label(owner)}";
        compact.Add(lines[0]);
        compact.Add(InkLine.Plain(TerminalText.Clip(ownerText, 56), Ink.Muted));
        DocumentOwnership? ownership = null;
        if (node.LineNumber != line)
        {
            Add($"Closes line {node.LineNumber} / {DertStructureIndex.Label(node)}");
            Add("owner returns to its opening line.", Ink.Muted);
        }
        else
        {
            switch (node)
            {
                case NoteEntryNode entry:
                    var siblings = owner is null ? [] : DertStructureIndex.Children(owner).OfType<NoteEntryNode>().ToArray();
                    Add($"Entry {Array.IndexOf(siblings, entry) + 1} of {siblings.Length}", Ink.Muted);
                    Add(entry.Value.Text);
                    Add(entry.Annotation is null ? "No explanation on this entry." : "// " + entry.Annotation.Text, Ink.Signal);
                    Add(entry.HasComma ? "Comma present / retained as written" : "No trailing comma", Ink.Muted);
                    compact.Add(InkLine.Plain(TerminalText.Clip(entry.Value.Text, 56)));
                    compact.Add(InkLine.Plain(TerminalText.Clip(entry.Annotation?.Text ?? "No entry explanation", 56), Ink.Signal));
                    break;
                case ReasoningNoteNode note:
                    Fields(note.Header);
                    Add("Reason / " + note.Header.Payload?.Text);
                    Add("Target names as written / no resolved binding", Ink.Muted);
                    break;
                case ReasoningBlockNode block:
                    Fields(block.Header);
                    Entries(block.Entries);
                    Add("Target names as written / no resolved binding", Ink.Muted);
                    break;
                case StatementNode statement:
                    Fields(statement.Header);
                    Entries(statement.Values);
                    Add("Source structure / operation meaning is separate", Ink.Muted);
                    break;
                case ChainNode chain:
                    Fields(chain.Header);
                    foreach (var child in chain.Children)
                        Add($"  |- {child.LineNumber} / {DertStructureIndex.Label(child)}", Ink.Muted);
                    break;
                case NoteNode note:
                    Add(note.Content.Text);
                    Add(note.IsTitle ? index.AtLine(line + 1)?.Node is RuleNode ? $"Title rule on line {line + 1}" : "No adjacent title rule is present."
                        : "Your complete note / no command inferred", Ink.Muted);
                    compact.Add(InkLine.Plain(TerminalText.Clip(note.Content.Text, 56)));
                    break;
                case TextNode text:
                    Add(text.Content.Text);
                    Add("Text retained without inventing a command.", Ink.Muted);
                    compact.Add(InkLine.Plain(TerminalText.Clip(text.Content.Text, 56)));
                    break;
                case RuleNode:
                    Add("Adaptive rule under the document title.", Ink.Muted);
                    break;
            }
        }
        Add(ownerText, Ink.Muted);
        var boundary = node is ReasoningBlockNode or ReasoningNoteNode or ChainNode ? node : owner;
        if (boundary is ReasoningBlockNode or ReasoningNoteNode or ChainNode)
        {
            var end = DertStructureIndex.Closing(boundary);
            var closing = end is { } span ? index.LineOf(span.Start) : (int?)null;
            var label = boundary is ChainNode ? closing is { } cl ? $"Chain ends on line {cl}" : "Chain ending missing"
                : closing is { } nl ? $"Closed by // on line {nl}" : "Closing // missing";
            Add(label, closing is null ? Ink.Copper : Ink.Muted);
            if (compact.Count < 5) compact.Add(InkLine.Plain(label, closing is null ? Ink.Copper : Ink.Muted));
            if (boundary is not ReasoningNoteNode) ownership = new(boundary.LineNumber, closing,
                DertStructureIndex.Children(boundary).Select(child => child.LineNumber).ToArray(), index.LineOf(Math.Max(boundary.Span.Start, boundary.Span.End - 1)));
        }
        else if (owner is StatementNode statement && statement.Values.Count > 0)
        {
            var last = statement.Values[^1];
            ownership = new(owner.LineNumber, last.HasComma ? null : last.LineNumber, statement.Values.Select(entry => entry.LineNumber).ToArray(), last.LineNumber);
        }
        if (compact.Count < 5) compact.Add(InkLine.Plain("structure / details keeps full text", Ink.Muted));
        return new(line, lines, compact) { Ownership = ownership };

        void Add(string text, Ink ink = Ink.Text) => lines.Add(InkLine.Plain(text, ink));

        void Fields(Header header)
        {
            Add("Head / " + header.Head.Text);
            if (header.Hook is not null) Add("; hook / " + (string.IsNullOrEmpty(header.Target?.Text) ? "target not yet written" : header.Target.Text), Ink.Copper);
            if (header.Line is not null)
            {
                var payload = header.Payload?.Text;
                var entries = DertStructureIndex.Children(node).OfType<NoteEntryNode>().Count();
                Add(";; line / " + (!string.IsNullOrEmpty(payload) ? node is ReasoningNoteNode ? "reason follows" : payload : entries > 0 ? $"{entries} entries below" : "no payload written"), Ink.Signal);
            }
            compact.Add(InkLine.Plain(TerminalText.Clip(header.Head.Text + (header.Target is null ? "" : " ; " + header.Target.Text), 56)));
            if (!string.IsNullOrEmpty(header.Payload?.Text)) compact.Add(InkLine.Plain(TerminalText.Clip(header.Payload.Text, 56), Ink.Signal));
        }

        void Entries(IReadOnlyList<NoteEntryNode> entries)
        {
            if (entries.Count == 0) return;
            foreach (var entry in entries)
            {
                Add($"  {(entry == entries[^1] ? "'" : "|")}- {entry.LineNumber} / {entry.Value.Text}");
                if (entry.Annotation is not null) Add("     // " + entry.Annotation.Text, Ink.Muted);
            }
            compact.Add(InkLine.Plain($"{entries.Count} entries / lines {entries[0].LineNumber}-{entries[^1].LineNumber}", Ink.Signal));
        }
    }
}
