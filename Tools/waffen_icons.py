"""
Zeichnet die Icons der Waffen/Buffs, die bisher nur den roten
"ICON"-Platzhalter (Assets/Resources/Shop/_missing.png) hatten.

Pro Id entstehen drei Bilder:

  Assets/Art/Icons/fin_<id>.png            64x64  (32er Pixelraster, 2x)  -> weaponIcon am Player-Prefab
  Assets/Resources/Workbench/<id>_14.png    14x14  (10x10 Motiv + Kontur)  -> Werkbank-Kachel
  Assets/Resources/Workbench/<id>_10.png    10x10  ( 6x6 Motiv + Kontur)   -> Evo-Chip / Evo-Zeile

Die kleinen Groessen sind von Hand gesetzt (ASCII unten), nicht herunter-
gerechnet - auf 6x6 bleibt von einem skalierten Bild nur Matsch.
Die Werkbank-Dateien behalten ihre .meta, es aendern sich nur die Pixel.

Aufruf aus dem Projektordner:  python Tools/waffen_icons.py [--preview pfad.png]
"""

import math
import os
import sys

from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ICONS = os.path.join(ROOT, "Assets", "Art", "Icons")
WORKBENCH = os.path.join(ROOT, "Assets", "Resources", "Workbench")

WB_OUTLINE = (0x3B, 0x2B, 0x33, 255)


def hx(s):
    s = s.lstrip("#")
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), 255)


# ----------------------------------------------------------------------------
#  Hilfen fuer das 32er Raster
# ----------------------------------------------------------------------------

class Canvas:
    def __init__(self, size=32):
        self.size = size
        self.img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
        self.d = ImageDraw.Draw(self.img)

    def px(self, x, y, c):
        if 0 <= x < self.size and 0 <= y < self.size:
            self.img.putpixel((int(x), int(y)), hx(c) if isinstance(c, str) else c)

    def pts(self, c, points):
        for x, y in points:
            self.px(x, y, c)

    def rect(self, x0, y0, x1, y1, c):
        self.d.rectangle([x0, y0, x1, y1], fill=hx(c))

    def ellipse(self, x0, y0, x1, y1, c):
        self.d.ellipse([x0, y0, x1, y1], fill=hx(c))

    def poly(self, points, c):
        self.d.polygon(points, fill=hx(c))

    def line(self, points, c, w=1):
        self.d.line(points, fill=hx(c), width=w)

    def pie(self, box, a0, a1, c):
        self.d.pieslice(box, a0, a1, fill=hx(c))

    def get(self, x, y):
        return self.img.getpixel((x, y))

    def outline(self, c, diagonal=False):
        """Kontur um alles Deckende, ausserhalb der Silhouette."""
        src = self.img.copy()
        col = hx(c)
        n4 = [(1, 0), (-1, 0), (0, 1), (0, -1)]
        n8 = n4 + [(1, 1), (1, -1), (-1, 1), (-1, -1)]
        for y in range(self.size):
            for x in range(self.size):
                if src.getpixel((x, y))[3]:
                    continue
                for dx, dy in (n8 if diagonal else n4):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < self.size and 0 <= ny < self.size and src.getpixel((nx, ny))[3]:
                        self.img.putpixel((x, y), col)
                        break

    def recolor_inside(self, mask_color, c):
        pass


def ring_points(cx, cy, r):
    out = []
    for a in range(0, 360, 2):
        out.append((round(cx + r * math.cos(math.radians(a))), round(cy + r * math.sin(math.radians(a)))))
    return out


# ----------------------------------------------------------------------------
#  64er Motive (auf 32 gezeichnet)
# ----------------------------------------------------------------------------

def draw_cooldown():
    """Stoppuhr, deren abgelaufenes Viertel schon eisblau ist."""
    c = Canvas()
    # Krone und Knopf
    c.rect(14, 2, 17, 3, "#8fd0ff")
    c.rect(15, 4, 16, 6, "#3d7fd6")
    c.rect(24, 6, 26, 8, "#3d7fd6")
    c.px(25, 5, "#8fd0ff")
    # Gehaeuse
    c.ellipse(4, 6, 27, 29, "#3d7fd6")
    c.ellipse(5, 7, 26, 28, "#5aa2ef")
    # Zifferblatt
    c.ellipse(7, 9, 24, 26, "#eaf6ff")
    c.pie((7, 9, 24, 26), -90, 30, "#a8dcff")
    # Striche
    c.pts("#3d7fd6", [(15, 10), (16, 10), (15, 25), (16, 25), (8, 17), (8, 18), (23, 17), (23, 18)])
    # Zeiger
    c.line([(16, 18), (16, 12)], "#1d3f78")
    c.line([(16, 18), (20, 14)], "#1d3f78")
    c.pts("#1d3f78", [(15, 17), (15, 18), (16, 17)])
    c.px(16, 18, "#ffffff")
    # Glanz
    c.pts("#ffffff", [(9, 14), (9, 13), (10, 12), (10, 11), (11, 11)])
    c.pts("#b8e2ff", [(6, 12), (6, 13), (7, 11)])
    # Frostfunken
    for x, y in [(27, 22), (28, 21), (28, 23), (29, 22), (2, 24), (2, 25)]:
        c.px(x, y, "#d8f1ff")
    c.outline("#16244a")
    return c


def draw_duration():
    """Sanduhr mit Holzrahmen und goldenem Sand."""
    c = Canvas()
    wood, wood_d, wood_l = "#9a5a2e", "#6b3a1f", "#c98547"
    glass, glass_l = "#cfeaf0", "#ffffff"
    sand, sand_d = "#f2b83b", "#c9821d"
    # Glaskolben
    c.poly([(9, 6), (22, 6), (22, 9), (17, 15), (17, 17), (22, 22), (22, 25), (9, 25), (9, 22), (14, 17), (14, 15), (9, 9)], glass)
    # Sand oben (Rest) und unten (Haufen)
    c.poly([(12, 11), (19, 11), (16, 14), (15, 14)], sand)
    c.poly([(10, 25), (21, 25), (21, 23), (17, 20), (14, 20), (10, 23)], sand)
    c.poly([(10, 25), (21, 25), (21, 24), (10, 24)], sand_d)
    c.line([(15, 15), (15, 20)], sand)
    c.px(16, 17, sand)
    # Glanz
    c.pts(glass_l, [(10, 7), (10, 8), (11, 9), (11, 7)])
    c.pts(glass_l, [(19, 21), (20, 22)])
    # Rahmen
    c.rect(5, 2, 26, 5, wood)
    c.rect(5, 26, 26, 29, wood)
    c.rect(5, 2, 26, 2, wood_l)
    c.rect(5, 26, 26, 26, wood_l)
    c.rect(5, 5, 26, 5, wood_d)
    c.rect(5, 29, 26, 29, wood_d)
    c.rect(6, 6, 7, 25, wood)
    c.rect(24, 6, 25, 25, wood)
    c.rect(6, 6, 6, 25, wood_l)
    c.rect(25, 6, 25, 25, wood_d)
    c.outline("#3a1c10")
    return c


def draw_second_chance():
    """Herz mit Heiligenschein: einmal wieder aufstehen."""
    c = Canvas()
    red, red_d, red_l = "#e4443d", "#a92a32", "#ff8a7a"
    gold, gold_l, gold_d = "#ffd23f", "#fff3a6", "#d18a12"
    # Heiligenschein
    c.ellipse(8, 2, 23, 8, gold)
    c.ellipse(10, 4, 21, 6, (0, 0, 0, 0) if False else "#000000")
    # Innenloch freistellen
    for y in range(4, 7):
        for x in range(10, 22):
            if c.get(x, y) == hx("#000000"):
                c.img.putpixel((x, y), (0, 0, 0, 0))
    c.pts(gold_l, [(11, 2), (12, 2), (13, 2), (9, 3), (10, 3)])
    c.pts(gold_d, [(12, 8), (13, 8), (18, 8), (19, 8), (21, 7), (22, 7)])
    # Herz
    c.ellipse(4, 11, 16, 23, red)
    c.ellipse(15, 11, 27, 23, red)
    c.poly([(5, 19), (26, 19), (16, 29), (15, 29)], red)
    # Schatten rechts unten
    c.poly([(26, 17), (26, 19), (16, 29), (15, 29), (15, 27), (24, 19)], red_d)
    c.pts(red_d, [(25, 15), (25, 16), (26, 16)])
    # Glanz
    c.pts(red_l, [(7, 14), (8, 13), (9, 13), (7, 15), (7, 16), (8, 14)])
    c.pts("#ffffff", [(8, 14)])
    # kleines Plus
    c.rect(19, 15, 23, 15, "#ffffff")
    c.rect(21, 13, 21, 17, "#ffffff")
    c.outline("#4a1020")
    # Schein bekommt eine goldbraune Kontur statt der roten
    return c


def draw_glass_cannon():
    """Kanone aus Glas, mit Sprung."""
    c = Canvas()
    g, g_l, g_d, g_dd = "#9fe3f0", "#e8fbff", "#5bb8d2", "#3a86a8"
    # Rohr: schraeg nach rechts oben
    ang = math.radians(-35)
    ux, uy = math.cos(ang), math.sin(ang)
    vx, vy = -uy, ux
    ox, oy = 10, 21

    def P(a, b):
        return (ox + ux * a + vx * b, oy + uy * a + vy * b)

    c.poly([P(-4, -5), P(15, -4), P(15, 4), P(-4, 5)], g)
    c.poly([P(14, -5), P(17, -5), P(17, 5), P(14, 5)], g_d)  # Muendung
    c.poly([P(-7, -3), P(-4, -4), P(-4, 4), P(-7, 3)], g_d)  # Boden
    # Glanzstreifen
    c.line([P(-2, -3), P(13, -2.5)], g_l)
    # Schattenseite
    c.line([P(-3, 4), P(13, 3.4)], g_d)
    # Sprung
    c.line([P(6, -4), P(8, -1), P(6, 1), P(9, 4)], "#ffffff")
    c.line([P(8, -1), P(11, 0)], "#ffffff")
    # Rad (Holz, damit es nicht in der Kanone verschwindet)
    c.ellipse(4, 19, 16, 31, "#8a5a36")
    c.ellipse(6, 21, 14, 29, "#b77b4a")
    c.ellipse(9, 24, 11, 26, "#5b3620")
    c.line([(10, 21), (10, 29)], "#8a5a36")
    c.line([(6, 25), (14, 25)], "#8a5a36")
    c.pts("#d9a06a", [(7, 22), (8, 21)])
    # Glassplitter
    c.pts(g_l, [(27, 13), (28, 14), (26, 17), (29, 9)])
    c.pts(g, [(28, 13), (27, 17), (29, 10)])
    c.outline("#18384a")
    return c


def draw_vortex():
    """Violetter Strudel, innen hell."""
    c = Canvas()
    cols = ["#3e1a6e", "#5a2a9a", "#7b3fc6", "#a067e6", "#d0a8ff", "#ffffff"]
    cx, cy = 15.5, 15.5
    turns = 2.4
    steps = 900
    for i in range(steps):
        t = i / steps
        a = t * turns * 2 * math.pi
        r = 14.0 * (1 - t) + 0.5
        w = 2.8 * (1 - t) + 1.2
        col = cols[min(len(cols) - 1, int(t * len(cols)))]
        x = cx + r * math.cos(a)
        y = cy + r * math.sin(a) * 0.9
        c.d.ellipse([x - w / 2, y - w / 2, x + w / 2, y + w / 2], fill=hx(col))
    c.ellipse(14, 14, 17, 17, "#ffffff")
    # Truemmer, die hineingezogen werden
    c.pts("#d0a8ff", [(2, 5), (3, 4), (28, 26), (29, 27), (5, 28)])
    c.pts("#f2b83b", [(26, 4), (27, 4), (26, 5)])
    c.outline("#1e0a38")
    return c


def draw_turret():
    """Keks-Geschuetz: Metallsockel, Keks-Kuppel, Rohr."""
    c = Canvas()
    metal, metal_d, metal_l = "#8a8f9c", "#5a5e6b", "#c4c8d2"
    ck, ck_d, ck_l, chip = "#d99a4e", "#a86a2c", "#f2c27e", "#5a3218"
    # Rohr
    c.rect(18, 12, 29, 16, metal)
    c.rect(18, 12, 29, 12, metal_l)
    c.rect(18, 16, 29, 16, metal_d)
    c.rect(27, 11, 30, 17, metal_d)
    c.rect(27, 11, 30, 11, metal)
    c.px(29, 14, "#2a2a33")
    # Sockel
    c.poly([(5, 29), (26, 29), (23, 23), (8, 23)], metal)
    c.rect(5, 28, 26, 29, metal_d)
    c.line([(8, 23), (23, 23)], metal_l)
    c.pts("#2a2a33", [(9, 26), (15, 26), (21, 26)])
    # Kuppel (halber Keks)
    c.pie((5, 7, 26, 38), 180, 360, ck)
    c.rect(5, 22, 26, 23, ck_d)
    c.pts(ck_l, [(9, 13), (10, 12), (11, 11), (12, 10), (8, 15), (8, 14)])
    c.pts(ck_d, [(24, 18), (24, 19), (23, 16), (24, 20)])
    for x, y in [(10, 17), (11, 17), (15, 11), (16, 11), (15, 12), (19, 16), (20, 16), (20, 17), (14, 19), (13, 15)]:
        c.px(x, y, chip)
    # Sehschlitz
    c.rect(12, 14, 18, 15, "#2a2a33")
    c.px(17, 14, "#ff5a4a")
    c.outline("#2a1a14")
    return c


def draw_crumb_trail():
    """Angebissener Keks, dahinter eine Spur aus Kruemeln."""
    c = Canvas()
    ck, ck_d, ck_l, chip = "#d99a4e", "#a86a2c", "#f2c27e", "#5a3218"
    # Keks oben rechts, links unten ein kleiner Biss
    c.ellipse(12, 1, 30, 19, ck_d)
    c.ellipse(12, 1, 29, 18, ck)
    c.ellipse(10, 12, 17, 19, "#000000")
    c.ellipse(13, 15, 18, 20, "#000000")
    for y in range(32):
        for x in range(32):
            if c.get(x, y) == hx("#000000"):
                c.img.putpixel((x, y), (0, 0, 0, 0))
    c.pts(ck_l, [(17, 3), (18, 3), (19, 2), (20, 2), (21, 2), (16, 4), (15, 5), (14, 6), (14, 7)])
    for x, y in [(18, 7), (19, 7), (19, 8), (24, 6), (25, 6), (25, 7), (22, 12), (23, 12), (23, 13),
                 (17, 12), (26, 11), (27, 12), (21, 16), (22, 16)]:
        c.px(x, y, chip)
    # Kruemel, nach links unten kleiner werdend
    crumbs = [(8, 20, 3), (4, 24, 3), (10, 25, 2), (1, 28, 2), (6, 28, 2), (11, 29, 1), (3, 31, 1)]
    for x, y, s in crumbs:
        c.rect(x, y, x + s - 1, y + s - 1, ck)
        if s > 1:
            c.px(x, y, ck_l)
            c.px(x + s - 1, y + s - 1, ck_d)
    c.outline("#3a1c10")
    return c


def draw_sticky_shatter():
    """Zersprungenes Marmeladenglas: Klecks, Scherben, Deckel."""
    c = Canvas()
    jam, jam_d, jam_l = "#6a2aa0", "#43186e", "#9a5ad0"
    glass, glass_l = "#cfe6f4", "#ffffff"
    lid, lid_d = "#6fb0f0", "#2f6fc8"
    # Klecks
    blob = [(4, 22), (7, 17), (11, 16), (13, 12), (17, 14), (21, 12), (23, 16), (27, 17),
            (28, 22), (25, 26), (26, 29), (20, 28), (16, 30), (12, 28), (6, 29), (7, 25)]
    c.poly(blob, jam)
    c.poly([(7, 25), (12, 27), (16, 29), (20, 27), (25, 26), (26, 29), (20, 28), (16, 30), (12, 28), (6, 29)], jam_d)
    c.pts(jam_l, [(10, 19), (11, 19), (12, 18), (9, 20), (18, 17), (19, 17)])
    c.ellipse(14, 20, 17, 23, jam_d)
    c.pts(jam_l, [(15, 20)])
    # Tropfen
    c.rect(3, 13, 4, 14, jam)
    c.rect(28, 11, 29, 12, jam)
    c.px(24, 9, jam)
    # Glasscherben
    c.poly([(3, 4), (8, 2), (7, 9)], glass)
    c.line([(4, 4), (7, 3)], glass_l)
    c.poly([(24, 20), (29, 21), (26, 25)], glass)
    c.px(25, 21, glass_l)
    c.poly([(10, 23), (13, 21), (13, 26)], glass)
    c.px(11, 23, glass_l)
    # Deckel, fliegt schraeg davon
    ang = math.radians(20)
    ux, uy = math.cos(ang), math.sin(ang)
    vx, vy = -uy, ux

    def P(a, b):
        return (18 + ux * a + vx * b, 5 + uy * a + vy * b)

    c.poly([P(-7, -2), P(7, -2), P(7, 2), P(-7, 2)], lid)
    for k in range(-6, 7, 3):
        c.poly([P(k, -2), P(k + 1.4, -2), P(k + 1.4, 2), P(k, 2)], lid_d)
    c.line([P(-7, 2.5), P(7, 2.5)], "#e8f2ff")
    # Funken der Evolution
    c.pts("#ffe66a", [(29, 3), (29, 5), (28, 4), (30, 4), (2, 17), (1, 18)])
    c.outline("#1e0a30")
    return c


# ----------------------------------------------------------------------------
#  Kleine Groessen von Hand. Kontur (#3b2b33) kommt automatisch dazu.
#  '.' = leer. Buchstaben siehe PAL.
# ----------------------------------------------------------------------------

PAL = {
    # Blau (Abklingzeit)
    "B": "#3d7fd6", "b": "#5aa2ef", "i": "#a8dcff", "w": "#eaf6ff", "n": "#1d3f78",
    # Holz/Sand (Wirkdauer)
    "H": "#9a5a2e", "h": "#c98547", "g": "#cfeaf0", "S": "#f2b83b", "s": "#c9821d",
    # Rot/Gold (Zweite Chance)
    "R": "#e4443d", "r": "#a92a32", "p": "#ff8a7a", "Y": "#ffd23f", "y": "#d18a12",
    # Glas (Glaskanone)
    "G": "#9fe3f0", "L": "#e8fbff", "D": "#5bb8d2", "W": "#b77b4a", "V": "#8a5a36",
    # Violett (Strudel, Marmelade)
    "1": "#3e1a6e", "2": "#5a2a9a", "3": "#7b3fc6", "4": "#a067e6", "5": "#d0a8ff",
    "J": "#6a2aa0", "j": "#43186e", "k": "#9a5ad0",
    # Keks + Metall (Geschuetz, Kruemel)
    "C": "#d99a4e", "c": "#a86a2c", "l": "#f2c27e", "x": "#5a3218",
    "M": "#8a8f9c", "m": "#5a5e6b", "o": "#c4c8d2", "z": "#2a2a33",
    # Allgemein
    "#": "#ffffff", "e": "#ff5a4a", "u": "#6fb0f0", "U": "#2f6fc8",
}

SMALL = {
    # --------------------------------------------------- 10x10 (fuer _14)
    ("buff_cooldown", 14): [
        "....bb....",
        "..BBBBBB..",
        ".BwwwiiiB.",
        "BwwwwiiiiB",
        "BwwwnniiiB",
        "BwwwnwwwwB",
        "BwwwwwwwwB",
        "BwwwwwwwwB",
        ".BwwwwwwB.",
        "..BBBBBB..",
    ],
    ("buff_duration", 14): [
        "HhhhhhhhhH",
        ".HHHHHHHH.",
        ".HgSSSSgH.",
        ".H.gSSg.H.",
        ".H..gS..H.",
        ".H..gS..H.",
        ".H.gSSg.H.",
        ".HgSSSSSH.",
        ".HHHHHHHH.",
        "HhhhhhhhhH",
    ],
    ("buff_second_chance", 14): [
        "..YYYYYY..",
        ".Y......Y.",
        "..yyyyyy..",
        "..........",
        ".RRR..RRR.",
        "RpRRRRRRRr",
        "RpRRRRR#Rr",
        ".RRRRR###.",
        "..rRRRR#r.",
        "....rr....",
    ],
    ("buff_glass_cannon", 14): [
        "........DD",
        "......GGGD",
        ".....LGGDD",
        "....LG#G..",
        "...LGG#D..",
        "..LGGGDD..",
        ".GGGGDD...",
        ".DGVVV....",
        "..VWWWV...",
        "...VVV....",
    ],
    ("vortex", 14): [
        "..222222..",
        ".2......3.",
        "2..3333..3",
        "2.3....4.3",
        "2.3.55.4.3",
        "2.3.5#.4.3",
        "2.3..44..3",
        "2..3.....3",
        ".2..3333..",
        "..2.......",
    ],
    ("turret", 14): [
        "..........",
        "...CCCC...",
        "..ClCxCC..",
        ".ClxCCzMMM",
        ".CzzzeCooo",
        ".CCxCCcmmm",
        ".cccccc...",
        "..oooooo..",
        ".MzMMzMMM.",
        ".mmmmmmmm.",
    ],
    ("crumb_trail", 14): [
        "....CCCC..",
        "...lCCxCC.",
        "..lxCCCCCc",
        "..CCCxCCxc",
        "...CCCCCCc",
        ".....CCcc.",
        "..C.......",
        "..........",
        "Cc..C.....",
        "..........",
    ],
    ("evo_sticky_shatter", 14): [
        ".g.....uUu",
        "gg....uUuU",
        "..........",
        "....JJ....",
        "..JJkJJJ..",
        ".JkkJJJJJ.",
        ".JJJJjJJgg",
        "JJjJJJJJJ.",
        ".jjJjjjJj.",
        "..j....j..",
    ],
    # --------------------------------------------------- 6x6 (fuer _10)
    ("buff_cooldown", 10): [
        "..bb..",
        ".BBBB.",
        "BwwiiB",
        "BwnwwB",
        "BwwwwB",
        ".BBBB.",
    ],
    ("buff_duration", 10): [
        "HhhhhH",
        ".HSSH.",
        "..gS..",
        "..Sg..",
        ".HSSH.",
        "HhhhhH",
    ],
    ("buff_second_chance", 10): [
        ".YYYY.",
        "......",
        "RR.RRr",
        "RpRRRr",
        ".RRRr.",
        "..rr..",
    ],
    ("buff_glass_cannon", 10): [
        "....GD",
        "...LGD",
        "..LGD.",
        ".GGD..",
        "VWWV..",
        ".VV...",
    ],
    ("vortex", 10): [
        ".2222.",
        "2.333.",
        "2.3.4.",
        "2.3#4.",
        "2..44.",
        ".2....",
    ],
    ("turret", 10): [
        "..CC..",
        ".ClxCM",
        ".CzeMo",
        ".cccc.",
        "oooooo",
        "mmmmmm",
    ],
    ("crumb_trail", 10): [
        "..CCC.",
        ".lxCCc",
        ".CCCxc",
        "..Ccc.",
        "C.....",
        "..C...",
    ],
    ("evo_sticky_shatter", 10): [
        "g...uU",
        "..JJ..",
        ".JkJJ.",
        "JJJjJg",
        ".jJjj.",
        "..j...",
    ],
}


def small_icon(rows, canvas):
    img = Image.new("RGBA", (canvas, canvas), (0, 0, 0, 0))
    off = 2  # 1px frei + 1px Kontur
    for y, row in enumerate(rows):
        assert len(row) == canvas - 4, (row, canvas)
        for x, ch in enumerate(row):
            if ch != ".":
                img.putpixel((x + off, y + off), hx(PAL[ch]))
    # Kontur (4er-Nachbarschaft) wie bei den vorhandenen Werkbank-Icons
    src = img.copy()
    for y in range(canvas):
        for x in range(canvas):
            if src.getpixel((x, y))[3]:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < canvas and 0 <= ny < canvas and src.getpixel((nx, ny))[3]:
                    img.putpixel((x, y), WB_OUTLINE)
                    break
    return img


ICONS_64 = {
    "buff_cooldown": draw_cooldown,
    "buff_duration": draw_duration,
    "buff_second_chance": draw_second_chance,
    "buff_glass_cannon": draw_glass_cannon,
    "vortex": draw_vortex,
    "turret": draw_turret,
    "crumb_trail": draw_crumb_trail,
    "evo_sticky_shatter": draw_sticky_shatter,
}


def main():
    preview = None
    if "--preview" in sys.argv:
        preview = sys.argv[sys.argv.index("--preview") + 1]

    results = []
    for wid, fn in ICONS_64.items():
        big = fn().img.resize((64, 64), Image.NEAREST)
        s14 = small_icon(SMALL[(wid, 14)], 14)
        s10 = small_icon(SMALL[(wid, 10)], 10)
        if preview is None:
            big.save(os.path.join(ICONS, f"fin_{wid}.png"))
            s14.save(os.path.join(WORKBENCH, f"{wid}_14.png"))
            s10.save(os.path.join(WORKBENCH, f"{wid}_10.png"))
        results.append((wid, big, s14, s10))

    if preview:
        sc = 4
        cell = 64 * sc + 16
        sheet = Image.new("RGBA", (len(results) * cell, 64 * sc + 14 * 12 + 40), (40, 40, 50, 255))
        for i, (_, big, s14, s10) in enumerate(results):
            sheet.alpha_composite(big.resize((64 * sc, 64 * sc), Image.NEAREST), (i * cell, 0))
            bg = Image.new("RGBA", (14 * 10 + 10 * 10 + 10, 14 * 10), (200, 190, 160, 255))
            bg.alpha_composite(s14.resize((140, 140), Image.NEAREST), (0, 0))
            bg.alpha_composite(s10.resize((100, 100), Image.NEAREST), (150, 0))
            bg = bg.crop((0, 0, min(bg.width, cell - 16), bg.height))
            sheet.alpha_composite(bg, (i * cell, 64 * sc + 20))
        sheet.save(preview)
        print("Vorschau:", preview)
    else:
        print("Geschrieben:", ", ".join(r[0] for r in results))


if __name__ == "__main__":
    main()
