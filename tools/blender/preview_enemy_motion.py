"""Renders the frames written by tools/blender/motion_dump as pictures, for a GIF of how an enemy moves.

    blender -b -P tools/blender/preview_enemy_motion.py -- <motion.json> <folder> [--eye x,y,z] [--lens 40] [--follow]
                                                         [--size 420x480] [--samples 10]

The body is the common enemy model built by build_enemy.py (nothing is written to the game). The gun is a plain
box. Parts that have been shot off are simply not drawn (no wound cap, no flying limb). These are offline
previews of the animation rules, not game footage: no game shader, no physics, flat ground.
"""
import bpy, sys, os, json, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import oa_blender as oa
import build_enemy as be
from mathutils import Vector, Matrix


def arg(args, name, default):
    return args[args.index(name) + 1] if name in args else default


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
    parts = be.make()
    for o in be.stump():
        o.hide_render = True
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

    follow = "--follow" in args
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
        gun.location = (0, 0, -500)
        shown.append(gun)
        if fr["guns"]:
            g = fr["guns"][0]
            gun.location = (g["at"][0], g["at"][2], g["at"][1])
            gun.rotation_euler = (0, 0, math.radians(-g["heading"]))
        oa.render_preview(os.path.join(folder, "f%03d.png" % i), shown,
                          (eye[0], eye[2], eye[1]), (centre[0], centre[2], centre[1]),
                          lens=lens, size=(w, h), samples=samples)
    with open(os.path.join(folder, "captions.json"), "w") as f:
        json.dump([fr.get("caption", "") for fr in frames], f)


if __name__ == "__main__":
    main()
