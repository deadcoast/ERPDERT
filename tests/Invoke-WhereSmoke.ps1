[CmdletBinding()]
param([string] $Executable)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$workspace = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if (-not $Executable) { $Executable = Join-Path $workspace '.artifacts/win-x64/DERT.exe' }
$Executable = (Resolve-Path -LiteralPath $Executable).Path
$proof = Join-Path $workspace ('.artifacts/where-proof/' + [guid]::NewGuid().ToString('N'))
$prefix = Join-Path $proof "Ryan's tools"
$state = Join-Path $proof 'state'
$record = [Collections.Generic.List[string]]::new()
$checks = 0
function Write-Fixture([string] $Relative, [string] $Text) {
    $path = Join-Path $prefix $Relative
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path))
    [IO.File]::WriteAllText($path, $Text)
}
function Invoke-Dert([string[]] $CommandArgs, [string] $InputText = '') {
    $start = [Diagnostics.ProcessStartInfo]::new($Executable)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.RedirectStandardInput = $true
    $start.WorkingDirectory = $proof
    $start.Environment['PATH'] = $prefix
    $start.Environment['PATHEXT'] = '.EXE;.CMD'
    $start.Environment['CODEX_HOME'] = Join-Path $proof 'fixture-data'
    $start.Environment['DERT_STATE_HOME'] = $state
    foreach ($argument in $CommandArgs) { $start.ArgumentList.Add($argument) }
    $child = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $child.StandardOutput.ReadToEndAsync()
        $stderr = $child.StandardError.ReadToEndAsync()
        $child.StandardInput.Write($InputText)
        $child.StandardInput.Close()
        if (-not $child.WaitForExit(30000)) { $child.Kill($true); throw 'Location/doctor fixture exceeded 30 seconds.' }
        $result = [pscustomobject]@{ Exit = $child.ExitCode; Output = $stdout.GetAwaiter().GetResult(); Error = $stderr.GetAwaiter().GetResult() }
        $record.Add("DERT $($CommandArgs -join ' ')`nEXIT=$($result.Exit)`n$($result.Output)$($result.Error)")
        return $result
    } finally { $child.Dispose() }
}
function Check([string] $Name, $Result, [int] $Exit, [string[]] $Expected) {
    if ($Result.Exit -ne $Exit -or $Result.Error) { throw "$Name failed: $($Result.Exit) $($Result.Output) $($Result.Error)" }
    foreach ($text in $Expected) { if (-not $Result.Output.Contains($text)) { throw "$Name omitted '$text': $($Result.Output)" } }
    if ($Result.Output.Contains([char]27)) { throw "$Name leaked ANSI controls." }
    $script:checks++
    Write-Host "PASS $Name"
}
try {
    $fixtureBuild = Join-Path $proof 'fixture-build'
    $build = (& dotnet publish (Join-Path $PSScriptRoot 'CommandFixture') -c Release -o $fixtureBuild 2>&1 | Out-String)
    if ($LASTEXITCODE -ne 0) { throw $build }
    [void][IO.Directory]::CreateDirectory($prefix)
    foreach ($file in Get-ChildItem -LiteralPath $fixtureBuild -File) { Copy-Item -LiteralPath $file.FullName -Destination $prefix }
    [void][IO.Directory]::CreateDirectory((Join-Path $proof 'fixture-data'))
    Write-Fixture 'codex.ps1' '& "$basedir/node$exe" "$basedir/node_modules/@openai/codex/bin/codex.js" $args'
    Write-Fixture 'npm.cmd' '@"%_prog%" "%dp0%\node_modules\npm\bin\npm-cli.js" %*'
    Write-Fixture 'node_modules/@openai/codex/package.json' '{"name":"@openai/codex","version":"1.0.0","bin":{"codex":"bin/codex.js"}}'
    Write-Fixture 'node_modules/@openai/codex/bin/codex.js' 'fixture; never launched'
    Write-Fixture 'node_modules/npm/package.json' '{"name":"npm","version":"11.0.0"}'
    Write-Fixture 'node_modules/npm/bin/npm-cli.js' 'fixture input; not npm'
    Write-Fixture 'scenario.json' '{"latest":"1.0.0","fault":null,"simulateRepair":true}'
    $launcher = Join-Path $prefix 'codex.ps1'
    $calls = Join-Path $prefix 'calls.jsonl'
    Check 'Location retains command and package evidence' (Invoke-Dert @('where','codex','--plain')) 0 @('DERT / where','codex.ps1','PACKAGE + COMMAND ROUTES','ADJACENT FOLDER')
    if (Test-Path -LiteralPath $calls) { throw 'Location started a process.' }
    $review = Invoke-Dert @('where','codex','-dr','--plain')
    Check 'Doctor alias reviews same-version reinstall' $review 0 @('REINSTALL PROPOSAL','--apply','1.0.0')
    $proposal = @(Get-ChildItem -LiteralPath (Join-Path $state 'doctor-proposals') -Filter '*.json')
    if ($proposal.Count -ne 1) { throw 'Expected exactly one saved review.' }
    $token = $proposal[0].BaseName
    $before = (Get-FileHash -LiteralPath $calls).Hash
    Check 'Wrong exact copy cannot consume an approval' (Invoke-Dert @('where','codex','--doctor','--path',(Join-Path $prefix 'other.cmd'),'--apply',$token,'--plain')) 1 @('different selected path')
    if ($before -ne (Get-FileHash -LiteralPath $calls).Hash) { throw 'Rejected approval queried a manager.' }
    Check 'Published doctor verifies simulated transaction and retains receipt' (Invoke-Dert @('where','codex','--doctor','--path',$launcher,'--apply',$token,'--plain')) 0 @('REINSTALL VERIFIED','Codex itself was not launched','Operation receipt')
    $receipt = @(Get-ChildItem -LiteralPath (Join-Path $state 'doctor-receipts') -Filter '*.jsonl')
    if ($receipt.Count -ne 1 -or -not ([IO.File]::ReadAllText($receipt[0].FullName)).Contains('"state":"Succeeded"')) { throw 'Missing verified transaction receipt.' }
    $source = Join-Path $proof 'where.DERT'
    [IO.File]::WriteAllText($source, "STERT ; where ;; codex`ncommands ; doctor ;;`nƒin.")
    $sourceHash = (Get-FileHash -LiteralPath $source).Hash
    Check 'UDL reaches source-bound doctor review' (Invoke-Dert @('run',$source,'--plain')) 0 @('DERT where codex --doctor','--from-source')
    Check 'MER retains its source beside the located and reviewed copy' (Invoke-Dert @('mer',$source,'--review','--plain') "at 2`nprobe`ndoctor`ndetails`n`n") 0 @('commands ; doctor ;;','awaiting choice','--from-source')
    if ($sourceHash -ne (Get-FileHash -LiteralPath $source).Hash) { throw 'Review changed source.' }
    Write-Fixture 'scenario.json' '{"latest":"1.0.0","fault":"root-cancelled","simulateRepair":true}'
    Check 'Cancelled manager query retains exit 130' (Invoke-Dert @('where','codex','--doctor','--plain')) 130 @('Doctor checks stopped')
    Check 'Packaged where example binds without findings' (Invoke-Dert @('run',(Join-Path (Split-Path $Executable) 'examples/where-my-tools.DERT'),'--preview','--plain')) 0 @('DERT where codex')
    Write-Host "$checks location/doctor executable checks passed. Evidence retained at $proof"
} finally {
    [void][IO.Directory]::CreateDirectory($proof)
    [IO.File]::WriteAllText((Join-Path $proof 'output.txt'), ($record -join "`n"))
}
