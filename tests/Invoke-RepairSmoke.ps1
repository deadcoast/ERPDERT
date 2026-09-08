[CmdletBinding()]
param(
    [string] $Executable = (Join-Path $PSScriptRoot '..\.artifacts\win-x64\ERPDERT.exe')
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$appPath = (Resolve-Path -LiteralPath $Executable).Path
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$proofPath = Join-Path $projectPath ('.artifacts\repair-proof\' + [Guid]::NewGuid().ToString('N'))
[void] (New-Item -ItemType Directory -Path $proofPath)
$encoding = [Text.UTF8Encoding]::new($false)
$record = [Collections.Generic.List[string]]::new()
$script:checks = 0

function Invoke-Check {
    param([string] $Label, [int] $ExpectedExit, [scriptblock] $Action, [string[]] $Patterns)
    $output = (& $Action 2>&1 | Out-String)
    $actualExit = $LASTEXITCODE
    $record.Add($Label + "`n" + $output)
    if ($actualExit -ne $ExpectedExit) { throw "$Label returned $actualExit, expected $ExpectedExit.`n$output" }
    foreach ($pattern in $Patterns) {
        if ($output -notmatch $pattern) { throw "$Label did not show $pattern.`n$output" }
    }
    $script:checks++
    Write-Host "$Label : exit $actualExit, expected output verified"
    return $output
}

function Get-ApprovalCommand {
    param([string] $Output)
    $match = [regex]::Match($Output, '(?m)^& .+ --apply [A-F0-9]{64}\r?$')
    if (-not $match.Success) { throw "No complete, executable approval command was printed.`n$Output" }
    $command = $match.Value.TrimEnd("`r")
    # Only evaluate commands produced by our own executable for these generated fixtures.
    $expectedPrefix = "& '" + $appPath.Replace("'", "''") + "' "
    if (-not $command.StartsWith($expectedPrefix, [StringComparison]::Ordinal)) {
        throw 'The copy command did not target the executable actually being tested.'
    }
    $parseErrors = $null
    [void] [Management.Automation.Language.Parser]::ParseInput($command, [ref] $null, [ref] $parseErrors)
    if ($parseErrors.Count -ne 0) { throw "The printed command is not valid PowerShell: $parseErrors" }
    return [scriptblock]::Create($command)
}

try {
    $fixture = Join-Path $proofPath 'reversible structures.DERT'
    [IO.File]::WriteAllBytes($fixture, [IO.File]::ReadAllBytes((Join-Path $PSScriptRoot 'Fixtures\mixed-structures.DERT')))
    $originalHash = (Get-FileHash -LiteralPath $fixture).Hash
    $preview = Invoke-Check 'Mixed structures repair rehearsal' 0 { & $appPath repair $fixture --plain } @('DERT2003', 'PREVIEW ONLY', '\+\s+2')
    if ((Get-FileHash -LiteralPath $fixture).Hash -cne $originalHash -or (Test-Path -LiteralPath (Join-Path $proofPath '.erpdert'))) {
        throw 'Preview changed the source or created recovery storage.'
    }
    $approve = Get-ApprovalCommand $preview
    $null = Invoke-Check 'Copied repair command' 0 $approve @('TITLE ALIGNED', 'before.DERT', 'DERT3003', 'still present')
    $alignedHash = (Get-FileHash -LiteralPath $fixture).Hash
    if ($alignedHash -ceq $originalHash) { throw 'Repair did not change the rule.' }
    $null = Invoke-Check 'Already aligned' 0 { & $appPath repair $fixture --plain } @('UNCHANGED', 'already align')
    $undoPreview = Invoke-Check 'Undo rehearsal' 0 { & $appPath undo $fixture --plain } @('UNDO REHEARSAL', 'PREVIEW ONLY')
    if ((Get-FileHash -LiteralPath $fixture).Hash -cne $alignedHash) { throw 'Undo preview changed the file.' }
    $undoApprove = Get-ApprovalCommand $undoPreview
    $null = Invoke-Check 'Copied undo command' 0 $undoApprove @('ORIGINAL RESTORED')
    if ((Get-FileHash -LiteralPath $fixture).Hash -cne $originalHash) { throw 'Undo did not restore the exact original bytes.' }
    $null = Invoke-Check 'Already restored' 0 { & $appPath undo $fixture --plain } @('UNCHANGED', 'already restored')

    $preview = Invoke-Check 'Fresh rehearsal before concurrent edit' 0 { & $appPath repair $fixture --plain } @('PREVIEW ONLY')
    $staleApprove = Get-ApprovalCommand $preview
    [IO.File]::AppendAllText($fixture, "`r`n// words written after preview", $encoding)
    $editedHash = (Get-FileHash -LiteralPath $fixture).Hash
    $null = Invoke-Check 'Stale approval refusal' 1 $staleApprove @('DERT4002', 'nothing was written')
    if ((Get-FileHash -LiteralPath $fixture).Hash -cne $editedHash) { throw 'Stale approval erased an edit.' }

    # Exercise the actual emitted PowerShell expression: smart apostrophe, literal
    # ASCII apostrophe, a format character, dollar and backtick all remain path data.
    $specialName = 'Ryan' + [char]0x2019 + 's ' + "'" + 'note' + [char]0x200B + '$` .DERT'
    $special = Join-Path $proofPath $specialName
    [IO.File]::WriteAllText($special, ([string][char]0xFEFF) + '// MY FIRST TITLE', $encoding)
    $specialHash = (Get-FileHash -LiteralPath $special).Hash
    $specialPreview = Invoke-Check 'Quoted filename and EOF rehearsal' 0 { & $appPath repair $special --plain } @('CRLF', '\[char\]0x2019', '\[char\]0x200B')
    $specialApprove = Get-ApprovalCommand $specialPreview
    $null = Invoke-Check 'Quoted filename copied application' 0 $specialApprove @('TITLE ALIGNED')
    $specialUndoPreview = Invoke-Check 'Quoted filename undo rehearsal' 0 { & $appPath undo $special --plain } @('UNDO REHEARSAL')
    $specialUndo = Get-ApprovalCommand $specialUndoPreview
    $null = Invoke-Check 'Quoted filename copied undo' 0 $specialUndo @('ORIGINAL RESTORED')
    if ((Get-FileHash -LiteralPath $special).Hash -cne $specialHash) { throw 'Quoted filename/BOM/EOF reversal was not exact.' }

    $interactive = Join-Path $proofPath 'interactive title.DERT'
    [IO.File]::WriteAllText($interactive, "// MY FIRST TITLE`r`n// ----`r`n`r`n// These are still my words.", $encoding)
    Write-Host "$script:checks executable checks passed. Interactive fixture: $interactive"
}
finally {
    [IO.File]::WriteAllText((Join-Path $proofPath 'terminal-output.txt'), ($record -join "`n"), $encoding)
    Write-Host "Evidence retained at $proofPath"
}
