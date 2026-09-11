using Dert.Cli.Discovery;
using Dert.Cli.Doctoring;
using Dert.Cli.Operations;
using Dert.Cli.Transactions;
using Dert.Cli.Updates;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Dert.Tests;

public class UpdateTransactionTests
{
    [Fact]
    public void An_approved_update_performs_one_pinned_install_and_verifies_the_result_without_uninstalling()
    {
        using var fixture = new TransactionFixture();
        var report = fixture.Inspect();
        Assert.Equal(UpdateState.Ready, report.State);
        Assert.NotNull(report.Proposal);
        Assert.Null(report.Proposal.Uninstall);
        Assert.Equal(NpmOperation.Update, report.Proposal.Operation);
        Assert.DoesNotContain(fixture.Calls, DoctorFixture.IsMutation);
        fixture.Calls.Clear();
        var result = fixture.Apply(report);
        Assert.True(result.State == NpmApplyState.Succeeded, result.Message);
        var mutation = Assert.Single(fixture.Calls, DoctorFixture.IsMutation);
        Assert.Equal("install", mutation.Arguments[1]);
        foreach (var argument in new[] { "@openai/codex@2.0.0", "--ignore-scripts", "--dry-run=false", "--engine-strict", "--force=false", "--include=optional" })
            Assert.Contains(argument, mutation.Arguments);
        Assert.Equal(new[] { "root", "ls", "install", "ls" }, fixture.Calls.Select(call => call.Arguments[1]));
        Assert.Null(result.Recovery);
        Assert.Contains("\"Operation\":0", File.ReadAllText(result.ReceiptPath!));
        Assert.Contains("verified", File.ReadAllText(result.ReceiptPath!));
        fixture.Calls.Clear();
        Assert.Equal(NpmApplyState.Refused, fixture.Apply(report).State);
        Assert.Empty(fixture.Calls);
    }

    [Fact]
    public void Doctor_and_update_approvals_cannot_authorize_each_other_even_in_the_same_proposal_folder()
    {
        using var fixture = new TransactionFixture();
        var update = fixture.Inspect();
        var doctor = DoctorService.Inspect(fixture.Discovery(), null, fixture);
        Assert.NotEqual(update.Proposal!.Token, doctor.Proposal!.Token);
        var directory = Path.Combine(fixture.Prefix, "proposals");
        UpdateTransaction.SaveProposal(update, directory);
        DoctorService.SaveProposal(doctor, directory);
        fixture.Calls.Clear();
        Assert.Throws<IOException>(() => UpdateTransaction.LoadProposal(doctor.Proposal.Token, directory));
        Assert.Throws<IOException>(() => DoctorService.LoadProposal(update.Proposal.Token, directory));
        Assert.Equal(NpmApplyState.Refused, UpdateTransaction.Apply(update, doctor.Proposal.Token, fixture, fixture.Receipts).State);
        Assert.Equal(DoctorApplyState.Refused, DoctorService.Apply(doctor, update.Proposal.Token, fixture, fixture.Receipts).State);
        Assert.Empty(fixture.Calls);
    }

    [Fact]
    public void Saved_update_retains_the_source_and_exact_request_without_querying_again()
    {
        using var fixture = new TransactionFixture();
        var sourcePath = Path.Combine(fixture.Prefix, "request.DERT");
        File.WriteAllText(sourcePath, "STERT ; update ;; codex\ncommands ; updateme ;;\nfin.");
        var report = fixture.Inspect(NpmSource.Capture(sourcePath));
        var directory = Path.Combine(fixture.Prefix, "proposals");
        var path = UpdateTransaction.SaveProposal(report, directory);
        Assert.Equal(path, report.ProposalPath);
        fixture.Calls.Clear();
        var loaded = UpdateTransaction.LoadProposal(report.Proposal!.Token, directory);
        Assert.Equal(path, loaded.ProposalPath);
        Assert.Equal(sourcePath, loaded.Proposal!.Source!.Path);
        Assert.Equal(report.Proposal.Install.Arguments, loaded.Proposal.Install.Arguments);
        Assert.Empty(fixture.Calls);
        File.AppendAllText(sourcePath, "\n// Changed reasoning.");
        Assert.Throws<IOException>(() => UpdateTransaction.LoadProposal(report.Proposal.Token, directory));
        Assert.Equal(NpmApplyState.Refused, fixture.Apply(loaded).State);
        Assert.Empty(fixture.Calls);
    }

    [Theory]
    [InlineData("launcher")]
    [InlineData("manager")]
    [InlineData("node")]
    [InlineData("metadata")]
    [InlineData("entrypoint")]
    [InlineData("absent-slot")]
    public void Changes_to_any_reviewed_input_refuse_before_manager_execution(string change)
    {
        using var fixture = new TransactionFixture();
        var report = fixture.Inspect();
        var path = change switch
        {
            "launcher" => fixture.Doctor.Launcher,
            "manager" => Path.Combine(fixture.Prefix, "node_modules", "npm", "bin", "npm-cli.js"),
            "node" => Path.Combine(fixture.Prefix, "node.exe"),
            "metadata" => fixture.Doctor.Metadata,
            "entrypoint" => Path.Combine(Path.GetDirectoryName(fixture.Doctor.Metadata)!, "bin", "codex.js"),
            _ => Path.Combine(fixture.Prefix, "codex.cmd")
        };
        File.AppendAllText(path, "changed evidence");
        fixture.Calls.Clear();
        Assert.Equal(NpmApplyState.Refused, fixture.Apply(report).State);
        Assert.Empty(fixture.Calls);
    }

    [Theory]
    [InlineData("install", "InstallFailed")]
    [InlineData("install-timeout", "InstallFailed")]
    [InlineData("install-cancelled", "Interrupted")]
    [InlineData("post-missing", "VerificationFailed")]
    [InlineData("post-inventory", "VerificationFailed")]
    [InlineData("install-noop", "VerificationFailed")]
    public void Failed_or_unverified_installs_keep_the_durable_partial_result_and_recovery(string fault, string expected)
    {
        using var fixture = new TransactionFixture();
        var report = fixture.Inspect();
        fixture.Doctor.Fault = fault;
        fixture.Calls.Clear();
        var result = fixture.Apply(report);
        Assert.True(result.State.ToString() == expected, result.Message);
        Assert.Single(fixture.Calls, DoctorFixture.IsMutation);
        Assert.NotNull(result.Recovery);
        Assert.Contains("@openai/codex@2.0.0", result.Recovery.Arguments);
        Assert.NotNull(result.ReceiptPath);
        Assert.Contains("install-started", File.ReadAllText(result.ReceiptPath));
        Assert.DoesNotContain("\"state\":\"Succeeded\"", File.ReadAllText(result.ReceiptPath));
    }

    [Fact]
    public void A_failed_receipt_or_cancelled_approval_starts_no_package_change()
    {
        using var fixture = new TransactionFixture();
        var report = fixture.Inspect();
        var blocked = Path.Combine(fixture.Prefix, "not-a-directory");
        File.WriteAllText(blocked, "a file");
        Assert.Equal(NpmApplyState.ReceiptFailed, UpdateTransaction.Apply(report, report.Proposal!.Token, fixture, blocked).State);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Equal(NpmApplyState.Interrupted, fixture.Apply(report, cancellation.Token).State);
        Assert.DoesNotContain(fixture.Calls, DoctorFixture.IsMutation);
    }

    [Fact]
    public void Doctor_and_update_share_one_installation_lock_even_with_different_receipt_folders()
    {
        using var fixture = new TransactionFixture();
        var update = fixture.Inspect();
        var doctor = DoctorService.Inspect(fixture.Discovery(), null, fixture);
        NpmApplyResult? concurrent = null;
        fixture.Doctor.BeforeUninstall = () =>
        {
            fixture.Doctor.BeforeUninstall = null;
            var before = fixture.Calls.Count;
            concurrent = UpdateTransaction.Apply(update, update.Proposal!.Token, fixture, Path.Combine(fixture.Prefix, "update-receipts"));
            Assert.Equal(before, fixture.Calls.Count);
        };
        var result = DoctorService.Apply(doctor, doctor.Proposal!.Token, fixture, fixture.Receipts);
        Assert.True(result.State == DoctorApplyState.Succeeded, result.Message);
        Assert.Equal(NpmApplyState.ReceiptFailed, concurrent!.State);
        Assert.Equal(2, fixture.Calls.Count(DoctorFixture.IsMutation));
    }

    [Fact]
    public void Changed_source_after_install_is_verification_failure_and_never_reuses_earlier_success()
    {
        using var fixture = new TransactionFixture();
        var sourcePath = Path.Combine(fixture.Prefix, "request.DERT");
        File.WriteAllText(sourcePath, "STERT ; update ;; codex\ncommands ; updateme ;;\nfin.");
        var report = fixture.Inspect(NpmSource.Capture(sourcePath));
        fixture.AfterInstall = () => File.AppendAllText(sourcePath, "\n// edited");
        var result = fixture.Apply(report);
        Assert.Equal(NpmApplyState.VerificationFailed, result.State);
        Assert.NotNull(result.Recovery);
    }

    [Theory]
    [InlineData("1.0.0", "Current")]
    [InlineData("0.9.0", "Ahead")]
    public void Current_or_ahead_versions_offer_no_update_transaction(string latest, string expected)
    {
        using var fixture = new DoctorFixture { Latest = latest };
        var report = UpdateTransaction.Inspect(CodexDiscovery.Scan(new(fixture.Prefix, ".CMD;.EXE")), null, fixture);
        Assert.Equal(expected, report.State.ToString());
        Assert.Null(report.Proposal);
        Assert.Throws<IOException>(() => UpdateTransaction.SaveProposal(report, fixture.Receipts));
        Assert.DoesNotContain(fixture.Calls, DoctorFixture.IsMutation);
    }

    [Theory]
    [InlineData("metadata")]
    [InlineData("entrypoint")]
    [InlineData("launcher")]
    [InlineData("new-slot")]
    public void Package_or_launcher_changes_during_final_inventory_invalidate_verification(string change)
    {
        using var fixture = new TransactionFixture();
        var report = fixture.Inspect();
        fixture.AfterRequest = request =>
        {
            if (request.Arguments[1] != "ls" || fixture.Doctor.InventoryVersion != "2.0.0") return;
            var path = change switch
            {
                "metadata" => fixture.Doctor.Metadata,
                "entrypoint" => Path.Combine(Path.GetDirectoryName(fixture.Doctor.Metadata)!, "bin", "codex.js"),
                "launcher" => fixture.Doctor.Launcher,
                _ => Path.Combine(fixture.Prefix, "codex.cmd")
            };
            File.AppendAllText(path, "changed during final inventory");
        };
        var result = fixture.Apply(report);
        Assert.Equal(NpmApplyState.VerificationFailed, result.State);
        Assert.NotNull(result.Recovery);
        Assert.DoesNotContain("\"state\":\"Succeeded\"", File.ReadAllText(result.ReceiptPath!));
    }

    [Fact]
    public void Source_changes_during_an_already_current_query_do_not_leave_a_successful_stale_reading()
    {
        using var fixture = new TransactionFixture("1.0.0");
        var sourcePath = Path.Combine(fixture.Prefix, "source.DERT");
        File.WriteAllText(sourcePath, "STERT ; update ;; codex\ncommands ; updateme ;;");
        var source = NpmSource.Capture(sourcePath);
        fixture.AfterRequest = request => { if (request.Arguments[1] == "view") File.AppendAllText(sourcePath, "\n// changed"); };
        var report = fixture.Inspect(source);
        Assert.Equal(UpdateState.Unavailable, report.State);
        Assert.Null(report.Proposal);
    }

    [Fact]
    public void Stale_discovery_version_is_refused_before_the_first_manager_query()
    {
        using var fixture = new TransactionFixture();
        var discovery = fixture.Discovery();
        File.WriteAllText(fixture.Doctor.Metadata, File.ReadAllText(fixture.Doctor.Metadata).Replace("1.0.0", "1.0.1"));
        var report = UpdateTransaction.Inspect(discovery, null, fixture);
        Assert.Equal(UpdateState.Unavailable, report.State);
        Assert.Null(report.Proposal);
        Assert.Empty(fixture.Calls);
    }

    [Fact]
    public void Recomputing_a_saved_checksum_cannot_turn_an_update_record_into_an_arbitrary_process_request()
    {
        using var fixture = new TransactionFixture();
        var report = fixture.Inspect();
        var original = report.Proposal!;
        var forged = original with { Install = original.Install with { Arguments = ["arbitrary-script.js", "run-anything"] } };
        forged = forged with { Token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        { schema = 3, forged.Operation, forged.SelectedPath, forged.Prefix, forged.Version, forged.InstalledVersion,
            forged.Uninstall, forged.Install, forged.Files, forged.Source, forged.RouteGuard })))) };
        var directory = Path.Combine(fixture.Prefix, "forged-record");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, forged.Token + ".json"), JsonSerializer.Serialize(new { Schema = 3, Proposal = forged }));
        fixture.Calls.Clear();
        Assert.Throws<IOException>(() => UpdateTransaction.LoadProposal(forged.Token, directory));
        report.Proposal = forged;
        Assert.Equal(NpmApplyState.Refused, fixture.Apply(report).State);
        Assert.Empty(fixture.Calls);
    }

    [Fact]
    public void Cancellation_after_install_preserves_a_partial_result_without_starting_final_inventory()
    {
        using var fixture = new TransactionFixture();
        var report = fixture.Inspect();
        using var cancellation = new CancellationTokenSource();
        fixture.AfterInstall = cancellation.Cancel;
        fixture.Calls.Clear();
        var result = fixture.Apply(report, cancellation.Token);
        Assert.Equal(NpmApplyState.Interrupted, result.State);
        Assert.Equal("install", fixture.Calls[^1].Arguments[1]);
        Assert.NotNull(result.Recovery);
        Assert.Contains("Interrupted", File.ReadAllText(result.ReceiptPath!));
    }
}

internal sealed class TransactionFixture : ICommandRunner, IDisposable
{
    internal DoctorFixture Doctor { get; }
    internal TransactionFixture(string latest = "2.0.0") => Doctor = new() { Latest = latest };
    internal string Prefix => Doctor.Prefix;
    internal string Receipts => Doctor.Receipts;
    internal List<CommandRequest> Calls => Doctor.Calls;
    internal Action? AfterInstall { get; set; }
    internal Action<CommandRequest>? AfterRequest { get; set; }
    internal DiscoveryReport Discovery() => CodexDiscovery.Scan(new(Prefix, ".CMD;.EXE"));
    internal UpdateReport Inspect(NpmSource? source = null) => UpdateTransaction.Inspect(Discovery(), null, this, source: source);
    internal NpmApplyResult Apply(UpdateReport report, CancellationToken cancellation = default) =>
        UpdateTransaction.Apply(report, report.Proposal!.Token, this, Receipts, cancellation);
    public CommandResult Run(CommandRequest request)
    {
        if (Doctor.Fault == "install-noop" && DoctorFixture.IsMutation(request))
        { Calls.Add(request); return new(0, "{}", ""); }
        var result = Doctor.Run(request);
        if (DoctorFixture.IsMutation(request) && request.Arguments[1] == "install" && result.Succeeded)
        {
            Doctor.InventoryVersion = "2.0.0";
            AfterInstall?.Invoke();
        }
        AfterRequest?.Invoke(request);
        return result;
    }
    public void Dispose() => Doctor.Dispose();
}
