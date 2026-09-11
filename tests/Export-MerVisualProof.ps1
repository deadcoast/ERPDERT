[CmdletBinding()]
param(
    [string] $BuildFolder,
    [int] $Columns = 110,
    [int] $Rows = 28,
    [ValidateSet('title', 'context', 'unfinished', 'unicode', 'failure')]
    [string] $DemoCase,
    [ValidateRange(1, 2147483647)]
    [Nullable[int]] $AtLine,
    [switch] $SourceView,
    [switch] $DocumentWindow,
    [switch] $StructureView,
    [switch] $ReferencesView,
    [switch] $ContextView,
    [string] $ResultRecord,
    [int[]] $IncludeNotes = @(),
    [Nullable[int]] $ReferenceNumber,
    [string] $DocumentPath,
    [switch] $ProbeLocal,
    [string] $InspectionPath,
    [ValidateSet('title', 'note')]
    [string] $RepairJourney,
    [string] $UpdatePrefix,
    [string] $LocationPrefix,
    [string] $LocationSearchPath,
    [switch] $DiagnoseOnly,
    [switch] $NotesJourney,
    [ValidateRange(1, 2147483647)]
    [int] $Copy = 1
)

$ErrorActionPreference = 'Stop'
if ($SourceView -and $null -eq $AtLine) { throw '-SourceView requires -AtLine.' }
if ($ResultRecord -and (-not $DocumentWindow -or $ContextView -or $ReferencesView -or $StructureView -or $ProbeLocal -or $DemoCase)) { throw '-ResultRecord requires DocumentWindow and no other inspection view.' }
if ($StructureView -and (-not $DocumentWindow -or $null -eq $AtLine)) { throw '-StructureView requires -DocumentWindow and -AtLine.' }
if ($ContextView -and (-not $DocumentWindow -or $null -eq $AtLine -or $StructureView -or $ReferencesView -or $ProbeLocal)) { throw '-ContextView requires DocumentWindow, AtLine and no other inspection view.' }
if ($IncludeNotes.Count -gt 0 -and -not $ContextView) { throw '-IncludeNotes requires ContextView.' }
if ($ReferencesView -and (-not $DocumentWindow -or $null -eq $AtLine -or $StructureView)) { throw '-ReferencesView requires -DocumentWindow, -AtLine and no StructureView.' }
if ($null -ne $ReferenceNumber -and (-not $ReferencesView -or $ReferenceNumber -lt 1)) { throw '-ReferenceNumber requires ReferencesView and a positive destination number.' }
if ($DocumentPath -and $DemoCase) { throw 'Choose DocumentPath or DemoCase for this captured source.' }
if ($ProbeLocal -and (-not $DocumentWindow -or $null -eq $AtLine)) { throw '-ProbeLocal requires -DocumentWindow and -AtLine.' }
if ($UpdatePrefix) { $UpdatePrefix = (Resolve-Path -LiteralPath $UpdatePrefix).Path }
if ($LocationPrefix) { $LocationPrefix = (Resolve-Path -LiteralPath $LocationPrefix).Path }
if (($LocationSearchPath -or $DiagnoseOnly) -and -not $LocationPrefix) { throw 'LocationSearchPath and DiagnoseOnly require LocationPrefix.' }
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if (-not $BuildFolder) { $BuildFolder = Join-Path $projectPath 'src/Dert.Cli/bin/Debug/net10.0' }
$BuildFolder = (Resolve-Path -LiteralPath $BuildFolder).Path
$proofPath = Join-Path $projectPath ('.artifacts/mer-visual/' + [Guid]::NewGuid().ToString('N'))
[void] [IO.Directory]::CreateDirectory($proofPath)
foreach ($name in @('Dert.Core', 'Dert.Mer', 'Spectre.Console', 'DERT')) {
    [void] [Reflection.Assembly]::LoadFrom((Join-Path $BuildFolder ($name + '.dll')))
}
Add-Type -AssemblyName System.Drawing

# Invoke the actual application adapter; the input is a shipped product example,
# not a prose specification. This harness renders engine frames, not OS screenshots.
$sourcePath = if ($DocumentPath) { (Resolve-Path -LiteralPath $DocumentPath).Path } else { Join-Path $BuildFolder 'examples/mer.DERT' }
$adapter = [Dert.Cli.CliApplication].Assembly.GetType('Dert.Cli.Motion.NoteScore')
$method = $adapter.GetMethod('Analyze', [Reflection.BindingFlags]'Static,NonPublic')
$read = [Func[string]] { [IO.File]::ReadAllText($sourcePath) }
$label = [IO.Path]::GetFileName($sourcePath)
if ($DemoCase) {
    $catalog = [Dert.Cli.CliApplication].Assembly.GetType('Dert.Cli.Motion.DemoCatalog')
    $cases = $catalog.GetProperty('Cases', [Reflection.BindingFlags]'Static,NonPublic').GetValue($null)
    $selected = $cases | Where-Object Id -EQ $DemoCase
    $read = $selected.Read
    $label = "demo/$DemoCase.DERT"
}
$sourceState = [Dert.Mer.DocumentSourceState]::Original
$resultPlan = $null
if ($ResultRecord) {
    $resultObservation = [Dert.Cli.Results.ObservationStore]::Read((Resolve-Path -LiteralPath $ResultRecord).Path)
    $resultPlan = [Dert.Core.ResultNotes]::Plan([Dert.Core.DertParser]::Parse($read.Invoke()), $resultObservation, $sourcePath, $AtLine)
    if (-not $resultPlan.IsReady) { throw $resultPlan.Explanation }
    $read = [Func[string]] { $resultPlan.ProposedText }
    $sourceState = [Dert.Mer.DocumentSourceState]::Proposed
    $resultText = [Dert.Core.SourceText]::new($resultPlan.ProposedText)
    $AtLine = ($resultText.Lines | Where-Object { $_.Start -le $resultPlan.AddedSpan.Start -and $_.End -gt $resultPlan.AddedSpan.Start } | Select-Object -First 1).Number
}
$analysis = $method.Invoke($null, @($read, $label, $true, $null, [Threading.CancellationToken]::None, $sourceState))
$score = $analysis.Score
if ($DemoCase) { $score = [Dert.Mer.MotionScore]::new($selected.Title, $score.Scope, $score.Stages, $score.Source, $true) }
if ($DocumentWindow) { $score.View = [Dert.Mer.MotionView]::Document }
$journal = [Dert.Mer.MotionJournal]::new($score, [DateTimeOffset]::Now)
$writer = [IO.StringWriter]::new()
$size = [Func[Dert.Mer.TerminalSize]] { [Dert.Mer.TerminalSize]::new($Columns, $Rows) }
$terminal = [Dert.Mer.InlineTerminal]::new($writer, $size, [Dert.Mer.MerColorMode]::TrueColor)
$record = [Collections.Generic.List[string]]::new()
$record.Add((@{ version = 2; width = $Columns; height = $Rows; title = 'MER / recorded local analysis'; timestamp = [DateTimeOffset]::Now.ToUnixTimeSeconds() } | ConvertTo-Json -Compress))
$colors = @{
    Text = '#E9E7DF'; Muted = '#8C97A3'; Copper = '#E6A57E'
    Signal = '#76C9C1'; Good = '#A5C685'; Bad = '#EF8F82'
}

function Export-Frame($Frame, [string] $Path) {
    $bitmap = [Drawing.Bitmap]::new(($Columns * 11 + 40), ($Frame.Lines.Count * 25 + 40))
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $regular = [Drawing.Font]::new('Consolas', 18, [Drawing.FontStyle]::Regular, [Drawing.GraphicsUnit]::Pixel)
    $bold = [Drawing.Font]::new('Consolas', 18, [Drawing.FontStyle]::Bold, [Drawing.GraphicsUnit]::Pixel)
    $format = [Drawing.StringFormat]::GenericTypographic.Clone()
    try {
        $graphics.Clear([Drawing.ColorTranslator]::FromHtml('#141A1E'))
        $graphics.TextRenderingHint = [Drawing.Text.TextRenderingHint]::AntiAliasGridFit
        $row = 0
        foreach ($line in $Frame.Lines) {
            $column = 0
            foreach ($run in $line.Runs) {
                $brush = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($colors[$run.Ink.ToString()]))
                try {
                    $elements = [Globalization.StringInfo]::GetTextElementEnumerator($run.Text)
                    while ($elements.MoveNext()) {
                        $glyph = $elements.GetTextElement()
                        $font = if ($run.Bold) { $bold } else { $regular }
                        $graphics.DrawString($glyph, $font, $brush, [Drawing.PointF]::new((20 + $column * 11), (20 + $row * 25)), $format)
                        $column += [Dert.Mer.TerminalText]::Width($glyph)
                    }
                }
                finally { $brush.Dispose() }
            }
            $row++
        }
        $bitmap.Save($Path, [Drawing.Imaging.ImageFormat]::Png)
    }
    finally { $format.Dispose(); $regular.Dispose(); $bold.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
}

$elapsed = 0.0
$index = 0
$offset = 0
try {
    foreach ($event in $analysis.Events) {
        $journal.Apply($event, [TimeSpan]::FromSeconds($elapsed))
        if ($event -is [Dert.Mer.FinishMotion]) { break }
        $frame = [Dert.Mer.MerCompositor]::Live($journal.Snapshot([TimeSpan]::FromSeconds($elapsed)), $Columns, $Rows, $false)
        $terminal.Draw($frame)
        if ($event -is [Dert.Mer.AddEvidence] -or $event -is [Dert.Mer.SettleStage]) {
            Export-Frame $frame (Join-Path $proofPath ('{0:D2}-{1}.png' -f $index++, $event.Id))
        }
        $newOutput = $writer.ToString().Substring($offset)
        $offset = $writer.ToString().Length
        $record.Add((@($elapsed, 'o', $newOutput) | ConvertTo-Json -Compress))
        foreach ($tick in 1..12) {
            $elapsed += 0.04
            $terminal.Draw([Dert.Mer.MerCompositor]::Live($journal.Snapshot([TimeSpan]::FromSeconds($elapsed)), $Columns, $Rows, $false))
            $newOutput = $writer.ToString().Substring($offset)
            $offset = $writer.ToString().Length
            if ($newOutput.Length) { $record.Add((@($elapsed, 'o', $newOutput) | ConvertTo-Json -Compress)) }
        }
    }
    $complete = $journal.Snapshot([TimeSpan]::FromSeconds($elapsed))
    if ($RepairJourney -or $UpdatePrefix -or $LocationPrefix -or $NotesJourney) {
        # Exercise the actual review adapter and guarded writer on a disposable,
        # harness-owned file. No user document or bundled example is edited.
        $repairPath = Join-Path $proofPath $(if ($NotesJourney) { 'weekend.DERT' } elseif ($LocationPrefix) { 'where-my-tools.DERT' } elseif ($UpdatePrefix) { 'my-tools.DERT' } else { 'repair-notes.DERT' })
        $repairText = if ($NotesJourney) { [IO.File]::ReadAllText((Join-Path $BuildFolder 'examples/weekend.DERT')) }
            elseif ($LocationPrefix) { "// WHERE MY TOOLS LIVE`n// ------------------`n`nSTERT ; where ;; codex`n// why ; codex ;; Check the exact copy. //`ncommands ; doctor ;;`nƒin." }
            elseif ($UpdatePrefix) { "// KEEP MY TOOLS CURRENT`n// ---------------------`n`nSTERT ; update ;; codex`n// why ; codex ;; Check the exact copy. //`ncommands ; updateme ;;`nfin." }
            elseif ($RepairJourney -eq 'title') { "// A LITTLE MORE THAN NOTES`r`n// ------------------`r`n`r`nSTERT`r`n// why ; music ;; Keep my words exactly. //`r`nfin." }
            else { "STERT`n// plans ; weekend ;;`n// music, // Bring the playlist.`n// food // Keep this outside the plan.`nfin." }
        [IO.File]::WriteAllText($repairPath, $repairText, [Text.UTF8Encoding]::new($false))
        $repairBytes = [IO.File]::ReadAllBytes($repairPath)
        $repairRead = [Func[string]] { $repairText }
        $repairAnalysis = $method.Invoke($null, @($repairRead, [string]$repairPath, $false, $null, [Threading.CancellationToken]::None, [Dert.Mer.DocumentSourceState]::Original))
        $repairAnalysis.Score.View = [Dert.Mer.MotionView]::Document
        $repairJournal = [Dert.Mer.MotionJournal]::new($repairAnalysis.Score, [DateTimeOffset]::Now)
        foreach ($change in $repairAnalysis.Events) { $repairJournal.Apply($change, [TimeSpan]::Zero) }
        $reviewOutput = [IO.StringWriter]::new()
        $settings = [Spectre.Console.AnsiConsoleSettings]::new()
        $settings.Ansi = [Spectre.Console.AnsiSupport]::No
        $settings.Interactive = [Spectre.Console.InteractionSupport]::No
        $settings.Out = [Spectre.Console.AnsiConsoleOutput]::new($reviewOutput)
        $reviewConsole = [Spectre.Console.AnsiConsole]::Create($settings)
        $reviewConsole.Profile.Width = $Columns
        $reviewConsole.Profile.Height = $Rows
        $cliAssembly = [Dert.Cli.CliApplication].Assembly
        $rendererType = $cliAssembly.GetType('Dert.Cli.DocumentRenderer')
        $renderer = $rendererType.GetConstructors()[0].Invoke(@($reviewConsole, $true, $null, $true))
        $reviewType = $cliAssembly.GetType('Dert.Cli.Motion.MerReview')
        $updateEnvironment = if ($UpdatePrefix -or $LocationPrefix) { [Dert.Cli.Discovery.DiscoveryEnvironment]::new($(if ($LocationSearchPath) { $LocationSearchPath } elseif ($LocationPrefix) { $LocationPrefix } else { $UpdatePrefix }), '.CMD;.EXE') } else { $null }
        $review = $reviewType.GetConstructors()[0].Invoke(@($repairAnalysis, $repairJournal.Snapshot([TimeSpan]::Zero), $repairBytes, [string]$repairPath,
            $reviewConsole, $renderer, [IO.StringReader]::new(''), $true, $updateEnvironment, $null, $null, [string](Join-Path $proofPath 'doctor-state')))
        $flags = [Reflection.BindingFlags]'Instance,NonPublic'
        $reviewType.GetField('selected', $flags).SetValue($review, $(if ($UpdatePrefix -or $LocationPrefix) { [int]6 } else { [int]2 }))
        $handle = $reviewType.GetMethod($(if ($NotesJourney) { 'HandleStructure' } elseif ($LocationPrefix) { 'HandleLocation' } elseif ($UpdatePrefix) { 'HandleUpdate' } else { 'HandleRepair' }), $flags)
        $steps = if ($NotesJourney) { @('find quiet', 'owner', 'hit 2', 'owner', 'structure') }
            elseif ($LocationPrefix) { if ($DiagnoseOnly) { @('probe', 'doctor') } else { @('probe', "copy $Copy", 'doctor') } }
            elseif ($UpdatePrefix) { @('rehearse', "copy $Copy", 'check') }
            elseif ($RepairJourney -eq 'title') { @('repair title', 'before', 'after', 'apply', 'undo', 'apply') }
            else { @('repair', 'close 4', 'before', 'after', 'apply', 'undo', 'apply') }
        $stepIndex = 0
        foreach ($command in $steps) {
            $reviewOutput.WriteLine("INPUT / $command")
            $arguments = if ($NotesJourney) { @($command) } else { @($command, [Threading.CancellationToken]::None) }
            if (-not $handle.Invoke($review, $arguments)) { throw "Review did not accept $command" }
            $reviewSnapshot = $reviewType.GetMethod('RepairView', $flags).Invoke($review, @())
            $reviewAnnotation = $reviewType.GetMethod('Annotation', $flags).Invoke($review, @())
            $reviewLine = $reviewType.GetField('selected', $flags).GetValue($review)
            $reviewFrame = [Dert.Mer.MerCompositor]::Document($reviewSnapshot, $Columns, $Rows, $reviewLine, $reviewAnnotation)
            Export-Frame $reviewFrame (Join-Path $proofPath ('{0}-{1:D2}-{2}.png' -f $(if ($NotesJourney) { 'notes' } elseif ($LocationPrefix) { 'location' } elseif ($UpdatePrefix) { 'update' } else { 'repair' }), $stepIndex++, $command.Replace(' ', '-')))
        }
        if ($UpdatePrefix) { [void]$reviewType.GetMethod('UpdateDetails', $flags).Invoke($review, @()) }
        if ($LocationPrefix) {
            [void]$reviewType.GetMethod('LocationDetails', $flags).Invoke($review, @())
            $doctorReports = $reviewType.GetField('doctors', $flags).GetValue($review)
            if ($doctorReports.ContainsKey([int]6)) {
                $doctorConnectionType = $cliAssembly.GetType('Dert.Cli.Motion.DoctorConnection')
                $doctorConnection = $doctorConnectionType.GetConstructors()[0].Invoke(@($doctorReports[[int]6]))
                $doctorLines = $doctorConnectionType.GetMethod('Receipt', $flags).Invoke($doctorConnection, @($Columns))
                Export-Frame ([pscustomobject]@{ Lines = $doctorLines }) (Join-Path $proofPath 'doctor-evidence.png')
            }
            $locations = [Dert.Cli.Discovery.ApplicationLocator]::Scan([Dert.Cli.Applications.ApplicationCatalog]::Codex, $updateEnvironment, [Threading.CancellationToken]::None, $null)
            $connectionType = $cliAssembly.GetType('Dert.Cli.Motion.LocationConnection')
            $connection = $connectionType.GetConstructors()[0].Invoke(@([int]6, $locations))
            $view = $connectionType.GetMethod('View', $flags).Invoke($connection, @())
            Export-Frame ([pscustomobject]@{ Lines = [Dert.Mer.LocationCompositor]::Receipt($view, $Columns) }) (Join-Path $proofPath 'where-receipt.png')
        }
        if ($NotesJourney) { [void]$reviewType.GetMethod('StructureDetails', $flags).Invoke($review, @()) }
        if ([Convert]::ToHexString([IO.File]::ReadAllBytes($repairPath)) -ne [Convert]::ToHexString($repairBytes)) { throw 'The repair journey did not restore its exact original bytes.' }
        [IO.File]::WriteAllText((Join-Path $proofPath $(if ($NotesJourney) { 'notes-review.txt' } elseif ($LocationPrefix) { 'location-review.txt' } elseif ($UpdatePrefix) { 'update-review.txt' } else { 'repair-review.txt' })), $reviewOutput.ToString(), [Text.UTF8Encoding]::new($false))
        $reviewOutput.Dispose()
    }
    $annotation = $null
    if ($resultPlan) {
        $resultPresentation = [Dert.Cli.CliApplication].Assembly.GetType('Dert.Cli.Results.ResultPresentation')
        $annotation = $resultPresentation.GetMethod('Annotation', [Reflection.BindingFlags]'Static,NonPublic').Invoke($null, @($resultPlan, [int]$AtLine))
    }
    if ($StructureView -or $ReferencesView) {
        $document = $adapter.GetProperty('Document', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($analysis)
        $structure = [Dert.Core.DertStructureIndex]::new($document)
        if ($ReferencesView) {
            $connection = [Dert.Cli.CliApplication].Assembly.GetType('Dert.Cli.Motion.ReferenceConnection')
            $reference = $connection.GetMethod('Read', [Reflection.BindingFlags]'Static,NonPublic').Invoke($null, @($structure, [int]$AtLine))
            if ($null -eq $reference) { throw 'This source line has no reasoning-note names.' }
            if ($null -ne $ReferenceNumber) {
                $choices = $connection.GetProperty('Choices', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($reference)
                if ($ReferenceNumber -gt $choices.Count) { throw 'No reference has that number.' }
                $choice = $choices[$ReferenceNumber - 1]
                $annotation = $connection.GetMethod('Visit', [Reflection.BindingFlags]'Instance,NonPublic').Invoke($reference, @($choice))
                $AtLine = $choice.Match.LineNumber
            } else {
                $annotation = $connection.GetMethod('Describe', [Reflection.BindingFlags]'Instance,NonPublic').Invoke($reference, @([int]1, $false))
            }
        } else {
            $connection = [Dert.Cli.CliApplication].Assembly.GetType('Dert.Cli.Motion.StructureConnection')
            $annotation = $connection.GetMethod('Describe', [Reflection.BindingFlags]'Static,NonPublic').Invoke($null, @($structure, [int]$AtLine))
        }
    }
    if ($ContextView) {
        $document = $adapter.GetProperty('Document', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($analysis)
        $contextSelection = [Dert.Core.ContextSelection]::Capture($document, [int]$AtLine, $sourcePath, [Dert.Core.ContextSourceKind]::FileSnapshot)
        $packet = $contextSelection.Build($IncludeNotes)
        $presentation = [Dert.Cli.CliApplication].Assembly.GetType('Dert.Cli.Authoring.ContextPresentation')
        $annotation = $presentation.GetMethod('Annotation', [Reflection.BindingFlags]'Static,NonPublic').Invoke($null, @($packet))
        $AtLine = $packet.Selection.Primary.LineNumber
    }
    if ($ProbeLocal) {
        $planType = [Dert.Cli.CliApplication].Assembly.GetType('Dert.Cli.Operations.DertOperationPlan')
        $document = $adapter.GetProperty('Document', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($analysis)
        $plan = $planType.GetMethod('Build').Invoke($null, @($document))
        if (-not $plan.CanRun -or -not ($plan.Steps | Where-Object { $_.RequestLine -eq $AtLine -and $_.Kind.ToString() -eq 'UpdateDiscovery' })) {
            throw 'The selected line is not an eligible local discovery request.'
        }
        $inspectionEnvironment = if ($PSBoundParameters.ContainsKey('InspectionPath')) { [Dert.Cli.Discovery.DiscoveryEnvironment]::new($InspectionPath, '.CMD;.EXE') }
            else { [Dert.Cli.Discovery.DiscoveryEnvironment]::Capture() }
        $report = [Dert.Cli.Discovery.CodexDiscovery]::Scan($inspectionEnvironment, [Threading.CancellationToken]::None)
        if ($Copy -gt [Math]::Max(1, $report.Installations.Count)) { throw 'No inspected copy has that number.' }
        $inspectionType = [Dert.Cli.CliApplication].Assembly.GetType('Dert.Cli.Motion.LocalInspection')
        $inspection = $inspectionType.GetConstructors()[0].Invoke(@([int]$AtLine, [DateTimeOffset]::Now, $report))
        $annotation = $inspectionType.GetMethod('Annotation', [Reflection.BindingFlags]'Instance,NonPublic').Invoke($inspection, @($Copy - 1))
        $inspectionDetail = $inspectionType.GetMethod('Details', [Reflection.BindingFlags]'Instance,NonPublic').Invoke($inspection, @())
        [IO.File]::WriteAllText((Join-Path $proofPath 'local-inspection.txt'), (($inspectionDetail | ForEach-Object Text) -join "`n"), [Text.UTF8Encoding]::new($false))
    }
    $held = if ($DocumentWindow) { [Dert.Mer.MerCompositor]::Document($complete, $Columns, $Rows, $AtLine, $annotation) }
            elseif ($SourceView) { [Dert.Mer.MerCompositor]::Source($complete, $AtLine, $Columns) }
            elseif ($null -ne $AtLine) { [Dert.Mer.MerCompositor]::Inspect($complete, $AtLine, $Columns, $Rows) }
            else { [Dert.Mer.MerCompositor]::Hold($complete, $Columns, $Rows) }
    if ($null -eq $held) { throw "No recorded focus at line $AtLine." }
    Export-Frame $held (Join-Path $proofPath 'held.png')
    if ($DocumentWindow) { $terminal.Finish($held.Lines) }
    else { $terminal.Finish([Dert.Mer.MerCompositor]::Receipt($complete)) }
    $record.Add((@($elapsed, 'o', $writer.ToString().Substring($offset)) | ConvertTo-Json -Compress))
    [IO.File]::WriteAllText((Join-Path $proofPath 'recording.cast'), ($record -join "`n"), [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $proofPath 'terminal.ansi'), $writer.ToString(), [Text.UTF8Encoding]::new($false))
    Write-Host "Rendered engine frames and ANSI replay retained at $proofPath"
}
finally { $terminal.Dispose(); $writer.Dispose() }
