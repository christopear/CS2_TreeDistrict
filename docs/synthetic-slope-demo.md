> The restored-frontier counts below are historical. The current generator has
> independent composition and abundance controls; see [local policies](local-vegetation-choice.md)
> and run `scripts/demo-local-policies.py` for the four-way comparison. The paired demo
> now writes to `artifacts/local-policies-review` (flat: 176 planes/597 bushes;
> default slope, composition only: 146 planes/688 bushes;
> optional both enabled: 134 planes/540 bushes).

# Synthetic slope comparison

Run `python scripts/demo-slope-environment.py` in an environment with Matplotlib
and NumPy after bootstrapping SDK/packages. It runs the C# generator, checks the
results and exports plots, plans and 1m heightmap CSV/PNG files to
`artifacts/restored-frontier-review`.

The 200m square is flat from x=0 to 70m, rises at 2:1 to x=130m, then becomes a
120m-high plateau. A centred 10m box filter rounds the heightmap's joins, and
slope is computed from its analytic derivative. The environment grid is 5m.

Profiles use measured mature sizes and saved preferred spacing radii for London
Plane (user name: field maple) and Wild Green Bush 01. The slope responses are
illustrative: maple declines from full suitability at 10° to zero at 70°; bush
from 55° to 90°. Temperature stays at 18°C and shade is disabled.

The default path uses the restored active-frontier sampler, with local vegetation
or empty-space choice before reserving spacing. See [local choice](local-vegetation-choice.md).
Flat ground reproduces every position from the saved original active-frontier plan,
not just its count: 176 maples and 597 bushes. Terrain changes can change subsequent
proposals, so the two terrain outputs are not paired subsets.

| Vegetation in x=[70,130) | Flat | Smoothed slope |
|---|---:|---:|
| Field maple | 52 | 17 |
| Wild green bush | 178 | 270 |

Whole slope totals: 137 maples and 702 bushes. Smaller vegetation can occupy more
of the slope when maples lose suitability. There are 3,286 environmentally empty
proposal attempts; these are retries, not a count of unique reserved empty plots.
No plants are culled by post-selection environmental filtering in the default path.

The runner verifies original flat placement equality, exact terrain heights,
pairwise spacing, accounting and differential response. Use `--post-suitability`
to run the retained old filtering stage instead.
