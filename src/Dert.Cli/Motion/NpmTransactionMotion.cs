using System.Diagnostics;
using Dert.Cli.Operations;
using Dert.Mer;
using Spectre.Console;

namespace Dert.Cli.Motion;

// Actual process boundaries drive one retained activity region. No timer or
// inferred package progress; the caller settles it from the transaction result.
internal sealed class NpmTransactionMotion : ICommandRunner, IDisposable
{
    private readonly ICommandRunner inner;
    private readonly IAnsiConsole console;
    private readonly bool live;
    private readonly bool applying;
    private readonly string operation;
    private readonly InlineTerminal terminal;
    private readonly MotionJournal journal;
    private readonly Stopwatch watch = Stopwatch.StartNew();
    private bool finished;

    internal NpmTransactionMotion(ICommandRunner inner, IAnsiConsole console, bool plain, bool still, bool applying, string operation = "doctor")
    {
        this.inner = inner; this.console = console; this.applying = applying; this.operation = operation;
        live = MerHost.CanAnimate(console, plain, still);
        terminal = new(console.Profile.Out.Writer, () => MerHost.Size(console), MerHost.Colors(console, plain));
        journal = new(new("Codex / " + operation, applying ? "Approved " + (operation == "update" ? "update" : "reinstall") : "Manager checks", [
            new("Transaction", operation, "Verify the selected npm and exact package target.")]), DateTimeOffset.Now);
        journal.Apply(new StartStage("Transaction"), watch.Elapsed);
    }

    public CommandResult Run(CommandRequest request)
    {
        var command = request.Arguments[1];
        var meaning = command switch
        {
            "root" => "Verify npm's target folder",
            "ls" => "Read this copy's manager inventory",
            "view" => "Resolve the registry's current release",
            "uninstall" => "Uninstall the reviewed package",
            "install" when request.Arguments.Contains("--dry-run") => "Rehearse dependency resolution",
            "install" => "Install the pinned package version",
            _ => "Query the selected manager"
        };
        journal.Apply(new AddEvidence("Transaction", new(meaning, request.WorkingDirectory, EvidenceKind.Path)), watch.Elapsed);
        if (live)
        {
            var size = MerHost.Size(console);
            if (size.Columns >= 4 && size.Rows >= 4)
            {
                var rows = new[] { "  " + operation + " ;; " + meaning, "     |", "     +-- " + request.WorkingDirectory,
                    applying ? "     '-- Approved " + operation + " in progress / receipt records each phase" : "     '-- npm may write cache/logs / no uninstall or reinstall" };
                var lines = rows.SelectMany(row => TerminalText.Wrap(row, size.Columns - 1))
                    .Take(size.Rows - 2).Select(row => InkLine.Plain(row, Ink.Muted)).ToArray();
                terminal.Draw(new(size.Columns, lines, size.Rows));
            }
        }
        return inner.Run(request);
    }

    internal void Finish(bool complete, bool interrupted, string message)
    {
        journal.Apply(new SettleStage("Transaction", interrupted ? StageState.Interrupted : complete ? StageState.Passed : StageState.Attention, message), watch.Elapsed);
        journal.Apply(new FinishMotion(interrupted ? MotionOutcome.Interrupted : complete ? MotionOutcome.Complete : MotionOutcome.Attention, message), watch.Elapsed);
        terminal.Finish([InkLine.Plain("  " + operation + " ;; " + message, complete ? Ink.Signal : Ink.Copper)]);
        finished = true;
    }

    public void Dispose()
    {
        try { if (!finished) terminal.Finish([InkLine.Plain("  " + operation + " ;; stopped / no completed result claimed", Ink.Copper)]); }
        finally { terminal.Dispose(); }
    }
}
