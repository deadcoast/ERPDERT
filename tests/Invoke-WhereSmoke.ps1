[CmdletBinding()]
param([string] $Executable)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$workspace = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if (-not $Executable) { $Executable = Join-Path $workspace '.artifacts/win-x64/DERT.exe' }
$Executable = (Resolve-Path -LiteralPath $Executable).Path
$proof = Join-Path $workspace ('.artifacts/where-proof/' + [guid]::NewGuid().ToString('N'))
$prefix = Join-Path $proof "Ryan's tools"
$searchPath = $prefix
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
    $start.Environment['PATH'] = $script:searchPath
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
    $second = Join-Path $proof 'later copy'
    [void][IO.Directory]::CreateDirectory($second)
    foreach ($file in Get-ChildItem -LiteralPath $fixtureBuild -File) { Copy-Item -LiteralPath $file.FullName -Destination $second }
    Copy-Item -LiteralPath (Join-Path $prefix 'node_modules') -Destination (Join-Path $second 'node_modules') -Recurse
    Copy-Item -LiteralPath (Join-Path $prefix 'npm.cmd') -Destination (Join-Path $second 'npm.cmd')
    $generatedShim = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Fixtures/NpmDoctorConflict/codex.ps1'))
    Write-Fixture 'codex.ps1' $generatedShim
    [IO.File]::WriteAllText((Join-Path $second 'codex.ps1'), $generatedShim)
    Write-Fixture 'scenario.json' '{"latest":"1.0.0","fault":null,"simulateRepair":true}'
    [IO.File]::WriteAllText((Join-Path $second 'scenario.json'), '{"latest":"1.0.0","fault":null,"simulateRepair":true}')
    $script:searchPath = $prefix + ';' + $second
    $laterBefore = @(Get-ChildItem -LiteralPath $second -Recurse -File | ForEach-Object { $_.FullName + ':' + (Get-FileHash -LiteralPath $_.FullName).Hash })
    $intact = Invoke-Dert @('where','codex','--doctor','--plain')
    Check 'Two intact copies remain possible overlap without a confirmed failure' $intact 1 @('POSSIBLE CONFLICT / evidence only')
    if ($intact.Output.Contains('CONFLICT CONFIRMED / external PATH')) { throw 'Duplicate installations were mislabeled as a confirmed conflict.' }
    $missing = [IO.Path]::GetFullPath((Join-Path $prefix 'node_modules/@openai/codex/bin/codex.js'))
    if (-not $missing.StartsWith([IO.Path]::GetFullPath($proof) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Broken entrypoint fixture escaped its workspace.' }
    Remove-Item -LiteralPath $missing
    $beforeConflict = (Get-FileHash -LiteralPath $calls).Hash
    Write-Fixture 'codex.ps1' ($generatedShim + "`nWrite-Output 'custom wrapper behavior'`n")
    $custom = Invoke-Dert @('where','codex','--doctor','--plain')
    Check 'Custom target-bearing wrapper remains incomplete evidence' $custom 1 @('CONFLICT CHECK INCOMPLETE')
    if ($custom.Output.Contains('CONFLICT CONFIRMED / external PATH')) { throw 'A custom wrapper was assumed to be an npm-generated route.' }
    Write-Fixture 'codex.ps1' $generatedShim
    $script:searchPath = '"' + $prefix + '";' + $second
    $quoted = Invoke-Dert @('where','codex','--doctor','--plain')
    Check 'Unsupported quoted PATH preserves an incomplete conflict check' $quoted 1 @('CONFLICT CHECK INCOMPLETE')
    if ($quoted.Output.Contains('CONFLICT CONFIRMED / external PATH')) { throw 'An unsupported PATH shape earned a confirmed diagnosis.' }
    $script:searchPath = $prefix + ';' + $second
    $conflict = Invoke-Dert @('where','codex','--doctor','--plain')
    Check 'Broken earlier generated route receives concrete conflict evidence before selection' $conflict 1 @('CONFLICT CONFIRMED / external PATH','First external route','Missing entry point','Later identified copy','FIXME / review the broken first copy')
    if ($beforeConflict -ne (Get-FileHash -LiteralPath $calls).Hash) { throw 'Conflict diagnosis started npm before copy selection.' }
    $conflictSource = Join-Path $proof "author's conflicts.DERT"
    [IO.File]::WriteAllText($conflictSource, "STERT ; where ;; codex`n// support doctoring ; conflicts ;;`n// possible_conflicts, // Several copies deserve a closer look.`n// conflicts, // Show the missing file before calling it broken.`n// fixme // Keep this reason beside the correction I choose.`n//`ncommands ; doctor ;;`nƒin.")
    $conflictSourceHash = (Get-FileHash -LiteralPath $conflictSource).Hash
    Check 'Authored conflict notes retain their words beside the source-bound correction' (Invoke-Dert @('run',$conflictSource,'--plain')) 1 @('CONFLICT CONFIRMED / external PATH','Keep this reason','--from-source')
    $selected = Invoke-Dert @('where','codex','--doctor','--path',$launcher,'--from-source',$conflictSource,'--plain')
    Check 'Selecting the broken first copy retains its conflict in an exact proposal' $selected 0 @('CONFLICT CONFIRMED / external PATH','REINSTALL PROPOSAL','--from-source','--apply')
    $approval = [regex]::Match($selected.Output,'--apply\s+''?([A-Fa-f0-9]{64})''?')
    if (-not $approval.Success) { throw 'The conflict proposal did not expose an exact approval token.' }
    $beforeStale = (Get-FileHash -LiteralPath $calls).Hash
    $script:searchPath = $second + ';' + $prefix
    Check 'Reordered PATH refuses the old conflict approval before npm starts' (Invoke-Dert @('where','codex','--doctor','--path',$launcher,'--from-source',$conflictSource,'--apply',$approval.Groups[1].Value,'--plain')) 1 @('changed')
    if ($beforeStale -ne (Get-FileHash -LiteralPath $calls).Hash) { throw 'Stale conflict approval started npm.' }
    $script:searchPath = $prefix + ';' + $second
    Check 'Approved earlier-copy repair verifies its external-route condition' (Invoke-Dert @('where','codex','--doctor','--path',$launcher,'--from-source',$conflictSource,'--apply',$approval.Groups[1].Value,'--plain')) 0 @('REINSTALL VERIFIED','EXTERNAL ROUTE / REPAIR VERIFIED','Operation receipt')
    $repeated = Invoke-Dert @('where','codex','--doctor','--plain')
    Check 'Repeated diagnosis drops confirmed conflict after repair' $repeated 1 @('POSSIBLE CONFLICT / evidence only')
    if ($repeated.Output.Contains('CONFLICT CONFIRMED / external PATH')) { throw 'A completed repair retained a stale confirmed conflict.' }
    $laterAfter = @(Get-ChildItem -LiteralPath $second -Recurse -File | ForEach-Object { $_.FullName + ':' + (Get-FileHash -LiteralPath $_.FullName).Hash })
    if (Compare-Object $laterBefore $laterAfter) { throw 'Repair changed or queried the unselected later copy.' }
    if ($conflictSourceHash -ne (Get-FileHash -LiteralPath $conflictSource).Hash) { throw 'Conflict review or repair changed authored source.' }
    $checks++; Write-Host 'PASS Other installation and authored source preserved'
    Write-Host "$checks location/doctor executable checks passed. Evidence retained at $proof"
} finally {
    [void][IO.Directory]::CreateDirectory($proof)
    [IO.File]::WriteAllText((Join-Path $proof 'output.txt'), ($record -join "`n"))
}
