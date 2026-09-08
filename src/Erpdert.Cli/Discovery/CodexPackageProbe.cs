using System.Text.Json;

namespace Erpdert.Cli.Discovery;

internal static class CodexPackageProbe
{
    internal static CommandMatch Inspect(string path, string identity, int position, IReadOnlyList<string> directories)
    {
        var issues = new List<DiscoveryIssue>();
        var extension = Path.GetExtension(path);
        if (extension is not (".ps1" or ".cmd" or ".bat" or "")) return Unknown();
        string text;
        try { text = DiscoveryFiles.Read(path); }
        catch (Exception error) when (DiscoveryFiles.IsReadError(error))
        {
            issues.Add(new("ERP5103", path, $"The launcher could not be read. Its target and manager remain unknown. {error.Message}"));
            return new(path, position, identity, null, issues.ToArray());
        }
        if (!References(text, "@openai/codex/bin/codex.js")) return Unknown();

        var prefix = Path.GetDirectoryName(path)!;
        var root = Path.Combine(prefix, "node_modules", "@openai", "codex");
        var metadata = Path.Combine(root, "package.json");
        string version;
        try
        {
            using var json = JsonDocument.Parse(DiscoveryFiles.Read(metadata));
            var package = json.RootElement;
            if (StringProperty(package, "name") != "@openai/codex" ||
                StringProperty(Property(package, "bin"), "codex") != "bin/codex.js")
                throw new IOException("Its package name or codex entrypoint does not match this launcher.");
            version = StringProperty(package, "version") ?? "";
            if (string.IsNullOrWhiteSpace(version) || version.Length > 128)
                throw new IOException("Its package version is missing or not readable.");
            root = DiscoveryFiles.ResolveLocal(root);
        }
        catch (Exception error) when (DiscoveryFiles.IsReadError(error) || error is JsonException)
        {
            issues.Add(new("ERP5104", metadata, $"The launcher refers to a package that could not be identified. {error.Message}"));
            return new(path, position, identity, null, issues.ToArray());
        }

        var entrypoint = Path.Combine(root, "bin", "codex.js");
        if (ProbeFile(entrypoint, issues, "ERP5105") is null)
            issues.Add(new("ERP5105", entrypoint, "The launcher's JavaScript entrypoint is missing or unreadable. Finding the launcher alone does not mean Codex can start."));

        var node = ProbeFile(Path.Combine(prefix, "node.exe"), issues, "ERP5106");
        if (node is null)
            foreach (var directory in directories)
            {
                node = ProbeFile(Path.Combine(directory, "node.exe"), issues, "ERP5106");
                if (node is not null) break;
            }
        if (node is null)
            issues.Add(new("ERP5106", path, "This package launcher needs Node, but no node.exe was found beside it or in the inspected PATH folders."));

        // A package's packageManager field describes its development tooling, not
        // the user's installer. Never promote it to installation ownership.
        string? npm = null;
        var npmPath = Path.Combine(prefix, "npm.cmd");
        if (ProbeFile(npmPath, issues, "ERP5107") is not null)
        {
            try
            {
                using var json = JsonDocument.Parse(DiscoveryFiles.Read(Path.Combine(prefix, "node_modules", "npm", "package.json")));
                if (References(DiscoveryFiles.Read(npmPath), "npm/bin/npm-cli.js") &&
                    StringProperty(json.RootElement, "name") == "npm" &&
                    ProbeFile(Path.Combine(prefix, "node_modules", "npm", "bin", "npm-cli.js"), issues, "ERP5107") is not null)
                    npm = npmPath;
                else
                    issues.Add(new("ERP5107", npmPath, "This nearby npm.cmd does not match the expected npm package and entrypoint. It was not selected as manager evidence."));
            }
            catch (Exception error) when (DiscoveryFiles.IsReadError(error) || error is JsonException)
            {
                issues.Add(new("ERP5107", npmPath, $"An npm launcher is nearby, but its package evidence could not be read. {error.Message}"));
            }
        }
        return new(path, position, identity, new(root, metadata, version, node, npm), issues.ToArray());

        CommandMatch Unknown()
        {
            issues.Add(new("ERP5103", path,
                "A file with this command name exists, but this adapter cannot identify its target or manager. It was not run to guess."));
            return new(path, position, identity, null, issues.ToArray());
        }
    }

    private static bool References(string text, string packageFile)
    {
        // Generated npm shims do not use block comments. A custom script with
        // them requires an adapter that understands that script, not text inside it.
        if (text.Contains("<#", StringComparison.Ordinal)) return false;
        // Evidence of a relative npm-style target, not a claim that arbitrary
        // script contents are trusted, executable, or fully understood.
        return text.Split('\n').Any(line =>
        {
            var normalized = line.Trim().Replace('\\', '/');
            if (normalized.StartsWith('#') || normalized.StartsWith("::", StringComparison.Ordinal) ||
                normalized.StartsWith("rem ", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("@rem ", StringComparison.OrdinalIgnoreCase)) return false;
            return normalized.Contains($"\"$basedir/node_modules/{packageFile}\"", StringComparison.Ordinal) ||
                normalized.Contains($"\"%dp0%/node_modules/{packageFile}\"", StringComparison.Ordinal);
        });
    }

    private static string? StringProperty(JsonElement element, string name)
    {
        var value = Property(element, name);
        if (value.ValueKind != JsonValueKind.String) return null;
        try { return value.GetString(); }
        catch (InvalidOperationException error)
        {
            // JSON permits escaped code units that cannot form a .NET string.
            throw new IOException($"The metadata field '{name}' contains an incomplete Unicode character.", error);
        }
    }

    private static JsonElement Property(JsonElement element, string name)
    {
        try
        {
            return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
                ? value : default;
        }
        catch (InvalidOperationException error)
        {
            throw new IOException("A metadata field name contains an incomplete Unicode character.", error);
        }
    }

    private static string? ProbeFile(string path, List<DiscoveryIssue> issues, string code)
    {
        try
        {
            var resolved = DiscoveryFiles.ResolveLocal(path);
            return DiscoveryFiles.IsFile(resolved) ? path : null;
        }
        catch (Exception error) when (DiscoveryFiles.IsReadError(error))
        {
            issues.Add(new(code, path, $"This dependency path could not be inspected. {error.Message}"));
            return null;
        }
    }
}
