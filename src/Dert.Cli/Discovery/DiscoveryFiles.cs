using System.Text;

namespace Dert.Cli.Discovery;

internal static class DiscoveryFiles
{
    internal static bool IsReadError(Exception error) =>
        error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;

    internal static bool IsFile(string path)
    {
        try { return (File.GetAttributes(path) & FileAttributes.Directory) == 0; }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException) { return false; }
    }

    internal static string Read(string path)
    {
        const int limit = 256 * 1024;
        using var stream = new FileStream(ResolveLocal(path), FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        // Bound both the allocation and the read, even when a file grows mid-scan.
        if (stream.Length > limit) throw new IOException("The metadata or launcher is larger than the 256 KiB inspection limit.");
        var bytes = new byte[limit + 1];
        var count = stream.ReadAtLeast(bytes, bytes.Length, throwOnEndOfStream: false);
        if (count > limit) throw new IOException("The metadata or launcher grew beyond the 256 KiB inspection limit.");
        return new UTF8Encoding(false, true).GetString(bytes, 0, count).TrimStart('\uFEFF');
    }

    internal static string ResolveLocal(string path, int remainingLinks = 32)
    {
        if (remainingLinks <= 0) throw new IOException("The path contains too many links or a link loop.");
        path = Path.GetFullPath(path);
        // Do not trigger UNC authentication or open device paths while discovering tools.
        if (path.StartsWith(@"\\", StringComparison.Ordinal))
            throw new IOException("Network and device paths are outside this local search.");
        var root = Path.GetPathRoot(path)!;
        if (OperatingSystem.IsWindows() && new DriveInfo(root).DriveType == DriveType.Network)
            throw new IOException("Mapped network drives are outside this local search.");
        var current = root;
        var parts = path[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < parts.Length; index++)
        {
            current = Path.Combine(current, parts[index]);
            FileAttributes attributes;
            try { attributes = File.GetAttributes(current); }
            catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
            {
                return Path.Combine(new[] { current }.Concat(parts[(index + 1)..]).ToArray());
            }
            if ((attributes & FileAttributes.ReparsePoint) == 0) continue;
            FileSystemInfo info = (attributes & FileAttributes.Directory) != 0
                ? new DirectoryInfo(current) : new FileInfo(current);
            var target = info.ResolveLinkTarget(returnFinalTarget: false);
            if (target is null) throw new IOException("This redirected path has no inspectable link target.");
            return ResolveLocal(Path.Combine(new[] { target.FullName }.Concat(parts[(index + 1)..]).ToArray()), remainingLinks - 1);
        }
        return current;
    }
}
