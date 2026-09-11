using Dert.Cli.Discovery;
using Dert.Cli.Operations;
using Dert.Cli.Updates;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Dert.Cli.Transactions;

internal static class NpmTransaction
{
    internal static UpdateReport Inspect(NpmOperation operation, DiscoveryReport discovery, string? selectedPath,
        ICommandRunner? runner = null, Action<string>? progress = null, Action<UpdateActivity>? activity = null,
        CancellationToken cancellation = default, NpmSource? source = null,
        NpmRouteGuard? routeGuard = null, Func<DiscoveryEnvironment>? environmentReader = null)
    {
        var report = new UpdateReport(discovery);
        IReadOnlyList<NpmFile>? files = null;
        try
        {
            cancellation.ThrowIfCancellationRequested();
            VerifySource(source);
            NpmRouteGuardChecks.Verify(routeGuard, environmentReader);
            runner ??= new SystemCommandRunner(cancellation);
            report = NpmUpdateRehearsal.Create(discovery, selectedPath,
                new BoundaryRunner(runner, () =>
                {
                    // Includes failed/interrupted replies and the final query.
                    if (files is not null) VerifyFiles(files);
                    VerifySource(source);
                    NpmRouteGuardChecks.Verify(routeGuard, environmentReader);
                }), progress, activity, repair: operation == NpmOperation.Doctor, beforeCommand: update =>
                {
                    cancellation.ThrowIfCancellationRequested();
                    VerifySource(source);
                    NpmRouteGuardChecks.Verify(routeGuard, environmentReader);
                    files ??= CaptureInputs(update);
                    VerifyFiles(files);
                });
            if (files is not null) VerifyFiles(files);
            VerifySource(source);
            NpmRouteGuardChecks.Verify(routeGuard, environmentReader);
            cancellation.ThrowIfCancellationRequested();
            if (report.State == UpdateState.Interrupted) throw new OperationCanceledException();
            if (report.State != UpdateState.Ready || report.Install is null || files is null) return report;
            var install = report.Install with { Timeout = TimeSpan.FromMinutes(5) };
            if (!install.Arguments.Contains("--ignore-scripts")) install = install with { Arguments = [.. install.Arguments, "--ignore-scripts"] };
            if (!install.Arguments.Contains("--dry-run=false")) install = install with { Arguments = [.. install.Arguments, "--dry-run=false"] };
            var uninstall = operation == NpmOperation.Doctor
                ? Request(install, "uninstall", "@openai/codex", "--ignore-scripts", "--dry-run=false", "--no-audit", "--no-fund") : null;
            var proposal = new NpmProposal(operation, report.Selected!.Path, report.Prefix!, report.Available!,
                report.Selected.Package!.Version, uninstall, install, files, source) { RouteGuard = routeGuard };
            report.Install = proposal.Install;
            ValidateProposal(proposal, operation);
            report.Proposal = proposal with { Token = Token(proposal) };
            report.State = UpdateState.Ready;
        }
        catch (OperationCanceledException)
        {
            report.State = UpdateState.Interrupted;
            report.Issues.Add(new("DERT5301", selectedPath ?? "codex", "The transaction checks were interrupted. No package change is approved."));
        }
        catch (Exception failure) when (IsEvidenceError(failure))
        { report.State = UpdateState.Unavailable; report.Proposal = null; report.Issues.Add(new("DERT5302", selectedPath ?? "codex", failure.Message)); }
        return report;
    }

    internal static NpmApplyResult Apply(NpmOperation operation, UpdateReport report, string token,
        ICommandRunner? runner = null, string? receiptDirectory = null,
        CancellationToken cancellation = default, Func<DiscoveryEnvironment>? environmentReader = null)
    {
        var phases = new List<NpmPhase>();
        var plan = report.Proposal;
        string? receiptPath = null;
        FileStream? receipt = null;
        FileStream? transactionLock = null;
        bool mutationStarted = false;
        string currentPhase = "review";
        try
        {
            cancellation.ThrowIfCancellationRequested();
            if (report.State != UpdateState.Ready || plan is null || plan.Operation != operation || token != plan.Token || Token(plan) != plan.Token)
                return Result(NpmApplyState.Refused, "The approval does not match this exact operation and proposal. Inspect again and review its current token.");
            ValidateProposal(plan, operation);
            NpmRouteGuardChecks.Verify(plan.RouteGuard, environmentReader);
            VerifyFiles(plan.Files);
            VerifySource(plan.Source);
            currentPhase = "receipt";
            // Both operations lock the canonical installation, independently of
            // receipt folders and configurable state locations. Keep the lock file.
            var lockPath = Path.Combine(plan.Prefix, ".dert-npm-transaction.lock");
            if (!Same(lockPath, DiscoveryFiles.ResolveLocal(lockPath))) throw new IOException("The npm transaction lock redirects elsewhere.");
            transactionLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            currentPhase = "review";
            runner ??= new SystemCommandRunner(cancellation);
            var inventory = Request(plan.Install, "ls", "--depth=0", "--long", "--json", "@openai/codex");
            var root = Run("target-recheck", Request(plan.Install, "root")).Trim();
            if (!Path.IsPathFullyQualified(root) || !Same(DiscoveryFiles.ResolveLocal(root), Path.Combine(plan.Prefix, "node_modules")))
                throw new IOException("npm's target folder changed. Inspect again before approving this operation.");
            using (var json = JsonDocument.Parse(Run("inventory-recheck", inventory)))
                NpmUpdateRehearsal.VerifyInventory(json.RootElement, plan.Prefix,
                    Path.Combine(plan.Prefix, "node_modules", "@openai", "codex"), plan.InstalledVersion);
            VerifyFiles(plan.Files);
            VerifySource(plan.Source);
            NpmRouteGuardChecks.Verify(plan.RouteGuard, environmentReader);
            cancellation.ThrowIfCancellationRequested();
            currentPhase = "receipt";
            var directory = receiptDirectory ?? Path.Combine(StateHome(), operation == NpmOperation.Doctor ? "doctor-receipts" : "update-receipts");
            directory = Path.GetFullPath(directory);
            if (!Same(directory, DiscoveryFiles.ResolveLocal(directory)))
                throw new IOException("The operation receipt folder redirects elsewhere. Choose a regular local folder.");
            if (IsWithin(directory, Path.Combine(plan.Prefix, "node_modules")))
                throw new IOException("The operation receipt must be outside npm's package folder so uninstall cannot remove it.");
            Directory.CreateDirectory(directory);
            receiptPath = Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffff") + "-" + Guid.NewGuid().ToString("N") + ".jsonl");
            receipt = new FileStream(receiptPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
            Write("prepared", new { schema = 3, plan.Operation, plan.Token, plan.SelectedPath, plan.Prefix, plan.InstalledVersion, plan.Version, plan.Uninstall, plan.Install, plan.Source, plan.Files, plan.RouteGuard,
                note = "A package change is not atomic. Failure may leave this copy absent or partial. No rollback is promised." });
            foreach (var phase in phases) Write(phase.Name, phase);
            if (plan.Uninstall is { } uninstall)
            {
                currentPhase = "uninstall";
                VerifyFiles(plan.Files);
                VerifySource(plan.Source);
                NpmRouteGuardChecks.Verify(plan.RouteGuard, environmentReader);
                cancellation.ThrowIfCancellationRequested();
                Write("uninstall-started", uninstall);
                mutationStarted = true;
                var removed = runner.Run(uninstall);
                Record("uninstall", removed);
                currentPhase = removed.Succeeded ? "install" : "uninstall";
                NpmRouteGuardChecks.Verify(plan.RouteGuard, environmentReader);
                if (!removed.Succeeded) return Result(removed.ExitCode == 130 ? NpmApplyState.Interrupted : NpmApplyState.UninstallFailed, "Uninstall did not complete; installation was not started. This copy may be partial. Review the receipt before using the exact recovery request.");
                currentPhase = "install";
                cancellation.ThrowIfCancellationRequested();
                VerifyManager(plan);
                VerifySource(plan.Source);
                NpmRouteGuardChecks.Verify(plan.RouteGuard, environmentReader);
                // npm must actually remove its package, not merely exit zero (for example,
                // an ambient dry-run setting). Never report a reinstall if removal did not occur.
                if (Directory.Exists(Path.Combine(plan.Prefix, "node_modules", "@openai", "codex")))
                    return Result(NpmApplyState.UninstallFailed, "npm exited successfully but the selected package folder remains. Installation was stopped; inspect the receipt and this copy.");
            }
            else
            {
                currentPhase = "install";
                VerifyFiles(plan.Files);
                VerifySource(plan.Source);
                VerifyManager(plan);
                NpmRouteGuardChecks.Verify(plan.RouteGuard, environmentReader);
                cancellation.ThrowIfCancellationRequested();
            }
            VerifyPendingLauncherSlots(plan);
            cancellation.ThrowIfCancellationRequested();
            Write("install-started", plan.Install);
            mutationStarted = true;
            var installed = runner.Run(plan.Install);
            Record("install", installed);
            currentPhase = installed.Succeeded ? "verification" : "install";
            NpmRouteGuardChecks.Verify(plan.RouteGuard, environmentReader);
            if (!installed.Succeeded) return Result(installed.ExitCode == 130 ? NpmApplyState.Interrupted : NpmApplyState.InstallFailed, "Installation did not complete. This copy may be absent or partial. The receipt retains the exact pinned recovery request; no rollback was attempted.");
            currentPhase = "verification";
            VerifyManager(plan);
            VerifySource(plan.Source);
            NpmRouteGuardChecks.Verify(plan.RouteGuard, environmentReader);
            var packageRoot = Path.Combine(plan.Prefix, "node_modules", "@openai", "codex");
            var selected = CodexPackageProbe.Inspect(plan.SelectedPath, DiscoveryFiles.ResolveLocal(plan.SelectedPath), 1,
                [Path.GetDirectoryName(plan.Install.Program)!]);
            if (selected.Package is not { } package || package.Version != plan.Version || !Same(package.Root, packageRoot) ||
                selected.Issues.Count > 0 || !DiscoveryFiles.IsFile(Path.Combine(packageRoot, "bin", "codex.js")))
                throw new IOException("The expected Codex package, launcher, and JavaScript entrypoint were not all verified after install.");
            var installedFiles = CaptureInputs(new(report.Discovery)
                { Selected = selected, Prefix = plan.Prefix, NpmCli = plan.Install.Arguments[0] });
            using (var json = JsonDocument.Parse(Run("inventory-after-install", inventory, afterMutation: true)))
                NpmUpdateRehearsal.VerifyInventory(json.RootElement, plan.Prefix, packageRoot, plan.Version);
            VerifyFiles(installedFiles);
            VerifyManager(plan);
            VerifySource(plan.Source);
            NpmRouteGuardChecks.Verify(plan.RouteGuard, environmentReader);
            cancellation.ThrowIfCancellationRequested();
            if (plan.RouteGuard is { } route)
            {
                NpmRouteGuardChecks.VerifyResolved(route, environmentReader);
                // Route inspection performs more filesystem reads. Close the
                // same source/package evidence boundary after that work, too.
                VerifyFiles(installedFiles);
                VerifyManager(plan);
                VerifySource(plan.Source);
                cancellation.ThrowIfCancellationRequested();
                var phase = new NpmPhase(NpmRouteGuardChecks.VerifiedPhase, true,
                    "The reviewed leading generated npm PowerShell route now has a readable declared entrypoint; external PATH order and protected later-copy evidence are unchanged. Parent-shell behavior and Codex runtime health were not tested.");
                Write(phase.Name, phase);
                phases.Add(phase);
            }
            Write("verified", new { version = plan.Version, packageRoot, scope = "Manager inventory and launcher/JavaScript entrypoint verified. Codex itself was not launched; authentication and native runtime health are not established." });
            return Result(NpmApplyState.Succeeded, (operation == NpmOperation.Doctor ? "Reinstalled" : "Updated to") + " the reviewed Codex version; npm inventory and launcher/JavaScript entrypoint are verified. Codex itself was not launched.");
        }
        catch (OperationCanceledException)
        { return Result(NpmApplyState.Interrupted, mutationStarted ? "The operation was interrupted after package changes began. This copy may be absent or partial; use the receipt's recovery request after inspecting its state." : "The operation was interrupted before package changes began."); }
        catch (Exception failure) when (IsEvidenceError(failure))
        {
            var state = currentPhase == "receipt" || failure is ReceiptWriteException ? NpmApplyState.ReceiptFailed : !mutationStarted ? NpmApplyState.Refused :
                currentPhase == "uninstall" ? NpmApplyState.UninstallFailed : currentPhase == "install" ? NpmApplyState.InstallFailed : NpmApplyState.VerificationFailed;
            return Result(state, failure.Message + (mutationStarted ? " Package changes began; this copy may be absent or partial. No rollback was attempted." : " No package changes began."));
        }
        finally { receipt?.Dispose(); transactionLock?.Dispose(); }

        string Run(string phase, CommandRequest request, bool afterMutation = false)
        {
            cancellation.ThrowIfCancellationRequested();
            if (afterMutation) VerifyManager(plan!); else VerifyFiles(plan!.Files);
            VerifySource(plan.Source);
            NpmRouteGuardChecks.Verify(plan.RouteGuard, environmentReader);
            var response = runner!.Run(request);
            phases.Add(new(phase, response.Succeeded, Detail(response)));
            if (receipt is not null) Write(phase, phases[^1]);
            if (afterMutation) VerifyManager(plan); else VerifyFiles(plan.Files);
            VerifySource(plan.Source);
            NpmRouteGuardChecks.Verify(plan.RouteGuard, environmentReader);
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
            try { receipt!.Write(bytes); receipt.Flush(flushToDisk: true); }
            catch (Exception failure) when (IsEvidenceError(failure))
            { throw new ReceiptWriteException("The transaction receipt could not be saved: " + failure.Message, failure); }
        }
        NpmApplyResult Result(NpmApplyState state, string message)
        {
            if (receipt is not null)
            {
                try { Write("result", new { state = state.ToString(), message }); }
                catch (Exception failure) when (IsEvidenceError(failure))
                { return new(NpmApplyState.ReceiptFailed, message + " The final receipt could not be saved: " + failure.Message, receiptPath, mutationStarted ? plan?.Install : null, phases); }
            }
            return new(state, message, receiptPath, mutationStarted && state != NpmApplyState.Succeeded ? plan?.Install : null, phases);
        }
    }

    private static IReadOnlyList<NpmFile> CaptureInputs(UpdateReport update)
    {
        var selected = update.Selected!;
        var package = selected.Package!;
        var prefix = update.Prefix!;
        var packageRoot = Path.Combine(prefix, "node_modules", "@openai", "codex");
        var npmRoot = Path.Combine(prefix, "node_modules", "npm");
        var current = CodexPackageProbe.Inspect(selected.Path, DiscoveryFiles.ResolveLocal(selected.Path), selected.PathPosition,
            [Path.GetDirectoryName(package.NodePath!)!]);
        if (current.Package is not { NodePath: not null, NpmPath: not null } currentPackage ||
            currentPackage.Version != package.Version || !Same(currentPackage.Root, packageRoot) ||
            !Same(DiscoveryFiles.ResolveLocal(currentPackage.NodePath), DiscoveryFiles.ResolveLocal(package.NodePath!)) ||
            !Same(DiscoveryFiles.ResolveLocal(currentPackage.NpmPath), DiscoveryFiles.ResolveLocal(package.NpmPath!)))
            throw new IOException("The discovered installation changed before its evidence could be captured. Inspect this copy again.");
        if (!Same(DiscoveryFiles.ResolveLocal(package.MetadataPath), Path.Combine(packageRoot, "package.json")) ||
            !Same(DiscoveryFiles.ResolveLocal(package.NpmPath!), Path.Combine(prefix, "npm.cmd")) ||
            !Same(DiscoveryFiles.ResolveLocal(Path.GetDirectoryName(selected.Path)!), prefix) ||
            !Same(selected.Identity, Path.Combine(prefix, Path.GetFileName(selected.Path))))
            throw new IOException("The discovered package metadata or npm launcher does not match the selected prefix.");
        foreach (var path in new[] { prefix, packageRoot, npmRoot, Path.Combine(packageRoot, "package.json"), Path.Combine(prefix, "npm.cmd"), Path.Combine(npmRoot, "bin", "npm-cli.js") })
            if (!Same(Path.GetFullPath(path), DiscoveryFiles.ResolveLocal(path)))
                throw new IOException("This operation requires regular local package and manager paths; a redirected path needs separate handling: " + path);
        var paths = new List<string> { selected.Path, package.MetadataPath, package.NodePath!, package.NpmPath!,
            Path.Combine(prefix, "npm.cmd"), Path.Combine(npmRoot, "package.json"), update.NpmCli!, Path.Combine(packageRoot, "bin", "codex.js") };
        foreach (var name in new[] { "codex", "codex.cmd", "codex.ps1" })
        {
            var path = Path.Combine(prefix, name);
            if (File.Exists(path))
            {
                var match = CodexPackageProbe.Inspect(path, DiscoveryFiles.ResolveLocal(path), 1, [Path.GetDirectoryName(package.NodePath!)!]);
                if (match.Package is null || !Same(match.Package.Root, packageRoot))
                    throw new IOException("An npm launcher slot belongs to an unidentified target. Installation would risk replacing it: " + path);
            }
            paths.Add(path);
        }
        return Array.AsReadOnly(paths.Distinct(StringComparer.OrdinalIgnoreCase).Select(NpmFile.Capture).ToArray());
    }
    private static void VerifyFiles(IEnumerable<NpmFile> files)
    {
        foreach (var original in files)
        {
            var current = NpmFile.Capture(original.Path);
            if (!Same(original.Identity, current.Identity) || current.Sha256 != original.Sha256)
                throw new IOException("The reviewed evidence changed; inspect again before approving this operation: " + original.Path);
        }
    }
    private static void VerifyManager(NpmProposal plan)
    {
        if (!Same(DiscoveryFiles.ResolveLocal(Path.GetDirectoryName(plan.SelectedPath)!), plan.Prefix))
            throw new IOException("The selected command's prefix link changed. The operation stopped before using another installation.");
        foreach (var path in new[] { plan.Prefix, Path.Combine(plan.Prefix, "node_modules"),
            Path.Combine(plan.Prefix, "node_modules", "@openai"), Path.Combine(plan.Prefix, "node_modules", "@openai", "codex") })
            if (!Same(path, DiscoveryFiles.ResolveLocal(path)))
                throw new IOException("The selected package destination now redirects elsewhere: " + path);
        var npmRoot = Path.Combine(plan.Prefix, "node_modules", "npm");
        VerifyFiles(plan.Files.Where(file => Same(file.Identity, plan.Install.Program) || IsWithin(file.Path, npmRoot) || Same(file.Path, Path.Combine(plan.Prefix, "npm.cmd"))));
    }
    private static void VerifySource(NpmSource? source)
    {
        if (source is not null) VerifyFiles([new(source.Path, source.Identity, source.Sha256)]);
    }
    private static void VerifyPendingLauncherSlots(NpmProposal plan)
    {
        foreach (var name in new[] { "codex", "codex.cmd", "codex.ps1" })
        {
            var path = Path.Combine(plan.Prefix, name);
            var original = plan.Files.First(file => Same(file.Path, path));
            var current = NpmFile.Capture(path);
            // npm may have removed its shim or left the reviewed shim in place.
            // A new occupant belongs to a different review, even if npm would
            // otherwise decide it is safe to replace that file.
            if (current.Sha256 is null ? !Same(current.Identity, path) :
                current.Sha256 != original.Sha256 || !Same(current.Identity, original.Identity))
                throw new IOException("A launcher slot changed before install. Installation stopped before replacing another command: " + path);
        }
    }
    private static CommandRequest Request(CommandRequest basis, params string[] args) => new(basis.Program,
        Array.AsReadOnly(new[] { basis.Arguments[0] }.Concat(args).Concat(["--global", "--prefix", basis.WorkingDirectory, "--no-update-notifier", "--color=false"]).ToArray()),
        basis.WorkingDirectory, basis.Timeout);
    private static string Token(NpmProposal plan) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
    { schema = 3, plan.Operation, plan.SelectedPath, plan.Prefix, plan.Version, plan.InstalledVersion, plan.Uninstall, plan.Install, plan.Files, plan.Source, plan.RouteGuard }))));
    internal static string SaveProposal(NpmOperation operation, UpdateReport report, string? proposalDirectory = null,
        Func<DiscoveryEnvironment>? environmentReader = null)
    {
        var plan = report.Proposal;
        if (report.State != UpdateState.Ready || plan is null || plan.Operation != operation || Token(plan) != plan.Token)
            throw new IOException("There is no complete reviewed proposal to save.");
        ValidateProposal(plan, operation);
        VerifyFiles(plan.Files);
        VerifySource(plan.Source);
        NpmRouteGuardChecks.Verify(plan.RouteGuard, environmentReader);
        var directory = ProposalDirectory(operation, proposalDirectory);
        if (IsWithin(directory, Path.Combine(plan.Prefix, "node_modules")))
            throw new IOException("Store transaction proposals outside npm's package folder.");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, plan.Token + ".json");
        if (File.Exists(path))
        {
            LoadProposal(operation, plan.Token, directory, environmentReader);
            report.ProposalPath = path;
            return path;
        }
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new SavedProposal(3, plan));
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.WriteThrough);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
        stream.Position = 0;
        using var actual = new MemoryStream();
        stream.CopyTo(actual);
        if (!actual.ToArray().AsSpan().SequenceEqual(bytes)) throw new IOException("The saved proposal bytes could not be verified. Inspect again before approval.");
        VerifyFiles(plan.Files);
        VerifySource(plan.Source);
        NpmRouteGuardChecks.Verify(plan.RouteGuard, environmentReader);
        report.ProposalPath = path;
        return path;
    }

    // Loading never invokes Node/npm. The old snapshot is checked before any
    // manager execution, instead of generating a fresh approval from changed files.
    internal static UpdateReport LoadProposal(NpmOperation operation, string token, string? proposalDirectory = null,
        Func<DiscoveryEnvironment>? environmentReader = null)
    {
        if (token.Length != 64 || !token.All(char.IsAsciiHexDigit))
            throw new IOException("The approval token is not a complete transaction proposal identifier.");
        var path = Path.Combine(ProposalDirectory(operation, proposalDirectory), token.ToUpperInvariant() + ".json");
        if (!Same(path, DiscoveryFiles.ResolveLocal(path))) throw new IOException("The saved proposal redirects elsewhere.");
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > 1024 * 1024) throw new IOException("The saved transaction proposal exceeds its inspection limit.");
        SavedProposal? saved;
        try { saved = JsonSerializer.Deserialize<SavedProposal>(stream); }
        catch (Exception failure) when (IsEvidenceError(failure) || failure is NullReferenceException)
        { throw new IOException("The saved transaction proposal could not be understood. Inspect again for a current review.", failure); }
        var plan = saved?.Proposal;
        if (saved?.Schema != 3 || plan is null || plan.Operation != operation || plan.Token != token || Token(plan) != token ||
            !Path.IsPathFullyQualified(plan.Prefix) || !Path.IsPathFullyQualified(plan.SelectedPath) ||
            !Same(DiscoveryFiles.ResolveLocal(Path.GetDirectoryName(plan.SelectedPath)!), plan.Prefix))
            throw new IOException("The saved proposal no longer matches this operation and approval. Inspect again for a current review.");
        ValidateProposal(plan, operation);
        VerifyFiles(plan.Files);
        VerifySource(plan.Source);
        NpmRouteGuardChecks.Verify(plan.RouteGuard, environmentReader);
        var root = Path.Combine(plan.Prefix, "node_modules", "@openai", "codex");
        var selected = new CommandMatch(plan.SelectedPath, 1, DiscoveryFiles.ResolveLocal(plan.SelectedPath),
            new(root, Path.Combine(root, "package.json"), plan.InstalledVersion, plan.Install.Program, Path.Combine(plan.Prefix, "npm.cmd")), []);
        var discovery = new DiscoveryReport([selected], [], 0);
        return new(discovery) { State = UpdateState.Ready, Proposal = plan, ProposalPath = path,
            Selected = selected, Prefix = plan.Prefix, Node = plan.Install.Program,
            NpmCli = plan.Install.Arguments[0], Available = plan.Version, Install = plan.Install };
    }
    private sealed record SavedProposal(int Schema, NpmProposal Proposal);
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
    private static string ProposalDirectory(NpmOperation operation, string? directory)
    {
        directory = Path.GetFullPath(directory ?? Path.Combine(StateHome(), operation == NpmOperation.Doctor ? "doctor-proposals" : "update-proposals"));
        if (!Same(directory, DiscoveryFiles.ResolveLocal(directory))) throw new IOException("The transaction proposal folder redirects elsewhere.");
        return directory;
    }
    private static void ValidateProposal(NpmProposal plan, NpmOperation operation)
    {
        if (plan.Operation != operation || !Enum.IsDefined(operation) ||
            !Path.IsPathFullyQualified(plan.Prefix) || !Same(plan.Prefix, DiscoveryFiles.ResolveLocal(plan.Prefix)) ||
            !Path.IsPathFullyQualified(plan.SelectedPath) || !Path.IsPathFullyQualified(plan.Install.Program) ||
            !NpmUpdateRehearsal.StableVersion(plan.Version, out var target) ||
            !NpmUpdateRehearsal.StableVersion(plan.InstalledVersion, out var installed) ||
            target < installed || operation == NpmOperation.Update && target == installed)
            throw new IOException("The saved transaction does not identify a supported version transition and local installation. Inspect again.");
        var npmRoot = Path.Combine(plan.Prefix, "node_modules", "npm");
        var packageRoot = Path.Combine(plan.Prefix, "node_modules", "@openai", "codex");
        var expectedInstall = new CommandRequest(plan.Install.Program,
            [Path.Combine(npmRoot, "bin", "npm-cli.js"), "install", "@openai/codex@" + plan.Version,
                "--engine-strict", "--force=false", "--include=optional", "--no-audit", "--no-fund",
                "--global", "--prefix", plan.Prefix, "--no-update-notifier", "--color=false", "--ignore-scripts", "--dry-run=false"],
            plan.Prefix, TimeSpan.FromMinutes(5));
        if (!SameRequest(plan.Install, expectedInstall) ||
            (operation == NpmOperation.Update ? plan.Uninstall is not null : plan.Uninstall is null ||
                !SameRequest(plan.Uninstall, Request(expectedInstall, "uninstall", "@openai/codex", "--ignore-scripts", "--dry-run=false", "--no-audit", "--no-fund"))))
            throw new IOException("The saved proposal does not contain the exact supported npm requests. Inspect again before approving this operation.");
        var required = new[] { plan.SelectedPath, plan.Install.Program, Path.Combine(packageRoot, "package.json"),
            Path.Combine(plan.Prefix, "npm.cmd"), Path.Combine(npmRoot, "package.json"), expectedInstall.Arguments[0],
            Path.Combine(packageRoot, "bin", "codex.js"), Path.Combine(plan.Prefix, "codex"),
            Path.Combine(plan.Prefix, "codex.cmd"), Path.Combine(plan.Prefix, "codex.ps1") };
        if (plan.Files.Any(file => file is null || !Path.IsPathFullyQualified(file.Path) || !Path.IsPathFullyQualified(file.Identity)) ||
            !required.All(path => plan.Files.Any(file => Same(file.Path, path) || Same(file.Identity, path))) ||
            !Same(DiscoveryFiles.ResolveLocal(Path.GetDirectoryName(plan.SelectedPath)!), plan.Prefix))
            throw new IOException("The saved proposal is missing required installation evidence. Inspect again before approval.");
        NpmRouteGuardChecks.Validate(plan);

        static bool SameRequest(CommandRequest first, CommandRequest second) => first.Program == second.Program &&
            first.WorkingDirectory == second.WorkingDirectory && first.Timeout == second.Timeout && first.Arguments.SequenceEqual(second.Arguments);
    }
    private sealed class ReceiptWriteException(string message, Exception inner) : IOException(message, inner);
    private sealed class BoundaryRunner(ICommandRunner inner, Action verify) : ICommandRunner
    {
        public CommandResult Run(CommandRequest request)
        {
            try { return inner.Run(request); }
            finally { verify(); }
        }
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
