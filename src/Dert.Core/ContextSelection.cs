using System.Security.Cryptography;
using System.Text;

namespace Dert.Core;

public enum ContextSourceKind { FileSnapshot, EditorBuffer }

public sealed record ContextNote(DertNode Node, DertNode? Parent, bool IsRelated)
{
    public int LineNumber => Node.LineNumber;
    public string Label => DertStructureIndex.Label(Node);
    public TextSpan Span => Node.Span;
    public bool IsClosed => DertStructureIndex.Closing(Node) is not null;
}

public sealed record ContextRange(TextSpan Span, int FirstLine, int LastLine, string Text);

public sealed class ContextSelection
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private readonly DertStructureIndex structure;
    private readonly Dictionary<int, ContextNote> byLine;
    public DertDocument Document { get; }
    public ContextNote Primary { get; }
    public IReadOnlyList<ContextNote> Notes { get; }
    public string SourceLabel { get; }
    public ContextSourceKind Kind { get; }
    public string SourceRevision { get; }

    private ContextSelection(DertStructureIndex structure, DertNode primary, string sourceLabel, ContextSourceKind kind)
    {
        this.structure = structure;
        Document = structure.Document;
        SourceLabel = sourceLabel;
        Kind = kind;
        SourceRevision = Hash(Document.Source.Text);
        var references = new NoteReferenceIndex(Document);
        var related = new HashSet<DertNode>(references.ForNote(primary).SelectMany(group => group.Matches).Select(match => match.Note),
            ReferenceEqualityComparer.Instance);
        Notes = Array.AsReadOnly(structure.Locations.Where(location => IsReasoning(location.Node))
            .OrderBy(location => location.Node.Span.Start)
            .Select(location => new ContextNote(location.Node, location.Parent, related.Contains(location.Node))).ToArray());
        byLine = Notes.ToDictionary(note => note.LineNumber);
        Primary = byLine[primary.LineNumber];
    }

    public static ContextSelection Capture(DertDocument document, int atLine, string sourceLabel, ContextSourceKind kind)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceLabel);
        if (!Enum.IsDefined(kind)) throw new ArgumentException("Choose a file snapshot or editor buffer as the context source.", nameof(kind));
        var structure = new DertStructureIndex(document);
        var location = structure.AtLine(atLine);
        var primary = location is null ? null : IsReasoning(location.Node) ? location.Node
            : location.Node is NoteEntryNode && location.Parent is ReasoningBlockNode ? location.Parent : null;
        if (primary is null)
            throw new ArgumentException("Choose a reasoning note's opener, one of its entries, or its closing //. This line has no reasoning note owner.", nameof(atLine));
        try
        {
            // Both the revision and its displayed identity must survive strict UTF-8.
            Utf8.GetByteCount(sourceLabel);
            return new(structure, primary, sourceLabel, kind);
        }
        catch (EncoderFallbackException error)
        {
            throw new ArgumentException("The captured source or its name contains an incomplete Unicode character. Finish that character before copying or exporting context; your source is unchanged.", error);
        }
    }

    public ContextPacket Build(IEnumerable<int>? includedLines = null)
    {
        var selected = new HashSet<int> { Primary.LineNumber };
        foreach (var line in includedLines ?? [])
        {
            if (!byLine.ContainsKey(line))
                throw new ArgumentException($"Line {line} is not a reasoning note opener in this captured source. Include a note using its opener line.", nameof(includedLines));
            selected.Add(line);
        }
        var included = Array.AsReadOnly(Notes.Where(note => selected.Contains(note.LineNumber)).ToArray());
        var excluded = new List<ContextRange>();
        var offset = 0;
        foreach (var note in included)
        {
            AddOmitted(offset, note.Span.Start);
            offset = note.Span.End;
        }
        AddOmitted(offset, Document.Source.Text.Length);
        return new(this, included, excluded.AsReadOnly());

        void AddOmitted(int start, int end)
        {
            if (start == end) return;
            var span = new TextSpan(start, end - start);
            excluded.Add(new(span, structure.LineOf(start), structure.LineOf(end - 1), Document.Source.Slice(span)));
        }
    }

    internal static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Utf8.GetBytes(text))).ToLowerInvariant();
    private static bool IsReasoning(DertNode node) => node is ReasoningNoteNode or ReasoningBlockNode;
}

public sealed class ContextPacket
{
    public ContextSelection Selection { get; }
    public IReadOnlyList<ContextNote> Included { get; }
    public IReadOnlyList<ContextRange> Excluded { get; }
    public string Report { get; }
    public string LeftOutReport { get; }
    public string ReviewId { get; }

    internal ContextPacket(ContextSelection selection, IReadOnlyList<ContextNote> included, IReadOnlyList<ContextRange> excluded)
    {
        Selection = selection;
        Included = included;
        Excluded = excluded;
        Report = ContextReport.Build(this, leftOut: false);
        ReviewId = ContextSelection.Hash(Report);
        LeftOutReport = ContextReport.Build(this, leftOut: true);
    }
}
