using System.ComponentModel;
using System.Diagnostics;

namespace Dert.Cli.Authoring;

public static class AuthoringCommand
{
    public static ProcessStartInfo LaunchInfo(string[] arguments, string applicationDirectory, bool guide = false)
    {
        string? path = null;
        string? result = null;
        int? at = null;
        var still = false;
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];
            if (argument == "--still" && !still) still = true;
            else if (argument == "--result" && result is null && ++i < arguments.Length && !arguments[i].StartsWith("--", StringComparison.Ordinal))
                result = Path.GetFullPath(arguments[i]);
            else if (argument == "--at" && at is null && ++i < arguments.Length &&
                int.TryParse(arguments[i], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number) && number > 0)
                at = number;
            else if (!argument.StartsWith("--", StringComparison.Ordinal) && path is null) path = Path.GetFullPath(argument);
            else throw new ArgumentException("Use DERT write [file.DERT] [--still], or write file.DERT --result record.json [--at N].");
        }
        if (at is not null && result is null || result is not null && path is null)
            throw new ArgumentException("A result review needs a destination file; --at is only available with --result.");
        var executable = Path.GetFullPath(Path.Combine(applicationDirectory, "notes", "DERT.Notes.exe"));
        var start = new ProcessStartInfo(executable) { UseShellExecute = false };
        if (guide) start.ArgumentList.Add("--guide");
        if (path is not null) start.ArgumentList.Add(path);
        if (result is not null) { start.ArgumentList.Add("--result"); start.ArgumentList.Add(result); }
        if (at is not null) { start.ArgumentList.Add("--at"); start.ArgumentList.Add(at.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)); }
        if (still) start.ArgumentList.Add("--still");
        return start;
    }

    internal static int Run(string[] arguments, DocumentRenderer renderer, bool guide = false)
    {
        try
        {
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The notes authoring window currently requires Windows.");
            var start = LaunchInfo(arguments, AppContext.BaseDirectory, guide);
            if (!File.Exists(start.FileName))
            {
                renderer.Failure("The notes host is missing from this copy. Keep the published notes folder beside DERT.exe. From source, use src/Publish-Windows.ps1 to build both together.");
                return 2;
            }
            using var process = Process.Start(start) ?? throw new IOException("Windows did not start the notes window.");
            return 0;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or Win32Exception or PlatformNotSupportedException)
        {
            renderer.Failure(error.Message);
            return 2;
        }
    }
}
