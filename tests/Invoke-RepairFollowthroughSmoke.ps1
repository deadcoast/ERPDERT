[CmdletBinding()]
param([string] $Executable = (Join-Path $PSScriptRoot '..\.artifacts\win-x64\ERPDERT.exe'))

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$appPath = (Resolve-Path -LiteralPath $Executable).Path
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$proofPath = Join-Path $projectPath ('.artifacts\followthrough-proof\' + [Guid]::NewGuid().ToString('N'))
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
    if ($output.Contains([string][char]0x1B)) { throw "$Label emitted control escapes despite plain mode." }
    $script:checks++
    Write-Host "$Label : exit $actualExit, expected output verified"
    return $output
}

function Get-PrintedCommand {
    param([string] $Output, [string] $Ending)
    $matches = [regex]::Matches($Output, '(?m)^& .+' + $Ending + '\r?$')
    if ($matches.Count -ne 1) { throw "Expected one complete command ending in $Ending.`n$Output" }
    $command = $matches[0].Value.TrimEnd("`r")
    $prefix = "& '" + $appPath.Replace("'", "''") + "' "
    if (-not $command.StartsWith($prefix, [StringComparison]::Ordinal)) { throw 'Command targets another executable.' }
    $parseErrors = $null
    [void] [Management.Automation.Language.Parser]::ParseInput($command, [ref] $null, [ref] $parseErrors)
    if ($parseErrors.Count) { throw "Printed command is not valid PowerShell: $parseErrors" }
    # Evaluate only this executable's commands for test files created below.
    return [scriptblock]::Create($command)
}

function Get-VerifiedReport {
    param([string] $Output)
    $start = $Output.IndexOf('VERIFIED RESULT', [StringComparison]::Ordinal)
    if ($start -lt 0) { throw "No verified result was printed.`n$Output" }
    return $Output.Substring($start)
}

try {
    $name = 'Ryan' + [char]0x2019 + 's ' + "'" + '[red]notes' + [char]0x200B + '$` .DERT'
    $file = Join-Path $proofPath $name
    $source = ([string][char]0xFEFF) + "// TITLE`r`nSTERT`r`n// context ; notes ;;`r`n// entry`r`nƒin."
    [IO.File]::WriteAllText($file, $source, $encoding)
    $sourceHash = (Get-FileHash -LiteralPath $file).Hash
    $preview = Invoke-Check 'Initial source-bound title rehearsal' 0 { & $appPath repair $file --from-lint $sourceHash --plain } @('PREVIEW ONLY')
    $approve = Get-PrintedCommand $preview ' --plain --apply [A-F0-9]{64}'
    $completed = Invoke-Check 'Applied title offers the shifted note' 0 $approve @('TITLE ALIGNED', 'VERIFIED RESULT', '1 finding still present', 'CHOOSE / NOTE BOUNDARY')
    $alignedHash = (Get-FileHash -LiteralPath $file).Hash
    $expected = $source.Replace("// TITLE`r`n", "// TITLE`r`n// -----`r`n")
    if ([Convert]::ToHexString([IO.File]::ReadAllBytes($file)) -cne [Convert]::ToHexString($encoding.GetBytes($expected))) {
        throw 'Title repair did not preserve the BOM and following bytes.'
    }
    $verified = Get-VerifiedReport $completed
    if ($verified -match 'DERT2003|--apply') { throw 'Result offered a resolved finding or inherited approval.' }
    $next = Get-PrintedCommand $verified " --at 4 --from-lint $alignedHash --plain"
    $receiptsBefore = @(Get-ChildItem -LiteralPath (Join-Path $proofPath '.erpdert') -Recurse -Filter receipt.json).Count
    Push-Location -LiteralPath $proofPath
    try { $choices = Invoke-Check 'Copied result action from another directory' 0 $next @('FROM LINT', 'CHOICES ONLY', 'context') }
    finally { Pop-Location }
    if ((Get-FileHash -LiteralPath $file).Hash -cne $alignedHash -or
        @(Get-ChildItem -LiteralPath (Join-Path $proofPath '.erpdert') -Recurse -Filter receipt.json).Count -ne $receiptsBefore) {
        throw 'Following the result action changed source or recovery storage.'
    }
    $choice = Get-PrintedCommand $choices " --close-before 6 --from-lint $alignedHash --plain"
    $selected = Invoke-Check 'Copied boundary selection still only rehearses' 0 $choice @('BOUNDARY REHEARSAL', 'PREVIEW ONLY')
    $noteApproval = Get-PrintedCommand $selected ' --plain --apply [A-F0-9]{64}'
    $clean = Invoke-Check 'Separate note approval completes supported checks' 0 $noteApproval @('NOTE BOUNDARY CLOSED', 'No remaining findings in the supported structural checks', 'Command meanings were not validated')
    if ((Get-VerifiedReport $clean) -match '--from-lint') { throw 'Clean result advertised another repair.' }
    $closedHash = (Get-FileHash -LiteralPath $file).Hash
    $null = Invoke-Check 'Earlier result link refuses subsequently repaired bytes' 1 $next @('DERT4002', 'changed since lint')
    if ((Get-FileHash -LiteralPath $file).Hash -cne $closedHash) { throw 'Stale link changed the file.' }
    $undoPreview = Invoke-Check 'Undo the note without touching the title' 0 { & $appPath undo $file --plain } @('UNDO REHEARSAL')
    $undo = Get-PrintedCommand $undoPreview ' --apply [A-F0-9]{64}'
    $restored = Invoke-Check 'Undo reports findings in the restored source' 0 $undo @('ORIGINAL RESTORED', 'in the restored original', 'CHOOSE / NOTE BOUNDARY')
    if ((Get-FileHash -LiteralPath $file).Hash -cne $alignedHash) { throw 'Undo did not restore the exact title-repaired bytes.' }
    $restoredLink = Get-PrintedCommand (Get-VerifiedReport $restored) " --at 4 --from-lint $alignedHash --plain"
    $null = Invoke-Check 'Restored-source result link works' 0 $restoredLink @('FROM LINT', 'CHOICES ONLY')

    $released = Join-Path $proofPath 'released block.DERT'
    [IO.File]::WriteAllText($released, "commands ; list ;;`n// outer ; notes ;;`n// inner ; notes ;;`n// item`nvalue", $encoding)
    $revision = (Get-FileHash -LiteralPath $released).Hash
    $cut = Invoke-Check 'Rehearse a cut that releases structured content' 0 { & $appPath repair $released --at 2 --close-before 3 --from-lint $revision --plain } @('BOUNDARY REHEARSAL', 'reasoning block')
    $cutApproval = Get-PrintedCommand $cut ' --plain --apply [A-F0-9]{64}'
    $cutResult = Invoke-Check 'Verified cut links the newly exposed block' 0 $cutApproval @('NOTE BOUNDARY CLOSED', 'VERIFIED RESULT')
    $cutHash = (Get-FileHash -LiteralPath $released).Hash
    $inner = Get-PrintedCommand (Get-VerifiedReport $cutResult) " --at 4 --from-lint $cutHash --plain"
    $null = Invoke-Check 'Copied released-block action selects inner, not outer' 0 $inner @('inner', 'CHOICES ONLY')
    if ((Get-FileHash -LiteralPath $released).Hash -cne $cutHash) { throw 'Released-block preview wrote source.' }
    Write-Host "$script:checks executable checks passed. Repaired-note fixture: $file"
}
finally {
    [IO.File]::WriteAllText((Join-Path $proofPath 'terminal-output.txt'), ($record -join "`n"), $encoding)
    Write-Host "Evidence retained at $proofPath"
}
