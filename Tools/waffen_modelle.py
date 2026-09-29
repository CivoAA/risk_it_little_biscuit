"""
Zeichnet die Spielgrafik der Waffen, die bis jetzt nur Unitys weissen
Standard-Kreis hatten (Icons dazu: Tools/waffen_icons.py).

  Assets/Art/Waffen/Fx/vortex.png          8 x 64x64  PPU 64  Strudel, dreht sich (Frames, nicht Transform)
  Assets/Art/Waffen/Fx/crumb_trail.png     4 x 32x32  PPU 32  Kruemelhaufen mit Glitzer
  Assets/Art/Waffen/Fx/sticky_puddle.png   6 x 48x48  PPU 48  Marmeladenlache mit Scherben, blubbert
  Assets/Art/Waffen/Fx/sticky_jar.png      4 x 16x16  PPU 32  gesprungenes Glas, ueberschlaegt sich
  Assets/Art/Waffen/Fx/turret.png          2 x 24x24  PPU 32  Sockel mit Keks-Kuppel, Licht blinkt
  Assets/Art/Waffen/Fx/turret_barrel.png   2 x 16x8   PPU 32  Rohr (ruhig / Muendungsfeuer), Drehpunkt links
  Assets/Art/Waffen/Fx/turret_shot.png     2 x 10x6   PPU 32  Schokosplitter mit Schweif, zeigt nach rechts

Die Groessen folgen den Stats: der Kollider hat Radius 0.5 und das Objekt wird
mit range skaliert (Strudel 2-3, Kruemel ~1, Lache 1.6). Mit der gewaehlten
PPU landet ein Texel bei ueblichem range ungefaehr auf einem Bildschirmpixel.

.meta entsteht nur beim ersten Lauf - danach behalten die Sprites ihre IDs
und die Prefabs ihre Verweise.

Aufruf aus dem Projektordner:  python Tools/waffen_modelle.py [--preview pfad.png]
"""

import math
import os
import random
import sys

from PIL import Image

from unity_meta import write_strip_meta

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Waffen", "Fx")

CLEAR = (0, 0, 0, 0)


def hx(s, a=255):
    s = s.lstrip("#")
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), a)


def mix(c1, c2, t):
    return tuple(int(round(c1[i] + (c2[i] - c1[i]) * t)) for i in range(4))


# Paletten wie in waffen_icons.py
V1, V2, V3, V4, V5 = hx("#3e1a6e"), hx("#5a2a9a"), hx("#7b3fc6"), hx("#a067e6"), hx("#d0a8ff")
VW = hx("#f4ecff")
C_MID, C_DARK, C_LIGHT, C_LINE = hx("#d99a4e"), hx("#a86a2c"), hx("#f2c27e"), hx("#5a3218")
CHOC = hx("#4a2616")
M_MID, M_DARK, M_LIGHT, M_LINE = hx("#8a8f9c"), hx("#5a5e6b"), hx("#c4c8d2"), hx("#2a2a33")
J_OUT, J_DARK, J_MID, J_LIGHT, J_GLOSS = hx("#43186e"), hx("#6a2aa0"), hx("#9a5ad0"), hx("#b98ae6"), hx("#e6d2f7")
GLASS, GLASS_L, GLASS_D = hx("#9fe3f0"), hx("#e8fbff"), hx("#5bb8d2")
LID, LID_L, LID_D = hx("#5aa2ef"), hx("#a8dcff"), hx("#2f6fc8")
SHADOW = (0x1C, 0x14, 0x19, 90)
FIRE_Y, FIRE_O, FIRE_W = hx("#ffd23f"), hx("#f2873b"), hx("#fff6d0")


class Px:
    def __init__(self, w, h):
        self.img = Image.new("RGBA", (w, h), CLEAR)
        self.px = self.img.load()
        self.w, self.h = w, h

    def set(self, x, y, c):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.px[x, y] = c

    def get(self, x, y):
        if 0 <= x < self.w and 0 <= y < self.h:
            return self.px[x, y]
        return CLEAR

    def over(self, x, y, c):
        """Alpha-Blend - fuer halbtransparente Flaechen."""
        if not (0 <= x < self.w and 0 <= y < self.h):
            return
        b = self.px[x, y]
        a = c[3] / 255
        if b[3] == 0:
            self.px[x, y] = c
            return
        ra = a + b[3] / 255 * (1 - a)
        rgb = tuple(int(round((c[i] * a + b[i] * b[3] / 255 * (1 - a)) / ra)) for i in range(3))
        self.px[x, y] = rgb + (int(round(ra * 255)),)

    def disc(self, cx, cy, r, c):
        for y in range(int(cy - r - 1), int(cy + r + 2)):
            for x in range(int(cx - r - 1), int(cx + r + 2)):
                if (x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2 <= r * r:
                    self.set(x, y, c)

    def ellipse(self, cx, cy, rx, ry, c, blend=False):
        for y in range(int(cy - ry - 1), int(cy + ry + 2)):
            for x in range(int(cx - rx - 1), int(cx + rx + 2)):
                if ((x + 0.5 - cx) / rx) ** 2 + ((y + 0.5 - cy) / ry) ** 2 <= 1:
                    (self.over if blend else self.set)(x, y, c)

    def outline(self, col):
        """1px Kontur aussen um alles Deckende (4er-Nachbarschaft)."""
        solid = {(x, y) for y in range(self.h) for x in range(self.w) if self.px[x, y][3] > 0}
        for (x, y) in list(solid):
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                n = (x + dx, y + dy)
                if n not in solid and 0 <= n[0] < self.w and 0 <= n[1] < self.h:
                    self.px[n] = col

    def stamp(self, rows, pal, ox, oy):
        for j, row in enumerate(rows):
            for i, ch in enumerate(row):
                if ch != ".":
                    self.set(ox + i, oy + j, pal[ch])


# ----------------------------------------------------------------------------
#  Strudel
# ----------------------------------------------------------------------------

def vortex_frame(f, frames=8):
    S = 64
    c = Px(S, S)
    cx = cy = S / 2
    arms = 3
    phase = f / frames * (2 * math.pi / arms)     # nach 8 Frames deckungsgleich
    R = 30.5
    for y in range(S):
        for x in range(S):
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            r = math.hypot(dx, dy)
            if r > R:
                continue
            t = r / R
            th = math.atan2(dy, dx)
            # logarithmische Spirale: Arme laufen nach innen zusammen
            v = (th * arms + math.log(max(r, 1)) * 4.2 + phase * arms) % (2 * math.pi)
            arm = math.cos(v)
            if t < 0.14:
                col = VW
            elif t < 0.24:
                col = V5
            elif arm > 0.55:
                col = V5 if t < 0.45 else (V4 if t < 0.75 else V3)
            elif arm > 0.05:
                col = V4 if t < 0.45 else (V3 if t < 0.75 else V2)
            elif arm > -0.5:
                col = V2 if t < 0.8 else V1
            else:
                col = V1
            # aussen durchsichtiger - der Boden scheint durch, der Rand franst aus
            alpha = 235 if t < 0.7 else (200 if t < 0.88 else 140)
            if t > 0.88 and arm < 0.05:
                continue
            c.set(x, y, col[:3] + (alpha,))
    # Truemmer, die mitkreiseln
    rng = random.Random(7)
    bits = [(rng.uniform(14, 28), rng.uniform(0, 2 * math.pi)) for _ in range(7)]
    for i, (rr, a0) in enumerate(bits):
        a = a0 + phase * 1.0 + f * 0.05
        # innen schneller - wie Wasser im Abfluss
        a += f / frames * (2 * math.pi / arms) * (28 / rr - 1) * 0
        x, y = int(cx + math.cos(a) * rr), int(cy + math.sin(a) * rr)
        c.set(x, y, VW if i % 2 else V5)
        if i % 3 == 0:
            c.set(x + 1, y, V5)
    return c.img


# ----------------------------------------------------------------------------
#  Kruemel
# ----------------------------------------------------------------------------

CRUMBS = [  # x, y, Form - eckige Brocken, kein Kreis sieht wie ein Kruemel aus
    (10, 11, "big"), (19, 9, "wedge"), (15, 18, "chunk"), (23, 17, "small"),
    (8, 20, "small2"), (20, 23, "wedge2"), (26, 11, "tiny"), (11, 26, "tiny"), (4, 14, "tiny"),
]
CRUMB_SHAPES = {
    "big":    ["..LLL.", ".LMMMx", "LMMcMx", "LMMMxx", ".xxx.."],
    "chunk":  [".LLL..", "LMMMM.", "LMcMMx", "MMMMxx", ".Mxx.."],
    "wedge":  ["LL...", "LMM..", "LMcM.", ".MMxx"],
    "wedge2": ["...LL", "..LMx", ".LMMx", "LMxx."],
    "small":  ["LL.", "LMx", ".xx"],
    "small2": [".L", "LM", "Mx"],
    "tiny":   ["Lx"],
}


def crumb_frame(f):
    c = Px(32, 32)
    # weicher Schatten unter dem Haufen
    c.ellipse(16, 19, 12, 7, (0x1C, 0x14, 0x19, 60), blend=True)
    pal = {"L": C_LIGHT, "M": C_MID, "x": C_DARK, "c": CHOC}
    top = Px(32, 32)
    for (x, y, k) in CRUMBS:
        rows = CRUMB_SHAPES[k]
        top.stamp(rows, pal, x - len(rows[0]) // 2, y - len(rows) // 2)
    top.outline(C_LINE)
    for y in range(32):
        for x in range(32):
            p = top.px[x, y]
            if p[3]:
                c.set(x, y, p)
    # Glitzer wandert von Kruemel zu Kruemel
    gx, gy, _ = CRUMBS[[0, 2, 1, 5][f]]
    gy -= 1
    for dx, dy, col in ((0, -1, FIRE_W), (-1, -1, C_LIGHT), (1, -1, C_LIGHT), (0, -2, C_LIGHT)):
        c.set(gx + dx - 1, gy + dy - 1, col)
    return c.img


# ----------------------------------------------------------------------------
#  Marmeladenlache (Sticky Shatter)
# ----------------------------------------------------------------------------

def puddle_shape(S):
    rng = random.Random(3)
    blobs = [(S / 2, S / 2, 15)]
    for i in range(9):
        a = i / 9 * 2 * math.pi + rng.uniform(-0.2, 0.2)
        d = rng.uniform(9, 14)
        blobs.append((S / 2 + math.cos(a) * d, S / 2 + math.sin(a) * d, rng.uniform(4.5, 7)))
    drops = []
    for i in range(6):
        a = rng.uniform(0, 2 * math.pi)
        d = rng.uniform(19, 22)
        drops.append((S / 2 + math.cos(a) * d, S / 2 + math.sin(a) * d, rng.uniform(1.2, 2.2)))
    return blobs + drops


def puddle_frame(f, frames=6):
    S = 48
    c = Px(S, S)
    shape = puddle_shape(S)
    inside = set()
    for y in range(S):
        for x in range(S):
            for (bx, by, br) in shape:
                if (x + 0.5 - bx) ** 2 + (y + 0.5 - by) ** 2 <= br * br:
                    inside.add((x, y))
                    break
    for (x, y) in inside:
        c.set(x, y, J_MID[:3] + (215,))
    # Tiefe: unten rechts dunkler, oben links Glanz
    for (x, y) in inside:
        if (x + 1, y + 1) not in inside or (x + 2, y + 2) not in inside:
            c.set(x, y, J_DARK[:3] + (230,))
    for (x, y) in inside:
        if (x - 1, y - 1) not in inside and (x + 1, y + 1) in inside:
            c.set(x, y, J_LIGHT[:3] + (230,))
    # Fruchtstueckchen wie in der alten Lache
    rng = random.Random(11)
    for _ in range(14):
        x, y = rng.randint(10, 37), rng.randint(10, 37)
        if (x, y) in inside and (x + 1, y + 1) in inside:
            c.set(x, y, J_DARK[:3] + (235,))
            c.set(x + 1, y, J_DARK[:3] + (235,))
    # Kontur
    for (x, y) in inside:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (x + dx, y + dy)
            if n not in inside:
                c.set(n[0], n[1], J_OUT[:3] + (235,))
    # Glanzstreifen
    for (x, y) in ((17, 16), (18, 16), (19, 16), (16, 17), (16, 18)):
        c.set(x, y, J_GLOSS)
    # Glasscherben
    shards = [((12, 29), ["gG", ".d"]), ((31, 13), [".G", "gd"]), ((30, 31), ["Gg", "gd", ".d"]),
              ((20, 24), ["G", "d"])]
    for (sx, sy), rows in shards:
        c.stamp(rows, {"G": GLASS_L, "g": GLASS, "d": GLASS_D}, sx, sy)
    # Blasen: wachsen und platzen, versetzt
    bubbles = [(24, 20), (15, 30), (33, 24)]
    for i, (bx, by) in enumerate(bubbles):
        s = (f + i * 2) % frames
        if s == 0:
            c.set(bx, by, J_LIGHT)
        elif s == 1:
            c.stamp([".l.", "lgl", ".l."], {"l": J_LIGHT, "g": J_GLOSS}, bx - 1, by - 1)
        elif s == 2:
            c.stamp([".ll.", "lg.l", "l..l", ".ll."], {"l": J_LIGHT, "g": J_GLOSS}, bx - 1, by - 2)
        elif s == 3:
            c.stamp(["g..g", "....", "g..g"], {"g": J_GLOSS}, bx - 1, by - 1)
    return c.img


# ----------------------------------------------------------------------------
#  Fliegendes Glas
# ----------------------------------------------------------------------------

JAR = [
    "....bBBBBb......",
    "...bLLLLLLb.....",
    "...bllllllb.....",
    "....oooooo......",
    "...O......O.....",
    "..OgJJJJJJgO....",
    "..OgJmmJJJJO....",
    "..OgJmJJkJJO....",
    "..OJJJJkJJJO....",
    "..OJJJkJJmJO....",
    "..OJJJJkJJJO....",
    "..OJJJJJJJJO....",
    "...OOOOOOOO.....",
]
JAR_PAL = {"b": LID_D, "B": LID, "L": LID_L, "l": LID, "o": hx("#9a5a2e"), "O": J_OUT,
           "g": GLASS_L, "J": J_DARK, "m": J_MID, "k": GLASS_L}


def jar_frame(f):
    c = Px(16, 16)
    c.stamp(JAR, JAR_PAL, 1, 1)
    # Glas ueberschlaegt sich: exakte Vierteldrehungen, bleibt pixelgenau
    return c.img.rotate(-90 * f)


# ----------------------------------------------------------------------------
#  Geschuetzturm
# ----------------------------------------------------------------------------

def turret_frame(f):
    c = Px(24, 24)
    c.ellipse(12, 20.5, 10, 3, SHADOW, blend=True)
    # Sockel
    for y in range(16, 21):
        for x in range(3, 21):
            c.set(x, y, M_MID)
    for x in range(3, 21):
        c.set(x, 16, M_LIGHT)
        c.set(x, 20, M_DARK)
    for x in (5, 9, 14, 18):
        c.set(x, 18, M_DARK)                 # Nieten
    # Keks-Kuppel
    for y in range(5, 17):
        for x in range(4, 20):
            dx, dy = (x + 0.5 - 12) / 8, (y + 0.5 - 16) / 11
            if dx * dx + dy * dy <= 1:
                c.set(x, y, C_MID)
                if dx < -0.3 and dy < -0.2:
                    c.set(x, y, C_LIGHT)
                elif dx > 0.45:
                    c.set(x, y, C_DARK)
    for (x, y) in ((8, 10), (14, 8), (15, 13), (10, 14)):
        c.set(x, y, CHOC)
    # Sichtschlitz mit blinkendem Licht
    for x in range(9, 15):
        c.set(x, 11, C_LINE)
    c.set(13 if f == 0 else 10, 11, hx("#ff5a4a") if f == 0 else hx("#ffb0a0"))
    c.outline(M_LINE)
    return c.img


def barrel_frame(f):
    c = Px(16, 8)
    # Rohr: Drehpunkt bei x=2 (Kuppelmitte), Muendung rechts
    for y in range(3, 6):
        for x in range(2, 12):
            c.set(x, y, M_MID)
    for x in range(2, 12):
        c.set(x, 3, M_LIGHT)
        c.set(x, 5, M_DARK)
    for y in range(2, 7):
        c.set(11, y, M_MID)
        c.set(12, y, M_DARK)
    c.set(11, 2, M_LIGHT)
    c.outline(M_LINE)
    if f == 1:
        c.stamp([".w..", "wyo.", "yyyo", "wyo.", ".w.."], {"w": FIRE_W, "y": FIRE_Y, "o": FIRE_O}, 13, 2)
        c.set(15, 4, FIRE_O)
    return c.img


def shot_frame(f):
    c = Px(10, 6)
    # Schweif
    for x in range(0, 5):
        if (x + f) % 2 == 0 or x > 2:
            c.set(x, 3 if x < 3 else 2 + (x % 2), C_LIGHT[:3] + (120 + x * 25,))
    # Schokosplitter
    c.stamp([".cc.", "cCCc", "cCCc", ".cc."], {"c": CHOC, "C": hx("#7a4428")}, 5, 1)
    c.set(6, 2, hx("#b07048"))
    return c.img


# ----------------------------------------------------------------------------
#  Ausgabe
# ----------------------------------------------------------------------------

SHEETS = [
    # Name, Framefunktion, Frames, Breite, Hoehe, PPU, Pivot (0..1)
    ("vortex",        vortex_frame,  8, 64, 64, 64, (0.5, 0.5)),
    ("crumb_trail",   crumb_frame,   4, 32, 32, 32, (0.5, 0.5)),
    ("sticky_puddle", puddle_frame,  6, 48, 48, 48, (0.5, 0.5)),
    ("sticky_jar",    jar_frame,     4, 16, 16, 32, (0.5, 0.5)),
    ("turret",        turret_frame,  2, 24, 24, 32, (0.5, 0.5)),
    ("turret_barrel", barrel_frame,  2, 16, 8,  32, (2.5 / 16, 0.5)),
    ("turret_shot",   shot_frame,    2, 10, 6,  32, (0.7, 0.5)),
]


def main():
    preview = sys.argv[sys.argv.index("--preview") + 1] if "--preview" in sys.argv else None
    os.makedirs(OUT, exist_ok=True)
    sheets = []
    for name, fn, frames, w, h, ppu, pivot in SHEETS:
        img = Image.new("RGBA", (w * frames, h), CLEAR)
        for f in range(frames):
            img.paste(fn(f), (f * w, 0))
        path = os.path.join(OUT, name + ".png")
        img.save(path)
        if not os.path.exists(path + ".meta"):
            write_strip_meta(path + ".meta", name, frames, w, h, ppu, pivot)
        sheets.append(img)
        print("geschrieben:", os.path.relpath(path, ROOT))

    if preview:
        W = max(s.width for s in sheets) + 8
        H = sum(s.height + 6 for s in sheets) + 6
        pv = Image.new("RGBA", (W, H), (74, 96, 64, 255))
        y = 6
        for s in sheets:
            pv.paste(s, (4, y), s)
            y += s.height + 6
        pv.resize((W * 3, H * 3), Image.NEAREST).save(preview)


if __name__ == "__main__":
    main()
