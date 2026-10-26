# Local UX scaffold and game integration contract

Implemented 2026-10-26. This is a runnable local editor and a game-facing UI entry,
not an installable mod. No vegetation generation, terrain sampling or game entity
creation has been implemented.

## Run and verify

Use Node 24 LTS (the package supports Node >=22.12). React is pinned to 18.3.1 to
match the inspected CS2 UI template. Vite serves only the local development
preview; it does not replace the official mod toolchain.

```bash
npm --prefix ui ci
npm --prefix ui run dev
# Open http://127.0.0.1:5173
npm --prefix ui run check
npm --prefix ui exec playwright install chromium
npm --prefix ui run test:browser
npm --prefix ui run format:check
bash scripts/check.sh
```

The preview includes:

- A searchable set picker, immediately followed by density and Draw district.
- Concave polygon drawing, select/reselect, redraw with cancellation, delete and
  bounded preview undo. Overlapping districts are deliberately allowed.
- District-local working sets; `*` compares against the saved baseline. Save as
  creates a reusable set, Save updates a custom set, and built-ins stay protected.
  Saving a library entry never rewrites other districts' settings or baselines.
- City climate defaults, per-field set inheritance/overrides, and an explicit
  manual fallback when the host cannot supply stable game climate.
- Add/remove vegetation assets, age inclusion/density and nested profile settings.
- Local persistence, JSON import/export, invalid-input feedback, import validation
  and recovery without silently overwriting unreadable browser data.
- New preview city retains the reusable set library. Export before resetting a
  city to retain its districts. Preview undo is session-local and limited to 30
  edits; it is not the future game undo system.

Generate, Regenerate, Ungenerate and Bake + delete are disabled with explanations.
The generation-mode field is saved but schedules no work. No plants are drawn in
the preview. Its grid coordinates are illustrative, not game coordinates or a
terrain model. The preview's buttons and panels are browser stand-ins; game-native
focus, styling, tool input and scaling need Windows verification.

## Files and ownership

| Location | Responsibility |
|---|---|
| `data/placeholder-catalog.v1.json` | Fictional asset/age profiles and example sets; no real tree estimates |
| `ui/src/domain/schema.ts` | Runtime validation and inferred TypeScript types |
| `data/*.v1.schema.json` | Exported JSON Schemas for tooling and future host readers |
| `ui/src/domain/store.ts` | Pure reference command semantics, snapshots, inheritance, polygon validation |
| `ui/src/editor/` | Shared editor; host and UI primitives supplied at the boundary |
| `ui/src/preview/` | Browser-only drawing, storage, import/export and undo host |
| `ui/src/game/` | Native CS2 controls, UIL entry icon, binding/command adapter |

Profiles are JSON data. Asset keys are stable textual identifiers, not runtime
Entity indices. Placeholder keys start with `placeholder:`. Their ages, footprints,
response curves, set membership and density values come from the catalog rather
than branches keyed on species names. UI numeric ranges are validation bounds,
not botanical defaults. Reset asset copies the catalog data into the working set.
Users save whole sets, never separate asset profiles.

The initial response editor uses a minimum/optimum/maximum triangle for both
slope and temperature. This is a provisional authoring representation, not a
commitment to a final ecological curve model. No generator consumes it yet.
Units are explicit: metres, degrees Celsius, degrees from horizontal, and percent.
The portable legacy C# TerrainRules is not used by this editor.

Regenerate the exported schemas after changing the runtime contract:

```bash
npm --prefix ui run schema
```

JSON Schema expresses shape and individual bounds. Runtime validators additionally
check minimum/optimum/maximum ordering, ordered elevation bounds, duplicate IDs
and polygon topology. Unknown schema versions are rejected; migrations must be
explicit. Do not equate passing JSON Schema validation with complete validation.

## Host protocol version 1

The proposed group is `TreeDistrict`. The native UI reads string value binding
`editor`, a JSON envelope:

```typescript
{
  protocolVersion: 1;
  visible: boolean;
  workspace: Workspace;
  selectedId: string | null;
  drawing: boolean;
  error: string | null;
}
```

The UI sends a JSON command string through trigger binding `command`. Settings
commands are the discriminated `Command` union in `store.ts`. Additional host/tool
commands are `togglePanel`, `selectDistrict { id }` and `beginDrawing { edit }`.
Drawing nodes and completion come from the host's map input system, not a React
DOM canvas in the game. Selecting a district must publish its ID and snapshot in
one envelope. Invalid or deleted selections must not leave stale edit targets.

The future C# host is authoritative. It must validate every command, assign or
validate unique IDs, execute commands in order, reject invalid changes without
mutating state, then republish the envelope. Never trust UI validation alone.
`saveSet.afterSaveLibraryId` makes Save-as-then-replace atomic: a failed save must
not discard the working set. Supply errors through the envelope. Do not invoke
ECS mutation directly from a UI callback.

Keep the city's reference defaults, districts, boundaries, baseline snapshots,
working settings, seeds and modes in the city save. Store custom library sets in
a separate versioned JSON document under the mod's user-data directory, written
atomically with recovery. Shipped catalog data is read-only. The preview exports
both sections together for convenience; browser localStorage is NOT the game's
persistence mechanism. Game asset availability, climate lookup, ECS save/load,
city transitions and atomic file writes remain host integration work.

`src/game/index.tsx` uses `cs2/ui` Panel, Button and Dropdown components, registers
under `Game` and `UniversalModMenu`, and uses
`coui://uil/Standard/Trees.svg`. Declare Unified Icon Library as a Paradox Mods
dependency when creating release metadata. No UIL assets are copied into this repo.
A missing host snapshot renders no editor and triggers no local fallback mutation.
The module exports `hasCSS` alongside its registrar.

`npm run build:game-contract` checks the narrow authored contract and emits a
smoke-test `.mjs`/`.css` pair under `artifacts/ui-contract`. React and CS2 modules
remain external. **This is not the official game build or a distributable mod.**
Replace `cs2-contract.d.ts` with installed official types and use the official UI
generator/webpack deployment before claiming compatibility. The narrow declarations
are deliberately not a vendored full SDK or proof that native controls work.

## Windows acceptance work

1. Generate the official C# and UI projects using the installed modding toolchain;
   preserve the postprocessor, framework, generated types and externals.
2. Implement the host above and the custom vegetation district tool: terrain-only
   raycasts, editable simple polygons, selection, cancellation and save/load.
3. Supply a stable climate baseline and actual asset enumeration. Keep researched
   vegetation profile values out of this step; placeholders remain explicit.
4. Replace the browser-only primitives with native controls (entry already exists),
   check dialogs, keyboard/gamepad focus, panel close behavior, input capture,
   scrolling, small resolutions and UI scale. Local text is currently English;
   add game localisation keys before release.
5. Verify district persistence, per-city resets, reusable library recovery and
   undo on a disposable city. Test multiple rapid edits and load another city.
6. Only after that, research real plant/age profiles and implement generation as
   separate milestones. No initial placement experiment is required by this phase.

References for the native bridge: [CS2 UI template declarations](https://github.com/CitiesSkylinesModding/StockModTemplatesDiffer/blob/main/ui/types/ui.d.ts),
[binding declarations](https://github.com/CitiesSkylinesModding/StockModTemplatesDiffer/blob/main/ui/types/api.d.ts),
[registration declarations](https://github.com/CitiesSkylinesModding/StockModTemplatesDiffer/blob/main/ui/types/modding.d.ts),
and [Unified Icon Library author guidance](https://github.com/algernon-A/UnifiedIconLibrary).
The installed Windows toolchain remains authoritative. Foliage modelling continues
to follow [the agreed reference design](foliage-reference-design.md).
