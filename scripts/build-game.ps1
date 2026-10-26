param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug')
$ErrorActionPreference = 'Stop'
if (Get-Process Cities2 -ErrorAction SilentlyContinue) {
    throw 'Close Cities: Skylines II before building; the official deployment replaces loaded mod files.'
}
. (Join-Path $PSScriptRoot 'env.ps1')
$gameRoot = Join-Path (Split-Path $PSScriptRoot -Parent) 'game'
$systemDotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
if (-not (Test-Path $systemDotnet)) { throw 'Install the SDK required by game/global.json.' }
if (-not (Test-Path "$env:DOTNET_ROOT\shared\Microsoft.NETCore.App\6.0.36")) {
    throw 'Install the postprocessor runtime as described in docs/windows-setup.md.'
}
Push-Location $gameRoot
try {
    & $systemDotnet build TreeDistrict/TreeDistrict.csproj -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw 'Game mod build failed.' }
    # The official C# deployment clears the mod directory; UI must be built last.
    & npm.cmd --prefix TreeDistrictUI run build
    if ($LASTEXITCODE -ne 0) { throw 'Game UI build failed.' }
} finally {
    Pop-Location
}
