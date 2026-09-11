namespace Dert.Cli.Transactions;

internal enum NpmRouteGuardKind { MissingNpmEntrypointShadowsLaterRoute }
internal sealed record NpmDirectory(string Path, string Identity, bool Exists);
internal sealed record NpmRouteGuard(NpmRouteGuardKind Kind, string SearchPath, string PathExtensions,
    string LeadingPath, string LeadingIdentity, string LeadingSha256, string MissingEntrypoint, string LaterPath, string LaterIdentity,
    IReadOnlyList<NpmFile> ProtectedFiles, IReadOnlyList<NpmDirectory> Directories)
{
    private IReadOnlyList<NpmFile> protectedFiles = Array.AsReadOnly(ProtectedFiles.ToArray());
    private IReadOnlyList<NpmDirectory> directories = Array.AsReadOnly(Directories.ToArray());
    public IReadOnlyList<NpmFile> ProtectedFiles { get => protectedFiles; init => protectedFiles = Array.AsReadOnly(value.ToArray()); }
    public IReadOnlyList<NpmDirectory> Directories { get => directories; init => directories = Array.AsReadOnly(value.ToArray()); }
}
