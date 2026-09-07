namespace Dert.Core;

public enum TitleRepairState { Ready, Aligned, NoTitle, Unavailable }

public sealed class TitleRepairPlan : ISourceRepairPlan
{
    internal TitleRepairPlan(string source, TitleRepairState state, string explanation,
        int titleLine = 0, int ruleLine = 0, int? width = null, SourceEdit? edit = null)
    {
        Source = source;
        State = state;
        Explanation = explanation;
        TitleLine = titleLine;
        RuleLine = ruleLine;
        Width = width;
        Edit = edit;
    }

    public string Source { get; }
    public string Kind => "title-rule-repair-v1";
    public bool IsReady => State == TitleRepairState.Ready;
    public TitleRepairState State { get; }
    public string Explanation { get; }
    public int TitleLine { get; }
    public int RuleLine { get; }
    public int? Width { get; }
    public SourceEdit? Edit { get; }
    public string ProposedText => Edit is null ? Source :
        Source[..Edit.Span.Start] + Edit.After + Source[Edit.Span.End..];
    public string ApplyTo(string source)
    {
        if (!string.Equals(source, Source, StringComparison.Ordinal))
            throw new InvalidOperationException("The document changed after this preview. Preview it again before applying.");
        return ProposedText;
    }
}

public static class TitleRuleRepair
{
    public static TitleRepairPlan Plan(DertDocument document)
    {
        var source = document.Source;
        var title = document.Nodes.OfType<NoteNode>().FirstOrDefault(n => n.IsTitle);
        if (title is null)
            return new(source.Text, TitleRepairState.NoTitle,
                "There is no main title to align. Notes and reasoning have been left alone.");
        if (title.Content.Text.Any(c => c is < ' ' or > '~'))
            return new(source.Text, TitleRepairState.Unavailable,
                "This title uses characters whose visible width this repair cannot yet measure reliably. Your words stay unchanged.",
                title.LineNumber, title.LineNumber + 1);

        var width = title.Content.Text.Length;
        var titleLine = source.Lines[title.LineNumber - 1];
        var next = source.Lines.ElementAtOrDefault(title.LineNumber);
        var existing = next is null ? "" : source.Slice(next.ContentSpan);
        var trimmed = existing.Trim();
        var hasPrefix = trimmed.StartsWith("//", StringComparison.Ordinal);
        var marks = hasPrefix ? trimmed[2..].Trim() : trimmed;
        var isRule = marks.Length > 0 && marks.All(c => c == '-');
        if (isRule && hasPrefix && marks.Length == width)
            return new(source.Text, TitleRepairState.Aligned, "The title and its rule already align. Nothing needs changing.",
                title.LineNumber, title.LineNumber + 1, width);

        var newRule = "// " + new string('-', width);
        SourceEdit edit;
        string explanation;
        if (isRule)
        {
            edit = new(next!.ContentSpan, existing, newRule);
            var difference = Math.Abs(width - marks.Length);
            explanation = !hasPrefix
                ? $"Give the title rule its // prefix and match the title's {width} columns."
                : $"The line under your title is {difference} {(difference == 1 ? "mark" : "marks")} " +
                    (marks.Length < width ? "short." : "too long.");
        }
        else
        {
            var newline = source.Text[titleLine.ContentEnd..titleLine.End];
            var addition = newline.Length > 0 ? newRule + newline : "\r\n" + newRule;
            edit = new(new(titleLine.End, 0), "", addition);
            explanation = $"Add a {width}-mark rule directly beneath your title. The following words stay where they belong.";
        }
        return new(source.Text, TitleRepairState.Ready, explanation, title.LineNumber,
            title.LineNumber + 1, width, edit);
    }
}
