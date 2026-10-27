# Active-frontier placement with independent local policies

The ProtonScatter-derived active frontier is retained: accepted plants propose nearby
locations, with up to 15 spatial attempts before retiring an active point. No fixed
opportunity lattice or post-placement environmental thinning is used by default.

## Controls

`data/proton-scatter.v1.json` exposes two independent booleans:

- `enableLocalComposition` (default **true**): local suitability changes relative species selection.
- `enableLocalAbundance` (default **false**, experimental): local suitability changes preferred spacing.

The corresponding C# properties are `EnableLocalComposition` and `EnableLocalAbundance`.
CLI flag `--abundance` opts into spacing-based abundance.
`--no-composition` and `--no-abundance` disable them individually; pass both
to ignore soft environmental responses. Hard terrain exclusions remain enforced.
The default is composition only: retain normal spacing and focus future abundance
work on culling. No new abundance-culling stage is implemented yet.
These are standalone generator settings, not yet in-game UI controls.

`usePrePlacementSuitability: false` (or `--post-suitability`) selects the retained
legacy post-selection filter instead. That alternative ignores both local policy
switches. It must not be combined with the default policies as a second suitability
penalty. Existing clearance and optional shade rejection still run independently.

## Input pipeline and suitability boundary

`FoliageGenerationCoordinator` is the preparation/entry point used by the CLI. It
accepts a `GenerationRequest` (boundary, selected `SpeciesProfile` objects, climate,
and settings), a stable terrain callback, and an optional
`IVegetationSuitabilityEvaluator`. It validates the request, builds the environment
grid, and calls the solver. It also exposes `Evaluate(profile, position)` for
inspection using the same grid, exact terrain, and evaluator as generation.

`PlacementEnvironmentProvider` joins exact ground with growing conditions from the
grid. Exact elevation, slope, water and blockage flags remain available for hard
limits; the interpolated slope and temperature drive soft responses. Without a grid,
it uses exact slope and temperature derived from request climate (or ground temperature
when no climate is supplied). Missing grid coverage is an error, not a silent fallback.

`VegetationSuitabilityEvaluator` is a pure calculation of profile + proposed-location
environment + district limits. Its `SuitabilityResult` separates:

- `Allowed`: valid environmental input and passing district/profile hard limits.
- `Score`: soft suitability in [0,1], retaining the minimum temperature/slope response.
- `TemperatureScore` / `SlopeScore`: individual responses for diagnostics.

A blocked location can have score 1 while `Allowed` is false. Conversely, a permitted
location can have score 0. Consumers must check `Allowed` before using the score.
Disabling composition ignores the soft score but still respects eligibility. Both
pre-placement selection and the retained post-selection filter use this evaluator;
there is no separate formula embedded in the solver. Implementations should be pure
and deterministic because the solver may evaluate a location more than once.

```csharp
var coordinator = new FoliageGenerationCoordinator(request, sampleTerrain);
var diagnostic = coordinator.Evaluate(request.Species[0], proposedPosition);
var plan = coordinator.Generate(new ProtonScatterSettings());
```

The existing generation-profile JSON is deserialized into `SpeciesProfile` at the
CLI boundary; file I/O stays outside Core. This does not yet translate raw botanical
research fields into authored response curves or access live CS2 terrain APIs. The
future game adapter must supply a safe, stable terrain snapshot. Keep request/profile
objects and that snapshot unchanged while using a coordinator; rebuild it after edits
so its environment grid cannot become stale. Grid size, sample budget and cancellation
are available at preparation time; solver cancellation is available on `Generate`.

This wiring adds no new culling or ecological rules. Pre-abundance remains disabled.

## Local response before placement

At each proposal location, read exact ground for hard constraints and the sampled
local environment for soft temperature/slope response. Let `w_i` be each species'
configured weight and `s_i` its suitability in [0,1], including hard eligibility.

Composition enabled: choose with relative weight `w_i * s_i`, normalized by their
sum. Composition disabled: choose with `w_i` among hard-eligible species. A zero
composition total means no plant can be chosen.

Independently, abundance is `A = sum(w_i * s_i) / sum(w_i)`, or 1 when disabled.
This uses original set weights, not the already-normalized composition weights.
Zero abundance is barren. Positive abundance scales every species' preferred radius:

```
effectiveRadius = preferredRadius / sqrt(overallDensityFraction * A)
```

For example, A=0.5 increases spacing radii by sqrt(2), giving approximately half the
packing density for a uniform single-species population. Actual counts depend on
boundaries, mixed radii, finite attempts, and subsequent clearance/shade rejection;
this is a spatial density control, not an exact quota or ecological calibration.
The former probabilistic empty ticket has been removed from the default local path:
retries cannot simply undo this reduction by repeatedly filling the same area.

A proposal distance uses conditions at the active centre; the resulting position
is evaluated again before reservation. Accepted points store their effective radius.
Pairwise spacing checks use the sum of **both endpoints'** effective radii, including
across changes in terrain. This can create a buffer beside sparse regions. Crown and
physical asset sizes do not change. Very large neighbour queries scan accepted points
rather than enumerate an enormous empty grid; workload limits still apply.

`LocalVegetationResponse` holds the independently computed choice weights and
abundance. It performs no random draws or reservations. The sampler accepts separate
local-choice and local-spacing callbacks, so neither policy is built into its traversal.

## Preserved behaviour and limitations

Fully suitable terrain consumes the original random stream and produces exactly the
original flat demo: 176 planes and 597 bushes, including identical positions/ages.
Disabling both policies also preserves that layout on the synthetic slope.

If barren ground is encountered and the frontier exhausts, up to 64 random restart
seeds explore other parts of the polygon. This can cross barren strips, but does not
guarantee finding every isolated patch. Later clearance/shade rejection can leave gaps;
dynamic growth/refill is not implemented.

`SamplingOpportunities` equals `EmptyEnvironmentSamples` + `SpacingRejectedSamples`
+ accepted sampling candidates. Empty now means no locally selectable plant or zero
abundance, rather than a failed soft probability ticket. `EnvironmentRejected` stays
zero in the default path. Algorithm ID: `foliage-proton-frontier-local-policies-v2`.

## Reproduce and compare

With matplotlib installed:

```
python scripts/demo-slope-environment.py
python scripts/demo-local-policies.py
```

Outputs are in `artifacts/local-policies-review`, including PNG/SVG comparisons and
actual C# JSON plans. The four-way comparison uses identical 200 m square boundaries,
seed 42, a smoothed 2:1 ramp, 5 m environment samples, and illustrative slope responses.
In its x=70–130 m ramp strip:

| Composition | Abundance | London planes | Bushes |
|---|---|---:|---:|
| Off | Off | 52 | 178 |
| On | Off | 21 | 275 |
| Off | On | 29 | 69 |
| On | On | 6 | 146 |

Composition alone favours bushes; abundance makes the slope sparser. Counts vary
with the new spatial traversal and are not matched pairs of individual plants.
Tests cover independent toggles, exact flat regression, spatially varying pairwise
spacing, overall density interaction, zero/near-zero abundance, hard exclusions,
determinism, workload limits, and retained post-selection filtering.

## Measured catalogue adapter and inspection

The CLI now accepts `--asset-catalog data/base-game-vegetation.v1.json`, joining
botanical records from `--botanical-catalog` (default: bundled
`real-world-vegetation.v1.json`) by asset ID. This is an optional input mode; existing
`--profiles` files still work and cannot be combined with `--asset-catalog`.

The adapter loads typed C# measurements and quantitative inputs and preserves full
source JSON for inspection, including botanical prose, provenance, age measurements,
identity uncertainty, and ecological observations. The current files contain 26
measured asset groups and 25 botanical records; an unmatched asset is explicitly
reported, not dropped. All assets are available for inspection; a forest subset can
be selected with comma-separated asset IDs through `--species`.

Simple resolution rules:

1. Prefer mature measurements; use static measurements if no mature meshes exist.
   Keep maximum measured height and half maximum width/depth as the footprint.
   Preferred spacing comes directly from the measured catalogue's authored value.
2. Use authored quantitative temperature/slope curves when present.
3. Otherwise use the editable kind-based slope defaults in
   `data/vegetation-adapter.v1.json`: tree optimum 10°, zero at 70°; shrub optimum
   55°, zero at 90°. Below optimum scores are 1; above it they decline linearly.
   These are prototype proxies copied from the slope experiment, not species research.
4. Missing temperature curves produce neutral response 1, labelled **unknown**.
   Hardiness categories and FAO crop ranges are not converted into mean-temperature
   triangles. Missing soil, rainfall, wind and sunlight responses are not invented.
5. The evaluator retains `score = min(temperatureScore, slopeScore)`, with hard
   eligibility separate. Hard elevation limits use authored pairs when present,
   otherwise broad solver limits. Missing trunk spacing/opacity disable those effects.
   Shade-tolerance percentages remain inspectable without an invented mortality mapping.

The adapter assigns equal proposal weights and mature/static appearance only. Those
are prototype defaults; it does not yet resolve saved vegetation-set weights or age
mixes. Missing measurements, preferred spacing, or kind policies fail explicitly.
Live terrain still arrives through the existing callback; this is not a CS2 API adapter.

Reproduce the inspectable all-asset test:

```sh
bash scripts/dotnet.sh run --project tools/TreeDistrict.Foliage.Cli --no-restore -- \
  --generator proton --asset-catalog data/base-game-vegetation.v1.json \
  --terrain-shape plateau --terrain-smoothing 10 --grade-percent 200 \
  --cooling-per-km 0 --no-shade --size 100 \
  --inspect-inputs artifacts/catalogue-inputs/inspection.json \
  --output artifacts/catalogue-inputs/plan.json
```

The inspection report contains the resolved profile, full original asset/botanical
records, mapping decisions, exact and gridded terrain values, and suitability at three
proposed locations (20%, 50%, 80% across the district). The plan contains actual solver
placements using those profiles. With the current illustrative policy, the central
63.43° slope scores London plane at 0.109 and wild green bush at 0.759; both flat samples
score 1. These are formula outputs, not measured survival probabilities.

## v0.2.0 vegetation presets

`data/vegetation-sets.v1.json` supplies five standalone solver presets:

| ID | Name | Contents (relative proposal weights) |
|---|---|---|
| `low-cover` | Bushes & flowers | Wild green bushes 01/02 (35/15), wild flower bushes 01/02 (25/25) |
| `european-woodland` | European trees + bushes | Oak 30, birch 25, alder 10, poplar 15, pine 10, spruce 10; wild green bushes 01/02 (30/10) |
| `european-trees` | European trees | Same six trees and weights, no bushes |
| `american-woodland` | American trees + bushes | Hickory 40, linden 30, London plane 30; wild green bushes 01/02 (30/10) |
| `american-trees` | American trees | Same three trees and weights, no bushes |

These are game/visual themes, not verified native ecological communities. The
American mix uses the NA-prefixed asset palette and shares the available wild bushes;
no native North American understory is claimed. Flower assets are flowering bushes,
not herbaceous ground flowers. Cultivated bushes, palms, orchard trees and the
unconfirmed European-spruce mesh group are excluded from these starter sets.

All start at 100% overall density and inherit the existing composition-on,
pre-abundance-off settings. Weights affect proposals, not promised final percentages.
The tree weights remain identical between paired presets; adding smaller bushes will
change final packing. Low cover means short vegetation, not reduced density.
No additional ecology curves or culling behaviour are introduced.

Select with `--vegetation-set ID` alongside `--asset-catalog`; optionally use `--sets`
for an edited preset file. `--density` overrides preset density. `--species` and
`--vegetation-set` are mutually exclusive. Selected members are copied before weights
are applied; the input catalogue is not mutated. Both plan and inspection JSON record
the selected preset. Example:

```sh
bash scripts/dotnet.sh run --project tools/TreeDistrict.Foliage.Cli --no-restore -- \
  --generator proton --asset-catalog data/base-game-vegetation.v1.json \
  --vegetation-set european-woodland --no-shade --size 100 \
  --output artifacts/vegetation-sets/european-woodland.json \
  --svg artifacts/vegetation-sets/european-woodland.svg
```

These presets belong to the v0.2.0 standalone pipeline pass. The existing in-game/UI
catalogue has not been migrated; connecting this preset library to that editor is a
separate integration step. Existing saved sets remain unchanged.
