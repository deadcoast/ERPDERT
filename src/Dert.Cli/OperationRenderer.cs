using Dert.Cli.Applications;
using Dert.Core;
using Dert.Cli.Discovery;
using Dert.Cli.Operations;

namespace Dert.Cli;

internal sealed partial class DocumentRenderer
{
    public void OperationPlan(DertOperationPlan plan, string path, bool preview)
    {
        Banner(preview ? "DERT / PREVIEW" : "DERT / RUN");
        EvidencePath(path);
        Line("YOUR SOURCE → DERT OPERATIONS", SemanticRole.Line);
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
                if (byLine.TryGetValue(chain.LineNumber, out var step))
                    Line($"STEP {++number:00} / {step.Command}", SemanticRole.Reasoning);
                else
                    Line(chain.Header.Hook is null && chain.Header.Line is null
                        ? "CONTEXT CHAIN / no header operation requested"
                        : "CHAIN / no supported header operation", SemanticRole.ContextNote);
                EvidencePath($"FROM {path}:{chain.LineNumber}");
                OperationSource(document, chain);
                if (step is not null)
                {
                    Line("MEANS   " + (step.Kind switch
                    {
                        ApplicationAction.Where => $"Locate {step.Request.Application.DisplayName}, its command routes and related folders.",
                        ApplicationAction.Doctor => "Diagnose the selected installation and review a manager-specific reinstall proposal.",
                        ApplicationAction.UpdateDiscovery => "Find local Codex command routes, packages, and dependency evidence.",
                        _ => "Prepare an update rehearsal for this Codex installation."
                    }), SemanticRole.Target);
                    Line("EFFECT  " + (step.Kind switch
                    {
                        ApplicationAction.Where => "Read local location evidence and measure folder sizes. No programs started or files changed.",
                        ApplicationAction.Doctor => "Verify the matching manager and review the exact repair. Queries may write npm cache/logs; package changes need separate approval.",
                        ApplicationAction.UpdateDiscovery => "Read-only discovery. The notes explain this request; they do not choose an installer or run commands.",
                        _ => "Query the identified npm and registry; explain the exact proposed update. npm may write cache/logs. No package changes."
                    }), SemanticRole.ContextNote);
                    if (step.RequestLine != chain.LineNumber) EvidencePath($"REQUEST {path}:{step.RequestLine}");
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
        CopyCommand($"{Invocation()} run {PowerShellLiteral(path)}" + (plain ? " --plain" : "") + (still ? " --still" : ""));
        Line("Your source was not changed. Running this request does not install or update packages; an update rehearsal queries npm and registry metadata.", SemanticRole.ContextNote);
        Line("ƒin.", SemanticRole.Boundary);
    }

    public void OperationResult(PlannedOperation step, string path, int number, DiscoveryReport report)
    {
        Line($"STEP {number:00} / RESULT", SemanticRole.Title);
        EvidencePath($"FROM {path}:{step.Source.LineNumber}");
        Discovery(report, embedded: true);
        Line("");
    }

    public void UpdateSourceResult(PlannedOperation step, string path, int number)
    {
        Line($"STEP {number:00} / RESULT", SemanticRole.Title);
        EvidencePath($"FROM {path}:{step.RequestLine}");
        UpdateHeading(embedded: true);
    }

    public void LocationSourceResult(PlannedOperation step, string path, int number)
    {
        Line($"STEP {number:00} / RESULT", SemanticRole.Title);
        EvidencePath($"FROM {path}:{step.RequestLine}");
    }

    public void OperationsCompleted(int completed, int attention, bool updateRehearsed = false, bool doctorInspected = false)
    {
        Line("DERT / COMPLETE", SemanticRole.Title);
        Line($"{Count(completed, "operation")} completed · {attention} {(attention == 1 ? "result needs" : "results need")} attention",
            attention > 0 ? SemanticRole.Hook : SemanticRole.Line);
        Line(doctorInspected ? "Your DERT requested doctor checks and a repair proposal. No uninstall or reinstall ran; npm may write cache/logs. Source stays unchanged."
            : updateRehearsed
            ? "Your DERT requested an update rehearsal. No package update was executed; npm checks may write cache/logs. Your source was not changed."
            : "Your DERT drove the discovery. No discovered program was launched; no installations or source files changed.", SemanticRole.ContextNote);
        Line("ƒin.", SemanticRole.Boundary);
    }

    private void OperationSource(DertDocument document, DertNode node)
    {
        foreach (var sourceLine in document.Source.Lines.Where(line => line.Number >= node.LineNumber && line.Start < node.Span.End))
            SourceLine(document, sourceLine, sourceLine.Number == node.LineNumber && node is ChainNode);
    }
}
