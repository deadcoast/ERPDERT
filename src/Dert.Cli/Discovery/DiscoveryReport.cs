using Dert.Cli.Applications;

namespace Dert.Cli.Discovery;

public sealed record DiscoveryEnvironment(string SearchPath, string PathExtensions)
{
    public IApplicationLocationEvidenceSource RegistryEvidence { get; init; } =
        EmptyApplicationLocationEvidenceSource.Instance;
    public IApplicationLocationEvidenceSource FolderEvidence { get; init; } =
        EmptyApplicationLocationEvidenceSource.Instance;
    public ApplicationMeasurementLimits MeasurementLimits { get; init; } =
        ApplicationMeasurementLimits.Default;

    public static DiscoveryEnvironment Capture() => new(
        Environment.GetEnvironmentVariable("PATH") ?? "",
        Environment.GetEnvironmentVariable("PATHEXT") ?? "")
    {
        RegistryEvidence = WindowsRegistryApplicationEvidenceSource.Instance,
        FolderEvidence = KnownApplicationFolderEvidenceSource.Instance
    };
}

public sealed record DiscoveryIssue(string Code, string Path, string Explanation);

public sealed record CodexPackage(string Root, string MetadataPath, string Version,
    string? NodePath, string? NpmPath);

public sealed record CommandMatch(string Path, int PathPosition, string Identity,
    CodexPackage? Package, IReadOnlyList<DiscoveryIssue> Issues);

public sealed record DiscoveryReport(IReadOnlyList<CommandMatch> Commands,
    IReadOnlyList<DiscoveryIssue> Issues, int DirectoryCount)
{
    public IReadOnlyList<IReadOnlyList<CommandMatch>> Installations => Commands
        .GroupBy(command => command.Package?.Root ?? command.Identity, StringComparer.OrdinalIgnoreCase)
        .Select(group => (IReadOnlyList<CommandMatch>)group.ToArray()).ToArray();

    public bool NeedsAttention => Commands.Count == 0 || Issues.Count > 0 ||
        Installations.Count > 1 || Commands.Any(command => command.Issues.Count > 0);
}

public enum ApplicationLocationKind
{
    Command,
    PackageRoot,
    RegistryInstallLocation,
    RegistryAppPath,
    AdjacentFolder
}

public sealed record ApplicationLocationCandidate(
    string Path,
    ApplicationLocationKind Kind,
    string Provenance);

public sealed record ApplicationLocationEvidenceResult(
    IReadOnlyList<ApplicationLocationCandidate> Locations,
    IReadOnlyList<DiscoveryIssue> Issues)
{
    public static ApplicationLocationEvidenceResult Empty { get; } =
        new(Array.Empty<ApplicationLocationCandidate>(), Array.Empty<DiscoveryIssue>());
}

public interface IApplicationLocationEvidenceSource
{
    ApplicationLocationEvidenceResult Read(ApplicationDefinition application,
        CancellationToken cancellation);
}

public sealed record ApplicationMeasurementLimits(int MaxEntries, int MaxDepth, TimeSpan MaxDuration)
{
    public static ApplicationMeasurementLimits Default { get; } =
        new(50_000, 64, TimeSpan.FromSeconds(2));
}

internal sealed class EmptyApplicationLocationEvidenceSource : IApplicationLocationEvidenceSource
{
    internal static EmptyApplicationLocationEvidenceSource Instance { get; } = new();
    public ApplicationLocationEvidenceResult Read(ApplicationDefinition application,
        CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        return ApplicationLocationEvidenceResult.Empty;
    }
}
