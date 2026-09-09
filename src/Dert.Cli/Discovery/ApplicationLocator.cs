using Dert.Cli.Applications;
using Microsoft.Win32;
using System.Diagnostics;

namespace Dert.Cli.Discovery;

public sealed record ApplicationLocationEvidence(
    ApplicationLocationKind Kind,
    string Label,
    string Provenance);

public sealed record ApplicationLocationSize(
    long Bytes,
    int Files,
    int Directories,
    bool IsPartial,
    string? Explanation);

public sealed record ApplicationLocation(
    string Path,
    string Identity,
    bool Exists,
    bool IsDirectory,
    string GroupKey,
    string GroupReason,
    IReadOnlyList<ApplicationLocationEvidence> Evidence,
    ApplicationLocationSize? Size,
    IReadOnlyList<DiscoveryIssue> Issues)
{
    public ApplicationLocationKind Kind => Evidence[0].Kind;
    public string Label => Evidence[0].Label;
    public string Provenance => string.Join("; ", Evidence.Select(item => item.Provenance));
    public long? SizeBytes => Size?.Bytes;
    public bool IsPartial => Size?.IsPartial ?? !Exists;
    public string? PartialReason => Size?.Explanation ?? (Exists ? null : "The location could not be inspected.");
}

public sealed record ApplicationLocationGroup(
    string Key,
    string Reason,
    IReadOnlyList<ApplicationLocation> Locations);

public enum ApplicationLocationPhase
{
    Paths,
    Registry,
    Folders,
    Measure
}

public sealed record ApplicationLocationActivity(
    ApplicationLocationPhase Phase,
    bool Completed,
    string Detail,
    string? Path = null);

public sealed record ApplicationLocationReport(
    ApplicationDefinition Application,
    DiscoveryReport Discovery,
    IReadOnlyList<ApplicationLocation> Locations,
    IReadOnlyList<DiscoveryIssue> Issues,
    DateTimeOffset CapturedAt)
{
    public IReadOnlyList<ApplicationLocationGroup> Groups => Locations
        .GroupBy(location => location.GroupKey, StringComparer.OrdinalIgnoreCase)
        .Select(group => new ApplicationLocationGroup(group.Key, group.First().GroupReason, group.ToArray()))
        .ToArray();
}

public static class ApplicationLocator
{
    public static ApplicationLocationReport Scan(ApplicationDefinition application,
        DiscoveryEnvironment environment, CancellationToken cancellation = default,
        Action<ApplicationLocationActivity>? activity = null)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(environment);
        Validate(environment.MeasurementLimits);
        cancellation.ThrowIfCancellationRequested();

        activity?.Invoke(new(ApplicationLocationPhase.Paths, false,
            $"Inspecting PATH for the exact command name '{application.CommandName}'."));
        var discovery = IsCodex(application)
            ? CodexDiscovery.Scan(environment, cancellation)
            : CommandPathScanner.Scan(application.CommandName, environment,
                (path, identity, position, _) => new CommandMatch(path, position, identity, null,
                    Array.Empty<DiscoveryIssue>()), cancellation);
        activity?.Invoke(new(ApplicationLocationPhase.Paths, true,
            $"Inspected {discovery.DirectoryCount} PATH folders and found {discovery.Commands.Count} command routes."));
        var reportIssues = new List<DiscoveryIssue>(discovery.Issues);
        var locations = new List<LocationBuilder>();
        var byIdentity = new Dictionary<string, LocationBuilder>(StringComparer.OrdinalIgnoreCase);
        var anchors = new List<GroupAnchor>();

        foreach (var command in discovery.Commands)
        {
            cancellation.ThrowIfCancellationRequested();
            var groupKey = command.Package?.Root ?? command.Identity;
            var groupReason = command.Package is null
                ? "This group is the exact resolved PATH command identity."
                : "These paths connect to the same verified Codex package root.";
            anchors.Add(new(groupKey, command.Identity, groupReason));
            if (command.Package is not null)
                anchors.Add(new(groupKey, command.Package.Root, groupReason));
            AddLocation(new(command.Path, ApplicationLocationKind.Command, "Command route",
                $"PATH position {command.PathPosition}", groupKey, groupReason,
                command.Issues), mergeWithExisting: true);
        }

        foreach (var package in discovery.Commands.Select(command => command.Package)
                     .OfType<CodexPackage>()
                     .GroupBy(package => package.Root, StringComparer.OrdinalIgnoreCase)
                     .Select(group => group.First()))
        {
            cancellation.ThrowIfCancellationRequested();
            AddLocation(new(package.Root, ApplicationLocationKind.PackageRoot,
                "Verified package root", "Codex launcher target and @openai/codex package metadata",
                package.Root, "These paths connect to the same verified Codex package root.",
                Array.Empty<DiscoveryIssue>()), mergeWithExisting: true);
        }

        ReadSource(environment.RegistryEvidence, ApplicationLocationPhase.Registry);
        ReadSource(environment.FolderEvidence, ApplicationLocationPhase.Folders);

        var finished = locations.Select(location => location.Finish()).ToArray();
        return new(application, discovery, finished, reportIssues.ToArray(), DateTimeOffset.UtcNow);

        void ReadSource(IApplicationLocationEvidenceSource source, ApplicationLocationPhase phase)
        {
            cancellation.ThrowIfCancellationRequested();
            activity?.Invoke(new(phase, false, phase == ApplicationLocationPhase.Registry
                ? "Inspecting registry-reported application locations."
                : "Inspecting known adjacent application folders."));
            ApplicationLocationEvidenceResult evidence;
            try { evidence = source.Read(application, cancellation); }
            catch (OperationCanceledException) { throw; }
            catch (Exception error) when (DiscoveryFiles.IsReadError(error) || error is System.Security.SecurityException)
            {
                reportIssues.Add(new("ERP5201", source.GetType().Name,
                    $"This application evidence source could not be inspected. {error.Message}"));
                activity?.Invoke(new(phase, true,
                    "The evidence source ended with an inspection finding."));
                return;
            }

            reportIssues.AddRange(evidence.Issues);
            foreach (var candidate in evidence.Locations)
            {
                cancellation.ThrowIfCancellationRequested();
                var grouping = GroupFor(candidate);
                AddLocation(new(candidate.Path, candidate.Kind, Label(candidate.Kind), candidate.Provenance,
                    grouping.Key, grouping.Reason, Array.Empty<DiscoveryIssue>()),
                    mergeWithExisting: candidate.Kind != ApplicationLocationKind.AdjacentFolder);
            }
            activity?.Invoke(new(phase, true,
                $"Recorded {evidence.Locations.Count} reported locations and {evidence.Issues.Count} source findings."));
        }

        Grouping GroupFor(ApplicationLocationCandidate candidate)
        {
            if (candidate.Kind == ApplicationLocationKind.AdjacentFolder)
                return new($"adjacent:{candidate.Path}",
                    "This is adjacent application data; folder-name evidence does not make it an installation.");

            string identity;
            bool isDirectory;
            try
            {
                identity = DiscoveryFiles.ResolveLocal(candidate.Path);
                isDirectory = Directory.Exists(identity);
            }
            catch (Exception error) when (DiscoveryFiles.IsReadError(error))
            {
                return new($"reported:{candidate.Path}",
                    "This reported location could not be connected to a verified PATH identity.");
            }

            var matching = anchors.Where(anchor => isDirectory
                    ? IsWithin(anchor.Path, identity)
                    : PathsEqual(anchor.Path, identity))
                .GroupBy(anchor => anchor.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First()).ToArray();
            return matching.Length == 1
                ? new(matching[0].Key, matching[0].Reason)
                : new($"reported:{identity}", matching.Length == 0
                    ? "This reported location has no verified PATH identity connection."
                    : "This reported location contains multiple PATH identities and was not guessed into one group.");
        }

        void AddLocation(LocationSeed seed, bool mergeWithExisting)
        {
            cancellation.ThrowIfCancellationRequested();
            activity?.Invoke(new(ApplicationLocationPhase.Measure, false,
                $"Measuring {seed.Label.ToLowerInvariant()}.", seed.Path));
            var inspected = Inspect(seed, environment.MeasurementLimits, cancellation);
            activity?.Invoke(new(ApplicationLocationPhase.Measure, true,
                inspected.MeasuredSize is null ? "The location could not be measured."
                    : inspected.MeasuredSize.IsPartial ? "Recorded an explicitly partial size."
                    : "Recorded the complete bounded size.", seed.Path));
            reportIssues.AddRange(inspected.Findings);
            var identityKey = (seed.Kind == ApplicationLocationKind.AdjacentFolder ? "adjacent:" : "") + inspected.Identity;
            if (mergeWithExisting && byIdentity.TryGetValue(identityKey, out var existing))
            {
                existing.AddEvidence(inspected.FirstEvidence);
                existing.AddIssues(inspected.Findings);
                return;
            }

            locations.Add(inspected);
            if (mergeWithExisting) byIdentity[identityKey] = inspected;
        }
    }

    private static LocationBuilder Inspect(LocationSeed seed, ApplicationMeasurementLimits limits,
        CancellationToken cancellation)
    {
        var issues = new List<DiscoveryIssue>(seed.Issues);
        var evidence = new ApplicationLocationEvidence(seed.Kind, seed.Label, seed.Provenance);
        string identity;
        FileAttributes attributes;
        try
        {
            if (string.IsNullOrWhiteSpace(seed.Path) || !Path.IsPathFullyQualified(seed.Path))
                throw new IOException("The reported path is not a fully qualified local path.");
            var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(seed.Path));
            identity = DiscoveryFiles.ResolveLocal(fullPath);
            attributes = File.GetAttributes(fullPath);
            var isDirectory = (attributes & FileAttributes.Directory) != 0;
            var size = Measure(fullPath, isDirectory, (attributes & FileAttributes.ReparsePoint) != 0,
                limits, issues, cancellation);
            return new(seed.Path, identity, true, isDirectory, seed.GroupKey, seed.GroupReason,
                evidence, size, issues);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) when (DiscoveryFiles.IsReadError(error))
        {
            identity = seed.Path;
            issues.Add(new("ERP5202", seed.Path,
                $"This reported application location could not be inspected. {error.Message}"));
            return new(seed.Path, identity, false, false, seed.GroupKey, seed.GroupReason,
                evidence, null, issues);
        }
    }

    private static ApplicationLocationSize Measure(string path, bool isDirectory, bool rootIsLink,
        ApplicationMeasurementLimits limits, List<DiscoveryIssue> issues, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!isDirectory)
        {
            try { return new(new FileInfo(path).Length, 1, 0, false, null); }
            catch (Exception error) when (DiscoveryFiles.IsReadError(error))
            {
                issues.Add(new("ERP5203", path, $"The file size could not be read. {error.Message}"));
                return new(0, 0, 0, true, "The file size could not be read.");
            }
        }

        if (rootIsLink)
            return new(0, 0, 0, true, "The location itself is a directory link; its target was not traversed.");
        if (IsDriveRoot(path))
            return new(0, 0, 0, true, "A whole-drive root is outside this bounded folder measurement.");

        long bytes = 0;
        var files = 0;
        var directories = 0;
        var entries = 0;
        var partialReasons = new List<string>();
        var pending = new Stack<(string Path, int Depth)>();
        pending.Push((path, 0));
        var elapsed = Stopwatch.StartNew();

        while (pending.Count > 0)
        {
            cancellation.ThrowIfCancellationRequested();
            if (elapsed.Elapsed >= limits.MaxDuration)
            {
                AddReason($"Measurement stopped after the {limits.MaxDuration.TotalSeconds:0.###}-second time bound.");
                break;
            }

            var current = pending.Pop();
            IEnumerator<string>? iterator = null;
            try
            {
                iterator = Directory.EnumerateFileSystemEntries(current.Path).GetEnumerator();
                while (true)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (elapsed.Elapsed >= limits.MaxDuration)
                    {
                        AddReason($"Measurement stopped after the {limits.MaxDuration.TotalSeconds:0.###}-second time bound.");
                        pending.Clear();
                        break;
                    }
                    if (!iterator.MoveNext()) break;
                    if (entries >= limits.MaxEntries)
                    {
                        AddReason($"Measurement stopped at the {limits.MaxEntries}-entry bound.");
                        pending.Clear();
                        break;
                    }

                    var entry = iterator.Current;
                    entries++;
                    try
                    {
                        var attributes = File.GetAttributes(entry);
                        if ((attributes & FileAttributes.Directory) != 0)
                        {
                            directories++;
                            if ((attributes & FileAttributes.ReparsePoint) != 0)
                            {
                                AddReason("One or more directory links were counted but their targets were not traversed.");
                            }
                            else if (current.Depth >= limits.MaxDepth)
                            {
                                AddReason($"Measurement stopped descending at the {limits.MaxDepth}-level depth bound.");
                            }
                            else pending.Push((entry, current.Depth + 1));
                        }
                        else
                        {
                            files++;
                            var length = new FileInfo(entry).Length;
                            bytes = length > long.MaxValue - bytes ? long.MaxValue : bytes + length;
                        }
                    }
                    catch (Exception error) when (DiscoveryFiles.IsReadError(error))
                    {
                        AddReason("One or more entries became inaccessible while the folder was measured.");
                        issues.Add(new("ERP5203", entry,
                            $"This entry was omitted from the folder total. {error.Message}"));
                    }
                }
            }
            catch (Exception error) when (DiscoveryFiles.IsReadError(error))
            {
                AddReason("One or more folders were inaccessible while the location was measured.");
                issues.Add(new("ERP5203", current.Path,
                    $"This folder was omitted from the location total. {error.Message}"));
            }
            finally { iterator?.Dispose(); }
        }

        return new(bytes, files, directories, partialReasons.Count > 0,
            partialReasons.Count == 0 ? null : string.Join(" ", partialReasons));

        void AddReason(string reason)
        {
            if (!partialReasons.Contains(reason, StringComparer.Ordinal)) partialReasons.Add(reason);
        }
    }

    private static bool IsCodex(ApplicationDefinition application) =>
        application.CommandName.Equals("codex", StringComparison.OrdinalIgnoreCase) &&
        application.NpmPackage?.Equals("@openai/codex", StringComparison.OrdinalIgnoreCase) == true;

    private static string Label(ApplicationLocationKind kind) => kind switch
    {
        ApplicationLocationKind.Command => "Command route",
        ApplicationLocationKind.PackageRoot => "Verified package root",
        ApplicationLocationKind.RegistryInstallLocation => "Registry install location",
        ApplicationLocationKind.RegistryAppPath => "Registry App Path",
        ApplicationLocationKind.AdjacentFolder => "Adjacent application folder",
        _ => "Application location"
    };

    private static bool PathsEqual(string left, string right) =>
        Path.TrimEndingDirectorySeparator(left).Equals(Path.TrimEndingDirectorySeparator(right),
            StringComparison.OrdinalIgnoreCase);

    internal static bool IsWithin(string child, string parent)
    {
        var relative = Path.GetRelativePath(parent, child);
        return relative == "." || (!Path.IsPathRooted(relative) && !relative.Equals("..", StringComparison.Ordinal) &&
            !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal));
    }

    private static bool IsDriveRoot(string path)
    {
        var root = Path.GetPathRoot(path);
        return root is not null && PathsEqual(path, root);
    }

    private static void Validate(ApplicationMeasurementLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        if (limits.MaxEntries <= 0) throw new ArgumentOutOfRangeException(nameof(limits.MaxEntries));
        if (limits.MaxDepth < 0) throw new ArgumentOutOfRangeException(nameof(limits.MaxDepth));
        if (limits.MaxDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(limits.MaxDuration));
    }

    private sealed record LocationSeed(string Path, ApplicationLocationKind Kind, string Label,
        string Provenance, string GroupKey, string GroupReason, IReadOnlyList<DiscoveryIssue> Issues);
    private sealed record GroupAnchor(string Key, string Path, string Reason);
    private sealed record Grouping(string Key, string Reason);

    private sealed class LocationBuilder
    {
        private readonly List<ApplicationLocationEvidence> evidence;
        private readonly List<DiscoveryIssue> issues;
        internal string Path { get; }
        internal string Identity { get; }
        private bool Exists { get; }
        private bool IsDirectory { get; }
        private string GroupKey { get; }
        private string GroupReason { get; }
        private ApplicationLocationSize? Size { get; }
        internal ApplicationLocationSize? MeasuredSize => Size;
        internal ApplicationLocationEvidence FirstEvidence => evidence[0];
        internal IReadOnlyList<DiscoveryIssue> Findings => issues;

        internal LocationBuilder(string path, string identity, bool exists, bool isDirectory,
            string groupKey, string groupReason, ApplicationLocationEvidence firstEvidence,
            ApplicationLocationSize? size, IReadOnlyList<DiscoveryIssue> initialIssues)
        {
            Path = path;
            Identity = identity;
            Exists = exists;
            IsDirectory = isDirectory;
            GroupKey = groupKey;
            GroupReason = groupReason;
            Size = size;
            evidence = new() { firstEvidence };
            issues = new(initialIssues);
        }

        internal void AddEvidence(ApplicationLocationEvidence item)
        {
            if (!evidence.Contains(item)) evidence.Add(item);
        }

        internal void AddIssues(IEnumerable<DiscoveryIssue> findings)
        {
            foreach (var issue in findings) if (!issues.Contains(issue)) issues.Add(issue);
        }

        internal ApplicationLocation Finish() => new(Path, Identity, Exists, IsDirectory,
            GroupKey, GroupReason, evidence.ToArray(), Size, issues.ToArray());
    }
}

internal sealed class WindowsRegistryApplicationEvidenceSource : IApplicationLocationEvidenceSource
{
    internal static WindowsRegistryApplicationEvidenceSource Instance { get; } = new();
    public ApplicationLocationEvidenceResult Read(ApplicationDefinition application,
        CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows()) return ApplicationLocationEvidenceResult.Empty;
        var locations = new List<ApplicationLocationCandidate>();
        var issues = new List<DiscoveryIssue>();
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            cancellation.ThrowIfCancellationRequested();
            var hiveLabel = hive == RegistryHive.CurrentUser ? "HKCU" : "HKLM";
            var viewLabel = view == RegistryView.Registry64 ? "64-bit" : "32-bit";
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, view);
                ReadAppPath(root, hiveLabel, viewLabel);
                ReadUninstall(root, hiveLabel, viewLabel);
            }
            catch (Exception error) when (IsRegistryReadError(error))
            {
                issues.Add(new("ERP5201", $"{hiveLabel} {viewLabel}",
                    $"This registry view could not be inspected. {error.Message}"));
            }
        }

        return new(locations
            .DistinctBy(location => $"{location.Kind}\0{location.Path}", StringComparer.OrdinalIgnoreCase)
            .ToArray(), issues.ToArray());

        void ReadAppPath(RegistryKey root, string hiveLabel, string viewLabel)
        {
            cancellation.ThrowIfCancellationRequested();
            var keyPath = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{application.CommandName}.exe";
            try
            {
                using var key = root.OpenSubKey(keyPath, writable: false);
                if (key?.GetValue(null) is string path && !string.IsNullOrWhiteSpace(path))
                    locations.Add(new(path, ApplicationLocationKind.RegistryAppPath,
                        $"{hiveLabel} {viewLabel} App Paths default value"));
            }
            catch (Exception error) when (IsRegistryReadError(error))
            {
                issues.Add(new("ERP5201", $"{hiveLabel}\\{keyPath}",
                    $"This App Paths entry could not be inspected. {error.Message}"));
            }
        }

        void ReadUninstall(RegistryKey root, string hiveLabel, string viewLabel)
        {
            cancellation.ThrowIfCancellationRequested();
            const string keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            try
            {
                using var uninstall = root.OpenSubKey(keyPath, writable: false);
                if (uninstall is null) return;
                foreach (var subkeyName in uninstall.GetSubKeyNames())
                {
                    cancellation.ThrowIfCancellationRequested();
                    try
                    {
                        using var entry = uninstall.OpenSubKey(subkeyName, writable: false);
                        if (entry?.GetValue("DisplayName") is not string displayName ||
                            !MatchesApplication(displayName, application)) continue;
                        if (entry.GetValue("InstallLocation") is string path && !string.IsNullOrWhiteSpace(path))
                            locations.Add(new(path, ApplicationLocationKind.RegistryInstallLocation,
                                $"{hiveLabel} {viewLabel} uninstall entry '{displayName}' InstallLocation"));
                    }
                    catch (Exception error) when (IsRegistryReadError(error))
                    {
                        issues.Add(new("ERP5201", $"{hiveLabel}\\{keyPath}\\{subkeyName}",
                            $"This matching uninstall entry could not be inspected. {error.Message}"));
                    }
                }
            }
            catch (Exception error) when (IsRegistryReadError(error))
            {
                issues.Add(new("ERP5201", $"{hiveLabel}\\{keyPath}",
                    $"The installed-application names and locations in this registry view could not be inspected. {error.Message}"));
            }
        }
    }

    private static bool MatchesApplication(string displayName, ApplicationDefinition application) =>
        displayName.Equals(application.DisplayName, StringComparison.OrdinalIgnoreCase) ||
        displayName.Equals(application.Id, StringComparison.OrdinalIgnoreCase) ||
        displayName.Equals(application.CommandName, StringComparison.OrdinalIgnoreCase);

    private static bool IsRegistryReadError(Exception error) =>
        error is IOException or UnauthorizedAccessException or System.Security.SecurityException;
}

internal sealed class KnownApplicationFolderEvidenceSource : IApplicationLocationEvidenceSource
{
    internal static KnownApplicationFolderEvidenceSource Instance { get; } = new();
    public ApplicationLocationEvidenceResult Read(ApplicationDefinition application,
        CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!application.CommandName.Equals("codex", StringComparison.OrdinalIgnoreCase))
            return ApplicationLocationEvidenceResult.Empty;

        var configured = Environment.GetEnvironmentVariable("CODEX_HOME");
        if (!string.IsNullOrWhiteSpace(configured))
            return new(new[] { new ApplicationLocationCandidate(configured,
                    ApplicationLocationKind.AdjacentFolder,
                    "Configured CODEX_HOME folder; its presence does not identify an installation") },
                Array.Empty<DiscoveryIssue>());

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(profile)) return ApplicationLocationEvidenceResult.Empty;
        var candidate = Path.Combine(profile, ".codex");
        return Directory.Exists(candidate)
            ? new(new[] { new ApplicationLocationCandidate(candidate,
                    ApplicationLocationKind.AdjacentFolder,
                    "Known default Codex data-folder candidate; its name and presence do not prove ownership") },
                Array.Empty<DiscoveryIssue>())
            : ApplicationLocationEvidenceResult.Empty;
    }
}
