using Dert.Cli.Applications;
using Dert.Cli.Discovery;
using Xunit;

namespace Dert.Tests;

public class ApplicationLocationTests
{
    [Theory]
    [InlineData("C:\\Tools\\codex.cmd", "D:\\RegisteredApp", false)]
    [InlineData("C:\\ToolsOther\\codex.cmd", "C:\\Tools", false)]
    [InlineData("C:\\Tools\\bin\\codex.cmd", "C:\\Tools", true)]
    public void Folder_connections_require_actual_containment_on_the_same_drive(string child, string parent, bool expected)
    {
        if (OperatingSystem.IsWindows()) Assert.Equal(expected, ApplicationLocator.IsWithin(child, parent));
    }
    [Fact]
    public void Catalog_resolves_codex_as_the_supported_update_adapter()
    {
        Assert.True(ApplicationCatalog.TryResolve("CoDeX", out var application, out var reason));
        Assert.Equal("", reason);
        Assert.Equal(new ApplicationDefinition("codex", "Codex", "codex", "@openai/codex"), application);
        Assert.True(application.CanUpdate);
        Assert.True(application.CanDoctor);
    }

    [Theory]
    [InlineData("pwsh")]
    [InlineData("code-insiders")]
    [InlineData("my.tool_2")]
    [InlineData("Visual Studio Code")]
    [InlineData("開発 ツール 2")]
    [InlineData("CMake GUI (Preview)+")]
    public void Catalog_accepts_safe_generic_command_names_for_where(string name)
    {
        Assert.True(ApplicationCatalog.TryResolve(name, out var application, out var reason));
        Assert.Equal("", reason);
        Assert.Equal(name, application.CommandName);
        Assert.Equal(name, application.DisplayName);
        Assert.False(application.CanUpdate);
        Assert.False(application.CanDoctor);
    }

    [Fact]
    public void Merely_naming_an_npm_package_does_not_create_an_update_adapter()
    {
        var application = new ApplicationDefinition("other", "Other", "other", "some-package");

        Assert.False(application.CanUpdate);
        Assert.False(application.CanDoctor);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../codex")]
    [InlineData("a\\codex")]
    [InlineData("-option")]
    [InlineData("bad*name")]
    [InlineData("bad;name")]
    [InlineData("bad\tname")]
    [InlineData("bad\nname")]
    [InlineData("\"quoted\"")]
    [InlineData("NUL")]
    [InlineData("COM1")]
    public void Catalog_rejects_names_that_are_not_safe_exact_commands(string name)
    {
        Assert.False(ApplicationCatalog.TryResolve(name, out _, out var reason));
        Assert.NotEmpty(reason);
    }

    [Fact]
    public void Generic_discovery_matches_only_the_exact_command_name_in_PATH_order()
    {
        using var scratch = new ScratchDocument("");
        File.Delete(scratch.Path);
        var first = Write(scratch.DirectoryPath, "first/moon.exe", "first");
        Write(scratch.DirectoryPath, "first/moon-helper.exe", "not a match");
        var second = Write(scratch.DirectoryPath, "second/moon.cmd", "second");
        ApplicationCatalog.TryResolve("moon", out var application, out _);

        var report = ApplicationLocator.Scan(application, new(
            string.Join(';', Path.GetDirectoryName(first), Path.GetDirectoryName(second)), ".EXE;.CMD"));

        Assert.Equal(new[] { first, second }, report.Discovery.Commands.Select(command => command.Path));
        Assert.All(report.Discovery.Commands, command =>
        {
            Assert.Null(command.Package);
            Assert.Empty(command.Issues);
        });
        Assert.Equal(2, report.Groups.Count);
    }

    [Fact]
    public void Spaced_display_name_is_also_the_literal_exact_PATH_filename()
    {
        using var scratch = new ScratchDocument("");
        File.Delete(scratch.Path);
        var command = Write(scratch.DirectoryPath, "bin/Visual Studio Code.exe", "application");
        Write(scratch.DirectoryPath, "bin/Visual Studio Code Helper.exe", "not a match");
        ApplicationCatalog.TryResolve("Visual Studio Code", out var application, out _);

        var report = ApplicationLocator.Scan(application,
            new DiscoveryEnvironment(Path.GetDirectoryName(command)!, ".EXE"));

        Assert.Equal(command, Assert.Single(report.Discovery.Commands).Path);
    }

    [Theory]
    [InlineData("Visual Studio Code")]
    [InlineData("開発 ツール")]
    public void Registry_evidence_source_receives_the_exact_spaced_or_Unicode_display_name(string name)
    {
        using var scratch = new ScratchDocument("");
        File.Delete(scratch.Path);
        var folder = Directory.CreateDirectory(Path.Combine(scratch.DirectoryPath, "registered")).FullName;
        ApplicationCatalog.TryResolve(name, out var application, out _);
        var source = new ExactNameRegistrySource(name, folder);

        var report = ApplicationLocator.Scan(application,
            new DiscoveryEnvironment("", ".EXE") { RegistryEvidence = source });

        Assert.Equal(name, source.ObservedDisplayName);
        Assert.Contains("exact display name", Assert.Single(report.Locations).Provenance,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Injected_registry_and_folder_evidence_stays_connected_to_a_verified_codex_package()
    {
        using var fixture = new DiscoveryFixture();
        var prefix = fixture.Package("npm", "1.2.3");
        var data = Directory.CreateDirectory(Path.Combine(fixture.Root, ".codex")).FullName;
        File.WriteAllBytes(Path.Combine(data, "state.bin"), new byte[17]);
        var environment = new DiscoveryEnvironment(prefix, ".CMD;.EXE")
        {
            RegistryEvidence = new FixedEvidenceSource(new ApplicationLocationCandidate(
                Path.Combine(prefix, "codex.cmd"), ApplicationLocationKind.RegistryAppPath,
                "HKCU App Paths (default value)")),
            FolderEvidence = new FixedEvidenceSource(new ApplicationLocationCandidate(
                data, ApplicationLocationKind.AdjacentFolder,
                "Known Codex data folder candidate; its presence does not prove ownership"))
        };

        var report = ApplicationLocator.Scan(ApplicationCatalog.Codex, environment);

        var command = Assert.Single(report.Locations, location =>
            location.Identity.EndsWith("codex.cmd", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(command.Evidence, evidence => evidence.Kind == ApplicationLocationKind.Command);
        Assert.Contains(command.Evidence, evidence => evidence.Kind == ApplicationLocationKind.RegistryAppPath);
        var package = Assert.Single(report.Locations, location =>
            location.Evidence.Any(evidence => evidence.Kind == ApplicationLocationKind.PackageRoot));
        Assert.Equal(command.GroupKey, package.GroupKey);
        Assert.Contains("verified Codex package", package.GroupReason, StringComparison.OrdinalIgnoreCase);
        var adjacent = Assert.Single(report.Locations, location =>
            location.Evidence.Any(evidence => evidence.Kind == ApplicationLocationKind.AdjacentFolder));
        Assert.Empty(adjacent.Issues);
        Assert.True(adjacent.Size is not null,
            string.Join(Environment.NewLine, adjacent.Issues.Select(issue => issue.Explanation)));
        Assert.Equal(17, adjacent.Size.Bytes);
        Assert.False(adjacent.Size.IsPartial);
    }

    [Fact]
    public void Folder_measurement_reports_an_explicit_partial_total_at_the_entry_bound()
    {
        using var scratch = new ScratchDocument("");
        File.Delete(scratch.Path);
        var folder = Directory.CreateDirectory(Path.Combine(scratch.DirectoryPath, "data")).FullName;
        File.WriteAllBytes(Path.Combine(folder, "one.bin"), new byte[3]);
        File.WriteAllBytes(Path.Combine(folder, "two.bin"), new byte[5]);
        ApplicationCatalog.TryResolve("moon", out var application, out _);
        var environment = new DiscoveryEnvironment("", ".EXE")
        {
            FolderEvidence = new FixedEvidenceSource(new ApplicationLocationCandidate(folder,
                ApplicationLocationKind.AdjacentFolder, "fixture folder")),
            MeasurementLimits = new(1, 16, TimeSpan.FromSeconds(5))
        };

        var location = Assert.Single(ApplicationLocator.Scan(application, environment).Locations);

        Assert.Empty(location.Issues);
        Assert.True(location.Size is not null,
            string.Join(Environment.NewLine, location.Issues.Select(issue => issue.Explanation)));
        Assert.True(location.Size.IsPartial);
        Assert.Equal(1, location.Size.Files);
        Assert.Contains("1-entry", location.Size.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Directory_links_are_visible_but_are_not_followed_for_size()
    {
        using var scratch = new ScratchDocument("");
        File.Delete(scratch.Path);
        var target = Directory.CreateDirectory(Path.Combine(scratch.DirectoryPath, "target")).FullName;
        File.WriteAllBytes(Path.Combine(target, "large.bin"), new byte[41]);
        var folder = Directory.CreateDirectory(Path.Combine(scratch.DirectoryPath, "data")).FullName;
        var link = Path.Combine(folder, "linked");
        CreateJunction(link, target);
        ApplicationCatalog.TryResolve("moon", out var application, out _);
        try
        {
            var environment = new DiscoveryEnvironment("", ".EXE")
            {
                FolderEvidence = new FixedEvidenceSource(new ApplicationLocationCandidate(folder,
                    ApplicationLocationKind.AdjacentFolder, "fixture folder"))
            };

            var location = Assert.Single(ApplicationLocator.Scan(application, environment).Locations);

            Assert.Empty(location.Issues);
            Assert.True(location.Size is not null,
                string.Join(Environment.NewLine, location.Issues.Select(issue => issue.Explanation)));
            Assert.Equal(0, location.Size.Bytes);
            Assert.True(location.Size.IsPartial);
            Assert.Contains("link", location.Size.Explanation, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(link); }
    }

    [Fact]
    public void Uninspectable_reported_location_remains_visible_with_its_provenance()
    {
        ApplicationCatalog.TryResolve("moon", out var application, out _);
        var invalid = Path.Combine(Path.GetTempPath(), "bad") + '\0';
        var environment = new DiscoveryEnvironment("", ".EXE")
        {
            RegistryEvidence = new FixedEvidenceSource(new ApplicationLocationCandidate(invalid,
                ApplicationLocationKind.RegistryInstallLocation, "fixture registry location"))
        };

        var report = ApplicationLocator.Scan(application, environment);

        var location = Assert.Single(report.Locations);
        Assert.Equal(invalid, location.Path);
        Assert.False(location.Exists);
        Assert.Contains(location.Evidence, evidence => evidence.Provenance == "fixture registry location");
        Assert.Contains(report.Issues, issue => issue.Code == "ERP5202" && issue.Path == invalid);
    }

    [Fact]
    public void Cancellation_stops_location_discovery_before_evidence_sources_are_read()
    {
        ApplicationCatalog.TryResolve("moon", out var application, out _);
        var source = new FixedEvidenceSource();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var environment = new DiscoveryEnvironment("", ".EXE") { RegistryEvidence = source };

        Assert.Throws<OperationCanceledException>(() =>
            ApplicationLocator.Scan(application, environment, cancellation.Token));
        Assert.Equal(0, source.ReadCount);
    }

    [Fact]
    public void Evidence_source_findings_are_preserved_without_hiding_other_locations()
    {
        using var scratch = new ScratchDocument("");
        File.Delete(scratch.Path);
        var folder = Directory.CreateDirectory(Path.Combine(scratch.DirectoryPath, "reported")).FullName;
        ApplicationCatalog.TryResolve("moon", out var application, out _);
        var finding = new DiscoveryIssue("ERP5201", "registry", "One registry view was inaccessible.");
        var environment = new DiscoveryEnvironment("", ".EXE")
        {
            RegistryEvidence = new FixedEvidenceSource(
                new[] { new ApplicationLocationCandidate(folder,
                    ApplicationLocationKind.RegistryInstallLocation, "fixture registry location") },
                new[] { finding })
        };

        var report = ApplicationLocator.Scan(application, environment);

        Assert.Single(report.Locations);
        Assert.Contains(finding, report.Issues);
    }

    [Fact]
    public void Activity_reports_real_phase_boundaries_and_measured_paths()
    {
        using var scratch = new ScratchDocument("");
        File.Delete(scratch.Path);
        var folder = Directory.CreateDirectory(Path.Combine(scratch.DirectoryPath, "data")).FullName;
        ApplicationCatalog.TryResolve("moon", out var application, out _);
        var activities = new List<ApplicationLocationActivity>();
        var environment = new DiscoveryEnvironment("", ".EXE")
        {
            FolderEvidence = new FixedEvidenceSource(new ApplicationLocationCandidate(folder,
                ApplicationLocationKind.AdjacentFolder, "fixture folder"))
        };

        ApplicationLocator.Scan(application, environment, activity: activities.Add);

        Assert.Equal(new[]
        {
            (ApplicationLocationPhase.Paths, false),
            (ApplicationLocationPhase.Paths, true),
            (ApplicationLocationPhase.Registry, false),
            (ApplicationLocationPhase.Registry, true),
            (ApplicationLocationPhase.Folders, false),
            (ApplicationLocationPhase.Measure, false),
            (ApplicationLocationPhase.Measure, true),
            (ApplicationLocationPhase.Folders, true)
        }, activities.Select(item => (item.Phase, item.Completed)));
        Assert.Equal(folder, activities.Single(item =>
            item.Phase == ApplicationLocationPhase.Measure && !item.Completed).Path);
    }

    [Fact]
    public void A_reported_drive_root_is_never_recursed_for_size()
    {
        ApplicationCatalog.TryResolve("moon", out var application, out _);
        var root = Path.GetPathRoot(Path.GetTempPath())!;
        var environment = new DiscoveryEnvironment("", ".EXE")
        {
            FolderEvidence = new FixedEvidenceSource(new ApplicationLocationCandidate(root,
                ApplicationLocationKind.AdjacentFolder, "fixture drive root")),
            MeasurementLimits = new(1, 1, TimeSpan.FromSeconds(1))
        };

        var location = Assert.Single(ApplicationLocator.Scan(application, environment).Locations);

        Assert.True(location.Size!.IsPartial);
        Assert.Contains("whole-drive", location.Size.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, location.Size.Files);
    }

    private static string Write(string root, string relative, string content)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private static void CreateJunction(string link, string target)
    {
        var start = new System.Diagnostics.ProcessStartInfo(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            Arguments = $"/d /c mklink /J \"{link}\" \"{target}\""
        };
        using var process = System.Diagnostics.Process.Start(start)!;
        Assert.True(process.WaitForExit(10000));
        Assert.Equal(0, process.ExitCode);
    }

    private sealed class FixedEvidenceSource : IApplicationLocationEvidenceSource
    {
        private readonly ApplicationLocationEvidenceResult result;
        public int ReadCount { get; private set; }

        public FixedEvidenceSource(params ApplicationLocationCandidate[] locations) :
            this(locations, Array.Empty<DiscoveryIssue>()) { }

        public FixedEvidenceSource(IReadOnlyList<ApplicationLocationCandidate> locations,
            IReadOnlyList<DiscoveryIssue> issues) => result = new(locations, issues);

        public ApplicationLocationEvidenceResult Read(ApplicationDefinition application,
            CancellationToken cancellation)
        {
            ReadCount++;
            cancellation.ThrowIfCancellationRequested();
            return result;
        }
    }

    private sealed class ExactNameRegistrySource(string expectedName, string path) :
        IApplicationLocationEvidenceSource
    {
        public string? ObservedDisplayName { get; private set; }

        public ApplicationLocationEvidenceResult Read(ApplicationDefinition application,
            CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            ObservedDisplayName = application.DisplayName;
            return application.DisplayName == expectedName
                ? new(new[] { new ApplicationLocationCandidate(path,
                        ApplicationLocationKind.RegistryInstallLocation,
                        "Registry InstallLocation from exact display name") },
                    Array.Empty<DiscoveryIssue>())
                : ApplicationLocationEvidenceResult.Empty;
        }
    }
}
