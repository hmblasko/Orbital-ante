"""Puts the envelope's draw lists together into pictures, the way the game's EnvelopeScreen does.

    python3 compose.py <pieces folder> <show folder> <out folder> [--every N] [--bg picture.png]

Reads frames.json (from `dotnet run -- show ...`), the piece PNGs (from `dotnet run -- pieces ...`) and the
card (Card.png in the show folder), and writes frame_0000.png ... at 1280 x 800. Words are drawn with the game's
own B612 font from Resources/Fonts. These are pictures of the layout and timing, not Unity screenshots: Unity's
text and blending differ in small ways.
"""
import json
import math
import os
import re
import sys

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
FONTS = os.path.normpath(os.path.join(HERE, "..", "..", "unity", "Assets", "OrbitalAnte", "Resources", "Fonts"))
W, H = 1280, 800
SUPER = 1  # drawn at 1:1, like the Deck's screen


def font(size, bold):
    name = "B612-Bold.ttf" if bold else "B612-Regular.ttf"
    return ImageFont.truetype(os.path.join(FONTS, name), size)


def tinted(img, rgba):
    r, g, b, a = rgba
    if (r, g, b, a) == (1, 1, 1, 1):
        return img
    px = img.split()
    out = [px[0].point(lambda v: v * r), px[1].point(lambda v: v * g), px[2].point(lambda v: v * b), px[3].point(lambda v: v * a)]
    return Image.merge("RGBA", out)


def place(canvas, img, item):
    """Scale about the pivot, rotate about it clockwise, and put the pivot at (x, y)."""
    w = max(1, int(round(abs(item["w"] * item["sx"]))))
    h = max(1, int(round(abs(item["h"] * item["sy"]))))
    pic = img.resize((w, h), Image.BILINEAR)
    if item["sx"] < 0:
        pic = pic.transpose(Image.FLIP_LEFT_RIGHT)
    if item["sy"] < 0:
        pic = pic.transpose(Image.FLIP_TOP_BOTTOM)
    # Where the pivot sits inside the scaled picture (a negative scale mirrors it).
    pxf = item["px"] if item["sx"] >= 0 else 1 - item["px"]
    pyf = item["py"] if item["sy"] >= 0 else 1 - item["py"]
    pvx, pvy = pxf * w, pyf * h
    rot = item["rot"]
    if abs(rot) > 0.01:
        # Pad so the pivot is the middle, then rotate about the middle.
        half_w = max(pvx, w - pvx)
        half_h = max(pvy, h - pvy)
        pad = Image.new("RGBA", (int(math.ceil(half_w * 2)), int(math.ceil(half_h * 2))), (0, 0, 0, 0))
        pad.paste(pic, (int(round(half_w - pvx)), int(round(half_h - pvy))))
        pic = pad.rotate(-rot, resample=Image.BICUBIC, expand=True)
        pvx, pvy = pic.size[0] / 2, pic.size[1] / 2
    x0, y0 = int(round(item["x"] - pvx)), int(round(item["y"] - pvy))
    layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    layer.paste(pic, (x0, y0))
    canvas.alpha_composite(layer)


COLOR = re.compile(r"<color=(#[0-9A-Fa-f]{6})>(.*?)</color>")


def runs(text, base):
    """Splits Unity rich text into (words, colour) runs."""
    out, pos = [], 0
    for m in COLOR.finditer(text):
        if m.start() > pos:
            out.append((text[pos:m.start()], base))
        hexc = m.group(1)
        out.append((m.group(2), tuple(int(hexc[i:i + 2], 16) for i in (1, 3, 5))))
        pos = m.end()
    if pos < len(text):
        out.append((text[pos:], base))
    return out


def wrap(draw, text, f, width):
    words, lines, line = text.split(" "), [], ""
    for w_ in words:
        trial = (line + " " + w_).strip()
        if draw.textlength(trial, font=f) <= width or not line:
            line = trial
        else:
            lines.append(line)
            line = w_
    if line:
        lines.append(line)
    return lines


def text_image(item):
    w, h = int(item["w"]), int(item["h"])
    f = font(int(item["size"]), item["bold"])
    img = Image.new("RGBA", (w, max(h, 1)), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    r, g, b, a = item["rgba"]
    base = (int(r * 255), int(g * 255), int(b * 255))
    alpha = int(a * 255)
    rich = runs(item["text"], base)
    plain = "".join(t for t, _ in rich)
    lines = wrap(d, plain, f, w) if len(rich) == 1 else [plain]
    asc, desc = f.getmetrics()
    lh = asc + desc
    top = (h - lh * len(lines)) / 2
    for li, line in enumerate(lines):
        lw = d.textlength(line, font=f)
        x = 0 if item["align"] == 0 else (w - lw if item["align"] == 2 else (w - lw) / 2)
        y = top + li * lh
        if len(rich) == 1:
            # The halo the game's labels have over the world (UiKit.Halo): a soft dark edge.
            for ox, oy in ((1, 1), (-1, 1), (1, -1), (-1, -1)):
                d.text((x + ox, y + oy), line, font=f, fill=(0, 0, 0, int(alpha * 0.45)))
            d.text((x, y), line, font=f, fill=base + (alpha,))
        else:
            for words, col in rich:
                d.text((x, y), words, font=f, fill=col + (alpha,))
                x += d.textlength(words, font=f)
    return img


def draw_items(canvas, items, textures, offset=(0, 0)):
    by_parent = {}
    for it in items:
        by_parent.setdefault(it["parent"], []).append(it)
    for it in by_parent.get("", []):
        draw_one(canvas, it, textures, by_parent)


def draw_one(canvas, it, textures, by_parent):
    if it["kind"] == 0:
        tex = textures.get(it["tex"])
        if tex is None:
            return
        place(canvas, tinted(tex, it["rgba"]), it)
    elif it["kind"] == 1:
        place(canvas, text_image(it), it)
    else:
        if it["w"] <= 0 or it["h"] <= 0:
            return
        # A group that does not clip (as in the game, where only a group marked clip has a mask) keeps what its
        # children draw a little outside its edge: a Celestial prize's beads sit just outside the card.
        m = 0 if it.get("clip") else 32
        w, h = it["w"] + 2 * m, it["h"] + 2 * m
        group = Image.new("RGBA", (int(w), int(h)), (0, 0, 0, 0))
        for child in by_parent.get(it["key"], []):
            draw_one(group, child if m == 0 else dict(child, x=child["x"] + m, y=child["y"] + m), textures, by_parent)
        a = it["rgba"][3]
        if a < 1:
            group = tinted(group, (1, 1, 1, a))
        place(canvas, group, dict(it, w=w, h=h, px=(it["px"] * it["w"] + m) / w, py=(it["py"] * it["h"] + m) / h))


def main():
    args = sys.argv[1:]
    pieces, show, out = args[0], args[1], args[2]
    every = int(args[args.index("--every") + 1]) if "--every" in args else 1
    bg = args[args.index("--bg") + 1] if "--bg" in args else None
    os.makedirs(out, exist_ok=True)
    textures = {}
    for name in os.listdir(pieces):
        if name.endswith(".png"):
            textures[name[:-4]] = Image.open(os.path.join(pieces, name)).convert("RGBA")
    textures["Card"] = Image.open(os.path.join(show, "Card.png")).convert("RGBA")
    data = json.load(open(os.path.join(show, "frames.json")))
    background = Image.open(bg).convert("RGBA").resize((W, H)) if bg else None
    for i, frame in enumerate(data["frames"]):
        if i % every:
            continue
        canvas = background.copy() if background else Image.new("RGBA", (W, H), (38, 44, 58, 255))
        draw_items(canvas, frame["items"], textures)
        canvas.convert("RGB").save(os.path.join(out, "frame_%04d.png" % (i // every)))
    print("wrote", (len(data["frames"]) + every - 1) // every, "frames to", out)


if __name__ == "__main__":
    main()
