using Dert.Cli.Operations;
using Dert.Cli.Discovery;
using System.Security.Cryptography;

namespace Dert.Cli.Transactions;

internal enum NpmOperation { Update, Doctor }
internal enum NpmApplyState { Refused, Succeeded, UninstallFailed, InstallFailed, VerificationFailed, Interrupted, ReceiptFailed }
internal sealed record NpmSource(string Path, string Identity, string Sha256)
{
    internal static NpmSource Capture(string path)
    {
        var captured = NpmFile.Capture(path);
        if (captured.Sha256 is null) throw new IOException("The source document is missing: " + path);
        return new(captured.Path, captured.Identity, captured.Sha256);
    }
}
internal sealed record NpmFile(string Path, string Identity, string? Sha256)
{
    internal static NpmFile Capture(string path)
    {
        path = System.IO.Path.GetFullPath(path);
        var identity = DiscoveryFiles.ResolveLocal(path);
        try
        {
            using var file = new FileStream(identity, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (file.Length > 256 * 1024 * 1024) throw new IOException("A transaction evidence file exceeds the 256 MiB inspection limit.");
            return new(path, identity, Convert.ToHexString(SHA256.HashData(file)));
        }
        catch (Exception failure) when (failure is FileNotFoundException or DirectoryNotFoundException)
        { return new(path, identity, null); }
    }
}
internal sealed record NpmProposal(NpmOperation Operation, string SelectedPath, string Prefix, string Version,
    string InstalledVersion, CommandRequest? Uninstall, CommandRequest Install, IReadOnlyList<NpmFile> Files, NpmSource? Source)
{
    public string Token { get; init; } = "";
    public NpmRouteGuard? RouteGuard { get; init; }
    private IReadOnlyList<NpmFile> files = Array.AsReadOnly(Files.ToArray());
    private CommandRequest install = Freeze(Install);
    private CommandRequest? uninstall = Uninstall is null ? null : Freeze(Uninstall);
    public IReadOnlyList<NpmFile> Files { get => files; init => files = Array.AsReadOnly(value.ToArray()); }
    public CommandRequest Install { get => install; init => install = Freeze(value); }
    public CommandRequest? Uninstall { get => uninstall; init => uninstall = value is null ? null : Freeze(value); }
    private static CommandRequest Freeze(CommandRequest request) => request with { Arguments = Array.AsReadOnly(request.Arguments.ToArray()) };
}
internal sealed record NpmPhase(string Name, bool Succeeded, string Detail);
internal sealed record NpmApplyResult(NpmApplyState State, string Message, string? ReceiptPath,
    CommandRequest? Recovery, IReadOnlyList<NpmPhase> Phases);
