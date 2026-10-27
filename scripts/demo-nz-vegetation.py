#!/usr/bin/env python3
"""Regenerate the NZ proxy preset on the noisy hill and plot actual solver placements."""
import json,os,subprocess,sys
from pathlib import Path
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
subprocess.run([sys.executable,'scripts/demo-noisy-heightmap.py'],cwd=repo,check=True)
subprocess.run(['bash','scripts/dotnet.sh','run','--project','tools/TreeDistrict.Foliage.Cli','--',
    '--vegetation-set','nz-regenerating-bush','--heightmap',str(out/'heightmap.json'),
    '--temperature','12','--no-shade','--output',str(out/'nz-vegetation.json')],cwd=repo,check=True)
plan=json.loads((out/'nz-vegetation.json').read_text())
h=np.array(json.loads((out/'heightmap.json').read_text())['heightsM'])
dz,dx=np.gradient(h,1,1,edge_order=2); slope=np.degrees(np.arctan(np.hypot(dx,dz)))
profiles={p['Id']:p for p in plan['request']['Species']}
styles={'cs2:GreenBushWild01':('#98ad39','Low scrub'), 'cs2:GreenBushWild02':('#348c79','Taller scrub'), 'cs2:NA_LindenTree01':('#234f38','Small canopy trees')}
fig,axs=plt.subplots(1,2,figsize=(13,7.6),facecolor='#f7f5ef')
fig.subplots_adjust(left=.065,right=.94,bottom=.19,top=.80,wspace=.23)
ax=axs[0]; ax.set_facecolor('#faf9f5')
ax.contourf(np.arange(201),np.arange(201),slope,levels=[45,90],colors=['#efd2ce'],alpha=.65)
ax.contour(np.arange(201),np.arange(201),slope,levels=[45],colors=['#a47e7e'],linewidths=.55)
handles=[]
for species,(color,label) in styles.items():
 ps=[p for p in plan['result']['Placements'] if p['SpeciesId']==species]
 ax.add_collection(PatchCollection([Circle((p['Position']['X'],p['Position']['Z']),profiles[species]['CrownRadiusM']) for p in ps],facecolor=color,edgecolor=color,alpha=.45,linewidth=.6))
 ax.scatter([p['Position']['X'] for p in ps],[p['Position']['Z'] for p in ps],s=4,color=color)
 handles.append(Patch(facecolor=color,alpha=.65,label=f'{label}: {len(ps)}'))
ax.set(title='NZ regenerating bush · actual solver placements',xlim=(0,200),ylim=(0,200),aspect='equal',xlabel='X (m)',ylabel='Z (m)')
ax=axs[1]
im=ax.imshow(slope,origin='lower',extent=(0,200,0,200),cmap='RdYlGn_r',norm=TwoSlopeNorm(vmin=0,vcenter=45,vmax=65))
ax.contour(np.arange(201),np.arange(201),slope,levels=[45],colors=['#544552'],linewidths=.7)
ax.set(title='Same noisy hillside · contours at 45°',xlabel='X (m)',ylabel='Z (m)')
fig.colorbar(im,ax=ax,label='Slope (degrees)',fraction=.047,pad=.035)
fig.suptitle('New Zealand scrub mix on the Perlin hillside',fontsize=17,y=.965)
fig.legend(handles=handles,loc='lower center',bbox_to_anchor=(.5,.09),ncol=3,frameon=False)
fig.text(.5,.065,'Pink terrain exceeds 45°: scrub allowed, tree centres excluded. Circles show crown footprints.',ha='center',fontsize=10)
fig.text(.5,.035,'NZ profile overrides · 12°C reference at sea level · 6.5°C/km cooling · composition ON · pre-abundance and shade OFF',ha='center',fontsize=9)
fig.savefig(out/'nz-vegetation.png',dpi=170)
fig.savefig(out/'nz-vegetation.svg')
