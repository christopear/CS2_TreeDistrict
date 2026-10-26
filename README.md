# TreeDistrict

A Cities: Skylines II mod in development: draw a vegetation area, choose its tree
and shrub mix, and populate it according to density, elevation and slope rules.

**Status: development foundation only; there is no installable game mod yet.**
The repository currently contains a portable C# core, terrain eligibility rules,
unit tests, and Linux/Windows CI. Polygon drawing, generation, persistence and the
game adapter are the next milestones.

## Develop on Ubuntu

```bash
bash scripts/bootstrap.sh
bash scripts/check.sh
```

Bootstrap installs the SDK pinned in `global.json` under `.tools/dotnet`, using
Microsoft's official installer. It needs internet access and `curl`, but no sudo
or shell-profile changes. SDK caches and packages stay in the ignored `.tools`
directory. `scripts/dotnet.sh` uses this SDK, falling back to a system installation.

For example:

```bash
bash scripts/dotnet.sh test TreeDistrict.sln
```

## Develop on Windows

Install the .NET SDK specified by `global.json`, then run from the repository root:

```powershell
dotnet restore TreeDistrict.sln --locked-mode
dotnet build TreeDistrict.sln -c Release --no-restore
dotnet test TreeDistrict.sln -c Release --no-build --no-restore
dotnet format TreeDistrict.sln --verify-no-changes --no-restore
```

These commands test the portable core without CS2. To create and test the actual
mod, follow [the Windows handoff](docs/windows-setup.md).

## Toolchain choices

- .NET 10 LTS SDK for portable development and tests; exact SDK feature band is
  pinned, with servicing patches allowed.
- `netstandard2.0` and C# 9 for the core, keeping its API surface compatible with
  the game's currently documented `net48` mod target. Actual game compatibility
  still needs an in-game test.
- xUnit, the .NET test SDK and coverage collector from the installed SDK's test
  template. NuGet lockfiles make dependency restoration reproducible.
- The **official CS2 mod template and post-processor** for the future adapter.
  Game-facing SDK/runtime/Unity versions come from the installed toolchain.
- The official UI generator when UI integration begins. No unrelated web app
  framework or Node dependencies are required for this initial core.

No game assemblies, Unity installation, generated mod, or third-party source code
are bundled here. The default solution deliberately builds without them.

See [architecture and milestones](docs/architecture.md) and
[reference sources](docs/references.md). An open-source licence has not yet been
chosen; public repository visibility alone does not grant reuse permissions.
