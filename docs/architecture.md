# Architecture and milestones

The [requirements](requirements.md), [reference design](foliage-reference-design.md)
and [UX design](ux-design.md) define the intended product. The current phase is
settings and district UX, with no vegetation generation.

## Current boundaries

- `data/`: versioned JSON placeholder profiles/sets plus exported schemas. Plant
  values are data, independent of the UI and eventual generator.
- `ui/src/domain`: validated data contracts and pure command semantics. Districts
  carry snapshots and comparison baselines; city climate is resolved at use time.
- `ui/src/editor`: shared settings editor, independent of storage and drawing host.
- `ui/src/preview`: browser-only test host for drawing, selection and persistence.
- `ui/src/game`: native CS2 controls and binding adapter with an original area icon.
- `game/TreeDistrict`: native AreaPrefab, settings validation, per-area snapshots
  and save references, plus a reusable JSON set library.
- `game/TreeDistrictUI`: installed official UI template, declarations and build.
- `src/TreeDistrict.Core`: existing game-independent C# terrain-rule foundation.
  Its legacy water flag is not a requirement for the future generator and is not
  used by the editor. The core must not reference Unity, UI or runtime ECS IDs.

See the [UI scaffold contract](ui-scaffold.md) for commands, ownership, persistence
and exact verification limits. The preview is not a game adapter or terrain model.

## Game host responsibilities

Create independent vegetation districts, rather than attaching this feature to
administrative districts. The host owns tool input, terrain-only raycasts, polygon
editing, selection, authoritative validation, city persistence, reusable set files,
undo and native UI bindings. Use the official mod template and postprocessor.
Profile/set files are JSON; entity ownership and city data use verified game save
hooks. Do not save runtime Entity indices as stable vegetation asset identities.

A field edit immediately changes district settings, not the reusable set library.
Generation is on demand by default. Auto mode will be event-driven and coalesced
when generation exists; currently it only persists a setting. Never claim manual
vegetation. Overlaps are allowed and left to the user in v1, with conflict handling
deferred to v2. Ownership must still identify one creating district per plant.

## Milestones

1. **Portable foundation — implemented:** C# library, legacy terrain rules and
   Linux/Windows tests; settings schemas and data-driven placeholder catalog.
2. **UX scaffold — implemented locally:** set-first editor, city inheritance,
   nested asset/age controls, draw/select/redraw/delete preview, custom set saving,
   import/export, native UI entry and command contract. Game host now implemented.
3. **Windows integration — in progress:** official template build, real district tool,
   host bindings, asset enumeration, stable climate, save/load and input verification.
   No generation is needed to validate this stage.
4. **Dedicated profile research:** estimate real asset/age footprints and responses;
   replace clearly marked fixtures with researched data and provenance.
5. **Generator:** implement the agreed simplified reference pipeline, deterministic
   sampling, terrain/obstacle validation, owned entities, bounded jobs and generation
   lifecycle. No water-specific exclusion or overlap resolution in v1.

Future generation should snapshot world/settings inputs, plan bounded candidates,
revalidate and apply owned entities in batches. Only the newest district revision
may commit. Preserve unrelated plants and last valid generation during failure.
No simulation or placement logic has been added as part of the UX milestone.
