"""Builds the alien creatures with texture pictures (Matthew, 17 Nov 2026: alien assets "that look realistic (not
ps2 style)", from the most unusual real animals, that "gib and lose limbs"; concepts and the nine he chose are in
docs/briefs/alien-creatures.md).

    blender -b -P tools/blender/build_creatures.py -- --write-to <folder>     (a trial: nothing in the game changes)
    blender -b -P tools/blender/build_creatures.py -- --game                  (writes the game's model and pictures)
    add  --preview <folder>  for pictures,  --only Strider  to build some,  --draft  for a quick look,
         --poses file.json  to draw the frames the game's own animator wrote (tools/blender/creature_dump)

Each creature is described once and built twice (see oa_materials.py): a low model for the game and a high one
whose bumps, spines, barnacles and surface are baked into the low model's pictures.

A creature is not built standing. Each piece that can be shot off is modelled in its own space: its joint at the
origin, +Y along the piece, +Z the side that faces up and outward when the limb is held level. The game (and the
previews here) put the pieces together from the sockets written in the file. A limb that the animal has many of is
modelled once and drawn many times (the Strider's eight walking legs are three pieces), so the pictures can be
four times as sharp.

The Strider starts from a real thing: the Japanese spider crab, Macrocheira kaempferi (pear-shaped spiny carapace
with two horns, ten very long limbs, the front pair ending in slender claws; orange with white blotches, pale
underneath, barnacles on old animals), at twice life size.
"""
import bpy, bmesh, sys, os, math, json, random, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import oa_blender as oa
import oa_real as real
import oa_materials as mats
from mathutils import Matrix, Vector, noise

HERE = os.path.dirname(os.path.abspath(__file__))
GAME = os.path.normpath(os.path.join(HERE, "..", "..", "unity", "Assets", "OrbitalAnte", "Resources", "Models"))


class Maker:
    """Collects one build of a creature: `hi` is False for the game's low model, True for the baked one."""

    def __init__(self, hi, flat, materials):
        self.hi = hi
        self.flat = flat                # material name -> (corner colour, smoothness)
        self.makers = materials         # material name -> function making the baked material
        self.materials = {}
        self.parts = {}                 # piece name -> [objects]
        self.count = 0

    def material(self, name):
        if name not in self.materials:
            self.materials[name] = self.makers[name]()
        return self.materials[name]

    def add(self, obj, material, piece, only=None, second=None, smooth=None):
        """only: 'hi' for a shape that exists only to be baked, 'lo' for one only in the game.
        second: (material, rule) gives the faces whose (centre, normal) the rule says yes to another material."""
        if only == ('lo' if self.hi else 'hi'):
            mesh = obj.data
            bpy.data.objects.remove(obj)
            bpy.data.meshes.remove(mesh)
            return None
        self.count += 1
        if smooth is not None:
            obj["oa_smooth"] = smooth
        if self.hi:
            obj.data.materials.append(self.material(material))
            if second:
                obj.data.materials.append(self.material(second[0]))
                for p in obj.data.polygons:
                    if second[1](p.center, p.normal):
                        p.material_index = 1
            oa.auto_smooth(obj.data, obj.get("oa_smooth", 60.0))
        else:
            colour, smoothness = self.flat[material]
            obj["oa_colour"] = list(colour)
            oa.paint(obj, wear=0.3, dirt=0.5, mottle=0.9, seed=self.count, grain=6.0)
            real.made_of(obj, smoothness, 0.0)
        self.parts.setdefault(piece, []).append(obj)
        return obj

    # ---- shapes: every size is given once; `hi` only changes how finely the result is cut.

    def skin(self, name, origin, axis, forward, keys, around=10, step=0.12, power=2.2):
        return real.limb(name, origin, axis, forward, keys, (0.5, 0.5, 0.5), n=around * 4 if self.hi else around,
                         step=step / 6 if self.hi else step, power=power)

    def ball(self, name, centre, radius, scale=(1, 1, 1), around=10):
        return oa.sphere(name, centre, radius, (0.5, 0.5, 0.5), scale=scale, segments=around * 3 if self.hi else around)

    def spine(self, name, base, way, length, radius, around=5, bend=(0, 0, 0)):
        """A thorn: a cone with a rounded foot and a blunt point, leaning toward `bend` as it rises."""
        base, way, bend = Vector(base), Vector(way).normalized(), Vector(bend)
        side = way.orthogonal().normalized()
        other = way.cross(side)
        n = around * 4 if self.hi else around
        shares = [(0.0, 1.25), (0.08, 1.0), (0.3, 0.72), (0.6, 0.42), (0.85, 0.2), (1.0, 0.05)] if self.hi else [(0.0, 1.15), (0.45, 0.55), (1.0, 0.06)]
        rings = []
        for t, r in shares:
            centre = base - way * (radius * 0.4) * (1 - t) + way * (length * t) + bend * (length * t * t)
            rings.append(real.ring(centre, side, other, radius * r, radius * r, n))
        return real.loft(name, rings, (0.5, 0.5, 0.5), smooth=80.0)

    def barnacle(self, name, base, way, radius, height):
        """A small limy volcano with a dark mouth. Only ever baked."""
        profile = [(-height * 0.3, radius * 1.1), (height * 0.15, radius), (height * 0.75, radius * 0.6), (height, radius * 0.52),
                   (height * 0.98, radius * 0.4), (height * 0.55, radius * 0.3)]
        return oa.lathe(name, base, way, profile, (0.5, 0.5, 0.5), segments=18)


# ====================================================================== the Strider (Japanese spider crab)

# Every number the game needs is also written into the model as a socket (see strider_sockets), and held by the rule
# test StriderModelFile_MatchesTheLayout against Creatures/WalkerLayout.cs (WalkerLayout.Strider). Metres, Blender's axes.
STRIDER = {
    # Where each limb joins the body (right side; the left is mirrored) and the way it points when held straight out,
    # in degrees round from the animal's right toward its front. 0 is the claw arm, 1 to 4 the walking legs.
    "hips": [((0.215, 0.300, -0.075), 64.0), ((0.335, 0.150, -0.080), 34.0), ((0.385, -0.020, -0.080), 6.0),
             ((0.360, -0.180, -0.080), -24.0), ((0.265, -0.310, -0.075), -54.0)],
    "leg": (1.52, 1.60, 0.56),          # upper, lower, tip
    "arm": (1.28, 0.368, 1.20),         # upper, lower, claw (to the point of the fixed finger)
    "hinge": (0.0, 0.624, 0.050),       # where the moving finger turns, in the claw's space
    "finger": 0.592,
    "stump": 0.075,                     # the radius the wound is modelled at; the game scales it to the joint
    "leg_scale": (1.0, 1.06, 1.0, 0.90),
    "size": 1.3,                        # the game draws the whole animal this much larger than it is modelled
}
LONG = 0.8      # the limbs were first drawn a quarter longer; every distance along one is multiplied by this


def sk(keys):
    return [(k[0] * LONG,) + tuple(k[1:]) for k in keys]

STRIDER_FLAT = {"shell": ((0.72, 0.36, 0.20), 0.40), "limb": ((0.74, 0.38, 0.21), 0.40), "under": ((0.80, 0.70, 0.58), 0.35),
                "joint": ((0.60, 0.42, 0.30), 0.25), "flesh": ((0.78, 0.50, 0.44), 0.6), "inside": ((0.80, 0.70, 0.64), 0.5),
                "lime": ((0.78, 0.75, 0.68), 0.08), "eye": ((0.08, 0.07, 0.06), 0.85), "claw": ((0.76, 0.46, 0.30), 0.45),
                "point": ((0.50, 0.26, 0.16), 0.5)}
ORANGE, CREAM, MAROON = (0.50, 0.115, 0.035), (0.66, 0.52, 0.38), (0.13, 0.028, 0.014)
STRIDER_MATERIALS = {
    "shell": lambda: mats.chitin("shell", ORANGE, CREAM, MAROON, seed=1.0, bumps=1.0, growth=0.75, mottle=0.75),
    "limb": lambda: mats.chitin("limb", ORANGE, CREAM, MAROON, under=(0.62, 0.40, 0.26), seed=4.0, bumps=0.9, growth=0.35, mottle=1.0, size=0.7),
    "under": lambda: mats.chitin("under", (0.60, 0.42, 0.27), (0.70, 0.60, 0.48), (0.20, 0.07, 0.035), seed=7.0, bumps=0.3, growth=0.0, mottle=0.5, gloss=0.36),
    "claw": lambda: mats.chitin("claw", ORANGE, CREAM, MAROON, under=(0.62, 0.40, 0.26), seed=9.0, bumps=0.45, growth=0.2, mottle=0.9, size=0.8,
                                tip=(1, 0.84, 1.14, (0.70, 0.62, 0.52))),
    "finger": lambda: mats.chitin("finger", ORANGE, CREAM, MAROON, seed=11.0, bumps=0.4, growth=0.2, mottle=0.9, size=0.8,
                                  tip=(1, 0.22, 0.51, (0.70, 0.62, 0.52))),
    "point": lambda: mats.chitin("point", ORANGE, CREAM, MAROON, under=(0.62, 0.40, 0.26), seed=13.0, bumps=0.3, growth=0.1, mottle=0.8, size=0.8,
                                 tip=(1, 0.24, 0.53, (0.060, 0.030, 0.018)), gloss=0.5),
    "joint": lambda: mats.membrane("joint", seed=2.0),
    "flesh": lambda: mats.flesh("flesh", seed=3.0),
    "inside": lambda: mats.shell_inside("inside", seed=5.0),
    "lime": lambda: mats.chalk("lime", seed=6.0),
    "eye": lambda: mats.eye("eye"),
}
STRIDER_FLAT["finger"] = STRIDER_FLAT["claw"]

# The carapace along its length, back to front: (distance, half width, height above the rim, depth of the belly below it).
SHELL_BACK = -0.44
SHELL_KEYS = [(0.00, 0.070, 0.035, 0.030), (0.05, 0.220, 0.105, 0.075), (0.16, 0.345, 0.185, 0.120), (0.32, 0.405, 0.225, 0.150),
              (0.48, 0.370, 0.225, 0.155), (0.62, 0.285, 0.200, 0.140), (0.74, 0.200, 0.150, 0.115), (0.84, 0.135, 0.100, 0.085),
              (0.92, 0.085, 0.060, 0.055), (0.97, 0.040, 0.030, 0.028)]
RIM_Z = -0.030
THICK = 0.022
# The swellings of the carapace over what lies under it, and the furrows between them: (x, y, reach, height).
SWELLS = [(0.0, 0.215, 0.105, 0.030), (0.0, 0.045, 0.075, 0.018), (0.0, -0.130, 0.095, 0.026), (0.0, -0.320, 0.070, 0.014),
          (0.205, -0.110, 0.130, 0.034), (-0.205, -0.110, 0.130, 0.034), (0.150, 0.170, 0.070, 0.018), (-0.150, 0.170, 0.070, 0.018),
          (0.095, 0.030, 0.035, -0.016), (-0.095, 0.030, 0.035, -0.016), (0.075, -0.120, 0.030, -0.012), (-0.075, -0.120, 0.030, -0.012),
          (0.0, -0.030, 0.030, -0.010), (0.130, -0.270, 0.040, -0.010), (-0.130, -0.270, 0.040, -0.010)]


def shell_swell(x, y):
    lumps = 0.010 * noise.noise(Vector((x * 7.0, y * 7.0, 0.5))) + 0.005 * noise.noise(Vector((x * 17.0, y * 17.0, 3.5)))
    return 2.3 * sum(h * math.exp(-((x - sx) ** 2 + (y - sy) ** 2) / (r * r)) for sx, sy, r, h in SWELLS) + lumps


def shell_top(x, y):
    """Height of the carapace's outer surface above the body's middle at a point seen from above, or None outside."""
    t = y - SHELL_BACK
    if t < SHELL_KEYS[0][0] or t > SHELL_KEYS[-1][0]:
        return None
    w, h, _ = real.along(SHELL_KEYS, t)
    if abs(x) >= w:
        return None
    s = (1.0 - (abs(x) / w) ** 2.3) ** (1 / 2.3)
    return RIM_Z + (h + shell_swell(x, y)) * s


def shell_normal(x, y):
    e = 0.004
    a, b, c = shell_top(x, y), shell_top(x + e, y), shell_top(x, y + e)
    if a is None or b is None or c is None:
        return Vector((0, 0, 1))
    return Vector((-(b - a) / e, -(c - a) / e, 1.0)).normalized()


def half_shell(g, name, top, around, step):
    """The carapace (top) or the belly tub (not top) as one closed skin: the outside, and the face that is seen when
    the two come apart."""
    n = around * 4 if g.hi else around
    m = around * 2 if g.hi else max(4, around // 2)
    step = step / 5 if g.hi else step
    places, t = [], SHELL_KEYS[0][0]
    while t < SHELL_KEYS[-1][0] - 1e-6:
        places.append(t)
        t += step
    places.append(SHELL_KEYS[-1][0])
    e = 2.0 / 2.3
    rings = []
    for t in places:
        w, h, d = real.along(SHELL_KEYS, t)
        y = SHELL_BACK + t
        ring = []
        for i in range(n + 1):
            a = math.pi * i / n
            c, s = math.cos(a), math.sin(a)
            x = w * math.copysign(abs(c) ** e, c)
            if top:
                ring.append(Vector((x, y, RIM_Z + (h + shell_swell(x, y)) * s ** e)))
            else:
                # The belly is flatter underneath than the back is on top.
                ring.append(Vector((x * (1.0 - 0.10 * s), y, RIM_Z - d * s ** 0.62)))
        for i in range(1, m):
            a = math.pi * (1 - i / m)
            c, s = math.cos(a), math.sin(a)
            if top:
                wi, hi_ = max(0.004, w - THICK), max(0.004, h - THICK)
                x = wi * math.copysign(abs(c) ** e, c)
                ring.append(Vector((x, y, RIM_Z + (hi_ + shell_swell(x, y) * 0.8) * s ** e)))
            else:
                # What lies in the tub: the animal's insides, heaped a little above the rim.
                wi = max(0.004, w * 0.90)
                x = wi * math.copysign(abs(c) ** e, c)
                lumps = 0.012 * noise.noise(Vector((x * 9.0, y * 9.0, 2.0)))
                ring.append(Vector((x, y, RIM_Z + (min(0.060, h * 0.45) + lumps) * s)))
        rings.append(ring)
    return real.loft(name, rings, (0.5, 0.5, 0.5), smooth=75.0)


def strider(hi):
    g = Maker(hi, STRIDER_FLAT, STRIDER_MATERIALS)
    rnd = random.Random(11)
    L = STRIDER

    # ------------------------------------------------------------- Carapace: the lid, horns, thorns, barnacles
    g.add(half_shell(g, "carapace", True, 14, 0.048), "shell", "Carapace",
          second=("inside", lambda c, nrm: (shell_top(c.x, c.y) or -9) - c.z > THICK * 0.5))
    for side in (-1, 1):
        # The two horns of the rostrum, and the thorn over each eye.
        g.add(g.spine("horn", (side * 0.040, 0.455, 0.000), (side * 0.30, 1.0, 0.16), 0.300, 0.034, around=7, bend=(side * 0.30, 0, 0.10)), "shell", "Carapace")
        g.add(g.spine("brow", (side * 0.120, 0.360, 0.040), (side * 0.75, 0.55, 0.35), 0.085, 0.020, around=5), "shell", "Carapace")
        # Thorns along each side of the lid, largest at the widest part.
        for t, size in ((0.10, 0.045), (0.20, 0.060), (0.30, 0.075), (0.42, 0.070), (0.54, 0.060), (0.66, 0.055), (0.77, 0.045)):
            w, h, _ = real.along(SHELL_KEYS, t)
            y = SHELL_BACK + t
            g.add(g.spine("side thorn", (side * (w - 0.012), y, RIM_Z + 0.030), (side, 0.12, 0.28), size, size * 0.30), "shell", "Carapace")
    # Thorns down the middle of the back and on each swelling.
    for x, y, size in ((0, 0.300, 0.050), (0, 0.215, 0.070), (0, 0.130, 0.050), (0, -0.130, 0.075), (0, -0.250, 0.050), (0, -0.330, 0.060),
                       (0.205, -0.110, 0.070), (0.150, 0.170, 0.050), (0.270, -0.010, 0.045), (0.250, -0.230, 0.045), (0.110, 0.290, 0.035),
                       (0.120, -0.040, 0.040), (0.300, -0.130, 0.040)):
        for side in ((-1, 1) if x else (1,)):
            n = shell_normal(side * x, y)
            g.add(g.spine("back thorn", (side * x, y, shell_top(side * x, y) - 0.004), n + Vector((0, -0.25, 0)), size, size * 0.34), "shell", "Carapace")
    # Small warts and barnacles: only in the pictures.
    for _ in range(150):
        x, y = rnd.uniform(-0.40, 0.40), rnd.uniform(-0.42, 0.44)
        z = shell_top(x, y)
        if z is None or z < RIM_Z + 0.03:
            continue
        n = shell_normal(x, y)
        size = rnd.uniform(0.008, 0.018)
        g.add(g.spine("wart", (x, y, z - 0.003), n, size * 0.9, size * 0.62, around=6), "shell", "Carapace", only='hi')
    spots = [(-0.17, -0.20), (0.11, -0.26), (0.24, 0.02), (-0.08, 0.10)]
    for cx, cy in spots:
        for _ in range(rnd.randint(4, 8)):
            x, y = cx + rnd.gauss(0, 0.035), cy + rnd.gauss(0, 0.035)
            z = shell_top(x, y)
            if z is None or z < RIM_Z + 0.05:
                continue
            r = rnd.uniform(0.009, 0.019)
            g.add(g.barnacle("barnacle", (x, y, z), shell_normal(x, y), r, r * rnd.uniform(0.7, 1.1)), "lime", "Carapace", only='hi')

    # ------------------------------------------------------------- Belly: the tub, mouth, eyes, tail flap, hips
    g.add(half_shell(g, "belly", False, 14, 0.048), "under", "Belly",
          second=("flesh", lambda c, nrm: c.z > RIM_Z + 0.002))
    # The tail flap folded forward under the body, in six bands.
    flap = [(0.0, 0.075, 0.016, 0, 0.0), (0.06, 0.115, 0.020, 0, 0.0), (0.22, 0.125, 0.020, 0, 0.0), (0.38, 0.100, 0.018, 0, 0.0), (0.47, 0.050, 0.012, 0, 0.0)]
    if hi:
        ridged = []
        for i in range(48):
            t = 0.47 * i / 47
            rs, rf, _, _ = real.along(flap, t)
            ridged.append((t, rs, rf * (1.0 + 0.22 * abs(math.sin(math.pi * t / 0.078))), 0, 0.0))
        flap = ridged
    g.add(g.skin("tail flap", (0, -0.385, -0.135), (0, 1, 0.02), (0, 0, -1), flap, around=10, step=0.08, power=2.6), "under", "Belly")
    # The doors over the mouth, and the mouth behind them.
    for side in (-1, 1):
        g.add(g.skin("mouth door", (side * 0.036, 0.250, -0.118), (side * 0.10, 1.0, 0.42), (0, 0.35, -1),
                     [(0.0, 0.030, 0.012), (0.05, 0.038, 0.015), (0.12, 0.034, 0.014), (0.16, 0.016, 0.008)], around=8, step=0.05, power=2.8), "under", "Belly")
        g.add(g.skin("feeler", (side * 0.060, 0.385, -0.040), (side * 0.35, 1.0, 0.25), (0, 0, 1),
                     [(0.0, 0.009, 0.009), (0.10, 0.006, 0.006), (0.20, 0.003, 0.003)], around=4, step=0.10), "under", "Belly")
        # An eye on a short stalk, tucked under the brow thorn.
        g.add(g.skin("eye stalk", (side * 0.085, 0.340, -0.012), (side * 0.80, 0.55, 0.22), (0, 0, 1),
                     [(0.0, 0.020, 0.020), (0.030, 0.017, 0.017), (0.055, 0.019, 0.019)], around=7, step=0.03), "joint", "Belly")
        g.add(g.ball("eye", Vector((side * 0.085, 0.340, -0.012)) + Vector((side * 0.80, 0.55, 0.22)).normalized() * 0.066, 0.025, around=8), "eye", "Belly")
    g.add(g.ball("mouth", (0, 0.300, -0.085), 0.050, scale=(1.2, 0.8, 0.8), around=8), "joint", "Belly")
    # The plates of the chest between the legs: only in the pictures.
    for i, y in enumerate((0.200, 0.085, -0.040, -0.165)):
        for side in (-1, 1):
            g.add(real.sweep("chest plate", [(side * 0.030, y, -0.168 + 0.004 * i), (side * 0.150, y - 0.010, -0.150), (side * 0.260, y - 0.030, -0.118)],
                             0.050, (0.5, 0.5, 0.5), n=20, flat=0.16, cuts=6, power=3.0), "under", "Belly", only='hi')
    # A short stub for each limb to turn on.
    for i, (place, degrees) in enumerate(L["hips"]):
        for side in (-1, 1):
            p = Vector((side * place[0], place[1], place[2]))
            out = Vector((side * math.cos(math.radians(degrees)), math.sin(math.radians(degrees)), 0.0))
            r = 0.078 if i == 0 else 0.068
            g.add(g.skin("hip", p - out * 0.17, out, (0, 0, 1), [(0.0, r * 1.45, r * 1.25), (0.07, r * 1.25, r * 1.18), (0.13, r * 1.08, r * 1.10), (0.17, r * 0.95, r * 0.98), (0.20, r * 0.60, r * 0.62)],
                         around=12, step=0.045, power=2.0), "limb", "Belly")

    # ------------------------------------------------------------- limbs
    def joint(piece, radius, seed=0):
        """The soft ball every piece starts with, over its joint."""
        g.add(g.ball("joint", (0, 0, 0), radius, around=8), "joint", piece)

    def thorns(piece, material, places, top=True):
        for y, r, size, lean in places:
            y *= LONG
            g.add(g.spine("thorn", (0, y, r - 0.006), (0, lean, 1), size, size * 0.30, around=4), material, piece)

    def spinules(piece, material, keys, count, seed, start=0.12, end=0.94):
        """Small sharp warts all round a limb, more of them on top: only in the pictures."""
        r = random.Random(seed)
        keys = sk(keys)
        for _ in range(count):
            y = keys[-1][0] * r.uniform(start, end)
            a = r.gauss(math.pi / 2, 1.5)
            rs, rf, _, of = real.along(keys, y)
            c, s_ = math.cos(a), math.sin(a)
            size = r.uniform(0.006, 0.014)
            g.add(g.spine("spinule", (rs * c * 0.97, y, of + rf * s_ * 0.97), (c, 0.35, s_), size * 1.5, size * 0.6, around=5), material, piece, only='hi')

    upper, lower, tip = L["leg"]
    # Upper leg: a long shaft, a little bowed, swelling toward the knee, with a row of thorns along the top.
    joint("LegUpper", 0.070)
    g.add(g.skin("upper leg", (0, 0, 0), (0, 1, 0), (0, 0, 1),
                 sk([(0.02, 0.040, 0.042, 0, 0.0), (0.09, 0.066, 0.070, 0, 0.0), (0.22, 0.058, 0.064, 0, 0.004), (0.70, 0.058, 0.063, 0, 0.016),
                  (1.30, 0.054, 0.059, 0, 0.014), (1.68, 0.052, 0.056, 0, 0.004), (1.80, 0.066, 0.070, 0, 0.0), (1.87, 0.064, 0.066, 0, 0.0), (upper / LONG, 0.040, 0.042, 0, 0.0)]),
                 around=12, step=0.13, power=2.25), "limb", "LegUpper")
    spinules("LegUpper", "limb", [(0.0, 0.058, 0.064, 0, 0.004), (0.70, 0.058, 0.063, 0, 0.016), (1.30, 0.054, 0.059, 0, 0.014), (1.90, 0.052, 0.056, 0, 0.004)], 110, 1)
    thorns("LegUpper", "limb", [(0.35, 0.064, 0.035, 0.2), (0.75, 0.074, 0.040, 0.2), (1.15, 0.071, 0.035, 0.2), (1.50, 0.062, 0.035, 0.2), (1.80, 0.070, 0.060, 0.5)])
    # Lower leg: a short wrist-like piece and a long shin grown together.
    joint("LegLower", 0.062)
    g.add(g.skin("lower leg", (0, 0, 0), (0, 1, 0), (0, 0, 1),
                 sk([(0.02, 0.038, 0.040, 0, 0.0), (0.08, 0.058, 0.062, 0, 0.0), (0.24, 0.060, 0.064, 0, 0.0), (0.33, 0.044, 0.046, 0, 0.0), (0.40, 0.054, 0.058, 0, 0.0),
                  (0.90, 0.048, 0.052, 0, -0.010), (1.60, 0.042, 0.046, 0, -0.008), (1.90, 0.044, 0.048, 0, 0.0), (lower / LONG, 0.030, 0.032, 0, 0.0)]),
                 around=12, step=0.13, power=2.25), "limb", "LegLower")
    spinules("LegLower", "limb", [(0.0, 0.056, 0.060, 0, 0.0), (0.90, 0.048, 0.052, 0, -0.010), (1.60, 0.042, 0.046, 0, -0.008), (2.0, 0.040, 0.044, 0, 0.0)], 90, 2, start=0.22)
    thorns("LegLower", "limb", [(0.22, 0.062, 0.035, 0.3), (0.80, 0.050, 0.025, 0.2), (1.30, 0.046, 0.025, 0.2)])
    # The tip: a curved, hard, dark point the animal stands on.
    joint("LegTip", 0.044)
    g.add(g.skin("leg tip", (0, 0, 0), (0, 1, 0), (0, 0, 1),
                 sk([(0.01, 0.028, 0.030, 0, 0.0), (0.06, 0.040, 0.043, 0, 0.0), (0.25, 0.032, 0.036, 0, -0.012), (0.50, 0.019, 0.023, 0, -0.040), (0.64, 0.010, 0.012, 0, -0.068),
                  (tip / LONG, 0.003, 0.004, 0, -0.085)]), around=8, step=0.10, power=2.2), "point", "LegTip")

    a_upper, a_lower, a_claw = L["arm"]
    joint("ArmUpper", 0.080)
    g.add(g.skin("upper arm", (0, 0, 0), (0, 1, 0), (0, 0, 1),
                 sk([(0.02, 0.046, 0.048, 0, 0.0), (0.10, 0.076, 0.080, 0, 0.0), (0.25, 0.066, 0.072, 0, 0.004), (0.80, 0.066, 0.072, 0, 0.014),
                  (1.38, 0.062, 0.068, 0, 0.004), (1.50, 0.076, 0.082, 0, 0.0), (1.57, 0.072, 0.076, 0, 0.0), (a_upper / LONG, 0.046, 0.048, 0, 0.0)]),
                 around=12, step=0.13, power=2.25), "limb", "ArmUpper")
    spinules("ArmUpper", "limb", [(0.0, 0.066, 0.072, 0, 0.004), (0.80, 0.066, 0.072, 0, 0.014), (1.60, 0.062, 0.068, 0, 0.004)], 110, 3)
    thorns("ArmUpper", "limb", [(0.30, 0.072, 0.040, 0.2), (0.62, 0.082, 0.045, 0.2), (0.94, 0.082, 0.045, 0.2), (1.24, 0.072, 0.040, 0.2), (1.50, 0.082, 0.065, 0.5)])
    joint("ArmLower", 0.072)
    g.add(g.skin("lower arm", (0, 0, 0), (0, 1, 0), (0, 0, 1),
                 sk([(0.02, 0.044, 0.046, 0, 0.0), (0.09, 0.068, 0.074, 0, 0.0), (0.24, 0.074, 0.082, 0, 0.004), (0.38, 0.068, 0.074, 0, 0.0), (a_lower / LONG, 0.042, 0.046, 0, 0.0)]),
                 around=12, step=0.09, power=2.3), "limb", "ArmLower")
    thorns("ArmLower", "limb", [(0.14, 0.074, 0.040, 0.0), (0.28, 0.084, 0.050, 0.2)])
    # The claw: a long narrow hand, deeper than it is thick, and the lower finger that does not move.
    hinge = Vector(L["hinge"])
    joint("Claw", 0.066)
    g.add(g.skin("hand", (0, 0, 0), (0, 1, 0), (0, 0, 1),
                 sk([(0.02, 0.040, 0.046, 0, 0.0), (0.10, 0.056, 0.074, 0, 0.0), (0.40, 0.060, 0.092, 0, 0.004), (0.66, 0.058, 0.094, 0, 0.002),
                  (0.80, 0.050, 0.074, 0, -0.026), (1.00, 0.040, 0.050, 0, -0.040), (1.25, 0.030, 0.036, 0, -0.030), (1.42, 0.018, 0.020, 0, -0.004), (a_claw / LONG, 0.005, 0.006, 0, 0.016)]),
                 around=12, step=0.10, power=2.5), "claw", "Claw")
    spinules("Claw", "claw", [(0.0, 0.056, 0.080, 0, 0.0), (0.40, 0.060, 0.092, 0, 0.004), (0.70, 0.056, 0.090, 0, 0.0)], 60, 4, start=0.15, end=0.9)
    thorns("Claw", "claw", [(0.20, 0.080, 0.040, 0.2), (0.48, 0.096, 0.040, 0.2)])
    for k in range(9):
        # Teeth along the lower finger's cutting edge: blunt knobs, larger near the hinge.
        y = (0.86 + 0.062 * k) * LONG
        _, rf, _, of = real.along(sk([(0.80, 0.050, 0.074, 0, -0.026), (1.00, 0.040, 0.050, 0, -0.040), (1.25, 0.030, 0.036, 0, -0.030), (1.42, 0.018, 0.020, 0, -0.004)]), y)
        g.add(g.spine("tooth", (0, y, of + rf - 0.006), (0, 0, 1), 0.016 - 0.0008 * k, 0.012 - 0.0006 * k, around=6), "claw", "Claw", only='hi')
    g.add(g.ball("hinge knob", hinge, 0.034, scale=(1.5, 1.0, 1.0), around=6), "joint", "Claw")
    # The finger that moves: hinged at the top of the hand, closing down onto the other.
    length = L["finger"]
    g.add(g.skin("finger", (0, 0, 0), (0, 1, 0), (0, 0, 1),
                 sk([(-0.02, 0.030, 0.030, 0, 0.0), (0.05, 0.042, 0.044, 0, 0.002), (0.25, 0.036, 0.040, 0, 0.006), (0.50, 0.027, 0.030, 0, -0.002), (0.66, 0.016, 0.018, 0, -0.022),
                  (length / LONG, 0.005, 0.006, 0, -0.050)]), around=8, step=0.10, power=2.4), "finger", "ClawFinger")
    for k in range(8):
        y = (0.10 + 0.064 * k) * LONG
        _, rf, _, of = real.along(sk([(0.05, 0.042, 0.044, 0, 0.002), (0.25, 0.036, 0.040, 0, 0.006), (0.50, 0.027, 0.030, 0, -0.002), (0.66, 0.016, 0.018, 0, -0.022)]), y)
        g.add(g.spine("tooth", (0, y, of - rf + 0.006), (0, 0, -1), 0.015 - 0.0008 * k, 0.011 - 0.0006 * k, around=6), "finger", "ClawFinger", only='hi')

    # ------------------------------------------------------------- what is left when something comes off
    # The wound: a ragged ring of broken shell round wet meat. Modelled looking along +Y, the way a piece points.
    r = L["stump"]
    meat = g.ball("wound meat", (0, 0.010, 0), r * 0.86, scale=(1.0, 0.55, 1.0), around=9)
    real.displace(meat, lambda p: 0.010 * noise.noise(p * 28.0) + 0.006 * noise.noise(p * 61.0))
    g.add(meat, "flesh", "Stump")
    ragged = []
    for i in range(9):
        t = i / 8
        ragged.append((-0.050 + 0.085 * t, r * (0.98 + 0.07 * t), r * (1.02 + 0.07 * t)))
    collar = g.skin("wound shell", (0, 0, 0), (0, 1, 0), (0, 0, 1), [ragged[0], ragged[4], ragged[8]] if not hi else ragged, around=10, step=0.04, power=2.25)
    # Break the collar's forward edge into teeth.
    bm = bmesh.new()
    bm.from_mesh(collar.data)
    for v in bm.verts:
        if v.co.y > 0.0:
            a = math.atan2(v.co.z, v.co.x)
            v.co.y += (v.co.y / 0.035) * 0.020 * (noise.noise(Vector((math.cos(a) * 3.1, math.sin(a) * 3.1, 5.0))) + 0.35 * math.sin(a * 7.0))
            v.co.y = max(v.co.y, 0.004)
    bm.to_mesh(collar.data)
    bm.free()
    g.add(collar, "limb", "Stump")
    # Two lumps of meat with shell still on them, for when the body bursts.
    for k, (size, squash) in enumerate((((0.15, (1.0, 0.75, 0.62))), ((0.115, (0.8, 1.0, 0.7))))):
        piece = "Meat%d" % k
        lump = g.ball("meat", (0, 0, 0), size, scale=squash, around=10)
        real.displace(lump, lambda p, k=k: 0.022 * noise.noise(p * 9.0 + Vector((k * 7.0, 0, 0))) + 0.010 * noise.noise(p * 23.0))
        g.add(lump, "flesh", piece)
        crust = real.ball_patch("shell bit", (0, 0, 0), (0.2, 0.1, 1.0), (0, 1, 0), size * squash[2] * 1.02, (0, 62), 0, 360, (0.5, 0.5, 0.5),
                                thickness=0.014, n=40 if hi else 9, m=14 if hi else 3)
        crust.data.transform(Matrix.Diagonal((squash[0] / squash[2] * 0.96, squash[1] / squash[2] * 0.96, 1.0, 1.0)))
        g.add(crust, "shell", piece)
        g.add(g.spine("thorn", (0.02, 0.01, size * squash[2] * 0.98), (0.2, 0.1, 1), 0.05, 0.016), "shell", piece)
    return g


def strider_sockets():
    L = STRIDER
    out = []
    for i, (place, degrees) in enumerate(L["hips"]):
        for side, tag in ((1, "R"), (-1, "L")):
            p = Vector((side * place[0], place[1], place[2]))
            way = Vector((side * math.cos(math.radians(degrees)), math.sin(math.radians(degrees)), 0.0))
            out.append(oa.socket("Hip%s%d" % (tag, i), p))
            out.append(oa.socket("Out%s%d" % (tag, i), p + way))
    out.append(oa.socket("LegUpperEnd", (0, L["leg"][0], 0)))
    out.append(oa.socket("LegLowerEnd", (0, L["leg"][1], 0)))
    out.append(oa.socket("LegTipEnd", (0, L["leg"][2], -0.085)))
    out.append(oa.socket("ArmUpperEnd", (0, L["arm"][0], 0)))
    out.append(oa.socket("ArmLowerEnd", (0, L["arm"][1], 0)))
    out.append(oa.socket("ClawEnd", (0, L["arm"][2], 0.016)))
    out.append(oa.socket("ClawHinge", L["hinge"]))
    out.append(oa.socket("FingerEnd", (0, L["finger"], -0.050)))
    out.append(oa.socket("StumpRim", (L["stump"], 0, 0)))
    return out


# ---------------------------------------------------------------------- putting a Strider together for pictures

def frame(origin, along, up_hint):
    """A piece's place: its joint at origin, its +Y along `along`, its +Z as near `up_hint` as that allows."""
    y = Vector(along).normalized()
    z = Vector(up_hint) - y * Vector(up_hint).dot(y)
    z.normalize()
    x = y.cross(z)
    m = Matrix((x, y, z)).transposed().to_4x4()
    m.translation = Vector(origin)
    return m


def strider_stand(height=1.55, reach=1.75, sway=0.0, missing=()):
    """A standing pose worked out here, for still pictures only (the game's animator, WalkerAnimator, is the real
    thing; draw its frames with --poses). Returns [(piece, matrix)] in the world, z up, ground at 0."""
    L = STRIDER
    placed = []
    body = Matrix.Translation((0, 0, height)) @ Matrix.Rotation(math.radians(sway), 4, 'Y')
    placed += [("Carapace", body), ("Belly", body)]
    up = Vector((0, 0, 1))
    for i, (place, degrees) in enumerate(L["hips"]):
        for side in (1, -1):
            key = ("R" if side > 0 else "L") + str(i)
            hip = body @ Vector((side * place[0], place[1], place[2]))
            out = Vector((side * math.cos(math.radians(degrees)), math.sin(math.radians(degrees)), 0.0))
            if key in missing:
                placed.append(("Stump", frame(hip, out, up) @ Matrix.Diagonal((1.0, 1.0, 1.0, 1.0))))
                continue
            if i == 0:
                # The claw arms: carried forward and low, hands hanging, fingers a little open.
                a, b, c = L["arm"]
                d1 = (out * math.cos(math.radians(18)) + up * math.sin(math.radians(18))).normalized()
                elbow = hip + d1 * a
                d2 = (out * math.cos(math.radians(-55)) + up * math.sin(math.radians(-55)) + Vector((-side * 0.25, 0.1, 0))).normalized()
                wrist = elbow + d2 * b
                d3 = (Vector((-side * 0.22, 0.55, -0.80))).normalized()
                placed.append(("ArmUpper", frame(hip, d1, up)))
                placed.append(("ArmLower", frame(elbow, d2, d1)))
                hand = frame(wrist, d3, d2 * -1 + Vector((0, 0.5, 0.3)))
                placed.append(("Claw", hand))
                placed.append(("ClawFinger", hand @ Matrix.Translation(L["hinge"]) @ Matrix.Rotation(math.radians(16 + 6 * side), 4, 'X')))
                continue
            s = L["leg_scale"][i - 1]
            a, b, c = (v * s for v in L["leg"])
            # Two-bone reach: the lower leg and its tip are one bent bone from the knee to the ground.
            tilt = math.radians(24)
            second = math.sqrt(b * b + c * c + 2 * b * c * math.cos(tilt))
            offset = math.atan2(c * math.sin(tilt), b + c * math.cos(tilt))
            r = reach * s * (1.0 + 0.06 * math.sin(i * 2.3 + side))
            foot = Vector((hip.x, hip.y, 0)) + out * r
            foot.z = 0.0
            span = min((foot - hip).length, a + second - 0.02)
            angle_to_foot = math.atan2(foot.z - hip.z, r)
            at_hip = math.acos(max(-1, min(1, (a * a + span * span - second * second) / (2 * a * span))))
            lift = angle_to_foot + at_hip
            d1 = out * math.cos(lift) + up * math.sin(lift)
            knee = hip + d1 * a
            to_foot = (foot - knee).normalized()
            # Turn the knee-to-foot line back by the offset to get the lower leg itself (the tip bends inward).
            axis = out.cross(up)
            d2 = Matrix.Rotation(offset, 3, axis) @ to_foot
            ankle = knee + d2 * b
            d3 = (foot - ankle).normalized()
            scale = Matrix.Diagonal((s, s, s, 1.0))
            placed.append(("LegUpper", frame(hip, d1, up) @ scale))
            placed.append(("LegLower", frame(knee, d2, d1 * -1 + out) @ scale))
            placed.append(("LegTip", frame(ankle, d3, out) @ scale))
    grow = Matrix.Diagonal((L["size"], L["size"], L["size"], 1.0))
    return [(piece, grow @ m) for piece, m in placed]


def instances(parts, placed):
    """Copies of the pieces' shapes standing where `placed` says (sharing the meshes)."""
    made = []
    for piece, matrix in placed:
        for o in parts[piece]:
            copy = bpy.data.objects.new(o.name + "_shown", o.data)
            copy["oa_smooth"] = o.get("oa_smooth", 60.0)
            copy.matrix_world = matrix
            bpy.context.scene.collection.objects.link(copy)
            made.append(copy)
    return made


def person(at):
    """Something the height of the player (1.8 m), so the pictures show how big the animal is."""
    x, y = at
    grey = (0.5, 0.5, 0.5)
    shapes = [oa.cylinder("person", (x, y, 0.0), (x, y, 1.45), 0.20, grey, radius_end=0.24, segments=14, bevel=0.05),
              oa.sphere("person head", (x, y, 1.64), 0.16, grey)]
    mat = bpy.data.materials.new("person")
    mat.use_nodes = True
    mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.75, 0.75, 0.72, 1)
    for s in shapes:
        s.data.materials.append(mat)
    return shapes


# ====================================================================== the Sprinter (camel spider)
#
# It starts from a real thing: a camel spider (Solifugae; Galeodes arabs), at about fifteen times life size. A domed
# head plate with two small eyes on a knob at its front edge; in front of it two enormous jaws side by side, each a
# swollen base running into a fixed upper finger with a moving lower finger under it, bare, polished and near black
# at the points; a long soft abdomen in ten bands; eight long hairy legs, the hind pair the longest; and in front of
# the legs a pair of thick feeling arms carried off the ground, ending in a blunt pale pad. Sand-coloured and hairy
# all over. Held by the rule test SprinterModelFile_MatchesTheLayout against WalkerLayout.Sprinter.
SPRINTER = {
    "hips": [((0.150, 0.560, -0.060), 72.0), ((0.215, 0.400, -0.085), 50.0), ((0.235, 0.250, -0.085), 18.0),
             ((0.225, 0.100, -0.085), -22.0), ((0.190, -0.040, -0.080), -52.0)],
    "leg": (0.55, 0.62, 0.36),
    "arm": (0.56, 0.56, 0.32),
    "hinge": (0.0, 0.130, -0.036),      # where the lower finger of a jaw turns, in the jaw's space
    "finger": 0.36,
    "jaw": (0.105, 0.640, 0.030),       # where the right jaw is hinged on the head
    "jaw_length": 0.52,
    "stump": 0.075,
    "leg_scale": (0.86, 0.94, 1.06, 1.30),
    "size": 1.0,
    "pieces": {"armoured": "Head", "soft": "Abdomen", "leg": ("LegUpper", "LegLower", "LegTip"),
               "arm": ("PalpUpper", "PalpLower", "PalpTip"), "finger": "JawFinger", "jaw": "Jaw"},
}
SAND, STRAW, UMBER = (0.40, 0.25, 0.115), (0.64, 0.52, 0.35), (0.085, 0.040, 0.020)
RUST, PITCH = (0.34, 0.13, 0.045), (0.030, 0.012, 0.008)
SPRINTER_FLAT = {"head": ((0.62, 0.42, 0.24), 0.30), "abdomen": ((0.60, 0.50, 0.38), 0.12), "leg": ((0.66, 0.50, 0.31), 0.14),
                 "palp": ((0.64, 0.47, 0.28), 0.14), "pad": ((0.62, 0.50, 0.36), 0.25), "jaw": ((0.55, 0.26, 0.11), 0.45),
                 "jawfinger": ((0.50, 0.22, 0.10), 0.45), "foot": ((0.50, 0.36, 0.22), 0.2), "joint": ((0.50, 0.36, 0.24), 0.2),
                 "flesh": ((0.78, 0.50, 0.44), 0.6), "eye": ((0.06, 0.05, 0.05), 0.85), "hair": ((0.72, 0.60, 0.42), 0.08)}
SPRINTER_MATERIALS = {
    "head": lambda: mats.bristly("head", (0.36, 0.19, 0.075), STRAW, UMBER, under=(0.50, 0.38, 0.24), seed=1.0, hair=0.55, gloss=0.34),
    "abdomen": lambda: mats.bristly("abdomen", (0.33, 0.22, 0.125), (0.54, 0.44, 0.31), UMBER, under=(0.50, 0.41, 0.29), seed=2.0, hair=0.8,
                                    plates=(0.110, 1.0), stripe=(0.11, (0.10, 0.055, 0.030), ROW), gloss=0.16),   # the Abdomen is the second piece in the bake's row
    "leg": lambda: mats.bristly("leg", SAND, STRAW, UMBER, seed=3.0, hair=1.0, size=0.7),
    "palp": lambda: mats.bristly("palp", SAND, STRAW, UMBER, seed=4.0, hair=1.0, size=0.7),
    "pad": lambda: mats.bristly("pad", (0.46, 0.36, 0.25), (0.60, 0.52, 0.40), UMBER, seed=5.0, hair=0.15, size=0.6, gloss=0.30),
    "foot": lambda: mats.bristly("foot", SAND, STRAW, UMBER, seed=6.0, hair=0.8, size=0.6, tip=(1, 0.30, 0.40, (0.10, 0.05, 0.03))),
    "jaw": lambda: mats.bristly("jaw", RUST, STRAW, UMBER, seed=7.0, hair=0.7, size=0.7, tip=(1, 0.20, 0.40, PITCH), gloss=0.40),
    "jawfinger": lambda: mats.bristly("jawfinger", RUST, STRAW, UMBER, seed=8.0, hair=0.5, size=0.7, tip=(1, 0.06, 0.26, PITCH), gloss=0.40),
    "joint": lambda: mats.membrane("joint", (0.27, 0.17, 0.09), seed=9.0),
    "flesh": lambda: mats.flesh("flesh", pale=(0.70, 0.62, 0.50), red=(0.36, 0.10, 0.04), seed=10.0),
    "eye": lambda: mats.eye("eye"),
    "hair": lambda: mats.bristly("hair", (0.50, 0.38, 0.22), STRAW, UMBER, seed=11.0, hair=0.0, gloss=0.20),
}

# The abdomen along its length, front to back: (distance, half width, half height, 0, how far its middle hangs).
ABDOMEN_KEYS = [(0.00, 0.060, 0.060, 0, 0.0), (0.05, 0.150, 0.120, 0, 0.0), (0.18, 0.225, 0.165, 0, -0.010), (0.40, 0.265, 0.200, 0, -0.020),
                (0.65, 0.255, 0.195, 0, -0.020), (0.88, 0.205, 0.160, 0, -0.012), (1.04, 0.130, 0.105, 0, 0.0), (1.13, 0.050, 0.045, 0, 0.008)]
HEAD_KEYS = [(0.00, 0.100, 0.070, 0, 0.0), (0.06, 0.190, 0.110, 0, 0.0), (0.22, 0.250, 0.130, 0, 0.0), (0.42, 0.265, 0.140, 0, 0.004),
             (0.58, 0.250, 0.146, 0, 0.010), (0.70, 0.225, 0.136, 0, 0.014), (0.78, 0.160, 0.100, 0, 0.016)]
HEAD_BACK = -0.14


def sliver(g, base, way, length, width=0.0034):
    """One long bristle as the game draws it: a three-sided sliver of four triangles (the baked one is a round hair)."""
    if g.hi:
        return g.spine("bristle", base, way, length, width * 0.8, around=3, bend=(0, 0, 0))
    base, way = Vector(base), Vector(way).normalized()
    side = way.orthogonal().normalized()
    other = way.cross(side)
    bm = bmesh.new()
    foot = [bm.verts.new(base - way * width + (side * math.cos(a) + other * math.sin(a)) * width) for a in (0.0, 2.094, 4.189)]
    point = bm.verts.new(base + way * length)
    for i in range(3):
        bm.faces.new((foot[i], foot[(i + 1) % 3], point))
    bm.faces.new((foot[2], foot[1], foot[0]))
    return oa._new_object("bristle", bm, (0.5, 0.5, 0.5), 30.0)


def sprinter(hi):
    g = Maker(hi, SPRINTER_FLAT, SPRINTER_MATERIALS)
    L = SPRINTER
    up = (0, 0, 1)

    def joint(piece, radius):
        g.add(g.ball("joint", (0, 0, 0), radius, around=7), "joint", piece)

    def bristles(piece, material, keys, fine, long, seed, start=0.10, end=0.95, lean=1.0, around_all=True):
        """Hairs standing off a limb. `fine` of them exist only to be baked: short, lying along the limb, so the
        pictures are hairy. `long` of them are real shapes in the game too: the long bristles a camel spider is
        fringed with, which is what makes its outline."""
        r = random.Random(seed)
        last = keys[-1][0]
        for k in range(fine + long):
            real_one = k >= fine
            y = last * r.uniform(start, end)
            a = r.uniform(0, 2 * math.pi) if around_all or not real_one else r.gauss(math.pi / 2, 1.3)
            rs, rf, _, of = real.along(keys, y)
            c, s_ = math.cos(a), math.sin(a)
            base = (rs * c * 0.96, y, of + rf * s_ * 0.96)
            if real_one:
                g.add(sliver(g, base, (c, 0.9 * lean, s_), r.uniform(0.05, 0.12)), "hair", piece)
            else:
                length = r.uniform(0.030, 0.060)
                g.add(g.spine("hair", base, (c * 0.42, 1.0 * lean, s_ * 0.42), length, r.uniform(0.0022, 0.0036), around=3), "hair", piece, only='hi')

    # ------------------------------------------------------------- Head: the plate, eyes, mouth, the stubs the limbs turn on
    head = g.skin("head", (0, HEAD_BACK, 0), (0, 1, 0), up, HEAD_KEYS, around=12, step=0.11, power=2.5)
    g.add(head, "head", "Head")
    # The domed plate over the jaw muscles, and the two swellings behind it.
    dome = g.ball("head plate", (0, 0.440, 0.085), 0.225, scale=(1.02, 1.12, 0.50), around=10)
    g.add(dome, "head", "Head")
    for side in (-1, 1):
        g.add(g.ball("back plate", (side * 0.085, 0.130, 0.080), 0.120, scale=(1.0, 1.25, 0.45), around=7), "head", "Head")
        # An eye each side of a small knob at the front edge of the plate.
        g.add(g.ball("eye", (side * 0.034, 0.668, 0.168), 0.027, around=6), "eye", "Head")
    g.add(g.ball("eye knob", (0, 0.660, 0.156), 0.040, scale=(1.3, 1.0, 0.8), around=6), "head", "Head")
    # The beak between the jaws, under them.
    g.add(g.skin("beak", (0, 0.560, -0.070), (0, 1, -0.15), up, [(0.0, 0.050, 0.040), (0.08, 0.040, 0.034), (0.17, 0.012, 0.012)], around=6, step=0.09), "joint", "Head")
    for i, (place, degrees) in enumerate(L["hips"]):
        for side in (-1, 1):
            p = Vector((side * place[0], place[1], place[2]))
            out = Vector((side * math.cos(math.radians(degrees)), math.sin(math.radians(degrees)), 0.0))
            r = 0.058 if i == 0 else 0.054
            g.add(g.skin("hip", p - out * 0.13, out, up, [(0.0, r * 1.5, r * 1.3), (0.06, r * 1.25, r * 1.2), (0.11, r * 1.05, r * 1.08), (0.15, r * 0.7, r * 0.72)],
                         around=8, step=0.05, power=2.0), "leg", "Head")
    bristles("Head", "head", [(0.0, 0.17, 0.11, 0, 0.0)] + [(k[0] + 0.001, k[1], k[2], 0, k[4]) for k in HEAD_KEYS[1:-1]], 0, 0, 20)
    r = random.Random(21)
    for k in range(220 + 70):
        # Hairs over the head: fine ones for the pictures, and a fringe of real ones round the plate.
        real_one = k >= 220
        y = r.uniform(0.02, 0.72)
        a = r.uniform(0.15, math.pi - 0.15)
        rs, rf, _, of = real.along(HEAD_KEYS, y)
        e = 2.0 / 2.5
        c, s_ = math.cos(a), math.sin(a)
        base = (rs * math.copysign(abs(c) ** e, c) * 0.97, HEAD_BACK + y, of + rf * abs(s_) ** e * 0.97)
        if real_one:
            g.add(sliver(g, base, (c, 0.7, s_ + 0.2), r.uniform(0.05, 0.11)), "hair", "Head")
        else:
            g.add(g.spine("hair", base, (c * 0.5, 1.0, s_ * 0.5), r.uniform(0.03, 0.06), r.uniform(0.0022, 0.0036), around=3), "hair", "Head", only='hi')

    # ------------------------------------------------------------- Abdomen: ten soft bands, hanging a little
    keys = ABDOMEN_KEYS
    if hi:
        ridged = []
        for i in range(140):
            t = 1.13 * i / 139
            rs, rf, _, of = real.along(ABDOMEN_KEYS, t)
            swell = 1.0 + 0.045 * abs(math.sin(math.pi * t / 0.110)) ** 0.6
            ridged.append((t, rs * swell, rf * swell, 0, of))
        keys = ridged
    g.add(g.skin("abdomen", (0, 0.060, 0.0), (0, -1, 0), up, keys, around=12, step=0.12, power=2.15), "abdomen", "Abdomen")
    r = random.Random(22)
    for k in range(420 + 110):
        real_one = k >= 420
        t = r.uniform(0.08, 1.08)
        a = r.uniform(0, 2 * math.pi) if not real_one else r.uniform(0.1, math.pi - 0.1)
        rs, rf, _, of = real.along(ABDOMEN_KEYS, t)
        c, s_ = math.cos(a), math.sin(a)
        # The skin's sideways is forward x axis = up x (0,-1,0): +x of the limb is the animal's +x.
        base = (rs * c * 0.97, 0.060 - t, of + rf * s_ * 0.97)
        if real_one:
            g.add(sliver(g, base, (c, -0.9, s_), r.uniform(0.05, 0.12)), "hair", "Abdomen")
        else:
            g.add(g.spine("hair", base, (c * 0.45, -1.0, s_ * 0.45), r.uniform(0.03, 0.065), r.uniform(0.0022, 0.0036), around=3), "hair", "Abdomen", only='hi')

    # ------------------------------------------------------------- legs: thigh, long shin, thin foot with two claws
    upper, lower, tip = L["leg"]
    k_upper = [(0.01, 0.034, 0.036, 0, 0.0), (0.06, 0.058, 0.062, 0, 0.0), (0.18, 0.062, 0.068, 0, 0.006), (0.38, 0.054, 0.058, 0, 0.006),
               (0.49, 0.046, 0.048, 0, 0.0), (upper, 0.032, 0.034, 0, 0.0)]
    joint("LegUpper", 0.052)
    g.add(g.skin("thigh", (0, 0, 0), (0, 1, 0), up, k_upper, around=8, step=0.10, power=2.2), "leg", "LegUpper")
    bristles("LegUpper", "leg", k_upper, 170, 30, 31)
    k_lower = [(0.01, 0.030, 0.032, 0, 0.0), (0.05, 0.046, 0.048, 0, 0.0), (0.24, 0.042, 0.044, 0, 0.0), (0.30, 0.032, 0.034, 0, 0.0),
               (0.35, 0.039, 0.041, 0, 0.0), (0.54, 0.031, 0.033, 0, -0.004), (lower, 0.022, 0.024, 0, 0.0)]
    joint("LegLower", 0.041)
    g.add(g.skin("shin", (0, 0, 0), (0, 1, 0), up, k_lower, around=8, step=0.10, power=2.2), "leg", "LegLower")
    bristles("LegLower", "leg", k_lower, 180, 32, 32)
    k_tip = [(0.01, 0.020, 0.022, 0, 0.0), (0.04, 0.028, 0.030, 0, 0.0), (0.18, 0.022, 0.024, 0, -0.004), (0.29, 0.016, 0.017, 0, -0.012), (0.335, 0.010, 0.010, 0, -0.018)]
    joint("LegTip", 0.026)
    g.add(g.skin("foot", (0, 0, 0), (0, 1, 0), up, k_tip, around=6, step=0.11, power=2.1), "foot", "LegTip")
    for side in (-1, 1):
        g.add(g.spine("claw", (side * 0.006, 0.330, -0.018), (side * 0.35, 1.0, -0.5), 0.040, 0.007, around=3, bend=(0, 0, -0.5)), "foot", "LegTip")
    bristles("LegTip", "foot", k_tip, 120, 18, 33, end=0.85)

    # ------------------------------------------------------------- feeling arms: thicker than a leg, a blunt pad at the end
    a_upper, a_lower, a_tip = L["arm"]
    k = [(0.01, 0.036, 0.038, 0, 0.0), (0.06, 0.060, 0.064, 0, 0.0), (0.20, 0.062, 0.068, 0, 0.004), (0.42, 0.054, 0.058, 0, 0.004), (a_upper, 0.036, 0.038, 0, 0.0)]
    joint("PalpUpper", 0.054)
    g.add(g.skin("palp upper", (0, 0, 0), (0, 1, 0), up, k, around=8, step=0.10, power=2.2), "palp", "PalpUpper")
    bristles("PalpUpper", "palp", k, 170, 30, 41)
    k = [(0.01, 0.034, 0.036, 0, 0.0), (0.06, 0.050, 0.054, 0, 0.0), (0.28, 0.050, 0.054, 0, 0.0), (0.47, 0.045, 0.047, 0, 0.0), (a_lower, 0.032, 0.034, 0, 0.0)]
    joint("PalpLower", 0.045)
    g.add(g.skin("palp lower", (0, 0, 0), (0, 1, 0), up, k, around=8, step=0.10, power=2.2), "palp", "PalpLower")
    bristles("PalpLower", "palp", k, 180, 32, 42)
    k = [(0.01, 0.030, 0.032, 0, 0.0), (0.05, 0.040, 0.042, 0, 0.0), (0.20, 0.038, 0.040, 0, 0.0), (0.27, 0.037, 0.039, 0, 0.0), (0.30, 0.031, 0.033, 0, 0.0), (0.318, 0.017, 0.018, 0, 0.0)]
    joint("PalpTip", 0.037)
    g.add(g.skin("palp tip", (0, 0, 0), (0, 1, 0), up, k, around=8, step=0.10, power=2.2), "palp", "PalpTip")
    bristles("PalpTip", "palp", k, 130, 26, 43, end=0.9)

    # ------------------------------------------------------------- jaws: a swollen base, a fixed upper finger, a moving lower one
    length = L["jaw_length"]
    k_jaw = [(-0.04, 0.050, 0.060, 0, 0.0), (0.03, 0.090, 0.116, 0, 0.0), (0.15, 0.100, 0.130, 0, 0.004), (0.26, 0.080, 0.094, 0, 0.026),
             (0.35, 0.048, 0.050, 0, 0.054), (0.43, 0.029, 0.029, 0, 0.052), (0.49, 0.014, 0.015, 0, 0.032), (length, 0.004, 0.004, 0, 0.006)]
    g.add(g.skin("jaw", (0, 0, 0), (0, 1, 0), up, k_jaw, around=10, step=0.075, power=2.3), "jaw", "Jaw")
    hinge = Vector(L["hinge"])
    g.add(g.ball("jaw hinge", hinge, 0.040, scale=(1.6, 1.0, 1.0), around=6), "joint", "Jaw")
    for i in range(7):
        # Teeth along the under side of the fixed finger: two big ones, then small.
        y = 0.255 + 0.033 * i
        _, rf, _, of = real.along(k_jaw, y)
        size = (0.036, 0.026, 0.030, 0.019, 0.016, 0.013, 0.010)[i]
        g.add(g.spine("tooth", (0, y, of - rf + 0.004), (0, 0.15, -1), size, size * 0.42, around=4), "jaw", "Jaw", only=None if i < 3 else 'hi')
    bristles("Jaw", "jaw", k_jaw[:4], 150, 26, 51, start=0.0, end=0.95, around_all=False)
    finger = L["finger"]
    k_finger = [(-0.02, 0.034, 0.036, 0, 0.0), (0.04, 0.054, 0.060, 0, -0.005), (0.14, 0.050, 0.052, 0, -0.014), (0.24, 0.031, 0.031, 0, -0.012),
                (0.31, 0.015, 0.015, 0, 0.007), (finger, 0.004, 0.004, 0, 0.026)]
    g.add(g.skin("jaw finger", (0, 0, 0), (0, 1, 0), up, k_finger, around=8, step=0.075, power=2.3), "jawfinger", "JawFinger")
    for i in range(6):
        y = 0.100 + 0.036 * i
        _, rf, _, of = real.along(k_finger, y)
        size = (0.031, 0.024, 0.026, 0.017, 0.013, 0.010)[i]
        g.add(g.spine("tooth", (0, y, of + rf - 0.004), (0, 0.15, 1), size, size * 0.42, around=4), "jawfinger", "JawFinger", only=None if i < 3 else 'hi')

    # ------------------------------------------------------------- what is left when something comes off
    r_ = L["stump"]
    meat = g.ball("wound meat", (0, 0.010, 0), r_ * 0.86, scale=(1.0, 0.55, 1.0), around=9)
    real.displace(meat, lambda p: 0.010 * noise.noise(p * 28.0) + 0.006 * noise.noise(p * 61.0))
    g.add(meat, "flesh", "Stump")
    ragged = [(-0.050 + 0.085 * i / 8, r_ * (0.98 + 0.07 * i / 8), r_ * (1.02 + 0.07 * i / 8)) for i in range(9)]
    collar = g.skin("wound skin", (0, 0, 0), (0, 1, 0), up, [ragged[0], ragged[4], ragged[8]] if not hi else ragged, around=10, step=0.04, power=2.25)
    bm = bmesh.new()
    bm.from_mesh(collar.data)
    for v in bm.verts:
        if v.co.y > 0.0:
            a = math.atan2(v.co.z, v.co.x)
            v.co.y += (v.co.y / 0.035) * 0.020 * (noise.noise(Vector((math.cos(a) * 3.1, math.sin(a) * 3.1, 8.0))) + 0.35 * math.sin(a * 5.0))
            v.co.y = max(v.co.y, 0.004)
    bm.to_mesh(collar.data)
    bm.free()
    g.add(collar, "leg", "Stump")
    for k, (size, squash) in enumerate((((0.13, (1.0, 0.8, 0.6))), ((0.10, (0.8, 1.0, 0.7))))):
        piece = "Meat%d" % k
        lump = g.ball("meat", (0, 0, 0), size, scale=squash, around=10)
        real.displace(lump, lambda p, k=k: 0.020 * noise.noise(p * 9.0 + Vector((k * 5.0, 3.0, 0))) + 0.009 * noise.noise(p * 23.0))
        g.add(lump, "flesh", piece)
        skin = real.ball_patch("skin bit", (0, 0, 0), (0.2, 0.1, 1.0), (0, 1, 0), size * squash[2] * 1.02, (0, 58), 0, 360, (0.5, 0.5, 0.5),
                               thickness=0.010, n=40 if hi else 9, m=14 if hi else 3)
        skin.data.transform(Matrix.Diagonal((squash[0] / squash[2] * 0.96, squash[1] / squash[2] * 0.96, 1.0, 1.0)))
        g.add(skin, "abdomen", piece)
    return g


def sprinter_sockets():
    L = SPRINTER
    out = []
    for i, (place, degrees) in enumerate(L["hips"]):
        for side, tag in ((1, "R"), (-1, "L")):
            p = Vector((side * place[0], place[1], place[2]))
            way = Vector((side * math.cos(math.radians(degrees)), math.sin(math.radians(degrees)), 0.0))
            out.append(oa.socket("Hip%s%d" % (tag, i), p))
            out.append(oa.socket("Out%s%d" % (tag, i), p + way))
    for k, piece in enumerate(L["pieces"]["leg"]):
        out.append(oa.socket(piece + "End", (0, L["leg"][k], 0)))
    for k, piece in enumerate(L["pieces"]["arm"]):
        out.append(oa.socket(piece + "End", (0, L["arm"][k], 0)))
    out.append(oa.socket("JawHinge", L["hinge"]))
    out.append(oa.socket("FingerEnd", (0, L["finger"], 0.022)))
    out.append(oa.socket("JawR", L["jaw"]))
    out.append(oa.socket("JawEnd", (0, L["jaw_length"], 0.004)))
    out.append(oa.socket("StumpRim", (L["stump"], 0, 0)))
    return out


# ====================================================================== the Roller (giant isopod)

# It starts from a real thing: the giant isopod, Bathynomus giganteus, at about six times life size. A deep-sea
# woodlouse: a broad head shield with two large triangular eyes of many facets set wide on its front corners and two
# long feelers; seven smooth, hard, overlapping plates over the back, each with a paler rear edge; a flat tail fan
# with short spines along its rear edge and a paddle either side; under the plates a pale soft body and fourteen
# jointed walking legs ending in a claw. Dusky lilac-grey above, cream underneath. It rolls into a ball.
# The body is a chain of nine links; each link's piece is modelled round the middle of its own length on the line of
# the spine, +Y toward the head, +Z its back. Held by the rule test RollerModelFile_MatchesTheLayout against
# Creatures/RollerLayout.cs.
ROLLER = {
    "head": 0.30, "seg": 0.24, "tail": 0.42,            # lengths along the spine
    "back": 0.32, "belly": 0.30,                        # a plate's top above the spine, the soft body's depth below it
    "plate_half": 0.56, "seg_half": 0.44, "overlap": 0.16, "skirt": 0.26,
    "head_half": 0.40, "head_top": 0.24, "tail_half": 0.42, "tail_top": 0.20,
    "hip": (0.34, 0.0, -0.20),
    "leg": 0.36,
    "stump": 0.05,
}
MAUVE, BONE, SOOT = (0.25, 0.20, 0.24), (0.60, 0.54, 0.50), (0.045, 0.038, 0.050)
ROLLER_FLAT = {"plate": ((0.44, 0.38, 0.43), 0.42), "margin": ((0.64, 0.58, 0.55), 0.40), "head": ((0.42, 0.36, 0.41), 0.42),
               "under": ((0.68, 0.60, 0.54), 0.25), "leg": ((0.66, 0.58, 0.52), 0.35), "tail": ((0.42, 0.36, 0.41), 0.42),
               "joint": ((0.50, 0.40, 0.36), 0.2), "flesh": ((0.78, 0.50, 0.44), 0.6), "eye": ((0.07, 0.06, 0.08), 0.85)}
ROLLER_MATERIALS = {
    "plate": lambda: mats.chitin("plate", MAUVE, BONE, SOOT, under=(0.58, 0.50, 0.46), seed=21.0, bumps=0.30, growth=0.25, mottle=0.45, gloss=0.50),
    "margin": lambda: mats.chitin("margin", (0.40, 0.34, 0.34), (0.60, 0.54, 0.50), SOOT, under=(0.58, 0.50, 0.46), seed=22.0, bumps=0.15, growth=0.15, mottle=0.3, gloss=0.52),
    "head": lambda: mats.chitin("head", MAUVE, BONE, SOOT, under=(0.58, 0.50, 0.46), seed=23.0, bumps=0.45, growth=0.2, mottle=0.5, gloss=0.48),
    "tail": lambda: mats.chitin("tail", MAUVE, BONE, SOOT, under=(0.58, 0.50, 0.46), seed=24.0, bumps=0.40, growth=0.3, mottle=0.5, gloss=0.46),
    "under": lambda: mats.chitin("under", (0.36, 0.28, 0.26), (0.52, 0.45, 0.40), (0.12, 0.07, 0.06), seed=25.0, bumps=0.2, growth=0.0, mottle=0.5, gloss=0.30),
    "leg": lambda: mats.chitin("leg", (0.21, 0.155, 0.15), (0.42, 0.35, 0.31), (0.07, 0.04, 0.04), seed=26.0, bumps=0.3, growth=0.05, mottle=0.6, size=0.6,
                               tip=(1, 0.27, 0.36, (0.07, 0.04, 0.035)), gloss=0.40),
    "joint": lambda: mats.membrane("joint", (0.36, 0.25, 0.22), seed=27.0),
    "flesh": lambda: mats.flesh("flesh", pale=(0.74, 0.64, 0.58), red=(0.38, 0.08, 0.06), seed=28.0),
    "eye": lambda: mats.eye("eye", colour=(0.016, 0.012, 0.022), glint=(0.09, 0.06, 0.13)),
}


def arch(name, rows, thickness, around, tuck=35.0):
    """A curved roof plate with thickness. rows: (y, half width, top, skirt depth), front to back; the cross-section
    is half an oval over the spine with a skirt each side that tucks in under it."""
    def profile(half, top, skirt, inset):
        points = []
        for i in range(around + 1):
            a = math.radians(-tuck + (180.0 + 2 * tuck) * i / around)
            x = (half - inset) * math.cos(a)
            z = (top - inset) * math.sin(a) if 0 <= a <= math.pi else (skirt + inset * 0.3) * math.sin(a) / math.sin(math.radians(tuck))
            points.append((x, z))
        return points
    bm = bmesh.new()
    rings = []
    for y, half, top, skirt in rows:
        outer = [bm.verts.new((x, y, z)) for x, z in profile(half, top, skirt, 0.0)]
        inner = [bm.verts.new((x, y, z)) for x, z in profile(half, top, skirt, thickness)]
        rings.append((outer, inner))
    for (o0, i0), (o1, i1) in zip(rings, rings[1:]):
        for k in range(around):
            bm.faces.new((o0[k], o0[k + 1], o1[k + 1], o1[k]))
            bm.faces.new((i0[k + 1], i0[k], i1[k], i1[k + 1]))
        for k in (0, around):
            bm.faces.new((o0[k], o1[k], i1[k], i0[k]))
    for outer, inner in (rings[0], rings[-1]):
        for k in range(around):
            bm.faces.new((outer[k], outer[k + 1], inner[k + 1], inner[k]))
    return oa._new_object(name, bm, (0.5, 0.5, 0.5), 50.0)


def roller(hi):
    g = Maker(hi, ROLLER_FLAT, ROLLER_MATERIALS)
    L = ROLLER
    up = (0, 0, 1)
    seg, back, belly = L["seg"], L["back"], L["belly"]

    # ------------------------------------------------------------- Plate: an arch of armour, its rear edge over the next one
    front, rear = seg * 0.5, -seg * 0.5 - L["overlap"]
    half = L["plate_half"]
    # Front to back: it starts narrow and low, tucked under the plate before it, and is full size where that one ends.
    shape = [(1.00, 0.860, 0.860, 0.80), (0.80, 0.905, 0.905, 0.92), (0.60, 0.950, 0.950, 1.0), (0.42, 0.992, 0.992, 1.0), (0.15, 1.000, 1.000, 1.0), (0.00, 0.988, 0.980, 1.0)]
    rows = []
    cuts = 28 if hi else 0
    if hi:
        shape = [(1.0 - i / cuts,) + tuple(real.along([(1.0 - k[0],) + k[1:] for k in shape], i / cuts)) for i in range(cuts + 1)]
    for t, wide, tall, skirt in shape:
        rows.append((rear + (front - rear) * t, half * wide, back * tall, L["skirt"] * skirt))
    plate = arch("plate", rows, 0.030, 56 if hi else 12)
    if hi:
        # Fine growth lines across the plate and a slow unevenness, for the bake.
        real.displace(plate, lambda p: 0.0016 * math.sin(p.y * 190.0) + 0.004 * noise.noise(p * 7.0)
                      + 0.010 * math.exp(-(p.x / 0.05) ** 2) * max(0.0, p.z / back))        # a low keel down the middle of the back
    g.add(plate, "plate", "Plate", second=("margin", lambda c, n: c.y < rear + 0.050))

    # ------------------------------------------------------------- Seg: the soft body under a plate, with a hip each side
    # From 0.26 above the spine (under the plate) to the belly below it.
    tall, mid = (0.26 + belly) * 0.5, (0.26 - belly) * 0.5
    keys = [(0.00, 0.39, tall * 0.84, 0, mid), (0.035, 0.44, tall, 0, mid), (seg - 0.005, 0.44, tall, 0, mid), (seg + 0.03, 0.39, tall * 0.84, 0, mid)]
    if hi:
        ridged = []
        for i in range(60):
            t = (seg + 0.03) * i / 59
            rs, rf, _, of = real.along(keys, t)
            fold = 1.0 + 0.018 * math.sin(math.pi * 2 * t / 0.045)
            ridged.append((t, rs * fold, rf * fold, 0, of))
        keys = ridged
    g.add(g.skin("seg", (0, -seg * 0.5 - 0.015, 0.0), (0, 1, 0), up, keys, around=12, step=0.10, power=2.6), "under", "Seg")
    hip = Vector(L["hip"])
    for side in (-1, 1):
        g.add(g.ball("hip", (side * hip.x, 0, hip.z), 0.060, scale=(1.0, 1.1, 0.9), around=6), "joint", "Seg")
        # The swimming flap under each side (only its shape in the pictures).
        g.add(g.ball("flap", (side * 0.16, -0.02, -belly + 0.012), 0.10, scale=(1.3, 1.0, 0.22), around=8), "under", "Seg", only='hi')

    # ------------------------------------------------------------- Head: a shield, two eyes of many facets, feelers
    hl = L["head"]
    hk = [(0.00, 0.385, 0.185, 0, 0.030), (0.05, 0.400, 0.205, 0, 0.030), (0.16, 0.385, 0.200, 0, 0.026), (0.24, 0.300, 0.165, 0, 0.012),
          (0.295, 0.170, 0.095, 0, -0.010), (0.31, 0.070, 0.040, 0, -0.020)]
    g.add(g.skin("head", (0, -hl * 0.5, 0.0), (0, 1, 0), up, hk, around=14, step=0.07, power=2.4), "head", "Head")
    brow = arch("brow", [(hl * 0.5 - 0.02, 0.30, 0.215, 0.02), (0.02, 0.395, 0.236, 0.05), (-hl * 0.5 - 0.03, 0.400, 0.238, 0.08)], 0.026, 40 if hi else 10, tuck=18.0)
    g.add(brow, "head", "Head", second=("margin", lambda c, n: c.y < -hl * 0.5 + 0.015))
    for side in (-1, 1):
        g.add(g.ball("eye", (side * 0.275, 0.085, 0.095), 0.092, scale=(0.80, 1.45, 0.62), around=8), "eye", "Head")
        g.add(g.ball("eye rim", (side * 0.262, 0.080, 0.088), 0.098, scale=(0.80, 1.5, 0.60), around=8), "head", "Head", only='hi')
        # The long feeler and the short one, swept forward and out.
        g.add(g.spine("feeler", (side * 0.135, 0.125, -0.020), (side * 0.75, 1.0, 0.10), 0.52, 0.020, around=5, bend=(side * 0.55, -0.25, -0.12)), "leg", "Head")
        g.add(g.spine("short feeler", (side * 0.060, 0.140, -0.035), (side * 0.25, 1.0, -0.05), 0.20, 0.014, around=4, bend=(side * 0.2, 0, -0.2)), "leg", "Head")
        # Jaw plates under the front.
        g.add(g.ball("mouth", (side * 0.055, 0.085, -0.130), 0.062, scale=(0.9, 1.3, 0.7), around=6), "joint", "Head")

    # ------------------------------------------------------------- Tail: a flat fan with a spined rear edge and a paddle each side
    tl = L["tail"]
    tk = [(0.00, 0.380, 0.165, 0, 0.025), (0.06, 0.405, 0.172, 0, 0.022), (0.20, 0.375, 0.130, 0, 0.010), (0.33, 0.275, 0.070, 0, 0.0),
          (0.40, 0.165, 0.034, 0, -0.006), (0.418, 0.070, 0.018, 0, -0.008)]
    g.add(g.skin("tail", (0, tl * 0.5, 0.0), (0, -1, 0), up, tk, around=14, step=0.08, power=2.3), "tail", "Tail")
    g.add(g.ball("tail ridge", (0, -0.02, 0.105), 0.07, scale=(0.6, 2.4, 0.5), around=7), "tail", "Tail")
    for side in (-1, 1):
        g.add(g.ball("paddle", (side * 0.335, -0.065, -0.012), 0.105, scale=(0.72, 1.55, 0.20), around=8), "tail", "Tail")
    for i in range(7):
        x = (i - 3) * 0.062
        y = tl * 0.5 - 0.405 + 0.012 * abs(i - 3) ** 1.6
        g.add(g.spine("tail spine", (x, y + 0.03, -0.004), (x * 0.6, -1, 0.05), 0.055 if i % 2 == 0 else 0.038, 0.014, around=4), "margin", "Tail", only=None if i % 2 == 0 else 'hi')

    # ------------------------------------------------------------- Leg: three joints and a claw, arched; the same either side
    leg = L["leg"]
    lk = [(0.00, 0.040, 0.044, 0, 0.0), (0.03, 0.052, 0.056, 0, 0.008), (0.11, 0.046, 0.050, 0, 0.034), (0.125, 0.036, 0.038, 0, 0.037),
          (0.14, 0.042, 0.044, 0, 0.038), (0.22, 0.033, 0.035, 0, 0.026), (0.235, 0.025, 0.026, 0, 0.020), (0.25, 0.028, 0.029, 0, 0.012),
          (0.31, 0.017, 0.018, 0, -0.028), (leg, 0.004, 0.004, 0, -0.070)]
    g.add(g.skin("leg", (0, 0, 0), (0, 1, 0), up, lk, around=7, step=0.05, power=2.1), "leg", "Leg")
    g.add(g.ball("leg joint", (0, 0, 0), 0.040, around=6), "joint", "Leg")
    r = random.Random(31)
    for k in range(18):
        # Short thorns along the under side of the leg (three of them real, the rest in the pictures).
        y = r.uniform(0.05, 0.30)
        rs, rf, _, of = real.along(lk, y)
        g.add(g.spine("thorn", (r.uniform(-0.4, 0.4) * rs, y, of - rf * 0.9), (0, 0.5, -1), r.uniform(0.012, 0.022), 0.005, around=3), "leg", "Leg", only='hi')

    # ------------------------------------------------------------- what is left when something comes off
    r_ = L["stump"]
    meat = g.ball("wound meat", (0, 0.008, 0), r_ * 0.86, scale=(1.0, 0.55, 1.0), around=9)
    real.displace(meat, lambda p: 0.007 * noise.noise(p * 40.0) + 0.004 * noise.noise(p * 90.0))
    g.add(meat, "flesh", "Stump")
    ragged = [(-0.034 + 0.058 * i / 8, r_ * (0.98 + 0.07 * i / 8), r_ * (1.02 + 0.07 * i / 8)) for i in range(9)]
    collar = g.skin("wound skin", (0, 0, 0), (0, 1, 0), up, [ragged[0], ragged[4], ragged[8]] if not hi else ragged, around=10, step=0.03, power=2.25)
    bm = bmesh.new()
    bm.from_mesh(collar.data)
    for v in bm.verts:
        if v.co.y > 0.0:
            a = math.atan2(v.co.z, v.co.x)
            v.co.y += (v.co.y / 0.024) * 0.014 * (noise.noise(Vector((math.cos(a) * 3.1, math.sin(a) * 3.1, 8.0))) + 0.35 * math.sin(a * 5.0))
            v.co.y = max(v.co.y, 0.003)
    bm.to_mesh(collar.data)
    bm.free()
    g.add(collar, "under", "Stump")
    for k, (size, squash) in enumerate((((0.15, (1.0, 0.8, 0.6))), ((0.12, (0.8, 1.0, 0.7))))):
        piece = "Meat%d" % k
        lump = g.ball("meat", (0, 0, 0), size, scale=squash, around=10)
        real.displace(lump, lambda p, k=k: 0.022 * noise.noise(p * 8.0 + Vector((k * 5.0, 3.0, 0))) + 0.010 * noise.noise(p * 21.0))
        g.add(lump, "flesh", piece)
        skin = real.ball_patch("skin bit", (0, 0, 0), (0.2, 0.1, 1.0), (0, 1, 0), size * squash[2] * 1.02, (0, 58), 0, 360, (0.5, 0.5, 0.5),
                               thickness=0.010, n=40 if hi else 9, m=14 if hi else 3)
        skin.data.transform(Matrix.Diagonal((squash[0] / squash[2] * 0.96, squash[1] / squash[2] * 0.96, 1.0, 1.0)))
        g.add(skin, "under", piece)
    return g


def roller_sockets():
    L = ROLLER
    return [oa.socket("HeadEnd", (0, L["head"] * 0.5, 0)), oa.socket("SegEnd", (0, L["seg"] * 0.5, 0)), oa.socket("TailEnd", (0, -L["tail"] * 0.5, 0)),
            oa.socket("PlateTop", (0, 0, L["back"])), oa.socket("PlateSide", (L["plate_half"], 0, 0)), oa.socket("PlateRear", (0, -L["seg"] * 0.5 - L["overlap"], -L["skirt"])),
            oa.socket("SegSide", (L["seg_half"], 0, -L["belly"])), oa.socket("HeadCorner", (L["head_half"], 0, L["head_top"])),
            oa.socket("TailCorner", (L["tail_half"], 0, L["tail_top"])), oa.socket("HipR", L["hip"]), oa.socket("LegEnd", (0, L["leg"], 0)),
            oa.socket("StumpRim", (L["stump"], 0, 0))]


# ====================================================================== the Cave hunter (giant centipede)

# It starts from a real thing: the Amazonian giant centipede, Scolopendra gigantea, at about twenty times life
# size. A flat body of many alike segments, each under a glossy mahogany back plate with two fine grooves down it
# and a paler rear edge, soft pleated skin along the sides, a pale plate underneath, and one pair of amber legs
# that darken to a claw; a flat rust-red head shield with a cluster of four small eyes each side, two long beaded
# feelers, and under the head the two heavy curved venom fangs with black points; the last pair of legs long,
# thick and thorny, held out behind.
# The body is a chain: each piece is modelled round its own node on the line of the spine, +Y toward the head, +Z
# its back. A leg, a feeler and a tail leg are modelled from their roots, +Y along, bending only toward +Z, so they
# are the same either side. A fang is the right one, curving in toward -X, the same above as below: the left one is
# it rolled half a turn. Held by the rule test HunterModelFile_MatchesTheLayout against Creatures/HunterLayout.cs.
HUNTER = {
    "spacing": 0.34,                                    # from one node to the next
    "head": 0.46, "head_half": 0.23,                    # the head runs from spacing / 2 behind its node to the rest in front
    "seg_half": 0.21, "seg_top": 0.09, "seg_bottom": 0.09,
    "hip": (0.19, 0.0, -0.04),
    "leg": 0.52, "leg_radius": 0.03,
    "fang_root": (0.10, 0.16, -0.05), "fang": 0.30,
    "feeler_root": (0.10, 0.24, 0.05), "feeler": 0.95,
    "tail_root": (0.10, -0.14, 0.0), "tail_leg": 0.85,
    "stump": 0.05,
}
MAHOGANY, RUSSET, PITCH = (0.066, 0.019, 0.013), (0.34, 0.15, 0.07), (0.022, 0.010, 0.008)
AMBER = (0.36, 0.20, 0.045)
HUNTER_FLAT = {"plate": ((0.30, 0.12, 0.09), 0.55), "margin": ((0.50, 0.28, 0.16), 0.5), "head": ((0.50, 0.17, 0.08), 0.55),
               "under": ((0.60, 0.46, 0.30), 0.3), "side": ((0.42, 0.26, 0.18), 0.2), "leg": ((0.70, 0.48, 0.16), 0.45),
               "tail": ((0.52, 0.26, 0.10), 0.45), "fang": ((0.42, 0.14, 0.07), 0.6), "feeler": ((0.62, 0.38, 0.14), 0.4),
               "joint": ((0.46, 0.30, 0.20), 0.2), "flesh": ((0.78, 0.50, 0.44), 0.6), "eye": ((0.05, 0.04, 0.04), 0.85)}
HUNTER_MATERIALS = {
    "plate": lambda: mats.chitin("plate", MAHOGANY, RUSSET, PITCH, under=(0.30, 0.20, 0.12), seed=41.0, bumps=0.12, growth=0.0, mottle=0.22, gloss=0.62),
    "margin": lambda: mats.chitin("margin", (0.21, 0.085, 0.040), (0.40, 0.22, 0.11), PITCH, seed=42.0, bumps=0.08, growth=0.0, mottle=0.3, gloss=0.6),
    "head": lambda: mats.chitin("head", (0.24, 0.055, 0.022), (0.42, 0.18, 0.08), PITCH, under=(0.34, 0.20, 0.10), seed=43.0, bumps=0.2, growth=0.0, mottle=0.3, gloss=0.6),
    "under": lambda: mats.chitin("under", (0.30, 0.20, 0.10), (0.44, 0.34, 0.20), (0.10, 0.05, 0.03), seed=44.0, bumps=0.12, growth=0.0, mottle=0.4, gloss=0.34),
    "side": lambda: mats.membrane("side", (0.24, 0.13, 0.085), seed=45.0, size=3.0),
    "leg": lambda: mats.chitin("leg", AMBER, (0.50, 0.36, 0.14), (0.10, 0.04, 0.02), seed=46.0, bumps=0.15, growth=0.0, mottle=0.4, size=0.6,
                               tip=(1, 0.36, 0.50, (0.035, 0.016, 0.010)), gloss=0.5),
    "tail": lambda: mats.chitin("tail", (0.26, 0.10, 0.035), (0.44, 0.26, 0.10), (0.06, 0.025, 0.015), seed=47.0, bumps=0.3, growth=0.0, mottle=0.4, size=0.7,
                                tip=(1, 0.62, 0.84, (0.035, 0.016, 0.010)), gloss=0.5),
    "fang": lambda: mats.chitin("fang", (0.22, 0.05, 0.02), (0.40, 0.18, 0.08), PITCH, seed=48.0, bumps=0.08, growth=0.0, mottle=0.2, size=0.6,
                                tip=(1, 0.12, 0.25, (0.012, 0.008, 0.008)), gloss=0.7),
    "feeler": lambda: mats.chitin("feeler", (0.32, 0.17, 0.045), (0.48, 0.32, 0.12), (0.10, 0.04, 0.02), seed=49.0, bumps=0.1, growth=0.0, mottle=0.3, size=0.5,
                                  tip=(1, 0.55, 0.95, (0.10, 0.045, 0.02)), gloss=0.45),
    "joint": lambda: mats.membrane("joint", (0.26, 0.15, 0.09), seed=50.0),
    "flesh": lambda: mats.flesh("flesh", pale=(0.70, 0.62, 0.46), red=(0.36, 0.09, 0.05), seed=51.0),
    "eye": lambda: mats.eye("eye", colour=(0.010, 0.008, 0.008), glint=(0.10, 0.05, 0.03)),
}


def leftovers(g, radius, skin):
    """The wound (`Stump`, modelled at this radius, +Y out of it) and the two lumps that come out of a burst."""
    hi, up = g.hi, (0, 0, 1)
    meat = g.ball("wound meat", (0, 0.008, 0), radius * 0.86, scale=(1.0, 0.55, 1.0), around=9)
    real.displace(meat, lambda p: 0.007 * noise.noise(p * 40.0) + 0.004 * noise.noise(p * 90.0))
    g.add(meat, "flesh", "Stump")
    ragged = [(-0.034 + 0.058 * i / 8, radius * (0.98 + 0.07 * i / 8), radius * (1.02 + 0.07 * i / 8)) for i in range(9)]
    collar = g.skin("wound skin", (0, 0, 0), (0, 1, 0), up, [ragged[0], ragged[4], ragged[8]] if not hi else ragged, around=10, step=0.03, power=2.25)
    bm = bmesh.new()
    bm.from_mesh(collar.data)
    for v in bm.verts:
        if v.co.y > 0.0:
            a = math.atan2(v.co.z, v.co.x)
            v.co.y += (v.co.y / 0.024) * 0.014 * (noise.noise(Vector((math.cos(a) * 3.1, math.sin(a) * 3.1, 8.0))) + 0.35 * math.sin(a * 5.0))
            v.co.y = max(v.co.y, 0.003)
    bm.to_mesh(collar.data)
    bm.free()
    g.add(collar, skin, "Stump")
    for k, (size, squash) in enumerate((((0.12, (1.0, 0.8, 0.6))), ((0.095, (0.8, 1.0, 0.7))))):
        piece = "Meat%d" % k
        lump = g.ball("meat", (0, 0, 0), size, scale=squash, around=10)
        real.displace(lump, lambda p, k=k: 0.018 * noise.noise(p * 9.0 + Vector((k * 5.0, 3.0, 0))) + 0.008 * noise.noise(p * 24.0))
        g.add(lump, "flesh", piece)
        bit = real.ball_patch("skin bit", (0, 0, 0), (0.2, 0.1, 1.0), (0, 1, 0), size * squash[2] * 1.02, (0, 58), 0, 360, (0.5, 0.5, 0.5),
                              thickness=0.008, n=40 if hi else 9, m=14 if hi else 3)
        bit.data.transform(Matrix.Diagonal((squash[0] / squash[2] * 0.96, squash[1] / squash[2] * 0.96, 1.0, 1.0)))
        g.add(bit, skin, piece)


def hunter(hi):
    g = Maker(hi, HUNTER_FLAT, HUNTER_MATERIALS)
    L = HUNTER
    up = (0, 0, 1)
    half, top, bottom = L["seg_half"], L["seg_top"], L["seg_bottom"]
    sp = L["spacing"]

    # ------------------------------------------------------------- Seg: a back plate, pleated sides, a plate underneath, two hips
    front, rear = sp * 0.5, -sp * 0.5 - 0.035
    # Front to back: narrow and low where it tucks under the plate before it, widest two thirds of the way back,
    # drawn in a little at its rear corners.
    shape = [(1.00, 0.80, 0.80, 0.5), (0.86, 0.90, 0.92, 0.8), (0.60, 0.97, 1.0, 1.0), (0.30, 1.00, 1.0, 1.0), (0.08, 0.985, 0.97, 1.0), (0.00, 0.93, 0.90, 0.9)]
    cuts = 26
    if hi:
        shape = [(1.0 - i / cuts,) + tuple(real.along([(1.0 - k[0],) + k[1:] for k in shape], i / cuts)) for i in range(cuts + 1)]
    rows = [(rear + (front - rear) * t, half * wide, top * tall, 0.030 * skirt) for t, wide, tall, skirt in shape]
    plate = arch("plate", rows, 0.014, 48 if hi else 10, tuck=22.0)
    if hi:
        # The two fine grooves a centipede's back plates carry, a shallow dish between them, and a slow unevenness.
        real.displace(plate, lambda p: (-0.0045 * (math.exp(-((abs(p.x) - 0.062) / 0.007) ** 2)) + 0.003 * noise.noise(p * 9.0)
                                        + 0.0010 * math.sin(p.y * 240.0)) * max(0.0, p.z / top))
    g.add(plate, "plate", "Seg", second=("margin", lambda c, n: c.y < rear + 0.030))
    # The soft body between the plates: pleated along its sides.
    keys = [(0.00, half * 0.80, 0.066, 0, -0.012), (0.04, half * 0.90, 0.078, 0, -0.012), (sp - 0.04, half * 0.90, 0.078, 0, -0.012), (sp, half * 0.80, 0.066, 0, -0.012)]
    if hi:
        pleated = []
        for i in range(48):
            t = sp * i / 47
            rs, rf, _, of = real.along(keys, t)
            fold = 1.0 + 0.016 * math.sin(math.pi * 2 * t / 0.068)
            pleated.append((t, rs * fold, rf, 0, of))
        keys = pleated
    g.add(g.skin("side", (0, -sp * 0.5, 0.0), (0, 1, 0), up, keys, around=10, step=0.11, power=2.5), "side", "Seg")
    # The soft joint to the segment behind: a ball over the joint, so a bend never opens a gap.
    g.add(g.ball("link", (0, -sp * 0.5, -0.010), 0.082, scale=(2.05, 1.0, 0.86), around=8), "side", "Seg")
    # The plate underneath: flat, paler.
    g.add(g.ball("belly plate", (0, -0.005, -bottom + 0.022), 0.15, scale=(0.92, 0.98, 0.15), around=8), "under", "Seg")
    hip = Vector(L["hip"])
    for side in (-1, 1):
        g.add(g.ball("hip", (side * (hip.x - 0.015), 0, hip.z), 0.045, scale=(1.1, 1.2, 0.85), around=6), "joint", "Seg")
        # The breathing hole above each hip (only in the pictures).
        g.add(g.ball("spiracle", (side * (half * 0.90), 0.06, 0.012), 0.012, scale=(0.5, 1.5, 0.8), around=5), "eye", "Seg", only='hi')

    # ------------------------------------------------------------- Head: a flat shield, eye clusters, the plate the fangs hang from
    hl, hh = L["head"], L["head_half"]
    back = -sp * 0.5
    hk = [(0.00, hh * 0.80, 0.062, 0, 0.004), (0.05, hh * 0.96, 0.076, 0, 0.004), (0.18, hh, 0.080, 0, 0.004), (0.30, hh * 0.90, 0.070, 0, 0.0),
          (0.39, hh * 0.66, 0.052, 0, -0.006), (0.445, hh * 0.36, 0.030, 0, -0.012), (hl, hh * 0.10, 0.010, 0, -0.016)]
    shield = g.skin("head", (0, back, 0.0), (0, 1, 0), up, hk, around=14, step=0.06, power=2.5)
    if hi:
        # A shallow groove down the middle and two that run out to the feelers.
        real.displace(shield, lambda p: (-0.004 * math.exp(-(p.x / 0.008) ** 2) - 0.003 * math.exp(-((abs(p.x) - 0.03 - max(0.0, p.y) * 0.5) / 0.007) ** 2)
                                         + 0.003 * noise.noise(p * 12.0)) * (1.0 if p.z > 0.02 else 0.0))
    g.add(shield, "head", "Head")
    # The plate under the head that the fangs are hinged to, and the mouth behind them.
    g.add(g.ball("fang plate", (0, 0.04, -0.062), 0.17, scale=(1.0, 1.05, 0.26), around=9), "under", "Head")
    g.add(g.ball("mouth", (0, 0.20, -0.048), 0.060, scale=(1.1, 1.2, 0.5), around=6), "joint", "Head")
    for side in (-1, 1):
        # Four small eyes in a cluster on each front corner.
        for k, (dx, dy, r) in enumerate(((0.0, 0.0, 0.017), (0.018, -0.024, 0.014), (-0.012, -0.030, 0.013), (0.010, 0.026, 0.012))):
            g.add(g.ball("eye", (side * (0.150 + dx), 0.150 + dy, 0.058 - abs(dx) * 0.3), r, around=6 if k == 0 else 5), "eye", "Head", only=None if k < 2 else 'hi')
        g.add(g.ball("feeler socket", (side * L["feeler_root"][0], L["feeler_root"][1], L["feeler_root"][2] - 0.012), 0.034, scale=(1.0, 1.0, 0.8), around=6), "joint", "Head")
        g.add(g.ball("fang hinge", (side * L["fang_root"][0], L["fang_root"][1] - 0.02, L["fang_root"][2]), 0.050, scale=(1.0, 1.2, 0.8), around=6), "joint", "Head")
        # The small second jaws, tucked between the fangs.
        g.add(g.spine("palp", (side * 0.035, 0.22, -0.055), (side * 0.15, 1.0, -0.2), 0.09, 0.016, around=4, bend=(-side * 0.3, 0, -0.1)), "leg", "Head")

    # ------------------------------------------------------------- Fang (the right one): heavy at the root, hooked in, a black point
    fl = L["fang"]
    # (distance, radius sideways, radius up, shift toward -X, shift up): it bows out, then hooks in across the mouth.
    fk = [(0.00, 0.046, 0.040, 0.000, 0), (0.04, 0.054, 0.046, -0.012, 0), (0.11, 0.046, 0.040, -0.028, 0), (0.18, 0.034, 0.030, -0.018, 0),
          (0.24, 0.021, 0.019, 0.022, 0), (0.28, 0.010, 0.009, 0.062, 0), (fl, 0.003, 0.003, 0.088, 0)]
    g.add(g.skin("fang", (0, 0, 0), (0, 1, 0), up, fk, around=8, step=0.035, power=2.0), "fang", "Fang")

    # ------------------------------------------------------------- Feeler: long, beaded, lifting then drooping to its tip
    el = L["feeler"]
    ek = [(0.00, 0.024, 0.024, 0, 0.0), (0.20, 0.020, 0.020, 0, 0.040), (0.50, 0.013, 0.013, 0, 0.060), (0.80, 0.007, 0.007, 0, 0.030), (el, 0.003, 0.003, 0, -0.020)]
    if hi:
        beaded = []
        beads = 22
        for i in range(beads * 6 + 1):
            t = el * i / (beads * 6)
            rs, rf, os_, of = real.along(ek, t)
            swell = 1.0 + 0.16 * math.sin(math.pi * 2 * i / 6 - math.pi / 2)
            beaded.append((t, rs * swell, rf * swell, os_, of))
        feeler = g.skin("feeler", (0, 0, 0), (0, 1, 0), up, beaded, around=5, step=el / (beads * 6) * 6, power=2.0)
    else:
        feeler = g.skin("feeler", (0, 0, 0), (0, 1, 0), up, ek, around=5, step=0.16, power=2.0)
    g.add(feeler, "feeler", "Feeler")

    # ------------------------------------------------------------- Leg: arched up at the knee, down to a dark claw; the same either side
    leg = L["leg"]
    lk = [(0.00, 0.026, 0.028, 0, 0.000), (0.03, 0.032, 0.034, 0, 0.012), (0.12, 0.028, 0.030, 0, 0.066), (0.135, 0.022, 0.023, 0, 0.072),
          (0.15, 0.026, 0.027, 0, 0.078), (0.25, 0.021, 0.022, 0, 0.092), (0.265, 0.016, 0.017, 0, 0.090), (0.28, 0.019, 0.020, 0, 0.086),
          (0.39, 0.013, 0.014, 0, 0.050), (0.40, 0.010, 0.010, 0, 0.046), (0.47, 0.008, 0.008, 0, 0.018), (leg, 0.003, 0.003, 0, 0.000)]
    g.add(g.skin("leg", (0, 0, 0), (0, 1, 0), up, lk, around=6, step=0.065, power=2.0), "leg", "Leg")
    g.add(g.ball("leg joint", (0, 0, 0), 0.030, around=5), "joint", "Leg")
    for y in (0.135, 0.265, 0.395):
        # The darker skin of each joint (only in the pictures).
        rs, rf, _, of = real.along(lk, y)
        g.add(g.ball("leg band", (0, y, of), rs * 1.12, scale=(1.0, 0.7, 1.05), around=5), "joint", "Leg", only='hi')

    # ------------------------------------------------------------- TailLeg: long, thick and thorny, lifting a little
    tl = L["tail_leg"]
    tk = [(0.00, 0.040, 0.042, 0, 0.000), (0.04, 0.050, 0.052, 0, 0.006), (0.24, 0.044, 0.046, 0, 0.040), (0.26, 0.034, 0.035, 0, 0.043),
          (0.28, 0.040, 0.041, 0, 0.046), (0.48, 0.030, 0.031, 0, 0.056), (0.50, 0.023, 0.024, 0, 0.056), (0.52, 0.026, 0.027, 0, 0.055),
          (0.70, 0.016, 0.017, 0, 0.034), (0.78, 0.010, 0.010, 0, 0.018), (tl, 0.003, 0.003, 0, 0.000)]
    g.add(g.skin("tail leg", (0, 0, 0), (0, 1, 0), up, tk, around=7, step=0.08, power=2.0), "tail", "TailLeg")
    g.add(g.ball("tail joint", (0, 0, 0), 0.045, around=6), "joint", "TailLeg")
    r = random.Random(53)
    for k in range(16):
        # Thorns on its first two joints (four of them real shapes, the rest in the pictures).
        y = r.uniform(0.06, 0.46)
        rs, rf, _, of = real.along(tk, y)
        a = r.uniform(-2.4, 2.4) + math.pi / 2 * (1 if k % 2 else -1) * 0.0
        way = Vector((math.sin(a), 0.5, -math.cos(a)))
        g.add(g.spine("thorn", (math.sin(a) * rs * 0.8, y, of - math.cos(a) * rf * 0.8), way, r.uniform(0.018, 0.032), 0.007, around=3), "tail", "TailLeg",
              only=None if k < 4 else 'hi')

    leftovers(g, L["stump"], "side")
    return g


def hunter_sockets():
    L = HUNTER
    sp = L["spacing"]
    return [oa.socket("SegEnd", (0, sp * 0.5, 0)), oa.socket("SegSide", (L["seg_half"], 0, -L["seg_bottom"])), oa.socket("SegTop", (0, 0, L["seg_top"])),
            oa.socket("HeadEnd", (0, L["head"] - sp * 0.5, 0)), oa.socket("HeadSide", (L["head_half"], 0, 0)),
            oa.socket("HipR", L["hip"]), oa.socket("LegEnd", (0, L["leg"], 0)), oa.socket("LegSide", (L["leg_radius"], 0, 0)),
            oa.socket("FangRootR", L["fang_root"]), oa.socket("FangEnd", (0, L["fang"], 0)),
            oa.socket("FeelerRootR", L["feeler_root"]), oa.socket("FeelerEnd", (0, L["feeler"], 0)),
            oa.socket("TailRootR", L["tail_root"]), oa.socket("TailLegEnd", (0, L["tail_leg"], 0)),
            oa.socket("StumpRim", (L["stump"], 0, 0))]


# ====================================================================== the Screamer (cicada)

# A copy of ScreamerLayout (Blender axes: x, forward, up). The rule test ScreamerModelFile_MatchesTheLayout holds
# the two together through the sockets.
SCREAMER = {
    "back": -0.34, "front": 0.52, "half": 0.30, "tall": 0.24,       # the fore body, round its own middle
    "abdomen_joint": (0.0, -0.30, 0.0), "abdomen": 0.86, "abdomen_half": 0.25,
    "drum": (0.27, -0.16, 0.02), "drum_radius": 0.15,
    "fore_hinge": (0.20, 0.10, 0.17), "hind_hinge": (0.19, -0.12, 0.14),
    "fore_wing": 1.62, "fore_chord": 0.56, "hind_wing": 0.95, "hind_chord": 0.50,
    "hips": ((0.15, 0.26, -0.17), (0.17, 0.04, -0.18), (0.15, -0.18, -0.17)),
    "leg": 0.40, "shin": 0.48, "leg_radius": 0.03,
    "stump": 0.05,
}
JET, EMBER, SOOT = (0.013, 0.012, 0.013), (0.50, 0.17, 0.025), (0.006, 0.006, 0.007)
SCREAMER_FLAT = {"shell": ((0.13, 0.12, 0.12), 0.6), "collar": ((0.62, 0.34, 0.10), 0.55), "under": ((0.60, 0.42, 0.22), 0.35),
                 "abdomen": ((0.14, 0.12, 0.12), 0.55), "band": ((0.66, 0.36, 0.10), 0.5), "drum": ((0.66, 0.56, 0.38), 0.5),
                 "wing": ((0.24, 0.20, 0.17), 0.5), "vein": ((0.78, 0.42, 0.10), 0.5), "leg": ((0.62, 0.34, 0.10), 0.45),
                 "joint": ((0.44, 0.28, 0.16), 0.2), "flesh": ((0.78, 0.62, 0.44), 0.6), "eye": ((0.62, 0.08, 0.05), 0.85)}
SCREAMER_MATERIALS = {
    "shell": lambda: mats.chitin("shell", JET, EMBER, SOOT, under=(0.20, 0.10, 0.04), seed=61.0, bumps=0.18, growth=0.0, mottle=0.10, gloss=0.66),
    "collar": lambda: mats.chitin("collar", (0.36, 0.13, 0.022), (0.56, 0.30, 0.08), SOOT, seed=62.0, bumps=0.10, growth=0.0, mottle=0.3, gloss=0.6),
    "under": lambda: mats.chitin("under", (0.30, 0.16, 0.06), (0.46, 0.32, 0.16), (0.06, 0.03, 0.02), seed=63.0, bumps=0.12, growth=0.0, mottle=0.4, gloss=0.36),
    "abdomen": lambda: mats.chitin("abdomen", (0.016, 0.014, 0.014), EMBER, SOOT, under=(0.34, 0.18, 0.06), seed=64.0, bumps=0.10, growth=0.0, mottle=0.06, gloss=0.6),
    "band": lambda: mats.chitin("band", (0.40, 0.15, 0.025), (0.58, 0.32, 0.09), SOOT, under=(0.36, 0.20, 0.07), seed=65.0, bumps=0.06, growth=0.0, mottle=0.3, gloss=0.55),
    "drum": lambda: mats.chitin("drum", (0.34, 0.25, 0.13), (0.50, 0.42, 0.26), (0.07, 0.035, 0.015), seed=66.0, bumps=0.0, growth=0.0, mottle=0.3, gloss=0.5),
    "wing": lambda: mats.chitin("wing", (0.050, 0.036, 0.026), (0.16, 0.085, 0.035), (0.014, 0.011, 0.010), seed=67.0, bumps=0.0, growth=0.0, mottle=0.35, size=1.6, gloss=0.7),
    "vein": lambda: mats.chitin("vein", (0.52, 0.19, 0.022), (0.66, 0.36, 0.10), (0.10, 0.03, 0.01), seed=68.0, bumps=0.0, growth=0.0, mottle=0.2, size=0.5, gloss=0.5),
    "leg": lambda: mats.chitin("leg", (0.36, 0.13, 0.022), (0.52, 0.28, 0.08), (0.05, 0.02, 0.01), seed=69.0, bumps=0.12, growth=0.0, mottle=0.3, size=0.6,
                               tip=(1, 0.30, 0.48, (0.016, 0.012, 0.010)), gloss=0.5),
    "joint": lambda: mats.membrane("joint", (0.22, 0.11, 0.05), seed=70.0),
    "flesh": lambda: mats.flesh("flesh", pale=(0.70, 0.60, 0.40), red=(0.36, 0.10, 0.05), seed=71.0),
    "eye": lambda: mats.eye("eye", colour=(0.30, 0.012, 0.008), glint=(0.9, 0.25, 0.12)),
}


def wing(g, name, piece, length, outline, veins, crosses):
    """A wing from its hinge: +Y along it, flat, its trailing edge toward +X, the same above as below.
    outline: (share of the length, across) points going round once. veins: paths of (share, across) from the base
    out, the first one the thick leading edge. crosses: short veins between them."""
    thick = 0.012
    flat = [(s * length, c) for s, c in outline]
    # The outline is rounded: finely for the pictures, enough for the game that its edge is a curve.
    rounded = real.smooth_path([Vector((0, y, z)) for y, z in flat] + [Vector((0, flat[0][0], flat[0][1]))], cuts=5 if g.hi else 2)
    flat = [(p[1], p[2]) for p in rounded[:-1]]
    # Drawn standing on edge, then laid flat: across becomes +X, its thickness up and down.
    sheet = oa.slab(name, flat, thick, (0.5, 0.5, 0.5), bevel=0.004 if g.hi else 0.0, segments=2)
    lay = Matrix.Rotation(math.radians(90), 4, 'Y')
    sheet.data.transform(lay)
    if g.hi:
        oa.densify(sheet, 0.02)
    g.add(sheet, "wing", piece)
    for k, path in enumerate(veins):
        points = [Vector((c, s * length, 0.0)) for s, c in path]
        if g.hi:
            points = real.smooth_path(points, 3)
        radius = 0.019 if k == 0 else 0.0115
        radii = [radius * (1.0 - 0.55 * i / (len(points) - 1)) for i in range(len(points))]
        # The leading edge is a real shape in the game too; the rest are only in the pictures.
        g.add(real.sweep("vein", points, radius, (0.5, 0.5, 0.5), n=10 if g.hi else 4, radii=radii), "vein", piece, only=None if k == 0 else 'hi')
    for a, b in crosses:
        points = [Vector((a[1], a[0] * length, 0.0)), Vector((b[1], b[0] * length, 0.0))]
        g.add(real.sweep("cross vein", points, 0.0075, (0.5, 0.5, 0.5), n=8), "vein", piece, only='hi')
    # The vein that runs round just inside the outer edge, closing the cells.
    inset = []
    middle = (0.5 * length, 0.2)
    for y, c in flat:
        share = y / length
        if share < 0.42 or c < 0.0:
            continue
        inset.append((share, Vector((c + (middle[1] - c) * 0.13, y + (middle[0] - y) * 0.07, 0.0))))
    inset.sort(key=lambda e: -math.atan2(e[1].x - middle[1], e[1].y - middle[0]))
    if g.hi and len(inset) > 3:
        g.add(real.sweep("edge vein", real.smooth_path([p for _, p in inset], 2), 0.0075, (0.5, 0.5, 0.5), n=8), "vein", piece, only='hi')
    g.add(g.ball("wing root", (0.015, 0.0, 0), 0.042, scale=(1.3, 1.0, 0.75), around=6), "joint", piece)


def screamer(hi):
    g = Maker(hi, SCREAMER_FLAT, SCREAMER_MATERIALS)
    L = SCREAMER
    up = (0, 0, 1)
    back, front, half, tall = L["back"], L["front"], L["half"], L["tall"]

    # ------------------------------------------------------------- Body: a domed thorax, a broad short head with eyes at its corners
    tk = [(0.00, half * 0.68, tall * 0.70, 0, 0.0), (0.06, half * 0.88, tall * 0.88, 0, 0.0), (0.24, half, tall, 0, 0.0), (0.44, half * 0.99, tall * 0.97, 0, 0.0),
          (0.58, half * 0.90, tall * 0.86, 0, -0.005), (0.68, half * 0.80, tall * 0.72, 0, -0.012)]
    thorax = g.skin("thorax", (0, back, 0.0), (0, 1, 0), up, tk, around=18, step=0.085, power=2.3)
    if hi:
        # The W of grooves a cicada carries on its back, a cross ridge at the rear, and a slow unevenness.
        def back_marks(p):
            if p.z < 0.04:
                return 0.0
            w = abs(abs(p.x) - 0.085 - 0.10 * max(0.0, (p.y + 0.05)))
            groove = -0.006 * math.exp(-(w / 0.010) ** 2) * (1.0 if -0.22 < p.y < 0.20 else 0.0)
            ridge = 0.007 * math.exp(-((p.y + 0.25) / 0.016) ** 2) * math.exp(-(p.x / 0.12) ** 2)
            return groove + ridge + 0.003 * noise.noise(p * 10.0)
        real.displace(thorax, back_marks)
    g.add(thorax, "shell", "Body", second=("under", lambda c, n: n.z < -0.55))
    # The collar behind the head.
    ck = [(0.00, half * 0.86, tall * 0.80, 0, 0.0), (0.03, half * 0.95, tall * 0.90, 0, 0.0), (0.10, half * 0.94, tall * 0.88, 0, -0.004), (0.14, half * 0.84, tall * 0.76, 0, -0.008)]
    g.add(g.skin("collar", (0, 0.20, 0.0), (0, 1, 0), up, ck, around=14, step=0.05, power=2.3), "collar", "Body")
    # The head: short and very wide, sloping down to the front.
    hk = [(0.00, half * 0.80, tall * 0.66, 0, -0.012), (0.06, half * 0.88, tall * 0.66, 0, -0.016), (0.12, half * 0.80, tall * 0.58, 0, -0.024),
          (0.17, half * 0.56, tall * 0.44, 0, -0.034), (front - 0.30, half * 0.22, tall * 0.22, 0, -0.045)]
    g.add(g.skin("head", (0, 0.30, 0.0), (0, 1, 0), up, hk, around=12, step=0.045, power=2.3), "shell", "Body")
    # The swollen, ribbed front of the face, and the beak folded back between the legs.
    face = g.ball("face", (0, 0.425, -0.075), 0.105, scale=(0.95, 0.85, 1.05), around=9)
    if hi:
        real.displace(face, lambda p: 0.004 * math.sin(p.z * 150.0) * (1.0 if p.y > 0.40 else 0.0))
    g.add(face, "collar", "Body")
    g.add(g.spine("beak", (0, 0.40, -0.15), (0, -1.0, -0.16), 0.40, 0.034, around=6), "under", "Body")
    for side in (-1, 1):
        # Big wide-set eyes, three small ones between them, a bristle of a feeler under each.
        g.add(g.ball("eye", (side * 0.222, 0.395, 0.035), 0.078, scale=(1.0, 1.05, 1.0), around=9), "eye", "Body")
        g.add(g.ball("small eye", (side * 0.045, 0.385, 0.128), 0.020, around=5), "eye", "Body")
        g.add(g.spine("feeler", (side * 0.10, 0.46, 0.01), (side * 0.45, 1.0, 0.25), 0.13, 0.011, around=4), "leg", "Body")
        for hinge in (L["fore_hinge"], L["hind_hinge"]):
            g.add(g.ball("wing socket", (side * (hinge[0] - 0.03), hinge[1], hinge[2] - 0.02), 0.058, scale=(1.0, 1.25, 0.8), around=6), "joint", "Body")
        for hip in L["hips"]:
            g.add(g.ball("hip", (side * (hip[0] - 0.02), hip[1], hip[2] + 0.03), 0.060, scale=(1.0, 1.1, 0.9), around=6), "joint", "Body")
    g.add(g.ball("small eye", (0, 0.425, 0.108), 0.020, around=5), "eye", "Body")
    # The paler plates underneath.
    g.add(g.ball("breast", (0, 0.03, -tall + 0.062), 0.24, scale=(0.86, 1.40, 0.24), around=9), "under", "Body")

    # ------------------------------------------------------------- Abdomen: from its joint back along -Y, ringed, hollow sounding
    al, ah = L["abdomen"], L["abdomen_half"]
    ak = [(0.00, ah * 0.78, 0.17, 0, 0.0), (0.07, ah * 0.98, 0.205, 0, 0.0), (0.26, ah, 0.215, 0, 0.0), (0.46, ah * 0.90, 0.195, 0, -0.004),
          (0.64, ah * 0.66, 0.145, 0, -0.010), (0.78, ah * 0.34, 0.075, 0, -0.016), (al, 0.018, 0.016, 0, -0.020)]
    rings_at = [0.12 + 0.105 * i for i in range(7)]
    if hi:
        ringed = []
        for i in range(130):
            t = al * i / 129
            rs, rf, os_, of = real.along(ak, t)
            # Each ring overlaps the next: a step down just behind its rear edge.
            lap = min(1.0, min(abs(t - r) for r in rings_at) / 0.012)
            step = 1.0 - 0.035 * (1.0 - lap)
            ringed.append((t, rs * step, rf * step, os_, of))
        ak = ringed
    belly = g.skin("abdomen", (0, 0, 0), (0, -1, 0), up, ak, around=18, step=0.105, power=2.2)
    def banded(c, n):
        if n.z < -0.55:
            return False
        t = -c.y
        return any(-0.034 < t - r < 0.0 for r in rings_at)
    g.add(belly, "abdomen", "Abdomen", second=("band", banded))
    g.add(g.ball("waist", (0, 0, 0), 0.16, scale=(1.25, 1.0, 1.0), around=8), "joint", "Abdomen")
    g.add(g.ball("belly plates", (0, -0.30, -0.15), 0.22, scale=(0.80, 1.5, 0.28), around=8), "under", "Abdomen")
    # The hollow each drum sits over.
    d = L["drum"]
    for side in (-1, 1):
        g.add(g.ball("drum seat", (side * (d[0] - 0.075), d[1], d[2]), 0.13, scale=(0.5, 1.0, 1.0), around=7), "joint", "Abdomen")

    # ------------------------------------------------------------- Drum (the right one): a pale ribbed dome in a dark rim
    dr = L["drum_radius"]
    dome = g.ball("drum", (0.0, 0, 0), dr * 0.94, scale=(0.46, 1.0, 1.0), around=14)
    if hi:
        real.displace(dome, lambda p: 0.0075 * math.sin(p.y * 190.0) * max(0.0, 1.0 - (p.y * p.y + p.z * p.z) / (dr * dr * 0.80)))
    g.add(dome, "drum", "Drum")
    g.add(g.ball("drum rim", (-0.022, 0, 0), dr, scale=(0.30, 1.0, 1.0), around=14), "shell", "Drum")

    # ------------------------------------------------------------- the wings
    fl, hl = L["fore_wing"], L["hind_wing"]
    fore = [(0.00, -0.035), (0.30, -0.050), (0.70, -0.040), (0.92, 0.030), (1.00, 0.150), (0.955, 0.300), (0.80, 0.440), (0.58, 0.510),
            (0.36, 0.430), (0.16, 0.250), (0.03, 0.090)]
    fore_veins = [[(0.00, -0.020), (0.30, -0.036), (0.70, -0.026), (0.90, 0.030), (0.985, 0.140)],
                  [(0.00, 0.010), (0.30, 0.040), (0.60, 0.080), (0.80, 0.130), (0.955, 0.210)],
                  [(0.00, 0.025), (0.25, 0.110), (0.50, 0.200), (0.72, 0.290), (0.90, 0.330)],
                  [(0.00, 0.040), (0.20, 0.170), (0.42, 0.300), (0.62, 0.400), (0.76, 0.440)],
                  [(0.02, 0.060), (0.14, 0.190), (0.28, 0.320), (0.42, 0.430)],
                  [(0.60, 0.080), (0.74, 0.200), (0.86, 0.290), (0.93, 0.320)],
                  [(0.50, 0.200), (0.62, 0.300), (0.70, 0.400), (0.72, 0.470)]]
    fore_crosses = [((0.45, 0.058), (0.42, 0.170)), ((0.70, 0.104), (0.67, 0.160)), ((0.83, 0.150), (0.80, 0.245)), ((0.80, 0.313), (0.70, 0.400)),
                    ((0.34, 0.145), (0.31, 0.240)), ((0.56, 0.228), (0.52, 0.350)), ((0.88, 0.060), (0.86, 0.165)), ((0.93, 0.230), (0.905, 0.325))]
    wing(g, "fore wing", "ForeWing", fl, fore, fore_veins, fore_crosses)
    hind = [(0.00, -0.020), (0.40, -0.032), (0.80, -0.010), (1.00, 0.110), (0.93, 0.290), (0.72, 0.430), (0.46, 0.468), (0.20, 0.350), (0.04, 0.120)]
    hind_veins = [[(0.00, -0.008), (0.40, -0.018), (0.80, 0.004), (0.98, 0.110)],
                  [(0.00, 0.015), (0.35, 0.090), (0.65, 0.170), (0.93, 0.250)],
                  [(0.00, 0.030), (0.28, 0.170), (0.52, 0.300), (0.72, 0.400)],
                  [(0.02, 0.050), (0.18, 0.200), (0.34, 0.340), (0.46, 0.440)]]
    hind_crosses = [((0.50, 0.130), (0.45, 0.262)), ((0.78, 0.208), (0.66, 0.368)), ((0.30, 0.182), (0.27, 0.280)), ((0.70, 0.010), (0.68, 0.178))]
    wing(g, "hind wing", "HindWing", hl, hind, hind_veins, hind_crosses)

    # ------------------------------------------------------------- Leg and Shin: the same either side, bending only upward
    leg, shin = L["leg"], L["shin"]
    lk = [(0.00, 0.028, 0.032, 0, 0.0), (0.05, 0.036, 0.044, 0, 0.004), (0.20, 0.033, 0.040, 0, 0.006), (0.34, 0.027, 0.030, 0, 0.002), (leg, 0.023, 0.025, 0, 0.0)]
    g.add(g.skin("thigh", (0, 0, 0), (0, 1, 0), up, lk, around=7, step=0.10, power=2.1), "leg", "Leg")
    g.add(g.ball("leg joint", (0, 0, 0), 0.040, around=6), "joint", "Leg")
    for k, y in enumerate((0.10, 0.18, 0.26, 0.32)):
        # The thorns under a cicada's thigh (two of them real shapes).
        g.add(g.spine("thorn", (0, y, -0.040), (0, 0.35, -1.0), 0.045 - 0.006 * k, 0.011, around=4), "leg", "Leg", only=None if k % 2 == 0 else 'hi')
    sk_ = [(0.00, 0.022, 0.024, 0, 0.0), (0.04, 0.025, 0.028, 0, 0.0), (0.26, 0.017, 0.019, 0, 0.0), (0.345, 0.013, 0.014, 0, 0.0),
           (0.355, 0.010, 0.010, 0, 0.0), (0.37, 0.012, 0.012, 0, 0.0), (0.455, 0.008, 0.009, 0, -0.004), (shin, 0.003, 0.003, 0, -0.014)]
    g.add(g.skin("shin", (0, 0, 0), (0, 1, 0), up, sk_, around=6, step=0.09, power=2.0), "leg", "Shin")
    g.add(g.ball("knee", (0, 0, 0), 0.030, around=6), "joint", "Shin")
    for k, y in enumerate((0.12, 0.20, 0.28)):
        # Short spurs along the shin (only in the pictures).
        g.add(g.spine("spur", (0, y, -0.018), (0, 0.6, -1.0), 0.03, 0.006, around=3), "leg", "Shin", only='hi')

    leftovers(g, L["stump"], "joint")
    return g


def screamer_sockets():
    L = SCREAMER
    out = [oa.socket("BodyFront", (0, L["front"], 0)), oa.socket("BodyBack", (0, L["back"], 0)), oa.socket("BodySide", (L["half"], 0, 0)),
           oa.socket("BodyTop", (0, 0, L["tall"])), oa.socket("AbdomenJoint", L["abdomen_joint"]), oa.socket("AbdomenEnd", (0, -L["abdomen"], 0)),
           oa.socket("AbdomenSide", (L["abdomen_half"], 0, 0)), oa.socket("DrumR", L["drum"]), oa.socket("DrumRim", (0, L["drum_radius"], 0)),
           oa.socket("ForeHingeR", L["fore_hinge"]), oa.socket("HindHingeR", L["hind_hinge"]),
           oa.socket("ForeWingEnd", (0, L["fore_wing"], 0)), oa.socket("ForeWingChord", (L["fore_chord"], 0, 0)),
           oa.socket("HindWingEnd", (0, L["hind_wing"], 0)), oa.socket("HindWingChord", (L["hind_chord"], 0, 0)),
           oa.socket("LegEnd", (0, L["leg"], 0)), oa.socket("ShinEnd", (0, L["shin"], 0)), oa.socket("LegSide", (L["leg_radius"], 0, 0)),
           oa.socket("StumpRim", (L["stump"], 0, 0))]
    for k, hip in enumerate(L["hips"]):
        out.append(oa.socket("Hip%dR" % k, hip))
    return out


# ====================================================================== the Ambusher (bobbit worm)

# A copy of AmbusherLayout (Blender axes: x, forward, up). The rule test AmbusherModelFile_MatchesTheLayout holds
# the two together through the sockets.
AMBUSHER = {
    "spacing": 0.36,
    "head": 0.50, "head_half": 0.20, "head_tall": 0.15,     # the head runs from spacing / 2 behind its node to the rest in front
    "seg_half": 0.17, "seg_tall": 0.15, "bristle": 0.07,
    "jaw_hinge": (0.15, 0.14, -0.02), "jaw": 0.70, "jaw_thick": 0.05,
    "feeler_ring": 0.13, "feeler_ahead": 0.10, "feeler_arc": 1.25, "feelers": 5, "feeler": 0.80, "feeler_radius": 0.02,
    "rim": 0.75, "hole": 0.26, "rim_height": 0.14,
    "stump": 0.05,
}
AMBUSHER_FLAT = {"skin": ((0.22, 0.13, 0.20), 0.7), "head": ((0.14, 0.10, 0.16), 0.7), "jaw": ((0.10, 0.07, 0.06), 0.75),
                 "tooth": ((0.70, 0.62, 0.48), 0.5), "bristle": ((0.72, 0.56, 0.30), 0.4), "lobe": ((0.46, 0.24, 0.24), 0.3),
                 "feeler": ((0.50, 0.38, 0.26), 0.4), "gullet": ((0.50, 0.14, 0.12), 0.6), "eye": ((0.04, 0.04, 0.05), 0.85),
                 "earth": ((0.36, 0.31, 0.25), 0.05), "pit": ((0.03, 0.03, 0.03), 0.05), "flesh": ((0.78, 0.50, 0.44), 0.6)}
AMBUSHER_MATERIALS = {
    "skin": lambda: mats.sheen("skin", seed=81.0),
    "head": lambda: mats.sheen("head", base=(0.030, 0.020, 0.034), under=(0.20, 0.10, 0.10), seed=82.0, ring=0.05, gloss=0.8, strength=0.14),
    "jaw": lambda: mats.chitin("jaw", (0.030, 0.016, 0.012), (0.20, 0.11, 0.05), (0.008, 0.006, 0.006), seed=83.0, bumps=0.05, growth=0.0, mottle=0.15, size=0.7,
                               tip=(1, 0.50, 0.70, (0.006, 0.005, 0.005)), gloss=0.78),
    "tooth": lambda: mats.chitin("tooth", (0.44, 0.36, 0.24), (0.60, 0.54, 0.42), (0.12, 0.07, 0.04), seed=84.0, bumps=0.0, growth=0.0, mottle=0.2, size=0.4, gloss=0.6),
    "bristle": lambda: mats.chitin("bristle", (0.46, 0.30, 0.10), (0.62, 0.50, 0.26), (0.12, 0.06, 0.02), seed=85.0, bumps=0.0, growth=0.0, mottle=0.2, size=0.4, gloss=0.5),
    "lobe": lambda: mats.membrane("lobe", (0.26, 0.10, 0.11), seed=86.0, size=3.0),
    "feeler": lambda: mats.banded("feeler", seed=87.0),
    "gullet": lambda: mats.flesh("gullet", pale=(0.50, 0.22, 0.20), red=(0.26, 0.03, 0.03), seed=88.0),
    "eye": lambda: mats.eye("eye", colour=(0.008, 0.008, 0.012), glint=(0.06, 0.08, 0.12)),
    "earth": lambda: mats.earth("earth", colour=(0.150, 0.112, 0.076), seed=89.0),
    "pit": lambda: mats.earth("pit", colour=(0.012, 0.011, 0.010), seed=90.0),
    "flesh": lambda: mats.flesh("flesh", pale=(0.70, 0.52, 0.50), red=(0.40, 0.06, 0.06), seed=91.0),
}


def ambusher(hi):
    g = Maker(hi, AMBUSHER_FLAT, AMBUSHER_MATERIALS)
    L = AMBUSHER
    up = (0, 0, 1)
    sp, half, tall = L["spacing"], L["seg_half"], L["seg_tall"]

    # ------------------------------------------------------------- Seg: a fat ringed length, a fleshy lobe and a fan of bristles each side
    over = 0.03
    sk_ = [(0.00, half * 0.90, tall * 0.90, 0, 0.0), (0.03, half * 0.975, tall * 0.975, 0, 0.0), (sp * 0.5 + over, half, tall, 0, 0.0),
           (sp + 2 * over - 0.03, half * 0.975, tall * 0.975, 0, 0.0), (sp + 2 * over, half * 0.90, tall * 0.90, 0, 0.0)]
    body = g.skin("length", (0, -sp * 0.5 - over, 0.0), (0, 1, 0), up, sk_, around=12, step=0.105, power=2.3)
    if hi:
        # Narrow rings all the way along, and a slow unevenness.
        real.displace(body, lambda p: 0.0030 * math.sin(p.y * 2 * math.pi / 0.045) + 0.003 * noise.noise(p * 9.0))
    g.add(body, "skin", "Seg")
    # A ball over the joint to the length behind, so a bend never opens a gap.
    g.add(g.ball("link", (0, -sp * 0.5, 0.0), tall * 0.985, scale=(half / tall, 0.85, 1.0), around=12), "skin", "Seg")
    r = random.Random(81)
    for side in (-1, 1):
        for y in (-0.085, 0.085):
            g.add(g.ball("lobe", (side * (half - 0.012), y, -0.045), 0.045, scale=(1.15, 1.35, 0.8), around=6), "lobe", "Seg")
            # The bristles: one stout one in the game's model, a fan of fine ones in the pictures.
            reach = half + L["bristle"]
            g.add(g.spine("bristle", (side * (half + 0.01), y, -0.055), (side, 0.0, -0.30), reach - half - 0.01 + 0.004, 0.011, around=3), "bristle", "Seg")
            for k in range(5):
                way = (side, r.uniform(-0.5, 0.5), r.uniform(-0.75, -0.05))
                g.add(g.spine("bristle", (side * (half + 0.012), y + r.uniform(-0.02, 0.02), -0.05), way, r.uniform(0.035, 0.060), 0.004, around=3), "bristle", "Seg", only='hi')

    # ------------------------------------------------------------- Head: blunt and armoured, a ring of teeth round its gullet, small eyes
    hl, hh, ht = L["head"], L["head_half"], L["head_tall"]
    back = -sp * 0.5
    hk = [(0.00, hh * 0.76, ht * 0.84, 0, 0.0), (0.05, hh * 0.88, ht * 0.97, 0, 0.0), (0.20, hh * 0.96, ht, 0, 0.0), (0.34, hh, ht * 0.97, 0, 0.0),
          (0.43, hh * 0.92, ht * 0.86, 0, 0.0), (0.485, hh * 0.72, ht * 0.66, 0, 0.0), (hl, hh * 0.50, ht * 0.46, 0, 0.0)]
    skull = g.skin("skull", (0, back, 0.0), (0, 1, 0), up, hk, around=14, step=0.07, power=2.4)
    if hi:
        # Two furrows over the crown, and a seam round the back of the head where the first ring joins.
        real.displace(skull, lambda p: -0.005 * math.exp(-((abs(p.x) - 0.075) / 0.010) ** 2) * (1.0 if p.z > 0.03 else 0.0)
                                       - 0.006 * math.exp(-((p.y - (back + 0.07)) / 0.010) ** 2) + 0.003 * noise.noise(p * 11.0))
    g.add(skull, "head", "Head")
    # The gullet: a dark red hollow in its blunt front, ringed with small pale teeth.
    g.add(g.ball("gullet", (0, back + hl - 0.012, 0.0), hh * 0.50, scale=(1.0, 0.30, 0.86), around=8), "gullet", "Head")
    teeth = 12 if hi else 6
    for k in range(teeth):
        a = 2 * math.pi * (k + 0.5) / teeth
        at = (math.cos(a) * hh * 0.44, back + hl - 0.02, math.sin(a) * ht * 0.42)
        g.add(g.spine("tooth", at, (-math.cos(a) * 0.35, 1.0, -math.sin(a) * 0.35), 0.055, 0.014, around=3), "tooth", "Head")
    for side in (-1, 1):
        g.add(g.ball("eye", (side * 0.085, 0.20, ht * 0.86), 0.022, around=6), "eye", "Head")
        g.add(g.ball("eye", (side * 0.140, 0.13, ht * 0.66), 0.017, around=5), "eye", "Head")
        hinge = L["jaw_hinge"]
        g.add(g.ball("jaw hinge", (side * (hinge[0] + 0.012), hinge[1] - 0.02, hinge[2]), 0.066, scale=(0.9, 1.25, 0.85), around=7), "lobe", "Head")
    for k in range(L["feelers"]):
        a = L["feeler_arc"] * (k - (L["feelers"] - 1) * 0.5) / ((L["feelers"] - 1) * 0.5)
        root = (math.sin(a) * L["feeler_ring"], L["feeler_ahead"], math.cos(a) * L["feeler_ring"])
        g.add(g.ball("feeler socket", (root[0] * 0.93, root[1], root[2] * 0.93), 0.036, scale=(1.0, 1.0, 1.0), around=5), "lobe", "Head")

    # ------------------------------------------------------------- Jaw (the right one): a sickle, bowed out and hooked in, toothed inside
    jl, jt = L["jaw"], L["jaw_thick"]
    jk = [(0.00, 0.058, jt, 0.000, 0), (0.06, 0.068, jt, -0.020, 0), (0.22, 0.058, jt * 0.84, -0.070, 0), (0.40, 0.046, jt * 0.68, -0.085, 0),
          (0.55, 0.032, jt * 0.52, -0.040, 0), (0.64, 0.018, jt * 0.32, 0.030, 0), (jl, 0.004, 0.004, 0.110, 0)]
    g.add(g.skin("jaw", (0, 0, 0), (0, 1, 0), up, jk, around=8, step=0.07, power=2.0), "jaw", "Jaw")
    g.add(g.ball("jaw root", (0, 0, 0), 0.060, scale=(1.0, 1.0, jt / 0.060), around=6), "jaw", "Jaw")
    for k, y in enumerate((0.20, 0.30, 0.40, 0.49, 0.57)):
        rs, rf, os_, of = real.along(jk, y)
        # Teeth along its inner edge (three of them real shapes, the rest in the pictures). The inner edge is toward -X.
        g.add(g.spine("jaw tooth", (-os_ - rs * 0.8, y, 0), (-1.0, 0.45, 0.0), 0.075 - 0.008 * k, 0.016, around=3), "tooth", "Jaw", only=None if k % 2 == 0 else 'hi')

    # ------------------------------------------------------------- Feeler: banded, tapering, lifting toward its tip
    fl, fr = L["feeler"], L["feeler_radius"]
    fk = [(0.00, fr * 1.3, fr * 1.3, 0, 0.0), (0.10, fr * 1.1, fr * 1.1, 0, 0.010), (0.40, fr * 0.75, fr * 0.75, 0, 0.050), (0.70, fr * 0.4, fr * 0.4, 0, 0.100),
          (fl, 0.004, 0.004, 0, 0.130)]
    g.add(g.skin("feeler", (0, 0, 0), (0, 1, 0), up, fk, around=5, step=0.16, power=2.0), "feeler", "Feeler")

    # ------------------------------------------------------------- Rim: the ring of ground thrown up round the mouth of its burrow
    rim, hole, rh = L["rim"], L["hole"], L["rim_height"]
    profile = [(0.0, rim), (rh * 0.22, rim * 0.90), (rh * 0.62, rim * 0.72), (rh * 0.92, rim * 0.58), (rh, rim * 0.50), (rh * 0.86, rim * 0.43), (rh * 0.40, hole * 1.10), (0.004, hole), (0.004, 0.0)]
    heap = oa.lathe("heap", (0, 0, 0), (0, 0, 1), profile, (0.5, 0.5, 0.5), segments=72 if hi else 24)
    def lumpy(p):
        # Uneven all round, never below the ground, and never higher than the layout says.
        a = math.atan2(p.y, p.x)
        if p.z < 0.01:
            # Its outer edge wanders in and out, so the heap is never a neat disc (0 leaves the vertex where it is
            # along its normal; the edge is moved below).
            return 0.0
        heaps = 0.5 + 0.25 * math.sin(a * 3.0 + 0.7) + 0.25 * math.sin(a * 5.0 + 2.1)
        return -(0.012 + 0.055 * heaps + 0.02 * (0.5 + 0.5 * noise.noise(Vector((p.x * 4.0, p.y * 4.0, 7.0))))) * (p.z / rh)
    if hi:
        heap = heap
    real.displace(heap, lumpy)
    for v in heap.data.vertices:
        d = math.hypot(v.co.x, v.co.y)
        if d > hole * 1.3:
            a = math.atan2(v.co.y, v.co.x)
            k = 1.0 - (0.10 + 0.05 * math.sin(a * 4.0 + 1.3) + 0.04 * math.sin(a * 7.0 + 0.4)) * min(1.0, (d - hole * 1.3) / (rim * 0.3))
            v.co.x *= k
            v.co.y *= k
    g.add(heap, "earth", "Rim", second=("pit", lambda c, n: math.hypot(c.x, c.y) < hole * 1.02))
    r = random.Random(89)
    for k in range(7):
        # Clods lying on the heap.
        a, d = r.uniform(0, 2 * math.pi), r.uniform(rim * 0.50, rim * 0.92)
        size = r.uniform(0.035, 0.065)
        g.add(g.ball("clod", (math.cos(a) * d, math.sin(a) * d, rh * max(0.1, 1.0 - abs(d - rim * 0.53) / (rim * 0.47)) * 0.7), size,
                     scale=(1.0, r.uniform(0.7, 1.2), 0.7), around=5), "earth", "Rim")

    leftovers(g, L["stump"], "skin")
    return g


def ambusher_sockets():
    L = AMBUSHER
    sp = L["spacing"]
    out = [oa.socket("SegEnd", (0, sp * 0.5, 0)), oa.socket("SegSide", (L["seg_half"], 0, 0)), oa.socket("SegTop", (0, 0, L["seg_tall"])),
           oa.socket("BristleEnd", (L["seg_half"] + L["bristle"], 0, 0)),
           oa.socket("HeadEnd", (0, L["head"] - sp * 0.5, 0)), oa.socket("HeadSide", (L["head_half"], 0, 0)), oa.socket("HeadTop", (0, 0, L["head_tall"])),
           oa.socket("JawHingeR", L["jaw_hinge"]), oa.socket("JawEnd", (0, L["jaw"], 0)), oa.socket("JawTop", (0, 0, L["jaw_thick"])),
           oa.socket("FeelerEnd", (0, L["feeler"], 0)), oa.socket("FeelerSide", (L["feeler_radius"], 0, 0)),
           oa.socket("RimEdge", (L["rim"], 0, 0)), oa.socket("RimHole", (L["hole"], 0, 0)), oa.socket("RimTop", (0, 0, L["rim_height"])),
           oa.socket("StumpRim", (L["stump"], 0, 0))]
    for k in range(L["feelers"]):
        a = L["feeler_arc"] * (k - (L["feelers"] - 1) * 0.5) / ((L["feelers"] - 1) * 0.5)
        out.append(oa.socket("Feeler%d" % k, (math.sin(a) * L["feeler_ring"], L["feeler_ahead"], math.cos(a) * L["feeler_ring"])))
    return out


# ====================================================================== the Blind stalker (olm)

# A copy of StalkerLayout (Blender axes: x, forward, up). The rule test StalkerModelFile_MatchesTheLayout holds the
# two together through the sockets.
STALKER = {
    "spacing": 0.34,
    "head": 0.62, "head_half": 0.20, "head_tall": 0.11,
    "seg_half": 0.17, "seg_tall": 0.16, "tail_half": 0.09, "tail_tall": 0.21,
    "hip": (0.14, 0.0, -0.07), "leg": 0.34, "shin": 0.38, "leg_radius": 0.045,
    "gill_root": (0.16, -0.10, 0.03), "gill": 0.36, "gill_spread": 0.13,
    "jaw_hinge": (0.0, 0.0, -0.035), "jaw": 0.40, "mouth": 0.36,
    "stump": 0.05,
}
STALKER_FLAT = {"skin": ((0.74, 0.60, 0.56), 0.6), "gill": ((0.62, 0.10, 0.12), 0.6), "mouth": ((0.50, 0.16, 0.16), 0.6),
                "tooth": ((0.80, 0.76, 0.66), 0.5), "spot": ((0.30, 0.22, 0.26), 0.5), "flesh": ((0.78, 0.50, 0.44), 0.6)}
STALKER_MATERIALS = {
    "skin": lambda: mats.pallid("skin", seed=95.0),
    "gill": lambda: mats.plume("gill", seed=96.0, reach=STALKER["gill"]),
    "mouth": lambda: mats.flesh("mouth", pale=(0.46, 0.20, 0.20), red=(0.24, 0.03, 0.04), seed=97.0),
    "tooth": lambda: mats.chitin("tooth", (0.48, 0.44, 0.36), (0.62, 0.58, 0.50), (0.20, 0.14, 0.10), seed=98.0, bumps=0.0, growth=0.0, mottle=0.15, size=0.4, gloss=0.6),
    "spot": lambda: mats.pallid("spot", base=(0.12, 0.09, 0.12), flush=(0.14, 0.07, 0.09), seed=99.0),
    "flesh": lambda: mats.flesh("flesh", pale=(0.72, 0.56, 0.52), red=(0.42, 0.05, 0.05), seed=100.0),
}


def stalker(hi):
    g = Maker(hi, STALKER_FLAT, STALKER_MATERIALS)
    L = STALKER
    up = (0, 0, 1)
    sp, half, tall = L["spacing"], L["seg_half"], L["seg_tall"]
    # Each length is closed and round at both ends and reaches well into its neighbours, so a bend never opens a gap
    # and there is no joint line to bake dirt into.
    over = 0.075

    # ------------------------------------------------------------- Seg: a length of trunk, soft, furrowed round, a low ridge along its back
    full = sp + 2 * over
    keys = [(0.00, half * 0.26, tall * 0.26, 0, 0.0), (0.015, half * 0.52, tall * 0.52, 0, 0.0), (0.045, half * 0.82, tall * 0.82, 0, 0.0), (0.09, half * 0.97, tall * 0.97, 0, 0.0),
            (full * 0.5, half, tall, 0, 0.0),
            (full - 0.09, half * 0.97, tall * 0.97, 0, 0.0), (full - 0.045, half * 0.82, tall * 0.82, 0, 0.0), (full - 0.015, half * 0.52, tall * 0.52, 0, 0.0), (full, half * 0.26, tall * 0.26, 0, 0.0)]
    body = g.skin("length", (0, -sp * 0.5 - over, 0.0), (0, 1, 0), up, keys, around=12, step=0.045, power=2.1)
    if hi:
        # Two furrows a length, a soft fold low on each flank, and skin that is never quite even.
        real.displace(body, lambda p: -0.0035 * math.exp(-((abs(p.y) - 0.085) / 0.020) ** 2) * (0.4 + 0.6 * min(1.0, abs(p.x) / 0.10))
                                      + 0.005 * math.exp(-((p.z + 0.07) / 0.04) ** 2) * min(1.0, abs(p.x) / 0.12)
                                      + 0.005 * noise.noise(p * 7.0) + 0.002 * noise.noise(p * 23.0))
    g.add(body, "skin", "Seg")

    # ------------------------------------------------------------- Tail: a length of tail, thin from side to side, a fin above and below
    th, tt = L["tail_half"], L["tail_tall"]
    keys = [(0.00, th * 0.30, tt * 0.40, 0, 0.0), (0.015, th * 0.55, tt * 0.70, 0, 0.0), (0.045, th * 0.84, tt * 0.90, 0, 0.0), (0.09, th * 0.97, tt * 0.98, 0, 0.0),
            (full * 0.5, th, tt, 0, 0.0),
            (full - 0.09, th * 0.97, tt * 0.98, 0, 0.0), (full - 0.045, th * 0.84, tt * 0.90, 0, 0.0), (full - 0.015, th * 0.55, tt * 0.70, 0, 0.0), (full, th * 0.30, tt * 0.40, 0, 0.0)]
    fin = g.skin("tail length", (0, -sp * 0.5 - over, 0.0), (0, 1, 0), up, keys, around=10, step=0.045, power=1.45)
    if hi:
        # Fine rays in the fin, above and below the muscle.
        real.displace(fin, lambda p: 0.0018 * math.sin(p.y * 2 * math.pi / 0.03) * (1.0 if abs(p.z) > tt * 0.5 else 0.0) + 0.003 * noise.noise(p * 9.0))
    g.add(fin, "skin", "Tail")

    # ------------------------------------------------------------- Head: flat and pear-shaped, no eyes; its lower jaw is a piece of its own
    hl, hh, ht = L["head"], L["head_half"], L["head_tall"]
    back = -sp * 0.5
    # Behind the hinge the head is whole; in front of it only the upper half (the jaw is the rest).
    keys = [(-0.07, half * 0.12, tall * 0.12, 0, 0.0), (-0.05, half * 0.55, tall * 0.55, 0, 0.0), (0.00, half * 0.84, tall * 0.84, 0, 0.0), (0.06, hh * 0.88, ht * 1.06, 0, 0.0), (0.15, hh * 0.99, ht * 0.98, 0, 0.005), (0.22, hh, ht * 0.80, 0, 0.022),
            (0.34, hh * 0.88, ht * 0.62, 0, 0.034), (0.48, hh * 0.76, ht * 0.52, 0, 0.030), (0.57, hh * 0.66, ht * 0.42, 0, 0.022), (0.605, hh * 0.52, ht * 0.30, 0, 0.014), (hl, hh * 0.26, ht * 0.14, 0, 0.008)]
    keys = [(k[0] + 0.07,) + tuple(k[1:]) for k in keys]
    skull = g.skin("skull", (0, back - 0.07, 0.0), (0, 1, 0), up, keys, around=14, step=0.06, power=2.2)
    if hi:
        # Jaw muscles bulging behind the cheeks, a shallow groove down the middle of the skull.
        real.displace(skull, lambda p: 0.010 * math.exp(-(((abs(p.x) - 0.11) / 0.05) ** 2 + ((p.y + 0.02) / 0.07) ** 2)) * (1.0 if p.z > 0 else 0.0)
                                       - 0.004 * math.exp(-(p.x / 0.012) ** 2) * (1.0 if p.z > 0.02 else 0.0) + 0.003 * noise.noise(p * 10.0))
    g.add(skull, "skin", "Head")
    # The roof of its mouth, seen when the jaw drops.
    g.add(g.ball("palate", (0, 0.215, -0.012), 0.10, scale=(1.75, 2.42, 0.26), around=8), "mouth", "Head")
    for side in (-1, 1):
        # Where eyes would be: a dark smudge under the skin, nothing more.
        g.add(g.ball("eye smudge", (side * 0.105, 0.22, 0.060), 0.013, scale=(1.0, 1.3, 0.4), around=5), "spot", "Head", only='hi')
        g.add(g.ball("nostril", (side * 0.030, 0.435, 0.030), 0.007, around=5), "spot", "Head", only='hi')
        root = L["gill_root"]
        g.add(g.ball("gill socket", (side * (root[0] - 0.012), root[1], root[2]), 0.045, scale=(0.8, 1.2, 0.9), around=6), "skin", "Head")

    # ------------------------------------------------------------- Jaw: the lower jaw, from its hinge; it hangs under the snout and drops open
    jl = L["jaw"]
    jk = [(0.00, hh * 0.90, 0.036, 0, -0.042), (0.10, hh * 0.93, 0.042, 0, -0.044), (0.22, hh * 0.82, 0.040, 0, -0.042), (0.32, hh * 0.70, 0.032, 0, -0.034),
          (0.375, hh * 0.56, 0.022, 0, -0.024), (jl, hh * 0.26, 0.010, 0, -0.012)]
    g.add(g.skin("jaw", (0, 0, 0), (0, 1, 0), up, jk, around=10, step=0.07, power=2.2), "skin", "Jaw")
    # Its tongue and the floor of its mouth.
    g.add(g.ball("tongue", (0, 0.17, -0.010), 0.085, scale=(1.15, 2.1, 0.16), around=8), "mouth", "Jaw")

    # ------------------------------------------------------------- Gill: three feathered plumes from one root, the same either side, lifting
    gl, gs = L["gill"], L["gill_spread"]
    for k, (aside, lift, reach) in enumerate(((-1.0, 0.03, 0.88), (0.0, 0.10, 1.0), (1.0, 0.03, 0.88))):
        end = gl * reach
        # A frond: flat, soft, its edge scalloped by the filaments (which are only in the baked model and the pictures).
        pk, n = [], 12
        for i in range(n + 1):
            t = i / n
            wide = (0.016 + 0.030 * math.sin(math.pi * min(1.0, t * 1.15)) ** 0.7) * (1.0 if i % 2 else 0.86) * (1.0 - 0.55 * t * t)
            pk.append((end * t, max(0.004, wide * 0.62), 0.015 * (1.0 - 0.7 * t) + 0.003, -aside * gs * 0.94 * t ** 1.3, lift * t ** 1.4))
        g.add(g.skin("frond", (0, 0, 0), (0, 1, 0), up, pk, around=6, step=end / n, power=2.0), "gill", "Gill")
        if hi:
            g.add(g.skin("frond stem", (0, 0, 0.004), (0, 1, 0), up, [(k_[0], 0.008 * (1.0 - 0.6 * k_[0] / end), 0.008 * (1.0 - 0.6 * k_[0] / end), k_[3], k_[4]) for k_ in pk],
                         around=5, step=end / n, power=2.0), "gill", "Gill", only='hi')
            barbs = 26
            for b in range(barbs):
                y = end * (0.10 + 0.86 * b / (barbs - 1))
                rs, rf, os_, of = real.along(pk, y)
                for way in (-1, 1):
                    g.add(g.spine("barb", (-os_, y, of + 0.004), (way * 0.85, 0.5, 0.0), rs * 1.15, 0.0045, around=3), "gill", "Gill", only='hi')
        # The fringe: soft filaments standing out either side of the frond and drooping at their tips.
        fringe = 7
        for b in range(fringe):
            y = end * (0.16 + 0.74 * b / (fringe - 1))
            rs, rf, os_, of = real.along(pk, y)
            for way in (-1, 1):
                long = 0.070 * (1.0 - 0.45 * b / fringe) * (1.0 if (b + (way > 0)) % 2 else 0.8)
                g.add(g.spine("filament", (-os_ + way * rs * 0.5, y, of), (way * 0.80, 0.42, 0.42), long, 0.0075, around=3, bend=(0, 0.25, -0.9)), "gill", "Gill")
    g.add(g.ball("gill root", (0, 0.01, 0), 0.034, around=6), "gill", "Gill")

    # ------------------------------------------------------------- Leg and Shin: thin and soft, the shin ending in three blunt toes
    ll, sl, lr = L["leg"], L["shin"], L["leg_radius"]
    lk = [(0.00, lr * 1.10, lr * 1.25, 0, 0), (ll * 0.30, lr * 1.0, lr * 1.12, 0, 0.004), (ll * 0.70, lr * 0.72, lr * 0.78, 0, 0.002), (ll, lr * 0.66, lr * 0.66, 0, 0)]
    g.add(g.skin("upper leg", (0, 0, 0), (0, 1, 0), up, lk, around=7, step=0.12, power=2.0), "skin", "Leg")
    g.add(g.ball("hip", (0, 0, 0), lr * 1.22, around=7), "skin", "Leg")
    sk_ = [(0.00, lr * 0.68, lr * 0.68, 0, 0), (sl * 0.25, lr * 0.80, lr * 0.74, 0, 0), (sl * 0.65, lr * 0.56, lr * 0.54, 0, 0), (sl * 0.88, lr * 0.50, lr * 0.44, 0, 0.004), (sl, lr * 0.86, lr * 0.34, 0, 0.012)]
    g.add(g.skin("lower leg", (0, 0, 0), (0, 1, 0), up, sk_, around=7, step=0.12, power=2.0), "skin", "Shin")
    g.add(g.ball("knee", (0, 0, 0), lr * 0.72, around=7), "skin", "Shin")
    for k in (-1, 0, 1):
        # Toes: they turn up toward the leg's upper side, which is the way the ground lies when it stands.
        tk = [(0.00, 0.013, 0.011, 0, 0), (0.05, 0.011, 0.009, 0, 0.004), (0.085 + (0.014 if k == 0 else 0.0), 0.0085, 0.007, 0, 0.008)]
        g.add(g.skin("toe", (k * 0.024, sl - 0.012, 0.008), (k * 0.36, 0.62, 0.70), (0, -0.7, 0.62), tk, around=5, step=0.06, power=2.0), "skin", "Shin")

    leftovers(g, L["stump"], "skin")
    return g


def stalker_sockets():
    L = STALKER
    sp = L["spacing"]
    return [oa.socket("SegEnd", (0, sp * 0.5, 0)), oa.socket("SegSide", (L["seg_half"], 0, 0)), oa.socket("SegTop", (0, 0, L["seg_tall"])),
            oa.socket("TailSide", (L["tail_half"], 0, 0)), oa.socket("TailTop", (0, 0, L["tail_tall"])),
            oa.socket("HeadEnd", (0, L["head"] - sp * 0.5, 0)), oa.socket("HeadSide", (L["head_half"], 0, 0)), oa.socket("HeadTop", (0, 0, L["head_tall"])),
            oa.socket("JawHinge", L["jaw_hinge"]), oa.socket("JawEnd", (0, L["jaw"], 0)), oa.socket("Mouth", (0, L["mouth"], 0)),
            oa.socket("GillR", L["gill_root"]), oa.socket("GillEnd", (0, L["gill"], 0)), oa.socket("GillSide", (L["gill_spread"], 0, 0)),
            oa.socket("HipR", L["hip"]), oa.socket("LegEnd", (0, L["leg"], 0)), oa.socket("ShinEnd", (0, L["shin"], 0)), oa.socket("LegSide", (L["leg_radius"], 0, 0)),
            oa.socket("StumpRim", (L["stump"], 0, 0))]


# ---------------------------------------------------------------------- building

CREATURES = {"Strider": (strider, strider_sockets, 2048), "Sprinter": (sprinter, sprinter_sockets, 2048), "Roller": (roller, roller_sockets, 2048),
             "Hunter": (hunter, hunter_sockets, 2048), "Screamer": (screamer, screamer_sockets, 2048),
             "Ambusher": (ambusher, ambusher_sockets, 2048), "Stalker": (stalker, stalker_sockets, 2048)}
# Shapes a far copy does without: from where it is seen they are thinner than a pixel.
FAR_LEAVE_OUT = {"Sprinter": ("bristle", "claw")}
# How a far copy is cut when the usual way starves it: (share of the triangles, fewest a shape may keep, small
# rounded shapes that are thinned like the big ones). The Sprinter's head is many small shapes, which would all be
# kept whole and leave its abdomen and legs sixteen triangles each.
FAR_HOW = {"Sprinter": (0.40, 40, ("hip", "back plate", "eye knob", "beak", "joint", "jaw hinge", "head plate", "pad", "tooth", "eye"))}
# Shapes too many and too thin to be given their own places on the pictures (each would take a patch and its margin,
# and hundreds of them took half the picture from the body): they all share one small patch, baked from a swatch
# of the named material.
# Shapes whose far copy is the shape itself: a thinned arch crumples and opens.
FAR_KEEP = {"Roller": ("plate", "brow"), "Hunter": ("plate",), "Ambusher": ("heap",)}
FINE = {"Sprinter": (("bristle",), "hair")}
FAR_SHARE = 0.30
ROW = 1.6        # pieces are stood this far apart in a row for the bake (each is modelled round its own joint)


def joined(objects, name):
    """One object holding all of these shapes, each with its own hard edges and materials (the bake is many times
    quicker from one detailed shape than from hundreds)."""
    for o in objects:
        oa.auto_smooth(o.data, o.get("oa_smooth", 60.0))
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:
        o.hide_render = False
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.convert(target='MESH')
    bpy.ops.object.join()
    whole = bpy.context.view_layer.objects.active
    whole.name = name
    return whole


def build(name, folder, previews=None, draft=False, poses=None, reuse=False):
    maker, sockets_of, size = CREATURES[name]
    if draft:
        size //= 2
    oa.reset_scene()
    started = time.time()
    textures = os.path.join(folder, "Textures")
    reuse = reuse and os.path.exists(os.path.join(textures, name + "_Color.png"))
    low = maker(False)
    pieces = list(low.parts)
    low_parts = [(piece, low.parts[piece]) for piece in pieces]
    low_objects = [o for _, objects in low_parts for o in objects]
    triangles = sum(oa._triangle_count(o) for o in low_objects)
    # Every piece is modelled round its own joint, so for the bake they are stood apart in a row.
    def shift(parts, sign):
        for k, piece in enumerate(pieces):
            move = Matrix.Translation((sign * ROW * k, 0, 0))
            for o in parts[piece]:
                o.data.transform(move)
    shift(low.parts, 1)
    fine_names, swatch_material = FINE.get(name, ((), None))
    fine = [o for o in low_objects if o.name.split(".")[0] in fine_names]
    plain = [o for o in low_objects if o.name.split(".")[0] not in fine_names]
    swatch = None
    if fine:
        swatch = oa.box("swatch", (0, 0, 30.0), (0.06, 0.06, 0.004), (0.5, 0.5, 0.5), bevel=0)
        plain.append(swatch)
    mats.unwrap(plain, angle=66.0, margin=0.004)
    if fine:
        # The patch: the middle of the swatch's largest face. Every corner of every fine shape is put there.
        layer = swatch.data.uv_layers[oa.TEX]
        face = max(swatch.data.polygons, key=lambda p: p.normal.z)
        spot = sum((layer.data[i].uv for i in face.loop_indices), Vector((0, 0))) / len(face.loop_indices)
        for o in fine:
            mats.triangulate(o)
            uv = o.data.uv_layers.get(oa.TEX) or o.data.uv_layers.new(name=oa.TEX)
            o.data.uv_layers.active = uv
            uv.active_render = True
            for loop in uv.data:
                loop.uv = spot
    if reuse:
        # The pictures on disk were baked for these same shapes: nothing is baked or written again.
        shift(low.parts, -1)
        print("OA %s: using the pictures already in %s" % (name, textures))
    else:
        high = maker(True)
        high_objects = [o for piece in pieces for o in high.parts[piece]]
        print("OA %s: %d pieces, %d shapes and %d triangles in the game's model; %d shapes and %d triangles in the baked one"
              % (name, len(pieces), len(low_objects), triangles, len(high_objects), sum(oa._triangle_count(o) for o in high_objects)))
        for piece, objects in low_parts:
            print("OA   %-12s %5d triangles" % (piece, sum(oa._triangle_count(o) for o in objects)))
        shift(high.parts, 1)
        if swatch is not None:
            twin = oa.box("swatch high", (0, 0, 30.0), (0.06, 0.06, 0.004), (0.5, 0.5, 0.5), bevel=0)
            twin.data.materials.append(high.material(swatch_material))
            high_objects.append(twin)
        whole = joined(high_objects, name + " high")
        print("OA %s: shapes made in %.0f s" % (name, time.time() - started))
        mats.bake_set(name, plain, [whole], size=size, cage=0.022, samples=4 if draft else 24, folder=textures)
        print("OA %s: baked at %.0f s" % (name, time.time() - started))
        shift(low.parts, -1)
        whole.hide_render = True
    if swatch is not None:
        bpy.data.objects.remove(swatch)
    sockets = sockets_of()
    far = []
    if not reuse:
        oa.export(os.path.join(folder, name + ".bytes"), low_parts, sockets, textures=name)
    if not (reuse and poses):
        share, least, thin_too = FAR_HOW.get(name, (FAR_SHARE, 16, ()))
        left_out = FAR_LEAVE_OUT.get(name, ())
        counted = sum(oa._triangle_count(o) for o in low_objects if o.name.split(".")[0] not in left_out)
        far = oa.thin(low_parts, int(counted * share), drop_below=0.05, least=least, leave_out=left_out, thin_too=thin_too, keep=FAR_KEEP.get(name, ()))
    if not reuse:
        oa.export(os.path.join(folder, name + oa.FAR + ".bytes"), far, sockets, textures=name)
        print("OA %s: written at %.0f s" % (name, time.time() - started))
    if previews:
        os.makedirs(previews, exist_ok=True)
        pictures(name, low.parts, dict(far), textures, previews, draft, poses)


def pictures(name, parts, far_parts, textures, previews, draft, poses):
    look = mats.textured_material(name, textures)
    samples = 24 if draft else 96
    tag = name.lower()
    for o in [o for objects in list(parts.values()) + list(far_parts.values()) for o in objects]:
        o.hide_render = True
    if poses:
        # Frames from the game's own animator (tools/blender/creature_dump): every piece's place, frame by frame.
        frames = json.load(open(poses))
        if "only" in frames:
            frames["first"], frames["last"], frames["every"] = frames["only"], frames["only"] + 1, 1
        first, last, every = frames.get("first", 0), frames.get("last", len(frames["frames"])), frames.get("every", 1)
        # "clean": true draws the animal alone on plain pale grey with no floor and nobody beside it: a reference
        # picture (for a concept sheet, or to hand to a model generator).
        clean = bool(frames.get("clean"))
        extra = person(frames["person"]) if "person" in frames and not clean else []
        for k in range(first, last, every):
            shot = frames["frames"][k]
            placed = [(p["piece"], Matrix([p["m"][0:4], p["m"][4:8], p["m"][8:12], p["m"][12:16]])) for p in shot["pieces"]]
            shown = instances(parts, placed)
            for o in shown:
                if not o.data.materials or o.data.materials[0] != look:
                    o.data.materials.clear()
                    o.data.materials.append(look)
            eye, target = shot.get("eye", frames["eye"]), shot.get("target", frames["target"])
            # A frame may say where the person stands in it (someone being dragged).
            walker = person(shot["person"]) if "person" in shot and not clean else []
            shown = shown + walker
            mats.render(os.path.join(previews, "%s_%s_%04d.png" % (tag, frames["name"], k)), shown + extra, None, eye, target,
                        lens=frames.get("lens", 30), size=tuple(frames.get("size", (960, 600))), samples=frames.get("samples", 16), floor=None if clean else 0.0, sun=(52, 12, 28),
                        **({"world": (0.86, 0.86, 0.86), "sky": 1.15, "sun_energy": 2.6} if clean else {}))
            for o in shown:
                bpy.data.objects.remove(o)
        return
    def shoot(file, placed, eye, target, lens, size=(1400, 900), with_person=True, which=parts):
        shown = instances(which, placed)
        for o in shown:
            o.data.materials.clear()
            o.data.materials.append(look)
        extra = person((1.9, 2.6)) if with_person else []
        mats.render(os.path.join(previews, file), shown + extra, None, eye, target, lens=lens, size=size, samples=samples, floor=0.0, sun=(52, 12, 28))
        for o in shown + extra:
            bpy.data.objects.remove(o)
    if name != "Strider":
        # The other creatures' stills are frames of the game's own animator (--poses with one frame and its own camera).
        apart = 1.25 if name == "Roller" else 0.7 if name == "Hunter" else 1.0 if name in ("Screamer", "Ambusher") else 0.8 if name == "Stalker" else 0.45
        row = [(piece, Matrix.Translation(((k - 0.5 * (len(parts) - 1)) * apart + 0.2, 0, 0.30))) for k, piece in enumerate(parts)]
        shoot(tag + "_pieces.png", row, (0.2, 0.6, 4.6 * apart / 0.45 * 0.62 if name in ("Roller", "Hunter", "Screamer", "Ambusher", "Stalker") else 4.6), (0.2, 0.6, 0.3), 30, with_person=False)
        return
    stand = strider_stand()
    shoot(tag + "_whole.png", stand, (5.6, 6.6, 1.7), (0, 0, 1.6), 28)
    shoot(tag + "_body.png", stand, (1.5, 2.3, 2.9), (0, 0.05, 2.0), 40, with_person=False)
    if draft:
        return
    shoot(tag + "_under.png", stand, (0.9, 2.3, 1.2), (0, 0.15, 2.0), 35, with_person=False)
    shoot(tag + "_claw.png", stand, (1.6, 3.4, 1.5), (0.5, 1.6, 1.3), 50, with_person=False)
    shoot(tag + "_legs_lost.png", strider_stand(height=1.1, reach=1.95, sway=7.0, missing=("R2", "R3", "L1", "L0")), (5.6, 6.6, 1.7), (0, 0, 1.2), 28)
    shoot(tag + "_far_copy.png", stand, (5.6, 6.6, 1.7), (0, 0, 1.6), 28, which=far_parts)
    # Every piece laid out flat, as the game holds them.
    row = []
    for k, piece in enumerate(parts):
        row.append((piece, Matrix.Translation((-3.2 + 0.62 * k, 0, 0.35)) @ Matrix.Rotation(math.radians(0), 4, 'X')))
    shoot(tag + "_pieces.png", row, (0.2, 1.0, 5.2), (0.2, 1.0, 0.3), 30, with_person=False)


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    previews = args[args.index("--preview") + 1] if "--preview" in args else None
    only = args[args.index("--only") + 1].split(",") if "--only" in args else list(CREATURES)
    poses = args[args.index("--poses") + 1] if "--poses" in args else None
    if "--game" in args and "--draft" in args:
        sys.exit("a draft is never written to the game")
    if "--game" in args:
        folder = GAME
    elif "--write-to" in args:
        folder = args[args.index("--write-to") + 1]
    else:
        sys.exit("say where to write: --write-to <folder> for a trial, or --game for the game's own files")
    for creature in only:
        build(creature, folder, previews, draft="--draft" in args, poses=poses, reuse="--reuse" in args)
