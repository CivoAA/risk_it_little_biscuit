"""
Zeichnet "Mochi" - ein Erdbeer-Daifuku-Maedchen: weicher rosa Mochi mit grossen
Glitzeraugen samt Wimpern, Baeckchen, einer Erdbeere als Muetzchen und einer
Schleife. Die Form wird pro Bild neu berechnet (Squash & Stretch), Gesicht,
Erdbeere und Schleife wandern mit.

Ausgabe: Assets/Art/Chars/Char_Mochi.png, 128x128 = 4x4 Zellen a 32x32,
gleiches Raster wie Char_Toast/Char_Jam (siehe Erinnerung charakter-32px-format):

  Zeile 1  Idle vorn     (2 Bilder: wabbelt)
  Zeile 2  Laufen vorn   (4 Bilder: huepft) - rechts/links per FlipX
  Zeile 3  Laufen hinten (4 Bilder)
  Zeile 4  Idle hinten   (2 Bilder)

Unterkante = unterste Zeile der Zelle (footRows 0), Pivot unten mittig.

Aufruf:  python Tools/char_erdbeere.py [--preview pfad.png]
"""

import os
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Chars", "Char_Mochi.png")

F = 32
CX = 16.0
BOTTOM = 32


def hx(s):
    s = s.lstrip('#')
    return tuple(int(s[i:i + 2], 16) for i in (0, 2, 4)) + (255,)


C = {k: hx(v) for k, v in {
    'ol': '#a14a70',   # Kontur Mochi
    'old': '#7a3356',  # Kontur unten
    'hi': '#ffffff',
    'm0': '#fff4f6',   # Mochi Licht
    'm1': '#ffe1e8',   # Mochi
    'm2': '#f7c3d1',   # Mochi Schatten
    'm3': '#e9a0b6',   # Mochi tiefer Schatten
    'E': '#3b1838',    # Auge / Wimpern
    'e1': '#7d2f6e',   # Iris
    'e2': '#c9569a',
    'e3': '#ff9ccb',
    'W': '#ffffff',
    'bl': '#ffa3bb',   # Baeckchen
    'mo': '#a8325a',   # Mund
    'sR': '#ff4d6d',   # Erdbeere
    'sr': '#d42c55',
    'sd': '#9e1b45',
    'sh': '#ff9aad',
    'sy': '#ffeaa0',
    'gL': '#9be37a',   # Blaetter
    'gG': '#5dbb5a',
    'gd': '#2f7d4b',
    'rb': '#ffd1e0',   # Schleife
    'rbd': '#f08cb0',
}.items()}

# Auge (links); das rechte ist dasselbe Bild, damit der Glanz auf beiden
# Augen oben links sitzt.
EYE = [
    ".EEE.",
    "EWWEE",
    "EWeEE",
    "Ee1eE",
    ".333.",
]
EYE_KEY = {'E': 'E', 'W': 'W', 'e': 'e1', '1': 'e2', '3': 'e3'}

BERRY = [
    "...gg.gg...",
    "..gLgGgLg..",
    ".gGGgLgGGg.",
    "gGgORRROgGg",
    ".gOhRRRyROg",
    ".OhRRyRRRrO",
    ".ORRRRRyrrO",
    "..OyRRRrrO.",
    "...OrRrrO..",
    "....OrrO...",
    ".....OO....",
]
BERRY_KEY = {'O': 'sd', 'R': 'sR', 'r': 'sr', 'h': 'sh', 'y': 'sy', 'g': 'gd', 'G': 'gG', 'L': 'gL'}

BOW = [
    "OO...OO",
    "ObOOObO",
    "ObbkbbO",
    "OBOOOBO",
    "OO...OO",
]
BOW_KEY = {'O': 'old', 'b': 'rb', 'B': 'rbd', 'k': 'sR'}

# (rx, ry, lift) je Bild
BASE = (12.6, 11.2, 0)
SQUISH = (13.2, 10.6, 0)
POSES_WALK = [(12.6, 11.2, 0), (12.0, 11.8, 2), (12.6, 11.2, 1), (13.4, 10.4, 0)]


def stamp(img, rows, x0, y0, key, flip=False):
    px = img.load()
    for dy, row in enumerate(rows):
        if flip:
            row = row[::-1]
        for dx, ch in enumerate(row):
            x, y = x0 + dx, y0 + dy
            if ch != '.' and 0 <= x < F and 0 <= y < F:
                px[x, y] = C[key[ch]]


def body(img, rx, ry, bottom):
    """Mochi: oben rund, unten flach gedrueckt, Licht von oben links."""
    px = img.load()
    cy = bottom - ry
    m = set()
    for y in range(F):
        for x in range(F):
            dx = (x + 0.5 - CX) / rx
            dy = (y + 0.5 - cy) / ry
            n = 2.2 if dy < 0 else 3.6
            if abs(dx) ** n + abs(dy) ** n <= 1.0:
                m.add((x, y))
    for (x, y) in m:
        dx = (x + 0.5 - CX) / rx
        dy = (y + 0.5 - cy) / ry
        light = -0.55 * dx - 0.85 * dy
        if dy > 0.62:
            c = 'm3' if dy > 0.82 else 'm2'
        elif light > 0.78:
            c = 'm0'
        elif dx > 0.78 or (dy > 0.45 and dx > 0.45):
            c = 'm2'
        else:
            c = 'm1'
        px[x, y] = C[c]
    for (x, y) in m:
        if any((x + a, y + b) not in m for a, b in ((1, 0), (-1, 0), (0, 1), (0, -1))):
            dy = (y + 0.5 - cy) / ry
            px[x, y] = C['old'] if dy > 0.3 else C['ol']
    gx, gy = int(CX - rx * 0.55), int(cy - ry * 0.55)
    for (x, y) in ((gx, gy), (gx + 1, gy - 1), (gx + 2, gy - 1)):
        if (x, y) in m:
            px[x, y] = C['hi']
    return cy


def face(img, ey):
    px = img.load()
    xl, xr = int(CX) - 10, int(CX) + 5
    stamp(img, EYE, xl, ey, EYE_KEY)
    stamp(img, EYE, xr, ey, EYE_KEY)
    px[xl - 1, ey + 1] = C['E']          # Wimpern aussen
    px[xr + 5, ey + 1] = C['E']
    for x in range(xl - 1, xl + 2):      # Baeckchen
        px[x, ey + 5] = C['bl']
    for x in range(xr + 3, xr + 6):
        px[x, ey + 5] = C['bl']
    mx = int(CX) - 1                     # kleiner Mund
    px[mx, ey + 4] = C['mo']
    px[mx + 1, ey + 4] = C['mo']
    px[mx - 1, ey + 3] = C['mo']
    px[mx + 2, ey + 3] = C['mo']


def frame(pose, front):
    rx, ry, lift = pose
    img = Image.new("RGBA", (F, F), (0, 0, 0, 0))
    bottom = BOTTOM - lift
    cy = body(img, rx, ry, bottom)
    top = bottom - 2 * ry
    if front:
        face(img, round(cy - 0.8))
    # Erdbeere sitzt mittig obendrauf, Schleife rechts daneben (von hinten links)
    stamp(img, BERRY, 10, round(top - 6.6), BERRY_KEY, flip=not front)
    bow_off = round(rx * 0.4)
    bx = int(CX) + bow_off if front else int(CX) - bow_off - len(BOW[0])
    stamp(img, BOW, bx, round(top - 0.6), BOW_KEY)
    return img


def frames():
    return [
        [frame(BASE, True), frame(SQUISH, True)],
        [frame(p, True) for p in POSES_WALK],
        [frame(p, False) for p in POSES_WALK],
        [frame(BASE, False), frame(SQUISH, False)],
    ]


def main():
    rows = frames()
    sheet = Image.new("RGBA", (4 * F, 4 * F), (0, 0, 0, 0))
    for r, row in enumerate(rows):
        for c, im in enumerate(row):
            sheet.alpha_composite(im, (c * F, r * F))
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
