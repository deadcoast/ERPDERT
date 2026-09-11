[CmdletBinding()]
param([string] $OutputDirectory = '.artifacts/win-x64')

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$destination = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
foreach ($project in @('Dert.Cli', 'Dert.Notes')) {
    $outputPath = if ($project -eq 'Dert.Notes') { Join-Path $destination 'notes' } else { $destination }
    & dotnet publish (Join-Path $PSScriptRoot $project) -c Release -r win-x64 --self-contained true -o $outputPath
    if ($LASTEXITCODE -ne 0) { throw "Publishing $project failed with exit $LASTEXITCODE." }
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Try Notes.cmd') -Destination $destination
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Try Help.cmd') -Destination $destination
Write-Host "CLI and notes host published together at $destination"
