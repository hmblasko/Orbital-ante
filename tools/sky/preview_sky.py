"""Pictures of the baked sky as the game should show it: 1280 x 800, the Deck's screen, 80 degrees of view.
Reads the same two files the game reads (DeepSpace.png and Stars.bytes) and draws the stars with a copy of
StarLook's sums, then a bloom and a tone curve like the game's. These are NOT Unity screenshots: they are for
judging the bake before anyone opens the game.

Run:  python3 preview_sky.py <folder>          (after bake_sky.py)
"""
import os, struct, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage
from skylib import *
from bake_sky import GAL2GAME, OUT

VW, VH, FOV = 1280, 800, 80.0


def load():
    png = np.asarray(Image.open(os.path.join(OUT, "DeepSpace.png")).convert("RGB")).astype(F) / 255.0
    lin = np.where(png <= 0.04045, png / 12.92, ((png + 0.055) / 1.055) ** 2.4)
    raw = open(os.path.join(OUT, "Stars.bytes"), "rb").read()
    assert raw[:4] == b"OAS1"
    count, real = struct.unpack("<ii", raw[4:12])
    rec = np.frombuffer(raw, dtype=np.dtype([("d", "<f4", 3), ("m", "<f4"), ("c", "u1", 4)]), offset=12, count=count)
    return lin, rec["d"].astype(F), rec["m"].astype(F), rec["c"][:, :3].astype(F) / 255.0, real


def half_size(m):          # StarLook.HalfSize
    return np.clip(1.6 + 0.55 * (3.5 - m), 1.6, 4.0)


def peak(m):               # StarLook.Peak
    return np.minimum(0.12 * (10 ** (-0.4 * (m - 6.5))) ** 0.7, 4.0)


def camera(l, b, roll=0.0):
    f = GAL2GAME @ lb_to_dir(l, b)
    north = GAL2GAME @ np.array([0, 0, 1.0])
    r = np.cross(north, f); r /= np.linalg.norm(r)      # Unity is left-handed: right = up x forward
    u = np.cross(f, r)
    c, s = np.cos(np.radians(roll)), np.sin(np.radians(roll))
    return f, r * c + u * s, -r * s + u * c


def view(sky, stars, l, b, roll, text, sun=None):
    f, r, u = camera(l, b, roll)
    t = np.tan(np.radians(FOV / 2))
    ys = (1 - (np.arange(VH) + 0.5) / VH * 2) * t
    xs = ((np.arange(VW) + 0.5) / VW * 2 - 1) * t * VW / VH
    X, Y = np.meshgrid(xs, ys)
    d = f + X[..., None] * r + Y[..., None] * u
    d /= np.linalg.norm(d, axis=-1, keepdims=True)
    H, W, _ = sky.shape
    tu = (np.arctan2(d[..., 2], d[..., 0]) / (2 * np.pi) + 0.5) * W - 0.5        # OA_Sky.shader's lookup
    tv = (1 - np.arccos(np.clip(d[..., 1], -1, 1)) / np.pi) * H - 0.5            # v = 0 is the PNG's bottom row
    img = np.stack([ndimage.map_coordinates(sky[..., c], [tv, tu % W], order=1, mode="grid-wrap") for c in range(3)], -1)

    def glare(dirs):                                                              # OA_SunGlare x 0.75
        if sun is None: return 0.0
        x = np.clip((np.clip(dirs @ sun, 0, 1) - 0.72) / (0.995 - 0.72), 0, 1)
        return 0.75 * x * x * (3 - 2 * x)
    img *= (1 - glare(d))[..., None] if sun is not None else 1.0

    dirs, mag, col, _ = stars
    z = dirs @ f
    m = z > 0.05
    px = ((dirs[m] @ r) / z[m] / (t * VW / VH) + 1) * 0.5 * VW - 0.5
    py = (1 - (dirs[m] @ u) / z[m] / t) * 0.5 * VH - 0.5
    hs = half_size(mag[m]); pk = peak(mag[m]) * (1 - glare(dirs[m])); cl = col[m]
    ok = (px > -8) & (px < VW + 8) & (py > -8) & (py < VH + 8)
    px, py, hs, pk, cl = px[ok], py[ok], hs[ok], pk[ok], cl[ok]
    ix = np.round(px).astype(int); iy = np.round(py).astype(int)
    for oy in range(-5, 6):
        for ox in range(-5, 6):
            xx = ix + ox; yy = iy + oy
            d2 = ((xx - px) ** 2 + (yy - py) ** 2) / (hs * hs)                    # the shader's corner distance squared
            w = np.clip(np.exp(-d2 * 4.5) - 0.0111, 0, None) * (np.abs(xx - px) <= hs) * (np.abs(yy - py) <= hs)
            k = (xx >= 0) & (xx < VW) & (yy >= 0) & (yy < VH) & (w > 0)
            np.add.at(img, (yy[k], xx[k]), cl[k] * (pk[k] * w[k])[:, None])
    if sun is not None:                                                           # the Sun's disc and glow, roughly
        toward = np.clip(d @ sun, 1e-4, 1)
        img += np.array([1.0, 0.93, 0.8], F) * (toward ** 48 * 0.35 + toward ** 600 * 1.6 + (toward > 0.99996) * 5)[..., None]

    hi = np.clip(img - 1.25, 0, None)                                             # bloom, as LookConfig's defaults
    img = img + 0.55 * sum(ndimage.gaussian_filter(hi, (s, s, 0)) * w for s, w in ((2, 0.5), (6, 0.3), (18, 0.2)))
    img = img / (1 + img.max(-1, keepdims=True) * 0.6) * 1.25                     # a neutral shoulder that keeps colour
    im = Image.fromarray((np.clip(img, 0, 1) ** (1 / 2.2) * 255).astype("u1"))
    try:
        font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 18)
    except OSError:
        font = ImageFont.load_default()
    ImageDraw.Draw(im).text((18, VH - 34), text + "  (offline picture of the baked sky, not a Unity screenshot)", fill=(205, 205, 205), font=font)
    return im


if __name__ == "__main__":
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    sky, dirs, mag, col, real = load()
    stars = (dirs, mag, col, real)
    print("stars", len(mag), "real", real)
    view(sky, stars, 8, 6, 12, "The Milky Way's centre").save(f"{out}/sky_milky_way.png")
    view(sky, stars, 200, -12, 0, "Orion and the winter Milky Way").save(f"{out}/sky_orion.png")
    view(sky, stars, 290, -32, -10, "The Magellanic Clouds and Carina").save(f"{out}/sky_magellanic.png")
    view(sky, stars, 222, 50, 0, "Showpiece: the grand spiral").save(f"{out}/sky_spiral.png")
    view(sky, stars, 150, 22, 0, "Showpiece: the large nebula").save(f"{out}/sky_nebula.png")
    view(sky, stars, 62, -46, 8, "Showpiece: the edge-on galaxy").save(f"{out}/sky_edge_on.png")
    view(sky, stars, 318, 36, 0, "Showpiece: the barred spiral").save(f"{out}/sky_barred.png")
    view(sky, stars, 30, 20, 0, "The Sun's glare hiding the stars near it", sun=GAL2GAME @ lb_to_dir(52, 28)).save(f"{out}/sky_sun_glare.png")
