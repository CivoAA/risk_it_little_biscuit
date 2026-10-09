# -*- coding: utf-8 -*-
"""Erfolgs-Kapsel fuer den Hub, Version 2: 80x128, 16 Frames als Endlosschleife.

Aufruf aus dem Projektordner:
    python Tools/kapsel_erfolgsbuch.py Assets/Art/World-Objects
    python Tools/kapsel_erfolgsbuch.py <ordner> --vorschau   # + GIF und Bogen

Schreibt kapsel_erfolgsbuch.png (Frame 1) und kapsel_erfolgsbuch_2..16.png.
Vorhandene .meta-Dateien bleiben liegen (GUIDs aendern sich nicht); fehlende
legt das Skript als Single-Sprite mit Pivot (0.5, 0.35) und PPU 32 an.
Im Hub spielt SpriteFrameLoop die Bilder ab (frameTime 0.1).

Die erste Kapsel war ein flacher Bogen. Diese hier ist als echter Koerper
gebaut: jedes Teil ist ein Zylinder in leichter Aufsicht (Ellipsen mit
Verhaeltnis K), schattiert ueber die Normale - Licht von links oben vorn -
und danach hart in die Materialrampe gestuft. Kein Dithering, wie im Rest
des Hubs; die Tiefe kommt aus Ellipsenkanten, Baendern und Glanz.

Aufbau von oben nach unten:
  Messingknauf -> Glaskuppel (Luft) -> Fluessigkeitsspiegel -> leuchtende
  Fluessigkeit mit schwebendem Buch, Mini-Trophaeen und Blasen ->
  Messingkragen mit Nieten -> Holztrommel mit Keks-Wappen -> Sockelplatte.
"""
import math, os, sys, uuid, random
from PIL import Image

W, H = 80, 128
FRAMES = 16
K = 0.13                      # Ellipsen-Verhaeltnis (Aufsicht)
CX = 40.0                     # Mittelachse auf der Pixelgrenze 39|40

# ------------------------------------------------------------------ Paletten
INK   = (59, 36, 51)          # 3B2433 Hub-Aussenlinie
INK2  = (38, 24, 40)          # tiefster Schatten unten

# Leuchtfluessigkeit: von Rostrot (Wand, Tiefe) bis Fast-Weiss (Kern)
LQ = [(104, 46, 48), (140, 66, 46), (178, 98, 52), (208, 136, 62),
      (228, 172, 82), (241, 205, 118), (250, 230, 168), (255, 248, 222)]
# Luft in der Kuppel: kuehles Pflaume, nach unten warm angeleuchtet
AIR = [(66, 40, 60), (84, 52, 72), (104, 64, 80), (132, 80, 82),
       (166, 104, 82), (200, 140, 92)]
# Messing
BR  = [(59, 36, 51), (96, 56, 46), (138, 88, 44), (178, 128, 56),
       (212, 172, 82), (236, 210, 128), (250, 240, 196)]
# Holz (Hub-Toene 925527/9F5A35/B1773E, Schatten nach Pflaume verschoben)
WD  = [(59, 36, 51), (86, 46, 52), (114, 62, 46), (146, 85, 39),
       (164, 100, 52), (182, 124, 66), (206, 154, 92)]
# Einband weinrot wie die Buecher im Regal (bib.png)
BK  = [(59, 36, 51), (87, 20, 69), (124, 34, 71), (150, 45, 86),
       (176, 74, 110), (204, 116, 140)]
PAGE = [(176, 120, 92), (214, 170, 120), (238, 214, 166), (250, 240, 210)]
WHITE = (255, 250, 232)


def clamp(v, a, b):
    return a if v < a else b if v > b else v


def ramp(r, v):
    """v in 0..1 -> Farbe der Rampe r, hart gestuft."""
    return r[clamp(int(round(v * (len(r) - 1))), 0, len(r) - 1)]


def rstep(r, i):
    return r[clamp(i, 0, len(r) - 1)]


class Buf:
    def __init__(self):
        self.px = [[None] * W for _ in range(H)]
        self.part = [[None] * W for _ in range(H)]

    def set(self, x, y, c, part=None):
        if 0 <= x < W and 0 <= y < H:
            self.px[y][x] = c
            if part is not None:
                self.part[y][x] = part

    def get(self, x, y):
        return self.px[y][x] if 0 <= x < W and 0 <= y < H else None

    def partof(self, x, y):
        return self.part[y][x] if 0 <= x < W and 0 <= y < H else None

    def image(self):
        im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        d = im.load()
        for y in range(H):
            for x in range(W):
                c = self.px[y][x]
                if c is not None:
                    d[x, y] = (c[0], c[1], c[2], 255)
        return im


# ------------------------------------------------------------ Licht & Formen
LX, LY, LZ = -0.55, -0.45, 0.70          # Licht von links oben vorn
_n = math.sqrt(LX * LX + LY * LY + LZ * LZ)
LX, LY, LZ = LX / _n, LY / _n, LZ / _n


def lambert(nx, ny, nz):
    return max(0.0, nx * LX + ny * LY + nz * LZ)


def spec(nx, ny, nz, power=18):
    # Halbvektor zwischen Licht und Blick (0,0,1)
    hx, hy, hz = LX, LY, LZ + 1.0
    m = math.sqrt(hx * hx + hy * hy + hz * hz)
    return max(0.0, (nx * hx + ny * hy + nz * hz) / m) ** power


# -------------------------------------------------------------- Geometrie
GR = 26.0                          # Glas-Aussenradius  -> x 14..65
CYL_TOP = 37.0                     # ab hier ist das Glas senkrecht
DOME_RY = 27.0                     # Kuppelhoehe -> Scheitel y=10
GLASS_BOT = 93                     # Glas steckt im Kragen (Ring oben y=95)
SURF_Y = 42.0                      # Fluessigkeitsspiegel (Mitte der Ellipse)


def glass_halfwidth(py):
    if py >= CYL_TOP:
        return GR
    dy = (CYL_TOP - py) / DOME_RY
    if dy >= 1.0:
        return 0.0
    return GR * math.sqrt(1.0 - dy * dy)


def in_glass(px, py):
    if py < CYL_TOP - DOME_RY:
        return False
    hw = glass_halfwidth(py)
    if abs(px - CX) > hw:
        return False
    # unten endet das Glas an der vorderen Kante seiner Bodenellipse im Kragen
    u = (px - CX) / GR
    return int(py) <= GLASS_BOT + int(round(GR * K * math.sqrt(max(0.0, 1 - u * u))))


def glass_normal(px, py):
    """Normale der Glasaussenhaut (Zylinder bzw. Kuppel-Ellipsoid)."""
    dx = (px - CX) / GR
    if py >= CYL_TOP:
        nx = clamp(dx, -1, 1)
        return nx, 0.0, math.sqrt(max(0.0, 1 - nx * nx))
    dy = (py - CYL_TOP) / DOME_RY
    nz2 = 1 - dx * dx - dy * dy
    nz = math.sqrt(max(0.0, nz2))
    nx, ny = dx, dy * (GR / DOME_RY)          # Ellipsoid-Gradient
    m = math.sqrt(nx * nx + ny * ny + nz * nz) or 1
    return nx / m, ny / m, nz / m


GMASK = [[in_glass(x + 0.5, y + 0.5) for x in range(W)] for y in range(H)]


def dist_field(mask):
    INF = 9999.0
    d = [[0.0 if not mask[y][x] else INF for x in range(W)] for y in range(H)]
    for y in range(H):
        for x in range(W):
            if d[y][x]:
                for dx, dy, w in ((-1, 0, 1), (0, -1, 1), (-1, -1, 1.414), (1, -1, 1.414)):
                    nx, ny = x + dx, y + dy
                    v = (d[ny][nx] if 0 <= nx < W and 0 <= ny < H else 0) + w
                    d[y][x] = min(d[y][x], v)
    for y in range(H - 1, -1, -1):
        for x in range(W - 1, -1, -1):
            if d[y][x]:
                for dx, dy, w in ((1, 0, 1), (0, 1, 1), (1, 1, 1.414), (-1, 1, 1.414)):
                    nx, ny = x + dx, y + dy
                    v = (d[ny][nx] if 0 <= nx < W and 0 <= ny < H else 0) + w
                    d[y][x] = min(d[y][x], v)
    return d


GDIST = dist_field(GMASK)


def surf_front(px):
    """y der Vorderkante des Fluessigkeitsspiegels."""
    u = (px - CX) / (GR - 1.5)
    return SURF_Y + (GR - 1.5) * K * math.sqrt(max(0.0, 1 - u * u))


def surf_back(px):
    u = (px - CX) / (GR - 1.5)
    return SURF_Y - (GR - 1.5) * K * math.sqrt(max(0.0, 1 - u * u))


# ---------------------------------------------------------------- Animation
def wave(frame, period=FRAMES, phase=0.0):
    return math.sin(2 * math.pi * (frame / period + phase))


def book_bob(frame):
    # -1, 0 oder +1 px, weich: lange auf den Endpunkten verweilen
    v = wave(frame, phase=0.0)
    return -1 if v > 0.45 else (1 if v < -0.45 else 0)


BOOK_W, BOOK_H = 21, 27
BOOK_X0 = 29                       # linke Kante des Deckels (inkl. Ruecken)
BOOK_Y0 = 56
DEPTH = 2                          # sichtbare Buchdicke (schraeg nach rechts oben)


def book_rect(frame):
    y0 = BOOK_Y0 + book_bob(frame)
    return BOOK_X0, y0, BOOK_X0 + BOOK_W - 1, y0 + BOOK_H - 1


def book_center(frame):
    x0, y0, x1, y1 = book_rect(frame)
    return (x0 + x1 + 1) / 2 + 1, (y0 + y1 + 1) / 2


# ------------------------------------------------------------------ Glas
def liquid_value(px, py, frame):
    bx, by = book_center(frame)
    u = (px - CX) / GR
    thick = (1 - u * u) ** 0.7                       # Mitte: viel Leuchtvolumen
    dx, dy = (px - bx) / 17.0, (py - by) / 21.0
    r2 = dx * dx + dy * dy
    glow = 1.0 if r2 < 0.55 else math.exp(-(r2 - 0.55) * 1.6)
    pulse = 0.04 * wave(frame, phase=0.25)            # sanftes Atmen
    deep = max(0.0, (py - 82.0) / 16.0) * 0.12       # zum Boden hin satter
    side = -u * 0.07                                 # links etwas heller
    return -0.10 + 0.56 * thick + (0.55 + pulse) * glow - deep + side


def air_value(px, py):
    u = (px - CX) / GR
    near = clamp(1.0 - (SURF_Y - py) / 14.0, 0, 1)   # Schein vom Spiegel
    return 0.08 + 0.50 * near ** 3 + 0.22 * (1 - u * u) - 0.10 * u


def draw_glass(buf, frame):
    for y in range(H):
        for x in range(W):
            if not GMASK[y][x]:
                continue
            px, py = x + 0.5, y + 0.5
            d = GDIST[y][x]
            in_liquid = py > surf_back(px) if py < SURF_Y + 4 else True
            on_surface = surf_back(px) < py <= surf_front(px)

            if on_surface:
                # Spiegel in Aufsicht: hell, nach hinten etwas dunkler
                t = (py - surf_back(px)) / max(0.5, surf_front(px) - surf_back(px))
                c = rstep(LQ, 5 if t > 0.45 else 4)
            elif in_liquid:
                v = liquid_value(px, py, frame)
                if py - surf_front(px) < 2.5:
                    v -= 0.16                     # Schatten direkt unter dem Spiegel
                c = ramp(LQ, v)
            else:
                c = ramp(AIR, air_value(px, py))

            # Glaswand: innen eine dunklere Kante, auf der Schattenseite
            # (rechts) dahinter eine helle Lichtkante - das Glas hat Dicke.
            if d <= 2.05:
                if in_liquid and not on_surface:
                    c = rstep(LQ, 1 if px < CX else 0)
                else:
                    c = rstep(AIR, 1)
            elif d <= 3.05 and px > CX + 6:
                c = rstep(LQ, 6) if in_liquid and not on_surface else rstep(AIR, 4)
            buf.set(x, y, c, "glass")

    # Spiegelkanten: vorn eine helle Meniskuslinie, hinten eine dunkle Fuge
    for x in range(W):
        px = x + 0.5
        if abs(px - CX) > GR - 2.5:
            continue
        yf = int(math.floor(surf_front(px)))
        yb = int(math.floor(surf_back(px)))
        if GMASK[yf][x] and GDIST[yf][x] > 2.05:
            buf.set(x, yf, rstep(LQ, 7 if px < CX + 8 else 6))
        if GMASK[yb][x] and GDIST[yb][x] > 2.05:
            buf.set(x, yb, rstep(LQ, 3))


def glass_glints(buf, frame):
    """Reflexe auf der Aussenhaut: folgen der Normalen, also der Form."""
    for y in range(H):
        for x in range(W):
            if not GMASK[y][x] or GDIST[y][x] <= 1.05:
                continue
            px, py = x + 0.5, y + 0.5
            if py > GLASS_BOT - 3:
                continue
            nx, ny, nz = glass_normal(px, py)
            sp = spec(nx, ny, nz, 30)
            # Hauptstreifen links (Fensterreflex), mit einer Luecke
            if py >= CYL_TOP - 2:
                if -0.74 <= nx <= -0.56 and (py <= 67 or 80 <= py <= 91) and not (58 <= py <= 60):
                    buf.set(x, y, WHITE if -0.70 <= nx <= -0.60 else rstep(LQ, 6))
                elif -0.50 <= nx <= -0.46 and 48 <= py <= 66:
                    buf.set(x, y, rstep(LQ, 6))
                elif 0.70 <= nx <= 0.76 and 60 <= py <= 88:
                    buf.set(x, y, rstep(LQ, 5))          # Gegenglanz rechts
            else:
                # Kuppel: Bogenreflex oben links + Glanzpunkt
                dx = (px - CX) / GR
                dy = (py - CYL_TOP) / DOME_RY
                rr = math.sqrt(dx * dx + dy * dy)
                ang = math.degrees(math.atan2(dy, dx))      # -180..0 oben
                if 0.70 <= rr <= 0.86 and -168 <= ang <= -112:
                    buf.set(x, y, WHITE if rr <= 0.80 else (226, 196, 186))
                elif 0.56 <= rr <= 0.62 and -150 <= ang <= -128:
                    buf.set(x, y, AIR[3])                   # zweiter, innerer Bogen
                elif 0.80 <= rr <= 0.85 and -50 <= ang <= -22:
                    buf.set(x, y, AIR[4])


# ------------------------------------------------------------------ Buch
COOKIE = [
    "..###..",
    ".#####.",
    "#######",
    "#######",
    "#######",
    ".#####.",
    "..###..",
]


def draw_book(buf, frame):
    x0, y0, x1, y1 = book_rect(frame)
    p = buf.set

    # Leuchthof: Fluessigkeit direkt um das Buch zwei Stufen heller
    for y in range(y0 - DEPTH - 4, y1 + 5):
        for x in range(x0 - 4, x1 + DEPTH + 5):
            if not (0 <= x < W and 0 <= y < H) or not GMASK[y][x] or GDIST[y][x] <= 3.05:
                continue
            ex = max(x0 - x, 0, x - (x1 + DEPTH))
            ey = max(y0 - DEPTH - y, 0, y - y1)
            dd = math.sqrt(ex * ex + ey * ey)
            c = buf.get(x, y)
            if c in LQ and dd > 0:
                bump = 1 if dd <= 2.2 else 0
                p(x, y, rstep(LQ, LQ.index(c) + bump))

    # Seitenschnitt (Seiten) oben und rechts, schraeg nach hinten versetzt
    for y in range(y0 - DEPTH, y1 - DEPTH + 1):
        for x in range(x0 + DEPTH, x1 + DEPTH + 1):
            p(x, y, PAGE[2])
    for y in range(y0 - DEPTH, y1 - DEPTH + 1):            # rechte Seitenflaeche
        for k in range(1, DEPTH + 1):
            x = x1 + k
            p(x, y, PAGE[1] if (y - y0) % 3 == 1 else PAGE[2])
    for x in range(x0 + DEPTH, x1 + DEPTH + 1):            # obere Seitenflaeche
        p(x, y0 - DEPTH, PAGE[3])
        p(x, y0 - DEPTH + 1, PAGE[2] if x % 3 else PAGE[3])
    # hinterer Deckel lugt oben rechts heraus
    for x in range(x0 + DEPTH + 1, x1 + DEPTH + 1):
        p(x, y0 - DEPTH - 1, BK[2])
    for y in range(y0 - DEPTH, y1 - DEPTH):
        p(x1 + DEPTH + 1, y, BK[1])

    # Vorderdeckel
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            p(x, y, BK[3])
    # Licht von links oben, Schatten unten rechts
    for x in range(x0 + 4, x1):
        p(x, y0 + 1, BK[4])
    for y in range(y0 + 1, y1):
        p(x1 - 1, y, BK[2])
    for x in range(x0 + 4, x1):
        p(x, y1 - 1, BK[2])
    # Ruecken links, gerundet
    for y in range(y0, y1 + 1):
        p(x0, y, BK[2]); p(x0 + 1, y, BK[4]); p(x0 + 2, y, BK[3]); p(x0 + 3, y, BK[1])
    for band in (y0 + 4, y1 - 4):
        for x in range(x0, x0 + 3):
            p(x, band, BR[5] if x == x0 + 1 else BR[4])
            p(x, band + 1, BR[3])

    # Goldrahmen-Linie auf dem Deckel
    fx0, fx1, fy0, fy1 = x0 + 6, x1 - 3, y0 + 3, y1 - 3
    for x in range(fx0, fx1 + 1):
        p(x, fy0, BR[4]); p(x, fy1, BR[3])
    for y in range(fy0, fy1 + 1):
        p(fx0, y, BR[4]); p(fx1, y, BR[3])

    # Eckbeschlaege
    for (cx, cy, sx, sy) in ((x1, y0, -1, 1), (x1, y1, -1, -1)):
        for i in range(3):
            for j in range(3 - i):
                p(cx + sx * i, cy + sy * j, BR[5] if (i + j) == 0 else BR[4] if sy > 0 else BR[3])

    # Keks-Wappen in der Mitte
    mx = (fx0 + fx1) // 2 - 3
    my = y0 + 7
    for iy in range(7):
        for ix in range(7):
            if COOKIE[iy][ix] != "#":
                continue
            edge = any(not (0 <= ix + a < 7 and 0 <= iy + b < 7) or COOKIE[iy + b][ix + a] != "#"
                       for a, b in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            lit = (ix - 3) + (iy - 3) < 0
            if edge:
                c = BR[3] if not lit else BR[5]
            else:
                c = BR[5] if lit else BR[4]
            p(mx + ix, my + iy, c)
    for ix, iy in ((2, 2), (4, 3), (2, 4), (4, 5)):
        p(mx + ix, my + iy, WD[3])
    # Titelzeilen
    for x in range(mx - 1, mx + 8):
        p(x, my + 10, BR[4])
    for x in range(mx + 1, mx + 6):
        p(x, my + 12, BR[3])

    # Aussenlinie des Buchs (Deckel + Buchblock)
    shape = set()
    for y in range(y0 - DEPTH - 1, y1 + 1):
        for x in range(x0, x1 + DEPTH + 2):
            front = x0 <= x <= x1 and y0 <= y <= y1
            back = x0 + DEPTH <= x <= x1 + DEPTH + 1 and y0 - DEPTH - 1 <= y <= y1 - DEPTH
            if front or back:
                shape.add((x, y))
    for (x, y) in shape:
        if any((x + a, y + b) not in shape for a, b in ((1, 0), (-1, 0), (0, 1), (0, -1))):
            p(x, y, INK)
    # Ecken abrunden
    for (x, y) in ((x0, y0), (x0, y1), (x1 + DEPTH + 1, y0 - DEPTH - 1)):
        p(x, y, buf.get(x - 1, y) if (x - 1, y) not in shape else buf.get(x, y + 1))
    # Kante zwischen Deckel und Seitenschnitt
    for k in range(1, DEPTH + 1):
        p(x1 + k, y0 - k, INK)
    p(x0 + DEPTH - 1, y0 - 1, INK)
    for x in range(x0 + 1, x1 + 1):
        p(x, y0, INK if x < x0 + 4 else BK[1])
    p(x0 + 1, y0, INK)
    for x in range(x0 + 4, x1 + 1):
        p(x, y0, BK[1])

    # Schatten unter dem Buch in der Fluessigkeit
    for x in range(x0 + 2, x1 + 1):
        y = y1 + 1
        c = buf.get(x, y)
        if c in LQ:
            p(x, y, rstep(LQ, LQ.index(c) - 2))
        c = buf.get(x + 1, y + 1)
        if c in LQ and x < x1:
            p(x + 1, y + 1, rstep(LQ, LQ.index(c) - 1))


# ------------------------------------------------------- Mini-Trophaeen
# o = Kontur, h = Licht, m = Mitte, d = Schatten
STAR = [
    "....o....",
    "...oho...",
    "ooohhmooo",
    "ohhhmmmdo",
    ".ohmmmdo.",
    "..ommmo..",
    ".ommodmo.",
    ".omo.odo.",
    ".oo...oo.",
]
MEDAL = [
    "oo.oo",
    "ohodo",
    ".ooo.",
    "ohhmo",
    "ohmdo",
    "omddo",
    ".ooo.",
]
def draw_trinket(buf, pat, ox, oy, cols):
    for iy, row in enumerate(pat):
        for ix, ch in enumerate(row):
            if ch not in cols:
                continue
            x, y = ox + ix, oy + iy
            if not GMASK[y][x] or GDIST[y][x] <= 3.05:
                continue
            buf.set(x, y, cols[ch])


def draw_trinkets(buf, frame):
    def bob(ph):
        v = wave(frame, phase=ph)
        return int(round(v * 1.4))
    # dunkle Silhouetten in der Fluessigkeit, Oberkante angestrahlt
    gold = {"o": INK, "h": WHITE, "m": BR[5], "d": BR[3]}
    draw_trinket(buf, STAR, 17, 69 + bob(0.30), gold)
    medal = dict(gold)
    medal["o"] = INK
    draw_trinket(buf, MEDAL, 56, 49 + bob(0.65), medal)
    # Baendchen der Medaille rot
    for (ix, iy) in ((1, 1), (3, 1)):
        x, y = 56 + ix, 49 + bob(0.65) + iy
        if GMASK[y][x]:
            buf.set(x, y, BK[3] if ix == 1 else BK[2])


# ------------------------------------------------------------------ Blasen
# (x, y_start, y_ende, radius, Versatz in Frames). Jede Blase legt ihre
# Strecke in genau FRAMES Bildern zurueck -> nahtlose Schleife.
BUBBLES = [
    (24, 92, 46, 1, 0), (30, 94, 50, 2, 5), (45, 93, 46, 1, 9),
    (55, 92, 47, 2, 2), (60, 90, 52, 1, 12), (20, 88, 58, 1, 7),
    (36, 92, 84, 1, 3), (50, 90, 82, 1, 11), (40, 94, 46, 1, 14),
    (27, 76, 47, 1, 10), (58, 72, 47, 1, 6),
]


def draw_bubbles(buf, frame):
    def put(x, y, c):
        if 0 <= x < W and 0 <= y < H and GMASK[y][x] and GDIST[y][x] > 3.05:
            if y + 0.5 > surf_front(x + 0.5):
                buf.set(x, y, c)

    for (bx, ys, ye, r, off) in BUBBLES:
        t = ((frame + off) % FRAMES) / FRAMES
        # leicht beschleunigt aufsteigen
        y = int(round(ys + (ye - ys) * (t * 0.6 + t * t * 0.4)))
        x = bx + (1 if math.sin((frame + off) * 1.3 + bx) > 0.5 else 0)
        if y <= int(surf_front(x + 0.5)) + 1:
            # an der Oberflaeche: kleiner Ring
            yy = int(surf_front(x + 0.5))
            for dx in (-1, 1):
                if GMASK[yy][x + dx]:
                    buf.set(x + dx, yy, WHITE)
            continue
        if r <= 1:
            put(x, y, WHITE)
            c = buf.get(x, y + 1)
            if c in LQ:
                put(x, y + 1, rstep(LQ, LQ.index(c) + 1))
        else:
            ring = ((0, -1), (1, -1), (-1, 0), (2, 0), (-1, 1), (2, 1), (0, 2), (1, 2))
            for dx, dy in ring:
                c = buf.get(x + dx, y + dy)
                if c in LQ:
                    put(x + dx, y + dy, rstep(LQ, LQ.index(c) + 2))
            put(x, y, WHITE)
            c = buf.get(x + 1, y + 1)
            if c in LQ:
                put(x + 1, y + 1, rstep(LQ, LQ.index(c) + 1))


DROPS = [(24, 30), (31, 22), (55, 27), (60, 37), (19, 40), (47, 17), (36, 34)]


def draw_drops(buf):
    """Kondenstropfen innen an der Kuppel: Lichtpunkt oben, Schatten drunter."""
    for (x, y) in DROPS:
        if not GMASK[y][x] or GDIST[y][x] <= 2.5 or y + 1.5 > surf_back(x + 0.5):
            continue
        buf.set(x, y, (226, 196, 186))
        buf.set(x, y + 1, AIR[0])


def draw_sparks(buf, frame):
    """Funken in der Kuppel-Luft, die auf- und abblenden."""
    sparks = [(27, 26, 0), (51, 20, 5), (38, 15, 10), (58, 33, 3), (22, 36, 8), (45, 31, 13)]
    for (x, y, off) in sparks:
        st = (frame + off) % FRAMES
        if st >= 6 or not GMASK[y][x] or GDIST[y][x] <= 2.5:
            continue
        rise = st // 2
        yy = y - rise
        if st in (2, 3):
            buf.set(x, yy, WHITE)
            for a, b in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                if GMASK[yy + b][x + a]:
                    buf.set(x + a, yy + b, AIR[5])
        else:
            buf.set(x, yy, AIR[5] if st != 5 else AIR[4])


# ----------------------------------------------------------------- Sockel
# Der Sockel rechnet in ganzen Zeilen: jede Spalte bekommt einen festen
# Ellipsen-Versatz e(x), und alle Kanten eines Teils laufen um genau diesen
# Versatz verschoben. So sind Lichtkante, Baender und Fuge exakt parallel -
# einzeln gerundete Kurven bekommen ungleiche Treppenstufen und flimmern.
class Ring:
    def __init__(self, r, top, height):
        self.r, self.top, self.h = r, top, height
        self.ry = r * K

    def u(self, x):
        return (x + 0.5 - CX) / self.r

    def e(self, x):
        u = self.u(x)
        if abs(u) > 1.0:
            return None
        return int(round(self.ry * math.sqrt(1.0 - u * u)))

    def rows_top(self, x):
        e = self.e(x)
        return [] if e is None else list(range(self.top - e, self.top + e + 1))

    def rows_side(self, x):
        e = self.e(x)
        return [] if e is None else list(range(self.top + e + 1, self.top + e + self.h + 1))


COLLAR_R = Ring(29.5, 95, 6)       # Messingkragen
DRUM_R = Ring(32.5, 104, 10)       # Holztrommel
PLINTH_R = Ring(36.5, 117, 3)      # Sockelplatte


def side_light(ring, x):
    u = clamp(ring.u(x), -1, 1)
    nz = math.sqrt(max(0.0, 1 - u * u))
    return lambert(u, 0.0, nz), spec(u, 0.0, nz, 40)


def paint_ring(buf, ring, part, side_fn, top_fn):
    for x in range(W):
        e = ring.e(x)
        if e is None:
            continue
        for y in ring.rows_top(x):
            buf.set(x, y, top_fn(x, y - (ring.top - e), 2 * e + 1), part)
        for k, y in enumerate(ring.rows_side(x)):
            buf.set(x, y, side_fn(x, k), part)


def glass_bottom(x):
    """Letzte Glaszeile in dieser Spalte."""
    ys = [y for y in range(H) if GMASK[y][x]]
    return max(ys) if ys else None


def draw_collar(buf, frame):
    r = COLLAR_R

    def side(x, k):
        l, sp = side_light(r, x)
        v = 0.06 + 0.78 * l ** 1.2 + 0.7 * sp
        if k == 0:
            v += 0.20                          # Lichtkante
        elif k == r.h - 1:
            v -= 0.30                          # Fuge unten
        elif k == r.h - 2:
            v -= 0.08
        return ramp(BR, clamp(v, 0, 1))

    def top(x, k, n):
        u = r.u(x)
        v = 0.62 - 0.18 * u + (0.10 if abs(u) < 0.55 else 0)   # Glasschein vorn
        if k == n - 1:
            v += 0.16                          # Vorderkante
        return ramp(BR, clamp(v, 0, 1))

    paint_ring(buf, r, "collar", side, top)

    # Nieten
    for u in (-0.86, -0.6, -0.3, 0.0, 0.3, 0.6, 0.86):
        x = int(math.floor(CX + u * r.r))
        rows = r.rows_side(x)
        if len(rows) < 4:
            continue
        y = rows[3]
        buf.set(x, y, BR[6] if u < 0.35 else BR[5])
        buf.set(x + 1, y, BR[2] if u < 0.35 else BR[1])
        buf.set(x, y + 1, BR[2])
        buf.set(x + 1, y + 1, BR[1])


def draw_drum(buf, frame):
    r = DRUM_R

    def side(x, k):
        l, sp = side_light(r, x)
        if k in (0, 1, r.h - 2, r.h - 1):      # Messingbaender
            v = 0.04 + 0.80 * l ** 1.2 + 0.6 * sp
            v += {0: 0.18, 1: -0.04, r.h - 2: 0.04, r.h - 1: -0.30}[k]
            return ramp(BR, clamp(v, 0, 1))
        v = 0.02 + 0.70 * l ** 1.3 + 0.25 * sp
        if k == 2:
            v -= 0.18                          # Schatten unter dem Band
        return ramp(WD, clamp(v, 0, 1))

    def top(x, k, n):
        u = r.u(x)
        v = 0.70 - 0.20 * u
        if k == n - 1:
            v += 0.14
        return ramp(WD, clamp(v, 0, 1))

    paint_ring(buf, r, "drum", side, top)

    # Fugen der Holzdauben
    for k in range(-5, 6):
        x = int(math.floor(CX + math.sin(k * 0.27) * r.r))
        for kk, y in enumerate(r.rows_side(x)):
            if 3 <= kk <= r.h - 3 and buf.get(x, y) in WD:
                c = buf.get(x, y)
                buf.set(x, y, rstep(WD, WD.index(c) - 1))

    draw_medallion(buf, CX, r.top + r.e(39) + 1 + r.h / 2.0)


# Keks-Medaillon vorn auf der Trommel: Messingring, darin ein Schokokeks
COOKIE_CHIPS = [(-2, -2), (1, -3), (2, 0), (-2, 1), (0, 2), (-1, -1)]


def draw_medallion(buf, mx, my):
    for y in range(int(my) - 7, int(my) + 7):
        for x in range(int(mx) - 7, int(mx) + 7):
            dx, dy = x + 0.5 - mx, y + 0.5 - my
            d = math.sqrt(dx * dx + dy * dy)
            if d > 6.0:
                continue
            ang = (dx * -0.7 + dy * -0.7) / max(d, 0.01)       # +1 = oben links
            if d > 5.0:
                c = INK
            elif d > 3.6:
                v = 0.55 + 0.35 * ang
                if d > 4.4 and ang > 0.3:
                    v += 0.2                                # Lichtkante aussen
                c = ramp(BR, clamp(v, 0, 1))
            elif d > 2.9 and ang < 0.2:
                c = WD[2]                                   # Keksrand im Schatten
            else:
                v = 0.62 + 0.30 * ang
                c = ramp(WD, clamp(v, 0, 1))
            buf.set(x, y, c)
    cx, cy = int(math.floor(mx)), int(math.floor(my))
    for (a, b) in COOKIE_CHIPS:
        buf.set(cx + a, cy + b, WD[1])
    buf.set(cx - 2, cy - 3, WD[6])
    buf.set(cx - 3, cy - 2, WD[6])
    # Glanzpunkt auf dem Ring
    buf.set(cx - 3, cy - 4, BR[6])
    buf.set(cx - 4, cy - 3, BR[6])


def draw_plinth(buf):
    r = PLINTH_R

    def side(x, k):
        l, sp = side_light(r, x)
        v = -0.02 + 0.60 * l ** 1.3 + 0.2 * sp
        if k == 0:
            v += 0.16
        if k == r.h - 1:
            v -= 0.12
        return ramp(WD, clamp(v, 0, 1))

    def top(x, k, n):
        u = r.u(x)
        v = 0.56 - 0.20 * u
        if k == n - 1:
            v += 0.16
        return ramp(WD, clamp(v, 0, 1))

    paint_ring(buf, r, "plinth", side, top)
    # Fuesse
    for fx in (7, 68):
        top_y = max(r.rows_side(fx + 2)) + 1
        for y in range(top_y, min(H, top_y + 2)):
            for x in range(fx, fx + 5):
                buf.set(x, y, BR[4] if x == fx + 1 else BR[3] if x < fx + 3 else BR[2], "foot")


def contact_shadows(buf):
    """Wo ein Teil auf dem naechsten steht, liegt eine dunkle Fuge."""
    for upper, lower, part in ((COLLAR_R, DRUM_R, "drum"), (DRUM_R, PLINTH_R, "plinth")):
        for x in range(W):
            rs = upper.rows_side(x)
            if not rs:
                continue
            for k in (1, 2):
                y = rs[-1] + k
                if buf.partof(x, y) != part:
                    continue
                c = buf.get(x, y)
                for rmp in (WD, BR):
                    if c in rmp:
                        buf.set(x, y, rstep(rmp, rmp.index(c) - (3 if k == 1 else 1)))


def glass_seat(buf):
    """Spalt zwischen Glas und Kragen + Glasschein auf dem Ring."""
    for x in range(W):
        yb = glass_bottom(x)
        if yb is None:
            continue
        y = yb + 1
        if buf.partof(x, y) == "collar":
            buf.set(x, y, BR[1])
            if buf.partof(x, y + 1) == "collar" and (y + 1) in COLLAR_R.rows_top(x):
                u = abs(COLLAR_R.u(x))
                buf.set(x, y + 1, rstep(BR, 6 if u < 0.45 else 5))


# ----------------------------------------------------------------- Kappe
CAP_R = Ring(9.5, 9, 3)            # Messingkappe auf dem Scheitel


def draw_cap(buf):
    r = CAP_R
    # Schatten der Kappe auf der Kuppel
    for x in range(W):
        rs = r.rows_side(x)
        if rs and GMASK[rs[-1] + 1][x]:
            buf.set(x, rs[-1] + 1, AIR[0])

    def side(x, k):
        l, sp = side_light(r, x)
        v = 0.04 + 0.8 * l ** 1.2 + 0.8 * sp + (0.18 if k == 0 else -0.25 if k == r.h - 1 else 0)
        return ramp(BR, clamp(v, 0, 1))

    def top(x, k, n):
        return ramp(BR, clamp(0.66 - 0.2 * r.u(x) + (0.14 if k == n - 1 else 0), 0, 1))

    paint_ring(buf, r, "cap", side, top)
    # Hals + Knauf
    for x in range(38, 42):
        buf.set(x, 6, BR[4] if x < 40 else BR[2], "cap")
        buf.set(x, 7, BR[3] if x < 40 else BR[1], "cap")
    kx, ky, kr = CX, 3.5, 3.1
    for y in range(0, 8):
        for x in range(32, 48):
            px, py = x + 0.5, y + 0.5
            dx, dy = (px - kx) / kr, (py - ky) / kr
            if dx * dx + dy * dy > 1:
                continue
            nz = math.sqrt(max(0.0, 1 - dx * dx - dy * dy))
            v = 0.08 + 0.8 * lambert(dx, dy, nz) + 0.9 * spec(dx, dy, nz, 25)
            buf.set(x, y, ramp(BR, clamp(v, 0, 1)), "cap")


# --------------------------------------------------------------- Aussenlinie
def outline(buf):
    solid = [[buf.px[y][x] is not None for x in range(W)] for y in range(H)]
    out = []
    for y in range(H):
        for x in range(W):
            if not solid[y][x]:
                continue
            for a, b in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + a, y + b
                if not (0 <= nx < W and 0 <= ny < H) or not solid[ny][nx]:
                    out.append((x, y))
                    break
    for (x, y) in out:
        buf.set(x, y, INK2 if y >= 118 else INK)


def build(frame):
    buf = Buf()
    draw_plinth(buf)
    draw_drum(buf, frame)
    draw_collar(buf, frame)
    contact_shadows(buf)
    draw_glass(buf, frame)
    glass_seat(buf)
    draw_trinkets(buf, frame)
    draw_book(buf, frame)
    draw_bubbles(buf, frame)
    draw_drops(buf)
    draw_sparks(buf, frame)
    glass_glints(buf, frame)
    draw_cap(buf)
    outline(buf)
    return buf.image()


# --------------------------------------------------------------------- Meta
def write_meta(png_path, template):
    meta = png_path + ".meta"
    if os.path.exists(meta) or not os.path.exists(template):
        return
    with open(template, "r", encoding="utf-8") as f:
        txt = f.read()
    import re
    txt = re.sub(r"guid: [0-9a-f]{32}", "guid: " + uuid.uuid4().hex, txt, count=1)
    txt = re.sub(r"spriteID: [0-9a-f]{32}", "spriteID: " + uuid.uuid4().hex, txt)
    txt = re.sub(r"internalID: -?\d+", "internalID: %d" % random.randint(10 ** 8, 2 ** 31 - 1), txt)
    with open(meta, "w", encoding="utf-8", newline="\n") as f:
        f.write(txt)
    print("meta angelegt:", meta)


def frame_name(i):
    return "kapsel_erfolgsbuch.png" if i == 0 else "kapsel_erfolgsbuch_%d.png" % (i + 1)


if __name__ == "__main__":
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    out = args[0] if args else "."
    os.makedirs(out, exist_ok=True)
    frames = [build(f) for f in range(FRAMES)]
    template = os.path.join(out, "kapsel_erfolgsbuch.png.meta")
    for i, im in enumerate(frames):
        path = os.path.join(out, frame_name(i))
        im.save(path)
        write_meta(path, template)
    print("geschrieben: %d Frames nach %s" % (FRAMES, out))

    if "--vorschau" in sys.argv:
        sc = 5
        big = [f.resize((W * sc, H * sc), Image.NEAREST) for f in frames]
        bg = (52, 40, 58, 255)
        gif = []
        for b in big:
            g = Image.new("RGBA", b.size, bg)
            g.alpha_composite(b)
            gif.append(g.convert("RGB"))
        gif[0].save(os.path.join(out, "vorschau.gif"), save_all=True,
                    append_images=gif[1:], duration=100, loop=0)
        sheet = Image.new("RGBA", (W * sc * 2 + 30, H * sc + 20), bg)
        sheet.alpha_composite(big[0], (10, 10))
        sheet.alpha_composite(big[FRAMES // 2], (W * sc + 20, 10))
        sheet.save(os.path.join(out, "vorschau.png"))
