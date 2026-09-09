using Dert.Cli.Discovery;
using Dert.Cli.Operations;
using Dert.Cli.Updates;

namespace Dert.Cli.Applications;

internal static class UpdateCommand
{
    internal static int Run(ApplicationRequest request, DocumentRenderer renderer,
        DiscoveryEnvironment? environment, ICommandRunner? runner)
    {
        if (!OperatingSystem.IsWindows())
        {
            renderer.Failure("Application discovery and npm rehearsal are currently implemented for Windows only.");
            return 2;
        }
        var discovery = CodexDiscovery.Scan(environment ?? DiscoveryEnvironment.Capture());
        if (request.Action == ApplicationAction.UpdateDiscovery)
        {
            renderer.Discovery(discovery);
            return discovery.NeedsAttention ? 1 : 0;
        }
        renderer.UpdateHeading();
        var rehearsal = renderer.RehearseUpdate(discovery, request.SelectedPath, runner);
        renderer.UpdateRehearsal(rehearsal, includeChecks: !renderer.UsesMerUpdateReceipt);
        return rehearsal.State == UpdateState.Interrupted ? 130 : rehearsal.NeedsAttention ? 1 : 0;
    }
}
