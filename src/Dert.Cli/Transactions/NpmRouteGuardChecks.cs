using Dert.Cli.Discovery;
using System.Text.Json;

namespace Dert.Cli.Transactions;

// A persisted condition, not an optional callback policy. The reader supplies
// current external input; every guarded transaction enforces the same checks.
internal static class NpmRouteGuardChecks
{
    internal const string VerifiedPhase = "external-route-condition-verified";
    internal static NpmRouteGuard Capture(DiscoveryEnvironment environment, string leadingPath, string laterPath)
    {
        var directories = CaptureDirectories(environment);
        var scan = CodexDiscovery.Scan(environment);
        if (scan.Issues.Count > 0 || scan.Commands.Count == 0 || !Same(scan.Commands[0].Path, leadingPath))
            throw new IOException("The complete external PATH no longer identifies the reviewed leading route.");
        var leading = scan.Commands[0];
        var later = scan.Commands.FirstOrDefault(item => Same(item.Path, laterPath));
        if (later is null || later.PathPosition <= leading.PathPosition || leading.Package is null || later.Package is null ||
            Same(leading.Package.Root, later.Package.Root))
            throw new IOException("The later route does not identify a distinct copy after the leading route.");
        var leadingFile = RequireKnownLauncher(leading);
        var laterFile = RequireKnownLauncher(later);
        var entrypoint = Entrypoint(leading);
        if (CaptureLocal(entrypoint).Sha256 is not null)
            throw new IOException("The reviewed leading entrypoint is no longer definitely missing.");
        RequirePresent(Entrypoint(later));
        var prefix = Path.GetDirectoryName(leading.Path)!;
        if (directories.Any(directory => IsWithin(directory.Path, Path.Combine(prefix, "node_modules"))))
            throw new IOException("A searched PATH folder is inside the package tree that reinstall would replace.");

        var paths = new List<string>();
        foreach (var directory in directories)
        {
            if (Same(directory.Path, prefix)) break;
            foreach (var extension in Extensions(environment))
            {
                var path = Path.Combine(directory.Path, "codex" + extension);
                if (CaptureLocal(path).Sha256 is not null)
                    throw new IOException("An earlier command route appeared while capturing the diagnosis: " + path);
                paths.Add(path);
            }
        }
        var package = later.Package;
        if (package.NodePath is null || package.NpmPath is null)
            throw new IOException("The later copy has no fully identified Node and nearby npm evidence.");
        var laterPrefix = Path.GetDirectoryName(later.Path)!;
        var required = new[] { later.Path, package.MetadataPath, Entrypoint(later), package.NodePath, package.NpmPath,
            Path.Combine(laterPrefix, "node_modules", "npm", "package.json"),
            Path.Combine(laterPrefix, "node_modules", "npm", "bin", "npm-cli.js") };
        foreach (var path in required) { RequirePresent(path); paths.Add(path); }
        paths.AddRange(new[] { "codex", "codex.cmd", "codex.ps1" }.Select(name => Path.Combine(laterPrefix, name)));
        if (paths.Any(path => IsWithin(path, prefix)))
            throw new IOException("The later copy depends on files inside the selected prefix; it cannot be protected independently.");
        var files = paths.Distinct(StringComparer.OrdinalIgnoreCase).Select(CaptureLocal).ToArray();
        // Check the exact bytes used for recognition against the retained hashes.
        VerifyFiles([leadingFile, laterFile]);
        var guard = new NpmRouteGuard(NpmRouteGuardKind.MissingNpmEntrypointShadowsLaterRoute,
            environment.SearchPath, environment.PathExtensions, leading.Path, leadingFile.Identity, leadingFile.Sha256!,
            entrypoint, later.Path, laterFile.Identity, files, directories);
        Verify(guard, () => environment);
        return guard;
    }

    internal static void Validate(NpmProposal plan)
    {
        if (plan.RouteGuard is not { } guard) return;
        if (plan.Operation != NpmOperation.Doctor || !Enum.IsDefined(guard.Kind) ||
            !Same(plan.Prefix, Path.GetDirectoryName(plan.SelectedPath)!) || !Same(plan.Prefix, Path.GetDirectoryName(guard.LeadingPath)!) ||
            !plan.Files.Any(file => Same(file.Path, guard.LeadingPath) && file.Sha256 == guard.LeadingSha256) ||
            !plan.Files.Any(file => Same(file.Path, guard.MissingEntrypoint) && file.Sha256 is null))
            throw new IOException("The saved external-route condition is not bound to this selected missing entrypoint.");
        VerifySnapshot(guard);
    }
    internal static void VerifySnapshot(NpmRouteGuard guard)
    {
        var expected = Capture(new(guard.SearchPath, guard.PathExtensions), guard.LeadingPath, guard.LaterPath);
        if (JsonSerializer.Serialize(expected) != JsonSerializer.Serialize(guard))
            throw new IOException("The saved external-route evidence is incomplete or has changed. Inspect again.");
    }

    internal static void Verify(NpmRouteGuard? guard, Func<DiscoveryEnvironment>? environmentReader)
    {
        if (guard is null) return;
        var current = (environmentReader ?? DiscoveryEnvironment.Capture)();
        if (current.SearchPath != guard.SearchPath || current.PathExtensions != guard.PathExtensions)
            throw new IOException("PATH or PATHEXT changed after the external-route review. Inspect again before continuing.");
        foreach (var original in guard.Directories)
        {
            var now = CaptureDirectory(original.Path);
            if (now != original) throw new IOException("A reviewed PATH folder changed identity or presence: " + original.Path);
        }
        VerifyFiles(guard.ProtectedFiles);
    }

    internal static void VerifyResolved(NpmRouteGuard guard, Func<DiscoveryEnvironment>? environmentReader)
    {
        Verify(guard, environmentReader);
        var scan = CodexDiscovery.Scan(new(guard.SearchPath, guard.PathExtensions));
        if (scan.Issues.Count > 0 || scan.Commands.Count == 0 || !Same(scan.Commands[0].Path, guard.LeadingPath))
            throw new IOException("The repaired launcher is no longer the first external PATH route.");
        var leading = scan.Commands[0];
        var launcher = RequireKnownLauncher(leading);
        if (!Same(Entrypoint(leading), guard.MissingEntrypoint))
            throw new IOException("The repaired launcher points to another entrypoint.");
        var entrypoint = RequirePresent(guard.MissingEntrypoint);
        VerifyFiles([launcher, entrypoint]);
        Verify(guard, environmentReader);
    }

    internal static NpmFile RequireKnownLauncher(CommandMatch command)
    {
        if (!command.Path.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase) || command.Package is null ||
            !Same(command.Package.Root, Path.Combine(Path.GetDirectoryName(command.Path)!, "node_modules", "@openai", "codex")))
            throw new IOException("The route is not an identified local npm PowerShell Codex launcher: " + command.Path);
        var file = RequirePresent(command.Path);
        RequirePresent(command.Package.MetadataPath);
        if (!NpmPowerShellShim.IsSupported(NpmPowerShellShim.Read(command.Path)))
            throw new IOException("The complete PowerShell launcher body is not a supported generated npm shape: " + command.Path);
        VerifyFiles([file]);
        return file;
    }
    internal static string Entrypoint(CommandMatch command) => Path.Combine(command.Package!.Root, "bin", "codex.js");
    internal static NpmFile RequirePresent(string path)
    {
        var file = CaptureLocal(path);
        if (file.Sha256 is null) throw new IOException("Required route evidence is missing: " + path);
        return file;
    }
    internal static NpmFile CaptureLocal(string path)
    {
        var file = NpmFile.Capture(path);
        if (!Same(file.Path, file.Identity)) throw new IOException("A route or its evidence redirects elsewhere: " + path);
        if (file.Sha256 is null)
        {
            // ERROR_PATH_NOT_FOUND also occurs when a parent is a file. That is
            // an obstructed path, not the supported missing-entrypoint condition.
            for (var parent = Path.GetDirectoryName(file.Path); parent is not null; parent = Path.GetDirectoryName(parent))
            {
                try
                {
                    if ((File.GetAttributes(parent) & FileAttributes.Directory) == 0)
                        throw new IOException("A file occupies a required route folder: " + parent);
                    break;
                }
                catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException) { }
            }
        }
        return file;
    }
    internal static IReadOnlyList<NpmDirectory> CaptureDirectories(DiscoveryEnvironment environment)
    {
        if (environment.SearchPath.Length > 32767 || environment.PathExtensions.Length > 32767)
            throw new IOException("The external command environment exceeds its inspection limit.");
        _ = Extensions(environment);
        var directories = new List<NpmDirectory>();
        foreach (var raw in environment.SearchPath.Split(';'))
        {
            var path = raw.Trim();
            if (path.Length == 0) continue; // The supported external PATH scanner skips empty entries.
            if (path.Contains('"') || !Path.IsPathFullyQualified(path))
                throw new IOException("A quoted or relative PATH entry prevents a complete external-route diagnosis: " + raw);
            path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            if (directories.Any(directory => Same(directory.Path, path))) continue;
            if (directories.Count >= 512) throw new IOException("PATH exceeds the 512-folder conflict inspection limit.");
            directories.Add(CaptureDirectory(path));
        }
        return Array.AsReadOnly(directories.ToArray());
    }
    private static NpmDirectory CaptureDirectory(string path)
    {
        var identity = DiscoveryFiles.ResolveLocal(path);
        if (!Same(path, identity)) throw new IOException("A PATH folder redirects elsewhere: " + path);
        try
        {
            if ((File.GetAttributes(path) & FileAttributes.Directory) == 0)
                throw new IOException("A PATH entry names a file instead of a folder: " + path);
            return new(path, identity, true);
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        { return new(path, identity, false); }
    }
    private static IReadOnlyList<string> Extensions(DiscoveryEnvironment environment)
    {
        var extensions = new List<string> { ".ps1" };
        foreach (var extension in environment.PathExtensions.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            if (extension.Length < 2 || extension[0] != '.' || !extension[1..].All(char.IsAsciiLetterOrDigit))
                throw new IOException("An unsupported PATHEXT entry prevents a complete external-route diagnosis.");
            if (!extensions.Contains(extension, StringComparer.OrdinalIgnoreCase)) extensions.Add(extension.ToLowerInvariant());
        }
        if (extensions.Count > 16) throw new IOException("PATHEXT exceeds the 16-extension conflict inspection limit.");
        extensions.Add("");
        return extensions;
    }
    private static void VerifyFiles(IEnumerable<NpmFile> files)
    {
        foreach (var file in files)
            if (CaptureLocal(file.Path) != file)
                throw new IOException("The protected external-route evidence changed: " + file.Path);
    }
    private static bool Same(string left, string right) => string.Equals(Path.TrimEndingDirectorySeparator(left), Path.TrimEndingDirectorySeparator(right), StringComparison.OrdinalIgnoreCase);
    private static bool IsWithin(string path, string root) => Same(path, root) || path.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
