#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"
bash scripts/dotnet.sh restore TreeDistrict.sln --locked-mode
bash scripts/dotnet.sh build TreeDistrict.sln --configuration Release --no-restore
bash scripts/dotnet.sh test TreeDistrict.sln --configuration Release --no-build --no-restore
bash scripts/dotnet.sh format TreeDistrict.sln --verify-no-changes --no-restore
