"""
Zeichnet den Hauptcharakter (den Keks) neu auf 32x32 - angelehnt an die
alten 64er-Frames aus Assets/Art/Chars/fin_main_base_*.png.

Ausgabe: Assets/Art/Chars/biscuit32.png, 4 Spalten x 6 Zeilen a 32x32

  Zeile 0  idle_front_0..1   (schaut leicht nach links - wie fin_main_base_idol)
  Zeile 1  idle_right_0..1   (schaut leicht nach rechts - wie ..._otherDir)
  Zeile 2  idle_back_0..1
  Zeile 3  walk_front_0..3
  Zeile 4  walk_right_0..3
  Zeile 5  walk_back_0..3

Bei 32 PPU (= assetsPPU der Pixel-Perfect-Kameras) sitzt jeder Pixel genau
im Weltraster. Die .meta (Slices, Namen, IDs) schreibt dieses Skript NICHT -
die liegt einmal von Hand gebaut daneben und bleibt, solange sich das Raster
nicht aendert.

Aufruf:  python Tools/biscuit32.py [--preview pfad.png]
"""

import os
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Chars", "biscuit32.png")

F = 32


def hx(s):
    s = s.lstrip("#")
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), 255)


# Palette aus den alten Frames abgenommen
OUTLINE = hx("#3b2118")
DARK = hx("#301b14")
FILL = hx("#f1b45a")
LIGHT = hx("#f8c877")
SHADE = hx("#df9a44")
DEEP = hx("#b1692a")
CRUMB = hx("#e3a04b")
EMBOSS = hx("#e8a64f")
EMBOSS_HI = hx("#f7c574")
SHOE = hx("#3f3f74")
SHOE_HI = hx("#6a6ab4")
SHOE_DK = hx("#27274a")
WHITE = hx("#fff4e0")

# Koerper-Rechteck (inkl. Kontur)
BX0, BX1 = 5, 26
BODY_H = 19          # Hoehe inkl. Kontur
FEET_Y = 26          # erste Zeile der Schuhe (bei ungehobenem Fuss)

# Kruemel: relativ zur linken oberen Ecke des Koerpers, fest - wandern mit.
SPECKLES = [(4, 3), (9, 2), (15, 4), (18, 8), (3, 9), (12, 11), (7, 14), (16, 14),
            (19, 3), (11, 6), (5, 12), (14, 16)]


class Frame:
    def __init__(self):
        self.img = Image.new("RGBA", (F, F), (0, 0, 0, 0))

    def px(self, x, y, c):
        if 0 <= x < F and 0 <= y < F:
            self.img.putpixel((x, y), c)

    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.px(x, y, c)


def draw(view, look, phase, walking):
    """
    view: 'front' | 'back'
    look: -1 = leicht links, +1 = leicht rechts (nur vorne)
    phase: Frame-Nummer
    walking: Laufzyklus statt Atmen
    """
    f = Frame()

    # --- Bewegung ---------------------------------------------------------
    if walking:
        bob = 1 if phase in (1, 3) else 0          # Koerper hebt sich im Schritt
        lift_l = 1 if phase == 1 else 0            # linker Fuss angehoben
        lift_r = 1 if phase == 3 else 0
        swing = {0: 0, 1: 1, 2: 0, 3: -1}[phase]   # Aermchen pendeln gegenlaeufig
        stretch = 0
    else:
        bob = 0
        lift_l = lift_r = 0
        swing = 0
        stretch = 1 if phase == 1 else 0           # Atmen: einen Pixel hoeher

    top = FEET_Y - BODY_H - bob - stretch
    bot = FEET_Y - 1 - bob

    # --- Schuhe (hinter dem Koerper) -------------------------------------
    def shoe(x0, lifted, forward):
        y0 = FEET_Y - lifted
        x0 += forward
        f.rect(x0, y0, x0 + 4, y0 + 1, SHOE)
        f.px(x0 + 1, y0, SHOE_HI)
        f.rect(x0, y0 + 2, x0 + 4, y0 + 2, SHOE_DK)
        f.px(x0 - 1, y0 + 1, OUTLINE)
        f.px(x0 + 5, y0 + 1, OUTLINE)
        f.rect(x0, y0 + 3, x0 + 4, y0 + 3, OUTLINE) if y0 + 3 < F else None

    shoe(8, lift_l, -1 if lift_l and view == "front" else 0)
    shoe(19, lift_r, 1 if lift_r and view == "front" else 0)

    # --- Aermchen: freistehende Striche mit abgewinkeltem Haendchen ------
    arm_top = top + 7
    for side, x, sw in ((-1, BX0 - 2, swing), (1, BX1 + 2, -swing)):
        y0, y1 = arm_top + sw, arm_top + 5 + sw
        f.px(x - side, y0 - 1, OUTLINE)                 # Schulter setzt am Koerper an
        f.rect(x, y0, x, y1, OUTLINE)
        f.px(x + side, y1 + 1, OUTLINE)                 # Haendchen nach aussen
        f.px(x, y1 + 1, OUTLINE)

    # --- Koerper -------------------------------------------------------------
    f.rect(BX0, top, BX1, bot, FILL)
    # Licht oben/links, Schatten rechts/unten
    f.rect(BX0 + 1, top + 1, BX1 - 1, top + 1, LIGHT)
    f.rect(BX0 + 1, top + 1, BX0 + 1, bot - 2, LIGHT)
    f.rect(BX1 - 2, top + 2, BX1 - 1, bot - 1, SHADE)
    f.rect(BX1 - 1, top + 2, BX1 - 1, bot - 1, DEEP)
    f.rect(BX0 + 2, bot - 1, BX1 - 1, bot - 1, SHADE)

    for sx, sy in SPECKLES:
        x, y = BX0 + 1 + sx, top + 1 + sy
        if BX0 + 2 <= x <= BX1 - 3 and top + 2 <= y <= bot - 2:
            f.px(x, y, CRUMB)

    # Kontur mit abgerundeten Ecken
    f.rect(BX0 + 2, top, BX1 - 2, top, OUTLINE)
    f.rect(BX0 + 2, bot, BX1 - 2, bot, OUTLINE)
    f.rect(BX0, top + 2, BX0, bot - 2, OUTLINE)
    f.rect(BX1, top + 2, BX1, bot - 2, OUTLINE)
    for cx, cy in ((BX0 + 1, top + 1), (BX1 - 1, top + 1), (BX0 + 1, bot - 1), (BX1 - 1, bot - 1)):
        f.px(cx, cy, OUTLINE)
    for cx, cy in ((BX0, top), (BX0 + 1, top), (BX0, top + 1),
                   (BX1, top), (BX1 - 1, top), (BX1, top + 1),
                   (BX0, bot), (BX0 + 1, bot), (BX0, bot - 1),
                   (BX1, bot), (BX1 - 1, bot), (BX1, bot - 1)):
        f.img.putpixel((cx, cy), (0, 0, 0, 0))
    # Rueckseite: die Schatten-Kante unten rechts dunkler (wie im Original)
    f.px(BX1 - 1, bot - 1, OUTLINE)

    # --- Gesicht / Rueckseite ----------------------------------------------
    if view == "front":
        ex = 9 + (3 if look > 0 else 0)
        ey = top + 6
        for x in (ex, ex + 7):
            f.rect(x, ey, x + 2, ey + 2, DARK)
            f.px(x + 2, ey + 2, OUTLINE)
            f.px(x, ey, WHITE)                  # Glanzpunkt
        mx = ex + 4
        f.rect(mx, ey + 5, mx + 2, ey + 5, DARK)
    else:
        # Das eingepraegte "RISK"-Oval - auf 32 px als Oval mit Andeutung
        oy = top + 6
        ring = [(9, 2), (10, 1), (11, 0), (12, 0), (13, 0), (14, 0), (15, 0), (16, 0), (17, 0),
                (18, 0), (19, 1), (20, 2), (19, 3), (18, 4), (17, 4), (16, 4), (15, 4), (14, 4),
                (13, 4), (12, 4), (11, 4), (10, 3)]
        for x, y in ring:
            f.px(x, oy + y, EMBOSS)
        for x in (11, 12, 14, 16, 17, 18):
            f.px(x, oy + 2, EMBOSS)
        f.px(13, oy + 2, EMBOSS_HI)
        f.px(15, oy + 1, EMBOSS)
        f.px(15, oy + 3, EMBOSS)

    return f.img


LAYOUT = [
    ("idle_front", lambda i: draw("front", -1, i, False), 2),
    ("idle_right", lambda i: draw("front", 1, i, False), 2),
    ("idle_back", lambda i: draw("back", 0, i, False), 2),
    ("walk_front", lambda i: draw("front", -1, i, True), 4),
    ("walk_right", lambda i: draw("front", 1, i, True), 4),
    ("walk_back", lambda i: draw("back", 0, i, True), 4),
]


def build():
    sheet = Image.new("RGBA", (4 * F, len(LAYOUT) * F), (0, 0, 0, 0))
    for row, (_, fn, n) in enumerate(LAYOUT):
        for i in range(n):
            sheet.alpha_composite(fn(i), (i * F, row * F))
    return sheet


def main():
    sheet = build()
    if "--preview" in sys.argv:
        path = sys.argv[sys.argv.index("--preview") + 1]
        sc = 6
        bg = Image.new("RGBA", sheet.size, (92, 120, 92, 255))
        bg.alpha_composite(sheet)
        bg.resize((sheet.width * sc, sheet.height * sc), Image.NEAREST).save(path)
        print("Vorschau:", path)
    else:
        sheet.save(OUT)
        print("Geschrieben:", OUT)


if __name__ == "__main__":
    main()
