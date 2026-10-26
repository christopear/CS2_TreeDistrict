$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$sdkVersion = (Get-Content (Join-Path $repoRoot 'global.json') -Raw | ConvertFrom-Json).sdk.version
$toolsPath = Join-Path $repoRoot '.tools'
New-Item -ItemType Directory -Force -Path $toolsPath | Out-Null
$installer = Join-Path $toolsPath 'dotnet-install.ps1'
Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer
& $installer -Version $sdkVersion -InstallDir (Join-Path $toolsPath 'dotnet') -NoPath
if (-not $?) { throw 'SDK installation failed.' }
. (Join-Path $PSScriptRoot 'env.ps1')
& dotnet --version
if ($LASTEXITCODE -ne 0) { throw 'SDK verification failed.' }
