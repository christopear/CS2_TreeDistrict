# Local environment sampling layer

`LocalEnvironmentGrid` is an immutable environmental snapshot, exposed through
`ILocalEnvironmentField.TrySample(position, out environment)`. It stores elevation
(metres), slope (degrees from horizontal), and reference temperature (Celsius).
It does not contain species, spawn quotas, randomness, or vegetation ownership.

## Resolution and queries

The default cell size is 5 metres, configurable independently of Poisson spacing
and overall density. Values are sampled at cell corners, with bilinear interpolation
between them. Final edge cells can be smaller than the requested resolution.
The grid covers the polygon's bounding rectangle; concavities do not create sampling
boundaries. Querying outside this rectangle returns false, never fabricated defaults.
The Poisson sampler remains responsible for keeping placements inside the polygon.
Changing resolution can affect accuracy over nonlinear terrain, but does not alter
Poisson point generation. At a fixed flat environment, output remains identical.

## Filling it from a map (adapter TODO)

`Build(boundary, provider, cellSizeM, maxSamples, cancellationToken)` calls a supplied
provider once per sample position. Positions are world-space X/Z metres and never
extend outside the bounding rectangle. The returned grid does not retain the provider.
The default budget is one million samples; overflow fails before allocation/sampling.
Invalid values fail construction. Cancellation is checked throughout the build.

`FromTerrain(...)` adapts the existing `GroundSample` callback, optionally using
`ClimateProfile` to convert sampled elevation into reference temperature. Without
that climate override it uses the callback's temperature. This is the hook used by
the standalone CLI's synthetic terrain today.

TODO for the CS2 adapter: obtain a stable map snapshot using the game's appropriate
thread/API, supply heights and normals (slope = acos(clamped normal.y) in degrees
for a normalized upward normal), and resolve city/set climate defaults. Keep Unity
and game APIs outside the standalone core. Rebuild the snapshot after relevant map,
climate, or resolution changes. No automatic invalidation is implemented here.

Wind, soil quality, weather extrema, and generated canopy shade are not invented or
stored as placeholder numbers. Add explicitly defined inputs when they are needed.

## Consumer integration

`ProtonFoliageGenerator.Generate(..., environment: field)` accepts any implementation
of the read-only field contract. With a field supplied, its existing suitability
stage reads local temperature and slope from the field. An uncovered candidate is
an error. Without a field, the previous exact callback behaviour remains available.

Actual placement height, hard slope/elevation limits, water and blocked-land checks
still come from the **exact** terrain callback at the proposed location. These
checks must not use averaged environmental conditions; a narrow obstruction or
cliff must not disappear through interpolation. Existing water behaviour is unchanged.
Generated canopy shade remains a separate neighbour-based stage.

The CLI now builds this grid for the Proton generator and records its resolution,
rows, columns, and sample count in JSON. Use `--environment-cell-size 5` to override
the resolution. The Poisson sampler never reads the environmental grid.

The default generator now passes this field to the local composition-and-abundance
chooser before any plant reserves spacing. The legacy post-selection filter remains
available via `UsePrePlacementSuitability = false`. See [local choice](local-vegetation-choice.md).

## Example

```csharp
var field = LocalEnvironmentGrid.FromTerrain(
    request.Boundary,
    sampleTerrainSnapshot,
    request.Climate,
    cellSizeM: 5,
    cancellationToken: cancellationToken);

var result = new ProtonFoliageGenerator().Generate(
    request, sampleTerrainSnapshot, scatterSettings,
    cancellationToken, environment: field);
```

Tests cover interpolation (including shortened edge cells), negative world
coordinates, climate conversion, sampling budgets, invalid values, cancellation,
field consumption by the generator, exact terrain checks, and independence of
Poisson locations from grid resolution.
