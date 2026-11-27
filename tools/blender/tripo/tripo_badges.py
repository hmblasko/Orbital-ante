import bpy, numpy as np, colorsys
bpy.ops.wm.open_mainfile(filepath="/tmp/claude-0/tripo/work/cut.blend")
h = bpy.data.objects["high"]; m = h.data
im = next(n.image for n in m.materials[0].node_tree.nodes if n.bl_idname == "ShaderNodeTexImage" and "basecolor" in n.image.name)
a = np.empty(im.size[0]*im.size[1]*4, dtype=np.float32); im.pixels.foreach_get(a); a = a.reshape(im.size[1], im.size[0], 4)
uv = m.uv_layers[0].data
cells = {}
for p in m.polygons:
    u = uv[p.loop_start].uv
    r, g, b = a[int(u[1] % 1 * 4095), int(u[0] % 1 * 4095), :3]
    hh, s, v = colorsys.rgb_to_hsv(r, g, b)
    kind = None
    if s > 0.55 and v > 0.3 and (hh < 0.04 or hh > 0.95): kind = "red"
    elif s > 0.5 and v > 0.3 and 0.55 < hh < 0.72: kind = "blue"
    elif s > 0.6 and v > 0.5 and 0.10 < hh < 0.18: kind = "yellow"
    if kind:
        c = p.center
        key = (kind, round(c.x / 0.06), round(c.y / 0.06), round(c.z / 0.06))
        cells.setdefault(key, []).append(tuple(c))
for k, pts in sorted(cells.items(), key=lambda kv: -len(kv[1])):
    if len(pts) < 25: continue
    p = np.array(pts)
    print("OA", k[0], len(pts), "centre", p.mean(0).round(3), "lo", p.min(0).round(3), "hi", p.max(0).round(3))
