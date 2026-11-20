"""Four more seat-display looks (C to F) for Matthew to compare with A and B. Offline mockups at 1280x800, not game
footage. Uses the helpers in hud_redesign_mockups.py.

    python3 tools/hud_mockups/hud_more_options.py <background.png> <out dir>
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import hud_redesign_mockups as m  # noqa: E402
from PIL import ImageFont  # noqa: E402

S, W, H = m.S, m.W, m.H
CREAM, DIM, AMBER, RED, TARGET = m.CREAM, m.DIM, m.AMBER, m.RED, m.TARGET
FD = m.FONT_DIR

m.FONTS.update({
    "plexmono": "ibmplexmono_IBMPlexMono-SemiBold.ttf",
    "plexmono_med": "ibmplexmono_IBMPlexMono-Medium.ttf",
    "barlow": "barlowcondensed_BarlowCondensed-SemiBold.ttf",
    "barlow_med": "barlowcondensed_BarlowCondensed-Medium.ttf",
    "antonio": "antonio_Antonio[wght].ttf",
    "plexcond": "ibmplexsanscondensed_IBMPlexSansCondensed-SemiBold.ttf",
})


def use(numbers_fn, label_fn):
    """Point the shared helpers (world labels, ship damage, ante box) at this option's faces."""
    m.numbers = numbers_fn
    m.label = label_fn


def face(key, axes=None):
    return lambda size, *a, **k: m.font(key, size, axes)


# ------------------------------------------------------------------- C: one-colour glass HUD
def option_c():
    """Everything drawn in one warm colour as thin lines on the window, like a real fighter jet's head-up display.
    No plates, no panels. Red is kept for BRAKE alone."""
    ink = (255, 196, 110)
    use(lambda size, *a, **k: m.font("plexmono", size * 0.68), lambda size, weight=500: m.font("plexmono", size))
    img = m.base()
    L = m.Layer()
    m.world_markers(L)
    cx, cy = W / 2, H / 2
    f_big, f_small, f_tiny = m.font("plexmono", 26), m.font("plexmono", 13), m.font("plexmono", 11)

    # Flight path marker (where you are drifting) instead of a plain cross: a ring with wings and a fin.
    fx, fy = cx + 70, cy + 38
    r = 8
    L.d.ellipse([(fx - r) * S, (fy - r) * S, (fx + r) * S, (fy + r) * S], outline=ink, width=2 * S)
    L.line([(fx - r - 14, fy), (fx - r, fy)], ink, 2)
    L.line([(fx + r, fy), (fx + r + 14, fy)], ink, 2)
    L.line([(fx, fy - r), (fx, fy - r - 8)], ink, 2)
    m.crosshair(L)

    # Orbit as an arc round the middle: the arc fills to your speed across, the tick is what a circle needs.
    R = 120
    box = [(cx - R) * S, (cy - R) * S, (cx + R) * S, (cy + R) * S]
    L.d.arc(box, 200, 340, fill=ink + (70,), width=2 * S)
    L.d.arc(box, 200, 200 + 140 * 1.1 / 4.8, fill=ink, width=4 * S)
    a = math.radians(200 + 140 * 3.2 / 4.8)
    L.line([(cx + math.cos(a) * (R - 9), cy + math.sin(a) * (R - 9)), (cx + math.cos(a) * (R + 9), cy + math.sin(a) * (R + 9))], ink, 2)
    L.text((cx, cy - R - 16), "ORBIT 1.1 / 3.2 KM/S  FALLING", f_tiny, ink, "mm", spacing=1)

    # Speed and height in open boxes either side.
    for x, title, num, unit, note, anchor in [(cx - 260, "SPD", "212", "M/S", "VS GROUND", "r"),
                                              (cx + 260, "ALT", "1.24", "KM", "AGL", "l")]:
        bw, bh = 104, 40
        x0 = x - bw if anchor == "r" else x
        L.rect(x0, cy - bh / 2, x0 + bw, cy + bh / 2, outline=ink, width=2)
        L.text((x0 + bw / 2, cy + 1), num, f_big, ink, "mm")
        L.text((x0 + bw / 2, cy - bh / 2 - 12), title + "  " + unit, f_small, ink, "mm", spacing=2)
        L.text((x0 + bw / 2, cy + bh / 2 + 14), note, f_tiny, ink + (190,), "mm", spacing=1)
    # Pull and wind as small lines under the boxes.
    L.text((cx - 312, cy + 62), "G 3.7  THR 9.0", f_tiny, ink + (190,), "mm", spacing=1)
    L.text((cx + 312, cy + 62), "WIND 14 →", f_tiny, ink + (190,), "mm", spacing=1)

    # Mode words along the bottom of the glass: bracketed when on, faint when off.
    words = [("BOOST", False), ("MATCH", False), ("LOCK", True), ("AUTO", False), ("BRAKE", True)]
    x = cx - 260
    for word, on in words:
        col = (RED if word == "BRAKE" else ink) + ((255,) if on else (60,))
        text = ("[" + word + "]") if on else (" " + word + " ")
        w = L.text((x, 470), text, f_small, col, "lm", spacing=2)
        x += w + 26
    L.text((cx, 500), "BRAKE: AIM AT PHOBOS, HOLD F", f_small, RED, "mm", spacing=1)

    # Where, top middle.
    L.text((cx, 36), "MARS", m.font("plexmono", 30), ink, "mm", spacing=6)
    L.text((cx, 62), "1.24 KM ABOVE GROUND", f_tiny, ink + (200,), "mm", spacing=2)
    m.ante_block(L, 34, 26, m.numbers, f_small)
    m.damage_block(L, W - 34, 30, m.numbers, f_small)
    m.keys_line(L)
    L.compose(img, halo=0.55, radius=1.8)
    return m.caption(img, "C_glass_hud", "C  Glass HUD, one colour  (IBM Plex Mono)")


# ------------------------------------------------------------------- D: analog dials
def dial(L, x, y, r, title, value, lo, hi, majors, readout, unit, needle_col=CREAM, notch=None, warn_from=None):
    """A round gauge: dark face, printed ticks and numbers, a needle, and a small digital window."""
    L.d.ellipse([(x - r - 4) * S, (y - r - 4) * S, (x + r + 4) * S, (y + r + 4) * S], fill=(48, 44, 40, 235))
    L.d.ellipse([(x - r) * S, (y - r) * S, (x + r) * S, (y + r) * S], fill=(12, 11, 10, 240))
    a0, a1 = 135, 405   # PIL degrees, clockwise from 3 o'clock: a 270 degree sweep
    if warn_from is not None:
        aw = a0 + (a1 - a0) * (warn_from - lo) / (hi - lo)
        L.d.arc([(x - r + 6) * S, (y - r + 6) * S, (x + r - 6) * S, (y + r - 6) * S], aw, a1, fill=RED + (200,), width=4 * S)
    for i in range(majors * 5 + 1):
        t = i / (majors * 5)
        a = math.radians(a0 + (a1 - a0) * t)
        major = i % 5 == 0
        r0 = r - (14 if major else 8)
        L.line([(x + math.cos(a) * r0, y + math.sin(a) * r0), (x + math.cos(a) * (r - 4), y + math.sin(a) * (r - 4))],
               CREAM + ((255,) if major else (150,)), 2 if major else 1)
        if major:
            v = lo + (hi - lo) * t
            L.text((x + math.cos(a) * (r - 26), y + math.sin(a) * (r - 26)), ("%g" % v), m.font("barlow", 13), DIM, "mm")
    if notch is not None:
        a = math.radians(a0 + (a1 - a0) * (notch - lo) / (hi - lo))
        L.poly([(x + math.cos(a) * (r - 2), y + math.sin(a) * (r - 2)),
                (x + math.cos(a + 0.07) * (r + 10), y + math.sin(a + 0.07) * (r + 10)),
                (x + math.cos(a - 0.07) * (r + 10), y + math.sin(a - 0.07) * (r + 10))], CREAM)
    t = max(0.0, min(1.0, (value - lo) / (hi - lo)))
    a = math.radians(a0 + (a1 - a0) * t)
    L.line([(x - math.cos(a) * 10, y - math.sin(a) * 10), (x + math.cos(a) * (r - 12), y + math.sin(a) * (r - 12))], needle_col, 3)
    L.d.ellipse([(x - 6) * S, (y - 6) * S, (x + 6) * S, (y + 6) * S], fill=(60, 56, 50))
    wy = y + r * 0.40
    L.rect(x - 34, wy - 13, x + 34, wy + 13, fill=(0, 0, 0, 255), outline=(80, 74, 66, 255), width=1)
    L.text((x - 2, wy), readout, m.font("barlow", 22), needle_col, "rm")
    L.text((x + 2, wy + 3), unit, m.font("barlow_med", 11), DIM, "lm")
    L.text((x, y + r * 0.80), title, m.font("barlow", 13), DIM, "mm", spacing=2)


def option_d():
    """Round gauges along the dash, like the instruments of a real lander or a classic car."""
    use(face("barlow"), lambda size, weight=500: m.font("barlow_med", size + 1))
    img = m.base()
    L = m.Layer()
    m.world_markers(L)
    m.crosshair(L)
    cx = W / 2
    r = 64
    y = 668
    dial(L, cx - 300, y, r, "SPEED", 212, 0, 300, 6, "212", "M/S")
    dial(L, cx - 150, y, r, "HEIGHT", 1.24, 0, 5, 5, "1.24", "KM")
    dial(L, cx + 150, y, r, "ORBIT", 1.1, 0, 4.8, 4, "1.1", "KM/S", needle_col=AMBER, notch=3.2)
    dial(L, cx + 300, y, r, "PULL", 3.7, 0, 12, 4, "3.7", "M/S²", warn_from=9.0)
    # Indicator lights between the dials: round lenses with the name printed under each.
    lights = [("BOOST", None), ("MATCH", None), ("LOCK", TARGET), ("AUTO", None), ("FALLING", AMBER), ("BRAKE", RED)]
    for i, (word, col) in enumerate(lights):
        col_i, row = i % 2, i // 2
        lx, ly = cx - 24 + col_i * 48, y - 46 + row * 46
        lit = col is not None
        L.d.ellipse([(lx - 13) * S, (ly - 13) * S, (lx + 13) * S, (ly + 13) * S], fill=(48, 44, 40, 240))
        L.d.ellipse([(lx - 10) * S, (ly - 10) * S, (lx + 10) * S, (ly + 10) * S],
                    fill=(col + (255,)) if lit else (30, 28, 26, 255))
        L.text((lx, ly + 20), word, m.font("barlow_med", 11), CREAM if lit else DIM, "mm", spacing=1)
    L.text((cx, y - r - 34), "BRAKE NOW: AIM AT PHOBOS AND HOLD F", m.font("barlow", 18), RED, "mm", spacing=1)
    L.text((cx, 34), "MARS", m.font("barlow", 42), AMBER, "mm", spacing=3)
    L.text((cx, 64), "1.24 KM ABOVE THE GROUND", m.font("barlow_med", 15), CREAM, "mm", spacing=2)
    m.ante_block(L, 34, 26, face("barlow"), m.font("barlow_med", 13))
    m.damage_block(L, W - 34, 30, face("barlow"), m.font("barlow_med", 13))
    m.keys_line(L)
    L.compose(img)
    return m.caption(img, "D_analog_dials", "D  Analog dials  (Barlow Condensed)")


# ------------------------------------------------------------------- E: big numerals
def option_e():
    """Almost nothing on screen but two huge numbers in the lower corners, magazine-cover style, and one word."""
    use(lambda size, *a, **k: m.font("antonio", size, [700]), lambda size, weight=500: m.font("plexmono_med", size))
    img = m.base()
    L = m.Layer()
    m.world_markers(L)
    m.crosshair(L)
    huge = m.font("antonio", 104, [700])
    small = m.font("plexmono", 13)
    # Speed, lower left.
    w = L.text((40, 700), "212", huge, CREAM, "ls")
    L.text((46 + w, 700 - 10), "M/S", small, DIM, "ls", spacing=2)
    L.text((44, 714), "SPEED  VS MARS GROUND", small, DIM, "lm", spacing=2)
    L.text((44, 732), "PULL 3.7  ·  WIND 14", small, DIM, "lm", spacing=2)
    # Height, lower right.
    w = L.text((W - 40, 700), "1.24", huge, CREAM, "rs")
    L.text((W - 46 - w, 700 - 10), "KM", small, DIM, "rs", spacing=2)
    L.text((W - 44, 714), "HEIGHT  ABOVE GROUND", small, DIM, "rm", spacing=2)
    L.text((W - 44, 732), "ORBIT 1.1 OF 3.2 KM/S", small, AMBER, "rm", spacing=2)
    # Orbit as one thin line right across the bottom, with the notch where a circle is.
    y = 752
    L.rect(40, y - 1, W - 40, y + 1, fill=(246, 234, 210, 60))
    L.rect(40, y - 2, 40 + (W - 80) * 1.1 / 4.8, y + 2, fill=AMBER + (255,))
    nx = 40 + (W - 80) * 3.2 / 4.8
    L.line([(nx, y - 8), (nx, y + 8)], CREAM, 2)
    # One word for the state that matters, big, under the crosshair, and small words for the rest.
    L.text((W / 2, 560), "BRAKE", m.font("antonio", 54, [700]), RED, "mm")
    L.text((W / 2, 596), "AIM AT PHOBOS, HOLD F", small, RED, "mm", spacing=2)
    L.text((W / 2, 626), "LOCK · FALLING", small, AMBER, "mm", spacing=3)
    L.text((W / 2, 40), "MARS", m.font("antonio", 44, [700]), AMBER, "mm")
    m.ante_block(L, 34, 26, lambda s: m.font("antonio", s, [700]), small)
    m.damage_block(L, W - 34, 30, lambda s: m.font("antonio", s, [700]), small)
    L.compose(img, halo=0.9, radius=4)
    return m.caption(img, "E_big_numerals", "E  Big numerals  (Antonio and IBM Plex Mono)")


# ------------------------------------------------------------------- F: lander panel with segment digits
SEG = {  # which of the seven segments a..g light for each digit
    "0": "abcdef", "1": "bc", "2": "abdeg", "3": "abcdg", "4": "bcfg", "5": "acdfg", "6": "acdefg",
    "7": "abc", "8": "abcdefg", "9": "abcdfg", "-": "g", " ": "", "+": "g",
}


def segments(L, x, y, text, h, on, off):
    """Seven-segment digits drawn as slanted bars, like an electroluminescent lander display. '.' sits on the last."""
    w = h * 0.56
    t = h * 0.15
    g = t * 0.35    # gap between bars
    sl = h * 0.08   # slant

    def hbar(a, b, yc):
        return [(a, yc), (a + t / 2, yc - t / 2), (b - t / 2, yc - t / 2), (b, yc), (b - t / 2, yc + t / 2), (a + t / 2, yc + t / 2)]

    def vbar(xc, a, b):
        return [(xc, a), (xc + t / 2, a + t / 2), (xc + t / 2, b - t / 2), (xc, b), (xc - t / 2, b - t / 2), (xc - t / 2, a + t / 2)]

    cx = x
    for ch in text:
        if ch == ".":
            r = t * 0.6
            px = cx - h * 0.1
            L.d.ellipse([(px - r) * S, (y + h - 2 * r) * S, (px + r) * S, (y + h) * S], fill=on)
            continue
        lit = SEG.get(ch, "")
        x0, x1 = cx + t / 2, cx + w - t / 2
        y0, ym, y1 = y + t / 2, y + h / 2, y + h - t / 2
        pts = {
            "a": hbar(x0 + g, x1 - g, y0), "g": hbar(x0 + g, x1 - g, ym), "d": hbar(x0 + g, x1 - g, y1),
            "f": vbar(x0, y0 + g, ym - g), "b": vbar(x1, y0 + g, ym - g),
            "e": vbar(x0, ym + g, y1 - g), "c": vbar(x1, ym + g, y1 - g),
        }
        for k, p in pts.items():
            col = on if k in lit else off
            if col[3] == 0:
                continue
            L.poly([(px + sl * (1 - (py - y) / h), py) for px, py in p], col)
        cx += w + h * 0.18
    return cx - x


def option_f():
    """A grey lander panel low in the view: segment digits in rows, and a block of labelled caution lights.
    In the spirit of the Apollo lander's own displays (our layout, not a copy)."""
    use(face("plexcond"), lambda size, weight=500: m.font("plexcond", size))
    green = (150, 255, 170, 255)
    ghost = (150, 255, 170, 12)
    img = m.base()
    L = m.Layer()
    m.world_markers(L)
    m.crosshair(L)
    x0, y0, x1, y1 = 290, 586, 990, 762
    L.rect(x0, y0, x1, y1, fill=(58, 56, 52, 245))
    L.rect(x0 + 3, y0 + 3, x1 - 3, y1 - 3, outline=(120, 114, 104, 255), width=1)
    for sx in (x0 + 10, x1 - 10):
        for sy in (y0 + 10, y1 - 10):   # four screw heads
            L.d.ellipse([(sx - 3) * S, (sy - 3) * S, (sx + 3) * S, (sy + 3) * S], fill=(140, 134, 124))
    lab = m.font("plexcond", 12)
    # Caution lights, two columns of three: frosted squares that light white or amber, the name printed on them.
    lights = [("BOOST", None), ("MATCH", None), ("LOCK", (235, 240, 245)), ("AUTO", None), ("FALLING", AMBER), ("BRAKE", RED)]
    for i, (word, col) in enumerate(lights):
        cx_, cy_ = x0 + 22 + (i % 2) * 86, y0 + 22 + (i // 2) * 44
        L.rect(cx_, cy_, cx_ + 78, cy_ + 34, fill=(col + (255,)) if col else (36, 35, 33, 255), outline=(20, 20, 18, 255), width=2)
        L.text((cx_ + 39, cy_ + 17), word, m.font("plexcond", 13), (25, 20, 15) if col else (120, 116, 108), "mm", spacing=1)
    # The digit window: black glass with three rows.
    wx0, wy0, wx1, wy1 = x0 + 200, y0 + 14, x1 - 16, y1 - 14
    L.rect(wx0, wy0, wx1, wy1, fill=(8, 10, 9, 255))
    rows = [("SPEED", "M/S", "  212"), ("HEIGHT", "KM", " 1.24"), ("ORBIT", "KM/S", "  1.1")]
    for i, (name, unit, digits) in enumerate(rows):
        ry = wy0 + 12 + i * 50
        L.rect(wx0 + 10, ry + 4, wx0 + 92, ry + 20, fill=(70, 160, 90, 255))
        L.text((wx0 + 51, ry + 12), name, lab, (8, 10, 9), "mm", spacing=1)
        L.text((wx0 + 51, ry + 32), unit, lab, (110, 190, 125), "mm", spacing=1)
        segments(L, wx0 + 120, ry, digits.replace(" ", "8"), 38, ghost, ghost)
        segments(L, wx0 + 120, ry, digits, 38, green, (0, 0, 0, 0))
    # Orbit's goal, small beside its row.
    L.text((wx1 - 12, wy0 + 12 + 2 * 50 + 19), "NEEDS 3.2", lab, (110, 190, 125), "rm", spacing=1)
    L.text((W / 2, y0 - 22), "BRAKE NOW: AIM AT PHOBOS AND HOLD F", m.font("plexcond", 17), RED, "mm", spacing=1)
    L.text((W / 2, 34), "MARS", m.font("plexcond", 38), AMBER, "mm", spacing=5)
    L.text((W / 2, 64), "1.24 KM ABOVE THE GROUND", m.font("plexcond", 15), CREAM, "mm", spacing=2)
    m.ante_block(L, 34, 26, face("plexcond"), lab)
    m.damage_block(L, W - 34, 30, face("plexcond"), lab)
    L.compose(img, halo=0.5)
    return m.caption(img, "F_lander_panel", "F  Lander panel, segment digits  (IBM Plex Sans Condensed)")


if __name__ == "__main__":
    for fn in (option_c, option_d, option_e, option_f):
        fn()
