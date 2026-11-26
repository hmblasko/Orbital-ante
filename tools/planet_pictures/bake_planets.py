"""Paints the pictures the far-off planets wear that do not come from the ground itself: the cloud tops of the
four gas giants, Venus's unbroken cloud deck, Earth's weather, Mars's wisps of ice cloud and Saturn's rings.
The planets are the same every run, so these are painted once, here, and kept as files the game reads
(Resources/Planets). Nothing here is a photograph: every picture is made from noise by the sums below, shaped
after what the real planets look like. The same seed gives the same files every time.

Run:  python3 bake_planets.py            (about a minute; needs numpy, scipy, pillow)
"""
import os, sys
import numpy as np
from PIL import Image
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "sky"))
from skylib import vnoise, fbm, smooth, F

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "..", "unity", "Assets", "OrbitalAnte", "Resources", "Planets")
MW, MH = 2048, 1024


def lin(c): c = np.asarray(c, F); return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
def srgb(c): c = np.clip(c, 0, 1); return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)
def hexc(h): return lin([int(h[i:i + 2], 16) / 255 for i in (1, 3, 5)])


def map_dirs(W=None, H=None):
    W = W or MW; H = H or MH
    u = (np.arange(W) + 0.5) / W; v = (np.arange(H) + 0.5) / H            # row 0 is the north pole
    U, V = np.meshgrid(u, v)
    pol = V * np.pi; az = U * 2 * np.pi
    return np.stack([np.sin(pol) * np.cos(az), np.cos(pol), np.sin(pol) * np.sin(az)], -1).astype(F)


def swirl(d, seed, count, radius, turn):
    """Storm systems: the pattern is wound round a handful of centres, tighter toward each middle."""
    rng = np.random.default_rng(seed)
    for i in range(count):
        c = rng.normal(size=3); c[1] *= 0.75; c /= np.linalg.norm(c); c = c.astype(F)
        if abs(c[1]) < 0.3: c[1] = 0.3 * np.sign(c[1] + 1e-6); c /= np.linalg.norm(c)
        cd = (d * c).sum(-1)
        ang = np.arccos(np.clip(cd, -1, 1))
        r = radius * rng.uniform(0.7, 1.3)
        th = (turn * rng.uniform(0.7, 1.2) * np.sign(c[1]) * np.exp(-(ang / r) ** 2))[..., None]
        d = d * np.cos(th) + np.cross(np.broadcast_to(c, d.shape), d) * np.sin(th) + c * cd[..., None] * (1 - np.cos(th))
    return d


def clouds_earth(seed=3):
    d0 = map_dirs()
    lat = np.arcsin(d0[..., 1])
    d = swirl(d0, seed, 9, 0.30, 2.6)
    x, y, z = d[..., 0], d[..., 1], d[..., 2]
    wx = fbm(x * 2.0, y * 2.0, z * 2.0, seed, 4); wy = fbm(x * 2.0 + 9, y * 2.0, z * 2.0, seed + 50, 4); wz = fbm(x * 2.0, y * 2.0 + 7, z * 2.0, seed + 90, 4)
    px, py, pz = x + 0.5 * wx, y * 1.7 + 0.3 * wy, z + 0.5 * wz
    big = fbm(px * 3.2, py * 3.2, pz * 3.2, seed + 7, 6, 0.55)
    mid = fbm(px * 9 + wz, py * 9, pz * 9 + wx, seed + 13, 5, 0.6)
    fine = fbm(px * 34, py * 34, pz * 34, seed + 21, 4, 0.6)
    # More cloud along the equator's rain belt and the stormy middle latitudes, less in the dry belts between.
    belt = 0.07 * np.exp(-(lat / 0.12) ** 2) + 0.10 * np.exp(-((np.abs(lat) - 0.95) / 0.3) ** 2) - 0.10 * np.exp(-((np.abs(lat) - 0.45) / 0.16) ** 2)
    n = big + belt + 0.30 * mid - 0.10
    c = smooth(0.0, 0.42, n) ** 1.25
    c = c * smooth(-0.35, 0.25, fine + 1.2 * c - 0.35)        # thin cloud breaks into puffs, thick cloud stays whole
    return np.clip(c, 0, 1).astype(F)


def clouds_wisps(seed, amount):
    d = map_dirs(); x, y, z = d[..., 0], d[..., 1], d[..., 2]
    w = fbm(x * 3, y * 3, z * 3, seed, 4)
    n = fbm(x * 5 + w, y * 16, z * 5 + w, seed + 3, 6, 0.55)
    return (smooth(0.12, 0.6, n) ** 1.5 * amount).astype(F)


def deck(seed, base, dark, light, streak=0.5):
    """A thick unbroken cloud deck (Venus, Titan): pale swept streaks, no ground."""
    d = map_dirs(); x, y, z = d[..., 0], d[..., 1], d[..., 2]
    w = fbm(x * 1.6, y * 1.6, z * 1.6, seed, 4)
    n = fbm(x * 2.2 + 0.8 * w, y * 7 + 0.8 * w, z * 2.2 + 0.8 * w, seed + 5, 6, 0.55)
    t = smooth(-0.35, 0.35, n)[..., None]
    col = base * (1 - streak) + streak * (dark * (1 - t) + light * t)
    return col.astype(F)


def giant(seed, bands, contrast=1.0, shear=1.0, spots=()):
    """Cloud tops of a gas giant. bands: list of (latitude in degrees, colour) from south to north."""
    d = map_dirs(); x, y, z = d[..., 0], d[..., 1], d[..., 2]
    lat = np.degrees(np.arcsin(y))
    lats = np.array([b[0] for b in bands], F); cols = np.array([b[1] for b in bands], F)
    # Jets: neighbouring bands slide past each other and their edges roll up into curls.
    w1 = fbm(x * 2.5, y * 2.5, z * 2.5, seed, 4)
    curl = fbm(x * 7 + 1.5 * w1, y * 22, z * 7 + 1.5 * w1, seed + 11, 6, 0.58)
    fine = fbm(x * 18 + 3 * curl, y * 70, z * 18 + 3 * curl, seed + 23, 5, 0.6)
    edge = np.abs(np.sin(np.radians(lat) * 9 + 1.3))                      # where jets run fastest
    lat2 = lat + shear * (5.5 * curl * (0.35 + 0.65 * edge) + 1.6 * fine + 2.0 * w1)
    col = np.stack([np.interp(lat2, lats, cols[:, c]) for c in range(3)], -1)
    grain = 1 + contrast * (0.16 * fine + 0.10 * curl)
    col = col * grain[..., None]
    lon = np.degrees(np.arctan2(z, x))
    for (slat, slon, rlat, rlon, colour, strength) in spots:
        dl = ((lon - slon + 180) % 360 - 180) / rlon; db = (lat - slat) / rlat
        r = np.sqrt(dl * dl + db * db)
        swirl = fbm(x * 30 + np.cos(r * 6) * 2, y * 30 + np.sin(r * 6) * 2, z * 30, seed + 40, 4)
        m = smooth(1.15, 0.75, r * (1 + 0.08 * swirl)) * strength
        ring = np.exp(-((r - 1.0) / 0.13) ** 2) * 0.35 * strength
        col = col * (1 - m[..., None]) + np.asarray(colour, F) * (0.85 + 0.3 * smooth(1, 0, r))[..., None] * m[..., None]
        col = col * (1 + ring[..., None] * 0.6)
    return np.clip(col, 0, 4).astype(F)


def ring_profile(n=2048, seed=5):
    """Opacity and brightness across the rings, inner edge to outer: faint C, dense bright B, the dark Cassini
    gap, A with its thin gap, a hair of an F ring. Hundreds of ringlets on top."""
    t = (np.arange(n) + 0.5) / n
    zero = np.zeros(n, F)
    rip = 0.5 + 0.5 * fbm(t * 60, zero, zero, seed, 6, 0.65)
    fine = 0.5 + 0.5 * fbm(t * 420, zero + 3, zero, seed + 4, 4, 0.7)
    op = np.zeros(n, F)
    op += 0.16 * smooth(0.0, 0.03, t) * smooth(0.27, 0.24, t) * (0.6 + 0.8 * rip)                 # C
    op += 0.92 * smooth(0.25, 0.29, t) * smooth(0.615, 0.605, t) * (0.72 + 0.28 * rip)            # B
    op += 0.06 * smooth(0.60, 0.61, t) * smooth(0.665, 0.655, t)                                  # Cassini gap
    op += 0.62 * smooth(0.655, 0.67, t) * smooth(0.93, 0.925, t) * (0.8 + 0.2 * rip)              # A
    op *= 1 - 0.9 * np.exp(-((t - 0.865) / 0.004) ** 2)                                           # thin gap in A
    op += 0.3 * np.exp(-((t - 0.975) / 0.004) ** 2)                                               # F
    op = np.clip(op * (0.8 + 0.4 * fine), 0, 1)
    bright = 0.55 + 0.45 * smooth(0.1, 0.9, op) * (0.85 + 0.3 * fine)
    return op.astype(F), bright.astype(F)


# ---------------------------------------------------------------- the planets

def jupiter_bands():
    c = hexc
    return [(-90, c("#77736E")), (-70, c("#8E877C")), (-58, c("#B7A88F")), (-52, c("#9C8468")), (-46, c("#D3C6AC")), (-40, c("#A67E5C")), (-35, c("#DDD0B6")), (-27, c("#E6DAC0")),
            (-20, c("#A8683F")), (-12, c("#8E5234")), (-7, c("#DFD2B6")), (0, c("#E9DDC2")), (5, c("#CFC0A2")), (9, c("#8F5636")),
            (16, c("#A9714A")), (21, c("#E0D3B8")), (27, c("#CBBB9E")), (32, c("#A8805E")), (38, c("#D2C4A8")), (44, c("#A08A70")), (50, c("#C4B79E")), (58, c("#9A8F80")), (70, c("#817C74")), (90, c("#74716C"))]


def jupiter(spot_longitude=40.0):
    c = hexc; L = spot_longitude
    return giant(21, jupiter_bands(), 1.0, 1.0, spots=[
        (-21, L, 7, 12, c("#B5512E"), 0.95),                 # the great red storm
        (-34, L - 34, 2.4, 3.6, c("#F3EEE2"), 0.9), (-34, L + 26, 2.0, 3.0, c("#F3EEE2"), 0.8), (-34, L + 150, 2.2, 3.2, c("#F3EEE2"), 0.85),
        (13, L - 22, 1.8, 4.5, c("#6E5A52"), 0.7), (13, L + 95, 1.6, 4.0, c("#6E5A52"), 0.6), (13, L - 140, 1.7, 4.2, c("#6E5A52"), 0.65)])


def saturn():
    c = hexc
    bands = [(-90, c("#B9A77A")), (-60, c("#CDBB8C")), (-40, c("#E0CF9E")), (-25, c("#D2BE8A")), (-12, c("#EBDCAE")), (0, c("#F0E3BA")),
             (10, c("#E2D09E")), (22, c("#D6C390")), (38, c("#E3D3A4")), (55, c("#C9B988")), (72, c("#B3A67E")), (90, c("#9FA08A"))]
    return giant(31, bands, 0.45, 0.45)


def uranus():
    # Nearly featureless: the faintest bands and a paler cap, as the real one shows.
    c = hexc
    bands = [(-90, c("#B5E6E2")), (-55, c("#A9E0DD")), (-30, c("#A1DCDA")), (0, c("#A6E0DD")), (25, c("#9FDAD9")), (50, c("#AAE1DE")), (72, c("#BDEBE6")), (90, c("#C6EFEA"))]
    return giant(41, bands, 0.12, 0.2)


def neptune():
    c = hexc
    bands = [(-90, c("#2C4FA8")), (-65, c("#3458C4")), (-45, c("#2F52B6")), (-25, c("#3F66D9")), (-8, c("#3A60CE")), (8, c("#4069DC")),
             (28, c("#3559C6")), (48, c("#3D64D4")), (70, c("#3154BC")), (90, c("#2B4CA4")) ]
    return giant(51, bands, 0.35, 0.55, spots=[(-20, 60, 5, 9, c("#1E3478"), 0.8), (-28, 64, 1.0, 5.5, c("#DCE8FF"), 0.8), (-42, 200, 1.2, 2.2, c("#DCE8FF"), 0.75), (32, 300, 0.8, 6, c("#C8D8FF"), 0.5)])


def save(name, rgb, alpha=None, size=None):
    os.makedirs(OUT, exist_ok=True)
    a = (srgb(rgb) * 255 + 0.5).astype("u1")
    if alpha is not None:
        a = np.dstack([a, (np.clip(alpha, 0, 1) * 255 + 0.5).astype("u1")])
    im = Image.fromarray(a)
    if size: im = im.resize(size, Image.LANCZOS)
    im.save(os.path.join(OUT, name + ".png"), optimize=True)
    print(name, im.size, round(os.path.getsize(os.path.join(OUT, name + ".png")) / 1e6, 2), "MB")


def bake():
    white = np.ones((MH, MW, 3), F)
    save("JupiterTops", jupiter())
    save("SaturnTops", saturn())
    save("UranusTops", uranus(), size=(1024, 512))
    save("NeptuneTops", neptune(), size=(1024, 512))
    save("VenusClouds", deck(12, hexc("#E9DDB8"), hexc("#C9B27A"), hexc("#FBF4DC"), 0.55), size=(1024, 512))
    save("EarthClouds", white, clouds_earth())
    save("MarsClouds", white * np.array([0.95, 0.9, 0.85], F), clouds_wisps(8, 1.0), size=(1024, 512))
    # The rings: one strip from the inner edge (left) to the outer; colour is brightness, alpha how much they hide.
    op, bright = ring_profile(1024)
    tint = hexc("#E3CF9A") * np.array([1.0, 0.97, 0.92], F)
    strip = np.repeat((bright[:, None] * tint[None, :])[None, :, :], 4, 0)
    save("SaturnRings", strip, np.repeat(op[None, :], 4, 0))


if __name__ == "__main__":
    bake()
