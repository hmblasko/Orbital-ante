"""Offline pictures of the base models (Blender's renderer, not the game's shader).

Called by build_bases.py with --preview: each base on a stand-in for the round foundation the game builds,
with a 1.8 m figure in the middle, seen from the air and from a walker's eye. Lamps and windows are shown lit.
"""
import bpy, math, os
import oa_blender as oa
import oa_nature as nat


def render(folder, b, radius, dusk=True):
    os.makedirs(folder, exist_ok=True)
    ground = oa.lathe("ground", (0, 0, -1.2), (0, 0, 1), [(0.0, radius + 4.0), (1.0, radius + 1.2), (1.2, radius)], (0.60, 0.58, 0.54), segments=48)
    oa.densify(ground, 2.5)
    oa.paint(ground, wear=0.3, dirt=0.6, mottle=0.8, seed=3, grain=0.5)
    land = oa.box("land", (0, 0, -1.25), (radius * 14, radius * 14, 0.1), (0.52, 0.42, 0.33), bevel=0)
    nat.flat_paint(land, (0.52, 0.42, 0.33))
    figure = [oa.cylinder("figure", (0.8, 0, 0), (0.8, 0, 1.5), 0.2, (0.92, 0.9, 0.86), radius_end=0.16, segments=10),
              oa.sphere("figure_head", (0.8, 0, 1.66), 0.15, (0.95, 0.6, 0.2), segments=10)]
    for o in figure:
        nat.flat_paint(o, o["oa_colour"][:3])
    drawn = [o for name, objs in b.groups.items() for o in objs]
    lit = b.groups["WindowGlow"] + b.groups["LampGlow"]
    for o in b.hits:
        o.hide_render = True
    everything = drawn + [ground, land] + figure
    views = [("air", (radius * 1.25, -radius * 2.0, radius * 1.25), (0, 0, radius * 0.12), 35, (52, 0, 35), (0.42, 0.56, 0.8)),
             ("walk", (radius * 0.3, -radius * 1.55, 1.9), (0, 0, radius * 0.2), 24, (62, 0, -50), (0.30, 0.36, 0.60))]
    for tag, eye, target, lens, sun, world in views:
        oa.render_preview(os.path.join(folder, "%s_%s.png" % (b.name, tag)), everything, eye, target, lens=lens, size=(1500, 860),
                          sun=sun, world=world, samples=48, glow=lit, sun_energy=3.4 if tag == "air" else 2.2, sky=0.7 if tag == "air" else 0.5)
    for o in [ground, land] + figure:
        bpy.data.objects.remove(o)
