using Dert.Cli.Discovery;
using Dert.Cli.Motion;
using Dert.Cli.Operations;
using Dert.Cli.Updates;
using Dert.Mer;

namespace Dert.Cli;

internal sealed partial class DocumentRenderer
{
    public bool UsesMerUpdateReceipt => !plain && console.Profile.Capabilities.Interactive;

    public UpdateReport RehearseUpdate(DiscoveryReport discovery, string? selected, ICommandRunner? runner, MotionSource? source = null)
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler stop = (_, args) => { args.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += stop;
        try { return Rehearse(cancellation.Token); }
        finally { Console.CancelKeyPress -= stop; }

        UpdateReport Rehearse(CancellationToken token)
        {
            runner ??= new SystemCommandRunner(token);
            if (!UsesMerUpdateReceipt)
            {
                var result = NpmUpdateRehearsal.Create(discovery, selected, runner, stage =>
                {
                    try { UpdateProgress(stage); }
                    catch (Exception error) when (error is IOException or ObjectDisposedException)
                    { throw new MerOutputException(error); }
                });
                if (token.IsCancellationRequested) result.State = UpdateState.Interrupted;
                return result;
            }
            source ??= new("Request", [new(new("STERT ", Ink.Copper, true), new("; ", Ink.Copper),
            new("DERT ", Ink.Signal), new(";; ", Ink.Signal), new("codex")),
            new(new("commands ", Ink.Text, true), new("; ", Ink.Copper), new("updateme ", Ink.Signal), new(";;", Ink.Signal))]);
            using var motion = new UpdateMotion(MerHost.Open(UpdateMotion.Score(source), console, plain, still));
            var report = NpmUpdateRehearsal.Create(discovery, selected, runner, activity: motion.Observe);
            if (token.IsCancellationRequested) report.State = UpdateState.Interrupted;
            motion.Finish(report);
            return report;
        }
    }
}
