# Dot-source this file to refresh this terminal: . .\scripts\env.ps1
$repoRoot = Split-Path $PSScriptRoot -Parent
foreach ($scope in @('Machine', 'User')) {
    foreach ($entry in [Environment]::GetEnvironmentVariables($scope).GetEnumerator()) {
        if ($entry.Key -like 'CSII_*') {
            [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
        }
    }
}
# Preserve session-specific tools while picking up newly installed Windows tools.
$paths = @(
    (Join-Path $repoRoot '.tools\dotnet')
    [Environment]::GetEnvironmentVariable('Path', 'Machine')
    [Environment]::GetEnvironmentVariable('Path', 'User')
    $env:Path
)
$env:Path = (($paths -join ';') -split ';' | Where-Object { $_ } | Select-Object -Unique) -join ';'
if (Test-Path (Join-Path $repoRoot '.tools\dotnet\dotnet.exe')) {
    $env:DOTNET_ROOT = Join-Path $repoRoot '.tools\dotnet'
}
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
