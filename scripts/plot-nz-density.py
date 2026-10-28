"""NZ density/spacing diagnostic. Prepare workspaces, run CLI, then render to scale.

python scripts/plot-nz-density.py --prepare
For each workspace: dotnet run --project tools/TreeDistrict.Foliage.Cli --
  --workspace artifacts/nz-density/d1-s0.workspace.json --district nz --output artifacts/nz-density/d1-s0.json
python scripts/plot-nz-density.py
"""
import copy
import json
import sys
from pathlib import Path

root = Path(__file__).resolve().parents[1]
out = root / 'artifacts/nz-density'
out.mkdir(parents=True, exist_ok=True)
cases = [(1, 0), (2, 0), (1, 1), (2, 1)]
if '--prepare' in sys.argv:
    catalog = json.loads((root / 'data/editor-catalog.v2.json').read_text())
    preset = next(s['set'] for s in catalog['sets'] if s['id'] == 'builtin:nz-regenerating-bush')
    for density, spacing in cases:
        settings = copy.deepcopy(preset)
        settings['overallDensityPercent'] = density * 100
        for asset in settings['assets']:
            asset['profile']['spacingAdjustmentM'] = spacing
        record = dict(id='nz', name='NZ flat comparison', seed=1, revision=0,
                      generationMode='onDemand', libraryId=None, baseline=None, working=settings,
                      boundary=[dict(x=x, z=z) for x, z in [(0,0), (100,0), (100,100), (0,100)]])
        workspace = dict(schemaVersion=1, city=catalog['cityDefaults'], districts=[record])
        (out / f'd{density}-s{spacing}.workspace.json').write_text(json.dumps(workspace, indent=2))
    sys.exit(0)

import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.collections import PatchCollection
from matplotlib.patches import Circle, Patch
import numpy as np

styles = {'cs2:GreenBushWild01': ('#83a746', 'Low scrub'),
          'cs2:GreenBushWild02': ('#2f8e81', 'Tall scrub'),
          'cs2:NA_LindenTree01': ('#36583a', 'Linden proxy')}
fig, axes = plt.subplots(2, 2, figsize=(12, 13), facecolor='#f6f4ee')
fig.subplots_adjust(top=.90, bottom=.11, left=.07, right=.96, hspace=.25, wspace=.18)
summary = []
for ax, (density, spacing) in zip(axes.flat, cases):
    plan = json.loads((out / f'd{density}-s{spacing}.json').read_text(encoding='utf-8-sig'))
    plants = plan['result']['Placements']
    profiles = {p['Id']: p for p in plan['request']['Species']}
    counts = {}
    for species, (color, label) in styles.items():
        group = [p for p in plants if p['SpeciesId'] == species]
        counts[label] = len(group)
        ax.add_collection(PatchCollection([
            Circle((p['Position']['X'], p['Position']['Z']), profiles[species]['CrownRadiusM'])
            for p in group], facecolor=color, edgecolor=color, alpha=.65, linewidth=.35))
        ax.scatter([p['Position']['X'] for p in group], [p['Position']['Z'] for p in group], s=2, color=color)
    xy = np.array([[p['Position']['X'], p['Position']['Z']] for p in plants])
    distance = np.sqrt(((xy[:, None, :] - xy[None, :, :]) ** 2).sum(axis=2))
    np.fill_diagonal(distance, np.inf)
    nearest = distance.min(axis=1)
    row = dict(densityMultiplier=density, spacingAdjustmentM=spacing, count=len(plants), counts=counts,
               minimumNearestM=float(nearest.min()), medianNearestM=float(np.median(nearest)))
    summary.append(row)
    ax.set(xlim=(0,100), ylim=(0,100), aspect='equal', xlabel='X (m)', ylabel='Z (m)')
    ax.set_facecolor('#fffef9'); ax.set_axisbelow(True); ax.grid(alpha=.15)
    ax.set_title(f'Density {density}× · spacing +{spacing} m\n{len(plants)} plants · median nearest centre {np.median(nearest):.2f} m', fontsize=12)
fig.suptitle('NZ bush: density and spacing comparison', y=.975, fontsize=20)
fig.text(.5,.94,'Each panel: 100 × 100 m · flat ground · seed 1 · 15°C · identical species weights and crown sizes', ha='center', fontsize=11)
fig.legend(handles=[Patch(facecolor=c, label=l) for c,l in styles.values()], loc='lower center', bbox_to_anchor=(.5,.045), ncol=3, frameon=False)
fig.text(.5,.031,'Centre spacing = (canopy diameter + spacing adjustment) / √density multiplier.', ha='center', fontsize=10)
fig.text(.5,.012,'Circles are mesh-derived crown proxies, not exact foliage outlines. All four panels have identical metre scales.', ha='center', fontsize=9)
fig.savefig(out / 'comparison.png', dpi=160)
fig.savefig(out / 'comparison.svg')
(out / 'statistics.json').write_text(json.dumps(summary, indent=2))
print(json.dumps(summary, indent=2))
