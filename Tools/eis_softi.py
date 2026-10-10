"""
Softi (EnemyId.Softi, Elite der Eiswelt): eine Waffeltuete mit Gesicht und
einer Frisur aus Erdbeer-Vanille-Softeis, die sich unablaessig dreht.

  * Die Tuete ist der Koerper: sie schaukelt auf ihrer Spitze von links nach
    rechts (Stehaufmaennchen-Gang), das Gesicht sitzt auf der Waffel.
  * Das Softeis sind vier Wuelste uebereinander. Die zweifarbigen Streifen
    wandern ueber die Wuelste (eine volle Umdrehung je Schleife), die Wuelste
    selbst eiern auf einer Schraube und haengen dem Schaukeln nach - je hoeher,
    desto mehr. Streusel und Schokotropfen drehen sich mit und verschwinden
    hinten.
  * Oben eine Schoko-Haube mit Kringel, darauf die Kirsche; ihr Stiel federt.
  * Kein Schatten und keine losen Teilchen im Bild: EliteGlow umrandet die
    Silhouette, alles Abgesetzte wuerde mitleuchten.
  * Zwei Schleifen: normal und Blinzeln mit Zunge.

  Assets/Art/Gegner/new/eis/softi_wirbel.png   2 x 16 Bilder, 64x72, PPU 32
  Pivot = Tuetenspitze am Boden.
  Assets/Resources/Bestiary/Softi.png           Bild 0, zugeschnitten

Aufruf aus dem Projektordner:
  python Tools/eis_softi.py [--preview pfad.gif]
"""

import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from eis_gegner_kit import (LIGHT, Spring, arg_preview, downsample, grid,  # noqa: E402
                            outline, periodic, pixel_grid, put, ramp, rgb,
                            save_all, stamp)

NAME = "softi_wirbel"
CELL_W, CELL_H = 64, 72
GROUND = 70
CX = 32.0
FPS = 16
PER = 16
LOOPS = 2
N = PER * LOOPS
SS = 5

CONE_H = 26.0
CONE_HW = 12.0
RIM0, RIM1 = 22.5, 26.5
ROCK = 2.4                      # Ausschlag oben an der Tuete (px)

# Wuelste: (Mitte y, halbe Breite, halbe Hoehe)
TIERS = [(30.0, 15.5, 5.6), (36.6, 12.6, 5.0), (42.4, 9.6, 4.4), (47.4, 6.4, 3.6)]
CURL_BASE = 50.0


def pal(*h):
    return tuple(rgb(x) for x in h)


PINK = pal("8a2a52", "c04878", "e66e9c", "f798ba", "ffc4d8", "fff0f6")
VAN = pal("a8865a", "d0b080", "ead09e", "f8e8c4", "fff6e2", "ffffff")
WAF = pal("5e3216", "8c5424", "b97836", "d89a4a", "eebc6c", "f8dc9a")
CHOCO = pal("1e0c08", "36190f", "4e2618", "6c3a24", "8e5634", "c08a62")
CHER = pal("4a0812", "7e1222", "b4202e", "e03a40", "ff7a72", "ffd0c8")
C = {
    "l_waf": rgb("3a1c0a"),
    "l_cream": rgb("5a2a3a"),
    "l_choco": rgb("140604"),
    "l_cher": rgb("3a0610"),
    "stem": rgb("4e8a2a"),
    "stem_l": rgb("8ac44a"),
    "eye": rgb("1a0a06"),
    "eglint": rgb("ffffff"),
    "brow": rgb("2a1206"),
    "mouth": rgb("1a0a06"),
    "maw": rgb("5a1420"),
    "tongue": rgb("f06a8a"),
    "tooth": rgb("ffffff"),
    "blush": rgb("f07a6a"),
    "glint": rgb("ffffff"),
}
SPRINKLES = [rgb("ff5f8f"), rgb("ffd34e"), rgb("5fe0b0"), rgb("5fa8ff"), rgb("ffffff"), rgb("c88cff")]


# ================================================================ Bewegung

def simulate():
    stem = Spring(220.0, 6.0)
    prev = {"top": None}

    def step(k, dt):
        loop = int(k // PER)
        a = 2 * math.pi * (k % PER) / PER
        rock = ROCK * math.sin(a)
        top = rock * 2.2
        v = 0.0 if prev["top"] is None else (top - prev["top"]) / dt
        prev["top"] = top
        sw = stem.step(0.0, dt, -v * dt * 9.0)          # Stiel federt gegen die Bewegung
        return dict(loop=loop, a=a, rock=rock, spin=a, stem=sw)

    return periodic(N, FPS, step)


def tier_offset(st, i):
    """Wulst i: Schraube (dreht mit) + Nachhaengen hinter dem Schaukeln."""
    helix = 0.75 * math.sin(st["spin"] + i * 1.6)
    lag = -0.9 * math.cos(st["a"]) * (i + 1) / 2.5
    return helix + lag


def shear(st, Y):
    return st["rock"] * np.clip(Y, 0, None) / CONE_H


# ================================================================ Formen

EMPTY, CONE, T0, T1, T2, T3, CURL, CHERRY, STEM = range(9)
TIER_CLS = (T0, T1, T2, T3)


def cone_hw(y):
    return CONE_HW * np.clip(y / CONE_H, 0, 1) ** 0.92


def tier_F(st, i, X, Y):
    cy, rx, ry = TIERS[i]
    x0 = tier_offset(st, i)
    xs = X - shear(st, Y) - x0
    # Unterseite etwas flacher (liegt auf), oben runder
    ryy = np.where(Y < cy, ry * 0.9, ry)
    return np.abs(xs / rx) ** 2.5 + np.abs((Y - cy) / ryy) ** 2, xs


def curl_geo(st):
    x0 = tier_offset(st, 3) + 0.4 * math.sin(st["spin"] + 2.2)
    bend = 1.6 * math.sin(st["spin"] + 1.0) - 0.8 * math.cos(st["a"])
    return x0, bend


def curl_mask(st, X, Y):
    x0, bend = curl_geo(st)
    xs = X - shear(st, Y)
    m = np.zeros(X.shape, bool)
    for t in np.linspace(0, 1, 9):
        cx = x0 + bend * t * t
        cy = CURL_BASE - 1.5 + 6.5 * t
        r = 3.9 * (1 - t) + 1.0 * t
        m |= (xs - cx) ** 2 + (Y - cy) ** 2 <= r * r
    return m


def cherry_pos(st):
    x0, bend = curl_geo(st)
    tip_x = x0 + bend + st["rock"] * (CURL_BASE + 7) / CONE_H
    return tip_x, CURL_BASE + 4.6


def render(st):
    X, Y = grid(CELL_W, CELL_H, SS, CX, GROUND)
    owner = np.zeros(X.shape, np.int16)
    xs = X - shear(st, Y)

    # Stiel zuerst (die Kirsche liegt davor), dann Kringel, Kirsche obendrauf
    hx, hy = cherry_pos(st)
    sx = hx + 0.6
    for t in np.linspace(0, 1, 14):
        px = sx + st["stem"] * t * t + 1.8 * t * t
        py = hy + 4.6 + 4.0 * t
        owner[(X - px) ** 2 + (Y - py) ** 2 <= 0.62 ** 2] = STEM
    owner[curl_mask(st, X, Y)] = CURL
    owner[(X - hx) ** 2 + (Y - hy - 2.6) ** 2 <= 3.4 ** 2] = CHERRY
    for i in (3, 2, 1, 0):
        F, _ = tier_F(st, i, X, Y)
        owner[F <= 1.0] = TIER_CLS[i]
    # Tuete mit Rand
    hw = cone_hw(Y)
    rim = (Y >= RIM0) & (Y <= RIM1)
    cone = ((np.abs(xs) <= hw) & (Y >= 0) & (Y <= RIM1)) | (rim & (np.abs(xs) <= CONE_HW + 0.9))
    # runde Spitze
    cone &= ~((Y < 2.2) & (np.abs(xs) > 0.9 + Y * 0.3))
    keep = (owner == T0) & (Y > RIM1 - 0.5)            # Wulst haengt vorn ueber den Rand
    owner[cone & ~keep] = CONE
    owner[Y < 0] = EMPTY
    return downsample(owner, CELL_W, CELL_H, SS, keep_thin=(STEM,))


# ================================================================ Faerben

def stripe(st, i, x, y, rx):
    """Zweifarbige Drehstreifen: Winkel auf dem Wulst + Drehung + Schraegung."""
    u = max(-1.0, min(1.0, x / rx))
    th = math.asin(u)
    return math.sin(2 * (th - st["spin"]) + i * 1.9 + (y - TIERS[i][0]) * 0.32) > 0


def tier_light(st, i, x, y):
    cy, rx, ry = TIERS[i]
    u = max(-1.0, min(1.0, x / rx))
    v = max(-1.0, min(1.0, (y - cy) / ry))
    nz = math.sqrt(max(0.0, 1 - min(1.0, u * u + 0.6 * v * v)))
    n = np.array([u * 1.1, v * 0.9, nz + 0.15])
    n /= np.linalg.norm(n)
    return float(n @ LIGHT)


def shade(pix, st, k):
    img = np.zeros((CELL_H, CELL_W, 4), np.uint8)
    Xp, Yp = pixel_grid(CELL_W, CELL_H, CX, GROUND)
    for r in range(CELL_H):
        for c in range(CELL_W):
            m = pix[r, c]
            if m == EMPTY:
                continue
            x, y = Xp[r, c], Yp[r, c]
            xs = x - float(shear(st, np.array(y)))
            if m == CONE:
                hw = max(float(cone_hw(np.array(y))), 1.0)
                if y >= RIM0:
                    hw = CONE_HW + 0.9
                u = max(-1.0, min(1.0, xs / hw))
                l = -u * 0.62 + math.sqrt(max(0, 1 - u * u)) * 0.56 + 0.12
                if y >= RIM0:                                   # Rand: Wulst, oben hell
                    l += 0.18 if y > (RIM0 + RIM1) / 2 else -0.08
                    col = ramp(l, WAF)
                else:
                    # Waffelgitter auf der abgerollten Tuete (steht still)
                    a = math.asin(u) * hw * 0.9
                    g1 = (a + y * 0.95) % 4.2
                    g2 = (a - y * 0.95) % 4.2
                    col = ramp(l, WAF)
                    if g1 < 0.95 or g2 < 0.95:
                        col = WAF[1] if l > 0.2 else WAF[0]
                    elif (g1 < 1.9 or g2 < 1.9) and l > 0.3:
                        col = WAF[4]
                img[r, c] = col
            elif m in TIER_CLS:
                i = TIER_CLS.index(m)
                xo = xs - tier_offset(st, i)
                cy, rx, ry = TIERS[i]
                l = tier_light(st, i, xo, y)
                if i == 3:                                      # Schoko-Haube
                    img[r, c] = ramp(l + 0.12, CHOCO, cuts=(0.92, 0.70, 0.45, 0.15))
                    continue
                pal_ = PINK if stripe(st, i, xo, y, rx) else VAN
                img[r, c] = ramp(l, pal_, cuts=(0.88, 0.66, 0.40, 0.12))
            elif m == CURL:
                x0, bend = curl_geo(st)
                u = max(-1.0, min(1.0, (xs - x0) / 4.0))
                l = -u * 0.6 + 0.5 + (0.15 if y > CURL_BASE + 2 else 0)
                img[r, c] = ramp(l, CHOCO)
            elif m == CHERRY:
                hx, hy = cherry_pos(st)
                u, v = (x - hx) / 3.4, (y - hy - 2.6) / 3.4
                nz = math.sqrt(max(0.0, 1 - u * u - v * v))
                l = float(np.array([u, v, nz]) @ LIGHT)
                img[r, c] = ramp(l, CHER)
            elif m == STEM:
                img[r, c] = C["stem"]

    def line_of(m):
        if m == CONE:
            return C["l_waf"]
        if m == T3:
            return C["l_choco"]
        if m in TIER_CLS:
            return C["l_cream"]
        if m == CURL:
            return C["l_choco"]
        if m == CHERRY:
            return C["l_cher"]
        return None

    out = outline(img, pix, line_of, empty=(EMPTY,))
    # Fugen: wo ein tieferer Wulst (oder die Tuete) ueber einem anderen liegt
    order = {CONE: -1, T0: 0, T1: 1, T2: 2, T3: 3, CHERRY: 4, CURL: 5, STEM: 6}
    for r in range(CELL_H - 1):
        for c in range(CELL_W):
            m, below = pix[r, c], pix[r + 1, c]
            if m == EMPTY or below == EMPTY or m == STEM:
                continue
            if order.get(below, 9) < order.get(m, 9):
                if m in TIER_CLS or m == CURL:
                    out[r, c] = C["l_choco"] if m in (T3, CURL) else C["l_cream"]
                elif m == CHERRY:
                    out[r, c] = C["l_cher"]
            # Tuetenrand gegen die Wuelste
            if m in TIER_CLS and below == CONE:
                out[r, c] = C["l_cream"]
    # Rand der Tuete: dunkle Linie unter dem Wulst, der darueber haengt
    for r in range(1, CELL_H):
        for c in range(CELL_W):
            if pix[r, c] == CONE and pix[r - 1, c] in TIER_CLS:
                out[r, c] = WAF[0]
    return out


# ================================================================ Details

def visible_point(st, i, alpha, dy):
    """Punkt auf Wulst i (Winkel alpha um die Achse, dy ueber der Mitte) -> (r, c, Vorderseite)."""
    cy, rx, ry = TIERS[i]
    ang = alpha + st["spin"]
    front = math.cos(ang)
    w = rx * 0.93 * math.sqrt(max(0.0, 1 - (dy / ry) ** 2))
    x = tier_offset(st, i) + w * math.sin(ang)
    y = cy + dy
    x += float(shear(st, np.array(y)))
    return int(math.floor(GROUND - y)), int(math.floor(CX + x)), front, math.sin(ang)


SPRINKLE_SET = [(i, a, dy, n % len(SPRINKLES))
                for n, (i, a, dy) in enumerate([
                    (0, 0.3, 1.8), (0, 1.5, -0.6), (0, 2.6, 2.4), (0, 3.7, 0.2), (0, 4.8, 1.6), (0, 5.8, -1.0),
                    (1, 0.9, 1.4), (1, 2.0, -0.4), (1, 3.2, 1.8), (1, 4.3, 0.0), (1, 5.4, 1.2),
                    (2, 0.2, 1.0), (2, 1.6, 0.0), (2, 3.0, 1.4), (2, 4.4, 0.4), (2, 5.6, 1.6),
                    ])]


def sprinkles(img, pix, st):
    for i, a, dy, ci in SPRINKLE_SET:
        r, c, front, side = visible_point(st, i, a, dy)
        if front < 0.12:
            continue
        cells = [(r, c)]
        if front > 0.55:                                  # vorn: 2 px quer, am Rand verkuerzt
            cells.append((r, c + 1) if (ci % 2 == 0) else (r - 1, c))
        for rr, cc in cells:
            if 0 <= rr < CELL_H and 0 <= cc < CELL_W and pix[rr, cc] == TIER_CLS[i] \
                    and tuple(img[rr, cc]) != C["l_cream"]:
                img[rr, cc] = SPRINKLES[ci]


DRIPS = [(0.0, 3.0), (1.3, 2.0), (2.4, 4.0), (3.6, 2.5), (4.7, 3.5), (5.6, 1.5)]


def choco_drips(img, pix, st):
    """Schokotropfen laufen von der Haube auf den Wulst darunter und drehen mit."""
    cy, rx, ry = TIERS[3]
    for a, ln in DRIPS:
        r0, c, front, side = visible_point(st, 3, a, -ry * 0.7)
        if front < 0.25:
            continue
        length = max(1, int(round(ln * (0.5 + 0.5 * front))))
        wide = front > 0.6
        for d in range(length + 1):
            rr = r0 + d
            cols = (c, c + 1) if wide and d < length else (c,) if not wide else (c, c + 1)
            for n, cc in enumerate(cols):
                if 0 <= rr < CELL_H and 0 <= cc < CELL_W and pix[rr, cc] in (T3, T2):
                    if d == length:
                        img[rr, cc] = C["l_choco"]               # runder Tropfenboden
                    else:
                        img[rr, cc] = CHOCO[4] if (n == 0 and wide and d > 0) else CHOCO[2]


def glints(img, pix, st):
    hx, hy = cherry_pos(st)
    r, c = int(math.floor(GROUND - hy - 4.0)), int(math.floor(CX + hx - 1.4))
    for dr, dc in ((0, 0), (0, 1), (1, 0)):
        if 0 <= r + dr < CELL_H and pix[r + dr, c + dc] == CHERRY:
            img[r + dr, c + dc] = C["glint"]
    # Stiel: Lichtkante
    for rr in range(CELL_H):
        for cc in range(CELL_W - 1):
            if pix[rr, cc] == STEM and pix[rr, cc + 1] != STEM:
                pass
    # Glanz auf der Schoko-Haube
    x0, bend = curl_geo(st)
    rr = int(math.floor(GROUND - CURL_BASE - 1.5))
    cc = int(math.floor(CX + x0 - 1.6 + float(shear(st, np.array(CURL_BASE)))))
    for dr, dc in ((0, 0), (-1, 1)):
        if pix[rr + dr, cc + dc] == CURL:
            img[rr + dr, cc + dc] = CHOCO[5]


EYES = {
    "boese": ("bb.....",
              ".bbbb..",
              ".......",
              "..###..",
              ".#ww##.",
              ".#w###.",
              "..###.."),
    "zu":    ("bb.....",
              ".bbbb..",
              ".......",
              ".......",
              ".......",
              ".#####.",
              "..###.."),
}
MOUTHS = {
    "grins": ("mm......mm",
              ".mffmmffm.",
              "..mrrrrm..",
              "...mmmm..."),
    "zunge": ("mm......mm",
              ".mffmmffm.",
              "..mrttrm..",
              "...mttm...",
              "....tt...."),
}


def face(img, pix, st, k):
    loop, j = st["loop"], k % PER
    eye, mouth = "boese", "grins"
    if loop == 1 and j in (5, 6):
        eye = "zu"
    if loop == 1 and 4 <= j <= 11:
        mouth = "zunge"
    yf = 19.0
    xf = float(shear(st, np.array(yf)))
    r0 = int(round(GROUND - yf)) - 3
    cm = int(round(CX + xf))
    cols = {"#": C["eye"], "w": C["eglint"], "b": C["brow"], "m": C["mouth"],
            "r": C["maw"], "t": C["tongue"], "f": C["tooth"]}
    only = (CONE,)
    marks = []

    def mark(rows, rr0, cc0, mirror=False):
        for dr, row in enumerate(rows):
            row = row[::-1] if mirror else row
            for dc, ch in enumerate(row):
                if ch != ".":
                    marks.append((rr0 + dr, cc0 + dc))

    e = EYES[eye]
    m = MOUTHS[mouth]
    mark(e, r0, cm - 8)
    mark(e, r0, cm + 1, True)
    mark(m, r0 + 8, cm - 5)
    # Hof: Waffelgitter direkt um Augen und Mund glaetten, damit das Gesicht steht
    ms = set(marks)
    for rr, cc in marks:
        for a in (-1, 0, 1):
            for b in (-1, 0, 1):
                q = (rr + a, cc + b)
                if q in ms or not (0 <= q[0] < CELL_H and 0 <= q[1] < CELL_W) or pix[q] != CONE:
                    continue
                if tuple(img[q]) in (WAF[0], WAF[1], WAF[4]) and tuple(img[q]) != C["l_waf"]:
                    img[q] = WAF[3] if q[1] < cm else WAF[2]
    stamp(img, e, r0, cm - 8, cols, pix=pix, only=only)
    stamp(img, e, r0, cm + 1, cols, mirror=True, pix=pix, only=only)
    for cc in (cm - 9, cm - 8, cm + 7, cm + 8):
        put(img, r0 + 7, cc, C["blush"], only, pix)
    stamp(img, m, r0 + 8, cm - 5, cols, pix=pix, only=only)


# ================================================================ Ablauf

def draw(states, k):
    st = states[k]
    pix = render(st)
    img = shade(pix, st, k)
    sprinkles(img, pix, st)
    choco_drips(img, pix, st)
    glints(img, pix, st)
    face(img, pix, st, k)
    return img


def main():
    states = simulate()
    frames = [draw(states, k) for k in range(N)]
    save_all(NAME, frames, CELL_W, CELL_H, (CX, GROUND), FPS,
             bestiary_id="Softi", preview=arg_preview(), per_row=PER // 2)


if __name__ == "__main__":
    main()
