"""Step 1 of the sky bake: the Milky Way as seen from the Sun, by marching rays through a model of the galaxy
(old disc, young arms, bar and bulge, clumpy dust that dims blue most). Galactic coordinates.
Writes cache/mw_<W>.npy (H, W, 3, linear) and cache/tau_<W>.npy (dust depth). Run: python3 milkyway.py 2048 (about 10 minutes)."""
import os, sys, time
import numpy as np
from skylib import *

W = int(sys.argv[1]) if len(sys.argv) > 1 else 1024
H = W // 2
dx, dy, dz, L, B = dirs_equirect(W, H)
dx = dx.ravel(); dy = dy.ravel(); dz = dz.ravel()
N = dx.size
I = np.zeros((N, 3), F)
tau = np.zeros(N, F)
KAP = np.array([0.78, 1.0, 1.32], F)          # dust dims blue most
steps = np.geomspace(0.03, 26.0, 84)
edges = np.concatenate([[0.0], np.sqrt(steps[:-1] * steps[1:]), [30.0]])
t0 = time.time()
for i, s in enumerate(steps):
    ds = F(edges[i + 1] - edges[i])
    x = -8.0 + s * dx; y = s * dy; z = 0.02 + s * dz
    R = np.sqrt(x * x + y * y); th = np.arctan2(y, x)
    az = np.abs(z)
    # spiral arms: 4 arms, 13 degree pitch, log spiral
    ph = 2.0 * (th - np.log(np.maximum(R, 0.3) / 8.0) / np.tan(np.radians(13.0)))
    arm2 = 0.5 + 0.5 * np.cos(ph + 0.6)
    arm4 = 0.5 + 0.5 * np.cos(2 * ph + 2.1)
    arms = (0.65 * arm2 ** 2 + 0.35 * arm4 ** 2) * smooth(2.5, 4.0, R)
    hole = smooth(0.0, 2.5, R) * 0.6 + 0.4
    thin = np.exp(-(R - 8.0) / 2.6 - az / 0.21) * hole
    thick = 0.07 * np.exp(-(R - 8.0) / 3.6 - az / 0.9)
    young = np.exp(-(R - 8.0) / 3.2 - az / 0.09) * arms * 1.6 * smooth(2.0, 4.0, R)
    # bar / bulge, tilted 27 degrees
    c, sn = np.cos(np.radians(-27)), np.sin(np.radians(-27))
    bx = x * c - y * sn; by = x * sn + y * c
    rb = np.sqrt((bx / 1.7) ** 2 + (by / 0.7) ** 2 + (z / 0.48) ** 2)
    bulge = 210.0 * np.exp(-rb * rb * 0.5) / (1 + rb) ** 1.2
    old = thin * (0.75 + 0.5 * arms) + thick
    em = np.stack([old * 1.08 + bulge * 1.22 + young * 0.78,
                   old * 1.00 + bulge * 1.00 + young * 0.95,
                   old * 0.86 + bulge * 0.66 + young * 1.30], -1)
    # dust: thin layer, in the arms, in clumps and filaments
    f = 1.0 / 0.32
    n = fbm(x * f, y * f, z * f * 2.2, 11, 6)
    n2 = ridged(x * f * 1.7 + 9, y * f * 1.7, z * f * 3.4, 23, 4)
    clump = np.clip(n * 1.9 + 0.38, 0, None) ** 2 * (0.25 + 1.6 * n2 ** 4) * 3.0
    dust = np.exp(-(R - 8.0) / 4.2 - az / 0.11) * (0.5 + 1.9 * arms) * clump * smooth(0.6, 2.4, R)
    # a few nearby cloud complexes off the plane (Ophiuchus/Taurus-like), random, within 0.5 kpc
    if s < 0.6:
        m = fbm(x * 11.0 + 40, y * 11.0, z * 11.0, 31, 5)
        dust = dust + np.clip(m - 0.3, 0, None) * 16.0 * np.exp(-az / 0.09)
    dt = F(2.3) * dust.astype(F) * ds
    tr = np.exp(-(tau + 0.5 * dt)[:, None] * KAP[None, :])
    I += (em * tr * ds * smooth(0.3, 1.1, s)).astype(F)
    tau += dt
    if i % 12 == 0:
        print(i, round(time.time() - t0, 1), flush=True)
I = I.reshape(H, W, 3)
os.makedirs("cache", exist_ok=True)
np.save(f"cache/mw_{W}.npy", I)
np.save(f"cache/tau_{W}.npy", tau.reshape(H, W))
print("done", I.max(), np.percentile(I, [50, 90, 99, 99.9]))
