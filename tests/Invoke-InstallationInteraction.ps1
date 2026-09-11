[CmdletBinding()]
param([string] $Setup, [string] $ProofId, [string] $ProofRoot)

$ErrorActionPreference = 'Stop'
if ($ProofId -notmatch '^[a-f0-9]{32}$' -or [IO.Path]::GetFileName($Setup) -ne "DERT-Proof-$ProofId.exe") { throw 'Use a namespaced installation proof.' }
if (-not ('InstallationProbe' -as [type])) { Add-Type -Path (Join-Path $PSScriptRoot 'InstallationProbe.cs') }
Add-Type -AssemblyName System.Drawing
$app = Join-Path $ProofRoot "program files [DERT] 日本語's & notes"
$stateKey = "Software\DERT\InstallationProof\$ProofId"
$pathValue = "DERT_Proof_$ProofId"
$user = [Microsoft.Win32.Registry]::CurrentUser
$state = $user.OpenSubKey($stateKey)
if (-not $state -or $state.GetValue('InstallDirectory') -ne $app) { throw 'The interaction proof needs its own installed application.' }
$state.Dispose()
$environmentKey = $user.OpenSubKey('Environment')
$pathBefore = $environmentKey.GetValue($pathValue, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
$environmentKey.Dispose()
$record = [Collections.Generic.List[string]]::new()
$wizard = $null; $notes = $null; $removal = $null
$noteWindow = [IntPtr]::Zero; $exampleWindow = [IntPtr]::Zero

function Wait-Window([string] $Title, [string] $Contains) {
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        $window = [InstallationProbe]::FindWindowWithText($Title, $Contains)
        if ($window -ne [IntPtr]::Zero) { return $window }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "No installer window contained '$Contains'."
}

function Capture([IntPtr] $Window, [string] $Name) {
    $size = [InstallationProbe]::Size($Window)
    $bitmap = [Drawing.Bitmap]::new($size[0], $size[1])
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $dc = $graphics.GetHdc()
    try { if (-not [InstallationProbe]::Print($Window, $dc)) { throw "Could not render $Name." } }
    finally { $graphics.ReleaseHdc($dc); $graphics.Dispose() }
    try { $bitmap.Save((Join-Path $ProofRoot "$Name.png"), [Drawing.Imaging.ImageFormat]::Png) }
    finally { $bitmap.Dispose() }
    [IO.File]::WriteAllText((Join-Path $ProofRoot "$Name.txt"), [InstallationProbe]::VisibleText($Window))
}

function Click([IntPtr] $Window, [string] $Text) {
    if (-not [InstallationProbe]::Click($Window, $Text)) { throw "Could not select the visible '$Text' button." }
}

function Close-Notes([Diagnostics.Process] $Process, [IntPtr] $Window) {
    $Process.Refresh()
    [IO.File]::AppendAllText((Join-Path $ProofRoot 'notes-window-state.txt'),
        "Closing observed HWND $Window; process $($Process.Id); .NET main HWND $($Process.MainWindowHandle)`n" + [InstallationProbe]::WindowState($Process.Id))
    # Close the window whose caption and owner we inspected. .NET's heuristic
    # can select another top-level WPF window, or none while it is being shown.
    if (-not [InstallationProbe]::CloseOwnedWindow($Window, $Process.Id)) { throw 'Could not post a normal close to the inspected Notes window; see notes-window-state.txt.' }
    if (-not $Process.WaitForExit(10000)) { throw 'Notes did not exit after the inspected window received a normal close; see notes-window-state.txt.' }
    if ($Process.ExitCode -ne 0) { throw "Notes closed with exit $($Process.ExitCode)." }
}

try {
    # Opening an uninstaller and declining must not change PATH or installed files.
    $registration = $user.OpenSubKey("Software\Microsoft\Windows\CurrentVersion\Uninstall\DERT.Proof.$($ProofId)_is1")
    $command = $registration.GetValue('UninstallString'); $registration.Dispose()
    if ($command -notmatch '^"(.+\\unins[0-9]+\.exe)"$') { throw 'Unexpected proof uninstaller command.' }
    $removal = Start-Process -FilePath $Matches[1] -ArgumentList '/NORESTART' -WindowStyle Hidden -PassThru
    $confirmation = Wait-Window 'DERT installation proof' 'Remove DERT'
    Capture $confirmation 'remove-confirmation'
    Click $confirmation 'No'
    if (-not $removal.WaitForExit(10000)) { throw 'Cancelled removal did not return.' }
    $environmentKey = $user.OpenSubKey('Environment')
    try { if ($environmentKey.GetValue($pathValue, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames) -ne $pathBefore) { throw 'Cancelled removal changed PATH.' } }
    finally { $environmentKey.Dispose() }
    if (-not (Test-Path -LiteralPath (Join-Path $app 'DERT.exe'))) { throw 'Cancelled removal removed application files.' }
    $record.Add('PASS native uninstall confirmation cancellation preserves PATH and installed files')

    $start = [Diagnostics.ProcessStartInfo]::new($Setup)
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true; $start.WindowStyle = 'Hidden'
    foreach ($argument in @('/NORESTART', "/DIR=$app", '/TASKS=commandline,fileassociation')) { $start.ArgumentList.Add($argument) }
    $wizard = [Diagnostics.Process]::Start($start)
    $window = Wait-Window 'DERT installation proof' 'A little room for your words.'
    Capture $window 'setup-welcome'
    Click $window 'Next'
    $window = Wait-Window 'DERT installation proof' "Choose where DERT's application files will live."
    Capture $window 'setup-directory'
    Click $window 'Next'
    $window = Wait-Window 'DERT installation proof' 'Choose how you will return to DERT.'
    Capture $window 'setup-connections'
    Click $window 'Next'
    $window = Wait-Window 'DERT installation proof' 'Ready to make room for DERT.'
    Capture $window 'setup-ready'

    # Use the actual Windows association resolver, explicitly choosing our ProgID.
    # This does not change the Windows default for any file extension.
    $literalPath = Join-Path $ProofRoot "open with [日本語]'s & 100% ; note.DERT"
    $source = "// MY OWN WORDS`r`n// ------------`r`n// unfinished ; note ;;`r`n"
    [IO.File]::WriteAllText($literalPath, $source, [Text.UTF8Encoding]::new($true))
    $before = (Get-FileHash -LiteralPath $literalPath).Hash
    $processId = [InstallationProbe]::OpenWith($literalPath, "DERT.Proof.$ProofId")
    $notes = [Diagnostics.Process]::GetProcessById($processId)
    $null = $notes.Handle
    $noteWindow = Wait-Window ([IO.Path]::GetFileName($literalPath) + ' / DERT') ''
    if ([InstallationProbe]::Owner($noteWindow) -ne $notes.Id) { throw 'The associated window belongs to a different process.' }
    Capture $noteWindow 'installed-notes'
    $record.Add('PASS Windows ShellExecuteEx resolves the registered Notes handler and preserves literal Unicode and punctuation in its filename')

    $executableBefore = (Get-FileHash -LiteralPath (Join-Path $app 'DERT.exe')).Hash
    Click $window 'Install'
    $window = Wait-Window 'DERT installation proof' 'Close DERT Notes'
    Capture $window 'setup-running-refusal'
    if ($notes.HasExited -or (Get-FileHash -LiteralPath (Join-Path $app 'DERT.exe')).Hash -ne $executableBefore) { throw 'Late application opening was not preserved.' }
    $record.Add('PASS opening Notes after the wizard starts still prevents installation before copying')
    # The preparation-error page offers Cancel and its ordinary confirmation.
    Click $window 'Cancel'
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    while (-not $wizard.HasExited -and [DateTime]::UtcNow -lt $deadline) {
        $exitWindow = [InstallationProbe]::FindWindowWithText('Exit Setup', '')
        if ($exitWindow -ne [IntPtr]::Zero) { Click $exitWindow 'Yes'; break }
        Start-Sleep -Milliseconds 100
    }
    if (-not $wizard.WaitForExit(10000)) { throw 'Cancelled installer did not return.' }
    $record.Add('PASS setup cancellation leaves the installed application intact')
    Close-Notes $notes $noteWindow
    if ((Get-FileHash -LiteralPath $literalPath).Hash -ne $before) { throw 'Association opening changed source bytes.' }
    $record.Add('PASS closing the associated unfinished note keeps exact original bytes')

    # Finish a real interactive install and take its authored-example handoff.
    $notes.Dispose(); $notes = $null
    $wizard.Dispose()
    $wizard = [Diagnostics.Process]::Start($start)
    $window = Wait-Window 'DERT installation proof' 'A little room for your words.'
    Click $window 'Next'
    $window = Wait-Window 'DERT installation proof' "Choose where DERT's application files will live."
    Click $window 'Next'
    $window = Wait-Window 'DERT installation proof' 'Choose how you will return to DERT.'
    Click $window 'Next'
    $window = Wait-Window 'DERT installation proof' 'Ready to make room for DERT.'
    Click $window 'Install'
    $window = Wait-Window 'DERT installation proof' 'DERT is here.'
    Capture $window 'setup-finished'
    Click $window 'Finish'
    $exampleWindow = Wait-Window 'weekend.DERT / DERT' ''
    $notes = [Diagnostics.Process]::GetProcessById([InstallationProbe]::Owner($exampleWindow))
    $null = $notes.Handle
    if ($notes.MainModule.FileName -ne (Join-Path $app 'notes/DERT.Notes.exe')) { throw 'The first-launch window did not come from the installed host.' }
    Capture $exampleWindow 'first-launch-example'
    if (-not $wizard.WaitForExit(10000) -or $wizard.ExitCode -ne 0) { throw 'Interactive setup did not return success after its handoff.' }
    Close-Notes $notes $exampleWindow
    $record.Add('PASS finishing native setup opens the retained editable example in the installed Notes host')
    Write-Host ($record -join "`n")
}
catch { $record.Add('FAIL ' + $_.Exception.Message); throw }
finally {
    if ($notes) {
        if (-not $notes.HasExited) {
            [void][InstallationProbe]::CloseOwnedWindow($noteWindow, $notes.Id)
            [void][InstallationProbe]::CloseOwnedWindow($exampleWindow, $notes.Id)
            [void]$notes.WaitForExit(10000)
        }
        $notes.Dispose()
    }
    if ($wizard) {
        if (-not $wizard.HasExited) {
            $remaining = [InstallationProbe]::FindWindowWithText('DERT installation proof', '')
            [void][InstallationProbe]::Click($remaining, 'Cancel')
            Start-Sleep -Milliseconds 250
            $confirm = [InstallationProbe]::FindWindowWithText('Exit Setup', '')
            if ($confirm -ne [IntPtr]::Zero) { [void][InstallationProbe]::Click($confirm, 'Yes') }
            [void]$wizard.WaitForExit(10000)
        }
        $wizard.Dispose()
    }
    if ($removal) { $removal.Dispose() }
    [IO.File]::WriteAllText((Join-Path $ProofRoot 'interaction.txt'), ($record -join "`n"))
}
