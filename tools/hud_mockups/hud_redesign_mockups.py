"""Offline mockups of the redesigned on-screen display, at the Steam Deck's 1280x800. Not game footage.

Drawn at 2x and scaled down, so text sizes match what the Deck shows. Fonts come from google/fonts (all SIL OFL):
put them in FONT_DIR first (see FONTS below). Background: the seat view still front-window-seat.png.

    python3 tools/hud_mockups/hud_redesign_mockups.py <background.png> <out dir>
"""
import math
import os
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageFont

S = 2
W, H = 1280, 800
FONT_DIR = os.environ.get("FONT_DIR", "/tmp/claude-0/fonts/")
BG = sys.argv[1] if len(sys.argv) > 1 else "/mnt/project-files/orbital-ante/front-window-seat.png"
OUT = sys.argv[2] if len(sys.argv) > 2 else "/tmp/claude-0/hud_redesign"

CREAM = (246, 234, 210)
DIM = (176, 166, 150)
AMBER = (245, 158, 52)
RED = (236, 72, 56)
TARGET = (130, 214, 250)
PLATE = (10, 9, 8, 150)
RULE = (246, 234, 210, 90)

FONTS = {
    "shoulders": "bigshoulders_BigShoulders[opsz,wght].ttf",
    "martian": "martianmono_MartianMono[wdth,wght].ttf",
    "saira": "sairacondensed_SairaCondensed-Bold.ttf",
    "sairamed": "sairacondensed_SairaCondensed-Medium.ttf",
    "chivo": "chivomono_ChivoMono[wght].ttf",
    "b612": "b612_B612-Bold.ttf",
    "b612mono": "b612mono_B612Mono-Bold.ttf",
}


def font(key, size, axes=None):
    f = ImageFont.truetype(FONT_DIR + FONTS[key], int(size * S))
    if axes is not None:
        f.set_variation_by_axes(axes)
    return f


STYLE = {"num": "shoulders", "lab": "martian"}


def numbers(size):
    """The option's number face: Big Shoulders (heavy, display size) or Saira Condensed Bold."""
    if STYLE["num"] == "saira":
        return font("saira", size)
    return font("shoulders", size, [800, 72])


def label(size, weight=500):
    """The option's label face: Martian Mono (a little narrow) or Chivo Mono."""
    if STYLE["lab"] == "chivo":
        return font("chivo", size, [max(weight, 500)])
    return font("martian", size, [weight, 100])


class Layer:
    def __init__(self):
        self.img = Image.new("RGBA", (W * S, H * S), (0, 0, 0, 0))
        self.d = ImageDraw.Draw(self.img)

    def text(self, xy, s, f, fill, anchor="la", spacing=0):
        x, y = xy
        if spacing:
            # Hand-tracked caps for small labels: wider letter spacing reads better small.
            widths = [self.d.textlength(c, font=f) + spacing * S for c in s]
            total = sum(widths) - spacing * S
            if anchor[0] == "r":
                x0 = x * S - total
            elif anchor[0] == "m":
                x0 = x * S - total / 2
            else:
                x0 = x * S
            for c, w in zip(s, widths):
                self.d.text((x0, y * S), c, font=f, fill=fill, anchor="l" + anchor[1])
                x0 += w
            return total / S
        self.d.text((x * S, y * S), s, font=f, fill=fill, anchor=anchor)
        return self.d.textlength(s, font=f) / S

    def rect(self, x0, y0, x1, y1, fill=None, outline=None, width=1):
        self.d.rectangle([x0 * S, y0 * S, x1 * S, y1 * S], fill=fill, outline=outline, width=int(width * S))

    def line(self, pts, fill, width=1):
        self.d.line([(x * S, y * S) for x, y in pts], fill=fill, width=int(round(width * S)))

    def poly(self, pts, fill):
        self.d.polygon([(x * S, y * S) for x, y in pts], fill=fill)

    def compose(self, onto, halo=0.7, radius=2.5):
        alpha = self.img.split()[3]
        shadow = Image.new("RGBA", self.img.size, (6, 5, 4, 0))
        a = alpha.filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.GaussianBlur(radius * S))
        a = a.point(lambda v: int(min(255, v * 1.6) * halo))
        shadow.putalpha(a)
        onto.alpha_composite(shadow)
        onto.alpha_composite(self.img)


def base():
    bg = Image.open(BG).convert("RGBA").resize((W * S, H * S), Image.LANCZOS)
    return bg


def plate(L, x0, y0, x1, y1, alpha=150, notch=True):
    """A dark see-through plate with square corners and one clipped corner: hardware, not a web card."""
    c = 7
    pts = [(x0, y0), (x1 - (c if notch else 0), y0), (x1, y0 + (c if notch else 0)), (x1, y1), (x0, y1)]
    L.poly(pts, (10, 9, 8, alpha))


def world_markers(L):
    """The world labels that float on bodies (kept as they are today, in the new lettering)."""
    f = label(13)
    for x, y, name, dist in [(300, 178, "DEIMOS", "21 km"), (1000, 118, "JUPITER", "1,904 km")]:
        L.d.ellipse([(x - 2.5) * S, (y - 2.5) * S, (x + 2.5) * S, (y + 2.5) * S], fill=CREAM)
        L.text((x, y + 16), name + "  " + dist, f, DIM, "mm", spacing=1)
    # Locked target: corner brackets round the body, the numbers you act on right beside it.
    x, y = 820, 205
    L.d.ellipse([(x - 3) * S, (y - 3) * S, (x + 3) * S, (y + 3) * S], fill=TARGET)
    r = 16
    for sx in (-1, 1):
        for sy in (-1, 1):
            cx, cy = x + sx * r, y + sy * r
            L.line([(cx, cy - sy * 6), (cx, cy), (cx - sx * 6, cy)], TARGET, 2)
    L.text((x + 26, y - 14), "PHOBOS", label(14, 700), TARGET, "lm", spacing=1)
    L.text((x + 26, y + 6), "8.1", numbers(24), TARGET, "lm")
    L.text((x + 62, y + 9), "KM", label(11), TARGET, "lm")
    L.poly([(x + 90, y + 1), (x + 102, y + 1), (x + 96, y + 10)], AMBER)   # closing: pointing down
    L.text((x + 108, y + 6), "230", numbers(24), AMBER, "lm")
    L.text((x + 152, y + 9), "M/S CLOSING", label(11), AMBER, "lm")


def crosshair(L):
    cx, cy = W / 2, H / 2
    for dx, dy in [(1, 0), (-1, 0), (0, 1), (0, -1)]:
        L.line([(cx + dx * 7, cy + dy * 7), (cx + dx * 15, cy + dy * 15)], CREAM + (220,), 2)


def ante_block(L, x, y, nf, lf):
    """The ante timer, score and streak (being added by the ante tasks work): top left, where the debug text was."""
    plate(L, x - 12, y - 10, x + 238, y + 88, 140)
    L.text((x, y + 2), "ANTE 3", lf, DIM, "la", spacing=2)
    L.text((x + 236 - 14, y + 2), "DUE", lf, DIM, "ra", spacing=2)
    L.text((x, y + 20), "12,450", nf(40), CREAM, "la")
    L.text((x + 224, y + 20), "4:12", nf(40), AMBER, "ra")
    # Streak pips
    for i in range(6):
        on = i < 4
        L.rect(x + i * 16, y + 74, x + i * 16 + 11, y + 78, fill=(AMBER + (255,)) if on else (246, 234, 210, 60))
    L.text((x + 104, y + 76), "STREAK ×4", lf, AMBER, "lm", spacing=1)


def damage_block(L, x1, y, nf, lf):
    """Hurt ship parts, top right: name, a short bar and the per cent, broken ones in red."""
    rows = [("THRUSTER 2", 0.54, None), ("FRONT GLASS", 0.81, None), ("LEG 1", 0.0, "BROKEN")]
    plate(L, x1 - 290, y - 12, x1 + 12, y + 26 * len(rows) + 12, 140)
    L.text((x1 - 278, y - 2), "SHIP", lf, DIM, "la", spacing=2)
    for i, (name, f, word) in enumerate(rows):
        yy = y + 22 + i * 26
        col = RED if word else (AMBER if f < 0.6 else CREAM)
        L.text((x1 - 278, yy), name, label(14, 600), col, "lm", spacing=1)
        bx = x1 - 128
        L.rect(bx, yy - 3, bx + 44, yy + 3, fill=(246, 234, 210, 50))
        if f > 0:
            L.rect(bx, yy - 3, bx + 44 * f, yy + 3, fill=col + (255,))
        L.text((x1, yy + 1), word or "%d%%" % round(f * 100), nf(22) if not word else label(13, 700), col, "rm")


def keys_line(L, faded=True):
    f = label(13)
    a = 110 if faded else 200
    L.text((W / 2, H - 38), "W A S D  THRUST     RMB  TURN     SHIFT  BOOST     F  MATCH     T  LOCK     G  AUTOPILOT     M  MAP",
           f, DIM + (a,), "mm", spacing=1)


def caption(img, name, title):
    img = img.convert("RGB").resize((W, H), Image.LANCZOS)
    d = ImageDraw.Draw(img)
    cap = ImageFont.truetype(FONT_DIR + FONTS["b612"], 13)
    d.rectangle([0, H - 22, W, H], fill=(0, 0, 0))
    d.text((8, H - 18), title + "   |   offline mockup at 1280x800, not game footage", font=cap, fill=(255, 255, 255))
    os.makedirs(OUT, exist_ok=True)
    img.save(f"{OUT}/{name}.png")
    return img


def caption_free(img):
    return img.convert("RGB").resize((W, H), Image.LANCZOS)


# ------------------------------------------------------------------- A: flight tapes
def tape(L, x, y, value, step, fmt, title, unit, under, left_side, nf, lf, col=CREAM):
    """A moving ladder like an aircraft's speed and height tapes: the window shows the number, ticks roll past."""
    h = 230
    w = 74
    x0 = x - w / 2
    plate(L, x0, y - h / 2, x0 + w, y + h / 2, 95, notch=False)
    px_per_step = 26
    frac = (value / step) % 1
    base_v = math.floor(value / step)
    for k in range(-5, 6):
        v = (base_v + k) * step
        yy = y - (k - frac) * px_per_step
        if yy < y - h / 2 + 6 or yy > y + h / 2 - 6:
            continue
        major = (base_v + k) % 2 == 0
        tx = x0 + w if left_side else x0
        d = -1 if left_side else 1
        L.line([(tx, yy), (tx + d * (12 if major else 6), yy)], RULE, 1.5)
        if major and abs(yy - y) > 24 and v >= 0:
            L.text((tx + d * 18, yy), fmt(v), label(11), DIM, ("r" if left_side else "l") + "m")
    # The reading window, pointing in at the crosshair.
    bw, bh = 92, 40
    bx0 = x - bw / 2 + (8 if left_side else -8)
    tip = (bx0 + bw + 10, y) if left_side else (bx0 - 10, y)
    L.poly([(bx0, y - bh / 2), (bx0 + bw, y - bh / 2), tip, (bx0 + bw, y + bh / 2), (bx0, y + bh / 2)] if left_side else
           [(bx0, y - bh / 2), (bx0 + bw, y - bh / 2), (bx0 + bw, y + bh / 2), (bx0, y + bh / 2), tip], (8, 7, 6, 235))
    L.line([(bx0, y - bh / 2), (bx0 + bw, y - bh / 2)], col + (255,), 2)
    L.line([(bx0, y + bh / 2), (bx0 + bw, y + bh / 2)], col + (255,), 2)
    L.text((bx0 + bw / 2, y + 1), fmt(value), nf(34), col, "mm")
    L.text((x, y - h / 2 - 22), title, lf, CREAM, "mm", spacing=2)
    L.text((x, y - h / 2 - 8), unit, label(11), DIM, "mm", spacing=1)
    for i, line in enumerate(under):
        L.text((x, y + h / 2 + 14 + i * 17), line[0], label(12, 600), line[1], "mm", spacing=1)


def option_a():
    STYLE.update(num="shoulders", lab="martian")
    img = base()
    L = Layer()
    nf, lf = numbers, label(13, 700)
    world_markers(L)
    crosshair(L)
    cx, cy = W / 2, H / 2
    tape(L, cx - 250, cy + 10, 212, 10, lambda v: "%d" % v, "SPEED", "M/S  vs GROUND",
         [("PULL 3.7 M/S²", DIM)], True, nf, lf)
    tape(L, cx + 250, cy + 10, 1.24, 0.1, lambda v: "%.2f" % v, "HEIGHT", "KM  ABOVE GROUND",
         [("WIND 14 M/S  →", DIM)], False, nf, lf)
    # Where: top middle, the body's name big, then the orbit bar under it.
    L.text((cx, 34), "MARS", numbers(40), AMBER, "mm")
    orbit_bar(L, cx, 72, lf)
    # Warning lamp under the crosshair: one word that matters, in a lit box.
    lamp(L, cx, cy + 150, "BRAKE", "AIM AT PHOBOS, HOLD F", RED, big=True)
    ante_block(L, 34, 26, nf, lf)
    damage_block(L, W - 34, 30, nf, lf)
    keys_line(L)
    L.compose(img)
    return caption(img, "A_flight_tapes", "A  Flight tapes  (Big Shoulders numbers, Martian Mono labels)")


def orbit_bar(L, cx, y, lf, width=300):
    """Orbit at a glance: the bar is your speed across the world, the notch is what a circle here needs."""
    you, need = 1.1, 3.2
    full = need * 1.5
    x0 = cx - width / 2
    L.text((x0 - 10, y), "ORBIT", lf, DIM, "rm", spacing=2)
    L.rect(x0, y - 3, x0 + width, y + 3, fill=(246, 234, 210, 55))
    L.rect(x0, y - 3, x0 + width * you / full, y + 3, fill=AMBER + (255,))
    nx = x0 + width * need / full
    L.line([(nx, y - 9), (nx, y + 9)], CREAM, 2)
    L.text((nx, y + 18), "3.2 NEEDED", label(11), CREAM, "mm", spacing=1)
    L.text((x0 + width * you / full, y - 15), "1.1 KM/S", label(11, 700), AMBER, "mm", spacing=1)
    L.text((x0 + width + 10, y), "FALLING", lf, AMBER, "lm", spacing=2)


def lamp(L, x, y, word, sub, col, big=False, lit=True, w=None):
    f = numbers(30 if big else 20)
    tw = L.d.textlength(word, font=f) / S
    w = w or tw + 40
    h = 44 if big else 28
    if lit:
        L.rect(x - w / 2, y - h / 2, x + w / 2, y + h / 2, fill=col + (235,))
        L.text((x, y + 1), word, f, (20, 12, 8), "mm")
    else:
        L.rect(x - w / 2, y - h / 2, x + w / 2, y + h / 2, fill=(10, 9, 8, 120), outline=(246, 234, 210, 70), width=1)
        L.text((x, y + 1), word, f, (246, 234, 210, 90), "mm")
    if sub:
        L.text((x, y + h / 2 + 14), sub, label(13, 700), col, "mm", spacing=1)


# ------------------------------------------------------------------- B: dash strip and warning lamps
def option_b():
    STYLE.update(num="saira", lab="chivo")
    img = base()
    L = Layer()
    nf = lambda s: font("saira", s)
    lf = font("chivo", 12, [600])
    world_markers(L)
    crosshair(L)
    cx = W / 2
    L.text((cx, 34), "MARS", nf(40), AMBER, "mm")
    L.text((cx, 64), "1.24 KM ABOVE THE GROUND", font("chivo", 14, [600]), CREAM, "mm", spacing=1)
    # The strip sits on the dash, under the window, like a row of real instruments.
    y0, y1 = 628, 724
    x0, x1 = 70, W - 70
    plate(L, x0, y0, x1, y1, 175)
    L.line([(x0, y0), (x1 - 7, y0)], AMBER + (255,), 2)
    cells = [
        ("SPEED", "212", "M/S", "VS MARS GROUND", CREAM),
        ("HEIGHT", "1.24", "KM", "ABOVE GROUND", CREAM),
        ("PULL", "3.7", "M/S²", "THRUST 9.0", CREAM),
        ("ORBIT", "1.1", "KM/S", "NEEDS 3.2", AMBER),
        ("WIND", "14", "M/S", "PUSHING RIGHT", CREAM),
    ]
    cw = (x1 - x0) / len(cells)
    for i, (name, num, unit, sub, col) in enumerate(cells):
        cx0 = x0 + i * cw
        if i:
            L.line([(cx0, y0 + 14), (cx0, y1 - 14)], RULE, 1)
        L.text((cx0 + 20, y0 + 18), name, lf, DIM, "lm", spacing=2)
        nw = L.text((cx0 + 20, y0 + 54), num, nf(46), col, "lm")
        L.text((cx0 + 26 + nw, y0 + 62), unit, lf, DIM, "lm")
        L.text((cx0 + 20, y1 - 14), sub, lf, col if col != CREAM else DIM, "lm", spacing=1)
        if name == "ORBIT":
            # Your speed across against the notch a circle needs.
            bx0, bw = cx0 + 128, 80
            L.rect(bx0, y1 - 17, bx0 + bw, y1 - 11, fill=(246, 234, 210, 55))
            L.rect(bx0, y1 - 17, bx0 + bw * 1.1 / 4.8, y1 - 11, fill=AMBER + (255,))
            nx = bx0 + bw * 3.2 / 4.8
            L.line([(nx, y1 - 22), (nx, y1 - 6)], CREAM, 2)
    # Lamp row just above the strip: each state is a lamp that lights, not a sentence.
    lamps = [("BOOST", False, AMBER), ("MATCH", False, AMBER), ("LOCK", True, TARGET), ("AUTO", False, AMBER),
             ("FALLING", True, AMBER), ("BRAKE", True, RED)]
    lw, gap = 104, 10
    total = len(lamps) * lw + (len(lamps) - 1) * gap
    for i, (word, lit, col) in enumerate(lamps):
        x = cx - total / 2 + lw / 2 + i * (lw + gap)
        lamp(L, x, y0 - 24, word, None, col, lit=lit, w=lw)
    L.text((cx, y0 - 52), "BRAKE NOW: AIM AT PHOBOS AND HOLD F", font("chivo", 15, [700]), RED, "mm", spacing=1)
    ante_block(L, 34, 26, nf, lf)
    damage_block(L, W - 34, 30, nf, lf)
    keys_line(L)
    L.compose(img)
    return caption(img, "B_dash_strip_lamps", "B  Dash strip and warning lamps  (Saira Condensed numbers, Chivo Mono labels)")


# ------------------------------------------------------------------- C: corner clusters (keeps B612)
def option_c():
    img = base()
    L = Layer()
    nf = lambda s: font("b612mono", s * 0.82)
    lf = font("b612", 12)
    world_markers(L)
    crosshair(L)
    cx = W / 2

    def readout(x, y, name, num, unit, anchor="l", col=CREAM, size=44):
        L.text((x, y), name, lf, DIM, anchor + "a", spacing=2)
        L.text((x, y + 18), num, nf(size), col, anchor + "a")
        nw = L.d.textlength(num, font=nf(size)) / S
        if anchor == "l":
            L.text((x + nw + 6, y + 40), unit, lf, DIM, "la")
        else:
            L.text((x - nw - 6, y + 40), unit, lf, DIM, "ra")

    plate(L, 20, 560, 420, 750, 120)
    readout(40, 576, "SPEED vs GROUND", "212", "M/S")
    readout(230, 576, "PULL", "3.7", "M/S²", size=32)
    readout(40, 660, "HEIGHT", "1.24", "KM")
    readout(230, 660, "WIND", "14", "M/S", size=32)
    # Orbit as an arc you fill: the tick is what a circle needs.
    ox, oy, r = W - 150, 655, 70
    L.d.arc([(ox - r) * S, (oy - r) * S, (ox + r) * S, (oy + r) * S], 150, 390, fill=(246, 234, 210, 60), width=7 * S)
    L.d.arc([(ox - r) * S, (oy - r) * S, (ox + r) * S, (oy + r) * S], 150, 150 + 240 * 1.1 / 4.8, fill=AMBER, width=7 * S)
    a = math.radians(150 + 240 * 3.2 / 4.8)
    L.line([(ox + math.cos(a) * (r - 10), oy + math.sin(a) * (r - 10)), (ox + math.cos(a) * (r + 10), oy + math.sin(a) * (r + 10))], CREAM, 2.5)
    L.text((ox, oy - 6), "1.1", nf(34), AMBER, "mm")
    L.text((ox, oy + 22), "ORBIT KM/S", lf, DIM, "mm", spacing=1)
    L.text((ox, oy + 50), "NEEDS 3.2  FALLING", lf, AMBER, "mm", spacing=1)
    L.text((cx, 34), "MARS", nf(38), AMBER, "mm")
    lamp(L, cx, H / 2 + 150, "BRAKE", "AIM AT PHOBOS, HOLD F", RED, big=True)
    ante_block(L, 34, 26, nf, lf)
    damage_block(L, W - 34, 30, nf, lf)
    keys_line(L)
    L.compose(img)
    return caption(img, "C_corner_clusters_B612", "C  Corner clusters  (B612, the font you have now, tidied)")


def before_after(a, name):
    """Today's seat view next to an option, scaled to half size each."""
    before = Image.open("/mnt/project-files/orbital-ante/hud/cleanup/hud_cleanup_1_before_flight.png").convert("RGB")
    canvas = Image.new("RGB", (W, H // 2 + 30), (0, 0, 0))
    canvas.paste(before.resize((W // 2, H // 2)), (0, 30))
    canvas.paste(a.resize((W // 2, H // 2)), (W // 2, 30))
    d = ImageDraw.Draw(canvas)
    cap = ImageFont.truetype(FONT_DIR + FONTS["b612"], 14)
    d.text((8, 8), "TODAY", font=cap, fill=(255, 255, 255))
    d.text((W // 2 + 8, 8), "PROPOSED", font=cap, fill=(255, 255, 255))
    canvas.save(f"{OUT}/{name}.png")


if __name__ == "__main__":
    a = option_a()
    b = option_b()
    before_after(a, "before_after_A")
