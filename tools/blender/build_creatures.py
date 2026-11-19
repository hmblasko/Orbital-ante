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
# test StriderModelFile_MatchesTheLayout against Creatures/StriderLayout.cs. Metres, Blender's axes.
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
    """A standing pose worked out here, for still pictures only (the game's animator, StriderAnimator, is the real
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


# ---------------------------------------------------------------------- building

CREATURES = {"Strider": (strider, strider_sockets, 2048)}
FAR_SHARE = 0.30


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
            move = Matrix.Translation((sign * 1.6 * k, 0, 0))
            for o in parts[piece]:
                o.data.transform(move)
    shift(low.parts, 1)
    mats.unwrap(low_objects, angle=66.0, margin=0.004)
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
        whole = joined(high_objects, name + " high")
        print("OA %s: shapes made in %.0f s" % (name, time.time() - started))
        mats.bake_set(name, low_objects, [whole], size=size, cage=0.022, samples=4 if draft else 24, folder=textures)
        print("OA %s: baked at %.0f s" % (name, time.time() - started))
        shift(low.parts, -1)
        whole.hide_render = True
    sockets = sockets_of()
    far = []
    if not reuse:
        oa.export(os.path.join(folder, name + ".bytes"), low_parts, sockets, textures=name)
    if not (reuse and poses):
        far = oa.thin(low_parts, int(triangles * FAR_SHARE), drop_below=0.05, least=16)
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
        first, last, every = frames.get("first", 0), frames.get("last", len(frames["frames"])), frames.get("every", 1)
        extra = person(frames["person"]) if "person" in frames else []
        for k in range(first, last, every):
            shot = frames["frames"][k]
            placed = [(p["piece"], Matrix([p["m"][0:4], p["m"][4:8], p["m"][8:12], p["m"][12:16]])) for p in shot["pieces"]]
            shown = instances(parts, placed)
            for o in shown:
                if not o.data.materials or o.data.materials[0] != look:
                    o.data.materials.clear()
                    o.data.materials.append(look)
            eye, target = shot.get("eye", frames["eye"]), shot.get("target", frames["target"])
            mats.render(os.path.join(previews, "%s_%s_%04d.png" % (tag, frames["name"], k)), shown + extra, None, eye, target,
                        lens=frames.get("lens", 30), size=tuple(frames.get("size", (960, 600))), samples=frames.get("samples", 16), floor=0.0, sun=(52, 12, 28))
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
