using System.Text.Json;
using Dert.Cli.Operations;
using Xunit;

namespace Dert.Tests;

public class UpdateRehearsalTests
{
    [Fact]
    public void Narrow_output_keeps_target_evidence_on_one_copyable_logical_line()
    {
        using var fixture = new UpdateFixture();
        var (exit, output) = UpdateCliTests.Invoke(["update", "codex", "updateme", "--plain"], fixture.Prefix, fixture, width: 40);
        Assert.Equal(0, exit);
        Assert.Contains("✓ TARGET FOLDER" + Environment.NewLine + "  " + fixture.Prefix + Environment.NewLine, output);
    }

    [Fact]
    public void One_proposed_change_is_described_as_singular()
    {
        using var fixture = new UpdateFixture();
        var (exit, output) = fixture.Run();
        Assert.Equal(0, exit);
        Assert.Contains("1 change proposed", output);
    }

    [Fact]
    public void Npm_dry_run_change_lines_before_json_are_retained_as_visible_proposed_changes()
    {
        using var fixture = new UpdateFixture { Fault = "dry-run-preamble" };
        var (exit, output) = fixture.Run();
        Assert.Equal(0, exit);
        Assert.Contains("REHEARSED / NOT APPLIED", output);
        Assert.Contains("NPM / PROPOSED CHANGES", output);
        Assert.Contains("@openai/codex-win32-x64", output);
    }

    [Theory]
    [InlineData("unexpected-preamble")]
    [InlineData("trailing-output")]
    public void Unexpected_text_around_dry_run_json_is_not_silently_discarded(string fault)
    {
        using var fixture = new UpdateFixture { Fault = fault };
        var (exit, output) = fixture.Run();
        Assert.Equal(1, exit);
        Assert.Contains("ERP5207", output);
        Assert.DoesNotContain("REHEARSED / NOT APPLIED", output);
    }

    [Fact]
    public void Npm_12_singleton_array_release_response_is_supported()
    {
        using var fixture = new UpdateFixture { Fault = "array-result" };
        var (exit, output) = fixture.Run();
        Assert.Equal(0, exit);
        Assert.Contains("REHEARSED / NOT APPLIED", output);
        Assert.Contains("@openai/codex@2.0.0", output);
    }

    [Theory]
    [InlineData("empty-array")]
    [InlineData("multiple-results")]
    public void Registry_response_must_identify_exactly_one_release(string fault)
    {
        using var fixture = new UpdateFixture { Fault = fault };
        var (exit, output) = fixture.Run();
        Assert.Equal(1, exit);
        Assert.DoesNotContain(fixture.Calls, c => c.Arguments.Contains("install"));
        Assert.DoesNotContain("MANUAL UPDATE / CHANGES PACKAGES", output);
    }

    [Fact]
    public void Confirmed_inventory_and_newer_release_produce_a_pinned_targeted_command()
    {
        using var fixture = new UpdateFixture();
        var (exit, output) = fixture.Run();
        Assert.Equal(0, exit);
        Assert.Contains("1.0.0 → 2.0.0", output);
        Assert.Contains("REHEARSED / NOT APPLIED", output);
        Assert.Contains("@openai/codex@2.0.0", output);
        Assert.Contains("--engine-strict", output);
        Assert.Contains("--include=optional", output);
        Assert.Contains("--prefix", output);
        Assert.Contains(fixture.Prefix.Replace("'", "''"), output);
        Assert.Equal(new[] { "root", "ls", "view", "install" }, fixture.Calls.Select(c => c.Arguments[1]));
        Assert.All(fixture.Calls, c =>
        {
            Assert.Equal(Path.Combine(fixture.Prefix, "node.exe"), c.Program);
            Assert.Equal(Path.Combine(fixture.Prefix, "node_modules", "npm", "bin", "npm-cli.js"), c.Arguments[0]);
            Assert.Equal(fixture.Prefix, c.WorkingDirectory);
            Assert.Contains(fixture.Prefix, c.Arguments);
        });
        var install = fixture.Calls[^1];
        Assert.Contains("--dry-run", install.Arguments);
        Assert.Contains("--ignore-scripts", install.Arguments);
        Assert.Contains("--force=false", install.Arguments);
        Assert.Equal(fixture.Original, File.ReadAllText(fixture.Metadata));
    }

    [Theory]
    [InlineData("1.0.0", "ALREADY CURRENT", 0)]
    [InlineData("0.9.0", "NEWER THAN LATEST", 1)]
    public void Current_or_ahead_versions_do_not_rehearse_an_install(string latest, string state, int expectedExit)
    {
        using var fixture = new UpdateFixture { Latest = latest };
        var (exit, output) = fixture.Run();
        Assert.Equal(expectedExit, exit);
        Assert.Contains(state, output);
        Assert.DoesNotContain(fixture.Calls, c => c.Arguments.Contains("install"));
        Assert.DoesNotContain("'install'", output);
    }

    [Theory]
    [InlineData("root")]
    [InlineData("inventory-path")]
    [InlineData("inventory-version")]
    [InlineData("inventory-error")]
    [InlineData("inventory-missing")]
    public void Mismatched_manager_inventory_prevents_a_registry_query(string fault)
    {
        using var fixture = new UpdateFixture { Fault = fault };
        var (exit, output) = fixture.Run();
        Assert.Equal(1, exit);
        Assert.Contains("ERP520", output);
        Assert.DoesNotContain(fixture.Calls, c => c.Arguments.Contains("view"));
        Assert.DoesNotContain(" install ", output);
    }

    [Theory]
    [InlineData("2.0.0-beta.1")]
    [InlineData("2.0.0;whoami")]
    [InlineData("02.0.0")]
    [InlineData("not-a-version")]
    public void Unsupported_release_versions_cannot_become_shell_arguments(string latest)
    {
        using var fixture = new UpdateFixture { Latest = latest };
        var (exit, output) = fixture.Run();
        Assert.Equal(1, exit);
        Assert.DoesNotContain(fixture.Calls, c => c.Arguments.Contains("install"));
        Assert.DoesNotContain(" install ", output);
    }

    [Theory]
    [InlineData("timeout")]
    [InlineData("invalid-json")]
    [InlineData("invalid-unicode")]
    [InlineData("engine-failure")]
    [InlineData("dry-run-json")]
    public void Failed_external_checks_do_not_produce_a_success_or_an_install_command(string fault)
    {
        using var fixture = new UpdateFixture { Fault = fault };
        var (exit, output) = fixture.Run();
        Assert.Equal(1, exit);
        Assert.Contains("ERP520", output);
        Assert.DoesNotContain("REHEARSED / NOT APPLIED", output);
        Assert.DoesNotContain("MANUAL UPDATE / CHANGES PACKAGES", output);
    }

    [Fact]
    public void A_package_changed_during_queries_invalidates_the_rehearsal()
    {
        using var fixture = new UpdateFixture { Fault = "changed-package" };
        var (exit, output) = fixture.Run();
        Assert.Equal(1, exit);
        Assert.Contains("changed", output);
        Assert.DoesNotContain("MANUAL UPDATE / CHANGES PACKAGES", output);
    }

    [Fact]
    public void Dert_runs_the_same_rehearsal_and_attributes_the_request_to_its_statement()
    {
        using var fixture = new UpdateFixture();
        using var file = new ScratchDocument("STERT ; update ;; codex\n// keep my reasoning\ncommands ; updateme ;;\nƒin.");
        var (exit, output) = UpdateCliTests.Invoke(["run", file.Path, "--plain"], fixture.Prefix, fixture);
        Assert.Equal(0, exit);
        Assert.Contains("keep my reasoning", output);
        Assert.Contains($"FROM {file.Path}:3", output);
        Assert.Contains("REHEARSED / NOT APPLIED", output);
        Assert.DoesNotContain("No discovered program was launched", output);
        Assert.Single(output.Split('\n'), line => line.TrimEnd('\r') == "ƒin.");
        Assert.Single(Directory.GetFiles(file.DirectoryPath));
    }

    [Fact]
    public void Explicit_selection_never_queries_the_other_installation()
    {
        using var fixture = new UpdateFixture();
        var other = fixture.Files.Package("other", "8.0.0");
        var (exit, output) = UpdateCliTests.Invoke(["update", "codex", "updateme", "--path", Path.Combine(fixture.Prefix, "codex.ps1"), "--plain"], other + ";" + fixture.Prefix, fixture);
        Assert.Equal(0, exit);
        Assert.Contains("1.0.0 → 2.0.0", output);
        Assert.All(fixture.Calls, call => Assert.Equal(fixture.Prefix, call.WorkingDirectory));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Equivalent_absolute_target_spellings_select_the_same_copy(bool forwardSlashes)
    {
        using var fixture = new UpdateFixture();
        var selected = Path.Combine(fixture.Prefix, ".", "codex.ps1");
        if (forwardSlashes) selected = selected.Replace('\\', '/');
        var (exit, output) = UpdateCliTests.Invoke(["update", "codex", "updateme", "--path", selected, "--plain"], fixture.Prefix, fixture);
        Assert.Equal(0, exit);
        Assert.Contains("REHEARSED / NOT APPLIED", output);
    }

    [Fact]
    public void Rehearsing_an_identified_broken_copy_does_not_claim_it_is_healthy()
    {
        using var fixture = new UpdateFixture();
        File.Delete(Path.Combine(fixture.Prefix, "node_modules", "@openai", "codex", "bin", "codex.js"));
        var (exit, output) = fixture.Run();
        Assert.Equal(1, exit);
        Assert.Contains("ERP5105", output);
        Assert.Contains("REHEARSED / NOT APPLIED", output);
    }
}

// Only the external npm process is substituted. Discovery, package files, DERT,
// planning, comparison, rendering and argument construction are the real code.
internal sealed class UpdateFixture : ICommandRunner, IDisposable
{
    public DiscoveryFixture Files { get; } = new();
    public string Prefix { get; }
    public string Metadata { get; }
    public string Original { get; }
    public string Latest { get; init; } = "2.0.0";
    public string? Fault { get; init; }
    public List<CommandRequest> Calls { get; } = [];
    public UpdateFixture()
    {
        Prefix = Files.Package("Ryan's node", "1.0.0");
        Metadata = Path.Combine(Prefix, "node_modules", "@openai", "codex", "package.json");
        Original = File.ReadAllText(Metadata);
    }
    public (int Exit, string Output) Run() => UpdateCliTests.Invoke(["update", "codex", "updateme", "--plain"], Prefix, this);
    public CommandResult Run(CommandRequest request)
    {
        Calls.Add(request);
        var command = request.Arguments[1];
        if (Fault == "timeout") return new(-1, "", "", "The command exceeded its time limit.");
        if (command == "root") return new(0, Path.Combine(Prefix, Fault == "root" ? "elsewhere" : "node_modules") + "\n", "");
        if (command == "ls")
        {
            if (Fault == "inventory-error") return new(1, "{}", "npm error invalid package");
            if (Fault == "inventory-missing") return new(0, "{\"dependencies\":{}}", "");
            var dependency = new Dictionary<string, object>
            {
                ["@openai/codex"] = new
                {
                    name = "@openai/codex",
                    version = Fault == "inventory-version" ? "9.0.0" : "1.0.0",
                    path = Fault == "inventory-path" ? Path.Combine(Prefix, "other") : Path.GetDirectoryName(Metadata),
                    extraneous = false,
                    overridden = false
                }
            };
            return new(0, JsonSerializer.Serialize(new { path = Prefix, dependencies = dependency }), "");
        }
        if (command == "view")
        {
            if (Fault == "invalid-json") return new(0, "{", "");
            if (Fault == "invalid-unicode") return new(0, "{\"version\":\"\\uD800\"}", "");
            var release = JsonSerializer.Serialize(new { version = Latest, engines = new { node = ">=16" } });
            return new(0, Fault switch { "array-result" => "[" + release + "]", "empty-array" => "[]", "multiple-results" => "[" + release + "," + release + "]", _ => release }, "");
        }
        if (command == "install")
        {
            if (!request.Arguments.Contains("--dry-run") || !request.Arguments.Contains("--ignore-scripts"))
                throw new InvalidOperationException("A rehearsal attempted a real install or allowed scripts.");
            if (Fault == "changed-package") File.WriteAllText(Metadata, Original.Replace("1.0.0", "1.0.1"));
            if (Fault == "engine-failure") return new(1, "{}", "npm error EBADENGINE Unsupported engine");
            if (Fault == "dry-run-json") return new(0, "{}", "");
            var summary = "{\"added\":0,\"removed\":0,\"changed\":1,\"audited\":0,\"funding\":0}";
            if (Fault == "dry-run-preamble") summary = "change @openai/codex 1.0.0 => 2.0.0\r\nadd @openai/codex-win32-x64 2.0.0-win32-x64\r\n" + summary;
            if (Fault == "unexpected-preamble") summary = "something unexpected\n" + summary;
            if (Fault == "trailing-output") summary += "\nanother unexpected result";
            return new(0, summary, "");
        }
        throw new InvalidOperationException("Unexpected process request: " + command);
    }
    public void Dispose() => Files.Dispose();
}
