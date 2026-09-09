using System.Text;
using System.Security.Cryptography;
using Dert.Core;
using Erpdert.Cli.Discovery;
using Erpdert.Cli.Operations;
using Erpdert.Cli.Updates;
using Erpdert.Cli.Motion;
using Erpdert.Mer;
using Spectre.Console;

namespace Erpdert.Cli;

public static class CliApplication
{
    public static int Run(string[] args, IAnsiConsole console, Func<string, bool>? confirmRepair = null,
        string[]? invocation = null, Func<IReadOnlyList<NoteBoundary>, string?>? chooseBoundary = null,
        DiscoveryEnvironment? discoveryEnvironment = null, ICommandRunner? commandRunner = null, TextReader? demoInput = null, TextReader? merInput = null)
    {
        try
        {
            if (args.FirstOrDefault()?.Equals("demo", StringComparison.OrdinalIgnoreCase) == true)
                return DemoCommand.Run(args, console, demoInput ?? Console.In);
            return RunCore(args, console, confirmRepair, invocation, chooseBoundary, discoveryEnvironment, commandRunner, merInput);
        }
        catch (MerOutputException) { return 2; } // The closed output cannot carry another renderer's error report.
    }

    private static int RunCore(string[] args, IAnsiConsole console, Func<string, bool>? confirmRepair,
        string[]? invocation, Func<IReadOnlyList<NoteBoundary>, string?>? chooseBoundary,
        DiscoveryEnvironment? discoveryEnvironment, ICommandRunner? commandRunner, TextReader? merInput)
    {
        var renderer = new DocumentRenderer(console, args.Contains("--plain"), invocation, args.Contains("--still"));
        if (args.Length == 0 || args[0] == "help" || args.Any(a => a is "-help" or "--help"))
        {
            renderer.Help();
            return 0;
        }
        if (args[0].Equals("mer", StringComparison.OrdinalIgnoreCase))
            return MerCommand.Run(args, console, renderer, merInput ?? Console.In, discoveryEnvironment, commandRunner);
        if (args[0].Equals("write", StringComparison.OrdinalIgnoreCase))
            return Authoring.AuthoringCommand.Run(args.Skip(1).ToArray(), renderer);
        if (args[0].Equals("codex", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length > 1 && args[1].Equals("updateme", StringComparison.OrdinalIgnoreCase))
            {
                string? selected = null;
                for (var index = 2; index < args.Length; index++)
                {
                    if (args[index] is "--plain" or "--still") continue;
                    if (args[index] == "--path" && selected is null && index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
                        selected = args[++index];
                    else
                    {
                        renderer.Failure("Use ERPDERT codex updateme [--path <discovered-launcher>] [--still] [--plain]. This opens a rehearsal, not an update approval.");
                        return 2;
                    }
                }
                if (!OperatingSystem.IsWindows())
                {
                    renderer.Failure("The npm update rehearsal is currently implemented for Windows only.");
                    return 2;
                }
                renderer.UpdateHeading();
                var rehearsal = renderer.RehearseUpdate(CodexDiscovery.Scan(discoveryEnvironment ?? DiscoveryEnvironment.Capture()), selected, commandRunner);
                renderer.UpdateRehearsal(rehearsal, includeChecks: !renderer.UsesMerUpdateReceipt);
                if (rehearsal.State == UpdateState.Interrupted) return 130;
                return rehearsal.NeedsAttention ? 1 : 0;
            }
            var invalid = args.Skip(1).FirstOrDefault(arg => arg != "--plain");
            if (invalid is not null)
            {
                renderer.Failure($"'{invalid}' is not available for codex discovery. Use ERPDERT codex [--plain] for a read-only search. No operation was executed.");
                return 2;
            }
            if (!OperatingSystem.IsWindows())
            {
                renderer.Failure("Codex discovery currently follows Windows PowerShell PATH rules. Discovery on this operating system is not implemented.");
                return 2;
            }
            var report = CodexDiscovery.Scan(discoveryEnvironment ?? DiscoveryEnvironment.Capture());
            renderer.Discovery(report);
            return report.NeedsAttention ? 1 : 0;
        }
        if (!CliOptions.TryRead(args, out var options, out var message))
        {
            renderer.Failure(message);
            return 2;
        }
        if (options!.Command is "repair" or "undo")
            return RepairCommand.Run(options, console, renderer, confirmRepair, chooseBoundary);
        try
        {
            // Decode strictly and retain a UTF-8 BOM in SourceText. Inspection never writes the file.
            var fullPath = Path.GetFullPath(options!.Path);
            var bytes = File.ReadAllBytes(fullPath);
            var text = new UTF8Encoding(false, true).GetString(bytes);
            var document = DertParser.Parse(text);
            if (options.Command == "run")
                return DertRunCommand.Run(document, fullPath, options.Preview, renderer, discoveryEnvironment, commandRunner);
            if (options.At is int at && at > document.Source.Lines.Count)
            {
                renderer.Failure($"This document has {document.Source.Lines.Count} lines. Choose a line in that range.");
                return 2;
            }
            if (options.Command == "inspect")
                renderer.Inspect(document, options);
            else
                renderer.Lint(document, fullPath, Convert.ToHexString(SHA256.HashData(bytes)));
            return document.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error) ? 1 : 0;
        }
        catch (DecoderFallbackException)
        {
            renderer.Failure("This file could not be read as UTF-8. Save a UTF-8 copy in your editor; the original was not changed.");
            return 2;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            renderer.Failure($"The file could not be opened. Check its path and whether you can read it. {error.Message}");
            return 2;
        }
    }
}
