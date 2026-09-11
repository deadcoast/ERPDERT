using Dert.Cli.Discovery;
using Dert.Cli.Operations;
using Dert.Cli.Updates;
using Dert.Cli.Transactions;
using Dert.Cli.Motion;
using Dert.Mer;
using Spectre.Console;
using System.Security.Cryptography;

namespace Dert.Cli.Applications;

internal static class UpdateCommand
{
    internal static int Run(ApplicationRequest request, DocumentRenderer renderer,
        DiscoveryEnvironment? environment, ICommandRunner? runner, IAnsiConsole? console = null,
        bool plain = false, string? stateDirectory = null, bool still = false,
        string? sourcePath = null, byte[]? capturedSource = null, MotionSource? motionSource = null, bool embedded = false)
    {
        if (!OperatingSystem.IsWindows())
        {
            renderer.Failure("Application discovery and npm rehearsal are currently implemented for Windows only.");
            return 2;
        }
        using var cancelled = new CancellationTokenSource();
        ConsoleCancelEventHandler stop = (_, args) => { args.Cancel = true; cancelled.Cancel(); };
        Console.CancelKeyPress += stop;
        try
        {
            if (request.Approval is not null)
            {
                var saved = UpdateTransaction.LoadProposal(request.Approval, ProposalDirectory(stateDirectory));
                if (saved.Proposal is not { } proposal || request.SelectedPath is null ||
                    !SamePath(request.SelectedPath, proposal.SelectedPath) ||
                    !SameOptionalPath(request.SourcePath, proposal.Source?.Path))
                { renderer.Failure("This approval belongs to a different selected path or source. Use the exact command from its reviewed proposal."); return 1; }
                renderer.UpdateApproval(saved);
                using var activity = new NpmTransactionMotion(runner ?? new SystemCommandRunner(cancelled.Token),
                    console ?? AnsiConsole.Console, plain, still, applying: true, operation: "update");
                var result = UpdateTransaction.Apply(saved, request.Approval, activity,
                    stateDirectory is null ? null : Path.Combine(stateDirectory, "update-receipts"), cancelled.Token);
                activity.Finish(result.State == NpmApplyState.Succeeded, result.State == NpmApplyState.Interrupted,
                    result.State == NpmApplyState.Succeeded ? "Update verification recorded" : "Update stopped / inspect the recorded phases");
                renderer.UpdateResult(result);
                return result.State == NpmApplyState.Succeeded ? 0 : result.State == NpmApplyState.Interrupted ? 130 : 1;
            }
            var discovery = CodexDiscovery.Scan(environment ?? DiscoveryEnvironment.Capture(), cancelled.Token);
            if (request.Action == ApplicationAction.UpdateDiscovery)
            {
                renderer.Discovery(discovery);
                return discovery.NeedsAttention ? 1 : 0;
            }
            var source = request.SourcePath ?? sourcePath;
            var binding = source is null ? null : NpmSource.Capture(source);
            if (capturedSource is not null && binding is not null && binding.Sha256 != Convert.ToHexString(SHA256.HashData(capturedSource)))
                throw new IOException("The source changed after its operation was read. Reopen it before rehearsing this update.");
            if (!embedded) renderer.UpdateHeading();
            var rehearsal = renderer.RehearseUpdate(discovery, request.SelectedPath, runner, motionSource, binding, stateDirectory, cancelled.Token);
            renderer.UpdateRehearsal(rehearsal, embedded, includeChecks: !renderer.UsesMerUpdateReceipt, sourcePath: binding?.Path);
            return rehearsal.State == UpdateState.Interrupted ? 130 : rehearsal.NeedsAttention ? 1 : 0;
        }
        catch (OperationCanceledException) { renderer.Failure("Update stopped at your request. No completed result is claimed."); return 130; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        { renderer.Failure("The update request could not finish. " + error.Message); return request.Approval is null ? 2 : 1; }
        finally { Console.CancelKeyPress -= stop; }
    }

    internal static string? ProposalDirectory(string? stateDirectory) => stateDirectory is null ? null : Path.Combine(stateDirectory, "update-proposals");
    private static bool SamePath(string first, string second) => Path.GetFullPath(first).Equals(Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);
    private static bool SameOptionalPath(string? first, string? second) => first is null || second is null ? first is null && second is null : SamePath(first, second);
}
