param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [switch]$StageOnly
)
$ErrorActionPreference = 'Stop'
if (-not $StageOnly -and (Get-Process Cities2 -ErrorAction SilentlyContinue)) {
    throw 'Close Cities: Skylines II before building; the official deployment replaces loaded mod files.'
}
. (Join-Path $PSScriptRoot 'env.ps1')
$gameRoot = Join-Path (Split-Path $PSScriptRoot -Parent) 'game'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$stageRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts/game-stage'))
$stageMod = Join-Path $stageRoot 'TreeDistrict'
# The official targets recursively clear DeployDir. Pin both global properties
# to this checked repository path when staging; never change saved CSII variables.
if (-not $stageRoot.StartsWith([IO.Path]::GetFullPath($repositoryRoot) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Staging directory must be inside the repository.'
}
$systemDotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
if (-not (Test-Path $systemDotnet)) { throw 'Install the SDK required by game/global.json.' }
if (-not (Test-Path "$env:DOTNET_ROOT\shared\Microsoft.NETCore.App\6.0.36")) {
    throw 'Install the postprocessor runtime as described in docs/windows-setup.md.'
}
Push-Location $gameRoot
$previousUiOutput = $env:TREEDISTRICT_OUTPUT_DIR
try {
    $buildArgs = @('build', 'TreeDistrict/TreeDistrict.csproj', '-c', $Configuration)
    if ($StageOnly) {
        Remove-Item -LiteralPath (Join-Path $stageRoot 'manifest.json') -ErrorAction SilentlyContinue
        $buildArgs += @("-p:LocalModsPath=$stageRoot", "-p:DeployDir=$stageMod")
        $env:TREEDISTRICT_OUTPUT_DIR = $stageMod
    } else {
        Remove-Item Env:TREEDISTRICT_OUTPUT_DIR -ErrorAction SilentlyContinue
    }
    & $systemDotnet @buildArgs
    if ($LASTEXITCODE -ne 0) { throw 'Game mod build failed.' }
    # The official C# deployment clears the mod directory; UI must be built last.
    & npm.cmd --prefix TreeDistrictUI run build
    if ($LASTEXITCODE -ne 0) { throw 'Game UI build failed.' }
    if ($StageOnly) {
        $files = Get-ChildItem -LiteralPath $stageMod -File -Recurse
        foreach ($required in @('TreeDistrict.dll', 'TreeDistrict.Core.dll', 'TreeDistrict.mjs', 'TreeDistrict.css', 'Icons/TreeArea.svg')) {
            if (-not (Test-Path -LiteralPath (Join-Path $stageMod $required))) { throw "Missing staged output: $required" }
        }
        if ($files | Where-Object { $_.Name -match '^(Game|Unity\.|UnityEngine\.|Colossal\.).*\.dll$' }) {
            throw 'Staged output unexpectedly contains game or Unity assemblies.'
        }
        $manifest = [ordered]@{
            commit = (& git -C $repositoryRoot rev-parse HEAD)
            workingTreeChanges = @(& git -C $repositoryRoot status --short)
            configuration = $Configuration
            builtUtc = [DateTime]::UtcNow.ToString('o')
            files = @($files | ForEach-Object { [ordered]@{
                path = $_.FullName.Substring($stageMod.Length + 1)
                sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
            } })
        }
        $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $stageRoot 'manifest.json') -Encoding utf8
        Write-Host "Staged build ready: $stageMod (live mod unchanged)"
    }
} finally {
    if ($null -eq $previousUiOutput) { Remove-Item Env:TREEDISTRICT_OUTPUT_DIR -ErrorAction SilentlyContinue }
    else { $env:TREEDISTRICT_OUTPUT_DIR = $previousUiOutput }
    Pop-Location
}
