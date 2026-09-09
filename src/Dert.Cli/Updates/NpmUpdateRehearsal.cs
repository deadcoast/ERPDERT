using Dert.Cli.Discovery;
using Dert.Cli.Operations;
using System.Globalization;
using System.Text.Json;

namespace Dert.Cli.Updates;

internal static class NpmUpdateRehearsal
{
    internal static UpdateReport Create(DiscoveryReport discovery, string? selectedPath,
        ICommandRunner? runner, Action<string>? progress = null, Action<UpdateActivity>? activity = null,
        bool repair = false, Action<UpdateReport>? beforeCommand = null)
    {
        var result = new UpdateReport(discovery);
        if (discovery.Commands.Count == 0)
        {
            result.Issues.Add(new("ERP5201", "PATH", "No codex command was discovered. An update needs an identified installed copy; this is not an installation request."));
            return result;
        }
        if (selectedPath is null && (discovery.Installations.Count > 1 || discovery.Issues.Count > 0))
        {
            result.State = UpdateState.ChooseCopy;
            return result;
        }
        CommandMatch? selected = null;
        try
        {
            if (selectedPath is null) selected = discovery.Commands[0];
            else if (Path.IsPathFullyQualified(selectedPath))
            {
                var normalized = Path.GetFullPath(selectedPath);
                selected = discovery.Commands.FirstOrDefault(command => SamePath(Path.GetFullPath(command.Path), normalized) || SamePath(command.Identity, normalized));
            }
        }
        catch (Exception error) when (DiscoveryFiles.IsReadError(error)) { /* Report this as an unrecognized selection below. */ }
        if (selected is null)
        {
            result.Issues.Add(new("ERP5201", selectedPath!, "That path is not one of the discovered Codex launchers. Run discovery again and choose its complete path."));
            return result;
        }
        result.Selected = selected;
        if (selected.Package is not { NodePath: not null, NpmPath: not null })
        {
            result.Issues.Add(new("ERP5202", selected.Path, "This copy needs an identified package, Node, and a matching nearby npm before an update can be rehearsed. Another npm on PATH was not substituted."));
            return result;
        }
        try
        {
            var package = selected.Package;
            var prefix = DiscoveryFiles.ResolveLocal(Path.GetDirectoryName(selected.Path)!);
            var expectedRoot = Path.Combine(prefix, "node_modules", "@openai", "codex");
            if (!SamePath(package.Root, expectedRoot))
                throw new CheckFailed("ERP5202", package.Root, "This package redirects outside the selected npm prefix. Updating a linked package needs a separate, explicit treatment.");
            result.Prefix = prefix;
            result.Node = DiscoveryFiles.ResolveLocal(package.NodePath);
            result.NpmCli = DiscoveryFiles.ResolveLocal(Path.Combine(prefix, "node_modules", "npm", "bin", "npm-cli.js"));
            var metadata = Path.Combine(expectedRoot, "package.json");
            var original = DiscoveryFiles.Read(metadata);
            var originalLauncher = DiscoveryFiles.Read(selected.Path);
            runner ??= new SystemCommandRunner();

            var root = Query(UpdatePhase.Prefix, "npm / target folder", "root").Trim();
            if (!Path.IsPathFullyQualified(root) || !SamePath(DiscoveryFiles.ResolveLocal(root), Path.Combine(prefix, "node_modules")))
                throw new CheckFailed("ERP5204", root, "npm's reported global package folder does not match this selected copy. No other prefix was substituted.");
            result.Checks.Add(new("TARGET FOLDER", prefix, IsPath: true));
            activity?.Invoke(new(UpdatePhase.Prefix, true, prefix, true));

            using (var inventory = JsonDocument.Parse(Query(UpdatePhase.Inventory, "npm / installed copy", "ls", "--depth=0", "--long", "--json", "@openai/codex")))
            {
                try { VerifyInventory(inventory.RootElement, prefix, expectedRoot, package.Version); }
                catch (IOException failure) { throw new CheckFailed("ERP5204", metadata, failure.Message); }
            }
            result.Checks.Add(new("MANAGER INVENTORY", $"npm sees @openai/codex {package.Version} in this exact folder; this does not prove who originally installed it."));
            activity?.Invoke(new(UpdatePhase.Inventory, true, result.Checks[^1].Detail));

            using (var available = JsonDocument.Parse(Query(UpdatePhase.Release, "registry / latest release", "view", "@openai/codex@latest", "version", "engines", "--json", "--prefer-online", "--fetch-retries=0", "--fetch-timeout=15000")))
            {
                // npm 12 always wraps view --json in an array. Older npm emits
                // an object for one package. Never choose from multiple results.
                var release = available.RootElement;
                if (release.ValueKind == JsonValueKind.Array)
                {
                    if (release.GetArrayLength() != 1) throw new IOException("The registry response did not identify exactly one latest release.");
                    release = release[0];
                }
                result.Available = String(release, "version");
                var engine = String(Property(release, "engines"), "node");
                if (!StableVersion(package.Version, out var installed) || !StableVersion(result.Available, out var latest))
                    throw new CheckFailed("ERP5205", metadata, "This rehearsal compares stable three-part versions. A missing, prerelease, or unrecognized version needs channel-aware handling; no upgrade or downgrade was guessed.");
                result.Checks.Add(new("REGISTRY RELEASE", $"latest resolves to {result.Available}; declared Node requirement: {engine ?? "not supplied"}."));
                CheckUnchanged();
                activity?.Invoke(new(UpdatePhase.Release, true, result.Checks[^1].Detail));
                if (installed == latest) result.State = UpdateState.Current;
                else if (installed > latest) result.State = UpdateState.Ahead;
                if (result.State == UpdateState.Ahead || result.State == UpdateState.Current && !repair) return result;
            }

            var install = Request("install", $"@openai/codex@{result.Available}", "--engine-strict", "--force=false", "--include=optional", "--no-audit", "--no-fund");
            if (repair) install = install with { Arguments = [.. install.Arguments, "--ignore-scripts", "--dry-run=false"] };
            var dryRun = install with { Arguments = [.. install.Arguments, "--dry-run", "--ignore-scripts", "--json"] };
            using (var dry = ReadDryRun(Execute(UpdatePhase.DryRun, "npm / dependency rehearsal", dryRun), result.ProposedChanges))
            {
                var added = Count(dry.RootElement, "added");
                var removed = Count(dry.RootElement, "removed");
                var changed = Count(dry.RootElement, "changed");
                result.Checks.Add(new("NPM DRY RUN", $"{Number(added, "addition")} · {Number(removed, "removal")} · {Number(changed, "change")} proposed by npm. Package scripts were disabled."));
            }
            CheckUnchanged();
            activity?.Invoke(new(UpdatePhase.DryRun, true, result.Checks[^1].Detail));
            result.Install = install;
            result.DryRun = dryRun;
            result.State = UpdateState.Ready;

            CommandRequest Request(params string[] arguments) => new(result.Node,
                [result.NpmCli, .. arguments, "--global", "--prefix", prefix, "--no-update-notifier", "--color=false"],
                prefix, TimeSpan.FromSeconds(30));
            string Query(UpdatePhase phase, string stage, params string[] arguments) => Execute(phase, stage, Request(arguments));
            string Execute(UpdatePhase phase, string stage, CommandRequest request)
            {
                progress?.Invoke(stage);
                activity?.Invoke(new(phase, false, stage));
                beforeCommand?.Invoke(result);
                var response = runner.Run(request);
                if (response.ExitCode == 130) throw new OperationCanceledException("The npm query was interrupted.");
                if (!response.Succeeded)
                {
                    var details = response.Failure ?? (response.Error.Length > 0 ? response.Error : response.Output);
                    if (details.Length > 3000) details = details[..3000] + " [truncated]";
                    throw new CheckFailed("ERP5203", result.NpmCli, $"{stage} did not complete (exit {response.ExitCode}). No update command is offered. {details}");
                }
                if (!string.IsNullOrWhiteSpace(response.Error))
                    result.Issues.Add(new("ERP5208", result.NpmCli, $"{stage} returned a warning: {response.Error[..Math.Min(response.Error.Length, 3000)]}"));
                return response.Output;
            }
            void CheckUnchanged()
            {
                if (!SamePath(DiscoveryFiles.ResolveLocal(Path.GetDirectoryName(selected.Path)!), prefix) ||
                    !SamePath(DiscoveryFiles.ResolveLocal(package.NodePath), result.Node) ||
                    original != DiscoveryFiles.Read(metadata) || originalLauncher != DiscoveryFiles.Read(selected.Path))
                    throw new CheckFailed("ERP5206", selected.Path, "The selected installation changed during the checks. Rehearse again against its current state; the earlier result was not reused.");
            }
        }
        catch (OperationCanceledException)
        {
            result.State = UpdateState.Interrupted;
            result.Issues.Add(new("ERP5203", selected.Path, "The npm query was interrupted. No package-changing request is offered."));
        }
        catch (CheckFailed failure) { result.Issues.Add(new(failure.Code, failure.Path, failure.Message)); }
        catch (Exception failure) when (DiscoveryFiles.IsReadError(failure) || failure is JsonException or InvalidOperationException)
        {
            result.Issues.Add(new("ERP5207", selected.Path, $"The update evidence could not be read or understood. No update command is offered. {failure.Message}"));
        }
        return result;
    }

    private static bool SamePath(string first, string second) => string.Equals(Path.TrimEndingDirectorySeparator(first), Path.TrimEndingDirectorySeparator(second), StringComparison.OrdinalIgnoreCase);
    // Shared evidence contract for update rehearsals and doctor's pre/post checks.
    internal static void VerifyInventory(JsonElement info, string prefix, string expectedRoot, string version)
    {
        var entry = Property(Property(info, "dependencies"), "@openai/codex");
        var listedRoot = String(info, "path");
        var listedPath = String(entry, "path");
        if (String(entry, "name") != "@openai/codex" || String(entry, "version") != version ||
            listedRoot is null || !Path.IsPathFullyQualified(listedRoot) || !SamePath(DiscoveryFiles.ResolveLocal(listedRoot), prefix) ||
            listedPath is null || !Path.IsPathFullyQualified(listedPath) || !SamePath(DiscoveryFiles.ResolveLocal(listedPath), expectedRoot) ||
            HasProblems(info) || HasProblems(entry) || Property(entry, "link").ValueKind == JsonValueKind.True)
            throw new IOException("npm did not confirm the same package path and installed version that discovery found. The inventory may be damaged, linked, or changing; inspect this copy before updating.");
    }
    private static string Number(int count, string singular) => $"{count} {singular}{(count == 1 ? "" : "s")}";
    private static JsonDocument ReadDryRun(string output, List<string> changes)
    {
        // npm's reify-output writes add/remove/change lines before its JSON
        // summary even with --json. Accept only that known preamble, retain it
        // for the person, and still parse the entire remaining JSON strictly.
        var remaining = output.AsSpan().TrimStart();
        while (remaining.StartsWith("add ", StringComparison.Ordinal) || remaining.StartsWith("remove ", StringComparison.Ordinal) || remaining.StartsWith("change ", StringComparison.Ordinal))
        {
            var end = remaining.IndexOf('\n');
            if (end < 0) throw new IOException("npm's change list has no JSON summary.");
            changes.Add(remaining[..end].TrimEnd('\r').ToString());
            remaining = remaining[(end + 1)..].TrimStart();
        }
        return JsonDocument.Parse(remaining.ToString());
    }
    private static JsonElement Property(JsonElement item, string name) => item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var value) ? value : default;
    private static string? String(JsonElement item, string name) => Property(item, name) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;
    private static bool HasProblems(JsonElement item) => Property(item, "problems") is { ValueKind: JsonValueKind.Array } problems && problems.GetArrayLength() > 0 || Property(item, "invalid").ValueKind == JsonValueKind.True || Property(item, "missing").ValueKind == JsonValueKind.True;
    private static int Count(JsonElement item, string name) => Property(item, name) is { ValueKind: JsonValueKind.Number } value && value.TryGetInt32(out var count) && count >= 0
        ? count : throw new IOException("npm's dry-run response is missing a valid " + name + " count.");
    private static bool StableVersion(string? text, out Version version)
    {
        version = new(0, 0, 0);
        if (text is null || text.Length > 128) return false;
        var parts = text.Split('.');
        if (parts.Length != 3 || parts.Any(part => part.Length == 0 || part.Length > 1 && part[0] == '0' || !part.All(char.IsAsciiDigit))) return false;
        var numbers = new int[3];
        for (var index = 0; index < 3; index++)
            if (!int.TryParse(parts[index], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[index])) return false;
        version = new(numbers[0], numbers[1], numbers[2]);
        return true;
    }
    private sealed class CheckFailed(string code, string path, string message) : Exception(message)
    {
        public string Code { get; } = code;
        public string Path { get; } = path;
    }
}
