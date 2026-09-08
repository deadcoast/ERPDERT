namespace Erpdert.Cli.Discovery;

public sealed record DiscoveryEnvironment(string SearchPath, string PathExtensions)
{
    public static DiscoveryEnvironment Capture() => new(
        Environment.GetEnvironmentVariable("PATH") ?? "",
        Environment.GetEnvironmentVariable("PATHEXT") ?? "");
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
