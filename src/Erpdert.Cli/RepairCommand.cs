using Dert.Core;
using Erpdert.Cli.Repairs;
using Spectre.Console;

namespace Erpdert.Cli;

internal static class RepairCommand
{
    internal static int Run(CliOptions options, IAnsiConsole console, DocumentRenderer renderer,
        Func<string, bool>? confirm, Func<IReadOnlyList<NoteBoundary>, string?>? chooseBoundary)
    {
        try
        {
            var service = new RepairFileService();
            var snapshot = options.Command == "undo" ? service.PreviewUndo(options.Path)
                : options.At is not null ? NoteRepairCommand.Select(options, console, renderer, service, chooseBoundary)
                : service.Preview(options.Path, options.FromLint);
            if (snapshot is null) return 0;
            if (options.ApplyToken is not null && options.ApplyToken != snapshot.ApprovalToken)
                throw new RepairException("DERT4002",
                    "That approval no longer matches this file and proposed edit. Preview it again; nothing was written.");
            renderer.RepairPreview(snapshot, options.FromLint is not null);
            if (!snapshot.CanApply)
                return snapshot.Plan is TitleRepairPlan { State: TitleRepairState.Unavailable } ? 1 : 0;

            var question = snapshot.IsUndo ? "Restore these original bytes?"
                : snapshot.Plan is NoteClosurePlan ? "Apply this note-boundary edit and save an undo copy?"
                : "Apply this title-rule edit and save an undo copy?";
            if (options.ApplyToken is null)
            {
                if (options.FromLint is not null || confirm is null && !console.Profile.Capabilities.Interactive)
                {
                    renderer.RepairWaiting(snapshot, options.FromLint is not null);
                    return 0;
                }
                var prompt = new ConfirmationPrompt(question)
                {
                    DefaultValue = false,
                    InvalidChoiceMessage = "Type y to approve this edit, or n to leave the file unchanged."
                };
                if (options.Plain)
                {
                    prompt.ChoicesStyle = Style.Plain;
                    prompt.DefaultValueStyle = Style.Plain;
                }
                var approved = confirm is not null ? confirm(question) : prompt.Show(console);
                if (!approved)
                {
                    renderer.RepairLeft();
                    return 0;
                }
            }
            var result = service.Apply(snapshot);
            renderer.RepairCompleted(snapshot, result);
            return 0;
        }
        catch (RepairException error)
        {
            renderer.RepairFailure(error);
            return 1;
        }
    }
}
