using System.ComponentModel;
using System.Diagnostics;

namespace Dert.Cli.Authoring;

public static class AuthoringCommand
{
    public static ProcessStartInfo LaunchInfo(string[] arguments, string applicationDirectory)
    {
        string? path = null;
        var still = false;
        foreach (var argument in arguments)
        {
            if (argument == "--still" && !still) still = true;
            else if (!argument.StartsWith("--", StringComparison.Ordinal) && path is null) path = Path.GetFullPath(argument);
            else throw new ArgumentException("Use DERT write [file.DERT] [--still]. Omit the file to start a new note.");
        }
        var executable = Path.GetFullPath(Path.Combine(applicationDirectory, "notes", "DERT.Notes.exe"));
        var start = new ProcessStartInfo(executable) { UseShellExecute = false };
        if (path is not null) start.ArgumentList.Add(path);
        if (still) start.ArgumentList.Add("--still");
        return start;
    }

    internal static int Run(string[] arguments, DocumentRenderer renderer)
    {
        try
        {
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The notes authoring window currently requires Windows.");
            var start = LaunchInfo(arguments, AppContext.BaseDirectory);
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
