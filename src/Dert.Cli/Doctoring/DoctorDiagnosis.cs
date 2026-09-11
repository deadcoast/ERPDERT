using Dert.Cli.Discovery;
using Dert.Cli.Transactions;

namespace Dert.Cli.Doctoring;

internal enum DoctorDiagnosisState { None, Possible, Confirmed, Incomplete }
internal sealed record DoctorDiagnosis(DoctorDiagnosisState State, string Summary, string? LeadingPath,
    string? MissingEntrypoint, string? LaterPath, IReadOnlyList<string> Reasons)
{
    public string Scope => "External PATH file resolution only. Parent-shell aliases, functions, cmdlets, execution policy and Codex runtime health are not inspected.";
    internal NpmRouteGuard? Guard { get; init; }
    internal static DoctorDiagnosis NotInspected { get; } = new(DoctorDiagnosisState.None,
        "No external-route conflict diagnosis was requested.", null, null, null, []);
}

internal static class DoctorConflictDiagnosis
{
    internal static DoctorDiagnosis Inspect(DiscoveryReport discovery, DiscoveryEnvironment environment)
    {
        string? leadingPath = discovery.Commands.FirstOrDefault()?.Path;
        string? missing = null;
        string? laterPath = null;
        try
        {
            _ = NpmRouteGuardChecks.CaptureDirectories(environment);
            if (discovery.Issues.Count > 0)
                throw new IOException("The PATH search was incomplete. " + string.Join(" ", discovery.Issues.Select(issue => issue.Explanation)));
            if (discovery.Installations.Count < 2)
                return new(DoctorDiagnosisState.None, "No earlier-route/later-copy conflict was established.", leadingPath, null, null,
                    ["Damage to a single copy is separate from interference between command routes."]);
            var leading = discovery.Commands[0];
            NpmRouteGuardChecks.RequireKnownLauncher(leading);
            missing = NpmRouteGuardChecks.Entrypoint(leading);
            var entrypoint = NpmRouteGuardChecks.CaptureLocal(missing);
            var later = discovery.Commands.FirstOrDefault(command => command.PathPosition > leading.PathPosition &&
                command.Package is not null && !Same(command.Package.Root, leading.Package!.Root));
            if (later is null) throw new IOException("No later distinct identified package supplies evidence for this conflict check.");
            laterPath = later.Path;
            NpmRouteGuardChecks.RequireKnownLauncher(later);
            NpmRouteGuardChecks.RequirePresent(NpmRouteGuardChecks.Entrypoint(later));
            if (entrypoint.Sha256 is not null)
                return new(DoctorDiagnosisState.Possible, "Multiple copies were found; a broken leading entrypoint was not established.",
                    leadingPath, null, laterPath, ["A duplicate PATH route alone does not demonstrate a conflict."]);
            var guard = NpmRouteGuardChecks.Capture(environment, leading.Path, later.Path);
            return FromGuard(guard);
        }
        catch (Exception error) when (DiscoveryFiles.IsReadError(error))
        {
            return new(DoctorDiagnosisState.Incomplete, "The external-route evidence is incomplete; no conflict is confirmed.",
                leadingPath, missing, laterPath, [error.Message]);
        }
    }
    internal static DoctorDiagnosis FromGuard(NpmRouteGuard guard) => new(DoctorDiagnosisState.Confirmed,
        "An earlier generated npm PowerShell launcher targets a missing entrypoint and hides a later identified copy on external PATH.",
        guard.LeadingPath, guard.MissingEntrypoint, guard.LaterPath,
        ["The entire leading PowerShell launcher matches a supported npm template.",
         "Its declared JavaScript entrypoint is definitely missing; the later copy's launcher and entrypoint were read successfully.",
         "PATH order, earlier command absences and later-copy evidence are retained for guarded repair."])
        { Guard = guard };
    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
