"""Materials and texture baking for the realistic models (Matthew, 18 Nov 2026: enemies and guns "substantially more
realistic and not obviously made in blender"; brief docs/briefs/realistic-enemies-and-guns.md).

Shared by every realistic build script (guns, enemy bodies, alien creatures), so there is one idea of what steel,
wood or cloth looks like. How a textured model is made:

1. The script builds every shape twice from the same numbers: a LOW one (the game's mesh: few triangles, sharp
   edges) and a HIGH one (never shipped: rounded edges, many segments, plus small extra shapes such as rivets, ribs
   and grooves that exist only to be baked).
2. Each high shape is given one of the materials below. They are worked out from the shape itself: bare metal
   where edges stick out, grime where things crowd together, grain, scratches and dents from noise.
3. unwrap() lays every low shape out on one square picture; bake_set() then looks at the high model from just
   outside the low one and writes down, for every point of the picture, the colour, the direction the surface
   faces (so sharp low edges catch the light as rounded ones) and the mask (hollows, smoothness, bare metal).
4. oa.export(..., textures=name) writes the low model with its place on the pictures.

Everything is in metres. Works on Blender 4.0 to 4.2.
"""
import bpy, bmesh, math, os
from mathutils import Vector
import oa_blender as oa


# ------------------------------------------------------------------ a small language for node trees

class Tree:
    """Builds a material's node tree from Python expressions. Values are node sockets or plain numbers/colours."""

    def __init__(self, name):
        self.mat = bpy.data.materials.new(name)
        self.mat.use_nodes = True
        self.t = self.mat.node_tree
        self.t.nodes.clear()
        self.coord = self.t.nodes.new("ShaderNodeTexCoord").outputs["Object"]
        geo = self.t.nodes.new("ShaderNodeNewGeometry")
        self.pointiness, self.normal = geo.outputs["Pointiness"], geo.outputs["Normal"]

    def _set(self, socket, value):
        if hasattr(value, "is_output"):
            self.t.links.new(value, socket)
        elif isinstance(value, (tuple, list)) and len(value) == 3 and socket.type == 'RGBA':
            socket.default_value = (*value, 1.0)
        else:
            socket.default_value = value

    def math(self, op, a, b=0.0, clamp=False):
        n = self.t.nodes.new("ShaderNodeMath")
        n.operation, n.use_clamp = op, clamp
        self._set(n.inputs[0], a)
        self._set(n.inputs[1], b)
        return n.outputs[0]

    def add(self, a, b): return self.math('ADD', a, b)
    def sub(self, a, b): return self.math('SUBTRACT', a, b)
    def mul(self, a, b): return self.math('MULTIPLY', a, b)
    def sat(self, a): return self.math('ADD', a, 0.0, clamp=True)

    def step(self, x, lo, hi):
        """0 below lo, 1 above hi, eased between."""
        n = self.t.nodes.new("ShaderNodeMapRange")
        n.interpolation_type = 'SMOOTHSTEP'
        self._set(n.inputs["Value"], x)
        n.inputs["From Min"].default_value, n.inputs["From Max"].default_value = lo, hi
        return n.outputs["Result"]

    def mix(self, a, b, t):
        """Colour a to colour b by t."""
        n = self.t.nodes.new("ShaderNodeMix")
        n.data_type = 'RGBA'
        self._set(n.inputs[0], t)
        self._set(n.inputs[6], a)
        self._set(n.inputs[7], b)
        return n.outputs[2]

    def lerp(self, a, b, t):
        n = self.t.nodes.new("ShaderNodeMix")
        n.data_type = 'FLOAT'
        self._set(n.inputs[0], t)
        self._set(n.inputs[2], a)
        self._set(n.inputs[3], b)
        return n.outputs[0]

    def stretched(self, scale, shift=(0, 0, 0)):
        n = self.t.nodes.new("ShaderNodeMapping")
        self.t.links.new(self.coord, n.inputs["Vector"])
        n.inputs["Scale"].default_value = scale
        n.inputs["Location"].default_value = shift
        return n.outputs["Vector"]

    def noise(self, scale, detail=4.0, rough=0.55, stretch=(1, 1, 1), shift=(0, 0, 0), distortion=0.0):
        n = self.t.nodes.new("ShaderNodeTexNoise")
        self.t.links.new(self.stretched(stretch, shift), n.inputs["Vector"])
        n.inputs["Scale"].default_value = scale
        n.inputs["Detail"].default_value = detail
        n.inputs["Roughness"].default_value = rough
        n.inputs["Distortion"].default_value = distortion
        return n.outputs["Fac"]

    def bands(self, direction, period, distortion=0.0, detail=2.0, stretch=(1, 1, 1)):
        """Stripes across `direction` ('X', 'Y', 'Z' or 'DIAGONAL') every `period` metres, 0 to 1."""
        n = self.t.nodes.new("ShaderNodeTexWave")
        n.wave_type, n.bands_direction = 'BANDS', direction
        self.t.links.new(self.stretched(stretch), n.inputs["Vector"])
        n.inputs["Scale"].default_value = 2 * math.pi / (20.0 * period)
        n.inputs["Distortion"].default_value = distortion
        n.inputs["Detail"].default_value = detail
        return n.outputs["Fac"]

    def axis(self, which):
        n = self.t.nodes.new("ShaderNodeSeparateXYZ")
        self.t.links.new(self.coord, n.inputs[0])
        return n.outputs[which]

    def facing(self, which):
        n = self.t.nodes.new("ShaderNodeSeparateXYZ")
        self.t.links.new(self.normal, n.inputs[0])
        return self.math('ABSOLUTE', n.outputs[which])

    def hollows(self, distance=0.02):
        """1 in the open, toward 0 where other surfaces crowd round (within `distance`)."""
        n = self.t.nodes.new("ShaderNodeAmbientOcclusion")
        n.samples = 16
        n.inputs["Distance"].default_value = distance
        return n.outputs["AO"]

    def edges(self, lo=0.012, hi=0.15, radius=0.003):
        """1 on edges and corners (where things rub), 0 on flats: how far the surface's facing differs from the
        facing of the same surface with its edges rounded off by `radius`. Inside corners count too, so use it
        together with hollows() where that matters."""
        bevel = self.t.nodes.new("ShaderNodeBevel")
        bevel.samples = 6
        bevel.inputs["Radius"].default_value = radius
        dot = self.t.nodes.new("ShaderNodeVectorMath")
        dot.operation = 'DOT_PRODUCT'
        self.t.links.new(bevel.outputs["Normal"], dot.inputs[0])
        self.t.links.new(self.normal, dot.inputs[1])
        open_air = self.step(self.hollows(0.006), 0.55, 0.85)
        return self.mul(self.step(self.sub(1.0, dot.outputs["Value"]), lo, hi), open_air)

    def finish(self, colour, smoothness, metal, height=None, occlusion=None):
        """Ends the tree: a real surface (for baking directions and for pictures) and two flat outputs to bake."""
        t = self.t
        bsdf = t.nodes.new("ShaderNodeBsdfPrincipled")
        self._set(bsdf.inputs["Base Color"], colour)
        self._set(bsdf.inputs["Roughness"], self.sub(1.0, smoothness))
        self._set(bsdf.inputs["Metallic"], metal)
        if height is not None:
            bump = t.nodes.new("ShaderNodeBump")
            bump.inputs["Strength"].default_value = 1.0
            bump.inputs["Distance"].default_value = 1.0
            self._set(bump.inputs["Height"], height)
            t.links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
        flat = t.nodes.new("ShaderNodeEmission")
        self._set(flat.inputs["Color"], colour)
        pack = t.nodes.new("ShaderNodeCombineColor")
        self._set(pack.inputs[0], occlusion if occlusion is not None else self.hollows(0.03))
        self._set(pack.inputs[1], smoothness)
        self._set(pack.inputs[2], metal)
        mask = t.nodes.new("ShaderNodeEmission")
        t.links.new(pack.outputs[0], mask.inputs["Color"])
        out = t.nodes.new("ShaderNodeOutputMaterial")
        t.links.new(bsdf.outputs[0], out.inputs["Surface"])
        self.mat["oa_surface"], self.mat["oa_colour"], self.mat["oa_mask"] = bsdf.name, flat.name, mask.name
        self.mat["oa_out"] = out.name
        return self.mat


def show(what):
    """Switches every baked material between its real surface ('surface') and its flat outputs ('colour', 'mask')."""
    for mat in bpy.data.materials:
        if "oa_out" in mat:
            t = mat.node_tree
            t.links.new(t.nodes[mat["oa_" + what]].outputs[0], t.nodes[mat["oa_out"]].inputs["Surface"])


# ------------------------------------------------------------------ the materials

DUST = (0.050, 0.042, 0.033)     # old oil and dirt packed into corners: dark, a little brown


def steel(name, coat=(0.017, 0.018, 0.021), bare=(0.42, 0.41, 0.39), coat_smooth=0.50, wear=1.0, patina=0.35, seed=0.0,
          dirt=0.7, coat_metal=0.0):
    """Gun steel under a dark coat (bluing, phosphate or baked paint) that has rubbed through to bright metal on
    edges and high spots. The coat is not bare metal (it does not mirror); the rubbed places are.
    coat_metal 1 makes the whole thing bare metal with `coat` its colour: brushed stainless, brass, copper."""
    m = Tree(name)
    big = m.noise(14.0, 5.0, 0.6, shift=(seed, 0, 0))
    fine = m.noise(260.0, 3.0, 0.7, shift=(0, seed, 0))
    # The coat: a little lighter and darker in soft patches, a brown cast where it has aged.
    colour = m.mix(tuple(c * 0.72 for c in coat), tuple(c * 1.45 for c in coat), big)
    aged = m.step(m.noise(7.0, 6.0, 0.65, shift=(3.1, seed, 0)), 0.56, 0.74)
    colour = m.mix(colour, (0.060, 0.042, 0.030), m.mul(aged, patina))
    # Rubbed-through edges, broken up so they are never an even line; fine hairline scratches along the gun.
    # (Only some of every edge: a line of bright metal all the way round every part looks drawn on.)
    rubbed = m.sat(m.mul(m.mul(m.edges(), m.step(m.noise(75.0, 4.0, 0.7, shift=(seed, seed, 0)), 0.44, 0.70)), m.add(0.35, big)))
    rubbed = m.mul(rubbed, wear)
    scratch = m.step(m.noise(46.0, 2.0, 0.5, stretch=(18.0, 0.55, 18.0), shift=(0, 0, seed)), 0.71, 0.78)
    scratch = m.mul(m.mul(scratch, m.step(big, 0.35, 0.7)), 0.55 * wear)
    worn = m.sat(m.add(rubbed, scratch))
    colour = m.mix(colour, bare, worn)
    # Dust and old oil where parts crowd together.
    near = m.hollows(0.012)
    grime = m.mul(m.sub(1.0, m.step(near, 0.35, 0.9)), dirt)
    colour = m.mix(colour, DUST, m.mul(grime, 0.8))
    smoothness = m.lerp(m.lerp(coat_smooth - 0.10, coat_smooth + 0.08, big), 0.66, worn)
    smoothness = m.lerp(smoothness, 0.10, grime)
    metal = m.sat(m.sub(m.math('MAXIMUM', worn, coat_metal), grime))
    height = m.add(m.mul(fine, 0.000035), m.mul(scratch, -0.00006))
    return m.finish(colour, smoothness, metal, height)


def bright_steel(name, seed=0.0):
    """Polished working steel that is oiled and handled: the bolt carrier, a charging handle, a cleaning rod."""
    m = Tree(name)
    big = m.noise(30.0, 4.0, 0.6, shift=(seed, 0, 0))
    lines = m.noise(60.0, 2.0, 0.5, stretch=(1.0, 0.04, 1.0), shift=(0, seed, 0))
    colour = m.mix((0.20, 0.20, 0.20), (0.46, 0.45, 0.43), m.lerp(big, lines, 0.5))
    near = m.hollows(0.010)
    grime = m.sub(1.0, m.step(near, 0.4, 0.9))
    colour = m.mix(colour, (0.035, 0.030, 0.025), m.mul(grime, 0.85))
    smoothness = m.lerp(m.lerp(0.55, 0.74, lines), 0.2, grime)
    return m.finish(colour, smoothness, m.sub(1.0, m.mul(grime, 0.7)), m.mul(lines, 0.00003))


def laminate(name, seed=0.0, wear=1.0, plies=1.0, dark=(0.042, 0.015, 0.008), light=(0.190, 0.070, 0.027)):
    """Birch plywood furniture under red-brown shellac: thin plies showing as fine lines that swim where the shape
    curves, long grain, lacquer gone dull and pale where hands and shoulders have rubbed, dents.
    plies 0 is solid timber (walnut, with `dark` and `light` to suit)."""
    m = Tree(name)
    grain = m.noise(70.0, 6.0, 0.65, stretch=(7.0, 0.10, 7.0), shift=(seed, 0, 0))
    cloud = m.noise(11.0, 3.0, 0.5, stretch=(2.0, 0.5, 2.0), shift=(0, seed, 4.0))
    ply = m.bands('X', 0.0030, distortion=0.55, detail=3.0, stretch=(1.0, 0.10, 0.22))
    colour = m.mix(dark, light, m.lerp(grain, cloud, 0.35))
    colour = m.mix(colour, (0.035, 0.013, 0.007), m.mul(m.step(ply, 0.70, 0.97), 0.22 * plies))
    # Long dark streaks of denser grain, and slow light-and-dark from board to board.
    streak = m.step(m.noise(34.0, 4.0, 0.6, stretch=(9.0, 0.07, 9.0), shift=(seed, 5.0, 0)), 0.52, 0.78)
    colour = m.mix(colour, (0.030, 0.011, 0.006), m.mul(streak, 0.55))
    pores = m.step(m.noise(210.0, 2.0, 0.5, stretch=(6.0, 0.25, 6.0), shift=(0, seed, 0)), 0.62, 0.74)
    colour = m.mix(colour, (0.040, 0.016, 0.008), m.mul(pores, 0.5))
    # Where the shellac is gone: edges, and a few soft patches (hand, cheek).
    patch = m.step(m.noise(9.0, 3.0, 0.5, shift=(seed, 2.0, 0)), 0.60, 0.80)
    bare = m.sat(m.mul(m.add(m.mul(m.edges(), 1.3), m.mul(patch, 0.55)), wear))
    bare = m.mul(bare, m.add(0.45, m.mul(grain, 0.9)))
    colour = m.mix(colour, (0.27, 0.165, 0.085), m.sat(bare))
    near = m.hollows(0.012)
    grime = m.sub(1.0, m.step(near, 0.35, 0.9))
    colour = m.mix(colour, (0.030, 0.020, 0.013), m.mul(grime, 0.8))
    dents = m.step(m.noise(120.0, 1.0, 0.5, shift=(0, 0, seed)), 0.70, 0.78)
    smoothness = m.lerp(m.lerp(0.40, 0.58, grain), 0.16, m.sat(bare))
    smoothness = m.lerp(smoothness, 0.08, m.sat(m.add(grime, m.mul(dents, 0.6))))
    height = m.add(m.add(m.mul(grain, 0.00006), m.mul(pores, -0.00005)), m.mul(dents, -0.00028))
    return m.finish(colour, smoothness, 0.0, height)


def bakelite(name, seed=0.0, checker=0.0):
    """Old moulded resin (a pistol grip): dark red-brown swirled with near-black, glossy where the palm polishes
    it. checker > 0 presses a diamond grip pattern of that depth (metres) into the faces that look sideways."""
    m = Tree(name)
    swirl = m.noise(34.0, 5.0, 0.6, stretch=(1.0, 1.0, 2.2), shift=(seed, 0, 0), distortion=2.4)
    colour = m.mix((0.010, 0.006, 0.005), (0.055, 0.018, 0.010), m.step(swirl, 0.28, 0.72))
    rubbed = m.sat(m.mul(m.edges(), 0.9))
    colour = m.mix(colour, (0.10, 0.042, 0.024), m.mul(rubbed, 0.5))
    near = m.hollows(0.010)
    grime = m.sub(1.0, m.step(near, 0.35, 0.9))
    height = m.mul(m.noise(300.0, 2.0, 0.5), 0.00002)
    smoothness = m.lerp(0.52, 0.64, swirl)
    if checker > 0:
        a = m.bands('DIAGONAL', 0.0024, stretch=(0.0, 1.0, 1.0))
        b = m.bands('DIAGONAL', 0.0024, stretch=(0.0, 1.0, -1.0))
        side = m.step(m.facing(0), 0.72, 0.9)
        cut = m.mul(m.math('MINIMUM', a, b), side)
        height = m.add(height, m.mul(cut, checker))
        dirt_in_cuts = m.mul(m.sub(1.0, m.math('MINIMUM', a, b)), side)
        colour = m.mix(colour, (0.035, 0.026, 0.018), m.mul(dirt_in_cuts, 0.55))
        smoothness = m.lerp(smoothness, 0.25, m.mul(dirt_in_cuts, 0.8))
    colour = m.mix(colour, (0.030, 0.022, 0.015), m.mul(grime, 0.8))
    smoothness = m.lerp(smoothness, 0.08, grime)
    return m.finish(colour, smoothness, 0.0, height)


def polymer(name, colour, smoothness=0.26, seed=0.0, checker=0.0):
    """Moulded plastic or rubber: an even colour with a fine pebbled skin, scuffed paler on edges and in a few
    scratches, dull. checker > 0 presses a diamond grip pattern of that depth into faces that look sideways."""
    m = Tree(name)
    big = m.noise(18.0, 4.0, 0.6, shift=(seed, 0, 0))
    base = m.mix(tuple(c * 0.82 for c in colour), tuple(c * 1.18 for c in colour), big)
    pale = tuple(min(1.0, c * 1.9 + 0.03) for c in colour)
    rubbed = m.mul(m.mul(m.edges(), m.step(m.noise(60.0, 4.0, 0.7, shift=(seed, seed, 0)), 0.40, 0.72)), 0.7)
    scratch = m.mul(m.step(m.noise(40.0, 2.0, 0.5, stretch=(14.0, 0.6, 14.0), shift=(0, 0, seed)), 0.72, 0.79), 0.45)
    worn = m.sat(m.add(rubbed, scratch))
    base = m.mix(base, pale, worn)
    grime = m.sub(1.0, m.step(m.hollows(0.012), 0.35, 0.9))
    height = m.mul(m.noise(520.0, 2.0, 0.6), 0.00005)
    smooth_ = m.lerp(m.lerp(smoothness - 0.05, smoothness + 0.05, big), smoothness + 0.12, worn)
    if checker > 0:
        a = m.bands('DIAGONAL', 0.0024, stretch=(0.0, 1.0, 1.0))
        b = m.bands('DIAGONAL', 0.0024, stretch=(0.0, 1.0, -1.0))
        side = m.step(m.facing(0), 0.72, 0.9)
        height = m.add(height, m.mul(m.mul(m.math('MINIMUM', a, b), side), checker))
        cuts = m.mul(m.sub(1.0, m.math('MINIMUM', a, b)), side)
        base = m.mix(base, DUST, m.mul(cuts, 0.45))
    base = m.mix(base, DUST, m.mul(grime, 0.8))
    return m.finish(base, m.lerp(smooth_, 0.07, grime), 0.0, height)


def glass(name):
    """A dark lens seen from outside: nearly black, mirror smooth."""
    m = Tree(name)
    return m.finish((0.010, 0.018, 0.026), 0.92, 0.0, None, occlusion=1.0)


def hollow(name):
    """The inside of something: a bore, a port, the bottom of a groove. Near black, sooty, never shiny."""
    m = Tree(name)
    return m.finish(m.mix((0.006, 0.006, 0.006), (0.020, 0.018, 0.016), m.noise(200.0)), 0.06, 0.0, None, occlusion=0.25)


# ------------------------------------------------------------------ laying out and baking

def triangulate(obj):
    """Cuts a shape into the triangles the game will draw, so the bake and the game agree on them (see the traps
    in oa.save_texture_set)."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bm.to_mesh(obj.data)
    bm.free()
    for p in obj.data.polygons:
        p.use_smooth = True


def unwrap(objects, angle=58.0, margin=0.0035):
    """Gives every low shape its place on one shared square picture (the UV map oa.TEX), area for area."""
    for o in objects:
        triangulate(o)
        layer = o.data.uv_layers.get(oa.TEX) or o.data.uv_layers.new(name=oa.TEX)
        o.data.uv_layers.active = layer
        layer.active_render = True
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(angle), island_margin=margin, area_weight=0.0, correct_aspect=True,
                             scale_to_bounds=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.select_all(action='DESELECT')


def _merged(objects, name):
    """One throwaway object holding copies of all the low shapes (the bake needs a single target), with the same
    hard edges the exporter will give each."""
    bm = bmesh.new()
    for o in objects:
        copy = o.data.copy()
        oa.auto_smooth(copy, o.get("oa_smooth", 40.0))
        bm.from_mesh(copy)
        bpy.data.meshes.remove(copy)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    mesh.uv_layers.active = mesh.uv_layers[oa.TEX]
    mesh.uv_layers[oa.TEX].active_render = True
    return obj


def bake_set(name, low, high, size=2048, cage=0.0016, samples=32, folder=None):
    """Bakes the high shapes onto the low ones' shared picture and writes <name>_Color/_Normal/_Mask.png.

    low: the game's shapes, already through unwrap(). high: the detailed shapes with materials from this file.
    cage: how far outside the low model the bake looks from, in metres; it must clear everything the high model
    adds (rivets, ribs) but stay under the gaps between separate parts. Returns the three Blender images.
    """
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.use_denoising = False
    target = _merged(low, "oa_bake_low")
    outside = _merged(low, "oa_bake_cage")
    for v in outside.data.vertices:
        v.co = v.co + v.normal * cage
    outside.hide_render = True
    for o in low:
        o.hide_render = True
    mat = bpy.data.materials.new("oa_bake_target")
    mat.use_nodes = True
    node = mat.node_tree.nodes.new("ShaderNodeTexImage")
    mat.node_tree.nodes.active = node
    target.data.materials.append(mat)
    images = {}
    for what, kind, colour in (("Normal", 'NORMAL', False), ("Color", 'EMIT', True), ("Mask", 'EMIT', False)):
        image = bpy.data.images.new("%s_%s" % (name, what), size, size, alpha=False, is_data=not colour)
        node.image = image
        show({"Normal": "surface", "Color": "colour", "Mask": "mask"}[what])
        scene.cycles.samples = 4 if what == "Normal" else samples
        bpy.ops.object.select_all(action='DESELECT')
        for o in high:
            o.select_set(True)
        target.select_set(True)
        bpy.context.view_layer.objects.active = target
        bpy.ops.object.bake(type=kind, use_selected_to_active=True, use_cage=True, cage_object=outside.name,
                            max_ray_distance=cage * 4, margin=10, margin_type='EXTEND', normal_space='TANGENT', use_clear=True)
        images[what] = image
    show("surface")
    for o in (target, outside):
        mesh = o.data
        bpy.data.objects.remove(o)
        bpy.data.meshes.remove(mesh)
    for o in low:
        o.hide_render = False
    oa.save_texture_set(name, images["Color"], images["Normal"], images["Mask"], folder)
    return images["Color"], images["Normal"], images["Mask"]


def textured_material(name, folder=None):
    """A Blender material that shows a baked texture set roughly as the game will (colour darkened in hollows by
    0.6 of the mask's red, smoothness from green, bare metal from blue, bumps from the normal picture). For
    pictures of the low model; it is not the game's shader."""
    folder = folder or oa.TEXTURES
    mat = bpy.data.materials.new("oa_textured_" + name)
    mat.use_nodes = True
    t = mat.node_tree
    bsdf = t.nodes["Principled BSDF"]
    uv = t.nodes.new("ShaderNodeUVMap")
    uv.uv_map = oa.TEX

    def picture(what, data):
        n = t.nodes.new("ShaderNodeTexImage")
        n.image = bpy.data.images.load(os.path.join(folder, "%s_%s.png" % (name, what)), check_existing=False)
        n.image.colorspace_settings.name = "Non-Color" if data else "sRGB"
        t.links.new(uv.outputs[0], n.inputs[0])
        return n

    colour, normal, mask = picture("Color", False), picture("Normal", True), picture("Mask", True)
    split = t.nodes.new("ShaderNodeSeparateColor")
    t.links.new(mask.outputs["Color"], split.inputs[0])
    shade = t.nodes.new("ShaderNodeMapRange")
    t.links.new(split.outputs[0], shade.inputs["Value"])
    shade.inputs["To Min"].default_value, shade.inputs["To Max"].default_value = 0.4, 1.0
    dark = t.nodes.new("ShaderNodeMix")
    dark.data_type, dark.blend_type = 'RGBA', 'MULTIPLY'
    dark.inputs[0].default_value = 1.0
    t.links.new(colour.outputs["Color"], dark.inputs[6])
    t.links.new(shade.outputs[0], dark.inputs[7])
    t.links.new(dark.outputs[2], bsdf.inputs["Base Color"])
    rough = t.nodes.new("ShaderNodeMath")
    rough.operation = 'SUBTRACT'
    rough.inputs[0].default_value = 1.0
    t.links.new(split.outputs[1], rough.inputs[1])
    t.links.new(rough.outputs[0], bsdf.inputs["Roughness"])
    t.links.new(split.outputs[2], bsdf.inputs["Metallic"])
    bump = t.nodes.new("ShaderNodeNormalMap")
    bump.uv_map = oa.TEX
    t.links.new(normal.outputs["Color"], bump.inputs["Color"])
    t.links.new(bump.outputs[0], bsdf.inputs["Normal"])
    return mat


def render(path, objects, material, eye, target, lens=50, size=(1100, 700), sun=(50, 20, 35), world=(0.36, 0.5, 0.72),
           samples=128, sun_energy=3.6, sky=0.7, floor=None):
    """A picture of shapes under the same soft sun and sky as oa.render_preview, wearing `material` (None leaves
    each shape's own). floor: a height to lay a pale floor at, or None."""
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = samples
    scene.cycles.use_denoising = False
    scene.render.resolution_x, scene.render.resolution_y = size
    scene.render.resolution_percentage = 100
    try:
        scene.view_settings.view_transform = 'Filmic'
        scene.view_settings.look = 'Medium High Contrast'
    except Exception:
        pass
    for o in bpy.data.objects:
        if o.type == 'MESH':
            o.hide_render = o not in objects
    extra = []
    if material is not None:
        for o in objects:
            if o.get("oa_glow"):        # shines by itself: keeps the material it was given
                continue
            o.data.materials.clear()
            o.data.materials.append(material)
            oa.auto_smooth(o.data, o.get("oa_smooth", 40.0))
    if floor is not None:
        ground = oa.box("floor", (0, 0, floor - 0.005), (40, 40, 0.01), (0.80, 0.74, 0.66), bevel=0)
        plain = bpy.data.materials.new("oa_floor")
        plain.use_nodes = True
        plain.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.61, 0.51, 0.39, 1)
        plain.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.7
        ground.data.materials.append(plain)
        extra.append(ground)
    if scene.world is None:
        scene.world = bpy.data.worlds.new("w")
    scene.world.use_nodes = True
    bg = scene.world.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (*world, 1)
    bg.inputs[1].default_value = sky
    light = bpy.data.lights.new("sun", 'SUN')
    light.energy, light.color, light.angle = sun_energy, (1.0, 0.93, 0.8), math.radians(6)
    lo = bpy.data.objects.new("sun", light)
    lo.rotation_euler = [math.radians(a) for a in sun]
    scene.collection.objects.link(lo)
    cam = bpy.data.cameras.new("cam")
    cam.lens = lens
    cam.clip_start = 0.01
    co = bpy.data.objects.new("cam", cam)
    co.location = eye
    co.rotation_euler = (Vector(target) - Vector(eye)).to_track_quat('-Z', 'Y').to_euler()
    scene.collection.objects.link(co)
    scene.camera = co
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    for o in (lo, co):
        bpy.data.objects.remove(o)
    for o in extra:
        mesh = o.data
        bpy.data.objects.remove(o)
        bpy.data.meshes.remove(mesh)
