"""The common enemy drawn again in the realistic look, to be judged beside the current one before anything is replaced.

    blender -b -P tools/blender/build_enemy_realistic.py -- --compare <folder>
    blender -b -P tools/blender/build_enemy_realistic.py -- --compare <folder> --pose <kinds.json> ready
                                    (both stand as the game's animator holds them; the file is pose_dump's, mode "kinds")

writes pictures of the current enemy (left) and this one (right) from the same place in the same light. It writes
no game file: the game still draws Enemy.bytes until Matthew has said yes to this level of realism.

What "realistic" means here (docs/art_direction.md): the proportions of a person in a real pressure suit, standing
on the game's own skeleton (BodySkeleton.cs, copied in build_enemy.py); cloth that hangs and gathers at the joints;
the hardware a suit really has (bearing rings, a chest control box with hoses to a life-support pack, a gold sun
visor, a wrist disconnect, blue silicone fingertips and soles); a soldier's kit worn over it (magazines on the belt,
a pistol on the thigh, knee pads); colours as they are in photographs, with dust, shadow and wear worked into the
paint. The pieces are the same fourteen, cut at the same joints, so the dismemberment system does not change.
"""
import bpy, sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import oa_blender as oa
import oa_real as R
import build_enemy as be
from mathutils import Vector, Matrix

SPINE_Z, NECK_Z, HEAD_Z = be.SPINE_Z, be.NECK_Z, be.HEAD_Z
SHOULDER_X, SHOULDER_Z, UPPER_ARM, FOREARM = be.SHOULDER_X, be.SHOULDER_Z, be.UPPER_ARM, be.FOREARM
HIP_X, HIP_Z, KNEE_Z, ANKLE_Z = be.HIP_X, be.HIP_Z, be.KNEE_Z, be.ANKLE_Z

# ---- colours, as seen on screen: taken from what these things really look like, greyed by use ----
FABRIC = (0.76, 0.74, 0.68)       # white suit cloth after a long time outdoors
FABRIC_PACK = (0.66, 0.65, 0.60)
WEB = (0.37, 0.32, 0.22)          # coyote webbing
WEB_DARK = (0.19, 0.18, 0.15)
ARMOUR = (0.23, 0.25, 0.23)       # dark grey-green hard shell
SHELL = (0.72, 0.71, 0.67)        # helmet shell
ALU = (0.60, 0.61, 0.62)
STEEL = (0.17, 0.175, 0.18)
RUBBER = (0.055, 0.055, 0.06)
SILICONE = (0.20, 0.27, 0.36)     # blue-grey fingertips and soles
GOLD = (0.86, 0.60, 0.20)
STRIPE = (0.50, 0.11, 0.08)       # the red band real suits carry to tell one wearer from another
BLUE_ANOD = (0.08, 0.20, 0.44)
RED_ANOD = (0.50, 0.09, 0.08)
DUST = (0.47, 0.44, 0.39)
LENS = (0.80, 0.83, 0.86)
PAGE = (0.80, 0.78, 0.66)


def fabric(o, seed=0):
    oa.paint(o, wear=0.06, dirt=0.9, mottle=0.26, seed=seed, grain=24.0, gradient=0.03)
    return R.made_of(o, 0.06)


def webbing(o, seed=0):
    oa.paint(o, wear=0.22, dirt=0.8, mottle=0.35, seed=seed, grain=30.0, gradient=0.02, wear_colour=(0.55, 0.50, 0.38))
    return R.made_of(o, 0.08)


def hard(o, seed=0):
    oa.paint(o, wear=0.8, dirt=0.6, mottle=0.22, seed=seed, grain=22.0, gradient=0.03, wear_colour=(0.62, 0.62, 0.59))
    return R.made_of(o, 0.42)


def metal(o, seed=0):
    oa.paint(o, wear=0.5, dirt=0.7, mottle=0.15, seed=seed, grain=30.0, gradient=0.0, wear_colour=(0.86, 0.86, 0.87))
    return R.made_of(o, 0.66, 1.0)


def rubber(o, seed=0):
    oa.paint(o, wear=0.3, dirt=0.4, mottle=0.2, seed=seed, grain=26.0, gradient=0.0, wear_colour=(0.24, 0.24, 0.25))
    return R.made_of(o, 0.24)


def gold(o):
    oa.paint(o, wear=0.0, dirt=0.0, mottle=0.04, gradient=0.0)
    return R.made_of(o, 0.88, 1.0)


def glass(o):
    oa.paint(o, wear=0.0, dirt=0.0, mottle=0.05, gradient=0.0)
    return R.made_of(o, 0.9)


def tiny(name, centre, size, colour, rot=(0, 0, 0)):
    """A fitting too small for rounded edges to show: a plain box, twelve triangles."""
    return oa.box(name, centre, size, colour, bevel=0, rot=rot)


def small(name, centre, size, colour, bevel=0.004, rot=(0, 0, 0)):
    return oa.box(name, centre, size, colour, bevel=bevel, segments=1, rot=rot)


def pad(keys):
    return [tuple(k) + (0.0,) * (5 - len(k)) for k in keys]


def cuff(name, origin, axis, forward, keys, t0, t1, extra, colour, n=16, power=2.0):
    """A band round a limb, following the limb's own shape, standing `extra` proud of it."""
    origin, axis, forward = Vector(origin), Vector(axis).normalized(), Vector(forward).normalized()
    side = forward.cross(axis)
    keys = pad(keys)
    rings = []
    for t, d in ((t0 - 0.003, -0.006), (t0, extra), (t1, extra), (t1 + 0.003, -0.006)):
        rs, rf, os_, of = R.along(keys, t)
        rings.append(R.ring(origin + axis * t + side * os_ + forward * of, side, forward, rs + d, rf + d, n, power))
    return R.loft(name, rings, colour)


# ------------------------------------------------------------------ head

def head():
    c = Vector((0, 0, HEAD_Z))
    pole, up = Vector((0, 1, -0.12)).normalized(), (0, 0, 1)
    r = 0.163
    o = []
    o.append(hard(R.ball_shell("helmet", c, pole, up, r, 61, 178, SHELL, n=24, rings=9, lip=0.012), 1))
    o.append(gold(R.ball_shell("visor", c, pole, up, r - 0.011, 2, 72, GOLD, n=24, rings=7, close_start=True, close_end=False)))
    # The sun shade, pulled a little way down over the top of the visor.
    o.append(hard(R.ball_patch("sun shade", c, pole, up, r + 0.002, (42, 66), -64, 64, (0.60, 0.60, 0.57), thickness=0.005, n=12, m=3), 2))
    # The band of colour over the crown that tells one suit from another.
    o.append(hard(R.ball_patch("crown stripe", c, pole, up, r + 0.001, (66, 152), -8, 8, STRIPE, thickness=0.0015, n=2, m=10), 3))
    for sx in (-1, 1):
        # The pivot the shades swing on, at each temple.
        o.append(hard(oa.cylinder("pivot", c + Vector((sx * 0.150, 0.004, -0.012)), c + Vector((sx * 0.177, 0.004, -0.012)), 0.036, ARMOUR, segments=12, bevel=0.005), 4))
        o.append(metal(oa.cylinder("pivot bolt", c + Vector((sx * 0.176, 0.004, -0.012)), c + Vector((sx * 0.182, 0.004, -0.012)), 0.014, ALU, segments=6), 5))
        # Helmet lamps above each temple.
        lamp = c + Vector((sx * 0.112, 0.088, 0.108))
        o.append(hard(small("lamp", lamp, (0.050, 0.066, 0.042), ARMOUR, bevel=0.008, rot=(-8, 0, -sx * 16)), 6))
        o.append(glass(oa.cylinder("lamp lens", lamp + Vector((sx * 0.009, 0.030, -0.004)), lamp + Vector((sx * 0.011, 0.037, -0.005)), 0.016, LENS, segments=8)))
    # A camera on the right, where real helmets carry one.
    o.append(hard(oa.cylinder("camera", c + Vector((0.160, 0.030, 0.045)), c + Vector((0.156, 0.100, 0.040)), 0.017, STEEL, segments=10, bevel=0.003), 7))
    o.append(glass(oa.cylinder("camera lens", c + Vector((0.156, 0.099, 0.040)), c + Vector((0.1558, 0.104, 0.0398)), 0.011, (0.05, 0.06, 0.08), segments=8)))
    o.append(hard(small("vent pad", c + Vector((0, -0.150, -0.045)), (0.100, 0.034, 0.075), (0.56, 0.56, 0.53), bevel=0.012, rot=(14, 0, 0)), 8))
    o.append(metal(oa.lathe("neck ring", (0, 0, NECK_Z + 0.010), (0, 0, 1), [(0.0, 0.106), (0.004, 0.123), (0.026, 0.123), (0.032, 0.110)], ALU, segments=24), 9))
    o.append(hard(small("neck latch", (0, 0.124, NECK_Z + 0.024), (0.044, 0.012, 0.020), STEEL, bevel=0.003), 10))
    return o


# ------------------------------------------------------------------ torso

TORSO = pad([
    (0.800, 0.030, 0.030, 0, 0.000),
    (0.826, 0.115, 0.082, 0, -0.004),
    (0.880, 0.176, 0.118, 0, -0.004),
    (0.950, 0.190, 0.128, 0, -0.004),
    (1.020, 0.178, 0.122, 0, 0.000),
    (1.090, 0.167, 0.118, 0, 0.002),
    (1.160, 0.180, 0.128, 0, 0.005),
    (1.230, 0.204, 0.146, 0, 0.008),
    (1.330, 0.216, 0.156, 0, 0.010),
    (1.410, 0.210, 0.150, 0, 0.008),
    (1.460, 0.178, 0.134, 0, 0.002),
    (1.495, 0.130, 0.116, 0, 0.000),
    (1.512, 0.104, 0.104, 0, 0.000),
    (1.522, 0.050, 0.050, 0, 0.000),
])
TORSO_POWER = 2.5


def torso_y(x, z, front=True):
    """Where the suit's surface is, at a height and a distance from the middle."""
    rs, rf, _, of = R.along(TORSO, z)
    reach = rf * (1.0 - min(1.0, abs(x / rs)) ** TORSO_POWER) ** (1.0 / TORSO_POWER)
    return of + reach if front else of - reach


def band(name, z0, z1, extra, colour):
    return cuff(name, (0, 0, 0), (0, 0, 1), (0, 1, 0), TORSO, z0, z1, extra, colour, n=24, power=TORSO_POWER)


def torso():
    o = []
    body = R.limb("body", (0, 0, 0), (0, 0, 1), (0, 1, 0), TORSO, FABRIC, n=22, step=0.028, power=TORSO_POWER)
    soft = lambda p: max(0.0, min(1.0, (1.22 - p.z) / 0.08))          # the chest is a hard shell under the cloth
    R.displace(body, R.creases(0.0055, (7, 7, 21), seed=3, where=soft), R.creases(0.0015, (12, 12, 12), seed=4))
    o.append(fabric(body, 20))
    # The metal ring where the top and bottom halves of a real suit lock together, and the ring the helmet locks to.
    o.append(metal(band("waist ring", 1.186, 1.208, 0.007, ALU), 21))
    o.append(metal(oa.lathe("collar ring", (0, 0, 1.489), (0, 0, 1), [(0.0, 0.112), (0.004, 0.130), (0.020, 0.130), (0.026, 0.114)], ALU, segments=24), 22))

    # The control box on the chest: display on top, switches on the face, a valve knob at each side.
    box = Vector((0, torso_y(0, 1.32) + 0.030, 1.318))
    o.append(fabric(oa.box("control box", box, (0.210, 0.072, 0.130), FABRIC_PACK, bevel=0.016, segments=3), 23))
    o.append(hard(small("control face", box + Vector((0, 0.034, 0.004)), (0.170, 0.008, 0.086), ARMOUR, bevel=0.003), 24))
    o.append(glass(tiny("display", box + Vector((-0.030, 0.008, 0.066)), (0.090, 0.040, 0.004), (0.06, 0.09, 0.08), rot=(0, 0, 0))))
    for i in range(4):
        o.append(metal(tiny("switch", box + Vector((-0.060 + i * 0.026, 0.043, 0.022)), (0.007, 0.014, 0.018), ALU, rot=(-20, 0, 0)), 25 + i))
    o.append(hard(small("switch guard", box + Vector((-0.021, 0.041, 0.003)), (0.112, 0.012, 0.005), STEEL, bevel=0.002), 29))
    o.append(hard(tiny("label", box + Vector((0.052, 0.0385, -0.022)), (0.046, 0.002, 0.018), PAGE), 30))
    o.append(hard(oa.cylinder("gauge", box + Vector((0.052, 0.036, 0.020)), box + Vector((0.052, 0.046, 0.020)), 0.019, STEEL, segments=12, bevel=0.002), 31))
    o.append(glass(oa.cylinder("gauge face", box + Vector((0.052, 0.0455, 0.020)), box + Vector((0.052, 0.0475, 0.020)), 0.015, PAGE, segments=12)))
    o.append(metal(oa.cylinder("purge valve", box + Vector((0.103, 0.004, 0.010)), box + Vector((0.132, 0.004, 0.010)), 0.021, RED_ANOD, segments=12, bevel=0.004), 32))
    o.append(metal(oa.cylinder("temperature valve", box + Vector((-0.103, 0.004, 0.010)), box + Vector((-0.128, 0.004, 0.010)), 0.024, ALU, segments=12, bevel=0.004), 33))
    # Two hoses leave the chest by anodised connectors and run round the waist to the pack.
    for sx, colour, seed in ((1, BLUE_ANOD, 34), (-1, RED_ANOD, 35)):
        a = Vector((sx * 0.078, torso_y(0.078, 1.225) + 0.004, 1.222))
        o.append(metal(oa.cylinder("connector", a, a + Vector((sx * 0.004, 0.026, -0.012)), 0.023, colour, segments=12, bevel=0.004), seed))
        path = [a + Vector((sx * 0.004, 0.028, -0.014)), a + Vector((sx * 0.020, 0.046, -0.050)),
                Vector((sx * 0.150, torso_y(0.150, 1.13) + 0.030, 1.125)), Vector((sx * 0.215, 0.030, 1.118)),
                Vector((sx * 0.212, -0.070, 1.125)), Vector((sx * 0.170, -0.150, 1.135))]
        hose = R.sweep("hose", path, 0.0155, (0.20, 0.21, 0.19), n=6, cuts=5, ribs=0.10)
        o.append(rubber(hose, seed))

    # A soldier's belt over the suit: three magazines in front, a pouch on the left hip.
    o.append(webbing(band("belt", 0.992, 1.040, 0.010, WEB), 36))
    o.append(hard(small("buckle", (0, torso_y(0, 1.016) + 0.014, 1.016), (0.052, 0.012, 0.040), STEEL, bevel=0.003), 37))
    for i, x in enumerate((-0.108, 0.108)):
        y = torso_y(x, 1.05) + 0.022
        turn = -x * 250
        o.append(webbing(small("magazine pouch", (x, y, 1.052), (0.066, 0.038, 0.128), WEB, bevel=0.009, rot=(0, 0, turn)), 38 + i))
        o.append(webbing(small("pouch flap", (x, y + 0.002, 1.100), (0.070, 0.044, 0.040), (0.33, 0.29, 0.20), bevel=0.008, rot=(0, 0, turn)), 40 + i))
        o.append(hard(tiny("flap tab", (x + math.sin(math.radians(-turn)) * 0.024, y + 0.024, 1.074), (0.018, 0.004, 0.026), WEB_DARK, rot=(0, 0, turn)), 42 + i))
    o.append(webbing(oa.box("hip pouch", (-0.196, -0.010, 1.000), (0.050, 0.110, 0.120), WEB, bevel=0.016, segments=3), 44))
    o.append(webbing(small("hip pouch flap", (-0.200, -0.010, 1.046), (0.056, 0.116, 0.036), (0.33, 0.29, 0.20), bevel=0.012), 45))

    # The life-support pack: a cloth-covered box as wide as the shoulders, a smaller unit on top, an aerial.
    pack = oa.box("pack", (0, -0.232, 1.262), (0.390, 0.190, 0.500), FABRIC_PACK, bevel=0.034, segments=3)
    oa.densify(pack, 0.10)
    R.displace(pack, R.creases(0.003, (9, 9, 9), seed=6))
    o.append(fabric(pack, 46))
    top = oa.box("pack top", (0, -0.238, 1.552), (0.330, 0.160, 0.110), FABRIC_PACK, bevel=0.030, segments=3)
    o.append(fabric(top, 47))
    o.append(webbing(small("pack strap", (0, -0.236, 1.505), (0.396, 0.196, 0.022), WEB_DARK, bevel=0.004), 48))
    o.append(webbing(small("pack strap low", (0, -0.234, 1.080), (0.396, 0.196, 0.022), WEB_DARK, bevel=0.004), 49))
    o.append(hard(tiny("access plate", (0.040, -0.330, 1.290), (0.200, 0.008, 0.240), (0.52, 0.52, 0.49)), 50))
    for dx, dz in ((-0.085, -0.105), (0.085, -0.105), (-0.085, 0.105), (0.085, 0.105)):
        o.append(metal(oa.cylinder("plate bolt", (0.040 + dx, -0.333, 1.290 + dz), (0.040 + dx, -0.338, 1.290 + dz), 0.007, ALU, segments=6), 51))
    o.append(hard(tiny("warning label", (-0.125, -0.3285, 1.380), (0.070, 0.002, 0.040), (0.70, 0.56, 0.16)), 52))
    o.append(metal(oa.cylinder("canister", (-0.150, -0.250, 1.020), (0.150, -0.250, 1.020), 0.046, (0.42, 0.44, 0.40), segments=14, bevel=0.012), 53))
    o.append(rubber(oa.cylinder("aerial", (0.130, -0.250, 1.600), (0.150, -0.262, 1.840), 0.006, RUBBER, radius_end=0.004, segments=6), 54))
    o.append(hard(oa.cylinder("aerial base", (0.128, -0.249, 1.596), (0.131, -0.251, 1.626), 0.013, STEEL, segments=8, bevel=0.002), 55))
    # The pack's straps come over the shoulders and down the chest to the waist ring.
    for sx in (-1, 1):
        x = sx * 0.118
        path = [Vector((x, torso_y(x, 1.205) + 0.004, 1.205)), Vector((x, torso_y(x, 1.30) + 0.004, 1.30)),
                Vector((x, torso_y(x, 1.40) + 0.004, 1.40)), Vector((x, torso_y(x, 1.455) + 0.004, 1.458)),
                Vector((x, 0.030, 1.496)), Vector((x, -0.050, 1.492)), Vector((x, -0.120, 1.455)), Vector((x, -0.150, 1.400))]
        out = lambda p: Vector((0.0, p.y, max(0.0, p.z - 1.40) * 1.6 + 0.02))
        o.append(webbing(R.sweep("harness", path, 0.021, WEB_DARK, n=8, up=out, flat=0.17, cuts=2, power=3.0), 56))
        o.append(hard(small("harness buckle", (x, torso_y(x, 1.245) + 0.009, 1.245), (0.050, 0.008, 0.026), STEEL, bevel=0.002), 57))
    return o


# ------------------------------------------------------------------ arm

UPPER_KEYS = [(-0.078, 0.020, 0.020), (-0.058, 0.060, 0.060), (-0.020, 0.083, 0.081), (0.030, 0.084, 0.080), (0.100, 0.073, 0.070),
              (0.180, 0.066, 0.064), (0.250, 0.062, 0.060), (0.288, 0.055, 0.054), (0.310, 0.025, 0.025)]
FORE_KEYS = [(-0.048, 0.020, 0.020), (-0.032, 0.050, 0.049), (0.000, 0.061, 0.059), (0.060, 0.060, 0.058), (0.130, 0.054, 0.052),
             (0.190, 0.048, 0.047), (0.218, 0.045, 0.044), (0.234, 0.025, 0.025)]


def right_arm():
    s = Vector((SHOULDER_X, 0, SHOULDER_Z))
    e = s + Vector((UPPER_ARM, 0, 0))
    w = e + Vector((FOREARM, 0, 0))
    X, F = (1, 0, 0), (0, 1, 0)
    upper, fore, hand = [], [], []

    sleeve = R.limb("upper sleeve", s, X, F, UPPER_KEYS, FABRIC, n=16, fine=((0.26, 0.075),))
    R.displace(sleeve, R.gathers(e, X, 0.070, 0.0060, 0.032, seed=1, lean=(0, 1, 0), bias=0.4),
               R.creases(0.0040, (17, 6, 6), seed=2))
    upper.append(fabric(sleeve, 60))
    upper.append(metal(cuff("shoulder bearing", s, X, F, UPPER_KEYS, 0.040, 0.062, 0.006, ALU), 61))
    upper.append(hard(cuff("arm stripe", s, X, F, UPPER_KEYS, 0.128, 0.166, 0.0075, STRIPE), 62))

    arm = R.limb("forearm", e, X, F, FORE_KEYS, FABRIC, n=16, fine=((0.0, 0.06),))
    R.displace(arm, R.gathers(e, X, 0.065, 0.0060, 0.032, seed=5, lean=(0, 1, 0), bias=0.4),
               R.creases(0.0036, (17, 6, 6), seed=6))
    fore.append(fabric(arm, 63))
    fore.append(metal(oa.lathe("wrist ring", e + Vector((0.200, 0, 0)), X, [(0.0, 0.043), (0.004, 0.0525), (0.030, 0.0525), (0.036, 0.046), (0.040, 0.030)], ALU, segments=18), 64))
    fore.append(hard(small("wrist lock", e + Vector((0.215, 0, 0.053)), (0.022, 0.026, 0.010), STEEL, bevel=0.003), 65))

    # The glove: a cloth gauntlet, a flat palm, four fingers and a thumb with silicone tips. Palm down, thumb forward.
    palm = R.limb("palm", w, X, F, [(-0.030, 0.015, 0.015), (-0.015, 0.040, 0.040), (0.012, 0.042, 0.044), (0.045, 0.029, 0.048),
                                    (0.085, 0.023, 0.050), (0.106, 0.018, 0.046), (0.114, 0.008, 0.030)], FABRIC, n=14, step=0.014, power=2.4)
    R.displace(palm, R.creases(0.0020, (30, 12, 12), seed=7))
    hand.append(fabric(palm, 66))
    hand.append(webbing(small("palm strap", w + Vector((0.062, 0.0, 0.024)), (0.026, 0.094, 0.006), WEB_DARK, bevel=0.002), 67))
    for i, (y, length) in enumerate(((0.036, 0.076), (0.012, 0.084), (-0.012, 0.078), (-0.035, 0.062))):
        p = w + Vector((0.098, y, -0.004))
        path = R.smooth_path([p, p + Vector((length * 0.42, 0, -0.005)), p + Vector((length * 0.76, 0, -0.022)), p + Vector((length * 0.96, 0, -0.047))], 2)
        hand.append(fabric(R.sweep("finger", path[:5], 0.0125, FABRIC, n=6, radii=[0.0125, 0.0128, 0.0125, 0.0122, 0.0120]), 68 + i))
        hand.append(rubber(R.sweep("fingertip", path[4:], 0.0128, SILICONE, n=6, radii=[0.0128, 0.0126, 0.0085]), 72 + i))
    p = w + Vector((0.030, 0.040, -0.008))
    path = R.smooth_path([p, p + Vector((0.030, 0.026, -0.010)), p + Vector((0.054, 0.036, -0.026)), p + Vector((0.068, 0.036, -0.046))], 2)
    hand.append(fabric(R.sweep("thumb", path[:5], 0.015, FABRIC, n=6, radii=[0.0165, 0.0160, 0.0150, 0.0142, 0.0138]), 76))
    hand.append(rubber(R.sweep("thumb tip", path[4:], 0.014, SILICONE, n=6, radii=[0.0145, 0.0140, 0.0095]), 77))
    return upper, fore, hand


# ------------------------------------------------------------------ leg

THIGH_KEYS = [(-0.086, 0.020, 0.020), (-0.066, 0.074, 0.078), (-0.020, 0.096, 0.104), (0.050, 0.098, 0.108, 0, 0.004),
              (0.150, 0.093, 0.102, 0, 0.006), (0.260, 0.085, 0.092, 0, 0.004), (0.360, 0.077, 0.081), (0.420, 0.072, 0.075),
              (0.452, 0.054, 0.056), (0.468, 0.020, 0.020)]
SHIN_KEYS = [(-0.062, 0.020, 0.020), (-0.046, 0.060, 0.062), (0.000, 0.075, 0.079, 0, 0.005), (0.060, 0.072, 0.077, 0, 0.000),
             (0.130, 0.073, 0.080, 0, -0.008), (0.220, 0.066, 0.070, 0, -0.004), (0.310, 0.061, 0.063), (0.375, 0.058, 0.060),
             (0.400, 0.034, 0.034)]


def right_leg():
    h = Vector((HIP_X, 0, HIP_Z))
    k = Vector((HIP_X, 0, KNEE_Z))
    a = Vector((HIP_X, 0, ANKLE_Z))
    D, F = (0, 0, -1), (0, 1, 0)
    thigh, shin, foot = [], [], []

    t = R.limb("thigh", h, D, F, THIGH_KEYS, FABRIC, n=18, fine=((0.40, 0.075),))
    R.displace(t, R.gathers(k, D, 0.075, 0.0065, 0.036, seed=11, lean=(0, -1, 0), bias=0.4),
               R.gathers(h, D, 0.060, 0.0050, 0.034, seed=12, lean=(0, 1, 0), bias=0.6),
               R.creases(0.0050, (6, 6, 17), seed=13))
    thigh.append(fabric(t, 80))
    thigh.append(hard(cuff("thigh stripe", h, D, F, THIGH_KEYS, 0.300, 0.338, 0.008, STRIPE, n=18), 81))

    sh = R.limb("shin", k, D, F, SHIN_KEYS, FABRIC, n=16, fine=((0.0, 0.07),))
    R.displace(sh, R.gathers(k, D, 0.070, 0.0065, 0.036, seed=14, lean=(0, -1, 0), bias=0.4),
               R.creases(0.0048, (6, 6, 17), seed=15))
    shin.append(fabric(sh, 82))
    # A hard knee pad on two straps.
    cap = oa.sphere("knee pad", k + Vector((0, 0.056, -0.006)), 0.067, ARMOUR, segments=14)
    be.squash(cap, k + Vector((0, 0.056, -0.006)), (1.0, 0.62, 1.28))
    shin.append(hard(cap, 83))
    shin.append(webbing(cuff("pad strap", k, D, F, SHIN_KEYS, -0.040, -0.020, 0.007, WEB_DARK), 84))
    shin.append(webbing(cuff("pad strap low", k, D, F, SHIN_KEYS, 0.048, 0.068, 0.007, WEB_DARK), 85))
    # The boot's cloth collar gathered over the ankle.
    collar = cuff("boot collar", k, D, F, SHIN_KEYS, 0.300, 0.392, 0.010, (0.60, 0.59, 0.55))
    shin.append(fabric(collar, 86))
    shin.append(webbing(cuff("collar strap", k, D, F, SHIN_KEYS, 0.326, 0.344, 0.014, WEB_DARK), 87))

    # The boot: a moulded upper on a ribbed silicone sole, like the overshoes worn on the Moon.
    base = Vector((HIP_X, 0, 0))
    upper_keys = [(-0.112, 0.022, 0.022, 0, 0.042), (-0.100, 0.050, 0.050, 0, 0.072), (-0.045, 0.064, 0.070, 0, 0.094),
                  (0.020, 0.066, 0.066, 0, 0.092), (0.085, 0.068, 0.050, 0, 0.078), (0.160, 0.067, 0.040, 0, 0.070),
                  (0.212, 0.058, 0.034, 0, 0.064), (0.234, 0.026, 0.018, 0, 0.054)]
    boot = R.limb("boot", base, (0, 1, 0), (0, 0, 1), upper_keys, (0.56, 0.56, 0.53), n=14, step=0.022, power=2.3)
    R.displace(boot, R.creases(0.0025, (10, 22, 10), seed=16))
    foot.append(fabric(boot, 88))
    sole_keys = [(-0.120, 0.034, 0.019, 0, 0.019), (-0.108, 0.056, 0.019, 0, 0.019), (-0.045, 0.069, 0.019, 0, 0.019),
                 (0.085, 0.073, 0.019, 0, 0.019), (0.170, 0.072, 0.019, 0, 0.019), (0.222, 0.062, 0.019, 0, 0.021), (0.242, 0.030, 0.015, 0, 0.024)]
    sole = R.limb("sole", base, (0, 1, 0), (0, 0, 1), sole_keys, SILICONE, n=10, step=0.024, power=4.0)
    foot.append(R.stripes(rubber(sole, 89), (0, 1, 0), 0.048, (0.06, 0.08, 0.11), share=0.5, strength=0.75))
    foot.append(rubber(oa.sphere("ankle", a, 0.058, (0.56, 0.56, 0.53), segments=10), 90))
    strap = [Vector((HIP_X - 0.074, 0.050, 0.050)), Vector((HIP_X - 0.060, 0.050, 0.118)), Vector((HIP_X, 0.050, 0.152)),
             Vector((HIP_X + 0.060, 0.050, 0.118)), Vector((HIP_X + 0.074, 0.050, 0.050))]
    foot.append(webbing(R.sweep("instep strap", strap, 0.012, WEB_DARK, n=6, up=lambda p: Vector((p.x - HIP_X, 0, p.z - 0.07)), flat=0.25, cuts=2), 91))
    foot.append(hard(small("strap buckle", (HIP_X + 0.068, 0.050, 0.100), (0.008, 0.026, 0.022), STEEL, bevel=0.002, rot=(0, 16, 0)), 92))
    return thigh, shin, foot


def make():
    parts = {"Head": head(), "Torso": torso()}
    upper, fore, hand = right_arm()
    thigh, shin, foot = right_leg()
    parts["RightUpperArm"], parts["RightForearm"], parts["RightHand"] = upper, fore, hand
    parts["RightThigh"], parts["RightShin"], parts["RightFoot"] = thigh, shin, foot
    for name in ("UpperArm", "Forearm", "Hand", "Thigh", "Shin", "Foot"):
        parts["Left" + name] = be.mirrored(parts["Right" + name])

    # What the two sides do not share. A pistol on the right thigh:
    h = Vector((HIP_X, 0, HIP_Z))
    D, F = (0, 0, -1), (0, 1, 0)
    x = HIP_X + 0.116
    parts["RightThigh"].append(hard(small("holster", (x, 0.004, HIP_Z - 0.200), (0.036, 0.088, 0.170), STEEL, bevel=0.010), 100))
    parts["RightThigh"].append(hard(small("pistol slide", (x, 0.000, HIP_Z - 0.104), (0.027, 0.120, 0.030), (0.10, 0.10, 0.105), bevel=0.004), 101))
    parts["RightThigh"].append(rubber(small("pistol grip", (x, -0.052, HIP_Z - 0.062), (0.026, 0.040, 0.088), (0.10, 0.09, 0.08), bevel=0.008, rot=(-16, 0, 0)), 102))
    parts["RightThigh"].append(webbing(cuff("holster strap", h, D, F, THIGH_KEYS, 0.150, 0.172, 0.008, WEB_DARK, n=18), 103))
    parts["RightThigh"].append(webbing(cuff("holster strap low", h, D, F, THIGH_KEYS, 0.240, 0.262, 0.008, WEB_DARK, n=18), 104))
    parts["RightThigh"].append(webbing(tiny("drop strap", (x - 0.010, 0.004, HIP_Z - 0.050), (0.006, 0.040, 0.150), WEB_DARK, rot=(0, -6, 0)), 105))
    # A cargo pocket on the left thigh:
    lx = -(HIP_X + 0.101)
    parts["LeftThigh"].append(fabric(oa.box("pocket", (lx, 0.010, HIP_Z - 0.215), (0.036, 0.104, 0.130), FABRIC, bevel=0.014, segments=3), 106))
    parts["LeftThigh"].append(fabric(small("pocket flap", (lx - 0.003, 0.010, HIP_Z - 0.166), (0.040, 0.110, 0.040), (0.70, 0.68, 0.62), bevel=0.010), 107))
    # A checklist strapped to the left forearm, as astronauts wear one:
    e = Vector((-(SHOULDER_X + UPPER_ARM), 0, SHOULDER_Z))
    parts["LeftForearm"].append(webbing(oa.box("checklist band", e + Vector((-0.120, 0, 0.0)), (0.050, 0.118, 0.112), WEB_DARK, bevel=0.045, segments=4), 108))
    parts["LeftForearm"].append(hard(small("checklist", e + Vector((-0.120, 0.004, 0.060)), (0.074, 0.060, 0.010), PAGE, bevel=0.002, rot=(8, 0, 0)), 109))
    parts["LeftForearm"].append(metal(tiny("checklist rings", e + Vector((-0.120, -0.026, 0.066)), (0.070, 0.006, 0.008), ALU, rot=(8, 0, 0)), 110))

    everything = [o for name in be.ORDER for o in parts[name]]
    R.crowd_shadow(everything, strength=0.62, reach=0.20)
    R.dust(everything, DUST, top=0.80, amount=0.62, on_top=0.10, seed=5)
    return parts


def triangles(parts):
    return sum(oa._triangle_count(o) for name in be.ORDER for o in parts[name])


def lower_arms(parts, degrees=66):
    """For pictures only: brings the arms down from the T they are modelled in, turning each about its shoulder."""
    for side, sign in (("Right", 1), ("Left", -1)):
        pivot = Vector((sign * SHOULDER_X, 0, SHOULDER_Z))
        turn = Matrix.Translation(pivot) @ Matrix.Rotation(math.radians(sign * degrees), 4, 'Y') @ Matrix.Translation(-pivot)
        for name in ("UpperArm", "Forearm", "Hand"):
            for o in parts[side + name]:
                o.data.transform(turn)
                o.data.update()


def stand(parts, pose):
    """For pictures only: puts the pieces where the game's own animator holds them (a pose from tools/blender/pose_dump)."""
    for name in be.ORDER:
        m = be.game_matrix(pose[name]) @ Matrix.Translation(-Vector(be.ORIGINS[name]))
        for o in parts[name]:
            o.data.transform(m)
            o.data.update()


def compare(folder, pose=None):
    """The current enemy on the left, this one on the right: same place, same light, same camera."""
    old = be.make()
    new = make()
    print("OA realistic enemy: %d triangles (the current one %d)" % (triangles(new), triangles(old)))
    for parts in (old, new):
        if pose:
            stand(parts, pose)
        else:
            lower_arms(parts)
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
             ("pair_back", (-1.1, -4.0, 1.55), (0.0, 0, 0.95), 42))
    wanted = [a for a in sys.argv if a.startswith("only=")]
    for name, eye, target, lens in shots:
        if wanted and name not in wanted[0][5:].split(","):
            continue
        oa.render_preview(os.path.join(folder, name + ".png"), shown, eye, target, lens=lens, size=(1280, 800), samples=64)


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    oa.reset_scene()
    if "--compare" in args:
        pose = None
        if "--pose" in args:
            import json
            with open(args[args.index("--pose") + 1]) as f:
                kinds = json.load(f)["kinds"]
            pose = [k for k in kinds if k["model"] == "Enemy"][0][args[args.index("--pose") + 2]]
        compare(args[args.index("--compare") + 1], pose)
    elif "--write-to" in args:
        # Writes Enemy.bytes and EnemyFar.bytes into a folder of your choosing, to try the game's rule tests on
        # them (OA_MODELS=<folder>). It never writes into the game's own Resources folder.
        be.OUT = os.path.abspath(args[args.index("--write-to") + 1])
        assert "Resources" not in be.OUT, "this script does not replace the game's enemy; see the top of the file"
        parts = make()
        be.write(parts, be.stump(), "Enemy")
        be.write_far(parts, "Enemy")
    else:
        print("OA realistic enemy: %d triangles" % triangles(make()))
