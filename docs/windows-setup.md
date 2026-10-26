# Windows game-integration handoff

The portable core is ready to develop on Ubuntu. Creating the game adapter is the
next integration step and requires access to an installed CS2 modding toolchain.
Do this on Windows, where the game is already installed.

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

The `codex/tree-district-poc` branch now contains the generated adapter under
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

The original integration checklist follows:

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
