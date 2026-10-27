#!/usr/bin/env python3
"""Plot a deterministic Perlin-perturbed version of the flat/ramp/flat fixture.
Requires numpy and matplotlib. Terrain only: does not run the foliage solver.
"""
import argparse
import json
import os
from pathlib import Path
os.environ.setdefault('MPLCONFIGDIR', '/tmp/treedistrict-matplotlib')
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import numpy as np
from matplotlib.colors import TwoSlopeNorm

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--seed', type=int, default=42)
parser.add_argument('--noise-amplitude', type=float, default=8, help='Metres multiplying the first Perlin octave; not peak displacement.')
parser.add_argument('--output', default='artifacts/noisy-heightmap')
args = parser.parse_args()
if not np.isfinite(args.noise_amplitude) or args.noise_amplitude < 0:
    parser.error('Noise amplitude must be finite and nonnegative.')
out = Path(__file__).resolve().parents[1] / args.output
out.mkdir(parents=True, exist_ok=True)
x = np.arange(201, dtype=float)
X, Z = np.meshgrid(x, x)

def perlin(x, z, wavelength, rng):
    """2D gradient noise with independent unit vectors and quintic interpolation."""
    u, v = x / wavelength, z / wavelength
    ix, iz = np.floor(u).astype(int), np.floor(v).astype(int)
    fx, fz = u - ix, v - iz
    angles = rng.uniform(0, 2*np.pi, (iz.max()+2, ix.max()+2))
    gx, gz = np.cos(angles), np.sin(angles)
    def dot(dx, dz):
        return gx[iz+dz, ix+dx]*(fx-dx) + gz[iz+dz, ix+dx]*(fz-dz)
    def fade(t): return t*t*t*(t*(t*6-15)+10)
    sx, sz = fade(fx), fade(fz)
    return ((1-sx)*dot(0,0)+sx*dot(1,0))*(1-sz) + ((1-sx)*dot(0,1)+sx*dot(1,1))*sz

# Same x=70..130 m ramp, 10 m box smoothing; 1:1 rise/run gives 45 degrees.
def integral(u): return .5*(np.maximum(u-70,0)**2 - np.maximum(u-130,0)**2)
smooth = (integral(X+5)-integral(X-5))/10
rng = np.random.default_rng(args.seed)
noise = args.noise_amplitude*(perlin(X,Z,32,rng) + .25*perlin(X,Z,16,rng))
height = smooth + noise
# Slope is the total surface gradient, including variation along Z.
dz, dx = np.gradient(height,1,1,edge_order=2)
slope = np.degrees(np.arctan(np.hypot(dx,dz)))
base_dz, base_dx = np.gradient(smooth,1,1,edge_order=2)
base_slope = np.degrees(np.arctan(np.hypot(base_dx,base_dz)))
interior = (X>=76)&(X<=124)
assert np.all(np.isfinite(height)) and np.all(np.isfinite(slope))
assert np.allclose(base_slope[interior],45)
if args.noise_amplitude > 0:
    assert np.any(slope[interior]<45) and np.any(slope[interior]>45)
metadata = dict(seed=args.seed,extentM=[200,200],sampleSpacingM=1,rampXM=[70,130],
    smoothingWindowM=10,baseRampDegrees=45,noise='2D gradient Perlin, quintic fade, two octaves',
    wavelengthM=[32,16],amplitudeMultipliersM=[args.noise_amplitude,args.noise_amplitude*.25],
    noiseDisplacementRangeM=[float(noise.min()),float(noise.max())],
    rampSlopeRangeDegrees=[float(slope[interior].min()),float(slope[interior].max())],
    rampAreaAbove45Percent=float(100*np.mean(slope[interior]>45)),
    note='Terrain-only diagnostic. Slope from 1m height derivatives; no foliage or environment-grid interpolation. Negative elevations are permitted; water is not modelled.')
(out/'metadata.json').write_text(json.dumps(metadata,indent=2)+'\n')
(out/'heightmap.json').write_text(json.dumps(dict(originM=[0,0],sampleSpacingM=1,heightsM=height.tolist()),separators=(',',':'))+'\n')
np.savetxt(out/'heights.csv',height,delimiter=',',fmt='%.6f')
np.savetxt(out/'slopes-degrees.csv',slope,delimiter=',',fmt='%.6f')

plt.rcParams.update({'font.family':'DejaVu Sans','font.size':10})
fig=plt.figure(figsize=(13,11),layout='constrained',facecolor='#f7f5ef')
ax=fig.add_subplot(2,2,1,projection='3d')
ax.plot_surface(X[::2,::2],Z[::2,::2],height[::2,::2],cmap='terrain',linewidth=0,antialiased=True)
ax.set(xlabel='X (m)',ylabel='Z (m)',zlabel='Height (m)',title='Terrain surface · equal metre scale')
ax.set_box_aspect((200,200,float(np.ptp(height)))); ax.view_init(elev=27,azim=-120)
ax=fig.add_subplot(2,2,2)
im=ax.imshow(height,origin='lower',extent=(0,200,0,200),cmap='terrain')
fig.colorbar(im,ax=ax,label='Elevation (m)',shrink=.85)
ax.set(title='Heightmap · 1 m samples',xlabel='X (m)',ylabel='Z (m)')
ax=fig.add_subplot(2,2,3)
im=ax.imshow(slope,origin='lower',extent=(0,200,0,200),cmap='RdYlGn_r',norm=TwoSlopeNorm(vmin=0,vcenter=45,vmax=65))
ax.contour(X,Z,slope,levels=[45],colors=['#3b3545'],linewidths=.65)
for line in [70,130]: ax.axvline(line,color='#666',ls='--',lw=.7)
fig.colorbar(im,ax=ax,label='Local slope (degrees)',shrink=.85)
ax.set(title='Slope · dark contours mark the 45° tree cap',xlabel='X (m)',ylabel='Z (m)')
ax=fig.add_subplot(2,2,4)
ax.plot(x,smooth[100],color='#222',ls='--',lw=1.8,label='Smooth 45° baseline')
for z,color in [(40,'#327a56'),(100,'#b16c32'),(160,'#516ca3')]:
    ax.plot(x,height[z],lw=1.7,color=color,label=f'Noisy section at Z = {z} m')
ax.axvspan(70,130,color='#b9c1ca',alpha=.2)
ax.set(title='Cross-sections through the same hillside',xlabel='X (m)',ylabel='Elevation (m)',xlim=(0,200))
ax.legend(loc='upper left',fontsize=9,frameon=False)
lo,hi=metadata['rampSlopeRangeDegrees']
fig.suptitle(f'Flat → 45° ramp → flat, with Perlin variation\nRamp interior: {lo:.1f}–{hi:.1f}° · {metadata["rampAreaAbove45Percent"]:.0f}% above the tree cap · seed {args.seed}',fontsize=16)
fig.savefig(out/'terrain-review.png',dpi=150)
fig.savefig(out/'terrain-review.svg')
print(json.dumps(metadata,indent=2))
print(out/'terrain-review.png')
