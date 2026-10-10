"""
Zeichnet die Hindernisse des Lebkuchen-Geisterwalds (Level 4, Szene Map_World6):
ein naechtlicher Wald aus Lebkuchen, Lakritz und Zuckerguss, in dem Kuerbisse,
Laternen, Kerzen und Pilze leuchten.

  Assets/Art/World-Objects/Geist/geist_<name>.png        Grundbild, 1 Sprite, PPU 32 (beleuchtet)
  Assets/Art/World-Objects/Geist/geist_<name>_glow.png   Leuchtpixel, Streifen, PPU 32 (unbeleuchtet)
  Assets/Art/World-Objects/Geist/geist_props.json        Fuesse, Lichter, Masse fuer GeistBuilder/geist_boden.py

Die Welt ist nachts dunkel (Geisterwald.cs dimmt das globale 2D-Licht). Alles,
was selbst leuchtet - Kerzenflammen, geschnitzte Kuerbisgesichter, Pilzhuete,
der Trank im Kessel - liegt zusaetzlich in einem eigenen Glow-Bild, das der
GeistBuilder mit Sprite-Unlit-Default darueberlegt. Dazu kommt je Leuchtprop
ein Light2D (Farbe/Radius/Staerke aus der json), das den Boden drumherum
anmalt und flackert (LichtFlackern.cs).

Stehend (Pivot = Mitte des Bodenschattens, SpriteSortPoint Pivot, Box am Fuss):
  lebkuchenbaum  knorriger Lebkuchenbaum, Zuckerguss tropft von den Aesten
  spukbaum       derselbe Baum mit geschnitztem, gluehendem Gesicht
  lakritzbaum    Trauerweide aus gedrehter Lakritze, Lakritzschnuere haengen herab
  grabstein      runder Lebkuchen-Grabstein mit Zuckerguss-Rand und Biss
  grabkreuz      schiefes Keks-Kreuz mit Streuseln
  kuerbis        Kuerbislaterne, Gesicht glueht
  laterne        Laterne an einem Lakritzpfahl, Zuckerglas-Scheiben
  kessel         Hexenkessel, gruener Trank blubbert (Glow animiert)
  kerzen         Kerzengruppe aus lila Wachs, tropft
  pilze          Baiser-Pilze mit leuchtenden Hueten
  zaun           kaputter Zuckerstangenzaun mit Lakritzseil

Flach (Background-Layer, kein Hindernis):
  laub           Haufen Gummi-Herbstblaetter
  pilzring       Hexenring aus winzigen Leuchtpilzen
  pfuetze        Pfuetze, in der sich der Mond spiegelt

Stil wie Tools/kueche_props.py / eis_props.py (Canvas, Licht oben links, warme Kontur).

Aufruf aus dem Projektordner:  python Tools/geist_props.py [--preview pfad.png] [--gif pfad.gif name]
Die .meta wird nur beim ersten Mal geschrieben (sonst verlieren Prefabs ihre Verweise).
"""

import json
import math
import os
import random
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kueche_props import Canvas, L, hx, mix, pal, tone_of  # noqa: E402
from unity_meta import write_strip_meta  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "Assets", "Art", "World-Objects", "Geist")
INFO = os.path.join(OUT_DIR, "geist_props.json")
PPU = 32

# --- Paletten (deep, shade, base, light, hi) --------------------------------------
GINGER = pal("#4e2616", "#7a4024", "#a65e30", "#c8833f", "#e6ad66")
ICING = pal("#8e8cb2", "#bdbdd8", "#e2e2f0", "#f4f4fb", "#ffffff")
LICO = pal("#0e0a14", "#1e1628", "#33263f", "#4c3a5c", "#7c6694")
LICO_RED = pal("#3c0a14", "#64121f", "#8e1e2c", "#b8323e", "#e0646a")
PUMPKIN = pal("#5e220a", "#983c16", "#cc6424", "#ec913c", "#ffc874")
STEM = pal("#25301a", "#3a4a24", "#55692f", "#768c40", "#a6ba68")
MERINGUE = pal("#6e6888", "#9e98b8", "#cdc9e0", "#e9e7f4", "#ffffff")
IRON = pal("#100c14", "#1e1824", "#302838", "#4a4054", "#7e7290")
WAX = pal("#3a1c4e", "#5a2e74", "#82469e", "#a86cc2", "#d4a8e6")
CANE_R = pal("#5a0e1c", "#8a1a2c", "#b42c3a", "#d8505a", "#f49898")
CANE_W = pal("#8a8098", "#b4aec4", "#dcd8e6", "#f0eef6", "#ffffff")
SOIL = pal("#1c1218", "#2c1d24", "#3e2a30", "#55393c", "#6e4c4a")
LEAVES = [pal("#5a1a0c", "#8e2c12", "#c4481c", "#e6702e", "#ffa45a"),
          pal("#5e3608", "#94580e", "#c8861a", "#e8ae36", "#ffd870"),
          pal("#4c0c18", "#7c1626", "#aa2636", "#d04a50", "#f28078")]
SPRINKLES = [hx("#ff6f9a"), hx("#ffd34e"), hx("#6fe0b6"), hx("#c88cff"), hx("#ffffff")]

# Leuchtfarben (stehen unbeleuchtet im Glow-Bild)
FIRE = [hx("#c2410e"), hx("#f07a1c"), hx("#ffb83c"), hx("#ffe58a"), hx("#fffbe0")]
CYAN = [hx("#1c6e78"), hx("#2fa8a8"), hx("#5ee0cc"), hx("#a8fff0"), hx("#f0fffc")]
VIOLET = [hx("#4a2a8a"), hx("#7a48c8"), hx("#b07cf4"), hx("#dcbcff"), hx("#fbf4ff")]
POTION = [hx("#1e5e1a"), hx("#36962a"), hx("#62d03c"), hx("#a8f46a"), hx("#eaffc8")]
MOON = [hx("#8a8aa0"), hx("#c8c4b0"), hx("#ece4c0"), hx("#fff8de")]

SHADOW_DARK = (14, 8, 20, 110)
HOLE = hx("#160806")

# Lichter (Farbe 0..1, Radius/Hoehe in Einheiten, Hoehe = ueber dem Pivot)
LIGHT_FIRE = (1.0, 0.62, 0.26)
LIGHT_CANDLE = (1.0, 0.78, 0.42)
LIGHT_CYAN = (0.36, 0.95, 0.88)
LIGHT_VIOLET = (0.72, 0.5, 1.0)
LIGHT_POTION = (0.5, 1.0, 0.35)
LIGHT_MOON = (0.75, 0.78, 1.0)


def light(col, radius, intensity, y, x=0.0, flicker=0.15, inner=0.0):
    return {"r": col[0], "g": col[1], "b": col[2], "radius": radius, "inner": inner,
            "intensity": intensity, "x": round(x, 4), "y": round(y, 4), "flicker": flicker}


def glow_canvas(c):
    return Canvas(c.w, c.h)


def shadow(c, cx, cy, rx, ry, col=SHADOW_DARK):
    for x, y in c.ellipse(cx, cy, rx, ry):
        if c.px[x, y][3] == 0:
            c.px[x, y] = col


def sprinkle(c, x, y, rng, on=None):
    col = rng.choice(SPRINKLES)
    dx, dy = rng.choice(((1, 0), (0, 1), (1, 1)))
    for px, py in ((x, y), (x + dx, y + dy)):
        if 0 <= px < c.w and 0 <= py < c.h and c.px[px, py][3] == 255:
            if on is None or (px, py) in on:
                c.px[px, py] = col


# ---------------------------------------------------------------------------
# Roehren: Aeste, Wurzeln, Pfaehle. Jede Roehre ist eine Kette aus Kreisen;
# je Pixel gewinnt die Mitte mit dem kleinsten normierten Abstand - das gibt
# eine runde Normale quer zur Roehre, die wir wie eine Kugel beleuchten.

class Tubes:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.best = {}

    def seg(self, x0, y0, r0, x1, y1, r1, tag=0):
        n = int(max(abs(x1 - x0), abs(y1 - y0)) * 3) + 1
        for i in range(n + 1):
            t = i / n
            cx, cy = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
            r = max(0.6, r0 + (r1 - r0) * t)
            for y in range(int(cy - r - 1), int(cy + r + 2)):
                for x in range(int(cx - r - 1), int(cx + r + 2)):
                    if not (0 <= x < self.w and 0 <= y < self.h):
                        continue
                    dx, dy = x + 0.5 - cx, y + 0.5 - cy
                    d = math.sqrt(dx * dx + dy * dy) / r
                    if d <= 1.0:
                        cur = self.best.get((x, y))
                        if cur is None or d < cur[0]:
                            self.best[(x, y)] = (d, dx / r, dy / r, tag, t, cy)

    def path(self, pts, tag=0):
        """pts: [(x, y, r), ...]"""
        for a, b in zip(pts, pts[1:]):
            self.seg(a[0], a[1], a[2], b[0], b[1], b[2], tag)

    def shade(self, c, p, tags=None, bias=0.0):
        out = []
        for (x, y), (d, nx, ny, tag, _, _) in self.best.items():
            if tags is not None and tag not in tags:
                continue
            nz = math.sqrt(max(0.0, 1 - nx * nx - ny * ny))
            v = nx * L[0] + ny * L[1] + nz * L[2] + bias
            c.px[x, y] = tone_of(v, p, gloss=False)
            out.append((x, y))
        return out


def branch_path(rng, x, y, ang, length, r0, r1, wiggle=0.25, curl=0.0, step=3.0):
    pts = [(x, y, r0)]
    n = max(2, int(length / step))
    a = ang
    for i in range(1, n + 1):
        t = i / n
        a += rng.uniform(-wiggle, wiggle) + curl * t
        x += math.cos(a) * step
        y += math.sin(a) * step
        pts.append((x, y, r0 + (r1 - r0) * t))
    return pts, a


def icing_on_top(c, tubes, rng, tags, level=-0.38, drip=0.35):
    """Zuckerguss auf allem, was nach oben zeigt, mit Nasen nach unten."""
    s = set(tubes.best.keys())
    cap = set()
    for (x, y), (d, nx, ny, tag, t, _) in tubes.best.items():
        if tag in tags and ny < level + 0.12 * math.sin(x * 0.9 + y * 0.3):
            cap.add((x, y))
    # Nasen: von der Unterkante des Gusses 1..4 px weiter herunter
    for (x, y) in list(cap):
        if (x, y + 1) not in cap and (x, y + 1) in s and rng.random() < drip:
            for k in range(1, rng.randint(2, 4)):
                if (x, y + k) in s:
                    cap.add((x, y + k))
    for (x, y) in cap:
        up = (x, y - 1) not in cap
        down = (x, y + 1) not in cap
        if up:
            c.px[x, y] = ICING[4] if (x + y) % 3 else ICING[3]
        elif down:
            c.px[x, y] = ICING[1]
        else:
            c.px[x, y] = ICING[3] if x % 4 else ICING[2]
    return cap


def bark(c, pts, rng, p=GINGER, pores=0.05):
    """Lebkuchen-Poren: kleine dunkle Punkte, ab und zu ein heller Krumen."""
    for x, y in pts:
        r = rng.random()
        if r < pores:
            c.px[x, y] = p[1] if c.px[x, y] != p[0] else p[0]
        elif r < pores + 0.012:
            c.px[x, y] = p[3]


# ---------------------------------------------------------------------------
# Baeume

def grow(tb, rng, x, y, ang, length, r0, depth, curl, tag=1):
    """Ast mit Zweigen; Spitzen rollen sich ein wie Hexenfinger."""
    r1 = max(0.75, r0 * 0.42)
    pts, a_end = branch_path(rng, x, y, ang, length, r0, r1, wiggle=0.14, curl=curl, step=2.6)
    tb.path(pts, tag)
    if depth > 0:
        for frac, side in ((0.45, -1 if rng.random() < 0.5 else 1), (0.72, 0)):
            j = max(1, min(len(pts) - 2, int(len(pts) * frac)))
            bx, by, br = pts[j]
            sd = side if side else (1 if rng.random() < 0.5 else -1)
            grow(tb, rng, bx, by, ang + sd * rng.uniform(0.55, 0.85), length * rng.uniform(0.45, 0.6),
                 max(0.9, br * 0.75), depth - 1, -sd * 0.22, tag)
    else:
        ex, ey, _ = pts[-1]
        roll, _ = branch_path(rng, ex, ey, a_end, 6, r1, 0.6, wiggle=0.0,
                              curl=(0.85 if math.cos(a_end) > 0 else -0.85), step=1.4)
        tb.path(roll, tag)
    return pts


def gingerbread_tree(face):
    c = Canvas(92, 112)
    g = glow_canvas(c)
    rng = random.Random(44 if face else 7)
    cx, base = 46, 104
    tb = Tubes(c.w, c.h)

    # Wurzeln: krallen sich nach links und rechts in den Boden
    for side, ln, r in ((-1, 16, 4.0), (1, 14, 3.8), (-1, 9, 2.6), (1, 10, 2.8)):
        pts, _ = branch_path(rng, cx + side * 2, base - 7, math.pi / 2 + side * (-1.2 if ln > 12 else -0.75),
                             ln, r, 1.0, wiggle=0.1, curl=side * 0.14, step=2.4)
        tb.path(pts, tag=2)

    # Stamm: S-Kurve, unten dick, oben schmal
    trunk = [(cx, base - 3, 7.5), (cx - 1.5, base - 16, 6.2), (cx + 1.0, base - 30, 5.6),
             (cx + 2.0, base - 42, 4.6), (cx + 0.5, base - 52, 3.8), (cx - 1.0, base - 60, 3.0)]
    tb.path(trunk, tag=0)

    # Krone: weit ausladend, links/rechts fast waagerecht, Spitzen hoch
    tops = [(-3.0, 30, 3.0, 0.20), (-2.45, 30, 3.2, 0.10), (-1.75, 26, 3.0, 0.05),
            (-1.05, 30, 3.1, -0.08), (-0.25, 30, 2.9, -0.22)]
    if face:
        tops = [(-2.9, 32, 3.2, 0.22), (-2.2, 26, 3.0, 0.08), (-1.45, 28, 3.0, -0.04),
                (-0.7, 32, 3.1, -0.14)]
    for k, (ang, ln, r0, curl) in enumerate(tops):
        t = k / max(1, len(tops) - 1)
        sx = cx - 1.5 + t * 3.5
        sy = base - 58 + abs(t - 0.5) * 14
        grow(tb, rng, sx, sy, ang, ln, r0, 2 if ln > 27 else 1, curl)
    # ein knorriger Seitenast tiefer am Stamm
    side = -1
    grow(tb, rng, cx + side * 3, base - 44, -math.pi / 2 + side * 1.25, 18, 2.6, 1, -side * 0.25)

    allpts = tb.shade(c, GINGER, bias=0.05)
    bark(c, allpts, rng)
    s = set(allpts)

    # Maserung: geschwungene Rillen am Stamm
    for k in range(7):
        x0 = cx - 5 + k * 1.7 + rng.uniform(-0.6, 0.6)
        y0 = rng.randint(base - 56, base - 24)
        for y in range(y0, y0 + rng.randint(5, 12)):
            x = int(round(x0 + math.sin(y * 0.25 + k) * 1.0))
            q = tb.best.get((x, y))
            if q and q[3] == 0 and abs(q[1]) < 0.7:
                c.px[x, y] = GINGER[1]
    # Astloch
    if not face:
        hx_, hy = cx + 1, base - 30
        for x, y in c.ellipse(hx_, hy, 2.2, 3.0):
            if (x, y) in s:
                c.px[x, y] = HOLE
        for x in range(hx_ - 2, hx_ + 2):
            c.px[x, hy - 3] = GINGER[4]

    # Zuckerguss: tropft von allem, was nach oben zeigt
    icing_on_top(c, tb, rng, tags=(1, 2), level=-0.45, drip=0.4)

    if face:
        fy = base - 27
        fx = cx - 1
        holes = []
        # Kuerbisgesicht: schraege Dreiecksaugen, Grinsen mit Zaehnen
        eye = [(-5, -2), (-4, -2), (-5, -1), (-4, -1), (-3, -1), (-4, 0), (-3, 0), (-2, 0)]
        for dx, dy in eye:
            holes.append((fx + dx, fy + dy))
            holes.append((fx - dx, fy + dy))
        mouth = [(-5, 3), (5, 3)] + [(dx, 4) for dx in range(-5, 6) if dx not in (-2, 2)] +                 [(dx, 5) for dx in range(-4, 5) if dx != 0] + [(dx, 6) for dx in range(-2, 3)]
        for dx, dy in mouth:
            holes.append((fx + dx, fy + dy))
        holes = [q for q in holes if q in s]
        hs = carve_glow(c, g, holes)
        for x, y in holes:
            for q in ((x, y + 1), (x, y - 1), (x - 1, y), (x + 1, y)):
                if q in s and q not in hs:
                    c.px[q[0], q[1]] = GINGER[4]
                    g.px[q[0], q[1]] = (255, 190, 110, 110)

    c.outline()
    shadow(c, cx + 2, base - 1, 24, 5)
    lit = light(LIGHT_FIRE, 2.6, 0.6, 26 / PPU, flicker=0.25) if face else None
    return {"c": c, "foot": (cx, base - 1), "size": (18, 10), "glow": [g] if face else None, "light": lit}


def lebkuchenbaum():
    return gingerbread_tree(False)


def spukbaum():
    return gingerbread_tree(True)


def lakritzbaum():
    c = Canvas(96, 108)
    rng = random.Random(19)
    cx, base = 48, 102
    tb = Tubes(c.w, c.h)
    for side, ln in ((-1, 10), (1, 11)):
        pts, _ = branch_path(rng, cx, base - 5, math.pi / 2 - side * 1.2, ln, 3.6, 1.2, wiggle=0.1, step=2.5)
        tb.path(pts, tag=2)
    trunk = [(cx, base - 2, 6.0), (cx + 1.5, base - 20, 4.8), (cx - 1, base - 40, 4.2), (cx, base - 62, 3.6)]
    tb.path(trunk, tag=0)
    # Schirmkrone: Boegen steigen nach aussen und haengen dann herab
    arcs = []
    specs = [(-2.85, 42, -0.3), (-2.4, 38, -0.36), (-1.95, 28, -0.5), (-1.2, 28, 0.5),
             (-0.75, 38, 0.36), (-0.3, 42, 0.3), (-1.57, 12, 0.0)]
    for k, (ang, ln, curl) in enumerate(specs):
        sx, sy = cx + (k - 3) * 0.6, base - 60 - (k % 2) * 2
        pts, _ = branch_path(rng, sx, sy, ang, ln, 2.8, 1.1, wiggle=0.05, curl=curl, step=2.5)
        tb.path(pts, tag=1)
        arcs.append(pts)

    # Lakritzschnuere haengen bis fast zum Boden, ein paar rote dazwischen
    occupied = set(tb.best.keys())
    strands = []
    for pts in arcs:
        for j in range(1, len(pts)):
            x, y, r = pts[j]
            if rng.random() < 0.5 or abs(x - cx) < 5:
                continue
            room = (base - 14) - y
            ln = int(room * rng.uniform(0.35, 0.95))
            if ln < 6:
                continue
            strands.append((x + rng.uniform(-0.6, 0.6), y + r * 0.5, ln, rng.random() < 0.16, rng.random() * 6))
    strands.sort(key=lambda q: -q[1])
    for sx, sy, ln, red, ph in strands:
        p = LICO_RED if red else LICO
        x, y = int(sx), int(sy)
        for k in range(ln):
            y = int(sy + k)
            x = int(round(sx + math.sin(k * 0.1 + ph) * 1.6 * (k / ln)))
            if not (0 <= x < c.w and 0 <= y < c.h - 4):
                break
            col = p[3] if k % 6 == 2 else p[2] if k % 2 else p[1]
            c.px[x, y] = col
        # Ende: kleines Lakritz-Tropfchen
        if 0 <= x < c.w and y + 1 < c.h:
            c.px[x, y] = p[4]
            c.px[x, y + 1] = p[2]
    # gedrehte Lakritze: Spiralstreifen ueber die Roehre
    for (x, y), (d, nx, ny, tag, t, cyy) in tb.best.items():
        nz = math.sqrt(max(0.0, 1 - nx * nx - ny * ny))
        v = nx * L[0] + ny * L[1] + nz * L[2] + 0.12
        stripe = math.floor((y * 0.55 + nx * 3.2 + (x * 0.3 if tag == 1 else 0)) / 1.7) % 2
        c.px[x, y] = tone_of(v - (0.28 if stripe else 0.0), LICO, gloss=True)

    # Lakritz-Konfekt-Perlen in der Krone
    allsort = [hx("#ff6fa8"), hx("#ffe060"), hx("#6fd8ff"), hx("#f4f4f4")]
    crown = [q for q in tb.best if tb.best[q][3] == 1]
    for _ in range(9):
        x, y = rng.choice(crown)
        col = rng.choice(allsort)
        for q in ((x, y), (x + 1, y), (x, y + 1), (x + 1, y + 1)):
            if q in occupied:
                c.px[q[0], q[1]] = col if q != (x + 1, y + 1) else mix(col, (0, 0, 0, 255), 0.35)
    c.outline()
    shadow(c, cx + 2, base - 1, 28, 5)
    return {"c": c, "foot": (cx, base - 1), "size": (14, 10)}


# ---------------------------------------------------------------------------
# Grabsteine

P_GLYPH = ["11.", "1.1", "11.", "1..", "1.."]
R_GLYPH = ["11.", "1.1", "11.", "1.1", "1.1"]
I_GLYPH = ["1", "1", "1", "1", "1"]


def glyphs(c, x, y, rows_list, col, mask=None):
    for g in rows_list:
        for gy, row in enumerate(g):
            for gx, v in enumerate(row):
                if v == "1" and (mask is None or (x + gx, y + gy) in mask):
                    c.px[x + gx, y + gy] = col
        x += len(g[0]) + 1


def mound(c, cx, cy, rx, ry, rng):
    pts = c.ellipse(cx, cy, rx, ry)
    c.sphere(pts, cx - 2, cy - 2, rx * 1.2, ry * 1.6, SOIL, gloss=False)
    for _ in range(6):
        x, y = rng.choice(pts)
        lp = rng.choice(LEAVES)
        c.px[x, y] = lp[3]
        if x + 1 < c.w:
            c.px[x + 1, y] = lp[2]
    return pts


def piping(c, body, inset, rng, dotted=True):
    """Zuckerguss-Linie 'inset' Pixel innerhalb der Kontur - wie bei Lebkuchenmaennern."""
    s = set(body)

    def depth(x, y):
        for k in range(1, inset + 2):
            for qx, qy in ((x + k, y), (x - k, y), (x, y + k), (x, y - k)):
                if (qx, qy) not in s:
                    return k
        return inset + 2

    line = [(x, y) for x, y in body if depth(x, y) == inset]
    for i, (x, y) in enumerate(sorted(line, key=lambda q: math.atan2(q[1] - 20, q[0] - 14))):
        if dotted and (x + y) % 3 == 0:
            continue
        c.px[x, y] = ICING[4] if (x + y) % 2 else ICING[3]
    return line


def grabstein():
    c = Canvas(36, 40)
    rng = random.Random(5)
    cx = 18
    mound(c, cx, 33, 14, 5, rng)
    body = [(x, y) for x, y in c.rect(cx - 10, 4, cx + 9, 33)
            if y >= 14 or ((x + 0.5 - cx) / 10.2) ** 2 + ((y - 14) / 10.5) ** 2 <= 1]
    # Biss oben rechts
    bite = c.ellipse(cx + 9, 7, 4.5, 4.5) + c.ellipse(cx + 6, 4, 3.4, 3.2)
    bs = set(bite)
    body = [q for q in body if q not in bs]
    c.cyl_v(body, cx - 1, 11, GINGER, gloss=False)
    bark(c, body, rng, pores=0.06)
    s = set(body)
    # Bissrand: hellerer Teig innen
    for x, y in body:
        if any(q in bs for q in ((x + 1, y), (x, y - 1), (x + 1, y - 1))):
            c.px[x, y] = GINGER[4] if (x + y) % 2 else GINGER[3]
    # Seitenflaeche: Keks ist 3 px dick
    for x, y in body:
        if (x + 1, y) not in s or (x + 2, y) not in s:
            c.px[x, y] = GINGER[1] if (x + 1, y) not in s else GINGER[2]
    piping(c, body, 2, rng)
    glyphs(c, cx - 6, 15, [R_GLYPH, I_GLYPH, P_GLYPH], ICING[4], mask=s)
    # Streusel unter der Schrift
    for _ in range(4):
        x, y = rng.choice([q for q in body if q[1] > 23])
        sprinkle(c, x, y, rng, s)
    c.outline()
    # Kruemel vom Biss liegen auf dem Huegel
    for x, y in ((cx + 12, 31), (cx + 13, 32), (cx + 10, 33), (cx + 14, 30)):
        c.px[x, y] = GINGER[3]
    shadow(c, cx + 2, 34, 15, 4)
    return {"c": c, "foot": (cx, 34), "size": (20, 9)}


def grabkreuz():
    c = Canvas(34, 42)
    rng = random.Random(9)
    cx = 17
    mound(c, cx, 36, 12, 4.5, rng)
    # Kreuz, leicht nach rechts gekippt
    ang = math.radians(-9)

    def rot(x, y, ox=cx, oy=34):
        dx, dy = x - ox, y - oy
        return (ox + dx * math.cos(ang) - dy * math.sin(ang), oy + dx * math.sin(ang) + dy * math.cos(ang))

    vert = [rot(cx - 3.5, 3), rot(cx + 3.5, 3), rot(cx + 3.5, 36), rot(cx - 3.5, 36)]
    hor = [rot(cx - 11, 11), rot(cx + 11, 11), rot(cx + 11, 18), rot(cx - 11, 18)]
    body = list(set(c.poly(vert)) | set(c.poly(hor)))
    c.cyl_v(body, cx - 2, 10, GINGER, gloss=False)
    bark(c, body, rng, pores=0.05)
    s = set(body)
    for x, y in body:
        if (x + 1, y) not in s:
            c.px[x, y] = GINGER[1]
        elif (x, y + 1) not in s:
            c.px[x, y] = GINGER[0]
    piping(c, body, 2, rng, dotted=False)
    for _ in range(6):
        x, y = rng.choice(body)
        sprinkle(c, x, y, rng, s)
    c.outline()
    shadow(c, cx + 2, 37, 13, 3.5)
    return {"c": c, "foot": (cx, 37), "size": (12, 8)}


# ---------------------------------------------------------------------------
# Leuchtende Props

def carve_glow(c, g, holes):
    hs = set(holes)
    for x, y in holes:
        c.px[x, y] = HOLE
    for x, y in holes:
        edge = sum((q not in hs) for q in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))
        if edge >= 3:
            col = FIRE[2]
        elif edge == 2:
            col = FIRE[2] if (x, y - 1) in hs else FIRE[1]
        elif (x, y - 1) not in hs:
            col = FIRE[3]
        else:
            col = FIRE[4] if (x + y) % 2 else FIRE[3]
        g.px[x, y] = col
    return hs


def kuerbis():
    c = Canvas(38, 34)
    g = glow_canvas(c)
    rng = random.Random(3)
    cx, cy = 19, 19
    rx, ry = 15, 11
    body = c.ellipse(cx, cy, rx, ry)
    # Rippen: fuenf Schnitze, jeder ein eigener kleiner Zylinder
    lobes = 5
    for x, y in body:
        u = (x + 0.5 - (cx - rx)) / (2 * rx)
        k = min(lobes - 1, int(u * lobes))
        lc = cx - rx + (k + 0.5) * (2 * rx / lobes)
        lw = rx / lobes * 1.25
        nx = max(-1.0, min(1.0, (x + 0.5 - lc) / lw))
        gx = (x + 0.5 - cx) / rx
        ny = (y + 0.5 - cy) / ry
        nz = math.sqrt(max(0.0, 1 - nx * nx * 0.6 - ny * ny * 0.8))
        v = (nx * 0.55 + gx * 0.45) * L[0] + ny * L[1] + nz * L[2]
        col = tone_of(v, PUMPKIN, gloss=True)
        if abs(nx) > 0.92:
            col = PUMPKIN[1]
        c.px[x, y] = col
    s = set(body)
    # Stiel + Ranke + Blatt
    stem = c.poly([(cx - 2, 3), (cx + 2, 2), (cx + 3, 9), (cx - 2, 10)])
    c.cyl_v(stem, cx, 3, STEM, gloss=False)
    c.arc(cx + 6, 6, 3, 2.5, 180, 470, STEM[3])
    leaf = c.ellipse(cx - 6, 7, 4, 2.2)
    c.sphere(leaf, cx - 7, 6, 4, 3, STEM)
    # Gesicht: dreieckige Augen, Nase, Zackenmund
    holes = []
    holes += c.poly([(cx - 9, 17), (cx - 3, 17), (cx - 6, 12)])
    holes += c.poly([(cx + 3, 17), (cx + 9, 17), (cx + 6, 12)])
    holes += c.poly([(cx - 1, 20), (cx + 1, 20), (cx, 18)])
    mouth = []
    for x in range(cx - 9, cx + 10):
        t = (x - cx) / 9.5
        top_y = 22 + int(round(1.8 * t * t)) * -1 + 1
        bot_y = 26 - int(round(2 * t * t))
        if x % 4 == 0:
            top_y += 2       # Zahn oben
        if x % 4 == 2 and abs(x - cx) < 6:
            bot_y -= 2       # Zahn unten
        for y in range(top_y, bot_y + 1):
            mouth.append((x, y))
    holes += mouth
    holes = [q for q in holes if q in s]
    hs = carve_glow(c, g, holes)
    # Schnittkante: helles Fruchtfleisch um die Loecher
    for x, y in holes:
        for q in ((x, y - 1), (x - 1, y), (x + 1, y)):
            if q in s and q not in hs:
                c.px[q[0], q[1]] = PUMPKIN[4]
                g.px[q[0], q[1]] = (255, 214, 140, 150)
    c.outline()
    shadow(c, cx + 2, 30, 15, 3.5)
    return {"c": c, "foot": (cx, 30), "size": (24, 10), "glow": [g],
            "light": light(LIGHT_FIRE, 3.2, 0.7, 10 / PPU, flicker=0.3)}


def laterne():
    c = Canvas(30, 72)
    g = glow_canvas(c)
    rng = random.Random(8)
    px_, base = 10, 68
    tb = Tubes(c.w, c.h)
    tb.path([(px_, base, 2.6), (px_, base - 30, 2.2), (px_, base - 54, 2.0), (px_ + 1, base - 60, 1.9)], tag=0)
    # Arm mit Haken
    tb.path([(px_ + 1, base - 58, 1.5), (px_ + 6, base - 62, 1.4), (px_ + 12, base - 61, 1.3),
             (px_ + 15, base - 58, 1.1)], tag=1)
    for (x, y), (d, nx, ny, tag, t, cyy) in tb.best.items():
        nz = math.sqrt(max(0.0, 1 - nx * nx - ny * ny))
        v = nx * L[0] + ny * L[1] + nz * L[2] + 0.12
        stripe = math.floor((y * 0.6 + nx * 2.5) / 1.6) % 2 if tag == 0 else 0
        c.px[x, y] = tone_of(v - (0.25 if stripe else 0), LICO, gloss=True)
    # Kette
    for y in range(base - 57, base - 53):
        c.px[px_ + 15, y] = IRON[3] if y % 2 else IRON[2]
    # Laterne: Dach, Glas, Boden
    lx, ly = px_ + 15, base - 46       # Mitte der Scheiben
    roof = c.poly([(lx - 6, ly - 6), (lx + 6, ly - 6), (lx + 1, ly - 11), (lx - 1, ly - 11)])
    c.flat(roof, IRON, 2)
    for x, y in roof:
        if (x, y - 1) not in set(roof):
            c.px[x, y] = IRON[4]
    c.hline(lx - 7, lx + 6, ly - 6, IRON[3])
    c.px[lx, ly - 12] = IRON[3]
    glass = c.rect(lx - 5, ly - 5, lx + 4, ly + 5)
    frame = set()
    for x, y in glass:
        if x in (lx - 5, lx + 4, lx) or y in (ly - 5, ly + 5):
            frame.add((x, y))
    for x, y in glass:
        if (x, y) in frame:
            c.px[x, y] = IRON[3] if x < lx else IRON[2]
        else:
            c.px[x, y] = FIRE[3]
            # Zuckerglas: warm, Kerze in der Mitte am hellsten
            d = abs(x + 0.5 - lx) + abs(y - ly - 1) * 0.6
            g.px[x, y] = FIRE[4] if d < 2.2 else FIRE[3] if d < 4 else FIRE[2]
    # Kerzenflamme im Glas
    for x, y in ((lx - 1, ly - 1), (lx - 1, ly), (lx + 1, ly - 2), (lx + 1, ly), (lx + 1, ly - 1)):
        g.px[x, y] = FIRE[4]
    bottom = c.rect(lx - 6, ly + 6, lx + 5, ly + 7)
    c.flat(bottom, IRON, 2)
    c.hline(lx - 6, lx + 5, ly + 6, IRON[3])
    c.px[lx, ly + 8] = IRON[2]
    c.outline()
    # Kontur um die Glasscheiben im Glow-Bild weglassen: Glow bleibt innen
    shadow(c, px_ + 2, base, 7, 2.5)
    return {"c": c, "foot": (px_, base), "size": (8, 8), "glow": [g],
            "light": light(LIGHT_CANDLE, 4.4, 0.85, (base - ly) / PPU, x=(lx - px_) / PPU, flicker=0.12)}


def kessel_frames(c, cx, top_y, rx, ry, frames=8):
    """Gruener Trank: Wirbel + Blasen, die platzen, dazu Dampf. Gibt Glow-Canvas-Liste."""
    rng = random.Random(77)
    surface = c.ellipse(cx, top_y, rx, ry)
    bubbles = [(rng.uniform(-0.7, 0.7), rng.uniform(-0.5, 0.5), rng.randrange(frames), rng.choice((1, 2)))
               for _ in range(6)]
    steam = [(rng.uniform(-rx * 0.6, rx * 0.6), rng.randrange(frames)) for _ in range(5)]
    out = []
    for f in range(frames):
        g = glow_canvas(c)
        ph = f / frames * 2 * math.pi
        for x, y in surface:
            nx, ny = (x + 0.5 - cx) / rx, (y + 0.5 - top_y) / ry
            r = math.sqrt(nx * nx + ny * ny)
            a = math.atan2(ny, nx)
            swirl = math.sin(a * 2 + r * 6 - ph)
            if r > 0.86:
                col = POTION[1]
            elif swirl > 0.55:
                col = POTION[3]
            elif swirl > -0.2:
                col = POTION[2]
            else:
                col = POTION[1] if r > 0.6 else POTION[2]
            g.px[x, y] = col
        for bx, by, start, size in bubbles:
            age = (f - start) % frames
            x = int(round(cx + bx * rx * 0.8))
            y = int(round(top_y + by * ry * 0.8))
            if age < 3:
                g.px[x, y] = POTION[4]
                if size == 2 and age >= 1:
                    for q in ((x + 1, y), (x, y - 1), (x + 1, y - 1)):
                        g.px[q[0], q[1]] = POTION[3]
            elif age == 3:
                for q in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                    g.px[q[0], q[1]] = POTION[4]
        # Dampf: steigt in Fetzen auf, wird duenner
        for sx, start in steam:
            age = (f - start) % frames
            y = int(top_y - 2 - age * 2)
            x = int(round(cx + sx + math.sin(age * 0.9 + sx) * 1.5))
            if 0 <= y < c.h:
                a = max(0, 200 - age * 26)
                g.px[x, y] = POTION[3][:3] + (a,)
                if age < 4:
                    g.px[x + 1, y] = POTION[2][:3] + (a // 2,)
        out.append(g)
    return out


def kessel():
    c = Canvas(42, 46)
    rng = random.Random(13)
    cx = 21
    # Beine
    for lx in (cx - 11, cx + 10, cx - 1):
        leg = c.rect(lx - 1, 34, lx + 1, 40 if lx != cx - 1 else 41)
        c.cyl_v(leg, lx, 2, IRON, gloss=False)
    # Feuer drunter: Glut zwischen den Beinen (Grundbild, Glow obendrauf)
    body = [(x, y) for x, y in c.ellipse(cx, 27, 16, 12.5) if y >= 18]
    c.sphere(body, cx - 4, 22, 17, 14, IRON)
    s = set(body)
    # Rand
    rim = c.ellipse(cx, 18, 16.5, 5.2)
    c.flat(rim, IRON, 2)
    c.arc(cx, 18, 16, 4.9, 180, 360, IRON[4])
    c.arc(cx, 18, 16, 4.9, 0, 180, IRON[1])
    inner = c.ellipse(cx, 18, 13.5, 3.8)
    c.flat(inner, POTION, 1)
    # Henkel
    c.arc(cx - 17, 21, 3, 3, 90, 270, IRON[3])
    c.arc(cx + 17, 21, 3, 3, -90, 90, IRON[2])
    # Glanzlicht und Nieten
    c.vline(cx - 10, 22, 28, IRON[4])
    for x in range(cx - 12, cx + 13, 6):
        y = 24 + int(round(2 * (1 - ((x - cx) / 16) ** 2)))
        if (x, y) in s:
            c.px[x, y] = IRON[3]
    # ueberlaufender Trank: zwei Nasen am Rand
    drips = [(cx - 6, 21, 4), (cx + 8, 21, 3)]
    for x, y, ln in drips:
        for k in range(ln):
            c.px[x, y + k] = POTION[2]
        c.px[x, y + ln] = POTION[3]
    c.outline()
    shadow(c, cx + 2, 41, 17, 3.5)
    # Glow: Trankoberflaeche animiert, dazu die Nasen
    frames = kessel_frames(c, cx, 18, 13.5, 3.8)
    for g in frames:
        for x, y, ln in drips:
            for k in range(ln + 1):
                g.px[x, y + k] = POTION[2] if k < ln else POTION[3]
    return {"c": c, "foot": (cx, 41), "size": (30, 9), "glow": frames, "glowFps": 8,
            "light": light(LIGHT_POTION, 3.8, 0.75, 23 / PPU, flicker=0.2)}


def kerzen():
    c = Canvas(34, 38)
    g = glow_canvas(c)
    rng = random.Random(21)
    cx, base = 17, 33
    # Wachspfuetze
    pool = c.ellipse(cx, base - 1, 14, 3.6)
    c.sphere(pool, cx - 3, base - 3, 15, 6, WAX, gloss=False)
    specs = [(cx - 8, 13, 2.5), (cx - 2, 22, 3.0), (cx + 5, 16, 2.5), (cx + 10, 8, 2.0), (cx + 1, 7, 2.0)]
    specs.sort(key=lambda q: q[1] < 10)    # die kleinen vorne
    flames = []
    for x0, h, r in specs:
        top = base - 3 - h
        body = c.rect(int(x0 - r), top, int(x0 + r), base - 3)
        c.cyl_v(body, x0, r + 0.5, WAX, gloss=True)
        bs = set(body)
        # Oberkante abgebrannt, Tropfen am Rand
        for x in range(int(x0 - r), int(x0 + r) + 1):
            c.px[x, top] = WAX[4] if x < x0 else WAX[3]
            if rng.random() < 0.45:
                ln = rng.randint(2, max(3, h // 2))
                for k in range(1, ln):
                    if (x, top + k) in bs:
                        c.px[x, top + k] = WAX[3] if x < x0 + 0.5 else WAX[2]
        # Docht + Flamme
        wx = int(round(x0))
        c.px[wx, top - 1] = IRON[1]
        flames.append((wx, top - 2))
    c.outline()
    for wx, fy in flames:
        shape = [(0, 0, 4), (0, -1, 3), (0, -2, 3), (0, -3, 2), (-1, 0, 2), (1, 0, 2), (-1, -1, 1), (0, -4, 1)]
        for dx, dy, k in shape:
            x, y = wx + dx, fy + dy
            if 0 <= y < c.h:
                g.px[x, y] = FIRE[k]
                c.px[x, y] = FIRE[k]
    shadow(c, cx + 2, base, 15, 3)
    return {"c": c, "foot": (cx, base), "size": (22, 8), "glow": [g],
            "light": light(LIGHT_CANDLE, 3.2, 0.7, 16 / PPU, flicker=0.35)}


def mushroom(c, g, x, base, h, cap_r, pal_glow, rng):
    stem = c.rect(x - 1, base - h, x + 1, base)
    c.cyl_v(stem, x, 2, MERINGUE, gloss=False)
    # Lamellen-Schatten unter dem Hut
    capy = base - h
    cap = [(px, py) for px, py in c.ellipse(x, capy, cap_r, cap_r * 0.8) if py <= capy + 1]
    cs = set(cap)
    for px, py in cap:
        nx, ny = (px + 0.5 - x) / cap_r, (py + 0.5 - capy) / (cap_r * 0.8)
        nz = math.sqrt(max(0.0, 1 - nx * nx - ny * ny))
        v = nx * L[0] + ny * L[1] + nz * L[2]
        k = 3 if v > 0.7 else 2 if v > 0.3 else 1
        if (px, py + 1) not in cs:
            k = 0
        c.px[px, py] = pal_glow[k]
        g.px[px, py] = pal_glow[k]
    # Punkte auf dem Hut
    for _ in range(max(1, int(cap_r) - 1)):
        px, py = rng.choice(cap)
        if (px, py + 1) in cs:
            c.px[px, py] = pal_glow[4]
            g.px[px, py] = pal_glow[4]
    return cap


def pilze():
    c = Canvas(34, 34)
    g = glow_canvas(c)
    rng = random.Random(31)
    base = 29
    for x, h, r, p in ((10, 9, 5.5, CYAN), (21, 15, 7.0, CYAN), (27, 6, 3.5, VIOLET), (15, 5, 3.0, VIOLET)):
        mushroom(c, g, x, base - (1 if x == 21 else 0), h, r, p, rng)
    c.outline()
    shadow(c, 19, base + 1, 13, 3)
    return {"c": c, "foot": (19, base + 1), "size": (18, 8), "glow": [g],
            "light": light(LIGHT_CYAN, 3.0, 0.65, 12 / PPU, flicker=0.1)}


def zaun():
    c = Canvas(64, 40)
    rng = random.Random(12)
    base = 35
    posts = [(8, 26, 0.0), (30, 24, 0.0), (52, 18, 0.22)]
    tops = []
    for x0, h, lean in posts:
        tb = Tubes(c.w, c.h)
        x1 = x0 + lean * h
        pts = [(x0, base, 2.4), (x1, base - h, 2.4)]
        # Hakenbogen der Zuckerstange
        hook = []
        for k in range(9):
            a = math.pi + k / 8 * math.pi
            hook.append((x1 + 4 + math.cos(a) * 4, base - h + math.sin(a) * 4 * 0.9, 2.2))
        tb.path(pts + hook)
        for (x, y), (d, nx, ny, tag, t, cyy) in tb.best.items():
            nz = math.sqrt(max(0.0, 1 - nx * nx - ny * ny))
            v = nx * L[0] + ny * L[1] + nz * L[2] + 0.1
            stripe = math.floor((y + x * 0.9) / 3.0) % 2
            c.px[x, y] = tone_of(v, CANE_R if stripe else CANE_W, gloss=True)
        tops.append((x1, base - h + 6))
    # Lakritzseil haengt zwischen den Pfosten durch
    for (ax, ay), (bx, by) in zip(tops, tops[1:]):
        for k in range(41):
            t = k / 40
            x = ax + (bx - ax) * t
            y = ay + (by - ay) * t + math.sin(t * math.pi) * 5
            xi, yi = int(round(x)), int(round(y))
            if c.px[xi, yi][3] == 0 or c.px[xi, yi] in LICO:
                c.px[xi, yi] = LICO[3] if k % 3 == 0 else LICO[2]
                c.px[xi, yi + 1] = LICO[1]
    c.outline()
    shadow(c, 31, base + 1, 28, 3)
    return {"c": c, "foot": (31, base + 1), "size": (50, 6)}


# ---------------------------------------------------------------------------
# Flach

def laub():
    c = Canvas(44, 26)
    rng = random.Random(17)
    cx, cy = 22, 14
    leaves = []
    for _ in range(46):
        a = rng.random() * 2 * math.pi
        r = math.sqrt(rng.random())
        x = cx + math.cos(a) * r * 18
        y = cy + math.sin(a) * r * 9
        leaves.append((y, x, rng.choice(LEAVES), rng.random() * math.pi))
    leaves.sort()
    for y, x, p, rot in leaves:
        ln = rng.choice((3, 4, 4, 5))
        for k in range(ln):
            t = k - (ln - 1) / 2
            px = int(round(x + math.cos(rot) * t))
            py = int(round(y + math.sin(rot) * t * 0.6))
            w = 1 if abs(t) < ln / 2 - 0.6 else 0
            for dy in range(-w, w + 1):
                if 0 <= px < c.w and 0 <= py + dy < c.h:
                    c.px[px, py + dy] = p[3] if dy < 0 else p[2] if dy == 0 else p[1]
        # Mittelrippe als hellerer Punkt
        if 0 <= int(x) < c.w and 0 <= int(y) < c.h:
            c.px[int(x), int(y)] = p[4] if rng.random() < 0.4 else p[2]
    c.outline()
    return {"c": c, "foot": (cx, cy), "size": (0, 0), "flat": True}


def pilzring():
    c = Canvas(64, 40)
    g = glow_canvas(c)
    rng = random.Random(23)
    cx, cy = 32, 22
    # Gras im Ring etwas dunkler, als waere da Magie
    for x, y in c.ellipse(cx, cy, 24, 13):
        if ((x + 0.5 - cx) / 24) ** 2 + ((y + 0.5 - cy) / 13) ** 2 > 0.55:
            continue
        if (x * 3 + y * 5) % 11 == 0:
            c.px[x, y] = (40, 24, 50, 120)
    shrooms = []
    for k in range(13):
        a = k / 13 * 2 * math.pi + rng.uniform(-0.12, 0.12)
        x = cx + math.cos(a) * 24
        y = cy + math.sin(a) * 13
        shrooms.append((y, x))
    shrooms.sort()
    for y, x in shrooms:
        xi, yi = int(round(x)), int(round(y)) + 4
        h = rng.choice((2, 3, 3, 4))
        p = VIOLET if rng.random() < 0.35 else CYAN
        mushroom(c, g, xi, yi, h, rng.choice((1.8, 2.2, 2.6)), p, rng)
    c.outline()
    # Glitzerstaub in der Mitte (nur Glow)
    for _ in range(9):
        x = int(cx + rng.uniform(-14, 14))
        y = int(cy + rng.uniform(-6, 6))
        g.px[x, y] = CYAN[3][:3] + (rng.choice((120, 170, 220)),)
    return {"c": c, "foot": (cx, cy), "size": (0, 0), "flat": True, "glow": [g],
            "light": light(LIGHT_CYAN, 3.0, 0.45, 0.0, flicker=0.12, inner=0.6)}


def pfuetze():
    c = Canvas(52, 26)
    g = glow_canvas(c)
    rng = random.Random(29)
    cx, cy = 26, 13
    ph = rng.random() * 6

    def r(a):
        return 1 + 0.12 * math.sin(3 * a + ph) + 0.06 * math.sin(5 * a + 1.3)

    water = []
    for y in range(c.h):
        for x in range(c.w):
            a = math.atan2(y + 0.5 - cy, x + 0.5 - cx)
            if ((x + 0.5 - cx) / 21) ** 2 + ((y + 0.5 - cy) / 9.5) ** 2 <= r(a) ** 2:
                water.append((x, y))
    ws = set(water)
    for x, y in water:
        ny = (y + 0.5 - cy) / 9.5
        c.px[x, y] = hx("#141428") if ny > 0.2 else hx("#1c1c36")
    # Uferkante: Matsch
    for x, y in water:
        if (x, y - 1) not in ws:
            c.px[x, y] = SOIL[1]
        if (x, y + 1) not in ws:
            c.px[x, y] = hx("#2a2a4a")
    # Mond spiegelt sich (Glow: bleibt hell im Dunkeln)
    mx, my = cx + 7, cy - 1
    moon = [(x, y) for x, y in c.ellipse(mx, my, 4.2, 2.6) if (x, y) in ws]
    for x, y in moon:
        dx = (x + 0.5 - mx) / 4.2
        col = MOON[3] if dx < -0.2 else MOON[2]
        if math.sin(y * 2.1) > 0.85:
            col = MOON[1]          # Wellenstreifen
        g.px[x, y] = col
        c.px[x, y] = col
    # Sterne
    for _ in range(5):
        x, y = rng.choice(water)
        if (x, y) not in set(moon):
            g.px[x, y] = MOON[2][:3] + (rng.choice((140, 200)),)
    # Wellenlinien
    for k in range(3):
        y = cy - 3 + k * 3
        for x in range(cx - 12 + k * 2, cx - 4 + k * 2):
            if (x, y) in ws and x % 3:
                c.px[x, y] = hx("#2e2e52")
    c.outline()
    return {"c": c, "foot": (cx, cy), "size": (0, 0), "flat": True, "glow": [g]}


# ---------------------------------------------------------------------------

PROPS = {
    "lebkuchenbaum": (lebkuchenbaum, 7.0),
    "spukbaum": (spukbaum, 1.6),
    "lakritzbaum": (lakritzbaum, 4.0),
    "grabstein": (grabstein, 2.6),
    "grabkreuz": (grabkreuz, 2.4),
    "kuerbis": (kuerbis, 2.2),
    "laterne": (laterne, 1.6),
    "kessel": (kessel, 0.7),
    "kerzen": (kerzen, 1.4),
    "pilze": (pilze, 2.0),
    "zaun": (zaun, 1.4),
}
FLATS = {
    "laub": (laub, 5.0),
    "pilzring": (pilzring, 1.2),
    "pfuetze": (pfuetze, 2.0),
}


def render():
    out = {}
    for group, flat in ((PROPS, False), (FLATS, True)):
        for name, (fn, weight) in group.items():
            d = fn()
            d["weight"] = weight
            d["flat"] = flat
            out[name] = d
    return out


def preview(items, path, scale=3):
    """Alle Props auf dem Boden, einmal am Tag und einmal 'bei Nacht' (Lichtsimulation)."""
    import importlib.util
    spec = importlib.util.spec_from_file_location("gb", os.path.join(os.path.dirname(__file__), "geist_boden.py"))
    gb = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(gb)
    sheet, _ = gb.build()
    from geist_licht import night
    cols, rows = 20, 9
    floor = Image.new("RGBA", (cols * 32, rows * 32))
    for r_ in range(rows):
        for c_ in range(cols):
            idx = (r_ * 7 + c_ * 3) % 4
            floor.paste(sheet.crop((idx * 32, 0, idx * 32 + 32, 32)), (c_ * 32, r_ * 32))
    glow = Image.new("RGBA", floor.size)
    lights = []
    x = 6
    for n, d in items.items():
        if not d["flat"]:
            continue
        img = d["c"].img
        floor.alpha_composite(img, (x, 8))
        if d.get("glow"):
            glow.alpha_composite(d["glow"][0].img, (x, 8))
        if d.get("light"):
            lt = d["light"]
            lights.append((x + d["foot"][0] + lt["x"] * 32, 8 + d["foot"][1] - lt["y"] * 32, lt))
        x += img.width + 8
    x = 6
    for n, d in items.items():
        if d["flat"]:
            continue
        img = d["c"].img
        fx, fy = d["foot"]
        if x + img.width > floor.width:
            break
        y0 = 280 - fy
        floor.alpha_composite(img, (x, y0))
        if d.get("glow"):
            glow.alpha_composite(d["glow"][0].img, (x, y0))
        if d.get("light"):
            lt = d["light"]
            lights.append((x + fx + lt["x"] * 32, y0 + fy - lt["y"] * 32, lt))
        x += img.width + 2
    keks = os.path.join(ROOT, "Assets", "Art", "Chars", "Char_Keks.png")
    if os.path.exists(keks):
        k = Image.open(keks).convert("RGBA").crop((0, 0, 40, 40))
        floor.alpha_composite(k, (cols * 32 - 60, 140))
        lights.append((cols * 32 - 40, 160, {"r": 1.0, "g": 0.92, "b": 0.8, "radius": 5.5, "inner": 0.0,
                                             "intensity": 0.55}))
    day = floor.copy()
    day.alpha_composite(glow)
    nt = night(floor, glow, lights)
    both = Image.new("RGBA", (floor.width, floor.height * 2))
    both.paste(day, (0, 0))
    both.paste(nt, (0, floor.height))
    both.resize((both.width * scale // 2, both.height * scale // 2), Image.NEAREST).save(path)


def main():
    items = render()
    if "--preview" in sys.argv:
        preview(items, sys.argv[sys.argv.index("--preview") + 1])
        return
    if "--gif" in sys.argv:
        k = sys.argv.index("--gif")
        d = items[sys.argv[k + 2] if len(sys.argv) > k + 2 else "kessel"]
        frames = []
        for g in d["glow"]:
            im = d["c"].img.copy()
            im.alpha_composite(g.img)
            bg = Image.new("RGBA", im.size, (40, 30, 50, 255))
            bg.alpha_composite(im)
            frames.append(bg.resize((im.width * 6, im.height * 6), Image.NEAREST))
        frames[0].save(sys.argv[k + 1], save_all=True, append_images=frames[1:],
                       duration=int(1000 / d.get("glowFps", 8)), loop=0)
        return
    os.makedirs(OUT_DIR, exist_ok=True)
    info = []
    for name, d in items.items():
        img = d["c"].img
        fx, fy = d["foot"]
        path = os.path.join(OUT_DIR, "geist_%s.png" % name)
        img.save(path)
        pivot = (round(fx / img.width, 4), round(1 - fy / img.height, 4))
        if not os.path.exists(path + ".meta"):
            write_strip_meta(path + ".meta", "geist_" + name, 1, img.width, img.height, PPU, pivot=pivot)
        glow_n = 0
        if d.get("glow"):
            frames = d["glow"]
            glow_n = len(frames)
            strip = Image.new("RGBA", (img.width * glow_n, img.height), (0, 0, 0, 0))
            for i, gc in enumerate(frames):
                strip.paste(gc.img, (i * img.width, 0))
            gpath = os.path.join(OUT_DIR, "geist_%s_glow.png" % name)
            strip.save(gpath)
            if not os.path.exists(gpath + ".meta"):
                write_strip_meta(gpath + ".meta", "geist_%s_glow" % name, glow_n, img.width, img.height, PPU,
                                 pivot=pivot)
        info.append({
            "name": name, "flat": d["flat"], "weight": d["weight"],
            "colliderW": d["size"][0], "colliderH": d["size"][1],
            "frameW": img.width, "frameH": img.height, "pivotX": fx, "pivotY": img.height - fy,
            "glowFrames": glow_n, "glowFps": d.get("glowFps", 0),
            "light": d.get("light"),
        })
    with open(INFO, "w", encoding="utf-8", newline="\n") as fh:
        json.dump({"props": info}, fh, indent=1)
    print("geschrieben:", len(info), "Props nach", OUT_DIR)


if __name__ == "__main__":
    main()
