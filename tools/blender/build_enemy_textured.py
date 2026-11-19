"""Gives a painted enemy body its texture pictures (Matthew, 18 Nov 2026: enemies "substantially more realistic and
not obviously made in blender"; brief docs/briefs/realistic-enemies-and-guns.md).

The body's shapes, pieces, joints and paint are those of its own build script (the trooper's is
build_enemy_realistic.py); nothing about how it breaks apart changes. This script lays every shape out on one
square picture, gives each a material that starts from its paint and adds what paint cannot carry (the lie of
cloth, ribs in webbing, scratches in a shell: oa_materials.py), bakes that onto the pictures and writes the model
as "OAM3" with its far copy. Arms and legs are built once and mirrored, so both sides share one place on the
pictures and get twice the detail for it; what only one side has (the thigh pocket, the checklist) has its own.

    <python with bpy> tools/blender/build_enemy_textured.py -- --write-to <folder> [--preview <folder>] [--draft]
    <python with bpy> tools/blender/build_enemy_textured.py -- --game [--preview <folder>]

--write-to is a trial that changes nothing in the game; --game writes Enemy.bytes, EnemyFar.bytes and
Textures/Enemy_*.png. --draft bakes at half size with few samples, to look at quickly; never for the game.
"""
import bpy, sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import oa_blender as oa
import oa_materials as mats
import build_enemy as be
import build_enemy_realistic as br
from mathutils import Vector, Matrix

NAME, SIZE = "Enemy", 2048
GAME = be.OUT

# Which material each of the body script's paints becomes.
KINDS = {"fabric": lambda: mats.cloth("oa_cloth"), "webbing": lambda: mats.strap("oa_strap"),
         "hard": lambda: mats.shell("oa_shell", 0.42), "colour_band": lambda: mats.shell("oa_band", 0.30, seed=3.0),
         "metal": lambda: mats.fitting("oa_fitting"), "rubber": lambda: mats.skin_rubber("oa_rubber"),
         "gold": lambda: mats.mirror_coat("oa_gold", 0.88, 1.0), "glass": lambda: mats.mirror_coat("oa_glass", 0.90, 0.0),
         "flesh": lambda: mats.flesh("oa_flesh")}
# The wound is one unit across (the game scales it): it is baked this much smaller and off to one side, or it
# would take most of the picture.
STUMP_SCALE, STUMP_AT = 0.11, Vector((0.0, 0.0, -1.0))


def _tag(module, kind):
    plain = getattr(module, kind)

    def tagged(o, *args, **kwargs):
        out = plain(o, *args, **kwargs)
        o["oa_kind"] = kind
        return out
    setattr(module, kind, tagged)


for _kind in ("fabric", "webbing", "hard", "colour_band", "metal", "rubber", "gold", "glass"):
    _tag(br, _kind)
for _kind in ("metal", "rubber"):
    _tag(be, _kind)


def build(folder, previews=None, draft=False):
    oa.reset_scene()
    parts = br.make()
    stump = be.stump()
    for o in stump:
        o["oa_kind"] = "flesh"
    before = None
    if previews:
        os.makedirs(previews, exist_ok=True)
        everything = [o for name in be.ORDER for o in parts[name]]
        shot(os.path.join(previews, "enemy_before"), everything, mats.paint_only("oa_before"), draft)

    # One side's limbs are laid out and baked; the other side's are made again from them afterwards.
    own = []
    for name in be.ORDER:
        if name.startswith("Left"):
            shared = len(parts["Right" + name[4:]])
            own += parts[name][shared:]
        else:
            own += parts[name]
    move = Matrix.Translation(STUMP_AT) @ Matrix.Scale(STUMP_SCALE, 4)
    for o in stump:
        o.data.transform(move)
    low = own + stump
    mats.unwrap(low, margin=0.0025)
    made = {}
    high = []
    for o in low:
        kind = o.get("oa_kind", "hard")
        if kind not in made:
            made[kind] = KINDS[kind]()
        c = o.copy()
        c.data = o.data.copy()
        bpy.context.scene.collection.objects.link(c)
        c.data.materials.clear()
        c.data.materials.append(made[kind])
        oa.auto_smooth(c.data, o.get("oa_smooth", 60.0))
        high.append(c)
    print("OA %s: %d shapes laid out, %d triangles" % (NAME, len(low), sum(oa._triangle_count(o) for o in low)))
    textures = os.path.join(folder, "Textures")
    mats.bake_set(NAME, low, high, size=SIZE // 2 if draft else SIZE, cage=0.0012, samples=6 if draft else 32, folder=textures)
    for o in high:
        o.hide_render = True
    back = move.inverted()
    for o in stump:
        o.data.transform(back)
        o.data.update()
    for name in be.ORDER:
        if name.startswith("Left"):
            right = parts["Right" + name[4:]]
            old, extra = parts[name][:len(right)], parts[name][len(right):]
            for o in old:
                mesh = o.data
                bpy.data.objects.remove(o)
                bpy.data.meshes.remove(mesh)
            parts[name] = be.mirrored(right) + extra

    be.OUT = folder
    write(parts, stump)
    far = be.far_parts(parts, NAME, br.FAR_KEEP, br.FAR_LEAVE_OUT, br.FAR_THIN_TOO)
    write(far, None)
    holes = oa.inside_out_where([o for name in be.ORDER for o in far[name]])
    print("OA far copy, seen from inside:", holes if holes else "nothing")
    if previews:
        look = mats.textured_material(NAME, textures)
        for name in be.ORDER:
            for o in far[name]:
                o.hide_render = True
        shot(os.path.join(previews, "enemy_after"), [o for name in be.ORDER for o in parts[name]], look, draft)
        if not draft:
            shot(os.path.join(previews, "enemy_far"), [o for name in be.ORDER for o in far[name]], look, draft, close=False)


def write(parts, stump):
    """As be.write and be.write_far, with the place on the pictures. stump None: the far copy (no wound)."""
    listed, sockets = [], []
    for name in be.ORDER:
        origin = Vector(be.ORIGINS[name])
        for o in parts[name]:
            o.data.transform(Matrix.Translation(-origin))
        listed.append((name, parts[name]))
        sockets.append(oa.socket(name, origin))
    if stump is not None:
        listed.append(("Stump", stump))
    oa.export(os.path.join(be.OUT, NAME + ("" if stump is not None else oa.FAR) + ".bytes"), listed, sockets, textures=NAME)
    for name in be.ORDER:
        origin = Vector(be.ORIGINS[name])
        for o in parts[name]:
            o.data.transform(Matrix.Translation(origin))
            o.data.update()


def shot(path, objects, material, draft, close=True):
    samples = 32 if draft else 128
    mats.render(path + "_front.png", objects, material, (1.5, 3.6, 1.45), (0, 0, 0.98), lens=60, size=(900, 1300), samples=samples, floor=0.0)
    if close:
        mats.render(path + "_chest.png", objects, material, (0.55, 1.25, 1.55), (0.02, 0, 1.38), lens=60, size=(1200, 1000), samples=samples, floor=0.0)
        mats.render(path + "_leg.png", objects, material, (0.75, 1.05, 0.62), (0.16, 0, 0.42), lens=60, size=(1000, 1000), samples=samples, floor=0.0)
    if not draft and close:
        mats.render(path + "_back.png", objects, material, (-1.5, -3.6, 1.45), (0, 0, 0.98), lens=60, size=(900, 1300), samples=samples, floor=0.0)


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    previews = args[args.index("--preview") + 1] if "--preview" in args else None
    if "--game" in args and "--draft" in args:
        sys.exit("a draft is never written to the game")
    if "--game" in args:
        folder = GAME
    elif "--write-to" in args:
        folder = os.path.abspath(args[args.index("--write-to") + 1])
    else:
        sys.exit("say where to write: --write-to <folder> for a trial, or --game for the game's own files")
    build(folder, previews, draft="--draft" in args)
