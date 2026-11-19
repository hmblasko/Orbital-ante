"""Builds the Drifter: the first enemy with a jointed body (Matthew, 15 Nov 2026: "a new enemy character ... a very
realistic ragdoll ... more refined than the current enemies. It should not look like Roblox").

    blender -b -P tools/blender/build_enemy_drifter.py            (writes EnemyDrifter.bytes and EnemyDrifterFar.bytes into the game)
    blender -b -P tools/blender/build_enemy_drifter.py -- --write-to <folder>
    blender -b -P tools/blender/build_enemy_drifter.py -- --compare <folder> [--pose <kinds.json> ready]
                    (the trooper beside the Drifter: same place, same light, same camera)

The named real thing (docs/art_direction.md): a high-altitude pilot's full pressure suit, the kind worn in the U-2
and the SR-71, with a pilot's survival vest over it. That gives a different outline from the trooper's Moon suit:
a close white helmet with a wide dark visor and a breathing hose, a lean back with no life-support pack (two small
bottles and a seat kit low on the back), a dark vest over a tan suit, the suit's restraint cords and laced
adjusters showing on arms and legs, tall laced boots. Same skeleton, same fourteen pieces, cut at the same joints.
"""
import bpy, sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import oa_blender as oa
import oa_real as R
import build_enemy as be
import build_enemy_realistic as T
from mathutils import Vector, Matrix

NAME = "EnemyDrifter"
SPINE_Z, NECK_Z, HEAD_Z = be.SPINE_Z, be.NECK_Z, be.HEAD_Z
SHOULDER_X, SHOULDER_Z, UPPER_ARM, FOREARM = be.SHOULDER_X, be.SHOULDER_Z, be.UPPER_ARM, be.FOREARM
HIP_X, HIP_Z, KNEE_Z, ANKLE_Z = be.HIP_X, be.HIP_Z, be.KNEE_Z, be.ANKLE_Z

# ---- colours as seen on screen: greyed, natural, from photographs of the real things ----
SUIT = (0.56, 0.50, 0.36)          # the tan outer cover of a pilot's pressure suit, sun-faded
SUIT_DARK = (0.44, 0.39, 0.28)
VEST = (0.20, 0.22, 0.19)          # a dark olive survival vest
VEST_LIGHT = (0.27, 0.29, 0.24)
CORD = (0.30, 0.27, 0.20)          # restraint cord and lacing
SHELL = (0.80, 0.79, 0.74)         # the helmet: off-white, so the head can be found from every side
VISOR = (0.06, 0.07, 0.09)
SEAL = (0.12, 0.12, 0.13)          # rubber
ALU, STEEL = T.ALU, T.STEEL
BRASS = (0.55, 0.44, 0.22)
GREEN_ANOD = (0.16, 0.34, 0.22)    # oxygen fittings are green
ORANGE = (0.66, 0.33, 0.12)        # the one band of colour: a faded international-orange flash
GLOVE, BOOT, JOINT = (0.17, 0.17, 0.18), (0.15, 0.14, 0.13), (0.15, 0.15, 0.14)
LEATHER = (0.24, 0.17, 0.11)
DUST = (0.50, 0.46, 0.39)
PAGE = T.PAGE

fabric, webbing, hard, metal, rubber, glass, gold = T.fabric, T.webbing, T.hard, T.metal, T.rubber, T.glass, T.gold
colour_band, tiny, small, pad, cuff = T.colour_band, T.tiny, T.small, T.pad, T.cuff


def leather(o, seed=0):
    oa.paint(o, wear=0.55, dirt=0.7, mottle=0.30, seed=seed, grain=26.0, gradient=0.02, wear_colour=(0.42, 0.33, 0.24))
    return R.made_of(o, 0.22)


def cord_along(name, origin, axis, forward, keys, t0, t1, angle, colour, radius=0.0042, proud=0.002, wave=0.0, n=3):
    """A restraint cord lying on a limb's skin, running along it at one place round it (angle in degrees from forward)."""
    origin, axis, forward = Vector(origin), Vector(axis).normalized(), Vector(forward).normalized()
    side = forward.cross(axis)
    keys = pad(keys)
    points = []
    count = max(3, int((t1 - t0) / 0.035) + 1)
    for i in range(count + 1):
        t = t0 + (t1 - t0) * i / count
        rs, rf, os_, of = R.along(keys, t)
        a = math.radians(angle + wave * math.sin(i * 1.7))
        out = side * math.sin(a) * (rs + proud) + forward * math.cos(a) * (rf + proud)
        points.append(origin + axis * t + side * os_ + forward * of + out)
    centre = lambda p: p - (origin + axis * (p - origin).dot(axis))
    return R.sweep(name, points, radius, colour, n=n, up=centre)


def lacing(name, origin, axis, forward, keys, t0, t1, angle, colour, crosses=5, width=24.0):
    """A laced adjuster: two rows of eyelets with the lace zig-zagging between them, on a limb's skin."""
    origin, axis, forward = Vector(origin), Vector(axis).normalized(), Vector(forward).normalized()
    side = forward.cross(axis)
    keys = pad(keys)
    points = []
    for i in range(crosses * 2 + 1):
        t = t0 + (t1 - t0) * i / (crosses * 2)
        rs, rf, os_, of = R.along(keys, t)
        a = math.radians(angle + (width * 0.5 if i % 2 else -width * 0.5))
        out = side * math.sin(a) * (rs + 0.003) + forward * math.cos(a) * (rf + 0.003)
        points.append(origin + axis * t + side * os_ + forward * of + out)
    centre = lambda p: p - (origin + axis * (p - origin).dot(axis))
    return R.sweep(name, points, 0.0032, colour, n=4, up=centre)


# ------------------------------------------------------------------ head

def head():
    c = Vector((0, 0, HEAD_Z - 0.004))
    pole, up = Vector((0, 1, -0.16)).normalized(), (0, 0, 1)
    r = 0.156
    o = []
    # A pilot's pressure helmet: a close shell with a wide face opening, a dark visor let into it.
    o.append(hard(R.ball_shell("helmet", c, pole, up, r, 56, 178, SHELL, n=24, rings=9, lip=0.010), 1))
    o.append(glass(R.ball_shell("visor", c, pole, up, r - 0.009, 2, 66, VISOR, n=24, rings=6, close_start=True, close_end=False)))
    # The visor's seal: a rubber rim round the opening.
    o.append(rubber(R.ball_shell("visor seal", c, pole, up, r + 0.004, 54, 61, SEAL, n=24, rings=1, lip=0.004, close_end=False), 2))
    # The sunshade, pushed up over the brow, with its two pivots.
    o.append(hard(R.ball_patch("sunshade", c, pole, up, r + 0.006, (58, 84), -58, 58, (0.16, 0.15, 0.13), thickness=0.004, n=12, m=3), 3))
    for sx in (-1, 1):
        p = c + Vector((sx * 0.146, 0.018, 0.018))
        o.append(metal(oa.cylinder("shade pivot", p, p + Vector((sx * 0.016, 0, 0)), 0.021, ALU, segments=10, bevel=0.003), 4))
        o.append(hard(oa.cylinder("pivot knob", p + Vector((sx * 0.015, 0, 0)), p + Vector((sx * 0.024, 0, 0)), 0.012, STEEL, segments=8, bevel=0.002), 5))
        # The earphone bulge either side of the shell.
        ear = oa.sphere("ear cup", c + Vector((sx * 0.128, -0.022, -0.030)), 0.058, SHELL, segments=12)
        be.squash(ear, c + Vector((sx * 0.128, -0.022, -0.030)), (0.55, 1.0, 1.15))
        o.append(hard(ear, 6))
    # The band of colour over the crown, front to back.
    o.append(colour_band(R.ball_patch("crown flash", c, pole, up, r + 0.0015, (62, 150), -7, 7, ORANGE, thickness=0.0015, n=2, m=10), 7))
    # The microphone and breathing valve under the visor, and the hose that leaves it for the chest.
    chin = c + Vector((0, 0.128, -0.094))
    o.append(hard(small("valve block", chin, (0.074, 0.040, 0.040), (0.32, 0.32, 0.30), bevel=0.008, rot=(18, 0, 0)), 8))
    o.append(metal(oa.cylinder("oxygen fitting", chin + Vector((0.030, 0.006, -0.014)), chin + Vector((0.040, 0.012, -0.044)), 0.015, GREEN_ANOD, segments=10, bevel=0.003), 9))
    o.append(metal(oa.cylinder("exhale valve", chin + Vector((-0.024, 0.016, 0.0)), chin + Vector((-0.024, 0.030, -0.004)), 0.013, ALU, segments=10, bevel=0.002), 10))
    # A dark cloth cover over the nape, so the head can be found from behind, and the rotating neck ring.
    o.append(fabric(R.ball_shell("nape cover", c, pole, up, r + 0.003, 138, 178, VEST, n=16, rings=3, lip=0.003), 11))
    o.append(metal(oa.lathe("neck ring", (0, 0, NECK_Z + 0.008), (0, 0, 1), [(0.0, 0.098), (0.004, 0.114), (0.012, 0.117), (0.022, 0.114), (0.028, 0.100)], ALU, segments=24), 12))
    for i in range(6):
        a = math.radians(30 + i * 60)
        o.append(hard(tiny("ring latch", (math.sin(a) * 0.117, math.cos(a) * 0.117, NECK_Z + 0.020), (0.016, 0.006, 0.012), STEEL, rot=(0, 0, -math.degrees(a))), 13))
    return o


# ------------------------------------------------------------------ torso

TORSO = pad([
    (0.800, 0.030, 0.030, 0, 0.000),
    (0.826, 0.110, 0.080, 0, -0.004),
    (0.880, 0.166, 0.112, 0, -0.004),
    (0.950, 0.176, 0.120, 0, -0.004),
    (1.020, 0.160, 0.112, 0, 0.000),
    (1.090, 0.150, 0.106, 0, 0.002),
    (1.160, 0.164, 0.116, 0, 0.004),
    (1.230, 0.188, 0.132, 0, 0.006),
    (1.330, 0.202, 0.142, 0, 0.008),
    (1.410, 0.198, 0.136, 0, 0.006),
    (1.460, 0.166, 0.122, 0, 0.002),
    (1.495, 0.120, 0.106, 0, 0.000),
    (1.512, 0.096, 0.096, 0, 0.000),
    (1.522, 0.050, 0.050, 0, 0.000),
])
POWER = 2.4
VEST_THICK = 0.030


def torso_y(x, z, front=True):
    rs, rf, _, of = R.along(TORSO, z)
    reach = rf * (1.0 - min(1.0, abs(x / rs)) ** POWER) ** (1.0 / POWER)
    if 1.10 <= z <= 1.44:
        reach += VEST_THICK - 0.019     # things on the chest and back sit on the vest, not on the suit under it
    return of + reach if front else of - reach


def band(name, z0, z1, extra, colour, n=24):
    return cuff(name, (0, 0, 0), (0, 0, 1), (0, 1, 0), TORSO, z0, z1, extra, colour, n=n, power=POWER)


def torso():
    o = []
    body = R.limb("body", (0, 0, 0), (0, 0, 1), (0, 1, 0), TORSO, SUIT, n=22, step=0.026, power=POWER)
    R.displace(body, R.creases(0.0045, (8, 8, 24), seed=3), R.creases(0.0015, (14, 14, 14), seed=4))
    o.append(fabric(body, 20))
    o.append(metal(oa.lathe("collar ring", (0, 0, 1.487), (0, 0, 1), [(0.0, 0.104), (0.004, 0.121), (0.018, 0.121), (0.024, 0.106)], ALU, segments=24), 21))

    # The survival vest: a dark cloth shell over chest and back, open down the front, with a zip and pouches.
    vest = band("vest", 1.105, 1.432, VEST_THICK, VEST, n=26)
    R.displace(vest, R.creases(0.0020, (10, 10, 16), seed=7))
    o.append(webbing(vest, 22))
    o.append(webbing(band("vest hem", 1.096, 1.116, VEST_THICK + 0.003, VEST_LIGHT, n=26), 23))
    o.append(webbing(band("vest collar", 1.420, 1.438, VEST_THICK + 0.003, VEST_LIGHT, n=26), 23))
    # The zip down the middle: two tapes and a row of teeth, a brass pull at the top.
    for k, (z0, z1) in enumerate(((1.112, 1.20), (1.20, 1.29), (1.29, 1.36), (1.36, 1.428))):
        zm = (z0 + z1) * 0.5
        lean = math.degrees(math.atan2(torso_y(0, z1) - torso_y(0, z0), z1 - z0))
        o.append(webbing(small("zip tape", (0, torso_y(0, zm) + 0.0195, zm), (0.026, 0.004, z1 - z0 + 0.004), VEST_LIGHT, bevel=0.0015, rot=(-lean, 0, 0)), 24))
        o.append(metal(tiny("zip", (0, torso_y(0, zm) + 0.0215, zm), (0.006, 0.003, z1 - z0 + 0.004), BRASS, rot=(-lean, 0, 0)), 25))
    o.append(metal(tiny("zip pull", (0, torso_y(0, 1.418) + 0.027, 1.410), (0.008, 0.004, 0.022), BRASS), 26))
    # Pouches: two on the chest, two lower, each with a flap, a press stud and a stitched edge.
    places = ((-0.105, 1.345, 0.078, 0.086), (0.105, 1.345, 0.078, 0.086), (-0.112, 1.205, 0.090, 0.110), (0.112, 1.205, 0.090, 0.110))
    for i, (x, z, w, h) in enumerate(places):
        y = torso_y(x, z) + 0.030
        turn = -x * 230
        o.append(webbing(oa.box("pouch", (x, y, z), (w, 0.034, h), VEST, bevel=0.009, segments=2, rot=(0, 0, turn)), 27 + i))
        o.append(webbing(small("pouch flap", (x, y + 0.003, z + h * 0.34), (w + 0.006, 0.040, h * 0.36), VEST_LIGHT, bevel=0.007, rot=(0, 0, turn)), 31 + i))
        o.append(metal(oa.cylinder("press stud", (x + math.sin(math.radians(-turn)) * 0.022, y + 0.021, z + h * 0.22),
                                   (x + math.sin(math.radians(-turn)) * 0.024, y + 0.026, z + h * 0.22), 0.0042, BRASS, segments=8), 35 + i))
    # The oxygen regulator on the left chest, where the helmet's hose ends, and its gauge.
    reg = Vector((0.060, torso_y(0.060, 1.452) + 0.016, 1.452))
    o.append(metal(oa.cylinder("regulator", reg, reg + Vector((0.002, 0.026, 0.004)), 0.027, GREEN_ANOD, segments=14, bevel=0.004), 39))
    o.append(glass(oa.cylinder("regulator dial", reg + Vector((0.002, 0.026, 0.004)), reg + Vector((0.002, 0.0285, 0.0044)), 0.017, PAGE, segments=12)))
    chin = Vector((0.030, 0.134, HEAD_Z - 0.112))
    hose = R.sweep("breathing hose", [chin + Vector((0.010, 0.010, -0.052)), chin + Vector((0.030, 0.034, -0.078)),
                                      reg + Vector((0.030, 0.050, 0.030)), reg + Vector((0.008, 0.030, 0.010))], 0.0135, SEAL, n=6, cuts=4, ribs=0.12)
    o.append(rubber(hose, 40))
    # The parachute harness: broad webbing over each shoulder, a chest strap, and steel quick releases.
    for sx in (-1, 1):
        x = sx * 0.128
        path = [Vector((x, torso_y(x, 1.10) + 0.024, 1.10)), Vector((x, torso_y(x, 1.25) + 0.024, 1.25)),
                Vector((x, torso_y(x, 1.40) + 0.024, 1.40)), Vector((x, torso_y(x, 1.452) + 0.014, 1.458)),
                Vector((x, 0.020, 1.492)), Vector((x, -0.060, 1.486)), Vector((x, torso_y(x, 1.40, False) - 0.024, 1.40)),
                Vector((x, torso_y(x, 1.20, False) - 0.024, 1.20)), Vector((x, torso_y(x, 1.10, False) - 0.024, 1.10))]
        out = lambda p: Vector((0.0, p.y, max(0.0, p.z - 1.40) * 1.6 + 0.02))
        o.append(webbing(R.sweep("harness", path, 0.022, T.WEB, n=8, up=out, flat=0.15, cuts=2, power=3.0), 41))
        o.append(metal(small("quick release", (x, torso_y(x, 1.398) + 0.032, 1.398), (0.040, 0.012, 0.050), STEEL, bevel=0.004), 42))
        o.append(hard(tiny("release tab", (x, torso_y(x, 1.398) + 0.040, 1.384), (0.022, 0.004, 0.018), ORANGE), 43))
    o.append(webbing(band("chest strap", 1.268, 1.300, VEST_THICK + 0.010, T.WEB, n=26), 44))
    o.append(metal(small("chest buckle", (0, torso_y(0, 1.284) + 0.036, 1.284), (0.058, 0.010, 0.038), STEEL, bevel=0.003), 45))

    # The waist: the suit's entry zip under a flap, and a webbing belt with a knife sheath on the right hip.
    o.append(webbing(band("belt", 1.014, 1.056, 0.009, T.WEB_DARK), 46))
    o.append(metal(small("belt buckle", (0, torso_y(0, 1.035) + 0.013, 1.035), (0.048, 0.010, 0.036), STEEL, bevel=0.003), 47))
    o.append(leather(oa.box("sheath", (0.176, 0.030, 0.972), (0.026, 0.050, 0.150), LEATHER, bevel=0.010, segments=2, rot=(0, -8, 0)), 48))
    o.append(hard(small("knife grip", (0.186, 0.030, 1.068), (0.022, 0.030, 0.070), (0.10, 0.10, 0.10), bevel=0.007, rot=(0, -8, 0)), 49))

    # The back: no pack. Two small oxygen bottles lie across the small of the back over a flat seat kit.
    for i, z in enumerate((1.118, 1.186)):
        y = torso_y(0, z, False) - 0.046
        o.append(metal(oa.cylinder("bottle", (-0.118, y, z), (0.118, y, z), 0.031, GREEN_ANOD if i == 0 else (0.50, 0.50, 0.46), segments=12, bevel=0.012), 50 + i))
        o.append(metal(oa.cylinder("bottle neck", (0.118, y, z), (0.142, y, z), 0.011, BRASS, segments=8), 52 + i))
    for sx in (-1, 1):
        o.append(webbing(small("bottle strap", (sx * 0.062, torso_y(0, 1.152, False) - 0.050, 1.152), (0.022, 0.072, 0.142), T.WEB_DARK, bevel=0.004), 54))
    kit = oa.box("seat kit", (0, torso_y(0, 0.985, False) - 0.036, 0.985), (0.250, 0.078, 0.150), VEST, bevel=0.022, segments=3)
    oa.densify(kit, 0.07)
    R.displace(kit, R.creases(0.003, (11, 11, 11), seed=8))
    o.append(webbing(kit, 56))
    o.append(webbing(small("kit strap", (0, torso_y(0, 0.985, False) - 0.038, 0.985), (0.256, 0.084, 0.020), T.WEB, bevel=0.003), 57))
    # A folded flash of orange cloth tucked under the back of the harness: the signal panel pilots carry.
    o.append(colour_band(small("signal panel", (0, torso_y(0, 1.33, False) - 0.028, 1.330), (0.150, 0.016, 0.090), ORANGE, bevel=0.005), 58))
    return o


# ------------------------------------------------------------------ arm

UPPER_KEYS = [(-0.076, 0.020, 0.020), (-0.056, 0.056, 0.056), (-0.020, 0.077, 0.075), (0.030, 0.077, 0.074), (0.100, 0.067, 0.065),
              (0.180, 0.061, 0.059), (0.250, 0.058, 0.056), (0.288, 0.052, 0.051), (0.310, 0.025, 0.025)]
FORE_KEYS = [(-0.048, 0.020, 0.020), (-0.032, 0.048, 0.047), (0.000, 0.058, 0.056), (0.060, 0.057, 0.055), (0.130, 0.051, 0.049),
             (0.190, 0.046, 0.045), (0.218, 0.043, 0.042), (0.234, 0.025, 0.025)]


def right_arm():
    s = Vector((SHOULDER_X, 0, SHOULDER_Z))
    e = s + Vector((UPPER_ARM, 0, 0))
    w = e + Vector((FOREARM, 0, 0))
    X, F = (1, 0, 0), (0, 1, 0)
    upper, fore, hand = [], [], []

    sleeve = R.limb("upper sleeve", s, X, F, UPPER_KEYS, SUIT, n=14, fine=((0.26, 0.070),))
    R.displace(sleeve, R.gathers(e, X, 0.070, 0.0065, 0.030, seed=1, lean=(0, 1, 0), bias=0.4),
               R.gathers(s, X, 0.050, 0.0045, 0.030, seed=9, lean=(0, 0, -1), bias=0.5), R.creases(0.0042, (18, 7, 7), seed=2))
    upper.append(fabric(sleeve, 60))
    upper.append(webbing(cuff("shoulder cuff", s, X, F, UPPER_KEYS, 0.030, 0.062, 0.009, JOINT, n=14), 61))
    upper.append(colour_band(cuff("arm flash", s, X, F, UPPER_KEYS, 0.118, 0.160, 0.009, ORANGE, n=14), 62))
    upper.append(webbing(cuff("elbow band", s, X, F, UPPER_KEYS, 0.254, 0.288, 0.009, JOINT, n=14), 63))
    # The restraint cords that stop a pressure suit ballooning: two along the sleeve, and a laced adjuster on top.
    for a, seed in ((70, 64), (-70, 65)):
        upper.append(webbing(cord_along("restraint cord", s, X, F, UPPER_KEYS, 0.066, 0.250, a, CORD), seed))
    upper.append(webbing(lacing("sleeve lacing", s, X, F, UPPER_KEYS, 0.170, 0.248, 0, CORD, crosses=4), 66))

    arm = R.limb("forearm", e, X, F, FORE_KEYS, SUIT, n=14, fine=((0.0, 0.055),))
    R.displace(arm, R.gathers(e, X, 0.062, 0.0060, 0.030, seed=5, lean=(0, 1, 0), bias=0.4), R.creases(0.0038, (18, 7, 7), seed=6))
    fore.append(fabric(arm, 67))
    for a, seed in ((70, 68), (-70, 69)):
        fore.append(webbing(cord_along("restraint cord", e, X, F, FORE_KEYS, 0.030, 0.186, a, CORD), seed))
    # The glove's locking ring: a bright bearing ring with a red lock tab, as on the real suit.
    fore.append(metal(oa.lathe("glove ring", e + Vector((0.196, 0, 0)), X, [(0.0, 0.041), (0.004, 0.0505), (0.012, 0.052), (0.028, 0.0505), (0.034, 0.044), (0.038, 0.030)], ALU, segments=14), 70))
    fore.append(hard(small("ring lock", e + Vector((0.212, 0, 0.051)), (0.018, 0.022, 0.009), (0.52, 0.12, 0.09), bevel=0.003), 71))

    palm = R.limb("palm", w, X, F, [(-0.030, 0.015, 0.015), (-0.015, 0.038, 0.038), (0.012, 0.040, 0.042), (0.045, 0.028, 0.046),
                                    (0.085, 0.022, 0.048), (0.106, 0.017, 0.044), (0.114, 0.008, 0.030)], GLOVE, n=12, step=0.014, power=2.4)
    R.displace(palm, R.creases(0.0020, (30, 12, 12), seed=7))
    hand.append(leather(palm, 72))
    hand.append(webbing(small("knuckle pad", w + Vector((0.074, 0.0, 0.024)), (0.040, 0.086, 0.007), (0.11, 0.11, 0.12), bevel=0.003), 73))
    for i, (y, length) in enumerate(((0.035, 0.074), (0.012, 0.082), (-0.011, 0.076), (-0.033, 0.060))):
        p = w + Vector((0.098, y, -0.004))
        path = R.smooth_path([p, p + Vector((length * 0.42, 0, -0.005)), p + Vector((length * 0.76, 0, -0.022)), p + Vector((length * 0.96, 0, -0.047))], 2)
        hand.append(leather(R.sweep("finger", path, 0.0122, GLOVE, n=6, radii=[0.0122, 0.0125, 0.0123, 0.0120, 0.0118, 0.0114, 0.0080][:len(path)]), 74 + i))
    p = w + Vector((0.030, 0.038, -0.008))
    path = R.smooth_path([p, p + Vector((0.030, 0.026, -0.010)), p + Vector((0.054, 0.036, -0.026)), p + Vector((0.068, 0.036, -0.046))], 2)
    hand.append(leather(R.sweep("thumb", path, 0.015, GLOVE, n=6, radii=[0.0160, 0.0156, 0.0148, 0.0140, 0.0136, 0.0128, 0.0090][:len(path)]), 78))
    grow = Matrix.Translation(w) @ Matrix.Scale(1.05, 4) @ Matrix.Translation(-w)
    for o in hand:
        o.data.transform(grow)
        o.data.update()
    return upper, fore, hand


# ------------------------------------------------------------------ leg

THIGH_KEYS = [(-0.086, 0.020, 0.020), (-0.066, 0.070, 0.074), (-0.020, 0.090, 0.098), (0.050, 0.091, 0.101, 0, 0.004),
              (0.150, 0.086, 0.095, 0, 0.006), (0.260, 0.079, 0.086, 0, 0.004), (0.360, 0.072, 0.076), (0.420, 0.068, 0.071),
              (0.452, 0.052, 0.054), (0.468, 0.020, 0.020)]
SHIN_KEYS = [(-0.062, 0.020, 0.020), (-0.046, 0.057, 0.059), (0.000, 0.071, 0.075, 0, 0.005), (0.060, 0.068, 0.073, 0, 0.000),
             (0.130, 0.069, 0.076, 0, -0.008), (0.220, 0.063, 0.067, 0, -0.004), (0.310, 0.059, 0.061), (0.375, 0.057, 0.059),
             (0.400, 0.034, 0.034)]


def right_leg():
    h = Vector((HIP_X, 0, HIP_Z))
    k = Vector((HIP_X, 0, KNEE_Z))
    a = Vector((HIP_X, 0, ANKLE_Z))
    D, F = (0, 0, -1), (0, 1, 0)
    thigh, shin, foot = [], [], []

    t = R.limb("thigh", h, D, F, THIGH_KEYS, SUIT, n=16, fine=((0.40, 0.070),))
    R.displace(t, R.gathers(k, D, 0.075, 0.0068, 0.034, seed=11, lean=(0, -1, 0), bias=0.4),
               R.gathers(h, D, 0.060, 0.0052, 0.032, seed=12, lean=(0, 1, 0), bias=0.6), R.creases(0.0052, (7, 7, 18), seed=13))
    thigh.append(fabric(t, 80))
    thigh.append(webbing(cuff("hip band", h, D, F, THIGH_KEYS, 0.058, 0.094, 0.009, JOINT, n=16), 81))
    # The parachute harness's leg strap, round the top of the thigh, and a restraint cord down each side.
    thigh.append(webbing(cuff("leg strap", h, D, F, THIGH_KEYS, 0.108, 0.140, 0.010, T.WEB, n=16), 82))
    thigh.append(metal(small("leg strap buckle", h + Vector((0.020, 0.106, -0.124)), (0.034, 0.008, 0.030), STEEL, bevel=0.003), 83))
    for ang, seed in ((90, 84), (-90, 85)):
        thigh.append(webbing(cord_along("restraint cord", h, D, F, THIGH_KEYS, 0.150, 0.410, ang, CORD), seed))
    # A flat pocket on the front of the thigh with a clear map window, the way pilots carry a checklist.
    front = Vector((HIP_X, 0.094, HIP_Z - 0.250))
    thigh.append(fabric(oa.box("thigh pocket", front, (0.110, 0.016, 0.150), SUIT_DARK, bevel=0.006, segments=2, rot=(-2, 0, 0)), 86))
    thigh.append(hard(tiny("map window", front + Vector((0, 0.009, 0.010)), (0.086, 0.002, 0.100), (0.40, 0.42, 0.36), rot=(-2, 0, 0)), 87))
    thigh.append(webbing(small("pocket flap", front + Vector((0, 0.004, 0.068)), (0.116, 0.020, 0.030), SUIT, bevel=0.004, rot=(-2, 0, 0)), 88))

    sh = R.limb("shin", k, D, F, SHIN_KEYS, SUIT, n=14, fine=((0.0, 0.065),))
    R.displace(sh, R.gathers(k, D, 0.070, 0.0068, 0.034, seed=14, lean=(0, -1, 0), bias=0.4), R.creases(0.0050, (7, 7, 18), seed=15))
    shin.append(fabric(sh, 89))
    shin.append(webbing(cuff("knee band", k, D, F, SHIN_KEYS, -0.036, -0.004, 0.009, JOINT, n=14), 90))
    # The laced calf adjuster.
    shin.append(webbing(lacing("calf lacing", k, D, F, SHIN_KEYS, 0.090, 0.210, 180, CORD, crosses=5), 92))
    # The top of a tall flight boot, laced up the front over the suit's leg.
    top = cuff("boot top", k, D, F, SHIN_KEYS, 0.236, 0.394, 0.009, BOOT, n=14)
    shin.append(leather(top, 93))
    shin.append(webbing(lacing("boot lacing", k, D, F, SHIN_KEYS, 0.250, 0.388, 0, (0.30, 0.28, 0.24), crosses=6, width=20.0), 94))
    shin.append(leather(cuff("boot collar", k, D, F, SHIN_KEYS, 0.228, 0.246, 0.013, LEATHER, n=14), 95))

    base = Vector((HIP_X, 0, 0))
    upper_keys = [(-0.110, 0.022, 0.022, 0, 0.042), (-0.098, 0.048, 0.050, 0, 0.070), (-0.045, 0.060, 0.068, 0, 0.092),
                  (0.020, 0.062, 0.064, 0, 0.090), (0.085, 0.063, 0.046, 0, 0.074), (0.160, 0.060, 0.036, 0, 0.064),
                  (0.214, 0.050, 0.031, 0, 0.058), (0.236, 0.024, 0.018, 0, 0.050)]
    boot = R.limb("boot", base, (0, 1, 0), (0, 0, 1), upper_keys, BOOT, n=14, step=0.020, power=2.3)
    R.displace(boot, R.creases(0.0028, (10, 24, 10), seed=16))
    foot.append(leather(boot, 96))
    sole_keys = [(-0.118, 0.032, 0.015, 0, 0.015), (-0.106, 0.052, 0.015, 0, 0.015), (-0.045, 0.063, 0.015, 0, 0.015),
                 (0.085, 0.066, 0.015, 0, 0.015), (0.170, 0.064, 0.015, 0, 0.015), (0.224, 0.054, 0.015, 0, 0.017), (0.244, 0.028, 0.012, 0, 0.020)]
    sole = R.limb("sole", base, (0, 1, 0), (0, 0, 1), sole_keys, (0.08, 0.08, 0.08), n=10, step=0.024, power=4.0)
    foot.append(R.stripes(rubber(sole, 97), (0, 1, 0), 0.030, (0.03, 0.03, 0.03), share=0.5, strength=0.8))
    heel = oa.box("heel", (HIP_X, -0.070, 0.016), (0.100, 0.080, 0.030), (0.07, 0.07, 0.07), bevel=0.008, segments=2)
    foot.append(rubber(heel, 98))
    foot.append(leather(oa.sphere("ankle", a, 0.056, BOOT, segments=10), 99))
    foot.append(leather(small("toe cap", (HIP_X, 0.196, 0.048), (0.098, 0.070, 0.050), (0.11, 0.10, 0.10), bevel=0.020), 100))
    # The laces over the instep.
    lace = [Vector((HIP_X + (0.020 if i % 2 else -0.020), 0.060 + i * 0.018, 0.150 - i * 0.016)) for i in range(6)]
    foot.append(webbing(R.sweep("instep lacing", lace, 0.0032, (0.30, 0.28, 0.24), n=4, up=(0, 0.6, 1)), 101))
    return thigh, shin, foot


def make():
    parts = {"Head": head(), "Torso": torso()}
    upper, fore, hand = right_arm()
    thigh, shin, foot = right_leg()
    parts["RightUpperArm"], parts["RightForearm"], parts["RightHand"] = upper, fore, hand
    parts["RightThigh"], parts["RightShin"], parts["RightFoot"] = thigh, shin, foot
    for name in ("UpperArm", "Forearm", "Hand", "Thigh", "Shin", "Foot"):
        parts["Left" + name] = be.mirrored(parts["Right" + name])

    # What the two sides do not share: a wrist altimeter on the left forearm, a mirror on the right, as pilots wear.
    e = Vector((-(SHOULDER_X + UPPER_ARM), 0, SHOULDER_Z))
    parts["LeftForearm"].append(webbing(oa.box("altimeter strap", e + Vector((-0.118, 0, 0.0)), (0.030, 0.112, 0.106), T.WEB_DARK, bevel=0.042, segments=3), 110))
    parts["LeftForearm"].append(metal(oa.cylinder("altimeter", e + Vector((-0.118, 0, 0.050)), e + Vector((-0.118, 0, 0.066)), 0.027, STEEL, segments=14, bevel=0.003), 111))
    parts["LeftForearm"].append(glass(oa.cylinder("altimeter face", e + Vector((-0.118, 0, 0.0655)), e + Vector((-0.118, 0, 0.0675)), 0.022, PAGE, segments=12)))
    e = Vector((SHOULDER_X + UPPER_ARM, 0, SHOULDER_Z))
    parts["RightForearm"].append(webbing(oa.box("mirror strap", e + Vector((0.118, 0, 0.0)), (0.024, 0.108, 0.102), T.WEB_DARK, bevel=0.040, segments=3), 112))
    parts["RightForearm"].append(metal(small("mirror", e + Vector((0.118, 0, 0.054)), (0.050, 0.060, 0.006), (0.74, 0.76, 0.78), bevel=0.002), 113))

    everything = [o for name in be.ORDER for o in parts[name]]
    R.crowd_shadow(everything, strength=0.62, reach=0.20)
    R.dust(everything, DUST, top=0.80, amount=0.55, on_top=0.08, seed=9)
    return parts


FAR_KEEP = ("helmet", "visor", "visor seal", "nape cover", "crown flash", "harness", "vest", "sunshade")
FAR_THIN_TOO = ("finger", "thumb")
FAR_LEAVE_OUT = ("restraint cord", "sleeve lacing", "calf lacing", "boot lacing", "instep lacing", "zip pull", "press stud",
                 "ring latch", "release tab", "pivot knob", "bottle neck", "ring lock", "leg strap buckle", "map window", "pocket flap",
                 "pouch flap", "exhale valve", "knuckle pad", "belt buckle", "chest buckle", "kit strap")


def triangles(parts):
    return sum(oa._triangle_count(o) for name in be.ORDER for o in parts[name])


def compare(folder, pose=None, trooper_pose=None):
    """The trooper on the left, the Drifter on the right: same place, same light, same camera."""
    old = T.make()
    new = make()
    print("OA Drifter: %d triangles (the trooper %d)" % (triangles(new), triangles(old)))
    for parts, p in ((old, trooper_pose), (new, pose)):
        if p:
            T.stand(parts, p)
        else:
            T.lower_arms(parts)
    old_objects = [o for name in be.ORDER for o in old[name]]
    new_objects = [o for name in be.ORDER for o in new[name]]
    for o in old_objects:
        o.data.transform(Matrix.Translation(Vector((0.58, 0, 0))))
    for o in new_objects:
        o.data.transform(Matrix.Translation(Vector((-0.58, 0, 0))))
    shown = old_objects + new_objects + [be.ground()]
    os.makedirs(folder, exist_ok=True)
    shots = (("pair_front", (0.0, 4.0, 1.30), (0.0, 0, 0.95), 42),
             ("pair_close", (0.0, 2.2, 1.48), (0.0, 0, 1.30), 34),
             ("pair_three_quarter", (1.5, 3.9, 1.50), (0.0, 0, 0.95), 42),
             ("pair_back", (-1.1, -4.0, 1.55), (0.0, 0, 0.95), 42),
             ("drifter_close", (-1.25, 1.55, 1.50), (-0.58, 0, 1.30), 50),
             ("drifter_legs", (-1.20, 1.70, 0.60), (-0.58, 0, 0.42), 50))
    wanted = [a for a in sys.argv if a.startswith("only=")]
    for name, eye, target, lens in shots:
        if wanted and name not in wanted[0][5:].split(","):
            continue
        oa.render_preview(os.path.join(folder, name + ".png"), shown, eye, target, lens=lens, size=(1280, 800), samples=64)


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    oa.reset_scene()
    if "--compare" in args:
        pose = trooper = None
        if "--pose" in args:
            import json
            with open(args[args.index("--pose") + 1]) as f:
                kinds = json.load(f)["kinds"]
            which = args[args.index("--pose") + 2]
            trooper = [k for k in kinds if k["model"] == "Enemy"][0][which]
            mine = [k for k in kinds if k["model"] == NAME]
            pose = mine[0][which] if mine else trooper
        compare(args[args.index("--compare") + 1], pose, trooper)
    else:
        if "--write-to" in args:
            be.OUT = os.path.abspath(args[args.index("--write-to") + 1])
        parts = make()
        print("OA Drifter: %d triangles" % triangles(parts))
        be.write(parts, be.stump(), NAME)
        far = be.write_far(parts, NAME, keep=FAR_KEEP, leave_out=FAR_LEAVE_OUT, thin_too=FAR_THIN_TOO)
        holes = oa.inside_out_where([o for name in be.ORDER for o in far[name]])
        print("OA far copy, seen from inside:", holes if holes else "nothing")
