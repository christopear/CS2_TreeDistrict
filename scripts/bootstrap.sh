#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"
sdk_version="$(sed -n 's/.*"version": "\([^"]*\)".*/\1/p' global.json)"
installer="$(mktemp)"
trap 'rm -f "$installer"' EXIT
curl --fail --silent --show-error --location --proto '=https' --tlsv1.2 \
    https://dot.net/v1/dotnet-install.sh -o "$installer"
bash "$installer" --version "$sdk_version" --install-dir "$repo_root/.tools/dotnet" --no-path
bash scripts/dotnet.sh --version
