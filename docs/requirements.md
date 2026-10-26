# TreeDistrict — draft product requirements

Status: discussion draft, 2026-10-26. This describes intended behaviour, not
implemented functionality. Recommendations below remain open to revision.

Selected references and the proposed technical baseline are documented in the
[foliage reference research and design report](foliage-reference-design.md).
That report narrows open density/climate/competition choices to concrete proposals
for prototyping; those proposals still need validation.

## Goal and division of control

Let the player design vegetation communities and their boundaries, then generate
natural-looking vegetation within those boundaries using editable environmental
preferences. Automate individual placement, local species selection and density
variation. Keep landscape composition under human control.

The player decides where woodland, scrub and lakeside planting belong. The mod
does not infer biomes, identify shorelines for special planting, choose unselected
species, or spread vegetation beyond an area. Water detection for exclusion is
required; shoreline classification for choosing vegetation is out of scope.

Environmental adaptation describes how a generated layout responds to terrain.
It does not imply ongoing ecological simulation, succession or automatic regrowth.

## Direction established in discussion

- Vegetation profiles have editable defaults for environmental preferences.
- Stable map climate and elevation jointly determine local climate suitability;
  this is unrelated to momentary weather.
- Trees and shrubs belong to one community. Their placement can interact through
  shade, available space and species-specific terrain preferences. Independent
  tree/shrub density layers are not an agreed requirement.
- Players combine vegetation into reusable groups with per-member abundance.
- Groups support spatial variation such as clumping.
- Vegetation must avoid water, roads, railways, buildings and placed objects.
- Presets should be reusable across saved cities; a favourite/default set should
  make starting a new area quick.
- Different landscape communities, including shoreline vegetation, are drawn by
  the player as separate areas.
- Ordinary manual placement and brushes remain useful and must keep working.

The following sections propose concrete behaviour for those requirements.

## Three editable levels

### Vegetation profile: how one plant behaves

Each supported vegetation asset has a stable identifier and defaults for:

- Climate suitability, ranging from unsuitable to fully suitable.
- Slope suitability, with an optional absolute cutoff.
- Physical footprint, approximate canopy extent and shade tolerance.
- Optional elevation constraints for explicit art direction or non-temperature
  effects; avoid applying the same thermal restriction twice.

Provide useful defaults and a reset action. Treat built-in profiles as editable
artistic presets, not authoritative botanical models. Broad categories such as
"oak" and "shrub" must not imply precise universal ecological tolerances.
Unknown/custom assets need a visible fallback profile and manual editing support.

Proposed basic curve editor: four ordered environmental values — lower limit, start
of preferred band, end of preferred band, upper limit. Suitability rises from 0
to 1, stays at 1, then falls to 0. Unequal transition widths allow a long uphill
tail without asking players to understand probability distributions. For climate
preferences the horizontal axis is temperature rather than raw elevation. A compact
graph makes the result visible. More flexible curves can come later.

### Vegetation set: what a community contains

A named set contains selected assets, per-species abundance/density defaults and
shared spatial controls. Trees and shrubs are members of the same community.

Proposed UX: one overall density multiplier, with editable per-species abundance
in the advanced editor. Whether abundance means candidate counts per hectare or
relative weights remains open: a shrub and a mature tree occupy different amounts
of space, so a flat percentage model may be misleading. Show estimated per-species
counts. Ratios may change with terrain, shade, spacing and competition.

Keep the initial spatial controls small: clumping strength and patch size.
Physical footprint and shade influence must be distinct: a shrub may coexist
under a canopy without intersecting a trunk. Use species-specific shade responses,
not a blanket "trees win, shrubs disappear" rule. Multi-pass placement may be an
implementation technique; it does not require separate user-facing communities.

Named presets can be created, duplicated, edited and deleted. A favourite/default
set is preselected for new areas. Save edited area settings as a new set.

### Vegetation area: where it applies

An area contains an editable outer boundary, exclusion holes, a set snapshot,
local overrides, a random seed and ownership information for generated plants.
Areas are independent of administrative districts.

Ordinary workflow: choose a set, draw the boundary, inspect a preview, apply.
Initially show only the set, overall density and clumping controls. Reveal
species and terrain preferences through an advanced editor.

Exclusion holes are the first mechanism for deliberate clearings and preserving
specific open land. Clumping creates incidental low-density patches. A dedicated
procedural-clearings control is optional later work, not essential first-version UI.

## Environmental adaptation and density

Default recommendation: terrain-aware generation. Provide a simple "Use terrain
preferences" toggle for players who want a consistent selected mix instead.
Turning it off never bypasses water, obstacle or placement-safety checks.

Interpret the environmental curve as a suitability multiplier, not as a probability
distribution over altitude: a map may contain very different amounts of land at
different elevations. Suitability changes the chance of placement at a location.

For each selected species, conceptually (a proposed model, not a fixed algorithm):

    local contribution of species i
      = base candidate density of i × overall density multiplier
        × environmental suitability of i at this location
        × response to local shade/space competition
        × spatial density variation

Do not automatically force a fixed total count after terrain filtering. However,
do not prohibit replacement either: shrubs may occupy space unsuitable for trees
or benefit from reduced canopy shade. Let species preferences and interactions
determine that outcome. All selected species being unsuitable must permit bare
ground. The earlier independent-layer formula is superseded by this discussion.

Spacing, obstacles and placement limits may reduce achieved density further. Do
not compensate by cramming plants into the remaining usable land. Show requested
versus estimated achieved counts, and explain when terrain rules reject most of
the area. Density units should be plants per hectare with a simpler sparse/dense
slider presentation; a final mapping needs visual tuning.

Proposed climate proxy:

    local baseline temperature
      = map reference temperature
        - cooling per kilometre × (height - reference height) / 1000

All quantities have explicit units and editable defaults. Use stable map climate
metadata if suitable data is available; otherwise expose a manual reference
temperature and height. Inspect whether game-provided temperatures already include
elevation before applying a correction. Save these inputs with the city.

Two sites with matching derived temperatures should receive matching thermal
suitability, even on maps with different reference climates. Other factors may
still differ. This is an artistic climate proxy, not a full ecological equivalence.
The cooling coefficient and exact climate statistic remain to be chosen. No live
weather or seasonal replacement is implied. Temperature is part of the proposed
core model, not automatically deferred to a later release.

## Placement, ownership and editing

Never place vegetation intersecting excluded water or occupied infrastructure.
Use spatial extent and clearance, not just a test of the trunk's centre point.
Road and rail exclusions include their occupied corridor. Exact footprint and
clearance behaviour must be verified in the game, including bridges and tunnels;
an underground tunnel should not automatically sterilise the surface above it.

Generated vegetation belongs to its area. Clearing or regenerating an area must
only affect its own plants. Existing manual vegetation is preserved and considered
when spacing new placements. Overlapping areas must not double-populate land.
Proposed first-version policy: show overlapping interiors as invalid until the
player trims them or creates a hole; touching boundaries remain valid.

Proposed lifecycle:

1. Preview generation without altering the city; show estimated counts.
2. Apply in bounded, cancellable batches, with undo for the operation.
3. Parameter or boundary edits create a pending preview; Apply confirms changes.
4. Regenerate uses the saved seed; "New variation" explicitly changes it.
5. Deleting an area offers either removing its plants or keeping them as ordinary,
   unmanaged vegetation. These must be distinct, clearly labelled actions.

No periodic refilling of manually bulldozed trees. New roads/buildings must not
leave generated vegetation intersecting them: use normal game removal if adequate,
otherwise clean up affected owned plants. This cleanup must never add replacements.
Terrain edits and water changes also need an invalid-placement policy. Temporary
flooding versus permanent water change is an open decision; no per-frame whole-map
regeneration is acceptable.

## Persistence and sharing between cities

City saves retain area geometry, holes, effective set/profile settings, seed,
schema/generator version, and plant ownership. Reloading must not reroll a layout.
Use stable asset identifiers; runtime entity identifiers alone are insufficient.

A separate local preset library is available across cities on the same machine.
Each area keeps a snapshot: editing a library preset never silently changes
existing areas or cities. Updating an area from a preset is explicit and previewed.

Export/import of versioned preset files is desirable for sharing and dual-boot
use; automatic cloud/account sync is out of scope. A preset contains references
and settings, not asset files. Missing assets are reported; do not silently replace
them or redistribute their abundance to other species. A saved city should retain
the unresolved settings so an asset can be restored later.

## First playable scope and acceptance examples

The first playable release should include editable areas with holes, reusable
sets, climate/elevation and slope suitability, interacting vegetation, basic clumping, obstacle
avoidance, previews, explicit application, ownership, undo and persistence.
Dynamic weather simulation, automatic biome detection, succession, seasonal
replacement and ecological regrowth are excluded.

- A forest spanning a hill changes mix and thins uphill according to its profiles;
  a uniformly unsuitable summit stays empty instead of filling with the last species.
- Matching derived temperatures on two different maps produce the same thermal
  suitability for the same species, with other inputs held equal.
- A selected shrub with greater slope tolerance can remain where selected trees
  are rejected. Shade-tolerant shrubs can coexist with canopy where space allows.
- Disabling terrain preferences produces the base mix, subject to spacing and
  exclusions; it does not permit planting in a lake or through a road.
- An exclusion hole remains unplanted through generation, save/load and edits.
- A separately drawn shoreline area uses only its selected vegetation.
- Adding a road leaves no intersecting generated plants, and removing the road
  does not immediately regrow them.
- Clearing an area leaves manually planted park trees intact.
- A preset saved in city A can be used in city B. Editing the preset leaves both
  cities' existing areas unchanged until explicitly updated.
- Preview and application agree for the same inputs; changes to the world between
  them trigger revalidation. Identical inputs, seed and generator version reproduce
  the plan. Save/load preserves the actual placed entities and manual removals.
- Large areas have bounded work, cancellation and a visible placement cap. On
  reaching a cap, explain it rather than silently reducing the requested density.

## Decisions to resolve before implementation

1. Which per-species density and competition policy gives predictable controls
   while permitting undergrowth and natural changes in composition?
2. Accept explicit Apply/regenerate, with automatic removal only for conflicts?
3. Is rejecting overlapping interiors acceptable initially, or is area priority
   important enough to implement from the start?
4. Which stable climate input and cooling coefficient should the proxy use?
5. Which elevation reference and water-change policy best match actual gameplay?

Performance budgets and exact supported obstacle categories require a game spike.
The existing TerrainRules class is only a hard eligibility filter; soft species
suitability and these product behaviours have not been implemented yet.

## Related work informing the proposal

The following is the initial survey. The selected build references are now Unreal
Procedural Foliage and EcoBrush; see the [reference report](foliage-reference-design.md).
EcoSys and Silva remain background, not additional implementation targets.

- [Deussen et al., Realistic Modeling and Rendering of Plant Ecosystems (1998)](https://graphics.stanford.edu/papers/ecosys/):
  combines manual landscape design with plant/environment interaction modelling.
- [Unreal Engine Procedural Foliage](https://dev.epicgames.com/documentation/unreal-engine/procedural-foliage-tool-in-unreal-engine):
  uses collision/shade concepts, shade-related placement settings and overlap
  priority. A practical reference for compact controls, not a CS2 dependency.
- [Silva: Generating and Rendering Large Scale Tiled Plant Populations (2013)](https://www.jvrb.org/jvrb/en/article/view/10.2013.1):
  considers environmental factors and plant neighbourhoods in a configurable
  generation pipeline. Its rendering/tiling machinery is outside this mod's scope.

These establish useful precedents, not botanical validation of our proposed
defaults. The first implementation should approximate local conditions and
neighbour interactions without requiring a time-stepped ecosystem simulation.
