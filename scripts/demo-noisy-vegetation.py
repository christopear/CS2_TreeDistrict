#!/usr/bin/env python3
"""Run the C# sampler on smooth/noisy 45-degree hills with updated profiles."""
import json
import os
from pathlib import Path
import subprocess
import sys
os.environ.setdefault('MPLCONFIGDIR','/tmp/treedistrict-matplotlib')
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.collections import PatchCollection
from matplotlib.colors import TwoSlopeNorm
from matplotlib.patches import Circle,Patch
import numpy as np
repo=Path(__file__).resolve().parents[1]
out=repo/'artifacts/noisy-heightmap'
subprocess.run([sys.executable,'scripts/demo-noisy-heightmap.py'],cwd=repo,check=True,stdout=subprocess.DEVNULL)
subprocess.run(['bash','scripts/dotnet.sh','build','tools/TreeDistrict.Foliage.Cli','--no-restore'],cwd=repo,check=True)
ids=['cs2:NA_LondonPlaneTree01','cs2:GreenBushWild01']
plans=[]
for name,terrain in [('smooth',['--terrain-shape','plateau','--terrain-smoothing','10','--grade-percent','100']),
                     ('noisy',['--heightmap',str(out/'heightmap.json')])]:
    subprocess.run(['bash','scripts/dotnet.sh','run','--project','tools/TreeDistrict.Foliage.Cli','--no-build','--',
        '--generator','proton','--profiles','data/vegetation-environment.v1.json','--species',','.join(ids),
        '--size','200','--seed','42','--temperature','18','--cooling-per-km','6.5','--environment-cell-size','5',
        '--no-shade','--no-abundance','--output',str(out/f'{name}-vegetation.json')]+terrain,cwd=repo,check=True)
    plans.append(json.loads((out/f'{name}-vegetation.json').read_text()))
height=np.array(json.loads((out/'heightmap.json').read_text())['heightsM'])
dz,dx=np.gradient(height,1,1,edge_order=2)
slope=np.degrees(np.arctan(np.hypot(dx,dz)))
def blend(a,x,z):
    ix=min(int(x),199); iz=min(int(z),199); tx=x-ix; tz=z-iz
    return (a[iz,ix]*(1-tx)+a[iz,ix+1]*tx)*(1-tz)+(a[iz+1,ix]*(1-tx)+a[iz+1,ix+1]*tx)*tz
profiles={p['Id']:p for p in plans[1]['request']['Species']}
plants=plans[1]['result']['Placements']
for p in plants:
    x,z=p['Position']['X'],p['Position']['Z']
    s=np.degrees(np.arctan(np.hypot(blend(dx,x,z),blend(dz,x,z))))
    assert s<=profiles[p['SpeciesId']]['Terrain']['MaximumSlopeDegrees']+1e-9
    assert abs(p['ElevationM']-blend(height,x,z))<1e-8
for plan in plans:
    assert not plan['scatterSettings']['EnableLocalAbundance']
    assert plan['scatterSettings']['EnableLocalComposition']
    assert all(c['EnvironmentRejected']==0 and c['TerrainRejected']==0 for c in plan['result']['Counts'])
    ps=plan['result']['Placements']
    for i,p in enumerate(ps):
        for q in ps[i+1:]:
            minimum=profiles[p['SpeciesId']]['PreferredSpacingRadiusM']+profiles[q['SpeciesId']]['PreferredSpacingRadiusM']
            assert np.hypot(p['Position']['X']-q['Position']['X'],p['Position']['Z']-q['Position']['Z'])>=minimum-1e-9

plt.rcParams.update({'font.family':'DejaVu Sans','font.size':11})
fig,axs=plt.subplots(1,3,figsize=(18,7.6),layout='constrained',facecolor='#f7f5ef')
colors={ids[0]:'#2f8055',ids[1]:'#c18a29'}
for ax,plan,name in zip(axs[:2],plans,['Smooth 45° hill','Same hill + Perlin noise']):
    ax.set_facecolor('#faf9f5')
    if name.startswith('Same'):
        # Shaded mask is a terrain diagnostic, not post-generation deletion.
        ax.contourf(np.arange(201),np.arange(201),slope,levels=[45,90],colors=['#e8bfc1'],alpha=.4)
        ax.contour(np.arange(201),np.arange(201),slope,levels=[45],colors=['#a6787c'],linewidths=.55)
    else: ax.axvspan(70,130,color='#b9c1ca',alpha=.13)
    ps=plan['result']['Placements']; counts={s:sum(p['SpeciesId']==s for p in ps) for s in ids}
    for species,color in colors.items():
        group=[p for p in ps if p['SpeciesId']==species]
        ax.add_collection(PatchCollection([Circle((p['Position']['X'],p['Position']['Z']),profiles[species]['CrownRadiusM']) for p in group],facecolor=color,edgecolor=color,alpha=.38,linewidth=.5))
        ax.scatter([p['Position']['X'] for p in group],[p['Position']['Z'] for p in group],s=4,color=color)
    ax.set(title=f"{name}\n{counts[ids[0]]} London planes · {counts[ids[1]]} bushes",xlim=(0,200),ylim=(0,200),aspect='equal',xlabel='X (m)',ylabel='Z (m)')
    print(name,counts)
ax=axs[2]
im=ax.imshow(slope,origin='lower',extent=(0,200,0,200),cmap='RdYlGn_r',norm=TwoSlopeNorm(vmin=0,vcenter=45,vmax=65))
ax.contour(np.arange(201),np.arange(201),slope,levels=[45],colors=['#544552'],linewidths=.7)
ax.set(title='Local slope on noisy hill\n45° tree cap · 60° bush cap',xlabel='X (m)',ylabel='Z (m)')
fig.colorbar(im,ax=ax,label='Slope (degrees)',shrink=.8)
fig.suptitle('Actual sampler output · updated temperature and slope profiles\nComposition ON · pre-abundance OFF · 5 m environment grid · seed 42',fontsize=17)
fig.legend(handles=[Patch(facecolor=colors[ids[0]],alpha=.5,label='London plane crown'),Patch(facecolor=colors[ids[1]],alpha=.5,label='Wild green bush crown'),Patch(facecolor='#e8bfc1',alpha=.6,label='Terrain above 45°: bushes allowed, trees excluded')],loc='lower center',ncol=3,frameon=False)
fig.text(.5,.055,'18°C reference at 0 m · cooling 6.5°C/km · shade OFF · dots mark placement centres; crowns can overhang steep ground',ha='center',fontsize=10)
fig.get_layout_engine().set(rect=(0,.12,1,.74))
fig.savefig(out/'vegetation-comparison.png',dpi=160)
fig.savefig(out/'vegetation-comparison.svg')
print('Verified exact terrain heights, per-species hard slope caps and pairwise preferred spacing.')
print(out/'vegetation-comparison.png')
