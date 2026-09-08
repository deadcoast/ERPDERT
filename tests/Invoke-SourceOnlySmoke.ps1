[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$proofPath = Join-Path $projectPath ('.artifacts\source-only-proof\' + [Guid]::NewGuid().ToString('N'))
$copyPath = Join-Path $proofPath 'project'
[void] (New-Item -ItemType Directory -Path $copyPath)
$record = [Collections.Generic.List[string]]::new()

# Build a fresh copy from source and test inputs only. Never move or hide the
# user's documents, and never reuse bin/obj outputs that could conceal a dependency.
$rootInputs = @('ERPDERT.slnx', 'Directory.Build.props', 'global.json')
foreach ($name in $rootInputs) {
    Copy-Item -LiteralPath (Join-Path $projectPath $name) -Destination (Join-Path $copyPath $name)
}
$sourceRoots = @((Join-Path $projectPath 'src'), (Join-Path $projectPath 'tests'))
foreach ($file in Get-ChildItem -LiteralPath $sourceRoots -Recurse -File) {
    $relative = [IO.Path]::GetRelativePath($projectPath, $file.FullName)
    if ($relative -match '(^|[\\/])(bin|obj|TestResults|\.artifacts)([\\/]|$)') { continue }
    if ($file.Extension -notin @('.cs', '.csproj', '.props', '.targets', '.json', '.ps1', '.DERT')) { continue }
    $destination = Join-Path $copyPath $relative
    [void] (New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force)
    Copy-Item -LiteralPath $file.FullName -Destination $destination
}

function Invoke-Dotnet {
    param([string[]] $CommandArgs)
    $output = (& dotnet @CommandArgs 2>&1 | Out-String)
    $actualExit = $LASTEXITCODE
    $record.Add("dotnet $($CommandArgs -join ' ')`n$output")
    Write-Host $output
    if ($actualExit -ne 0) { throw "Source-only check failed: dotnet exited $actualExit." }
}

Push-Location -LiteralPath $copyPath
try {
    Invoke-Dotnet @('test', 'ERPDERT.slnx', '-c', 'Release')
    Invoke-Dotnet @('publish', 'src/Erpdert.Cli', '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-o', '.artifacts/win-x64')
    foreach ($suite in @('Invoke-PublishedSmoke.ps1', 'Invoke-RepairSmoke.ps1', 'Invoke-NoteRepairSmoke.ps1', 'Invoke-LintHandoffSmoke.ps1', 'Invoke-RepairFollowthroughSmoke.ps1', 'Invoke-DiscoverySmoke.ps1', 'Invoke-DertRunSmoke.ps1')) {
        $output = (& (Join-Path $copyPath "tests\$suite") 6>&1 | Out-String)
        $record.Add("$suite`n$output")
        Write-Host $output
    }
    Write-Host 'Source-only build, tests, publish, and all seven executable suites passed without documentation.'
}
catch {
    $record.Add($_.ToString())
    throw
}
finally {
    Pop-Location
    [IO.File]::WriteAllText((Join-Path $proofPath 'output.txt'), ($record -join "`n"), [Text.UTF8Encoding]::new($false))
    Write-Host "Source-only evidence retained at $proofPath"
}
