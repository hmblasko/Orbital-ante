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


class Gun:
    """Collects one build of a gun: `hi` is False for the game's low model, True for the high one that is baked."""

    # material name -> (corner colour as seen on screen, smoothness, bare metal): what shows without the pictures.
    FLAT = {"steel": ((0.20, 0.21, 0.23), 0.40, 0.2), "cover": ((0.20, 0.21, 0.23), 0.40, 0.2),
            "magazine": ((0.24, 0.25, 0.25), 0.34, 0.2), "bright": ((0.55, 0.55, 0.53), 0.62, 1.0),
            "wood": ((0.47, 0.25, 0.13), 0.42, 0.0), "grip": ((0.27, 0.12, 0.08), 0.5, 0.0),
            "hollow": ((0.05, 0.05, 0.05), 0.05, 0.0)}

    def __init__(self, hi):
        self.hi = hi
        self.parts = {}         # piece name -> [objects]
        self.materials = {}
        self.count = 0

    def material(self, name):
        if name not in self.materials:
            self.materials[name] = {
                "steel": lambda: mats.steel("steel", seed=1.0),
                "cover": lambda: mats.steel("cover", seed=5.0, wear=1.25, patina=0.5),
                "magazine": lambda: mats.steel("magazine", coat=(0.026, 0.028, 0.027), coat_smooth=0.34, wear=1.5, patina=0.6, seed=9.0),
                "bright": lambda: mats.bright_steel("bright"),
                "wood": lambda: mats.laminate("wood", seed=2.0),
                "grip": lambda: mats.bakelite("grip", seed=4.0, checker=0.00045),
                "hollow": lambda: mats.hollow("hollow"),
            }[name]()
        return self.materials[name]

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
            colour, smooth, metal = self.FLAT[material]
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
    g.add(g.prism("catch", [(0.1075, 0.012), (0.1160, 0.012), (0.1125, -0.011), (0.1030, -0.0225), (0.0975, -0.0200), (0.1050, -0.0095)], 0.0115, edge=0.0009), "steel")
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


def sockets_of(name):
    places = {"Rifle": (("Muzzle", (0, 0.69, 0.045)), ("SupportHand", (0, 0.37, 0.01)), ("Magazine", (0, 0.15, 0.01)))}[name]
    return [oa.socket(n, p) for n, p in places]


GUNS = {"Rifle": (rifle, 2048)}
FAR_TRIANGLES, FAR_SHARE = 2000, 0.40
# How long each gun is and where its middle is, for framing pictures (the same as build_weapons.py).
SHOTS = {"Rifle": (0.75, (0, 0.16, 0.0))}


def build(name, folder, previews=None, draft=False):
    """draft: quarter-size pictures and few samples, to look at shape and colour quickly. Never for the game."""
    maker, size = GUNS[name]
    if draft:
        size //= 2
    oa.reset_scene()
    low, high = maker(False), maker(True)
    low_parts = [(piece, objects) for piece, objects in low.parts.items()]
    low_objects = [o for _, objects in low_parts for o in objects]
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
    oa.export(os.path.join(folder, name + ".bytes"), low_parts, sockets, textures=name)
    far = oa.thin(low_parts, min(FAR_TRIANGLES, int(triangles * FAR_SHARE)), drop_below=0.012)
    oa.export(os.path.join(folder, name + oa.FAR + ".bytes"), far, sockets, textures=name)
    if previews:
        os.makedirs(previews, exist_ok=True)
        look = mats.textured_material(name, textures)
        length, mid = SHOTS[name]
        far_objects = [o for _, objects in far for o in objects]
        for tag, shown in (("", low_objects), ("_far", far_objects)):
            mats.render(os.path.join(previews, name.lower() + tag + "_side.png"), shown, look, samples=40 if draft else 128, eye=(2.4 * length, mid[1], 0.25 * length), target=mid, lens=85, floor=-0.5 * length)
            if tag:
                continue
            mats.render(os.path.join(previews, name.lower() + "_close.png"), shown, look, (0.42, 0.02, 0.16), (0, 0.11, 0.02), lens=70, size=(1400, 900), samples=40 if draft else 128, floor=-0.5 * length)
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
