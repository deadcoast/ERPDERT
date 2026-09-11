namespace Dert.Cli;

/// <summary>Lets Windows setup detect live work without terminating a process.</summary>
public static class ApplicationLifetime
{
    public const string MutexName = @"Local\DERT.Application.Running";

    // Retain a handle, without claiming exclusive ownership: many notes and CLI
    // commands can coexist. Windows removes the object after the final handle.
    public static IDisposable? Track() => OperatingSystem.IsWindows()
        ? new Mutex(false, MutexName)
        : null;
}
