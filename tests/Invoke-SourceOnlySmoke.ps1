[CmdletBinding()]
param([string] $InstallerCompiler)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
if ($InstallerCompiler) { $InstallerCompiler = (Resolve-Path -LiteralPath $InstallerCompiler).Path }
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$proofPath = Join-Path $projectPath ('.artifacts\source-only-proof\' + [Guid]::NewGuid().ToString('N'))
$copyPath = Join-Path $proofPath 'project'
[void] (New-Item -ItemType Directory -Path $copyPath)
$record = [Collections.Generic.List[string]]::new()

# Build a fresh copy from source and test inputs only. Never move or hide the
# user's documents, and never reuse bin/obj outputs that could conceal a dependency.
$rootInputs = @('DERT.slnx', 'Directory.Build.props', 'global.json')
foreach ($name in $rootInputs) {
    Copy-Item -LiteralPath (Join-Path $projectPath $name) -Destination (Join-Path $copyPath $name)
}
$sourceRoots = @((Join-Path $projectPath 'src'), (Join-Path $projectPath 'tests'))
foreach ($file in Get-ChildItem -LiteralPath $sourceRoots -Recurse -File) {
    $relative = [IO.Path]::GetRelativePath($projectPath, $file.FullName)
    if ($relative -match '(^|[\\/])(bin|obj|TestResults|\.artifacts)([\\/]|$)') { continue }
    if ($file.Extension -notin @('.cs', '.csproj', '.props', '.targets', '.json', '.ps1', '.cmd', '.DERT', '.xaml', '.js', '.mjs', '.iss') -and $file.Name -notin @('AVALONEDIT-LICENSE.txt', 'UNICODE-LICENSE.txt')) { continue }
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
    Invoke-Dotnet @('test', 'DERT.slnx', '-c', 'Release')
    $output = (& (Join-Path $copyPath 'src\Publish-Windows.ps1') 6>&1 | Out-String)
    $record.Add("Publish-Windows.ps1`n$output")
    Write-Host $output
    foreach ($suite in @('Invoke-PublishedSmoke.ps1', 'Invoke-RepairSmoke.ps1', 'Invoke-NoteRepairSmoke.ps1', 'Invoke-LintHandoffSmoke.ps1', 'Invoke-RepairFollowthroughSmoke.ps1', 'Invoke-DiscoverySmoke.ps1', 'Invoke-DertRunSmoke.ps1', 'Invoke-UpdateSmoke.ps1', 'Invoke-WhereSmoke.ps1', 'Invoke-MerSmoke.ps1', 'Invoke-NotesSmoke.ps1')) {
        $output = (& (Join-Path $copyPath "tests\$suite") 6>&1 | Out-String)
        $record.Add("$suite`n$output")
        Write-Host $output
    }
    Write-Host 'Source-only build, tests, publish, and all eleven executable suites passed without documentation.'
    if ($InstallerCompiler) {
        & (Join-Path $copyPath 'src/Build-Installer.ps1') -Compiler $InstallerCompiler
        $installationId = [Guid]::NewGuid().ToString('N')
        $installationRoot = Join-Path $proofPath ('installation/' + $installationId)
        & (Join-Path $copyPath 'src/Build-Installer.ps1') -Compiler $InstallerCompiler -ProofId $installationId -ProofRoot $installationRoot -OutputDirectory $installationRoot
        $output = (& (Join-Path $copyPath 'tests/Invoke-InstallationSmoke.ps1') -Setup (Join-Path $installationRoot "DERT-Proof-$installationId.exe") -ProofId $installationId -ProofRoot $installationRoot 6>&1 | Out-String)
        $record.Add("Invoke-InstallationSmoke.ps1`n$output")
        Write-Host $output
    }
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
# Expected nonzero native exits inside a successful smoke suite must not become
# this script's exit status. Exceptions above still fail before reaching here.
exit 0
