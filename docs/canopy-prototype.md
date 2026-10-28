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

The standalone `CanopyGeometry` class builds 96 m chunks with jittered samples
roughly 6 m apart. It clips Delaunay triangles against a triangulated native area
outline rather than implementing constrained Delaunay insertion. Shared samples
and continuous world-space height/colour fields join adjacent chunks. The top is
10–19 m above sampled terrain. Exterior skirts extend down to the terrain. A
baked colour texture darkens hollows; HDRP provides surface lighting and shadows.
The fallback HDRP Unlit shader has baked colour only if Lit is unavailable. A
missing supported shader logs an error rather than silently placing tree assets.

One chunk is built per main-loop update, including while paused/in the editor.
Node change versions and area structural changes trigger reconciliation. There
is no ongoing geometry regeneration for unchanged areas. Boundary changes clear
the old result and queue the replacement; native temporary previews are ignored.
Deleting an area cancels pending work and frees its meshes, textures and materials.
Native saved area nodes are the source of truth; derived meshes rebuild on load.

## First-version limitations

- Simple green geometry, without leaf textures, detailed edge foliage or LODs.
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
