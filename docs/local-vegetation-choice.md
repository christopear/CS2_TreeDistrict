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
