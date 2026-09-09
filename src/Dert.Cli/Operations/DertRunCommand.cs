using Dert.Cli.Applications;
using Dert.Core;
using Dert.Cli.Discovery;
using Dert.Cli.Updates;
using Dert.Mer;
using Spectre.Console;
using System.Text;

namespace Dert.Cli.Operations;

internal static class DertRunCommand
{
    internal static int Run(DertDocument document, string path, bool preview,
        DocumentRenderer renderer, DiscoveryEnvironment? environment, ICommandRunner? commandRunner = null,
        IAnsiConsole? console = null, bool plain = false, string? doctorStateDirectory = null, bool still = false)
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
                case ApplicationAction.UpdateDiscovery:
                    var report = CodexDiscovery.Scan(capturedEnvironment);
                    renderer.OperationResult(step, path, ++completed, report);
                    if (report.NeedsAttention) attention++;
                    break;
                case ApplicationAction.UpdateRehearsal:
                    renderer.UpdateSourceResult(step, path, ++completed);
                    var requestLines = document.Source.Lines.Where(line => line.Number >= step.Source.LineNumber && line.Number <= step.RequestLine)
                        .Select(line => InkLine.Plain($"{line.Number,3}  " + document.Source.Slice(line.ContentSpan))).ToArray();
                    var rehearsal = renderer.RehearseUpdate(CodexDiscovery.Scan(capturedEnvironment), null, commandRunner, new(path, requestLines));
                    renderer.UpdateRehearsal(rehearsal, embedded: true, includeChecks: !renderer.UsesMerUpdateReceipt);
                    if (rehearsal.State == UpdateState.Interrupted) return 130;
                    if (rehearsal.NeedsAttention) attention++;
                    break;
                case ApplicationAction.Where:
                case ApplicationAction.Doctor:
                    renderer.LocationSourceResult(step, path, ++completed);
                    var exit = WhereCommand.Run(step.Request, renderer, console ?? AnsiConsole.Console, capturedEnvironment,
                        commandRunner, plain, step.Kind == ApplicationAction.Doctor ? path : null,
                        new UTF8Encoding(false, true).GetBytes(document.Source.Text), doctorStateDirectory, still);
                    if (exit == 130) return 130;
                    if (exit != 0) attention++;
                    break;
                default:
                    throw new InvalidOperationException("The operation planner emitted an unhandled operation kind.");
            }
        }
        renderer.OperationsCompleted(completed, attention, plan.Steps.Any(step => step.Kind == ApplicationAction.UpdateRehearsal),
            plan.Steps.Any(step => step.Kind == ApplicationAction.Doctor));
        return attention > 0 ? 1 : 0;
    }
}
