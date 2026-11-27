"""Step 2 of the sky bake: writes the two files the game reads, in the game's own sky frame.

  unity/Assets/OrbitalAnte/Resources/Sky/DeepSpace.png   the picture wrapped round the sky (OA_Sky.shader)
  unity/Assets/OrbitalAnte/Resources/Sky/Stars.bytes     the stars drawn as points (Look/StarCatalogue.cs)

What is real: the 9,096 stars of the Bright Star Catalogue at their true places, brightness and colour; the
places and sizes of the Magellanic Clouds, Andromeda, Triangulum and nine bright nebulae; the lie of the Milky
Way (its centre, plane and tilt against the planets' plane). What is modelled: the Milky Way's light and dust
(milkyway.py), and the stars too faint for the catalogue (scattered by a seed, thicker where the Milky Way is
brighter). What is invented, on purpose (Matthew, 26 Nov 2026, "interesting galaxy structures"): the
SHOWPIECES list below.

Run:  python3 milkyway.py 2048   (once, about 10 minutes)
      python3 bake_sky.py        (about 2 minutes)       then  python3 preview_sky.py <folder>
Needs numpy, scipy and Pillow.
"""
import os, struct, sys
import numpy as np
from PIL import Image
from scipy import ndimage
from skylib import *

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "..", "unity", "Assets", "OrbitalAnte", "Resources", "Sky")
W, H = 4096, 2048
SRC = 2048
SPRITE_FAINTEST = 8.2        # stars down to this magnitude are drawn as points by the game
SPRITE_EXTRA = 18000         # how many of those are past the catalogue (scattered by the seed)
BAKED_FAINTEST = 10.6        # fainter ones, down to this, are dots in the picture
BAKED_COUNT = 70000
SEED = 4104

# ---------------------------------------------------------------- the game's sky frame
# The game's Y is the north pole of the planets' plane (the ecliptic), X points at the March equinox.
# OA_Sky.shader: u = atan2(d.z, d.x) / 2pi + 0.5, v = acos(d.y) / pi, and v = 0 is the BOTTOM row of a PNG.
OBLIQUITY = np.radians(23.4392911)
_c, _s = np.cos(OBLIQUITY), np.sin(OBLIQUITY)
EQ2ECL = np.array([[1, 0, 0], [0, _c, _s], [0, -_s, _c]])
ECL2GAME = np.array([[1, 0, 0], [0, 0, 1], [0, 1, 0]])       # game = (x, z, y): right-handed sky, left-handed Unity
EQ2GAME = ECL2GAME @ EQ2ECL
GAL2GAME = EQ2GAME @ EQ2GAL.T
GAME2GAL = GAL2GAME.T


def game_dirs():
    v = 1.0 - (np.arange(H) + 0.5) / H            # image row 0 is the top, which is v = 1 (straight down)
    u = (np.arange(W) + 0.5) / W
    U, V = np.meshgrid(u, v)
    polar = V * np.pi; around = (U - 0.5) * 2 * np.pi
    ring = np.sin(polar)
    return np.stack([ring * np.cos(around), np.cos(polar), ring * np.sin(around)], -1)


def main():
    os.makedirs(OUT, exist_ok=True)
    g = game_dirs() @ GAME2GAL.T                   # every dot's direction in galactic coordinates
    dx, dy, dz = (g[..., i].astype(F) for i in range(3))

    # ------------------------------------------------------------ the Milky Way
    mw = np.load(os.path.join(HERE, "cache", f"mw_{SRC}.npy")); tau = np.load(os.path.join(HERE, "cache", f"tau_{SRC}.npy"))
    sh, sw = tau.shape
    su, sv = dir_to_uv(dx, dy, dz, sw, sh)
    I = np.stack([ndimage.map_coordinates(mw[..., c], [sv, su % sw], order=1, mode="grid-wrap") for c in range(3)], -1)
    tau = ndimage.map_coordinates(tau, [sv, su % sw], order=1, mode="grid-wrap")
    I = I / np.percentile(mw, 99.5)
    floor = np.percentile(mw.reshape(-1, 3), 4, axis=0) / np.percentile(mw, 99.5)
    I = np.clip(I - 0.97 * floor, 0, None)
    # finer dust than the march resolves, only where there is dust, and the grain of unresolved star clouds
    fil = fbm(dx * 19, dy * 19, dz * 19, 311, 5)
    ext = np.clip(np.clip(tau, 0, 6) / 6 * 3.0, 0, 1) * np.clip(fil + 0.1, 0, 1) * 2.0
    I *= np.exp(-ext[..., None] * np.array([0.8, 1.0, 1.3], F))
    I *= (1 + 0.22 * fbm(dx * 150, dy * 150, dz * 150, 321, 4) + 0.12 * fbm(dx * 520, dy * 520, dz * 520, 331, 3))[..., None]
    sky = np.clip(I, 0, None).astype(F)

    # ------------------------------------------------------------ things in the sky
    def stamp(l, b, radius_deg, fn):
        c = lb_to_dir(l, b)
        dot = dx * c[0] + dy * c[1] + dz * c[2]
        m = dot > np.cos(np.radians(min(radius_deg * 1.6, 89)))
        east = np.cross([0, 0, 1.0], c); east /= np.linalg.norm(east)
        north = np.cross(c, east)
        px, py, pz, pd = dx[m], dy[m], dz[m], dot[m]
        r = np.tan(np.radians(radius_deg))
        u = (px * east[0] + py * east[1] + pz * east[2]) / pd / r
        v = (px * north[0] + py * north[1] + pz * north[2]) / pd / r
        add, mul = fn(u.astype(F), v.astype(F))
        if mul is not None:
            sky[m] = sky[m] * mul
        sky[m] = sky[m] + add

    for item in REAL + SHOWPIECES:
        stamp(*item)
    rng = np.random.default_rng(5)
    for i in range(46):                            # a faint field of far galaxies, off the Milky Way
        v3 = rng.normal(size=3); v3 /= np.linalg.norm(v3)
        b = np.degrees(np.arcsin(v3[2])); l = np.degrees(np.arctan2(v3[1], v3[0]))
        size = 0.25 + 0.9 * rng.random() ** 3
        kind = rng.random(); a, c, d, e = rng.uniform(20, 78), rng.uniform(0, 180), rng.uniform(14, 26), rng.uniform(0.7, 1.6)
        if abs(b) < 18: continue
        stamp(l, b, size, spiral(100 + i, a, c, 2, d, e, flocc=0.5) if kind < 0.6 else edge_on(100 + i, c, e))

    # ------------------------------------------------------------ exposure: black sky off the Milky Way
    lum = sky.mean(-1, keepdims=True)
    sky = sky * smooth(0.03, 0.35, lum) * 0.36

    # ------------------------------------------------------------ stars
    stars = real_stars()
    e_dirs, e_mag, e_bv = faint_stars(sky, rng, SPRITE_EXTRA, 6.6, SPRITE_FAINTEST)
    n_real = len(stars[1])
    order = np.argsort(e_mag)
    dirs = np.concatenate([stars[0], e_dirs[order]])                # real stars first, each group brightest first
    mags = np.concatenate([stars[1], e_mag[order]])
    cols = star_colour(np.concatenate([stars[2], e_bv[order]]))
    write_stars(os.path.join(OUT, "Stars.bytes"), dirs, mags, cols, n_real)
    # the faintest go into the picture as soft dots
    b_dirs, b_mag, b_bv = faint_stars(sky, rng, BAKED_COUNT, SPRITE_FAINTEST, BAKED_FAINTEST)
    bake_dots(sky, b_dirs, b_mag, star_colour(b_bv))

    # ------------------------------------------------------------ write the picture
    sky = sky / (1 + 0.35 * sky)                                   # ease the brightest part under white
    enc = np.where(sky <= 0.0031308, sky * 12.92, 1.055 * np.power(np.maximum(sky, 1e-9), 1 / 2.4) - 0.055)
    dither = (np.random.default_rng(SEED).random(enc.shape[:2] + (1,)) - 0.5) / 255.0
    png = (np.clip(enc + dither, 0, 1) * 255 + 0.5).astype("u1")
    Image.fromarray(png).save(os.path.join(OUT, "DeepSpace.png"), optimize=True)
    print("picture", png.shape, "mean", png.mean().round(2), "max", png.max(), "| stars", len(mags), "of which real", n_real)


# ---------------------------------------------------------------- stars
def real_stars():
    rows = np.loadtxt(os.path.join(HERE, "bsc5_stars.csv"), delimiter=",", comments="#")
    ra, dec = np.radians(rows[:, 1]), np.radians(rows[:, 2])
    eq = np.stack([np.cos(dec) * np.cos(ra), np.cos(dec) * np.sin(ra), np.sin(dec)], -1)
    return (eq @ EQ2GAME.T).astype(F), rows[:, 3].astype(F), rows[:, 4].astype(F)


def faint_stars(sky, rng, count, lo, hi):
    """Stars past the catalogue's reach (it thins out from magnitude 6.5), scattered by a seed, thicker where
    the Milky Way is brighter. Each magnitude fainter has about 2.8 times as many stars, as in the real sky."""
    lum_map = sky.mean(-1)
    got = []
    have = 0
    while have < count:
        v = rng.normal(size=(count, 3)); v /= np.linalg.norm(v, axis=1, keepdims=True)
        u = (np.arctan2(v[:, 2], v[:, 0]) / (2 * np.pi) + 0.5) * W - 0.5
        t = (1 - np.arccos(np.clip(v[:, 1], -1, 1)) / np.pi) * H - 0.5
        lum = ndimage.map_coordinates(lum_map, [t, u % W], order=1, mode="grid-wrap")
        v = v[rng.random(count) < np.clip(0.2 + lum * 4.0, 0, 1)]
        got.append(v); have += len(v)
    v = np.concatenate(got)[:count]
    k = np.log(2.8)
    mag = np.log(np.exp(k * lo) + rng.random(count) * (np.exp(k * hi) - np.exp(k * lo))) / k
    bv = np.clip(rng.normal(0.7, 0.35, count), -0.2, 1.7)
    return v.astype(F), mag.astype(F), bv.astype(F)


def star_colour(bv):
    """A star's tint from its B-V colour: true to its temperature but held to two thirds of the full colour,
    as stars look to the eye and in photographs (pale blue-white to pale orange), brightest channel 1."""
    c = 1 + (bv_to_rgb(np.asarray(bv, dtype=np.float64)) - 1) * 0.66
    return (c / c.max(-1, keepdims=True)).astype(F)


def write_stars(path, dirs, mags, cols, n_real):
    with open(path, "wb") as f:
        f.write(b"OAS1")
        f.write(struct.pack("<ii", len(mags), n_real))
        for d, m, c in zip(dirs, mags, cols):
            f.write(struct.pack("<ffff", float(d[0]), float(d[1]), float(d[2]), float(m)))
            f.write(bytes(int(round(float(x) * 255)) for x in c) + b"\x00")


def bake_dots(sky, dirs, mags, cols):
    u = (np.arctan2(dirs[:, 2], dirs[:, 0]) / (2 * np.pi) + 0.5) * W - 0.5
    t = (1 - np.arccos(np.clip(dirs[:, 1], -1, 1)) / np.pi) * H - 0.5
    flux = 0.12 * (10 ** (-0.4 * (mags - 6.5))) ** 0.7 * 1.5       # the game's peak for that magnitude, spread over the dot
    widen = 1 / np.maximum(np.sqrt(1 - dirs[:, 1] ** 2), 0.05)     # dots stay round near the poles
    ix = np.round(u).astype(int); iy = np.round(t).astype(int)
    sig = 0.6
    for oy in range(-2, 3):
        for ox in range(-6, 7):
            xx = ix + ox; yy = iy + oy
            w = np.exp(-(((xx - u) / widen) ** 2 + (yy - t) ** 2) / (2 * sig * sig)) / (2 * np.pi * sig * sig)
            k = (yy >= 0) & (yy < H) & (np.abs(ox) <= np.ceil(2.5 * widen))
            np.add.at(sky, (yy[k], xx[k] % W), cols[k] * (flux[k] * w[k])[:, None])


# ---------------------------------------------------------------- painters (u, v run -1 to 1 across the thing)
def rot(u, v, pa):
    c, s = np.cos(np.radians(pa)), np.sin(np.radians(pa))
    return u * c + v * s, -u * s + v * c


def spiral(seed, incl, pa, arms=2, pitch=17.0, bright=1.0, bar=0.0, flocc=0.3):
    def fn(u, v):
        x, y = rot(u, v, pa)
        y = y / np.cos(np.radians(incl))
        r = np.sqrt(x * x + y * y) + 1e-4; th = np.arctan2(y, x)
        warp = 0.9 * fbm(x * 2.2, y * 2.2, seed * 1.0, seed, 4)
        ph = arms * (th - np.log(r / 0.12) / np.tan(np.radians(pitch))) + warp * 2.2
        arm = (0.5 + 0.5 * np.cos(ph)) ** 2.4
        lane = (0.5 + 0.5 * np.cos(ph + 0.9)) ** 5
        n = fbm(x * 9, y * 9, 3.0, seed + 5, 5)
        nf = fbm(x * 38, y * 38, 7.0, seed + 9, 4)
        disk = np.exp(-r / 0.26) * (1 - smooth(0.85, 1.08, r))
        inner = smooth(0.07, 0.22, r)
        bulge = np.exp(-(r / 0.07) ** 0.9) * 2.4
        if bar:
            bulge = bulge + bar * np.exp(-((x / 0.22) ** 2 + (y / 0.06) ** 2))
        patch = np.clip(1 + flocc * 2.2 * n + 0.8 * nf, 0.15, None)
        old = disk * (0.45 + 0.4 * arm * inner)
        young = disk * arm * inner * patch * 1.6 * smooth(0.0, 0.5, r + 0.25)
        hii = disk * inner * arm * np.clip(nf * 1.6 + n * 0.9 - 0.45, 0, None) ** 2 * 26
        dust = inner * lane * disk * (2.6 + 3.0 * np.clip(n + 0.3, 0, 1)) * (1 - smooth(0.6, 1.0, r)) * 3.0
        tr = np.exp(-dust[:, None] * np.array([0.7, 1.0, 1.35], F))
        col = (old[:, None] * np.array([1.1, 0.95, 0.72], F) + bulge[:, None] * np.array([1.25, 1.0, 0.68], F)
               + young[:, None] * np.array([0.72, 0.9, 1.28], F)) * tr + hii[:, None] * np.array([1.3, 0.38, 0.5], F)
        col *= (1 + 0.25 * nf)[:, None]
        return (col * bright).astype(F), None
    return fn


def edge_on(seed, pa, bright=1.0):
    def fn(u, v):
        x, y = rot(u, v, pa)
        n = fbm(x * 14, y * 40, 2.0, seed, 5)
        disk = np.exp(-np.abs(x) / 0.3) / np.cosh(y / 0.035) ** 2 * (1 - smooth(0.85, 1.05, np.abs(x)))
        halo = np.exp(-np.sqrt((x / 0.5) ** 2 + (y / 0.2) ** 2) / 0.25) * 0.12
        bulge = np.exp(-np.sqrt((x / 0.13) ** 2 + (y / 0.1) ** 2) / 0.6) * 1.8
        warp = 0.008 * np.sin(x * 4)
        lane = np.exp(-((y - warp) / (0.011 * (1 + 0.6 * n))) ** 2) * smooth(1.0, 0.55, np.abs(x)) * (2.4 + 2 * n)
        tr = np.exp(-np.clip(lane, 0, None)[:, None] * np.array([0.75, 1.0, 1.3], F))
        col = (disk[:, None] * np.array([1.0, 0.95, 0.92], F) + (bulge + halo)[:, None] * np.array([1.22, 1.0, 0.7], F)) * tr
        return (col * bright).astype(F), None
    return fn


def irregular(seed, pa, ax, bright, barlen=0.5):
    def fn(u, v):
        x, y = rot(u, v, pa)
        r = np.sqrt(x * x + (y / ax) ** 2)
        n = fbm(x * 5, y * 5, 1.0, seed, 5); nf = fbm(x * 30, y * 30, 2.0, seed + 3, 4)
        body = np.exp(-r / 0.3) * np.clip(1 + 1.3 * n, 0.1, None) * (1 - smooth(0.8, 1.1, r))
        bar = np.exp(-((x / barlen) ** 2 + (y / 0.12) ** 2)) * 1.5
        hii = body * np.clip(nf + n - 0.55, 0, None) ** 2 * 30
        col = body[:, None] * np.array([0.85, 0.95, 1.15], F) + bar[:, None] * np.array([1.1, 1.0, 0.82], F) \
            + hii[:, None] * np.array([1.3, 0.4, 0.55], F)
        col *= (1 + 0.3 * nf)[:, None]
        return (col * bright).astype(F), None
    return fn


def nebula(seed, bright, kind="emission", pillars=0.6):
    def fn(u, v):
        r = np.sqrt(u * u + v * v)
        n = fbm(u * 2.4 + seed, v * 2.4, 1.0, seed, 6)
        n2 = 1 - np.abs(fbm(u * 3.1 + seed, v * 3.1, 4.0, seed + 2, 6)) * 2.0     # wisps along the noise's contours
        n3 = fbm(u * 11, v * 11, 9.0, seed + 4, 5)
        fall = np.exp(-(r / 0.55) ** 2)
        gas = np.clip(n * 1.6 + 0.35, 0, None) * fall
        wisps = gas * (0.35 + 1.3 * np.clip(n2 - 0.45, 0, None) * 2)
        core = np.exp(-(r / 0.22) ** 2) * np.clip(0.6 + n3, 0, None)
        dark = np.clip(fbm(u * 4.2 + 7, v * 4.2, 6.0, seed + 8, 5) * 2.4 + 0.1, 0, 1) ** 2 * pillars * fall * 3.5
        tr = np.exp(-dark[:, None] * np.array([0.8, 1.0, 1.25], F))
        if kind == "emission":
            col = wisps[:, None] * np.array([1.25, 0.3, 0.42], F) + (core * gas)[:, None] * np.array([0.45, 0.95, 0.9], F) * 0.9
        else:                                                                  # starlight on dust: blue
            col = wisps[:, None] * np.array([0.5, 0.72, 1.25], F)
        return (col * bright * tr).astype(F), tr.astype(F)
    return fn


# galactic longitude, latitude, radius in degrees, painter
REAL = [
    (280.47, -32.89, 5.4, irregular(41, 30, 0.85, 1.5, 0.45)),            # Large Magellanic Cloud
    (302.80, -44.30, 2.6, irregular(42, 60, 0.6, 1.2, 0.5)),              # Small Magellanic Cloud
    (121.17, -21.57, 1.6, spiral(43, 77, 38, 2, 14, 2.2, flocc=0.2)),     # Andromeda
    (133.61, -31.33, 0.55, spiral(44, 54, 23, 3, 22, 0.9, flocc=0.6)),    # Triangulum
    (287.6, -0.6, 2.4, nebula(51, 2.2, "emission", 0.5)),                 # Carina
    (6.0, -1.2, 1.2, nebula(52, 1.8, "emission", 0.5)),                   # Lagoon
    (209.0, -19.4, 1.4, nebula(53, 2.2, "emission", 0.5)),                # Orion
    (85.0, -1.0, 2.2, nebula(54, 0.9, "emission", 0.5)),                  # North America
    (206.3, -2.1, 1.3, nebula(55, 0.9, "emission", 0.5)),                 # Rosette
    (16.0, 0.0, 1.6, nebula(56, 1.2, "emission", 0.5)),                   # Eagle and Omega
    (173.0, -1.5, 2.5, nebula(57, 0.55, "emission", 0.5)),                # Auriga
    (134.8, 1.0, 2.2, nebula(58, 0.7, "emission", 0.5)),                  # Heart and Soul
    (353.1, 16.9, 2.6, nebula(59, 0.55, "reflection", 0.5)),              # Rho Ophiuchi
]
# Invented, away from the Milky Way so they stand against black. Nothing here is in the real sky.
SHOWPIECES = [
    (222.0, 52.0, 7.5, spiral(71, 34, 25, 2, 19, 3.4, flocc=0.35)),          # a grand spiral, nearly face on
    (62.0, -48.0, 6.0, edge_on(72, -22, 3.4)),                               # an edge-on galaxy with a dust lane
    (318.0, 38.0, 4.2, spiral(73, 58, 110, 2, 24, 2.6, bar=1.6, flocc=0.5)),  # a tilted barred spiral
    (150.0, 24.0, 9.0, nebula(74, 1.7, "emission", 0.9)),                    # a large glowing nebula
    (20.0, 34.0, 5.0, nebula(75, 1.1, "reflection", 0.7)),                   # a blue dust nebula
]

if __name__ == "__main__":
    main()
