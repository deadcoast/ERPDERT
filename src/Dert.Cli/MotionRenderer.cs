using Dert.Cli.Discovery;
using Dert.Cli.Motion;
using Dert.Cli.Operations;
using Dert.Cli.Updates;
using Dert.Cli.Transactions;
using Dert.Cli.Applications;
using Dert.Mer;

namespace Dert.Cli;

internal sealed partial class DocumentRenderer
{
    public bool UsesMerUpdateReceipt => !plain && console.Profile.Capabilities.Interactive;

    public UpdateReport RehearseUpdate(DiscoveryReport discovery, string? selected, ICommandRunner? runner, MotionSource? source = null,
        NpmSource? binding = null, string? stateDirectory = null, CancellationToken cancellationToken = default)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ConsoleCancelEventHandler stop = (_, args) => { args.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += stop;
        try { return Rehearse(cancellation.Token); }
        finally { Console.CancelKeyPress -= stop; }

        UpdateReport Rehearse(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            runner ??= new SystemCommandRunner(token);
            if (!UsesMerUpdateReceipt)
            {
                var result = UpdateTransaction.Inspect(discovery, selected, runner, stage =>
                {
                    try { UpdateProgress(stage); }
                    catch (Exception error) when (error is IOException or ObjectDisposedException)
                    { throw new MerOutputException(error); }
                }, cancellation: token, source: binding);
                if (token.IsCancellationRequested) result.State = UpdateState.Interrupted;
                Store(result);
                return result;
            }
            source ??= new("Request", [new(new("STERT ", Ink.Copper, true), new("; ", Ink.Copper),
            new("update ", Ink.Signal), new(";; ", Ink.Signal), new("codex")),
            new(new("commands ", Ink.Text, true), new("; ", Ink.Copper), new("updateme ", Ink.Signal), new(";;", Ink.Signal))]);
            using var motion = new UpdateMotion(MerHost.Open(UpdateMotion.Score(source), console, plain, still));
            var report = UpdateTransaction.Inspect(discovery, selected, runner, activity: motion.Observe, cancellation: token, source: binding);
            if (token.IsCancellationRequested) report.State = UpdateState.Interrupted;
            Store(report);
            motion.Finish(report);
            return report;
        }

        void Store(UpdateReport report)
        {
            if (report.State != UpdateState.Ready) return;
            try { UpdateTransaction.SaveProposal(report, UpdateCommand.ProposalDirectory(stateDirectory)); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
            {
                report.State = UpdateState.Unavailable; report.Proposal = null;
                report.Issues.Add(new("DERT5402", selected ?? "codex", "The reviewed update proposal could not be saved. " + error.Message));
            }
        }
    }
}
