[CmdletBinding()]
param([Parameter(Mandatory)][string] $ToolPrefix, [string] $Executable)

# Opt-in real npm and PowerShell proof. Every installed package and invoked
# Codex-named runtime belongs to this workspace's disposable fixture.
$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if (-not $Executable) { $Executable = Join-Path $workspace '.artifacts/win-x64/DERT.exe' }
$Executable = (Resolve-Path -LiteralPath $Executable).Path
$ToolPrefix = (Resolve-Path -LiteralPath $ToolPrefix).Path
$shellPath = (Get-Process -Id $PID).Path
$proof = [IO.Path]::GetFullPath((Join-Path $workspace ('.artifacts/live-npm-doctor-conflict/' + [guid]::NewGuid().ToString('N'))))
$earlier = Join-Path $proof "earlier [author's] prefix"
$later = Join-Path $proof 'later intact prefix'
$searchPath = $earlier + ';' + $later
$node = Join-Path $earlier 'node.exe'
$npm = Join-Path $earlier 'node_modules/npm/bin/npm-cli.js'
$records = [Collections.Generic.List[object]]::new()
$visualProofs = [Collections.Generic.List[object]]::new()
$server = $null; $registry = ''; $outcome = 'Incomplete'; $failure = ''; $checks = 0

function Assert-WorkspacePath([string] $Path) {
    $resolved = [IO.Path]::GetFullPath($Path)
    if (-not $resolved.StartsWith($proof + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
        -not $proof.StartsWith($workspace + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "Fixture path is outside its disposable workspace: $resolved" }
    return $resolved
}
function Write-Fixture([string] $Path, [string] $Text) {
    $Path = Assert-WorkspacePath $Path
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path))
    [IO.File]::WriteAllText($Path, $Text, [Text.UTF8Encoding]::new($false))
}
function Snapshot([string] $Root) {
    Get-ChildItem -LiteralPath $Root -Recurse -File | ForEach-Object { $_.FullName + ':' + (Get-FileHash -LiteralPath $_.FullName).Hash }
}
function New-Start([string] $Program, [string[]] $Arguments) {
    $start = [Diagnostics.ProcessStartInfo]::new($Program)
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true; $start.RedirectStandardInput = $true
    $start.WorkingDirectory = $proof
    foreach ($key in @($start.Environment.Keys | Where-Object { $_ -like 'npm_config_*' -or $_ -in @('HTTP_PROXY','HTTPS_PROXY','ALL_PROXY') })) { [void]$start.Environment.Remove($key) }
    $start.Environment['PATH'] = $script:searchPath
    $start.Environment['PATHEXT'] = '.EXE;.CMD'
    $start.Environment['DERT_STATE_HOME'] = Join-Path $proof 'state'
    $start.Environment['CODEX_HOME'] = Join-Path $proof 'fixture-data'
    $start.Environment['npm_config_userconfig'] = Join-Path $proof 'empty-user.npmrc'
    $start.Environment['npm_config_globalconfig'] = Join-Path $proof 'empty-global.npmrc'
    $start.Environment['npm_config_cache'] = Join-Path $proof 'cache'
    $start.Environment['npm_config_registry'] = if ($script:registry) { $script:registry } else { 'http://127.0.0.1:1/' }
    $start.Environment['npm_config_audit'] = 'false'; $start.Environment['npm_config_fund'] = 'false'
    $start.Environment['npm_config_update_notifier'] = 'false'; $start.Environment['npm_config_fetch_retries'] = '0'
    $start.Environment['NO_PROXY'] = '127.0.0.1,localhost'
    $start.Environment['NODE_OPTIONS'] = ''
    $start.Environment['NODE_COMPILE_CACHE'] = Join-Path $proof 'node-cache'
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    return $start
}
function Invoke-Proof([string] $Name, [string] $Program, [string[]] $Arguments, [int] $ExpectedExit = 0) {
    $process = [Diagnostics.Process]::Start((New-Start $Program $Arguments))
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync(); $process.StandardInput.Close()
        if (-not $process.WaitForExit(60000)) { $process.Kill($true); throw "$Name exceeded 60 seconds." }
        $result = [pscustomobject]@{ Name=$Name; Program=$Program; Arguments=$Arguments; Exit=$process.ExitCode; Output=$stdout.GetAwaiter().GetResult(); Error=$stderr.GetAwaiter().GetResult() }
        $records.Add($result)
        if ($result.Exit -ne $ExpectedExit) { throw "$Name failed with exit $($result.Exit), expected $ExpectedExit : $($result.Output) $($result.Error)" }
        $script:checks++; Write-Host "EXIT OK $Name"; return $result
    } finally { $process.Dispose() }
}
function Assert-Contains([string] $Text, [string[]] $Expected) {
    foreach ($value in $Expected) { if (-not $Text.Contains($value)) { throw "Expected '$value' in retained output: $Text" } }
}

try {
    $toolFiles = @('node.exe','node_modules/npm/package.json','node_modules/npm/bin/npm-cli.js','node_modules/@openai/codex/package.json') |
        ForEach-Object { Join-Path $ToolPrefix $_ } | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf }
    $toolHashes = @($toolFiles | ForEach-Object { [pscustomobject]@{ Path=$_; Hash=(Get-FileHash -LiteralPath $_).Hash } })
    foreach ($prefix in @($earlier,$later)) {
        [void](Assert-WorkspacePath $prefix)
        [void][IO.Directory]::CreateDirectory((Join-Path $prefix 'node_modules'))
        Copy-Item -LiteralPath (Join-Path $ToolPrefix 'node.exe') -Destination (Join-Path $prefix 'node.exe')
        Copy-Item -LiteralPath (Join-Path $ToolPrefix 'node_modules/npm') -Destination (Join-Path $prefix 'node_modules/npm') -Recurse
        Write-Fixture (Join-Path $prefix 'npm.cmd') '@"%_prog%" "%dp0%\node_modules\npm\bin\npm-cli.js" %*'
    }
    Write-Fixture (Join-Path $proof 'empty-user.npmrc') ''; Write-Fixture (Join-Path $proof 'empty-global.npmrc') ''
    [void][IO.Directory]::CreateDirectory((Assert-WorkspacePath (Join-Path $proof 'fixture-data')))
    $nodeVersion = (Invoke-Proof 'Inspect copied Node version' $node @('--version')).Output.Trim()
    $npmVersion = (Invoke-Proof 'Inspect copied npm version' $node @($npm,'--version')).Output.Trim()
    $fixture = Join-Path $proof 'package'
    $manifest = @{ name='@openai/codex'; version='2.0.0'; bin=@{codex='bin/codex.js'}; engines=@{node='>=16'}; scripts=@{install='node -e "require(''fs'').writeFileSync(''SCRIPT_RAN'',''unexpected'')"'} }
    Write-Fixture (Join-Path $fixture 'package.json') ($manifest | ConvertTo-Json -Depth 5 -Compress)
    Write-Fixture (Join-Path $fixture 'bin/codex.js') ([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Fixtures/NpmDoctorConflict/runtime.js')))
    $packed = Invoke-Proof 'Pack benign test-owned Codex-named fixture' $node @($npm,'pack',$fixture,'--pack-destination',$proof,'--json','--ignore-scripts','--cache',(Join-Path $proof 'pack-cache'))
    $tarball = Join-Path $proof (($packed.Output | ConvertFrom-Json)[0].filename)
    $ready = Join-Path $proof 'registry-ready.json'; $requests = Join-Path $proof 'registry-requests.jsonl'; $configuration = Join-Path $proof 'registry.json'
    Write-Fixture $configuration (@{packages=@(@{manifest=$manifest;tarball=$tarball});ready=$ready;log=$requests} | ConvertTo-Json -Depth 10)
    $server = [Diagnostics.Process]::Start((New-Start $node @((Join-Path $PSScriptRoot 'Fixtures/NpmUpdate/registry.mjs'),$configuration)))
    $server.StandardInput.Close()
    for ($attempt=0; $attempt -lt 100 -and -not (Test-Path -LiteralPath $ready); $attempt++) {
        if ($server.HasExited) { throw ('Loopback registry stopped: ' + $server.StandardError.ReadToEnd()) }
        Start-Sleep -Milliseconds 100
    }
    if (-not (Test-Path -LiteralPath $ready)) { throw 'Loopback registry did not become ready.' }
    $script:registry = (Get-Content -LiteralPath $ready | ConvertFrom-Json).registry
    foreach ($prefix in @($earlier,$later)) {
        [void](Invoke-Proof 'Install real npm-generated launcher into disposable prefix' $node @($npm,'install','@openai/codex@2.0.0','--global','--prefix',$prefix,'--ignore-scripts','--dry-run=false','--include=optional','--engine-strict','--force=false','--no-audit','--no-fund'))
    }
    $earlierLaunch = Join-Path $earlier 'codex.ps1'; $laterLaunch = Join-Path $later 'codex.ps1'
    $entrypoint = Assert-WorkspacePath (Join-Path $earlier 'node_modules/@openai/codex/bin/codex.js')
    Remove-Item -LiteralPath $entrypoint
    $laterBefore = @(Snapshot $later)
    $resolveAndInvoke = '$PSNativeCommandUseErrorActionPreference=$false; $command=Get-Command codex -ErrorAction Stop; Write-Output ("CHOSEN="+$command.Source); & codex; exit $LASTEXITCODE'
    $before = Invoke-Proof 'Fresh PowerShell selects earlier route and reproduces missing entry point' $shellPath @('-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-Command',$resolveAndInvoke) 1
    Assert-Contains $before.Output @('CHOSEN=' + $earlierLaunch)
    Assert-Contains $before.Error @('MODULE_NOT_FOUND','codex.js')
    $laterCommand = '& ' + "'" + $laterLaunch.Replace("'","''") + "'" + '; exit $LASTEXITCODE'
    $direct = Invoke-Proof 'Direct later fixture launcher still succeeds' $shellPath @('-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-Command',$laterCommand)
    Assert-Contains $direct.Output @('DERT_NPM_DOCTOR_CONFLICT_OK')
    $diagnosis = Invoke-Proof 'DERT confirms the reproduced external-route conflict before selection' $Executable @('where','codex','--doctor','--plain') 1
    Assert-Contains $diagnosis.Output @('CONFLICT CONFIRMED / external PATH','First external route','Missing entry point','Later identified copy','FIXME / review the broken first copy')
    $review = Invoke-Proof 'Review correction of the broken earlier prefix' $Executable @('where','codex','--doctor','--path',$earlierLaunch,'--plain')
    Assert-Contains $review.Output @('CONFLICT CONFIRMED / external PATH','REINSTALL PROPOSAL','--apply')
    $token = [regex]::Match($review.Output,'--apply\s+''?([A-Fa-f0-9]{64})''?')
    if (-not $token.Success) { throw 'The correction did not retain an exact approval token.' }
    foreach ($columns in @(110,58)) {
        $visual = Invoke-Proof "Retain $columns-column MER conflict evidence before repair" $shellPath @('-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',(Join-Path $PSScriptRoot 'Export-MerVisualProof.ps1'),'-LocationPrefix',$earlier,'-LocationSearchPath',$searchPath,'-DiagnoseOnly','-Columns',$columns.ToString(),'-BuildFolder',(Split-Path $Executable))
        $visualMatch = [regex]::Match($visual.Output,'Rendered engine frames and ANSI replay retained at ([^\r\n]+)')
        if (-not $visualMatch.Success) { throw 'The visual exporter did not report its retained evidence folder.' }
        $visualPath = [IO.Path]::GetFullPath($visualMatch.Groups[1].Value.Trim())
        $visualRoot = [IO.Path]::GetFullPath((Join-Path $workspace '.artifacts/mer-visual'))
        if (-not $visualPath.StartsWith($visualRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Visual evidence escaped the workspace artifact folder.' }
        foreach ($image in @('doctor-evidence.png','location-01-doctor.png')) {
            if (-not (Test-Path -LiteralPath (Join-Path $visualPath $image) -PathType Leaf)) { throw "The visual exporter omitted $image." }
        }
        $visualProofs.Add([pscustomobject]@{ Columns=$columns; Path=$visualPath; Scope='Rendered actual MER engine frames before repair; not terminal OS screenshots.' })
    }
    $apply = Invoke-Proof 'Apply the exact earlier-copy correction through real npm' $Executable @('where','codex','--doctor','--path',$earlierLaunch,'--apply',$token.Groups[1].Value,'--plain')
    Assert-Contains $apply.Output @('REINSTALL VERIFIED','EXTERNAL ROUTE / REPAIR VERIFIED','Operation receipt')
    $after = Invoke-Proof 'Fresh PowerShell still selects earlier route and now invokes the fixture successfully' $shellPath @('-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-Command',$resolveAndInvoke)
    Assert-Contains $after.Output @(('CHOSEN=' + $earlierLaunch),'DERT_NPM_DOCTOR_CONFLICT_OK')
    $again = Invoke-Proof 'Repeated diagnosis retains overlap without a stale confirmed conflict' $Executable @('where','codex','--doctor','--plain') 1
    Assert-Contains $again.Output @('POSSIBLE CONFLICT / evidence only')
    if ($again.Output.Contains('CONFLICT CONFIRMED / external PATH')) { throw 'The repaired route still received a confirmed conflict diagnosis.' }
    if (Compare-Object $laterBefore @(Snapshot $later)) { throw 'Repair or fixture invocation changed the unrelated later prefix.' }
    foreach ($prefix in @($earlier,$later)) { if (Test-Path -LiteralPath (Join-Path $prefix 'node_modules/@openai/codex/SCRIPT_RAN')) { throw 'A fixture package installation script ran.' } }
    foreach ($original in $toolHashes) { if ($original.Hash -ne (Get-FileHash -LiteralPath $original.Path).Hash) { throw "A source tool file changed: $($original.Path)" } }
    $journalFile = Get-ChildItem -LiteralPath (Join-Path $proof 'state/doctor-receipts') -Filter '*.jsonl' -File | Sort-Object LastWriteTimeUtc | Select-Object -Last 1
    $journal = @(Get-Content -LiteralPath $journalFile.FullName | ForEach-Object { $_ | ConvertFrom-Json })
    foreach ($phase in @('prepared','uninstall-started','uninstall-finished','install-started','install-finished','external-route-condition-verified','verified','result')) { if ($journal.phase -notcontains $phase) { throw "Journal omitted $phase." } }
    if ($journal[-1].detail.state -ne 'Succeeded') { throw 'Doctor did not retain a successful final receipt.' }
    $outcome = 'Verified'; $checks++
    Write-Host "PASS Later prefix/source tools unchanged; package scripts disabled; retained doctor receipt. $checks live conflict checks passed."
}
catch { $failure = $_.Exception.Message; throw }
finally {
    if ($server) { if (-not $server.HasExited) { $server.Kill($true); $server.WaitForExit() }; $server.Dispose() }
    [void][IO.Directory]::CreateDirectory($proof)
    [pscustomobject]@{Outcome=$outcome;Failure=$failure;Scope='Test-owned benign packages for real npm-generated external PATH diagnosis and repair. Fresh PowerShell bypasses profiles and execution policy. This does not establish user-session aliases, native Codex runtime or authentication health.';Node=$nodeVersion;Npm=$npmVersion;PowerShell=$PSVersionTable.PSVersion.ToString();Earlier=$earlier;Later=$later;SearchPath=$searchPath;Checks=$checks;VisualProofs=$visualProofs;Calls=$records} |
        ConvertTo-Json -Depth 15 | Set-Content -LiteralPath (Join-Path $proof 'result.json')
    Write-Output (Join-Path $proof 'result.json')
}
