"""Builds the realistic guns with texture pictures (Matthew, 18 Nov 2026: "substantially more realistic and not
obviously made in blender"; brief docs/briefs/realistic-enemies-and-guns.md).

    blender -b -P tools/blender/build_guns_realistic.py -- --write-to <folder>     (a trial: nothing in the game changes)
    blender -b -P tools/blender/build_guns_realistic.py -- --game                  (writes the game's model and pictures)
    add  --preview <folder>  for pictures of the result,  --only Rifle  to build some

Each gun is described once and built twice (see oa_materials.py): a low model for the game and a high one whose
rounded edges, rivets, ribs and surface are baked into the low model's pictures. The gun's space is the same as
build_weapons.py: origin where the firing hand holds it, Y out of the barrel, Z up, metres; the sockets and the
overall length are the old guns', so hands, tracers and reloads still line up.

The rifle starts from a real thing: a stamped-steel Kalashnikov-pattern rifle with laminated wood furniture and a
ribbed steel magazine. No markings, numbers or maker's signs anywhere.
"""
import bpy, bmesh, sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import oa_blender as oa
import oa_real as real
import oa_materials as mats
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
GAME = os.path.normpath(os.path.join(HERE, "..", "..", "unity", "Assets", "OrbitalAnte", "Resources", "Models"))


# name -> (how it is made for the bake, corner colour as seen on screen, smoothness, bare metal). The last three
# are what the game shows without the pictures.
MATERIALS = {
    "steel": (lambda: mats.steel("steel", seed=1.0), (0.20, 0.21, 0.23), 0.40, 0.2),
    "cover": (lambda: mats.steel("cover", seed=5.0, wear=1.25, patina=0.5), (0.20, 0.21, 0.23), 0.40, 0.2),
    "magazine": (lambda: mats.steel("magazine", coat=(0.026, 0.028, 0.027), coat_smooth=0.34, wear=1.5, patina=0.6, seed=9.0), (0.24, 0.25, 0.25), 0.34, 0.2),
    "black": (lambda: mats.steel("black", coat=(0.011, 0.011, 0.012), coat_smooth=0.40, wear=0.9, patina=0.15, seed=3.0), (0.14, 0.14, 0.15), 0.40, 0.2),
    "anodised": (lambda: mats.steel("anodised", coat=(0.009, 0.009, 0.011), bare=(0.55, 0.56, 0.58), coat_smooth=0.52, wear=0.7, patina=0.0, seed=6.0), (0.12, 0.12, 0.13), 0.5, 0.2),
    "satin": (lambda: mats.steel("satin", coat=(0.36, 0.36, 0.35), bare=(0.62, 0.62, 0.60), coat_smooth=0.58, wear=0.8, patina=0.2, seed=7.0, coat_metal=1.0), (0.60, 0.60, 0.59), 0.58, 1.0),
    "brass": (lambda: mats.steel("brass", coat=(0.42, 0.25, 0.075), bare=(0.72, 0.55, 0.24), coat_smooth=0.55, wear=0.9, patina=0.9, seed=8.0, coat_metal=1.0), (0.66, 0.52, 0.26), 0.55, 1.0),
    "copper": (lambda: mats.steel("copper", coat=(0.40, 0.15, 0.075), bare=(0.75, 0.42, 0.30), coat_smooth=0.50, wear=0.7, patina=1.0, seed=11.0, coat_metal=1.0), (0.64, 0.40, 0.30), 0.5, 1.0),
    "bright": (lambda: mats.bright_steel("bright"), (0.55, 0.55, 0.53), 0.62, 1.0),
    "wood": (lambda: mats.laminate("wood", seed=2.0), (0.47, 0.25, 0.13), 0.42, 0.0),
    "walnut": (lambda: mats.laminate("walnut", seed=12.0, plies=0.0, dark=(0.030, 0.014, 0.007), light=(0.150, 0.068, 0.030)), (0.40, 0.25, 0.15), 0.40, 0.0),
    "grip": (lambda: mats.bakelite("grip", seed=4.0, checker=0.00045), (0.27, 0.12, 0.08), 0.5, 0.0),
    "rubber": (lambda: mats.polymer("rubber", (0.012, 0.012, 0.012), smoothness=0.10, seed=2.0, checker=0.0004), (0.11, 0.11, 0.11), 0.06, 0.0),
    "pad": (lambda: mats.polymer("pad", (0.014, 0.013, 0.012), smoothness=0.09, seed=5.0), (0.12, 0.12, 0.11), 0.06, 0.0),
    "olive": (lambda: mats.polymer("olive", (0.060, 0.075, 0.035), smoothness=0.24, seed=1.0), (0.29, 0.32, 0.21), 0.22, 0.0),
    "olive dark": (lambda: mats.polymer("olive dark", (0.036, 0.046, 0.024), smoothness=0.22, seed=4.0), (0.22, 0.25, 0.17), 0.2, 0.0),
    "cell paint": (lambda: mats.polymer("cell paint", (0.040, 0.150, 0.065), smoothness=0.35, seed=7.0), (0.22, 0.44, 0.28), 0.3, 0.0),
    "glass": (lambda: mats.glass("glass"), (0.08, 0.12, 0.15), 0.9, 0.0),
    "hollow": (lambda: mats.hollow("hollow"), (0.05, 0.05, 0.05), 0.05, 0.0),
}


class Gun:
    """Collects one build of a gun: `hi` is False for the game's low model, True for the high one that is baked."""

    def __init__(self, hi):
        self.hi = hi
        self.parts = {}         # piece name -> [objects]
        self.glows = {}         # piece name -> [objects]: things that shine, in the low build only
        self.materials = {}
        self.count = 0

    def material(self, name):
        if name not in self.materials:
            self.materials[name] = MATERIALS[name][0]()
        return self.materials[name]

    def glow(self, obj, piece, colour):
        """A shape that shines by itself (its piece's name ends in Glow): flat colour, no place on the pictures."""
        if self.hi:
            mesh = obj.data
            bpy.data.objects.remove(obj)
            bpy.data.meshes.remove(mesh)
            return None
        obj["oa_colour"] = list(colour)
        oa.paint(obj, wear=0, dirt=0, mottle=0, gradient=0)
        self.glows.setdefault(piece, []).append(obj)
        return obj

    def add(self, obj, material, piece="Body", only=None):
        """only: 'hi' for a shape that exists only to be baked (a rivet, a rib), 'lo' for one only in the game."""
        if only == ('lo' if self.hi else 'hi'):
            mesh = obj.data
            bpy.data.objects.remove(obj)
            bpy.data.meshes.remove(mesh)
            return None
        self.count += 1
        if self.hi:
            obj.data.materials.append(self.material(material))
            oa.auto_smooth(obj.data, 32.0)
        else:
            _, colour, smooth, metal = MATERIALS[material]
            obj["oa_colour"] = list(colour)
            oa.paint(obj, wear=0.25, dirt=0.4, mottle=0.4, seed=self.count, grain=20.0)
            real.made_of(obj, smooth, metal)
        self.parts.setdefault(piece, []).append(obj)
        return obj

    # ---- shapes. Every size is given once; `hi` only changes how round and how finely cut the result is.

    def prism(self, name, outline, width, x=0.0, edge=0.0012, chamfer=0.0, across='x', smooth=40.0):
        """An outline given thickness. across='x': outline is (forward, up), thickened sideways about x.
        across='y': outline is (sideways, up), thickened forward from y = x to x + width.
        edge: how round its edges really are (baked); chamfer: a flat cut the low model keeps on them, if any."""
        bm = bmesh.new()
        if across == 'x':
            a = [bm.verts.new((x - width / 2, p[0], p[1])) for p in outline]
            b = [bm.verts.new((x + width / 2, p[0], p[1])) for p in outline]
        else:
            a = [bm.verts.new((p[0], x, p[1])) for p in outline]
            b = [bm.verts.new((p[0], x + width, p[1])) for p in outline]
        n = len(outline)
        bm.faces.new(a)
        bm.faces.new(list(reversed(b)))
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((a[j], a[i], b[i], b[j]))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        # Only real corners are rounded: the short straight runs a curved outline is drawn with are left alone.
        corners = [e for e in bm.edges if len(e.link_faces) == 2 and e.calc_face_angle(0.0) > math.radians(20)]
        if self.hi and edge > 0:
            bmesh.ops.bevel(bm, geom=corners, offset=min(edge, width * 0.45), segments=3, profile=0.5, affect='EDGES', clamp_overlap=True)
        elif not self.hi and chamfer > 0:
            bmesh.ops.bevel(bm, geom=corners, offset=min(chamfer, width * 0.45), segments=1, profile=0.5, affect='EDGES', clamp_overlap=True)
        return oa._new_object(name, bm, (0.5, 0.5, 0.5), smooth)

    def box(self, name, centre, size, edge=0.001, chamfer=0.0, rot=(0, 0, 0)):
        if self.hi:
            return oa.box(name, centre, size, (0.5, 0.5, 0.5), bevel=edge, segments=3, rot=rot)
        return oa.box(name, centre, size, (0.5, 0.5, 0.5), bevel=chamfer, segments=1, rot=rot)

    def tube(self, name, start, end, radius, radius_end=None, around=14, edge=0.0006):
        around = int(round(around * 1.5))
        return oa.cylinder(name, start, end, radius, (0.5, 0.5, 0.5), radius_end=radius_end,
                           segments=around * 3 if self.hi else around, bevel=edge if self.hi else 0.0)

    def lathe(self, name, origin, axis, profile, around=14):
        return oa.lathe(name, origin, axis, profile, (0.5, 0.5, 0.5), segments=around * 4 if self.hi else around)

    def skin(self, name, origin, axis, forward, keys, around=16, step=0.05, power=2.6):
        """A rounded solid through cross-sections (wooden furniture, a grip). keys as oa_real.limb."""
        return real.limb(name, origin, axis, forward, keys, (0.5, 0.5, 0.5), n=around * 4 if self.hi else int(around * 1.5),
                         step=step / 8 if self.hi else step / 2, power=power)

    def dome(self, name, at, out, radius, height, around=12):
        """A low rounded head standing on a surface: a rivet or a pin end. `out` is the way it faces."""
        profile = [(0.0, radius), (height * 0.55, radius * 0.88), (height * 0.9, radius * 0.55), (height, radius * 0.18)]
        return self.lathe(name, Vector(at) - Vector(out).normalized() * 0.0002, out, profile, around)


def banana(y0, z0, length, half, start_deg, end_deg, steps=9, offset=0.0, t0=0.0, t1=1.0):
    """Outline of a strip that follows the magazine's curve, `half` each side of a line `offset` from its middle,
    between the shares t0 and t1 of its length. The whole magazine is offset 0 from 0 to 1."""
    spine = []
    y, z = y0, z0
    fine = 96
    for i in range(fine + 1):
        t = i / fine
        lean = math.radians(start_deg + (end_deg - start_deg) * t)
        spine.append((y, z, lean))
        y += math.sin(lean) * length / fine
        z -= math.cos(lean) * length / fine
    left, right = [], []
    for i in range(steps + 1):
        t = t0 + (t1 - t0) * i / steps
        y, z, lean = spine[min(fine, int(round(t * fine)))]
        dy, dz = math.sin(lean), -math.cos(lean)       # down the magazine
        ny, nz = -dz, dy                               # toward its front
        w = half * (1.0 - 0.05 * t) if offset == 0.0 else half
        left.append((y + ny * (offset - w), z + nz * (offset - w)))
        right.append((y + ny * (offset + w), z + nz * (offset + w)))
    return left + list(reversed(right))


def arch(half, base, shoulder, top, points):
    """Cross-section of a rounded lid: straight sides from `base` up to `shoulder`, then an arc over to `top`."""
    out = [(-half, base)]
    for i in range(points + 1):
        a = math.pi * i / points
        out.append((-half * math.cos(a), shoulder + (top - shoulder) * math.sin(a)))
    out.append((half, base))
    return out


def rifle(hi):
    g = Gun(hi)
    arc = 14 if hi else 5
    BORE = 0.045

    # ---- the stamped receiver: a folded steel box, its rear trunnion, and what is riveted and pinned to it.
    g.add(g.prism("receiver", [(-0.080, 0.014), (0.270, 0.014), (0.270, 0.059), (-0.066, 0.059), (-0.080, 0.046)], 0.037, edge=0.0016), "steel")
    g.add(g.prism("rear trunnion", [(-0.086, 0.044), (-0.060, 0.044), (-0.060, 0.070), (-0.072, 0.070), (-0.086, 0.058)], 0.031, edge=0.0012), "steel")
    g.add(g.box("spring button", (0, -0.0605, 0.066), (0.010, 0.006, 0.008), edge=0.0008), "steel")
    for side in (-1, 1):
        x = side * 0.0185
        for y, z in ((0.236, 0.050), (0.258, 0.050), (0.247, 0.026), (-0.052, 0.050), (-0.066, 0.030), (0.150, 0.048), (0.196, 0.022)):
            g.add(g.dome("rivet", (x, y, z), (side, 0, 0), 0.0031, 0.0009), "steel", only='hi')
        for y, z in ((0.060, 0.030), (0.086, 0.040), (0.028, 0.034)):
            g.add(g.dome("pin", (x, y, z), (side, 0, 0), 0.0026, 0.0005), "steel", only='hi')
        # The long dimple pressed in above the magazine: a dish, drawn as a rim round a lower middle.
        dish = [(0.0, 0.0), (0.0002, 0.62), (0.0009, 1.0)]
        bm = bmesh.new()
        rings = []
        for depth, share in dish:
            if share == 0.0:
                rings.append([bm.verts.new((x + side * depth, 0.150, 0.031))])
            else:
                rings.append([bm.verts.new((x + side * depth, 0.150 + 0.019 * share * math.cos(a), 0.031 + 0.0075 * share * math.sin(a)))
                              for a in [2 * math.pi * k / 40 for k in range(40)]])
        for k in range(40):
            bm.faces.new((rings[0][0], rings[1][k], rings[1][(k + 1) % 40]))
            bm.faces.new((rings[1][k], rings[2][k], rings[2][(k + 1) % 40], rings[1][(k + 1) % 40]))
        g.add(oa._new_object("dimple", bm, (0.5, 0.5, 0.5), 60.0), "steel", only='hi')

    # ---- the lid over the action, with its pressed ribs, and the bolt carrier showing in the port on the right.
    g.add(g.prism("cover", arch(0.0180, 0.058, 0.071, 0.0855, arc), 0.268, x=-0.060, across='y', edge=0.0009, smooth=50.0), "cover")
    for y in (-0.036, 0.004, 0.044):
        g.add(g.prism("cover rib", arch(0.0183, 0.066, 0.0713, 0.0860, 14), 0.011, x=y, across='y', edge=0.0045, smooth=60.0), "cover", only='hi')
    g.add(g.box("carrier", (0.0125, 0.150, 0.0655), (0.0135, 0.104, 0.011), edge=0.0012), "bright")
    g.add(g.box("port shadow", (0.0150, 0.150, 0.0585), (0.0086, 0.112, 0.003), edge=0.0), "hollow")
    g.add(g.tube("handle", (0.016, 0.190, 0.066), (0.043, 0.190, 0.069), 0.0048, radius_end=0.0042, around=10), "bright")
    g.add(g.tube("handle tip", (0.040, 0.190, 0.0687), (0.047, 0.190, 0.0695), 0.0062, around=10, edge=0.0015), "bright")

    # ---- the selector lever down the right side, with the notches it clicks into.
    g.add(g.prism("selector", [(0.104, 0.049), (0.109, 0.043), (0.103, 0.037), (0.012, 0.027), (-0.002, 0.026), (-0.002, 0.033), (0.006, 0.037), (0.092, 0.050)],
                  0.0024, x=0.0197, edge=0.0007), "steel")
    g.add(g.box("selector tab", (0.0255, 0.004, 0.0295), (0.011, 0.013, 0.0045), edge=0.001), "steel")
    g.add(g.dome("selector pivot", (0.0209, 0.098, 0.0435), (1, 0, 0), 0.0058, 0.0016), "steel")
    for z in (0.0285, 0.0215):
        g.add(g.box("selector notch", (0.01855, 0.000, z), (0.0006, 0.007, 0.0026), edge=0.0), "hollow", only='hi')

    # ---- rear sight: its block on the trunnion, the leaf, the slider and the notch plate.
    g.add(g.prism("sight block", [(0.212, 0.058), (0.292, 0.058), (0.292, 0.072), (0.276, 0.081), (0.212, 0.081)], 0.030, edge=0.0014), "steel")
    g.add(g.prism("sight leaf", [(0.214, 0.0815), (0.288, 0.0805), (0.288, 0.0845), (0.214, 0.0875)], 0.011, edge=0.0006), "steel")
    g.add(g.box("sight slider", (0, 0.252, 0.0850), (0.019, 0.008, 0.0062), edge=0.0008), "steel")
    g.add(g.prism("sight notch", [(0.2125, 0.082), (0.2165, 0.082), (0.2165, 0.0955), (0.2125, 0.0955)], 0.017, edge=0.0006), "steel")
    g.add(g.box("sight notch cut", (0, 0.2119, 0.0935), (0.0032, 0.0008, 0.005), edge=0.0), "hollow")
    for k in range(7):
        g.add(g.box("sight mark", (0, 0.226 + 0.0075 * k, 0.0871 - 0.0003 * k * 0.4), (0.008, 0.0007, 0.0006), edge=0.0, rot=(-2.3, 0, 0)), "hollow", only='hi')

    # ---- barrel, gas tube and block, front sight, muzzle, cleaning rod.
    g.add(g.tube("barrel", (0, 0.266, BORE), (0, 0.660, BORE), 0.0088, around=14), "steel")
    g.add(g.tube("gas tube", (0, 0.288, 0.0735), (0, 0.500, 0.0735), 0.0086, around=12), "steel")
    g.add(g.prism("gas block", [(0.474, 0.034), (0.512, 0.034), (0.512, 0.060), (0.505, 0.084), (0.489, 0.084), (0.474, 0.062)], 0.0235, edge=0.0018), "steel")
    g.add(g.tube("gas block pin", (-0.0125, 0.493, 0.052), (0.0125, 0.493, 0.052), 0.0026, around=8), "bright", only='hi')
    g.add(g.prism("front sight base", [(0.586, 0.020), (0.626, 0.020), (0.626, 0.058), (0.586, 0.058)], 0.0225, edge=0.0022, chamfer=0.002), "steel")
    g.add(g.box("front sight web", (0, 0.606, 0.064), (0.0225, 0.020, 0.014), edge=0.0012), "steel")
    for side in (-1, 1):
        g.add(g.prism("front sight ear", [(0.597, 0.068), (0.615, 0.068), (0.6175, 0.093), (0.611, 0.105), (0.601, 0.105), (0.5945, 0.093)],
                      0.0032, x=side * 0.0098, edge=0.0008), "steel")
    g.add(g.tube("front sight post", (0, 0.606, 0.070), (0, 0.606, 0.0985), 0.0021, around=8, edge=0.0004), "steel")
    brake = g.lathe("muzzle", (0, 0.650, BORE), (0, 1, 0), [(0.0, 0.0088), (0.002, 0.0118), (0.013, 0.0118), (0.015, 0.0106), (0.052, 0.0106)], around=14)
    bore = g.tube("bore", (0, 0.652, BORE), (0, 0.706, BORE), 0.0050, around=10, edge=0.0)
    for obj, reach in ((brake, 0.0), (bore, 0.0005)):
        # The spoon-shaped cut that throws gas up and to the right: the mouth slants, longest underneath.
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        normal = Vector((0.18, 0.80, 0.57)).normalized()
        cut = bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=Vector((0, 0.680, BORE)) + normal * reach,
                                     plane_no=normal, clear_outer=True)
        rim = [e for e in cut["geom_cut"] if isinstance(e, bmesh.types.BMEdge)]
        bmesh.ops.contextual_create(bm, geom=rim)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        bm.to_mesh(obj.data)
        bm.free()
    g.add(brake, "steel")
    g.add(bore, "hollow")
    g.add(g.tube("rod", (0, 0.452, 0.0265), (0, 0.662, 0.0265), 0.0031, around=8, edge=0.0), "bright")
    g.add(g.tube("rod head", (0, 0.660, 0.0265), (0, 0.672, 0.0265), 0.0043, around=8, edge=0.001), "bright")

    # ---- wooden furniture: laminated handguards and stock, a moulded grip.
    g.add(g.skin("lower handguard", (0, 0.277, 0), (0, 1, 0), (0, 0, 1),
                 [(0.0, 0.0200, 0.0225, 0, 0.0345), (0.030, 0.0215, 0.0235, 0, 0.0335), (0.075, 0.0262, 0.0240, 0, 0.0330),
                  (0.120, 0.0235, 0.0232, 0, 0.0338), (0.166, 0.0205, 0.0215, 0, 0.0355)], around=16, step=0.028, power=3.2), "wood")
    g.add(g.skin("upper handguard", (0, 0.293, 0), (0, 1, 0), (0, 0, 1),
                 [(0.0, 0.0172, 0.0150, 0, 0.0745), (0.020, 0.0190, 0.0166, 0, 0.0745), (0.118, 0.0186, 0.0162, 0, 0.0745),
                  (0.139, 0.0170, 0.0148, 0, 0.0745)], around=14, step=0.035, power=2.3), "wood")
    g.add(g.box("handguard catch", (0, 0.2735, 0.036), (0.0445, 0.0075, 0.048), edge=0.0014), "steel")
    g.add(g.box("handguard lever", (0.0228, 0.2735, 0.050), (0.003, 0.006, 0.016), edge=0.0008, rot=(25, 0, 0)), "steel")
    g.add(g.box("handguard band", (0, 0.4495, 0.036), (0.0440, 0.0110, 0.050), edge=0.0016), "steel")
    g.add(g.box("upper band", (0, 0.4370, 0.0745), (0.0350, 0.0080, 0.0310), edge=0.0014), "steel")
    loop = [(-0.022, 0.4495, 0.040), (-0.030, 0.4495, 0.043), (-0.031, 0.4495, 0.024), (-0.022, 0.4495, 0.026)]
    g.add(real.sweep("sling loop", loop, 0.0017, (0.5, 0.5, 0.5), n=24 if hi else 6, cuts=4 if hi else 1), "steel")
    grip_axis = Vector((0, -0.047, -0.112)).normalized()
    grip_front = Vector((0, 0.112, -0.047)).normalized()
    g.add(g.skin("grip", (0, -0.010, 0.020), grip_axis, grip_front,
                 [(0.0, 0.0150, 0.0250), (0.020, 0.0152, 0.0228), (0.070, 0.0160, 0.0215), (0.113, 0.0170, 0.0240), (0.1215, 0.0158, 0.0228), (0.1245, 0.0120, 0.0190)],
                 around=14, step=0.024, power=2.9), "grip")
    foot = Vector((0, -0.010, 0.020)) + grip_axis * 0.1243
    g.add(g.dome("grip screw", foot, grip_axis, 0.0042, 0.0007), "steel", only='hi')
    g.add(g.skin("stock", (0, -0.066, 0), (0, -1, 0), (0, 0, 1),
                 [(0.0, 0.0172, 0.0215, 0, 0.0365), (0.040, 0.0180, 0.0262, 0, 0.0305), (0.160, 0.0190, 0.0420, 0, 0.0125),
                  (0.284, 0.0200, 0.0575, 0, -0.0040)], around=18, step=0.07, power=3.4), "wood")
    g.add(g.skin("butt plate", (0, -0.3498, 0), (0, -1, 0), (0, 0, 1),
                 [(0.0, 0.0203, 0.0580, 0, -0.0040), (0.0036, 0.0200, 0.0577, 0, -0.0040), (0.0048, 0.0180, 0.0555, 0, -0.0040)],
                 around=18, step=0.0036, power=3.4), "steel")
    for z in (0.034, -0.044):
        g.add(g.dome("butt screw", (0, -0.3546, z), (0, -1, 0), 0.0040, 0.0006), "steel", only='hi')
    g.add(g.dome("butt trap", (0, -0.3545, -0.004), (0, -1, 0), 0.0105, 0.0005, around=14), "steel", only='hi')
    g.add(g.box("upper tang", (0, -0.092, 0.0582), (0.015, 0.044, 0.0022), edge=0.0008), "steel")
    g.add(g.dome("tang screw", (0, -0.104, 0.0592), (0, 0, 1), 0.0036, 0.0006), "bright", only='hi')
    swivel = [(-0.019, -0.262, -0.012), (-0.027, -0.262, -0.016), (-0.027, -0.292, -0.021), (-0.0195, -0.292, -0.020)]
    g.add(real.sweep("stock swivel", swivel, 0.0017, (0.5, 0.5, 0.5), n=24 if hi else 6, cuts=4 if hi else 1), "steel")
    g.add(g.box("swivel plate", (-0.0193, -0.277, -0.0165), (0.0016, 0.040, 0.012), edge=0.0006, rot=(9, 0, 0)), "steel")

    # ---- trigger, its guard and the magazine catch.
    g.add(g.prism("guard", [(0.1205, 0.0145), (0.1165, -0.0225), (0.1060, -0.0315), (-0.0080, -0.0315),
                            (-0.0080, -0.0292), (0.1050, -0.0292), (0.1142, -0.0215), (0.1182, 0.0145)], 0.0165, edge=0.0007), "steel")
    # The catch is a short flat tab lying against the front of the guard: hooked like the trigger it read as a second one.
    g.add(g.prism("catch", [(0.1090, 0.012), (0.1170, 0.012), (0.1152, -0.0150), (0.1100, -0.0185)], 0.0115, edge=0.0009), "steel")
    g.add(g.prism("trigger", [(0.046, 0.015), (0.0545, 0.015), (0.0510, 0.000), (0.0450, -0.0115), (0.0370, -0.0180), (0.0345, -0.0150), (0.0405, -0.0090), (0.0435, 0.000)],
                  0.0068, edge=0.0011), "steel")

    # ---- the magazine: pressed steel, curved, three long ribs a side, a spine and a floor plate.
    def strip(half, offset=0.0, t0=0.0, t1=1.0, steps=9):
        return banana(0.150, 0.016, 0.215, half, 6, 40, steps=steps, offset=offset, t0=t0, t1=t1)

    g.add(g.prism("magazine", strip(0.0335, steps=28 if hi else 14), 0.0290, edge=0.0026, chamfer=0.0022), "magazine", "Magazine")
    for side in (-1, 1):
        for offset, t0 in ((-0.0190, 0.20), (0.0, 0.14), (0.0190, 0.20)):
            g.add(g.prism("magazine rib", strip(0.0042, offset, t0, 0.90, 28), 0.0024, x=side * 0.0142, edge=0.00105, smooth=70.0),
                  "magazine", "Magazine", only='hi')
    g.add(g.prism("magazine spine", strip(0.0032, -0.0325, 0.10, 0.985, 28 if hi else 14), 0.0130, edge=0.0010), "magazine", "Magazine")
    g.add(g.prism("magazine floor", strip(0.0375, 0.001, 0.975, 1.012, 3), 0.0325, edge=0.0011, chamfer=0.001), "magazine", "Magazine")
    g.add(g.prism("magazine lug", strip(0.0040, 0.0345, 0.035, 0.075, 2), 0.0130, edge=0.0008), "magazine", "Magazine")
    return g


def strip(points, thickness):
    """Outline of a bent strip of sheet metal: the given line (forward, up) made `thickness` thick on its inside
    (the left of the way it runs)."""
    inner = []
    n = len(points)
    for i, (y, z) in enumerate(points):
        ay, az = points[max(0, i - 1)]
        by, bz = points[min(n - 1, i + 1)]
        dy, dz = by - ay, bz - az
        length = math.hypot(dy, dz) or 1.0
        inner.append((y - dz / length * thickness, z + dy / length * thickness))
    return list(points) + list(reversed(inner))


def bow(y0, y1, top, bottom, corner, steps=4):
    """The line of a trigger guard from its front root (y1, top) down, round two corners and back to (y0, top)."""
    line = [(y1, top)]
    for cy, a0 in ((y1 - corner, 0.0), (y0 + corner, 90.0)):
        for i in range(steps + 1):
            a = math.radians(a0 + 90.0 * i / steps)
            line.append((cy + math.cos(a) * corner, bottom + corner - math.sin(a) * corner))
    line.append((y0, top))
    return line


def pistol(hi):
    """A very large gas-operated pistol: a long fixed barrel with a ribbed top and a deep gas block under its
    mouth, a short slide behind it, brushed steel over a black frame, rubber grip."""
    g = Gun(hi)
    BORE = 0.062
    g.add(g.prism("frame", [(-0.046, 0.0462), (0.112, 0.0462), (0.112, 0.0330), (0.098, 0.0190), (-0.020, 0.0190), (-0.046, 0.0300)], 0.0300, edge=0.0016, chamfer=0.0012), "black")
    g.add(g.prism("tang", [(-0.046, 0.0462), (-0.046, 0.0330), (-0.0570, 0.0350), (-0.0640, 0.0405), (-0.0625, 0.0445)], 0.0200, edge=0.0030, chamfer=0.0016), "black")
    # The barrel: a wedge-topped bar the whole way, deeper at the front where the gas is tapped.
    top = [(-0.0168, 0.0462), (-0.0168, 0.0660), (-0.0092, 0.0838), (0.0092, 0.0838), (0.0168, 0.0660), (0.0168, 0.0462)]
    g.add(g.prism("barrel", top, 0.1200, x=0.0320, across='y', edge=0.0011), "satin")
    g.add(g.prism("gas block", [(0.1120, 0.0470), (0.1500, 0.0470), (0.1500, 0.0400), (0.1420, 0.0330), (0.1120, 0.0330)], 0.0336, edge=0.0014, chamfer=0.0010), "satin")
    g.add(g.tube("gas plug", (0, 0.1490, 0.0405), (0, 0.1512, 0.0405), 0.0048, around=8, edge=0.0005), "black")
    slide = [(-0.0176, 0.0462), (-0.0176, 0.0668), (-0.0098, 0.0850), (0.0098, 0.0850), (0.0176, 0.0668), (0.0176, 0.0462)]
    g.add(g.prism("slide", slide, 0.0790, x=-0.0480, across='y', edge=0.0011), "satin")
    for k in range(8):
        g.add(g.box("rail cut", (0, 0.050 + 0.0118 * k, 0.0839), (0.0150, 0.0040, 0.0007), edge=0.0), "hollow", only='hi')
    g.add(g.box("port", (0.0030, 0.0060, 0.0851), (0.0130, 0.0270, 0.0006), edge=0.0), "hollow")
    for side in (-1, 1):
        for k in range(7):
            g.add(g.box("serration", (side * 0.01765, -0.0420 + 0.0050 * k, 0.0570), (0.0005, 0.0020, 0.0180), edge=0.0, rot=(-14, 0, 0)), "hollow", only='hi')
        g.add(g.box("flute", (side * 0.01685, 0.0900, 0.0575), (0.0005, 0.0900, 0.0034), edge=0.0), "hollow", only='hi')
        g.add(g.prism("safety", [(-0.0360, 0.0660), (-0.0180, 0.0650), (-0.0150, 0.0690), (-0.0180, 0.0725), (-0.0360, 0.0720)], 0.0026, x=side * 0.0186, edge=0.0008), "black")
        g.add(g.dome("frame pin", (side * 0.0150, 0.0700, 0.0370), (side, 0, 0), 0.0024, 0.0004), "bright", only='hi')
        g.add(g.dome("frame pin", (side * 0.0150, -0.0300, 0.0390), (side, 0, 0), 0.0022, 0.0004), "bright", only='hi')
        g.add(g.dome("gas pin", (side * 0.0168, 0.1300, 0.0400), (side, 0, 0), 0.0028, 0.0005), "black", only='hi')
        g.add(g.dome("grip screw", (side * 0.0162, -0.0240, -0.0300), (side, 0, 0), 0.0030, 0.0005), "bright", only='hi')
    g.add(g.prism("slide stop", [(0.0180, 0.0440), (0.0560, 0.0440), (0.0590, 0.0405), (0.0560, 0.0372), (0.0180, 0.0384)], 0.0024, x=-0.0158, edge=0.0008), "black")
    g.add(g.tube("crown", (0, 0.1500, BORE), (0, 0.1548, BORE), 0.0104, around=12, edge=0.0010), "satin")
    g.add(g.tube("bore", (0, 0.1400, BORE), (0, 0.1552, BORE), 0.0062, around=10, edge=0.0), "hollow")
    g.add(g.prism("front sight", [(0.1300, 0.0836), (0.1480, 0.0836), (0.1440, 0.0915), (0.1370, 0.0915)], 0.0038, edge=0.0005), "black")
    g.add(g.prism("rear sight", [(-0.0470, 0.0848), (-0.0300, 0.0848), (-0.0330, 0.0915), (-0.0470, 0.0915)], 0.0200, edge=0.0008), "black")
    g.add(g.box("rear notch", (0, -0.0473, 0.0895), (0.0034, 0.0008, 0.0042), edge=0.0), "hollow")
    g.add(g.prism("hammer", [(-0.0478, 0.0500), (-0.0570, 0.0560), (-0.0625, 0.0700), (-0.0575, 0.0760), (-0.0510, 0.0740), (-0.0478, 0.0680)], 0.0088, edge=0.0010), "black")
    g.add(g.prism("guard", strip(bow(0.0060, 0.1010, 0.0200, -0.0190, 0.0110, 4 if hi else 3), 0.0028), 0.0136, edge=0.0008), "black")
    g.add(g.prism("trigger", [(0.0440, 0.0200), (0.0525, 0.0200), (0.0505, 0.0080), (0.0455, -0.0030), (0.0400, -0.0080), (0.0380, -0.0055), (0.0420, 0.0000), (0.0440, 0.0080)], 0.0064, edge=0.0010), "black")
    axis = Vector((0, -0.026, -0.106)).normalized()
    front = Vector((0, 0.106, -0.026)).normalized()
    g.add(g.skin("grip", (0, -0.0130, 0.0270), axis, front,
                 [(0.0, 0.0150, 0.0290), (0.020, 0.0160, 0.0268), (0.050, 0.0166, 0.0282), (0.085, 0.0164, 0.0286), (0.103, 0.0162, 0.0300), (0.107, 0.0140, 0.0276)],
                 around=14, step=0.020, power=3.2), "rubber")
    foot = Vector((0, -0.0130, 0.0270)) + axis * 0.1100
    # The magazine itself, up inside the grip: seen when it is out for a reload.
    g.add(g.box("magazine", foot - axis * 0.0480, (0.0210, 0.0400, 0.0900), edge=0.0016, chamfer=0.0010, rot=(-13.8, 0, 0)), "magazine", "Magazine")
    g.add(g.box("magazine floor", foot, (0.0300, 0.0620, 0.0076), edge=0.0014, chamfer=0.0010, rot=(-13.8, 0, 0)), "black", "Magazine")
    return g


def smg(hi):
    """A compact stamped-steel submachine gun: a boxy receiver with long pressed ribs, the magazine up through
    the grip, a folded steel stock."""
    g = Gun(hi)
    arc = 12 if hi else 7
    BORE = 0.046
    g.add(g.prism("receiver", [(-0.105, 0.018), (0.135, 0.018), (0.135, 0.074), (-0.105, 0.074)], 0.0420, edge=0.0020, chamfer=0.0014), "steel")
    g.add(g.prism("cover", arch(0.0202, 0.0735, 0.0790, 0.0868, arc), 0.232, x=-0.101, across='y', edge=0.0009, smooth=50.0), "cover")
    for side in (-1, 1):
        for z in (0.0370, 0.0560):
            g.add(g.box("rib", (side * 0.0210, 0.018, z), (0.0018, 0.200, 0.0068), edge=0.0030), "steel", only='hi')
        for y, z in ((-0.094, 0.026), (-0.094, 0.066), (0.124, 0.026), (0.124, 0.066), (0.050, 0.0465)):
            g.add(g.dome("rivet", (side * 0.0210, y, z), (side, 0, 0), 0.0028, 0.0008), "steel", only='hi')
        g.add(g.prism("grip panel", [(0.0130, 0.0060), (-0.0310, 0.0060), (-0.0375, -0.0880), (0.0070, -0.0880)], 0.0046, x=side * 0.0183, edge=0.0014), "grip")
        g.add(g.dome("grip screw", (side * 0.0206, -0.0120, -0.0400), (side, 0, 0), 0.0030, 0.0005), "bright", only='hi')
        g.add(g.box("stock arm", (side * 0.0238, 0.000, 0.0200), (0.0042, 0.200, 0.0100), edge=0.0012), "steel")
        for y in (0.112, -0.086):
            g.add(g.prism("sight ear", [(y - 0.0085, 0.0850), (y + 0.0085, 0.0850), (y + 0.0060, 0.1060), (y - 0.0060, 0.1060)], 0.0030, x=side * 0.0110, edge=0.0007), "steel")
    g.add(g.box("cocking slot", (0, 0.020, 0.0869), (0.0050, 0.110, 0.0006), edge=0.0), "hollow", only='hi')
    g.add(g.tube("knob stem", (0, 0.034, 0.0850), (0, 0.034, 0.0935), 0.0060, around=8, edge=0.0), "steel")
    g.add(g.tube("knob", (0, 0.034, 0.0925), (0, 0.034, 0.1020), 0.0118, around=16, edge=0.0020), "black")
    g.add(g.lathe("barrel nut", (0, 0.1340, BORE), (0, 1, 0), [(0.0, 0.0160), (0.003, 0.0212), (0.022, 0.0212), (0.0255, 0.0170), (0.0255, 0.0110)], around=20), "steel")
    for k in range(18):
        a = 2 * math.pi * k / 18
        g.add(g.box("nut cut", (math.cos(a) * 0.0213, 0.1465, BORE + math.sin(a) * 0.0213), (0.0007, 0.0170, 0.0026), edge=0.0, rot=(0, -math.degrees(a), 0)), "hollow", only='hi')
    g.add(g.tube("barrel", (0, 0.1550, BORE), (0, 0.2195, BORE), 0.0098, around=16, edge=0.0008), "steel")
    g.add(g.tube("bore", (0, 0.2000, BORE), (0, 0.2200, BORE), 0.0052, around=10, edge=0.0), "hollow")
    g.add(g.tube("front post", (0, 0.112, 0.0860), (0, 0.112, 0.1020), 0.0020, around=8, edge=0.0003), "steel")
    g.add(g.box("rear leaf", (0, -0.086, 0.0935), (0.0190, 0.0030, 0.0150), edge=0.0006), "steel")
    g.add(g.box("rear peep", (0, -0.0877, 0.0975), (0.0036, 0.0008, 0.0036), edge=0.0), "hollow")
    g.add(g.prism("grip frame", [(0.0200, 0.0200), (-0.0380, 0.0200), (-0.0450, -0.0980), (0.0130, -0.0980)], 0.0340, edge=0.0022, chamfer=0.0016), "steel")
    g.add(g.box("grip safety", (0, -0.0440, -0.0300), (0.0125, 0.0060, 0.0520), edge=0.0016, rot=(3.4, 0, 0)), "black")
    g.add(g.box("selector", (-0.0178, -0.0040, 0.0125), (0.0034, 0.0230, 0.0070), edge=0.0010), "black")
    g.add(g.prism("guard", strip(bow(0.0150, 0.0860, 0.0190, -0.0262, 0.0090, 4 if hi else 3), 0.0024), 0.0140, edge=0.0007), "steel")
    g.add(g.prism("trigger", [(0.0440, 0.0190), (0.0520, 0.0190), (0.0490, 0.0040), (0.0430, -0.0080), (0.0350, -0.0140), (0.0330, -0.0110), (0.0385, -0.0050), (0.0415, 0.0040)], 0.0066, edge=0.0011), "black")
    g.add(g.tube("stock hinge", (-0.0270, -0.0980, 0.0255), (0.0270, -0.0980, 0.0255), 0.0068, around=10, edge=0.0010), "steel")
    g.add(g.box("stock plate", (0, 0.0960, 0.0040), (0.0520, 0.0056, 0.0440), edge=0.0014, chamfer=0.0012), "steel")
    loop = [(-0.0210, -0.070, 0.0660), (-0.0290, -0.070, 0.0690), (-0.0290, -0.092, 0.0690), (-0.0210, -0.092, 0.0660)]
    g.add(real.sweep("sling loop", loop, 0.0016, (0.5, 0.5, 0.5), n=24 if hi else 6, cuts=4 if hi else 1), "steel")
    # The magazine: a long straight box with pressed ribs and witness holes.
    g.add(g.box("magazine", (0, -0.0110, -0.1680), (0.0240, 0.0385, 0.1540), edge=0.0022, chamfer=0.0016), "magazine", "Magazine")
    g.add(g.box("magazine floor", (0, -0.0110, -0.2475), (0.0280, 0.0440, 0.0075), edge=0.0012, chamfer=0.001), "magazine", "Magazine")
    for side in (-1, 1):
        g.add(g.box("magazine rib", (side * 0.0120, -0.0110, -0.170), (0.0014, 0.0090, 0.118), edge=0.0026), "magazine", "Magazine", only='hi')
        for k in range(4):
            g.add(g.box("witness hole", (side * 0.01205, 0.0020, -0.130 - 0.026 * k), (0.0004, 0.0034, 0.0034), edge=0.0), "hollow", "Magazine", only='hi')
    return g


CELL = (0.45, 1.0, 0.55)
LENS = (1.0, 0.25, 0.28)


def shotgun(hi):
    """A side-by-side break-action gun rebuilt to fire light: blued barrels wound with copper near brass mouths,
    a steel breech with brass-framed windows on the charge, a walnut stock, a power cell where the shells went."""
    g = Gun(hi)
    BORE = 0.052
    g.add(g.prism("breech", [(-0.070, 0.008), (0.215, 0.008), (0.215, 0.084), (0.192, 0.094), (-0.040, 0.094), (-0.070, 0.068)], 0.1040, edge=0.0040, chamfer=0.0035), "steel")
    g.add(g.prism("top lever", [(-0.052, 0.0940), (0.022, 0.0940), (0.022, 0.0985), (-0.040, 0.1040), (-0.056, 0.1010)], 0.0130, edge=0.0016), "bright")
    g.add(g.dome("lever pivot", (0, 0.016, 0.0982), (0, 0, 1), 0.0062, 0.0012), "bright", only='hi')
    g.add(g.box("hinge block", (0, 0.2260, 0.0500), (0.1200, 0.0300, 0.0700), edge=0.0035, chamfer=0.003), "steel")
    g.add(g.tube("hinge pin", (-0.0605, 0.2260, 0.0240), (0.0605, 0.2260, 0.0240), 0.0058, around=10, edge=0.0008), "bright", only='hi')
    for side in (-1, 1):
        x = side * 0.0300
        g.add(g.tube("barrel", (x, 0.212, BORE), (x, 0.596, BORE), 0.0262, around=12, edge=0.0), "steel")
        g.add(g.lathe("mouth", (x, 0.5780, BORE), (0, 1, 0), [(0.0, 0.0266), (0.006, 0.0312), (0.029, 0.0328), (0.034, 0.0300), (0.034, 0.0218), (0.012, 0.0206)], around=14), "brass")
        g.add(g.lathe("band", (x, 0.2620, BORE), (0, 1, 0), [(-0.009, 0.0262), (-0.007, 0.0296), (0.007, 0.0296), (0.009, 0.0262)], around=12), "brass")
        for i in range(6):
            g.add(g.lathe("coil", (x, 0.440 + 0.0205 * i, BORE), (0, 1, 0), [(-0.0085, 0.0262), (-0.0060, 0.0312), (0.0, 0.0326), (0.0060, 0.0312), (0.0085, 0.0262)], around=12), "copper")
        g.glow(oa.cylinder("emitter", (x, 0.5985, BORE), (x, 0.6040, BORE), 0.0204, CELL, segments=12), "CellGlow", CELL)
        g.add(g.box("window frame", (side * 0.0516, 0.0900, 0.0520), (0.0030, 0.0840, 0.0350), edge=0.0014), "brass")
        g.glow(oa.box("charge window", (side * 0.0524, 0.0900, 0.0520), (0.0030, 0.0680, 0.0200), CELL, bevel=0.0012, segments=1), "CellGlow", CELL)
        for y, z in ((0.044, 0.0380), (0.044, 0.0660), (0.136, 0.0380), (0.136, 0.0660)):
            g.add(g.dome("frame screw", (side * 0.0531, y, z), (side, 0, 0), 0.0020, 0.0005), "brass", only='hi')
        for y, z in ((-0.036, 0.050), (0.186, 0.026), (0.186, 0.074)):
            g.add(g.dome("rivet", (side * 0.0520, y, z), (side, 0, 0), 0.0036, 0.0010), "steel", only='hi')
    for k in range(6):
        g.add(g.box("fin", (0, 0.2840 + 0.0105 * k, BORE), (0.1260, 0.0036, 0.0680), edge=0.0010), "steel")
    for side in (-1, 1):
        g.add(g.box("side plate", (side * 0.0515, 0.0660, 0.0500), (0.0016, 0.2240, 0.0700), edge=0.0050), "steel", only='hi')
    g.add(g.prism("rib", [(0.215, 0.0715), (0.572, 0.0715), (0.572, 0.0790), (0.215, 0.0790)], 0.0100, edge=0.0008), "steel")
    for k in range(40):
        g.add(g.box("rib cut", (0, 0.224 + 0.0085 * k, 0.0791), (0.0096, 0.0016, 0.0005), edge=0.0), "hollow", only='hi')
    g.add(g.dome("bead", (0, 0.5620, 0.0788), (0, 0, 1), 0.0034, 0.0050, around=10), "brass")
    g.add(g.box("barrel tie", (0, 0.4180, BORE), (0.0200, 0.0160, 0.0260), edge=0.0016), "steel")
    g.add(g.skin("fore-end", (0, 0.2480, 0), (0, 1, 0), (0, 0, 1),
                 [(0.0, 0.0540, 0.0200, 0, 0.0130), (0.080, 0.0575, 0.0245, 0, 0.0090), (0.150, 0.0550, 0.0215, 0, 0.0120), (0.192, 0.0500, 0.0170, 0, 0.0165)],
                 around=18, step=0.05, power=3.0), "walnut")
    axis = Vector((0, -0.052, -0.112)).normalized()
    front = Vector((0, 0.112, -0.052)).normalized()
    g.add(g.skin("grip", (0, -0.0120, 0.0200), axis, front,
                 [(0.0, 0.0180, 0.0290), (0.030, 0.0176, 0.0262), (0.090, 0.0184, 0.0250), (0.122, 0.0196, 0.0270), (0.131, 0.0180, 0.0250), (0.134, 0.0130, 0.0200)],
                 around=14, step=0.027, power=2.9), "walnut")
    g.add(g.skin("stock", (0, -0.0580, 0), (0, -1, 0), (0, 0, 1),
                 [(0.0, 0.0200, 0.0280, 0, 0.0490), (0.050, 0.0212, 0.0320, 0, 0.0420), (0.170, 0.0225, 0.0470, 0, 0.0200), (0.286, 0.0235, 0.0610, 0, 0.0010)],
                 around=18, step=0.07, power=3.2), "walnut")
    g.add(g.skin("butt pad", (0, -0.3438, 0), (0, -1, 0), (0, 0, 1),
                 [(0.0, 0.0238, 0.0615, 0, 0.0010), (0.0180, 0.0234, 0.0605, 0, 0.0010), (0.0222, 0.0200, 0.0570, 0, 0.0010)], around=18, step=0.011, power=3.2), "pad")
    g.add(g.prism("guard", strip(bow(0.0040, 0.1220, 0.0100, -0.0370, 0.0130, 4 if hi else 3), 0.0026), 0.0150, edge=0.0008), "steel")
    for y, lean in ((0.046, 0.0), (0.076, 0.004)):
        g.add(g.prism("trigger", [(y, 0.0100), (y + 0.0080, 0.0100), (y + 0.0050 + lean, -0.0050), (y - 0.0010 + lean, -0.0165), (y - 0.0085, -0.0225), (y - 0.0105, -0.0195), (y - 0.0050, -0.0140), (y - 0.0020 + lean, -0.0050)],
                      0.0066, edge=0.0011), "bright")
    # The power cell: a painted can with a brass cap and a pull.
    g.add(g.tube("cell", (0, 0.1680, 0.0100), (0, 0.1680, -0.0680), 0.0340, around=12, edge=0.0030), "magazine", "Magazine")
    g.add(g.lathe("cell band", (0, 0.1680, -0.0240), (0, 0, -1), [(-0.012, 0.0340), (-0.010, 0.0356), (0.010, 0.0356), (0.012, 0.0340)], around=18), "cell paint", "Magazine")
    g.add(g.tube("cell cap", (0, 0.1680, -0.0660), (0, 0.1680, -0.0790), 0.0290, around=12, edge=0.0025), "brass", "Magazine")
    g.add(g.box("cell pull", (0, 0.1680, -0.0840), (0.0090, 0.0300, 0.0090), edge=0.0020), "black", "Magazine")
    return g


def sniper(hi):
    """A long bolt-action rifle in a green thumb-hole stock: fluted heavy barrel, slotted brake, a very large
    scope, a bipod folded under."""
    g = Gun(hi)
    BORE = 0.050
    g.add(g.tube("action", (0, -0.060, BORE), (0, 0.252, BORE), 0.0222, around=16, edge=0.0020), "black")
    a = math.radians(24)
    g.add(g.box("port", (math.cos(a) * 0.0221, 0.0900, BORE + math.sin(a) * 0.0221), (0.0008, 0.0800, 0.0150), edge=0.0, rot=(0, -24, 0)), "hollow")
    g.add(g.box("bolt body", (math.cos(a) * 0.0223, 0.0840, BORE + math.sin(a) * 0.0223), (0.0008, 0.0560, 0.0104), edge=0.0, rot=(0, -24, 0)), "bright")
    g.add(g.tube("bolt arm", (0.0200, -0.0200, BORE), (0.0500, -0.0300, 0.0320), 0.0052, around=8, edge=0.0), "bright")
    ball = oa.sphere("bolt ball", (0.0520, -0.0310, 0.0300), 0.0122, (0.5, 0.5, 0.5), segments=32 if hi else 12)
    g.add(ball, "pad")
    g.add(g.tube("bolt shroud", (0, -0.0900, BORE), (0, -0.0580, BORE), 0.0180, around=10, edge=0.0025), "black")
    g.add(g.tube("barrel", (0, 0.250, BORE), (0, 0.840, BORE), 0.0152, radius_end=0.0126, around=16, edge=0.0), "black")
    for i in range(6):
        a = math.radians(60 * i + 30)
        g.add(g.box("flute", (math.cos(a) * 0.0143, 0.4800, BORE + math.sin(a) * 0.0143), (0.0012, 0.2900, 0.0056), edge=0.0, rot=(0, -math.degrees(a), 0)), "hollow", only='hi')
    g.add(g.lathe("brake", (0, 0.8260, BORE), (0, 1, 0), [(0.0, 0.0128), (0.005, 0.0205), (0.058, 0.0205), (0.072, 0.0170), (0.072, 0.0085), (0.030, 0.0085), (0.030, 0.0005)], around=20), "black")
    for y in (0.8460, 0.8640, 0.8820):
        g.add(g.box("brake slot", (0, y, BORE), (0.0420, 0.0075, 0.0120), edge=0.0), "hollow")
    R, C = 0.0100, 0.0060
    g.add(g.prism("fore-end", [(0.060, 0.040), (0.070, -0.004), (0.440, 0.008), (0.470, 0.028), (0.470, 0.040)], 0.0540, edge=R, chamfer=C), "olive")
    g.add(g.prism("trigger housing", [(-0.046, 0.042), (0.066, 0.042), (0.066, 0.006), (0.020, 0.010), (-0.046, 0.012)], 0.0500, edge=R, chamfer=C), "olive")
    g.add(g.prism("stock top", [(-0.040, 0.044), (-0.110, 0.050), (-0.360, 0.050), (-0.360, 0.022), (-0.120, 0.018), (-0.050, 0.010)], 0.0480, edge=R, chamfer=C), "olive")
    g.add(g.prism("stock bottom", [(-0.170, 0.000), (-0.360, -0.014), (-0.360, -0.090), (-0.200, -0.062)], 0.0450, edge=R, chamfer=C), "olive")
    g.add(g.prism("stock back", [(-0.300, 0.040), (-0.366, 0.040), (-0.366, -0.080), (-0.300, -0.070)], 0.0510, edge=R, chamfer=C), "olive")
    g.add(g.prism("grip bridge", [(-0.066, -0.070), (-0.200, -0.040), (-0.200, -0.066), (-0.070, -0.100)], 0.0380, edge=0.0090, chamfer=0.005), "olive")
    axis = Vector((0, -0.054, -0.112)).normalized()
    front = Vector((0, 0.112, -0.054)).normalized()
    g.add(g.skin("grip", (0, -0.0110, 0.0160), axis, front,
                 [(0.0, 0.0200, 0.0290), (0.030, 0.0196, 0.0262), (0.090, 0.0200, 0.0250), (0.122, 0.0206, 0.0262), (0.130, 0.0170, 0.0220)], around=14, step=0.027, power=3.0), "olive dark")
    g.add(g.prism("cheek piece", [(-0.130, 0.052), (-0.290, 0.052), (-0.282, 0.084), (-0.150, 0.088)], 0.0390, edge=0.0085, chamfer=0.005), "olive dark")
    for y in (-0.170, -0.250):
        g.add(g.tube("cheek post", (0, y, 0.040), (0, y, 0.058), 0.0050, around=8, edge=0.0), "bright")
    g.add(g.prism("butt pad", [(-0.364, -0.084), (-0.388, -0.076), (-0.388, 0.036), (-0.364, 0.044)], 0.0520, edge=0.0050, chamfer=0.004), "pad")
    g.add(g.prism("guard", strip(bow(0.0000, 0.0960, 0.0130, -0.0330, 0.0120, 4 if hi else 3), 0.0026), 0.0140, edge=0.0008), "black")
    g.add(g.prism("trigger", [(0.0400, 0.0100), (0.0480, 0.0100), (0.0450, -0.0040), (0.0390, -0.0150), (0.0310, -0.0210), (0.0290, -0.0180), (0.0345, -0.0125), (0.0375, -0.0040)], 0.0066, edge=0.0011), "bright")
    sz = 0.122
    scope = [(-0.150, 0.0005), (-0.150, 0.0250), (-0.142, 0.0290), (-0.085, 0.0290), (-0.070, 0.0200), (0.130, 0.0200), (0.170, 0.0380), (0.262, 0.0400), (0.270, 0.0372), (0.270, 0.0005)]
    g.add(g.lathe("scope", (0, 0.020, sz), (0, 1, 0), scope, around=24), "anodised")
    g.add(g.tube("eyepiece glass", (0, -0.1310, sz), (0, -0.1302, sz), 0.0225, around=12, edge=0.0), "glass")
    g.add(g.lathe("zoom ring", (0, -0.0700, sz), (0, 1, 0), [(-0.012, 0.0290), (-0.010, 0.0310), (0.010, 0.0310), (0.012, 0.0290)], around=16), "pad")
    for k in range(28):
        a = 2 * math.pi * k / 28
        g.add(g.box("ring cut", (math.cos(a) * 0.0311, -0.0700, sz + math.sin(a) * 0.0311), (0.0007, 0.0190, 0.0030), edge=0.0, rot=(0, -math.degrees(a), 0)), "hollow", only='hi')
    g.add(g.tube("turret top", (0, 0.060, sz + 0.0180), (0, 0.060, sz + 0.0420), 0.0128, around=10, edge=0.0018), "anodised")
    g.add(g.tube("turret side", (-0.0180, 0.060, sz), (-0.0420, 0.060, sz), 0.0128, around=10, edge=0.0018), "anodised")
    for y in (0.005, 0.110):
        g.add(g.lathe("scope ring", (0, y, sz), (0, 1, 0), [(-0.008, 0.0200), (-0.0065, 0.0252), (0.0065, 0.0252), (0.008, 0.0200)], around=14), "black")
        g.add(g.box("ring foot", (0, y, 0.0880), (0.0210, 0.0150, 0.0300), edge=0.0016), "black")
        for side in (-1, 1):
            g.add(g.dome("ring screw", (side * 0.0105, y, 0.0900), (side, 0, 0), 0.0030, 0.0008), "bright", only='hi')
    g.add(g.box("rail", (0, 0.058, 0.0750), (0.0200, 0.1700, 0.0080), edge=0.0008), "black")
    for k in range(13):
        g.add(g.box("rail slot", (0, -0.014 + 0.012 * k, 0.0786), (0.0204, 0.0050, 0.0016), edge=0.0), "hollow", only='hi')
    g.glow(oa.cylinder("front glass", (0, 0.2905, sz), (0, 0.2925, sz), 0.0340, LENS, segments=18), "LensGlow", LENS)
    g.add(g.box("bipod block", (0, 0.4400, 0.0000), (0.0380, 0.0240, 0.0200), edge=0.0020), "black")
    for side in (-1, 1):
        g.add(g.tube("bipod leg", (side * 0.0200, 0.4360, -0.0060), (side * 0.0300, 0.2260, -0.0140), 0.0058, around=8, edge=0.0), "black")
        g.add(g.tube("bipod foot", (side * 0.0300, 0.2320, -0.0140), (side * 0.0300, 0.2120, -0.0140), 0.0092, around=8, edge=0.0015), "pad")
    g.add(g.prism("magazine", [(0.118, 0.010), (0.200, 0.010), (0.206, -0.058), (0.126, -0.066)], 0.0300, edge=0.0022, chamfer=0.0016), "magazine", "Magazine")
    g.add(g.prism("magazine floor", [(0.120, -0.058), (0.212, -0.050), (0.212, -0.0600), (0.122, -0.0720)], 0.0350, edge=0.0014, chamfer=0.001), "magazine", "Magazine")
    return g


def sockets_of(name):
    places = {"Rifle": (("Muzzle", (0, 0.69, 0.045)), ("SupportHand", (0, 0.37, 0.01)), ("Magazine", (0, 0.15, 0.01))),
              "Pistol": (("Muzzle", (0, 0.155, 0.062)), ("SupportHand", (0, 0.0, -0.03)), ("Magazine", (0, -0.022, -0.07))),
              "Smg": (("Muzzle", (0, 0.220, 0.046)), ("SupportHand", (0, 0.100, 0.006)), ("Magazine", (0, -0.011, -0.098))),
              "Shotgun": (("Muzzle", (0, 0.612, 0.052)), ("SupportHand", (0, 0.335, 0.000)), ("Magazine", (0, 0.168, 0.000))),
              "Sniper": (("Muzzle", (0, 0.900, 0.050)), ("SupportHand", (0, 0.330, 0.000)), ("Magazine", (0, 0.160, 0.005)))}[name]
    return [oa.socket(n, p) for n, p in places]


# The pictures' size: 2048 for the long guns the player holds, 1024 for the small ones (a 1024 picture on a
# pistol is finer per centimetre than 2048 on a rifle).
GUNS = {"Rifle": (rifle, 2048), "Pistol": (pistol, 1024), "Smg": (smg, 1024), "Shotgun": (shotgun, 2048), "Sniper": (sniper, 2048)}
FAR_TRIANGLES, FAR_SHARE = 2000, 0.40
# What a small gun's far copy does without: at 9 m and beyond these are a pixel or less.
FAR_LEAVE_OUT = {"Smg": ("knob stem", "sling loop", "selector", "grip safety", "rear peep", "front post", "stock hinge", "sight ear", "grip screw"),
                 "Pistol": ("safety", "slide stop", "rear notch", "gas plug", "tang"),
                 "Shotgun": ("fin", "frame screw", "lever pivot"),
                 "Sniper": ("brake slot", "port", "bolt body", "eyepiece glass", "cheek post", "bipod foot", "zoom ring", "ring foot", "turret side", "bolt arm")}
# Thin strips that crumple when thinned.
FAR_KEEP = {"Sniper": ("guard",)}
# How long each gun is and where its middle is, for framing pictures (the same as build_weapons.py).
SHOTS = {"Rifle": (0.75, (0, 0.16, 0.0)), "Pistol": (0.36, (0, 0.045, 0.0)), "Smg": (0.60, (0, 0.05, -0.07)),
         "Shotgun": (1.12, (0, 0.12, 0.0)), "Sniper": (1.45, (0, 0.26, 0.02))}


def build(name, folder, previews=None, draft=False):
    """draft: quarter-size pictures and few samples, to look at shape and colour quickly. Never for the game."""
    maker, size = GUNS[name]
    if draft:
        size //= 2
    oa.reset_scene()
    low, high = maker(False), maker(True)
    low_parts = [(piece, objects) for piece, objects in low.parts.items()]
    low_objects = [o for _, objects in low_parts for o in objects]
    glow_parts = [(piece, objects) for piece, objects in low.glows.items()]
    glow_objects = [o for _, objects in glow_parts for o in objects]
    for o in glow_objects:          # they shine in the game, but must not light or shade the bake
        o.hide_render = True
        o["oa_glow"] = True
    high_objects = [o for objects in high.parts.values() for o in objects]
    triangles = sum(oa._triangle_count(o) for o in low_objects)
    print("OA %s: %d shapes and %d triangles in the game's model; %d shapes and %d triangles in the baked one"
          % (name, len(low_objects), triangles, len(high_objects), sum(oa._triangle_count(o) for o in high_objects)))
    mats.unwrap(low_objects)
    textures = os.path.join(folder, "Textures")
    mats.bake_set(name, low_objects, high_objects, size=size, samples=6 if draft else 32, folder=textures)
    for o in high_objects:
        o.hide_render = True
    sockets = sockets_of(name)
    low_parts = low_parts + glow_parts
    oa.export(os.path.join(folder, name + ".bytes"), low_parts, sockets, textures=name)
    far = oa.thin(low_parts, min(FAR_TRIANGLES, int(triangles * FAR_SHARE)), drop_below=0.012, leave_out=FAR_LEAVE_OUT.get(name, ()), keep=FAR_KEEP.get(name, ()))
    oa.export(os.path.join(folder, name + oa.FAR + ".bytes"), far, sockets, textures=name)
    if previews:
        os.makedirs(previews, exist_ok=True)
        look = mats.textured_material(name, textures)
        length, mid = SHOTS[name]
        far_objects = [o for piece, objects in far for o in objects if not piece.endswith("Glow")]
        if glow_objects:
            shine = bpy.data.materials.new("oa_glow")
            shine.use_nodes = True
            emit = shine.node_tree.nodes.new("ShaderNodeEmission")
            emit.inputs[0].default_value = (*[oa._srgb_to_linear(c) for c in glow_objects[0]["oa_colour"][:3]], 1.0)
            emit.inputs[1].default_value = 4.0
            shine.node_tree.links.new(emit.outputs[0], shine.node_tree.nodes["Material Output"].inputs["Surface"])
            for o in glow_objects:
                o.data.materials.append(shine)
        for tag, shown in (("", low_objects + glow_objects), ("_far", far_objects + glow_objects)):
            mats.render(os.path.join(previews, name.lower() + tag + "_side.png"), shown, look, samples=40 if draft else 128, eye=(2.4 * length, mid[1], 0.25 * length), target=mid, lens=85, floor=-0.5 * length)
            if tag:
                continue
            mats.render(os.path.join(previews, name.lower() + "_close.png"), shown, look, (0.56 * length, mid[1] - 0.19 * length, 0.21 * length), (0, mid[1] - 0.07 * length, 0.03 * length), lens=70, size=(1400, 900), samples=40 if draft else 128, floor=-0.5 * length)
            if draft:
                continue
            mats.render(os.path.join(previews, name.lower() + "_front.png"), shown, look, (1.2 * length, mid[1] + 1.9 * length, 0.8 * length), mid, lens=70, floor=-0.5 * length)
            mats.render(os.path.join(previews, name.lower() + "_held.png"), shown, look, (-0.5 * length, mid[1] - 1.5 * length, 0.55 * length), (0, mid[1] + 0.2 * length, 0), lens=45, floor=-0.5 * length)
            mats.render(os.path.join(previews, name.lower() + "_left.png"), shown, look, (-2.4 * length, mid[1], 0.25 * length), mid, lens=85, floor=-0.5 * length)


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    previews = args[args.index("--preview") + 1] if "--preview" in args else None
    only = args[args.index("--only") + 1].split(",") if "--only" in args else list(GUNS)
    if "--game" in args and "--draft" in args:
        sys.exit("a draft is never written to the game")
    if "--game" in args:
        folder = GAME
    elif "--write-to" in args:
        folder = args[args.index("--write-to") + 1]
    else:
        sys.exit("say where to write: --write-to <folder> for a trial, or --game for the game's own files")
    for gun in only:
        build(gun, folder, previews, draft="--draft" in args)
