[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $Compiler,
    [string] $PackageDirectory = '.artifacts/win-x64',
    [string] $OutputDirectory = '.artifacts/installer',
    [string] $ProofId,
    [string] $ProofRoot
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$Compiler = (Resolve-Path -LiteralPath $Compiler).Path
$PackageDirectory = (Resolve-Path -LiteralPath $PackageDirectory).Path
$OutputDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
foreach ($required in @('DERT.exe', 'coreclr.dll', 'notes/DERT.Notes.exe', 'notes/coreclr.dll', 'notes/PresentationFramework.dll', 'notes/ICSharpCode.AvalonEdit.dll', 'notes/AVALONEDIT-LICENSE.txt', 'UNICODE-LICENSE.txt', 'examples/weekend.DERT')) {
    if (-not (Test-Path -LiteralPath (Join-Path $PackageDirectory $required) -PathType Leaf)) { throw "The package is incomplete: $required. Run src/Publish-Windows.ps1 first." }
}
# The recipe checks the actual compiler's VER, rather than trusting an adjacent
# executable's version metadata (ISCC itself has no useful FileVersion).
$version = (Get-Item -LiteralPath (Join-Path $PackageDirectory 'DERT.exe')).VersionInfo.FileVersion
if ($version -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') { throw 'The published executable has no numeric Windows file version.' }
$arguments = @('/Q', "/O$OutputDirectory", "/DPackageDirectory=$PackageDirectory", "/DBuildVersion=$version")
if ($ProofId) {
    if ($ProofId -notmatch '^[a-f0-9]{32}$' -or -not $ProofRoot) { throw 'Proof builds need a 32-character lowercase GUID and a disposable ProofRoot.' }
    $ProofRoot = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ProofRoot)
    $arguments += @("/DProofId=$ProofId", "/DProofRoot=$ProofRoot")
}
elseif ($ProofRoot) { throw 'ProofRoot requires ProofId.' }
[void][IO.Directory]::CreateDirectory($OutputDirectory)
& $Compiler @arguments (Join-Path $PSScriptRoot 'Installer/DERT.iss')
if ($LASTEXITCODE -ne 0) { throw "Installer compilation failed with exit $LASTEXITCODE." }
$name = if ($ProofId) { "DERT-Proof-$ProofId.exe" } else { 'DERT-Setup.exe' }
$setup = Join-Path $OutputDirectory $name
$hash = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash
[IO.File]::WriteAllText(($setup + '.sha256'), "$hash  $name`n", [Text.UTF8Encoding]::new($false))
Write-Host "Unsigned workspace installer: $setup"
Write-Host "SHA256: $hash"
