# TreeDistrict — product requirements draft

Updated 2026-10-26 after reference review. The direction below incorporates the
user's scope corrections; remaining recommendations are labelled. It describes
intended behaviour, not implemented functionality.

Use the [reference design report](foliage-reference-design.md) for the selected
Unreal Procedural Foliage and EcoBrush references, research and proposed pipeline.

## Goal

Draw a district, select a reusable vegetation community, and generate its plants
according to terrain, configured climate, occupied space and neighbouring plants.
The user chooses the community and boundary; the mod automates placement inside it.
Trees and shrubs belong to the same community, with species-specific preferences.

## 1. City setup

The user defines three stable climate inputs, saved with the city:

- Reference temperature in °C.
- Reference elevation in metres, in the same height convention as terrain samples.
- Cooling rate in °C per kilometre of elevation gained.

For terrain height h:

    localTemperature = referenceTemperature
        - coolingCPerKm × (h - referenceElevation) / 1000

These user values are authoritative. No live weather input or additional climate
inference is required. Recommendation: label the temperature as annual mean and
calibrate defaults accordingly. A future automatic suggestion must explain its
source and must not overwrite the user's settings.

The map supplies terrain heights/normals, occupied geometry and existing vegetation.
Exact game APIs, units and coordinates still require verification. Geometric
height is sampled from terrain, not from the water surface or tops of buildings.

## 2. Vegetation profiles and age states

Selecting an asset pre-populates an editable profile:

- Temperature and slope response curves.
- Optional hard placement limits.
- Physical footprint, representative height, canopy extent and opacity.
- Shade response, distinguishing shade tolerance from a preference for shade.
- Default candidate density, measured in opportunities per hectare.
- Available ages/variants and their estimated properties.

Each age state needs its own estimates, especially height, occupied footprint,
canopy radius and opacity. Species preferences may be shared initially, with
age-specific overrides supported where useful. Do not assume everything scales
uniformly with height. Record whether an estimate is measured, tuned or inherited.

The selected age must be resolved before collision, shade and clearance checks.
Changing a species' age mixture divides its candidate opportunities among ages;
it does not allocate a full density to every age independently.

Existing game growth needs a Windows spike: determine whether plants change age
or size after placement, and how to preserve future obstacle clearance. Freezing
ages is not assumed, nor is it an agreed requirement.

Unknown/custom assets receive an explicit fallback profile which can be edited.
Defaults are artistic approximations, not universal ecological measurements.

## 3. Reusable vegetation sets

A set contains the selected assets and their effective profile settings, plus:

| Control | Meaning |
|---|---|
| Per-species density percentage | Multiplier of that species' default; independent of other species |
| Overall density percentage | Multiplier applied to all species in the set |
| Age selection/mixture | Allowed age states and relative weights within each species |
| Clumping strength and patch size | Shared spatial variation controls |
| Optional set-wide hard limits | Additional bounds applying to every selected plant |

**Density percentages do not sum to 100%.** Recommendation: show 100% as the
profile default, 50% as half its candidate density, and 0% as disabled. A selected
species at 50% in a set at 60% receives 30% of its default candidate opportunities.
Permit values above 100% only if useful in the prototype, within global caps.

    candidateDensity_i = defaultDensity_i
        × speciesDensityPercent_i / 100
        × overallDensityPercent / 100

Climate, slope, shade, occupied space and spacing affect how many opportunities
become plants. A species' default can differ substantially from another's: 100%
for a shrub does not mean the same count as 100% for a large tree.

Do not redistribute rejected opportunities to force a final total. Suitable shrubs
can still occupy space where trees fail. The preview reports resulting counts per
species and age, and explains major rejection reasons.

Set-wide hard limits intersect species limits, so the more restrictive condition
wins. They do not override physical collision constraints. Prefer leaving these
advanced controls unset by default; the district already defines spatial extent.

Save, duplicate, rename and delete sets in a library available across cities on
this machine. Support a favourite/default set. Export/import is useful for sharing
and dual-boot use; cloud sync is out of scope. Presets contain settings and asset
references, never asset files. Missing assets remain visibly unresolved.

Each district stores a snapshot. Editing or deleting a library preset must not
silently alter existing districts. Updating a district from a set is explicit and
previewed. Local density overrides should be savable as a new set if desired.

## 4. District geometry and exclusions

A district is one simple closed polygon, which may be concave. C/U shapes and a
ring with an open slice are valid. Populate only inside the drawn domain; do not
replace it with its bounding box or convex hull. Self-intersections are invalid.
**No interior-ring/hole editor is required.**

Water coverage is deliberately ignored. If underwater terrain is included, plants
may be generated there when the other rules allow it. This does not require the
mod to defeat any native game restriction. No water query, flood cleanup, or
shoreline classifier is part of the first version.

Occupied roads, rail corridors, buildings and ploppables remain exclusions. Use
physical extent and clearance rather than only a trunk-centre test. Account for
vertical separation, including bridges and tunnels. Apply conservative physical
footprint containment at district edges; canopy influence is not a hard border
exclusion. The visible shape of foliage depends on its asset representation.

Existing vegetation is preserved, affects spacing/shade where relevant, and is
not automatically assigned to a district. Neighbours outside the district may
influence plants just inside it.

Overlapping district interiors remain an open decision. Recommendation for the
first version: reject overlapping interiors while allowing shared boundaries.
Never duplicate planting or leave ownership ambiguous.

## 5. Ownership, deletion and baking

All plants created by a district belong to it, regardless of their age or type.
This ownership survives save/load. Merely drawing around existing plants does not
claim them.

- **Delete district:** remove the district and its owned plants.
- **Bake to ordinary vegetation:** keep its plants, detach procedural ownership,
  and remove the district. Existing ordinary game tools can edit those plants.
- **Regenerate:** replace/reconcile only that district's owned plants using its
  effective settings and seed. Preserve manually placed and baked vegetation.

Baking is the intended name for the proposed "raster" action. It does not convert
plants into an image, merge meshes, or reduce their rendering cost. It is not
silently reversible by redrawing the same boundary; use undo to restore ownership.
Deletion and baking must be undoable operations with safe save/load behaviour.
Deleting a preset from the library is unrelated to deleting a district.

## 6. Everyday workflow

1. Configure city climate once.
2. Pick a vegetation set, or create one by selecting plants and accepting defaults.
3. Draw a district.
4. Preview; optionally adjust local overall density and clumping.
5. Apply. Save set changes explicitly when they should be reusable.

Keep detailed profile/age parameters in an advanced editor. Preview/apply is
explicit; settings changes do not silently regenerate the city. A saved seed
repeats a layout for the same inputs/version, while New variation changes it.

Lower density targets fewer generated instances, not a precise VRAM budget. No
promise is made that reducing density by half halves GPU memory: asset variety,
rendering and other game behaviour matter. Preview plant counts and enforce a
visible instance/candidate cap rather than inventing a VRAM estimate.

Generation must be bounded, cancellable and applied in batches. No periodic
refilling after manual bulldozing. Newly occupied infrastructure must not leave
conflicting owned plants; prefer native game removal where sufficient, otherwise
perform local cleanup. This never triggers automatic replacement planting.

## 7. Acceptance examples

- Equivalent derived temperatures on different maps give matching thermal scores.
- Tolerant shrubs can remain on slopes where selected trees are unsuitable.
- Shade-tolerant vegetation coexists under canopy without physical intersections.
- A C/U district leaves its open cutout empty; included underwater terrain receives
  no special rejection by the planner.
- Age mixtures use the matching footprint/canopy estimates, without multiplying
  the species' requested population.
- 50% species density and 60% overall density yield 30% of baseline candidate
  opportunities before environmental rejection, subject to deterministic rounding.
- Set-wide limits cannot widen tighter species restrictions.
- Delete removes only owned plants; Bake preserves them and removes the district.
- Undo and reload preserve or restore ownership correctly; no reroll on reload.
- Changing a saved set does not mutate existing districts without explicit update.
- Occupied infrastructure blocks placement, including a last recheck before Apply.

## 8. Next decisions and implementation gaps

- Measure/tune age-specific profiles for the available CS2 foliage assets.
- Verify game growth/age transitions and future-clearance policy.
- Tune candidate density and clumping so simple controls produce useful results.
- Validate the reference report's proposed height-ordered interaction pass.
- Decide district overlap rules and undo retention limits.
- Verify terrain, occupied geometry, asset and ownership/save APIs in Windows.

The existing TerrainRules class is initial hard-filter scaffolding. Its water
parameter reflects an older scope and must not become a requirement in the new
planner. No generator, age catalogue, baking or district integration exists yet.
