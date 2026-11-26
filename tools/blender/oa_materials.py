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

    def cells(self, scale, shift=(0, 0, 0), stretch=(1, 1, 1), soft=0.0):
        """Scattered cells `1 / scale` metres apart: (distance from the nearest cell's middle, in cells: 0 at the
        middle to about 0.7; a random number 0 to 1 that is the same all over one cell). soft > 0 rounds the
        valleys between cells."""
        n = self.t.nodes.new("ShaderNodeTexVoronoi")
        n.feature = 'SMOOTH_F1' if soft > 0 else 'F1'
        self.t.links.new(self.stretched(stretch, shift), n.inputs["Vector"])
        n.inputs["Scale"].default_value = scale
        if soft > 0:
            n.inputs["Smoothness"].default_value = soft
        split = self.t.nodes.new("ShaderNodeSeparateColor")
        self.t.links.new(n.outputs["Color"], split.inputs[0])
        return n.outputs["Distance"], split.outputs[0]

    def up(self):
        """How far the surface faces up: 1 straight up, -1 straight down (in the shape's own space)."""
        n = self.t.nodes.new("ShaderNodeSeparateXYZ")
        self.t.links.new(self.normal, n.inputs[0])
        return n.outputs["Z"]

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
          dirt=0.7):
    """Gun steel under a dark coat (bluing, phosphate or baked paint) that has rubbed through to bright metal on
    edges and high spots. The coat is not bare metal (it does not mirror); the rubbed places are."""
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
    metal = m.sat(m.sub(worn, grime))
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


def laminate(name, seed=0.0, wear=1.0):
    """Birch plywood furniture under red-brown shellac: thin plies showing as fine lines that swim where the shape
    curves, long grain, lacquer gone dull and pale where hands and shoulders have rubbed, dents."""
    m = Tree(name)
    grain = m.noise(70.0, 6.0, 0.65, stretch=(7.0, 0.10, 7.0), shift=(seed, 0, 0))
    cloud = m.noise(11.0, 3.0, 0.5, stretch=(2.0, 0.5, 2.0), shift=(0, seed, 4.0))
    plies = m.bands('X', 0.0030, distortion=0.55, detail=3.0, stretch=(1.0, 0.10, 0.22))
    colour = m.mix((0.042, 0.015, 0.008), (0.190, 0.070, 0.027), m.lerp(grain, cloud, 0.35))
    colour = m.mix(colour, (0.035, 0.013, 0.007), m.mul(m.step(plies, 0.70, 0.97), 0.22))
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


def hollow(name):
    """The inside of something: a bore, a port, the bottom of a groove. Near black, sooty, never shiny."""
    m = Tree(name)
    return m.finish(m.mix((0.006, 0.006, 0.006), (0.020, 0.018, 0.016), m.noise(200.0)), 0.06, 0.0, None, occlusion=0.25)


# ------------------------------------------------------------------ living things (alien creatures)
#
# Sizes here are for animals metres across seen from a few metres, so bumps are millimetres, not the hundredths of
# a millimetre a gun's surface has. `size` scales every pattern (1 = a creature about a metre across the body).

SILT = (0.085, 0.080, 0.050)     # what settles on a sea animal's back: grey-green, dull


def chitin(name, base=(0.50, 0.115, 0.035), pale=(0.66, 0.52, 0.38), dark=(0.13, 0.028, 0.014), under=None, seed=0.0,
           size=1.0, bumps=1.0, mottle=0.8, growth=0.5, gloss=0.42, tip=None):
    """The shell of a crab, an isopod, a centipede: a hard coloured crust over paler layers. Blotches of the pale
    colour with ragged edges, pale freckles, rounded tubercles that are rubbed pale on top with dirt round their
    feet, a fine grainy skin, pits; pale `under` colour on surfaces that face down; silt and weed on surfaces that
    face up (`growth`); dark old dirt where shapes crowd together; edges and ridges rubbed through to the pale
    layer. tip: (axis index, from, to, colour) darkens toward a claw's point along that axis, in metres."""
    m = Tree(name)
    big = m.noise(2.4 / size, 4.0, 0.6, shift=(seed, 0, 0))
    mid = m.noise(11.0 / size, 5.0, 0.65, shift=(0, seed, 0))
    fine = m.noise(190.0, 3.0, 0.7, shift=(0, 0, seed))
    colour = m.mix(tuple(c * 0.62 for c in base), tuple(min(1.0, c * 1.32) for c in base), m.lerp(big, mid, 0.5))
    # The dark mineral staining old shells have, in slow patches.
    colour = m.mix(colour, dark, m.mul(m.step(m.noise(5.0 / size, 5.0, 0.6, shift=(seed, 4.0, 0)), 0.56, 0.78), 0.55))
    # Pale blotches: ragged islands, more of them where the big pattern says so.
    blotch = m.step(m.add(m.mul(big, 0.55), m.mul(mid, 0.45)), 0.535, 0.575)
    colour = m.mix(colour, pale, m.mul(blotch, mottle))
    freckle_at, freckle_rnd = m.cells(46.0 / size, shift=(seed, seed, 0))
    freckle = m.mul(m.step(m.sub(0.30, freckle_at), 0.0, 0.10), m.step(freckle_rnd, 0.62, 0.66))
    colour = m.mix(colour, pale, m.mul(freckle, 0.85 * mottle))
    # Tubercles: only some cells grow one, and they are of different sizes.
    at, rnd = m.cells(30.0 / size, shift=(0, seed, seed), soft=0.25)
    grown = m.step(rnd, 0.38, 0.52)
    knob = m.mul(m.step(m.sub(0.46, at), 0.0, 0.34), grown)
    foot = m.mul(m.mul(m.step(m.sub(0.52, at), 0.0, 0.14), m.sub(1.0, m.step(m.sub(0.40, at), 0.0, 0.1))), grown)
    small_at, small_rnd = m.cells(120.0 / size, shift=(seed, 0, seed), soft=0.3)
    grain = m.mul(m.step(m.sub(0.5, small_at), 0.0, 0.4), m.step(small_rnd, 0.2, 0.5))
    pits = m.step(m.noise(330.0 / size, 2.0, 0.5, shift=(seed, seed, seed)), 0.66, 0.74)
    scratch = m.step(m.noise(30.0 / size, 2.0, 0.5, stretch=(1.0, 14.0, 5.0), shift=(0, 0, seed)), 0.72, 0.77)
    colour = m.mix(colour, pale, m.mul(knob, 0.55 * bumps))
    colour = m.mix(colour, dark, m.mul(foot, 0.6 * bumps))
    colour = m.mix(colour, dark, m.mul(pits, 0.35))
    colour = m.mix(colour, pale, m.mul(scratch, 0.30))
    facing_up = m.up()
    if under is not None:
        below = m.step(m.mul(facing_up, -1.0), -0.15, 0.75)
        colour = m.mix(colour, m.mix(tuple(c * 0.8 for c in under), under, mid), m.mul(below, 0.92))
    # Rubbed ridges and edges: the pale layer under the colour.
    rubbed = m.sat(m.mul(m.edges(lo=0.02, hi=0.25, radius=0.006 * size), m.add(0.3, mid)))
    colour = m.mix(colour, pale, m.mul(rubbed, 0.6))
    if tip is not None:
        which, start, end, tip_colour = tip
        toward = m.step(m.axis(which), start, end)
        colour = m.mix(colour, tip_colour, toward)
    # What grows and settles on top, and what packs into hollows.
    settled = m.mul(m.mul(m.step(facing_up, 0.25, 0.9), m.step(m.noise(7.0 / size, 5.0, 0.7, shift=(9.0, seed, 0)), 0.42, 0.70)), growth)
    weed = m.mul(settled, m.step(m.noise(60.0 / size, 3.0, 0.6, shift=(0, 9.0, seed)), 0.35, 0.65))
    colour = m.mix(colour, SILT, m.mul(settled, 0.75))
    colour = m.mix(colour, (0.035, 0.060, 0.022), m.mul(weed, 0.45))
    near = m.hollows(0.035 * size)
    grime = m.sub(1.0, m.step(near, 0.35, 0.92))
    colour = m.mix(colour, tuple(c * 0.45 for c in dark), m.mul(grime, 0.85))
    smoothness = m.lerp(m.lerp(gloss - 0.12, gloss + 0.08, big), gloss + 0.14, m.mul(knob, bumps))
    smoothness = m.lerp(smoothness, 0.10, m.sat(m.add(settled, grime)))
    smoothness = m.lerp(smoothness, 0.14, m.mul(pits, 0.7))
    height = m.add(m.mul(knob, 0.0042 * size * bumps), m.mul(grain, 0.0009 * size))
    height = m.add(height, m.add(m.mul(pits, -0.0006 * size), m.mul(scratch, -0.0004 * size)))
    height = m.add(height, m.add(m.mul(fine, 0.00025), m.mul(weed, 0.0012 * size)))
    occlusion = m.mul(near, m.sub(1.0, m.mul(foot, 0.5 * bumps)))
    return m.finish(colour, smoothness, 0.0, height, occlusion=occlusion)


def bristly(name, base=(0.42, 0.27, 0.13), pale=(0.62, 0.50, 0.34), dark=(0.10, 0.045, 0.02), under=None, seed=0.0, size=1.0,
            hair=1.0, plates=None, stripe=None, tip=None, gloss=0.24):
    """The skin of a hairy desert arthropod (a camel spider, a tarantula's legs): a dull sandy cuticle under a coat of
    fine hairs lying along the limb (+Y), paler where the hair is thick and darker in the sockets of the big
    bristles; faint darker clouds; dust in the hollows. plates: (period, strength) lays darker hard plates across
    the body every `period` metres with pale soft skin between them (an abdomen's segments). stripe: (half width,
    colour) is a darker band down the middle of the back. tip: (axis index, from, to, colour) darkens toward a
    point along that axis, in metres, and takes the hair off it (jaws are bare and polished)."""
    m = Tree(name)
    big = m.noise(3.0 / size, 4.0, 0.6, shift=(seed, 0, 0))
    mid = m.noise(14.0 / size, 5.0, 0.65, shift=(0, seed, 0))
    colour = m.mix(tuple(c * 0.70 for c in base), tuple(min(1.0, c * 1.25) for c in base), m.lerp(big, mid, 0.5))
    colour = m.mix(colour, dark, m.mul(m.step(m.noise(7.0 / size, 4.0, 0.6, shift=(seed, 5.0, 0)), 0.58, 0.80), 0.35))
    facing_up = m.up()
    height = m.mul(mid, 0.0006 * size)
    smoothness = m.lerp(gloss - 0.06, gloss + 0.06, big)
    bare = 0.0
    if plates is not None:
        period, strength = plates
        plate = m.step(m.bands('Y', period, distortion=0.6, detail=2.0), 0.10, 0.26)
        on_top = m.step(facing_up, -0.25, 0.35)
        hard = m.mul(plate, on_top)
        colour = m.mix(m.mix(colour, pale, 0.30), m.mix(colour, dark, 0.22 * strength), hard)
        height = m.add(height, m.mul(hard, 0.004 * size))
        smoothness = m.lerp(smoothness, gloss + 0.10, hard)
    if stripe is not None:
        half, stripe_colour, middle = stripe
        centre = m.sub(1.0, m.step(m.math('ABSOLUTE', m.sub(m.axis(0), middle)), half * 0.5, half))
        colour = m.mix(colour, stripe_colour, m.mul(m.mul(centre, m.step(facing_up, 0.1, 0.7)), 0.7))
    if under is not None:
        below = m.step(m.mul(facing_up, -1.0), -0.10, 0.70)
        colour = m.mix(colour, m.mix(tuple(c * 0.8 for c in under), under, mid), m.mul(below, 0.9))
    if tip is not None:
        which, start, end, tip_colour = tip
        bare = m.step(m.axis(which), start, end)
        colour = m.mix(colour, m.mix(tuple(c * 0.6 for c in tip_colour), tip_colour, mid), bare)
        smoothness = m.lerp(smoothness, 0.62, bare)
    # The coat: fine hairs in long streaks along the limb, each a pale line with a dark gap beside it.
    coat = m.sub(1.0, bare) if tip is not None else 1.0
    strand = m.noise(520.0 / size, 2.0, 0.5, stretch=(1.0, 0.045, 1.0), shift=(seed, 0, seed))
    lock = m.noise(90.0 / size, 3.0, 0.6, stretch=(1.0, 0.12, 1.0), shift=(0, seed, 3.0))
    thick = m.mul(m.mul(m.step(m.noise(9.0 / size, 4.0, 0.6, shift=(4.0, 0, seed)), 0.30, 0.62), hair), coat)
    colour = m.mix(colour, pale, m.mul(m.mul(m.step(strand, 0.52, 0.68), thick), 0.55))
    colour = m.mix(colour, tuple(c * 0.5 for c in dark), m.mul(m.mul(m.step(m.sub(1.0, strand), 0.56, 0.70), thick), 0.40))
    colour = m.mix(colour, pale, m.mul(m.mul(m.step(lock, 0.55, 0.75), thick), 0.25))
    # The sockets of the big bristles: dark dots, each on a small pale bump.
    at, rnd = m.cells(70.0 / size, shift=(seed, 0, seed), soft=0.2)
    grown = m.mul(m.step(rnd, 0.45, 0.55), coat)
    bump = m.mul(m.step(m.sub(0.40, at), 0.0, 0.30), grown)
    dot = m.mul(m.step(m.sub(0.16, at), 0.0, 0.08), grown)
    colour = m.mix(colour, pale, m.mul(bump, 0.30))
    colour = m.mix(colour, tuple(c * 0.6 for c in dark), m.mul(dot, 0.40))
    near = m.hollows(0.035 * size)
    grime = m.sub(1.0, m.step(near, 0.35, 0.92))
    colour = m.mix(colour, tuple(c * 0.5 for c in dark), m.mul(grime, 0.8))
    smoothness = m.lerp(smoothness, 0.10, m.sat(m.add(m.mul(thick, 0.7), grime)))
    height = m.add(height, m.add(m.mul(m.mul(strand, thick), 0.0011 * size), m.mul(bump, 0.0016 * size)))
    height = m.add(height, m.mul(m.mul(lock, thick), 0.0010 * size))
    return m.finish(colour, smoothness, 0.0, height, occlusion=near)


def membrane(name, colour=(0.42, 0.25, 0.15), seed=0.0, along='Y', size=1.0):
    """The soft skin in an arthropod's joints, a gill, an eyestalk: leathery, in fine wrinkles that run round the
    joint (across `along`), darker down in the folds, a little wet."""
    m = Tree(name)
    wrinkle = m.bands(along, 0.011 * size, distortion=1.6, detail=3.0)
    cloud = m.noise(40.0 / size, 4.0, 0.6, shift=(seed, 0, 0))
    shade = m.mix(tuple(c * 0.55 for c in colour), tuple(min(1.0, c * 1.25) for c in colour), m.lerp(wrinkle, cloud, 0.4))
    near = m.hollows(0.03 * size)
    grime = m.sub(1.0, m.step(near, 0.35, 0.9))
    shade = m.mix(shade, tuple(c * 0.25 for c in colour), m.mul(grime, 0.8))
    height = m.add(m.mul(wrinkle, 0.0016 * size), m.mul(cloud, 0.0006 * size))
    return m.finish(shade, m.lerp(0.22, 0.40, wrinkle), 0.0, height, occlusion=near)


def sheen(name, base=(0.060, 0.030, 0.045), under=(0.30, 0.17, 0.15), lights=((0.05, 0.42, 0.30), (0.10, 0.22, 0.62), (0.42, 0.12, 0.50), (0.62, 0.34, 0.08)),
          seed=0.0, ring=0.030, along='Y', gloss=0.74, strength=0.6):
    """The wet skin of a bristle worm: dark, ringed all the way along, and shot with colours like oil on water
    that change with the way the surface faces (green, blue, violet, copper). Paler underneath; dull dirt in the
    grooves where things crowd together."""
    m = Tree(name)
    rings = m.bands(along, ring, distortion=0.5, detail=2.0)
    cloud = m.noise(5.0, 4.0, 0.6, shift=(seed, 0, 0))
    fine = m.noise(60.0, 3.0, 0.6, shift=(0, seed, 0))
    colour = m.mix(tuple(c * 0.6 for c in base), tuple(min(1.0, c * 1.5) for c in base), m.lerp(cloud, fine, 0.4))
    facing_up = m.up()
    # Where on the film's run of colours this spot is: it follows the facing, and wanders.
    film = m.add(m.add(m.mul(facing_up, 0.45), m.mul(cloud, 0.9)), m.mul(m.noise(1.6, 3.0, 0.5, shift=(0, 0, seed)), 0.8))
    for k, light in enumerate(lights):
        middle = 0.30 + 0.28 * k
        band = m.mul(m.step(film, middle - 0.16, middle - 0.02), m.sub(1.0, m.step(film, middle + 0.02, middle + 0.16)))
        colour = m.mix(colour, light, m.mul(band, m.mul(strength, m.lerp(0.55, 1.0, fine))))
    below = m.step(m.mul(facing_up, -1.0), 0.05, 0.8)
    colour = m.mix(colour, m.mix(tuple(c * 0.75 for c in under), under, cloud), m.mul(below, 0.85))
    # The grooves between the rings are darker.
    colour = m.mix(colour, tuple(c * 0.35 for c in base), m.mul(m.sub(1.0, m.step(rings, 0.0, 0.35)), 0.7))
    near = m.hollows(0.03)
    grime = m.sub(1.0, m.step(near, 0.35, 0.92))
    colour = m.mix(colour, DUST, m.mul(grime, 0.8))
    smoothness = m.lerp(m.lerp(gloss - 0.1, gloss + 0.08, cloud), 0.15, grime)
    height = m.add(m.mul(rings, 0.0022), m.mul(fine, 0.0004))
    return m.finish(colour, smoothness, 0.0, height, occlusion=near)


def banded(name, light=(0.62, 0.50, 0.32), dark=(0.10, 0.05, 0.04), period=0.09, along='Y', seed=0.0, gloss=0.45):
    """A feeler in light and dark bands along its length, finely ringed."""
    m = Tree(name)
    stripe = m.step(m.bands(along, period), 0.42, 0.58)
    fine = m.bands(along, period / 7.0)
    cloud = m.noise(30.0, 3.0, 0.6, shift=(seed, 0, 0))
    colour = m.mix(dark, light, stripe)
    colour = m.mix(colour, tuple(c * 0.6 for c in dark), m.mul(m.sub(1.0, m.step(fine, 0.0, 0.4)), 0.4))
    colour = m.mix(colour, tuple(c * 0.8 for c in light), m.mul(cloud, 0.2))
    near = m.hollows(0.02)
    return m.finish(colour, m.lerp(gloss - 0.1, gloss + 0.1, cloud), 0.0, m.mul(fine, 0.0008), occlusion=near)


def earth(name, colour=(0.21, 0.18, 0.15), seed=0.0):
    """Ground thrown up out of a hole: clods and grit, dull, darker and damp toward the bottom of the heap's folds."""
    m = Tree(name)
    clod_at, clod_rnd = m.cells(16.0, shift=(seed, 0, 0), soft=0.4)
    clod = m.mul(m.step(m.sub(0.5, clod_at), 0.0, 0.4), m.step(clod_rnd, 0.3, 0.6))
    grit_at, grit_rnd = m.cells(70.0, shift=(0, seed, 0), soft=0.3)
    grit = m.mul(m.step(m.sub(0.5, grit_at), 0.0, 0.35), m.step(grit_rnd, 0.4, 0.6))
    cloud = m.noise(4.0, 4.0, 0.6, shift=(0, 0, seed))
    shade = m.mix(tuple(c * 0.6 for c in colour), tuple(min(1.0, c * 1.3) for c in colour), m.lerp(cloud, clod_rnd, 0.4))
    near = m.hollows(0.05)
    shade = m.mix(shade, tuple(c * 0.35 for c in colour), m.mul(m.sub(1.0, m.step(near, 0.35, 0.95)), 0.8))
    height = m.add(m.mul(clod, 0.012), m.mul(grit, 0.0025))
    return m.finish(shade, 0.06, 0.0, height, occlusion=near)


def flesh(name, pale=(0.72, 0.58, 0.50), red=(0.42, 0.045, 0.035), seed=0.0, size=1.0, along='Y'):
    """Torn meat at a wound, the inside of a broken shell: pale wet muscle in bundles running along the limb,
    red where it is torn deepest, dark clots, shiny."""
    m = Tree(name)
    fibre = m.bands('X' if along == 'Y' else 'Y', 0.006 * size, distortion=2.2, detail=3.0)
    lump = m.noise(26.0 / size, 4.0, 0.6, shift=(seed, 0, 0), distortion=1.2)
    colour = m.mix(pale, red, m.step(m.lerp(lump, fibre, 0.35), 0.38, 0.72))
    clot = m.step(m.noise(60.0 / size, 3.0, 0.6, shift=(0, seed, 2.0)), 0.62, 0.72)
    colour = m.mix(colour, (0.10, 0.008, 0.008), m.mul(clot, 0.85))
    near = m.hollows(0.02 * size)
    colour = m.mix(colour, (0.07, 0.008, 0.008), m.mul(m.sub(1.0, m.step(near, 0.3, 0.9)), 0.8))
    height = m.add(m.mul(lump, 0.006 * size), m.mul(fibre, 0.0012 * size))
    return m.finish(colour, m.lerp(0.55, 0.78, lump), 0.0, height, occlusion=near)


def shell_inside(name, colour=(0.62, 0.50, 0.43), seed=0.0, size=1.0):
    """The inside of a shell plate: smooth, pale, faintly pink, stained where meat was attached."""
    m = Tree(name)
    cloud = m.noise(9.0 / size, 4.0, 0.6, shift=(seed, 0, 0))
    shade = m.mix(tuple(c * 0.7 for c in colour), colour, cloud)
    stain = m.step(m.noise(16.0 / size, 4.0, 0.6, shift=(0, seed, 0)), 0.55, 0.72)
    shade = m.mix(shade, (0.36, 0.07, 0.05), m.mul(stain, 0.6))
    near = m.hollows(0.03 * size)
    return m.finish(shade, m.lerp(0.45, 0.62, cloud), 0.0, m.mul(cloud, 0.0008 * size), occlusion=near)


def chalk(name, colour=(0.60, 0.57, 0.50), seed=0.0):
    """Barnacles, worm tubes, old coral: rough limy white, ribbed, grey-green in the pits."""
    m = Tree(name)
    rough = m.noise(140.0, 4.0, 0.7, shift=(seed, 0, 0))
    near = m.hollows(0.012)
    shade = m.mix(tuple(c * 0.6 for c in colour), colour, rough)
    shade = m.mix(shade, SILT, m.mul(m.sub(1.0, m.step(near, 0.4, 0.95)), 0.8))
    return m.finish(shade, 0.08, 0.0, m.mul(rough, 0.0012), occlusion=near)


def eye(name, colour=(0.012, 0.010, 0.009), glint=(0.10, 0.045, 0.02)):
    """A hard dark eye: nearly black, deep brown where the light gets in, as glossy as wet glass."""
    m = Tree(name)
    facet_at, _ = m.cells(900.0)
    shade = m.mix(colour, glint, m.step(m.noise(30.0), 0.45, 0.75))
    return m.finish(shade, 0.86, 0.0, m.mul(facet_at, -0.00015))


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
