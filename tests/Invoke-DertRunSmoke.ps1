[CmdletBinding()]
param([string] $Executable)

$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if (-not $Executable) { $Executable = Join-Path $projectPath '.artifacts/win-x64/ERPDERT.exe' }
$Executable = (Resolve-Path -LiteralPath $Executable).Path
$shellPath = (Get-Process -Id $PID).Path
$proofPath = Join-Path $projectPath ('.artifacts\dert-run-proof\' + [Guid]::NewGuid().ToString('N'))
[void] (New-Item -ItemType Directory -Path $proofPath)
$record = [Collections.Generic.List[string]]::new()
$checks = 0

function Write-Fixture {
    param([string] $Relative, [string] $Content)
    $path = Join-Path $proofPath $Relative
    [void] [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path))
    [IO.File]::WriteAllText($path, $Content, [Text.UTF8Encoding]::new($false))
    return $path
}

function Invoke-Child {
    param([string] $Program, [string[]] $CommandArgs)
    $start = [Diagnostics.ProcessStartInfo]::new($Program)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.StandardOutputEncoding = [Text.UTF8Encoding]::new($false)
    $start.WorkingDirectory = $proofPath
    $start.Environment['PATH'] = Join-Path $proofPath 'node'
    $start.Environment['PATHEXT'] = '.EXE;.CMD'
    foreach ($argument in $CommandArgs) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(20000)) {
            $process.Kill($true)
            throw 'DERT run child exceeded 20 seconds.'
        }
        $result = [pscustomobject] @{ Exit = $process.ExitCode; Output = $stdout.GetAwaiter().GetResult(); Error = $stderr.GetAwaiter().GetResult() }
        $record.Add("$Program $($CommandArgs -join ' ')`nEXIT=$($result.Exit)`n$($result.Output)$($result.Error)")
        return $result
    }
    finally { $process.Dispose() }
}

function Assert-Result {
    param([string] $Name, $Result, [int] $ExpectedExit, [string[]] $Expected, [string[]] $Absent = @())
    if ($Result.Exit -ne $ExpectedExit) { throw "$Name returned $($Result.Exit), expected $ExpectedExit. $($Result.Output) $($Result.Error)" }
    if ($Result.Error) { throw "$Name wrote stderr. $($Result.Error)" }
    foreach ($text in $Expected) {
        if (-not $Result.Output.Contains($text)) { throw "$Name omitted '$text'. $($Result.Output)" }
    }
    foreach ($text in $Absent) {
        if ($Result.Output.Contains($text)) { throw "$Name unexpectedly included '$text'. $($Result.Output)" }
    }
    if ($Result.Output.Contains([char]27)) { throw "$Name included ANSI controls in plain output." }
    $script:checks++
    Write-Host "PASS $Name"
}

try {
    $null = Write-Fixture 'node/codex.ps1' '& "$basedir/node$exe" "$basedir/node_modules/@openai/codex/bin/codex.js" $args'
    $null = Write-Fixture 'node/codex.cmd' '@"%_prog%" "%dp0%\node_modules\@openai\codex\bin\codex.js" %*'
    $null = Write-Fixture 'node/node.exe' 'must not execute'
    $null = Write-Fixture 'node/node_modules/@openai/codex/package.json' '{"name":"@openai/codex","version":"8.7.6","bin":{"codex":"bin/codex.js"}}'
    $null = Write-Fixture 'node/node_modules/@openai/codex/bin/codex.js' 'throw new Error("must not execute");'
    $sample = Join-Path (Split-Path -Parent $Executable) 'examples/my-tools.DERT'
    $sampleHash = (Get-FileHash -LiteralPath $sample).Hash
    $result = Invoke-Child $Executable @('run', $sample, '--plain', '--preview')
    Assert-Result 'Packaged example interprets without discovery' $result 0 @('DERT / PREVIEW', 'support doctoring', 'No discovery was performed') @('CODEX / DISCOVERY', '8.7.6')

    $result = Invoke-Child $Executable @('run', $sample, '--plain')
    Assert-Result 'Packaged example drives the real discovery adapter' $result 0 @('DERT / RUN', 'CODEX / DISCOVERY', '8.7.6', '1 operation completed', '0 results need attention')
    $direct = Invoke-Child $Executable @('codex', '--plain')
    Assert-Result 'Direct CLI sees the same fixture package' $direct 0 @('8.7.6', 'One installation group')

    $path = Write-Fixture "Ryan’s '[red]tools.DERT" ([char]0xfeff + "STERT ; ERPDERT ;; codex`r`n// preserved context`r`n")
    $sourceHash = (Get-FileHash -LiteralPath $path).Hash
    $preview = Invoke-Child $Executable @('run', $path, '--preview', '--plain')
    Assert-Result 'BOM and quoted filename preview' $preview 0 @('preserved context', 'No discovery was performed')
    $commands = @($preview.Output -split '\r?\n' | Where-Object { $_.StartsWith('& ') -and $_.Contains(' run ') })
    if ($commands.Count -ne 1) { throw 'The preview did not provide one intact PowerShell command.' }
    if (-not $commands[0].EndsWith('--plain')) { throw 'The copied run command lost plain mode.' }
    $copied = Invoke-Child $shellPath @('-NoProfile', '-NonInteractive', '-Command', $commands[0])
    Assert-Result 'Copied preview command works from another directory' $copied 0 @('CODEX / DISCOVERY', '8.7.6', '1 operation completed')

    $two = Write-Fixture 'two.DERT' "STERT ; ERPDERT ;; codex`nfin.`nSTERT ; ERPDERT ;; codex"
    $result = Invoke-Child $Executable @('run', $two, '--plain')
    Assert-Result 'Two source chains have two attributed results' $result 0 @('STEP 01 / RESULT', 'STEP 02 / RESULT', '2 operations completed')

    $unsupported = Write-Fixture 'unsupported.DERT' "STERT ; ERPDERT ;; codex`nƒin.`nSTERT ; ERPDERT ;; claude"
    $result = Invoke-Child $Executable @('run', $unsupported, '--plain')
    Assert-Result 'Unsupported later operation prevents a partial run' $result 1 @('ERP6004', 'claude', 'No operations ran') @('CODEX / DISCOVERY', '8.7.6')

    $notes = Write-Fixture 'notes.DERT' '// STERT ; ERPDERT ;; codex //'
    $result = Invoke-Child $Executable @('run', $notes, '--plain')
    Assert-Result 'An operation example inside a note remains a note' $result 0 @('No operations to run') @('CODEX / DISCOVERY')

    $warning = Write-Fixture 'warning.DERT' "// TITLE WITHOUT A RULE`nSTERT ; ERPDERT ;; codex"
    $result = Invoke-Child $Executable @('run', $warning, '--plain')
    Assert-Result 'Formatting warning does not prevent discovery' $result 0 @('DERT2003', 'CODEX / DISCOVERY', '1 operation completed')

    $unfinished = Write-Fixture 'unfinished.DERT' "STERT ; ERPDERT ;; codex`n// support ; conflicts ;;`n// one entry"
    $result = Invoke-Child $Executable @('run', $unfinished, '--plain')
    Assert-Result 'Unfinished structure is cited before dispatch' $result 1 @('DERT1001', 'No operations ran') @('CODEX / DISCOVERY')

    $result = Invoke-Child $Executable @('run', $sample, '--apply', '--plain')
    Assert-Result 'Run does not accept a repair approval flag' $result 2 @('--apply') @('CODEX / DISCOVERY')

    if ((Get-FileHash -LiteralPath $sample).Hash -ne $sampleHash -or (Get-FileHash -LiteralPath $path).Hash -ne $sourceHash) {
        throw 'Running or previewing DERT changed a source file.'
    }
    if (@(Get-ChildItem -LiteralPath $proofPath -Force -Directory | Where-Object Name -Like '*recovery*').Count -ne 0) {
        throw 'Read-only run unexpectedly produced recovery files.'
    }
    $script:checks++
    Write-Host 'PASS Source bytes preserved without recovery artifacts'
    Write-Host "$checks DERT-to-operation executable checks passed. Evidence retained at $proofPath"
}
catch {
    $record.Add($_.ToString())
    throw
}
finally {
    [IO.File]::WriteAllText((Join-Path $proofPath 'output.txt'), ($record -join "`n"), [Text.UTF8Encoding]::new($false))
}
