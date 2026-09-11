using Dert.Cli.Discovery;
using Dert.Cli.Operations;
using Dert.Cli.Transactions;

namespace Dert.Cli.Updates;

internal static class UpdateTransaction
{
    internal static UpdateReport Inspect(DiscoveryReport discovery, string? selectedPath,
        ICommandRunner? runner = null, Action<string>? progress = null, Action<UpdateActivity>? activity = null,
        CancellationToken cancellation = default, NpmSource? source = null) =>
        NpmTransaction.Inspect(NpmOperation.Update, discovery, selectedPath, runner, progress, activity, cancellation, source);
    internal static string SaveProposal(UpdateReport report, string? proposalDirectory = null) => NpmTransaction.SaveProposal(NpmOperation.Update, report, proposalDirectory);
    internal static UpdateReport LoadProposal(string token, string? proposalDirectory = null) => NpmTransaction.LoadProposal(NpmOperation.Update, token, proposalDirectory);
    internal static NpmApplyResult Apply(UpdateReport report, string token, ICommandRunner? runner = null,
        string? receiptDirectory = null, CancellationToken cancellation = default) =>
        NpmTransaction.Apply(NpmOperation.Update, report, token, runner, receiptDirectory, cancellation);
}
