namespace Dert.Core;

public sealed record SourceEdit(TextSpan Span, string Before, string After);

public interface ISourceRepairPlan
{
    string Source { get; }
    string ProposedText { get; }
    string Explanation { get; }
    string Kind { get; }
    bool IsReady { get; }
    SourceEdit? Edit { get; }
}
