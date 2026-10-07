"""
Zeichnet die Kuechen-Hindernisse fuer World1 (Szene Map_World0) - Dinge, die
fuer einen Keks riesig sind und im Weg stehen.

  Assets/Art/World-Objects/Kueche/kueche_<name>.png   1 Sprite, PPU 32

Stilregeln wie bei Baeumen und Figuren:
  - eine warme, dunkle Kontur um alles (aus der Nachbarfarbe abgedunkelt), kein Schwarz
  - Licht oben links, wenige flaechige Toene pro Material, ein klarer Glanz
  - weicher Bodenschatten, damit das Ding auf den Fliesen steht

Der Pivot sitzt in der Mitte des Bodenschattens (Fusspunkt). Die Prefabs
sortieren per SpriteSortPoint = Pivot, so laeuft der Keks korrekt davor
und dahinter. Der dritte Rueckgabewert jeder Prop-Funktion ist die Kollisionsflaeche am
Fuss in Pixeln (Breite, Hoehe); sie landet in kueche_props.json, daraus baut
KuecheBuilder (Tools -> Welt -> Kueche einrichten) den BoxCollider2D.

Aufruf aus dem Projektordner:  python Tools/kueche_props.py [--preview pfad.png]
Die .meta wird nur beim ersten Mal geschrieben (sonst verlieren Prefabs ihre Verweise).
"""

import json
import math
import os
import sys

from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from unity_meta import write_strip_meta  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "Assets", "Art", "World-Objects", "Kueche")
PPU = 32


def hx(s):
    s = s.lstrip("#")
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), 255)


def pal(*cols):
    """5 Toene von dunkel nach hell: deep, shade, base, light, hi."""
    return [hx(c) for c in cols]


def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3)) + (255,)


INK = hx("#2e1a26")          # Grundton der Kontur (warmes Pflaume-Schwarz)
SHADOW = (46, 26, 38, 96)    # Bodenschatten, halbtransparent

L = (-0.55, -0.62, 0.56)
_n = math.sqrt(sum(c * c for c in L))
L = tuple(c / _n for c in L)


def tone_of(d, p, gloss=True):
    if d > 0.93 and gloss:
        return p[4]
    if d > 0.68:
        return p[3]
    if d > 0.32:
        return p[2]
    if d > -0.05:
        return p[1]
    return p[0]


class Canvas:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        self.px = self.img.load()

    # --- Masken -----------------------------------------------------------
    def mask(self, draw_fn):
        m = Image.new("L", (self.w, self.h), 0)
        draw_fn(ImageDraw.Draw(m))
        mp = m.load()
        return [(x, y) for y in range(self.h) for x in range(self.w) if mp[x, y]]

    def ellipse(self, cx, cy, rx, ry):
        pts = []
        for y in range(self.h):
            for x in range(self.w):
                if ((x + 0.5 - cx) / rx) ** 2 + ((y + 0.5 - cy) / ry) ** 2 <= 1.0:
                    pts.append((x, y))
        return pts

    def poly(self, points):
        return self.mask(lambda d: d.polygon(points, fill=255))

    def rect(self, x0, y0, x1, y1):
        return [(x, y) for y in range(max(0, y0), min(self.h, y1 + 1))
                for x in range(max(0, x0), min(self.w, x1 + 1))]

    # --- Fuellen ----------------------------------------------------------
    def fill(self, pts, col):
        for x, y in pts:
            if 0 <= x < self.w and 0 <= y < self.h:
                self.px[x, y] = col

    def sphere(self, pts, cx, cy, rx, ry, p, gloss=True):
        for x, y in pts:
            nx = (x + 0.5 - cx) / rx
            ny = (y + 0.5 - cy) / ry
            nz = math.sqrt(max(0.0, 1 - nx * nx - ny * ny))
            d = nx * L[0] + ny * L[1] + nz * L[2]
            self.px[x, y] = tone_of(d, p, gloss)

    def cyl_v(self, pts, cx, rx, p, gloss=True):
        """Stehender Zylinder: Licht nur nach x."""
        for x, y in pts:
            nx = max(-1.0, min(1.0, (x + 0.5 - cx) / rx))
            nz = math.sqrt(max(0.0, 1 - nx * nx))
            d = nx * L[0] + nz * L[2] + 0.12
            self.px[x, y] = tone_of(d, p, gloss)

    def cyl_h(self, pts, cy, ry, p, gloss=True):
        """Liegender Zylinder: Licht nur nach y."""
        for x, y in pts:
            ny = max(-1.0, min(1.0, (y + 0.5 - cy) / ry))
            nz = math.sqrt(max(0.0, 1 - ny * ny))
            d = ny * L[1] + nz * L[2] + 0.1
            self.px[x, y] = tone_of(d, p, gloss)

    def flat(self, pts, p, level):
        self.fill(pts, p[level])

    def line(self, pts, col):
        self.fill(pts, col)

    def hline(self, x0, x1, y, col):
        self.fill([(x, y) for x in range(x0, x1 + 1)], col)

    def vline(self, x, y0, y1, col):
        self.fill([(x, y) for y in range(y0, y1 + 1)], col)

    def arc(self, cx, cy, rx, ry, a0, a1, col, step=0.01):
        """Ellipsenbogen (Winkel in Grad, 0 = rechts, 90 = unten)."""
        seen = set()
        a = a0
        while a <= a1:
            r = math.radians(a)
            x = int(math.floor(cx + rx * math.cos(r)))
            y = int(math.floor(cy + ry * math.sin(r)))
            if (x, y) not in seen:
                seen.add((x, y))
                if 0 <= x < self.w and 0 <= y < self.h:
                    self.px[x, y] = col
            a += step * 57.3
        return seen

    # --- Abschluss --------------------------------------------------------
    def outline(self):
        """Aussenkontur: transparente Nachbarn bekommen die abgedunkelte Farbe."""
        src = self.img.copy().load()
        for y in range(self.h):
            for x in range(self.w):
                if src[x, y][3]:
                    continue
                best = None
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < self.w and 0 <= ny < self.h and src[nx, ny][3] == 255:
                        c = src[nx, ny]
                        if best is None or sum(c[:3]) < sum(best[:3]):
                            best = c
                if best is not None:
                    self.px[x, y] = mix(best, INK, 0.78)

    def shadow(self, cx, cy, rx, ry):
        for x, y in self.ellipse(cx, cy, rx, ry):
            if self.px[x, y][3] == 0:
                self.px[x, y] = SHADOW


# ---------------------------------------------------------------------------
# Paletten
RED_ENAMEL = pal("#6e1f2a", "#9e2f3a", "#c8464a", "#e2705f", "#ffc3a8")
STEEL = pal("#4b5468", "#6f7a90", "#98a3b6", "#c3cbd8", "#f2f5fa")
BURLAP = pal("#7a5a3a", "#a07c52", "#c9a675", "#e2c796", "#f3e1b8")
FLOUR = pal("#b9aca0", "#d9cfc4", "#ece5dc", "#f8f4ee", "#ffffff")
CARTON = pal("#8d9bb5", "#b9c4d8", "#e4eaf3", "#f6f9fd", "#ffffff")
BLUE = pal("#22417a", "#2f5aa0", "#3f78c8", "#6a9ee0", "#b6d4ff")
JAM = pal("#4f1022", "#7e1d33", "#a92c47", "#cf4a62", "#ff9fae")
GLASS = pal("#8fa8b4", "#b5cdd6", "#d5e8ee", "#eef8fb", "#ffffff")
WOOD = pal("#6e4228", "#8e5a33", "#b67a45", "#d39a5e", "#ecc28a")
EGG = pal("#8a5638", "#b0784e", "#d39b6c", "#e9bc8e", "#fbe2c3")
MINT = pal("#2f6e66", "#4a9a88", "#6cc0a6", "#9fe0c6", "#e6fff4")
MUSTARD = pal("#8a5a1c", "#b97e26", "#e0a83a", "#f3c95e", "#fff0b8")
COFFEE = pal("#2e1810", "#4a2a1a", "#6b3f25", "#8e5a36", "#c99a6e")
CHEESE = pal("#a8701e", "#d69a2e", "#f2c14e", "#ffdc78", "#fff3c0")
CLOTH_R = hx("#c8464a")
CLOTH_W = hx("#f6efe6")
CREAM = pal("#b39f84", "#d8c6a6", "#efe2c6", "#faf2e0", "#ffffff")
LEAF = pal("#2f5a2a", "#47762b", "#6a9e38", "#8fc24e", "#c4e68a")

# 3x5-Pixelschrift fuer Aufdrucke
FONT = {
    "M": ["1.1", "111", "111", "1.1", "1.1"], "E": ["111", "1..", "11.", "1..", "111"],
    "H": ["1.1", "1.1", "111", "1.1", "1.1"], "L": ["1..", "1..", "1..", "1..", "111"],
    "I": ["111", ".1.", ".1.", ".1.", "111"], "C": [".11", "1..", "1..", "1..", ".11"],
    "S": [".11", "1..", ".1.", "..1", "11."], "A": [".1.", "1.1", "111", "1.1", "1.1"],
    "Z": ["111", "..1", ".1.", "1..", "111"], "K": ["1.1", "1.1", "11.", "1.1", "1.1"],
    "R": ["11.", "1.1", "11.", "1.1", "1.1"],
}


def text(c, s, x, y, col):
    for ch in s:
        g = FONT[ch]
        for gy, row in enumerate(g):
            for gx, v in enumerate(row):
                if v == "1":
                    c.px[x + gx, y + gy] = col
        x += 4


# ---------------------------------------------------------------------------
# Props. Jede Funktion gibt (Canvas, Fusspunkt (x, y), Fuss (w, h)) zurueck.

def kochtopf():
    c = Canvas(64, 60)
    cx = 32
    # Griffe hinter dem Koerper
    for side in (-1, 1):
        x0 = cx + side * 27
        pts = c.rect(min(x0, x0 + side * 5), 21, max(x0, x0 + side * 5), 25)
        c.flat(pts, STEEL, 1)
        c.hline(min(x0, x0 + side * 5), max(x0, x0 + side * 5), 21, STEEL[3])
    # Koerper: Zylinder mit gerundetem Boden
    body = [(x, y) for (x, y) in c.rect(cx - 25, 18, cx + 24, 56)
            if y <= 49 + 6 * math.sqrt(max(0, 1 - ((x + 0.5 - cx) / 25) ** 2))]
    c.cyl_v(body, cx, 25, RED_ENAMEL)
    # helles Band unten (Emaille-Rand)
    for x, y in body:
        lim = 49 + 6 * math.sqrt(max(0, 1 - ((x + 0.5 - cx) / 25) ** 2))
        if y >= lim - 1:
            c.px[x, y] = RED_ENAMEL[0]
    # Rand oben
    rim = c.ellipse(cx, 18, 26, 7.5)
    c.flat(rim, STEEL, 2)
    c.arc(cx, 18, 25.5, 7, 180, 360, STEEL[4])
    c.arc(cx, 18, 25.5, 7, 0, 180, STEEL[1])
    # Deckel, leicht gewoelbt
    lid = c.ellipse(cx, 16, 23, 6.2)
    c.sphere(lid, cx - 2, 14, 26, 9, STEEL)
    c.arc(cx, 16, 23, 6.2, 10, 170, STEEL[1])
    # Knauf
    knob_stem = c.rect(cx - 2, 9, cx + 2, 13)
    c.cyl_v(knob_stem, cx, 3, STEEL)
    knob = c.ellipse(cx, 9, 5, 2.6)
    c.sphere(knob, cx - 1, 8, 6, 3.5, RED_ENAMEL)
    # Glanzlicht auf dem Emaille
    c.vline(cx - 15, 27, 40, RED_ENAMEL[4])
    c.vline(cx - 14, 29, 34, RED_ENAMEL[4])
    c.outline()
    c.shadow(cx + 2, 54, 30, 6)
    return c, (cx + 1, 54), (48, 10)


def mehlsack():
    c = Canvas(60, 64)
    cx = 28
    # Mehlhaeufchen links vorne (hinter dem Sack beginnend)
    pile = c.ellipse(9, 56, 9, 5)
    c.sphere(pile, 7, 55, 10, 7, FLOUR)
    # Sack-Koerper: unten breit und flach, oben eingeschnuert
    body = c.poly([(cx - 10, 18), (cx + 10, 18), (cx + 19, 28), (cx + 22, 44), (cx + 21, 55),
                   (cx + 16, 59), (cx - 16, 59), (cx - 21, 55), (cx - 22, 44), (cx - 19, 28)])
    c.sphere(body, cx - 1, 40, 25, 24, BURLAP, gloss=False)
    # Falten
    for (x0, y0, x1, y1) in [(cx - 9, 21, cx - 13, 33), (cx + 8, 21, cx + 13, 31), (cx - 2, 22, cx - 3, 28)]:
        n = max(abs(x1 - x0), abs(y1 - y0))
        for i in range(n + 1):
            x = round(x0 + (x1 - x0) * i / n)
            y = round(y0 + (y1 - y0) * i / n)
            c.px[x, y] = BURLAP[1]
            if c.px[x + 1, y][3]:
                c.px[x + 1, y] = BURLAP[3]
    # Hals mit Schnur
    neck = c.rect(cx - 6, 12, cx + 5, 19)
    c.cyl_v(neck, cx, 7, BURLAP, gloss=False)
    tie = c.rect(cx - 7, 16, cx + 6, 18)
    c.cyl_v(tie, cx, 7, pal("#6b2a1e", "#8e3a26", "#b0533a", "#cf7454", "#e9a07c"))
    # Zipfel oben, aufgefaechert
    tuft = c.poly([(cx - 6, 13), (cx - 11, 5), (cx - 6, 7), (cx - 2, 2), (cx + 2, 6), (cx + 7, 3),
                   (cx + 10, 8), (cx + 6, 13)])
    c.sphere(tuft, cx - 3, 6, 12, 9, BURLAP, gloss=False)
    # Mehl schaut oben heraus
    c.hline(cx - 3, cx + 2, 5, FLOUR[3])
    c.hline(cx - 2, cx + 1, 4, FLOUR[4])
    # Aufdruck MEHL + Aehre
    text(c, "MEHL", cx - 8, 38, hx("#6b4a2e"))
    for i in range(6):
        c.px[cx, 30 + i] = hx("#8a6440")
    for i in range(3):
        c.px[cx - 1, 30 + i * 2] = hx("#b88a52")
        c.px[cx + 1, 31 + i * 2] = hx("#b88a52")
    # Saum unten
    c.hline(cx - 15, cx + 15, 58, BURLAP[0])
    c.outline()
    # Mehlstaub ueber der Kontur verstreut
    for x, y in [(2, 50), (4, 61), (17, 62), (21, 61), (1, 57)]:
        if c.px[x, y][3] == 0:
            c.px[x, y] = FLOUR[3]
    c.shadow(cx + 2, 59, 26, 5)
    return c, (cx, 59), (40, 9)


def milchtuete():
    c = Canvas(44, 76)
    # Frontflaeche x 5..28, Seitenflaeche x 29..37 (perspektivisch nach oben versetzt)
    fx0, fx1, top, bot = 5, 28, 26, 70
    front = c.rect(fx0, top, fx1, bot)
    c.flat(front, CARTON, 2)
    side = c.poly([(fx1 + 1, top), (fx1 + 9, top - 4), (fx1 + 9, bot - 4), (fx1 + 1, bot)])
    c.flat(side, CARTON, 1)
    # Giebel: vordere Dachschraege (beleuchtet) und Seitendreieck
    roof = c.poly([(fx0, top), (fx1 + 1, top), (fx1 + 9, top - 4), (fx1 + 1, 12), (fx0 + 8, 12)])
    roof_front = c.poly([(fx0, top - 1), (fx1 + 1, top - 1), (fx1 - 3, 13), (fx0 + 6, 13)])
    c.flat(roof, CARTON, 1)
    c.flat(roof_front, CARTON, 3)
    # Falz oben
    fin = c.rect(fx0 + 6, 7, fx1 - 3, 12)
    c.flat(fin, CARTON, 3)
    c.hline(fx0 + 6, fx1 - 3, 7, CARTON[4])
    c.hline(fx0 + 6, fx1 - 3, 12, CARTON[1])
    # Blaues Band + Schrift
    band = c.rect(fx0, 44, fx1, 54)
    c.flat(band, BLUE, 2)
    c.hline(fx0, fx1, 44, BLUE[3])
    c.hline(fx0, fx1, 54, BLUE[1])
    side_band = c.poly([(fx1 + 1, 44), (fx1 + 9, 40), (fx1 + 9, 50), (fx1 + 1, 54)])
    c.flat(side_band, BLUE, 1)
    text(c, "MILCH", fx0 + 2, 47, CARTON[4])
    # Kuhflecken
    for (sx, sy, rx, ry) in [(10, 33, 3.2, 2.4), (22, 37, 2.6, 2), (14, 61, 3, 2.2), (25, 64, 2, 1.6)]:
        c.fill(c.ellipse(sx, sy, rx, ry), hx("#3b2433"))
    c.fill(c.ellipse(fx1 + 5, 30, 1.8, 2.4), hx("#4e3642"))
    # Licht an der Vorderkante, Schatten unten
    c.vline(fx0, top, bot, CARTON[4])
    c.vline(fx1, top, bot, CARTON[1])
    c.hline(fx0, fx1, bot, CARTON[0])
    # Kante Front/Dach
    c.hline(fx0, fx1, top - 1, CARTON[4])
    c.outline()
    c.shadow(21, 70, 20, 4)
    return c, (21, 70), (32, 6)


def marmeladenglas():
    c = Canvas(48, 58)
    cx = 23
    body = [(x, y) for (x, y) in c.rect(cx - 17, 16, cx + 17, 54)
            if y <= 49 + 4 * math.sqrt(max(0, 1 - ((x + 0.5 - cx) / 18) ** 2))]
    # Marmelade im Glas (fast voll)
    c.cyl_v(body, cx, 18, JAM)
    # Glas oben ueber der Marmelade (heller Rand)
    gap = [(x, y) for (x, y) in body if y < 21]
    c.cyl_v(gap, cx, 18, GLASS)
    # Glasglanz: senkrechte Streifen links
    c.vline(cx - 12, 23, 46, GLASS[3])
    c.vline(cx - 11, 23, 40, GLASS[4])
    c.vline(cx + 13, 25, 44, JAM[3])
    # Etikett mit Erdbeere
    lab = [(x, y) for (x, y) in c.rect(cx - 9, 31, cx + 10, 44)]
    c.cyl_v(lab, cx, 18, CREAM, gloss=False)
    c.hline(cx - 9, cx + 10, 31, CREAM[1])
    c.hline(cx - 9, cx + 10, 44, CREAM[1])
    berry = c.poly([(cx - 3, 35), (cx + 4, 35), (cx + 3, 39), (cx, 42), (cx - 2, 39)])
    c.flat(berry, JAM, 3)
    c.px[cx - 1, 37] = JAM[4]
    c.px[cx + 2, 38] = CREAM[3]
    c.px[cx, 40] = CREAM[3]
    c.hline(cx - 2, cx + 3, 34, LEAF[2])
    c.px[cx, 33] = LEAF[1]
    # Deckel mit kariertem Tuch
    cloth = c.ellipse(cx, 15, 21, 7)
    skirt = [(x, y) for (x, y) in c.rect(cx - 21, 15, cx + 21, 22)
             if abs(x + 0.5 - cx) <= 21 - (y - 15) * 0.5]
    for x, y in cloth + skirt:
        chk = ((x // 3) + (y // 3)) % 2
        base = CLOTH_R if chk else CLOTH_W
        nx = (x + 0.5 - cx) / 21
        if nx > 0.45 or y >= 20:
            base = mix(base, INK, 0.25)
        elif nx < -0.4 and y < 14:
            base = mix(base, hx("#ffffff"), 0.25)
        c.px[x, y] = base
    # Zackenrand des Tuchs
    for x in range(cx - 20, cx + 21):
        if (x % 3) == 0:
            c.px[x, 23] = mix(CLOTH_R, INK, 0.3)
    # Kordel
    c.arc(cx, 19, 18, 3.5, 20, 160, hx("#e8d4a0"))
    c.outline()
    c.shadow(cx + 2, 53, 21, 4)
    return c, (cx + 1, 53), (32, 7)


def nudelholz():
    c = Canvas(88, 30)
    cy = 14
    # Griffe
    for x0, x1 in ((3, 15), (72, 84)):
        h = c.rect(x0, cy - 4, x1, cy + 4)
        c.cyl_h(h, cy, 4.5, WOOD)
    for kx in (3, 84):
        knob = c.ellipse(kx + 0.5, cy + 0.5, 3, 5)
        c.cyl_h(knob, cy, 5.5, WOOD)
    # Rolle
    roll = c.rect(15, cy - 8, 72, cy + 8)
    c.cyl_h(roll, cy, 8.5, WOOD)
    for x in (15, 72):
        c.vline(x, cy - 8, cy + 8, WOOD[1])
    # Maserung
    for (x0, y, n) in [(22, cy - 2, 9), (40, cy + 3, 12), (55, cy - 4, 8), (31, cy + 5, 6), (60, cy + 2, 7)]:
        c.hline(x0, x0 + n, y, WOOD[1] if y > cy else WOOD[2])
    # Mehl auf der Rolle
    for x, y in [(26, cy - 6), (27, cy - 6), (45, cy - 7), (46, cy - 6), (58, cy - 6), (34, cy - 5)]:
        c.px[x, y] = FLOUR[3]
    c.outline()
    c.shadow(44, cy + 10, 40, 4)
    return c, (44, cy + 10), (76, 8)


def ei():
    c = Canvas(32, 40)
    cx, cy = 15, 21
    pts = [(x, y) for y in range(c.h) for x in range(c.w)
           if ((x + 0.5 - cx) / (11 if y + 0.5 > cy else 9.5)) ** 2 + ((y + 0.5 - cy) / (13 if y + 0.5 > cy else 15)) ** 2 <= 1]
    c.sphere(pts, cx, cy + 1, 12, 15, EGG)
    # Sommersprossen
    for x, y in [(19, 16), (12, 27), (21, 25), (16, 31), (9, 20)]:
        c.px[x, y] = EGG[1]
    c.outline()
    c.shadow(cx + 2, 35, 12, 3)
    return c, (cx + 1, 35), (18, 5)


def teekanne():
    c = Canvas(70, 58)
    cx, cy = 34, 34
    # Henkel links (hinter dem Koerper)
    ring = [(x, y) for (x, y) in c.ellipse(cx - 20, cy - 1, 9, 11)
            if not (((x + 0.5 - (cx - 20)) / 5) ** 2 + ((y + 0.5 - (cy - 1)) / 7) ** 2 <= 1)]
    c.sphere(ring, cx - 22, cy - 4, 11, 13, MINT)
    # Tuelle rechts
    spout = c.poly([(cx + 14, cy + 6), (cx + 18, cy - 2), (cx + 26, cy - 12), (cx + 31, cy - 15),
                    (cx + 32, cy - 12), (cx + 27, cy - 7), (cx + 22, cy + 6)])
    c.cyl_v(spout, cx + 24, 6, MINT)
    c.fill(c.ellipse(cx + 31, cy - 14, 2.5, 1.4), MINT[0])
    # Koerper
    body = c.ellipse(cx, cy, 21, 17)
    c.sphere(body, cx, cy, 21, 17, MINT)
    # Fuss
    foot = c.rect(cx - 13, cy + 15, cx + 13, cy + 18)
    c.cyl_v(foot, cx, 14, MINT)
    # Dekor: weisse Tupfen-Bordüre
    for i, a in enumerate(range(200, 345, 18)):
        r = math.radians(a)
        x = int(cx + 17 * math.cos(r))
        y = int(cy + 3 + 9 * math.sin(r) * -1)
        c.px[x, y] = CREAM[4]
    for a in range(-150, -20, 14):
        r = math.radians(a)
        x = int(cx + 19 * math.cos(r))
        y = int(cy + 4 - 6 * math.sin(r))
        c.px[x, y] = MINT[0]
    # Deckel + Knauf
    c.arc(cx, cy - 13, 11, 3.5, 0, 180, MINT[0])
    lid = c.ellipse(cx, cy - 15, 11, 4)
    c.sphere(lid, cx - 2, cy - 17, 13, 7, MINT)
    knob = c.ellipse(cx, cy - 20, 3.5, 3)
    c.sphere(knob, cx - 1, cy - 21, 4, 4, CREAM)
    c.outline()
    c.shadow(cx + 3, cy + 19, 22, 4)
    return c, (cx + 2, cy + 19), (40, 7)


def salzstreuer():
    c = Canvas(28, 50)
    cx = 13
    glass = [(x, y) for (x, y) in c.rect(cx - 9, 16, cx + 9, 46)
             if y <= 43 + 3 * math.sqrt(max(0, 1 - ((x + 0.5 - cx) / 9.5) ** 2))]
    c.cyl_v(glass, cx, 9.5, GLASS)
    # Salz drin bis zur Haelfte
    salt = [(x, y) for (x, y) in glass if y >= 30]
    c.cyl_v(salt, cx, 9.5, CREAM, gloss=False)
    c.arc(cx, 30, 8.5, 2, 0, 180, CREAM[4])
    c.vline(cx - 6, 18, 42, GLASS[4])
    # Kappe
    cap = c.rect(cx - 9, 8, cx + 9, 16)
    c.cyl_v(cap, cx, 9.5, STEEL)
    top = c.ellipse(cx, 8, 9, 3.4)
    c.sphere(top, cx - 2, 6, 12, 6, STEEL)
    for x, y in [(cx - 3, 7), (cx, 6), (cx + 3, 7), (cx - 1, 9), (cx + 2, 9)]:
        c.px[x, y] = STEEL[0]
    c.hline(cx - 9, cx + 9, 16, STEEL[0])
    c.outline()
    c.shadow(cx + 1, 46, 11, 3)
    return c, (cx + 1, 46), (16, 5)


def tasse():
    c = Canvas(52, 48)
    cx = 22
    # Henkel rechts
    ring = [(x, y) for (x, y) in c.ellipse(cx + 17, 26, 8, 9.5)
            if not (((x + 0.5 - (cx + 17)) / 4) ** 2 + ((y + 0.5 - 26) / 5.5) ** 2 <= 1)
            and x >= cx + 14]
    c.sphere(ring, cx + 16, 23, 10, 12, MUSTARD)
    body = [(x, y) for (x, y) in c.rect(cx - 17, 12, cx + 17, 46)
            if y <= 39 + 4 * math.sqrt(max(0, 1 - ((x + 0.5 - cx) / 17.5) ** 2))]
    c.cyl_v(body, cx, 17.5, MUSTARD)
    # Herz-Aufdruck
    heart = [(x, y) for y in range(c.h) for x in range(c.w)
             if ((x - (cx - 4)) ** 2 + (y - 26) ** 2 <= 6.5 or (x - (cx + 1)) ** 2 + (y - 26) ** 2 <= 6.5
                 or (y >= 27 and abs(x + 0.5 - (cx - 1.5)) <= (33 - y) * 0.9))]
    c.fill(heart, CHEESE[4])
    c.px[cx - 5, 25] = hx("#ffffff")
    # Rand + Kaffee
    rim = c.ellipse(cx, 12, 17.5, 6)
    c.flat(rim, MUSTARD, 3)
    c.arc(cx, 12, 17, 5.5, 180, 360, MUSTARD[4])
    coffee = c.ellipse(cx, 12.5, 15, 4.5)
    c.flat(coffee, COFFEE, 2)
    c.arc(cx, 12.5, 15, 4.5, 180, 360, COFFEE[0])
    c.fill(c.ellipse(cx + 3, 13.5, 6, 1.6), COFFEE[3])
    c.hline(cx + 1, cx + 4, 13, COFFEE[4])
    c.outline()
    c.shadow(cx + 3, 44, 21, 4)
    return c, (cx + 2, 44), (34, 6)


def kaese():
    c = Canvas(60, 46)
    # Keil: Oberseite (hell) als Dreieck, Front (Rinde/Schnitt) als Viereck
    topf = c.poly([(4, 18), (54, 10), (54, 18), (10, 26)])
    front = c.poly([(4, 18), (10, 26), (10, 41), (4, 34)])
    cut = c.poly([(10, 26), (54, 18), (54, 34), (10, 41)])
    c.flat(topf, CHEESE, 3)
    c.flat(front, CHEESE, 1)
    c.flat(cut, CHEESE, 2)
    # Lichtkanten
    for i in range(45):
        x = 10 + i
        y = round(26 - 8 * i / 44)
        c.px[x, y] = CHEESE[4]
    c.vline(10, 26, 41, CHEESE[3])
    # Loecher in der Schnittflaeche
    for (hx_, hy, rx, ry) in [(20, 31, 3, 2.5), (33, 28, 2.4, 2), (44, 26, 3.4, 2.8), (27, 37, 1.8, 1.5),
                              (48, 33, 1.6, 1.4), (39, 36, 2.2, 1.6)]:
        hole = c.ellipse(hx_, hy, rx, ry)
        c.fill(hole, CHEESE[1])
        c.arc(hx_, hy, rx, ry, 180, 300, CHEESE[0])
        c.arc(hx_, hy, rx, ry, 20, 120, CHEESE[3])
    # Loch oben
    c.fill(c.ellipse(30, 17, 3, 1.4), CHEESE[2])
    c.hline(29, 31, 16, CHEESE[1])
    # Unterkante
    for i in range(45):
        x = 10 + i
        y = round(41 - 7 * i / 44)
        c.px[x, y] = CHEESE[0]
    c.outline()
    c.shadow(31, 40, 27, 4)
    return c, (30, 39), (46, 8)


PROPS = {
    "kochtopf": kochtopf,
    "mehlsack": mehlsack,
    "milchtuete": milchtuete,
    "marmeladenglas": marmeladenglas,
    "nudelholz": nudelholz,
    "ei": ei,
    "teekanne": teekanne,
    "salzstreuer": salzstreuer,
    "tasse": tasse,
    "kaese": kaese,
}


def render_all():
    out = {}
    for name, fn in PROPS.items():
        c, foot, size = fn()
        out[name] = (c.img, foot, size)
    return out


def preview(props, path, scale=3):
    """Alle Props auf einem Stueck Kuechenboden, dazu der Keks als Massstab."""
    import importlib.util
    spec = importlib.util.spec_from_file_location("kb", os.path.join(os.path.dirname(__file__), "kueche_boden.py"))
    kb = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(kb)
    sheet, _ = kb.build()
    cols, rows = 14, 6
    floor = Image.new("RGBA", (cols * 32, rows * 32))
    for r in range(rows):
        for c in range(cols):
            idx = 0 if (r + c) % 2 == 0 else 10
            floor.paste(sheet.crop((idx * 32, 0, idx * 32 + 32, 32)), (c * 32, r * 32))
    names = list(props)
    x = 6
    row_y = [92, 182]
    for i, n in enumerate(names):
        img, foot, _ = props[n]
        ry = row_y[0] if i < 5 else row_y[1]
        if i == 5:
            x = 6
        floor.alpha_composite(img, (x, ry - foot[1]))
        x += img.width + 8
    keks = os.path.join(ROOT, "Assets", "Art", "Chars", "Char_Keks.png")
    if os.path.exists(keks):
        k = Image.open(keks).convert("RGBA")
        cell = k.crop((0, 0, 40, 40)) if k.width >= 40 else k
        floor.alpha_composite(cell, (cols * 32 - 46, row_y[1] - 34))
    floor.resize((floor.width * scale, floor.height * scale), Image.NEAREST).save(path)


def main():
    props = render_all()
    if "--preview" in sys.argv:
        preview(props, sys.argv[sys.argv.index("--preview") + 1])
        return
    os.makedirs(OUT_DIR, exist_ok=True)
    info = []
    for name, (img, foot, size) in props.items():
        path = os.path.join(OUT_DIR, "kueche_%s.png" % name)
        img.save(path)
        meta = path + ".meta"
        if not os.path.exists(meta):
            pivot = (round(foot[0] / img.width, 4), round(1 - foot[1] / img.height, 4))
            write_strip_meta(meta, "kueche_" + name, 1, img.width, img.height, PPU, pivot=pivot)
        info.append({"name": name, "colliderW": size[0], "colliderH": size[1]})
    # Liest KuecheBuilder (JsonUtility) fuer die Kollisionsflaechen der Prefabs.
    with open(os.path.join(OUT_DIR, "kueche_props.json"), "w", newline="\n") as fh:
        json.dump({"props": info}, fh, indent=1)
    print("geschrieben:", len(props), "Props nach", OUT_DIR)


if __name__ == "__main__":
    main()
