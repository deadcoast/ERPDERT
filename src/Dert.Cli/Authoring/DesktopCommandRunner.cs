using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Dert.Cli.Authoring;

public sealed record DesktopCommandResult(int ExitCode, string Output, bool Cancelled);

public static class DesktopCommandRunner
{
    /// <summary>Reads literal DERT arguments. Quotes group text; nothing is expanded or evaluated.</summary>
    public static IReadOnlyList<string> ParseArguments(string command, string cliPath)
    {
        ValidateExecutable(cliPath);
        ArgumentNullException.ThrowIfNull(command);
        if (command.Any(character => char.IsControl(character) && character != '\t'))
            throw new ArgumentException("Enter one DERT command on one line.");
        var words = new List<string>();
        var word = new StringBuilder();
        var started = false;
        var quote = '\0';
        for (var i = 0; i < command.Length; i++)
        {
            var character = command[i];
            if (quote != '\0')
            {
                if (character == quote)
                {
                    if (quote == '\'' && i + 1 < command.Length && command[i + 1] == '\'')
                    { word.Append('\''); i++; }
                    else quote = '\0';
                }
                else word.Append(character);
            }
            else if (character is '\'' or '"') { quote = character; started = true; }
            else if (char.IsWhiteSpace(character))
            {
                if (started) { words.Add(word.ToString()); word.Clear(); started = false; }
            }
            else if (character == '&' && !started && words.Count == 0 && i + 1 < command.Length && char.IsWhiteSpace(command[i + 1]))
                words.Add("&");
            else
            {
                if (";&|<>`$%(){}[]*?#".Contains(character, StringComparison.Ordinal))
                    throw new ArgumentException("Enter literal DERT arguments. Put punctuation or spaces inside quotes; shell expressions, pipes and redirection are unavailable here.");
                word.Append(character);
                started = true;
            }
        }
        if (quote != '\0') throw new ArgumentException("Finish the quoted argument before running this DERT command.");
        if (started) words.Add(word.ToString());
        if (words.Count == 0)
            throw new ArgumentException("Enter a DERT command, such as DERT -help or DERT where codex.");
        if (words.FirstOrDefault() == "&")
        {
            if (words.Count < 2 || !string.Equals(words[1], cliPath, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("This command names a different executable. Use a command from this bundled DERT, or enter DERT followed by its arguments.");
            words.RemoveRange(0, 2);
        }
        else if (words.FirstOrDefault()?.Equals("DERT", StringComparison.OrdinalIgnoreCase) == true ||
                 words.FirstOrDefault()?.Equals("DERT.exe", StringComparison.OrdinalIgnoreCase) == true)
            words.RemoveAt(0);
        return words.ToArray();
    }

    /// <summary>
    /// Only known read-only commands can be stopped by terminating their process.
    /// Rehearsals can write proposals/cache; approved changes must finish through the CLI's own transaction checks.
    /// </summary>
    public static bool SupportsCancellation(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0) return true;
        var values = arguments.Select(value => value.ToLowerInvariant()).ToArray();
        if (values.Any(value => value.StartsWith("--apply", StringComparison.Ordinal))) return false;
        return values[0] switch
        {
            "help" or "-help" or "--help" or "inspect" or "lint" => true,
            "settings" => values.Skip(1).All(value => value is "--plain" or "--still"),
            "where" => !values.Any(value => value is "--doctor" or "-dr"),
            "update" => !values.Contains("updateme"),
            "context" => !values.Contains("--output"),
            "result" => !values.Contains("--write"),
            _ => false
        };
    }

    /// <summary>
    /// The caller supplies its fixed bundled DERT.exe path, never an executable from typed input.
    /// Already-cancelled requests do not start. Once a command with effects starts, cancellation is ignored
    /// and its actual exit/output is awaited. Output callbacks are advisory; display failure cannot interrupt a change.
    /// </summary>
    public static async Task<DesktopCommandResult> RunAsync(string cliPath, IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default, Action<string>? output = null)
    {
        ValidateExecutable(cliPath);
        ArgumentNullException.ThrowIfNull(arguments);
        var literalArguments = arguments.ToArray();
        if (literalArguments.Any(argument => argument is null || argument.Contains('\0')))
            throw new ArgumentException("DERT arguments cannot be null or contain a null character.");
        if (cancellationToken.IsCancellationRequested)
            return new(130, "The DERT command was cancelled before starting.", true);
        var start = new ProcessStartInfo(cliPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false)
        };
        foreach (var argument in literalArguments) start.ArgumentList.Add(argument);
        start.Environment["NO_COLOR"] = "1";
        using var process = new Process { StartInfo = start };
        try
        {
            if (!process.Start()) return new(-1, "The bundled DERT command could not start.", false);
        }
        catch (Exception error) when (error is Win32Exception or IOException or InvalidOperationException)
        { return new(-1, $"The bundled DERT command '{cliPath}' could not start. Check the installation. {error.Message}", false); }
        process.StandardInput.Close();
        var captured = new StringBuilder();
        var sync = new object();
        using var captureCancellation = new CancellationTokenSource();
        void Publish(string chunk)
        {
            lock (sync)
            {
                captured.Append(chunk);
                try { output?.Invoke(chunk); }
                catch (Exception) { output = null; } // Keep draining and retain the final result if the window closes.
            }
        }
        async Task Drain(StreamReader reader)
        {
            var buffer = new char[4096];
            try
            {
                while (await reader.ReadAsync(buffer.AsMemory(), captureCancellation.Token).ConfigureAwait(false) is var count && count > 0)
                    Publish(new string(buffer, 0, count));
            }
            catch (OperationCanceledException) when (captureCancellation.IsCancellationRequested) { }
            catch (IOException error) { Publish($"\nDERT output could not be read completely. {error.Message}\n"); }
        }
        var stdout = Drain(process.StandardOutput);
        var stderr = Drain(process.StandardError);
        var cancelled = false;
        var effectiveCancellation = SupportsCancellation(literalArguments) ? cancellationToken : CancellationToken.None;
        try { await process.WaitForExitAsync(effectiveCancellation).ConfigureAwait(false); }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    cancelled = true;
                }
            }
            catch (Exception error) when (error is Win32Exception or InvalidOperationException or AggregateException)
            { Publish($"\nThe DERT command could not be stopped. Waiting for its result. {error.Message}\n"); }
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }
        var drained = Task.WhenAll(stdout, stderr);
        try { await drained.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false); }
        catch (TimeoutException)
        {
            // A detached Notes/Help window can inherit these handles. The CLI has exited;
            // stop reading its pipes without terminating any launched program.
            captureCancellation.Cancel();
            await drained.ConfigureAwait(false);
            Publish("\nDERT has exited. A program it started kept the output connection open; capture has ended.\n");
        }
        return new(process.ExitCode, captured.ToString(), cancelled);
    }

    private static void ValidateExecutable(string cliPath)
    {
        if (string.IsNullOrWhiteSpace(cliPath) || !Path.IsPathFullyQualified(cliPath) ||
            !Path.GetFileName(cliPath).Equals("DERT.exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Use the full path of the bundled DERT.exe. The desktop command runner does not select programs from PATH.");
    }
}
