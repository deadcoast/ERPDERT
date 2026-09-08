using Dert.Core;
using Erpdert.Cli.Discovery;
using Erpdert.Cli.Operations;

namespace Erpdert.Cli;

internal sealed partial class DocumentRenderer
{
    public void OperationPlan(DertOperationPlan plan, string path, bool preview)
    {
        Banner(preview ? "DERT / PREVIEW" : "DERT / RUN");
        EvidencePath(path);
        Line("YOUR SOURCE → ERPDERT OPERATIONS", SemanticRole.Line);
        Line($"{Count(plan.Steps.Count, "supported operation")} identified · notes remain context", SemanticRole.ContextNote);
        Line("");

        var document = plan.Document;
        var byLine = plan.Steps.ToDictionary(step => step.Source.LineNumber);
        var number = 0;
        foreach (var node in document.Nodes)
        {
            if (node is ChainNode chain)
            {
                Line("");
                if (byLine.ContainsKey(chain.LineNumber))
                    Line($"STEP {++number:00} / ERPDERT codex", SemanticRole.Reasoning);
                else
                    Line(chain.Header.Hook is null && chain.Header.Line is null
                        ? "CONTEXT CHAIN / no header operation requested"
                        : "CHAIN / no supported header operation", SemanticRole.ContextNote);
                EvidencePath($"FROM {path}:{chain.LineNumber}");
                OperationSource(document, chain);
                if (byLine.ContainsKey(chain.LineNumber))
                {
                    Line("MEANS   Find local Codex command routes, packages, and dependency evidence.", SemanticRole.Target);
                    Line("EFFECT  Read-only discovery. The notes explain this request; they do not choose an installer or run commands.", SemanticRole.ContextNote);
                }
            }
            else OperationSource(document, node);
        }
        foreach (var finding in plan.Findings)
        {
            Line("");
            Line($"{finding.Code} / {finding.Severity.ToString().ToUpperInvariant()}",
                finding.Severity == DiagnosticSeverity.Error ? SemanticRole.Hook : SemanticRole.ContextNote);
            EvidencePath($"{path}:{finding.LineNumber}");
            SourceLine(document, document.Source.Lines[finding.LineNumber - 1], true);
            Line(finding.Message);
        }
        Line("");
    }

    public void OperationsBlocked()
    {
        Line("No operations ran. The source findings above need attention before this request can be dispatched.", SemanticRole.Hook);
        Line("Your source was not changed.", SemanticRole.ContextNote);
        Line("ƒin.", SemanticRole.Boundary);
    }

    public void NoOperations()
    {
        Line("No operations to run. This document contains no supported operation request.");
        Line("Nothing was searched or changed.", SemanticRole.ContextNote);
        Line("ƒin.", SemanticRole.Boundary);
    }

    public void OperationsPreviewed(string path)
    {
        Line("No discovery was performed. This preview only interprets your DERT.", SemanticRole.Reasoning);
        Line("Run the request:", SemanticRole.Text);
        CopyCommand($"{Invocation()} run {PowerShellLiteral(path)}" + (plain ? " --plain" : ""));
        Line("Your source was not changed. Running this supported request searches only; it does not install or update anything.", SemanticRole.ContextNote);
        Line("ƒin.", SemanticRole.Boundary);
    }

    public void OperationResult(PlannedOperation step, string path, int number, DiscoveryReport report)
    {
        Line($"STEP {number:00} / RESULT", SemanticRole.Title);
        EvidencePath($"FROM {path}:{step.Source.LineNumber}");
        Discovery(report, embedded: true);
        Line("");
    }

    public void OperationsCompleted(int completed, int attention)
    {
        Line("DERT / COMPLETE", SemanticRole.Title);
        Line($"{Count(completed, "operation")} completed · {attention} {(attention == 1 ? "result needs" : "results need")} attention",
            attention > 0 ? SemanticRole.Hook : SemanticRole.Line);
        Line("Your DERT drove the discovery. No discovered program was launched; no installations or source files changed.", SemanticRole.ContextNote);
        Line("ƒin.", SemanticRole.Boundary);
    }

    private void OperationSource(DertDocument document, DertNode node)
    {
        foreach (var sourceLine in document.Source.Lines.Where(line => line.Number >= node.LineNumber && line.Start < node.Span.End))
            SourceLine(document, sourceLine, sourceLine.Number == node.LineNumber && node is ChainNode);
    }
}
