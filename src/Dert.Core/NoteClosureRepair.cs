using System.Globalization;

namespace Dert.Core;

public sealed record NoteBoundary(string Key, int? BeforeLine, int Offset, int KeptCount, int ReleasedCount);
public enum NoteResultRole { OrdinaryNote, ReasoningNote, ReasoningBlock, ReasoningEntry, Other }
public sealed record NoteOwnershipChange(int OriginalLine, int ResultLine, NoteResultRole Role,
    int? OwnerLine, string? OwnerLabel);
public sealed class NoteClosureAnalysis
{
    internal NoteClosureAnalysis(DertDocument document, ReasoningBlockNode block, NoteBoundary[] boundaries)
    {
        Document = document; Block = block; Boundaries = Array.AsReadOnly(boundaries);
    }
    public DertDocument Document { get; }
    public ReasoningBlockNode Block { get; }
    public IReadOnlyList<NoteBoundary> Boundaries { get; }
}

public sealed class NoteClosurePlan : ISourceRepairPlan
{
    public const string RepairKind = "note-closure-repair-v1";
    internal NoteClosurePlan(NoteClosureAnalysis analysis, NoteBoundary boundary, SourceEdit edit,
        DertDocument result, NoteOwnershipChange[] changes, bool usesDefaultNewline, int closingLine)
    {
        Analysis = analysis; Boundary = boundary; Edit = edit; ResultDocument = result;
        OwnershipChanges = Array.AsReadOnly(changes); UsesDefaultNewline = usesDefaultNewline;
        ClosingLine = closingLine;
    }
    public NoteClosureAnalysis Analysis { get; }
    public string Source => Analysis.Document.Source.Text;
    public string ProposedText => ResultDocument.Source.Text;
    public string Kind => RepairKind;
    public bool IsReady => true;
    public string Explanation => Boundary.BeforeLine is int line
        ? $"Close this reasoning block before source line {line}. Only the selected // marker is added."
        : "Close this reasoning block at the end of the file. Only the selected // marker is added.";
    public SourceEdit Edit { get; }
    public NoteBoundary Boundary { get; }
    public IReadOnlyList<NoteOwnershipChange> OwnershipChanges { get; }
    public DertDocument ResultDocument { get; }
    public bool UsesDefaultNewline { get; }
    public int ClosingLine { get; }
    public string ApplyTo(string source)
    {
        if (!string.Equals(Source, source, StringComparison.Ordinal))
            throw new InvalidOperationException("The document changed after this preview. Preview it again before applying.");
        return ProposedText;
    }
}

public static class NoteClosureRepair
{
    public static NoteClosureAnalysis Analyze(DertDocument document, int atLine)
    {
        if (atLine < 1 || atLine > document.Source.Lines.Count)
            throw new ArgumentException($"Choose a source line from 1 to {document.Source.Lines.Count}.");
        var offset = document.Source.Lines[atLine - 1].Start;
        var block = Walk(document.Nodes).Select(n => n.Node).OfType<ReasoningBlockNode>()
            .FirstOrDefault(b => b.Closing is null && (b.Span.Contains(offset) ||
                offset == document.Source.Text.Length && b.Span.End == offset));
        if (block is null)
            throw new ArgumentException("There is no unfinished reasoning block at this line. Choose its opener or an entry; inline notes and lists need a different decision.");
        var boundaries = block.Entries.Select((e, i) => new NoteBoundary(
            e.LineNumber.ToString(CultureInfo.InvariantCulture), e.LineNumber, e.Span.Start, i, block.Entries.Count - i)).ToList();
        var stop = block.Span.End == document.Source.Text.Length ? null
            : document.Source.Lines.First(l => l.Start == block.Span.End);
        boundaries.Add(new(stop?.Number.ToString(CultureInfo.InvariantCulture) ?? "eof", stop?.Number,
            block.Span.End, block.Entries.Count, 0));
        return new(document, block, boundaries.ToArray());
    }

    public static NoteClosurePlan Plan(NoteClosureAnalysis analysis, string boundaryKey)
    {
        var boundary = analysis.Boundaries.FirstOrDefault(b => b.Key.Equals(boundaryKey, StringComparison.OrdinalIgnoreCase));
        if (boundary is null)
            throw new ArgumentException("Choose one of this block's offered source lines, or eof when offered. No boundary was guessed.");
        var source = analysis.Document.Source;
        var previousNewline = source.Lines.LastOrDefault(l => l.End <= boundary.Offset && l.End > l.ContentEnd);
        var newline = previousNewline is null ? "\r\n" : source.Text[previousNewline.ContentEnd..previousNewline.End];
        var atUnterminatedEof = boundary.BeforeLine is null && !source.Text.EndsWith('\r') && !source.Text.EndsWith('\n');
        var addition = atUnterminatedEof ? newline + "//" : "//" + newline;
        var edit = new SourceEdit(new(boundary.Offset, 0), "", addition);
        var proposed = source.Text[..boundary.Offset] + addition + source.Text[boundary.Offset..];
        var result = DertParser.Parse(proposed);
        var nodes = Walk(result.Nodes).ToArray();
        var closed = nodes.Select(n => n.Node).OfType<ReasoningBlockNode>()
            .SingleOrDefault(b => b.LineNumber == analysis.Block.LineNumber);
        var markerOffset = boundary.Offset + (atUnterminatedEof ? newline.Length : 0);
        if (closed?.Closing?.Start != markerOffset || closed.Entries.Count != boundary.KeptCount ||
            closed.Entries.Where((entry, i) =>
                entry.LineNumber != analysis.Block.Entries[i].LineNumber ||
                entry.Span.Start != analysis.Block.Entries[i].Span.Start ||
                entry.Value != analysis.Block.Entries[i].Value ||
                entry.Annotation != analysis.Block.Entries[i].Annotation ||
                entry.HasComma != analysis.Block.Entries[i].HasComma).Any())
            throw new InvalidOperationException("The selected boundary did not preserve this block's expected entries. No repair is available for this interpretation.");

        // Match the node's own source line and real AST parent. A pending statement
        // may span across unrelated notes, so enclosing-span lookup is not ownership.
        var byLine = nodes.GroupBy(n => n.Node.LineNumber).ToDictionary(g => g.Key, g => g.First());
        var changes = analysis.Block.Entries.Skip(boundary.KeptCount).Select(entry =>
        {
            var resultLine = entry.LineNumber + 1;
            if (!byLine.TryGetValue(resultLine, out var location))
                throw new InvalidOperationException("A released source line could not be located in the proposed document.");
            var role = location.Node switch
            {
                NoteNode => NoteResultRole.OrdinaryNote,
                ReasoningNoteNode => NoteResultRole.ReasoningNote,
                ReasoningBlockNode => NoteResultRole.ReasoningBlock,
                NoteEntryNode => NoteResultRole.ReasoningEntry,
                _ => NoteResultRole.Other
            };
            var owner = location.Parent switch
            {
                ReasoningBlockNode b => $"{b.Header.Head.Text} ; {b.Header.Target?.Text}",
                StatementNode s => $"{s.Header.Head.Text} ; {s.Header.Target?.Text}",
                ChainNode => "STERT chain",
                _ => "document"
            };
            return new NoteOwnershipChange(entry.LineNumber, resultLine, role, location.Parent?.LineNumber, owner);
        }).ToArray();
        var closingLine = result.Source.Lines.Single(l => l.ContentSpan.Contains(markerOffset)).Number;
        return new(analysis, boundary, edit, result, changes, previousNewline is null, closingLine);
    }

    private static IEnumerable<(DertNode Node, DertNode? Parent)> Walk(IEnumerable<DertNode> nodes, DertNode? parent = null)
    {
        foreach (var node in nodes)
        {
            yield return (node, parent);
            IEnumerable<DertNode> children = node switch
            {
                ChainNode chain => chain.Children,
                ReasoningBlockNode block => block.Entries,
                StatementNode statement => statement.Values,
                _ => []
            };
            foreach (var child in Walk(children, node)) yield return child;
        }
    }
}
