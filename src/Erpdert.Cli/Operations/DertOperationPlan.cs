using Dert.Core;

namespace Erpdert.Cli.Operations;

internal enum OperationKind { CodexDiscovery }

internal sealed record PlannedOperation(OperationKind Kind, ChainNode Source);

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
                    if (child is StatementNode)
                        Problem("ERP6004", child,
                            "This statement has no operation handler in this build. It was not ignored or substituted with discovery.");
                    else if (child is TextNode)
                        Problem("ERP6005", child,
                            "This chain contains text that the runner cannot interpret as an operation. If it is a note, prefix it with //; otherwise its operation needs an implementation.");
                }
            }
            else if (node is StatementNode)
                Problem("ERP6004", node,
                    "This statement has no standalone operation handler. The supported discovery form is STERT ; ERPDERT ;; codex.");
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
                    "This operation header is incomplete. To request Codex discovery, write STERT ; ERPDERT ;; codex.");
                return;
            }
            if (!header.Target!.Text.Equals("ERPDERT", StringComparison.OrdinalIgnoreCase))
            {
                Problem("ERP6002", chain,
                    $"This build has no operation handler for '{header.Target.Text}'. ERPDERT is the implemented receiver.");
                return;
            }
            if (string.IsNullOrWhiteSpace(header.Payload?.Text))
            {
                Problem("ERP6003", chain,
                    "Choose an operation after ;;. This build supports codex for read-only discovery.");
                return;
            }
            if (!header.Payload!.Text.Equals("codex", StringComparison.OrdinalIgnoreCase))
            {
                Problem("ERP6004", chain,
                    $"The operation '{header.Payload.Text}' is not implemented here. This build supports codex discovery; it does not forward source text to a shell.");
                return;
            }
            steps.Add(new(OperationKind.CodexDiscovery, chain));
        }

        void Problem(string code, DertNode node, string explanation) => findings.Add(new(
            code, DiagnosticSeverity.Error, explanation,
            document.Source.Lines[node.LineNumber - 1].ContentSpan, node.LineNumber));
    }
}
