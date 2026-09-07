using Dert.Core;
using Erpdert.Cli.Repairs;
using Spectre.Console;

namespace Erpdert.Cli;

internal static class NoteRepairCommand
{
    internal static RepairSnapshot? Select(CliOptions options, IAnsiConsole console, DocumentRenderer renderer,
        RepairFileService service, Func<IReadOnlyList<NoteBoundary>, string?>? choose)
    {
        var inspection = service.InspectNote(options.Path, options.At!.Value, options.FromLint);
        if (options.CloseBefore is not null) return service.PreviewNote(inspection, options.CloseBefore);
        var redirected = options.FromLint is not null || choose is null && !console.Profile.Capabilities.Interactive;
        renderer.NoteChoices(inspection, redirected, options.FromLint is not null);
        if (redirected) return null;

        var choices = inspection.Analysis.Boundaries;
        var prompt = new TextPrompt<string>("Close before which source line (or eof when offered)?")
            .DefaultValue("leave")
            .Validate(key => key.Equals("leave", StringComparison.OrdinalIgnoreCase) ||
                choices.Any(c => c.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                ? ValidationResult.Success()
                : ValidationResult.Error("Type an offered source line, eof when offered, or leave. Nothing has been changed."));
        if (options.Plain) prompt.DefaultValueStyle = Style.Plain;
        var selected = choose is null ? prompt.Show(console) : choose(choices);
        if (selected is null || selected.Equals("leave", StringComparison.OrdinalIgnoreCase))
        {
            renderer.RepairLeft();
            return null;
        }
        return service.PreviewNote(inspection, selected);
    }
}
