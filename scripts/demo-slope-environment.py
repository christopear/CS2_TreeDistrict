#!/usr/bin/env python3
"""Run paired synthetic terrains and plot actual C# generator output. Requires matplotlib.
Usage: python scripts/demo-slope-environment.py [--output artifacts/slope-review]
Uses recorded asset dimensions, no live CS2 inputs. Slope responses are illustrative.
"""
import argparse
import csv
import json
import os
from pathlib import Path
import subprocess

os.environ.setdefault('MPLCONFIGDIR', '/tmp/treedistrict-matplotlib')
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.collections import PatchCollection
from matplotlib.patches import Circle, Patch
import numpy as np

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--output', default='artifacts/local-policies-review')
args = parser.parse_args()
repo = Path(__file__).resolve().parents[1]
out = (repo / args.output).resolve()
out.mkdir(parents=True, exist_ok=True)
subprocess.run(['bash', 'scripts/dotnet.sh', 'build', 'tools/TreeDistrict.Foliage.Cli', '--no-restore'], cwd=repo, check=True)
# Keep copied demo profile dimensions/radii traceable to the authoritative catalogue.
catalogue = {a['id']:a for a in json.loads((repo/'data/base-game-vegetation.v1.json').read_text())['assets']}
for profile in json.loads((repo/'data/slope-demo.v1.json').read_text())['profiles']:
    asset = catalogue[profile['assetId']]
    assert profile['preferredSpacingRadiusM'] == asset['placementDefaults']['preferredSpacingRadiusM']
plans = []
for name, grade in [('flat', 0), ('gradient', 200)]:
    subprocess.run(['bash', 'scripts/dotnet.sh', 'run', '--project', 'tools/TreeDistrict.Foliage.Cli', '--no-build', '--',
        '--generator', 'proton', '--profiles', 'data/slope-demo.v1.json', '--terrain-shape', 'plateau',
        '--terrain-smoothing', '10', '--grade-percent', str(grade), '--cooling-per-km', '0', '--temperature', '18', '--no-shade',
        '--seed', '42', '--size', '200', '--environment-cell-size', '5', '--output', str(out / f'{name}.json')], cwd=repo, check=True)
    plans.append(json.loads((out / f'{name}.json').read_text()))

# Export real-valued heights at 1m resolution; formulas/extent match the CLI terrain metadata.
x = np.arange(201, dtype=float)
heights = []
def terrain_height(x, t):
    x = np.asarray(x)
    width = t.get('smoothingM', 0)
    if width:
        def integral(u):
            return .5 * (np.maximum(u-t['rampStartM'],0)**2 - np.maximum(u-t['rampEndM'],0)**2)
        run = (integral(x+width/2) - integral(x-width/2)) / width
    else:
        run = np.clip(x-t['rampStartM'],0,t['rampEndM']-t['rampStartM'])
    return t['baseElevationM'] + run*t['gradePercent']/100

for name, plan in zip(['flat', 'gradient'], plans):
    t = plan['terrain']
    h = terrain_height(x,t)
    heightmap = np.tile(h, (201, 1))
    heights.append(h)
    np.savetxt(out / f'{name}-heightmap.csv', heightmap, delimiter=',', fmt='%.2f',
        header='height metres; columns x=0..200m, rows z=0..200m; 1m spacing')
    plt.imsave(out / f'{name}-heightmap.png', heightmap, cmap='gray', vmin=0, vmax=120, origin='lower')
    for p in plan['result']['Placements']:
        expected = terrain_height(p['Position']['X'],t)
        assert abs(p['ElevationM'] - expected) < 1e-8

# Active-frontier proposals depend on accepted vegetation; compare rules, not paired locations.
a, b = [p['result'] for p in plans]
assert b['EmptyEnvironmentSamples'] == a['EmptyEnvironmentSamples'] == 0
assert all(c['EnvironmentRejected'] == 0 for result in [a,b] for c in result['Counts'])
for result in [a,b]:
    assert result['SamplingOpportunities'] == result['EmptyEnvironmentSamples'] + result['SpacingRejectedSamples'] + sum(c['Candidates'] for c in result['Counts'])
baseline_path = repo/'artifacts/slope-spacing-review/flat.json'
if baseline_path.exists():
    baseline = json.loads(baseline_path.read_text())['result']['Placements']
    assert a['Placements'] == baseline, 'Flat layout changed from the original active-frontier sampler'
assert {c['SpeciesId']:c['Accepted'] for c in a['Counts']} == {'field-maple':176,'bush':597}
rows = []
for name, result in zip(['flat','gradient'], [a,b]):
    for zone, low, high in [('lower',0,70),('ramp',70,130),('upper',130,200)]:
        for species in ['field-maple','bush']:
            count = sum(p['SpeciesId'] == species and low <= p['Position']['X'] < high for p in result['Placements'])
            rows.append(dict(terrain=name,zone=zone,species=species,count=count))
with (out / 'zone-counts.csv').open('w') as f:
    writer = csv.DictWriter(f, fieldnames=['terrain','zone','species','count']); writer.writeheader(); writer.writerows(rows)
for row in rows: print(row)
def count(name,species): return next(r['count'] for r in rows if r['terrain']==name and r['zone']=='ramp' and r['species']==species)
assert count('gradient','field-maple') < count('flat','field-maple') * .5
assert count('gradient','bush') > count('flat','bush') * .5

plt.rcParams.update({'font.family':'DejaVu Sans','font.size':10})
fig, axs = plt.subplots(2, 2, figsize=(13,10), gridspec_kw={'height_ratios':[4,1.5]}, layout='constrained')
fig.set_facecolor('#f5f3ed')
colors = {'field-maple':'#358654','bush':'#c78527'}
profiles = {p['Id']:p for p in plans[0]['request']['Species']}
radii = {s:p['CrownRadiusM'] for s,p in profiles.items()}
# Enforce the new per-vegetation distance contract on both complete outputs.
for result in [a,b]:
    ps = result['Placements']
    for i,p in enumerate(ps):
        for q in ps[i+1:]:
            minimum = profiles[p['SpeciesId']]['PreferredSpacingRadiusM'] + profiles[q['SpeciesId']]['PreferredSpacingRadiusM']
            distance = np.hypot(p['Position']['X']-q['Position']['X'],p['Position']['Z']-q['Position']['Z'])
            assert distance + 1e-9 >= minimum

for col, (name, result, h) in enumerate(zip(['Flat control','Smoothed flat → ramp → flat'], [a,b], heights)):
    ax=axs[0,col]; ax.set_facecolor('#faf9f5')
    ax.axvspan(70,130,color='#9aa6b0',alpha=.13)
    for species in colors:
        ps=[p for p in result['Placements'] if p['SpeciesId']==species]
        collection=PatchCollection([Circle((p['Position']['X'],p['Position']['Z']),radii[species]) for p in ps],
            facecolor=colors[species],edgecolor=colors[species],alpha=.32,linewidth=.45)
        ax.add_collection(collection)
        ax.scatter([p['Position']['X'] for p in ps],[p['Position']['Z'] for p in ps],s=3,color=colors[species])
    for line in [70,130]:ax.axvline(line,color='#687482',ls='--',lw=.8)
    total={s:sum(p['SpeciesId']==s for p in result['Placements']) for s in colors}
    ax.set_title(f"{name}\n{total['field-maple']} field maples · {total['bush']} bushes",fontsize=13)
    ax.set(xlim=(0,200),ylim=(0,200),xlabel='X (m)',ylabel='Z (m)',aspect='equal')
    section=axs[1,col]; section.set_facecolor('#faf9f5')
    section.fill_between(x,0,h,color='#b8a785',alpha=.45)
    if col:
        section.plot(x, np.clip(x-70,0,60)*2, color='#aaa', ls='--', lw=1, label='Original ramp')
    section.plot(x,h,color='#756445',lw=2, label='10 m smoothing' if col else 'Flat')
    if col: section.legend(loc='upper left',fontsize=8,frameon=False)
    section.axvspan(70,130,color='#9aa6b0',alpha=.13)
    section.set(xlim=(0,200),ylim=(-5,140),xlabel='X (m)',ylabel='Elevation (m)')
    section.set_title('Terrain cross-section (same height scale)',fontsize=10)
    if col:section.text(100,75,'2:1 rise/run\n63.4°',ha='center',fontsize=10)
    else:section.text(100,55,'0° everywhere',ha='center')
fig.suptitle(f"Active-frontier sampler · local composition · abundance OFF\n200 × 200 m · seed 42 · 10 m terrain smoothing · 5 m environment grid", fontsize=15)
fig.legend(handles=[Patch(facecolor=colors[s],alpha=.5,label=(f"Field maple: {radii[s]:.2f} m crown radius" if s=='field-maple' else f"Wild green bush: {radii[s]:.2f} m crown radius")) for s in colors],
    loc='lower center',bbox_to_anchor=(.5,.025),ncol=2,frameon=False)
fig.text(.5, .008, '18°C throughout · shade disabled · illustrative slope responses · shaded band: x = 70–130 m', ha='center', fontsize=9)
# Reserve distinct space for the explanatory footer and species legend.
fig.get_layout_engine().set(rect=(0, .075, 1, .925))
fig.savefig(out/'comparison.png',dpi=150)
fig.savefig(out/'comparison.svg')
print('Verified exact heights, pairwise spacing, pre-placement accounting, and differential response.')
print(out/'comparison.png')
