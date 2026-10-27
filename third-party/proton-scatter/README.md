# ProtonScatter source reference

Upstream: https://github.com/HungryProton/scatter (mirror of https://codeberg.org/hungryproton/proton_scatter).
Pinned commit: `7198cc00078ebde61307578137c3f35223c6e438`.
Remote in this checkout: `proton-upstream` (Git remotes are local configuration).

`create_inside_poisson.gd` is an unmodified snapshot of
`addons/proton_scatter/src/modifiers/create_inside_poisson.gd` at that commit.
`LICENSE` is the upstream MIT licence. The source credits Sebastian Lague's
Poisson disc sampling implementation. No demo assets are included.

Adaptation: `src/TreeDistrict.Core/Foliage/ProtonPoissonSampler.cs`.
Preserved: active spawn list, random active-point selection, sine/cosine direction,
uniform distance in [radius, 2*radius), retry/retirement loop, radius/sqrt(2) grid
cells, 5x5 neighbour search, polygon containment, and final shuffle.

Changes: 2D-only C# rather than Godot transforms; explicit stable PRNG; sparse grid
coordinates instead of upstream's incorrect rectangular stride/ambiguous empty
cell zero; retained initial point for tiny domains; bounded initial-point search
instead of the upstream unbounded `or tries > 200` condition; cancellation and
workload caps. Results are not bit-identical to Godot's PRNG.

`ProtonVegetationSampler.cs` extends the same active-frontier design to preselected
species with pairwise summed preferred radii. It uses the existing sparse spatial
index for variable search ranges, budgets neighbour checks, and retires active
points with swap-removal. This is a TreeDistrict adaptation, not upstream functionality.

The default path now uses `ProtonVegetationSampler` with local choice before
spacing reservations. The fixed-opportunity `LocalVegetationSampler` experiment
was removed because it reduced flat-terrain packing. Fully suitable terrain retains
the original active-frontier proposal sequence exactly.
