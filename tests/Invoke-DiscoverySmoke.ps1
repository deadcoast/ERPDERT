[CmdletBinding()]
param([string] $Executable)

$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if (-not $Executable) { $Executable = Join-Path $projectPath '.artifacts\win-x64\ERPDERT.exe' }
$Executable = (Resolve-Path -LiteralPath $Executable).Path
$shellPath = (Get-Process -Id $PID).Path
$proofPath = Join-Path $projectPath ('.artifacts\discovery-proof\' + [Guid]::NewGuid().ToString('N'))
[void] (New-Item -ItemType Directory -Path $proofPath)
$record = [Collections.Generic.List[string]]::new()
$checks = 0

function Write-Fixture {
    param([string] $Relative, [string] $Content)
    $target = Join-Path $proofPath $Relative
    [void] [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
    [IO.File]::WriteAllText($target, $Content, [Text.UTF8Encoding]::new($false))
    return $target
}

function New-PackageFixture {
    param([string] $Name, [string] $Version)
    $null = Write-Fixture "$Name/codex.ps1" '& "$basedir/node$exe" "$basedir/node_modules/@openai/codex/bin/codex.js" $args'
    $null = Write-Fixture "$Name/codex.cmd" '@"%_prog%" "%dp0%\node_modules\@openai\codex\bin\codex.js" %*'
    $null = Write-Fixture "$Name/codex" 'exec node "$basedir/node_modules/@openai/codex/bin/codex.js" "$@"'
    $null = Write-Fixture "$Name/node_modules/@openai/codex/package.json" ('{"name":"@openai/codex","version":"' + $Version + '","bin":{"codex":"bin/codex.js"},"packageManager":"pnpm@10"}')
    $null = Write-Fixture "$Name/node_modules/@openai/codex/bin/codex.js" 'throw new Error("must not execute");'
    $null = Write-Fixture "$Name/node.exe" 'must not execute'
    $null = Write-Fixture "$Name/npm.cmd" '@"%_prog%" "%dp0%\node_modules\npm\bin\npm-cli.js" %*'
    $null = Write-Fixture "$Name/node_modules/npm/package.json" '{"name":"npm","version":"11.0.0"}'
    $null = Write-Fixture "$Name/node_modules/npm/bin/npm-cli.js" 'throw new Error("must not execute");'
    return Join-Path $proofPath $Name
}

function Invoke-Isolated {
    param([string] $Program, [string[]] $CommandArgs, [string] $SearchPath, [string] $Extensions = '.COM;.EXE;.BAT;.CMD')
    $start = [Diagnostics.ProcessStartInfo]::new($Program)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.StandardOutputEncoding = [Text.UTF8Encoding]::new($false)
    $start.WorkingDirectory = $proofPath
    $start.Environment['PATH'] = $SearchPath
    $start.Environment['PATHEXT'] = $Extensions
    foreach ($argument in $CommandArgs) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(20000)) {
            $process.Kill($true)
            throw 'Discovery smoke child exceeded 20 seconds.'
        }
        $result = [pscustomobject] @{ Exit = $process.ExitCode; Output = $stdout.GetAwaiter().GetResult(); Error = $stderr.GetAwaiter().GetResult() }
        $record.Add("$Program $($CommandArgs -join ' ')`nPATH=$SearchPath`nPATHEXT=$Extensions`nEXIT=$($result.Exit)`n$($result.Output)$($result.Error)")
        return $result
    }
    finally { $process.Dispose() }
}

function Assert-Discovery {
    param([string] $Name, [string] $SearchPath, [int] $ExpectedExit, [string[]] $ExpectedText, [string[]] $CommandArgs = @('codex', '--plain'))
    $result = Invoke-Isolated $Executable $CommandArgs $SearchPath
    if ($result.Exit -ne $ExpectedExit) { throw "$Name returned $($result.Exit), expected $ExpectedExit. $($result.Output) $($result.Error)" }
    if ($result.Error.Length -gt 0) { throw "$Name wrote stderr: $($result.Error)" }
    foreach ($fragment in $ExpectedText) {
        if (-not $result.Output.Contains($fragment)) { throw "$Name omitted '$fragment'. $($result.Output)" }
    }
    if ($result.Output.Contains([char]27)) { throw "$Name included terminal controls in --plain output." }
    $script:checks++
    Write-Host "PASS $Name"
}

function Assert-ShellParity {
    param([string] $Name, [string] $SearchPath, [string] $Extensions = '.COM;.EXE;.BAT;.CMD')
    $probe = '$PSModuleAutoLoadingPreference = ''None''; @(Microsoft.PowerShell.Core\Get-Command codex -All -CommandType Application,ExternalScript -ErrorAction SilentlyContinue).Path'
    $shellResult = Invoke-Isolated $shellPath @('-NoProfile', '-NonInteractive', '-Command', $probe) $SearchPath $Extensions
    $cliResult = Invoke-Isolated $Executable @('codex', '--plain') $SearchPath $Extensions
    $expected = @($shellResult.Output -split '\r?\n' | Where-Object { $_.Length -gt 0 })
    # The headline is intentionally an unambiguous path on its own logical line.
    # These proof paths stay below the redirected renderer's minimum width for
    # the first-line comparison by removing only display wrapping whitespace.
    $compactOutput = $cliResult.Output -replace '\s', ''
    if ($expected.Count -gt 0) {
        $headline = [regex]::Match($cliResult.Output, '(?s)First (?:PowerShell PATH match|observed PATH match[^\r\n]*)\r?\n(.*?)\r?\n(?:One|\d+) installation group')
        if (-not $headline.Success -or ($headline.Groups[1].Value -replace '\s', '') -ine ($expected[0] -replace '\s', '')) {
            throw "$Name disagrees with PowerShell's first external match: $($expected[0]). $($cliResult.Output)"
        }
    }
    foreach ($path in $expected) {
        if (-not $compactOutput.Contains(($path -replace '\s', ''))) { throw "$Name omitted PowerShell match $path." }
    }
    if (-not $cliResult.Output.Contains("$($expected.Count) command match")) { throw "$Name has a different match count from PowerShell." }
    $script:checks++
    Write-Host "PASS $Name"
}

try {
    $first = New-PackageFixture 'first' '1.2.3'
    $second = New-PackageFixture 'second' '2.0.0'
    $manualFile = Write-Fixture 'manual/codex.exe' 'not an executable'
    $manual = Split-Path -Parent $manualFile
    $marker = Join-Path $proofPath 'executed.txt'
    $trap = Write-Fixture 'trap/codex.ps1' ("[IO.File]::WriteAllText('" + $marker.Replace("'", "''") + "','bad')")
    $before = @{}
    foreach ($file in Get-ChildItem -LiteralPath $proofPath -Recurse -File) { $before[$file.FullName] = (Get-FileHash -LiteralPath $file.FullName).Hash }

    Assert-Discovery 'No match is not an uninstall verdict' '' 1 @('does not prove Codex is uninstalled', '0 command matches')
    Assert-Discovery 'One npm package, three launchers' $first 0 @('One installation group', '1.2.3', '3 launcher paths', 'original installer is not proven')
    Assert-Discovery 'Two packages retain precedence' "$first;$second" 1 @('2 installation groups', '1.2.3', '2.0.0')
    Assert-Discovery 'Unknown earlier file is not skipped' "$manual;$first" 1 @('ERP5103', '2 installation groups')
    Assert-Discovery 'No discovery command executes a launcher' (Split-Path -Parent $trap) 1 @('ERP5103', 'Nothing was executed')
    Assert-Discovery 'Update is not implicitly authorized' $first 2 @('read-only', 'update') @('codex', 'update', '--plain')
    Assert-ShellParity 'PowerShell package launcher order' $first
    Assert-ShellParity 'PowerShell folder precedence' "$manual;$first"
    $null = Write-Fixture 'manual/codex.cmd' '@echo do not execute'
    Assert-ShellParity 'PowerShell custom PATHEXT order' "$manual;$first" '.CMD;.EXE'
    Assert-ShellParity 'PowerShell quoted PATH semantics' ('"' + $first + '";' + $second)
    Assert-ShellParity 'PowerShell empty PATH' ''

    foreach ($path in $before.Keys) {
        if ((Get-FileHash -LiteralPath $path).Hash -ne $before[$path]) { throw "Discovery modified fixture $path." }
    }
    if (Test-Path -LiteralPath $marker) { throw 'A discovered launcher was executed.' }
    $script:checks++
    Write-Host 'PASS All inspected files remain unchanged; execution trap untouched'

    $entrypoint = Join-Path $first 'node_modules/@openai/codex/bin/codex.js'
    # Remove only this explicitly created fixture to simulate a broken package.
    Remove-Item -LiteralPath $entrypoint
    Assert-Discovery 'Missing entrypoint is explained' $first 1 @('ERP5105', 'codex.js')
    $metadata = Join-Path $first 'node_modules/@openai/codex/package.json'
    [IO.File]::WriteAllText($metadata, '{"name":"@openai/codex","version":"\uD800","bin":{"codex":"bin/codex.js"}}')
    Assert-Discovery 'Damaged Unicode metadata does not abort later discovery' "$first;$second" 1 @('ERP5104', 'incomplete Unicode', '2.0.0')
    Write-Host "$checks executable discovery checks passed. Evidence retained at $proofPath"
}
catch {
    $record.Add($_.ToString())
    throw
}
finally {
    [IO.File]::WriteAllText((Join-Path $proofPath 'output.txt'), ($record -join "`n"), [Text.UTF8Encoding]::new($false))
}
