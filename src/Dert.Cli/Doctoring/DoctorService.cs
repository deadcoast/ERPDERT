using Dert.Cli.Discovery;
using Dert.Cli.Operations;
using Dert.Cli.Updates;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Dert.Cli.Doctoring;

internal static class DoctorService
{
    internal static DoctorReport Inspect(DiscoveryReport discovery, string? selectedPath,
        ICommandRunner? runner = null, Action<string>? progress = null,
        CancellationToken cancellation = default, DoctorSource? source = null)
    {
        var report = new DoctorReport(discovery);
        IReadOnlyList<DoctorFile>? files = null;
        try
        {
            cancellation.ThrowIfCancellationRequested();
            VerifySource(source);
            report.Update = NpmUpdateRehearsal.Create(discovery, selectedPath,
                runner ?? new SystemCommandRunner(cancellation), progress, repair: true, beforeCommand: update =>
                {
                    cancellation.ThrowIfCancellationRequested();
                    VerifySource(source);
                    files ??= CaptureInputs(update);
                    VerifyFiles(files);
                });
            cancellation.ThrowIfCancellationRequested();
            report.Issues.AddRange(report.Update.Issues);
            if (report.Update.State == UpdateState.Interrupted) throw new OperationCanceledException();
            report.State = report.Update.State == UpdateState.ChooseCopy ? DoctorState.ChooseCopy : DoctorState.Unavailable;
            if (report.Update.State != UpdateState.Ready || report.Update.Install is null || files is null) return report;
            VerifyFiles(files);
            VerifySource(source);
            var update = report.Update;
            var install = update.Install with { Timeout = TimeSpan.FromMinutes(5) };
            var uninstall = Request(install, "uninstall", "@openai/codex", "--ignore-scripts", "--dry-run=false", "--no-audit", "--no-fund");
            var proposal = new DoctorProposal(update.Selected!.Path, update.Prefix!, update.Available!,
                uninstall, install, files, source) { InstalledVersion = update.Selected.Package!.Version };
            report.Proposal = proposal with { Token = Token(proposal) };
            report.State = DoctorState.Ready;
        }
        catch (OperationCanceledException)
        {
            report.State = DoctorState.Interrupted;
            report.Issues.Add(new("DERT5301", selectedPath ?? "codex", "Doctor checks were interrupted. No repair is approved."));
        }
        catch (Exception failure) when (IsEvidenceError(failure))
        { report.Issues.Add(new("DERT5302", selectedPath ?? "codex", failure.Message)); }
        return report;
    }

    internal static DoctorApplyResult Apply(DoctorReport report, string token,
        ICommandRunner? runner = null, string? receiptDirectory = null,
        CancellationToken cancellation = default)
    {
        var phases = new List<DoctorPhase>();
        var plan = report.Proposal;
        string? receiptPath = null;
        FileStream? receipt = null;
        FileStream? transactionLock = null;
        bool mutationStarted = false;
        string currentPhase = "review";
        try
        {
            cancellation.ThrowIfCancellationRequested();
            if (report.State != DoctorState.Ready || plan is null || token != plan.Token || Token(plan) != plan.Token)
                return Result(DoctorApplyState.Refused, "The approval does not match this exact repair proposal. Inspect again and review its current token.");
            VerifyFiles(plan.Files);
            VerifySource(plan.Source);
            runner ??= new SystemCommandRunner(cancellation);
            var inventory = Request(plan.Install, "ls", "--depth=0", "--long", "--json", "@openai/codex");
            var root = Run("target-recheck", Request(plan.Install, "root")).Trim();
            if (!Path.IsPathFullyQualified(root) || !Same(DiscoveryFiles.ResolveLocal(root), Path.Combine(plan.Prefix, "node_modules")))
                throw new IOException("npm's target folder changed. Inspect again before approving a repair.");
            using (var json = JsonDocument.Parse(Run("inventory-recheck", inventory)))
                NpmUpdateRehearsal.VerifyInventory(json.RootElement, plan.Prefix,
                    Path.Combine(plan.Prefix, "node_modules", "@openai", "codex"), plan.InstalledVersion);
            VerifyFiles(plan.Files);
            VerifySource(plan.Source);
            cancellation.ThrowIfCancellationRequested();
            currentPhase = "receipt";
            var directory = receiptDirectory ?? Path.Combine(StateHome(), "doctor-receipts");
            directory = Path.GetFullPath(directory);
            if (!Same(directory, DiscoveryFiles.ResolveLocal(directory)))
                throw new IOException("The operation receipt folder redirects elsewhere. Choose a regular local folder.");
            if (IsWithin(directory, Path.Combine(plan.Prefix, "node_modules")))
                throw new IOException("The operation receipt must be outside npm's package folder so uninstall cannot remove it.");
            Directory.CreateDirectory(directory);
            var lockName = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plan.Prefix.ToUpperInvariant()))) + ".lock";
            var lockPath = Path.Combine(directory, lockName);
            if (!Same(lockPath, DiscoveryFiles.ResolveLocal(lockPath))) throw new IOException("The doctor transaction lock redirects elsewhere.");
            transactionLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            receiptPath = Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffff") + "-" + Guid.NewGuid().ToString("N") + ".jsonl");
            receipt = new FileStream(receiptPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
            Write("prepared", new { plan.Token, plan.SelectedPath, plan.Prefix, plan.Version, plan.Uninstall, plan.Install, plan.Source, plan.Files,
                note = "Non-atomic repair. Uninstall or install failure may leave this copy absent or partial. No rollback is promised." });
            currentPhase = "uninstall";
            VerifyFiles(plan.Files);
            VerifySource(plan.Source);
            cancellation.ThrowIfCancellationRequested();
            Write("uninstall-started", plan.Uninstall);
            mutationStarted = true;
            var removed = runner.Run(plan.Uninstall);
            Record("uninstall", removed);
            if (!removed.Succeeded) return Result(removed.ExitCode == 130 ? DoctorApplyState.Interrupted : DoctorApplyState.UninstallFailed, "Uninstall did not complete; installation was not started. This copy may be partial. Review the receipt before using the exact recovery request.");
            currentPhase = "install";
            cancellation.ThrowIfCancellationRequested();
            VerifyManager(plan);
            VerifySource(plan.Source);
            // npm must actually remove its package, not merely exit zero (for example,
            // an ambient dry-run setting). Never report a reinstall if removal did not occur.
            if (Directory.Exists(Path.Combine(plan.Prefix, "node_modules", "@openai", "codex")))
                return Result(DoctorApplyState.UninstallFailed, "npm exited successfully but the selected package folder remains. Installation was stopped; inspect the receipt and this copy.");
            Write("install-started", plan.Install);
            var installed = runner.Run(plan.Install);
            Record("install", installed);
            if (!installed.Succeeded) return Result(installed.ExitCode == 130 ? DoctorApplyState.Interrupted : DoctorApplyState.InstallFailed, "Installation did not complete after uninstall. This copy may be absent or partial. The receipt retains the exact pinned recovery request; no rollback was attempted.");
            currentPhase = "verification";
            VerifyManager(plan);
            VerifySource(plan.Source);
            var packageRoot = Path.Combine(plan.Prefix, "node_modules", "@openai", "codex");
            var selected = CodexPackageProbe.Inspect(plan.SelectedPath, DiscoveryFiles.ResolveLocal(plan.SelectedPath), 1,
                [Path.GetDirectoryName(plan.Install.Program)!]);
            if (selected.Package is not { } package || package.Version != plan.Version || !Same(package.Root, packageRoot) ||
                selected.Issues.Count > 0 || !DiscoveryFiles.IsFile(Path.Combine(packageRoot, "bin", "codex.js")))
                throw new IOException("The expected Codex package, launcher, and JavaScript entrypoint were not all verified after install.");
            using (var json = JsonDocument.Parse(Run("inventory-after-install", inventory, afterMutation: true)))
                NpmUpdateRehearsal.VerifyInventory(json.RootElement, plan.Prefix, packageRoot, plan.Version);
            VerifyManager(plan);
            VerifySource(plan.Source);
            cancellation.ThrowIfCancellationRequested();
            Write("verified", new { version = plan.Version, packageRoot, scope = "Manager inventory and launcher/JavaScript entrypoint verified. Codex itself was not launched; authentication and native runtime health are not established." });
            return Result(DoctorApplyState.Succeeded, "Reinstalled the reviewed Codex version; npm inventory and launcher/JavaScript entrypoint are verified. Codex itself was not launched.");
        }
        catch (OperationCanceledException)
        { return Result(DoctorApplyState.Interrupted, mutationStarted ? "Repair was interrupted after package changes began. This copy may be absent or partial; use the receipt's recovery request after inspecting its state." : "Doctor was interrupted before package changes began."); }
        catch (Exception failure) when (IsEvidenceError(failure))
        {
            var state = currentPhase == "receipt" ? DoctorApplyState.ReceiptFailed : !mutationStarted ? DoctorApplyState.Refused :
                currentPhase == "uninstall" ? DoctorApplyState.UninstallFailed : currentPhase == "install" ? DoctorApplyState.InstallFailed : DoctorApplyState.VerificationFailed;
            return Result(state, failure.Message + (mutationStarted ? " Package changes began; this copy may be absent or partial. No rollback was attempted." : " No package changes began."));
        }
        finally { receipt?.Dispose(); transactionLock?.Dispose(); }

        string Run(string phase, CommandRequest request, bool afterMutation = false)
        {
            cancellation.ThrowIfCancellationRequested();
            if (afterMutation) VerifyManager(plan!); else VerifyFiles(plan!.Files);
            VerifySource(plan.Source);
            var response = runner!.Run(request);
            phases.Add(new(phase, response.Succeeded, Detail(response)));
            if (response.ExitCode == 130) throw new OperationCanceledException("The npm query was interrupted.");
            cancellation.ThrowIfCancellationRequested();
            if (!response.Succeeded) throw new IOException(phase + " did not complete: " + Detail(response));
            return response.Output;
        }
        void Record(string phase, CommandResult response)
        {
            phases.Add(new(phase, response.Succeeded, Detail(response)));
            Write(phase + "-finished", new { response.ExitCode, response.Failure, response.Succeeded, detail = Detail(response) });
        }
        void Write(string phase, object detail)
        {
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { at = DateTimeOffset.UtcNow, phase, detail }) + "\n");
            receipt!.Write(bytes);
            receipt.Flush(flushToDisk: true);
        }
        DoctorApplyResult Result(DoctorApplyState state, string message)
        {
            if (receipt is not null)
            {
                try { Write("result", new { state = state.ToString(), message }); }
                catch (Exception failure) when (IsEvidenceError(failure))
                { return new(DoctorApplyState.ReceiptFailed, message + " The final receipt could not be saved: " + failure.Message, receiptPath, mutationStarted ? plan?.Install : null, phases); }
            }
            return new(state, message, receiptPath, mutationStarted && state != DoctorApplyState.Succeeded ? plan?.Install : null, phases);
        }
    }

    private static IReadOnlyList<DoctorFile> CaptureInputs(UpdateReport update)
    {
        var selected = update.Selected!;
        var package = selected.Package!;
        var prefix = update.Prefix!;
        var packageRoot = Path.Combine(prefix, "node_modules", "@openai", "codex");
        var npmRoot = Path.Combine(prefix, "node_modules", "npm");
        if (!Same(DiscoveryFiles.ResolveLocal(package.MetadataPath), Path.Combine(packageRoot, "package.json")) ||
            !Same(DiscoveryFiles.ResolveLocal(package.NpmPath!), Path.Combine(prefix, "npm.cmd")) ||
            !Same(DiscoveryFiles.ResolveLocal(Path.GetDirectoryName(selected.Path)!), prefix) ||
            !Same(selected.Identity, Path.Combine(prefix, Path.GetFileName(selected.Path))))
            throw new IOException("The discovered package metadata or npm launcher does not match the selected prefix.");
        foreach (var path in new[] { prefix, packageRoot, npmRoot, Path.Combine(packageRoot, "package.json"), Path.Combine(prefix, "npm.cmd"), Path.Combine(npmRoot, "bin", "npm-cli.js") })
            if (!Same(Path.GetFullPath(path), DiscoveryFiles.ResolveLocal(path)))
                throw new IOException("This repair requires regular local package and manager paths; a redirected path needs separate handling: " + path);
        var paths = new List<string> { selected.Path, package.MetadataPath, package.NodePath!, package.NpmPath!,
            Path.Combine(prefix, "npm.cmd"), Path.Combine(npmRoot, "package.json"), update.NpmCli!, Path.Combine(packageRoot, "bin", "codex.js") };
        foreach (var name in new[] { "codex", "codex.cmd", "codex.ps1" })
        {
            var path = Path.Combine(prefix, name);
            if (File.Exists(path))
            {
                var match = CodexPackageProbe.Inspect(path, DiscoveryFiles.ResolveLocal(path), 1, [Path.GetDirectoryName(package.NodePath!)!]);
                if (match.Package is null || !Same(match.Package.Root, packageRoot))
                    throw new IOException("An npm launcher slot belongs to an unidentified target. Repair would risk replacing it: " + path);
            }
            paths.Add(path);
        }
        return Array.AsReadOnly(paths.Distinct(StringComparer.OrdinalIgnoreCase).Select(DoctorFile.Capture).ToArray());
    }
    private static void VerifyFiles(IEnumerable<DoctorFile> files)
    {
        foreach (var original in files)
        {
            var current = DoctorFile.Capture(original.Path);
            if (!Same(original.Identity, current.Identity) || current.Sha256 != original.Sha256)
                throw new IOException("The reviewed evidence changed; inspect again before approving repair: " + original.Path);
        }
    }
    private static void VerifyManager(DoctorProposal plan)
    {
        if (!Same(DiscoveryFiles.ResolveLocal(Path.GetDirectoryName(plan.SelectedPath)!), plan.Prefix))
            throw new IOException("The selected command's prefix link changed. Repair stopped before using another installation.");
        foreach (var path in new[] { plan.Prefix, Path.Combine(plan.Prefix, "node_modules"),
            Path.Combine(plan.Prefix, "node_modules", "@openai"), Path.Combine(plan.Prefix, "node_modules", "@openai", "codex") })
            if (!Same(path, DiscoveryFiles.ResolveLocal(path)))
                throw new IOException("The selected package destination now redirects elsewhere: " + path);
        var npmRoot = Path.Combine(plan.Prefix, "node_modules", "npm");
        VerifyFiles(plan.Files.Where(file => Same(file.Identity, plan.Install.Program) || IsWithin(file.Path, npmRoot) || Same(file.Path, Path.Combine(plan.Prefix, "npm.cmd"))));
    }
    private static void VerifySource(DoctorSource? source)
    {
        if (source is not null) VerifyFiles([new(source.Path, source.Identity, source.Sha256)]);
    }
    private static CommandRequest Request(CommandRequest basis, params string[] args) => new(basis.Program,
        Array.AsReadOnly(new[] { basis.Arguments[0] }.Concat(args).Concat(["--global", "--prefix", basis.WorkingDirectory, "--no-update-notifier", "--color=false"]).ToArray()),
        basis.WorkingDirectory, basis.Timeout);
    private static string Token(DoctorProposal plan) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
    { schema = 1, plan.SelectedPath, plan.Prefix, plan.Version, plan.InstalledVersion, plan.Uninstall, plan.Install, plan.Files, plan.Source }))));
    internal static string SaveProposal(DoctorReport report, string? proposalDirectory = null)
    {
        var plan = report.Proposal;
        if (report.State != DoctorState.Ready || plan is null || Token(plan) != plan.Token)
            throw new IOException("There is no complete reviewed proposal to save.");
        VerifyFiles(plan.Files);
        VerifySource(plan.Source);
        var directory = ProposalDirectory(proposalDirectory);
        if (IsWithin(directory, Path.Combine(plan.Prefix, "node_modules")))
            throw new IOException("Store doctor proposals outside npm's package folder.");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, plan.Token + ".json");
        if (File.Exists(path))
        {
            LoadProposal(plan.Token, directory);
            return path;
        }
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new SavedProposal(1, plan));
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
        return path;
    }

    // Loading never invokes Node/npm. The old snapshot is checked before any
    // manager execution, instead of generating a fresh approval from changed files.
    internal static DoctorReport LoadProposal(string token, string? proposalDirectory = null)
    {
        if (token.Length != 64 || !token.All(char.IsAsciiHexDigit))
            throw new IOException("The approval token is not a complete doctor proposal identifier.");
        var path = Path.Combine(ProposalDirectory(proposalDirectory), token.ToUpperInvariant() + ".json");
        if (!Same(path, DiscoveryFiles.ResolveLocal(path))) throw new IOException("The saved proposal redirects elsewhere.");
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > 1024 * 1024) throw new IOException("The saved doctor proposal exceeds its inspection limit.");
        SavedProposal? saved;
        try { saved = JsonSerializer.Deserialize<SavedProposal>(stream); }
        catch (JsonException failure) { throw new IOException("The saved doctor proposal could not be understood.", failure); }
        var plan = saved?.Proposal;
        if (saved?.Schema != 1 || plan is null || plan.Token != token || Token(plan) != token ||
            !Path.IsPathFullyQualified(plan.Prefix) || !Path.IsPathFullyQualified(plan.SelectedPath) ||
            !Same(DiscoveryFiles.ResolveLocal(Path.GetDirectoryName(plan.SelectedPath)!), plan.Prefix))
            throw new IOException("The saved proposal no longer matches this approval. Inspect again before repair.");
        VerifyFiles(plan.Files);
        VerifySource(plan.Source);
        var root = Path.Combine(plan.Prefix, "node_modules", "@openai", "codex");
        var selected = new CommandMatch(plan.SelectedPath, 1, DiscoveryFiles.ResolveLocal(plan.SelectedPath),
            new(root, Path.Combine(root, "package.json"), plan.InstalledVersion, plan.Install.Program, Path.Combine(plan.Prefix, "npm.cmd")), []);
        var discovery = new DiscoveryReport([selected], [], 0);
        return new(discovery)
        {
            State = DoctorState.Ready, Proposal = plan,
            Update = new(discovery) { State = UpdateState.Ready, Selected = selected, Prefix = plan.Prefix,
                Node = plan.Install.Program, NpmCli = plan.Install.Arguments[0], Available = plan.Version, Install = plan.Install }
        };
    }
    private sealed record SavedProposal(int Schema, DoctorProposal Proposal);
    private static string StateHome()
    {
        var configured = Environment.GetEnvironmentVariable("DERT_STATE_HOME");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (!Path.IsPathFullyQualified(configured)) throw new IOException("DERT_STATE_HOME must be an absolute local folder.");
            return Path.GetFullPath(configured);
        }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DERT");
    }
    private static string ProposalDirectory(string? directory)
    {
        directory = Path.GetFullPath(directory ?? Path.Combine(StateHome(), "doctor-proposals"));
        if (!Same(directory, DiscoveryFiles.ResolveLocal(directory))) throw new IOException("The doctor proposal folder redirects elsewhere.");
        return directory;
    }
    private static bool Same(string first, string second) => string.Equals(Path.TrimEndingDirectorySeparator(first), Path.TrimEndingDirectorySeparator(second), StringComparison.OrdinalIgnoreCase);
    private static bool IsWithin(string path, string root) => Same(path, root) || path.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    private static bool IsEvidenceError(Exception error) => DiscoveryFiles.IsReadError(error) || error is JsonException or InvalidOperationException;
    private static string Detail(CommandResult result)
    {
        var text = result.Failure ?? (result.Error.Length > 0 ? result.Error : result.Output);
        return $"exit {result.ExitCode}: " + text[..Math.Min(text.Length, 3000)];
    }
}
