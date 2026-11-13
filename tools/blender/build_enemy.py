"""Builds the game's enemy: a stout, patched-up suit raider in fourteen breakable pieces, plus the wound stump.

    blender -b -P tools/blender/build_enemy.py                       (writes Resources/Models/Enemy.bytes)
    blender -b -P tools/blender/build_enemy.py -- --preview <folder> (also renders pictures of the rest pose)
    blender -b -P tools/blender/build_enemy.py -- --poses <file.json> --preview <folder>
                                                                     (renders the poses a C# pose dump describes)

The figure is modelled standing at the origin in a T-pose, facing +Y, in metres, with the joints exactly where
the game's skeleton has them (Scripts/Runtime/Dismemberment/BodySkeleton.cs). Each piece is then written in its
own space, measured from its joint, under the name of its body part, so the game can hang it on the matching
bone and shoot it off. The numbers below are a copy of the skeleton's; the rule test
EnemyModelFile_MatchesTheSkeleton fails if they drift.

Every piece starts with a ball where it joins its parent, so no gap opens when a joint bends.
"""
import bpy, sys, os, math, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import oa_blender as oa
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "unity", "Assets", "OrbitalAnte", "Resources", "Models"))

# ---- the skeleton (a copy of BodySkeleton.cs; heights are above the feet) ----
SPINE_Z = 1.05
NECK_Z = 1.50
HEAD_Z = 1.69
SHOULDER_X, SHOULDER_Z = 0.24, 1.40
UPPER_ARM, FOREARM = 0.28, 0.26
HIP_X, HIP_Z = 0.105, 0.92
KNEE_Z, ANKLE_Z = 0.50, 0.09

# A little cartoon exaggeration: big head, big mitts, big boots.
HEAD_SCALE = 1.08
HAND_SCALE = 1.16
FOOT_SCALE = 1.12

# ---- colours, as seen on screen ----
SUIT = (0.22, 0.43, 0.43)        # dusty teal cloth
SUIT_DARK = (0.15, 0.31, 0.33)
SHELL = (0.87, 0.80, 0.64)       # cream hard shell
SIGNAL = (0.83, 0.36, 0.13)      # rust orange
BRASS = (0.66, 0.52, 0.26)
RUBBER = (0.13, 0.13, 0.16)
PACK = (0.40, 0.42, 0.36)
GLOVE = (0.74, 0.53, 0.22)       # mustard mitts: the gun hand must show
VISOR = (0.20, 0.10, 0.03)
LENS = (0.98, 0.84, 0.42)
FLESH = (0.66, 0.07, 0.07)
BONE = (0.90, 0.86, 0.74)


def cloth(o, seed=0):
    oa.paint(o, wear=0.25, dirt=0.65, mottle=0.9, seed=seed, grain=14.0, gradient=0.06)
    return o


def shell(o, seed=0):
    oa.paint(o, wear=0.5, dirt=0.55, mottle=0.45, seed=seed, grain=12.0, wear_colour=(0.97, 0.94, 0.86))
    return o


def metal(o, seed=0):
    oa.paint(o, wear=0.75, dirt=0.6, mottle=0.5, seed=seed, grain=20.0, wear_colour=(0.93, 0.83, 0.55))
    return o


def rubber(o, seed=0):
    oa.paint(o, wear=0.35, dirt=0.4, mottle=0.5, seed=seed, grain=18.0, wear_colour=(0.36, 0.36, 0.40))
    return o


def glass(o, seed=0):
    oa.paint(o, wear=0.0, dirt=0.0, mottle=0.25, seed=seed, grain=6.0, gradient=0.35)
    return o


def small_box(name, centre, size, colour, bevel=0.004, segments=1, rot=(0, 0, 0)):
    """A box whose rounded edges are one cut wide unless it asks for more: enough for small fittings."""
    return oa.box(name, centre, size, colour, bevel=bevel, segments=segments, rot=rot)


def squash(obj, pivot, scale):
    """Scales a piece about a point (to make a round thing oval)."""
    p = Vector(pivot)
    obj.data.transform(Matrix.Translation(p) @ Matrix.Diagonal((*scale, 1.0)) @ Matrix.Translation(-p))
    obj.data.update()
    return obj


def tilt(obj, pivot, axis, degrees):
    p = Vector(pivot)
    obj.data.transform(Matrix.Translation(p) @ Matrix.Rotation(math.radians(degrees), 4, axis) @ Matrix.Translation(-p))
    obj.data.update()
    return obj


def mirrored(objects):
    """The same pieces for the other side of the body."""
    out = []
    for o in objects:
        c = o.copy()
        c.data = o.data.copy()
        bpy.context.scene.collection.objects.link(c)
        c.data.transform(Matrix.Diagonal((-1.0, 1.0, 1.0, 1.0)))
        c.data.flip_normals()
        c.data.update()
        out.append(c)
    return out


def bellows(name, origin, axis, length, radius, ribs, colour, seed):
    """A ribbed, flexible joint cover."""
    profile = [(0.0, radius * 0.72)]
    step = length / (ribs * 2)
    for i in range(ribs):
        profile.append(((2 * i + 1) * step, radius * 1.06))
        profile.append(((2 * i + 2) * step, radius * 0.86))
    profile.append((length + step * 0.5, radius * 0.80))
    return rubber(oa.lathe(name, origin, axis, profile, colour, segments=16), seed)


# ------------------------------------------------------------------ the pieces

def head():
    c = Vector((0, 0, HEAD_Z))
    o = []
    o.append(shell(squash(oa.sphere("helmet", c, 0.160, SHELL, segments=24), c, (1.04, 1.0, 0.97)), 1))
    # A wide amber visor in a brass frame, and a rebreather snout under it: a face you can read from far away.
    vc = c + Vector((0, 0.082, 0.022))
    o.append(metal(squash(oa.sphere("visor frame", vc, 0.104, BRASS, segments=16), vc, (1.32, 0.94, 0.82)), 2))
    vc2 = c + Vector((0, 0.096, 0.022))
    o.append(glass(squash(oa.sphere("visor", vc2, 0.096, VISOR, segments=16), vc2, (1.30, 0.93, 0.70)), 3))
    # A glint bar across the visor so it never reads as a flat black hole.
    o.append(glass(squash(oa.sphere("visor glint", vc2 + Vector((0.040, 0.062, 0.026)), 0.026, (0.80, 0.52, 0.22), segments=8),
                          vc2 + Vector((0.040, 0.062, 0.026)), (1.5, 0.5, 0.30)), 4))
    o.append(shell(oa.cylinder("snout", c + Vector((0, 0.105, -0.085)), c + Vector((0, 0.192, -0.095)), 0.046, SIGNAL, radius_end=0.040, segments=16, bevel=0.008), 5))
    o.append(rubber(oa.cylinder("snout grille", c + Vector((0, 0.188, -0.095)), c + Vector((0, 0.200, -0.096)), 0.028, RUBBER, segments=12, bevel=0.003), 6))
    for sx in (-1, 1):
        o.append(rubber(oa.cylinder("filter", c + Vector((sx * 0.030, 0.135, -0.092)), c + Vector((sx * 0.098, 0.108, -0.098)), 0.031, RUBBER, segments=12, bevel=0.006), 7))
        o.append(metal(oa.cylinder("filter cap", c + Vector((sx * 0.094, 0.110, -0.098)), c + Vector((sx * 0.106, 0.105, -0.099)), 0.024, BRASS, segments=12, bevel=0.003), 8))
        o.append(metal(oa.cylinder("ear", c + Vector((sx * 0.140, -0.005, -0.005)), c + Vector((sx * 0.182, -0.005, -0.005)), 0.040, SIGNAL, radius_end=0.032, segments=14, bevel=0.007), 9))
    # A crest from brow to nape: the helmet's own silhouette.
    arc = []
    for i in range(11):
        a = math.radians(38 + 118 * i / 10)
        arc.append((math.cos(a) * 0.186, HEAD_Z + math.sin(a) * 0.180))
    inner = [(math.cos(math.radians(38 + 118 * i / 10)) * 0.150, HEAD_Z + math.sin(math.radians(38 + 118 * i / 10)) * 0.146) for i in range(10, -1, -1)]
    o.append(shell(oa.slab("crest", arc + inner, 0.034, SIGNAL, bevel=0.007, segments=2), 11))
    # Aerial on one ear, work lamp on the other brow.
    o.append(metal(oa.cylinder("aerial", c + Vector((0.168, -0.005, 0.03)), c + Vector((0.196, -0.035, 0.235)), 0.0065, RUBBER, radius_end=0.004, segments=8), 12))
    o.append(shell(oa.sphere("aerial tip", c + Vector((0.197, -0.036, 0.240)), 0.015, SIGNAL, segments=10), 13))
    o.append(shell(oa.cylinder("lamp", c + Vector((-0.108, 0.070, 0.098)), c + Vector((-0.118, 0.142, 0.094)), 0.031, SHELL, segments=14, bevel=0.006), 14))
    o.append(glass(oa.cylinder("lamp lens", c + Vector((-0.1175, 0.140, 0.094)), c + Vector((-0.119, 0.149, 0.0935)), 0.023, LENS, segments=12, bevel=0.003), 15))
    o.append(metal(oa.lathe("neck ring", (0, 0, NECK_Z + 0.012), (0, 0, 1), [(0.0, 0.070), (0.006, 0.098), (0.034, 0.098), (0.046, 0.084)], BRASS, segments=20), 16))
    for piece in o:
        squash(piece, (0, 0, NECK_Z), (HEAD_SCALE, HEAD_SCALE, HEAD_SCALE))
    return o


def torso():
    o = []
    # Cloth body: pelvis, quilted belly, chest. Oval, wider than it is deep.
    body = [(0.845, 0.03), (0.862, 0.105), (0.905, 0.152), (0.955, 0.170), (0.992, 0.172)]
    z = 1.04
    while z < 1.19:
        body.append((z, 0.166 + (z - 1.04) * 0.10))
        body.append((z + 0.022, 0.174 + (z - 1.04) * 0.10))
        z += 0.044
    body += [(1.22, 0.188), (1.30, 0.196), (1.40, 0.190), (1.46, 0.150), (1.49, 0.10), (1.50, 0.06)]
    o.append(cloth(squash(oa.lathe("body", (0, 0, 0), (0, 0, 1), body, SUIT, segments=20), (0, 0, 0), (1.0, 0.80, 1.0)), 20))
    # Cream hard shell over the chest and shoulders.
    plate = [(1.215, 0.170), (1.222, 0.204), (1.30, 0.210), (1.39, 0.205), (1.45, 0.168), (1.487, 0.118), (1.508, 0.100), (1.512, 0.07)]
    o.append(shell(squash(oa.lathe("chest shell", (0, 0, 0), (0, 0, 1), plate, SHELL, segments=20), (0, 0, 0), (1.0, 0.82, 1.0)), 21))
    o.append(metal(oa.lathe("collar", (0, 0, 1.492), (0, 0, 1), [(0.0, 0.080), (0.004, 0.106), (0.022, 0.106), (0.028, 0.090)], BRASS, segments=20), 22))
    # Belt, buckle, pouches.
    o.append(rubber(squash(oa.lathe("belt", (0, 0, 0.985), (0, 0, 1), [(0.0, 0.165), (0.006, 0.182), (0.050, 0.182), (0.056, 0.165)], RUBBER, segments=20), (0, 0, 0), (1.0, 0.80, 1.0)), 23))
    o.append(metal(small_box("buckle", (0, 0.146, 1.013), (0.066, 0.020, 0.050), BRASS, bevel=0.007), 24))
    for sx in (-1, 1):
        o.append(cloth(tilt(small_box("pouch", (sx * 0.128, 0.094, 0.975), (0.064, 0.050, 0.082), SUIT_DARK, bevel=0.012), (sx * 0.128, 0.094, 0.975), 'Z', -sx * 38), 25))
        o.append(metal(oa.cylinder("pouch stud", (sx * 0.143, 0.112, 0.992), (sx * 0.150, 0.121, 0.992), 0.008, BRASS, segments=8), 26))
        # Shoulder bearing rings the arms plug into.
        o.append(metal(oa.lathe("shoulder ring", (sx * 0.178, 0, SHOULDER_Z), (sx, 0, 0), [(0.0, 0.070), (0.006, 0.092), (0.040, 0.092), (0.050, 0.078)], BRASS, segments=18), 27))
    # Chest box with a dial and three buttons.
    o.append(rubber(small_box("chest box", (0.012, 0.166, 1.305), (0.150, 0.056, 0.108), (0.23, 0.25, 0.31), bevel=0.012), 28))
    o.append(metal(oa.cylinder("dial", (-0.030, 0.190, 1.318), (-0.030, 0.203, 1.318), 0.026, BRASS, segments=14, bevel=0.004), 29))
    o.append(rubber(small_box("dial hand", (-0.030, 0.2045, 1.326), (0.006, 0.004, 0.026), RUBBER, bevel=0.0015, rot=(0, 25, 0)), 30))
    for i, colour in enumerate(((0.85, 0.20, 0.14), (0.93, 0.74, 0.20), (0.30, 0.62, 0.42))):
        o.append(glass(oa.cylinder("button", (0.030 + i * 0.026, 0.192, 1.330), (0.030 + i * 0.026, 0.201, 1.330), 0.009, colour, segments=10, bevel=0.002), 31 + i))
    o.append(shell(small_box("label", (0.056, 0.1945, 1.288), (0.070, 0.004, 0.026), SHELL, bevel=0.002), 34))
    # A stripe down the shell, off centre.
    o.append(shell(small_box("stripe", (-0.105, 0.150, 1.33), (0.036, 0.030, 0.16), SIGNAL, bevel=0.008, rot=(-9, 0, 0)), 35))
    # Backpack: a box, two tanks and a bedroll on top.
    o.append(shell(small_box("pack", (0, -0.190, 1.275), (0.350, 0.140, 0.360), PACK, bevel=0.035, segments=2), 36))
    o.append(shell(small_box("pack lid", (0, -0.262, 1.33), (0.19, 0.022, 0.15), (0.50, 0.52, 0.45), bevel=0.008), 37))
    for sx in (-1, 1):
        o.append(shell(oa.cylinder("tank", (sx * 0.192, -0.200, 1.095), (sx * 0.192, -0.200, 1.400), 0.050, SIGNAL, segments=14, bevel=0.022), 38))
        o.append(metal(oa.cylinder("tank valve", (sx * 0.192, -0.200, 1.398), (sx * 0.192, -0.200, 1.428), 0.016, BRASS, segments=10, bevel=0.003), 39))
        o.append(rubber(oa.lathe("roll strap", (sx * 0.090, -0.185, 1.492), (1, 0, 0), [(-0.010, 0.044), (-0.008, 0.059), (0.008, 0.059), (0.010, 0.044)], RUBBER, segments=14), 40))
    o.append(cloth(oa.cylinder("bedroll", (-0.175, -0.185, 1.492), (0.175, -0.185, 1.492), 0.054, SUIT_DARK, segments=14, bevel=0.016), 41))
    return o


def right_arm():
    s = Vector((SHOULDER_X, 0, SHOULDER_Z))
    e = s + Vector((UPPER_ARM, 0, 0))
    w = e + Vector((FOREARM, 0, 0))
    upper, fore, hand = [], [], []

    upper.append(cloth(oa.sphere("shoulder", s, 0.090, SUIT, segments=14), 50))
    upper.append(cloth(oa.lathe("sleeve", s, (1, 0, 0), [(0.0, 0.055), (0.030, 0.084), (0.11, 0.080), (0.20, 0.069), (0.262, 0.061), (0.284, 0.046)], SUIT, segments=16), 51))
    upper.append(shell(oa.lathe("arm band", s + Vector((0.105, 0, 0)), (1, 0, 0), [(0.0, 0.070), (0.004, 0.086), (0.040, 0.084), (0.044, 0.070)], SIGNAL, segments=16), 52))

    fore.append(rubber(oa.sphere("elbow", e, 0.062, RUBBER, segments=12), 53))
    fore.append(bellows("elbow bellows", e + Vector((0.004, 0, 0)), (1, 0, 0), 0.070, 0.062, 3, RUBBER, 54))
    fore.append(shell(oa.lathe("forearm", e + Vector((0.070, 0, 0)), (1, 0, 0), [(0.0, 0.050), (0.008, 0.060), (0.060, 0.066), (0.120, 0.070), (0.130, 0.050)], SHELL, segments=16), 55))
    fore.append(metal(oa.lathe("cuff", e + Vector((0.186, 0, 0)), (1, 0, 0), [(0.0, 0.050), (0.005, 0.069), (0.050, 0.069), (0.058, 0.060), (0.074, 0.058), (0.078, 0.040)], BRASS, segments=16), 56))

    hand.append(rubber(oa.sphere("wrist", w, 0.046, RUBBER, segments=10), 57))
    hand.append(rubber(small_box("palm", w + Vector((0.058, 0, 0)), (0.094, 0.090, 0.052), GLOVE, bevel=0.020, segments=3), 58))
    hand.append(rubber(small_box("fingers", w + Vector((0.124, -0.004, -0.012)), (0.066, 0.084, 0.044), GLOVE, bevel=0.019, segments=3, rot=(0, 22, 0)), 59))
    hand.append(rubber(oa.cylinder("thumb", w + Vector((0.040, 0.036, -0.006)), w + Vector((0.096, 0.072, -0.020)), 0.019, GLOVE, radius_end=0.016, segments=10, bevel=0.007), 60))
    hand.append(shell(small_box("knuckle pad", w + Vector((0.066, -0.002, 0.027)), (0.062, 0.070, 0.014), SIGNAL, bevel=0.005), 61))
    for piece in hand:
        squash(piece, w, (HAND_SCALE, HAND_SCALE, HAND_SCALE))
    return upper, fore, hand


def right_leg():
    h = Vector((HIP_X, 0, HIP_Z))
    k = Vector((HIP_X, 0, KNEE_Z))
    a = Vector((HIP_X, 0, ANKLE_Z))
    thigh, shin, foot = [], [], []
    down = (0, 0, -1)

    thigh.append(cloth(oa.sphere("hip", h, 0.094, SUIT, segments=14), 70))
    thigh.append(cloth(oa.lathe("thigh", h, down, [(0.0, 0.060), (0.030, 0.104), (0.14, 0.106), (0.28, 0.090), (0.385, 0.076), (0.424, 0.058)], SUIT, segments=16), 71))
    thigh.append(cloth(small_box("thigh pocket", h + Vector((0.088, 0.004, -0.205)), (0.040, 0.094, 0.118), SUIT_DARK, bevel=0.013), 72))
    thigh.append(metal(oa.cylinder("pocket stud", h + Vector((0.106, 0.004, -0.168)), h + Vector((0.113, 0.004, -0.168)), 0.009, BRASS, segments=8), 73))

    shin.append(rubber(oa.sphere("knee", k, 0.072, RUBBER, segments=12), 74))
    shin.append(bellows("knee bellows", k + Vector((0, 0, -0.004)), down, 0.066, 0.073, 3, RUBBER, 75))
    shin.append(shell(small_box("knee pad", k + Vector((0, 0.062, -0.004)), (0.112, 0.050, 0.124), SHELL, bevel=0.022, segments=3), 76))
    shin.append(cloth(oa.lathe("shin", k + Vector((0, 0, -0.066)), down, [(0.0, 0.058), (0.010, 0.071), (0.12, 0.073), (0.235, 0.066), (0.245, 0.05)], SUIT, segments=16), 77))
    shin.append(shell(small_box("shin guard", k + Vector((0, 0.058, -0.185)), (0.104, 0.050, 0.200), SHELL, bevel=0.020), 78))
    shin.append(metal(oa.lathe("boot cuff", k + Vector((0, 0, -0.300)), down, [(0.0, 0.056), (0.006, 0.083), (0.058, 0.083), (0.068, 0.070), (0.090, 0.066), (0.096, 0.045)], BRASS, segments=16), 79))

    foot.append(rubber(oa.sphere("ankle", a, 0.066, RUBBER, segments=10), 80))
    upper = oa.sphere("boot", a + Vector((0, 0.046, -0.022)), 0.072, RUBBER, segments=14)
    foot.append(rubber(squash(upper, a + Vector((0, 0.046, -0.022)), (0.95, 1.85, 0.84)), 81))
    foot.append(rubber(small_box("sole", a + Vector((0, 0.050, -0.069)), (0.134, 0.300, 0.042), (0.07, 0.07, 0.09), bevel=0.016, segments=2), 82))
    cap = oa.sphere("toe cap", a + Vector((0, 0.140, -0.030)), 0.062, SIGNAL, segments=12)
    foot.append(shell(squash(cap, a + Vector((0, 0.140, -0.030)), (1.0, 1.0, 0.80)), 83))
    foot.append(metal(small_box("heel plate", a + Vector((0, -0.086, -0.040)), (0.100, 0.022, 0.050), BRASS, bevel=0.007), 84))
    for piece in foot:
        squash(piece, (a.x, 0.0, 0.0), (FOOT_SCALE, FOOT_SCALE, FOOT_SCALE))
    return thigh, shin, foot


def stump():
    """What shows where a piece came off: one unit across, pointing up. The game scales and turns it."""
    o = []
    lump = oa.sphere("flesh", (0, 0, 0.05), 0.95, FLESH, segments=12)
    squash(lump, (0, 0, 0.05), (1.0, 1.0, 0.55))
    oa.crumple(lump, 0.10, grain=2.2, seed=3)
    oa.paint(lump, wear=0.0, dirt=0.8, mottle=1.2, seed=90, grain=3.0, gradient=0.0)
    o.append(lump)
    bone = oa.cylinder("bone", (0, 0, 0.2), (0.06, 0.03, 0.80), 0.24, BONE, radius_end=0.20, segments=10, bevel=0.06)
    oa.paint(bone, wear=0.2, dirt=0.5, mottle=0.4, seed=91, grain=4.0)
    o.append(bone)
    knob = oa.sphere("bone end", (0.065, 0.033, 0.83), 0.27, BONE, segments=8)
    oa.paint(knob, wear=0.2, dirt=0.3, mottle=0.4, seed=92, grain=4.0)
    o.append(knob)
    return o


ORIGINS = {
    "Head": (0, 0, HEAD_Z),
    "Torso": (0, 0, SPINE_Z),
    "RightUpperArm": (SHOULDER_X, 0, SHOULDER_Z),
    "RightForearm": (SHOULDER_X + UPPER_ARM, 0, SHOULDER_Z),
    "RightHand": (SHOULDER_X + UPPER_ARM + FOREARM, 0, SHOULDER_Z),
    "RightThigh": (HIP_X, 0, HIP_Z),
    "RightShin": (HIP_X, 0, KNEE_Z),
    "RightFoot": (HIP_X, 0, ANKLE_Z),
}
for _name in list(ORIGINS):
    if _name.startswith("Right"):
        _x, _y, _z = ORIGINS[_name]
        ORIGINS["Left" + _name[5:]] = (-_x, _y, _z)

ORDER = ["Head", "Torso",
         "LeftUpperArm", "LeftForearm", "LeftHand", "RightUpperArm", "RightForearm", "RightHand",
         "LeftThigh", "LeftShin", "LeftFoot", "RightThigh", "RightShin", "RightFoot"]


def make():
    parts = {"Head": head(), "Torso": torso()}
    upper, fore, hand = right_arm()
    thigh, shin, foot = right_leg()
    parts["RightUpperArm"], parts["RightForearm"], parts["RightHand"] = upper, fore, hand
    parts["RightThigh"], parts["RightShin"], parts["RightFoot"] = thigh, shin, foot
    for name in ("UpperArm", "Forearm", "Hand", "Thigh", "Shin", "Foot"):
        parts["Left" + name] = mirrored(parts["Right" + name])
    # What makes the two sides different: a shoulder guard on the left, a strap and a patch on the right.
    s = Vector((-SHOULDER_X, 0, SHOULDER_Z))
    guard = oa.sphere("shoulder guard", s + Vector((-0.012, 0, 0.022)), 0.104, SHELL, segments=14)
    squash(guard, s + Vector((-0.012, 0, 0.022)), (1.0, 1.0, 0.78))
    parts["LeftUpperArm"].append(shell(guard, 100))
    parts["LeftUpperArm"].append(shell(small_box("guard stripe", s + Vector((-0.020, 0, 0.098)), (0.085, 0.040, 0.016), SIGNAL, bevel=0.006), 101))
    h = Vector((HIP_X, 0, HIP_Z))
    parts["RightThigh"].append(rubber(oa.lathe("thigh strap", h + Vector((0, 0, -0.290)), (0, 0, -1), [(0.0, 0.080), (0.004, 0.096), (0.030, 0.094), (0.034, 0.078)], RUBBER, segments=16), 102))
    k = Vector((-HIP_X, 0, KNEE_Z))
    parts["LeftShin"].append(shell(small_box("shin patch", k + Vector((-0.064, 0.0, -0.17)), (0.022, 0.060, 0.070), SIGNAL, bevel=0.006), 103))
    return parts


def write(parts, stump_objects):
    """Each piece is written measured from its own joint; the picture scene is left as it was."""
    sockets = []
    listed = []
    for name in ORDER:
        origin = Vector(ORIGINS[name])
        for o in parts[name]:
            o.data.transform(Matrix.Translation(-origin))
        listed.append((name, parts[name]))
        sockets.append(oa.socket(name, origin))
    listed.append(("Stump", stump_objects))
    oa.export(os.path.join(OUT, "Enemy.bytes"), listed, sockets)
    for name in ORDER:
        origin = Vector(ORIGINS[name])
        for o in parts[name]:
            o.data.transform(Matrix.Translation(origin))
            o.data.update()


def ground():
    floor = oa.box("floor", (0, 0, -0.005), (60, 60, 0.01), (0.78, 0.71, 0.62), bevel=0)
    oa.paint(floor, wear=0, dirt=0, mottle=0, gradient=0)
    return floor


def game_matrix(values):
    """A 4x4 from the game's space (x right, y up, z forward), given row by row, in Blender's space."""
    m = Matrix((values[0:4], values[4:8], values[8:12], values[12:16]))
    swap = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
    return swap @ m @ swap


def render_poses(parts, stump_objects, pose_file, folder):
    with open(pose_file) as f:
        shots = json.load(f)["shots"]
    floor = ground()
    for o in stump_objects:
        o.hide_render = True
    for shot in shots:
        shown = [floor]
        for name in ORDER:
            m = shot["parts"].get(name)
            for o in parts[name]:
                o.hide_render = m is None
                if m is not None:
                    o.matrix_world = game_matrix(m) @ Matrix.Translation(-Vector(ORIGINS[name]))
                    shown.append(o)
        eye = shot.get("eye", [2.6, 2.6, 1.3])
        target = shot.get("target", [0, 0, 0.9])
        # The dump gives the camera in the game's space too.
        oa.render_preview(os.path.join(folder, shot["name"] + ".png"), shown,
                          (eye[0], eye[2], eye[1]), (target[0], target[2], target[1]),
                          lens=shot.get("lens", 60), size=tuple(shot.get("size", [520, 640])),
                          samples=shot.get("samples", 40))


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    previews = args[args.index("--preview") + 1] if "--preview" in args else None
    poses = args[args.index("--poses") + 1] if "--poses" in args else None
    oa.reset_scene()
    parts = make()
    stump_objects = stump()
    if poses and previews:
        render_poses(parts, stump_objects, poses, previews)
    else:
        write(parts, stump_objects)
        if previews:
            for o in stump_objects:
                o.hide_render = True
            shown = [o for name in ORDER for o in parts[name]] + [ground()]
            oa.render_preview(os.path.join(previews, "enemy_front.png"), shown, (1.1, 3.4, 1.35), (0, 0, 0.95), lens=60, size=(900, 900), samples=64)
            oa.render_preview(os.path.join(previews, "enemy_back.png"), shown, (-1.4, -3.3, 1.5), (0, 0, 0.95), lens=60, size=(900, 900), samples=64)
            oa.render_preview(os.path.join(previews, "enemy_face.png"), shown, (0.45, 1.25, 1.62), (0, 0, 1.50), lens=60, size=(900, 900), samples=64)
