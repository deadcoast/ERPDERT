using Dert.Core;

namespace Dert.Cli;

internal enum LintRehearsal { None, TitleRule, NoteBoundary, AuthoringList }

internal sealed record LintRepairAdvice(LintRehearsal Rehearsal, string Explanation, int? AtLine = null)
{
    internal static LintRepairAdvice For(DertDocument document, Diagnostic finding)
    {
        switch (finding.Code)
        {
            case "DERT2003":
                var title = TitleRuleRepair.Plan(document);
                return title.IsReady
                    ? new(LintRehearsal.TitleRule, "See the exact underline edit. Opening this rehearsal does not change your file.")
                    : new(LintRehearsal.None, title.Explanation);
            case "DERT1001":
                try
                {
                    var analysis = NoteClosureRepair.Analyze(document, finding.LineNumber);
                    if (analysis.Block.LineNumber == finding.LineNumber)
                        return new(LintRehearsal.NoteBoundary,
                            "See this note's entries and possible endings. You choose what belongs inside; no ending is guessed.",
                            analysis.Block.LineNumber);
                }
                catch (ArgumentException) { /* Preserve the finding if no supported block can be selected. */ }
                return new(LintRehearsal.None, "This finding could not be matched to a supported unfinished block. Your source stays unchanged.");
            case "DERT1002":
                return new(LintRehearsal.None, "Inline-note closure is not available as a repair. The block-boundary repair does not apply to this line.");
            case "DERT3003":
                return new(LintRehearsal.AuthoringList, "The notes window offers Add another item and Finish this list with exact previews. Opening it does not apply either choice.", finding.LineNumber);
            case "DERT3004":
                return new(LintRehearsal.None, TitleRuleRepair.Plan(document).Explanation);
            default:
                return new(LintRehearsal.None, "There is no supported repair for this finding yet. Your source stays unchanged.");
        }
    }
}
