#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
export DOTNET_CLI_HOME="$repo_root/.tools/cli-home"
export NUGET_PACKAGES="$repo_root/.tools/nuget/packages"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
# Keep SDK mutexes writable inside restricted development environments.
export XDG_RUNTIME_DIR="$repo_root/.tools/run"
mkdir -p "$XDG_RUNTIME_DIR"
chmod 700 "$XDG_RUNTIME_DIR"
if [[ -x "$repo_root/.tools/dotnet/dotnet" ]]; then
    export DOTNET_ROOT="$repo_root/.tools/dotnet"
    exec "$DOTNET_ROOT/dotnet" "$@"
fi
if command -v dotnet >/dev/null 2>&1; then
    exec dotnet "$@"
fi
echo 'No .NET SDK found. Run: bash scripts/bootstrap.sh' >&2
exit 1
