"""Pictures of the far planets, the earlier drawing beside the realistic one, at about the size the Deck shows
them. Reads the pictures bake_planets.py wrote and, for the rocky worlds, the far map and heights that
`tools/terrain_view map <body> 1024 map_<body>.ppm 1 h_<body>.ppm` writes from the game's own ground. The sums
copy OA_Body, OA_Air, OA_Clouds and OA_Rings and the numbers copy OrbitLook.cs: if those change, change this.
These are NOT Unity screenshots; they are for judging the pictures and the numbers before anyone opens the game.

Run:  python3 preview_planets.py <folder with map_*.ppm and h_*.ppm> <out folder> [earth,mars,...]
"""
import os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "sky"))
sys.path.insert(0, HERE)
import bake_planets as bake
from skylib import vnoise, fbm, smooth, F

G, OUT = sys.argv[1], sys.argv[2]
os.makedirs(OUT, exist_ok=True)
N = 1280                      # worked at twice the size shown
MW, MH = 2048, 1024


def lin(c): c = np.asarray(c, F); return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
def hexc(h): return lin([int(h[i:i + 2], 16) / 255 for i in (1, 3, 5)])
def norm(v): return v / np.maximum(1e-9, np.linalg.norm(v, axis=-1, keepdims=True))
def dot(a, b): return (a * b).sum(-1)


def map_dirs(W=MW, H=MH):
    u = (np.arange(W) + 0.5) / W; v = (np.arange(H) + 0.5) / H            # row 0 is the north pole
    U, V = np.meshgrid(u, v)
    pol = V * np.pi; az = U * 2 * np.pi
    return np.stack([np.sin(pol) * np.cos(az), np.cos(pol), np.sin(pol) * np.sin(az)], -1).astype(F)


def sample(img, d):
    H, W = img.shape[:2]
    u = (np.arctan2(d[..., 2], d[..., 0]) / (2 * np.pi)) % 1.0
    v = np.arccos(np.clip(d[..., 1], -1, 1)) / np.pi
    x = u * W - 0.5; y = np.clip(v * H - 0.5, 0, H - 1)
    if img.ndim == 2:
        return ndimage.map_coordinates(img, [y, x % W], order=1, mode="grid-wrap")
    return np.stack([ndimage.map_coordinates(img[..., c], [y, x % W], order=1, mode="grid-wrap") for c in range(img.shape[2])], -1)


def load(body):
    alb = lin(np.asarray(Image.open(f"{G}/map_{body}.ppm")).astype(F) / 255)
    h = np.asarray(Image.open(f"{G}/h_{body}.ppm")).astype(F) / 255
    return alb, h[..., 0], h[..., 1]


def giant_today(a, b, seed):
    d = map_dirs(); x, y, z = d[..., 0], d[..., 1], d[..., 2]
    wob = fbm(x * 3, y * 3, z * 3, seed, 3) * 0.5 * 0.22
    bands = np.sin((y + wob) * 14 + seed) * 0.5 + 0.5
    fine = np.sin((y + wob * 0.5) * 41) * 0.5 + 0.5
    t = np.clip(bands * 0.75 + fine * 0.25, 0, 1)[..., None]
    return (hexc(a) * (1 - t) + hexc(b) * t).astype(F)


# ---------------------------------------------------------------- the picture

def rot(lon0, lat0, tilt):
    """Columns: where the body's x, y (north), z end up in view space (x right, y up, z toward the eye)."""
    a, b, c = np.radians(lon0), np.radians(lat0), np.radians(tilt)
    Ry = np.array([[np.cos(a), 0, np.sin(a)], [0, 1, 0], [-np.sin(a), 0, np.cos(a)]])
    Rx = np.array([[1, 0, 0], [0, np.cos(b), -np.sin(b)], [0, np.sin(b), np.cos(b)]])
    Rz = np.array([[np.cos(c), -np.sin(c), 0], [np.sin(c), np.cos(c), 0], [0, 0, 1]])
    return (Rz @ Rx @ Ry).astype(F)


def baked(name):
    """A picture the game reads, as linear colour, with its alpha if it has one."""
    a = np.asarray(Image.open(os.path.join(bake.OUT, name + ".png"))).astype(F) / 255
    return lin(a[..., :3]), (a[..., 3] if a.shape[2] == 4 else None)


def tone(img, label):
    hi = np.clip(img - 1.25, 0, None)
    img = img + 0.55 * sum(ndimage.gaussian_filter(hi, (s, s, 0)) * w for s, w in ((3, 0.5), (9, 0.3), (27, 0.2)))
    img = img / (1 + img.max(-1, keepdims=True) * 0.6) * 1.25
    im = Image.fromarray((np.clip(img, 0, 1) ** (1 / 2.2) * 255).astype("u1")).resize((N // 2, N // 2), Image.LANCZOS)
    font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 17)
    ImageDraw.Draw(im).text((14, 12), label, fill=(210, 210, 210), font=font)
    return im


SUNRAW = np.array([1.0, 0.96, 0.9], F) * 1.25
AMBIENT = lin([0.055, 0.062, 0.105])


def render(cfg, new):
    R = cfg.get("size", 0.66)                                   # the planet's radius as a share of half the picture
    ax = (np.arange(N) + 0.5) / N * 2 - 1
    X, Y = np.meshgrid(ax / R, -ax / R)
    r2 = X * X + Y * Y
    on = r2 < 1
    Z = np.sqrt(np.clip(1 - r2, 0, 1))
    nv = np.stack([X, Y, Z], -1).astype(F)                      # the ball's surface direction, view space
    M = rot(*cfg["turn"])
    s = norm(np.array(cfg["sun"], F))
    e = np.array([0, 0, 1], F)
    nb = nv @ M                                                 # the same in the body's own frame
    sb = s @ M
    img = np.zeros((N, N, 3), F)
    mu_s = dot(nv, s)
    air = cfg.get("air")

    if not new:
        alb = sample(cfg["today_map"], nb)
        wrap = np.clip((mu_s + 0.22) / 1.22, 0, 1)
        sm = np.clip((wrap - 0.03) / 0.47, 0, 1); sm = sm * sm * (3 - 2 * sm)
        sun = 0.5 * wrap + 0.5 * sm
        edge = np.clip(1 - np.abs(wrap - 0.16) * 6, 0, 1)
        light = SUNRAW * (sun[..., None] + np.array([1, 0.36, 0.14], F) * (edge * 0.16)[..., None])
        img = np.where(on[..., None], alb * (AMBIENT + light), 0)
        if air:
            shell = cfg.get("shell", 1.04); limb = 1 - np.sqrt(max(0, 1 - 1 / shell ** 2))
            rs2 = r2 / shell ** 2
            g = 1 - np.sqrt(np.clip(1 - rs2, 0, 1))             # grazing on the larger ball
            rim = np.where(g < limb, (g / limb) ** 3, (np.clip((1 - g) / max(0.02, 1 - limb), 0, 1)) ** 2)
            nsh = np.stack([X / shell, Y / shell, np.sqrt(np.clip(1 - rs2, 0, 1))], -1)
            facing = dot(nsh, s)
            day = np.clip(facing * 1.6 + 0.35, 0, 1)
            dusk = np.clip(1 - np.abs(facing + 0.05) * 3.2, 0, 1)
            col = hexc(air["sky"]); br = dot(col, np.array([0.3, 0.5, 0.2], F))
            aircol = col * (1 - dusk * 0.55)[..., None] + np.array([1, 0.52, 0.3], F) * br * 1.5 * (dusk * 0.55)[..., None]
            through = np.clip(-s[2], 0, 1) ** 4
            img += np.where((rs2 < 1)[..., None], aircol * (rim * day * air["strength"] * (1 + 1.5 * through))[..., None], 0)
    else:
        alb = sample(cfg["map"], nb)
        n = nv
        sea = np.zeros((N, N), F)
        if "height" in cfg:
            # Relief: the ground's real slope, from the same heights the land is built from, tilts the light.
            h = cfg["height"]; k = cfg["relief"]
            up = np.array([0, 1, 0], F)
            east = norm(np.cross(up, nb)); north = np.cross(nb, east)
            eps = 2.2 / MW * 2 * np.pi
            h0 = sample(h, nb)
            de = (sample(h, norm(nb + east * eps)) - sample(h, norm(nb - east * eps))) / (2 * eps)
            dn = (sample(h, norm(nb + north * eps)) - sample(h, norm(nb - north * eps))) / (2 * eps)
            bumped = norm(nb - k * (de[..., None] * east + dn[..., None] * north))
            if "sea" in cfg:
                sea = sample(cfg["sea"], nb)
                bumped = norm(bumped * (1 - sea[..., None]) + nb * sea[..., None])
            n = bumped @ M.T
        nl = np.clip(dot(n, s), 0, 1)
        # The Sun is a disc, not a point, and air bends a little light round: a narrow soft edge to the night.
        soft = cfg.get("soft", 0.03)
        term = smooth(-soft, soft * 2.5, mu_s)
        sunlight = np.broadcast_to(SUNRAW, (N, N, 3)).copy()
        if air:
            # Low sunlight has come a long way through the air and arrives red.
            path = 1 / np.maximum(mu_s + 0.06, 0.03)
            sunlight = SUNRAW * np.exp(-np.asarray(air["redden"], F) * np.minimum(path, 14)[..., None])
        cloud = np.zeros((N, N), F)
        shadow = np.ones((N, N), F)
        if "clouds" in cfg:
            cloud = sample(cfg["clouds"], nb) * cfg.get("cloud_amount", 1)
            # Clouds' shadows fall on the ground a little way down-Sun.
            tang = sb - nb * dot(nb, sb)[..., None]
            shadow = 1 - 0.55 * sample(cfg["clouds"], norm(nb + tang * 0.035)) * cfg.get("cloud_amount", 1)
        ground = alb * (sunlight * (nl * term * shadow)[..., None] + cfg.get("night", 0.0004))
        if "sea" in cfg:
            # The Sun's glint on water: a bright smear where the sea mirrors it to the eye.
            hv = norm(s + e)
            rough = fbm(nb[..., 0] * 60, nb[..., 1] * 60, nb[..., 2] * 60, 4, 3) * 0.05
            spec = np.clip(dot(norm(nv + rough[..., None]), hv), 0, 1) ** 45
            ground += sunlight * (spec * sea * term * 1.4)[..., None]
        if "lights" in cfg:
            ground += cfg["lights_col"] * (sample(cfg["lights"], nb) * (1 - sea) * smooth(0.02, -0.12, mu_s))[..., None]
        if "clouds" in cfg:
            cl = cfg.get("cloud_col", np.array([1, 1, 1], F))
            # Cloud tops: bright, a touch of shade on their own night-ward side, and red at sunset.
            lit = sunlight * (np.clip(mu_s * 0.9 + 0.1, 0, 1) * term)[..., None]
            ground = ground * (1 - cloud[..., None]) + cl * (lit * 0.92 + 0.0003) * cloud[..., None]
        img = np.where(on[..., None], ground, 0)
        if air:
            # Air over the disc: thicker toward the edge, so the planet pales and blues at its limb.
            sky = hexc(air["sky"]) * air.get("gain", 1.0)
            depth = air["depth"]
            col_path = 1 / np.maximum(Z, 0.05)
            dayf = smooth(-0.12, 0.25, mu_s)
            amt = 1 - np.exp(-depth * col_path)
            dusk = np.exp(-((mu_s - 0.02) / 0.07) ** 2)
            tint = sky * (1 - 0.6 * dusk[..., None]) + np.array([1.0, 0.45, 0.2], F) * dot(sky, np.array([0.3, 0.5, 0.2], F)) * 1.4 * (0.6 * dusk[..., None])
            over = tint * (amt * dayf)[..., None] * SUNRAW
            img = np.where(on[..., None], img * (1 - amt * air.get("hide", 0.6))[..., None] + over, img)
            # Beyond the edge: a thin shell that thins out with height, lit by the Sun, brightest looking sunward.
            r = np.sqrt(r2)
            hgt = np.clip(r - 1, 0, None)
            nrim = np.stack([X / np.maximum(r, 1e-6), Y / np.maximum(r, 1e-6), np.zeros_like(r)], -1)
            mu_r = dot(nrim, s)
            forward = np.clip(-s[2], 0, 1)
            glow = np.exp(-hgt / air["scale"]) * (1 - np.exp(-depth * 9))
            dayr = smooth(-0.10 - 0.6 * forward, 0.2, mu_r)
            duskr = np.exp(-((mu_r + 0.02) / 0.10) ** 2)
            tintr = sky * (1 - 0.55 * duskr[..., None]) + np.array([1.0, 0.45, 0.2], F) * dot(sky, np.array([0.3, 0.5, 0.2], F)) * 1.4 * (0.55 * duskr[..., None])
            rimc = tintr * (glow * dayr * (1 + 5 * forward ** 3))[..., None] * SUNRAW
            img = np.where((~on)[..., None], rimc, img)
        if "aurora" in cfg:
            lat = np.degrees(np.arcsin(np.clip(nb[..., 1], -1, 1)))
            lon = np.arctan2(nb[..., 2], nb[..., 0])
            oval = np.exp(-((np.abs(lat) - 68 - 4 * np.sin(lon * 2 + 1)) / 2.2) ** 2)
            cur = 0.35 + 0.65 * smooth(-0.2, 0.6, fbm(nb[..., 0] * 9, nb[..., 1] * 9, nb[..., 2] * 9, 77, 4))
            img += np.where(on[..., None], np.asarray(cfg["aurora"], F) * (oval * cur * smooth(0.05, -0.2, mu_s))[..., None], 0)

    if "rings" in cfg:
        a = M[:, 1]                                            # the body's north in view space: the rings' axis
        inner, outer = cfg["rings"]
        zp = -(a[0] * X + a[1] * Y) / a[2]
        rho = np.sqrt(X * X + Y * Y + zp * zp)
        t = (rho - inner) / (outer - inner)
        hit = (t > 0) & (t < 1) & ((~on) | (zp > Z))
        tint = hexc(cfg["ring_tint"])
        if not new:
            bands = [(0, .16, .55), (.19, .52, .95), (.54, .62, .75), (.70, .92, .85), (.95, 1, .60)]
            b = np.zeros_like(t)
            for lo, hi_, br in bands: b = np.where((t >= lo) & (t < hi_), br, b)
            facing = abs(float(a @ s)) * 0.8 + 0.2
            wrap = np.clip((facing + 0.22) / 1.22, 0, 1)
            col = tint * b[..., None] * (AMBIENT + SUNRAW * wrap)
            img = np.where((hit & (b > 0))[..., None], col, img)
        else:
            op, br = cfg["ring_profile"]
            ti = np.clip(t, 0, 1) * (len(op) - 1)
            o = np.interp(ti, np.arange(len(op)), op); bb = np.interp(ti, np.arange(len(op)), br)
            # Seen at a slant a thin ring hides more.
            o = 1 - (1 - o) ** (1 / max(0.25, abs(a[2])))
            P = np.stack([X, Y, zp], -1)
            # The planet's shadow lies across the rings.
            pd = dot(P, s)
            inshadow = (pd < 0) & ((rho * rho - pd * pd) < 1)
            sh = np.where(inshadow, smooth(0.985, 1.0, np.sqrt(np.clip(rho * rho - pd * pd, 0, None))), 1.0)
            lit_side = (a @ s) * a[2] > 0
            face = (0.35 + 0.65 * abs(float(a @ s))) if lit_side else 0.0
            thru = 0.25 * abs(float(a @ s)) * (1 - o) * o * 4 if not lit_side else 0
            if "ring_rgb" in cfg:
                rr = cfg["ring_rgb"]
                base = np.stack([np.interp(ti, np.arange(len(op)), rr[:, c]) for c in range(3)], -1)
            else:
                base = tint * (np.array([1.0, 0.97, 0.92], F) * bb[..., None])
            col = base * (SUNRAW * ((face + thru) * sh)[..., None] + 0.001)
            img = np.where(hit[..., None], img * (1 - o[..., None]) + col * o[..., None], img)
            # And the rings' shadow falls on the planet.
            if abs(float(a @ s)) > 1e-3:
                Q = nv
                tt = -dot(Q, a) / float(a @ s)
                Pq = Q + tt[..., None] * s
                rq = (np.linalg.norm(Pq, axis=-1) - inner) / (outer - inner)
                oq = np.interp(np.clip(rq, 0, 1) * (len(op) - 1), np.arange(len(op)), op)
                shade = np.where((tt > 0) & (rq > 0) & (rq < 1), 1 - 0.85 * oq, 1.0)
                img = np.where((on & ~hit)[..., None], img * shade[..., None], img)
    return img


def pair(name, cfg, label):
    a = tone(render(cfg, False), "Today")
    b = tone(render(cfg, True), "Proposed")
    im = Image.new("RGB", (N, N // 2 + 34), (0, 0, 0))
    im.paste(a, (0, 0)); im.paste(b, (N // 2, 0))
    font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 16)
    ImageDraw.Draw(im).text((14, N // 2 + 8), label + "  (offline mock-up, not a Unity screenshot)", fill=(190, 190, 190), font=font)
    im.save(f"{OUT}/{name}.jpg", quality=90)
    print(name)


def lights(seed, count, land_mask=None):
    rng = np.random.default_rng(seed)
    m = np.zeros((MH, MW), F)
    v = rng.normal(size=(count, 3)); v /= np.linalg.norm(v, axis=1, keepdims=True)
    xs = ((np.arctan2(v[:, 2], v[:, 0]) / (2 * np.pi)) % 1 * MW).astype(int); ys = (np.arccos(v[:, 1]) / np.pi * MH).astype(int)
    m[ys.clip(0, MH - 1), xs.clip(0, MW - 1)] = rng.uniform(40, 120, count)
    return ndimage.gaussian_filter(m, 1.3, mode="wrap")


def air(sky, opacity, height_over_radius, interior=False):
    """OrbitLook.Air."""
    blue = hexc(sky)[2] > hexc(sky)[0]
    if interior:
        return dict(sky=sky, strength=0.6, depth=0.05, scale=0.012, hide=1.0, gain=0.35, soft=0.06,
                    redden=((0.012 if blue else 0.02) * opacity, 0.03 * opacity, (0.075 if blue else 0.045) * opacity))
    return dict(sky=sky, strength=0.6 * max(0.35, opacity), depth=0.08 * opacity ** 1.6, scale=min(0.035, max(0.01, height_over_radius * 0.05)),
                hide=0.55, gain=0.5 + 0.38 * opacity, soft=0.02 + 0.06 * min(1, max(0, (opacity - 0.9) * 10)),
                redden=((0.012 if blue else 0.02) * opacity, 0.03 * opacity, (0.075 if blue else 0.045) * opacity))


if __name__ == "__main__":
    which = sys.argv[3].split(",") if len(sys.argv) > 3 else None
    def want(n): return which is None or n in which
    warm = np.array([1.0, 0.72, 0.38], F)

    if want("earth") or want("earth_night"):
        alb, h, sea = load("Earth")
        _, ec = baked("EarthClouds")
        a = air("#6EB5FF", 0.93, 170 / 500)
        earth = dict(today_map=alb, map=alb, height=h * (187.7 / 500), relief=0.8, sea=sea, clouds=ec, cloud_amount=0.95, soft=a["soft"], air=a,
                     shell=1.17, lights=lights(4, 28), lights_col=warm, aurora=(0.05, 0.5, 0.22), turn=(215, 18, 12), sun=(0.75, 0.3, 0.6))
        if want("earth"): pair("planet_earth", earth, "Earth: clouds with shadows, relief, the Sun's glint on the sea, air that pales the limb")
        if want("earth_night"):
            earth.update(sun=(-0.82, 0.12, -0.55), turn=(215, 30, 12))
            pair("planet_earth_night", earth, "Earth from its night side: a thin lit rim of air, base lights, aurora")
    if want("mars"):
        alb, h, _ = load("Mars")
        col, mc = baked("MarsClouds")
        a = air("#D9A066", 0.6, 120 / 350)
        mars = dict(today_map=alb, map=alb, height=h * (129 / 350), relief=0.8, clouds=mc, cloud_amount=0.16, cloud_col=col[0, 0], soft=a["soft"], air=a,
                    shell=1.17, turn=(40, 10, 20), sun=(0.8, 0.2, 0.35))
        pair("planet_mars", mars, "Mars: relief that catches the light, a thin dusty limb, wisps of ice cloud")
    if want("moon"):
        alb, h, _ = load("Moon")
        moon = dict(today_map=alb, map=alb, height=h * (53.3 / 150), relief=0.8, soft=0.008, turn=(100, 5, 5), sun=(0.92, 0.1, 0.12))
        pair("planet_moon", moon, "The Moon: no air, so a hard edge to the night and long shadows in every crater near it")
    if want("venus"):
        alb, h, _ = load("Venus")
        a = air("#E6D27A", 1.0, 240 / 450)
        v = dict(today_map=alb, map=baked("VenusClouds")[0], soft=a["soft"], air=a, shell=1.27, turn=(30, 8, 3), sun=(0.7, 0.25, 0.55))
        pair("planet_venus", v, "Venus: an unbroken deck of swept cloud hides the ground until you fly down through it")
    for name, sky, ta, tb, seed, turn in (("jupiter", "#B8743C", "#D9B38C", "#A8683F", 3, (-50, 6, 4)), ("uranus", "#86CFCF", "#A6E3E0", "#7CC7C9", 7, (0, 10, 60)), ("neptune", "#3458C4", "#3F66D9", "#2E4FB0", 9, (-30, 8, 20))):
        if not want(name): continue
        a = air(sky, 1.0, 0, True)
        g = dict(today_map=giant_today(ta, tb, seed), map=baked(name.capitalize() + "Tops")[0], soft=a["soft"], air=a, shell=1.025, turn=turn, sun=(0.7, 0.15, 0.6))
        pair("planet_" + name, g, name.capitalize() + ": painted cloud tops and a darkened limb")
    if want("saturn"):
        a = air("#D4BC80", 1.0, 0, True)
        op_rgb, op = baked("SaturnRings")
        sdict = dict(today_map=giant_today("#E3CF9A", "#BFA36B", 5), map=baked("SaturnTops")[0], soft=a["soft"], size=0.44, air=a, shell=1.025,
                     rings=(1700 / 1300, 2900 / 1300), ring_tint="#E3CF9A", ring_profile=(op[0], op_rgb[0].max(-1)), ring_rgb=op_rgb[0],
                     turn=(20, 17, 14), sun=(0.72, 0.36, 0.5))
        pair("planet_saturn", sdict, "Saturn: hundreds of fine ringlets with gaps, the planet's shadow on the rings and theirs on it")
