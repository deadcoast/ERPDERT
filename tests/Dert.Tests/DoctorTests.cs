using System.Text.Json;
using Dert.Cli.Discovery;
using Dert.Cli.Doctoring;
using Dert.Cli.Operations;
using Xunit;

namespace Dert.Tests;

public class DoctorTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Nvm_style_prefix_alias_is_captured_and_cannot_retarget_a_saved_repair(bool retarget, bool afterUninstall, bool changeManager)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new DoctorFixture();
        var alias = Path.Combine(fixture.Prefix, "..", "active-node");
        var other = fixture.Files.Package("different-node-version", "1.0.0");
        Link(fixture.Prefix);
        try
        {
            var report = DoctorService.Inspect(CodexDiscovery.Scan(new(alias, ".CMD;.EXE")), null, fixture);
            Assert.True(report.State == DoctorState.Ready, string.Join("\n", report.Issues.Select(issue => issue.Explanation)));
            Assert.Equal(fixture.Prefix, report.Proposal!.Prefix);
            var proposals = Path.Combine(fixture.Prefix, "proposals");
            DoctorService.SaveProposal(report, proposals);
            fixture.Calls.Clear();
            if (retarget)
            {
                Directory.Delete(alias);
                Link(other);
                Assert.Throws<IOException>(() => DoctorService.LoadProposal(report.Proposal.Token, proposals));
                Assert.Empty(fixture.Calls);
            }
            else
            {
                var loaded = DoctorService.LoadProposal(report.Proposal.Token, proposals);
                if (afterUninstall) fixture.AfterUninstall = () => { Directory.Delete(alias); Link(other); };
                if (changeManager) fixture.AfterUninstall = () => File.AppendAllText(Path.Combine(fixture.Prefix, "npm.cmd"), "\nrem changed");
                var result = DoctorService.Apply(loaded, loaded.Proposal!.Token, fixture, fixture.Receipts);
                Assert.True(result.State == (afterUninstall || changeManager ? DoctorApplyState.InstallFailed : DoctorApplyState.Succeeded), result.Message);
                if (afterUninstall || changeManager) Assert.Single(fixture.Calls, DoctorFixture.IsMutation);
                Assert.All(fixture.Calls, call => Assert.Equal(fixture.Prefix, call.WorkingDirectory));
            }
        }
        finally { if (Directory.Exists(alias)) Directory.Delete(alias); }

        void Link(string target)
        {
            var start = new System.Diagnostics.ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"))
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "/d", "/c", "mklink", "/J", alias, target }) start.ArgumentList.Add(argument);
            using var child = System.Diagnostics.Process.Start(start)!;
            Assert.True(child.WaitForExit(10000));
            Assert.Equal(0, child.ExitCode);
        }
    }

    [Theory]
    [InlineData("root")]
    [InlineData("ls")]
    public void Cancelled_queries_stop_inspection_and_apply_as_interrupted(string phase)
    {
        using var fixture = new DoctorFixture();
        var discovery = CodexDiscovery.Scan(new(fixture.Prefix, ".CMD;.EXE"));
        fixture.Fault = phase + "-cancelled";
        Assert.Equal(DoctorState.Interrupted, DoctorService.Inspect(discovery, null, fixture).State);
        Assert.Equal(phase, fixture.Calls[^1].Arguments[1]);
        fixture.Fault = null;
        var report = fixture.Inspect();
        fixture.Calls.Clear();
        fixture.Fault = phase + "-cancelled";
        Assert.Equal(DoctorApplyState.Interrupted, DoctorService.Apply(report, report.Proposal!.Token, fixture, fixture.Receipts).State);
        Assert.Equal(phase, fixture.Calls[^1].Arguments[1]);
        Assert.DoesNotContain(fixture.Calls, DoctorFixture.IsMutation);
    }

    [Fact]
    public void Current_version_produces_explicit_reinstall_without_mutation()
    {
        using var fixture = new DoctorFixture();
        var report = fixture.Inspect();
        Assert.Equal(DoctorState.Ready, report.State);
        var plan = Assert.IsType<DoctorProposal>(report.Proposal);
        Assert.Contains("@openai/codex@1.0.0", plan.Install.Arguments);
        Assert.Contains("--ignore-scripts", plan.Install.Arguments);
        Assert.Contains("--include=optional", plan.Install.Arguments);
        Assert.Contains("@openai/codex", plan.Uninstall.Arguments);
        Assert.DoesNotContain(fixture.Calls, DoctorFixture.IsMutation);
        Assert.Equal(plan.Token, fixture.Inspect().Proposal!.Token);
    }

    [Fact]
    public void Distinct_copies_require_explicit_selection_and_only_selected_prefix_is_queried()
    {
        using var fixture = new DoctorFixture();
        var other = fixture.Files.Package("other", "1.0.0");
        var discovery = CodexDiscovery.Scan(new(other + ";" + fixture.Prefix, ".CMD;.EXE"));
        Assert.Equal(DoctorState.ChooseCopy, DoctorService.Inspect(discovery, null, fixture).State);
        Assert.Empty(fixture.Calls);
        var report = DoctorService.Inspect(discovery, fixture.Launcher, fixture);
        Assert.Equal(DoctorState.Ready, report.State);
        Assert.All(fixture.Calls, call => Assert.Equal(fixture.Prefix, call.WorkingDirectory));
    }

    [Fact]
    public void Unknown_manager_and_newer_installed_version_never_offer_uninstall()
    {
        using var fixture = new DoctorFixture { Latest = "0.9.0" };
        Assert.Null(fixture.Inspect().Proposal);
        File.WriteAllText(fixture.Launcher, "unknown launcher");
        Assert.Null(fixture.Inspect().Proposal);
        Assert.DoesNotContain(fixture.Calls, DoctorFixture.IsMutation);
    }

    [Theory]
    [InlineData("token")]
    [InlineData("launcher")]
    [InlineData("node")]
    [InlineData("npm")]
    [InlineData("metadata-bom")]
    [InlineData("source")]
    [InlineData("inventory")]
    public void Changed_evidence_or_wrong_token_refuses_before_uninstall(string change)
    {
        using var fixture = new DoctorFixture();
        var sourcePath = Path.Combine(fixture.Prefix, "request.DERT");
        File.WriteAllText(sourcePath, "STERT ; where ;; codex\ncommands ; doctor ;;\nfin.");
        var report = fixture.Inspect(DoctorSource.Capture(sourcePath));
        Assert.NotNull(report.Proposal);
        var token = report.Proposal.Token;
        if (change == "token") token = new string('0', 64);
        if (change == "launcher") File.AppendAllText(fixture.Launcher, "\n#changed");
        if (change == "node") File.AppendAllText(Path.Combine(fixture.Prefix, "node.exe"), "changed");
        if (change == "npm") File.AppendAllText(Path.Combine(fixture.Prefix, "node_modules", "npm", "bin", "npm-cli.js"), "changed");
        if (change == "metadata-bom") File.WriteAllText(fixture.Metadata, File.ReadAllText(fixture.Metadata), new System.Text.UTF8Encoding(true));
        if (change == "source") File.AppendAllText(sourcePath, "\n// edited");
        if (change == "inventory") fixture.InventoryVersion = "8.0.0";
        var result = DoctorService.Apply(report, token, fixture, fixture.Receipts);
        Assert.Equal(DoctorApplyState.Refused, result.State);
        Assert.DoesNotContain(fixture.Calls, DoctorFixture.IsMutation);
    }

    [Theory]
    [InlineData("uninstall", "UninstallFailed")]
    [InlineData("install", "InstallFailed")]
    [InlineData("post-missing", "VerificationFailed")]
    [InlineData("post-inventory", "VerificationFailed")]
    [InlineData("uninstall-timeout", "UninstallFailed")]
    [InlineData("install-timeout", "InstallFailed")]
    [InlineData("uninstall-cancelled", "Interrupted")]
    public void Failure_stops_later_phases_and_retains_durable_recovery(string fault, string expected)
    {
        using var fixture = new DoctorFixture();
        var report = fixture.Inspect();
        fixture.Fault = fault;
        var result = DoctorService.Apply(report, report.Proposal!.Token, fixture, fixture.Receipts);
        Assert.Equal(expected, result.State.ToString());
        Assert.NotNull(result.ReceiptPath);
        Assert.True(File.Exists(result.ReceiptPath));
        Assert.Contains("@openai/codex@1.0.0", result.Recovery!.Arguments);
        if (fault.StartsWith("uninstall", StringComparison.Ordinal)) Assert.Single(fixture.Calls, DoctorFixture.IsMutation);
        Assert.Contains("uninstall-started", File.ReadAllText(result.ReceiptPath));
        Assert.DoesNotContain("\"state\":\"Succeeded\"", File.ReadAllText(result.ReceiptPath));
    }

    [Fact]
    public void Successful_repair_requires_inventory_and_entrypoint_after_both_mutations()
    {
        using var fixture = new DoctorFixture();
        var report = fixture.Inspect();
        var result = DoctorService.Apply(report, report.Proposal!.Token, fixture, fixture.Receipts);
        Assert.True(result.State == DoctorApplyState.Succeeded, result.Message);
        Assert.Equal(new[] { "uninstall", "install" }, fixture.Calls.Where(DoctorFixture.IsMutation).Select(c => c.Arguments[1]));
        Assert.Equal("ls", fixture.Calls[^1].Arguments[1]);
        Assert.Contains("verified", File.ReadAllText(result.ReceiptPath!));
        Assert.All(fixture.Calls.Where(DoctorFixture.IsMutation), request =>
        {
            Assert.Equal(Path.Combine(fixture.Prefix, "node.exe"), request.Program);
            Assert.Contains(fixture.Prefix, request.Arguments);
            Assert.Contains("--ignore-scripts", request.Arguments);
        });
    }

    [Fact]
    public void Receipt_failure_and_cancellation_prevent_mutation()
    {
        using var fixture = new DoctorFixture();
        var report = fixture.Inspect();
        var blocked = Path.Combine(fixture.Prefix, "file-not-folder");
        File.WriteAllText(blocked, "owned test input");
        Assert.Equal(DoctorApplyState.ReceiptFailed, DoctorService.Apply(report, report.Proposal!.Token, fixture, blocked).State);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Equal(DoctorApplyState.Interrupted, DoctorService.Apply(report, report.Proposal.Token, fixture, fixture.Receipts, cancellation.Token).State);
        Assert.DoesNotContain(fixture.Calls, DoctorFixture.IsMutation);
    }

    [Fact]
    public void Changed_manager_during_inspection_prevents_the_next_query()
    {
        using var fixture = new DoctorFixture { Fault = "change-npm-during-root" };
        Assert.Null(fixture.Inspect().Proposal);
        Assert.Single(fixture.Calls);
    }

    [Theory]
    [InlineData("uninstall-noop", "UninstallFailed")]
    [InlineData("change-node-after-uninstall", "InstallFailed")]
    [InlineData("cancel-after-uninstall", "Interrupted")]
    public void Successful_uninstall_exit_does_not_override_guards_between_phases(string fault, string expected)
    {
        using var fixture = new DoctorFixture();
        var report = fixture.Inspect();
        fixture.Fault = fault;
        using var cancellation = new CancellationTokenSource();
        fixture.AfterUninstall = () => { if (fault == "cancel-after-uninstall") cancellation.Cancel(); };
        var result = DoctorService.Apply(report, report.Proposal!.Token, fixture, fixture.Receipts, cancellation.Token);
        Assert.Equal(expected, result.State.ToString());
        Assert.Single(fixture.Calls, DoctorFixture.IsMutation);
        Assert.NotNull(result.Recovery);
    }

    [Fact]
    public void An_unrelated_launcher_slot_cannot_be_overwritten_by_repair()
    {
        using var fixture = new DoctorFixture();
        File.WriteAllText(Path.Combine(fixture.Prefix, "codex.cmd"), "an unrelated custom command");
        var discovery = CodexDiscovery.Scan(new(fixture.Prefix, ".CMD;.EXE"));
        var report = DoctorService.Inspect(discovery, fixture.Launcher, fixture);
        Assert.Null(report.Proposal);
        Assert.Empty(fixture.Calls);
    }

    [Fact]
    public void Saved_approval_loads_without_queries_and_changed_manager_blocks_it_before_execution()
    {
        using var fixture = new DoctorFixture();
        var report = fixture.Inspect();
        var directory = Path.Combine(fixture.Prefix, "proposals");
        DoctorService.SaveProposal(report, directory);
        fixture.Calls.Clear();
        var saved = DoctorService.LoadProposal(report.Proposal!.Token, directory);
        Assert.Empty(fixture.Calls);
        Assert.Equal(report.Proposal.Token, saved.Proposal!.Token);
        File.AppendAllText(Path.Combine(fixture.Prefix, "node.exe"), "changed");
        Assert.Throws<IOException>(() => DoctorService.LoadProposal(report.Proposal.Token, directory));
        Assert.Empty(fixture.Calls);
    }

    [Fact]
    public void Saved_approval_rejects_changed_commands_and_can_apply_the_original_review()
    {
        using var fixture = new DoctorFixture();
        var report = fixture.Inspect();
        var directory = Path.Combine(fixture.Prefix, "proposals");
        var path = DoctorService.SaveProposal(report, directory);
        var bytes = File.ReadAllBytes(path);
        File.WriteAllText(path, File.ReadAllText(path).Replace("--ignore-scripts", "--allow-scripts"));
        Assert.Throws<IOException>(() => DoctorService.LoadProposal(report.Proposal!.Token, directory));
        File.WriteAllBytes(path, bytes);
        var loaded = DoctorService.LoadProposal(report.Proposal!.Token, directory);
        var result = DoctorService.Apply(loaded, loaded.Proposal!.Token, fixture, fixture.Receipts);
        Assert.True(result.State == DoctorApplyState.Succeeded, result.Message);
    }

    [Fact]
    public void A_redirected_package_parent_after_uninstall_prevents_installing_outside_the_reviewed_prefix()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new DoctorFixture();
        var report = fixture.Inspect();
        var scope = Path.Combine(fixture.Prefix, "node_modules", "@openai");
        var elsewhere = Path.Combine(fixture.Prefix, "unrelated-data");
        Directory.CreateDirectory(elsewhere);
        fixture.AfterUninstall = () =>
        {
            Directory.Delete(scope);
            var start = new System.Diagnostics.ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"))
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add("/d"); start.ArgumentList.Add("/c");
            start.ArgumentList.Add("mklink"); start.ArgumentList.Add("/J"); start.ArgumentList.Add(scope); start.ArgumentList.Add(elsewhere);
            using var process = System.Diagnostics.Process.Start(start)!;
            Assert.True(process.WaitForExit(10000));
            Assert.Equal(0, process.ExitCode);
        };
        try
        {
            var result = DoctorService.Apply(report, report.Proposal!.Token, fixture, fixture.Receipts);
            Assert.Equal(DoctorApplyState.InstallFailed, result.State);
            Assert.Single(fixture.Calls, DoctorFixture.IsMutation);
            Assert.Empty(Directory.GetFileSystemEntries(elsewhere));
        }
        finally { if ((File.GetAttributes(scope) & FileAttributes.ReparsePoint) != 0) Directory.Delete(scope); }
    }

    [Fact]
    public void Concurrent_doctor_apply_cannot_start_a_second_uninstall_for_the_same_prefix()
    {
        using var fixture = new DoctorFixture();
        var report = fixture.Inspect();
        DoctorApplyResult? concurrent = null;
        fixture.BeforeUninstall = () =>
        {
            fixture.BeforeUninstall = null;
            concurrent = DoctorService.Apply(report, report.Proposal!.Token, fixture, fixture.Receipts);
        };
        var result = DoctorService.Apply(report, report.Proposal!.Token, fixture, fixture.Receipts);
        Assert.True(result.State == DoctorApplyState.Succeeded, result.Message);
        Assert.Equal(DoctorApplyState.ReceiptFailed, concurrent!.State);
        Assert.Equal(2, fixture.Calls.Count(DoctorFixture.IsMutation));
    }

    [Fact]
    public void Identified_package_with_missing_entrypoint_can_be_repaired_after_review()
    {
        using var fixture = new DoctorFixture();
        var entrypoint = Path.Combine(Path.GetDirectoryName(fixture.Metadata)!, "bin", "codex.js");
        File.Delete(entrypoint);
        var report = fixture.Inspect();
        Assert.Equal(DoctorState.Ready, report.State);
        Assert.Contains(report.Update!.Selected!.Issues, issue => issue.Code == "ERP5105");
        Assert.False(File.Exists(entrypoint));
        var result = DoctorService.Apply(report, report.Proposal!.Token, fixture, fixture.Receipts);
        Assert.True(result.State == DoctorApplyState.Succeeded, result.Message);
        Assert.True(File.Exists(entrypoint));
    }
}

internal sealed class DoctorFixture : ICommandRunner, IDisposable
{
    public DiscoveryFixture Files { get; } = new();
    public string Prefix { get; }
    public string Launcher => Path.Combine(Prefix, "codex.ps1");
    public string Metadata => Path.Combine(Prefix, "node_modules", "@openai", "codex", "package.json");
    public string Receipts { get; set; }
    public string Latest { get; init; } = "1.0.0";
    public string InventoryVersion { get; set; } = "1.0.0";
    public string? Fault { get; set; }
    public Action? AfterUninstall { get; set; }
    public Action? BeforeUninstall { get; set; }
    public List<CommandRequest> Calls { get; } = [];
    private bool installed;
    public DoctorFixture() { Prefix = Files.Package("doctor's prefix", "1.0.0"); Receipts = Path.Combine(Prefix, "receipts"); }
    public DoctorReport Inspect(DoctorSource? source = null) => DoctorService.Inspect(CodexDiscovery.Scan(new(Prefix, ".CMD;.EXE")), null, this, source: source);
    public static bool IsMutation(CommandRequest call) => call.Arguments[1] is "uninstall" or "install" && !call.Arguments.Contains("--dry-run");
    public CommandResult Run(CommandRequest request)
    {
        Calls.Add(request);
        var command = request.Arguments[1];
        if (Fault == command + "-cancelled") return new(130, "", "", "The command was cancelled.");
        if (command == "root")
        {
            if (Fault == "change-npm-during-root") File.AppendAllText(Path.Combine(Prefix, "node_modules", "npm", "bin", "npm-cli.js"), "changed");
            return new(0, Path.Combine(Prefix, "node_modules"), "");
        }
        if (command == "ls") return new(0, JsonSerializer.Serialize(new { path = Prefix, dependencies = new Dictionary<string, object> { ["@openai/codex"] = new { name = "@openai/codex", version = installed && Fault == "post-inventory" ? "9.0.0" : InventoryVersion, path = Path.GetDirectoryName(Metadata) } } }), "");
        if (command == "view") return new(0, JsonSerializer.Serialize(new { version = Latest, engines = new { node = ">=18" } }), "");
        if (request.Arguments.Contains("--dry-run")) return new(0, "{\"added\":0,\"removed\":0,\"changed\":0}", "");
        if (command is "uninstall" or "install")
        {
            // The real transaction must flush its receipt before this external boundary.
            Assert.Contains(Directory.GetFiles(Receipts, "*.jsonl"), path => ReadActiveReceipt(path).Contains(command + "-started"));
            if (Fault == command) return new(1, "", "simulated npm failure");
            if (Fault == command + "-timeout") return new(-1, "", "", "The command exceeded its time limit.");
            if (Fault == command + "-cancelled") return new(130, "", "", "The command was cancelled.");
            if (command == "uninstall")
            {
                BeforeUninstall?.Invoke();
                if (Fault != "uninstall-noop") Directory.Delete(Path.GetDirectoryName(Metadata)!, recursive: true);
                if (Fault == "change-node-after-uninstall") File.AppendAllText(Path.Combine(Prefix, "node.exe"), "changed");
                AfterUninstall?.Invoke();
            }
            if (command == "install")
            {
                installed = true;
                Files.Package("doctor's prefix", Latest);
                if (Fault == "post-missing") File.Delete(Path.Combine(Path.GetDirectoryName(Metadata)!, "bin", "codex.js"));
            }
            return new(0, "{}", "");
        }
        throw new InvalidOperationException("Unexpected external request: " + command);
    }
    public void Dispose() => Files.Dispose();
    private static string ReadActiveReceipt(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
