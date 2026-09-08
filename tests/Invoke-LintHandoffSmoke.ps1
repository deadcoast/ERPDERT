[CmdletBinding()]
param([string] $Executable = (Join-Path $PSScriptRoot '..\.artifacts\win-x64\ERPDERT.exe'))

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$appPath = (Resolve-Path -LiteralPath $Executable).Path
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$proofPath = Join-Path $projectPath ('.artifacts\lint-proof\' + [Guid]::NewGuid().ToString('N'))
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
    if ($output.Contains([string][char]0x1B)) { throw "$Label emitted color/control escapes despite plain mode." }
    $script:checks++
    Write-Host "$Label : exit $actualExit, expected output verified"
    return $output
}

function Get-PrintedCommand {
    param([string] $Output, [string] $Pattern)
    $matches = [regex]::Matches($Output, '(?m)^& .+' + $Pattern + '\r?$')
    if ($matches.Count -ne 1) { throw "Expected one complete command matching $Pattern.`n$Output" }
    $command = $matches[0].Value.TrimEnd("`r")
    $expectedPrefix = "& '" + $appPath.Replace("'", "''") + "' "
    if (-not $command.StartsWith($expectedPrefix, [StringComparison]::Ordinal)) {
        throw 'Printed command did not name the executable being tested.'
    }
    $parseErrors = $null
    [void] [Management.Automation.Language.Parser]::ParseInput($command, [ref] $null, [ref] $parseErrors)
    if ($parseErrors.Count) { throw "Printed command was not valid PowerShell: $parseErrors" }
    # Only evaluate our own executable's output for fixtures this script created.
    return [scriptblock]::Create($command)
}

function Assert-Unchanged {
    param([string] $Path, [string] $Hash)
    if ((Get-FileHash -LiteralPath $Path).Hash -cne $Hash) { throw "A preview changed $Path" }
    if (Test-Path -LiteralPath (Join-Path $proofPath '.erpdert')) { throw 'Preview created recovery storage.' }
}

try {
    $corpus = Join-Path $proofPath 'mixed structures copy.DERT'
    [IO.File]::WriteAllBytes($corpus, [IO.File]::ReadAllBytes((Join-Path $PSScriptRoot 'Fixtures\mixed-structures.DERT')))
    $corpusHash = (Get-FileHash -LiteralPath $corpus).Hash
    $relative = [IO.Path]::GetRelativePath((Get-Location).Path, $corpus)
    $lint = Invoke-Check 'Mixed supported and unsupported findings' 0 { & $appPath lint $relative --plain } @('DERT2003', 'REHEARSE / TITLE RULE', 'DERT3003', 'NO REPAIR OFFERED', '--from-lint')
    if ($lint -match '--apply') { throw 'Lint offered approval without an edit preview.' }
    $titleHandoff = Get-PrintedCommand $lint ' --from-lint [A-F0-9]{64} --plain'
    Push-Location -LiteralPath $proofPath
    try {
        $null = Invoke-Check 'Copied title link from another directory' 0 $titleHandoff @('FROM LINT', 'DERT2003', 'PREVIEW ONLY')
    }
    finally { Pop-Location }
    Assert-Unchanged $corpus $corpusHash

    $source = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Fixtures\contextual-notes.DERT'))
    $text = [regex]::new('(?m)^//\r?\n').Replace($source, '', 1)
    $note = Join-Path $proofPath 'contextual notes missing closer.DERT'
    [IO.File]::WriteAllText($note, $text, $encoding)
    $noteHash = (Get-FileHash -LiteralPath $note).Hash
    $lines = [regex]::Split($text, '\r\n|\n|\r')
    $stop = [Array]::IndexOf($lines, 'commands ; updateme ;;') + 1
    if ($stop -lt 1) { throw 'Fixture command boundary was not found.' }
    $noteLint = Invoke-Check 'Missing-closer lint link' 1 { & $appPath lint $note --plain } @('CHOOSE / NOTE BOUNDARY', '--at', '--from-lint')
    $noteHandoff = Get-PrintedCommand $noteLint ' --from-lint [A-F0-9]{64} --plain'
    $choices = Invoke-Check 'Copied note handoff' 0 $noteHandoff @('FROM LINT', 'CHOICES ONLY', 'support doctoring')
    if ($choices -match '--apply') { throw 'An unselected boundary offered approval.' }
    $choice = Get-PrintedCommand $choices " --close-before $stop --from-lint [A-F0-9]{64} --plain"
    $selected = Invoke-Check 'Copied bound note selection' 0 $choice @('FROM LINT', 'BOUNDARY REHEARSAL', 'PREVIEW ONLY', '3 kept')
    Assert-Unchanged $note $noteHash

    $unsupported = Join-Path $proofPath 'unsupported width.DERT'
    [IO.File]::WriteAllText($unsupported, '// café', $encoding)
    $unsupportedLint = Invoke-Check 'Unsupported title does not offer a dead-end repair' 0 { & $appPath lint $unsupported --plain } @('DERT2003', 'NO REPAIR OFFERED', 'width')
    if ($unsupportedLint -match '--from-lint|(?m)^& ') { throw 'Unsupported title got a repair command.' }

    $special = Join-Path $proofPath ('Ryan' + [char]0x2019 + 's ' + "'" + '[red]note' + [char]0x200B + '$` .DERT')
    [IO.File]::WriteAllText($special, ([string][char]0xFEFF) + '// [red]TITLE[/]', $encoding)
    $specialHash = (Get-FileHash -LiteralPath $special).Hash
    $specialLint = Invoke-Check 'Literal-safe filename and BOM lint link' 0 { & $appPath lint $special --plain } @('\[char\]0x2019', '\[char\]0x200B', '\[red\]TITLE\[/\]')
    $specialHandoff = Get-PrintedCommand $specialLint ' --from-lint [A-F0-9]{64} --plain'
    $null = Invoke-Check 'Copied literal-safe handoff' 0 $specialHandoff @('FROM LINT', 'PREVIEW ONLY', '\[red\]TITLE\[/\]')
    Assert-Unchanged $special $specialHash

    [IO.File]::WriteAllText($note, '// a different note now occupies the file', $encoding)
    $changedHash = (Get-FileHash -LiteralPath $note).Hash
    $null = Invoke-Check 'Old lint link refuses shifted source' 1 $noteHandoff @('DERT4002', 'changed since lint')
    $null = Invoke-Check 'Old copied selection refuses shifted source' 1 $choice @('DERT4002', 'changed since lint')
    Assert-Unchanged $note $changedHash

    # Follow the separate approval from the already checked special-title preview.
    $titlePreview = Invoke-Check 'Fresh title handoff for explicit approval' 0 $specialHandoff @('FROM LINT', 'PREVIEW ONLY')
    $approve = Get-PrintedCommand $titlePreview ' --plain --apply [A-F0-9]{64}'
    $null = Invoke-Check 'Separate copied approval applies the exact title edit' 0 $approve @('TITLE ALIGNED', 'before.DERT')
    $undoPreview = Invoke-Check 'Repaired title undo preview' 0 { & $appPath undo $special --plain } @('UNDO REHEARSAL')
    $undo = Get-PrintedCommand $undoPreview ' --apply [A-F0-9]{64}'
    $null = Invoke-Check 'Copied undo restores linted original' 0 $undo @('ORIGINAL RESTORED')
    if ((Get-FileHash -LiteralPath $special).Hash -cne $specialHash) { throw 'Post-handoff undo was not byte exact.' }
    Write-Host "$script:checks executable checks passed. Interactive fixture: $corpus"
}
finally {
    [IO.File]::WriteAllText((Join-Path $proofPath 'terminal-output.txt'), ($record -join "`n"), $encoding)
    Write-Host "Evidence retained at $proofPath"
}
