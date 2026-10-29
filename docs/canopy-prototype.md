# Automatic canopy area prototype

Use **Canopy Areas** in the game/editor to activate the native area tool. Complete
an outline and the canopy starts appearing automatically. Add, move or remove
boundary nodes with the native tool to rebuild it. Delete the area to remove it.
There are no canopy settings or Generate button. Existing Tree Areas are separate.

This is a continuous decorative surface, not individual tree entities. It does
not participate in forestry resources, tree aging, collision or fire simulation.

## Geometry and rendering

Inspired by Julian Heuser's public description of his Diamond Visionics canopy
work: scattered vertices, Delaunay triangulation, varying heights and darkened
hollows. Reference: https://julianh.dev/portfolio/graphics/#professional-work
This is our implementation of that broad approach, not Diamond Visionics code
or a reproduction of its unpublished renderer.

The standalone `CanopyGeometry` class builds 48 m chunks with jittered samples
roughly 1 m apart. Fixed lattice connectivity replaces the repeated Delaunay
circumcircle searches; triangles are still clipped against the native outline.
Shared samples and continuous world-space height fields join adjacent chunks.
The surface is the upper envelope of overlapping, differently sized ellipsoidal
lobes, with heights around 3–20 m, plus smaller bumps. These are mathematical
surface features, not stored tree objects. Per-lobe tint and pronounced dark
hollows separate the clumps. A colour texture and tangent-space normal map sampled
every 25 cm add foliage mottling and lighting relief. Boundary height tapers to
0.5 m over a varying 2.5–4.5 m band, replacing the tall flat perimeter wall.
HDRP supplies lighting/shadows; mesh tangents support its normal mapping.
A 200 m square has roughly 82,000 top triangles. This is a look-development
fixture, not a claim of map-scale rendering readiness or a match to the reference.
The fallback HDRP Unlit shader has baked colour only if Lit is unavailable. A
missing supported shader logs an error rather than silently placing tree assets.

One chunk is built per main-loop update, including while paused/in the editor.
Node change versions and area structural changes trigger reconciliation. There
is no ongoing geometry regeneration for unchanged areas. Boundary changes clear
the old result and queue the replacement; native temporary previews are ignored.
Deleting an area cancels pending work and frees its meshes, textures and materials.
Native saved area nodes are the source of truth; derived meshes rebuild on load.

## First-version limitations

- Procedural foliage relief, without leaf cutouts, branch geometry or LODs.
- Terrain edits alone do not trigger rebuilds: edit a boundary node afterward.
- No road/water exclusion, brush painting, holes, or tree simulation.
- Very large areas still cost rendering memory/time; chunks are not a promise
  of unlimited coverage. Start with a few hundred metres for the visual trial.
- The game build and geometry tests are automated; live game rendering, save/load
  and native editing require the user's game test.

## Manual verification

1. Draw a small rectangle on flat terrain; completion creates the canopy.
2. Draw a concave outline across a slope; the indentation stays empty and the
   canopy follows terrain. Check seams and exterior walls from an oblique angle.
3. Add/move/delete nodes, including while chunks are still appearing.
4. Delete the area during and after construction; no canopy should remain.
5. Save/reload and repeat in the editor. Inspect `TreeDistrict` logs for
   `Canopy queued`, `Canopy shader`, `Canopy complete`, or generation errors.
   Completion logs separate geometry time from terrain/material/Unity work;
   neither includes the waits between frames.

## Repeatable 200 m visual study

Run `dotnet run --project tools/TreeDistrict.Canopy.Preview -c Release` and then
`python scripts/preview-canopy.py` with NumPy and Matplotlib installed. Outputs
are `artifacts/canopy-preview.json` and `.png`. The CLI calls the same C# surface
and material functions used by the game and reports geometry timing separately
from offline sampling. The PNG samples the field at 0.5 m with approximate lighting:
it is not an image of the exact 1 m mesh, a CS2 screenshot, or an HDRP simulation.
Use it to evaluate clump shapes before testing the actual game material.

For offline alternatives, run `python scripts/study-canopy-variants.py` after the
CLI. It creates `artifacts/canopy-variants/comparison.png` (200 m overview),
`details.png` (60 m crops), reusable numeric fields, and `study.json`. A is the
current C# shape; B–D are experimental Python fields, not ported into the game.
All use the same rolling hill (maximum 28% grade), palette, approximate lighting
and camera, without vertical exaggeration. B widens and interlocks crowns; C
layers branch-scale masses with occasional taller emergents; D emphasizes ragged
relief and irregular hollows. Colour shading uses a local concavity estimate,
not physical ambient occlusion. These plots do not establish in-game quality or
performance; sub-metre details will need an appropriate mesh/material treatment.

`python scripts/study-canopy-variants.py --between` writes a second study to
`artifacts/canopy-between-b-d/`. It keeps original B as the control and blends
25%, 50%, and 75% toward D's larger-scale shapes and hollows, while retaining B's
fine roughness amplitude throughout. These percentages are structural blends,
not a blend with D's full jagged surface. The close-up panels also share vertical
limits to keep framing consistent. No game code or deployed files are changed.
