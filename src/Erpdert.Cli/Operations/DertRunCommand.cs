using Dert.Core;
using Erpdert.Cli.Discovery;

namespace Erpdert.Cli.Operations;

internal static class DertRunCommand
{
    internal static int Run(DertDocument document, string path, bool preview,
        DocumentRenderer renderer, DiscoveryEnvironment? environment)
    {
        var plan = DertOperationPlan.Build(document);
        renderer.OperationPlan(plan, path, preview);
        if (!plan.CanRun)
        {
            renderer.OperationsBlocked();
            return 1;
        }
        if (plan.Steps.Count == 0)
        {
            renderer.NoOperations();
            return 0;
        }
        if (preview)
        {
            renderer.OperationsPreviewed(path);
            return 0;
        }
        if (!OperatingSystem.IsWindows())
        {
            renderer.Failure("The DERT request was understood, but Codex discovery is currently implemented for Windows only. No operations ran.");
            return 2;
        }

        var completed = 0;
        var attention = 0;
        var capturedEnvironment = environment ?? DiscoveryEnvironment.Capture();
        foreach (var step in plan.Steps)
        {
            // Dispatch a typed operation, never reconstructed shell text.
            switch (step.Kind)
            {
                case OperationKind.CodexDiscovery:
                    var report = CodexDiscovery.Scan(capturedEnvironment);
                    renderer.OperationResult(step, path, ++completed, report);
                    if (report.NeedsAttention) attention++;
                    break;
                default:
                    throw new InvalidOperationException("The operation planner emitted an unhandled operation kind.");
            }
        }
        renderer.OperationsCompleted(completed, attention);
        return attention > 0 ? 1 : 0;
    }
}
