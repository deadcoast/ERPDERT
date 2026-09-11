using System.Globalization;
using System.Text;
using Dert.Core;
using Spectre.Console;

namespace Dert.Cli.Authoring;

internal static class ContextCommand
{
    internal const string Usage = "Use DERT context notes.DERT --at 7 [--include 12] [--details | --text]. Export adds --output new.txt --from-preview <review ID>.";

    internal static int Run(string[] args, IAnsiConsole console, DocumentRenderer renderer)
    {
        try
        {
            if (args.Length < 4 || args[1].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException(Usage);
            int? at = null; var included = new List<int>(); var flags = new HashSet<string>(StringComparer.Ordinal);
            string? output = null, review = null; var details = false; var textOnly = false;
            for (var i = 2; i < args.Length; i++)
            {
                var flag = args[i];
                if (flag != "--include" && !flags.Add(flag)) throw new ArgumentException("That option was repeated. " + Usage);
                if (flag is "--at" or "--include")
                {
                    if (++i >= args.Length || !int.TryParse(args[i], NumberStyles.None, CultureInfo.InvariantCulture, out var line) || line < 1)
                        throw new ArgumentException("Choose a positive source line. " + Usage);
                    if (flag == "--at") at = line;
                    else if (!included.Contains(line)) included.Add(line);
                    else throw new ArgumentException("That included note was repeated. " + Usage);
                }
                else if (flag is "--output" or "--from-preview")
                {
                    if (++i >= args.Length || string.IsNullOrWhiteSpace(args[i])) throw new ArgumentException(Usage);
                    if (flag == "--output") output = args[i]; else review = args[i];
                }
                else if (flag == "--details") details = true;
                else if (flag == "--text") textOnly = true;
                else if (flag is not ("--plain" or "--still")) throw new ArgumentException(Usage);
            }
            if (at is null || textOnly && (details || output is not null) || (output is null) != (review is null)) throw new ArgumentException(Usage);
            var path = Path.GetFullPath(args[1]);
            // Keep the captured file readable but deny replacement/writes until
            // this review/export finishes. The token covers all captured text.
            using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var bytes = new MemoryStream(); source.CopyTo(bytes);
            var document = DertParser.Parse(new UTF8Encoding(false, true).GetString(bytes.ToArray()));
            var packet = ContextSelection.Capture(document, at.Value, path, ContextSourceKind.FileSnapshot).Build(included);
            if (review is not null && !string.Equals(review, packet.ReviewId, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The source, identity or selected notes changed. Open a fresh context preview before exporting. No export was written.");
            if (textOnly) { console.Profile.Out.Writer.Write(packet.Report); return 0; }
            ContextPresentation.Draw(packet, console, args.Contains("--plain"));
            renderer.ContextReview(packet, details);
            if (output is not null)
            {
                var saved = ContextExport.WriteNew(packet, output);
                renderer.ContextSaved(saved, packet.ReviewId);
            }
            return 0;
        }
        catch (DecoderFallbackException)
        { renderer.Failure("The source could not be read as UTF-8. Choose a UTF-8 document; no context export was written."); return 2; }
        catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException)
        { renderer.Failure(error.Message); return 2; }
    }
}
