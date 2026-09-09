using System.Text;
using System.Security.Cryptography;
using Dert.Cli.Discovery;
using Dert.Cli.Operations;
using Dert.Mer;
using Spectre.Console;

namespace Dert.Cli.Motion;

internal static class MerCommand
{
    internal static int Run(string[] args, IAnsiConsole console, DocumentRenderer renderer, TextReader input, DiscoveryEnvironment? environment = null,
        ICommandRunner? commandRunner = null, string? doctorStateDirectory = null)
    {
        string? path = null;
        var replay = false;
        var still = false;
        var plain = false;
        var details = false;
        var review = false;
        foreach (var argument in args.Skip(1))
            switch (argument)
            {
                case "--replay": replay = true; break;
                case "--still": still = true; break;
                case "--plain": plain = true; break;
                case "--details": details = true; break;
                case "--review": review = true; break;
                default:
                    if (path is not null || argument.StartsWith("--", StringComparison.Ordinal))
                    {
                        renderer.Failure("Use DERT mer [file.DERT] [--replay] [--still] [--plain] [--details | --review]. With no file, it replays the bundled note. Reading never edits; a live repair requires preview and explicit apply.");
                        return 2;
                    }
                    path = argument;
                    break;
            }
        if (details && review)
        {
            renderer.Failure("Use --details for the full report, or --review to accept inspection commands from input. Choose one.");
            return 2;
        }
        replay |= path is null;
        path ??= Path.Combine(AppContext.BaseDirectory, "examples", "mer.DERT");
        try
        {
            var fullPath = Path.GetFullPath(path);
            using var cancelled = new CancellationTokenSource();
            ConsoleCancelEventHandler stop = (_, args) => { args.Cancel = true; cancelled.Cancel(); };
            Console.CancelKeyPress += stop;
            try
            {
                byte[]? bytes = null;
                string Read()
                {
                    bytes = File.ReadAllBytesAsync(fullPath, cancelled.Token).GetAwaiter().GetResult();
                    return new UTF8Encoding(false, true).GetString(bytes);
                }
                NoteScore analysis;
                MotionSnapshot result;
                if (!replay)
                {
                    using var live = MerHost.Open(NoteScore.Describe(fullPath, false) with { View = MotionView.Document }, console, plain, still, details);
                    analysis = NoteScore.Analyze(Read, fullPath, false, live.Publish, cancelled.Token);
                    result = live.Snapshot();
                }
                else
                {
                    analysis = NoteScore.Analyze(Read, fullPath, true, cancellation: cancelled.Token);
                    analysis = analysis with { Score = analysis.Score with { View = MotionView.Document } };
                    var recording = new ReplaySequence(analysis.Score, analysis.Events);
                    ReplayPlayer.Play(analysis, console, plain, still, cancelled.Token, holdResult: !details, recording: recording);
                    result = recording.Recorded;
                }
                cancelled.Token.ThrowIfCancellationRequested();
                if (details)
                {
                    if (analysis.Document is { } document && bytes is not null)
                        renderer.MerRehearsals(document, fullPath, Convert.ToHexString(SHA256.HashData(bytes)));
                }
                else if (review || console.Profile.Capabilities.Interactive)
                    return new MerReview(analysis, result, bytes, fullPath, console, renderer, input, plain, environment, commandRunner: commandRunner,
                        doctorStateDirectory: doctorStateDirectory).Run(cancelled.Token);
                else Say("Use --details for the full record and repair commands.", Ink.Muted);
                cancelled.Token.ThrowIfCancellationRequested();
                return analysis.Exit;
            }
            catch (OperationCanceledException) when (cancelled.IsCancellationRequested) { return 130; }
            finally { Console.CancelKeyPress -= stop; }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            renderer.Failure("The MER session could not finish. " + error.Message);
            return 2;
        }

        void Say(string text, Ink ink)
        {
            using var terminal = new InlineTerminal(console.Profile.Out.Writer, () => MerHost.Size(console), MerHost.Colors(console, plain));
            terminal.Finish(TerminalText.Wrap(text, Math.Max(1, MerHost.Size(console).Columns - 1)).Select(line => InkLine.Plain(line, ink)).ToArray());
        }
    }
}
