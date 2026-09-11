[CmdletBinding()]
param([string] $Executable)

$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if (-not $Executable) { $Executable = Join-Path $projectPath '.artifacts/win-x64/DERT.exe' }
$Executable = (Resolve-Path -LiteralPath $Executable).Path
$shellPath = (Get-Process -Id $PID).Path
$proofPath = Join-Path $projectPath ('.artifacts\update-proof\' + [Guid]::NewGuid().ToString('N'))
$fixtureBuild = Join-Path $proofPath 'fixture-build'
$prefix = Join-Path $proofPath "Ryan's [tools]"
$record = [Collections.Generic.List[string]]::new()
$checks = 0
$searchPath = $prefix

function Write-Fixture([string] $Path, [string] $Content) {
    [void] [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path))
    [IO.File]::WriteAllText($Path, $Content, [Text.UTF8Encoding]::new($false))
}

function New-Package([string] $Root) {
    [void] [IO.Directory]::CreateDirectory($Root)
    foreach ($file in Get-ChildItem -LiteralPath $fixtureBuild -File) { Copy-Item -LiteralPath $file.FullName -Destination $Root }
    Write-Fixture (Join-Path $Root 'codex.ps1') '& "$basedir/node$exe" "$basedir/node_modules/@openai/codex/bin/codex.js" $args'
    Write-Fixture (Join-Path $Root 'npm.cmd') '@"%_prog%" "%dp0%\node_modules\npm\bin\npm-cli.js" %*'
    Write-Fixture (Join-Path $Root 'node_modules/@openai/codex/package.json') '{"name":"@openai/codex","version":"1.0.0","bin":{"codex":"bin/codex.js"}}'
    Write-Fixture (Join-Path $Root 'node_modules/@openai/codex/bin/codex.js') 'must not execute'
    Write-Fixture (Join-Path $Root 'node_modules/npm/package.json') '{"name":"npm","version":"11.0.0"}'
    Write-Fixture (Join-Path $Root 'node_modules/npm/bin/npm-cli.js') 'Process fixture input; not a real npm program.'
    Write-Fixture (Join-Path $Root 'scenario.json') '{"latest":"2.0.0","fault":null}'
}

function Set-Scenario([string] $Latest, [string] $Fault = '', [bool] $SimulateRepair = $false) {
    Write-Fixture (Join-Path $prefix 'scenario.json') (@{ latest = $Latest; fault = $Fault; simulateRepair = $SimulateRepair } | ConvertTo-Json -Compress)
}

function Invoke-Child([string] $Program, [string[]] $CommandArgs, [string] $ReviewInput = '') {
    $start = [Diagnostics.ProcessStartInfo]::new($Program)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.RedirectStandardInput = $true
    $start.WorkingDirectory = $proofPath
    $start.Environment['PATH'] = $script:searchPath
    $start.Environment['PATHEXT'] = '.EXE;.CMD'
    $start.Environment['DERT_STATE_HOME'] = Join-Path $proofPath 'state'
    foreach ($argument in $CommandArgs) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $process.StandardInput.Write($ReviewInput)
        $process.StandardInput.Close()
        if (-not $process.WaitForExit(20000)) { $process.Kill($true); throw 'Update fixture exceeded 20 seconds.' }
        $result = [pscustomobject] @{ Exit = $process.ExitCode; Output = $stdout.GetAwaiter().GetResult(); Error = $stderr.GetAwaiter().GetResult() }
        $record.Add("$Program $($CommandArgs -join ' ')`nEXIT=$($result.Exit)`n$($result.Output)$($result.Error)")
        return $result
    }
    finally { $process.Dispose() }
}

function Assert-Result([string] $Name, $Result, [int] $Exit, [string[]] $Expected, [string[]] $Absent = @()) {
    if ($Result.Exit -ne $Exit -or $Result.Error) { throw "$Name failed: $($Result.Exit) $($Result.Output) $($Result.Error)" }
    foreach ($text in $Expected) { if (-not $Result.Output.Contains($text)) { throw "$Name omitted '$text': $($Result.Output)" } }
    foreach ($text in $Absent) { if ($Result.Output.Contains($text)) { throw "$Name unexpectedly included '$text': $($Result.Output)" } }
    if ($Result.Output.Contains([char]27)) { throw "$Name leaked ANSI controls into plain output." }
    $script:checks++
    Write-Host "PASS $Name"
}

function Get-Approval($Result) {
    $commands = @($Result.Output -split '\r?\n' | Where-Object { $_.StartsWith('& ') -and $_.Contains(' --apply ') })
    if ($commands.Count -ne 1) { throw 'Expected one intact DERT approval command.' }
    $token = [regex]::Match($commands[0], '--apply\s+''?([A-Fa-f0-9]{64})''?')
    if (-not $token.Success) { throw 'Approval command did not retain its exact proposal token.' }
    if ($commands[0].Contains("'install'") -or -not $commands[0].Contains('updateme')) { throw 'Approval escaped DERT into a raw package-manager command.' }
    return [pscustomobject]@{ Command = $commands[0]; Token = $token.Groups[1].Value }
}

function Get-Calls {
    $path = Join-Path $prefix 'calls.jsonl'
    if (Test-Path -LiteralPath $path) { Get-Content -LiteralPath $path | ForEach-Object { ,($_ | ConvertFrom-Json) } }
}

function Assert-Journal([string] $State, [bool] $Verified = $false) {
    $folder = Join-Path $proofPath 'state/update-receipts'
    $file = Get-ChildItem -LiteralPath $folder -File -Filter '*.jsonl' | Sort-Object LastWriteTimeUtc | Select-Object -Last 1
    if (-not $file) { throw 'The update did not retain an operation journal.' }
    $events = @(Get-Content -LiteralPath $file.FullName | ForEach-Object { $_ | ConvertFrom-Json })
    foreach ($phase in @('prepared', 'install-started', 'install-finished', 'result')) {
        if ($events.phase -notcontains $phase) { throw "Journal omitted $phase : $($file.FullName)" }
    }
    if (@($events.phase | Where-Object { $_ -like 'uninstall*' }).Count -gt 0) { throw 'Update used a doctor uninstall phase.' }
    if ($events[-1].detail.state -ne $State) { throw "Journal recorded $($events[-1].detail.state), expected $State." }
    if ($Verified -ne ($events.phase -contains 'verified')) { throw 'Journal verification did not match the actual outcome.' }
    $script:checks++
    Write-Host "PASS Durable $State journal / no uninstall"
}

try {
    $buildOutput = (& dotnet publish (Join-Path $PSScriptRoot 'CommandFixture') -c Release -o $fixtureBuild 2>&1 | Out-String)
    if ($LASTEXITCODE -ne 0) { throw $buildOutput }
    $record.Add($buildOutput)
    New-Package $prefix
    $example = Join-Path (Split-Path -Parent $Executable) 'examples/update-my-tools.DERT'
    Assert-Result 'Shipped update example previews without structural findings' (Invoke-Child $Executable @('run', $example, '--preview', '--plain')) 0 @('DERT update codex updateme', 'support doctoring') @('DERT2003', 'DERT1001', 'CHECK / npm')
    $metadata = Join-Path $prefix 'node_modules/@openai/codex/package.json'
    $originalHash = (Get-FileHash -LiteralPath $metadata).Hash
    $result = Invoke-Child $Executable @('update', 'codex', 'updateme', '--plain')
    Assert-Result 'Newer release retains a reviewed DERT proposal' $result 0 @('REHEARSED / NOT APPLIED', '1.0.0', '2.0.0', 'UPDATE PROPOSAL', ' --apply ') @('MANUAL UPDATE / CHANGES PACKAGES')
    $approval = Get-Approval $result
    if ($originalHash -ne (Get-FileHash -LiteralPath $metadata).Hash) { throw 'Rehearsal changed the installed package.' }
    $beforeApply = @(Get-Calls).Count
    Set-Scenario '2.0.0' '' $true
    $copied = Invoke-Child $shellPath @('-NoProfile', '-NonInteractive', '-Command', ($approval.Command + '; exit $LASTEXITCODE'))
    Assert-Result 'Copied DERT approval updates only the reviewed process fixture' $copied 0 @('UPDATE / VERIFIED', 'Operation receipt')
    $appliedCalls = @(Get-Calls | Select-Object -Skip $beforeApply)
    $mutations = @($appliedCalls | Where-Object { $_[1] -eq 'install' -and $_ -notcontains '--dry-run' })
    if ($mutations.Count -ne 1 -or @($appliedCalls | Where-Object { $_[1] -eq 'uninstall' }).Count -ne 0) { throw 'Update must run one install and no uninstall.' }
    $install = $mutations[0]
    foreach ($argument in @($prefix, '@openai/codex@2.0.0', '--dry-run=false', '--ignore-scripts', '--include=optional', '--engine-strict', '--force=false')) {
        if ($install -notcontains $argument) { throw "Approved install lost $argument." }
    }
    if ((Get-Content -LiteralPath $metadata | ConvertFrom-Json).version -ne '2.0.0') { throw 'The process fixture was not updated.' }
    Assert-Journal 'Succeeded' $true
    $beforeRepeated = (Get-FileHash -LiteralPath (Join-Path $prefix 'calls.jsonl')).Hash
    Assert-Result 'An old successful approval refuses re-execution' (Invoke-Child $shellPath @('-NoProfile', '-NonInteractive', '-Command', ($approval.Command + '; exit $LASTEXITCODE'))) 1 @('changed') @('UPDATE / VERIFIED')
    if ($beforeRepeated -ne (Get-FileHash -LiteralPath (Join-Path $prefix 'calls.jsonl')).Hash) { throw 'An old approval unexpectedly ran npm.' }
    New-Package $prefix

    Set-Scenario '1.0.0'
    Assert-Result 'Already-current copy needs no install rehearsal' (Invoke-Child $Executable @('update', 'codex', 'updateme', '--plain')) 0 @('ALREADY CURRENT') @('MANUAL UPDATE / CHANGES PACKAGES')
    Set-Scenario '0.9.0'
    Assert-Result 'Newer installed version is not downgraded' (Invoke-Child $Executable @('update', 'codex', 'updateme', '--plain')) 1 @('NEWER THAN LATEST') @('MANUAL UPDATE / CHANGES PACKAGES')
    Set-Scenario '2.0.0' 'wrong-root'
    Assert-Result 'Wrong npm target folder is explained' (Invoke-Child $Executable @('update', 'codex', 'updateme', '--plain')) 1 @('ERP5204') @('MANUAL UPDATE / CHANGES PACKAGES')
    Set-Scenario '2.0.0' 'view'
    Assert-Result 'Registry failure is not a current-version claim' (Invoke-Child $Executable @('update', 'codex', 'updateme', '--plain')) 1 @('ERP5203', 'fixture npm failure') @('ALREADY CURRENT', 'MANUAL UPDATE / CHANGES PACKAGES')
    Set-Scenario '2.0.0' 'install'
    Assert-Result 'Failed npm dry run never offers an update' (Invoke-Child $Executable @('update', 'codex', 'updateme', '--plain')) 1 @('ERP5203') @('REHEARSED / NOT APPLIED', 'MANUAL UPDATE / CHANGES PACKAGES')
    Set-Scenario '2.0.0-beta.1'
    Assert-Result 'Unimplemented release-channel comparison stays explicit' (Invoke-Child $Executable @('update', 'codex', 'updateme', '--plain')) 1 @('ERP5205') @('MANUAL UPDATE / CHANGES PACKAGES')
    Set-Scenario '2.0.0'

    $second = Join-Path $proofPath 'another copy'
    New-Package $second
    $script:searchPath = $prefix + ';' + $second
    $beforeChoice = (Get-FileHash -LiteralPath (Join-Path $prefix 'calls.jsonl')).Hash
    $choices = Invoke-Child $Executable @('update', 'codex', 'updateme', '--plain')
    Assert-Result 'Competing copies ask for a target before querying' $choices 1 @('CHOOSE THE COPY', '--path') @('MANUAL UPDATE / CHANGES PACKAGES')
    if ($beforeChoice -ne (Get-FileHash -LiteralPath (Join-Path $prefix 'calls.jsonl')).Hash) { throw 'Choosing a copy unexpectedly ran npm.' }
    $choice = @($choices.Output -split '\r?\n' | Where-Object { $_.StartsWith('& ') -and $_.Contains(' --path ') })[0]
    Assert-Result 'Copied target choice retains its literal path and plain output' (Invoke-Child $shellPath @('-NoProfile', '-NonInteractive', '-Command', $choice)) 0 @('REHEARSED / NOT APPLIED')
    $script:searchPath = $prefix

    $source = Join-Path $proofPath 'my updates.DERT'
    Write-Fixture $source "STERT ; update ;; codex`n// keep this explanation`ncommands ; updateme ;;`nƒin."
    $sourceHash = (Get-FileHash -LiteralPath $source).Hash
    Assert-Result 'DERT reaches the same rehearsal with its requesting line' (Invoke-Child $Executable @('run', $source, '--plain')) 0 @('REHEARSED / NOT APPLIED', 'keep this explanation', "FROM ${source}:3")
    $sourceReview = Invoke-Child $Executable @('run', $source, '--plain')
    $sourceApproval = Get-Approval $sourceReview
    if (-not $sourceApproval.Command.Contains(' --from-source ')) { throw 'The source-bound approval lost its originating document.' }
    $beforeStale = (Get-FileHash -LiteralPath (Join-Path $prefix 'calls.jsonl')).Hash
    [IO.File]::AppendAllText($source, "`n// A later authored change.")
    Assert-Result 'An edited source invalidates its saved approval before npm starts' (Invoke-Child $shellPath @('-NoProfile', '-NonInteractive', '-Command', ($sourceApproval.Command + '; exit $LASTEXITCODE'))) 1 @('changed') @('UPDATE / VERIFIED')
    if ($beforeStale -ne (Get-FileHash -LiteralPath (Join-Path $prefix 'calls.jsonl')).Hash) { throw 'Stale source approval unexpectedly ran npm.' }
    Write-Fixture $source "STERT ; update ;; codex`n// keep this explanation`ncommands ; updateme ;;`nƒin."
    $beforePreview = (Get-FileHash -LiteralPath (Join-Path $prefix 'calls.jsonl')).Hash
    Assert-Result 'DERT preview interprets update intent without external queries' (Invoke-Child $Executable @('run', $source, '--preview', '--plain')) 0 @('DERT update codex updateme') @('CHECK / npm', 'MANUAL UPDATE / CHANGES PACKAGES')
    if ($beforePreview -ne (Get-FileHash -LiteralPath (Join-Path $prefix 'calls.jsonl')).Hash) { throw 'Source preview unexpectedly ran npm.' }
    $unsupported = Join-Path $proofPath 'unsupported.DERT'
    Write-Fixture $unsupported "STERT ; update ;; codex`ncommands ; updateme ;;`nƒin.`nSTERT ; update ;; unknown"
    Assert-Result 'Unsupported later chain prevents even the earlier npm query' (Invoke-Child $Executable @('run', $unsupported, '--plain')) 1 @('No operations ran') @('CHECK / npm')
    if ($beforePreview -ne (Get-FileHash -LiteralPath (Join-Path $prefix 'calls.jsonl')).Hash) { throw 'Blocked document unexpectedly ran npm.' }
    Assert-Result 'MER discovers update copies without querying npm' (Invoke-Child $Executable @('mer', $source, '--review', '--plain') "at 3`nrehearse`n`n") 0 @('Copy 1 of 1', 'check queries npm and the registry') @('MANUAL UPDATE / CHANGES PACKAGES')
    if ($beforePreview -ne (Get-FileHash -LiteralPath (Join-Path $prefix 'calls.jsonl')).Hash) { throw 'MER preparation unexpectedly ran npm.' }
    Assert-Result 'MER runs the same process rehearsal attached to its source' (Invoke-Child $Executable @('mer', $source, '--review', '--plain') "at 1`nrehearse`ncheck`ndetails`napply`n`n") 0 @('3 | commands ; updateme ;;', 'Rehearsed / not applied', '1.0.0 -> 2.0.0', 'Update rehearsal recorded', 'NPM DRY RUN')
    $merCalls = @(Get-Content -LiteralPath (Join-Path $prefix 'calls.jsonl') | Select-Object -Last 4 | ForEach-Object { ,($_ | ConvertFrom-Json) })
    if ($merCalls.Count -ne 4 -or $merCalls[-1] -notcontains '--dry-run') { throw 'MER did not limit its install call to dry-run.' }
    Set-Scenario '2.0.0' 'install'
    Assert-Result 'MER retains a failed dependency dry run without offering an update' (Invoke-Child $Executable @('mer', $source, '--review', '--plain') "at 3`nrehearse`ncheck`ndetails`n`n") 1 @('Rehearsal stopped', 'ERP5203') @('MANUAL UPDATE / CHANGES PACKAGES')
    Set-Scenario '1.0.0'
    Assert-Result 'MER shows current versions without a fake dry run' (Invoke-Child $Executable @('mer', $source, '--review', '--plain') "at 3`nrehearse`ncheck`n`n") 0 @('Already current') @('Rehearsed / not applied')
    Set-Scenario '2.0.0'
    $script:searchPath = $prefix + ';' + $second
    $beforeChoice = (Get-FileHash -LiteralPath (Join-Path $prefix 'calls.jsonl')).Hash
    Assert-Result 'MER chooses and checks only the second copy' (Invoke-Child $Executable @('mer', $source, '--review', '--plain') "at 3`nrehearse`ncheck`ncopy 2`ncheck`ncopy 1`ncopy 2`n`n") 0 @('Copy 2 of 2', 'Rehearsed / not applied')
    if ($beforeChoice -ne (Get-FileHash -LiteralPath (Join-Path $prefix 'calls.jsonl')).Hash) { throw 'MER queried the unselected first copy.' }
    if ($sourceHash -ne (Get-FileHash -LiteralPath $source).Hash -or $originalHash -ne (Get-FileHash -LiteralPath $metadata).Hash) { throw 'Rehearsal changed source or installed metadata.' }
    $script:checks++
    Write-Host 'PASS Source and installed fixture bytes preserved'
    $script:searchPath = $prefix
    foreach ($failure in @(@{ Fault = 'install'; Exit = 1; State = 'InstallFailed'; Heading = 'INSTALLFAILED' }, @{ Fault = 'install-cancelled'; Exit = 130; State = 'Interrupted'; Heading = 'INTERRUPTED' })) {
        New-Package $prefix
        $review = Invoke-Child $Executable @('update', 'codex', 'updateme', '--plain')
        Assert-Result 'Rehearsal prepares the failure/interruption fixture' $review 0 @('REHEARSED / NOT APPLIED', ' --apply ')
        $failureApproval = Get-Approval $review
        Set-Scenario '2.0.0' $failure.Fault $true
        $beforeFailure = @(Get-Calls).Count
        $failed = Invoke-Child $shellPath @('-NoProfile', '-NonInteractive', '-Command', ($failureApproval.Command + '; exit $LASTEXITCODE'))
        Assert-Result "Update records $($failure.State) without claiming verification" $failed $failure.Exit @("UPDATE / $($failure.Heading)", 'Operation receipt') @('UPDATE / VERIFIED')
        $failureCalls = @(Get-Calls | Select-Object -Skip $beforeFailure)
        if (@($failureCalls | Where-Object { $_[1] -eq 'uninstall' }).Count -gt 0) { throw 'A failed update unexpectedly uninstalled the package.' }
        Assert-Journal $failure.State
    }
    Write-Host "$checks update executable checks passed. Evidence retained at $proofPath"
}
finally {
    [void] [IO.Directory]::CreateDirectory($proofPath)
    [IO.File]::WriteAllText((Join-Path $proofPath 'output.txt'), ($record -join "`n"), [Text.UTF8Encoding]::new($false))
}
