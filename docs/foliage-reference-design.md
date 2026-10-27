# Procedural foliage references and TreeDistrict design

Version: 0.3 — researched 2026-10-26; scope revised after user review.

Revision: one concave district boundary, no interior-hole editor or water checks;
city climate defaults with set overrides; age-specific values saved only in sets;
set-first shared editor; default on-demand plus opt-in auto generation; ungenerate.
The [agreed UX](ux-design.md) controls interaction semantics.
Sections 2–3 describe the original references and retain their water-related facts.

**Decision:** Unreal Engine Procedural Foliage is our primary behavioural reference.
EcoBrush (Gain, Long, Cordonnier and Cani, 2017) is our supporting environmental
reference. TreeDistrict will implement its own simplified C# generator; neither
Unreal nor EcoBrush is a runtime dependency.

This report defines the reference baseline for subsequent work. Source findings
in sections 2–3 are separated from our proposed design in sections 4–9. Proposed
parameters are not botanical facts, confirmed CS2 APIs, or implemented features.
The [requirements](requirements.md) retain the wider product behaviour.

## 1. Why these references

| Reference | Role | Why it fits | Boundary of adoption |
|---|---|---|---|
| Unreal Procedural Foliage | Primary: bounded generation, species configuration, local competition | Closest practical model for a reusable vegetation set applied to a drawn area | Reference behaviour, not engine code or a literal port |
| EcoBrush | Supporting: terrain-conditioned communities | Addresses the climate/elevation and neighbouring-plant questions missing from a simple scatter tool | Borrow environmental reasoning; avoid its full simulation/database machinery |

These roles are deliberately complementary. A placement tool alone would leave
climate unexplained; a full ecosystem framework would require more inputs and
calibration than this mod needs. Our district is the player's design boundary,
not an automatically inferred biome.

Deussen's EcoSys and Silva remain background reading, not additional implementation
targets. ParkManager and Tree Controller remain CS2 integration references only.
Do not silently introduce a third algorithmic reference during implementation:
record a new decision here, with the problem it solves and its input cost.

## 2. Primary reference: Unreal Procedural Foliage

### 2.1 Documented workflow

An author defines foliage types, assigns them to a spawner volume, and explicitly
resimulates. Virtual seeds spread and age; overlapping candidates compete using
collision/shade extents and priority. Multiple types share the spawner. Blocking
volumes remove planting regions. This is an authoring operation, not a requirement
for continuous gameplay simulation. See the official [tool walkthrough, sections
2–5][U1].

### 2.2 Input inventory

The following are relevant exposed fields, not a claim that authors must manually
set every field. Defaults exist. Rendering-only settings are omitted.

| Scope | Inputs and meanings | Evidence |
|---|---|---|
| Region | Spawner volume, selected foliage assets, blocking volumes | [Walkthrough][U1] |
| Spawner | `RandomSeed`; `TileSize` (cm); `NumUniqueTiles`; `MinimumQuadTreeSize`; foliage types; optional terrain-material restriction | [Spawner API][U3] |
| Initial population | `InitialSeedDensity` (seeds along 10 m); `DistributionSeed` | [Type API][U2] |
| Propagation | `NumSteps`; `SeedsPerStep`; `AverageSpreadDistance`; `SpreadVariance`; `MaxInitialSeedOffset` | [Type API][U2] |
| Competition | `CollisionRadius`; `ShadeRadius`; `OverlapPriority` | [Type API][U2] |
| Shade | `bCanGrowInShade`: ignore other types' shade-radius tests; `bSpawnsInShade`: shade-only spawning | [Type API][U2] |
| Growth | `MaxInitialAge`; `MaxAge`; `ProceduralScale`; `ScaleCurve` | [Type API][U2] |
| Surface | `GroundSlopeAngle`; `Height` (world Z); `CollisionWithWorld`; `CollisionScale`; landscape-layer filters | [Type API][U2] |
| Pose | Normal alignment, random orientation, offsets | [Type API][U2] |

**Interpretation traps:** procedural seed density differs from painting `Density`;
shade permission differs from shade-only spawning. The shared type API contains
painting and procedural fields: they are not all equivalent controls. [U2]

**Research limit:** documentation was inspected, not an Unreal source checkout or
a running test scene. Exact tie-breaking, boundary stitching and interaction order
are not verified. We must define and test our own contracts for those behaviours.
The inspected documentation identifies itself as Unreal Engine 5.8.

## 3. Supporting reference: EcoBrush

### 3.1 Method and inputs

This inventory covers authoring inputs and major model controls; it is not a
reproduction specification for every numerical constant in the research system.

EcoBrush derives environmental maps, simulates small plant communities, analyses
their spatial distributions, then synthesises larger landscapes from those
distributions. Its editing controls operate on age, density and variability.
See [paper §§3–7][E1].

| Input category | Paper's inputs |
|---|---|
| Terrain | Heightfield, latitude, compass direction |
| Climate | Winter/summer monthly-average daily maxima at the lowest elevation; cooling rate; monthly rainfall |
| Soil | Absorption limit, reservoir capacity; slope-dependent absorption |
| Plants | Functional types; temperature/light/moisture/slope response curves; growth, maturity, longevity, propagation; height-related canopy/root extents; light transmission |
| Synthesis | Simulation-derived distributions, environmental conditions, ecosystem age |

Temperature is elevation-corrected; the example default is 6.5°C/km. Sunlight uses
terrain shadowing. Water modelling includes runoff and riverbank effects.
Piecewise-linear viability ranges from −1 to +1; the minimum factor controls vigour.
Canopy and root competition affect light and moisture separately. See [§§4–5,
figures 4–5][E1].

**Calibration limit:** Table 2 provides relative response rankings, not a complete
set of numeric species curves. Its temperature statistic is not annual mean.
Do not transplant these rankings as degrees or claim an exact reproduction.
See [Table 2, p.73][E1].

### 3.2 Consequence for this project

We need to author and version our own asset profiles. A label such as "oak" is
insufficient to infer exact limits automatically. Start with a small, visibly
labelled set of tuned defaults, allow editing, and expand after visual review.

We will not require rainfall histories, soil calibration, river modelling or a
precomputed ecosystem database. This means our output will be an artistic
approximation, with a narrower claim than a reproduced ecosystem simulation.

## 4. Proposed adaptation decisions

These are TreeDistrict design choices informed by the references, not statements
about what either reference implements.

| ID | Decision | Reason / tradeoff |
|---|---|---|
| D01 | One simple, possibly concave polygon defines the planting domain | C/U shapes and cut-open rings express the desired extent; no interior rings |
| D02 | One community contains every selected tree and shrub | No mandatory independent tree/shrub tools or density layers |
| D03 | Use stable climate plus elevation and slope | Meets cross-map climate intent without live-weather coupling |
| D04 | Separate hard occupied space from soft canopy influence | Allows undergrowth without allowing trunk/object intersections |
| D05 | Generate a static layout with bounded work | Avoids reproduction, mortality and years of ecosystem simulation |
| D06 | One overall density multiplier; per-species candidate density underneath | Gives comparable controls without forcing shrubs and trees into a fixed percentage quota |
| D07 | Use clumping strength and patch size | Avoid exposing propagation steps, seed counts and simulated age just to adjust appearance |
| D08 | Immediate district settings; on-demand default or opt-in auto generation; saved seed | Separate editing, library saving and generated state |
| D09 | Do not replace missing or unsuitable species to meet a total count | Bare ground is valid; suitable shrubs can still occupy available space |
| D10 | Keep moisture and terrain-sun exposure out of the initial model | Player-defined communities handle shorelines; canopy shade is only a local proxy |

Revisit D06 after a small visual prototype: candidate density is a request for
placement opportunities, not a promise of achieved plants per hectare. The preview
must make this distinction clear.

## 5. TreeDistrict input contract

All distances are metres, areas hectares where labelled, temperatures Celsius,
and slopes degrees from horizontal. No Unreal centimetre values cross the adapter
boundary without conversion. Fields below are proposed interfaces, not existing
classes. Reject NaN/infinity and out-of-range values explicitly.

### 5.1 Per-city environment

| Input | Source / fallback | Exposure and validation |
|---|---|---|
| Terrain height and normal | CS2 adapter, API unverified | Internal; convert to one declared coordinate convention; unavailable samples reject placement |
| Occupied geometry / clearance query | CS2 adapter, API unverified | Must cover roads, railways, buildings and ploppables; account for vertical separation |
| Existing plants: position, footprint, height, canopy | Game entities plus profile catalogue | Preserve them; include relevant neighbours even outside the area |
| `ReferenceTemperatureC` | Stable game climate or manual override | Show statistic, elevation and source; never current weather |
| `ReferenceElevationM` | Explicit elevation for that temperature | City-level; not an unlabelled assumption that world zero is sea level |
| `CoolingCPerKm` | Editable project default | City-level, finite and nonnegative; zero supports constant climate |
| Climate statistic/version | Declared by our profile schema | Prevent mixing annual-mean profiles with seasonal-maximum inputs |

**Recommended first statistic:** annual-mean temperature as an artistic proxy,
with profiles authored for that statistic. **Proposed starting cooling default:**
6.5°C/km, editable and subject to visual tuning. This is our reduced model, not
EcoBrush's full temperature calculation. User-provided reference values are
authoritative when overridden. Compatible stable map climate may otherwise supply
the reference value; current weather is never a generator input.

Do not apply an elevation correction to data already corrected for that location.
For reference height `h0` and candidate height `h`:

```text
T(h) = T0 - coolingCPerKm * (h - h0) / 1000
```

Example under that chosen default: 20°C at reference elevation becomes 13.5°C
1000 m above it. A second map with a 13.5°C reference location should give the same
thermal score there, holding all other inputs equal. This is a model consistency
test, not a claim that the locations have identical real-world ecosystems.

Each set inherits these city values unless it stores an explicit per-field override.
Inherited values resolve for the current city. Effective changes queue auto-mode
updates or mark on-demand districts stale. Save flags and values separately.

### 5.2 Per-asset vegetation profile

| Input | Meaning | Source / validation |
|---|---|---|
| Stable asset ID; profile version | Resolve vegetation and preserve settings | Adapter/catalogue; missing ID remains unresolved, never guessed |
| Temperature response | Four-point, 0–1 suitability curve in °C | Authored default; advanced editor; strictly ordered points initially |
| Slope response | 0–1 response over 0–90° | Authored default; flat-ground support must not need a negative slope |
| Hard placement limits | Optional explicit elevation/slope restrictions | Advanced override; always separate from soft preference |
| Physical footprint | Occupied region used for collision | Conservative asset estimate plus editable correction; a trunk-only disc is insufficient for all ploppables |
| Representative height | Relative canopy ordering for the chosen variant | Asset estimate/profile, >0; avoid arbitrary trees-first category sorting |
| Canopy radius and opacity | Extent and strength of approximate shade | Profile; radius ≥0, opacity in [0,1] |
| Shade response | 0–1 suitability versus canopy coverage | Profile; tolerating shade and requiring shade are distinct curves |
| Density default | Candidate opportunities per hectare in suitable conditions | Profile; finite and ≥0 |
| Appearance choices | Allowed game-supported ages/variants, age weights and rotations | Choose age before placement tests; use that age's estimated physical and canopy properties |

These profiles are internal defaults and inline set values, not independently
savable user presets. Only whole sets are saved. Shrubs use the same fields. A shrub can have a different slope or shade response
without receiving a universal "shrubs survive anything" exemption. No roots,
reproduction rates, lifespan, or arbitrary biological growth simulation are needed.

### Age-specific parameter estimates

Resolve each asset to available game age states. A species profile provides shared
preferences; each age state has estimated height, physical footprint, canopy
radius/opacity and optional preference overrides. Record whether each estimate is
measured from an asset, manually tuned, or inherited. Do not assume all properties
scale linearly with height. Allow age mixtures in a set, normalize their weights
within that species, and choose the age before checking shade and clearance.
Changing age weights must not multiply the species' candidate density.

### 5.3 Per-set and per-area inputs

| Scope | Stored inputs | Normal UI |
|---|---|---|
| Reusable set | ID/version/name, asset list, per-species density multipliers, inline age/profile values, climate overrides, density/clumping defaults | Set picker; advanced species editor |
| Area | Simple polygon, set snapshot, overrides, seed, generator version, generation mode/revision | Same panel for creation and editing |
| Spatial variation | Clumping strength [0,1], patch scale >0 m | Two controls; default to preset values |
| Execution | Placement and candidate caps, cancellation, chunk budget | Internal limits; show count/cap warnings |
| Persistence | Ownership records, actual placed entities, saved climate inputs and profile snapshots | Save/load; explicit preset updates |

Density percentages are independent multipliers, not shares that sum to 100%:
100% retains the profile default, 50% halves it, 0% disables it. For species i:
`candidateDensity_i = defaultDensity_i × speciesPercent/100 × overallPercent/100`.
Climate, slope, shade, spacing and exclusions then reduce accepted placements.
Species with small footprints can have different default counts from large trees.
Set-wide hard limits intersect species limits; they never loosen obstacle checks.
A district may override overall density without changing the saved set.

Changing a shared preset does not rewrite existing areas. Cross-city import carries
settings and asset references, not meshes. Save stable input versions with a plan.

## 6. Proposed generation pipeline

### Population contracts: a 200 m by 200 m test plot

`tests/fixtures/set-population.v1.json` records versioned, hand-calculated
population scenarios. These are model expectations, not field surveys or Unreal
output samples. `SetPopulationTests.cs` exercises the actual current planner;
`test_population_contracts.py` checks fixture arithmetic and independent density
semantics. Passing fixture checks does **not** mean mixed generation works.

The controlled plot is flat, empty, at the species' optimum temperature, with
clumping disabled and no shade/competition unless a scenario explicitly says so.
The main plot is 40,000 m², or 4 hectares. The initial reference normalization is
`opportunities/ha = 10,000 / pitch²`, where `pitch = 1.25 × matureSpacingM`.
This matches the prototype's density scale without requiring future placement to
use a grid. It is not a packing optimum or a claim that crowns cannot overlap.
The 1.25 allowance is provisional; changing it requires a deliberate contract
version change, not silently updating expectations to match generator output.

| Test profile | Spacing proxy | Reference pitch | Baseline opportunities/ha | Opportunities in 4 ha |
|---|---:|---:|---:|---:|
| Compact field maple (game London Plane) | 8 m | 10 m | 100 | 400 |
| Silver birch | 12 m | 15 m | 44.44 | 178 |
| Oak | 16 m | 20 m | 25 | 100 |

Birch and oak spacings are explicit test proxies, not botanical measurements or
new runtime defaults. Candidate counts are independent: adding birch does not
halve maple's request. A maple/birch set at 100% requests 400/178 candidates;
adding oak requests another 100. Halving overall density halves each request.
At 60% overall, with maple/birch/oak at 50%/25%/100%, requests are 120/27/60.
Age proportions divide each species' population after placement and do not add
extra candidate populations.

Fixtures distinguish **candidate budgets** from **uncontested final counts**.
Uniform 50% suitability or 50% eligible area halves expected uncontested counts.
With both species at 100% and overall density at 50%, then uniform suitability
at 50%, the uncontested maple/birch means are 100/44, with engineering bands
60–140 and 17–72. These are deliberately isolated tests with interactions
disabled; they are not predictions for a competing mixed forest.

Current planner tests run eight fixed, varied seeds. Each run must fall within
`mean ± max(10% of mean, 4 × sqrt(mean))`, rounded outward. The eight-seed average
uses `mean ± max(3% of mean, 4 × sqrt(mean / 8))`. The square-root allowance is a
conservative Poisson-scale tolerance for small populations; the relative floor
allows boundary/placement differences. These are engineering bands, not ecological
confidence intervals. Expected zero is exactly zero. Age counts must be within
one tree of their requested proportions. Existing containment and spacing tests
remain complementary. A quarter-area case checks that counts scale with area.

The 200 m plot avoids today's 10,000-tree safety cap. Scaling the three-species
baseline to 1 km² would request about 16,944 candidates before competition. A
future planner must expose requested, rejected and accepted counts rather than
silently truncate or force a quota. All-species-zero currently throws: an
explicitly skipped contract records the desired empty-plan behaviour. Mixed
planner tests are explicitly skipped until it supports asset IDs and stage counts,
never emulated by a toy generator merely to obtain green tests.

When mixed placement is implemented, connect the fixtures through a test adapter
and enable the skipped tests. Then add shared-space competition experiments:

- Equal-footprint/equal-height species should not gain an advantage from list
  order; permuting input order must preserve results by stable asset ID.
- A disabled or entirely unsuitable species must not change another's candidates.
- Accepted plus rejected counts must reconcile with candidates, by species and
  rejection reason; rejected candidates must not be redistributed as a quota.
- Age-only edits must preserve species counts and positions under mature spacing.
- Test taller-neighbour shade, mixed-footprint exclusion and increasing density
  separately. Do not demand linear final counts once competition saturates space.
- Derive final-count bands only after choosing pairwise exclusion, conflict order
  and the shade response. Compare against analytical bounds or independently
  derived controlled arrangements, not snapshots of the implementation under test.

This deliberately leaves realistic shared-space final populations undecided:
spacing alone cannot tell us how many maples survive beneath birch canopy.
Density normalization is a proposed calibration target; species competition is
still an open model choice.

This is an original simplification to prototype. It is not the Unreal seed-growth
algorithm or EcoBrush's statistical synthesis algorithm.

```mermaid
flowchart LR
    A[Area and community snapshot] --> B[Read terrain and exclusions]
    B --> C[Generate deterministic candidates]
    C --> D[Climate and slope suitability]
    D --> E[Space and canopy interactions]
    E --> F[Preview counts and rejection reasons]
    F --> G[Revalidate and apply owned plants]
```

1. Validate the simple polygon and profile references. Capture immutable settings
   and a world snapshot/version. Overlapping districts are allowed in v1 and are
   the user's responsibility; cross-district conflict resolution is deferred to v2.
   Each future instance still belongs exclusively to its creating district.
2. Generate bounded candidate opportunities for each selected species over the
   whole domain. Use metres and candidate density, with deterministic stochastic
   rounding of counts. Sample a shared spatial patch field plus optional
   species-specific variation; document its normalization before implementing it.
3. Reject hard exclusions first. Evaluate climate/slope using 0–1 curves. Proposed
   environmental score: `min(thermalSuitability, slopeSuitability)`. This avoids
   compounded weakening from several mildly unsuitable inputs; compare against a
   product model during tuning. A zero score must reject placement.
4. Resolve occupied-space conflicts and canopy influence using a spatial index.
   Start by testing descending representative height, with a seeded tie-break.
   Every asset uses the same rule; stature, not "tree" versus "shrub", determines
   order. Existing manual plants are immutable constraints.
5. Approximate shade from accepted taller neighbours' canopy coverage/opacity.
   Apply the candidate's shade response. Do not treat a canopy as an opaque
   physical exclusion. Account for neighbours outside the boundary via a query
   margin at least as wide as relevant interaction extents.
6. Combine the environmental and shade scores using an explicit, versioned rule
   (initial proposal: their minimum), and accept using deterministic random values.
   Do not repeatedly retry failures until a species quota is met. This permits
   thinning while allowing selected shade-tolerant plants underneath canopy.
7. Report counts by species and rejection reason. Commit after rechecking occupied
   geometry; a changed world requires an updated plan or clearly disclosed skipped
   placements. Creation is batched/cancellable and records area ownership.
   Trigger via Generate or a debounced auto event; only the latest district revision
   may commit. Settings changes themselves need no Apply action.

**Known approximation:** ordering by height favours tall candidates for occupied
space. It does not reproduce bidirectional competition or succession. Test mixed
communities before adopting it as permanent. Do not add an arbitrary priority
slider merely to conceal undesirable results. If necessary, prototype a bounded
conflict-resolution pass and record the tradeoff here.

**Growth after placement:** the game may later change plant size. The first adapter
must verify whether final/mature footprints should be reserved or whether selected
variants remain stable. Runtime game growth is distinct from our generator and
must not invalidate promised obstacle clearance unnoticed.

## 7. UX and operational requirements

The default flow is **choose set → draw → Generate**. Auto mode replaces the last
step with automatic updates on valid nodes/settings changes (or completed drawing
where necessary). Select an existing district to edit it in the same window.
The set name's `*` means unsaved to the library; generation freshness is separate.
City climate supplies defaults and sets can override each field. Only sets are
saved; nested asset/age edits are part of them. See [UX](ux-design.md).

The advanced species editor shows a small graph and descriptive values. A preview
inspection at a point should explain, for example, "too cold", "slope unsuitable",
"occupied by road", or "insufficient canopy shade". A density control cannot
promise an exact final count while these rules apply.

The drawn boundary is the planting domain, including concave C/U shapes. No
interior-hole editing is required. Separate districts express different shoreline
communities. Do not infer banks, rain shadows, soil, or river-dependent selection.
Water is deliberately not queried or excluded: included underwater terrain may
receive vegetation if other rules permit it. Never move plants to the water surface.

All generated plants belong to their district; existing manually placed plants
are never implicitly adopted. Delete district removes its owned plants. A separate
**Bake to ordinary vegetation** operation detaches ownership and removes the district
while preserving the plants. Baked plants retain no procedural edit relationship.
Ungenerate removes owned plants but keeps the district; recommendation: it also
switches to On demand. These operations need undo and save/load validation. Baking is not a rendering or
memory optimization; it changes ownership only.

No continuous filling of gaps after bulldozing alone. A later auto regeneration
after a district edit may recreate those plants. Construction-conflict cleanup may
remove owned plants, but must not plant replacements. Undo, save/load, cancellation
and missing-asset recovery are product requirements beyond these references.

## 8. Validation plan and feasibility gates

No performance claims are borrowed from the references. Their engines, workloads
and representations differ from CS2. Establish our own budgets with profiling.

| Test / spike | What must be demonstrated |
|---|---|
| Climate equivalence | Equal derived temperatures produce equal thermal scores across maps |
| Slope contrast | On a synthetic ramp, a tolerant selected shrub remains where an intolerant selected tree fails |
| Mixed community | Under-canopy planting works without physical intersections; sun-dependent plants thin beneath dense canopy |
| Shade semantics | Shade-required plants fail in open ground; shade-tolerant plants are not forced under trees |
| Empty habitat | All-zero suitability yields no plants and no retry loop |
| District edges | Concave cutouts stay empty; outside plants still affect edge clearance/shade |
| Determinism | Same normalized inputs/seed/version yields the same plan regardless of task scheduling |
| Safety | Every supported infrastructure category rejects intersections, including changed geometry before generation commit; no water-specific rejection |
| Save/undo | Manual plants survive clear/undo; reloading neither rerolls nor refills deliberate removals |
| Scale | Candidate cap, cancellation latency, memory and generation time measured for small/large areas |

**Before game-facing implementation:** identify actual CS2 terrain,
obstacle, prefab and age-state data. Record game version, units, coordinate axes and
sample outputs. No method names have been assumed in this report. In particular,
an unavailable obstacle query is a release blocker, not permission to ignore that
obstacle category. Reference temperature can use compatible stable game climate data; manual input is the fallback.

**Prototype next:** a game-independent synthetic hillside with three fictional
plant profiles. Vary map temperature, slope and canopy settings; inspect predicted
counts and layout. Fictional profiles keep algorithm validation separate from
claims of botanical accuracy. Then validate the same contracts against a small
owned batch of real game assets on Windows.

## 9. Decisions still open

- Confirm annual mean as the initial climate statistic and tune profile curves.
- Calibrate footprint/canopy estimates for actual CS2 assets and growth states.
- Select the spatial patch field and exact density normalization.
- Validate height-ordered placement versus a bounded competition pass.
- Revisit cross-district overlap handling in v2; v1 allows overlap.
- Define undo retention limits and verify bake/save/load behaviour.

The references are selected now; these implementation choices are not falsely
presented as settled by them. Record future changes with a date and reason.

## Sources and verification notes

- **U1:** Epic Games, [Procedural Foliage Tool][U1], sections 2–5. Official walkthrough.
- **U2:** Epic Games, [UFoliageType][U2], procedural and placement properties. Official API reference.
- **U3:** Epic Games, [UProceduralFoliageSpawner][U3], spawner fields. Official API reference.
- **E1:** Gain, J.; Long, H.; Cordonnier, G.; Cani, M.-P. (2017),
  [EcoBrush: Interactive Control of Visually Consistent Large-Scale Ecosystems][E1],
  Computer Graphics Forum 36(2), 63–73. [DOI: 10.1111/cgf.13107][E2].
  Full author-hosted paper inspected, particularly §§3–7, figures 4–5 and Table 2.
  SHA-256 of inspected PDF:
  `5e5369cea58808b097c5dc30f3980d016162173c511a3f96b2bce6be8af648e7`.

All sources accessed 2026-10-26. Web documentation can change; preserve the field
names and distinctions in this report when revisiting it. No external source code,
paper figures, plant data tables, or engine assets are vendored. Referencing an
algorithm does not establish permission to copy an implementation; inspect any
candidate code's licence separately before reuse.

[U1]: https://dev.epicgames.com/documentation/en-us/unreal-engine/procedural-foliage-tool-in-unreal-engine
[U2]: https://dev.epicgames.com/documentation/en-us/unreal-engine/API/Runtime/Foliage/UFoliageType
[U3]: https://dev.epicgames.com/documentation/en-us/unreal-engine/API/Runtime/Foliage/UProceduralFoliageSpawner
[E1]: https://people.cs.uct.ac.za/~jgain/wp-content/papercite-data/pdf/gain2017.pdf
[E2]: https://doi.org/10.1111/cgf.13107
