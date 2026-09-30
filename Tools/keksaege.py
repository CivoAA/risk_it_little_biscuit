"""
Zeichnet die Keksaege neu (ingame + Waffenicon + Werkbank-Icons).

  Assets/Art/Waffen/KREISsage.png            4 x 64x64  PPU 64  Saegeblatt, Frame 0 ist das
                                                               Sprite KREISsage_0 (Prefab + Icon)
  Assets/Resources/Workbench/cookie_saw_14.png   14x14
  Assets/Resources/Workbench/cookie_saw_10.png   10x10

Die .meta bleiben, wie sie sind (sonst verlieren Prefab und Player ihre
Verweise). Das Sprite KREISsage_0 liegt bei x 2, y 1 (von unten), 59x59.

Das Blatt dreht sich ingame per Transform (DrehenWaffen, +200 Grad/s, also
gegen den Uhrzeigersinn) - die steilen Schneidkanten der Zaehne zeigen darum
in diese Richtung.

Aufruf aus dem Projektordner:  python Tools/keksaege.py [--preview pfad.png]
"""

import math
import os
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SHEET = os.path.join(ROOT, "Assets", "Art", "Waffen", "KREISsage.png")
WORKBENCH = os.path.join(ROOT, "Assets", "Resources", "Workbench")

CLEAR = (0, 0, 0, 0)


def hx(s, a=255):
    s = s.lstrip("#")
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), a)


def mix(c1, c2, t):
    t = max(0.0, min(1.0, t))
    return tuple(int(round(c1[i] + (c2[i] - c1[i]) * t)) for i in range(4))


LINE = hx("#1f1a24")
# Stahl, dunkel -> hell
S0, S1, S2, S3, S4, S5 = hx("#3b3f4d"), hx("#565b6b"), hx("#7c8293"), hx("#a7adbb"), hx("#d3d8e2"), hx("#f6f8ff")
# Keks
K_EDGE, K_DARK, K_MID, K_LIGHT, K_HI = hx("#8a4f22"), hx("#b8702f"), hx("#d99a4e"), hx("#efbd72"), hx("#fbdc9c")
CHOC_D, CHOC, CHOC_L = hx("#2e160c"), hx("#4a2616"), hx("#7a4428")
# Warnlack auf dem Stahlring
RED_D, RED, RED_L = hx("#7a1c24"), hx("#c23a3a"), hx("#ef6a5a")

# Licht von oben links (y nach oben)
LX, LY = -0.62, 0.78

# Schokostuecke im Keks: (Winkel in Grad, Abstand 0..1 vom Keksrand, Radius 0..1)
CHIPS = [(20, 0.62, 0.17), (95, 0.55, 0.15), (160, 0.66, 0.16), (215, 0.5, 0.13),
         (280, 0.64, 0.17), (330, 0.42, 0.12), (60, 0.35, 0.11), (245, 0.82, 0.1)]


def saw(size, rot=0.0, teeth=14, small=False, chips=None):
    """Saegeblatt in einem size x size Bild, Mitte = Mitte des mittleren Pixels."""
    img = Image.new("RGBA", (size, size), CLEAR)
    px = img.load()
    c = size / 2.0
    R = size / 2.0 - 1.0          # Platz fuer die Kontur
    Rt = R                        # Zahnspitze
    Rr = R * (0.66 if small else 0.76)      # Zahngrund
    Rring = R * (0.50 if small else 0.58)   # Stahlring innen / Keks aussen
    Rhub = 0.0 if small else R * 0.16

    chip_pos = []
    for ang, dist, rad in (chips or CHIPS):
        a = math.radians(ang + rot)
        chip_pos.append((math.cos(a) * dist * Rring, math.sin(a) * dist * Rring, rad * Rring))

    for y in range(size):
        for x in range(size):
            dx = x + 0.5 - c
            dy = c - (y + 0.5)          # y nach oben
            r = math.hypot(dx, dy)
            if r > Rt:
                continue
            a = math.degrees(math.atan2(dy, dx)) - rot
            nx, ny = (dx / r, dy / r) if r > 0 else (0.0, 0.0)
            light = nx * LX + ny * LY   # -1..1, Aussenseite

            if r > Rr - 0.5:
                # Zaehne: Rueckenrampe, steile Schneidkante vorne (gegen Uhrzeigersinn)
                p = ((a % 360.0) / 360.0 * teeth) % 1.0
                tip = Rr + (Rt - Rr) * (p ** 0.8)
                if r > tip:
                    continue
                k = (r - Rr) / (Rt - Rr)
                col = mix(S2, S3, k)
                if p > 0.86:
                    col = S5 if k > 0.25 else S4           # geschliffene Schneide
                elif p > 0.74:
                    col = S4
                elif p < 0.2:
                    col = S1                                # Kehle, im Schatten
                if light < -0.35 and col not in (S5,):
                    col = mix(col, S0, 0.45)
                elif light > 0.45 and p <= 0.86:
                    col = mix(col, S4, 0.35)
                px[x, y] = col
            elif r > Rring:
                # Stahlring mit Fase und rotem Warnstreifen
                k = (r - Rring) / (Rr - Rring)
                if k > 0.75 or (small and k > 0.5):
                    col = S3 if light > -0.2 else S1
                    if light > 0.55:
                        col = S5
                elif k > 0.25 and not small:
                    # roter Streifen, unterbrochen von Stahlstegen
                    seg = ((a % 360.0) / 360.0 * 6) % 1.0
                    if seg < 0.14:
                        col = S2
                    else:
                        col = RED
                        if light > 0.5:
                            col = RED_L
                        elif light < -0.4:
                            col = RED_D
                else:
                    col = S2 if light < 0.3 else S3
                    if light < -0.5:
                        col = S1
                px[x, y] = col
            elif r > Rring - 1.0 and not small:
                px[x, y] = LINE
            elif r > Rhub:
                # Keks
                k = r / Rring
                col = K_MID
                ilight = -light          # Innenwoelbung: unten rechts hell, Rand oben links dunkel
                if k > 0.84:
                    col = K_DARK if ilight < 0.4 else K_MID
                    if ilight < -0.3:
                        col = K_EDGE
                elif k < 0.7 and light > 0.2 and k > 0.35:
                    col = K_LIGHT
                if k > 0.84 and ilight > 0.75:
                    col = K_LIGHT
                for (cx, cy, cr) in chip_pos:
                    d = math.hypot(dx - cx, dy - cy)
                    if d <= cr:
                        col = CHOC
                        if (dx - cx) * LX + (dy - cy) * LY > cr * 0.35:
                            col = CHOC_L
                        elif (dx - cx) * LX + (dy - cy) * LY < -cr * 0.45:
                            col = CHOC_D
                        break
                    elif d <= cr + 1.0 and (dx - cx) * LX + (dy - cy) * LY < 0:
                        col = K_DARK            # Schokostueck sitzt im Teig
                px[x, y] = col
            elif r > Rhub - 1.0:
                px[x, y] = LINE
            else:
                # Nabe: Stahlmutter mit Glanz
                k = r / max(Rhub, 1e-6)
                col = S3
                if light > 0.3 and k > 0.35:
                    col = S5
                elif light < -0.3 and k > 0.35:
                    col = S1
                if k < 0.35:
                    col = S0
                px[x, y] = col

    # Kontur
    solid = {(x, y) for y in range(size) for x in range(size) if px[x, y][3] > 0}
    for (x, y) in list(solid):
        for ddx, ddy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (x + ddx, y + ddy)
            if n not in solid and 0 <= n[0] < size and 0 <= n[1] < size:
                px[n] = LINE
    return img


# ----------------------------------------------------------------------------
#  Kleine Werkbank-Icons: Scheibe + Keks, Zaehne als feste Pixelformen, die
#  viermal um 90 Grad gedreht werden (gerader + schraeger Zahn, beide mit
#  Haken gegen den Uhrzeigersinn).
# ----------------------------------------------------------------------------

def icon(size, rb, rc, axial, diag, chips, chip_hi=()):
    img = Image.new("RGBA", (size, size), CLEAR)
    px = img.load()
    c = size / 2.0

    def lit_at(x, y):
        dx, dy = x + 0.5 - c, c - (y + 0.5)
        r = math.hypot(dx, dy)
        return ((dx * LX + dy * LY) / r if r else 0.0), r

    for y in range(size):
        for x in range(size):
            lit, r = lit_at(x, y)
            if r <= rc:
                col = K_MID
                if lit > 0.5 and r > rc * 0.5:
                    col = K_LIGHT
                elif lit < -0.35 and r > rc * 0.5:
                    col = K_DARK
                px[x, y] = col
            elif r <= rb:
                px[x, y] = S4 if lit > 0.5 else (S1 if lit < -0.8 else (S3 if lit > -0.1 else S2))

    for n in range(4):
        for shape in (axial, diag):
            for i, (x, y) in enumerate(shape):
                for _ in range(n):
                    x, y = y, size - 1 - x
                lit, _r = lit_at(x, y)
                if i == len(shape) - 1:
                    px[x, y] = S5 if lit > -0.3 else S3      # Spitze
                else:
                    px[x, y] = S3 if lit > 0.2 else (S2 if lit > -0.75 else S1)

    for p in chips:
        px[p] = CHOC
    for p in chip_hi:
        px[p] = CHOC_L

    solid = {(x, y) for y in range(size) for x in range(size) if px[x, y][3] > 0}
    for (x, y) in list(solid):
        for ddx, ddy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (x + ddx, y + ddy)
            if n not in solid and 0 <= n[0] < size and 0 <= n[1] < size:
                px[n] = LINE
    return img


def icon14():
    return icon(14, 5.0, 3.2, [(7, 1), (6, 1), (6, 0)], [(10, 2), (11, 2), (10, 1)],
                [(5, 5), (8, 6), (6, 8), (8, 8)], [(9, 6)])


def icon10():
    return icon(10, 3.4, 2.1, [(5, 1), (4, 1), (4, 0)], [(7, 1)], [(4, 4), (5, 6)])


def main():
    preview = sys.argv[sys.argv.index("--preview") + 1] if "--preview" in sys.argv else None

    sheet = Image.new("RGBA", (256, 64), CLEAR)
    for f in range(4):
        # Sprite-Rect x 2, y 1 von unten, 59x59 -> im PNG Zeilen 4..62
        sheet.paste(saw(59, rot=f * 360.0 / 14 / 4), (f * 64 + 2, 64 - 1 - 59))
    sheet.save(SHEET)
    print("geschrieben:", os.path.relpath(SHEET, ROOT))

    icons = []
    for name, fn in (("cookie_saw_14.png", icon14), ("cookie_saw_10.png", icon10)):
        img = fn()
        img.save(os.path.join(WORKBENCH, name))
        icons.append(img)
        print("geschrieben:", name)

    if preview:
        pv = Image.new("RGBA", (256 * 3 + 20, 64 * 3 + 14 * 12 + 30), (74, 96, 64, 255))
        pv.alpha_composite(sheet.resize((256 * 3, 64 * 3), Image.NEAREST), (10, 10))
        x = 10
        for img in icons:
            big = img.resize((img.width * 12, img.height * 12), Image.NEAREST)
            pv.alpha_composite(big, (x, 64 * 3 + 20))
            x += big.width + 20
        # Spielgroesse: 1.5 x 60/64 Tiles bei 32 PPU ~ 45 px, dreifach
        small = saw(59).resize((45, 45), Image.NEAREST).resize((135, 135), Image.NEAREST)
        pv.alpha_composite(small, (x + 20, 64 * 3 + 20))
        pv.save(preview)


if __name__ == "__main__":
    main()
