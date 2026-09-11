using Dert.Core;

namespace Dert.Cli;

internal sealed partial class DocumentRenderer
{
    internal void ResultRetained(ResultObservation observation, string record)
    {
        Line("Result retained / bytes verified", SemanticRole.Sinker);
        EvidencePath(record);
        Line("The source is unchanged. Review the observation before keeping it in your note.");
        CopyCommand($"{Invocation()} result {PowerShellLiteral(record)} --into {PowerShellLiteral(observation.Origin.Path)}" + (plain ? " --plain" : ""));
    }

    internal void ResultReview(ResultObservation observation, string record, string destination, ResultNotePlan? plan, bool details, int? at)
    {
        Line($"Observed {observation.ObservedAt:yyyy-MM-dd HH:mm:ss zzz} / {observation.Outcome}", SemanticRole.Reasoning);
        Line(observation.Summary);
        Line("Historical record / producer-reported evidence; no current installation check or operation was performed by opening it.", SemanticRole.ContextNote);
        if (plan is not null)
        {
            Line(plan.Comparison.Message, SemanticRole.Target);
            Line(plan.Explanation, plan.IsReady ? SemanticRole.Text : SemanticRole.Hook);
        }
        else Line("The original source is unavailable. Choose --into with an existing document to review keeping this history.", SemanticRole.ContextNote);
        if (details)
        {
            ContextText(ResultNotes.Describe(observation));
            if (plan is not null)
            {
                Line("SOURCE COMPARISON", SemanticRole.Reasoning); ContextText(ResultNotes.DescribeComparison(plan.Comparison));
                Line("INSERTION PREVIEW / added source only", SemanticRole.Reasoning); ContextText(plan.NoteText);
            }
        }
        if (plan?.IsReady == true)
        {
            Line("Keep result in this note / open the exact insertion preview:", SemanticRole.Text);
            CopyCommand($"{Invocation()} write {PowerShellLiteral(destination)} --result {PowerShellLiteral(record)}" +
                (at is int line ? $" --at {line}" : "") + (still ? " --still" : ""));
            Line("Apply changes the Notes buffer once; Undo removes it. Save keeps it on disk.", SemanticRole.ContextNote);
        }
        if (!details) Line("Use --details for all evidence, captured/current source and the complete insertion.", SemanticRole.ContextNote);
    }
}
