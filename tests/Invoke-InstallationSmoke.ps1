[CmdletBinding()]
param(
    [string] $Setup,
    [string] $ProofId,
    [string] $ProofRoot,
    [string] $Compiler,
    [string] $PackageDirectory = '.artifacts/win-x64',
    [switch] $OnProofDesktop
)

$ErrorActionPreference = 'Stop'
if ($Compiler) {
    if ($Setup -or $ProofId -or $ProofRoot) { throw 'Choose Compiler for a new proof, or provide an already-built proof, without mixing the two.' }
    $ProofId = [Guid]::NewGuid().ToString('N')
    $ProofRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../.artifacts/installation-proof/$ProofId"))
    & (Join-Path $PSScriptRoot '../src/Build-Installer.ps1') -Compiler $Compiler -PackageDirectory $PackageDirectory -ProofId $ProofId -ProofRoot $ProofRoot -OutputDirectory $ProofRoot
    $Setup = Join-Path $ProofRoot "DERT-Proof-$ProofId.exe"
}
if (-not $Setup -or -not (Test-Path -LiteralPath $Setup)) {
    throw 'The Windows installer has not been built. Build a proof installer before running its delivery checks.'
}
if ($ProofId -notmatch '^[a-f0-9]{32}$' -or [IO.Path]::GetFileName($Setup) -ne "DERT-Proof-$ProofId.exe") {
    throw 'Installation smoke accepts only an explicitly named proof build, never the personal installation.'
}
$Setup = (Resolve-Path -LiteralPath $Setup).Path
$ProofRoot = [IO.Path]::GetFullPath($ProofRoot)
if (-not ('InstallationProbe' -as [type])) { Add-Type -Path (Join-Path $PSScriptRoot 'InstallationProbe.cs') }
if (-not $OnProofDesktop) {
    # A separate desktop keeps real native controls away from the person's typing.
    # All quoted values are literal filesystem paths; Windows names cannot contain ".
    $childArguments = '-NoProfile -File "' + $PSCommandPath + '" -Setup "' + $Setup + '" -ProofId ' + $ProofId + ' -ProofRoot "' + $ProofRoot + '" -OnProofDesktop'
    $exitCode = [InstallationProbe]::RunOnDesktop((Get-Process -Id $PID).Path, $childArguments, "DertProof$ProofId", (Get-Location).Path)
    foreach ($file in @('interaction.txt', 'output.txt')) {
        $output = Join-Path $ProofRoot $file
        if (Test-Path -LiteralPath $output) { Write-Host ([IO.File]::ReadAllText($output)) }
    }
    if ($exitCode -ne 0) { throw "Installation proof failed with exit $exitCode. Evidence: $ProofRoot" }
    Write-Host "Installation proof completed on an isolated desktop. Evidence: $ProofRoot"
    return
}
$app = Join-Path $ProofRoot "program files [DERT] 日本語's & notes"
$examples = Join-Path $ProofRoot 'Documents/DERT examples'
$record = [Collections.Generic.List[string]]::new()
$stateKey = "Software\DERT\InstallationProof\$ProofId"
$progId = "DERT.Proof.$ProofId"
$extension = ".dert-proof-$ProofId"
$pathValue = "DERT_Proof_$ProofId"
$group = Join-Path ([Environment]::GetFolderPath('Programs')) "DERT proof $ProofId"
$uninstallKey = "Software\Microsoft\Windows\CurrentVersion\Uninstall\DERT.Proof.$($ProofId)_is1"
$user = [Microsoft.Win32.Registry]::CurrentUser

function Assert([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw $Message }
    $record.Add("PASS $Message")
}

function Read-Value([string] $Key, [string] $Name = '') {
    $openedKey = $user.OpenSubKey($Key)
    if (-not $openedKey) { return $null }
    try { return $openedKey.GetValue($Name, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames) }
    finally { $openedKey.Dispose() }
}

function Invoke-Setup([string[]] $Extra = @(), [switch] $Refused) {
    $start = [Diagnostics.ProcessStartInfo]::new($Setup)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.WindowStyle = 'Hidden'
    foreach ($argument in @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/DIR=$app", "/LOG=$(Join-Path $ProofRoot ('setup-' + [Guid]::NewGuid().ToString('N') + '.log'))") + $Extra) {
        $start.ArgumentList.Add($argument)
    }
    $process = [Diagnostics.Process]::Start($start)
    try {
        if (-not $process.WaitForExit(120000)) { throw 'Installer exceeded its deadline; inspect the retained log and process.' }
        if ($Refused) { Assert ($process.ExitCode -ne 0) 'setup refuses an unsafe change' }
        else { Assert ($process.ExitCode -eq 0) 'installer reports completed installation' }
    } finally { $process.Dispose() }
}

function Invoke-Removal([switch] $Refused) {
    $uninstallCommand = Read-Value $uninstallKey 'UninstallString'
    if ($uninstallCommand -notmatch '^"(.+\\unins[0-9]+\.exe)"$') { throw 'The registered proof removal command is not a quoted Inno uninstaller.' }
    $uninstaller = $Matches[1]
    if ([IO.Path]::GetDirectoryName($uninstaller) -ne $app) { throw 'The proof removal command points outside its owned application directory.' }
    $start = [Diagnostics.ProcessStartInfo]::new($uninstaller)
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true; $start.WindowStyle = 'Hidden'
    foreach ($argument in @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/LOG=$(Join-Path $ProofRoot ('remove-' + [Guid]::NewGuid().ToString('N') + '.log'))")) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    try {
        if (-not $process.WaitForExit(120000)) { throw 'Removal exceeded its deadline; inspect the retained log and process.' }
        if ($Refused) { Assert ($process.ExitCode -ne 0) 'removal refuses an unsafe change' }
        else {
            Assert ($process.ExitCode -eq 0) 'uninstaller reports completed removal'
            # Inno's final helper removes the running uninstaller after its parent
            # exits. Wait for that actual cleanup before attempting a fresh install.
            $deadline = [DateTime]::UtcNow.AddSeconds(10)
            while ((Test-Path -LiteralPath $uninstaller) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 100 }
            Assert (-not (Test-Path -LiteralPath $uninstaller)) 'the uninstaller finishes its own file cleanup'
        }
    } finally { $process.Dispose() }
}

function Check-Help {
    $start = [Diagnostics.ProcessStartInfo]::new((Join-Path $app 'DERT.exe'))
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    $start.WorkingDirectory = $ProofRoot
    $start.Environment['PATH'] = ''
    $start.Environment['DOTNET_ROOT'] = Join-Path $ProofRoot 'absent runtime'
    $start.Environment['DOTNET_MULTILEVEL_LOOKUP'] = '0'
    $start.ArgumentList.Add('-help')
    $process = [Diagnostics.Process]::Start($start)
    try {
        $output = $process.StandardOutput.ReadToEndAsync(); $errors = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(15000)) { throw 'Installed CLI did not finish help.' }
        Assert ($process.ExitCode -eq 0 -and $output.Result.Contains('write') -and -not $errors.Result) 'installed help works without PATH or an external runtime'
    } finally { $process.Dispose() }
}

[void][IO.Directory]::CreateDirectory($ProofRoot)
$notesProcess = $null
$notesWindow = [IntPtr]::Zero
$environmentKey = $user.CreateSubKey('Environment')
try {
    Assert ($null -eq (Read-Value $stateKey 'InstallDirectory') -and $null -eq (Read-Value 'Environment' $pathValue)) 'proof starts with fresh isolated registrations'
    Assert (-not (Test-Path -LiteralPath $app)) 'proof owns a new disposable application directory'
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    $record.Add("ACCOUNT $($identity.Name); administrator token: $($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)); this is not a clean-account claim")
    $originalPath = '%TEMP%\first;;C:\another tool;'
    $environmentKey.SetValue($pathValue, $originalPath, [Microsoft.Win32.RegistryValueKind]::ExpandString)
    $otherAssociation = $user.CreateSubKey("Software\Classes\$extension")
    $otherAssociation.SetValue('', 'Other.Notes'); $otherAssociation.Dispose()

    $environmentKey.SetValue($pathValue, [string[]]@('not', 'a PATH string'), [Microsoft.Win32.RegistryValueKind]::MultiString)
    Invoke-Setup @('/TASKS=commandline,fileassociation') -Refused
    Assert (-not (Test-Path -LiteralPath (Join-Path $app 'DERT.exe'))) 'unsupported PATH storage is refused before copying application files'
    $environmentKey.SetValue($pathValue, $originalPath, [Microsoft.Win32.RegistryValueKind]::ExpandString)

    [void][IO.Directory]::CreateDirectory($app)
    $occupied = Join-Path $app 'existing words.txt'
    [IO.File]::WriteAllText($occupied, 'Leave these words alone.')
    Invoke-Setup @('/TASKS=commandline,fileassociation') -Refused
    Assert ([IO.File]::ReadAllText($occupied) -eq 'Leave these words alone.') 'an occupied destination is refused without overwriting its contents'
    Remove-Item -LiteralPath $occupied

    Invoke-Setup @('/TASKS=commandline,fileassociation')
    Check-Help
    Assert ((Read-Value $uninstallKey 'InstallLocation').TrimEnd('\') -eq $app) 'Windows removal registration names the installed folder'
    Assert ((Read-Value 'Environment' $pathValue) -eq ($originalPath + $app)) 'PATH appends one directory and preserves existing order, variables and empty entries'
    Assert ($environmentKey.GetValueKind($pathValue) -eq [Microsoft.Win32.RegistryValueKind]::ExpandString) 'PATH retains its registry value type'
    $command = Read-Value "Software\Classes\$progId\shell\open\command"
    Assert ($command -eq ('"' + (Join-Path $app 'notes/DERT.Notes.exe') + '" "%1"')) 'association opens the fixed Notes host with a quoted literal filename'
    Assert ((Read-Value "Software\Classes\$extension") -eq 'Other.Notes') 'installation preserves another default file handler'
    Assert ($null -ne (Read-Value "Software\Classes\$extension\OpenWithProgids" $progId)) 'DERT is registered as an Open with choice'
    $shortcut = [InstallationProbe]::ReadShortcut((Join-Path $group 'DERT Notes.lnk'))
    Assert ($shortcut[0] -eq (Join-Path $app 'notes/DERT.Notes.exe')) 'Start opens the installed Notes host directly, preserving Unicode'
    $examplePath = Join-Path $examples 'weekend.DERT'
    Assert (Test-Path -LiteralPath $examplePath) 'the first-launch example lives outside installed binaries'
    $edited = "// MY OWN WORDS`r`n// ------------`r`n// Unfinished ; note ;;`r`n"
    [IO.File]::WriteAllText($examplePath, $edited, [Text.UTF8Encoding]::new($true))
    $exampleHash = (Get-FileHash -LiteralPath $examplePath).Hash
    $foreignPath = Join-Path $app 'my own note.DERT'
    [IO.File]::WriteAllText($foreignPath, $edited)
    $markerPath = Join-Path $app '.dert-install-owner'
    $markerTime = [DateTime]::new(2020, 1, 2, 3, 4, 5, [DateTimeKind]::Utc)
    [IO.File]::SetLastWriteTimeUtc($markerPath, $markerTime)

    Invoke-Setup @('/TASKS=commandline,fileassociation')
    Assert ([IO.File]::GetLastWriteTimeUtc($markerPath) -eq $markerTime) 'repeat installation reuses valid recovery evidence without rewriting it'
    Assert ((Read-Value 'Environment' $pathValue) -eq ($originalPath + $app)) 'repeat installation does not duplicate PATH'
    Assert ((Get-FileHash -LiteralPath $examplePath).Hash -eq $exampleHash) 'repeat installation preserves the edited first-launch example'
    $exampleShortcut = [InstallationProbe]::ReadShortcut((Join-Path $group 'Your DERT example.lnk'))
    Assert ($exampleShortcut[1] -eq ('"' + $examplePath + '"')) 'the repeat-visit shortcut returns to the same editable example'

    Invoke-Setup @('/TASKS=')
    Assert ((Read-Value 'Environment' $pathValue) -eq $originalPath) 'deselecting CLI on reinstall restores the exact earlier PATH'
    Assert ($null -eq (Read-Value "Software\Classes\$progId\shell\open\command")) 'deselecting file integration on reinstall withdraws that choice'
    Assert ($null -eq (Read-Value $stateKey 'AddedPath')) 'withdrawing CLI clears its PATH ownership'
    Invoke-Setup @('/TASKS=commandline,fileassociation')
    Assert ((Read-Value 'Environment' $pathValue) -eq ($originalPath + $app)) 'reselecting CLI restores exactly one working connection'
    & (Join-Path $PSScriptRoot 'Invoke-InstallationInteraction.ps1') -Setup $Setup -ProofId $ProofId -ProofRoot $ProofRoot
    $record.Add('PASS native installer controls, cancellation, late-open refusal and Windows association opening')

    # Start the actual installed application with hostile-to-shell, valid filenames.
    $literalPath = Join-Path $ProofRoot "literal [日本語]'s & 100% ; note.DERT"
    [IO.File]::WriteAllText($literalPath, $edited, [Text.UTF8Encoding]::new($true))
    $start = [Diagnostics.ProcessStartInfo]::new((Join-Path $app 'notes/DERT.Notes.exe'))
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true; $start.WindowStyle = 'Hidden'
    $start.Environment['PATH'] = ''; $start.Environment['DOTNET_ROOT'] = Join-Path $ProofRoot 'absent runtime'
    $start.ArgumentList.Add($literalPath); $start.ArgumentList.Add('--still')
    $notesProcess = [Diagnostics.Process]::Start($start)
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        Start-Sleep -Milliseconds 100
        $notesWindow = [InstallationProbe]::FindWindow([IO.Path]::GetFileName($literalPath) + ' / DERT')
    } while (-not $notesProcess.HasExited -and $notesWindow -eq [IntPtr]::Zero -and [DateTime]::UtcNow -lt $deadline)
    Assert (-not $notesProcess.HasExited -and $notesWindow -ne [IntPtr]::Zero -and [InstallationProbe]::Owner($notesWindow) -eq $notesProcess.Id) 'installed Notes opens a literal filename without an external editor or runtime'
    Invoke-Setup @('/TASKS=commandline,fileassociation') -Refused
    Invoke-Removal -Refused
    Assert (-not $notesProcess.HasExited) 'refused setup and removal leave the open Notes process alive'
    Assert ([InstallationProbe]::CloseOwnedWindow($notesWindow, $notesProcess.Id)) 'installed Notes accepts ordinary window close'
    Assert ($notesProcess.WaitForExit(10000) -and $notesProcess.ExitCode -eq 0) 'installed Notes closes normally'
    $notesProcess.Dispose(); $notesProcess = $null
    Assert ([IO.File]::ReadAllText($literalPath) -eq $edited) 'opening the unfinished note leaves its source intact'

    $environmentKey.SetValue($pathValue, [string[]]@('changed', 'to another type'), [Microsoft.Win32.RegistryValueKind]::MultiString)
    Invoke-Removal -Refused
    Assert ((Test-Path -LiteralPath (Join-Path $app 'DERT.exe')) -and $null -ne (Read-Value $stateKey 'AddedPath')) 'unreadable owned PATH stops removal while retaining application files and ownership'

    $environmentKey.SetValue($pathValue, ($originalPath + $app + ';C:\added later'), [Microsoft.Win32.RegistryValueKind]::ExpandString)
    Invoke-Removal
    Assert (-not (Test-Path -LiteralPath (Join-Path $app 'DERT.exe'))) 'removal deletes installed binaries'
    Assert (Test-Path -LiteralPath $foreignPath) 'removal preserves an unrelated note even inside the application folder'
    Assert ((Get-FileHash -LiteralPath $examplePath).Hash -eq $exampleHash) 'removal preserves edited examples and their exact bytes'
    Assert ((Read-Value 'Environment' $pathValue) -eq ($originalPath + 'C:\added later')) 'removal preserves PATH changes made by another program'
    Assert ($null -eq (Read-Value "Software\Classes\$progId\shell\open\command")) 'removal withdraws the DERT file-opening command'
    Assert ((Read-Value "Software\Classes\$extension") -eq 'Other.Notes') 'removal preserves another file handler'
    Assert (-not (Test-Path -LiteralPath (Join-Path $group 'DERT Notes.lnk'))) 'removal withdraws its Start entry'
    Assert ($null -eq (Read-Value $uninstallKey 'InstallLocation')) 'removal withdraws its Windows uninstall entry'

    # Pre-existing equivalent entries are never claimed for removal.
    $existingPath = 'C:\before;"' + $app.ToUpperInvariant() + '\";C:\after'
    $environmentKey.SetValue($pathValue, $existingPath, [Microsoft.Win32.RegistryValueKind]::String)
    # An unrelated file left after removal makes the destination intentionally occupied.
    Remove-Item -LiteralPath $foreignPath
    Invoke-Setup @('/TASKS=commandline,fileassociation')
    Assert ((Read-Value 'Environment' $pathValue) -eq $existingPath) 'an existing quoted, case-varied PATH entry is not duplicated'
    Invoke-Removal
    Assert ((Read-Value 'Environment' $pathValue) -eq $existingPath -and $environmentKey.GetValueKind($pathValue) -eq [Microsoft.Win32.RegistryValueKind]::String) 'removal leaves a pre-existing PATH entry and value type intact'

    Invoke-Setup @('/TASKS=')
    Assert ($null -eq (Read-Value "Software\Classes\$progId\shell\open\command")) 'declining file integration creates no association'
    Assert ((Read-Value 'Environment' $pathValue) -eq $existingPath) 'declining command-line integration leaves PATH unchanged'
    Invoke-Removal
    $environmentKey.DeleteValue($pathValue)
    Invoke-Setup @('/TASKS=commandline', '/ProofPathFailure=1') -Refused
    Assert (Test-Path -LiteralPath (Join-Path $app 'DERT.exe')) 'a late integration failure reports failure with the installed application retained'
    Check-Help
    $environmentKey.SetValue($pathValue, '', [Microsoft.Win32.RegistryValueKind]::String)
    Invoke-Removal

    $environmentKey.DeleteValue($pathValue, $false)
    Invoke-Setup @('/TASKS=commandline')
    Assert ((Read-Value 'Environment' $pathValue) -eq $app) 'an absent PATH gains the selected application directory'
    Invoke-Removal
    Assert ($null -eq (Read-Value 'Environment' $pathValue)) 'removal restores an originally absent PATH value'

    $environmentKey.SetValue($pathValue, '', [Microsoft.Win32.RegistryValueKind]::String)
    Invoke-Setup @('/TASKS=commandline')
    $environmentKey.SetValue($pathValue, $app, [Microsoft.Win32.RegistryValueKind]::ExpandString)
    Invoke-Removal
    Assert ((Read-Value 'Environment' $pathValue) -ceq '' -and $environmentKey.GetValueKind($pathValue) -eq [Microsoft.Win32.RegistryValueKind]::ExpandString) 'removal restores an empty PATH while preserving a later value-type change'

    $environmentKey.DeleteValue($pathValue)
    # Interrupt our own setup process during a real payload copy. Never target
    # a running authoring window or a process discovered by its name.
    $start = [Diagnostics.ProcessStartInfo]::new($Setup)
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true; $start.WindowStyle = 'Hidden'
    foreach ($argument in @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/DIR=$app", '/TASKS=commandline,fileassociation', "/LOG=$(Join-Path $ProofRoot 'interrupted.log')")) { $start.ArgumentList.Add($argument) }
    $interrupted = [Diagnostics.Process]::Start($start)
    try {
        $deadline = [DateTime]::UtcNow.AddSeconds(20)
        $firstPayload = Join-Path $app 'DERT.exe'
        while (-not $interrupted.HasExited -and -not (Test-Path -LiteralPath $firstPayload) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 10 }
        Assert (-not $interrupted.HasExited -and (Test-Path -LiteralPath $firstPayload)) 'the interruption case reaches a real in-progress payload copy'
        $interrupted.Kill($true)
        [void]$interrupted.WaitForExit(10000)
    } finally { $interrupted.Dispose() }
    Assert (Test-Path -LiteralPath (Join-Path $app '.dert-install-owner')) 'the interrupted installation retains a verified ownership marker'
    [IO.File]::SetLastWriteTimeUtc($markerPath, $markerTime)
    $unrelatedAfterInterruption = Join-Path $app 'words added after interruption.DERT'
    [IO.File]::WriteAllText($unrelatedAfterInterruption, $edited)
    Invoke-Setup @('/TASKS=commandline,fileassociation')
    Check-Help
    Assert ([IO.File]::GetLastWriteTimeUtc($markerPath) -eq $markerTime) 'retry preserves the durable marker against another interruption'
    Assert ([IO.File]::ReadAllText($unrelatedAfterInterruption) -eq $edited) 'retry completes the owned payload without changing an unrelated file'
    Invoke-Removal
    Assert ((Test-Path -LiteralPath $unrelatedAfterInterruption) -and -not (Test-Path -LiteralPath (Join-Path $app '.dert-install-owner'))) 'removal after retry preserves authored files and removes the recovery marker'
    $orphanLogs = @(Get-ChildItem -LiteralPath $app -Filter 'unins*' -File)
    if ($orphanLogs.Count -gt 0) { $record.Add('RETAINED incomplete uninstall artifacts from interruption: ' + (($orphanLogs | Select-Object -ExpandProperty Name) -join ', ')) }
    $environmentKey.DeleteValue($pathValue, $false)
    $user.DeleteSubKeyTree("Software\Classes\$extension")
    Write-Host ($record -join "`n")
    Write-Host "Installation delivery checks passed. Evidence: $ProofRoot"
}
catch {
    $record.Add('FAIL ' + $_.Exception.Message)
    throw
}
finally {
    # Never kill an application to make cleanup pass. Retain failed installs for inspection.
    if ($notesProcess) { [void][InstallationProbe]::CloseOwnedWindow($notesWindow, $notesProcess.Id); $notesProcess.Dispose() }
    $environmentKey.Dispose()
    [IO.File]::WriteAllText((Join-Path $ProofRoot 'output.txt'), ($record -join "`n"), [Text.UTF8Encoding]::new($false))
}
