# Architecture and first milestones

The [product requirements draft](requirements.md) is the current design discussion.
Use the [foliage reference design](foliage-reference-design.md) for selected source
models, input contracts and the proposed generator pipeline.
The milestones below predate that draft and will be revised once its decisions
are agreed; they are not a complete statement of first-release scope.

## Boundaries

`src/TreeDistrict.Core` owns game-independent planning and validation. It must not
reference Unity, game DLLs, UI code, filesystem paths or live ECS entities.
World coordinates and elevations use metres; slopes use degrees from horizontal.
Terrain limits are inclusive. Invalid samples and water/obstacles are rejected.

The future `game/TreeDistrict` adapter will own input tools, terrain/obstacle
sampling, entity creation, undo/removal, save/load and UI bindings. Its official
toolchain must remain authoritative over game builds. The core is intentionally
small today: only terrain eligibility has been implemented.

Vegetation areas should be independent of administrative districts. Start with
explicit preview/apply/regenerate actions; automatic maintenance is later work.
Existing manually placed vegetation must never be implicitly adopted or removed.

## Planned data and generation

An area needs an ID, polygon, species weights, density, terrain rules, seed and
generator/schema versions. Persist stable prefab identifiers rather than runtime
entity IDs. Missing assets need a visible, recoverable policy.

Use a deterministic sampler with an explicitly specified random algorithm. Do not
depend on `System.Random` producing the same sequence across runtime upgrades.
Combine minimum spacing with patch-scale density variation and species clustering.
Preserve area membership for every generated entity so clearing one area cannot
delete unrelated trees. Resolve overlap ownership explicitly before supporting
overlapping areas. Save/load and undo are part of the feature, not polish.

The adapter should snapshot terrain inputs, plan placements, preview the count,
then apply bounded batches with cancellation. Use a spatial index for proximity
checks, cap candidate attempts and total placements, and avoid rescanning the map
each frame. Ordinary game trees still have ordinary rendering/simulation costs.

## Milestones

1. **Foundation (present):** portable library, terrain-rule tests, reproducible
   restore/build/format checks and CI on Linux and Windows.
2. **Generator prototype:** polygon validation/containment, deterministic sampling,
   two weighted species, density and minimum spacing on synthetic terrain.
   Test concave boundaries, exclusion rules, reproducibility and bounded work.
3. **Game spike:** official template loads; place and clear a small owned batch;
   verify core assembly loading and save/reload before building a large UI.
4. **First playable version:** editable polygon, settings, preview, apply, clear,
   explicit regeneration and persistence. Protect manual trees and obstacles.
5. **Natural woodland:** patches, clearings, edge thinning, per-species terrain
   preferences, shrubs, age variation, and profiling at larger scales.

Unresolved integration details must be checked against the installed game: area
editing APIs, prefab enumeration, terrain sampling, obstacle queries, persistence
hooks and compatibility with forestry-industry areas. No API names are assumed
from screenshots or older CS1/BepInEx examples.
