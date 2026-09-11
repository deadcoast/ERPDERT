using Dert.Cli.Discovery;
using Dert.Cli.Operations;
using Dert.Cli.Transactions;
using Dert.Cli.Updates;

namespace Dert.Cli.Doctoring;

// Compatibility surface; evidence, approval persistence and execution are shared.
internal static class DoctorService
{
    internal static DoctorReport Inspect(DiscoveryReport discovery, string? selectedPath,
        ICommandRunner? runner = null, Action<string>? progress = null,
        CancellationToken cancellation = default, DoctorSource? source = null,
        DiscoveryEnvironment? environment = null, Func<DiscoveryEnvironment>? environmentReader = null)
    {
        var diagnosis = environment is null ? DoctorDiagnosis.NotInspected : DoctorConflictDiagnosis.Inspect(discovery, environment);
        var guard = diagnosis.Guard is { } found && SelectsLeadingCopy(discovery, selectedPath, found) ? found : null;
        var report = FromShared(NpmTransaction.Inspect(NpmOperation.Doctor, discovery, selectedPath, runner, progress,
            cancellation: cancellation, source: Source(source), routeGuard: guard, environmentReader: environmentReader));
        if (diagnosis.Guard is { } captured)
        {
            try
            {
                NpmRouteGuardChecks.Verify(captured, environmentReader);
                NpmRouteGuardChecks.VerifySnapshot(captured);
            }
            catch (Exception error) when (DiscoveryFiles.IsReadError(error))
            {
                diagnosis = diagnosis with { State = DoctorDiagnosisState.Incomplete, Guard = null,
                    Summary = "The external-route diagnosis changed during inspection; inspect again before treating it as confirmed.",
                    Reasons = [error.Message] };
            }
        }
        report.Diagnosis = diagnosis;
        return report;
    }
    private static bool SelectsLeadingCopy(DiscoveryReport discovery, string? selectedPath, NpmRouteGuard guard)
    {
        if (selectedPath is null) return false;
        try
        {
            if (!Path.IsPathFullyQualified(selectedPath)) return false;
            var normalized = Path.GetFullPath(selectedPath);
            var selected = discovery.Commands.FirstOrDefault(command => Same(command.Path, normalized) || Same(command.Identity, normalized));
            return selected is not null && Same(DiscoveryFiles.ResolveLocal(Path.GetDirectoryName(selected.Path)!), Path.GetDirectoryName(guard.LeadingPath)!);
        }
        catch (Exception error) when (DiscoveryFiles.IsReadError(error)) { return false; }
        static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    internal static DoctorApplyResult Apply(DoctorReport report, string token,
        ICommandRunner? runner = null, string? receiptDirectory = null, CancellationToken cancellation = default,
        Func<DiscoveryEnvironment>? environmentReader = null)
    {
        var result = NpmTransaction.Apply(NpmOperation.Doctor, ToShared(report), token, runner, receiptDirectory, cancellation, environmentReader);
        var state = result.State switch
        {
            NpmApplyState.Refused => DoctorApplyState.Refused,
            NpmApplyState.Succeeded => DoctorApplyState.Succeeded,
            NpmApplyState.UninstallFailed => DoctorApplyState.UninstallFailed,
            NpmApplyState.InstallFailed => DoctorApplyState.InstallFailed,
            NpmApplyState.VerificationFailed => DoctorApplyState.VerificationFailed,
            NpmApplyState.Interrupted => DoctorApplyState.Interrupted,
            NpmApplyState.ReceiptFailed => DoctorApplyState.ReceiptFailed,
            _ => throw new InvalidOperationException("The npm transaction returned an unknown outcome.")
        };
        return new(state, result.Message, result.ReceiptPath, result.Recovery,
            Array.AsReadOnly(result.Phases.Select(phase => new DoctorPhase(phase.Name, phase.Succeeded, phase.Detail)).ToArray()));
    }
    internal static string SaveProposal(DoctorReport report, string? proposalDirectory = null, Func<DiscoveryEnvironment>? environmentReader = null) =>
        NpmTransaction.SaveProposal(NpmOperation.Doctor, ToShared(report), proposalDirectory, environmentReader);
    internal static DoctorReport LoadProposal(string token, string? proposalDirectory = null, Func<DiscoveryEnvironment>? environmentReader = null) =>
        FromShared(NpmTransaction.LoadProposal(NpmOperation.Doctor, token, proposalDirectory, environmentReader));

    private static NpmSource? Source(DoctorSource? source) => source is null ? null : new(source.Path, source.Identity, source.Sha256);
    private static UpdateReport ToShared(DoctorReport report)
    {
        var proposal = report.Proposal;
        return new(report.Discovery)
        {
            State = report.State switch { DoctorState.Ready => UpdateState.Ready, DoctorState.Interrupted => UpdateState.Interrupted,
                DoctorState.ChooseCopy => UpdateState.ChooseCopy, _ => UpdateState.Unavailable },
            Proposal = proposal is null ? null : new(NpmOperation.Doctor, proposal.SelectedPath, proposal.Prefix, proposal.Version,
                proposal.InstalledVersion, proposal.Uninstall, proposal.Install,
                proposal.Files.Select(file => new NpmFile(file.Path, file.Identity, file.Sha256)).ToArray(), Source(proposal.Source)) { Token = proposal.Token, RouteGuard = proposal.RouteGuard }
        };
    }
    private static DoctorReport FromShared(UpdateReport update)
    {
        var proposal = update.Proposal;
        var report = new DoctorReport(update.Discovery)
        {
            Update = update,
            Diagnosis = proposal?.RouteGuard is { } guard ? DoctorConflictDiagnosis.FromGuard(guard) : DoctorDiagnosis.NotInspected,
            State = update.State switch { UpdateState.Ready => DoctorState.Ready, UpdateState.Interrupted => DoctorState.Interrupted,
                UpdateState.ChooseCopy => DoctorState.ChooseCopy, _ => DoctorState.Unavailable },
            Proposal = proposal is null ? null : new(proposal.SelectedPath, proposal.Prefix, proposal.Version, proposal.Uninstall!, proposal.Install,
                Array.AsReadOnly(proposal.Files.Select(file => new DoctorFile(file.Path, file.Identity, file.Sha256)).ToArray()),
                proposal.Source is null ? null : new(proposal.Source.Path, proposal.Source.Identity, proposal.Source.Sha256))
                { Token = proposal.Token, InstalledVersion = proposal.InstalledVersion, RouteGuard = proposal.RouteGuard }
        };
        report.Issues.AddRange(update.Issues);
        return report;
    }
}
