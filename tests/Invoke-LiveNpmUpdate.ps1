[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $ToolPrefix,
    [string] $Executable
)

# Uses real npm against an isolated loopback registry and disposable package prefix.
# The test package shares Codex's package name only so the real adapter is exercised.
$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if (-not $Executable) { $Executable = Join-Path $workspace '.artifacts/win-x64/DERT.exe' }
$Executable = (Resolve-Path -LiteralPath $Executable).Path
$ToolPrefix = (Resolve-Path -LiteralPath $ToolPrefix).Path
$proof = Join-Path $workspace ('.artifacts/live-npm-update/' + [guid]::NewGuid().ToString('N'))
$prefix = [IO.Path]::GetFullPath((Join-Path $proof "disposable [author's] prefix"))
if (-not $prefix.StartsWith($workspace + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'The npm fixture must stay inside this workspace.' }
$node = Join-Path $prefix 'node.exe'
$npmRoot = Join-Path $prefix 'node_modules/npm'
$npm = Join-Path $npmRoot 'bin/npm-cli.js'
$records = [Collections.Generic.List[object]]::new()
$server = $null
$registry = ''
$outcome = 'Incomplete'

function Write-Fixture([string] $Path, [string] $Text) {
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path))
    [IO.File]::WriteAllText($Path, $Text, [Text.UTF8Encoding]::new($false))
}

function New-Start([string] $Program, [string[]] $Arguments) {
    $start = [Diagnostics.ProcessStartInfo]::new($Program)
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true; $start.RedirectStandardInput = $true
    $start.WorkingDirectory = $proof
    foreach ($key in @($start.Environment.Keys | Where-Object { $_ -like 'npm_config_*' -or $_ -in @('HTTP_PROXY', 'HTTPS_PROXY', 'ALL_PROXY') })) { [void]$start.Environment.Remove($key) }
    $start.Environment['PATH'] = $prefix
    $start.Environment['PATHEXT'] = '.EXE;.CMD'
    $start.Environment['DERT_STATE_HOME'] = Join-Path $proof 'state'
    $start.Environment['npm_config_userconfig'] = Join-Path $proof 'empty-user.npmrc'
    $start.Environment['npm_config_globalconfig'] = Join-Path $proof 'empty-global.npmrc'
    $start.Environment['npm_config_cache'] = Join-Path $proof 'cache'
    $start.Environment['npm_config_registry'] = if ($script:registry) { $script:registry } else { 'http://127.0.0.1:1/' }
    $start.Environment['npm_config_audit'] = 'false'
    $start.Environment['npm_config_fund'] = 'false'
    $start.Environment['npm_config_update_notifier'] = 'false'
    $start.Environment['npm_config_fetch_retries'] = '0'
    $start.Environment['NO_PROXY'] = '127.0.0.1,localhost'
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    return $start
}

function Invoke-Proof([string] $Name, [string] $Program, [string[]] $Arguments, [int] $ExpectedExit = 0) {
    $process = [Diagnostics.Process]::Start((New-Start $Program $Arguments))
    try {
        $output = $process.StandardOutput.ReadToEndAsync(); $errors = $process.StandardError.ReadToEndAsync(); $process.StandardInput.Close()
        if (-not $process.WaitForExit(60000)) { $process.Kill($true); throw "$Name exceeded 60 seconds." }
        $result = [pscustomobject]@{ Name = $Name; Program = $Program; Arguments = $Arguments; Exit = $process.ExitCode; Output = $output.GetAwaiter().GetResult(); Error = $errors.GetAwaiter().GetResult() }
        $records.Add($result)
        if ($result.Exit -ne $ExpectedExit) { throw "$Name failed with exit $($result.Exit), expected $ExpectedExit : $($result.Output) $($result.Error)" }
        Write-Host "PASS $Name"
        return $result
    }
    finally { $process.Dispose() }
}

try {
    foreach ($relative in @('node.exe', 'node_modules/npm/package.json', 'node_modules/npm/bin/npm-cli.js')) {
        if (-not (Test-Path -LiteralPath (Join-Path $ToolPrefix $relative) -PathType Leaf)) { throw "ToolPrefix is missing $relative." }
    }
    $originalFiles = @('node.exe', 'node_modules/npm/package.json', 'node_modules/npm/bin/npm-cli.js', 'node_modules/@openai/codex/package.json') |
        ForEach-Object { Join-Path $ToolPrefix $_ } | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf }
    $originalHashes = @($originalFiles | ForEach-Object { [pscustomobject]@{ Path = $_; Hash = (Get-FileHash -LiteralPath $_).Hash } })
    [void][IO.Directory]::CreateDirectory((Split-Path $npmRoot))
    Copy-Item -LiteralPath (Join-Path $ToolPrefix 'node.exe') -Destination $node
    Copy-Item -LiteralPath (Join-Path $ToolPrefix 'node_modules/npm') -Destination $npmRoot -Recurse
    Write-Fixture (Join-Path $prefix 'npm.cmd') '@"%_prog%" "%dp0%\node_modules\npm\bin\npm-cli.js" %*'
    Write-Fixture (Join-Path $proof 'empty-user.npmrc') ''
    Write-Fixture (Join-Path $proof 'empty-global.npmrc') ''
    $nodeVersion = (Invoke-Proof 'Inspect copied Node version' $node @('--version')).Output.Trim()
    $npmVersion = (Invoke-Proof 'Inspect copied npm version' $node @($npm, '--version')).Output.Trim()
    $packages = @()
    foreach ($version in @('1.0.0', '2.0.0')) {
        $fixture = Join-Path $proof "package-$version"
        $manifest = @{ name = '@openai/codex'; version = $version; bin = @{ codex = 'bin/codex.js' }; engines = @{ node = '>=16' }; scripts = @{ install = 'node -e "require(''fs'').writeFileSync(''SCRIPT_RAN'', ''unexpected'')"' } }
        Write-Fixture (Join-Path $fixture 'package.json') ($manifest | ConvertTo-Json -Depth 5 -Compress)
        Write-Fixture (Join-Path $fixture 'bin/codex.js') ("#!/usr/bin/env node`n" + 'require("node:fs").writeFileSync(require("node:path").join(__dirname, "CODEX_RAN"), "unexpected"); throw new Error("Test fixture; never launch as Codex.");')
        # npm pack seeds its content cache. Keep packing separate so the later
        # update must fetch 2.0.0 from this fixture's loopback registry.
        $packed = Invoke-Proof "Pack local $version fixture" $node @($npm, 'pack', $fixture, '--pack-destination', $proof, '--json', '--ignore-scripts', '--cache', (Join-Path $proof 'pack-cache'))
        $tarball = Join-Path $proof (($packed.Output | ConvertFrom-Json)[0].filename)
        $packages += @{ manifest = $manifest; tarball = $tarball }
    }
    $ready = Join-Path $proof 'registry-ready.json'; $requests = Join-Path $proof 'registry-requests.jsonl'; $configuration = Join-Path $proof 'registry.json'
    Write-Fixture $configuration (@{ packages = $packages; ready = $ready; log = $requests } | ConvertTo-Json -Depth 10)
    $server = [Diagnostics.Process]::Start((New-Start $node @((Join-Path $PSScriptRoot 'Fixtures/NpmUpdate/registry.mjs'), $configuration)))
    $server.StandardInput.Close()
    for ($attempt = 0; $attempt -lt 100 -and -not (Test-Path -LiteralPath $ready); $attempt++) {
        if ($server.HasExited) { throw ('Loopback registry stopped: ' + $server.StandardError.ReadToEnd()) }
        Start-Sleep -Milliseconds 100
    }
    if (-not (Test-Path -LiteralPath $ready)) { throw 'Loopback registry did not become ready.' }
    $script:registry = (Get-Content -LiteralPath $ready | ConvertFrom-Json).registry
    [void](Invoke-Proof 'Seed disposable prefix with fixture 1.0.0' $node @($npm, 'install', $packages[0].tarball, '--global', '--prefix', $prefix, '--ignore-scripts', '--dry-run=false', '--include=optional', '--engine-strict', '--force=false', '--no-audit', '--no-fund'))
    $metadata = Join-Path $prefix 'node_modules/@openai/codex/package.json'
    $initialBytes = [IO.File]::ReadAllBytes($metadata)
    $launch = Join-Path $prefix 'codex.ps1'
    $rehearsal = Invoke-Proof 'Rehearse real npm update through DERT' $Executable @('update', 'codex', 'updateme', '--path', $launch, '--plain')
    if (-not $rehearsal.Output.Contains('REHEARSED / NOT APPLIED')) { throw 'The DERT rehearsal did not complete.' }
    if ([Convert]::ToBase64String($initialBytes) -ne [Convert]::ToBase64String([IO.File]::ReadAllBytes($metadata))) { throw 'Rehearsal changed package metadata.' }
    $token = [regex]::Match($rehearsal.Output, '--apply\s+''?([A-Fa-f0-9]{64})''?')
    if (-not $token.Success) { throw 'DERT did not retain an approval token for the reviewed update.' }
    $applied = Invoke-Proof 'Apply reviewed update through real npm' $Executable @('update', 'codex', 'updateme', '--path', $launch, '--apply', $token.Groups[1].Value, '--plain')
    if (-not $applied.Output.Contains('UPDATE / VERIFIED')) { throw 'DERT did not verify the installed update.' }
    $inventory = Invoke-Proof 'Independently inspect final npm inventory' $node @($npm, 'ls', '--depth=0', '--long', '--json', '@openai/codex', '--global', '--prefix', $prefix)
    if (($inventory.Output | ConvertFrom-Json).dependencies.'@openai/codex'.version -ne '2.0.0') { throw 'Final inventory did not identify fixture 2.0.0.' }
    if ((Get-Content -LiteralPath $metadata | ConvertFrom-Json).version -ne '2.0.0') { throw 'Final package metadata is not fixture 2.0.0.' }
    foreach ($marker in @('node_modules/@openai/codex/SCRIPT_RAN', 'node_modules/@openai/codex/bin/CODEX_RAN')) {
        if (Test-Path -LiteralPath (Join-Path $prefix $marker)) { throw "Unexpected execution marker: $marker" }
    }
    $journalFile = Get-ChildItem -LiteralPath (Join-Path $proof 'state/update-receipts') -Filter '*.jsonl' -File | Sort-Object LastWriteTimeUtc | Select-Object -Last 1
    $journal = @(Get-Content -LiteralPath $journalFile.FullName | ForEach-Object { $_ | ConvertFrom-Json })
    if ($journal.phase -notcontains 'verified' -or $journal[-1].detail.state -ne 'Succeeded') { throw 'Verified transaction journal was not retained.' }
    if (@($journal | Where-Object { $_.phase -eq 'install-started' }).Count -ne 1 -or @($journal | Where-Object { $_.phase -like 'uninstall*' }).Count -gt 0) { throw 'DERT must run one pinned install without uninstalling.' }
    $install = @($journal | Where-Object { $_.phase -eq 'install-started' })[0].detail
    foreach ($argument in @('@openai/codex@2.0.0', '--ignore-scripts', '--dry-run=false', '--engine-strict', '--include=optional', '--force=false', $prefix)) {
        if ($install.Arguments -notcontains $argument) { throw "The journal lost required install argument $argument." }
    }
    $served = @(Get-Content -LiteralPath $requests | ForEach-Object { $_ | ConvertFrom-Json })
    if ($served.path -notcontains '/tarballs/codex-2.0.0.tgz') { throw 'Real npm did not retrieve the 2.0.0 fixture from the loopback registry.' }
    $requestsBeforeReplay = (Get-FileHash -LiteralPath $requests).Hash
    $receiptsBeforeReplay = @(Get-ChildItem -LiteralPath (Join-Path $proof 'state/update-receipts') -Filter '*.jsonl' -File).Count
    $logs = Join-Path $proof 'cache/_logs'
    $logsBeforeReplay = @(Get-ChildItem -LiteralPath $logs -File | ForEach-Object { $_.Name + ':' + (Get-FileHash -LiteralPath $_.FullName).Hash })
    if ($logsBeforeReplay.Count -eq 0) { throw 'npm produced no command logs; replay command absence cannot be checked.' }
    $replayed = Invoke-Proof 'Refuse the used approval before starting npm again' $Executable @('update', 'codex', 'updateme', '--path', $launch, '--apply', $token.Groups[1].Value, '--plain') 1
    if (-not $replayed.Output.Contains('changed') -or $replayed.Output.Contains('UPDATE / VERIFIED')) { throw 'The used approval did not explain its stale evidence.' }
    $logsAfterReplay = @(Get-ChildItem -LiteralPath $logs -File | ForEach-Object { $_.Name + ':' + (Get-FileHash -LiteralPath $_.FullName).Hash })
    if (Compare-Object $logsBeforeReplay $logsAfterReplay) { throw 'A used approval unexpectedly caused npm command-log changes.' }
    if ($requestsBeforeReplay -ne (Get-FileHash -LiteralPath $requests).Hash -or $receiptsBeforeReplay -ne @(Get-ChildItem -LiteralPath (Join-Path $proof 'state/update-receipts') -Filter '*.jsonl' -File).Count) { throw 'A used approval unexpectedly created registry traffic or another transaction receipt.' }
    foreach ($original in $originalHashes) { if ($original.Hash -ne (Get-FileHash -LiteralPath $original.Path).Hash) { throw "A source tool file changed: $($original.Path)" } }
    $outcome = 'Verified'
    Write-Host "PASS Real npm $npmVersion / Node $nodeVersion / DERT update 1.0.0 -> 2.0.0 / one install, no uninstall, no scripts, no Codex launch."
}
finally {
    if ($server) { if (-not $server.HasExited) { $server.Kill($true); $server.WaitForExit() }; $server.Dispose() }
    [void][IO.Directory]::CreateDirectory($proof)
    [pscustomobject]@{ Outcome = $outcome; Scope = 'Actual DERT update transaction using real npm and test-owned packages from a loopback registry. No live Codex installation or native runtime health claim.'; Prefix = $prefix; Node = $nodeVersion; Npm = $npmVersion; Registry = $registry; Calls = $records } |
        ConvertTo-Json -Depth 15 | Set-Content -LiteralPath (Join-Path $proof 'result.json')
    Write-Output (Join-Path $proof 'result.json')
}
