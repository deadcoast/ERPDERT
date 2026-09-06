using System.Text;
using Dert.Core;
using Spectre.Console;

namespace Erpdert.Cli;

public static class CliApplication
{
    public static int Run(string[] args, IAnsiConsole console, Func<string, bool>? confirmRepair = null,
        string[]? invocation = null)
    {
        var renderer = new DocumentRenderer(console, args.Contains("--plain"), invocation);
        if (args.Length == 0 || args[0] == "help" || args.Any(a => a is "-help" or "--help"))
        {
            renderer.Help();
            return 0;
        }
        if (!CliOptions.TryRead(args, out var options, out var message))
        {
            renderer.Failure(message);
            return 2;
        }
        if (options!.Command is "repair" or "undo")
            return RepairCommand.Run(options, console, renderer, confirmRepair);
        try
        {
            // Decode strictly and retain a UTF-8 BOM in SourceText. Inspection never writes the file.
            var text = new UTF8Encoding(false, true).GetString(File.ReadAllBytes(options!.Path));
            var document = DertParser.Parse(text);
            if (options.At is int at && at > document.Source.Lines.Count)
            {
                renderer.Failure($"This document has {document.Source.Lines.Count} lines. Choose a line in that range.");
                return 2;
            }
            if (options.Command == "inspect")
                renderer.Inspect(document, options);
            else
                renderer.Lint(document, options.Path);
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
