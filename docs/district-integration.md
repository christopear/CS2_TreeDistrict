# District-tool integration workstream

Status: preparation only. The local editor works; a real game tool and C# settings
host have not yet been implemented. Continue on `feat/district-tool-integration`.
This workstream must remain a draft until verified against the installed CS2
Modding Toolchain.

## Required environment

Continue on Windows with the installed game and its official Modding Toolchain.
Follow [Windows setup](windows-setup.md), then inspect the generated project,
installed game assemblies and UI declarations before choosing integration APIs.
Record game/toolchain versions here when available. Do not copy game DLLs into Git.

The existing browser preview is sufficient for UX iteration, but cannot establish
compatibility with area tools, input actions, entity selection or save serialization.
The current `cs2-contract.d.ts` is a narrow provisional UI contract, not an installed
SDK. Replace it with official declarations during integration.

## Implementation sequence

1. Generate the official mod/UI projects and verify an empty mod loads. Preserve
   the generated build imports, postprocessor and game-owned React externals.
2. Inspect the game's area/district tool implementation for reusable drawing,
   boundary overlays and selection behaviour. Prefer supported extension points;
   document which can be reused and which require a custom tool. Our vegetation
   districts remain independent of administrative districts. Do not assume vanilla
   district drawing has the same polygon/node model as the local preview.
3. Implement the authoritative C# settings host for the existing
   [editor command contract](ui-scaffold.md#host-protocol-version-1). Validate
   commands, maintain district snapshots and reusable set files, and publish
   selection/settings together. Keep placeholder profile values in JSON.
4. Connect the native panel's draw action to the vegetation district tool. Map
   committed boundaries and selection to stable district IDs. Support finish,
   cancel, reselect, redraw and delete. Use terrain heights/normals; no water
   exclusion or cross-district overlap prevention in v1.
5. Integrate city save/load and city-transition cleanup, plus undo using verified
   game facilities. Persist boundaries and working sets without needing the
   original reusable library entry. Never adopt manual vegetation.
6. Connect native focus, panel visibility, Escape/cancel and map-input suppression.
   Declare Unified Icon Library as a dependency in release configuration. Replace
   browser-only state/persistence with host bindings; keep the preview usable.

No tree generation, real tree-profile research, planting experiments or overlap
resolution belongs to this workstream. Generation actions stay unavailable.

## Acceptance on a disposable city

- Choose a set, draw a concave vegetation district, then reselect it in the same
  editor. Verify whether intermediate nodes or only completed shapes are available.
- Edit city defaults, per-set overrides and nested asset/age settings; save a custom
  set and use it for another district without changing the original snapshot.
- Cancel drawing and an existing boundary edit without leaving phantom districts
  or replacing a valid prior boundary. Delete/undo affects only the target district.
- Save/reload the city and load a different city: retain the correct districts and
  settings without leaking selection or transient drawing state between cities.
- Allow overlapping districts and underwater boundaries. Roads/buildings do not
  need placement queries yet because there is no vegetation generation.
- Verify UIL rendering, keyboard focus, scrolling and input capture; UI interaction
  must not place map nodes. Check vanilla district and landscaping tools still work.
- Run portable and UI tests, then record the separate in-game checks and limitations
  in the PR. Browser tests do not substitute for these checks.
