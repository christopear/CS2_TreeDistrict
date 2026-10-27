#!/usr/bin/env python3
"""Compare independent composition/abundance switches on the same synthetic slope.
Run with a Python environment containing matplotlib. Outputs actual C# plans and PNG/SVG.
"""
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
repo = Path(__file__).resolve().parents[1]
out = repo / 'artifacts/local-policies-review'
out.mkdir(parents=True, exist_ok=True)
subprocess.run(['bash','scripts/dotnet.sh','build','tools/TreeDistrict.Foliage.Cli','--no-restore'],cwd=repo,check=True)
fig, axes = plt.subplots(2,2,figsize=(12,12),layout='constrained')
colors = {'field-maple':'#358654','bush':'#c78527'}
summary=[]
for ax,(composition,abundance) in zip(axes.flat,[(False,False),(True,False),(False,True),(True,True)]):
    name = f'composition-{int(composition)}-abundance-{int(abundance)}'
    command=['bash','scripts/dotnet.sh','run','--project','tools/TreeDistrict.Foliage.Cli','--no-build','--',
        '--generator','proton','--profiles','data/slope-demo.v1.json','--terrain-shape','plateau',
        '--terrain-smoothing','10','--grade-percent','200','--cooling-per-km','0','--temperature','18',
        '--no-shade','--seed','42','--size','200','--environment-cell-size','5','--output',str(out/f'{name}.json')]
    if not composition: command.append('--no-composition')
    command.append('--abundance' if abundance else '--no-abundance')
    subprocess.run(command,cwd=repo,check=True)
    plan=json.loads((out/f'{name}.json').read_text())
    profiles={p['Id']:p for p in plan['request']['Species']}
    placements=plan['result']['Placements']
    assert all(c['EnvironmentRejected']==0 for c in plan['result']['Counts'])
    counts={s:sum(p['SpeciesId']==s for p in placements) for s in colors}
    ramp={s:sum(p['SpeciesId']==s and 70<=p['Position']['X']<130 for p in placements) for s in colors}
    summary.append(dict(composition=composition,abundance=abundance,total=counts,ramp=ramp))
    ax.set_facecolor('#faf9f5'); ax.axvspan(70,130,color='#9aa6b0',alpha=.15)
    for species,color in colors.items():
        plants=[p for p in placements if p['SpeciesId']==species]
        ax.add_collection(PatchCollection([Circle((p['Position']['X'],p['Position']['Z']),profiles[species]['CrownRadiusM']) for p in plants],
            facecolor=color,edgecolor=color,alpha=.35,linewidth=.45))
        ax.scatter([p['Position']['X'] for p in plants],[p['Position']['Z'] for p in plants],s=3,color=color)
    for x in [70,130]: ax.axvline(x,color='#687482',ls='--',lw=.8)
    ax.set(xlim=(0,200),ylim=(0,200),aspect='equal',xlabel='X (m)',ylabel='Z (m)')
    ax.set_title(f"Composition {'ON' if composition else 'OFF'} · Abundance {'ON' if abundance else 'OFF'}\n"
                 f"{counts['field-maple']} planes + {counts['bush']} bushes; ramp: {ramp['field-maple']} + {ramp['bush']}")
fig.suptitle('Independent local policies · same smoothed 2:1 slope\n200 × 200 m · seed 42 · 5 m environment grid',fontsize=16)
fig.legend(handles=[Patch(facecolor=c,alpha=.5,label=s) for s,c in zip(['London plane (CS2 field maple)','Wild green bush'],colors.values())],loc='lower center',ncol=2,frameon=False)
fig.get_layout_engine().set(rect=(0,.04,1,.96))
fig.savefig(out/'policy-switches.png',dpi=140);fig.savefig(out/'policy-switches.svg')
(out/'policy-counts.json').write_text(json.dumps(summary,indent=2)+'\n')
print(json.dumps(summary,indent=2))
