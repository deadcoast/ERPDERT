using Dert.Cli.Discovery;
using Xunit;

namespace Dert.Tests;

public class CodexDiscoveryTests
{
    [Fact]
    public void Three_package_launchers_are_one_installation_in_PowerShell_order()
    {
        using var fixture = new DiscoveryFixture();
        var prefix = fixture.Package("node tools", "1.2.3");
        var report = fixture.Scan(prefix);
        Assert.Equal(new[] { "codex.ps1", "codex.cmd", "codex" },
            report.Commands.Select(command => Path.GetFileName(command.Path)));
        Assert.Single(report.Installations);
        var package = Assert.IsType<CodexPackage>(report.Commands[0].Package);
        Assert.Equal("1.2.3", package.Version);
        Assert.Equal(Path.Combine(prefix, "node.exe"), package.NodePath);
        Assert.Equal(Path.Combine(prefix, "npm.cmd"), package.NpmPath);
        Assert.False(report.NeedsAttention);
    }

    [Fact]
    public void Earlier_native_match_is_not_skipped_for_a_better_identified_later_package()
    {
        using var fixture = new DiscoveryFixture();
        var first = fixture.Write("manual/codex.exe", "not an executable");
        var prefix = fixture.Package("npm", "1.2.3");
        var report = fixture.Scan(Path.GetDirectoryName(first)!, prefix);
        Assert.Equal(first, report.Commands[0].Path);
        Assert.Null(report.Commands[0].Package);
        Assert.Equal(2, report.Installations.Count);
        Assert.True(report.NeedsAttention);
    }

    [Fact]
    public void Two_versions_keep_their_own_manager_and_dependency_evidence()
    {
        using var fixture = new DiscoveryFixture();
        var first = fixture.Package("first", "1.2.3");
        var second = fixture.Package("second", "2.0.0");
        var report = fixture.Scan(first, second);
        Assert.Equal(2, report.Installations.Count);
        Assert.Equal(new[] { "1.2.3", "2.0.0" }, report.Installations.Select(i => i[0].Package!.Version));
        Assert.Equal(Path.Combine(second, "npm.cmd"), report.Installations[1][0].Package!.NpmPath);
        Assert.True(report.NeedsAttention);
    }

    [Fact]
    public void Repeated_case_insensitive_PATH_entries_do_not_create_conflicts()
    {
        using var fixture = new DiscoveryFixture();
        var prefix = fixture.Package("npm", "1.0.0");
        var report = fixture.Scan(prefix, prefix.ToUpperInvariant(), prefix + Path.DirectorySeparatorChar);
        Assert.Equal(3, report.Commands.Count);
        Assert.Equal(1, report.DirectoryCount);
        Assert.False(report.NeedsAttention);
    }

    [Fact]
    public void PATHEXT_order_is_observed_within_each_PATH_directory()
    {
        using var fixture = new DiscoveryFixture();
        var exe = fixture.Write("first/codex.exe", "exe");
        var cmd = fixture.Write("first/codex.cmd", "cmd");
        var later = fixture.Write("second/codex.ps1", "script");
        var report = CodexDiscovery.Scan(new(
            string.Join(';', Path.GetDirectoryName(exe), Path.GetDirectoryName(later)),
            ".CMD;.EXE;.CMD"));
        Assert.Equal(new[] { cmd, exe, later }, report.Commands.Select(c => c.Path));
    }

    [Fact]
    public void Quoted_PATH_entry_is_not_silently_repaired_into_a_first_match()
    {
        using var fixture = new DiscoveryFixture();
        var first = fixture.Package("quoted", "1.0.0");
        var second = fixture.Package("working", "2.0.0");
        var report = fixture.Scan('"' + first + '"', second);
        Assert.Equal(3, report.Commands.Count);
        Assert.Equal(Path.Combine(second, "codex.ps1"), report.Commands[0].Path);
        Assert.Single(report.Issues);
    }

    [Fact]
    public void A_nearby_package_does_not_identify_an_unrelated_launcher()
    {
        using var fixture = new DiscoveryFixture();
        var prefix = fixture.Package("npm", "1.0.0");
        File.WriteAllText(Path.Combine(prefix, "codex.ps1"), "Write-Output unrelated");
        var report = fixture.Scan(prefix);
        Assert.Null(report.Commands[0].Package);
        Assert.Equal(2, report.Installations.Count);
        Assert.Contains(report.Commands[0].Issues, i => i.Code == "ERP5103");
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("\"text\"")]
    public void Valid_JSON_with_the_wrong_root_type_does_not_crash_discovery(string content)
    {
        using var fixture = new DiscoveryFixture();
        var prefix = fixture.Package("npm", "1.0.0");
        File.WriteAllText(Path.Combine(prefix, "node_modules", "@openai", "codex", "package.json"), content);
        var report = fixture.Scan(prefix);
        Assert.Null(report.Commands[0].Package);
        Assert.Contains(report.Commands[0].Issues, i => i.Code == "ERP5104");
    }

    [Theory]
    [InlineData("version")]
    [InlineData("name")]
    [InlineData("bin")]
    [InlineData("npm")]
    public void An_unpaired_JSON_surrogate_is_a_metadata_finding_not_a_crash(string field)
    {
        using var fixture = new DiscoveryFixture();
        var prefix = fixture.Package("broken", "1.0.0");
        var metadata = Path.Combine(prefix, "node_modules", "@openai", "codex", "package.json");
        if (field == "npm") metadata = Path.Combine(prefix, "node_modules", "npm", "package.json");
        var content = File.ReadAllText(metadata);
        content = field switch
        {
            "version" => content.Replace("1.0.0", "\\uD800"),
            "name" => content.Replace("@openai/codex", "\\uD800"),
            "bin" => content.Replace("bin/codex.js", "\\uD800"),
            _ => content.Replace("\"npm\"", "\"\\uD800\"")
        };
        File.WriteAllText(metadata, content);
        var second = fixture.Package("working", "2.0.0");
        var report = fixture.Scan(prefix, second);
        Assert.Contains(report.Commands[0].Issues, i => i.Code == (field == "npm" ? "ERP5107" : "ERP5104"));
        Assert.Equal("2.0.0", report.Commands.Last().Package!.Version);
    }

    [Theory]
    [InlineData("root")]
    [InlineData("bin")]
    [InlineData("npm")]
    public void Invalid_Unicode_in_JSON_property_names_is_also_contained(string location)
    {
        using var fixture = new DiscoveryFixture();
        var prefix = fixture.Package("broken", "1.0.0");
        var metadata = location == "npm"
            ? Path.Combine(prefix, "node_modules", "npm", "package.json")
            : Path.Combine(prefix, "node_modules", "@openai", "codex", "package.json");
        var content = location switch
        {
            "root" => """{"name":"@openai/codex","version":"1.0.0","bin":{"codex":"bin/codex.js"},"\uD800":"bad"}""",
            "bin" => """{"name":"@openai/codex","version":"1.0.0","bin":{"codex":"bin/codex.js","\uD800":"bad"}}""",
            _ => """{"name":"npm","\uD800":"bad"}"""
        };
        File.WriteAllText(metadata, content);
        var second = fixture.Package("working", "2.0.0");
        var report = fixture.Scan(prefix, second);
        Assert.Contains(report.Commands[0].Issues, i => i.Code == (location == "npm" ? "ERP5107" : "ERP5104"));
        Assert.Equal("2.0.0", report.Commands.Last().Package!.Version);
    }

    [Fact]
    public void A_comment_mentioning_a_package_is_not_a_launcher_reference()
    {
        using var fixture = new DiscoveryFixture();
        var prefix = fixture.Package("npm", "1.0.0");
        File.WriteAllText(Path.Combine(prefix, "codex.ps1"),
            "# & \"node$exe\" \"$basedir/node_modules/@openai/codex/bin/codex.js\" $args\nWrite-Output something-else");
        var report = fixture.Scan(prefix);
        Assert.Null(report.Commands[0].Package);
    }

    [Fact]
    public void Nearby_unrelated_npm_cmd_is_not_silently_treated_as_manager_evidence()
    {
        using var fixture = new DiscoveryFixture();
        var prefix = fixture.Package("npm", "1.0.0");
        File.WriteAllText(Path.Combine(prefix, "npm.cmd"), "@echo unrelated");
        var report = fixture.Scan(prefix);
        Assert.Null(report.Commands[0].Package!.NpmPath);
        Assert.Contains(report.Commands[0].Issues, i => i.Code == "ERP5107");
    }

    [Fact]
    public void A_locked_launcher_does_not_hide_other_accessible_routes()
    {
        using var fixture = new DiscoveryFixture();
        var prefix = fixture.Package("npm", "1.0.0");
        using var held = new FileStream(Path.Combine(prefix, "codex.ps1"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var report = fixture.Scan(prefix);
        Assert.Equal(3, report.Commands.Count);
        Assert.Contains(report.Commands[0].Issues, i => i.Code == "ERP5103");
        Assert.NotNull(report.Commands[1].Package);
    }

    [Fact]
    public void Junction_aliases_of_one_package_do_not_become_competing_installs()
    {
        using var fixture = new DiscoveryFixture();
        var prefix = fixture.Package("real", "1.0.0");
        var alias = Path.Combine(fixture.Root, "alias");
        // Directory symbolic links are not available to all Windows users;
        // a junction can be created without administrator privileges.
        var start = new System.Diagnostics.ProcessStartInfo(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.Arguments = $"/d /c mklink /J \"{alias}\" \"{prefix}\"";
        using var process = System.Diagnostics.Process.Start(start)!;
        Assert.True(process.WaitForExit(10000));
        Assert.Equal(0, process.ExitCode);
        try
        {
            var report = fixture.Scan(alias, prefix);
            Assert.Equal(6, report.Commands.Count);
            Assert.Single(report.Installations);
            Assert.Equal(Path.Combine(alias, "codex.ps1"), report.Commands[0].Path);
            Assert.False(report.NeedsAttention);
        }
        finally { Directory.Delete(alias); }
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("wrong-name")]
    [InlineData("wrong-bin")]
    [InlineData("oversized")]
    public void Missing_or_invalid_package_metadata_is_explained_without_aborting_other_matches(string failure)
    {
        using var fixture = new DiscoveryFixture();
        var prefix = fixture.Package("broken", "1.0.0");
        var metadata = Path.Combine(prefix, "node_modules", "@openai", "codex", "package.json");
        if (failure == "missing") File.Delete(metadata);
        else File.WriteAllText(metadata, failure switch
        {
            "malformed" => "{",
            "wrong-name" => "{\"name\":\"different\",\"version\":\"1\",\"bin\":{\"codex\":\"bin/codex.js\"}}",
            "wrong-bin" => "{\"name\":\"@openai/codex\",\"version\":\"1\",\"bin\":{\"codex\":\"../elsewhere.js\"}}",
            _ => new string('x', 300_000)
        });
        var other = fixture.Package("working", "2.0.0");
        var report = fixture.Scan(prefix, other);
        Assert.Null(report.Commands[0].Package);
        Assert.Contains(report.Commands[0].Issues, i => i.Code == "ERP5104" && i.Path == metadata);
        Assert.Equal("2.0.0", report.Commands.Last().Package!.Version);
    }

    [Fact]
    public void A_missing_entrypoint_is_not_reported_as_a_healthy_installation()
    {
        using var fixture = new DiscoveryFixture();
        var prefix = fixture.Package("npm", "1.0.0");
        var entrypoint = Path.Combine(prefix, "node_modules", "@openai", "codex", "bin", "codex.js");
        File.Delete(entrypoint);
        var report = fixture.Scan(prefix);
        Assert.Contains(report.Commands[0].Issues, i => i.Code == "ERP5105" && i.Path == entrypoint);
        Assert.True(report.NeedsAttention);
    }

    [Fact]
    public void Node_falls_back_to_PATH_without_borrowing_another_installations_npm()
    {
        using var fixture = new DiscoveryFixture();
        var prefix = fixture.Package("npm", "1.0.0");
        File.Delete(Path.Combine(prefix, "node.exe"));
        File.Delete(Path.Combine(prefix, "npm.cmd"));
        var node = fixture.Write("runtime/node.exe", "node");
        fixture.Write("runtime/npm.cmd", "unrelated npm");
        var report = fixture.Scan(prefix, Path.GetDirectoryName(node)!);
        var package = report.Commands[0].Package!;
        Assert.Equal(node, package.NodePath);
        Assert.Null(package.NpmPath);
    }

    [Fact]
    public void Missing_node_is_a_specific_dependency_finding()
    {
        using var fixture = new DiscoveryFixture();
        var prefix = fixture.Package("npm", "1.0.0");
        File.Delete(Path.Combine(prefix, "node.exe"));
        var report = fixture.Scan(prefix);
        Assert.Null(report.Commands[0].Package!.NodePath);
        Assert.Contains(report.Commands[0].Issues, i => i.Code == "ERP5106");
    }

    [Fact]
    public void Empty_PATH_does_not_silently_search_the_working_directory()
    {
        using var fixture = new DiscoveryFixture();
        fixture.Write("codex.exe", "do not discover implicitly");
        var report = fixture.Scan();
        Assert.Empty(report.Commands);
        Assert.True(report.NeedsAttention);
    }

    [Fact]
    public void Directory_named_codex_is_not_an_external_command()
    {
        using var fixture = new DiscoveryFixture();
        Directory.CreateDirectory(Path.Combine(fixture.Root, "codex.ps1"));
        Assert.Empty(fixture.Scan(fixture.Root).Commands);
    }

    [Fact]
    public void Invalid_or_remote_PATH_entries_do_not_hide_later_local_matches()
    {
        using var fixture = new DiscoveryFixture();
        var prefix = fixture.Package("npm", "1.0.0");
        var report = fixture.Scan("bad\0path", "\\\\server\\share", prefix);
        Assert.Equal(3, report.Commands.Count);
        Assert.Equal(2, report.Issues.Count);
        Assert.True(report.NeedsAttention);
    }

    [Fact]
    public void Discovery_reads_but_never_executes_launchers_or_writes_installation_files()
    {
        using var fixture = new DiscoveryFixture();
        var marker = Path.Combine(fixture.Root, "executed.txt");
        var launcher = fixture.Write("bin/codex.ps1", $"[IO.File]::WriteAllText('{marker.Replace("'", "''")}', 'bad')");
        var original = File.ReadAllBytes(launcher);
        fixture.Scan(Path.GetDirectoryName(launcher)!);
        Assert.False(File.Exists(marker));
        Assert.Equal(original, File.ReadAllBytes(launcher));
        Assert.Single(Directory.EnumerateFiles(fixture.Root, "*", SearchOption.AllDirectories));
    }
}

internal sealed class DiscoveryFixture : IDisposable
{
    private readonly ScratchDocument scratch = new("");
    public string Root => scratch.DirectoryPath;

    public DiscoveryFixture() => File.Delete(scratch.Path);

    public string Write(string relative, string content)
    {
        var path = Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    public string Package(string name, string version)
    {
        Write($"{name}/codex.ps1", "& \"$basedir/node$exe\" \"$basedir/node_modules/@openai/codex/bin/codex.js\" $args");
        Write($"{name}/codex.cmd", "@\"%_prog%\" \"%dp0%\\node_modules\\@openai\\codex\\bin\\codex.js\" %*");
        Write($"{name}/codex", "exec node \"$basedir/node_modules/@openai/codex/bin/codex.js\" \"$@\"");
        Write($"{name}/node_modules/@openai/codex/package.json",
            $$"""{"name":"@openai/codex","version":"{{version}}","bin":{"codex":"bin/codex.js"},"packageManager":"pnpm@10"}""");
        Write($"{name}/node_modules/@openai/codex/bin/codex.js", "throw new Error('must not execute');");
        Write($"{name}/node.exe", "must not execute");
        Write($"{name}/npm.cmd", "@\"%_prog%\" \"%dp0%\\node_modules\\npm\\bin\\npm-cli.js\" %*");
        Write($"{name}/node_modules/npm/package.json", "{\"name\":\"npm\",\"version\":\"11.0.0\"}");
        Write($"{name}/node_modules/npm/bin/npm-cli.js", "throw new Error('must not execute');");
        return Path.Combine(Root, name);
    }

    public DiscoveryReport Scan(params string[] directories) => CodexDiscovery.Scan(new(
        string.Join(';', directories), ".COM;.EXE;.BAT;.CMD"));

    public void Dispose() => scratch.Dispose();
}
