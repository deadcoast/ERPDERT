[CmdletBinding()]
param(
    [string] $Executable = (Join-Path $PSScriptRoot '..\.artifacts\win-x64\ERPDERT.exe')
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$appPath = (Resolve-Path -LiteralPath $Executable).Path
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$corpusPath = Join-Path $PSScriptRoot 'Fixtures\mixed-structures.DERT'
$corpusHash = (Get-FileHash -LiteralPath $corpusPath).Hash
$proofPath = Join-Path $projectPath ('.artifacts\proof\' + [Guid]::NewGuid().ToString('N'))
[void] (New-Item -ItemType Directory -Path $proofPath)

$sample = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Fixtures\contextual-notes.DERT'))
$samplePath = Join-Path $proofPath 'contextual notes.DERT'
$damagedPath = Join-Path $proofPath 'missing closer.DERT'
$helpFilePath = Join-Path $proofPath 'help'
$sampleBytes = [Text.UTF8Encoding]::new($false).GetBytes($sample)
[IO.File]::WriteAllBytes($samplePath, $sampleBytes)
$closerPattern = [regex]::new('(?m)^//\r?\n')
$damaged = $closerPattern.Replace($sample, '', 1)
if ($damaged -eq $sample) { throw 'No standalone closer was found to exercise recovery.' }
[IO.File]::WriteAllText($damagedPath, $damaged, [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText($helpFilePath, $damaged, [Text.UTF8Encoding]::new($false))
$sampleHash = (Get-FileHash -LiteralPath $samplePath).Hash
$damagedHash = (Get-FileHash -LiteralPath $damagedPath).Hash
$helpFileHash = (Get-FileHash -LiteralPath $helpFilePath).Hash

function Assert-Command {
    param([string] $Label, [int] $ExpectedExit, [string[]] $Arguments, [string[]] $Patterns)
    $output = (& $appPath @Arguments 2>&1 | Out-String)
    $actualExit = $LASTEXITCODE
    if ($actualExit -ne $ExpectedExit) {
        throw "$Label returned $actualExit, expected $ExpectedExit.`n$output"
    }
    foreach ($pattern in $Patterns) {
        if ($output -notmatch $pattern) { throw "$Label did not show $pattern.`n$output" }
    }
    Write-Host "$Label : exit $actualExit, expected output verified"
    return $output
}

$helpOutput = Assert-Command -Label 'Help' -ExpectedExit 0 -Arguments @('-help') -Patterns @('inspect', 'lint')
$contextOutput = Assert-Command -Label 'Folded reasoning notes' -ExpectedExit 0 `
    -Arguments @('inspect', $samplePath, '--plain') -Patterns @('support doctoring', '3 entries', '0 errors')
$fixmeIndex = [Array]::FindIndex([string[]] ($sample -split '\r?\n'), [Predicate[string]] { param($line) $line.StartsWith('// fixme,') })
if ($fixmeIndex -lt 0) { throw 'The fixture fixme entry was not found.' }
$focusedOutput = Assert-Command -Label 'Entry ownership' -ExpectedExit 0 `
    -Arguments @('inspect', $samplePath, '--at', [string] ($fixmeIndex + 1), '--plain') `
    -Patterns @('Value\s+fixme', 'Context\s+the confirmation command to fix the conflict', 'Belongs to\s+support doctoring')
$damagedOutput = Assert-Command -Label 'Missing closer recovery' -ExpectedExit 1 `
    -Arguments @('lint', $damagedPath, '--plain') -Patterns @('DERT1001', 'standalone //')
$corpusOutput = Assert-Command -Label 'Mixed structures lint' -ExpectedExit 0 `
    -Arguments @('lint', $corpusPath, '--plain') -Patterns @('DERT2003', 'DERT3003', '0 errors')
$missingOutput = Assert-Command -Label 'Missing file' -ExpectedExit 2 `
    -Arguments @('inspect', (Join-Path $proofPath 'does not exist.DERT'), '--plain') -Patterns @('could not')
Push-Location -LiteralPath $proofPath
try {
    $helpFileOutput = Assert-Command -Label 'File literally named help' -ExpectedExit 1 `
        -Arguments @('lint', 'help', '--plain') -Patterns @('DERT1001', 'standalone //')
}
finally { Pop-Location }

if ((Get-FileHash -LiteralPath $corpusPath).Hash -cne $corpusHash -or
    (Get-FileHash -LiteralPath $samplePath).Hash -cne $sampleHash -or
    (Get-FileHash -LiteralPath $damagedPath).Hash -cne $damagedHash -or
    (Get-FileHash -LiteralPath $helpFilePath).Hash -cne $helpFileHash) {
    throw 'An inspected fixture changed during this check.'
}

$record = @($helpOutput, $contextOutput, $focusedOutput, $damagedOutput, $corpusOutput, $missingOutput, $helpFileOutput) -join "`n"
[IO.File]::WriteAllText((Join-Path $proofPath 'terminal-output.txt'), $record, [Text.UTF8Encoding]::new($false))
Write-Host 'Source preservation : inspected test inputs unchanged'
Write-Host "Evidence retained at $proofPath"
