namespace Dert.Core;

public sealed record ChainEnding(TextSpan Span, int LineNumber, int ChainLine, string Spelling);

public sealed class FlorinEditPlan : ISourceRepairPlan
{
    public string Source { get; }
    public string ProposedText { get; }
    public string Explanation { get; }
    public string Kind => "florin-goodbye-v1";
    public bool IsReady => Edit is not null;
    public SourceEdit? Edit { get; }
    public int LineNumber { get; }
    public int ChainLine { get; }
    public string BeforeLine { get; } = "";
    public string AfterLine { get; } = "";

    internal FlorinEditPlan(DertDocument document, ChainEnding? ending)
    {
        Source = ProposedText = document.Source.Text;
        Explanation = "Select the goodbye that closes a STERT chain. Ordinary words are left alone.";
        if (ending is null) return;
        LineNumber = ending.LineNumber; ChainLine = ending.ChainLine;
        BeforeLine = AfterLine = document.Source.Slice(document.Source.Lines[LineNumber - 1].ContentSpan);
        Explanation = "This goodbye already uses ƒ in your source.";
        if (ending.Spelling != "fin.") return;
        Edit = new(new(ending.Span.Start, 1), "f", "ƒ");
        ProposedText = Source[..Edit.Span.Start] + Edit.After + Source[Edit.Span.End..];
        AfterLine = new SourceText(ProposedText).Slice(document.Source.Lines[LineNumber - 1].ContentSpan);
        Explanation = "Change only f to ƒ in your note. Undo reverses this edit; Save keeps it on disk.";
    }
}

public static class FlorinGoodbye
{
    public static IReadOnlyList<ChainEnding> Endings(DertDocument document) =>
        document.Nodes.OfType<ChainNode>().Where(chain => chain.Terminator is not null)
            .Select(chain => new ChainEnding(chain.Terminator!.Value,
                document.Source.Lines.First(line => line.ContentSpan.Contains(chain.Terminator.Value.Start)).Number,
                chain.LineNumber, document.Source.Slice(chain.Terminator.Value))).ToArray();

    public static ChainEnding? EndingAt(DertDocument document, int line) =>
        Endings(document).FirstOrDefault(ending => ending.LineNumber == line);

    public static FlorinEditPlan Plan(DertDocument document, int line) => new(document, EndingAt(document, line));
}
