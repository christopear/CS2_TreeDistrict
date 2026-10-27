# ProtonScatter first-pass generator

A separate standalone generator; the original and in-game generator remain available.
Primary source is ProtonScatter, pinned and attributed in
[third-party/proton-scatter](../third-party/proton-scatter/README.md).
This implementation does not use Epic source or a growth/dispersal simulation.

## Pipeline

1. Ported fixed-radius Poisson sampler fills the polygon using active spawn points.
2. Shuffled points receive a weighted species selection from the existing placeholder data.
3. Terrain hard limits and temperature/slope suitability filter those points.
4. Tallest mature canopies are processed first. Accepted plants alone reserve mature
   physical clearance and shade shorter candidates. Existing plants are immutable constraints.
5. Visual ages are assigned using the existing set weights; ages never shrink clearance or canopy size.

The port is specifically the Poisson modifier, not the whole Godot plugin. Its
engine/editor integration is unnecessary for this standalone experiment. Generic
profile validation, geometry, PRNG, and age allocation reuse our existing C# core.

## Controls and interpretation

`data/proton-scatter.v1.json` stores placeholder set-level spacing (6.25 m) and
upstream's default 15 attempts per active point. Spacing is centre-to-centre, not
crown radius. At density D%, effective spacing is spacing/sqrt(D/100). Zero density
returns an empty plan. Workload overflows throw rather than return a silent partial plan.

For this generator, `CandidatesPerHectare * DensityPercent/100` is a **relative
species weight**, not an absolute count contract. Overall count is controlled by
spacing and overall density. Scaling every species weight equally leaves the
mixture unchanged. Existing profile JSON is reused for comparison; its old
`algorithmVersion` identifies that fixture, not the new generation algorithm.
Output `result.algorithmVersion` identifies the actual generator and output includes
its effective settings. The CLI still validates the legacy fixture version.

All existing numerical profiles are placeholders, not botanical measurements.
The fixed sampler is independent of species assignment: large physical clearances
can still remove points later. Canopy overlap is deliberately permitted; shade is
a simple mature-height/crown-centre proxy. There is no crown-overlap percentage cap.

## Known first-pass limitations

- No refill following suitability/clearance/shade rejection, so filtering can leave gaps.
- Single spacing for the whole set; no variable-radius sampler or custom clustering mask yet.
- The active frontier can miss regions behind very narrow connections, as in upstream.
  Only one simple polygon is supported, not multiple disconnected polygons.
- Tallest-first acceptance biases conflicts toward taller vegetation. No competition priority setting.
- Whole-area deterministic regeneration; small edits need not preserve all placements.
- Terrain and species weight filtering happen after the Poisson stage; unsuitable species
  still consume some sample opportunities. This intentionally keeps the initial port close to upstream.
- Not connected to the in-game district lifecycle yet.

## Reproduce the comparison

From repository root (SDK/packages already bootstrapped):

```sh
bash scripts/dotnet.sh run --project tools/TreeDistrict.Foliage.Cli -- --generator original --seed 42 --size 200 --output artifacts/proton-review/original.json --svg artifacts/proton-review/original.svg
bash scripts/dotnet.sh run --project tools/TreeDistrict.Foliage.Cli -- --generator proton --seed 42 --size 200 --output artifacts/proton-review/proton.json --svg artifacts/proton-review/proton.svg
python3 scripts/compare-foliage-plots.py artifacts/proton-review/original.svg artifacts/proton-review/proton.svg artifacts/proton-review/comparison.svg
convert -background white artifacts/proton-review/comparison.svg artifacts/proton-review/comparison.png
```

The renderer uses exactly the same scales, colours, mature crown circles, and dots
for both algorithms. Green = birch, amber = maple, blue = oak. The original's 678
independent candidates produce 408 survivors (88/246/74); the new sampler's 638
points produce 524 survivors (114/319/91), with 114 shade rejections and zero
physical-clearance rejections. Thus this is an equal-input comparison, not equal
final density or equal random positions. Seed equality does not imply paired locations.
No timing claim should be inferred from a single first-call/JIT-inclusive CLI run.

Regression checks cover rectangular-grid spacing, concave boundaries, tiny domains,
reproducibility, density, mature clearance independent of visual age, terrain,
existing plants, shade, cancellation, and workload limits.
