using Dert.Cli.Discovery;
using Dert.Cli.Operations;
using Dert.Cli.Updates;
using Dert.Cli.Transactions;

namespace Dert.Cli.Doctoring;

internal enum DoctorState { Unavailable, ChooseCopy, Ready, Interrupted }
internal enum DoctorApplyState { Refused, Succeeded, UninstallFailed, InstallFailed, VerificationFailed, Interrupted, ReceiptFailed }
internal sealed record DoctorSource(string Path, string Identity, string Sha256)
{
    public static DoctorSource Capture(string path)
    {
        var snapshot = DoctorFile.Capture(path);
        if (snapshot.Sha256 is null) throw new IOException("The source document is missing: " + path);
        return new(snapshot.Path, snapshot.Identity, snapshot.Sha256!);
    }
}
internal sealed record DoctorFile(string Path, string Identity, string? Sha256)
{
    internal static DoctorFile Capture(string path)
    {
        var file = NpmFile.Capture(path);
        return new(file.Path, file.Identity, file.Sha256);
    }
}
internal sealed record DoctorProposal(string SelectedPath, string Prefix, string Version,
    CommandRequest Uninstall, CommandRequest Install, IReadOnlyList<DoctorFile> Files, DoctorSource? Source)
{
    public string Token { get; init; } = "";
    public string InstalledVersion { get; init; } = "";
    public NpmRouteGuard? RouteGuard { get; init; }
}
internal sealed class DoctorReport(DiscoveryReport discovery)
{
    public DiscoveryReport Discovery { get; } = discovery;
    public DoctorState State { get; set; }
    public UpdateReport? Update { get; set; }
    public List<DiscoveryIssue> Issues { get; } = [];
    public DoctorProposal? Proposal { get; set; }
    public DoctorDiagnosis Diagnosis { get; set; } = DoctorDiagnosis.NotInspected;
}
internal sealed record DoctorPhase(string Name, bool Succeeded, string Detail);
internal sealed record DoctorApplyResult(DoctorApplyState State, string Message, string? ReceiptPath,
    CommandRequest? Recovery, IReadOnlyList<DoctorPhase> Phases);
