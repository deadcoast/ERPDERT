using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Erpdert.Cli.Repairs;

internal static class WindowsFileGuard
{
    internal static string Identity(FileStream stream)
    {
        if (!OperatingSystem.IsWindows()) return stream.Name;
        if (!GetFileInformationByHandle(stream.SafeFileHandle, out var info))
            throw new IOException("The opened file's identity could not be checked.", new Win32Exception(Marshal.GetLastWin32Error()));
        return $"{info.Volume:X8}-{info.IndexHigh:X8}{info.IndexLow:X8}";
    }

    internal static void CheckPath(string path)
    {
        for (string? current = System.IO.Path.GetFullPath(path); current is not null;
            current = System.IO.Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new RepairException("DERT4101",
                    "This path passes through a link or redirected folder. Preview is available; repair a regular local copy instead.");
        }
    }

    internal static FileStream OpenExclusive(string path)
    {
        if (!OperatingSystem.IsWindows() || path.StartsWith(@"\\", StringComparison.Ordinal))
            throw new RepairException("DERT4101", "Applying repairs currently requires a regular local Windows file. Preview is still available.");
        if (path[(System.IO.Path.GetPathRoot(path)?.Length ?? 0)..].Contains(':'))
            throw new RepairException("DERT4101", "This path names an alternate file stream, not a separate document. Repair a regular copy instead.");
        CheckPath(path);
        // Do not follow a final symbolic link that appeared after the path check.
        var handle = CreateFile(path, 0xC0000000, 0, IntPtr.Zero, 3, 0x80200000, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            var error = new Win32Exception(Marshal.GetLastWin32Error());
            handle.Dispose();
            throw new IOException("The file could not be locked for repair. Check read-only status and close other programs holding it open.", error);
        }
        try
        {
            if (!GetFileInformationByHandle(handle, out var info))
                throw new IOException("The opened file's identity could not be checked.", new Win32Exception(Marshal.GetLastWin32Error()));
            if ((info.Attributes & (uint)(FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0 || info.Links != 1)
                throw new RepairException("DERT4101", "This is a linked or redirected file. Repair a regular copy so another path's contents cannot change too.");
            return new FileStream(handle, FileAccess.ReadWrite);
        }
        catch { handle.Dispose(); throw; }
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security,
        uint creation, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation information);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME Creation, Access, Write;
        public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }
}
