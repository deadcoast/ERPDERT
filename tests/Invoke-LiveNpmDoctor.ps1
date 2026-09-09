param([Parameter(Mandatory)][string]$ToolPrefix)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$proofRoot = Join-Path $workspace ('.artifacts/npm-doctor-proof/' + [guid]::NewGuid().ToString('N'))
$prefix = Join-Path $proofRoot 'disposable prefix'
if (-not $prefix.StartsWith($workspace + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Proof must stay in this workspace.' }
New-Item -ItemType Directory -Path $prefix -Force | Out-Null
$node = Join-Path $prefix 'node.exe'
$npmRoot = Join-Path $prefix 'node_modules/npm'
New-Item -ItemType Directory -Path (Split-Path $npmRoot) -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $ToolPrefix 'node.exe') -Destination $node
Copy-Item -LiteralPath (Join-Path $ToolPrefix 'node_modules/npm') -Destination $npmRoot -Recurse
$npm = Join-Path $npmRoot 'bin/npm-cli.js'
$config = Join-Path $proofRoot 'empty.npmrc'
[IO.File]::WriteAllText($config, '')
$globalConfig = Join-Path $proofRoot 'empty-global.npmrc'
[IO.File]::WriteAllText($globalConfig, '')
$common = @('--userconfig', $config, '--globalconfig', $globalConfig, '--cache', (Join-Path $proofRoot 'cache'), '--ignore-scripts', '--no-audit', '--no-fund', '--no-update-notifier', '--color=false')
$package = Join-Path $proofRoot 'fixture'
New-Item -ItemType Directory -Path (Join-Path $package 'bin') -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $package 'package.json'), '{"name":"@openai/codex","version":"1.0.0","bin":{"codex":"bin/codex.js"},"scripts":{"install":"node -e \"require(''fs'').writeFileSync(''SCRIPT_RAN'', ''unexpected'')\""}}')
[IO.File]::WriteAllText((Join-Path $package 'bin/codex.js'), '#!/usr/bin/env node' + "`n" + 'throw new Error("This is a doctor fixture, not Codex.");')
$pack = & $node $npm pack $package --pack-destination $proofRoot --json @common
if ($LASTEXITCODE -ne 0) { throw 'Fixture pack failed.' }
$tarball = Join-Path $proofRoot (($pack | ConvertFrom-Json)[0].filename)
$records = [Collections.Generic.List[object]]::new()
foreach ($phase in @('install', 'uninstall', 'install')) {
    $target = if ($phase -eq 'uninstall') { '@openai/codex' } else { $tarball }
    $response = & $node $npm $phase $target --global --prefix $prefix --dry-run=false --include=optional --engine-strict --force=false @common 2>&1
    $records.Add([pscustomobject]@{ Phase = $phase; ExitCode = $LASTEXITCODE; Output = ($response | Out-String) })
    if ($LASTEXITCODE -ne 0) { throw "$phase failed: $response" }
    $root = Join-Path $prefix 'node_modules/@openai/codex'
    if ($phase -eq 'uninstall' -and (Test-Path -LiteralPath $root)) { throw 'npm did not remove the package directory.' }
    if ($phase -eq 'install' -and -not (Test-Path -LiteralPath (Join-Path $root 'bin/codex.js'))) { throw 'npm did not restore the entrypoint.' }
    if (Test-Path -LiteralPath (Join-Path $root 'SCRIPT_RAN')) { throw 'Package scripts unexpectedly ran.' }
}
$inventory = & $node $npm ls --depth=0 --long --json '@openai/codex' --global --prefix $prefix @common
if ($LASTEXITCODE -ne 0) { throw 'Inventory failed.' }
$listed = $inventory | ConvertFrom-Json
if ($listed.dependencies.'@openai/codex'.version -ne '1.0.0') { throw 'Unexpected installed version.' }
$result = [pscustomobject]@{ Scope = 'Real npm with a local fixture tarball; not a live Codex repair or DoctorService integration.'; Prefix = $prefix; Node = (& $node --version); Npm = (& $node $npm --version); ScriptsDisabled = $true; Phases = $records; Inventory = $listed }
$result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $proofRoot 'result.json')
Write-Output (Join-Path $proofRoot 'result.json')
