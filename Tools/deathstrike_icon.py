"""
Zeichnet das Deathstrike-Icon (vorher ein gruenes Schwert).

  Assets/Resources/Shop/deathstrike.png          64x64  Shop-/Waffen-/Unlock-Icon
  Assets/Resources/Workbench/deathstrike_14.png  14x14
  Assets/Resources/Workbench/deathstrike_10.png  10x10

Ingame schlaegt die Waffe als gruener Blitz auf zufaellige Gegner ein
(Art/Waffen/fin_green_lightning.png). Das grosse Icon: ein Totenschaedel, der
aus einer Gewitterwolke ragt, mit gruen gluehenden Augen, und der den
Todesblitz aus dem Maul nach unten speit - mit Verästelungen, gruenem Schein
und Einschlag am Boden. Die Werkbank-Icons bleiben Wolke + Blitz (bei 10/14 px
liest sich ein Schaedel nicht). Gruentoene aus fin_green_lightning.

Die .meta von deathstrike.png behaelt guid, Sprite-Namen und IDs (Player-Prefab
+ World Map verweisen darauf); nur Rechteck und PPU sind auf 64 gestellt.

Aufruf aus dem Projektordner:  python Tools/deathstrike_icon.py [--preview pfad.png] [--dry]
"""

import math
import os
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ICON = os.path.join(ROOT, "Assets", "Resources", "Shop", "deathstrike.png")
WORKBENCH = os.path.join(ROOT, "Assets", "Resources", "Workbench")

CLEAR = (0, 0, 0, 0)
S = 64


def hx(s, a=255):
    s = s.lstrip("#")
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), a)


LINE = hx("#0f0c16")
# Gewitterwolke, dunkel -> hell
C_DEEP, C_DARK, C_MID, C_LIGHT, C_HI = hx("#221e33"), hx("#332e4a"), hx("#4b4566"), hx("#686284"), hx("#8d88a8")
# Wolke von unten gruen angestrahlt
C_GLOW, C_GLOW2 = hx("#2f5a4e"), hx("#3f8463")
# Knochen, gruenlich kalt
B_DEEP, B_DARK, B_MID, B_LIGHT, B_HI = hx("#3a4148"), hx("#66727a"), hx("#9ea9a8"), hx("#cdd6cc"), hx("#eef4ea")
# Blitz (aus fin_green_lightning)
G_DEEP, G_DARK, G_MID, G_CORE, G_WHITE = hx("#0b4a25"), hx("#137437"), hx("#44c574"), hx("#baf7d2"), hx("#f6fffa")

# Licht von oben links
LX, LY = -0.62, -0.78


# ---------------------------------------------------------------- Geometrie

def seg_dist(px_, py_, ax, ay, bx, by):
    vx, vy = bx - ax, by - ay
    t = max(0.0, min(1.0, ((px_ - ax) * vx + (py_ - ay) * vy) / (vx * vx + vy * vy)))
    return math.hypot(px_ - (ax + t * vx), py_ - (ay + t * vy))


def line_dist(x, y, pts):
    return min(seg_dist(x, y, *pts[i], *pts[i + 1]) for i in range(len(pts) - 1))


def in_ellipse(x, y, cx, cy, rx, ry):
    return ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1.0


def in_poly(x, y, pts):
    inside = False
    j = len(pts) - 1
    for i in range(len(pts)):
        xi, yi = pts[i]
        xj, yj = pts[j]
        if (yi > y) != (yj > y) and x < (xj - xi) * (y - yi) / (yj - yi) + xi:
            inside = not inside
        j = i
    return inside


# Schaedel: Hirnschale + Wangen/Kiefer, symmetrisch um x = 31.5
CX = 31.5
CRANIUM = (CX, 17.5, 14.5, 13.0)
JAW = [(19.5, 22), (43.5, 22), (42.5, 30), (39.5, 36), (23.5, 36), (20.5, 30)]
# Augenhoehlen: schraeg, innen tiefer (boeser Blick)
EYE_L = [(19.5, 17), (23, 15.5), (29.5, 19.5), (28.5, 24.5), (22.5, 25), (19.5, 21.5)]
EYE_R = [(2 * CX - x, y) for x, y in EYE_L]
NOSE = [(CX, 24.5), (34, 29.5), (CX, 30.5), (29, 29.5)]
# Maul: offen, der Blitz kommt heraus
MOUTH = [(25.5, 32), (37.5, 32), (36.5, 36), (26.5, 36)]

# Wolke um den Schaedel (x, y, r)
CLOUD = [(14, 16, 9.0), (6.5, 26, 5.5), (13, 30.5, 4.5), (51, 18, 8.0), (57, 27, 5.0), (49.5, 30, 5.0),
         (24, 6.5, 6.0), (40.5, 5.5, 5.0)]

# Blitz: Hauptstrahl aus dem Maul, zwei Aeste
BOLT = [(31.5, 34), (37, 40.5), (28, 44.5), (36, 50.5), (28.5, 55.5), (31.5, 60.5)]
FORKS = [
    [(37, 40.5), (41.5, 41.5), (43, 46), (48, 47.5)],
    [(28, 44.5), (24, 45.5), (21.5, 50), (17, 51)],
    [(43, 46), (45.5, 51), (44.5, 54)],
    [(21.5, 50), (19.5, 54)],
]
IMPACT = (31.5, 61)


def draw64():
    img = Image.new("RGBA", (S, S), CLEAR)
    px = img.load()

    # Masken
    skull = [[False] * S for _ in range(S)]
    cloud = [[False] * S for _ in range(S)]
    bolt_d = [[99.0] * S for _ in range(S)]
    fork_d = [[99.0] * S for _ in range(S)]
    for y in range(S):
        for x in range(S):
            cx_, cy_ = x + 0.5, y + 0.5
            if in_ellipse(cx_, cy_, *CRANIUM) or in_poly(cx_, cy_, JAW):
                skull[y][x] = True
            for ccx, ccy, r in CLOUD:
                if math.hypot(cx_ - ccx, cy_ - ccy) <= r:
                    cloud[y][x] = True
            bolt_d[y][x] = line_dist(cx_, cy_, BOLT)
            fork_d[y][x] = min(line_dist(cx_, cy_, f) for f in FORKS)

    # Blitz verjuengt sich nach unten nicht, Aeste werden zum Ende duenn
    def bolt_level(x, y):
        d = bolt_d[y][x]
        if d <= 0.7:
            return 4
        if d <= 1.3:
            return 3
        if d <= 1.95:
            return 2
        if d <= 2.5:
            return 1
        return 0

    bolt = [[bolt_level(x, y) for x in range(S)] for y in range(S)]

    # Einschlag: flacher Ring + Strahlen
    ix, iy = IMPACT
    impact = [[0] * S for _ in range(S)]
    for y in range(S):
        for x in range(S):
            dx, dy = (x + 0.5 - ix) / 8.0, (y + 0.5 - iy) / 2.6
            r = math.hypot(dx, dy)
            if r <= 0.45:
                impact[y][x] = 4
            elif r <= 0.7:
                impact[y][x] = 3
            elif r <= 1.0:
                impact[y][x] = 2
    for ang, ln in ((-165, 8), (-130, 5), (-50, 5), (-15, 8)):
        a = math.radians(ang)
        for t in range(2, ln + 1):
            x = int(round(ix - 0.5 + math.cos(a) * t * 1.5))
            y = int(round(iy - 0.5 + math.sin(a) * t * 0.7))
            if 0 <= x < S and 0 <= y < S and not impact[y][x]:
                impact[y][x] = 3 if t < ln - 1 else 1

    solid = [[skull[y][x] or cloud[y][x] or bolt[y][x] > 0 or impact[y][x] > 1
              for x in range(S)] for y in range(S)]

    # 1) gruener Schein (halbtransparent) hinter allem
    for y in range(S):
        for x in range(S):
            if solid[y][x]:
                continue
            d = min(bolt_d[y][x], fork_d[y][x] + 1.0)
            dy = (y + 0.5 - iy) / 4.5
            di = math.hypot((x + 0.5 - ix) / 14.0, dy)
            a = 0
            if d <= 7.5:
                a = max(a, int(110 * (1 - d / 7.5) ** 1.3))
            if di <= 1.0:
                a = max(a, int(120 * (1 - di)))
            # Augenschein
            for ex in (24.5, 38.5):
                de = math.hypot(x + 0.5 - ex, y + 0.5 - 20.5)
                if de <= 9:
                    a = max(a, int(60 * (1 - de / 9)))
            if a > 6:
                px[x, y] = (68, 197, 116, a)

    # 2) Wolke, Beulen von hinten nach vorn
    for ccx, ccy, r in CLOUD:
        for y in range(S):
            for x in range(S):
                cx_, cy_ = x + 0.5, y + 0.5
                d = math.hypot(cx_ - ccx, cy_ - ccy)
                if d > r:
                    continue
                n = ((cx_ - ccx) * LX + (cy_ - ccy) * LY) / r
                col = C_MID
                if n > 0.55 and d > r - 2.0:
                    col = C_HI
                elif n > 0.15:
                    col = C_LIGHT
                elif n < -0.45 and d > r - 2.2:
                    # Unterseite: vom Blitz gruen angestrahlt
                    col = C_GLOW if (cy_ - ccy) > 0.3 * r and cy_ > 26 else C_DARK
                elif n < -0.1:
                    col = C_DARK
                px[x, y] = col

    # Schatten der Wolke auf ihren Ueberlappungen mit dem Schaedel weglassen:
    # 3) Schaedel
    for y in range(S):
        for x in range(S):
            if not skull[y][x]:
                continue
            cx_, cy_ = x + 0.5, y + 0.5
            # Licht von oben links auf die Hirnschale, Kiefer dunkler
            nx = (cx_ - CRANIUM[0]) / CRANIUM[2]
            ny = (cy_ - CRANIUM[1]) / CRANIUM[3]
            n = nx * LX + ny * LY
            col = B_MID
            if n > 0.55:
                col = B_HI
            elif n > 0.2:
                col = B_LIGHT
            elif n < -0.45:
                col = B_DARK
            # Schlaefen-Einbuchtung unter der Hirnschale
            if not in_ellipse(cx_, cy_, *CRANIUM) and (cx_ < 22.5 or cx_ > 40.5):
                col = B_DARK
            # Wangenknochen: helle Kante unter den Augen
            if 25.5 <= cy_ <= 27 and (21 <= cx_ <= 26 or 37 <= cx_ <= 42):
                col = B_LIGHT
            px[x, y] = col

    # Kante zwischen Hirnschale und Kiefer schattieren (Rand des Schaedels innen)
    def sk(x, y):
        return 0 <= x < S and 0 <= y < S and skull[y][x]
    for y in range(S):
        for x in range(S):
            if skull[y][x] and (not sk(x + 1, y) or not sk(x, y + 1)):
                if px[x, y] in (B_MID, B_LIGHT, B_HI):
                    px[x, y] = B_DARK

    # Riss auf der Stirn
    for x, y in ((36, 7), (36, 8), (35, 9), (35, 10), (36, 11), (37, 11), (34, 11), (34, 12)):
        px[x, y] = B_DEEP
    for x, y in ((37, 8), (36, 10)):
        px[x, y] = B_HI

    # Augenhoehlen, Nase, Maul
    for y in range(S):
        for x in range(S):
            cx_, cy_ = x + 0.5, y + 0.5
            if in_poly(cx_, cy_, EYE_L) or in_poly(cx_, cy_, EYE_R) or in_poly(cx_, cy_, NOSE):
                px[x, y] = LINE
            elif in_poly(cx_, cy_, MOUTH):
                px[x, y] = LINE
    # innere Augenkante oben hell (Knochenwulst), unten tief
    for poly in (EYE_L, EYE_R):
        for y in range(S):
            for x in range(S):
                if in_poly(x + 0.5, y + 0.5, poly):
                    continue
                if in_poly(x + 0.5, y + 1.5, poly) and skull[y][x]:
                    px[x, y] = B_DEEP
    # Zaehne oben und unten
    for x in range(26, 38):
        if x % 2 == 0:
            px[x, 32] = B_LIGHT
            px[x, 33] = B_MID
        else:
            px[x, 32] = B_DARK
    for x in range(27, 37):
        if x % 2 == 1:
            px[x, 35] = B_MID
            px[x, 34] = B_DARK

    # Gluehende Augen: Pupille + Lichtpunkt
    for ex, ey in ((24, 20), (38, 20)):
        for dx in range(-2, 3):
            for dy in range(-2, 3):
                r = abs(dx) + abs(dy)
                if r <= 2:
                    px[ex + dx, ey + dy] = G_DARK if r == 2 else G_MID
        for dx, dy in ((0, 0), (-1, 0), (0, -1), (1, 0), (0, 1)):
            px[ex + dx, ey + dy] = G_CORE
        px[ex, ey] = G_WHITE
        px[ex - 1, ey - 1] = G_WHITE
    # gruener Schimmer auf den Wangen unter den Augen
    for ex in (24, 38):
        for dx in (-2, -1, 0, 1, 2):
            x, y = ex + dx, 25
            if px[x, y] in (B_MID, B_DARK):
                px[x, y] = hx("#8fc7a5")

    # 4) Einschlag unter dem Blitz
    imp_col = {1: G_DARK, 2: G_MID, 3: G_CORE, 4: G_WHITE}
    for y in range(S):
        for x in range(S):
            if impact[y][x] and not skull[y][x]:
                if impact[y][x] == 1:
                    if px[x, y][3] < 255:
                        px[x, y] = G_MID
                else:
                    px[x, y] = imp_col[impact[y][x]]

    # 5) Blitz
    blt_col = {1: G_DARK, 2: G_MID, 3: G_CORE, 4: G_WHITE}
    for y in range(S):
        for x in range(S):
            if bolt[y][x] and y >= 33:
                px[x, y] = blt_col[bolt[y][x]]

    # 6) Kontur um alles Feste
    def opaque(x, y):
        return 0 <= x < S and 0 <= y < S and px[x, y][3] == 255 and px[x, y] != LINE
    for y in range(S):
        for x in range(S):
            if px[x, y][3] == 255:
                continue
            if any(opaque(x + dx, y + dy) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                # am Blitz/Einschlag dunkelgruen statt schwarz -> leuchtet staerker
                near_g = any(0 <= x + dx < S and 0 <= y + dy < S and px[x + dx, y + dy] in
                             (G_MID, G_CORE, G_WHITE, G_DARK)
                             for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
                px[x, y] = G_DEEP if near_g else LINE

    # 6b) Aeste: 1 px, ohne Kontur, nur heller Kern im Schein
    for y in range(S):
        for x in range(S):
            if fork_d[y][x] <= 0.55 and (px[x, y][3] < 255 or px[x, y] in (LINE, G_DEEP, G_DARK)) and not skull[y][x]:
                px[x, y] = G_CORE
            elif fork_d[y][x] <= 1.05 and px[x, y][3] < 200:
                px[x, y] = (68, 197, 116, 170)

    # 7) Funken
    for sx, sy, s in ((8, 44, 2), (55, 42, 2), (12, 58, 1), (52, 58, 1), (46, 36, 1), (16, 39, 1), (58, 51, 1)):
        for d in range(-s, s + 1):
            for x, y in ((sx + d, sy), (sx, sy + d)):
                if 0 <= x < S and 0 <= y < S and px[x, y][3] < 255:
                    px[x, y] = G_WHITE if d == 0 else (G_CORE if abs(d) < s else G_MID)
    return img


# ------------------------------------------------- Werkbank-Icons (von Hand)
# o Kontur, h/c/d Wolke hell/mittel/dunkel, g/w Blitz
SMALL = {
    14: ["...oooo.ooo...",
         "..ohhhhohhhoo.",
         ".ohhcccchccho.",
         "ohcccccccccdo.",
         "odcccccccdddo.",
         ".oddowwgoddo..",
         "..ooowggoo....",
         "...owwggo.....",
         "...oowwwgo....",
         "....oowgo.....",
         "....owgo......",
         "...owgo.......",
         "...ogo........",
         "...oo........."],
    10: ["..ooo.oo..",
         ".ohhhohho.",
         "ohccccccco",
         "odcccccddo",
         ".odowwodo.",
         "..owwgo...",
         "..oowwgo..",
         "...owgo...",
         "..owgo....",
         "..oo......"],
}
SMALL_COL = {"o": LINE, "h": C_LIGHT, "c": C_MID, "d": C_DARK, "g": G_MID, "w": G_CORE}


def draw_small(size):
    rows = SMALL[size]
    assert len(rows) == size and all(len(r) == size for r in rows), size
    img = Image.new("RGBA", (size, size), CLEAR)
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch in SMALL_COL:
                img.putpixel((x, y), SMALL_COL[ch])
    return img


def main():
    preview = None
    if "--preview" in sys.argv:
        preview = sys.argv[sys.argv.index("--preview") + 1]
    dry = "--dry" in sys.argv

    icon = draw64()
    w14 = draw_small(14)
    w10 = draw_small(10)

    if preview:
        pics = [icon, w14, w10]
        out = Image.new("RGBA", (len(pics) * 270, 270), (52, 50, 62, 255))
        for i, p in enumerate(pics):
            k = 256 // max(p.size)
            out.alpha_composite(p.resize((p.width * k, p.height * k), Image.NEAREST), (i * 270, 0))
        out.save(preview)
    if not dry:
        icon.save(ICON)
        w14.save(os.path.join(WORKBENCH, "deathstrike_14.png"))
        w10.save(os.path.join(WORKBENCH, "deathstrike_10.png"))


if __name__ == "__main__":
    main()
