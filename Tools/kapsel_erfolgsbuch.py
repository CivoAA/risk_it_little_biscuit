# -*- coding: utf-8 -*-
"""Erzeugt die Erfolgs-Kapsel fuer den Hub: 80x128, drei Frames (Blasen + Funkeln).

Aufruf aus dem Projektordner:
    python Tools/kapsel_erfolgsbuch.py Assets/Art/World-Objects

Schreibt kapsel_erfolgsbuch.png (Frame 1), _2.png und _3.png. Die .meta-Dateien
bleiben liegen, die GUIDs aendern sich also nicht. Braucht nur Pillow.

Stilregeln, abgelesen an altar.png, goldstatue5.png, werkstatt.png und bib.png:
  * Jede Farbe hier kommt aus einem bestehenden Hub-Asset - nichts Neues erfunden.
  * Kein Dithering. Geschattet wird in flaechigen Baendern mit klarer Kante.
  * Wenige Stufen pro Material, niedriger Kontrast, entsaettigt.
  * Eine dunkle Aussenlinie, Glanzlichter als 1px-Linie in Creme.
"""
import math, os, sys
from PIL import Image

W, H = 80, 128

# ------------------------------------------------- Palette (alle aus dem Hub)
INK     = (59, 36, 51)      # 3B2433  Aussenlinie
INK2    = (14, 19, 53)      # 0E1335  tiefster Schatten
PLUM    = (99, 65, 69)      # 634145  weiche Schattenkante
MAUVE   = (121, 65, 76)     # 79414C

WD_D    = (131, 90, 80)     # 835A50
WD      = (146, 85, 39)     # 925527
WD_M    = (159, 90, 53)     # 9F5A35
WD_L    = (177, 119, 62)    # B1773E
WD_L2   = (182, 121, 72)    # B67948

GOLD_D  = (196, 147, 71)    # C49347
GOLD    = (214, 176, 80)    # D6B050
GOLD_L  = (217, 182, 107)   # D9B66B
CREAM   = (230, 206, 141)   # E6CE8D
CREAM2  = (230, 196, 147)   # E6C493
WHITE   = (245, 237, 202)   # F5EDCA
PEACH   = (225, 171, 116)   # E1AB74

BOOK_D  = (87, 20, 69)      # 571445
BOOK    = (124, 34, 71)     # 7C2247
BOOK_L  = (150, 45, 86)     # 962D56
BOOK_H  = (167, 69, 113)    # A74571

# Leuchtkern von hell nach satt. Nur vier klar getrennte Stufen - mehr ergeben
# konzentrische Ringe statt einer leuchtenden Roehre, und der Hub schattiert
# ohnehin in wenigen Flaechen pro Material.
GLOW = [CREAM, GOLD, GOLD_D, WD_L]

SPARK = WHITE


class Buf:
    def __init__(self):
        self.px = [[None] * W for _ in range(H)]

    def set(self, x, y, c):
        if 0 <= x < W and 0 <= y < H:
            self.px[y][x] = c

    def get(self, x, y):
        return self.px[y][x] if 0 <= x < W and 0 <= y < H else None

    def rect(self, x0, x1, y0, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.set(x, y, c)

    def image(self):
        im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        d = im.load()
        for y in range(H):
            for x in range(W):
                c = self.px[y][x]
                if c is not None:
                    d[x, y] = (c[0], c[1], c[2], 255)
        return im


# ------------------------------------------------------- Kapsel-Silhouette
CX = 40.0            # Mittelachse liegt auf der Pixelgrenze 39|40
RX = 28.0            # halbe Breite der Roehre
DOME_CY = 36.5       # Mittelpunkt der Kuppel-Ellipse
DOME_RY = 32.5
GLASS_TOP = 4
GLASS_BOT = 104      # darunter deckt der Sockel ab


def in_glass(x, y):
    px, py = x + 0.5, y + 0.5
    if py > GLASS_BOT + 0.5 or py < GLASS_TOP:
        return False
    if py >= DOME_CY:
        return abs(px - CX) <= RX
    dy = (DOME_CY - py) / DOME_RY
    if dy > 1.0:
        return False
    return abs(px - CX) <= RX * math.sqrt(1.0 - dy * dy)


def distance_field(mask):
    """Chamfer-Distanz bis zum ersten Pixel ausserhalb der Maske."""
    INF = 9999.0
    d = [[0.0 if not mask[y][x] else INF for x in range(W)] for y in range(H)]
    for y in range(H):
        for x in range(W):
            if d[y][x] == 0.0:
                continue
            best = d[y][x]
            for dx, dy, w in ((-1, 0, 1.0), (0, -1, 1.0), (-1, -1, 1.41421), (1, -1, 1.41421)):
                nx, ny = x + dx, y + dy
                v = (d[ny][nx] if 0 <= nx < W and 0 <= ny < H else 0.0) + w
                if v < best:
                    best = v
            d[y][x] = best
    for y in range(H - 1, -1, -1):
        for x in range(W - 1, -1, -1):
            best = d[y][x]
            for dx, dy, w in ((1, 0, 1.0), (0, 1, 1.0), (1, 1, 1.41421), (-1, 1, 1.41421)):
                nx, ny = x + dx, y + dy
                v = (d[ny][nx] if 0 <= nx < W and 0 <= ny < H else 0.0) + w
                if v < best:
                    best = v
            d[y][x] = best
    return d


MASK = [[in_glass(x, y) for x in range(W)] for y in range(H)]
DIST = distance_field(MASK)


def is_rim(x, y):
    """Silhouettenpixel mit freiem 4er-Nachbarn - so bleibt die Linie geschlossen."""
    if not MASK[y][x]:
        return False
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        nx, ny = x + dx, y + dy
        if not (0 <= nx < W and 0 <= ny < H) or not MASK[ny][nx]:
            return True
    return False


def cavity(x, y):
    return 0 <= x < W and 0 <= y < H and MASK[y][x] and DIST[y][x] > 4.4


def glow_step(x, y):
    """Welche Stufe der Leuchtrampe gehoert hierhin? Die Helligkeit haengt vor
    allem am Abstand zur Glaswand und an der Hoehe - so entstehen Baender, die
    der Roehre folgen, statt konzentrischer Ringe. Bewusst hart gestuft: der
    Hub schattiert in Flaechen, nicht in Verlaeufen."""
    px, py = x + 0.5, y + 0.5
    wall = max(0.0, min(1.0, (17.0 - DIST[y][x]) / 13.0))      # Rand wird satt
    tief = max(0.0, min(1.0, (py - 60.0) / 42.0))              # unten wird satt
    dx, dy = (px - CX) / 20.0, (py - 50.0) / 24.0
    kern = max(0.0, 1.0 - math.sqrt(dx * dx + dy * dy))        # Licht um das Buch
    seite = (px - CX) / 28.0                                   # Licht von links oben
    t = 0.35 + wall * 3.0 + tief * 1.3 + seite * 0.5 - kern * 1.4
    return max(0, min(len(GLOW) - 1, int(round(t))))


# ------------------------------------------------------------------- Glas
def draw_glass(buf):
    for y in range(H):
        for x in range(W):
            if not MASK[y][x]:
                continue
            if is_rim(x, y):
                buf.set(x, y, INK)
                continue
            d = DIST[y][x]
            if d <= 2.05:
                buf.set(x, y, WHITE)          # Lichtkante der Glaswand
            elif d <= 3.05:
                buf.set(x, y, CREAM)
            elif d <= 4.05:
                buf.set(x, y, GOLD_D)         # Innenkante, trennt Wand vom Kern
            else:
                buf.set(x, y, GLOW[glow_step(x, y)])


def draw_glass_light(buf):
    """Glanz im Glas. Die Lichter liegen auf konstantem Abstand zur Aussenkante,
    folgen also der Form und reissen nirgends auf. Zwischen Wand und Glanz bleibt
    ein satter Streifen stehen - sonst verschwimmt beides zu einer breiten
    hellen Kante und die Roehre wirkt flach."""
    for y in range(H):
        for x in range(W):
            if not cavity(x, y):
                continue
            d = DIST[y][x]
            if not (7.0 < d <= 9.0):
                continue
            inner = d > 8.0
            left = x < CX - 8
            if 10 <= y <= 28 and not inner:
                buf.set(x, y, WHITE)                    # Bogen unter der Kuppel
            elif left and 26 <= y <= 58:
                buf.set(x, y, WHITE if not inner else CREAM)
            elif left and y <= 72 and not inner:
                buf.set(x, y, CREAM)
            elif not left and 46 <= y <= 66 and not inner:
                buf.set(x, y, GOLD_L)                   # knapper Gegenglanz rechts

    # kurzer, schmaler Streifen weiter innen
    for y in range(50, 76):
        if cavity(25, y):
            buf.set(25, y, CREAM if y < 70 else GOLD_L)


# --------------------------------------------------------------------- Buch
# Deutlich kleiner als vorher: 24x32 statt 35x48. Der Einband ist weinrot wie
# die Buecher im Regal (bib.png) - Braun wuerde im Goldlicht verschwinden.
BK_L, BK_R = 28, 51
BK_T, BK_B = 36, 67
SP_W = 4                      # Breite des Buchruecken

# Keks-Medaillon, 9x9, von Hand gesetzt
MEDAL = [
    "..#####..",
    ".#######.",
    "#########",
    "#########",
    "#########",
    "#########",
    "#########",
    ".#######.",
    "..#####..",
]


def draw_medal(buf, ox, oy, face, shade, line, chip):
    ins = lambda ix, iy: 0 <= ix < 9 and 0 <= iy < 9 and MEDAL[iy][ix] == "#"
    for iy in range(9):
        for ix in range(9):
            if not ins(ix, iy):
                continue
            rand = any(not ins(ix + dx, iy + dy) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            buf.set(ox + ix, oy + iy, line if rand else (face if (ix - 4) + (iy - 4) <= 0 else shade))
    for ix, iy in ((3, 2), (6, 3), (2, 5), (5, 6)):
        buf.set(ox + ix, oy + iy, chip)


def draw_book(buf):
    p = buf.set

    # Schatten des schwebenden Buchs im Lichtnebel
    for x in range(BK_L + 3, BK_R - 1):
        for dy in range(3, 6):
            y = BK_B + dy
            if not cavity(x, y):
                continue
            if min(x - (BK_L + 3), (BK_R - 2) - x) < dy - 2:
                continue
            p(x, y, GLOW[min(len(GLOW) - 1, glow_step(x, y) + 1)])

    buf.rect(BK_L, BK_R, BK_T, BK_B, BOOK_L)

    # Buchruecken links, mit einer Lichtkante
    buf.rect(BK_L, BK_L + SP_W, BK_T, BK_B, BOOK_D)
    for y in range(BK_T + 1, BK_B):
        p(BK_L + 1, y, BOOK)
    for band in (BK_T + 6, BK_B - 8):
        for x in range(BK_L + 1, BK_L + SP_W + 1):
            p(x, band, GOLD)

    # Seitenschnitt: unten dick, rechts eine schmale Kante
    buf.rect(BK_L + SP_W + 1, BK_R - 1, BK_B - 2, BK_B - 1, WHITE)
    for x in range(BK_L + SP_W + 2, BK_R - 1):
        p(x, BK_B - 1, CREAM2)
    for y in range(BK_T + 2, BK_B - 2):
        p(BK_R - 1, y, CREAM2)

    # Deckel
    buf.rect(BK_L + SP_W + 1, BK_R - 2, BK_T + 1, BK_B - 3, BOOK_L)
    for x in range(BK_L + SP_W + 1, BK_R - 1):
        p(x, BK_T + 1, BOOK_H)                  # Licht von oben links
    for y in range(BK_T + 1, BK_B - 2):
        p(BK_L + SP_W + 1, y, BOOK_H)
    for y in range(BK_T + 2, BK_B - 2):
        p(BK_R - 2, y, BOOK)
    for x in range(BK_L + SP_W + 2, BK_R - 1):
        p(x, BK_B - 3, BOOK)

    draw_medal(buf, 36, 43, GOLD_L, GOLD, INK, WD_M)

    # zwei gepraegte Zeilen als "Titel"
    for y, x0, x1 in ((56, 37, 45), (59, 39, 43)):
        for x in range(x0, x1 + 1):
            p(x, y, GOLD)

    # Aussenlinie, Ecken gerundet
    for x in range(BK_L + 1, BK_R):
        p(x, BK_T, INK); p(x, BK_B, INK)
    for y in range(BK_T + 1, BK_B):
        p(BK_L, y, INK); p(BK_R, y, INK)
    for x, y in ((BK_L, BK_T), (BK_R, BK_T), (BK_L, BK_B), (BK_R, BK_B)):
        buf.set(x, y, GLOW[glow_step(x, y)])


# ------------------------------------------------------------------ Sockel
# (y0, y1, x0, x1) von oben nach unten - der Sockel liegt ueber dem Glas
TIERS = [
    (96, 103, 7, 72),      # Kragen, in dem die Kapsel steckt
    (104, 107, 11, 68),    # Taille
    (108, 117, 8, 71),     # Korpus mit Wappen
    (118, 121, 4, 75),     # Stufe
    (122, 127, 2, 77),     # Grundplatte
]


def draw_base(buf):
    solid = [[False] * W for _ in range(H)]
    for (y0, y1, x0, x1) in TIERS:
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                if y == y0 and (x == x0 or x == x1) and y0 in (96, 118, 122):
                    continue                       # obere Ecken leicht brechen
                solid[y][x] = True

    for (y0, y1, x0, x1) in TIERS:
        h = y1 - y0
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                if not solid[y][x]:
                    continue
                k = y - y0
                if k == 0:
                    c = WD_L
                elif k == 1:
                    c = WD_L2
                elif k >= h:
                    c = PLUM
                elif k >= h - 1:
                    c = WD
                else:
                    c = WD_M
                buf.set(x, y, c)

        # Zierrillen im Korpus, links und rechts vom Wappen
        if y0 == 108:
            for gy in (110, 114):
                for seg in (range(12, 34), range(47, 68)):
                    for x in seg:
                        buf.set(x, gy, WD)
                        buf.set(x, gy + 1, WD_L)

        # Seitenlicht: links eine Spur heller, rechts abgedunkelt
        for y in range(y0 + 2, y1):
            if solid[y][x0]:
                buf.set(x0, y, WD_L)
            if solid[y][x0 + 1]:
                buf.set(x0 + 1, y, WD_L2)
            for off in (0, 1):
                if solid[y][x1 - off]:
                    buf.set(x1 - off, y, WD if off else WD_D)

    draw_medal(buf, 36, 108, GOLD, GOLD_D, INK, WD)

    # geschlossene Aussenlinie um den ganzen Sockel
    for y in range(95, H):
        for x in range(W):
            if not solid[y][x]:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                out = not (0 <= nx < W and 0 <= ny < H) or not solid[ny][nx]
                if out and not (dy == -1 and y == 96):
                    buf.set(x, y, INK if y < 124 else INK2)
                    break
    for x in range(2, 78):
        if solid[96][x] and not MASK[95][x]:
            buf.set(x, 96, INK)

    # Lichtabdruck der Kapsel auf dem Kragen
    for x in range(W):
        if not MASK[95][x]:
            continue
        for y, c in ((96, CREAM2), (97, PEACH)):
            if buf.get(x, y) not in (None, INK, INK2):
                buf.set(x, y, c)


# ---------------------------------------------------------- Funkeln/Blasen
SPARKS = [(27, 28, 1), (52, 22, 0), (63, 44, 1), (19, 60, 0), (60, 72, 1),
          (29, 92, 0), (47, 31, 2), (33, 95, 1), (63, 90, 0), (17, 46, 2),
          (36, 18, 0), (21, 74, 1)]

BUBBLES = [  # x, start-y, radius, tempo
    (28, 90, 2, 7), (47, 86, 1, 9), (34, 72, 1, 6), (56, 80, 2, 8),
    (22, 84, 1, 10), (61, 64, 1, 7), (40, 92, 2, 9), (51, 56, 1, 6),
    (25, 50, 1, 8), (64, 53, 1, 9), (44, 78, 1, 11), (18, 66, 1, 7),
]
BUB_TOP, BUB_BOT = 13, 94
# Blasen vor dem dunklen Buch: gedaempfte Toene, sonst stanzen sie Loecher hinein
BUB_DIM = {WHITE: BOOK_H, CREAM: BOOK_L, GOLD_L: BOOK_L, GOLD_D: BOOK_D}


def draw_sparks(buf, frame):
    for (x, y, phase) in SPARKS:
        stage = (frame + phase) % 3
        if stage == 2 or not cavity(x, y):
            continue
        buf.set(x, y, SPARK)
        if stage == 0:
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                if cavity(x + dx, y + dy):
                    buf.set(x + dx, y + dy, CREAM)


def draw_bubbles(buf, frame):
    def put(x, y, c):
        if not cavity(x, y):
            return
        if BK_L <= x <= BK_R and BK_T <= y <= BK_B:
            c = BUB_DIM.get(c, c)
        buf.set(x, y, c)

    span = BUB_BOT - BUB_TOP
    for (bx, by, r, speed) in BUBBLES:
        y = BUB_TOP + ((by - BUB_TOP) - speed * frame) % span
        x = bx + ((frame + bx) % 2 if r > 1 else 0)
        if r <= 1:
            put(x, y, WHITE)
            put(x, y + 1, CREAM)
        else:
            for dx, dy, c in ((0, -1, WHITE), (1, -1, WHITE), (-1, 0, WHITE),
                              (0, 0, CREAM), (1, 0, CREAM), (2, 0, CREAM),
                              (-1, 1, CREAM), (0, 1, GOLD_D), (1, 1, GOLD_D), (2, 1, GOLD_D)):
                put(x + dx, y + dy, c)


def build(frame):
    buf = Buf()
    draw_glass(buf)
    draw_glass_light(buf)
    draw_book(buf)
    draw_sparks(buf, frame)
    draw_bubbles(buf, frame)
    draw_base(buf)
    return buf.image()


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    os.makedirs(out, exist_ok=True)
    names = ["kapsel_erfolgsbuch.png", "kapsel_erfolgsbuch_2.png", "kapsel_erfolgsbuch_3.png"]
    for f, name in enumerate(names):
        path = os.path.join(out, name)
        build(f).save(path)
        print("geschrieben:", path)
