using System.Text;
using System.Security.Cryptography;
using Dert.Core;
using Dert.Cli.Discovery;
using Dert.Cli.Operations;
using Dert.Cli.Updates;
using Dert.Cli.Motion;
using Dert.Mer;
using Spectre.Console;

namespace Dert.Cli;

public static class CliApplication
{
    public static int Run(string[] args, IAnsiConsole console, Func<string, bool>? confirmRepair = null,
        string[]? invocation = null, Func<IReadOnlyList<NoteBoundary>, string?>? chooseBoundary = null,
        DiscoveryEnvironment? discoveryEnvironment = null, ICommandRunner? commandRunner = null, TextReader? demoInput = null, TextReader? merInput = null,
        string? doctorStateDirectory = null, string? desktopSettingsPath = null)
    {
        try
        {
            if (args.FirstOrDefault()?.Equals("demo", StringComparison.OrdinalIgnoreCase) == true)
                return DemoCommand.Run(args, console, demoInput ?? Console.In);
            return RunCore(args, console, confirmRepair, invocation, chooseBoundary, discoveryEnvironment, commandRunner, merInput, doctorStateDirectory, desktopSettingsPath);
        }
        catch (MerOutputException) { return 2; } // The closed output cannot carry another renderer's error report.
    }

    private static int RunCore(string[] args, IAnsiConsole console, Func<string, bool>? confirmRepair,
        string[]? invocation, Func<IReadOnlyList<NoteBoundary>, string?>? chooseBoundary,
        DiscoveryEnvironment? discoveryEnvironment, ICommandRunner? commandRunner, TextReader? merInput, string? doctorStateDirectory, string? desktopSettingsPath)
    {
        var renderer = new DocumentRenderer(console, args.Contains("--plain"), invocation, args.Contains("--still"));
        if (args.Length == 0 || args[0] == "help" || args.Any(a => a is "-help" or "--help"))
        {
            renderer.Help();
            return 0;
        }
        if (args[0].Equals("mer", StringComparison.OrdinalIgnoreCase))
            return MerCommand.Run(args, console, renderer, merInput ?? Console.In, discoveryEnvironment, commandRunner, doctorStateDirectory);
        if (args[0].Equals("write", StringComparison.OrdinalIgnoreCase))
            return Authoring.AuthoringCommand.Run(args.Skip(1).ToArray(), renderer);
        if (args[0].Equals("guide", StringComparison.OrdinalIgnoreCase))
            return Authoring.AuthoringCommand.Run(args.Skip(1).ToArray(), renderer, guide: true);
        if (args[0].Equals("settings", StringComparison.OrdinalIgnoreCase))
            return Authoring.DesktopSettingsCommand.Run(args, renderer, desktopSettingsPath);
        if (args[0].Equals("context", StringComparison.OrdinalIgnoreCase))
            return Authoring.ContextCommand.Run(args, console, renderer);
        if (args[0].Equals("result", StringComparison.OrdinalIgnoreCase))
            return Results.ResultCommand.Run(args, console, renderer);
        if (args[0].Equals("update", StringComparison.OrdinalIgnoreCase) || args[0].Equals("where", StringComparison.OrdinalIgnoreCase))
        {
            if (!Applications.ApplicationRequest.TryRead(args, out var request, out var reason))
            { renderer.Failure(reason + (args[0].Equals("update", StringComparison.OrdinalIgnoreCase) ? " DERT update codex is read-only; add updateme to request a rehearsal." : "")); return 2; }
            return request!.Action is Applications.ApplicationAction.Where or Applications.ApplicationAction.Doctor
                ? Applications.WhereCommand.Run(request, renderer, console, discoveryEnvironment, commandRunner, args.Contains("--plain"), stateDirectory: doctorStateDirectory, still: args.Contains("--still"))
                : Applications.UpdateCommand.Run(request, renderer, discoveryEnvironment, commandRunner, console,
                    args.Contains("--plain"), doctorStateDirectory, args.Contains("--still"));
        }
        if (!CliOptions.TryRead(args, out var options, out var message))
        {
            renderer.Failure(message);
            return 2;
        }
        if (options!.Command is "repair" or "undo")
            return RepairCommand.Run(options, console, renderer, confirmRepair, chooseBoundary);
        if (options.Receipt is not null)
            return Results.ResultRunCommand.Run(options, renderer, discoveryEnvironment);
        try
        {
            // Decode strictly and retain a UTF-8 BOM in SourceText. Inspection never writes the file.
            var fullPath = Path.GetFullPath(options!.Path);
            var bytes = File.ReadAllBytes(fullPath);
            var text = new UTF8Encoding(false, true).GetString(bytes);
            var document = DertParser.Parse(text);
            if (options.Command == "run")
                return DertRunCommand.Run(document, fullPath, options.Preview, renderer, discoveryEnvironment, commandRunner, console, args.Contains("--plain"), doctorStateDirectory, args.Contains("--still"));
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
