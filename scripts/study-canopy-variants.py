"""Reproducible offline shape studies; does not modify the C# or game renderer.

Run after TreeDistrict.Canopy.Preview. Needs NumPy and Matplotlib.
All panels share terrain, scale, palette, lighting, camera and sampling density.
"""
import json
import argparse
from pathlib import Path
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import numpy as np

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--between', action='store_true', help='B to D structure blends with B-level fine roughness')
args = parser.parse_args()
OUT = Path('artifacts/canopy-between-b-d' if args.between else 'artifacts/canopy-variants')
OUT.mkdir(parents=True, exist_ok=True)
source = json.loads(Path('artifacts/canopy-preview.json').read_text())
n, size = source['resolution'], source['size']
step = size / (n - 1)
x, z = np.meshgrid(np.linspace(0, size, n), np.linspace(0, size, n))


def hash2(ix, iz, seed):
    with np.errstate(over='ignore'):
        h = np.asarray(ix).astype(np.uint32) * np.uint32(374761393)
        h = h + np.asarray(iz).astype(np.uint32) * np.uint32(668265263)
        h = h + np.uint32((seed * 2246822519) & 0xffffffff)
        h = (h ^ (h >> 13)) * np.uint32(1274126177)
        return (h ^ (h >> 16)).astype(float) / 4294967295


def noise(u, v, seed):
    ix, iz = np.floor(u).astype(int), np.floor(v).astype(int)
    a, b = u - ix, v - iz
    a, b = a*a*(3-2*a), b*b*(3-2*b)
    return ((1-a)*hash2(ix, iz, seed)+a*hash2(ix+1, iz, seed))*(1-b) + \
        ((1-a)*hash2(ix, iz+1, seed)+a*hash2(ix+1, iz+1, seed))*b


def field(scale, seed):
    return noise((.8*x+.6*z)/scale, (-.6*x+.8*z)/scale, seed)


def lobes(cell, radius, amplitude, seed, exponent=.5):
    # Distort the coordinate field, then overlap asymmetric, rotated lobes.
    u = x + 3*(field(19, seed+1)-.5)
    v = z + 3*(field(21, seed+2)-.5)
    ix, iz = np.floor(u/cell).astype(int), np.floor(v/cell).astype(int)
    best = np.zeros_like(x)
    for dz in range(-2, 3):
        for dx in range(-2, 3):
            cx, cz = ix+dx, iz+dz
            px = (cx+.05+.9*hash2(cx, cz, seed+3))*cell
            pz = (cz+.05+.9*hash2(cx, cz, seed+4))*cell
            angle = hash2(cx, cz, seed+5)*np.pi
            a, b = u-px, v-pz
            rx = radius*(.8+.5*hash2(cx, cz, seed+6))
            rz = radius*(.6+.6*hash2(cx, cz, seed+7))
            du = (a*np.cos(angle)+b*np.sin(angle))/rx
            dv = (-a*np.sin(angle)+b*np.cos(angle))/rz
            q = np.maximum(0, 1-du*du-dv*dv)
            h = amplitude*(.55+.65*hash2(cx, cz, seed+8))*q**exponent
            best = np.maximum(best, h)
    return best


def blur(a, radius):
    p = np.pad(a, radius, mode='edge')
    p = np.pad(p, ((1, 0), (1, 0)))
    s = p.cumsum(0).cumsum(1)
    w = 2*radius+1
    return (s[w:, w:] - s[:-w, w:] - s[w:, :-w] + s[:-w, :-w])/(w*w)


# One gently rolling hill and adjoining dip; max ground gradient fixed at 28%.
ground = 24*np.exp(-((x-96)**2/75**2+(z-112)**2/83**2)) + 4*np.sin(x/63+z/92)
gz, gx = np.gradient(ground, step)
ground *= .28 / np.max(np.hypot(gx, gz))
ground -= ground.min()
distance = np.minimum.reduce([x, z, size-x, size-z])
t = np.minimum(1, distance/(3+2*field(6, 900)))
edge = np.sqrt(t*(2-t))

branch = lobes(3.1, 3.0, 2.7, 350, .42)
rough = 1.2*(field(.85, 405)-.5) + .55*(field(.32, 407)-.5)
broad = 7 + lobes(11, 12, 6, 300, .38)
layered = 7 + lobes(13, 13, 5, 500, .4) + lobes(5, 5.1, 3.3, 520, .45)
layered += 1.4*np.maximum(0, lobes(17, 8, 14, 560, .55)-9)
broken = 6 + lobes(10, 10, 6, 600, .42) + lobes(3.8, 3.5, 4.1, 620, .55)
heights = [
    np.asarray(source['height']).reshape(n, n),
    .5 + (broad + branch + rough - .5)*edge,
    .5 + (layered + .7*branch + 1.5*rough - .5)*edge,
    .5 + (broken + 1.8*rough - 2.5*field(7, 650) - .5)*edge,
]
labels = ['A — Current C# shape', 'B — Broad, interlocking crowns',
          'C — Layered branch masses', 'D — Broken, ragged canopy']
notes = ['Control: previous C# surface', 'Wide crowns + smaller asymmetric lobes',
         'Layered foliage with occasional taller clumps', 'Stronger local relief and irregular hollows']
if args.between:
    # Blend metre-scale structure, NOT D's amplified sub-metre roughness.
    # This keeps B's texture while bringing in progressively stronger hollows.
    b_structure = broad + branch
    d_structure = broken - 2.5*field(7, 650)
    weights = [0, .25, .5, .75]
    heights = [.5 + ((1-w)*b_structure + w*d_structure + rough - .5)*edge
               for w in weights]
    labels = ['B — Original', 'B/D 25 — Subtle hollows',
              'B/D 50 — Balanced structure', 'B/D 75 — Stronger breakup']
    notes = ['Unchanged reference', '25% toward D’s larger shapes',
             '50% toward D’s larger shapes', '75% toward D’s larger shapes']

# Common material prevents palette or lighting differences masquerading as shape changes.
patch = field(13, 800)
base = np.stack((.28+.07*patch, .35+.045*patch, .18+.055*patch), axis=-1)
grain = .83+.17*field(.32, 810)
light = np.array([-.5, -.35, .79]); light /= np.linalg.norm(light)


def draw(ax, h, i, close=False):
    total = ground+h
    dz, dx = np.gradient(total, step)
    normal = np.stack((-dx, -dz, np.ones_like(dx)), axis=-1)
    normal /= np.linalg.norm(normal, axis=-1, keepdims=True)
    diffuse = np.maximum(0, normal @ light)
    # Local concavity estimate, not a physical occlusion/shadow renderer.
    ao = np.exp(-.24*np.maximum(0, blur(h, 7)-h))
    rgb = np.clip(base*(grain*ao*(.46+1.05*diffuse))[:, :, None], 0, 1)
    sl = np.s_[140:261, 140:261] if close else np.s_[:, :]
    ax.plot_surface(x[sl], z[sl], total[sl], facecolors=rgb[sl], rstride=1,
                    cstride=1, linewidth=0, antialiased=False, shade=False)
    span = 60 if close else size
    low = 70 if close else 0
    ax.set_xlim(low, low+span); ax.set_ylim(low, low+span)
    lo, hi = (float(ground[sl].min()), max(float((ground+v)[sl].max()) for v in heights)+3) if close else (0, 52)
    ax.set_zlim(lo, hi)
    ax.set_box_aspect((span, span, hi-lo), zoom=1.02)
    ax.view_init(elev=33, azim=-63)
    ax.set_facecolor('#bebdb5'); ax.set_axis_off()
    ax.set_title(labels[i]+'\n'+notes[i], fontsize=12, pad=0)


for close in (False, True):
    fig = plt.figure(figsize=(16, 12), facecolor='#bebdb5')
    for i, h in enumerate(heights):
        ax = fig.add_subplot(2, 2, i+1, projection='3d', computed_zorder=False)
        draw(ax, h, i, close)
    fig.suptitle(('60 m detail crops' if close else '200 m rolling-hill canopy variants') +
                 ' — offline shape studies, not CS2', fontsize=18)
    footer = ('Fine roughness stays at B’s level in every panel. ' if args.between else '')
    fig.text(.5, .02, footer + 'Same hill, camera, palette and light. No vertical exaggeration.\n'
             '0.5 m field samples; offline renderer, not the game mesh/shader.', ha='center', fontsize=10)
    fig.subplots_adjust(left=0, right=1, top=.88, bottom=.05, wspace=0, hspace=.12)
    output = OUT / ('details.png' if close else 'comparison.png')
    fig.savefig(output, dpi=135); plt.close(fig)
    print(output)

np.savez_compressed(OUT/'fields.npz', ground=ground, heights=np.array(heights), labels=labels)
summary = {'areaMetres': size, 'sampleSpacingMetres': step, 'maxTerrainGrade': .28,
           'variants': [{'name': label, 'heightMin': float(h.min()), 'heightMax': float(h.max())}
                        for label, h in zip(labels, heights)]}
(OUT/'study.json').write_text(json.dumps(summary, indent=2))
