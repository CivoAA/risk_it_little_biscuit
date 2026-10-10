"""
Zeichnet die Hindernisse der Eiswelt (Level 3, Szene Map_World5) - ein
Gletscher aus Eiscreme, Waffeln und Vanilleschnee.

  Assets/Art/World-Objects/Eis/eis_<name>.png        stehende Props, 1 Sprite, PPU 32
  Assets/Art/World-Objects/Eis/eis_glatteis_<n>.png  Glatteis-Pfuetzen, Flipbook-Streifen, PPU 32
  Assets/Art/World-Objects/Eis/eis_props.json        Fuesse, Polygone, Masse fuer EisBuilder/eis_boden.py

Stehend (Pivot = Mitte des Bodenschattens, SpriteSortPoint Pivot, Box am Fuss):
  waffeltanne   Tanne aus drei Waffeltueten, Sahneschnee auf jeder Stufe, Kirsche oben
  eiskugeln     Haufen aus Pistazie, Erdbeer und Schoko mit Streuseln und Waffelroellchen
  eis_am_stiel  Erdbeer-Eis mit Schokohaube und Biss, steckt im Schnee
  kandis        Kandiszucker-Kristalle, eisblau und rosa
  schneemann    Schneemann mit Waffeltueten-Hut, Zuckerstangen-Schal und Keks-Knoepfen
  gummibaer     ein Gummibaer, im Eisblock eingefroren

Flach (Pivot = Mitte, Background-Layer, kein Hindernis):
  glatteis_*    gefrorene Milchpfuetzen mit Schneewall, Glanzstreifen laeuft drueber.
                Wer drauf laeuft, rutscht (Glatteis.cs) - das Polygon in der json ist die Eisflaeche.

Stil wie Tools/kueche_props.py (Canvas, Licht oben links, warme Kontur).

Aufruf aus dem Projektordner:  python Tools/eis_props.py [--preview pfad.png]
Die .meta wird nur beim ersten Mal geschrieben (sonst verlieren Prefabs ihre Verweise).
"""

import json
import math
import os
import random
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kueche_props import Canvas, hx, mix, pal  # noqa: E402
from unity_meta import write_strip_meta  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "Assets", "Art", "World-Objects", "Eis")
INFO = os.path.join(OUT_DIR, "eis_props.json")
PPU = 32

# --- Paletten (deep, shade, base, light, hi) --------------------------------------
SNOW = pal("#8a86b4", "#a9a9d2", "#cdd0ea", "#e8eaf8", "#ffffff")
ICE = pal("#3b5f96", "#5684bc", "#7eaedb", "#b1d8f1", "#effbff")
WAFFLE = pal("#6e3f1c", "#9a6229", "#c98a3e", "#e6b464", "#f8dc9a")
STRAW = pal("#82284a", "#b94468", "#e46e8c", "#f6a0b4", "#ffd8e2")
PISTA = pal("#3c6438", "#5a8e4e", "#82b868", "#acd88c", "#e0f4c4")
CHOCO = pal("#2c1610", "#48281a", "#6a3e25", "#8c5935", "#c3946a")
CHERRY = pal("#560e1c", "#8a182c", "#c32a3c", "#e8545c", "#ffb6b4")
CANDY = pal("#7a3a7a", "#ae5aa6", "#e08ac8", "#f6bce0", "#fff0fa")
BEAR = pal("#7a1420", "#b0222c", "#e0403a", "#f47a5a", "#ffc4a4")
WOOD = pal("#7a5232", "#a07446", "#c8a06a", "#e2c494", "#f6e4c0")
SPRINKLES = [hx("#ff6f9a"), hx("#ffd34e"), hx("#6fe0b6"), hx("#6fb2ff"), hx("#ffffff"), hx("#c88cff")]
SHADOW_SNOW = (64, 58, 110, 70)


def sprinkle(c, x, y, rng, on=None):
    """Streusel: 2 px lang, waagerecht oder senkrecht, nur auf bemalten Pixeln."""
    col = rng.choice(SPRINKLES)
    dx, dy = rng.choice(((1, 0), (0, 1), (1, 1)))
    for px, py in ((x, y), (x + dx, y + dy)):
        if 0 <= px < c.w and 0 <= py < c.h and c.px[px, py][3] == 255:
            if on is None or (px, py) in on:
                c.px[px, py] = col


def scoop_shade(c, pts, cx, cy, rx, ry, p):
    c.sphere(pts, cx, cy, rx, ry, p)


def snow_cap(c, pts, cx, cy, rx, ry, rng, level=-0.45):
    """Sahne/Schnee auf der Oberseite einer Kugel: weiche Wellenkante mit Tropfen."""
    ph = rng.random() * 6
    cap = []
    for x, y in pts:
        nx = (x + 0.5 - cx) / rx
        ny = (y + 0.5 - cy) / ry
        edge = level + 0.12 * math.sin(nx * 7 + ph) + (0.22 if (int((nx + 1) * 5 + ph) % 3 == 0 and abs(nx) < 0.7) else 0)
        if ny < edge:
            cap.append((x, y))
    c.sphere(cap, cx - 1, cy - 2, rx * 1.1, ry * 1.1, SNOW)
    s = set(cap)
    for x, y in cap:
        if (x, y + 1) not in s and (x, y + 1) in set(pts):
            c.px[x, y] = SNOW[1]
    return s


# ---------------------------------------------------------------------------
# Stehende Props: (Canvas, Fusspunkt, Fuss (w, h))

def waffle_tier(c, apex_y, base_y, hw, cx, rng, seed):
    """Eine Stufe der Waffeltanne: Tuete mit gewoelbtem Unterrand und Waffelmuster."""
    pts = []
    for y in range(apex_y, base_y + 5):
        for x in range(cx - hw - 1, cx + hw + 2):
            t = (y - apex_y) / max(1, base_y - apex_y)
            half = hw * min(1.0, t)
            dx = x + 0.5 - cx
            if abs(dx) > half:
                continue
            bottom = base_y + 4 * math.sqrt(max(0, 1 - (dx / max(1, hw)) ** 2))
            if y <= bottom:
                pts.append((x, y))
    c.cyl_v(pts, cx, hw, WAFFLE, gloss=False)
    s = set(pts)
    # Waffelgitter: zwei Diagonalen, Rille dunkel, darunter ein heller Grat
    for x, y in pts:
        a = (x + y + seed) % 6
        b = (x - y + seed + 60) % 6
        lit = x < cx
        if a == 0 or b == 0:
            c.px[x, y] = WAFFLE[1] if lit else WAFFLE[0]
        elif (a == 1 or b == 1) and lit and rng.random() < 0.6:
            c.px[x, y] = WAFFLE[3]
    # Unterrand: dunkle Kante
    for x, y in pts:
        if (x, y + 1) not in s:
            c.px[x, y] = WAFFLE[0]
    return s


def waffeltanne():
    c = Canvas(58, 92)
    cx = 29
    rng = random.Random(11)
    # Stamm: Waffelroellchen
    trunk = c.rect(cx - 4, 74, cx + 3, 86)
    c.cyl_v(trunk, cx, 4, WAFFLE)
    for x, y in trunk:
        if (y * 2 + x) % 5 == 0:
            c.px[x, y] = WAFFLE[1]
    tiers = [(44, 76, 27, 0), (26, 58, 22, 2), (8, 40, 16, 4)]
    prev = set()
    for i, (ay, by, hw, sd) in enumerate(tiers):
        s = waffle_tier(c, ay, by, hw, cx, rng, sd)
        # Schatten der neuen Stufe auf der unteren
        for x, y in s:
            for k in (1, 2, 3):
                q = (x, y + k)
                if q in prev and q not in s:
                    c.px[q[0], q[1]] = WAFFLE[0] if k < 3 else WAFFLE[1]
        # Sahneschnee auf der Stufe: oben dick, laeuft in Nasen herunter
        # (die Spitze der unteren Stufen steckt unter der oberen - dort liegt
        # der Schnee auf den Schultern, also an den schraegen Aussenkanten)
        cap = []
        ph = i * 1.7 + 0.4
        for x, y in s:
            t = (y - ay) / (by - ay)
            dx = (x + 0.5 - cx) / hw
            drip = 0.10 * math.sin(dx * 9 + ph) + (0.13 if math.sin(dx * 4.3 + ph * 2) > 0.55 else 0)
            half = hw * min(1.0, t)
            edge = half - abs(x + 0.5 - cx)
            if t < 0.3 + drip or (0.3 < t < 0.8 and edge < 1.8 + 2.0 * drip + 0.9 * math.sin(y * 0.9 + ph)):
                cap.append((x, y))
        c.cyl_v(cap, cx - 2, hw * 0.75, SNOW)
        cs = set(cap)
        for x, y in cap:
            if (x, y + 1) in s and (x, y + 1) not in cs:
                c.px[x, y] = SNOW[1]
        for _ in range(4 + i):
            x, y = rng.choice(cap)
            sprinkle(c, x, y, rng, cs)
        prev |= s
    # Kirsche auf der Spitze
    ch = c.ellipse(cx + 0.5, 7, 4.6, 4.4)
    c.sphere(ch, cx - 0.5, 6, 5, 5, CHERRY)
    for (x, y) in ((cx + 1, 2), (cx + 2, 1), (cx + 3, 0), (cx + 2, 2)):
        c.px[x, y] = PISTA[1]
    c.outline()
    c.shadow(cx + 2, 86, 18, 5)
    return c, (cx, 86), (16, 10)


def scoop(c, cx, cy, rx, ry, p, rng):
    pts = c.ellipse(cx, cy, rx, ry)
    # Eis-Portionierer: wellige Unterkante
    body = [(x, y) for x, y in pts if y < cy + ry * 0.75 + 1.4 * math.sin(x * 1.3)]
    c.sphere(body, cx, cy, rx, ry, p)
    s = set(body)
    for x, y in body:
        if (x, y + 1) not in s:
            c.px[x, y] = p[0]
        elif (x, y + 2) not in s and (x % 3 == 0):
            c.px[x, y] = p[1]
    cap = snow_cap(c, body, cx, cy, rx, ry, rng)
    for _ in range(5):
        x, y = rng.choice(body)
        sprinkle(c, x, y, rng)
    return s | cap


def eiskugeln():
    c = Canvas(58, 52)
    rng = random.Random(23)
    # Waffelroellchen schraeg hinter der oberen Kugel
    roll = c.poly([(30, 4), (35, 2), (42, 18), (37, 20)])
    c.cyl_h(roll, 11, 9, WAFFLE)
    for x, y in roll:
        if (x * 2 - y) % 5 == 0:
            c.px[x, y] = WAFFLE[1]
    scoop(c, 28, 22, 13, 11, CHOCO, rng)
    scoop(c, 17, 34, 13.5, 11.5, PISTA, rng)
    scoop(c, 39, 35, 13.5, 11.5, STRAW, rng)
    # Kirsche vorne
    ch = c.ellipse(27.5, 42.5, 3.6, 3.4)
    c.sphere(ch, 27, 42, 4, 4, CHERRY)
    c.px[28, 38] = PISTA[1]
    c.px[29, 37] = PISTA[1]
    c.outline()
    c.shadow(29, 47, 26, 5)
    return c, (29, 47), (42, 12)


def eis_am_stiel():
    c = Canvas(32, 58)
    cx = 16
    # Stiel, unten im Schnee
    stick = c.rect(cx - 3, 38, cx + 2, 50)
    c.cyl_v(stick, cx, 3, WOOD, gloss=False)
    # Koerper: oben rund
    body = [(x, y) for x, y in c.rect(5, 3, 26, 40)
            if not (y < 9 and ((x + 0.5 - 11) ** 2 + (y + 0.5 - 9) ** 2 > 36 and x < 11))
            and not (y < 9 and ((x + 0.5 - 21) ** 2 + (y + 0.5 - 9) ** 2 > 36 and x > 21))]
    # Biss oben rechts
    body = [(x, y) for x, y in body if (x + 0.5 - 25) ** 2 + (y + 0.5 - 6) ** 2 > 26]
    c.cyl_v(body, 15.5, 11, STRAW)
    s = set(body)
    # Schokohaube mit Nasen
    choco = [(x, y) for x, y in body
             if y < 15 + 2.5 * math.sin(x * 0.9) + (5 if x in (9, 10, 19) else 0)]
    c.cyl_v(choco, 15.5, 11, CHOCO)
    cs = set(choco)
    rng = random.Random(5)
    for _ in range(7):
        x, y = rng.choice(choco)
        sprinkle(c, x, y, rng, cs)
    # Biss: helle Eiskante im Anschnitt
    for x, y in body:
        if (x + 0.5 - 25) ** 2 + (y + 0.5 - 6) ** 2 <= 34:
            c.px[x, y] = STRAW[3] if (x, y) not in cs else CHOCO[3]
    for x, y in body:
        if (x, y + 1) not in s:
            c.px[x, y] = STRAW[0]
    # Glanz
    c.vline(9, 18, 32, STRAW[4])
    c.vline(10, 20, 26, STRAW[4])
    # Schneehaufen am Fuss
    mound = [(x, y) for x, y in c.ellipse(cx, 52, 12, 5) if y >= 47]
    c.sphere(mound, cx - 2, 50, 13, 6, SNOW)
    c.outline()
    c.shadow(cx + 1, 54, 13, 3)
    return c, (cx, 54), (14, 8)


def crystal(c, x, base, h, w, p, tilt):
    tip = (x + tilt, base - h)
    pts = c.poly([(x - w, base), (x - w + tilt * 0.8, base - h + w + 2), tip,
                  (x + w + tilt * 0.8, base - h + w + 2), (x + w, base)])
    s = set(pts)
    for px, py in pts:
        mid = x + tilt * (base - py) / max(1, h)
        if px < mid - 0.5:
            col = p[3]
        elif px < mid + 0.5:
            col = p[4]
        else:
            col = p[1]
        if (px, py + 1) not in s:
            col = p[0]
        c.px[px, py] = col
    # Spitzenfacette
    for px, py in pts:
        if py < base - h + w + 1 and px >= x + tilt * 0.6:
            c.px[px, py] = p[2]
    return s


def kandis():
    c = Canvas(48, 50)
    specs = [(24, 40, 36, 4, ICE, 1), (15, 41, 26, 4, CANDY, -4), (33, 42, 28, 4, CANDY, 5),
             (8, 43, 16, 3, ICE, -4), (40, 43, 17, 3, ICE, 4), (21, 44, 18, 3, ICE, -2),
             (28, 44, 14, 3, CANDY, 2)]
    for x, b, h, w, p, t in specs:
        crystal(c, x, b, h, w, p, t)
    rng = random.Random(9)
    # Zuckerglitzer
    for _ in range(9):
        x, y = rng.randint(6, 42), rng.randint(10, 40)
        if c.px[x, y][3] == 255:
            c.px[x, y] = (255, 255, 255, 255)
    mound = [(x, y) for x, y in c.ellipse(24, 45, 21, 5) if y >= 42]
    c.sphere(mound, 22, 43, 22, 6, SNOW)
    c.outline()
    c.shadow(25, 47, 22, 3)
    return c, (24, 47), (32, 10)


def schneemann():
    c = Canvas(46, 64)
    cx = 23
    rng = random.Random(31)
    # Arme: Waffelstaebchen
    for side in (-1, 1):
        for k in range(10):
            x = cx + side * (11 + k)
            y = 40 - k // 2 - (k // 3 if k > 6 else 0)
            c.px[x, y] = WAFFLE[2]
            c.px[x, y + 1] = WAFFLE[0]
        tx, ty = cx + side * 20, 34
        c.px[tx, ty] = WAFFLE[2]
        c.px[tx + side, ty - 1] = WAFFLE[2]
    low = c.ellipse(cx, 47, 14, 12)
    c.sphere(low, cx, 47, 14, 12, SNOW)
    up = c.ellipse(cx, 27, 10.5, 9.5)
    c.sphere(up, cx, 27, 10.5, 9.5, SNOW)
    # Keks-Knoepfe
    for by in (41, 47, 53):
        b = c.ellipse(cx + 0.5, by + 0.5, 2.4, 2.2)
        c.sphere(b, cx, by, 2.6, 2.6, WAFFLE)
        c.px[cx, by] = CHOCO[1]
    # Gesicht
    for ex in (cx - 4, cx + 3):
        c.px[ex, 25] = CHOCO[0]
        c.px[ex + 1, 25] = CHOCO[0]
        c.px[ex, 26] = CHOCO[0]
        c.px[ex + 1, 26] = CHOCO[0]
        c.px[ex, 25] = (255, 255, 255, 255)
    for bx in (cx - 7, cx + 6):
        c.px[bx, 28] = STRAW[3]
        c.px[bx + 1, 28] = STRAW[3]
    for x in range(cx - 2, cx + 2):
        c.px[x, 30] = CHOCO[1]
    c.px[cx - 3, 29] = CHOCO[1]
    c.px[cx + 2, 29] = CHOCO[1]
    # Nase: kleines Bonbon (orange)
    c.px[cx, 27] = hx("#f08a3a")
    c.px[cx + 1, 27] = hx("#f08a3a")
    c.px[cx + 2, 28] = hx("#c8622a")
    # Schal: Zuckerstange
    scarf = [(x, y) for x, y in c.rect(cx - 10, 34, cx + 10, 37)
             if ((x + 0.5 - cx) / 10.5) ** 2 < 1.05]
    tail = c.rect(cx + 5, 37, cx + 8, 45)
    for x, y in scarf + tail:
        red = ((x + y) // 2) % 2 == 0
        c.px[x, y] = CHERRY[2] if red else SNOW[4]
    for x, y in scarf:
        if y == 37:
            c.px[x, y] = CHERRY[1] if ((x + y) // 2) % 2 == 0 else SNOW[1]
    # Hut: Waffeltuete, Spitze nach oben, Sahnerand
    hat = c.poly([(cx - 9, 19), (cx + 1, 1), (cx + 9, 19)])
    c.cyl_v(hat, cx, 9, WAFFLE, gloss=False)
    for x, y in hat:
        if (x + y) % 5 == 0 or (x - y + 50) % 5 == 0:
            c.px[x, y] = WAFFLE[1] if x < cx else WAFFLE[0]
    rim = [(x, y) for x, y in c.ellipse(cx, 19, 11, 3)]
    c.sphere(rim, cx - 1, 18, 11, 3.5, SNOW)
    for _ in range(4):
        x, y = rng.choice(rim)
        sprinkle(c, x, y, rng, set(rim))
    ch = c.ellipse(cx + 1.5, 2.5, 2.4, 2.2)
    c.sphere(ch, cx + 1, 2, 3, 3, CHERRY)
    c.outline()
    c.shadow(cx + 2, 58, 16, 4)
    return c, (cx, 58), (22, 10)


def gummibaer():
    c = Canvas(48, 50)
    # Eisblock: Vorderseite, Deckel, rechte Seite
    front = c.rect(5, 17, 35, 44)
    top = c.poly([(5, 17), (13, 8), (43, 8), (35, 17)])
    side = c.poly([(36, 17), (43, 9), (43, 36), (36, 44)])
    for x, y in side:
        c.px[x, y] = ICE[1] if (x + y) % 7 else ICE[2]
    for x, y in front:
        t = (y - 17) / 27
        c.px[x, y] = ICE[3] if t < 0.15 else ICE[2]
    # Gummibaer im Eis (farblich gedaempft)
    bear = set()
    bear |= set(c.ellipse(20, 26, 5, 4.5))           # Kopf
    bear |= set(c.ellipse(16, 22, 2, 2))              # Ohren
    bear |= set(c.ellipse(24.5, 22, 2, 2))
    bear |= set(c.ellipse(20.5, 35, 6.5, 6))          # Bauch
    bear |= set(c.ellipse(13.5, 31, 2.4, 2.4))        # Arme
    bear |= set(c.ellipse(27.5, 30, 2.4, 2.4))
    bear |= set(c.ellipse(15.5, 41, 2.8, 2.2))        # Fuesse
    bear |= set(c.ellipse(25.5, 41, 2.8, 2.2))
    for x, y in bear:
        nx, ny = (x - 20) / 9, (y - 31) / 12
        d = -(nx * 0.7 + ny * 0.7)
        base = BEAR[3] if d > 0.35 else BEAR[2] if d > -0.3 else BEAR[1]
        c.px[x, y] = mix(base, ICE[2], 0.38)
    for x, y in ((18, 25), (22, 25)):
        c.px[x, y] = mix(BEAR[0], ICE[1], 0.3)
    c.px[20, 28] = mix(BEAR[0], ICE[1], 0.3)
    c.px[17, 32] = mix(BEAR[4], ICE[3], 0.3)
    c.px[18, 31] = mix(BEAR[4], ICE[3], 0.3)
    # Deckel mit Schnee
    for x, y in top:
        c.px[x, y] = ICE[3]
    snow = [(x, y) for x, y in top if y < 13 + 1.5 * math.sin(x * 0.7) and x > 9]
    c.flat(snow, SNOW, 3)
    for x, y in snow:
        if y <= 9:
            c.px[x, y] = SNOW[4]
    # Kanten + Glanz
    c.hline(5, 35, 17, ICE[4])
    c.vline(35, 17, 44, ICE[4])
    for k in range(9):
        c.px[8 + k, 41 - k * 2] = ICE[4]
        c.px[9 + k, 41 - k * 2] = ICE[4]
        if k < 5:
            c.px[13 + k, 42 - k * 2] = ICE[4]
    for k in range(6):
        c.px[29 + k // 2, 22 + k] = ICE[3]       # Riss
    c.hline(5, 35, 44, ICE[0])
    c.outline()
    c.shadow(25, 45, 23, 4)
    return c, (24, 45), (36, 12)


PROPS = {
    "waffeltanne": (waffeltanne, 4.0),
    "eiskugeln": (eiskugeln, 2.0),
    "eis_am_stiel": (eis_am_stiel, 1.4),
    "kandis": (kandis, 1.8),
    "schneemann": (schneemann, 0.7),
    "gummibaer": (gummibaer, 0.9),
}


# ---------------------------------------------------------------------------
# Glatteis-Pfuetzen

POND_FRAMES = 16
POND_FPS = 9.0
POND_SPECS = [
    # name, rx, ry (px), seed, Einschluss, Gewicht
    ("klein", 38, 24, 3, None, 3.0),
    ("mittel", 56, 34, 7, "kirsche", 2.4),
    ("gross", 78, 46, 11, "streusel", 1.4),
    ("lang", 96, 28, 17, None, 1.2),
]
RIM = 4


def pond_shape(seed):
    rng = random.Random(seed)
    a = [(rng.uniform(0.05, 0.11), k, rng.uniform(0, 6.28)) for k in (2, 3, 5)]

    def r(theta):
        return 1.0 + sum(amp * math.sin(k * theta + ph) for amp, k, ph in a)
    return r


def pond(name, rx, ry, seed, inclusion):
    r = pond_shape(seed)
    w, h = 2 * (rx + RIM) + 4, 2 * (ry + RIM) + 6
    cx, cy = w / 2, (ry + RIM) + 2
    rng = random.Random(seed * 3)

    def dist(x, y):
        dx, dy = x + 0.5 - cx, y + 0.5 - cy
        th = math.atan2(dy / ry, dx / rx)
        return math.hypot(dx / rx, dy / ry) / r(th), dx / rx, dy / ry

    base = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    px = base.load()
    rim_n = RIM / ((rx + ry) / 2)
    field = {}
    for y in range(h):
        for x in range(w):
            d, nx, ny = dist(x, y)
            field[(x, y)] = (d, nx, ny)
            if d <= 1.0:
                # Eis: Rand heller, Mitte tiefer
                if d > 0.9:
                    col = ICE[3]
                elif d < 0.5 and ((x + y) % 2 == 0 or d < 0.38):
                    col = ICE[1]
                else:
                    col = ICE[2]
                # Schatten des oberen Schneewalls
                if ny < 0 and d > 0.84 - 0.08 * ny:
                    col = ICE[1]
                px[x, y] = col
            elif d <= 1.0 + rim_n:
                t = (d - 1.0) / rim_n
                if t > 0.72:
                    col = SNOW[1]
                elif ny > 0:
                    col = SNOW[4] if t < 0.35 else SNOW[3]
                else:
                    col = SNOW[2] if t < 0.35 else SNOW[3]
                px[x, y] = col
            elif d <= 1.0 + rim_n * 1.9 and ny > -0.2:
                px[x, y] = SHADOW_SNOW
    inside = lambda x, y: 0 <= x < w and 0 <= y < h and field[(x, y)][0] <= 0.93  # noqa: E731

    # Einschluss unter dem Eis
    if inclusion == "kirsche":
        ix, iy = int(cx + rx * 0.25), int(cy + ry * 0.1)
        for x in range(ix - 4, ix + 5):
            for y in range(iy - 4, iy + 5):
                if (x - ix) ** 2 + (y - iy) ** 2 <= 14 and inside(x, y):
                    lit = (x - ix) + (y - iy) < -1
                    px[x, y] = mix(CHERRY[3] if lit else CHERRY[2], ICE[2], 0.42)
        for k in range(5):
            if inside(ix + k // 2, iy - 4 - k):
                px[ix + k // 2, iy - 4 - k] = mix(PISTA[1], ICE[2], 0.4)
    elif inclusion == "streusel":
        for _ in range(16):
            x, y = int(cx + rng.uniform(-0.6, 0.6) * rx), int(cy + rng.uniform(-0.5, 0.6) * ry)
            col = mix(rng.choice(SPRINKLES), ICE[2], 0.45)
            dx, dy = rng.choice(((1, 0), (0, 1), (1, 1)))
            for qx, qy in ((x, y), (x + dx, y + dy)):
                if inside(qx, qy):
                    px[qx, qy] = col

    # Risse: duenne helle Linien vom Rand nach innen
    for _ in range(2 if rx < 50 else 3):
        th = rng.uniform(0, 6.28)
        x = cx + math.cos(th) * rx * r(th) * 0.9
        y = cy + math.sin(th) * ry * r(th) * 0.9
        ang = th + math.pi + rng.uniform(-0.6, 0.6)
        for step in range(int(min(rx, ry) * rng.uniform(0.5, 0.9))):
            ang += rng.uniform(-0.35, 0.35)
            x += math.cos(ang)
            y += math.sin(ang) * 0.8
            ix, iy = int(x), int(y)
            if not inside(ix, iy):
                break
            px[ix, iy] = ICE[4] if step % 4 else ICE[3]
            if rng.random() < 0.08:
                bx, by = ix, iy
                for _ in range(rng.randint(2, 4)):
                    bx += rng.choice((-1, 1))
                    by += rng.choice((-1, 0, 1))
                    if inside(bx, by):
                        px[bx, by] = ICE[3]

    # feste Spiegelung: zwei kurze Schraegstriche oben links
    for k, (ox, oy, ln) in enumerate(((-0.45, -0.35, 0.35), (-0.3, -0.12, 0.22))):
        sx, sy = cx + ox * rx, cy + oy * ry
        for i in range(int(ln * rx)):
            x, y = int(sx + i), int(sy - i * 0.55)
            for t in (0, 1):
                if inside(x, y + t):
                    px[x, y + t] = ICE[4] if t == 0 else ICE[3]

    # Animation: Glanzstreifen wandert diagonal drueber, danach Funkeln
    sparkles = [(int(cx + rng.uniform(-0.6, 0.6) * rx), int(cy + rng.uniform(-0.5, 0.5) * ry), rng.randrange(POND_FRAMES))
                for _ in range(3 if rx < 50 else 5)]
    frames = []
    for f in range(POND_FRAMES):
        im = base.copy()
        fp = im.load()
        sweep = f / (POND_FRAMES * 0.6)
        pos = -1.6 + 3.2 * sweep
        if sweep <= 1.0:
            for y in range(h):
                for x in range(w):
                    d, nx, ny = field[(x, y)]
                    if d > 0.93:
                        continue
                    band = abs((nx + ny * 0.7) - pos)
                    if band < 0.07:
                        fp[x, y] = ICE[4]
                    elif band < 0.16 and (x + y) % 2 == 0:
                        fp[x, y] = mix(fp[x, y], ICE[4], 0.6)
        for sx, sy, start in sparkles:
            age = (f - start) % POND_FRAMES
            if age < 3 and inside(sx, sy):
                arm = 2 if age == 1 else 1
                fp[sx, sy] = (255, 255, 255, 255)
                for k in range(1, arm + 1):
                    for qx, qy in ((sx + k, sy), (sx - k, sy), (sx, sy + k), (sx, sy - k)):
                        if inside(qx, qy):
                            fp[qx, qy] = ICE[4]
        frames.append(im)

    # Polygon der Eisflaeche (2 px eingerueckt), in Einheiten relativ zur Mitte
    poly = []
    for i in range(28):
        th = 2 * math.pi * i / 28
        rr = r(th)
        x = math.cos(th) * (rx * rr - 2)
        y = math.sin(th) * (ry * rr - 2)
        poly.append([round(x / PPU, 4), round(-y / PPU, 4)])
    info = {
        "name": name, "frames": POND_FRAMES, "fps": POND_FPS, "collider": poly,
        "frameW": w, "frameH": h, "pivotX": cx, "pivotY": h - cy,
        "halfW": round((rx + RIM) * 1.2 / PPU, 3), "halfH": round((ry + RIM) * 1.2 / PPU, 3),
    }
    return frames, info


def render_ponds():
    out = []
    for name, rx, ry, seed, inc, weight in POND_SPECS:
        frames, info = pond(name, rx, ry, seed, inc)
        info["weight"] = weight
        out.append((frames, info))
    return out


def render_props():
    out = {}
    for name, (fn, weight) in PROPS.items():
        c, foot, size = fn()
        out[name] = (c.img, foot, size, weight)
    return out


def preview(props, ponds, path, scale=3):
    """Props und Pfuetzen auf Schneeboden, Keks als Massstab."""
    import importlib.util
    spec = importlib.util.spec_from_file_location("eb", os.path.join(os.path.dirname(__file__), "eis_boden.py"))
    eb = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(eb)
    sheet, _ = eb.build()
    cols, rows = 16, 9
    floor = Image.new("RGBA", (cols * 32, rows * 32))
    for r_ in range(rows):
        for c_ in range(cols):
            idx = (r_ * 7 + c_ * 3) % 4
            floor.paste(sheet.crop((idx * 32, 0, idx * 32 + 32, 32)), (c_ * 32, r_ * 32))
    x = 6
    for frames, info in ponds:
        fr = frames[4]
        floor.alpha_composite(fr, (x, 6))
        x += fr.width + 6
    x = 8
    for n, (img, foot, _, _) in props.items():
        floor.alpha_composite(img, (x, 270 - foot[1]))
        x += img.width + 10
    keks = os.path.join(ROOT, "Assets", "Art", "Chars", "Char_Keks.png")
    if os.path.exists(keks):
        k = Image.open(keks).convert("RGBA").crop((0, 0, 40, 40))
        floor.alpha_composite(k, (cols * 32 - 50, 270 - 36))
        floor.alpha_composite(k, (60, 40))
    floor.resize((floor.width * scale, floor.height * scale), Image.NEAREST).save(path)


def main():
    props = render_props()
    ponds = render_ponds()
    if "--preview" in sys.argv:
        preview(props, ponds, sys.argv[sys.argv.index("--preview") + 1])
        return
    if "--gif" in sys.argv:
        frames, _ = ponds[int(sys.argv[sys.argv.index("--gif") + 2]) if len(sys.argv) > sys.argv.index("--gif") + 2 else 2]
        big = [f.resize((f.width * 3, f.height * 3), Image.NEAREST) for f in frames]
        big[0].save(sys.argv[sys.argv.index("--gif") + 1], save_all=True, append_images=big[1:],
                    duration=int(1000 / POND_FPS), loop=0, disposal=2)
        return
    os.makedirs(OUT_DIR, exist_ok=True)
    pinfo = []
    for name, (img, foot, size, weight) in props.items():
        path = os.path.join(OUT_DIR, "eis_%s.png" % name)
        img.save(path)
        if not os.path.exists(path + ".meta"):
            pivot = (round(foot[0] / img.width, 4), round(1 - foot[1] / img.height, 4))
            write_strip_meta(path + ".meta", "eis_" + name, 1, img.width, img.height, PPU, pivot=pivot)
        pinfo.append({"name": name, "colliderW": size[0], "colliderH": size[1], "weight": weight,
                      "frameW": img.width, "frameH": img.height, "pivotX": foot[0], "pivotY": img.height - foot[1]})
    oinfo = []
    for frames, info in ponds:
        w, h = info["frameW"], info["frameH"]
        strip = Image.new("RGBA", (w * len(frames), h), (0, 0, 0, 0))
        for i, f in enumerate(frames):
            strip.paste(f, (i * w, 0))
        path = os.path.join(OUT_DIR, "eis_glatteis_%s.png" % info["name"])
        strip.save(path)
        if not os.path.exists(path + ".meta"):
            pivot = (round(info["pivotX"] / w, 4), round(info["pivotY"] / h, 4))
            write_strip_meta(path + ".meta", "eis_glatteis_" + info["name"], len(frames), w, h, PPU,
                             pivot=pivot, max_size=4096)
        oinfo.append(info)
    with open(INFO, "w", encoding="utf-8", newline="\n") as fh:
        json.dump({"props": pinfo, "ponds": oinfo}, fh, indent=1)
    print("geschrieben:", len(pinfo), "Props +", len(oinfo), "Glatteis nach", OUT_DIR)


if __name__ == "__main__":
    main()
