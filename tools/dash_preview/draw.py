"""Turns the JSON from tools/dash_preview/Program.cs into a picture: python3 draw.py in.json out.png"""
import json, sys
from PIL import Image, ImageDraw, ImageFont

K = 3                                   # drawn three times too big, then shrunk: smooth edges
W, H = 1000, 300
states = json.load(open(sys.argv[1]))
sheet = Image.new("RGB", (W * 2, (H + 8) * 2 * len(states)), (20, 16, 14))
bold = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
plain = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
for n, s in enumerate(states):
    im = Image.new("RGBA", (W * K, H * K), (0, 0, 0, 255))
    v = s["v"]
    for i in range(0, len(v), 3):
        layer = Image.new("RGBA", im.size, (0, 0, 0, 0)) if v[i][5] < 0.999 else None
        target = ImageDraw.Draw(layer if layer else im)
        c = v[i]
        target.polygon([(p[0] * K, (H - p[1]) * K) for p in v[i:i + 3]], fill=tuple(int(255 * min(1, max(0, x))) for x in c[2:6]))
        if layer:
            im = Image.alpha_composite(im, layer)
    cam = s["cam"]
    d = ImageDraw.Draw(im)
    d.rectangle([cam[0] * K, (H - cam[1] - cam[3]) * K, (cam[0] + cam[2]) * K, (H - cam[1]) * K], fill=(34, 40, 52, 255))
    d.pieslice([(cam[0] + 30) * K, (H - cam[1] - 50) * K, (cam[0] + cam[2] - 30) * K, (H - cam[1] + 50) * K], 180, 360, fill=(120, 104, 88, 255))
    for t in s["t"]:
        if not t["w"]:
            continue
        font = ImageFont.truetype(bold if t["b"] else plain, int(t["s"] * K * 1.05))
        box = d.textbbox((0, 0), t["w"], font=font)
        w, h = box[2] - box[0], box[3] - box[1]
        x = t["x"] * K - (w / 2 if t["a"] == 1 else w if t["a"] == 2 else 0)
        d.text((x - box[0], (H - t["y"]) * K - h / 2 - box[1]), t["w"], font=font, fill=tuple(int(255 * min(1, x)) for x in t["c"]))
    sheet.paste(im.convert("RGB").resize((W * 2, H * 2), Image.LANCZOS), (0, n * (H + 8) * 2))
sheet.save(sys.argv[2])
