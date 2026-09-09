using System.Diagnostics;
using Erpdert.Mer;
using Spectre.Console;

namespace Erpdert.Cli.Motion;

internal static class ReplayPlayer
{
    internal static bool Play(NoteScore analysis, IAnsiConsole console, bool plain, bool still,
        CancellationToken cancellation, Func<string?>? stepInput = null, Action<string>? prompt = null, bool holdResult = false, ReplaySequence? recording = null,
        int reservedRows = 0, Func<TerminalSize, IReadOnlyList<InkLine>>? heldView = null)
    {
        var sequence = recording ?? new ReplaySequence(analysis.Score, analysis.Events);
        sequence.Seek(0, TimeSpan.Zero); // Each playback owns a fresh clock, not a fresh analysis.
        using var terminal = Open();
        var clock = Stopwatch.StartNew();
        try
        {
            if (stepInput is not null)
            {
                var beats = analysis.Events.Select((change, index) => (change, index))
                    .Where(item => item.change is AddEvidence or SettleStage or FinishMotion).Select(item => item.index + 1).ToArray();
                var at = 0;
                while (at < beats.Length)
                {
                    cancellation.ThrowIfCancellationRequested();
                    sequence.Seek(beats[at], clock.Elapsed);
                    var size = MerHost.Size(console);
                    var frame = MerCompositor.Live(sequence.Snapshot(clock.Elapsed), size.Columns, size.Rows, still: true);
                    // A paused inspection is an ordinary scrollback block. Release
                    // all row ownership before the line prompt can echo input.
                    using (var paused = Open()) paused.Finish(frame.Lines);
                    while (true)
                    {
                        prompt?.Invoke($"Paused replay {at + 1}/{beats.Length} / Enter · back · repeat · next to review · q");
                        var choice = stepInput()?.Trim().ToLowerInvariant();
                        if (choice is null or "q") { terminal.Finish(sequence.Receipt(stopped: true)); return false; }
                        if (choice == "") { at++; break; }
                        if (choice == "back") { at = Math.Max(0, at - 1); break; }
                        if (choice == "repeat") { at = 0; break; }
                        if (choice == "next") { at = beats.Length; break; }
                        prompt?.Invoke("No step changed. Use Enter, back, repeat, next, or q.");
                    }
                }
            }
            else
            {
                var animate = MerHost.CanAnimate(console, plain, still);
                for (var position = 1; position <= sequence.Count; position++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    sequence.Seek(position, clock.Elapsed);
                    var change = analysis.Events[position - 1];
                    if (change is FinishMotion) break;
                    Draw();
                    if (!animate || change is SetSource) continue;
                    var hold = change is StartStage ? 450 : change is AddEvidence ? 400 : 120;
                    var until = clock.Elapsed + TimeSpan.FromMilliseconds(hold);
                    while (clock.Elapsed < until)
                    {
                        Task.Delay(40, cancellation).GetAwaiter().GetResult();
                        Draw();
                    }
                }
                void Draw()
                {
                    if (!MerHost.CanDrawLive(console, plain)) return;
                    var size = MerHost.Size(console);
                    if (size.Columns < 4 || size.Rows < 4) return;
                    terminal.Draw(MerCompositor.Live(sequence.Snapshot(clock.Elapsed), size.Columns, size.Rows, still: !animate));
                }
            }
            var finalSize = MerHost.Size(console);
            terminal.Finish(holdResult ? heldView?.Invoke(finalSize) ??
                sequence.Hold(finalSize.Columns, Math.Max(4, finalSize.Rows - reservedRows)).Lines : sequence.Receipt(columns: finalSize.Columns));
            return true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            terminal.Finish(sequence.Receipt(stopped: true));
            throw;
        }

        InlineTerminal Open() => new(console.Profile.Out.Writer, () => MerHost.Size(console), MerHost.Colors(console, plain));
    }
}
