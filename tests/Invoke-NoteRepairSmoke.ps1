[CmdletBinding()]
param([string] $Executable = (Join-Path $PSScriptRoot '..\.artifacts\win-x64\DERT.exe'))

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$appPath = (Resolve-Path -LiteralPath $Executable).Path
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$proofPath = Join-Path $projectPath ('.artifacts\note-proof\' + [Guid]::NewGuid().ToString('N'))
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

function Get-PrintedCommand {
    param([string] $Output, [string] $Ending)
    $matches = [regex]::Matches($Output, '(?m)^& .+ ' + $Ending + '\r?$')
    if ($matches.Count -ne 1) { throw "Expected one complete command ending in $Ending.`n$Output" }
    $command = $matches[0].Value.TrimEnd("`r")
    $expectedPrefix = "& '" + $appPath.Replace("'", "''") + "' "
    if (-not $command.StartsWith($expectedPrefix, [StringComparison]::Ordinal)) {
        throw 'Printed command did not name the executable being tested.'
    }
    $parseErrors = $null
    [void] [Management.Automation.Language.Parser]::ParseInput($command, [ref] $null, [ref] $parseErrors)
    if ($parseErrors.Count) { throw "Printed command was not valid PowerShell: $parseErrors" }
    # Evaluate only commands our executable produced for this script's owned fixtures.
    return [scriptblock]::Create($command)
}

try {
    $source = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Fixtures\contextual-notes.DERT'))
    $text = [regex]::Replace($source, '(?m)^//\r?\n', '')
    $lines = [regex]::Split($text, '\r\n|\n|\r')
    $opener = [Array]::IndexOf($lines, '// support doctoring ; conflicts ;;') + 1
    $stop = [Array]::IndexOf($lines, 'commands ; updateme ;;') + 1
    if ($opener -lt 1 -or $stop -lt 1) { throw 'Required fixture boundaries were not found.' }
    $fixture = Join-Path $proofPath 'contextual notes without closer.DERT'
    [IO.File]::WriteAllText($fixture, $text, $encoding)
    $originalHash = (Get-FileHash -LiteralPath $fixture).Hash
    $choices = Invoke-Check 'Reasoning boundary choices' 0 { & $appPath repair $fixture --at $opener --plain } @('recovered grouping', '3 kept', 'CHOICES ONLY')
    if ($choices -match '--apply' -or (Test-Path -LiteralPath (Join-Path $proofPath '.erpdert'))) {
        throw 'Unselected choice report offered approval or wrote recovery storage.'
    }
    $selection = Get-PrintedCommand $choices "--close-before $stop"
    $preview = Invoke-Check 'Copied boundary selection' 0 $selection @('BOUNDARY REHEARSAL', '3 kept', '0 released', 'PREVIEW ONLY')
    if ((Get-FileHash -LiteralPath $fixture).Hash -cne $originalHash) { throw 'Selection changed source.' }
    $approve = Get-PrintedCommand $preview '--apply [A-F0-9]{64}'
    $null = Invoke-Check 'Copied note approval' 0 $approve @('NOTE BOUNDARY CLOSED', 'before.DERT')
    $appliedHash = (Get-FileHash -LiteralPath $fixture).Hash
    if ($appliedHash -ceq $originalHash) { throw 'Approval did not insert the marker.' }
    $undoPreview = Invoke-Check 'Note undo rehearsal' 0 { & $appPath undo $fixture --plain } @('original unfinished grouping', 'UNDO REHEARSAL')
    if ((Get-FileHash -LiteralPath $fixture).Hash -cne $appliedHash) { throw 'Undo preview wrote source.' }
    $undo = Get-PrintedCommand $undoPreview '--apply [A-F0-9]{64}'
    $null = Invoke-Check 'Copied note undo' 0 $undo @('ORIGINAL RESTORED', 'DERT1001')
    if ((Get-FileHash -LiteralPath $fixture).Hash -cne $originalHash) { throw 'Original reversal was not byte exact.' }
    $null = Invoke-Check 'Repeated undo leaves restored bytes' 0 { & $appPath undo $fixture --plain } @('UNCHANGED', 'already restored')

    $overlap = Join-Path $proofPath 'ownership overlap.DERT'
    $overlapText = "commands ; list ;;`n// outer ; notes ;;`n// inner ; notes ;;`n// item`nvalue"
    [IO.File]::WriteAllText($overlap, $overlapText, $encoding)
    $scope = Invoke-Check 'Released structured ownership' 0 { & $appPath repair $overlap --at 2 --close-before 3 --plain } @('reasoning block', 'reasoning entry', 'inner ; notes', 'result line 4', 'DERT1001')
    $wrongCut = Get-PrintedCommand $scope '--apply [A-F0-9]{64}'
    [IO.File]::AppendAllText($overlap, "`n// later words", $encoding)
    $laterHash = (Get-FileHash -LiteralPath $overlap).Hash
    $null = Invoke-Check 'Changed source cancels boundary approval' 1 $wrongCut @('DERT4002', 'nothing was written')
    if ((Get-FileHash -LiteralPath $overlap).Hash -cne $laterHash) { throw 'Stale approval overwrote later words.' }
    $null = Invoke-Check 'Unoffered boundary refused' 1 { & $appPath repair $overlap --at 2 --close-before 1 --plain } @('DERT4005', 'No\s+boundary was guessed')

    $specialName = 'Ryan' + [char]0x2019 + 's ' + "'" + '[red]note' + [char]0x200B + '$` .DERT'
    $special = Join-Path $proofPath $specialName
    [IO.File]::WriteAllText($special, ([string][char]0xFEFF) + '// café ; notes ;;', $encoding)
    $specialHash = (Get-FileHash -LiteralPath $special).Hash
    $eof = Invoke-Check 'Quoted Unicode EOF boundary' 0 { & $appPath repair $special --at 1 --close-before eof --plain } @('Windows CRLF', '\[char\]0x2019', '\[char\]0x200B')
    $eofApprove = Get-PrintedCommand $eof '--apply [A-F0-9]{64}'
    $null = Invoke-Check 'Quoted copied note approval' 0 $eofApprove @('NOTE BOUNDARY CLOSED')
    $eofUndoPreview = Invoke-Check 'Quoted note undo rehearsal' 0 { & $appPath undo $special --plain } @('UNDO REHEARSAL')
    $eofUndo = Get-PrintedCommand $eofUndoPreview '--apply [A-F0-9]{64}'
    $null = Invoke-Check 'Quoted copied note undo' 0 $eofUndo @('ORIGINAL RESTORED')
    if ((Get-FileHash -LiteralPath $special).Hash -cne $specialHash) { throw 'Unicode/BOM/EOF reversal was not byte exact.' }

    $interactive = Join-Path $proofPath 'interactive boundary.DERT'
    [IO.File]::WriteAllText($interactive, "// support doctoring ; conflicts ;;`r`n// possible_conflicts, // something known to block it`r`n// fixme, // choose the supported repair`r`ncommands ; updateme ;;", $encoding)
    Write-Host "$script:checks executable checks passed. Interactive fixture: $interactive"
}
finally {
    [IO.File]::WriteAllText((Join-Path $proofPath 'terminal-output.txt'), ($record -join "`n"), $encoding)
    Write-Host "Evidence retained at $proofPath"
}
