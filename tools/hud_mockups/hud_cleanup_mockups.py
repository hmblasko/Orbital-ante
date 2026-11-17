"""Offline mockups for the HUD clean-up (17 Nov 2026) at the Steam Deck's 1280x800. Not game footage.

Draws four pictures: the flight view as it is now, the flight view after the clean-up, on foot outside after the
clean-up, and the new Run Stats page in the pause menu. Drawn at 2x and scaled down so text sizes match the Deck.

    python3 tools/hud_mockups/hud_cleanup_mockups.py <backgrounds folder> <out folder>

The backgrounds folder needs front-window-seat.png and ground_mars_on_foot.jpg (both in the project files).
"""
import math
import os
import sys
from PIL import Image, ImageDraw, ImageFont, ImageFilter

S = 2
W, H = 1280, 800
HERE = os.path.dirname(os.path.abspath(__file__))
FONTS = os.path.join(HERE, "..", "..", "unity", "Assets", "OrbitalAnte", "Resources", "Fonts")
BG = sys.argv[1] if len(sys.argv) > 1 else "."
OUT = sys.argv[2] if len(sys.argv) > 2 else "."

# The game's colours (UiKit).
CREAM = (246, 232, 204)
MUTED = (184, 168, 148)
AMBER = (237, 135, 41)
DANGER = (219, 64, 51)
O2 = (150, 215, 245)
FOCUS = (120, 210, 240)
PANEL = (48, 41, 36)
INK = (33, 28, 26)


def font(bold, size):
    return ImageFont.truetype(os.path.join(FONTS, "B612-Bold.ttf" if bold else "B612-Regular.ttf"), int(size * S))


def background(name, crop_wide=False):
    img = Image.open(os.path.join(BG, name)).convert("RGBA")
    if crop_wide:
        w, h = img.size
        h -= 40  # and leave out their own caption along the bottom
        cw = int(h * W / H)
        # The landscape pictures are before/after pairs: take the right-hand (after) half, clear of its caption.
        x = w - cw
        img = img.crop((x - 20, 0, x - 20 + cw, h))
    return img.resize((W * S, H * S), Image.LANCZOS)


class Layer:
    """Shapes and words, with the soft dark halo every HUD label gets in the game (UiKit.Halo)."""

    def __init__(self):
        self.img = Image.new("RGBA", (W * S, H * S), (0, 0, 0, 0))
        self.d = ImageDraw.Draw(self.img)

    def text(self, xy, s, f, fill, anchor="la", spacing=0.0):
        x, y = xy
        if spacing <= 0:
            self.d.text((x * S, y * S), s, font=f, fill=fill, anchor=anchor)
            return self.d.textlength(s, font=f) / S
        # Letter-spaced small capitals for labels.
        total = sum(self.d.textlength(c, font=f) / S for c in s) + spacing * (len(s) - 1)
        if anchor[0] == "r":
            x -= total
        elif anchor[0] == "m":
            x -= total / 2
        a = "l" + anchor[1]
        for c in s:
            self.d.text((x * S, y * S), c, font=f, fill=fill, anchor=a)
            x += self.d.textlength(c, font=f) / S + spacing
        return total

    def width(self, s, f):
        return self.d.textlength(s, font=f) / S

    def rect(self, x0, y0, x1, y1, fill, r=0):
        if r > 0:
            self.d.rounded_rectangle([x0 * S, y0 * S, x1 * S, y1 * S], radius=r * S, fill=fill)
        else:
            self.d.rectangle([x0 * S, y0 * S, x1 * S, y1 * S], fill=fill)

    def compose(self, onto, halo=0.75, radius=3):
        alpha = self.img.split()[3]
        shadow = Image.new("RGBA", self.img.size, (8, 6, 5, 0))
        a = alpha.filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.GaussianBlur(radius * S))
        a = a.point(lambda v: int(min(255, v * 1.8) * halo))
        shadow.putalpha(a)
        onto.alpha_composite(shadow)
        onto.alpha_composite(self.img)


def save(img, name, title):
    img = img.convert("RGB").resize((W, H), Image.LANCZOS)
    d = ImageDraw.Draw(img)
    cap = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 13)
    d.rectangle([0, H - 20, W, H], fill=(0, 0, 0))
    d.text((8, H - 17), title + "   |   offline mockup at 1280x800, not game footage", font=cap, fill=(255, 255, 255))
    img.save(os.path.join(OUT, name + ".png"))


def aim_cross(L):
    for i in range(4):
        a = math.radians(90 * i)
        cx, cy = W / 2 + 13 * math.cos(a), H / 2 + 13 * math.sin(a)
        if i % 2 == 0:
            L.rect(cx - 5, cy - 1, cx + 5, cy + 1, CREAM + (205,))
        else:
            L.rect(cx - 1, cy - 5, cx + 1, cy + 5, CREAM + (205,))


def body_marker(L, x, y, name, dist, aimed=False, small=False):
    f = font(False, 14 if small else 16)
    col = (115, 217, 255) if aimed else (MUTED if small else CREAM)
    L.d.ellipse([(x - 3) * S, (y - 3) * S, (x + 3) * S, (y + 3) * S], fill=col + (255,))
    label = name + "  " + dist
    if aimed:
        label = "[ " + label + " ]"
    L.text((x, y + 16), label, f, col, "mm")


# ------------------------------------------------------------------ before: the flight view today
def before_flight():
    img = background("front-window-seat.png")
    L = Layer()
    aim_cross(L)
    body_marker(L, 820, 250, "Phobos", "8.1 km", aimed=True)
    body_marker(L, 300, 180, "Deimos", "21 km", small=True)
    body_marker(L, 1000, 120, "Jupiter", "1,904 km")
    # The F3 readout, on by default.
    f = font(True, 18)
    lines = [
        "16.9 ms avg   31.2 ms worst   59 fps",
        "Seed 482913   gibs 12/60   blood 140/600   splats 22/64 (projectors)",
        "Budget 16.7 ms: TIGHT   1% worst 29.4 ms   11% of frames over",
        "Picture finish: on   Far copies 6 of 9 bodies",
        "Anchored to Mars (turning with it)   bodies 38   recentres 12   time 3412 s",
        "Ground ready 3/38  pictures 9/38  work waiting 0   Mars: tiles 96 (solid 12)  things 410",
    ]
    for i, s in enumerate(lines):
        L.text((14, 10 + i * 22), s, f, CREAM)
    L.text((W / 2, 196), "MARS   1.24 km above the ground", font(True, 26), AMBER, "mm")
    f = font(True, 22)
    readout = [
        "Speed  212 m/s   against Mars's ground",
        "Pull   3.7 m/s²",
        "Orbit  a circle here needs 3.2 km/s across   you: 1.1 km/s",
        "This path comes down to the ground: go faster across to stay up",
        "LOCKED ON Phobos   8.1 km   closing 230 m/s",
        "Wind   14 m/s   it carries you sideways",
    ]
    for i, s in enumerate(readout):
        L.text((22, 600 + i * 26), s, f, CREAM, "ls")
    L.text((W / 2, 550), "BRAKE NOW:  aim at Phobos and hold Match Velocity", font(True, 24), DANGER, "mm")
    L.text((22, 768), "Thrusters: W A S D, Space up, Ctrl down     Turn the ship: hold RMB, roll Q / E     Boost: Shift     "
           "Match velocity: hold F     Lock: T     Autopilot: G     Map: M", font(False, 16), MUTED, "ls")
    # Ship damage list, top right.
    for i, s in enumerate(["Thruster 2: 54%", "Front glass: 81%", "Leg 1: BROKEN"]):
        L.text((W - 22, 112 + i * 24), s, font(True, 18), CREAM, "ra")
    L.compose(img)
    save(img, "hud_cleanup_1_before_flight", "BEFORE  the seat view today (debug readout on by default, help line always on)")


# ------------------------------------------------------------------ after: the flight view
def label(L, xy, s, col=MUTED, anchor="la", size=13):
    return L.text(xy, s, font(True, size), col, anchor, spacing=1.6)


def after_flight():
    img = background("front-window-seat.png")
    L = Layer()
    aim_cross(L)
    body_marker(L, 820, 250, "Phobos", "8.1 km", aimed=True)
    body_marker(L, 300, 180, "Deimos", "21 km", small=True)
    body_marker(L, 1000, 120, "Jupiter", "1,904 km")

    # Where: top centre, name over height.
    label(L, (W / 2, 22), "MARS", MUTED, "mt", 14)
    L.text((W / 2, 42), "1.24 km", font(True, 30), CREAM, "mt")
    L.text((W / 2, 80), "above the ground", font(False, 14), MUTED, "mt")

    # Speed: lower left.
    x, y = 40, 586
    label(L, (x, y), "SPEED")
    w = L.text((x, y + 18), "212", font(True, 44), CREAM)
    L.text((x + w + 6, y + 48), "m/s", font(True, 18), MUTED, "ls")
    L.text((x, y + 74), "against Mars's ground", font(False, 15), MUTED)
    L.text((x, y + 96), "down 4.2 m/s", font(False, 15), CREAM)
    # Orbit aid: a bar across with a notch where a circle would be.
    oy = y + 124
    label(L, (x, oy), "ORBIT")
    bx, bw = x + 64, 200
    L.rect(bx, oy + 3, bx + bw, oy + 9, (0, 0, 0, 140), 3)
    circle_at = 0.62
    you = 0.62 * 1.1 / 3.2
    L.rect(bx, oy + 3, bx + bw * you, oy + 9, AMBER + (255,), 3)
    L.rect(bx + bw * circle_at - 1, oy - 2, bx + bw * circle_at + 1, oy + 14, CREAM + (255,))
    L.text((x, oy + 22), "comes down: go faster across", font(False, 15), AMBER)
    L.text((x, oy + 44), "Wind 14 m/s carries you sideways", font(False, 14), MUTED)

    # Target: lower right.
    rx = W - 40
    ty = 610
    tagw = L.width("LOCKED", font(True, 12)) + 18
    L.rect(rx - tagw, ty - 2, rx, ty + 16, (115, 217, 255, 235), 4)
    L.text((rx - tagw / 2, ty + 7), "LOCKED", font(True, 12), INK, "mm")
    L.text((rx, ty + 26), "Phobos", font(True, 20), (115, 217, 255), "ra")
    w = L.text((rx, ty + 86), "km", font(True, 18), MUTED, "rs")
    L.text((rx - w - 6, ty + 90), "8.1", font(True, 40), CREAM, "rs")
    L.text((rx, ty + 100), "closing 230 m/s", font(False, 15), DANGER, "ra")

    # The one notice.
    L.text((W / 2, 550), "BRAKE NOW:  aim at Phobos and hold Match Velocity (F)", font(True, 22), DANGER, "mm")

    # Ship damage: a small panel of bars, only while something is hurt.
    sx, sy = W - 40, 120
    label(L, (sx, sy), "SHIP", MUTED, "ra")
    rows = [("Thruster 2", 0.54, None), ("Front glass", 0.81, None), ("Leg 1", 0.0, "BROKEN")]
    for i, (name, f, tag) in enumerate(rows):
        yy = sy + 22 + i * 22
        L.text((sx - 96, yy), name, font(False, 15), CREAM, "ra")
        if tag:
            L.text((sx, yy), tag, font(True, 13), DANGER, "ra", spacing=1.0)
        else:
            L.rect(sx - 84, yy + 5, sx, yy + 11, (0, 0, 0, 140), 3)
            col = AMBER if f < 0.6 else CREAM
            L.rect(sx - 84, yy + 5, sx - 84 + 84 * f, yy + 11, col + (255,), 3)
    L.compose(img)
    save(img, "hud_cleanup_2_after_flight", "AFTER  the seat view: four groups, debug and key lists moved to the pause menu")


# ------------------------------------------------------------------ after: on foot outside, in a fight
def visor_shade(img):
    v = Image.new("L", img.size, 0)
    vd = ImageDraw.Draw(v)
    vd.ellipse([-260 * S, -420 * S, (W + 260) * S, (H + 70) * S], fill=255)
    v = v.filter(ImageFilter.GaussianBlur(40 * S))
    shade = Image.new("RGBA", img.size, (10, 8, 8, 255))
    shade.putalpha(v.point(lambda x: int((255 - x) * 0.55)))
    img.alpha_composite(shade)


def after_on_foot():
    img = background("ground_mars_on_foot.jpg", crop_wide=True)
    visor_shade(img)
    L = Layer()
    d = L.d
    c = (W / 2 * S, H / 2 * S)
    d.ellipse([c[0] - 4 * S, c[1] - 4 * S, c[0] + 4 * S, c[1] + 4 * S], fill=(0, 0, 0, 170))
    d.ellipse([c[0] - 2.6 * S, c[1] - 2.6 * S, c[0] + 2.6 * S, c[1] + 2.6 * S], fill=CREAM + (255,))
    R, WID, SPAN = 74, 9, 150

    def arc(cx, cy, r, w, a0, a1, fill):
        d.arc([(cx - r) * S, (cy - r) * S, (cx + r) * S, (cy + r) * S], a0, a1, fill=fill, width=int(w * S))

    # Air, lower left (unchanged from option A).
    cx, cy = 230, 690
    air = 0.38
    arc(cx, cy, R, WID, 105, 105 + SPAN, (0, 0, 0, 130))
    arc(cx, cy, R, WID, 105, 105 + SPAN * air, O2 + (255,))
    for i in range(11):
        a = math.radians(105 + SPAN * i / 10)
        k = 15 if i % 5 == 0 else 11
        d.line([(cx + (R + 7) * math.cos(a)) * S, (cy + (R + 7) * math.sin(a)) * S,
                (cx + (R + k) * math.cos(a)) * S, (cy + (R + k) * math.sin(a)) * S], fill=CREAM + (210,), width=2 * S)
    L.text((cx - 38, cy - 52), "AIR", font(True, 15), O2)
    w = L.text((cx - 38, cy - 32), "38", font(True, 40), CREAM)
    L.text((cx - 38 + w + 3, cy - 4), "%", font(True, 20), MUTED, "ls")
    L.text((cx - 38, cy + 18), "4:12 left", font(False, 17), MUTED)
    # Jets bar and speed, as now.
    L.rect(cx - 176 + 54 + 26 - 9, H - 26 - 86 + 5, cx - 176 + 54 + 26 + 9, H - 26 - 10, (0, 0, 0, 140), 4)
    L.text((406, 706), "2.4 m/s against Mars's ground", font(True, 18), CREAM)
    L.text((406, 734), "TETHER OFF  SHIP 140 m", font(False, 15), AMBER)

    # Suit plates, moved in to make room for the gun.
    cx2 = W - 380
    lit_n = 6
    for i in range(10):
        a1 = 75 - SPAN * i / 10
        a0 = a1 - SPAN / 10 + 2.2
        col = (CREAM + (255,)) if i < lit_n else (0, 0, 0, 130)
        if i == lit_n - 1:
            col = AMBER + (255,)
        arc(cx2, cy, R, WID + 3, a0 % 360, a1 % 360, col)
    L.text((cx2 + 38, cy - 52), "SUIT", font(True, 15), CREAM, "ra")
    L.text((cx2 + 38, cy - 32), "62", font(True, 40), CREAM, "ra")
    L.text((cx2 + 38, cy + 18), "6 of 10 plates", font(False, 17), MUTED, "ra")

    # Gun, lower right: name, the ladder as pips, then the magazine.
    rx = W - 32
    gy = 632
    L.text((rx, gy), "SNIPER", font(True, 18), CREAM, "ra")
    L.text((rx, gy - 22), "+2 guns  [1 / D-Pad Up]", font(True, 13), MUTED, "ra")
    pips, filled = 6, 4
    py = gy + 30
    nx = L.text((rx, py - 6), "SMG", font(True, 12), MUTED, "ra", spacing=1.2)
    L.text((rx - nx - 6, py - 6), "›", font(True, 14), MUTED, "ra")
    px = rx - nx - 24
    for i in range(pips):
        x0 = px - (pips - i) * 11
        col = CREAM + (255,) if i < filled else (0, 0, 0, 150)
        L.rect(x0, py - 4, x0 + 8, py + 4, col, 2)
        if i >= filled:
            d.rounded_rectangle([x0 * S, (py - 4) * S, (x0 + 8) * S, (py + 4) * S], radius=2 * S, outline=MUTED + (200,), width=S)
    w = L.text((rx, H - 34), "4", font(True, 46), CREAM, "rs")
    L.text((rx - w - 8, H - 38), "/ 5", font(True, 20), MUTED, "rs")

    # Focus, bottom centre.
    fy = H - 118
    L.rect(W / 2 - 100, fy - 6, W / 2 + 100, fy + 6, (0, 0, 0, 140), 6)
    L.rect(W / 2 - 98, fy - 4, W / 2 - 98 + 196 * 0.64, fy + 4, FOCUS + (255,), 4)
    L.text((W / 2 - 110, fy), "FOCUS", font(True, 13), MUTED, "rm", spacing=1.4)
    L.text((W / 2, 448), "E   pick up  Shotgun", font(False, 17), CREAM, "mm")
    L.compose(img)
    save(img, "hud_cleanup_3_after_on_foot", "AFTER  on foot outside: the gun and its ladder join the lower right; nothing else changes")


# ------------------------------------------------------------------ the pause menu's Run Stats page
def stats_page():
    img = background("ground_mars_on_foot.jpg", crop_wide=True)
    dim = Image.new("RGBA", img.size, (13, 10, 10, 184))
    img.alpha_composite(dim)
    L = Layer()
    cw, ch = 1140, 690
    x0, y0 = (W - cw) / 2, 40
    L.rect(x0, y0, x0 + cw, y0 + ch, AMBER + (255,), 16)
    L.rect(x0 + 5, y0 + 5, x0 + cw - 5, y0 + ch - 5, PANEL + (247,), 14)
    L.text((W / 2, y0 + 44), "RUN STATS", font(True, 40), AMBER, "mm")

    cols = [
        ("THIS RUN", [("Time this run", "12:41"), ("Kills", "9"), ("Gun", "3 of 6  Sniper"),
                      ("Next gun", "2 kills to SMG"), ("Guns carried", "Pistol +1"), ("Focus", "64%"),
                      ("Seed", "482913")]),
        ("SHIP", [("Fuel", "72%"), ("Air", "88%"), ("Thrust", "100%"), ("Turning", "92%"),
                  ("Thruster 2", "54%"), ("Front glass", "81%"), ("Leg 1", "BROKEN")]),
        ("SUIT AND FLIGHT", [("Suit air", "38%  4:12 left"), ("Jet fuel", "61%"), ("Armour", "6 of 10 plates"),
                             ("Near", "Mars"), ("Height", "1.24 km"), ("Speed", "212 m/s"), ("Pull", "3.7 m/s²")]),
    ]
    colw = (cw - 80) / 3
    for c, (title, rows) in enumerate(cols):
        cx = x0 + 40 + c * colw
        label(L, (cx, y0 + 96), title, AMBER, "la", 14)
        L.rect(cx, y0 + 118, cx + colw - 30, y0 + 119, MUTED + (90,))
        for i, (k, v) in enumerate(rows):
            yy = y0 + 132 + i * 34
            L.text((cx, yy), k, font(False, 17), MUTED)
            col = DANGER if v == "BROKEN" else CREAM
            L.text((cx + colw - 30, yy), v, font(True, 18), col, "ra")

    # Buttons.
    by = y0 + ch - 128
    bw = (cw - 100) / 2
    L.text((W / 2, y0 + 420), "Performance numbers (fps, frame times, the Deck budget) are under Technical details.", font(False, 15), MUTED, "mm")
    for i, s in enumerate(["Performance overlay: OFF", "Technical details"]):
        bx = x0 + 40 + i * (bw + 20)
        L.rect(bx, by, bx + bw, by + 50, (84, 71, 61, 255), 12)
        L.text((bx + bw / 2, by + 25), s, font(True, 22), CREAM, "mm")
    L.rect(x0 + 40, by + 62, x0 + cw - 40, by + 112, AMBER + (255,), 12)
    L.text((W / 2, by + 87), "Back", font(True, 22), CREAM, "mm")
    img.alpha_composite(L.img)
    save(img, "hud_cleanup_4_pause_run_stats", "AFTER  pause menu > Run Stats: everything that left the screen, in one place")


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    before_flight()
    after_flight()
    after_on_foot()
    stats_page()
