"""Tresen + Werkbank fuer Ricos Laden, passend zu shop_boden.py / shop_wand.py.

Ueberschreibt Assets/Art/World-Objects/podium.png (96x64) und werkstatt.png (32x64).
podium: Sprite-Rect x0 y6 96x52 (von unten, d. h. Bildzeilen 6..57) mit eigenem Pivot
(0.5, 0.34615) = derselbe Pixel wie der alte Mittelpunkt -> Position, Kollision und Ricos Platz
bleiben gleich, oben ist Platz fuer Glocke, Bonbonglas und Kasse. Rico steht bei x ~34..68, frei lassen.
werkstatt: Rect x5 y0 23x63 (Spalten 5..27, Zeilen 1..63), unveraendert.
Aufruf: python Tools/shop_moebel.py [--preview]
"""
import os
import random
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import shop_boden as sb  # noqa: E402
import shop_wand as sw  # noqa: E402

hx = sb.hx
OUT = os.path.join(sb.ROOT, "Assets", "Art", "World-Objects")

LINIE = hx("#241c2e")
# Warmes Honigholz + Cremeplatte: Komplementaer zum kuehlen Steinboden, damit die
# bedienbaren Moebel sofort ins Auge fallen
HOLZ_DD = hx("#6b3a24")
HOLZ_D = hx("#94562c")
HOLZ = hx("#c4813f")
HOLZ_H = hx("#e3a65a")
HOLZ_HH = hx("#f0c27a")
PLATTE = hx("#ead2a0")
PLATTE_H = hx("#f7e6c0")
PLATTE_D = hx("#cdb07e")
METALL = hx("#9aa3b8")
METALL_H = hx("#d3d9e6")
METALL_D = hx("#5f6782")
MESSING = sw.MESSING
MESSING_D = sw.MESSING_D
MESSING_H = hx("#f0d68a")
GLAS_D = hx("#2e3550")
GLAS_H = hx("#9fb6c8")
SCHATTEN = (20, 16, 32, 150)
WEISS = (255, 255, 255, 255)


def rect(px, x0, y0, x1, y1, c):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            px[x, y] = c


def umriss(im, y_von=0):
    """Aeussere Pixel der Silhouette werden zur dunklen Linie (innen, damit nichts aus dem Sprite-Rect faellt)."""
    px = im.load()
    w, h = im.size
    voll = lambda x, y: 0 <= x < w and 0 <= y < h and px[x, y][3] == 255
    rand = [(x, y) for y in range(y_von, h) for x in range(w)
            if voll(x, y) and not all(voll(x + dx, y + dy) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))]
    for x, y in rand:
        px[x, y] = LINIE


def miniglas(px, x, y, farbe):
    """Glas 5x6, y = Boden."""
    rect(px, x, y - 4, x + 4, y, GLAS_H)
    rect(px, x + 1, y - 3, x + 3, y - 1, farbe)
    rect(px, x, y - 5, x + 4, y - 5, sb.T_CREME)
    return x + 4


ZAHNRAD = [
    "...##...",
    ".#.##.#.",
    "########",
    ".##..##.",
    ".##..##.",
    "########",
    ".#.##.#.",
    "...##...",
]


def schicht(im, zeichne):
    """Zeichnet auf eine eigene Ebene, gibt ihr einen Umriss und legt sie auf im."""
    lage = Image.new("RGBA", im.size, (0, 0, 0, 0))
    zeichne(lage.load())
    umriss(lage)
    im.alpha_composite(lage)


def zeichne_glocke(px):
    cx = 10
    rect(px, cx - 5, 29, cx + 5, 29, HOLZ_DD)                  # Sockel
    rect(px, cx - 5, 28, cx + 5, 28, HOLZ_D)
    for i, w in enumerate((1, 3, 4, 4, 5)):                     # Kuppel
        rect(px, cx - w, 23 + i, cx + w, 23 + i, MESSING)
    rect(px, cx - 1, 21, cx + 1, 22, MESSING_D)                 # Knopf
    px[cx, 21] = MESSING_H
    px[cx - 2, 24] = MESSING_H
    px[cx - 3, 25] = MESSING_H
    px[cx - 3, 26] = MESSING_H
    rect(px, cx + 2, 25, cx + 4, 27, MESSING_D)


def zeichne_bonbonglas(px):
    """Glas voller XP-Bonbons."""
    x0, x1 = 17, 29
    farben = [sw.ROT, sw.PETROL_H, MESSING, sw.ROSA, PLATTE_H, hx("#7fc96b")]
    rect(px, 22, 12, 24, 13, MESSING_D)                         # Knauf
    px[23, 12] = MESSING_H
    rect(px, x0 + 1, 14, x1 - 1, 15, MESSING)                   # Deckel
    rect(px, x0 + 1, 15, x1 - 1, 15, MESSING_D)
    for y in range(16, 30):                                     # Glas, abgerundet
        r = 0 if 18 <= y <= 27 else 1
        rect(px, x0 + r, y, x1 - r, y, GLAS_H)
    i = 0
    for r, y in enumerate((26, 23, 20)):                        # runde Bonbons, von unten gestapelt
        for x in range(x0 + 1 + (r % 2) * 2, x1 - 2, 3):
            f = farben[i % len(farben)]
            rect(px, x, y, x + 2, y + 2, f)
            dunkel = tuple(int(c * 0.72) for c in f[:3]) + (255,)
            for ex, ey in ((x + 2, y), (x, y + 2), (x + 2, y + 2)):
                px[ex, ey] = dunkel
            px[x + 1, y + 1] = WEISS if i % 3 == 0 else f
            i += 1
    rect(px, x0 + 1, 29, x1 - 1, 29, farben[0])                 # unterste Lage
    for x in range(x0 + 2, x1 - 1, 3):
        px[x, 29] = farben[(x // 3) % len(farben)]
    rect(px, x0 + 1, 17, x0 + 1, 21, WEISS)                     # Glanz
    px[x0 + 2, 17] = WEISS


def zeichne_kasse(px):
    """Messing-Registrierkasse mit Kurbel und Kaese-Faehnchen."""
    rect(px, 69, 25, 93, 29, HOLZ)                              # Geldlade
    rect(px, 69, 25, 93, 25, HOLZ_H)
    rect(px, 69, 29, 93, 29, HOLZ_DD)
    rect(px, 79, 27, 83, 27, MESSING)
    px[81, 27] = MESSING_H
    for y in range(15, 25):                                     # Gehaeuse, vorne schraeg
        e = (24 - y) // 4
        rect(px, 71 + e, y, 91 - e, y, MESSING)
        px[71 + e, y] = MESSING_H
        px[91 - e, y] = MESSING_D
    for x in range(74, 89, 3):                                  # Ziermuster
        px[x, 16] = MESSING_D
    for r, y in enumerate((18, 20, 22)):                        # Tasten
        for x in range(74 + r % 2, 88, 3):
            rect(px, x, y, x + 1, y, PLATTE_H)
            rect(px, x, y + 1, x + 1, y + 1, PLATTE_D)
    for y in range(7, 15):                                      # Anzeige mit Bogen
        rand = 2 if y == 7 else 1 if y == 8 else 0
        rect(px, 76 + rand, y, 86 - rand, y, MESSING)
    rect(px, 78, 9, 84, 13, GLAS_D)
    rect(px, 79, 10, 83, 12, PLATTE_H)
    rect(px, 80, 11, 82, 12, sw.KAESE)
    px[80, 10] = sw.KAESE
    px[82, 12] = sw.KAESE_D
    px[81, 7] = MESSING_H
    px[81, 6] = MESSING_D
    rect(px, 92, 18, 93, 19, METALL_D)                          # Kurbel
    rect(px, 94, 15, 94, 19, METALL)
    rect(px, 93, 14, 95, 15, HOLZ_DD)


def zeichne_kaese_emblem(px, cx, cy):
    for i in range(5):
        rect(px, cx - 5 + i * 2, cy + 2 - i, cx + 5, cy + 2 - i, MESSING)
    rect(px, cx - 5, cy + 2, cx + 5, cy + 2, MESSING_D)
    px[cx + 2, cy] = MESSING_D
    px[cx - 1, cy + 1] = MESSING_D
    px[cx + 4, cy - 1] = MESSING_H


def zeichne_keks_emblem(px, cx, cy):
    for y in range(cy - 4, cy + 5):
        for x in range(cx - 4, cx + 5):
            if (x - cx) ** 2 + (y - cy) ** 2 <= 17:
                px[x, y] = MESSING if (x - cx) + (y - cy) < 2 else MESSING_D
    for x, y in ((cx - 2, cy - 1), (cx + 1, cy - 2), (cx + 2, cy + 1), (cx - 1, cy + 2)):
        px[x, y] = HOLZ_DD
    px[cx - 2, cy - 3] = MESSING_H


def saeule(px, x0):
    """Gedrechselte Ecksaeule, 6 px breit, Zeilen 39..53."""
    rect(px, x0, 39, x0 + 5, 40, MESSING)
    rect(px, x0, 39, x0 + 5, 39, MESSING_H)
    for y in range(41, 52):
        ring = y in (44, 48)
        a, b = (x0, x0 + 5) if ring else (x0 + 1, x0 + 4)
        rect(px, a, y, b, y, HOLZ_HH if ring else HOLZ)
        if not ring:
            px[x0 + 1, y] = HOLZ_HH
            px[x0 + 4, y] = HOLZ_D
    rect(px, x0, 52, x0 + 5, 53, MESSING_D)
    rect(px, x0, 52, x0 + 5, 52, MESSING)


def tresen():
    """Ricos Ladentheke. Bild 96x64, Sprite-Rect = Zeilen 6..57, Pivot auf dem alten Mittelpunkt."""
    im = Image.new("RGBA", (96, 64), (0, 0, 0, 0))
    px = im.load()
    rnd = random.Random(11)
    # Korpus aus Honigholz
    rect(px, 4, 39, 91, 52, HOLZ)
    rect(px, 4, 39, 91, 39, HOLZ_DD)
    # Seitenfelder mit Messing-Intarsien (links Kaese, rechts Keks)
    for a, b in ((9, 29), (66, 86)):
        rect(px, a, 41, b, 51, HOLZ_DD)
        rect(px, a + 1, 41, b - 1, 50, HOLZ_D)
        rect(px, a + 1, 41, b - 1, 41, HOLZ_H)
        rect(px, a + 1, 41, a + 1, 50, HOLZ_H)
        rect(px, a + 2, 42, b - 2, 50, HOLZ)
        rect(px, a + 3, 43, b - 3, 49, HOLZ_D)
        rect(px, a + 4, 44, b - 3, 49, HOLZ)
        rect(px, a + 4, 44, b - 4, 44, HOLZ_H)
    zeichne_kaese_emblem(px, 19, 46)
    zeichne_keks_emblem(px, 76, 46)
    # Vitrine mit rotem Samt, bauchig (Rahmen in der Mitte hoeher)
    v0, v1 = 32, 63
    for x in range(v0, v1 + 1):
        oben = 39 if abs(x - (v0 + v1) / 2) < 10 else 40
        rect(px, x, oben, x, 52, MESSING_D)
        if x not in (v0, v1):
            for y in range(oben + 1, 52):
                f = sb.T_ROT if y < 44 else sb.T_ROT_D
                if rnd.random() < 0.08:
                    f = sb.T_ROT_D
                px[x, y] = f
            px[x, oben] = MESSING
    rect(px, v0 + 1, 46, v1 - 1, 46, MESSING)                   # Glasboden
    rect(px, v0 + 1, 41, v1 - 1, 41, sw.ROSA)                   # Licht oben
    x = v0 + 3
    for art in ("trank", "glas", "kaese", "trank2", "glas2"):
        if art.startswith("trank"):
            fl, hl = (sw.PETROL, sw.PETROL_H) if art == "trank" else (hx("#7a4fb0"), hx("#b58ae6"))
            px[x + 1, 41] = HOLZ_H
            rect(px, x, 42, x + 2, 45, fl)
            px[x, 43] = hl
            x += 5
        elif art.startswith("glas"):
            rect(px, x, 42, x + 3, 42, PLATTE_H)
            rect(px, x, 43, x + 3, 45, GLAS_H)
            rect(px, x + 1, 44, x + 2, 45, sw.ROT if art == "glas" else MESSING)
            x += 6
        else:
            rect(px, x, 45, x + 6, 45, sw.KAESE_D)
            rect(px, x + 1, 44, x + 6, 44, sw.KAESE)
            rect(px, x + 3, 43, x + 6, 43, sw.KAESE)
            rect(px, x + 5, 42, x + 6, 42, sw.KAESE)
            px[x + 4, 44] = sw.KAESE_D
            x += 9
    for kx in range(v0 + 3, v1 - 4, 6):                         # Kekse unten
        rect(px, kx, 49, kx + 4, 51, sw.KEKS)
        rect(px, kx + 1, 48, kx + 3, 48, sw.KEKS)
        px[kx + 1, 50] = sw.KEKS_D
        px[kx + 3, 49] = sw.KEKS_D
    for i in range(5):                                          # Glasspiegelung
        for dx in (0, 2):
            x, y = v0 + 2 + dx + i, 42 + i
            if px[x, y][:3] in (sb.T_ROT[:3], sb.T_ROT_D[:3]):
                px[x, y] = sw.ROSA
    # Ecksaeulen
    saeule(px, 1)
    saeule(px, 89)
    # Sockel + Kugelfuesse
    rect(px, 3, 53, 92, 55, HOLZ_DD)
    rect(px, 3, 53, 92, 53, HOLZ_D)
    for fx in (2, 89):
        rect(px, fx + 1, 54, fx + 4, 55, MESSING)
        px[fx + 1, 54] = MESSING_H
    # Marmorplatte mit Ueberstand + Messingband
    rect(px, 0, 30, 95, 35, PLATTE)
    rect(px, 1, 31, 94, 31, PLATTE_H)
    # zwei duenne, fliessende Adern
    for start, ende, hoehe in ((3, 40, 32), (52, 92, 33)):
        y = hoehe
        for x in range(start, ende):
            if rnd.random() < 0.18:
                y = min(34, max(32, y + rnd.choice((-1, 1))))
            px[x, y] = PLATTE_D
    rect(px, 0, 36, 95, 36, PLATTE_H)
    rect(px, 0, 37, 95, 37, PLATTE_D)
    rect(px, 1, 38, 94, 38, MESSING)
    for x in range(4, 92, 6):
        px[x, 38] = MESSING_H
    umriss(im)
    # Bodenschatten (Zeilen 56/57 liegen noch im Sprite-Rect)
    for x in range(2, 94):
        if px[x, 56][3] == 0:
            px[x, 56] = SCHATTEN
        px[x, 57] = (20, 16, 32, 80)
    # Dinge auf der Platte; die Mitte (x ~34..68) bleibt fuer Rico frei
    schicht(im, zeichne_glocke)
    schicht(im, zeichne_bonbonglas)
    schicht(im, zeichne_kasse)
    return im


def werkbank():
    im = Image.new("RGBA", (32, 64), (0, 0, 0, 0))
    px = im.load()
    a, b = 5, 27                 # Spalten des Sprite-Rechtecks
    # Lochwand (oben)
    rect(px, a + 1, 1, b - 1, 36, LINIE)
    rect(px, a + 2, 2, b - 2, 35, PLATTE)
    rect(px, a + 2, 2, b - 2, 2, PLATTE_H)
    for y in range(5, 35, 4):
        for x in range(a + 4, b - 2, 4):
            px[x, y] = HOLZ_D
    rect(px, a + 1, 1, b - 1, 1, MESSING_D)
    # Werkzeug: Hammer
    rect(px, a + 5, 7, a + 6, 19, HOLZ_H)
    rect(px, a + 3, 6, a + 8, 8, METALL)
    rect(px, a + 3, 6, a + 8, 6, METALL_H)
    rect(px, a + 3, 8, a + 8, 8, METALL_D)
    # Schraubenschluessel
    rect(px, a + 13, 9, a + 14, 21, METALL)
    rect(px, a + 13, 9, a + 13, 21, METALL_H)
    rect(px, a + 12, 6, a + 15, 8, METALL)
    px[a + 13, 6] = PLATTE_D
    px[a + 14, 6] = PLATTE_D
    rect(px, a + 12, 22, a + 15, 23, METALL)
    # Zahnrad (Werkbank-Symbol)
    for zy, zeile in enumerate(ZAHNRAD):
        for zx, ch in enumerate(zeile):
            if ch == "#":
                px[a + 15 + zx - 8, 24 + zy] = METALL if zy < 4 else METALL_D
    # Arbeitsplatte
    t = 37
    rect(px, a, t, b, t + 3, HOLZ_H)
    rect(px, a, t, b, t, HOLZ_HH)
    rect(px, a, t + 4, b, t + 4, LINIE)
    rect(px, a, t, a, t + 4, LINIE)
    rect(px, b, t, b, t + 4, LINIE)
    # Unterbau mit Schublade
    rect(px, a + 1, t + 5, b - 1, 52, HOLZ)
    rect(px, a + 1, t + 5, a + 1, 52, LINIE)
    rect(px, b - 1, t + 5, b - 1, 52, LINIE)
    rect(px, a + 3, t + 6, b - 3, t + 11, HOLZ_D)
    rect(px, a + 4, t + 7, b - 4, t + 10, HOLZ_H)
    rect(px, a + 9, t + 8, a + 13, t + 8, MESSING)
    rect(px, a + 3, t + 13, b - 3, 51, HOLZ_D)
    # Beine
    for lx in (a + 2, b - 4):
        rect(px, lx, 53, lx + 2, 61, HOLZ_D)
        rect(px, lx, 53, lx, 61, LINIE)
        rect(px, lx, 62, lx + 2, 62, LINIE)
    # Querstrebe + Kiste darunter
    rect(px, a + 5, 57, b - 5, 58, HOLZ)
    rect(px, a + 6, 59, b - 6, 62, HOLZ_H)
    rect(px, a + 6, 59, b - 6, 59, HOLZ_HH)
    rect(px, a + 6, 62, b - 6, 62, HOLZ_D)
    umriss(im)
    # Schraubstock rechts auf der Platte
    rect(px, b - 7, t - 4, b - 2, t - 1, METALL)
    rect(px, b - 7, t - 4, b - 2, t - 4, METALL_H)
    rect(px, b - 5, t - 3, b - 4, t - 2, LINIE)
    rect(px, b - 9, t - 2, b - 8, t - 2, MESSING)
    # Holzklotz links
    rect(px, a + 2, t - 3, a + 7, t - 1, HOLZ_D)
    rect(px, a + 2, t - 3, a + 7, t - 3, HOLZ)
    # Bodenschatten
    for x in range(a + 1, b):
        if px[x, 63][3] == 0:
            px[x, 63] = SCHATTEN
    return im


if __name__ == "__main__":
    t, w = tresen(), werkbank()
    t.save(os.path.join(OUT, "podium.png"))
    w.save(os.path.join(OUT, "werkstatt.png"))
    print("geschrieben: podium.png, werkstatt.png")
    if "--preview" in sys.argv:
        pv = Image.new("RGBA", (140, 70), (40, 40, 60, 255))
        pv.alpha_composite(w, (2, 3))
        pv.alpha_composite(t, (40, 3))
        pv.resize((pv.width * 4, pv.height * 4), Image.NEAREST).save(
            os.path.join(os.environ.get("TEMP", "."), "shop_moebel_preview.png"))
