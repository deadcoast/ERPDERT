using System.Text.Json;

// A test-only external-process recorder. Not included in the product publish.
// It refuses actual installs. Explicit repair simulation changes owned fixture
// metadata only, never invokes npm, and stays inside this executable's prefix.
var prefixIndex = Array.IndexOf(args, "--prefix");
if (args.Length < 2 || prefixIndex < 0 || prefixIndex + 1 >= args.Length) return 90;
var prefix = Path.GetFullPath(args[prefixIndex + 1]);
if (!string.Equals(prefix, Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory), StringComparison.OrdinalIgnoreCase)) return 91;
File.AppendAllText(Path.Combine(prefix, "calls.jsonl"), JsonSerializer.Serialize(args) + "\n");
var command = args[1];
using var configuration = JsonDocument.Parse(File.ReadAllText(Path.Combine(prefix, "scenario.json")));
var simulateRepair = configuration.RootElement.TryGetProperty("simulateRepair", out var repair) && repair.GetBoolean();
if (command == "install" && !args.Contains("--dry-run") && !simulateRepair)
{
    Console.WriteLine("Manual invocation captured; fixture refuses installation.");
    return 73;
}
var fault = configuration.RootElement.GetProperty("fault").GetString();
var latest = configuration.RootElement.GetProperty("latest").GetString();
if (fault == command)
{
    Console.Error.WriteLine("fixture npm failure at " + command);
    return 1;
}
if (fault == command + "-cancelled") return 130;
if (simulateRepair && (command == "uninstall" || command == "install" && !args.Contains("--dry-run")))
{
    if (!args.Contains("--ignore-scripts") || !args.Contains("--dry-run=false")) return 94;
    var packageRoot = Path.GetFullPath(Path.Combine(prefix, "node_modules", "@openai", "codex"));
    if (!packageRoot.StartsWith(prefix + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return 95;
    if (command == "uninstall") Directory.Delete(packageRoot, recursive: true);
    else
    {
        Directory.CreateDirectory(Path.Combine(packageRoot, "bin"));
        File.WriteAllText(Path.Combine(packageRoot, "package.json"), JsonSerializer.Serialize(new { name = "@openai/codex", version = latest, bin = new { codex = "bin/codex.js" } }));
        File.WriteAllText(Path.Combine(packageRoot, "bin", "codex.js"), "fixture entrypoint; never launched");
    }
    Console.WriteLine("Owned fixture repair phase completed.");
    return 0;
}
switch (command)
{
    case "root":
        Console.WriteLine(Path.Combine(prefix, fault == "wrong-root" ? "wrong" : "node_modules"));
        return 0;
    case "ls":
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            path = prefix,
            dependencies = new Dictionary<string, object>
            {
                ["@openai/codex"] = new { name = "@openai/codex", version = JsonDocument.Parse(File.ReadAllText(Path.Combine(prefix, "node_modules", "@openai", "codex", "package.json"))).RootElement.GetProperty("version").GetString(), path = Path.Combine(prefix, "node_modules", "@openai", "codex"), extraneous = false }
            }
        }));
        return 0;
    case "view":
        Console.WriteLine(JsonSerializer.Serialize(new { version = latest, engines = new { node = ">=16" } }));
        return 0;
    case "install":
        if (!args.Contains("--ignore-scripts") || !args.Contains("--engine-strict") || !args.Contains("--force=false") || !args.Contains("--include=optional")) return 92;
        Console.WriteLine($"change @openai/codex 1.0.0 => {latest}");
        Console.WriteLine("{\"added\":0,\"removed\":0,\"changed\":1}");
        return 0;
    default:
        return 93;
}
