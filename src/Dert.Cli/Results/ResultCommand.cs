using System.Globalization;
using System.Text;
using Dert.Cli.Authoring;
using Dert.Core;
using Spectre.Console;

namespace Dert.Cli.Results;

internal static class ResultCommand
{
    private const string Usage = "Use DERT result record.json [--into notes.DERT] [--at N] [--details] [--compare earlier.json]. --write opens an insertion review in Notes.";

    internal static int Run(string[] args, IAnsiConsole console, DocumentRenderer renderer)
    {
        try
        {
            if (args.Length < 2 || args[1].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException(Usage);
            string? destination = null, comparison = null; int? at = null;
            var flags = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 2; i < args.Length; i++)
            {
                var flag = args[i];
                if (!flags.Add(flag)) throw new ArgumentException("That option was repeated. " + Usage);
                if (flag is "--into" or "--compare")
                {
                    if (++i >= args.Length || string.IsNullOrWhiteSpace(args[i]) || args[i].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException(Usage);
                    if (flag == "--into") destination = Path.GetFullPath(args[i]); else comparison = Path.GetFullPath(args[i]);
                }
                else if (flag == "--at")
                {
                    if (++i >= args.Length || !int.TryParse(args[i], NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 1) throw new ArgumentException(Usage);
                    at = number;
                }
                else if (flag is not ("--details" or "--write" or "--plain" or "--still")) throw new ArgumentException(Usage);
            }
            var record = Path.GetFullPath(args[1]);
            var observation = ObservationStore.Read(record);
            var explicitDestination = destination is not null;
            destination ??= observation.Origin.Path;
            ResultNotePlan? plan = null;
            if (File.Exists(destination) || explicitDestination)
            {
                using var input = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var bytes = new MemoryStream(); input.CopyTo(bytes);
                plan = ResultNotes.Plan(DertParser.Parse(new UTF8Encoding(false, true).GetString(bytes.ToArray())), observation, destination, at);
                ResultPresentation.Draw(plan, destination, console, flags.Contains("--plain"));
            }
            else if (at is not null) throw new ArgumentException("Choose an existing --into document before comparing a source line.");
            renderer.ResultReview(observation, record, destination, plan, flags.Contains("--details"), at);
            if (comparison is not null) renderer.ContextText(ResultNotes.CompareObservations(ObservationStore.Read(comparison), observation));
            if (flags.Contains("--write"))
            {
                if (plan?.IsReady != true) throw new ArgumentException("The insertion is not ready. Review the source explanation before opening this handoff.");
                return AuthoringCommand.Run([destination, "--result", record, .. (at is int line ? new[] { "--at", line.ToString(CultureInfo.InvariantCulture) } : []),
                    .. (flags.Contains("--still") ? new[] { "--still" } : [])], renderer);
            }
            return 0;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        { renderer.Failure(error.Message); return 2; }
    }
}
