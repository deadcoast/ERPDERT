[CmdletBinding()]
param([string] $Executable)

$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if (-not $Executable) { $Executable = Join-Path $projectPath '.artifacts/win-x64/DERT.exe' }
$Executable = (Resolve-Path -LiteralPath $Executable).Path
$packagePath = Split-Path -Parent $Executable
$proofPath = Join-Path $projectPath ('.artifacts/notes-publish-proof/' + [Guid]::NewGuid().ToString('N'))
[void] [IO.Directory]::CreateDirectory($proofPath)
$record = [Collections.Generic.List[string]]::new()

if (-not ('NotesWindowProbe' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class NotesWindowProbe {
    private delegate bool Visit(IntPtr window, IntPtr state);
    [DllImport("user32.dll")] private static extern bool EnumWindows(Visit visit, IntPtr state);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int size);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Entry {
        public uint Size, Usage, Process;
        public UIntPtr Heap;
        public uint Module, Threads, Parent;
        public int Priority;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Exe;
    }
    [DllImport("kernel32.dll")] private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint process);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool Process32FirstW(IntPtr snapshot, ref Entry entry);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool Process32NextW(IntPtr snapshot, ref Entry entry);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    public static int Child(int parent) {
        var snapshot = CreateToolhelp32Snapshot(2, 0);
        if (snapshot == new IntPtr(-1)) return 0;
        try {
            var entry = new Entry { Size = (uint)Marshal.SizeOf<Entry>() };
            if (Process32FirstW(snapshot, ref entry)) do {
                if (entry.Parent == parent && entry.Exe.Equals("DERT.Notes.exe", StringComparison.OrdinalIgnoreCase)) return (int)entry.Process;
            } while (Process32NextW(snapshot, ref entry));
            return 0;
        } finally { CloseHandle(snapshot); }
    }
    public static IntPtr Find(int process, string caption) {
        IntPtr found = IntPtr.Zero;
        EnumWindows((window, state) => {
            GetWindowThreadProcessId(window, out uint owner);
            if (owner != process) return true;
            var title = new StringBuilder(1024); GetWindowText(window, title, title.Capacity);
            if (title.ToString() == caption) { found = window; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }
    public static void Close(IntPtr window) { PostMessage(window, 0x10, IntPtr.Zero, IntPtr.Zero); }
}
'@
}

function Start-Info([string] $Program, [string[]] $CommandArgs) {
    $start = [Diagnostics.ProcessStartInfo]::new($Program)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $start.WorkingDirectory = $proofPath
    $start.Environment['PATH'] = ''
    $start.Environment['DOTNET_ROOT'] = (Join-Path $proofPath 'no-runtime')
    $start.Environment['DOTNET_MULTILEVEL_LOOKUP'] = '0'
    $start.Environment['DERT_MOTION'] = 'off'
    foreach ($argument in $CommandArgs) { $start.ArgumentList.Add($argument) }
    return $start
}

function Check-Window([string[]] $CommandArgs, [string] $Caption, [switch] $ThroughCli) {
    $launcher = $null
    $process = $null
    try {
        if ($ThroughCli) {
            $start = Start-Info $Executable (@('write') + $CommandArgs)
            $launcher = [Diagnostics.Process]::Start($start)
            $deadline = [DateTime]::UtcNow.AddSeconds(10)
            $child = 0
            while ([DateTime]::UtcNow -lt $deadline) {
                $child = [NotesWindowProbe]::Child($launcher.Id)
                if ($child -ne 0) { break }
                Start-Sleep -Milliseconds 50
            }
            if ($child -eq 0) { throw 'CLI write did not launch its packaged notes child.' }
            $process = [Diagnostics.Process]::GetProcessById($child)
            # Retain the process handle while it is alive so its exit code is
            # still queryable after WM_CLOSE (GetProcessById opens lazily).
            $null = $process.Handle
            if (-not $launcher.WaitForExit(5000) -or $launcher.ExitCode -ne 0) { throw 'CLI write did not return success after launch.' }
        }
        else {
            $start = Start-Info (Join-Path $packagePath 'notes/DERT.Notes.exe') $CommandArgs
            $process = [Diagnostics.Process]::Start($start)
        }
        $deadline = [DateTime]::UtcNow.AddSeconds(15)
        $window = [IntPtr]::Zero
        while (-not $process.HasExited -and [DateTime]::UtcNow -lt $deadline) {
            $window = [NotesWindowProbe]::Find($process.Id, $Caption)
            if ($window -ne [IntPtr]::Zero) { break }
            Start-Sleep -Milliseconds 50
        }
        if ($window -eq [IntPtr]::Zero) { throw "The published notes host did not create '$Caption'." }
        [NotesWindowProbe]::Close($window)
        if (-not $process.WaitForExit(5000)) { throw "The untouched notes window did not close promptly: $Caption; CLI=$ThroughCli" }
        if ($process.ExitCode -ne 0) { throw "The untouched notes window exited $($process.ExitCode): $Caption; CLI=$ThroughCli" }
        $record.Add("PASS published window: $Caption; CLI=$ThroughCli; no PATH or installed-runtime lookup; normal close")
    }
    finally {
        if ($process) {
            if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }
            $process.Dispose()
        }
        if ($launcher) {
            if (-not $launcher.HasExited) { $launcher.Kill($true); $launcher.WaitForExit() }
            $launcher.Dispose()
        }
    }
}

try {
    foreach ($required in @('notes/DERT.Notes.exe', 'notes/ICSharpCode.AvalonEdit.dll', 'notes/PresentationFramework.dll', 'notes/coreclr.dll', 'notes/AVALONEDIT-LICENSE.txt', 'UNICODE-LICENSE.txt', 'notes/UNICODE-LICENSE.txt', 'Try Notes.cmd')) {
        if (-not (Test-Path -LiteralPath (Join-Path $packagePath $required))) { throw "Missing packaged file: $required" }
    }
    $record.Add('PASS host, desktop runtime, editor dependency, license and first-launch shortcut packaged')
    $invalid = Start-Info $Executable @('write', '--apply')
    $invalid.RedirectStandardOutput = $true; $invalid.RedirectStandardError = $true
    $process = [Diagnostics.Process]::Start($invalid)
    try {
        $output = $process.StandardOutput.ReadToEnd(); $errorOutput = $process.StandardError.ReadToEnd()
        if (-not $process.WaitForExit(5000) -or $process.ExitCode -ne 2 -or -not $output.Contains('Use DERT write') -or $errorOutput) { throw "CLI write argument validation failed: $output $errorOutput" }
        $record.Add('PASS CLI write rejects unsupported arguments before launch')
    }
    finally { $process.Dispose() }
    Check-Window @('--still') 'Untitled.DERT / DERT'
    $path = Join-Path $proofPath "my [notes] ' & literal.DERT"
    $source = "// MY WORDS`r`n// --`r`n// why ; keep ;; Every word stays. //`r`n"
    [IO.File]::WriteAllText($path, $source, [Text.UTF8Encoding]::new($true))
    $before = (Get-FileHash -LiteralPath $path).Hash
    Check-Window @($path, '--still') ([IO.Path]::GetFileName($path) + ' / DERT')
    Check-Window @($path, '--still') ([IO.Path]::GetFileName($path) + ' / DERT') -ThroughCli
    if ((Get-FileHash -LiteralPath $path).Hash -ne $before) { throw 'Opening/reopening changed the original bytes.' }
    $record.Add('PASS mismatched title, BOM and original source unchanged across open/reopen')
    $listPath = Join-Path $proofPath "packing [list] ' & choices.DERT"
    $listSource = "items ; weekend ;;`r`nsnacks, // Apples, pears,`r`n"
    [IO.File]::WriteAllText($listPath, $listSource, [Text.UTF8Encoding]::new($true))
    $lint = Start-Info $Executable @('lint', $listPath, '--plain')
    $lint.RedirectStandardOutput = $true; $lint.RedirectStandardError = $true
    $process = [Diagnostics.Process]::Start($lint)
    try {
        $output = $process.StandardOutput.ReadToEnd(); $errorOutput = $process.StandardError.ReadToEnd()
        if (-not $process.WaitForExit(5000) -or $process.ExitCode -ne 0 -or $errorOutput) { throw 'Published list lint did not finish cleanly.' }
        $expected = "& '" + $Executable.Replace("'", "''") + "' write '" + $listPath.Replace("'", "''") + "'"
        if (-not $output.Contains('CHOOSE / IN NOTES') -or -not $output.Contains($expected) -or $output.Contains('--apply') -or $output.Contains('--from-lint')) {
            throw "List handoff did not distinguish current-file authoring from source-bound repair.`n$output"
        }
        $record.Add('PASS list lint prints an exact literal authoring command, with no repair approval')
    }
    finally { $process.Dispose() }
    $listBefore = (Get-FileHash -LiteralPath $listPath).Hash
    Check-Window @($listPath, '--still') ([IO.Path]::GetFileName($listPath) + ' / DERT') -ThroughCli
    if ((Get-FileHash -LiteralPath $listPath).Hash -ne $listBefore) { throw 'Opening the incomplete list changed its comma or annotation.' }
    $record.Add('PASS current-file authoring keeps the continuation comma and annotation untouched on open')
    $goodbyePath = Join-Path $proofPath "goodbye [fin] ' & florin.DERT"
    $goodbyeSource = "STERT`r`n// fin. stays a note`nfin. // Until tomorrow."
    [IO.File]::WriteAllText($goodbyePath, $goodbyeSource, [Text.UTF8Encoding]::new($true))
    $goodbyeBefore = (Get-FileHash -LiteralPath $goodbyePath).Hash
    Check-Window @($goodbyePath, '--still') ([IO.Path]::GetFileName($goodbyePath) + ' / DERT') -ThroughCli
    if ((Get-FileHash -LiteralPath $goodbyePath).Hash -ne $goodbyeBefore) { throw 'Opening the goodbye changed the literal source bytes.' }
    $record.Add('PASS goodbye opening preserves typed fin., BOM, mixed newlines and annotations')
    $unicodePath = Join-Path $proofPath '日本語 café [notes].DERT'
    $unicodeSource = "// café 日本語 👩‍💻`r`n// --------------`n// Keep these words."
    [IO.File]::WriteAllText($unicodePath, $unicodeSource, [Text.UTF8Encoding]::new($true))
    $unicodeBefore = (Get-FileHash -LiteralPath $unicodePath).Hash
    Check-Window @($unicodePath, '--still') ([IO.Path]::GetFileName($unicodePath) + ' / DERT') -ThroughCli
    if ((Get-FileHash -LiteralPath $unicodePath).Hash -ne $unicodeBefore) { throw 'Unicode title opening changed the source bytes.' }
    $record.Add('PASS Unicode title opening preserves its spelling, cell-counted rule, BOM and mixed newlines')
    Write-Host ($record -join "`n")
    Write-Host "All $($record.Count) published notes checks passed. Evidence: $proofPath"
}
finally { [IO.File]::WriteAllText((Join-Path $proofPath 'output.txt'), ($record -join "`n"), [Text.UTF8Encoding]::new($false)) }
