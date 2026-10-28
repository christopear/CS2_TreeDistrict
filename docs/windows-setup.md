# Windows game-integration handoff

Current checkpoint: `codex/standalone-foliage-pipeline`. Ubuntu now validates the
researched profile catalogue, six presets, editor settings and pure
`DistrictGenerationAdapter` → Proton solver connection. The live game generator
still uses its earlier single-London-Plane planner; mixed sets intentionally fail
before touching owned trees. The bridge is available but not wired to spawning.

On Windows, first pull this branch with CS2 closed, then build below. Check the
new Core project reference is deployed alongside the mod. Next replace the legacy
planner with the shared adapter/coordinator, resolve every species to its native
prefab, and distinguish tree ages from static bush/flower entities. Validate
terrain sampling and occupied land, then ownership across regenerate, ungenerate,
bake/delete, save/reload and a second area. Stable game climate is still a TODO;
manual city temperature remains the fallback. Use a disposable test city.

Do not calibrate culling or turn pre-abundance on as part of this integration.

## Prepare a repository terminal

Run once to install the portable SDK pinned by `global.json` into ignored
`.tools/dotnet` (leaves the system/game SDK installed):

```powershell
& .\scripts\bootstrap.ps1
```

In each new repository terminal, load the environment before building:

```powershell
. .\scripts\env.ps1
dotnet --version
npm.cmd --prefix ui ci
```

The helper refreshes saved Windows PATH entries and `CSII_*` variables and
selects the repository-local SDK. It changes only the current terminal. Restart
your IDE after installing tools to refresh its inherited environment too.
Use `npm.cmd` if PowerShell execution policy prevents the npm PowerShell shim.
The installed game template can be inspected with the system .NET executable
from outside this checkout, avoiding the portable `global.json` pin.

## Integrate the game adapter

The current branch contains the generated adapter under
`game/TreeDistrict`. Build from the repository root with:

```powershell
# Once, after bootstrap.ps1: the installed CS2 postprocessor targets net6.0.
& .\.tools\dotnet-install.ps1 -Runtime dotnet -Version 6.0.36 -InstallDir .\.tools\dotnet -NoPath
npm.cmd --prefix ui ci
npm.cmd --prefix game/TreeDistrictUI ci
& .\scripts\build-game.ps1
```

The build helper uses the system .NET 8 SDK selected by `game/global.json` and
the local runtime for the official postprocessor. The generated targets deploy
to the user's `CSII_LOCALMODSPATH/TreeDistrict` directory. Restart CS2 after a
build. This is local deployment, not publication to Paradox Mods.

The proof of concept registers a uniquely named, green `SpacePrefab` (a stock
`AreaPrefab` subtype), adds it to the same menu category as the native district
prefab, and delegates interaction to the game's `AreaToolSystem`. It does not add
administrative district components, terrain surfaces, clearing or vegetation.
The stable prefab name is `TreeDistrict.Area.v1`; area nodes and geometry use
native game components. Save/load and editing must be verified in-game before
relying on this in a real city. Sharing the native Space category is an initial
integration experiment, not the final overlap/isolation contract.

Inspected installation: CS2 1.6.2f1 (767.21d1), game runtime Unity 2022.3.71f1;
installed toolchain Unity 2022.3.62f2,
Entities 1.3.10, official template 1.0.0, SDK 8.0.427, postprocessor runtime
6.0.36. Official build and postprocessing succeed with no warnings. No game
assemblies are copied into the deployed output.

## Mixed-vegetation integration

Experimental map-editor access: the UI also registers on the official `Editor`
hook, with a **Tree Areas** button near the top centre. Open it, choose a set and
use Draw district to activate the native area tool directly; it does not depend
on the gameplay district menu. Settings bindings run in GameOrEditor mode, and
editor selection uses ToolSystem instead of the gameplay-only selected-info UI.
Editor drawing, generation and saved/exported map behaviour need in-game testing.
The portable test verifies a 5 km square can produce more than 1,000,000 plants;
it does not establish that creating a million native entities performs acceptably.
The stamp path samples each copied plant directly; it does not build the full-area
environment grid previously used by native generation.

The native Generate action now calls `DistrictGenerationAdapter` and
`StampFoliageGenerator`, using the embedded `data/proton-scatter.v1.json` to build
eight neutral templates in [1,199] m on both axes. Randomly selected/quarter-turned
templates fit world-anchored 200 m tiles. Adjacent tiles have at least 2 m between
plant centres; no species-specific seam reconciliation is performed. The cache
excludes area boundary and seed, so extensions reuse the library and retain the
same layout in overlapping regions. Effective settings changes select a different
entry; the cache is session-only and rebuilt deterministically after restart.
Logs report planning duration, cache reuse and completed placement jobs.
Native placement runs in batches of at most 50,000 plants, with at least 0.5 seconds
of wall-clock time after each batch before the next. Only one placement job runs
at a time, and missed intervals never cause catch-up bursts. Planning remains
synchronous. Progress updates once per batch; idle areas do not scan their plants.
Cancel keeps completed batches linked to the area. Remove/delete also cancels the
queue; bake cancels it and releases already placed plants. Saving during placement
saves completed batches only, without resuming the pending plan on reload.
Current area nodes supply the polygon. Native terrain supplies exact placement
heights and sampled slope; water deeper than 1 cm is excluded. Each enabled JSON
asset resolves its existing prefab name before any owned plants are replaced.
Native TreeData determines whether to apply growth state; static shrubs remain
ordinary plants without a Tree component. Both use the existing area-owned,
remapped entity-reference save layout (version 2).

Existing vegetation is ignored. Roads/buildings are not supplied as solver
obstacles in this pass either. Stamps use neutral conditions and relative species
weights. At world placements, hard limits and zero temperature/slope suitability
are culled, but positive suitability does not thin or remix the cached distribution.
Additional shade/competition passes remain off. Clumping
must be zero. Climate uses the authored city reference and set overrides with
altitude cooling; no live seasonal temperature is read. Generation is synchronous,
manual, with no TreeDistrict population cap. Areas over 25 km² show a non-blocking
warning before generation; native game limits and available memory still apply.
Candidate/attempt/neighbour budgets are raised alongside ownership/save-load limits;
this does not make planning or spawning asynchronous. Oversized plans fail rather
than silently truncating. Saved areas retain their settings; choose a new preset
explicitly to try the newer mixtures.

Build safely while the game is open:

```powershell
./scripts/build-game.ps1 -StageOnly
```

This redirects both official C# deployment and UI output to
`artifacts/game-stage/TreeDistrict`, verifies required files/no game DLLs, and
writes a SHA-256 manifest alongside it. The manifest records source commit and
uncommitted changes. A failed staged build removes the previous ready manifest.
The usual build without `-StageOnly` still installs locally and refuses to run
while CS2 is open. Close the game before deploying; do not publish to Paradox Mods
as part of this test handoff.

Windows verification: official post-processing, core DLL packaging and native UI
build succeeded. Portable tests cover shipped presets, a 30% grade, water
exclusion and native age mapping. NuGet audit connectivity emitted NU1900 warnings
on the staged build; the SDK helper also printed its existing root-SDK lookup
diagnostic, but compilation and all post-processing completed successfully.
The running installation was hashed before and after staging and was unchanged.
These checks do not establish native runtime correctness. Test in a disposable
city after deployment:

1. Choose a mixed preset, draw an area on flat and sloping ground, then Generate.
   Confirm multiple species, shrubs, terrain alignment and the status count.
2. Change a mix/age distribution, add boundary nodes, then regenerate. Edits alone
   must not regenerate. Only previously owned plants should be replaced.
3. Manually remove some generated plants, then regenerate and remove generated.
   Existing unowned plants should remain untouched.
4. Save/reload a planted area; confirm count and ownership, then test both UI area
   deletion and native deletion. Both must remove owned trees and shrubs.
5. Cancel the bake confirmation; then confirm Bake + delete. Plants must remain,
   and deleting another area must not affect them.
6. Disable all species and regenerate to empty the owned planting. An unavailable
   prefab or invalid setting must instead report an error and retain it.

The original integration checklist follows (historical initial area-only phase):

1. Start CS2 and install/update the official toolchain in **Options → Modding**.
   Let it select Unity, Burst/Entities, SDK and Node dependencies. Do not substitute
   the latest Unity release or an old BepInEx tutorial.
2. Restart your terminal/IDE so it sees the new `CSII_*` environment variables.
   Visual Studio or Rider can use the installed mod template; VS Code is also an
   editing option. Keep game-installation paths out of tracked files.
3. Inspect the installation before creating a project:

   ```powershell
   Get-ChildItem Env:CSII_*
   dotnet --list-sdks
   dotnet new csiimod --help
   ```

4. Generate the adapter with that installed template. The root `global.json` is
   for portable development: run the generator from a temporary folder outside
   this checkout if it requires a different SDK. Move the generated project into
   `game/TreeDistrict`, and give `game/global.json` the SDK required by the
   toolchain if necessary. The nested file must be used by building from `game/`.
   Typical generator command (check its help first):

   ```powershell
   dotnet new csiimod -n TreeDistrict
   ```

5. Preserve the generated `Mod.props` / `Mod.targets` imports and framework.
   Add a reference to `../../src/TreeDistrict.Core/TreeDistrict.Core.csproj`.
   Verify the post-processed output and that the core DLL is deployed alongside
   the mod. Keep the adapter outside the portable solution; use a separate game
   solution if needed. Do not add global Directory.Build.props overrides that
   interfere with the toolchain.
6. First prove that the generated mod loads and logs on a disposable test city.
   Implement the [settings/district host contract](ui-scaffold.md), then verify
   drawing, selection, editing and save/load. Do not add tree generation yet.
7. To integrate the existing shared editor and native entry, use the installed `@colossalorder/create-csii-ui-mod` generator inside the adapter
   directory using the toolchain's Node environment. Match its mod ID to the C#
   assembly name, commit its lockfile, and use `npm ci` for subsequent installs.
   Verify deploy order: the C# deployment can clear the shared mod output folder.

Every game/Unity assembly reference must have `Private=false`; never distribute
those DLLs. Keep `Properties/PublishConfiguration.xml` local (ignored), review it
for account details, and provide a sanitised example if documentation needs it.

## Before claiming the adapter works

- Record the actual game version and toolchain versions used.
- Build through the official post-processor and restart the game.
- Check the game log for loader errors and unwanted bundled game assemblies.
- Verify district drawing, selection, cancellation, settings and save/reload on a
  test city. Generation actions must remain unavailable in this phase.
- Check ordinary vegetation tools and construction still behave normally.

The Linux core CI proves none of these game behaviours. Publishing to Paradox
Mods is a separate release step, after in-game verification.
