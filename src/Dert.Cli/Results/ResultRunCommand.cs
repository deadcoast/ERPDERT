using System.Text;
using Dert.Cli.Applications;
using Dert.Cli.Discovery;
using Dert.Cli.Operations;
using Dert.Core;

namespace Dert.Cli.Results;

internal static class ResultRunCommand
{
    internal static int Run(CliOptions options, DocumentRenderer renderer, DiscoveryEnvironment? environment)
    {
        try
        {
            var path = Path.GetFullPath(options.Path);
            var output = ObservationStore.Destination(options.Receipt!, path);
            using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var bytes = new MemoryStream(); source.CopyTo(bytes);
            var document = DertParser.Parse(new UTF8Encoding(false, true).GetString(bytes.ToArray()));
            var plan = DertOperationPlan.Build(document);
            if (!plan.CanRun || plan.Steps.Count != 1 || plan.Steps[0].Kind != ApplicationAction.UpdateDiscovery)
                throw new ArgumentException("Retaining a result currently needs one eligible STERT ; update ;; codex discovery request. Other operations keep their existing journeys; no operation ran.");
            if (!OperatingSystem.IsWindows()) throw new ArgumentException("Local Codex discovery currently requires Windows. No operation ran.");
            renderer.OperationPlan(plan, path, false);
            var report = CodexDiscovery.Scan(environment ?? DiscoveryEnvironment.Capture());
            var observation = DiscoveryObservation.Capture(document, path, plan.Steps[0].Source, report, DateTimeOffset.Now);
            renderer.OperationResult(plan.Steps[0], path, 1, report);
            var saved = ObservationStore.WriteNew(observation, output);
            renderer.ResultRetained(observation, saved);
            return report.NeedsAttention ? 1 : 0;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        { renderer.Failure(error.Message); return 2; }
    }
}
