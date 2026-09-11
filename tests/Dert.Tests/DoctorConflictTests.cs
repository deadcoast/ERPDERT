using Dert.Cli.Discovery;
using Dert.Cli.Doctoring;
using Dert.Cli.Operations;
using Dert.Cli.Transactions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Dert.Tests;

public class DoctorConflictTests
{
    [Theory]
    [InlineData("replaced")]
    [InlineData("previously-absent")]
    public void A_new_foreign_shim_after_uninstall_stops_before_install_can_replace_it(string condition)
    {
        using var fixture = new ConflictFixture();
        var slot = condition == "replaced" ? fixture.Doctor.Launcher : Path.Combine(fixture.Doctor.Prefix, "codex.cmd");
        if (condition == "previously-absent") File.Delete(slot);
        var report = fixture.Inspect();
        fixture.Doctor.Calls.Clear();
        const string foreign = "a new unrelated owner's command";
        fixture.Doctor.AfterUninstall = () => File.WriteAllText(slot, foreign);
        var result = fixture.Apply(report);
        Assert.Equal(DoctorApplyState.InstallFailed, result.State);
        Assert.Single(fixture.Doctor.Calls, DoctorFixture.IsMutation);
        Assert.Equal(foreign, File.ReadAllText(slot));
        Assert.NotNull(result.ReceiptPath);
        Assert.NotNull(result.Recovery);
        Assert.DoesNotContain(result.Phases, phase => phase.Name == "external-route-condition-verified");
    }

    [Theory]
    [InlineData("source")]
    [InlineData("metadata")]
    public void The_route_scan_cannot_outlive_the_final_source_and_package_verification(string changed)
    {
        using var fixture = new ConflictFixture();
        var sourcePath = Path.Combine(fixture.Doctor.Files.Root, "reviewed.DERT");
        File.WriteAllText(sourcePath, "stert; codex; doctor; fin");
        var report = DoctorService.Inspect(fixture.Scan(), fixture.Doctor.Launcher, fixture,
            source: DoctorSource.Capture(sourcePath), environment: fixture.Environment, environmentReader: () => fixture.Environment);
        bool finalInventory = false;
        bool changedDuringRouteCheck = false;
        int readsAfterInventory = 0;
        fixture.AfterRequest = request =>
        {
            if (request.Arguments[1] == "ls" && File.Exists(fixture.Entrypoint)) finalInventory = true;
        };
        var result = DoctorService.Apply(report, report.Proposal!.Token, fixture, fixture.Doctor.Receipts, environmentReader: () =>
        {
            // The first boundary after the final query is its post-query check;
            // the second closes normal package verification; the third starts
            // the independent external-route scan.
            if (finalInventory && ++readsAfterInventory == 3)
            {
                changedDuringRouteCheck = true;
                if (changed == "source") File.AppendAllText(sourcePath, " changed");
                else File.WriteAllText(fixture.Doctor.Metadata, File.ReadAllText(fixture.Doctor.Metadata).Replace("1.0.0", "1.0.1", StringComparison.Ordinal));
            }
            return fixture.Environment;
        });
        Assert.True(changedDuringRouteCheck);
        Assert.Equal(DoctorApplyState.VerificationFailed, result.State);
        Assert.DoesNotContain(result.Phases, phase => phase.Name == "external-route-condition-verified");
        Assert.NotNull(result.Recovery);
    }

    [Fact]
    public void A_diagnosis_invalidated_during_npm_queries_is_retained_as_incomplete()
    {
        using var fixture = new ConflictFixture();
        fixture.AfterRequest = _ => File.WriteAllText(fixture.Entrypoint, "another actor repaired the entrypoint");
        var report = fixture.Inspect();
        Assert.Equal(DoctorState.Unavailable, report.State);
        Assert.Equal(DoctorDiagnosisState.Incomplete, report.Diagnosis.State);
        Assert.Null(report.Diagnosis.Guard);
        Assert.Null(report.Proposal);
    }

    [Theory]
    [InlineData("dot")]
    [InlineData("sibling")]
    public void Any_selected_launcher_in_the_leading_installation_preserves_the_route_guard(string spelling)
    {
        using var fixture = new ConflictFixture();
        var selected = spelling == "dot" ? Path.Combine(fixture.Doctor.Prefix, ".", "codex.ps1") : Path.Combine(fixture.Doctor.Prefix, "codex.cmd");
        var report = DoctorService.Inspect(fixture.Scan(), selected, fixture,
            environment: fixture.Environment, environmentReader: () => fixture.Environment);
        Assert.Equal(DoctorState.Ready, report.State);
        Assert.NotNull(report.Proposal!.RouteGuard);
        fixture.Doctor.Calls.Clear();
        fixture.Change("path");
        Assert.Equal(DoctorApplyState.Refused, fixture.Apply(report).State);
        Assert.Empty(fixture.Doctor.Calls);
    }

    [Fact]
    public void A_saved_leading_route_repair_retains_its_guard_and_durably_verifies_the_external_condition()
    {
        using var fixture = new ConflictFixture();
        var report = fixture.Inspect();
        Assert.Equal(DoctorState.Ready, report.State);
        Assert.NotNull(report.Proposal!.RouteGuard);
        var directory = Path.Combine(fixture.Doctor.Files.Root, "guarded proposals");
        var path = DoctorService.SaveProposal(report, directory, () => fixture.Environment);
        Assert.Contains("\"Schema\":3", File.ReadAllText(path));
        var loaded = DoctorService.LoadProposal(report.Proposal.Token, directory, () => fixture.Environment);
        Assert.Equal(DoctorDiagnosisState.Confirmed, loaded.Diagnosis.State);
        var result = fixture.Apply(loaded);
        Assert.Equal(DoctorApplyState.Succeeded, result.State);
        Assert.Contains(result.Phases, phase => phase.Name == "external-route-condition-verified" && phase.Succeeded);
        var receipt = File.ReadAllText(result.ReceiptPath!);
        Assert.Contains("RouteGuard", receipt);
        Assert.True(receipt.IndexOf("external-route-condition-verified", StringComparison.Ordinal) < receipt.IndexOf("\"phase\":\"verified\"", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("path")]
    [InlineData("pathext")]
    [InlineData("earlier")]
    [InlineData("later-entrypoint")]
    [InlineData("later-launcher")]
    [InlineData("later-node")]
    [InlineData("later-metadata")]
    public void Reviewed_route_drift_refuses_before_any_manager_process(string change)
    {
        using var fixture = new ConflictFixture();
        var report = fixture.Inspect();
        fixture.Doctor.Calls.Clear();
        fixture.Change(change);
        var result = fixture.Apply(report);
        Assert.Equal(DoctorApplyState.Refused, result.State);
        Assert.Empty(fixture.Doctor.Calls);
        Assert.Null(result.ReceiptPath);
    }

    [Theory]
    [InlineData("root")]
    [InlineData("ls")]
    [InlineData("view")]
    [InlineData("install")]
    public void Inspection_query_drift_cannot_issue_a_guarded_proposal(string query)
    {
        using var fixture = new ConflictFixture();
        fixture.AfterRequest = request => { if (request.Arguments[1] == query) fixture.Change("pathext"); };
        var report = fixture.Inspect();
        Assert.Equal(DoctorState.Unavailable, report.State);
        Assert.Null(report.Proposal);
        Assert.DoesNotContain(fixture.Doctor.Calls, DoctorFixture.IsMutation);
    }

    [Theory]
    [InlineData("uninstall", "InstallFailed")]
    [InlineData("install", "VerificationFailed")]
    [InlineData("final-inventory", "VerificationFailed")]
    public void Protected_route_drift_during_mutation_prevents_a_resolved_claim(string phase, string expected)
    {
        using var fixture = new ConflictFixture();
        var report = fixture.Inspect();
        fixture.AfterRequest = request =>
        {
            if (request.Arguments[1] == phase || phase == "final-inventory" && request.Arguments[1] == "ls" && File.Exists(fixture.Entrypoint))
                fixture.Change("later-entrypoint");
        };
        var result = fixture.Apply(report);
        Assert.Equal(expected, result.State.ToString());
        Assert.NotNull(result.ReceiptPath);
        Assert.DoesNotContain(result.Phases, item => item.Name == "external-route-condition-verified");
        Assert.NotNull(result.Recovery);
    }

    [Fact]
    public void Save_and_load_recheck_the_raw_environment_without_running_npm()
    {
        using var fixture = new ConflictFixture();
        var report = fixture.Inspect();
        var directory = Path.Combine(fixture.Doctor.Files.Root, "guarded proposals");
        DoctorService.SaveProposal(report, directory, () => fixture.Environment);
        fixture.Doctor.Calls.Clear();
        fixture.Change("path");
        Assert.Throws<IOException>(() => DoctorService.SaveProposal(report, directory, () => fixture.Environment));
        Assert.Throws<IOException>(() => DoctorService.LoadProposal(report.Proposal!.Token, directory, () => fixture.Environment));
        Assert.Empty(fixture.Doctor.Calls);
    }

    [Fact]
    public void Saved_guard_cannot_drop_protected_inputs_even_with_a_recomputed_checksum()
    {
        using var fixture = new ConflictFixture();
        var report = fixture.Inspect();
        var directory = Path.Combine(fixture.Doctor.Files.Root, "guarded proposals");
        var path = DoctorService.SaveProposal(report, directory, () => fixture.Environment);
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        var plan = json.RootElement.GetProperty("Proposal").Deserialize<NpmProposal>()!;
        var forged = plan with { RouteGuard = plan.RouteGuard! with { ProtectedFiles = [] } };
        forged = forged with { Token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        { schema = 3, forged.Operation, forged.SelectedPath, forged.Prefix, forged.Version, forged.InstalledVersion,
            forged.Uninstall, forged.Install, forged.Files, forged.Source, forged.RouteGuard })))) };
        File.WriteAllText(Path.Combine(directory, forged.Token + ".json"), JsonSerializer.Serialize(new { Schema = 3, Proposal = forged }));
        fixture.Doctor.Calls.Clear();
        var failure = Assert.Throws<IOException>(() => DoctorService.LoadProposal(forged.Token, directory, () => fixture.Environment));
        Assert.Contains("external-route evidence", failure.Message);
        Assert.Empty(fixture.Doctor.Calls);
    }

    [Fact]
    public void A_guard_freezes_the_file_and_directory_collections_it_was_given()
    {
        using var fixture = new ConflictFixture();
        var guard = fixture.Inspect().Proposal!.RouteGuard!;
        var files = guard.ProtectedFiles.ToList();
        var directories = guard.Directories.ToList();
        var copy = guard with { ProtectedFiles = files, Directories = directories };
        files.Clear(); directories.Clear();
        Assert.NotEmpty(copy.ProtectedFiles);
        Assert.NotEmpty(copy.Directories);
        Assert.Throws<NotSupportedException>(() => ((IList<NpmFile>)copy.ProtectedFiles).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<NpmDirectory>)copy.Directories).Clear());
    }

    [Fact]
    public void A_known_earlier_npm_launcher_with_a_definitely_missing_entrypoint_conflicts_with_a_later_intact_copy()
    {
        using var fixture = new ConflictFixture();
        var diagnosis = DoctorConflictDiagnosis.Inspect(fixture.Scan(), fixture.Environment);
        Assert.Equal(DoctorDiagnosisState.Confirmed, diagnosis.State);
        Assert.Equal(fixture.Doctor.Launcher, diagnosis.LeadingPath);
        Assert.Equal(fixture.Entrypoint, diagnosis.MissingEntrypoint);
        Assert.Equal(Path.Combine(fixture.Later, "codex.ps1"), diagnosis.LaterPath);
        Assert.NotNull(diagnosis.Guard);
        Assert.Contains(diagnosis.Guard.ProtectedFiles, file => file.Path == Path.Combine(fixture.Earlier, "codex.ps1") && file.Sha256 is null);
        Assert.Empty(fixture.Doctor.Calls);
    }

    [Theory]
    [InlineData("healthy", "Possible")]
    [InlineData("single", "None")]
    [InlineData("custom", "Incomplete")]
    [InlineData("executable", "Incomplete")]
    [InlineData("relative", "Incomplete")]
    [InlineData("quoted", "Incomplete")]
    [InlineData("unreadable", "Incomplete")]
    [InlineData("unreadable-later", "Incomplete")]
    [InlineData("entrypoint-directory", "Incomplete")]
    [InlineData("parent-file", "Incomplete")]
    [InlineData("unsupported-pathext", "Incomplete")]
    public void Incomplete_or_nonconflicting_evidence_never_becomes_a_confirmed_conflict(string condition, string expected)
    {
        using var fixture = new ConflictFixture();
        if (condition == "healthy") File.WriteAllText(fixture.Entrypoint, "intact owned fixture");
        if (condition == "single") fixture.Environment = fixture.Environment with { SearchPath = fixture.Doctor.Prefix };
        if (condition == "custom") File.AppendAllText(fixture.Doctor.Launcher, "\nWrite-Output 'extra behavior'\n");
        if (condition == "executable") File.WriteAllText(Path.Combine(fixture.Earlier, "codex.exe"), "an unknown executable");
        if (condition == "relative") fixture.Environment = fixture.Environment with { SearchPath = ".;" + fixture.Environment.SearchPath };
        if (condition == "quoted") fixture.Environment = fixture.Environment with { SearchPath = "\"" + fixture.Earlier + "\";" + fixture.Environment.SearchPath };
        if (condition == "unsupported-pathext") fixture.Environment = fixture.Environment with { PathExtensions = ".EXE;bad" };
        if (condition == "entrypoint-directory") Directory.CreateDirectory(fixture.Entrypoint);
        if (condition == "parent-file")
        {
            var bin = Path.GetDirectoryName(fixture.Entrypoint)!;
            Directory.Delete(bin); // The fixture's entrypoint was already removed, leaving this directory empty.
            File.WriteAllText(bin, "a file occupies the required parent folder");
        }
        FileStream? held = null;
        try
        {
            if (condition == "unreadable")
            {
                File.WriteAllText(fixture.Entrypoint, "not missing");
                held = new(fixture.Entrypoint, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            if (condition == "unreadable-later")
                held = new(Path.Combine(fixture.Later, "node_modules", "@openai", "codex", "bin", "codex.js"),
                    FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            var diagnosis = DoctorConflictDiagnosis.Inspect(fixture.Scan(), fixture.Environment);
            Assert.Equal(expected, diagnosis.State.ToString());
            Assert.Null(diagnosis.Guard);
        }
        finally { held?.Dispose(); }
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Whole_generated_npm_shape_is_recognized_but_executable_additions_and_target_changes_are_not(string newline)
    {
        var text = ConflictFixture.Shim.Replace("\n", newline, StringComparison.Ordinal);
        Assert.True(NpmPowerShellShim.IsSupported(text));
        Assert.True(NpmPowerShellShim.IsSupported("\uFEFF" + text));
        Assert.False(NpmPowerShellShim.IsSupported(text + "Write-Output 'also do this'\n"));
        Assert.False(NpmPowerShellShim.IsSupported(text.Replace("codex.js", "another.js", StringComparison.Ordinal)));
        Assert.False(NpmPowerShellShim.IsSupported("<#\n" + text + "\n#>"));
    }
}

internal sealed class ConflictFixture : ICommandRunner, IDisposable
{
    internal DoctorFixture Doctor { get; } = new();
    internal string Earlier { get; }
    internal string Later { get; }
    internal string Entrypoint => Path.Combine(Path.GetDirectoryName(Doctor.Metadata)!, "bin", "codex.js");
    internal DiscoveryEnvironment Environment { get; set; }
    internal Action<CommandRequest>? AfterRequest { get; set; }
    internal ConflictFixture()
    {
        Earlier = Path.Combine(Doctor.Files.Root, "earlier empty"); Directory.CreateDirectory(Earlier);
        Later = Doctor.Files.Package("later intact", "1.0.0");
        File.WriteAllText(Doctor.Launcher, Shim);
        File.WriteAllText(Path.Combine(Later, "codex.ps1"), Shim);
        File.Delete(Entrypoint);
        Environment = new(string.Join(';', Earlier, Doctor.Prefix, Later), ".COM;.EXE;.BAT;.CMD");
    }
    internal DiscoveryReport Scan() => CodexDiscovery.Scan(Environment);
    internal DoctorReport Inspect() => DoctorService.Inspect(Scan(), Doctor.Launcher, this,
        environment: Environment, environmentReader: () => Environment);
    internal DoctorApplyResult Apply(DoctorReport report) => DoctorService.Apply(report, report.Proposal!.Token, this,
        Doctor.Receipts, environmentReader: () => Environment);
    internal void Change(string change)
    {
        switch (change)
        {
            case "path": Environment = Environment with { SearchPath = Environment.SearchPath + ";" }; break;
            case "pathext": Environment = Environment with { PathExtensions = ".CMD;.EXE" }; break;
            case "earlier": File.WriteAllText(Path.Combine(Earlier, "codex.ps1"), "new earlier route"); break;
            case "later-entrypoint": File.AppendAllText(Path.Combine(Later, "node_modules", "@openai", "codex", "bin", "codex.js"), "changed"); break;
            case "later-launcher": File.AppendAllText(Path.Combine(Later, "codex.ps1"), "extra behavior"); break;
            case "later-node": File.AppendAllText(Path.Combine(Later, "node.exe"), "changed"); break;
            case "later-metadata": File.AppendAllText(Path.Combine(Later, "node_modules", "@openai", "codex", "package.json"), " "); break;
        }
    }
    public CommandResult Run(CommandRequest request)
    {
        var result = Doctor.Run(request);
        if (DoctorFixture.IsMutation(request) && request.Arguments[1] == "install" && result.Succeeded)
            File.WriteAllText(Doctor.Launcher, Shim);
        AfterRequest?.Invoke(request);
        return result;
    }
    public void Dispose() => Doctor.Dispose();
    internal const string Shim = """
#!/usr/bin/env pwsh
$basedir=Split-Path $MyInvocation.MyCommand.Definition -Parent

$exe=""
if ($PSVersionTable.PSVersion -lt "6.0" -or $IsWindows) {
  # Fix case when both the Windows and Linux builds of Node
  # are installed in the same directory
  $exe=".exe"
}
$ret=0
if (Test-Path "$basedir/node$exe") {
  # Support pipeline input
  if ($MyInvocation.ExpectingInput) {
    $input | & "$basedir/node$exe"  "$basedir/node_modules/@openai/codex/bin/codex.js" $args
  } else {
    & "$basedir/node$exe"  "$basedir/node_modules/@openai/codex/bin/codex.js" $args
  }
  $ret=$LASTEXITCODE
} else {
  # Support pipeline input
  if ($MyInvocation.ExpectingInput) {
    $input | & "node$exe"  "$basedir/node_modules/@openai/codex/bin/codex.js" $args
  } else {
    & "node$exe"  "$basedir/node_modules/@openai/codex/bin/codex.js" $args
  }
  $ret=$LASTEXITCODE
}
exit $ret

""";
}
