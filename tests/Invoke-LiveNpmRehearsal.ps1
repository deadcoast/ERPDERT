[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $NodePrefix,
    [string] $Executable,
    [switch] $Mer
)

# Opt-in integration check: uses existing Node/npm and the public registry, but
# targets only a newly created fixture prefix. Never invokes the manual update.
$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if (-not $Executable) { $Executable = Join-Path $projectPath '.artifacts/win-x64/ERPDERT.exe' }
$Executable = (Resolve-Path -LiteralPath $Executable).Path
$NodePrefix = (Resolve-Path -LiteralPath $NodePrefix).Path
$node = Join-Path $NodePrefix 'node.exe'
$npm = Join-Path $NodePrefix 'node_modules/npm'
if (-not (Test-Path -LiteralPath $node -PathType Leaf) -or -not (Test-Path -LiteralPath (Join-Path $npm 'bin/npm-cli.js') -PathType Leaf)) {
    throw 'Choose an existing prefix with node.exe and node_modules/npm/bin/npm-cli.js.'
}
$proof = Join-Path $projectPath ('.artifacts\live-npm-proof\' + [Guid]::NewGuid().ToString('N'))
$prefix = Join-Path $proof 'disposable prefix'
$package = Join-Path $prefix 'node_modules/@openai/codex'
[void] [IO.Directory]::CreateDirectory((Join-Path $package 'bin'))
Copy-Item -LiteralPath $node -Destination (Join-Path $prefix 'node.exe')
# A copy, not a link: none of npm's files in the working installation is writable
# through this fixture. This check is slower than process fixtures and is opt-in.
Copy-Item -LiteralPath $npm -Destination (Join-Path $prefix 'node_modules/npm') -Recurse
[IO.File]::WriteAllText((Join-Path $prefix 'npm.cmd'), '@"%_prog%" "%dp0%\node_modules\npm\bin\npm-cli.js" %*')
[IO.File]::WriteAllText((Join-Path $prefix 'codex.ps1'), '& "$basedir/node$exe" "$basedir/node_modules/@openai/codex/bin/codex.js" $args')
[IO.File]::WriteAllText((Join-Path $package 'package.json'), '{"name":"@openai/codex","version":"0.0.0","bin":{"codex":"bin/codex.js"}}')
[IO.File]::WriteAllText((Join-Path $package 'bin/codex.js'), 'throw new Error("live rehearsal must never launch Codex");')
$userConfig = Join-Path $proof 'empty-user.npmrc'
$globalConfig = Join-Path $proof 'empty-global.npmrc'
[IO.File]::WriteAllText($userConfig, '')
[IO.File]::WriteAllText($globalConfig, '')
$before = @(Get-ChildItem -LiteralPath $prefix -Recurse -File | ForEach-Object { $_.FullName + ':' + (Get-FileHash -LiteralPath $_.FullName).Hash })
$start = [Diagnostics.ProcessStartInfo]::new($Executable)
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
$start.RedirectStandardInput = $true
$start.WorkingDirectory = $proof
$start.Environment['PATH'] = $prefix
$start.Environment['PATHEXT'] = '.EXE;.CMD'
# Keep network/cache effects in the disposable proof and use no private registry
# credentials or project config. Existing parent environment remains untouched.
foreach ($key in @($start.Environment.Keys | Where-Object { $_ -like 'npm_config_*' -or $_ -like 'NPM_CONFIG_*' })) { [void] $start.Environment.Remove($key) }
$start.Environment['npm_config_registry'] = 'https://registry.npmjs.org/'
$start.Environment['npm_config_userconfig'] = $userConfig
$start.Environment['npm_config_globalconfig'] = $globalConfig
$start.Environment['npm_config_cache'] = Join-Path $proof 'cache'
$source = Join-Path $proof 'live-update.DERT'
if ($Mer) {
    [IO.File]::WriteAllText($source, "STERT ; ERPDERT ;; codex`n// why ; codex ;; Check the exact copy. //`ncommands ; updateme ;;`nfin.")
    foreach ($argument in @('mer', $source, '--review', '--plain')) { $start.ArgumentList.Add($argument) }
}
else { foreach ($argument in @('codex', 'updateme', '--plain')) { $start.ArgumentList.Add($argument) } }
$process = [Diagnostics.Process]::Start($start)
try {
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    if ($Mer) { $process.StandardInput.Write("at 3`nrehearse`ncheck`ndetails`n`n") }
    $process.StandardInput.Close()
    if (-not $process.WaitForExit(60000)) { $process.Kill($true); throw 'Live npm rehearsal exceeded 60 seconds.' }
    $output = $stdout.GetAwaiter().GetResult()
    $errorOutput = $stderr.GetAwaiter().GetResult()
    [IO.File]::WriteAllText((Join-Path $proof 'output.txt'), $output + $errorOutput)
    Write-Host $output
    $expected = if ($Mer) { 'Rehearsed / not applied' } else { 'REHEARSED / NOT APPLIED' }
    if ($process.ExitCode -ne 0 -or $errorOutput -or -not $output.Contains($expected)) { throw "Live npm rehearsal did not complete: exit $($process.ExitCode). Evidence: $proof" }
    $after = @(Get-ChildItem -LiteralPath $prefix -Recurse -File | ForEach-Object { $_.FullName + ':' + (Get-FileHash -LiteralPath $_.FullName).Hash })
    if (Compare-Object $before $after) { throw "Live rehearsal changed fixture installation files. Evidence: $proof" }
    Write-Host "PASS Genuine npm inventory, registry query, and dry run; all $($before.Count) installation files unchanged. Evidence: $proof"
}
finally { $process.Dispose() }
