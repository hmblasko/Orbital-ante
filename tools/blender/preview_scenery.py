"""Offline pictures of the scenery models (Blender's renderer, not the game's shader).

Called by build_scenery.py with --preview. One picture per kind showing its four variants at their usual
height beside a 1.8 m figure, and a wide picture of a hillside planted with everything.
"""
import bpy, bmesh, math, os, random
from mathutils import Vector, Matrix, noise
import oa_blender as oa
import oa_nature as nat

GRASS = (0.36, 0.56, 0.24)


def _hide_all():
    for o in bpy.data.objects:
        if o.type == 'MESH':
            o.hide_render = True


def _ground(size, height, colour=GRASS, cells=90, name="ground", seed=3.0, tint=None):
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=cells, y_segments=cells, size=size)
    for v in bm.verts:
        v.co.z = height(v.co.x, v.co.y)
    o = oa._new_object(name, bm, colour, 80.0, recalc=False)
    cols = []
    for v in o.data.vertices:
        n = noise.noise(v.co * 0.08 + Vector((seed, 0, 0)))
        m = noise.noise(v.co * 0.5)
        c = nat.mix(colour, (0.56, 0.66, 0.26), nat.smooth(0.5 + n))
        c = nat.mix(c, (0.30, 0.46, 0.22), nat.smooth(0.3 + m) * 0.3)
        if tint:
            c = tint(v.co, c)
        cols.append(c)
    nat._store(o, cols)
    o["oa_unchecked"] = True
    return o


def _figure(x, y, z=0.0):
    """A 1.8 m person-sized marker, so the sizes can be judged."""
    body = oa.cylinder("figure", (x, y, z), (x, y, z + 1.5), 0.2, (0.92, 0.9, 0.86), radius_end=0.16, segments=10, bevel=0.05)
    head = oa.sphere("figure_head", (x, y, z + 1.66), 0.15, (0.95, 0.6, 0.2), segments=10)
    for o in (body, head):
        nat.flat_paint(o, o["oa_colour"][:3])
    return [body, head]


def _instance(tree, where, height, yaw=0.0, name="inst"):
    made = []
    for o in tree.objects:
        c = bpy.data.objects.new(name, o.data)
        c["oa_colour"] = o["oa_colour"]
        c["oa_smooth"] = o.get("oa_smooth", 40.0)
        c.location = where
        c.scale = (height, height, height)
        c.rotation_euler = (0, 0, yaw)
        bpy.context.scene.collection.objects.link(c)
        made.append(c)
    return made


def render(folder, built, typical, wide_only=False):
    os.makedirs(folder, exist_ok=True)
    for kind, trees in ({} if wide_only else built).items():
        _hide_all()
        h = typical.get(kind, 5)
        gap = h * 1.3 + 0.8
        temp = [_ground(h * 6 + 10, lambda x, y: 0.0, cells=40)]
        temp += _figure(-gap * 2.05, -h * 0.25)
        shown = []
        for i, tree in enumerate(trees):
            shown += _instance(tree, ((i - 1.5) * gap, 0, 0), h, yaw=0.5 * i)
        eye = (-gap * 0.2, -h * 6.4 - 5, h * 0.7 + 0.8)
        oa.render_preview(os.path.join(folder, "scenery_%s.png" % kind), temp + shown, eye, (-gap * 0.2, 0, h * 0.47), lens=40,
                          size=(1400, 760), sun=(52, 8, 38), samples=64)
        for o in temp + shown:
            bpy.data.objects.remove(o)
    if len(built) >= 8:
        hillside(folder, built, typical)


def hillside(folder, built, typical, name="scenery_hillside.png", seed=11):
    """Everything together on rolling ground with a snowy ridge behind: closer to what a landing looks like."""
    _hide_all()
    rng = random.Random(seed)

    def height(x, y):
        ridge = max(0.0, (y - 55) / 60.0)
        return (5.0 * noise.noise(Vector((x * 0.018, y * 0.018, 0.3))) + 1.6 * noise.noise(Vector((x * 0.06, y * 0.06, 2.0)))
                + 60.0 * ridge ** 1.6 * (0.75 + 0.5 * noise.noise(Vector((x * 0.03, y * 0.02, 5.0)))))

    def tint(co, c):
        rock = nat.smooth((co.z - 14) / 14.0)
        c = nat.mix(c, (0.46, 0.44, 0.42), rock)
        return nat.mix(c, (0.95, 0.97, 1.0), nat.smooth((co.z - 34 + 6 * noise.noise(co * 0.1)) / 8.0))

    temp = [_ground(150, height, cells=150, tint=tint)]
    shown = []

    def plant(kind, x, y, scale=1.0):
        if kind not in built:
            return
        tree = rng.choice(built[kind])
        h = typical.get(kind, 5) * scale * rng.uniform(0.8, 1.2)
        shown.extend(_instance(tree, (x, y, height(x, y) - 0.02 * h), h, yaw=rng.uniform(0, 6.28)))

    def grove(kinds, cx, cy, radius, count, scale=1.0):
        for _ in range(count):
            a, d = rng.uniform(0, 6.28), radius * math.sqrt(rng.random())
            plant(rng.choice(kinds), cx + math.cos(a) * d, cy + math.sin(a) * d, scale)

    grove(["Broadleaf", "Broadleaf", "Maple", "Birch"], -22, 26, 16, 22)
    grove(["Conifer", "Conifer", "Pine"], 24, 40, 18, 30)
    grove(["SnowConifer"], 10, 74, 16, 16, 0.8)
    grove(["GiantTree"], 46, 20, 10, 5)
    grove(["Blossom"], -6, 8, 6, 4)
    grove(["Birch"], 8, 14, 7, 7)
    grove(["Bush", "Fern", "Meadow"], 0, 6, 16, 40)
    grove(["Meadow"], 2, -2, 9, 26)
    grove(["Outcrop"], -14, 10, 8, 3)
    grove(["Crag"], -34, 60, 12, 3)
    grove(["SnowCrag"], 20, 92, 26, 5)
    plant("Snag", 12, 4)
    plant("Log", 5, 1)
    temp += _figure(3.0, -1.0, height(3.0, -1.0))
    eye = (4, -24, height(4, -24) + 3.2)
    oa.render_preview(os.path.join(folder, name), temp + shown, eye, (4, 30, 10), lens=28, size=(1600, 900),
                      sun=(58, 0, 55), world=(0.42, 0.62, 0.9), samples=72)
    for o in temp + shown:
        bpy.data.objects.remove(o)
