using Dert.Mer;
using Spectre.Console;

namespace Dert.Cli.Motion;

internal static class DemoCommand
{
    private sealed record Observation(string Case, string Mode, TerminalSize Size, string Outcome,
        string Verdict = "not reviewed", string Note = "", bool PlayedToEnd = false, string Compared = "");

    internal static int Run(string[] args, IAnsiConsole console, TextReader input)
    {
        var plain = args.Contains("--plain");
        var still = args.Contains("--still");
        var all = false;
        var review = false;
        var step = false;
        string? selected = null;
        var invalid = false;
        for (var i = 1; i < args.Length; i++)
            switch (args[i])
            {
                case "--plain": case "--still": break;
                case "--all": all = true; break;
                case "--review": review = true; break;
                case "--step": step = true; break;
                case "--case" when selected is null && i + 1 < args.Length: selected = args[++i]; break;
                case "--help": case "-help": Help(); return 0;
                default: invalid = true; break;
            }
        var index = selected is null ? 0 : DemoCatalog.Cases.ToList().FindIndex(item => item.Id.Equals(selected, StringComparison.OrdinalIgnoreCase));
        if (invalid || index < 0 || all && (selected is not null || review || step)) { Help(); return 2; }
        var interactive = !all && (review || step || console.Profile.Capabilities.Interactive);
        var observations = new List<Observation>();
        var exit = 0;
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler stop = (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += stop;
        try
        {
            Say(";  MER / try it for yourself", Ink.Copper, true);
            Say("No files are written. No installs, network requests, or user-note reads.");
            Say("Each case replays real local analysis. Your review is separate from the check results.", Ink.Muted);
            if (interactive)
            {
                Menu();
                while (true)
                {
                    Say($"Enter starts {DemoCatalog.Cases[index].Title}. Or type a case number/name, step, still, plain, or q.", Ink.Signal);
                    var choice = Read();
                    if (choice is null || choice.Equals("q", StringComparison.OrdinalIgnoreCase)) { Card(); return 0; }
                    if (choice.Length == 0) break;
                    if (Choose(choice, out var chosen)) { index = chosen; break; }
                    if (Mode(choice)) continue;
                    Say("That choice was not recognized. The available cases are above.", Ink.Copper);
                }
            }
            while (index < DemoCatalog.Cases.Count)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                var item = DemoCatalog.Cases[index];
                Say("");
                Say($";;  {index + 1}/{DemoCatalog.Cases.Count}  {item.Title}", Ink.Signal, true);
                Say("Look for / " + item.LookFor);
                var analysis = NoteScore.Analyze(item.Read, $"demo/{item.Id}.DERT", true, cancellation: cancellation.Token);
                analysis = analysis with { Score = analysis.Score with { Title = item.Title } };
                var recording = new ReplaySequence(analysis.Score, analysis.Events);
                var selectedLine = recording.InitialInspectionLine;
                var sourceView = false;
                var outcome = ((FinishMotion)analysis.Events[^1]).Outcome.ToString();
                observations.Add(new(item.Id, CurrentMode(), MerHost.Size(console), outcome));
                if (!ReplayPlayer.Play(analysis, console, plain, still, cancellation.Token,
                    step && interactive ? Read : null, text => Say(text, Ink.Signal), holdResult: interactive, recording: recording, reservedRows: 3)) { Card(); return exit; }
                observations[^1] = observations[^1] with { PlayedToEnd = true };
                if (analysis.Exit != item.ExpectedExit)
                {
                    exit = 1;
                    Say($"Case result differed from its expected input behavior (exit {analysis.Exit}, expected {item.ExpectedExit}). This is not a design verdict.", Ink.Bad);
                }
                if (!interactive) { if (selected is not null) break; index++; continue; }
                while (true)
                {
                    Say("Review / Enter next case · keep · change <your note> · q", Ink.Signal);
                    Say("Compare / details · repeat · step · still · plain · 1–5", Ink.Muted);
                    if (recording.InspectionLines.Count > 0)
                        Say("Entries / prev · next · source · " + string.Join(" · ", recording.InspectionLines.Select(line => "at " + line)), Ink.Muted);
                    var answer = Read();
                    if (answer is null || answer.Equals("q", StringComparison.OrdinalIgnoreCase)) { Card(); return exit; }
                    if (answer.Length == 0) { index++; break; }
                    if (answer.Equals("source", StringComparison.OrdinalIgnoreCase))
                    {
                        var line = selectedLine ?? recording.InspectionLines.Cast<int?>().FirstOrDefault();
                        if (line is null) Say("This case has no recorded entries. Use details for its checks, or Enter for the next case.", Ink.Copper);
                        else
                        {
                            sourceView = !sourceView;
                            if (ShowSelection(line)) selectedLine = line;
                            else { sourceView = !sourceView; Say("That source excerpt is unavailable. Use details for the recorded checks.", Ink.Copper); }
                        }
                        continue;
                    }
                    if (answer.Equals("at", StringComparison.OrdinalIgnoreCase) || answer.StartsWith("at ", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(answer.Length > 3 ? answer[3..] : "", out var line) &&
                            recording.InspectionLines.Contains(line) && ShowSelection(line))
                        {
                            selectedLine = line;
                        }
                        else Say("No recorded connection at that line. Use a listed at command or details for the complete source.", Ink.Copper);
                        continue;
                    }
                    if (answer.Equals("prev", StringComparison.OrdinalIgnoreCase) || answer.Equals("next", StringComparison.OrdinalIgnoreCase))
                    {
                        var lines = recording.InspectionLines.ToList();
                        var previous = answer.Equals("prev", StringComparison.OrdinalIgnoreCase);
                        var position = selectedLine is int current ? lines.IndexOf(current) : previous ? lines.Count : -1;
                        var target = position + (previous ? -1 : 1);
                        if (lines.Count == 0) Say("This case has no recorded entries. Use details for its checks, or Enter for the next case.", Ink.Copper);
                        else if (target < 0 || target >= lines.Count)
                            Say($"Already at the {(previous ? "first" : "last")} recorded entry. Your selection has not changed.", Ink.Copper);
                        else if (ShowSelection(lines[target])) selectedLine = lines[target];
                        continue;
                    }
                    if (answer.Equals("details", StringComparison.OrdinalIgnoreCase))
                    {
                        Say("Full recorded detail / no second execution", Ink.Copper);
                        using var detail = new InlineTerminal(console.Profile.Out.Writer, () => MerHost.Size(console), MerHost.Colors(console, plain));
                        detail.Finish(recording.Receipt());
                        continue;
                    }
                    if (answer.Equals("keep", StringComparison.OrdinalIgnoreCase))
                    { observations[^1] = observations[^1] with { Verdict = "looks good" }; index++; break; }
                    if (answer.StartsWith("change ", StringComparison.OrdinalIgnoreCase) && answer[7..].Trim().Length > 0)
                    { observations[^1] = observations[^1] with { Verdict = "needs work", Note = answer[7..].Trim() }; index++; break; }
                    var repeat = answer.Equals("repeat", StringComparison.OrdinalIgnoreCase);
                    if (repeat || Mode(answer))
                    {
                        var mode = CurrentMode();
                        var compared = observations[^1].Compared.Split(" · ", StringSplitOptions.RemoveEmptyEntries).Append(mode).Distinct();
                        observations[^1] = observations[^1] with { Compared = string.Join(" · ", compared) };
                        if (repeat || answer.Equals("step", StringComparison.OrdinalIgnoreCase) && step)
                        {
                            Say("Replaying the same record / no second execution", Ink.Copper);
                            if (!ReplayPlayer.Play(analysis, console, plain, still, cancellation.Token, step ? Read : null,
                                text => Say(text, Ink.Signal), holdResult: true, recording: recording, reservedRows: 3,
                                heldView: size => SelectionLines(ComposeSelection(selectedLine, size)!, selectedLine, size.Columns))) { Card(); return exit; }
                        }
                        else ShowSelection(selectedLine);
                        continue;
                    }
                    if (Choose(answer, out var next)) { index = next; break; }
                    Say("Use change followed by what needs work, keep, repeat, a case number, or Enter to leave it unreviewed.", Ink.Copper);
                }

                bool ShowSelection(int? line)
                {
                    var size = MerHost.Size(console);
                    var frame = ComposeSelection(line, size);
                    if (frame is null) return false;
                    using var focused = new InlineTerminal(console.Profile.Out.Writer, () => MerHost.Size(console), MerHost.Colors(console, plain));
                    focused.Finish(SelectionLines(frame, line, size.Columns));
                    return true;
                }
                MerFrame? ComposeSelection(int? line, TerminalSize size)
                {
                    var height = Math.Max(4, size.Rows - 3);
                    return line is int at ? sourceView ? recording.Source(at, size.Columns) : recording.Inspect(at, size.Columns, height)
                        : recording.Hold(size.Columns, height);
                }
                IReadOnlyList<InkLine> SelectionLines(MerFrame frame, int? line, int columns) => line is not null && !sourceView
                    ? [.. TerminalText.Wrap($"Inspecting recorded line {line} / no second execution", Math.Max(1, columns - 1))
                        .Select(text => InkLine.Plain(text, Ink.Copper)), .. frame.Lines] : frame.Lines;
            }
            Card();
            return exit;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        { Say("Demo stopped at your request. Unreviewed cases have no verdict.", Ink.Copper); Card(); return 130; }
        finally { Console.CancelKeyPress -= stop; }

        string? Read() => Task.Run(input.ReadLine, cancellation.Token).WaitAsync(cancellation.Token).GetAwaiter().GetResult()?.Trim();
        string CurrentMode() => step && interactive ? (plain ? "step / plain" : "step / still frames") :
            plain ? "plain" : still ? "still" : MerHost.CanAnimate(console, plain, still) ? "motion" : "static / terminal preference";
        bool Mode(string choice)
        {
            if (choice.Equals("still", StringComparison.OrdinalIgnoreCase)) { still = !still; plain = false; }
            else if (choice.Equals("plain", StringComparison.OrdinalIgnoreCase)) plain = !plain;
            else if (choice.Equals("step", StringComparison.OrdinalIgnoreCase)) step = !step;
            else return false;
            Say("Presentation / " + CurrentMode(), Ink.Copper);
            return true;
        }
        static bool Choose(string choice, out int found)
        {
            found = int.TryParse(choice, out var number) ? number - 1 :
                DemoCatalog.Cases.ToList().FindIndex(item => item.Id.Equals(choice, StringComparison.OrdinalIgnoreCase));
            return found >= 0 && found < DemoCatalog.Cases.Count;
        }
        void Menu()
        {
            foreach (var (item, number) in DemoCatalog.Cases.Select((item, number) => (item, number)))
                Say($"  {number + 1}  {item.Title} / {item.Id}");
        }
        void Help()
        {
            Say("DERT demo [--case title|context|unfinished|unicode|failure] [--step] [--still] [--plain]");
            Say("--step pauses at evidence and results. Enter moves forward; back revisits a recorded step without running it again.");
            Say("--all runs every case without prompts. --review enables review prompts, including with piped input.");
            Say("Interactive cases hold the result for review. Type details for the full recorded source and checks.");
            Say("Use a listed at <line> command to inspect an entry from the same record without replaying the case.");
            Say("prev and next move between recorded entries. still and plain keep your selection; repeat replays the same analysis.");
            Say("source switches between the selected connection and its original source excerpt, including punctuation and wrapped context.");
            Say("An offline, no-write showcase with optional personal review. Expected failure cases are not demo failures.");
            Menu();
        }
        void Card()
        {
            Say("");
            Say(";  Your review / copy this card", Ink.Copper, true);
            Say("Personal judgments, not automatic certification. Nothing saved or uploaded.", Ink.Muted);
            foreach (var observation in observations)
            {
                Say($"{observation.Case} / {observation.Verdict} / {observation.Mode} / {observation.Size.Columns}x{observation.Size.Rows} / analysis: {observation.Outcome}");
                Say("  view: " + (observation.PlayedToEnd ? "complete" : "partial"), Ink.Muted);
                if (observation.Compared.Length > 0) Say("  compared: " + observation.Compared, Ink.Muted);
                if (observation.Note.Length > 0) Say("  " + observation.Note);
            }
            foreach (var item in DemoCatalog.Cases.Where(item => !observations.Any(observation => observation.Case == item.Id)))
                Say(item.Id + " / not reviewed / not shown", Ink.Muted);
            Say("Repeat any case with DERT demo --case <name>. Add --still or --plain to compare.");
        }
        void Say(string text, Ink ink = Ink.Text, bool bold = false)
        {
            using var output = new InlineTerminal(console.Profile.Out.Writer, () => MerHost.Size(console), MerHost.Colors(console, plain));
            var width = Math.Max(1, MerHost.Size(console).Columns - 1);
            var lines = TerminalText.Wrap(text, width).Select(line => new InkLine(new InkRun(line, ink, bold))).ToArray();
            output.Finish(lines.Length == 0 ? [InkLine.Plain("")] : lines);
        }
    }
}
