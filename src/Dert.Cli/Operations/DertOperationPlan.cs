using Dert.Core;
using Dert.Cli.Applications;

namespace Dert.Cli.Operations;

internal sealed record PlannedOperation(ApplicationRequest Request, ChainNode Source, int RequestLine)
{
    public ApplicationAction Kind => Request.Action;
    public string Command => Request.Command;
}

// Binding belongs to the application, not the DERT grammar. An unsupported
// operation is a missing application capability, not a forbidden language form.
internal sealed record DertOperationPlan(DertDocument Document,
    IReadOnlyList<PlannedOperation> Steps, IReadOnlyList<Diagnostic> Findings)
{
    public bool CanRun => !Findings.Any(finding => finding.Severity == DiagnosticSeverity.Error);

    public static DertOperationPlan Build(DertDocument document)
    {
        var steps = new List<PlannedOperation>();
        var findings = new List<Diagnostic>(document.Diagnostics);
        foreach (var node in document.Nodes)
        {
            if (node is ChainNode chain)
            {
                BindHeader(chain);
                foreach (var child in chain.Children)
                {
                    // Notes retain their contextual role. Their headers, entries,
                    // and annotations are never traversed as command requests.
                    if (child is StatementNode statement && steps.LastOrDefault() is { } step && step.Source == chain &&
                        Action(statement, step.Kind) is { } action)
                        steps[^1] = step with { Request = step.Request with { Action = action }, RequestLine = statement.LineNumber };
                    else if (child is StatementNode)
                        Problem("ERP6004", child,
                            "This statement has no operation handler in this build. It was not ignored or substituted with discovery.");
                    else if (child is TextNode)
                        Problem("ERP6005", child,
                            "This chain contains text that the runner cannot interpret as an operation. If it is a note, prefix it with //; otherwise its operation needs an implementation.");
                }
            }
            else if (node is StatementNode)
                Problem("ERP6004", node,
                    "This statement has no standalone operation handler. The supported discovery form is STERT ; update ;; codex.");
        }
        return new(document, steps.ToArray(), findings.OrderBy(f => f.LineNumber).ThenBy(f => f.Span.Start).ToArray());

        void BindHeader(ChainNode chain)
        {
            var header = chain.Header;
            // A bare STERT can hold notes without requesting an operation.
            if (header.Hook is null && header.Line is null) return;
            if (header.Hook is null || header.Line is null || string.IsNullOrWhiteSpace(header.Target?.Text))
            {
                Problem("ERP6001", chain,
                    "This operation header is incomplete. To request Codex discovery, write STERT ; update ;; codex.");
                return;
            }
            var receiver = header.Target!.Text;
            var where = receiver.Equals("where", StringComparison.OrdinalIgnoreCase);
            if (!where && !receiver.Equals("update", StringComparison.OrdinalIgnoreCase) && !receiver.Equals("ERPDERT", StringComparison.OrdinalIgnoreCase))
            {
                Problem("ERP6002", chain,
                    $"This build has no operation handler for '{receiver}'. Use update or where as the command after the hook.");
                return;
            }
            if (string.IsNullOrWhiteSpace(header.Payload?.Text))
            {
                Problem("ERP6003", chain,
                    "Choose an application after ;;, such as codex. The command before ;; selects update or where.");
                return;
            }
            if (!ApplicationCatalog.TryResolve(header.Payload!.Text, out var application, out var reason) ||
                !where && application.NpmPackage is null)
            {
                Problem("ERP6004", chain,
                    $"The operation '{header.Payload.Text}' is not implemented here. " + (reason.Length > 0 ? reason : "This application has no update adapter. DERT where can still locate it."));
                return;
            }
            steps.Add(new(new(application, where ? ApplicationAction.Where : ApplicationAction.UpdateDiscovery), chain, chain.LineNumber));
        }

        static ApplicationAction? Action(StatementNode statement, ApplicationAction current)
        {
            if (!statement.Header.Head.Text.Equals("commands", StringComparison.OrdinalIgnoreCase) ||
                statement.Header.Line is null || !string.IsNullOrWhiteSpace(statement.Header.Payload?.Text) || statement.Values.Count != 0) return null;
            var name = statement.Header.Target?.Text;
            if (current == ApplicationAction.UpdateDiscovery && name?.Equals("updateme", StringComparison.OrdinalIgnoreCase) == true) return ApplicationAction.UpdateRehearsal;
            if (current == ApplicationAction.Where && name?.Equals("doctor", StringComparison.OrdinalIgnoreCase) == true) return ApplicationAction.Doctor;
            return null;
        }

        void Problem(string code, DertNode node, string explanation) => findings.Add(new(
            code, DiagnosticSeverity.Error, explanation,
            document.Source.Lines[node.LineNumber - 1].ContentSpan, node.LineNumber));
    }
}
