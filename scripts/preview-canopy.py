"""Offline view of the C# canopy field, not a CS2 screenshot or HDRP match."""
import json
import sys
from pathlib import Path
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import numpy as np

source = Path(sys.argv[1] if len(sys.argv) > 1 else 'artifacts/canopy-preview.json')
data = json.loads(source.read_text())
n, size = data['resolution'], data['size']
h = np.asarray(data['height']).reshape(n, n)
rgb = np.asarray(data['colour']).reshape(n, n, 3)
detail = np.asarray(data['normal']).reshape(n, n, 3)
dz, dx = np.gradient(h, size / (n - 1))
# Approximate macro + detail lighting to inspect shapes/material, not engine exposure.
normal = np.stack((-dx + detail[:, :, 0], -dz + detail[:, :, 1], np.ones_like(dx)), axis=-1)
normal /= np.linalg.norm(normal, axis=-1, keepdims=True)
light = np.array([-.5, -.35, .79]); light /= np.linalg.norm(light)
diffuse = np.maximum(0, normal @ light)
lit = np.clip(rgb * (.5 + 1.1 * diffuse[:, :, None]), 0, 1)
x, z = np.meshgrid(np.linspace(0, size, n), np.linspace(0, size, n))
fig = plt.figure(figsize=(13, 8), facecolor='#b7b5ad')
ax = fig.add_subplot(111, projection='3d', computed_zorder=False)
ax.set_facecolor('#b7b5ad')
ax.plot_surface(x, z, h, facecolors=lit, rstride=1, cstride=1, linewidth=0,
                antialiased=False, shade=False)
ax.set_box_aspect((size, size, 42), zoom=1.05)
ax.set_xlim(0, size); ax.set_ylim(0, size)
ax.set_zlim(0, 42); ax.view_init(elev=30, azim=-60)
ax.set_axis_off()
fig.suptitle('200 m canopy study — offline field preview (not CS2)', fontsize=14)
fig.tight_layout()
output = source.with_suffix('.png'); fig.savefig(output, dpi=150)
print(output)

