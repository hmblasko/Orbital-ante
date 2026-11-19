"""Renders the frames written by tools/blender/motion_dump as pictures, for a GIF of how an enemy moves.

    blender -b -P tools/blender/preview_enemy_motion.py -- <motion.json> <folder> [--eye x,y,z] [--lens 40] [--follow]
                                                         [--size 420x480] [--samples 10] [--only 3,10,24]
                                                         [--model EnemyDrifter]

The body is the common enemy model built by build_enemy.py (nothing is written to the game). The gun is a plain
box. A part that is shot off flies away as one piece, together with everything below it, on a simple thrown path
(gravity, a spin, a few bounces) standing in for the game's physics, and the model's wound stump shows where it came
off. These are offline previews of the animation rules, not game footage: no game shader, no physics engine, no
blood, flat ground.
"""
import bpy, sys, os, json, math, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import oa_blender as oa
import build_enemy as be
from mathutils import Vector, Matrix


def arg(args, name, default):
    return args[args.index(name) + 1] if name in args else default


# Which part a cut part hangs from, and which way its stump points (in the model's own space, z up), with the
# wound's size the game gives it (MannequinFactory).
CUT_FROM = {
    "LeftUpperArm": ("Torso", (-1, 0, 0), 0.07), "RightUpperArm": ("Torso", (1, 0, 0), 0.07),
    "LeftForearm": ("LeftUpperArm", (-1, 0, 0), 0.06), "RightForearm": ("RightUpperArm", (1, 0, 0), 0.06),
    "LeftThigh": ("Torso", (0, 0, -1), 0.09), "RightThigh": ("Torso", (0, 0, -1), 0.09),
    "LeftShin": ("LeftThigh", (0, 0, -1), 0.072), "RightShin": ("RightThigh", (0, 0, -1), 0.072),
}
CHAIN = {"UpperArm": ("UpperArm", "Forearm", "Hand"), "Forearm": ("Forearm", "Hand"),
         "Thigh": ("Thigh", "Shin", "Foot"), "Shin": ("Shin", "Foot")}


def as_matrix(values):
    return Matrix((values[0:4], values[4:8], values[8:12], values[12:16]))


def flight(frames, cut, dt, seed=4):
    """Where each piece of a cut-off chain is on every frame after the cut, in the game's space: thrown out from the
    body's side and up, spinning, falling, bouncing and sliding to a stop. A stand-in for the game's physics."""
    side = cut[:4] if cut.startswith("Left") else cut[:5]
    names = [side + n for n in CHAIN[cut[len(side):]]]
    start = next(i for i, fr in enumerate(frames) if cut not in fr["parts"])
    before = frames[start - 1]["parts"]
    rest = {n: as_matrix(before[n]) for n in names}
    torso = as_matrix(before["Torso"])
    outward = torso.col[0].xyz * (-1 if side == "Left" else 1)
    back = -torso.col[2].xyz
    rnd = random.Random(seed)
    pivot = sum((rest[n].col[3].xyz for n in names), Vector()) / len(names)
    v = outward * 1.5 + back * 0.7 + Vector((0, 2.7, 0))
    leg = "Thigh" in cut or "Shin" in cut
    # A leg tumbles end over end (about the body's side axis) and comes to rest lying down, not stood on its stump.
    axis = torso.col[0].xyz if leg else torso.col[2].xyz
    spin_axis = (axis + Vector((rnd.uniform(-.3, .3), rnd.uniform(-.3, .3), rnd.uniform(-.3, .3)))).normalized()
    spin = 15.0
    angle = 0.0
    at = pivot.copy()
    out = {}
    for i in range(start, len(frames)):
        turn = Matrix.Rotation(angle, 4, spin_axis)
        placed = {}
        for n in names:
            m = Matrix.Translation(at) @ turn @ Matrix.Translation(-pivot) @ rest[n]
            placed[n] = [x for row in m for x in row]
        out[i] = placed
        for _ in range(3):   # three small steps a picture
            h = dt / 3
            v.y -= 9.8 * h
            at += v * h
            angle += spin * h
            if at.y < 0.12 and v.y < 0:
                at.y = 0.12
                v.y = -v.y * 0.35
                v.x *= 0.45
                v.z *= 0.45
                spin *= 0.5
                if v.y < 0.4:
                    v.y = 0.0
            if at.y <= 0.121 and v.y == 0.0:
                v.x *= 0.8
                v.z *= 0.8
                spin *= 0.85
                if leg:
                    lying = round((angle - math.pi / 2) / math.pi) * math.pi + math.pi / 2
                    angle += (lying - angle) * 0.25
    return names, out


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    path, folder = args[0], args[1]
    w, h = (int(v) for v in arg(args, "--size", "420x480").split("x"))
    samples = int(arg(args, "--samples", "10"))
    with open(path) as f:
        clip = json.load(f)
    frames = clip["frames"]
    os.makedirs(folder, exist_ok=True)

    oa.reset_scene()
    if arg(args, "--model", "") == "EnemyDrifter":
        import build_enemy_drifter
        parts = build_enemy_drifter.make()
    else:
        parts = be.make()
    stump = be.stump()
    for o in stump:
        o.location = (0, 0, -500)
    floor = be.ground()
    gun = oa.box("gun", (0, 0, 0), (0.07, 0.62, 0.16), (0.16, 0.17, 0.19), bevel=0.01)
    oa.paint(gun, wear=0, dirt=0, mottle=0, gradient=0)

    # Frame the whole clip: look at the middle of where the torso goes, from a fixed place.
    xs = [fr["parts"]["Torso"][3] for fr in frames if "Torso" in fr["parts"]]
    zs = [fr["parts"]["Torso"][11] for fr in frames if "Torso" in fr["parts"]]
    centre = ((min(xs) + max(xs)) / 2, 0.85, (min(zs) + max(zs)) / 2)
    eye = [float(v) for v in arg(args, "--eye", "3.2,1.7,-6.0").split(",")]
    eye = (centre[0] + eye[0], eye[1], centre[2] + eye[2])
    lens = float(arg(args, "--lens", "40"))

    # Parts that are cut off during the clip: a copy of each flies away, and a stump shows on what it came from.
    dt = clip.get("seconds_per_frame", 0.05)
    cuts = [n for n in CUT_FROM if n in frames[0]["parts"] and any(n not in fr["parts"] for fr in frames)
            and CUT_FROM[n][0] in frames[-1]["parts"]]
    gibs, stumps = {}, []
    for cut in cuts:
        names, path = flight(frames, cut, dt)
        copies = {}
        for n in names:
            copies[n] = []
            for o in parts[n]:
                c = o.copy()
                bpy.context.scene.collection.objects.link(c)
                copies[n].append(c)
        gibs[cut] = (copies, path)
        parent, facing, radius = CUT_FROM[cut]
        mine = []
        for o in stump:
            c = o.copy()
            bpy.context.scene.collection.objects.link(c)
            mine.append(c)
        joint = Vector(be.ORIGINS[cut])
        place = Matrix.Translation(joint - Vector(be.ORIGINS[parent])) @ \
            Vector((0, 0, 1)).rotation_difference(Vector(facing)).to_matrix().to_4x4() @ Matrix.Scale(radius, 4)
        stumps.append((cut, parent, mine, place))

    follow = "--follow" in args
    only = {int(v) for v in arg(args, "--only", "").split(",") if v}   # just these pictures, for a quick look
    smooth = None
    for i, fr in enumerate(frames):
        if follow and "Torso" in fr["parts"]:
            # A camera that keeps the body in the middle of the picture, drifting after it smoothly.
            t = fr["parts"]["Torso"]
            now = (t[3], 0.85, t[11])
            smooth = now if smooth is None else tuple(a + (b - a) * 0.3 for a, b in zip(smooth, now))  # catches up within about half a second
            centre = smooth
            off = [float(v) for v in arg(args, "--eye", "3.2,1.7,-6.0").split(",")]
            eye = (centre[0] + off[0], off[1], centre[2] + off[2])
        shown = [floor]
        for name in be.ORDER:
            m = fr["parts"].get(name)
            for o in parts[name]:
                # A part that is gone is parked far below the floor rather than hidden: switching what is drawn
                # from frame to frame crashes this Blender's ray tracer.
                o.matrix_world = (be.game_matrix(m) if m is not None else Matrix.Translation((0, 0, -500))) \
                    @ Matrix.Translation(-Vector(be.ORIGINS[name]))
                shown.append(o)
        for cut, (copies, path) in gibs.items():
            for n, objs in copies.items():
                m = path.get(i, {}).get(n)
                for o in objs:
                    o.matrix_world = (be.game_matrix(m) if m is not None else Matrix.Translation((0, 0, -500))) \
                        @ Matrix.Translation(-Vector(be.ORIGINS[n]))
                    shown.append(o)
        for cut, parent, objs, place in stumps:
            m = fr["parts"].get(parent)
            for o in objs:
                o.matrix_world = be.game_matrix(m) @ place if (m is not None and cut not in fr["parts"]) \
                    else Matrix.Translation((0, 0, -500))
                shown.append(o)
        gun.location = (0, 0, -500)
        shown.append(gun)
        if fr["guns"]:
            g = fr["guns"][0]
            gun.location = (g["at"][0], g["at"][2], g["at"][1])
            # A held gun carries its pitch (muzzle up positive); one on the ground lies flat.
            gun.rotation_euler = (math.radians(g.get("pitch", 0.0)), 0, math.radians(-g["heading"]))
        if only and i not in only:
            continue
        oa.render_preview(os.path.join(folder, "f%03d.png" % i), shown,
                          (eye[0], eye[2], eye[1]), (centre[0], centre[2], centre[1]),
                          lens=lens, size=(w, h), samples=samples)
    with open(os.path.join(folder, "captions.json"), "w") as f:
        json.dump([fr.get("caption", "") for fr in frames], f)


if __name__ == "__main__":
    main()
