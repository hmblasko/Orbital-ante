"""Builds the three other enemy bodies: the Runner, the Bruiser and the Marksman.

    blender -b -P tools/blender/build_enemy_types.py                       (writes EnemyRunner, EnemyBruiser, EnemyMarksman .bytes)
    blender -b -P tools/blender/build_enemy_types.py -- --preview <folder> (also renders each one, and a line-up of all four)
    blender -b -P tools/blender/build_enemy_types.py -- --poses <file.json> --preview <folder> --type Runner
                                                                           (renders the poses a C# pose dump describes)

Each is modelled on the same joints as the ordinary enemy (tools/blender/build_enemy.py, a copy of BodySkeleton.cs)
and at the same size, in the same fourteen breakable pieces plus the wound stump. The game makes the whole
figure bigger or smaller (Runner 0.85, Bruiser 1.3, Marksman 1.2), so what is drawn here is the build, not the
height: thin or thick limbs, the head, the gear. They have to be told apart by outline alone, at a glance.

The names are placeholders and belong to no faction.
"""
import bpy, sys, os, math, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import oa_blender as oa
import build_enemy as be
from build_enemy import (cloth, shell, metal, rubber, glass, small_box, squash, tilt, mirrored, bellows,
                         SPINE_Z, NECK_Z, HEAD_Z, SHOULDER_X, SHOULDER_Z, UPPER_ARM, FOREARM, HIP_X, HIP_Z, KNEE_Z, ANKLE_Z,
                         BRASS, RUBBER, ORDER)
from mathutils import Vector, Matrix

# How much bigger or smaller the game draws each one (EnemyRules.cs). Only used here for the line-up picture.
GAME_SCALE = {"Enemy": 1.0, "EnemyRunner": 0.85, "EnemyBruiser": 1.3, "EnemyMarksman": 1.2}


class Look:
    """One body's colours, as seen on screen."""
    def __init__(self, suit, suit_dark, plate, signal, glove, visor, lens, pack):
        self.suit, self.suit_dark, self.plate, self.signal = suit, suit_dark, plate, signal
        self.glove, self.visor, self.lens, self.pack = glove, visor, lens, pack


RUNNER = Look(suit=(0.20, 0.24, 0.33), suit_dark=(0.13, 0.16, 0.23), plate=(0.86, 0.84, 0.76), signal=(0.93, 0.72, 0.14),
              glove=(0.93, 0.72, 0.14), visor=(0.06, 0.10, 0.14), lens=(0.55, 0.95, 0.90), pack=(0.33, 0.36, 0.42))
BRUISER = Look(suit=(0.30, 0.27, 0.25), suit_dark=(0.20, 0.18, 0.17), plate=(0.72, 0.19, 0.15), signal=(0.90, 0.80, 0.62),
               glove=(0.22, 0.21, 0.22), visor=(0.10, 0.03, 0.02), lens=(1.00, 0.62, 0.25), pack=(0.36, 0.33, 0.30))
MARKSMAN = Look(suit=(0.34, 0.38, 0.44), suit_dark=(0.22, 0.26, 0.32), plate=(0.88, 0.89, 0.86), signal=(0.24, 0.50, 0.82),
                glove=(0.16, 0.18, 0.22), visor=(0.03, 0.06, 0.12), lens=(0.60, 0.86, 1.00), pack=(0.40, 0.44, 0.50))


# ------------------------------------------------------------------ limbs every build shares

def arm(L, upper=1.0, fore=1.0, hand=1.0, band=True):
    """The right arm in three pieces, thinner or thicker than the ordinary enemy's by the numbers given."""
    s = Vector((SHOULDER_X, 0, SHOULDER_Z))
    e = s + Vector((UPPER_ARM, 0, 0))
    w = e + Vector((FOREARM, 0, 0))
    U, F, H = [], [], []
    U.append(cloth(oa.sphere("shoulder", s, min(0.118, 0.090 * upper), L.suit, segments=14), 50))
    U.append(cloth(oa.lathe("sleeve", s, (1, 0, 0), [(0.0, 0.055 * upper), (0.030, 0.084 * upper), (0.11, 0.080 * upper), (0.20, 0.069 * upper),
                                                     (0.262, 0.061 * upper), (0.284, 0.046 * upper)], L.suit, segments=16), 51))
    if band:
        U.append(shell(oa.lathe("arm band", s + Vector((0.105, 0, 0)), (1, 0, 0), [(0.0, 0.070 * upper), (0.004, 0.086 * upper), (0.040, 0.084 * upper),
                                                                                  (0.044, 0.070 * upper)], L.signal, segments=16), 52))
    j = max(upper, fore * 0.86)
    F.append(rubber(oa.sphere("elbow", e, 0.062 * j, RUBBER, segments=12), 53))
    F.append(bellows("elbow bellows", e + Vector((0.004, 0, 0)), (1, 0, 0), 0.070, 0.062 * j, 3, RUBBER, 54))
    F.append(shell(oa.lathe("forearm", e + Vector((0.070, 0, 0)), (1, 0, 0), [(0.0, 0.050 * fore), (0.008, 0.060 * fore), (0.060, 0.066 * fore),
                                                                           (0.120, 0.070 * fore), (0.130, 0.050 * fore)], L.plate, segments=16), 55))
    F.append(metal(oa.lathe("cuff", e + Vector((0.186, 0, 0)), (1, 0, 0), [(0.0, 0.050 * fore), (0.005, 0.069 * fore), (0.050, 0.069 * fore), (0.058, 0.060 * fore),
                                                                        (0.074, 0.058 * fore), (0.078, 0.040 * fore)], BRASS, segments=16), 56))
    H.append(rubber(oa.sphere("wrist", w, 0.046, RUBBER, segments=10), 57))
    H.append(rubber(small_box("palm", w + Vector((0.058, 0, 0)), (0.094, 0.090, 0.052), L.glove, bevel=0.020, segments=3), 58))
    H.append(rubber(small_box("fingers", w + Vector((0.124, -0.004, -0.012)), (0.066, 0.084, 0.044), L.glove, bevel=0.019, segments=3, rot=(0, 22, 0)), 59))
    H.append(rubber(oa.cylinder("thumb", w + Vector((0.040, 0.036, -0.006)), w + Vector((0.096, 0.072, -0.020)), 0.019, L.glove, radius_end=0.016, segments=10, bevel=0.007), 60))
    H.append(shell(small_box("knuckle pad", w + Vector((0.066, -0.002, 0.027)), (0.062, 0.070, 0.014), L.signal, bevel=0.005), 61))
    k = be.HAND_SCALE * hand
    for piece in H:
        squash(piece, w, (k, k, k))
    return U, F, H


def leg(L, thigh=1.0, shin=1.0, foot=1.0, guard=True, pocket=True):
    """The right leg in three pieces. The boot's sole stays on the ground whatever its size."""
    h = Vector((HIP_X, 0, HIP_Z))
    k = Vector((HIP_X, 0, KNEE_Z))
    a = Vector((HIP_X, 0, ANKLE_Z))
    T, S, Ft = [], [], []
    down = (0, 0, -1)
    T.append(cloth(oa.sphere("hip", h, min(0.120, 0.094 * thigh), L.suit, segments=14), 70))
    T.append(cloth(oa.lathe("thigh", h, down, [(0.0, 0.060 * thigh), (0.030, 0.104 * thigh), (0.14, 0.106 * thigh), (0.28, 0.090 * thigh),
                                                (0.385, 0.076 * thigh), (0.424, 0.058 * thigh)], L.suit, segments=16), 71))
    if pocket:
        T.append(cloth(small_box("thigh pocket", h + Vector((0.088 * thigh, 0.004, -0.205)), (0.040, 0.094, 0.118), L.suit_dark, bevel=0.013), 72))
    j = max(thigh * 0.9, shin)
    S.append(rubber(oa.sphere("knee", k, 0.072 * j, RUBBER, segments=12), 74))
    S.append(bellows("knee bellows", k + Vector((0, 0, -0.004)), down, 0.066, 0.073 * j, 3, RUBBER, 75))
    S.append(shell(small_box("knee pad", k + Vector((0, 0.062 * shin, -0.004)), (0.112 * shin, 0.050, 0.124), L.plate, bevel=0.022, segments=3), 76))
    S.append(cloth(oa.lathe("shin", k + Vector((0, 0, -0.066)), down, [(0.0, 0.058 * shin), (0.010, 0.071 * shin), (0.12, 0.073 * shin), (0.235, 0.066 * shin),
                                                                     (0.245, 0.05 * shin)], L.suit, segments=16), 77))
    if guard:
        S.append(shell(small_box("shin guard", k + Vector((0, 0.058 * shin, -0.185)), (0.104 * shin, 0.050, 0.200), L.plate, bevel=0.020), 78))
    S.append(metal(oa.lathe("boot cuff", k + Vector((0, 0, -0.300)), down, [(0.0, 0.056 * shin), (0.006, 0.083 * shin), (0.058, 0.083 * shin), (0.068, 0.070 * shin),
                                                                          (0.090, 0.066 * shin), (0.096, 0.045 * shin)], BRASS, segments=16), 79))
    Ft.append(rubber(oa.sphere("ankle", a, 0.066, RUBBER, segments=10), 80))
    upper = oa.sphere("boot", a + Vector((0, 0.046, -0.022)), 0.072, RUBBER, segments=14)
    Ft.append(rubber(squash(upper, a + Vector((0, 0.046, -0.022)), (0.95, 1.85, 0.84)), 81))
    Ft.append(rubber(small_box("sole", a + Vector((0, 0.050, -0.069)), (0.134, 0.300, 0.042), (0.07, 0.07, 0.09), bevel=0.016, segments=2), 82))
    cap = oa.sphere("toe cap", a + Vector((0, 0.140, -0.030)), 0.062, L.signal, segments=12)
    Ft.append(shell(squash(cap, a + Vector((0, 0.140, -0.030)), (1.0, 1.0, 0.80)), 83))
    Ft.append(metal(small_box("heel plate", a + Vector((0, -0.086, -0.040)), (0.100, 0.022, 0.050), BRASS, bevel=0.007), 84))
    f = be.FOOT_SCALE * foot
    for piece in Ft:
        # About the point on the ground under the ankle: wider and longer, and no taller than the ankle allows.
        squash(piece, (a.x, 0.0, 0.0), (f, f, min(f, 1.22)))
    return T, S, Ft


def body(L, wide, deep, seed=20, quilted=True):
    """The cloth trunk, pelvis to neck: 'wide' and 'deep' are against the ordinary enemy's."""
    prof = [(0.845, 0.03), (0.862, 0.105), (0.905, 0.152), (0.955, 0.170), (0.992, 0.172)]
    z = 1.04
    while z < 1.19:
        prof.append((z, 0.166 + (z - 1.04) * 0.10))
        prof.append((z + 0.022, (0.174 if quilted else 0.168) + (z - 1.04) * 0.10))
        z += 0.044
    prof += [(1.22, 0.188), (1.30, 0.196), (1.40, 0.190), (1.46, 0.150), (1.49, 0.10), (1.50, 0.06)]
    return cloth(squash(oa.lathe("body", (0, 0, 0), (0, 0, 1), prof, L.suit, segments=20), (0, 0, 0), (wide, 0.80 * deep, 1.0)), seed)


def both_sides(parts, right_arm, right_leg):
    upper, fore, hand = right_arm
    thigh, shin, foot = right_leg
    parts["RightUpperArm"], parts["RightForearm"], parts["RightHand"] = upper, fore, hand
    parts["RightThigh"], parts["RightShin"], parts["RightFoot"] = thigh, shin, foot
    for name in ("UpperArm", "Forearm", "Hand", "Thigh", "Shin", "Foot"):
        parts["Left" + name] = mirrored(parts["Right" + name])
    return parts


# ------------------------------------------------------------------ the Runner: small, slim, swept back

def runner():
    L = RUNNER
    c = Vector((0, 0, HEAD_Z - 0.012))
    H = []
    # A bullet of a helmet, long front to back, with a fin that sweeps to a point behind.
    H.append(shell(squash(oa.sphere("helmet", c + Vector((0, -0.018, 0)), 0.142, L.plate, segments=24), c, (0.93, 1.24, 0.94)), 1))
    fin = [(0.08, HEAD_Z + 0.118), (-0.01, HEAD_Z + 0.164), (-0.17, HEAD_Z + 0.196), (-0.40, HEAD_Z + 0.158), (-0.23, HEAD_Z + 0.094), (-0.10, HEAD_Z + 0.060), (0.0, HEAD_Z + 0.075)]
    H.append(shell(oa.slab("fin", fin, 0.034, L.signal, bevel=0.008, segments=2), 2))
    # One big round goggle: a brass rim and a pale green glass. No mouth, no nose: an insect's stare.
    gc = c + Vector((0, 0.128, 0.008))
    H.append(metal(oa.lathe("goggle rim", gc, (0, 1, 0), [(-0.020, 0.074), (0.010, 0.092), (0.030, 0.092), (0.038, 0.078)], BRASS, segments=20), 3))
    H.append(glass(squash(oa.sphere("goggle", gc + Vector((0, 0.022, 0)), 0.080, L.lens, segments=16), gc + Vector((0, 0.022, 0)), (1.0, 0.42, 1.0)), 4))
    H.append(glass(squash(oa.sphere("goggle pupil", gc + Vector((0.014, 0.050, 0.016)), 0.030, L.visor, segments=10), gc + Vector((0.014, 0.050, 0.016)), (1.0, 0.35, 1.0)), 5))
    # A filter can under the chin, lying across.
    H.append(rubber(oa.cylinder("filter", c + Vector((-0.070, 0.105, -0.108)), c + Vector((0.070, 0.105, -0.108)), 0.034, RUBBER, segments=12, bevel=0.008), 6))
    for sx in (-1, 1):
        H.append(metal(oa.cylinder("filter cap", c + Vector((sx * 0.066, 0.105, -0.108)), c + Vector((sx * 0.080, 0.105, -0.108)), 0.027, L.signal, segments=12, bevel=0.003), 7))
        H.append(metal(oa.cylinder("ear", c + Vector((sx * 0.120, -0.01, -0.01)), c + Vector((sx * 0.150, -0.01, -0.01)), 0.034, BRASS, radius_end=0.027, segments=12, bevel=0.006), 8))
    H.append(metal(oa.lathe("neck ring", (0, 0, NECK_Z + 0.012), (0, 0, 1), [(0.0, 0.060), (0.006, 0.084), (0.030, 0.084), (0.040, 0.072)], BRASS, segments=18), 9))

    T = []
    T.append(body(L, 0.80, 0.84, seed=20, quilted=False))
    # A short vest, not a shell: the shoulders stay narrow.
    vest = [(1.235, 0.150), (1.242, 0.176), (1.30, 0.182), (1.39, 0.176), (1.45, 0.146), (1.487, 0.104), (1.506, 0.086), (1.510, 0.06)]
    T.append(shell(squash(oa.lathe("vest", (0, 0, 0), (0, 0, 1), vest, L.plate, segments=20), (0, 0, 0), (0.86, 0.72, 1.0)), 21))
    T.append(metal(oa.lathe("collar", (0, 0, 1.492), (0, 0, 1), [(0.0, 0.068), (0.004, 0.090), (0.020, 0.090), (0.026, 0.076)], BRASS, segments=18), 22))
    T.append(rubber(squash(oa.lathe("belt", (0, 0, 0.985), (0, 0, 1), [(0.0, 0.165), (0.006, 0.180), (0.040, 0.180), (0.046, 0.165)], RUBBER, segments=20), (0, 0, 0), (0.80, 0.68, 1.0)), 23))
    T.append(metal(small_box("buckle", (0, 0.118, 1.008), (0.050, 0.018, 0.040), L.signal, bevel=0.006), 24))
    # A sash of magazines from one shoulder to the other hip, and a stripe to match.
    for i in range(5):
        t = i / 4.0
        x, z = -0.112 + 0.215 * t, 1.385 - 0.300 * t
        y = 0.118 + 0.022 * math.sin(t * math.pi)
        T.append(metal(small_box("sash pouch", (x, y, z), (0.050, 0.030, 0.066), L.signal if i % 2 == 0 else L.suit_dark, bevel=0.008, rot=(0, -35, 0)), 25 + i))
    for sx in (-1, 1):
        T.append(metal(oa.lathe("shoulder ring", (sx * 0.160, 0, SHOULDER_Z), (sx, 0, 0), [(0.0, 0.056), (0.006, 0.074), (0.050, 0.074), (0.060, 0.062)], BRASS, segments=16), 30))
        # Two slim bottles low on the back. No jets under them: it runs fast, it does not fly, and a body should
        # not promise what its owner cannot do.
        T.append(shell(oa.cylinder("bottle", (sx * 0.062, -0.150, 1.10), (sx * 0.062, -0.150, 1.40), 0.044, L.signal, segments=14, bevel=0.020), 31))
        T.append(metal(oa.cylinder("bottle valve", (sx * 0.062, -0.150, 1.398), (sx * 0.062, -0.150, 1.425), 0.014, BRASS, segments=10, bevel=0.003), 32))
        T.append(metal(oa.cylinder("bottle foot", (sx * 0.062, -0.150, 1.085), (sx * 0.062, -0.150, 1.108), 0.030, BRASS, segments=12, bevel=0.005), 33))
    T.append(shell(small_box("bottle frame", (0, -0.128, 1.26), (0.200, 0.040, 0.210), L.pack, bevel=0.014), 34))

    parts = {"Head": H, "Torso": T}
    both_sides(parts, arm(L, upper=0.76, fore=0.80, hand=0.88), leg(L, thigh=0.80, shin=0.78, foot=0.88, pocket=False))
    # Elbow and knee spikes swept back, like the fin: the whole figure leans the way it runs.
    for sx, side in ((1, "Right"), (-1, "Left")):
        e = Vector((sx * (SHOULDER_X + UPPER_ARM), 0, SHOULDER_Z))
        spike = oa.cylinder("elbow spike", e + Vector((0, -0.030, 0)), e + Vector((0, -0.120, 0.012)), 0.026, L.signal, radius_end=0.006, segments=10, bevel=0.003)
        parts[side + "Forearm"].append(shell(spike, 110))
        k = Vector((sx * HIP_X, 0, KNEE_Z))
        heel = oa.cylinder("heel spur", (sx * HIP_X, -0.105, 0.085), (sx * HIP_X, -0.190, 0.135), 0.022, L.signal, radius_end=0.006, segments=10, bevel=0.003)
        parts[side + "Foot"].append(shell(heel, 111))
    return parts


# ------------------------------------------------------------------ the Bruiser: a barrel on legs

def bruiser():
    L = BRUISER
    c = Vector((0, 0, HEAD_Z - 0.050))
    H = []
    # A small, low dome sunk into the collar, with a slit to see through and a jaw like a bucket.
    H.append(shell(squash(oa.sphere("helmet", c, 0.132, L.plate, segments=22), c, (1.12, 1.02, 0.84)), 1))
    # The slit is the brightest thing on the whole figure: the head is the only place a shot ends it, and the
    # eye should go there, not to the chest.
    H.append(glass(small_box("slit", c + Vector((0, 0.124, 0.010)), (0.186, 0.050, 0.044), L.lens, bevel=0.012), 2))
    H.append(rubber(small_box("slit shade", c + Vector((0, 0.104, 0.046)), (0.206, 0.076, 0.022), L.visor, bevel=0.009), 3))
    H.append(shell(small_box("jaw", c + Vector((0, 0.080, -0.062)), (0.200, 0.130, 0.086), L.suit, bevel=0.026, segments=3), 4))
    for i in range(4):
        H.append(rubber(small_box("jaw vent", c + Vector((-0.054 + 0.036 * i, 0.146, -0.062)), (0.014, 0.010, 0.052), RUBBER, bevel=0.003), 5))
    for sx in (-1, 1):
        H.append(metal(oa.cylinder("bolt", c + Vector((sx * 0.132, 0.0, 0.0)), c + Vector((sx * 0.176, 0.0, 0.0)), 0.036, BRASS, radius_end=0.030, segments=12, bevel=0.007), 6))
    H.append(metal(oa.lathe("neck ring", (0, 0, NECK_Z + 0.004), (0, 0, 1), [(0.0, 0.080), (0.006, 0.108), (0.030, 0.108), (0.040, 0.094)], BRASS, segments=20), 7))

    T = []
    # A barrel: deepest at the belly, and still narrow enough at the top for the shoulders to stand clear.
    barrel = [(0.845, 0.04), (0.862, 0.130), (0.905, 0.190), (0.96, 0.225), (1.04, 0.250), (1.13, 0.262), (1.22, 0.262), (1.30, 0.250),
              (1.37, 0.228), (1.43, 0.190), (1.47, 0.150), (1.50, 0.11), (1.51, 0.06)]
    T.append(cloth(squash(oa.lathe("body", (0, 0, 0), (0, 0, 1), barrel, L.suit, segments=22), (0, 0, 0), (0.94, 1.0, 1.0)), 20))
    # Red armour over the chest, with one great round port in the middle of it. The port is dark, a blind
    # window: nothing on the chest glows, because shooting the chest is the slow way.
    plate = [(1.150, 0.255), (1.158, 0.285), (1.24, 0.290), (1.33, 0.276), (1.40, 0.244), (1.455, 0.196), (1.495, 0.150), (1.516, 0.128), (1.520, 0.09)]
    T.append(shell(squash(oa.lathe("chest armour", (0, 0, 0), (0, 0, 1), plate, L.plate, segments=22), (0, 0, 0), (0.94, 1.0, 1.0)), 21))
    pc = Vector((0, 0.262, 1.290))
    T.append(metal(oa.lathe("port rim", pc, (0, 1, 0), [(-0.030, 0.090), (0.012, 0.104), (0.034, 0.104), (0.042, 0.088)], BRASS, segments=20), 22))
    T.append(glass(squash(oa.sphere("port", pc + Vector((0, 0.026, 0)), 0.090, L.visor, segments=16), pc + Vector((0, 0.026, 0)), (1.0, 0.36, 1.0)), 23))
    for i in range(6):
        a = math.radians(60 * i + 30)
        T.append(metal(oa.sphere("port bolt", pc + Vector((math.cos(a) * 0.096, 0.040, math.sin(a) * 0.096)), 0.012, L.signal, segments=8), 24))
    # A collar the head sits down into.
    T.append(shell(oa.lathe("gorget", (0, 0, 1.475), (0, 0, 1), [(0.0, 0.150), (0.010, 0.172), (0.060, 0.166), (0.094, 0.146), (0.098, 0.128), (0.060, 0.120)], L.plate, segments=22), 25))
    T.append(rubber(squash(oa.lathe("belt", (0, 0, 0.975), (0, 0, 1), [(0.0, 0.232), (0.008, 0.252), (0.078, 0.258), (0.086, 0.240)], RUBBER, segments=22), (0, 0, 0), (0.94, 1.0, 1.0)), 26))
    T.append(metal(small_box("buckle", (0, 0.250, 1.018), (0.120, 0.026, 0.084), BRASS, bevel=0.012), 27))
    T.append(shell(small_box("buckle boss", (0, 0.262, 1.018), (0.060, 0.018, 0.040), L.plate, bevel=0.008), 28))
    for sx in (-1, 1):
        T.append(metal(oa.lathe("shoulder ring", (sx * 0.176, 0, SHOULDER_Z), (sx, 0, 0), [(0.0, 0.086), (0.006, 0.112), (0.050, 0.112), (0.062, 0.096)], BRASS, segments=18), 29))
        # Two stacks up the back, like a tractor.
        T.append(metal(oa.cylinder("stack", (sx * 0.130, -0.262, 1.30), (sx * 0.130, -0.288, 1.558), 0.030, RUBBER, segments=12, bevel=0.004), 30))
        T.append(metal(oa.lathe("stack cap", (sx * 0.130, -0.288, 1.548), (0, -0.09, 1), [(0.0, 0.030), (0.006, 0.042), (0.030, 0.042), (0.034, 0.028)], BRASS, segments=12), 31))
    # One fat tank across the back.
    T.append(shell(oa.cylinder("tank", (-0.215, -0.262, 1.20), (0.215, -0.262, 1.20), 0.118, L.pack, segments=18, bevel=0.050), 32))
    for sx in (-1, 1):
        T.append(metal(oa.lathe("tank band", (sx * 0.110, -0.262, 1.20), (1, 0, 0), [(-0.016, 0.110), (-0.012, 0.124), (0.012, 0.124), (0.016, 0.110)], BRASS, segments=18), 33))
    T.append(shell(small_box("tank mount", (0, -0.200, 1.24), (0.300, 0.110, 0.200), L.suit_dark, bevel=0.020), 34))

    parts = {"Head": H, "Torso": T}
    both_sides(parts, arm(L, upper=1.26, fore=1.62, hand=1.42, band=False), leg(L, thigh=1.26, shin=1.34, foot=1.26, pocket=False))
    for sx, side in ((1, "Right"), (-1, "Left")):
        # A pauldron over each shoulder, red, with a brass rim: the widest thing on it.
        s = Vector((sx * SHOULDER_X, 0, SHOULDER_Z))
        pc2 = s + Vector((sx * 0.046, 0, 0.050))
        cap = oa.sphere("pauldron", pc2, 0.160, L.plate, segments=18)
        squash(cap, pc2, (1.0, 1.06, 0.74))
        parts[side + "UpperArm"].append(shell(cap, 120))
        parts[side + "UpperArm"].append(metal(oa.lathe("pauldron rim", pc2 + Vector((0, 0, -0.064)), (0, 0, 1), [(0.0, 0.142), (0.004, 0.160), (0.026, 0.164), (0.030, 0.148)], BRASS, segments=18), 121))
        for i in range(3):
            a = math.radians(-50 + 50 * i)
            parts[side + "UpperArm"].append(metal(oa.sphere("pauldron stud", pc2 + Vector((sx * math.cos(a) * 0.100, math.sin(a) * 0.105, 0.084)), 0.017, L.signal, segments=8), 122))
        # A band of armour round each great forearm, and knuckle bars on the fists.
        e = Vector((sx * (SHOULDER_X + UPPER_ARM), 0, SHOULDER_Z))
        parts[side + "Forearm"].append(shell(oa.lathe("gauntlet", e + Vector((sx * 0.090, 0, 0)), (sx, 0, 0), [(0.0, 0.106), (0.006, 0.124), (0.086, 0.130), (0.092, 0.112)], L.plate, segments=18), 123))
        w = e + Vector((sx * FOREARM, 0, 0))
        parts[side + "Hand"].append(metal(small_box("knuckle bar", w + Vector((sx * 0.176, -0.002, 0.012)), (0.036, 0.150, 0.060), BRASS, bevel=0.012), 124))
        k = Vector((sx * HIP_X, 0, KNEE_Z))
        parts[side + "Shin"].append(shell(small_box("knee plate", k + Vector((0, 0.098, 0.010)), (0.150, 0.040, 0.150), L.plate, bevel=0.022, segments=2), 125))
    return parts


# ------------------------------------------------------------------ the Marksman: tall, thin, one long eye

def marksman():
    L = MARKSMAN
    c = Vector((0, 0, HEAD_Z + 0.010))
    H = []
    # A tall, narrow helmet.
    H.append(shell(squash(oa.sphere("helmet", c, 0.150, L.plate, segments=24), c, (0.90, 0.98, 1.22)), 1))
    H.append(shell(oa.slab("brow band", [(0.150, HEAD_Z + 0.050), (0.150, HEAD_Z + 0.082), (-0.150, HEAD_Z + 0.082), (-0.150, HEAD_Z + 0.050)], 0.276, L.signal, bevel=0.010, segments=2), 2))
    # One long glass over the right eye, and a little dark port for the left.
    eye = c + Vector((0.052, 0.090, 0.010))
    H.append(metal(oa.cylinder("long eye", eye, eye + Vector((0, 0.190, 0)), 0.040, L.suit_dark, radius_end=0.046, segments=16, bevel=0.006), 3))
    H.append(metal(oa.lathe("long eye rim", eye + Vector((0, 0.176, 0)), (0, 1, 0), [(0.0, 0.046), (0.004, 0.056), (0.022, 0.056), (0.026, 0.044)], BRASS, segments=16), 4))
    H.append(glass(squash(oa.sphere("long eye glass", eye + Vector((0, 0.196, 0)), 0.042, L.lens, segments=12), eye + Vector((0, 0.196, 0)), (1.0, 0.30, 1.0)), 5))
    H.append(metal(oa.lathe("long eye ring", eye + Vector((0, 0.080, 0)), (0, 1, 0), [(0.0, 0.040), (0.004, 0.049), (0.020, 0.049), (0.024, 0.040)], BRASS, segments=16), 6))
    H.append(glass(oa.cylinder("left port", c + Vector((-0.056, 0.118, 0.012)), c + Vector((-0.060, 0.142, 0.012)), 0.030, L.visor, segments=12, bevel=0.004), 7))
    H.append(metal(oa.lathe("left port rim", c + Vector((-0.056, 0.124, 0.012)), (-0.16, 1, 0), [(0.0, 0.030), (0.004, 0.039), (0.014, 0.039), (0.018, 0.030)], BRASS, segments=12), 8))
    # A slim breather, pointing down.
    H.append(rubber(oa.cylinder("breather", c + Vector((0, 0.108, -0.085)), c + Vector((0, 0.130, -0.170)), 0.032, RUBBER, radius_end=0.026, segments=12, bevel=0.006), 9))
    # A rangefinder box on the left ear, and from it an aerial as tall again as the head, with a blue tag.
    H.append(shell(small_box("rangefinder", c + Vector((-0.150, 0.010, 0.020)), (0.050, 0.110, 0.070), L.suit_dark, bevel=0.012), 10))
    H.append(metal(oa.cylinder("aerial", c + Vector((-0.152, -0.020, 0.050)), c + Vector((-0.166, -0.050, 0.470)), 0.012, RUBBER, radius_end=0.008, segments=8), 11))
    H.append(shell(oa.sphere("aerial tip", c + Vector((-0.166, -0.050, 0.474)), 0.022, L.signal, segments=10), 12))
    tag = oa.slab("aerial tag", [(-0.054, HEAD_Z + 0.440), (-0.054, HEAD_Z + 0.360), (-0.220, HEAD_Z + 0.378), (-0.165, HEAD_Z + 0.400), (-0.220, HEAD_Z + 0.424)], 0.012, L.signal, bevel=0.0015, segments=1, x=-0.166)
    H.append(cloth(tag, 13))
    H.append(metal(oa.cylinder("ear", c + Vector((0.134, -0.005, -0.005)), c + Vector((0.166, -0.005, -0.005)), 0.034, BRASS, radius_end=0.028, segments=12, bevel=0.006), 14))
    H.append(metal(oa.lathe("neck ring", (0, 0, NECK_Z + 0.012), (0, 0, 1), [(0.0, 0.062), (0.006, 0.086), (0.034, 0.086), (0.046, 0.074)], BRASS, segments=18), 15))

    T = []
    T.append(body(L, 0.86, 0.90, seed=20))
    plate = [(1.255, 0.160), (1.262, 0.188), (1.32, 0.194), (1.39, 0.188), (1.45, 0.156), (1.487, 0.110), (1.508, 0.092), (1.512, 0.066)]
    T.append(shell(squash(oa.lathe("chest plate", (0, 0, 0), (0, 0, 1), plate, L.plate, segments=20), (0, 0, 0), (0.88, 0.76, 1.0)), 21))
    # A tall collar, turned up.
    T.append(cloth(oa.lathe("collar", (0, 0, 1.470), (0, 0, 1), [(0.0, 0.094), (0.010, 0.112), (0.070, 0.104), (0.096, 0.098), (0.098, 0.086), (0.060, 0.090)], L.signal, segments=20), 22))
    T.append(rubber(squash(oa.lathe("belt", (0, 0, 0.985), (0, 0, 1), [(0.0, 0.165), (0.006, 0.182), (0.050, 0.182), (0.056, 0.165)], RUBBER, segments=20), (0, 0, 0), (0.86, 0.72, 1.0)), 23))
    T.append(metal(small_box("buckle", (0, 0.128, 1.013), (0.056, 0.018, 0.044), BRASS, bevel=0.006), 24))
    # A row of long rounds across the chest.
    for i in range(5):
        x = -0.088 + 0.044 * i
        y = 0.142 - 0.040 * abs(i - 2) / 2.0
        T.append(metal(oa.cylinder("round", (x, y, 1.275), (x, y, 1.345), 0.012, BRASS, segments=8, bevel=0.003), 25))
        T.append(shell(oa.cylinder("round tip", (x, y, 1.343), (x, y, 1.372), 0.0105, L.signal, radius_end=0.003, segments=8), 26))
    T.append(rubber(small_box("round loop", (0, 0.134, 1.300), (0.230, 0.022, 0.030), L.suit_dark, bevel=0.006), 27))
    for sx in (-1, 1):
        T.append(metal(oa.lathe("shoulder ring", (sx * 0.170, 0, SHOULDER_Z), (sx, 0, 0), [(0.0, 0.060), (0.006, 0.080), (0.046, 0.080), (0.056, 0.066)], BRASS, segments=16), 28))
        T.append(cloth(tilt(small_box("pouch", (sx * 0.112, 0.082, 0.975), (0.056, 0.046, 0.078), L.suit_dark, bevel=0.012), (sx * 0.112, 0.082, 0.975), 'Z', -sx * 38), 29))
    # A short cape down the back that fans out wider than the arms hang, so that from the front the figure is
    # a tall A with a stalk on top: a shape nobody else has, and one that still reads when the long eye, the
    # spare barrel and everything else on it are too small to see.
    # Seven pleats fanned from the roll at the shoulders, on three levels so no two that overlap lie in one
    # plane, every other one darker, so it reads as cloth and not as a board.
    shade = tuple(v * 0.80 for v in L.signal)
    for i in range(7):
        x = -0.186 + 0.062 * i
        proud = 0.012 * (i % 3)
        hem = 0.905 + 0.030 * (i % 2)
        fold = [(-0.118 - proud, 1.475), (-0.150 - proud, 1.36), (-0.188 - proud, 1.18), (-0.248 - proud, hem), (-0.270 - proud, hem + 0.012),
                (-0.214 - proud, 1.19), (-0.176 - proud, 1.37), (-0.142 - proud, 1.482)]
        pleat = oa.slab("cape pleat", fold, 0.150, shade if i % 2 else L.signal, bevel=0.010, segments=2, x=x)
        T.append(cloth(tilt(pleat, (x, -0.130, 1.468), 'Y', (3 - i) * 6.5), 30))
    T.append(cloth(oa.cylinder("cape roll", (-0.205, -0.130, 1.468), (0.205, -0.130, 1.468), 0.030, L.signal, segments=12, bevel=0.012), 31))
    # A spare barrel in a tube, slung across the back and standing up over one shoulder.
    T.append(metal(oa.cylinder("barrel tube", (-0.090, -0.215, 1.02), (0.120, -0.205, 1.560), 0.030, L.pack, segments=12, bevel=0.008), 32))
    T.append(metal(oa.cylinder("barrel tube cap", (0.118, -0.205, 1.552), (0.124, -0.205, 1.580), 0.034, BRASS, segments=12, bevel=0.004), 33))

    parts = {"Head": H, "Torso": T}
    both_sides(parts, arm(L, upper=0.84, fore=0.84, hand=0.94), leg(L, thigh=0.86, shin=0.84, foot=0.94, guard=False))
    for sx, side in ((1, "Right"), (-1, "Left")):
        # Tall boots to the knee.
        k = Vector((sx * HIP_X, 0, KNEE_Z))
        parts[side + "Shin"].append(rubber(oa.lathe("tall boot", k + Vector((0, 0, -0.120)), (0, 0, -1), [(0.0, 0.060), (0.008, 0.072), (0.110, 0.068), (0.190, 0.064)], L.suit_dark, segments=16), 130))
    return parts


BUILDS = (("EnemyRunner", runner), ("EnemyBruiser", bruiser), ("EnemyMarksman", marksman))


def all_objects(parts):
    return [o for name in ORDER for o in parts[name]]


def report(name, parts):
    """Prints how thick each piece came out, for fitting hitboxes: half extents across the bone, and along it."""
    for piece in ("Head", "Torso", "RightUpperArm", "RightForearm", "RightHand", "RightThigh", "RightShin", "RightFoot"):
        lo, hi = Vector((1e9, 1e9, 1e9)), Vector((-1e9, -1e9, -1e9))
        for o in parts[piece]:
            for v in o.data.vertices:
                p = v.co
                lo = Vector((min(lo.x, p.x), min(lo.y, p.y), min(lo.z, p.z)))
                hi = Vector((max(hi.x, p.x), max(hi.y, p.y), max(hi.z, p.z)))
        print("OA fit %s %s x %.3f..%.3f y %.3f..%.3f z %.3f..%.3f" % (name, piece, lo.x, hi.x, lo.y, hi.y, lo.z, hi.z))


def line_up(folder):
    """All four standing side by side at the sizes the game draws them, in colour and as plain black shapes."""
    oa.reset_scene()
    shown = []
    makers = (("Enemy", be.make),) + BUILDS
    order = ("EnemyRunner", "Enemy", "EnemyMarksman", "EnemyBruiser")
    at = {"EnemyRunner": -2.15, "Enemy": -0.75, "EnemyMarksman": 0.75, "EnemyBruiser": 2.35}
    for name, maker in makers:
        parts = maker()
        k = GAME_SCALE[name]
        m = Matrix.Translation(Vector((at[name], 0, 0))) @ Matrix.Diagonal((k, k, k, 1.0))
        for o in all_objects(parts):
            o.data.transform(m)
            o.data.update()
            shown.append(o)
    floor = be.ground()
    oa.render_preview(os.path.join(folder, "enemy_types.png"), shown + [floor], (0.8, 12.5, 1.7), (0.1, 0, 1.22), lens=62, size=(1500, 900), samples=64)
    oa.render_preview(os.path.join(folder, "enemy_types_back.png"), shown + [floor], (-0.8, -12.5, 1.9), (0.1, 0, 1.22), lens=62, size=(1500, 900), samples=48)
    # Plain black on white: can they be told apart by outline alone?
    floor.hide_render = True
    black_shapes(os.path.join(folder, "enemy_types_outline.png"), shown, (0.1, 16.0, 1.25), (0.1, 0, 1.25), lens=80, size=(1500, 700))


def posed_line_up(pose_file, folder):
    """All four in the poses the game's animator gives them, each carrying itself its own way.

    The file comes from tools/blender/pose_dump (mode "kinds").
    """
    with open(pose_file) as f:
        kinds = json.load(f)["kinds"]
    makers = dict((("Enemy", be.make),) + BUILDS)
    for state, gap, eye, target, lens in (("idle", 1.75, (2.4, 12.6, 1.9), (0.25, 0, 1.18), 62), ("ready", 1.75, (2.4, 12.6, 1.9), (0.25, 0, 1.18), 62),
                                          ("walk", 2.0, (11.5, 3.6, 1.7), (0.0, 0.2, 1.1), 62), ("run", 2.3, (12.5, 4.2, 1.7), (0.0, 0.4, 1.1), 62)):
        oa.reset_scene()
        shown = []
        for i, kind in enumerate(kinds):
            parts = makers[kind["model"]]()
            k = kind["scale"]
            # Idle and ready stand in a row across the picture; walking and running they go one behind another
            # across it, seen from the side, so every stride can be read.
            offset = (i - (len(kinds) - 1) / 2.0) * gap
            across = state in ("idle", "ready")
            place = Matrix.Translation(Vector((offset, 0, 0) if across else (0, -offset, 0))) @ Matrix.Diagonal((k, k, k, 1.0))
            for name in be.ORDER:
                m = kind[state].get(name)
                for o in parts[name]:
                    o.data.transform(place @ be.game_matrix(m) @ Matrix.Translation(-Vector(be.ORIGINS[name])))
                    o.data.update()
                    shown.append(o)
        floor = be.ground()
        oa.render_preview(os.path.join(folder, "enemy_kinds_" + state + ".png"), shown + [floor], eye, target, lens=lens, size=(1500, 820), samples=56)
        if state == "idle":
            floor.hide_render = True
            black_shapes(os.path.join(folder, "enemy_kinds_outline.png"), shown, (0.0, 16.0, 1.25), (0.0, 0, 1.25), lens=80, size=(1500, 700))


def black_shapes(path, objects, eye, target, lens, size):
    """The models as flat black shapes on white, for judging outlines."""
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 8
    scene.cycles.use_denoising = False
    scene.render.resolution_x, scene.render.resolution_y = size
    try:
        scene.view_settings.view_transform = 'Standard'
        scene.view_settings.look = 'None'
    except Exception:
        pass
    ink = bpy.data.materials.new("oa_ink")
    ink.use_nodes = True
    nodes, links = ink.node_tree.nodes, ink.node_tree.links
    glow = nodes.new("ShaderNodeEmission")
    glow.inputs[0].default_value = (0.0, 0.0, 0.0, 1.0)
    links.new(glow.outputs[0], nodes["Material Output"].inputs["Surface"])
    for o in objects:
        o.data.materials.clear()
        o.data.materials.append(ink)
    if scene.world is None:
        scene.world = bpy.data.worlds.new("w")
    scene.world.use_nodes = True
    bg = scene.world.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (1.0, 1.0, 1.0, 1)
    bg.inputs[1].default_value = 1.0
    cam = bpy.data.cameras.new("cam")
    cam.lens = lens
    co = bpy.data.objects.new("cam", cam)
    co.location = eye
    co.rotation_euler = (Vector(target) - Vector(eye)).to_track_quat('-Z', 'Y').to_euler()
    scene.collection.objects.link(co)
    scene.camera = co
    scene.render.filepath = path
    scene.render.film_transparent = False
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(co)


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    previews = args[args.index("--preview") + 1] if "--preview" in args else None
    poses = args[args.index("--poses") + 1] if "--poses" in args else None
    only = args[args.index("--type") + 1] if "--type" in args else None
    kinds = args[args.index("--kinds") + 1] if "--kinds" in args else None
    if kinds and previews:
        posed_line_up(kinds, previews)
        sys.exit(0)
    for name, maker in BUILDS:
        if only and name != "Enemy" + only:
            continue
        oa.reset_scene()
        parts = maker()
        stump_objects = be.stump()
        if poses and previews:
            be.render_poses(parts, stump_objects, poses, previews)
            continue
        report(name, parts)
        be.write(parts, stump_objects, name)
        if previews:
            for o in stump_objects:
                o.hide_render = True
            shown = all_objects(parts) + [be.ground()]
            short = name[5:].lower()
            oa.render_preview(os.path.join(previews, short + "_front.png"), shown, (1.3, 3.6, 1.35), (0, 0, 0.95), lens=60, size=(800, 800), samples=48)
            oa.render_preview(os.path.join(previews, short + "_back.png"), shown, (-1.5, -3.5, 1.5), (0, 0, 0.95), lens=60, size=(800, 800), samples=48)
    if previews and not poses and not only:
        line_up(previews)
