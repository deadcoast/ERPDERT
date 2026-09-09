[CmdletBinding()]
param([string] $Executable)

$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if (-not $Executable) { $Executable = Join-Path $projectPath '.artifacts/win-x64/DERT.exe' }
$Executable = (Resolve-Path -LiteralPath $Executable).Path
$proofPath = Join-Path $projectPath ('.artifacts/mer-proof/' + [Guid]::NewGuid().ToString('N'))
[void] [IO.Directory]::CreateDirectory($proofPath)
$record = [Collections.Generic.List[string]]::new()
$checks = 0

function Invoke-Mer([string[]] $CommandArgs, [string] $InputText = '', [string] $SearchPath = '') {
    $start = [Diagnostics.ProcessStartInfo]::new($Executable)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.RedirectStandardInput = $true
    $start.WorkingDirectory = $proofPath
    $start.Environment['PATH'] = $SearchPath
    foreach ($argument in $CommandArgs) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    try {
        $output = $process.StandardOutput.ReadToEndAsync()
        $errors = $process.StandardError.ReadToEndAsync()
        $process.StandardInput.Write($InputText)
        $process.StandardInput.Close()
        if (-not $process.WaitForExit(15000)) { $process.Kill($true); throw 'MER did not finish redirected output promptly.' }
        $result = [pscustomobject] @{ Exit = $process.ExitCode; Output = $output.GetAwaiter().GetResult(); Error = $errors.GetAwaiter().GetResult() }
        $record.Add("$($CommandArgs -join ' ')`nEXIT=$($result.Exit)`n$($result.Output)$($result.Error)")
        return $result
    }
    finally { $process.Dispose() }
}

function Assert-Mer([string] $Name, $Result, [int] $Exit, [string[]] $Expected) {
    if ($Result.Exit -ne $Exit -or $Result.Error) { throw "$Name failed: $($Result.Exit) $($Result.Output) $($Result.Error)" }
    foreach ($text in $Expected) { if (-not $Result.Output.Contains($text)) { throw "$Name omitted '$text': $($Result.Output)" } }
    if ($Result.Output.Contains([char]27)) { throw "$Name leaked terminal controls into redirected output." }
    $script:checks++
    Write-Host "PASS $Name"
}

function Invoke-PrintedPreview([string] $Output, [string] $Pattern = ' --from-lint [A-F0-9]{64} --plain') {
    $matches = [regex]::Matches($Output, '(?m)^& .+' + $Pattern + '\r?$')
    if ($matches.Count -ne 1) { throw "Expected one complete preview command.`n$Output" }
    $command = $matches[0].Value.TrimEnd("`r")
    $prefix = "& '" + $Executable.Replace("'", "''") + "' repair "
    if (-not $command.StartsWith($prefix, [StringComparison]::Ordinal) -or $command.Contains('--apply')) {
        throw 'Expected a preview from the tested executable.'
    }
    # Run only this build's printed command against this script's generated files.
    Push-Location -LiteralPath $proofPath
    try {
        $text = (& ([scriptblock]::Create($command)) 2>&1 | Out-String)
        $result = [pscustomobject] @{ Exit = $LASTEXITCODE; Output = $text; Error = '' }
        $record.Add("Copied preview: $command`nEXIT=$($result.Exit)`n$text")
        return $result
    }
    finally { Pop-Location }
}

try {
    $window = Invoke-Mer @('mer')
    Assert-Mer 'Bundled analysis opens its source window from another folder with no PATH dependencies' $window 0 @('.-- mer.DERT --.', 'Replay / original text', '1 | // A LITTLE MORE THAN NOTES', '6 marks short.', 'not applied', '--details')
    if ($window.Output.Contains('Read the source / checked') -or $window.Output.Contains('--from-lint')) { throw 'The default source window was followed by the full report.' }
    Assert-Mer 'Reduced motion retains the document frame and actual diff' (Invoke-Mer @('mer', '--still', '--plain')) 0 @('.-- mer.DERT --.', 'Line 2 / title rule', '+ // ------------------------')
    Assert-Mer 'Explicit detail keeps the complete recorded evidence' (Invoke-Mer @('mer', '--details')) 0 @('REPLAY', 'possible_conflicts', 'Read the source / checked', 'Line 2 / proposed', '--from-lint')
    Assert-Mer 'Recorded connections are named and navigable in scripted review' (Invoke-Mer @('mer', '--review', '--plain') "next`nnext`nprev`n`n") 0 @('next: line 4 / Codex discovery', 'Find Codex on this PC', 'Reasoning note / line 5')
    Assert-Mer 'Explicit local probe keeps an empty PATH result attached to its request' (Invoke-Mer @('mer', '--review', '--plain') "at 4`nprobe`ndetails`n`n") 0 @('No Codex launcher found', 'Local inspection recorded', 'source line 4', 'No programs started')
    Assert-Mer 'A note naming Codex cannot become a probe request' (Invoke-Mer @('mer', '--review', '--plain') "at 5`nprobe`n`n") 0 @('No discovery request at this line')
    Assert-Mer 'Conflicting detail and review options explain the choice' (Invoke-Mer @('mer', '--review', '--details', '--plain')) 2 @('Choose one')
    $prefixes = foreach ($copy in 1..2) {
        $prefix = Join-Path $proofPath "copy $copy"
        $package = Join-Path $prefix 'node_modules/@openai/codex'
        [void][IO.Directory]::CreateDirectory((Join-Path $package 'bin'))
        $npm = Join-Path $prefix 'node_modules/npm'
        [void][IO.Directory]::CreateDirectory((Join-Path $npm 'bin'))
        [IO.File]::WriteAllText((Join-Path $prefix 'codex.cmd'), '@"%_prog%" "%dp0%\node_modules\@openai\codex\bin\codex.js" %*')
        [IO.File]::WriteAllText((Join-Path $package 'package.json'), (@{ name = '@openai/codex'; version = "9.0.$copy"; bin = @{ codex = 'bin/codex.js' } } | ConvertTo-Json -Compress))
        [IO.File]::WriteAllText((Join-Path $package 'bin/codex.js'), 'throw new Error("must not run");')
        [IO.File]::WriteAllText((Join-Path $prefix 'node.exe'), 'fixture; not executable')
        [IO.File]::WriteAllText((Join-Path $prefix 'npm.cmd'), '@"%_prog%" "%dp0%\node_modules\npm\bin\npm-cli.js" %*')
        [IO.File]::WriteAllText((Join-Path $npm 'package.json'), '{"name":"npm","version":"11.0.0"}')
        [IO.File]::WriteAllText((Join-Path $npm 'bin/npm-cli.js'), 'throw new Error("must not run");')
        $prefix
    }
    Assert-Mer 'Local package copies can be compared inside the source review' (Invoke-Mer @('mer', '--review', '--plain') "at 4`nprobe`ncopy 2`ndetails`n`n" ($prefixes -join ';')) 0 @('Copy 2 of 2', '@openai/codex 9.0.2', (Join-Path $prefixes[1] 'node.exe'), 'No programs started')
    $note = Join-Path $proofPath "Ryan's [note].DERT"
    $repairRoot = Join-Path $proofPath 'repair-journeys'
    [void][IO.Directory]::CreateDirectory($repairRoot)
    $repairNote = Join-Path $repairRoot 'repair round trip.DERT'
    $repairSource = "// WEEKEND`r`n// --`nSTERT`r`n// music`nfin."
    [IO.File]::WriteAllText($repairNote, $repairSource, [Text.UTF8Encoding]::new($true))
    $repairOriginal = [Convert]::ToHexString([IO.File]::ReadAllBytes($repairNote))
    Assert-Mer 'In-window comparison leaves its source untouched' (Invoke-Mer @('mer', $repairNote, '--review', '--plain') "repair`nbefore`nafter`ncancel`n`n") 0 @('Proposed / not saved', 'Before edit / captured bytes', '2 | // -------', 'Preview left without applying')
    if ([Convert]::ToHexString([IO.File]::ReadAllBytes($repairNote)) -ne $repairOriginal) { throw 'A comparison wrote source bytes.' }
    Assert-Mer 'An explicit apply saves and verifies the displayed edit' (Invoke-Mer @('mer', $repairNote, '--review', '--plain') "repair`napply`ndetails`n`n") 0 @('Title aligned', 'Saved / bytes verified', 'Saved original /', 'This subsequent analysis makes no further edits')
    if ([IO.File]::ReadAllText($repairNote) -ne $repairSource.Replace('// --', '// -------')) { throw 'The saved edit did not match its proposal.' }
    Assert-Mer 'Undo compares and restores the exact original bytes' (Invoke-Mer @('mer', $repairNote, '--review', '--plain') "undo`nbefore`nafter`napply`n`n") 0 @('Restore the saved original', 'Restored / bytes verified')
    if ([Convert]::ToHexString([IO.File]::ReadAllBytes($repairNote)) -ne $repairOriginal) { throw 'Undo did not restore BOM, line endings and original text exactly.' }
    Assert-Mer 'Replay cannot apply a source edit' (Invoke-Mer @('mer', $repairNote, '--review', '--replay', '--plain') "repair`napply`n`n") 0 @('Replay is read-only')
    if ([Convert]::ToHexString([IO.File]::ReadAllBytes($repairNote)) -ne $repairOriginal) { throw 'Replay applied an edit.' }
    $boundaryNote = Join-Path $repairRoot 'boundary round trip.DERT'
    [IO.File]::WriteAllText($boundaryNote, "// plans ; weekend ;;`n// music,`n// food")
    Assert-Mer 'A note ending requires a choice and shows the changed ownership' (Invoke-Mer @('mer', $boundaryNote, '--review', '--plain') "repair`napply`nclose 3`ndetails`napply`n`n") 0 @('Choose where this note ends', 'No preview is ready to apply', '1 kept / 1 released', 'ordinary note', 'Note boundary closed')
    Assert-Mer 'Undo restores the unfinished note and its remaining finding' (Invoke-Mer @('mer', $boundaryNote, '--review', '--plain') "undo`napply`n`n") 1 @('Restored / bytes verified', 'needs attention')
    $source = "// WEEKEND`r`n// --`r`nSTERT`r`n// why ; music ;; café 日本語 👩‍💻 //`r`nƒin.`r`n"
    [IO.File]::WriteAllText($note, $source, [Text.UTF8Encoding]::new($true))
    $original = (Get-FileHash -LiteralPath $note).Hash
    $analyzed = Invoke-Mer @('mer', $note, '--details', '--plain')
    Assert-Mer 'User source carries Unicode context and its exact proposed rule' $analyzed 0 @('café 日本語 👩‍💻', '+ // -------', 'not applied')
    Assert-Mer 'Copied MER title action opens a source-bound preview from another folder' (Invoke-PrintedPreview $analyzed.Output) 0 @('FROM LINT', 'PREVIEW ONLY', 'no write prompt')
    if ((Get-FileHash -LiteralPath $note).Hash -ne $original) { throw 'MER changed its input.' }
    $checks++
    Write-Host 'PASS UTF-8 BOM and CRLF source remain byte-identical'
    $broken = Join-Path $proofPath 'unfinished.DERT'
    [IO.File]::WriteAllText($broken, "STERT`n// why ; music ;;`n// unfinished", [Text.UTF8Encoding]::new($false))
    $brokenHash = (Get-FileHash -LiteralPath $broken).Hash
    Assert-Mer 'Default unfinished source stays framed with its real problem' (Invoke-Mer @('mer', $broken, '--plain')) 1 @('.-- unfinished.DERT --.', 'needs attention', 'unfinished', 'standalone //')
    $unfinished = Invoke-Mer @('mer', $broken, '--replay', '--details', '--plain')
    Assert-Mer 'Unfinished notes remain visible with attention' $unfinished 1 @('ATTENTION', 'unfinished', 'missing')
    $choices = Invoke-PrintedPreview $unfinished.Output
    Assert-Mer 'Copied MER note action leaves the boundary choice open' $choices 0 @('FROM LINT', 'CHOICES ONLY', '--close-before eof')
    Assert-Mer 'Copied note choice retains the MER source revision until rehearsal' (Invoke-PrintedPreview $choices.Output ' --close-before eof --from-lint [A-F0-9]{64} --plain') 0 @('BOUNDARY REHEARSAL', 'PREVIEW ONLY', 'no write prompt')
    if ((Get-FileHash -LiteralPath $broken).Hash -ne $brokenHash -or (Test-Path -LiteralPath (Join-Path $proofPath '.erpdert'))) { throw 'MER handoff previews changed source or created recovery storage.' }
    [IO.File]::WriteAllText($broken, '// An entirely different note', [Text.UTF8Encoding]::new($false))
    Assert-Mer 'A copied MER action refuses changed source' (Invoke-PrintedPreview $unfinished.Output) 1 @('DERT4002')
    Assert-Mer 'Default missing source keeps a clear failure inside the window' (Invoke-Mer @('mer', (Join-Path $proofPath 'missing.DERT'), '--plain')) 2 @('Source unavailable', 'Could not finish', 'no file edits')
    Assert-Mer 'Missing file detail retains the observed read failure' (Invoke-Mer @('mer', (Join-Path $proofPath 'missing.DERT'), '--details', '--plain')) 2 @('Read the source / failed', 'FAILED', 'No file was changed')
    $bad = Join-Path $proofPath 'invalid-utf8.DERT'
    [IO.File]::WriteAllBytes($bad, [byte[]] @(255, 254, 255))
    Assert-Mer 'Invalid encoding does not become a clean result' (Invoke-Mer @('mer', $bad, '--plain')) 2 @('Could not finish', 'UTF-8')
    Assert-Mer 'Unrecognized options remain explained' (Invoke-Mer @('mer', '--whatever', '--plain')) 2 @('Use DERT mer', 'Reading never edits', 'explicit apply')
    Assert-Mer 'New engine is discoverable in executable help' (Invoke-Mer @('-help', '--plain')) 0 @('DERT mer', '--still')
    Assert-Mer 'Offline demo runs all cases without setup or input' (Invoke-Mer @('demo', '--all', '--plain')) 0 @('Title alignment', 'Context that belongs together', 'Unfinished is not broken', 'Words beyond ASCII', 'When a check cannot finish', 'not reviewed', 'No files')
    Assert-Mer 'Selectable title case rehearses the actual rule' (Invoke-Mer @('demo', '--case', 'title', '--plain')) 0 @('+ // --------------', 'Look for', 'not applied')
    Assert-Mer 'Selectable context case shows actual entry ownership' (Invoke-Mer @('demo', '--case', 'context', '--plain')) 0 @('belongs to line 4', 'Last train at 23:40')
    Assert-Mer 'Selectable unfinished case keeps its real finding' (Invoke-Mer @('demo', '--case', 'unfinished', '--plain')) 0 @('missing', 'ATTENTION')
    Assert-Mer 'Selectable Unicode case preserves the original words' (Invoke-Mer @('demo', '--case', 'unicode', '--plain')) 0 @('café', '日本語', '👩‍💻')
    $unicodeTitlePath = Join-Path $proofPath 'unicode title.DERT'
    [IO.File]::WriteAllText($unicodeTitlePath, "// café 日本語 👩‍💻`r`n// --`n// Keep my words.", [Text.UTF8Encoding]::new($true))
    $unicodeTitleHash = (Get-FileHash -LiteralPath $unicodeTitlePath).Hash
    Assert-Mer 'Unicode title uses the shared width in MER' (Invoke-Mer @('mer', $unicodeTitlePath, '--plain')) 0 @('café 日本語 👩‍💻', '12 marks', 'short')
    Assert-Mer 'Unicode title exact repair keeps joined emoji visible' (Invoke-Mer @('repair', $unicodeTitlePath, '--plain')) 0 @('café 日本語 👩‍💻', '// --------------', '14 title columns', 'PREVIEW ONLY')
    if ((Get-FileHash -LiteralPath $unicodeTitlePath).Hash -ne $unicodeTitleHash) { throw 'Unicode title review wrote source bytes.' }
    Assert-Mer 'Intentional decoding failure is a failed check not a failed demo' (Invoke-Mer @('demo', '--case', 'failure', '--plain')) 0 @('Read the source / failed', 'FAILED', 'not run')
    Assert-Mer 'Step backward and record a real personal judgment' (Invoke-Mer @('demo', '--review', '--step', '--plain') "`n`nback`nnext`nchange Too much text`nq`n") 0 @('Paused replay', 'title / needs work', 'Too much text', 'context / not reviewed')
    Assert-Mer 'End of input leaves unseen cases unreviewed' (Invoke-Mer @('demo', '--review', '--plain')) 0 @('title / not reviewed / not shown', 'Nothing saved or uploaded')
    Assert-Mer 'Contradictory demo options show the usable choices' (Invoke-Mer @('demo', '--all', '--case', 'title', '--plain')) 2 @('DERT demo', '--case')
    $heldTitle = Invoke-Mer @('demo', '--case', 'title', '--review', '--plain') "`nq`n"
    Assert-Mer 'Title review holds its actual measured preview' $heldTitle 0 @('Measured against your title', '+ // --------------', 'Preview only / not applied')
    if ($heldTitle.Output.Contains('Read the source / checked')) { throw 'The held title was buried beneath the full audit.' }
    Assert-Mer 'Held context retains its real owner and closure' (Invoke-Mer @('demo', '--case', 'context', '--review', '--plain') "`nq`n") 0 @('Belongs to plans', 'Closing // at line 8', 'Last train at 23:40')
    Assert-Mer 'Details opens the full recorded source on demand' (Invoke-Mer @('demo', '--case', 'title', '--review', '--plain') "`ndetails`nq`n") 0 @('Full recorded detail', 'Read the source / checked', 'Keep my words exactly as I wrote them.')
    $inspected = Invoke-Mer @('demo', '--case', 'context', '--review', '--plain') "`nat 5`nat 6`nq`n"
    Assert-Mer 'Recorded entries can be inspected without replaying the case' $inspected 0 @('Inspecting recorded line 5', 'Bring the playlist everyone keeps asking for.', 'Inspecting recorded line 6', 'Belongs to plans')
    if (($inspected.Output.Split('Look for /')).Count -ne 2) { throw 'Direct inspection replayed the case.' }
    Assert-Mer 'An invalid entry offers recovery without losing the review' (Invoke-Mer @('demo', '--case', 'context', '--review', '--plain') "`nat 999`nat 5`nq`n") 0 @('No recorded connection at that line', 'Inspecting recorded line 5', 'context / not reviewed')
    $continuous = Invoke-Mer @('demo', '--case', 'context', '--review', '--plain') "`nprev`nsource`nprev`nplain`nsource`nrepeat`nq`n"
    Assert-Mer 'Neighbor and source comparisons retain one analysis and selected entry' $continuous 0 @('Recorded source / line 6', '// food, // Book the corner table', 'Recorded source / line 5', '// music, // Bring the playlist', 'Inspecting recorded line 5', 'Replaying the same record', 'compared:')
    if (($continuous.Output.Split('Look for /')).Count -ne 2 -or ($continuous.Output.Split('context / not reviewed /')).Count -ne 2) { throw 'Review navigation or comparison created another analysis or observation.' }
    Assert-Mer 'Navigation stops at an endpoint without silently wrapping' (Invoke-Mer @('demo', '--case', 'context', '--review', '--plain') "`nat 5`nprev`nnext`nq`n") 0 @('Already at the first recorded entry', 'Inspecting recorded line 6')
    Assert-Mer 'Source inspection keeps an unfinished ending explicitly unresolved' (Invoke-Mer @('demo', '--case', 'unfinished', '--review', '--plain') "`nat 5`nsource`nnext`nq`n") 0 @('Recorded source / line 5', 'Recorded source / line 6', 'Closing // missing', 'unresolved')
    Assert-Mer 'Stepped replay can restart after skipping directly to review' (Invoke-Mer @('demo', '--case', 'context', '--review', '--step', '--plain') "`nnext`nrepeat`n`nnext`nq`n") 0 @('Replaying the same record', 'Paused replay 2/', 'context / not reviewed')
    $launcher = Join-Path (Split-Path -Parent $Executable) 'Try MER.cmd'
    $launch = [Diagnostics.ProcessStartInfo]::new((Join-Path $env:SystemRoot 'System32/cmd.exe'))
    $launch.UseShellExecute = $false
    $launch.CreateNoWindow = $true
    $launch.RedirectStandardOutput = $true
    $launch.RedirectStandardError = $true
    $launch.RedirectStandardInput = $true
    $launch.WorkingDirectory = $proofPath
    $launch.Environment['PATH'] = ''
    $launch.Arguments = '/d /c ""' + $launcher + '""'
    $launched = [Diagnostics.Process]::Start($launch)
    try {
        $launcherOutput = $launched.StandardOutput.ReadToEndAsync()
        $launcherError = $launched.StandardError.ReadToEndAsync()
        $launched.StandardInput.Close()
        if (-not $launched.WaitForExit(15000)) { $launched.Kill($true); throw 'The demo launcher did not finish with closed input.' }
        Assert-Mer 'Packaged click-to-try launcher works from another folder without PATH' ([pscustomobject] @{ Exit = $launched.ExitCode; Output = $launcherOutput.GetAwaiter().GetResult(); Error = $launcherError.GetAwaiter().GetResult() }) 0 @('MER / try it for yourself', 'Your review')
    }
    finally { $launched.Dispose() }
    $notesExample = Join-Path (Split-Path -Parent $Executable) 'examples/weekend.DERT'
    $notesOriginal = [IO.File]::ReadAllBytes($notesExample)
    Assert-Mer 'Packaged notes need no operational chain' (Invoke-Mer @('lint', $notesExample, '--plain')) 0 @('0 errors', '0 warnings', '0 notes')
    $notesReview = Invoke-Mer @('mer', $notesExample, '--review', '--plain') "find quiet`nhit 2`nowner`nstructure`ndetails`n`n"
    Assert-Mer 'Notes search follows explanations into their actual reasoning owner' $notesReview 0 @('2 matching lines', 'Hit 2 of 2 / line 14', 'Reasoning block / line 11', 'Target names as written', 'Closed by // on line 15')
    if ($notesReview.Output.Contains('ERP6004') -or $notesReview.Output.Contains('Operation needs attention')) { throw 'Notes-only inspection invented an operation problem.' }
    Assert-Mer 'Active sinkers retain their header and expanded source' (Invoke-Mer @('mer', $notesExample, '--review', '--plain') "at 8`nowner`nstructure`ndetails`n`n") 0 @('Statement / line 6', '; hook / overnight', '3 entries below', 'headphones')
    Assert-Mer 'Search uses literal text and leaves bad choices unchanged' (Invoke-Mer @('mer', $notesExample, '--review', '--plain') "find quiet`nhit 0`nfind [.*]`nstructure`n`n") 0 @('No search hit has that number', 'No literal matches', 'Sinker / line 8')
    if ([Convert]::ToHexString($notesOriginal) -ne [Convert]::ToHexString([IO.File]::ReadAllBytes($notesExample))) { throw 'Notes inspection changed the example.' }
    Write-Host "All $checks MER executable checks passed."
}
finally {
    [IO.File]::WriteAllText((Join-Path $proofPath 'output.txt'), ($record -join "`n"), [Text.UTF8Encoding]::new($false))
    Write-Host "MER evidence retained at $proofPath"
}
