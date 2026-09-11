using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

// WScript.Shell exposes ANSI shortcut paths on some Windows configurations.
// Read the same Unicode interface that Explorer uses to resolve the shortcut.
public static class InstallationProbe
{
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr window, IntPtr dc, uint flags);
    public static int[] Size(IntPtr window)
    {
        GetWindowRect(window, out var rect);
        return new[] { rect.Right - rect.Left, rect.Bottom - rect.Top };
    }
    public static bool Print(IntPtr window, IntPtr dc) { return PrintWindow(window, dc, 2); }
    private delegate bool Visit(IntPtr window, IntPtr state);
    [DllImport("user32.dll")] private static extern bool EnumWindows(Visit callback, IntPtr state);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, Visit callback, IntPtr state);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int size);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsWindowEnabled(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);

    public static string Caption(IntPtr window)
    {
        var text = new StringBuilder(32768);
        GetWindowText(window, text, text.Capacity);
        return text.ToString();
    }

    public static IntPtr FindWindow(string caption)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((window, _) => {
            if (Caption(window) != caption) return true;
            found = window;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    public static IntPtr FindWindowWithText(string title, string content)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((window, _) => {
            if (!Caption(window).Contains(title) || !VisibleText(window).Contains(content)) return true;
            found = window;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    public static bool Click(IntPtr parent, string text)
    {
        if (parent == IntPtr.Zero) return false;
        IntPtr found = IntPtr.Zero;
        EnumChildWindows(parent, (window, _) => {
            if (Caption(window).Replace("&", "") != text || !IsWindowVisible(window) || !IsWindowEnabled(window)) return true;
            found = window;
            return false;
        }, IntPtr.Zero);
        if (found == IntPtr.Zero) return false;
        // Posting avoids blocking on a modal confirmation opened by the button.
        return PostMessage(found, 0x00F5, IntPtr.Zero, IntPtr.Zero);
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);

    public static bool CloseOwnedWindow(IntPtr window, int expectedProcess)
    {
        if (window == IntPtr.Zero || !IsWindow(window) || Owner(window) != expectedProcess || !IsWindowEnabled(window))
            return false;
        return PostMessage(window, 0x10, IntPtr.Zero, IntPtr.Zero);
    }

    public static string WindowState(int process)
    {
        var text = new StringBuilder();
        EnumWindows((window, _) => {
            if (Owner(window) == process)
                text.AppendLine($"HWND {window}; visible={IsWindowVisible(window)}; enabled={IsWindowEnabled(window)}; owner={GetWindow(window, 4)}; caption={Caption(window)}");
            return true;
        }, IntPtr.Zero);
        return text.ToString();
    }

    public static string VisibleText(IntPtr parent)
    {
        if (parent == IntPtr.Zero) return "";
        var text = new StringBuilder(Caption(parent));
        EnumChildWindows(parent, (window, _) => {
            if (IsWindowVisible(window)) text.AppendLine(Caption(window));
            return true;
        }, IntPtr.Zero);
        return text.ToString();
    }

    public static int Owner(IntPtr window)
    {
        GetWindowThreadProcessId(window, out var process);
        return (int)process;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellExecuteInfo
    {
        public int Size;
        public uint Mask;
        public IntPtr Window;
        public string Verb, File, Parameters, Directory;
        public int Show;
        public IntPtr Instance, IdList;
        public string Class;
        public IntPtr ClassKey;
        public uint HotKey;
        public IntPtr Icon, Process;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool ShellExecuteEx(ref ShellExecuteInfo info);
    [DllImport("kernel32.dll")] private static extern uint GetProcessId(IntPtr handle);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Size;
        public string Reserved, Desktop, Title;
        public int X, Y, Width, Height, XChars, YChars, Fill;
        public uint Flags;
        public short Show, ReservedSize;
        public IntPtr ReservedPointer, Input, Output, Error;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInfo { public IntPtr Process, Thread; public int ProcessId, ThreadId; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateDesktop(string name, IntPtr device, IntPtr mode, uint flags, uint access, IntPtr security);
    [DllImport("user32.dll")] private static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcess(string application, StringBuilder arguments, IntPtr processAttributes,
        IntPtr threadAttributes, bool inherit, uint flags, IntPtr environment, string directory,
        ref StartupInfo startup, out ProcessInfo info);
    [DllImport("kernel32.dll")] private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
    [DllImport("kernel32.dll")] private static extern bool GetExitCodeProcess(IntPtr process, out uint code);

    public static int RunOnDesktop(string executable, string arguments, string name, string directory)
    {
        var desktop = CreateDesktop(name, IntPtr.Zero, IntPtr.Zero, 0, 0x01FF, IntPtr.Zero);
        if (desktop == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            var startup = new StartupInfo { Size = Marshal.SizeOf<StartupInfo>(), Desktop = "WinSta0\\" + name };
            if (!CreateProcess(executable, new StringBuilder("\"" + executable + "\" " + arguments), IntPtr.Zero,
                IntPtr.Zero, false, 0x08000000, IntPtr.Zero, directory, ref startup, out var process))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                WaitForSingleObject(process.Process, 0xFFFFFFFF);
                GetExitCodeProcess(process.Process, out var code);
                return (int)code;
            }
            finally { CloseHandle(process.Thread); CloseHandle(process.Process); }
        }
        finally { CloseDesktop(desktop); }
    }

    public static int OpenWith(string file, string progId)
    {
        var info = new ShellExecuteInfo {
            Size = Marshal.SizeOf<ShellExecuteInfo>(), Mask = 0x00000141,
            Verb = "open", File = file, Class = progId, Show = 0
        };
        if (!ShellExecuteEx(ref info)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        try { return (int)GetProcessId(info.Process); }
        finally { if (info.Process != IntPtr.Zero) CloseHandle(info.Process); }
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int count, IntPtr data, uint flags);
        void GetIDList(out IntPtr value);
        void SetIDList(IntPtr value);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder text, int count);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string text);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder text, int count);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string text);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder text, int count);
    }

    public static string[] ReadShortcut(string path)
    {
        object link = new ShellLink();
        try
        {
            ((IPersistFile)link).Load(path, 0);
            var target = new StringBuilder(32768);
            var arguments = new StringBuilder(32768);
            ((IShellLinkW)link).GetPath(target, target.Capacity, IntPtr.Zero, 4);
            ((IShellLinkW)link).GetArguments(arguments, arguments.Capacity);
            return new[] { target.ToString(), arguments.ToString() };
        }
        finally { Marshal.FinalReleaseComObject(link); }
    }
}
