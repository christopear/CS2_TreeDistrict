# TreeDistrict

A Cities: Skylines II mod in development: draw a vegetation area, choose its tree
and shrub mix, and populate it according to density, elevation and slope rules.

**Status: native Tree Area, in-game settings editor and manual London Plane generation.**
The repository includes a runnable district editor preview, versioned JSON
species profiles, a CS2 AreaPrefab/settings adapter, and the portable C# core.
See [what works and the Windows integration contract](docs/ui-scaffold.md).

Generation creates ordinary native trees and keeps remapped ownership references
in area records. Regenerate replaces owned trees; remove clears them; deleting an
area also removes its owned trees. Bake + delete releases the trees and removes
the area after confirmation. Boundary edits do not automatically regenerate.
The user has confirmed visible generation after the native update-order fix;
save/reload, native deletion and the previously reported second-area crash still
need a complete regression pass before release.

[London Plane data](data/london-plane.json) separates botanical evidence from
provisional simulation values. The current jittered-grid placement and 20 m
spacing are prototype assumptions and can look sparse and orchard-like.
Shade competition is not implemented; generation currently supports London Plane
only. Older design documents describe planned features beyond this prototype.

## Try the editor locally

With Node 24 LTS installed:

```bash
npm --prefix ui ci
npm --prefix ui run dev
```

Open http://127.0.0.1:5173. Pick a set, draw a district, then edit its settings.
The preview stores settings in this browser and supports JSON export/import.
London Plane is the first researched profile; numerical shade and environmental
curves remain provisional, as recorded alongside its source references.

```bash
npm --prefix ui run check
npm --prefix ui exec playwright install chromium
npm --prefix ui run test:browser
```

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

Install the pinned SDK locally and load the Windows environment, then run from
the repository root:

```powershell
& .\scripts\bootstrap.ps1 # First setup only
. .\scripts\env.ps1      # Each new terminal
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
- The **official CS2 mod template and post-processor** for the Windows adapter.
  Game-facing SDK/runtime/Unity versions come from the installed toolchain.
- React 18.3.1 / TypeScript for the shared editor, with a Vite-only local preview,
  Vitest state tests and Playwright workflow tests. Game builds use the
  installed official UI generator, native controls and an original Tree Area icon.

No game assemblies, Unity installation, generated mod, or third-party source code
are bundled here. The default solution deliberately builds without them.

See the [draft product requirements](docs/requirements.md),
[foliage reference research and design](docs/foliage-reference-design.md),
[UX and UI technology decision](docs/ux-design.md),
[architecture and milestones](docs/architecture.md) and
[reference sources](docs/references.md). Licensed under [MIT](LICENSE).
