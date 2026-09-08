namespace Erpdert.Cli.Discovery;

public static class CodexDiscovery
{
    public static DiscoveryReport Scan(DiscoveryEnvironment environment)
    {
        var issues = new List<DiscoveryIssue>();
        var directories = new List<SearchDirectory>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = environment.SearchPath.Split(';');
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index].Trim();
            if (entry.Length == 0) continue;
            try
            {
                if (entry.Contains('"'))
                    throw new IOException("PowerShell does not remove quotes inside PATH entries. This quoted folder was not treated as a command route.");
                // A parent PowerShell session can have a different working directory
                // from this process. Do not quietly invent its relative PATH meaning.
                if (!Path.IsPathFullyQualified(entry))
                    throw new IOException("This PATH entry is relative. Its meaning depends on the calling shell's current directory.");
                var path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(entry));
                if (!seen.Add(path)) continue;
                var resolved = DiscoveryFiles.ResolveLocal(path);
                directories.Add(new(path, resolved, index + 1));
            }
            catch (Exception error) when (DiscoveryFiles.IsReadError(error))
            {
                issues.Add(new("ERP5101", entry, $"This PATH folder was not inspected. {error.Message}"));
            }
        }

        // Match PowerShell's external-command discovery: ps1, PATHEXT, bare name.
        // Aliases, functions, cmdlets and execution policy are deliberately not inferred.
        var extensions = new List<string> { ".ps1" };
        foreach (var extension in environment.PathExtensions.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            if (extension.Length < 2 || extension[0] != '.' || !extension[1..].All(char.IsAsciiLetterOrDigit))
            {
                issues.Add(new("ERP5102", extension, "This PATHEXT entry is not supported; the search may be incomplete."));
                continue;
            }
            if (!extensions.Contains(extension, StringComparer.OrdinalIgnoreCase)) extensions.Add(extension.ToLowerInvariant());
        }
        extensions.Add("");

        var matches = new List<CommandMatch>();
        foreach (var directory in directories)
        {
            foreach (var extension in extensions)
            {
                var path = Path.Combine(directory.Path, "codex" + extension);
                try
                {
                    var identity = DiscoveryFiles.ResolveLocal(Path.Combine(directory.Resolved, "codex" + extension));
                    if (!DiscoveryFiles.IsFile(identity)) continue;
                    matches.Add(CodexPackageProbe.Inspect(path, identity, directory.Position,
                        directories.Select(d => d.Resolved).ToArray()));
                }
                catch (Exception error) when (DiscoveryFiles.IsReadError(error))
                {
                    issues.Add(new("ERP5101", path, $"This command path could not be inspected. {error.Message}"));
                }
            }
        }
        return new(matches.ToArray(), issues.ToArray(), directories.Count);
    }

    private sealed record SearchDirectory(string Path, string Resolved, int Position);
}
