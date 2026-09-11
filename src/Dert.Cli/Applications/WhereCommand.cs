using Dert.Cli.Discovery;
using Dert.Cli.Doctoring;
using Dert.Cli.Motion;
using Dert.Cli.Operations;
using Dert.Mer;
using Spectre.Console;

namespace Dert.Cli.Applications;

internal static class WhereCommand
{
    internal static int Run(ApplicationRequest request, DocumentRenderer renderer, IAnsiConsole console,
        DiscoveryEnvironment? environment, ICommandRunner? runner, bool plain, string? sourcePath = null, byte[]? capturedSource = null,
        string? stateDirectory = null, bool still = false)
    {
        if (!OperatingSystem.IsWindows())
        { renderer.Failure("Application location currently follows Windows paths and registration. This platform has no location adapter yet."); return 2; }
        using var cancelled = new CancellationTokenSource();
        ConsoleCancelEventHandler stop = (_, args) => { args.Cancel = true; cancelled.Cancel(); };
        Console.CancelKeyPress += stop;
        Func<DiscoveryEnvironment>? environmentReader = environment is null ? null : () => environment;
        try
        {
            if (request.Approval is not null)
            {
                if (!request.Application.CanDoctor) { renderer.Failure("This application has no doctor adapter."); return 1; }
                var saved = DoctorService.LoadProposal(request.Approval, stateDirectory is null ? null : Path.Combine(stateDirectory, "doctor-proposals"), environmentReader);
                if (saved.Proposal is not { } proposal || request.SelectedPath is null ||
                    !SamePath(request.SelectedPath, proposal.SelectedPath) ||
                    !SameOptionalPath(request.SourcePath, proposal.Source?.Path))
                { renderer.Failure("This approval belongs to a different selected path or source. Use the exact command from its reviewed proposal."); return 1; }
                renderer.Doctor(saved, request.Application.Id, request.SourcePath);
                using var activity = new NpmTransactionMotion(runner ?? new SystemCommandRunner(cancelled.Token), console, plain, still, applying: true);
                var applied = DoctorService.Apply(saved, request.Approval, activity,
                    stateDirectory is null ? null : Path.Combine(stateDirectory, "doctor-receipts"), cancelled.Token, environmentReader);
                activity.Finish(applied.State == DoctorApplyState.Succeeded, applied.State == DoctorApplyState.Interrupted,
                    applied.State == DoctorApplyState.Succeeded ? "Reinstall verification recorded" : "Repair stopped / inspect the recorded phases");
                renderer.DoctorResult(applied);
                return applied.State == DoctorApplyState.Succeeded ? 0 : applied.State == DoctorApplyState.Interrupted ? 130 : 1;
            }
            var scanEnvironment = environment ?? DiscoveryEnvironment.Capture();
            var locations = LocationMotion.Scan(request.Application, scanEnvironment, console, plain, still, cancelled.Token);
            if (request.Action != ApplicationAction.Doctor) return new LocationConnection(0, locations).NeedsAttention ? 1 : 0;
            if (!request.Application.CanDoctor)
            { renderer.Failure($"{request.Application.DisplayName} has location evidence but no doctor adapter in this build. No repair is offered."); return 1; }
            var source = request.SourcePath ?? sourcePath;
            DoctorSource? binding = source is null ? null : DoctorSource.Capture(source);
            if (capturedSource is not null && binding is not null &&
                binding.Sha256 != Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(capturedSource)))
                throw new IOException("The source changed after its operation was read. Reopen it before requesting doctor checks.");
            using var checks = new NpmTransactionMotion(runner ?? new SystemCommandRunner(cancelled.Token), console, plain, still, applying: false);
            var doctor = DoctorService.Inspect(locations.Discovery, request.SelectedPath, checks,
                cancellation: cancelled.Token, source: binding, environment: scanEnvironment, environmentReader: environmentReader);
            checks.Finish(doctor.State == DoctorState.Ready, doctor.State == DoctorState.Interrupted,
                doctor.State == DoctorState.Ready ? "Manager checks complete / proposal awaits your choice" :
                doctor.State == DoctorState.ChooseCopy ? "Route inspection retained / choose a copy before manager checks" :
                "Manager checks stopped / no repair approved");
            if (doctor.State == DoctorState.Ready)
            {
                var stored = DoctorService.SaveProposal(doctor, stateDirectory is null ? null : Path.Combine(stateDirectory, "doctor-proposals"), environmentReader);
                renderer.DoctorProposalStored(stored);
            }
            renderer.Doctor(doctor, request.Application.Id, source);
            return doctor.State == DoctorState.Ready ? 0 : doctor.State == DoctorState.Interrupted ? 130 : 1;
        }
        catch (OperationCanceledException) { renderer.Failure("Application inspection stopped at your request. No completed result is claimed."); return 130; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        { renderer.Failure("The application request could not finish. " + error.Message); return request.Approval is null ? 2 : 1; }
        finally { Console.CancelKeyPress -= stop; }
    }

    private static bool SamePath(string first, string second) =>
        Path.GetFullPath(first).Equals(Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);
    private static bool SameOptionalPath(string? first, string? second) =>
        first is null || second is null ? first is null && second is null : SamePath(first, second);
}
