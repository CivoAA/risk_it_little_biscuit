"""
Zeichnet "Inari" - einen kleinen Fuchs aus Inari-Sushi: Kopf und Koerper sind
eine fuchsrote Tofutasche (Aburaage, mit Poren), oben quillt ein Reishaeubchen
heraus. Dazu alles, was ihn zum Fuchs macht: grosse Spitzohren mit weissem
Flaum, die Fuchsmaske (oranger Keil bis zur Nase, weisse Wangen mit
Puscheln), dunkle Pfoten und ein buschiger Schwanz mit weisser Spitze.
Um den Hals ein blaues Halstuch, dessen Zipfel vorn herunterhaengt und
beim Laufen schwingt.
Die Form wird pro Bild neu berechnet (Squash & Stretch), alles andere
wandert mit.

Ausgabe: Assets/Art/Chars/Char_Fuchs.png, 128x128 = 4x4 Zellen a 32x32,
gleiches Raster wie Char_Mochi/Char_Toast (siehe Erinnerung charakter-32px-format):

  Zeile 1  Idle vorn     (2 Bilder: atmet, Schwanz wedelt)
  Zeile 2  Laufen vorn   (4 Bilder: huepft) - rechts/links per FlipX
  Zeile 3  Laufen hinten (4 Bilder)
  Zeile 4  Idle hinten   (2 Bilder)

Unterkante = unterste Zeile der Zelle (footRows 0), Pivot unten mittig.

Aufruf:  python Tools/char_fuchs.py [--preview pfad.png] [--dry]
"""

import math
import os
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Chars", "Char_Fuchs.png")

F = 32
CX = 16.0
BOTTOM = 31          # unterste Zeile der Pfoten
EYES = 'a'           # Augenvariante (siehe EYE_SETS)


def hx(s):
    s = s.lstrip('#')
    return tuple(int(s[i:i + 2], 16) for i in (0, 2, 4)) + (255,)


C = {k: hx(v) for k, v in {
    # Fuchsfell / Tofutasche
    'ol': '#7d3218',   # Kontur
    'old': '#561f12',  # Kontur unten
    'f0': '#ffc47a',   # Licht
    'f1': '#f59a45',   # Grundton
    'f2': '#dc7531',   # Schatten
    'f3': '#b35526',   # tiefer Schatten
    'fp': '#e98a3c',   # Poren der Tofuhaut
    # weisses Fell / Reis
    'w0': '#ffffff',
    'w1': '#fff4e6',
    'w2': '#efd9c4',
    'wo': '#b98468',   # Kontur weiss
    # Ohren / Pfoten
    'tip': '#3f1d16',
    'ti': '#5c2c1f',
    # Gesicht
    'E': '#2a1424',    # Augenkontur / Wimpern
    'p': '#140a14',    # Pupille
    'W': '#ffffff',
    'bl': '#ff9a8c',   # Baeckchen
    'no': '#2a1414',   # Nase
    'nh': '#6a4a4a',   # Nasenglanz
    'mo': '#7a3020',   # Mund
    # Halstuch
    'b0': '#a8dcff',
    'b1': '#5cabef',
    'b2': '#3a82d4',
    'b3': '#2a62b0',
    'bo': '#1d3f7a',
}.items()}

# Augenvarianten (linkes Auge; das rechte ist dasselbe Bild, damit der Glanz
# auf beiden Augen oben links sitzt). Dunkle Kulleraugen wie beim Mochi:
# fast ganz Iris, nur ein Glanzpunkt - grosses Weiss wirkt sonst wie ein
# starrender Blick. d = dunkel, m = mittel, l = Lichtschimmer unten.
EYE_SETS = {
    # Mochi-Rezept: 5x5, Doppelglanz oben links, blauer Schimmer unten
    'a': ([
        ".EEE.",
        "EWWdE",
        "EWddE",
        "EdmlE",
        ".EEE.",
    ], {'d': '#2a1e3c', 'm': '#3f4f96', 'l': '#7fb4f0'}),
    # etwas groesser (6x5), runder Glanz, warmer Braunschimmer
    'b': ([
        ".EEEE.",
        "EWWddE",
        "EWdddE",
        "EddmlE",
        ".EEEE.",
    ], {'d': '#2c1a22', 'm': '#6a3a3a', 'l': '#c88a5a'}),
    # 5x6, hoch-oval, Glanz oben + kleiner Glanz unten
    'c': ([
        ".EEE.",
        "EWWdE",
        "EWddE",
        "EdddE",
        "EdmWE",
        ".EEE.",
    ], {'d': '#2a1e3c', 'm': '#4a5aa8', 'l': '#7fb4f0'}),
}

# linkes Ohr, Spitze kippt leicht nach aussen; das rechte ist gespiegelt.
# T = dunkle Spitze, i/w = weisser Flaum innen
EAR = [
    ".T......",
    ".TT.....",
    "OTTT....",
    "OTtTO...",
    "OLfiiO..",
    "OLfiiiO.",
    "OLfiwiO.",
    "OLfiwwiO",
    "OffiwwiO",
    "Offiiwif",
]
EAR_KEY = {'T': 'tip', 't': 'ti', 'O': 'ol', 'L': 'f0', 'f': 'f1', 'i': 'w2', 'w': 'w1'}
EAR_BACK_KEY = {'T': 'tip', 't': 'ti', 'O': 'ol', 'L': 'f0', 'f': 'f1', 'i': 'f2', 'w': 'f2'}

# Knoten des Halstuchs von hinten, zwei Zipfel haengen herunter
KNOT = [
    ".OO.OO.",
    "ObbObbO",
    ".OBkBO.",
    ".ObOBO.",
    "ObBOObO",
    "OBO.OBO",
    "OO...OO",
]
KNOT_KEY = {'O': 'bo', 'b': 'b1', 'B': 'b2', 'k': 'b0'}

# (rx, ry, lift, Schwanzwinkel, Tuchschwung) je Bild
IDLE = [(11.0, 11.0, 0, 0.0, 0), (11.5, 10.6, 0, 0.14, 0)]
WALK = [(11.0, 11.0, 0, -0.10, 0), (10.6, 11.4, 2, 0.08, -1), (11.0, 11.0, 1, 0.18, 0), (11.6, 10.4, 0, 0.02, 1)]
# Pfoten: (links hoch, rechts hoch) je Laufbild
STEP = [(0, 0), (1, 0), (0, 0), (0, 1)]


# ---------------------------------------------------------------- Werkzeuge

def put(img, x, y, c):
    if 0 <= x < F and 0 <= y < F:
        img.load()[x, y] = C[c] if isinstance(c, str) else c


def stamp(img, rows, x0, y0, key, flip=False):
    for dy, row in enumerate(rows):
        if flip:
            row = row[::-1]
        for dx, ch in enumerate(row):
            if ch != '.':
                v = key[ch]
                put(img, x0 + dx, y0 + dy, hx(v) if v.startswith('#') else v)


N4 = ((1, 0), (-1, 0), (0, 1), (0, -1))


def edge(m, x, y):
    return any((x + a, y + b) not in m for a, b in N4)


def fill_mask(img, m, shade, outline):
    """shade(x, y) -> Farbe innen, outline(x, y) -> Farbe am Rand."""
    for (x, y) in m:
        put(img, x, y, outline(x, y) if edge(m, x, y) else shade(x, y))


def circles_mask(pts):
    m = set()
    for (px_, py_, r) in pts:
        for y in range(int(py_ - r - 1), int(py_ + r + 2)):
            for x in range(int(px_ - r - 1), int(px_ + r + 2)):
                if (x + 0.5 - px_) ** 2 + (y + 0.5 - py_) ** 2 <= r * r:
                    m.add((x, y))
    # einzelne Ausreisser-Pixel weg, sonst steht ein Konturpunkt allein da
    return {(x, y) for (x, y) in m if sum((x + a, y + b) in m for a, b in N4) >= 2}


# ---------------------------------------------------------------- Teile

TAIL_FRONT = [(-1, 0, 2.0), (3, -3, 2.7), (6, -8, 3.1), (6, -14, 3.0), (3, -18, 2.4)]
TAIL_BACK = [(0, 0, 2.3), (-4, -1, 2.8), (-7, -4, 3.1), (-8, -9, 2.8), (-6, -13, 2.2)]


def tail(img, base, ctrl, wag):
    """Buschiger Schwanz: Kreise entlang einer Kurve, um die Wurzel gedreht; die Spitze ist weiss."""
    bx, by = base
    ca, sa = math.cos(wag), math.sin(wag)
    key = [(bx + x * ca - y * sa, by + x * sa + y * ca, r) for (x, y, r) in ctrl]
    pts = []
    for i in range(len(key) - 1):
        (x0, y0, r0), (x1, y1, r1) = key[i], key[i + 1]
        for k in range(4):
            t = k / 4
            pts.append((x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, r0 + (r1 - r0) * t))
    pts.append(key[-1])
    m = circles_mask(pts)
    tx, ty, _ = key[-1]
    tx2, ty2, _ = key[-2]
    mx, my = tx2 + (tx - tx2) * 0.35, ty2 + (ty - ty2) * 0.35
    vx, vy = tx - tx2, ty - ty2

    def is_tip(x, y):
        return (x + 0.5 - mx) * vx + (y + 0.5 - my) * vy > 0

    def shade(x, y):
        if is_tip(x, y):
            return 'w0' if (x - 1, y - 1) not in m else 'w1'
        if (x - 1, y - 1) not in m or (x, y - 1) not in m:
            return 'f0'
        if (x + 1, y + 1) not in m:
            return 'f3'
        if (x + 2, y + 1) not in m or (x + 1, y + 2) not in m:
            return 'f2'
        return 'f1'
    fill_mask(img, m, shade, lambda x, y: 'wo' if is_tip(x, y) else ('old' if (x, y + 1) not in m else 'ol'))


def ear(img, cx, top, side, front):
    rows = EAR if side < 0 else [r[::-1] for r in EAR]
    x0 = int(round(cx)) - (5 if side < 0 else 2)
    stamp(img, rows, x0, int(round(top)), EAR_KEY if front else EAR_BACK_KEY)


def body_mask(rx, ry, bottom):
    cy = bottom - ry
    top = bottom - 2 * ry
    m = set()
    for y in range(F):
        for x in range(F):
            dx = (x + 0.5 - CX) / rx
            dy = (y + 0.5 - cy) / ry
            n = 2.1 if dy < 0 else 3.0
            if abs(dx) ** n + abs(dy) ** n <= 1.0:
                m.add((x, y))
            # Reis quillt als Haeubchen oben aus der Tasche
            if ((x + 0.5 - CX) / (rx * 0.5)) ** 2 + ((y + 0.5 - (top + 1.0)) / 2.6) ** 2 <= 1.0:
                m.add((x, y))
    # Reiskoerner als kleine Huckel auf der Haube
    for x in range(F):
        col = [y for (xx, y) in m if xx == x]
        if col and abs(x + 0.5 - CX) < rx * 0.36 and x % 3 != 1:
            m.add((x, min(col) - 1))
    return m, cy, top


def body(img, rx, ry, bottom, front, ey):
    """Tofutasche mit Fuchsmaske: oranger Keil bis zur Nase, weisse Wangen und Kinn."""
    m, cy, top = body_mask(rx, ry, bottom)

    def is_rice(x, y):
        dx = (x + 0.5 - CX) / rx
        return y + 0.5 < top + 2.4 - 2.0 * dx * dx

    def is_white(x, y):
        if not front:
            return False
        d = abs(x + 0.5 - CX)
        # Wangen ab Augenunterkante, zur Mitte hin zieht sich der orange Keil zur Nase
        line = ey + 2.6 + max(0.0, 3.4 - d) * 0.75 + (d / rx) ** 2 * 1.5
        return y + 0.5 > line and d < rx * 0.98 and y < ey + 9

    def shade(x, y):
        dx = (x + 0.5 - CX) / rx
        dy = (y + 0.5 - cy) / ry
        if is_rice(x, y):
            if not is_rice(x, y + 1):
                return 'w2'
            if (x * 3 + y * 5) % 7 == 0:
                return 'w2'
            return 'w0' if dx < -0.1 else 'w1'
        if is_rice(x, y - 1):
            return 'f3'                       # Taschenrand unter dem Reis
        if is_white(x, y):
            return 'w2' if dx > 0.55 or not is_white(x, y - 1) and abs(dx) > 0.3 else 'w1'
        light = -0.55 * dx - 0.85 * dy
        if dy > 0.62:
            c = 'f3' if dy > 0.84 else 'f2'
        elif light > 0.6:
            c = 'f0'
        elif dx > 0.76 or (dy > 0.42 and dx > 0.42):
            c = 'f2'
        else:
            c = 'f1'
        if c == 'f1' and (x * 5 + y * 3) % 11 == 0:
            c = 'fp'                          # Poren der Tofuhaut
        return c

    def outl(x, y):
        if is_rice(x, y):
            return 'wo'
        dy = (y + 0.5 - cy) / ry
        return 'old' if dy > 0.3 else 'ol'
    fill_mask(img, m, shade, outl)
    gx, gy = int(CX - rx * 0.66), int(cy - ry * 0.45)
    for (x, y) in ((gx, gy), (gx, gy + 1)):
        if (x, y) in m and not is_rice(x, y) and not edge(m, x, y):
            put(img, x, y, 'w1')
    return m, cy, top


def cheek_tufts(img, m, y):
    """Spitze weisse Wangenpuschel, die seitlich aus dem Gesicht stehen."""
    rows = ["O..",
            "wOo",
            "wwO",
            "wO.",
            "O.."]
    for side in (-1, 1):
        xs = [x for (x, yy) in m if yy == y]
        ex = max(xs) if side > 0 else min(xs)
        for dy, row in enumerate(rows):
            for dx, ch in enumerate(row):
                if ch != '.':
                    put(img, ex + side * dx, y + dy, 'w1' if ch == 'w' else 'wo')


def face(img, ey):
    rows, iris = EYE_SETS[EYES]
    key = {'E': 'E', 'W': 'W', 'd': iris['d'], 'm': iris['m'], 'l': iris['l']}
    w = len(rows[0])
    xl, xr = int(CX) - 4 - w, int(CX) + 4
    stamp(img, rows, xl, ey, key)
    stamp(img, rows, xr, ey, key)
    # eine Wimper aussen, leicht nach oben
    put(img, xl - 1, ey + 1, 'E')
    put(img, xr + w, ey + 1, 'E')
    n = int(CX) - 1
    # Nase: kleines Knoepfchen mit Glanz
    put(img, n, ey + 3, 'nh')
    put(img, n + 1, ey + 3, 'no')
    put(img, n, ey + 4, 'no')
    put(img, n + 1, ey + 4, 'no')
    # Mund: kleines w
    put(img, n - 1, ey + 5, 'mo')
    put(img, n + 2, ey + 5, 'mo')
    put(img, n, ey + 6, 'mo')
    put(img, n + 1, ey + 6, 'mo')
    # Baeckchen unter den Augen, aussen
    for x in (xl - 1, xl, xl + 1):
        put(img, x, ey + 5, 'bl')
    for x in (xr + w - 2, xr + w - 1, xr + w):
        put(img, x, ey + 5, 'bl')


def fabric(img, pts, wid):
    """Ein Stoffband entlang einer Punktliste; Ende mit Schwalbenschwanz-Kerbe
    und weissem Streifen kurz davor. Licht links, Schatten rechts."""
    m = set()
    rows = []
    for i in range(len(pts) - 1):
        (x0, y0), (x1, y1) = pts[i], pts[i + 1]
        for y in range(int(round(y0)), int(round(y1))):
            t = (y - y0) / max(1e-6, y1 - y0)
            rows.append((y, x0 + (x1 - x0) * t))
    rows.append((int(round(pts[-1][1])), pts[-1][0]))
    last = rows[-1][0]
    for (y, c) in rows:
        for x in range(int(math.floor(c - wid / 2)), int(math.floor(c - wid / 2)) + wid):
            m.add((x, y))
    # Kerbe am Ende
    c_end = rows[-1][1]
    m.discard((int(math.floor(c_end - wid / 2)) + wid // 2, last))
    stripe = last - 2

    def shade(x, y):
        c = dict(rows).get(y, c_end)
        rel = x + 0.5 - c
        if y == stripe:
            return 'w1'
        if rel < -0.5:
            return 'b0'
        if rel > 0.5:
            return 'b2'
        return 'b1'
    fill_mask(img, m, shade, lambda x, y: 'bo')


def scarf(img, m, y0, swing, front):
    """Blaues Halstuch: dickes Band um den Hals, seitlich geknotet; zwei breite
    Enden haengen herunter und flattern beim Laufen."""
    side = -1 if front else 1                 # Knoten links (von hinten rechts)

    def sag(x):
        # das Band legt sich um den runden Koerper: in der Mitte einen Pixel tiefer
        return 1 if abs(x + 0.5 - CX) < 6 else 0
    band = {(x, y) for (x, y) in m if y0 - 1 + sag(x) <= y <= y0 + 2 + sag(x)}

    def bshade(x, y):
        r = y - (y0 - 1 + sag(x))
        if r == 1:
            return 'b0' if x + 0.5 < CX - 2 else 'b1'
        return 'b2' if x + 0.5 < CX + 5 else 'b3'
    fill_mask(img, band, bshade, lambda x, y: 'bo')
    kx = int(CX) + side * 7
    ky = y0 + 2
    # Enden: eines haengt fast gerade, das andere steht nach aussen ab
    fabric(img, [(kx + side * 2.5, ky), (kx + side * 5.0 + swing * 0.3, ky + 3),
                 (kx + side * 6.0 + swing * 0.8, ky + 5)], 4)
    fabric(img, [(kx + side * 0.5, ky), (kx + side * 1.0 + swing * 0.5, ky + 4),
                 (kx + side * 0.5 + swing, ky + 7)], 4)
    # Knoten
    stamp(img, [".OOO.", "ObkbO", "ObbBO", "OBBBO", ".OOO."], kx - 2, y0 - 2,
          {'O': 'bo', 'b': 'b1', 'B': 'b2', 'k': 'b0'})


def paws(img, bottom, lift_l, lift_r):
    for side, lift in ((-1, lift_l), (1, lift_r)):
        x0 = int(CX) - 7 if side < 0 else int(CX) + 3
        y = bottom - lift
        for dx in range(4):
            put(img, x0 + dx, y, 'old')
            put(img, x0 + dx, y - 1, 'tip' if dx in (1, 2) else 'old')


# ---------------------------------------------------------------- Bild

def frame(pose, front, step=(0, 0)):
    rx, ry, lift, wag, swing = pose
    img = Image.new("RGBA", (F, F), (0, 0, 0, 0))
    bottom = BOTTOM - lift          # Pfoten ueberdecken die unterste Zeile
    if front:
        # Schwanz von vorn: ringelt sich rechts hinter dem Koerper hoch
        tail(img, (CX + rx * 0.6, bottom - 3), TAIL_FRONT, -wag)
    top = bottom - 2 * ry
    ear(img, CX - rx * 0.5, top - 7.0, -1, front)
    ear(img, CX + rx * 0.5, top - 7.0, 1, front)
    ey = round(top + 5.4)       # Augen tief = Kindchenschema
    m, cy, top = body(img, rx, ry, bottom, front, ey)
    y0 = ey + 8
    if front:
        cheek_tufts(img, m, ey + 3)
        face(img, ey)
        paws(img, BOTTOM - lift, *step)
        scarf(img, m, y0, swing, True)
    else:
        scarf(img, m, y0, -swing, False)
        paws(img, BOTTOM - lift, *step)
        tail(img, (CX + 1, bottom - 2), TAIL_BACK, wag)
    return img


def frames():
    return [
        [frame(p, True) for p in IDLE],
        [frame(p, True, s) for p, s in zip(WALK, STEP)],
        [frame(p, False, s) for p, s in zip(WALK, STEP)],
        [frame(p, False) for p in IDLE],
    ]


def main():
    rows = frames()
    sheet = Image.new("RGBA", (4 * F, 4 * F), (0, 0, 0, 0))
    for r, row in enumerate(rows):
        for c, im in enumerate(row):
            sheet.alpha_composite(im, (c * F, r * F))
    if "--dry" not in sys.argv:
        sheet.save(OUT)
        print("geschrieben:", OUT)

    if "--preview" in sys.argv:
        path = sys.argv[sys.argv.index("--preview") + 1]
        s = 6
        bg = Image.new("RGBA", sheet.size, (90, 110, 90, 255))
        bg.alpha_composite(sheet)
        bg.resize((sheet.width * s, sheet.height * s), Image.NEAREST).save(path)
        gif = []
        for i in range(8):
            f = Image.new("RGBA", (3 * F + 12, F + 4), (90, 110, 90, 255))
            f.alpha_composite(rows[0][(i // 4) % 2], (2, 2))
            f.alpha_composite(rows[1][i % 4], (F + 6, 2))
            f.alpha_composite(rows[2][i % 4], (2 * F + 10, 2))
            gif.append(f.resize((f.width * s, f.height * s), Image.NEAREST).convert("P"))
        gif[0].save(os.path.splitext(path)[0] + ".gif", save_all=True,
                    append_images=gif[1:], duration=111, loop=0)


if __name__ == "__main__":
    main()
