using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Erpdert.Cli.Operations;

public sealed record CommandRequest(string Program, IReadOnlyList<string> Arguments, string WorkingDirectory,
    TimeSpan Timeout);

public sealed record CommandResult(int ExitCode, string Output, string Error, string? Failure = null)
{
    public bool Succeeded => ExitCode == 0 && Failure is null;
}

// External process boundary. DERT is bound to application operations before any
// request is constructed; source text is never handed to a command shell.
public interface ICommandRunner
{
    CommandResult Run(CommandRequest request);
}

public sealed class SystemCommandRunner(CancellationToken cancellation = default) : ICommandRunner
{
    public CommandResult Run(CommandRequest request) => RunAsync(request, cancellation).GetAwaiter().GetResult();

    private static async Task<CommandResult> RunAsync(CommandRequest request, CancellationToken cancellation)
    {
        if (cancellation.IsCancellationRequested) return new(130, "", "", "The command was cancelled before starting.");
        if (!Path.IsPathFullyQualified(request.Program) || !Path.IsPathFullyQualified(request.WorkingDirectory) ||
            request.Timeout <= TimeSpan.Zero || request.Timeout > TimeSpan.FromMinutes(5))
            return new(-1, "", "", "The command needs absolute paths and a bounded positive time limit.");
        var start = new ProcessStartInfo(request.Program)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false),
            WorkingDirectory = request.WorkingDirectory
        };
        foreach (var argument in request.Arguments) start.ArgumentList.Add(argument);
        // Do not let ambient Node preloads inject another program into metadata checks.
        start.Environment.Remove("NODE_OPTIONS");
        start.Environment.Remove("NODE_PATH");
        start.Environment["NO_COLOR"] = "1";
        using var process = new Process { StartInfo = start };
        try
        {
            if (!process.Start()) return new(-1, "", "", "The command could not start.");
        }
        catch (Exception error) when (error is Win32Exception or IOException or InvalidOperationException or ArgumentException)
        {
            return new(-1, "", "", $"The command could not start. {error.Message}");
        }
        process.StandardInput.Close();
        var overflow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var output = ReadBounded(process.StandardOutput, overflow);
        var errorOutput = ReadBounded(process.StandardError, overflow);
        var finished = Task.WhenAll(process.WaitForExitAsync(), output, errorOutput);
        using var timer = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        var timeout = Task.Delay(request.Timeout, timer.Token);
        var winner = await Task.WhenAny(finished, overflow.Task, timeout).ConfigureAwait(false);
        timer.Cancel();
        if (winner != finished || overflow.Task.IsCompleted || cancellation.IsCancellationRequested)
        {
            string? stopFailure = null;
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (Exception error) when (error is Win32Exception or InvalidOperationException or AggregateException)
            { stopFailure = " The child could not be confirmed stopped: " + error.Message; }
            try { await finished.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false); }
            catch (Exception error) when (error is TimeoutException or IOException or ObjectDisposedException) { /* Never wait indefinitely on inherited handles. */ }
            return new(cancellation.IsCancellationRequested ? 130 : -1, "", "", (cancellation.IsCancellationRequested
                ? "The command was cancelled; its evidence was not accepted."
                : overflow.Task.IsCompleted
                ? "The command exceeded the 256 KiB output limit; its evidence was not accepted."
                : "The command exceeded its time limit; its evidence was not accepted.") + stopFailure);
        }
        try
        {
            await finished.ConfigureAwait(false);
            return new(process.ExitCode, await output.ConfigureAwait(false), await errorOutput.ConfigureAwait(false));
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException)
        {
            return new(-1, "", "", "The command output could not be read. " + error.Message);
        }
    }

    private static async Task<string> ReadBounded(StreamReader reader, TaskCompletionSource overflow)
    {
        const int limit = 256 * 1024;
        var text = new StringBuilder();
        var buffer = new char[4096];
        while (true)
        {
            var count = await reader.ReadAsync(buffer.AsMemory()).ConfigureAwait(false);
            if (count == 0) return text.ToString();
            if (text.Length + count > limit)
            {
                overflow.TrySetResult();
                return text.ToString();
            }
            text.Append(buffer, 0, count);
        }
    }
}
