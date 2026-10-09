"""
Zeichnet "Mochi" - ein Erdbeer-Daifuku-Maedchen, das immer Musik hoert:
kraeftig rosa Mochi mit Puderzucker-Glitzern, riesigen dunklen Kulleraugen
samt Wimpern, Baeckchen, einer Erdbeere als Muetzchen und minzgruenen
Kopfhoerern mit kleiner Schleife am Buegel. Passend zu ihrer Waffe, der
Mochi-Melodie (Tools/mochi_melodie.py): im Idle wippt sie im Takt und singt
ab und zu mit, dabei steigt ein Notenchen auf.

Version 2 (10.10.2026): Nick fand die erste Fassung (blassrosa 4x4-Sheet)
"am wenigsten Qualitaet" - jetzt mehr Kontrast, Aermchen, Kopfhoerer und
ein 8x4-Sheet wie der Schleimkoenig.

Ausgabe: Assets/Art/Chars/Char_Mochi.png, 256x128 = 8x4 Zellen a 32x32:

  Zeile 1  Idle vorn     (8 Bilder: Wippen, Blinzeln, Mitsingen; Clip = 16 Schritte)
  Zeile 2  Laufen vorn   (6 Bilder: ein ganzer Hopser) - rechts/links per FlipX
  Zeile 3  Laufen hinten (6 Bilder)
  Zeile 4  Idle hinten   (4 Bilder)

Unterkante = unterste Zeile der Zelle (footRows 0), Pivot unten mittig, PPU 32.

Schreibt Bild, .meta (guid bleibt) und die Clip-Inhalte in
Animations/Char_Mochi (Clip-guids und Controller bleiben).

Aufruf:  python Tools/char_erdbeere.py [--preview pfad.png] [--dry]
"""

import math
import os
import re
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import char_schleimkoenig as csk  # noqa: E402  (Clip-/Meta-Bausteine)

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Chars", "Char_Mochi.png")
ANIM_DIR = os.path.join(ROOT, "Assets", "Animations", "Char_Mochi")
BASE = "Char_Mochi"

F = 32
COLS, ROWS = 8, 4
CX = 16.0
BOTTOM = 32          # Unterkante (exklusiv)


def hx(s):
    s = s.lstrip('#')
    return tuple(int(s[i:i + 2], 16) for i in (0, 2, 4)) + (255,)


C = {k: hx(v) for k, v in {
    # Mochi - kraeftiger als Version 1, damit sie auf dem Boden nicht verschwindet
    'ol': '#9c2f62',   # Kontur
    'old': '#6e1d47',  # Kontur unten
    'hi': '#ffffff',
    'm0': '#fff3f7',   # Licht
    'm1': '#ffcadd',   # Grundton
    'm2': '#ffa9c6',   # Schatten
    'm3': '#f08bb0',   # tiefer Schatten
    'm4': '#d96b97',   # Kernschatten unten
    # Augen
    'E': '#341333',
    'Es': '#c94f93',   # farbiger Schimmer unten in der Iris
    'W': '#ffffff',
    'bl': '#ff86ad',   # Baeckchen
    'blh': '#ffc4d6',
    'mo': '#8a1f4f',   # Mund
    'mi': '#ff6f96',   # Mund innen
    # Erdbeere
    'sR': '#ff4566',
    'sr': '#d9264f',
    'sd': '#8f1640',
    'sh': '#ffa0b2',
    'sy': '#ffe9a0',
    'gL': '#a6ec7c',
    'gG': '#5fc25a',
    'gd': '#2c7a4c',
    # Kopfhoerer (Minze)
    'p0': '#d9fff2',
    'p1': '#8ae8cd',
    'p2': '#4fc3a8',
    'p3': '#2f8f86',
    'pd': '#1f5560',
    # Schleife + Note
    'rb': '#ff9cc0',
    'rbd': '#e0608f',
    'nt': '#ffffff',
}.items()}


def put(img, x, y, c):
    if 0 <= x < F and 0 <= y < F:
        img.putpixel((int(x), int(y)), C[c] if isinstance(c, str) else c)


def stamp(img, rows, x0, y0, key, flip=False):
    for dy, row in enumerate(rows):
        if flip:
            row = row[::-1]
        for dx, ch in enumerate(row):
            if ch != '.':
                put(img, x0 + dx, y0 + dy, key[ch])


# ------------------------------------------------------------------ Motive

EYE = [
    ".EEE.",
    "EWWEE",
    "EWEEE",
    "EEEEE",
    "EEsEE",
    ".EEE.",
]
EYE_KEY = {'E': 'E', 'W': 'W', 's': 'Es'}
EYE_SHUT = [          # Blinzeln
    ".....",
    ".....",
    ".....",
    "E...E",
    ".EEE.",
    ".....",
]
EYE_HAPPY = [         # ^ ^ beim Singen
    ".....",
    ".....",
    "..E..",
    ".E.E.",
    "E...E",
    ".....",
]

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
    "....OOO....",
]
BERRY_KEY = {'O': 'sd', 'R': 'sR', 'r': 'sr', 'h': 'sh', 'y': 'sy', 'g': 'gd', 'G': 'gG', 'L': 'gL'}

CUP = [               # Ohrmuschel, 5x7
    ".ddd.",
    "dpqqd",
    "dqrrd",
    "dqrsd",
    "dqrsd",
    "drssd",
    ".ddd.",
]
CUP_KEY = {'d': 'pd', 'p': 'p0', 'q': 'p1', 'r': 'p2', 's': 'p3'}

BOW = [
    "dd.dd",
    "dbkbd",
    "dd.dd",
]
BOW_KEY = {'d': 'rbd', 'b': 'rb', 'k': 'sR'}

NOTE = [              # Achtelnote zum Mitsingen (Kontur kommt per Code)
    "..nn.",
    "..n.n",
    "..n..",
    ".nn..",
    "nnn..",
    ".n...",
]
NOTE_KEY = {'n': 'nt'}


# ------------------------------------------------------------------ Posen

def pose(rx, ry, lift=0, berry=0, cups=0, arms=0, eyes='o', mouth='w', note=None):
    """rx/ry Koerper, lift Abstand zum Boden, berry/cups: Nachhaengen in px
    (+ = tiefer), arms: Aermchen hoch/runter, eyes o/-/^, mouth w/o, note: Hoehe der Note oder None."""
    return dict(rx=rx, ry=ry, lift=lift, berry=berry, cups=cups, arms=arms,
                eyes=eyes, mouth=mouth, note=note)


B = (12.2, 9.6)
DOWN = (12.8, 9.0)
UP = (11.7, 10.2)

IDLE_FRONT = [
    pose(*B),
    pose(*DOWN, berry=1, cups=1, arms=-1),
    pose(*B, berry=1),
    pose(*UP, berry=-1, arms=1),
    pose(*DOWN, berry=1, cups=1, arms=-1, eyes='-'),
    pose(*DOWN, berry=1, cups=1, arms=1, eyes='^', mouth='o', note=0),
    pose(*UP, berry=-1, arms=2, eyes='^', mouth='o', note=1),
    pose(*B, berry=0, arms=1, eyes='^', mouth='o', note=2),
]
# 2 s bei 8 fps: wippen, blinzeln, wippen, mitsingen
IDLE_FRONT_SEQ = [0, 1, 2, 3, 0, 4, 2, 3, 0, 1, 2, 3, 5, 6, 7, 3]

# Hopser: Landung, Erholen, Ausholen, Absprung, Scheitel, Fallen
WALK = [
    pose(13.2, 8.1, 0, berry=2, cups=2, arms=-2, eyes='^'),
    pose(12.4, 9.4, 0, berry=1, cups=1, arms=-1),
    pose(13.0, 8.7, 0, berry=1, cups=1, arms=-1),
    pose(11.0, 10.8, 1, berry=-1, cups=-1, arms=2),
    pose(11.6, 10.1, 3, berry=-2, cups=-2, arms=1),
    pose(11.9, 9.8, 2, berry=-1, cups=-1, arms=0),
]

IDLE_BACK = [
    pose(*B),
    pose(*DOWN, berry=1, cups=1, arms=-1),
    pose(*B, berry=1),
    pose(*UP, berry=-1, arms=1),
]

FPS_IDLE = 8
FPS_WALK = 12


# ------------------------------------------------------------------ Zeichnen

def body_mask(rx, ry, bottom):
    cy = bottom - ry
    m = set()
    for y in range(F):
        for x in range(F):
            dx = (x + 0.5 - CX) / rx
            dy = (y + 0.5 - cy) / ry
            n = 2.1 if dy < 0 else 3.4
            if abs(dx) ** n + abs(dy) ** n <= 1.0:
                m.add((x, y))
    return m, cy


def edge(m, x, y):
    return any((x + a, y + b) not in m for a, b in ((1, 0), (-1, 0), (0, 1), (0, -1)))


def body(img, p, bottom, front):
    rx, ry = p['rx'], p['ry']
    m, cy = body_mask(rx, ry, bottom)
    for (x, y) in m:
        dx = (x + 0.5 - CX) / rx
        dy = (y + 0.5 - cy) / ry
        light = -0.5 * dx - 0.85 * dy
        if dy > 0.8:
            c = 'm4'
        elif dy > 0.6 or (dx > 0.8 and dy > 0.1):
            c = 'm3'
        elif light < -0.45 or dx > 0.72:
            c = 'm2'
        elif light > 0.7:
            c = 'm0'
        else:
            c = 'm1'
        put(img, x, y, c)
    for (x, y) in m:
        if edge(m, x, y):
            dy = (y + 0.5 - cy) / ry
            put(img, x, y, 'old' if dy > 0.25 else 'ol')
    # Glanz oben links + Puderzucker
    gx, gy = int(CX - rx * 0.55), int(cy - ry * 0.62)
    for (x, y) in ((gx, gy + 1), (gx, gy), (gx + 1, gy - 1), (gx + 2, gy - 1)):
        if (x, y) in m and not edge(m, x, y):
            put(img, x, y, 'hi')
    for (fx, fy) in ((0.35, -0.75), (0.62, -0.42), (-0.2, -0.85)):
        x, y = int(CX + fx * rx), int(cy + fy * ry)
        if (x, y) in m and not edge(m, x, y):
            put(img, x, y, 'hi')
    if not front:
        # Daifuku-Naht hinten: die zusammengedrueckte Falte
        sx, sy = int(CX) - 1, int(bottom - ry * 0.55)
        for (x, y, c) in ((sx, sy, 'm3'), (sx + 1, sy + 1, 'm3'), (sx + 2, sy, 'm3'),
                          (sx + 1, sy - 1, 'm2'), (sx, sy + 2, 'm3'), (sx + 2, sy + 2, 'm3')):
            put(img, x, y, c)
    return m, cy, bottom - 2 * ry


def arms(img, p, bottom, cy):
    """Stummelaermchen links und rechts, ragen ein Stueck aus dem Koerper."""
    rx = p['rx']
    y0 = int(round(cy + p['ry'] * 0.3 - p['arms']))
    shape = [".oo.", "omMo", "ommo", ".oo."]
    for side in (-1, 1):
        x0 = int(round(CX + side * rx)) - 2
        rows = shape if side < 0 else [r[::-1] for r in shape]
        stamp(img, rows, x0, y0, {'o': 'ol', 'm': 'm2', 'M': 'm1'})


def face(img, p, cy):
    ey = int(round(cy - 1.5))
    xl, xr = int(CX) - 10, int(CX) + 5
    shape = {'o': EYE, '-': EYE_SHUT, '^': EYE_HAPPY}[p['eyes']]
    stamp(img, shape, xl, ey, EYE_KEY)
    stamp(img, shape, xr, ey, EYE_KEY)
    if p['eyes'] == 'o':
        put(img, xl - 1, ey + 1, 'E')            # Wimpern aussen
        put(img, xl - 1, ey, 'E')
        put(img, xr + 5, ey + 1, 'E')
        put(img, xr + 5, ey, 'E')
        put(img, xl + 3, ey + 4, 'W')            # zweiter kleiner Glanz
        put(img, xr + 3, ey + 4, 'W')
    else:
        put(img, xl - 1, ey + 3, 'E')
        put(img, xr + 5, ey + 3, 'E')
    # Baeckchen
    by = ey + 6
    for x in range(xl - 1, xl + 3):
        put(img, x, by, 'bl')
    for x in range(xr + 2, xr + 6):
        put(img, x, by, 'bl')
    put(img, xl, by, 'blh')
    put(img, xr + 3, by, 'blh')
    # Mund
    mx, my = int(CX) - 2, ey + 5
    if p['mouth'] == 'w':
        for (x, y) in ((mx, my), (mx + 1, my + 1), (mx + 2, my), (mx + 3, my + 1), (mx + 4, my)):
            put(img, x, y, 'mo')
    else:
        stamp(img, [".mmm.", "mfffm", ".mfm."], mx, my, {'m': 'mo', 'f': 'mi'})


def headphones(img, p, top, cy, front, part):
    """part 'band' (hinter dem Kopf) oder 'cups' (davor)."""
    rx = p['rx']
    cup_y = int(round(top + 2 + p['cups']))
    lx = int(round(CX - rx)) - 2
    rxp = int(round(CX + rx)) - 2
    if part == 'band':
        # Buegel: Bogen ueber dem Kopf von Muschel zu Muschel
        a, b = (rxp + 2 - lx) / 2.0, (cup_y + 1) - (top - 3)
        mx = lx + a
        for i in range(0, 61):
            t = math.pi * i / 60
            x = mx - math.cos(t) * a
            y = cup_y + 1 - math.sin(t) * b
            put(img, round(x), round(y), 'pd')
            put(img, round(x), round(y) + 1, 'p2')
        return
    stamp(img, CUP, lx, cup_y, CUP_KEY, flip=False)
    stamp(img, CUP, rxp, cup_y, CUP_KEY, flip=True)
    # Herzchen auf der Muschel, die man sieht
    hxp = lx + 1 if front else rxp + 1
    put(img, hxp + 1, cup_y + 3, 'rb')
    put(img, hxp + 2, cup_y + 3, 'rb')


def bow(img, p, top, front):
    # Schleife am Buegel, rechts oben (von hinten gespiegelt links)
    off = int(round(p['rx'] * 0.62))
    x = int(CX) + off - 2 if front else int(CX) - off - 3
    stamp(img, BOW, x, int(round(top - 2 + p['cups'] * 0.5)), BOW_KEY)


def note(img, p):
    if p['note'] is None:
        return
    h = p['note']
    x0, y0 = 1 + (h % 2), 8 - 3 * h
    inner = set()
    for dy, row in enumerate(NOTE):
        for dx, ch in enumerate(row):
            if ch != '.':
                inner.add((x0 + dx, y0 + dy))
    for (x, y) in inner:
        for a, b in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            if (x + a, y + b) not in inner:
                put(img, x + a, y + b, 'p3')
    for (x, y) in inner:
        put(img, x, y, 'nt')


def frame(p, front):
    img = Image.new("RGBA", (F, F), (0, 0, 0, 0))
    bottom = BOTTOM - p['lift']
    _, cy0 = body_mask(p['rx'], p['ry'], bottom)
    top = bottom - 2 * p['ry']
    headphones(img, p, top, cy0, front, 'band')
    m, cy, top = body(img, p, bottom, front)
    arms(img, p, bottom, cy)
    if front:
        face(img, p, cy)
    stamp(img, BERRY, int(CX) - 6, int(round(top - 7 + p['berry'] * 0.5)), BERRY_KEY, flip=not front)
    headphones(img, p, top, cy, front, 'cups')
    bow(img, p, top, front)
    if front:
        note(img, p)
    return img


def frames():
    return [
        [frame(p, True) for p in IDLE_FRONT],
        [frame(p, True) for p in WALK],
        [frame(p, False) for p in WALK],
        [frame(p, False) for p in IDLE_BACK],
    ]


# ------------------------------------------------------------------ Unity

CLIPS = {
    "Front_I": (0, IDLE_FRONT_SEQ, FPS_IDLE, False),
    "Front_IR": (0, IDLE_FRONT_SEQ, FPS_IDLE, True),
    "Walk_D": (1, list(range(len(WALK))), FPS_WALK, False),
    "Walk_L": (1, list(range(len(WALK))), FPS_WALK, False),
    "Walk_R": (1, list(range(len(WALK))), FPS_WALK, True),
    "Walk_U": (2, list(range(len(WALK))), FPS_WALK, False),
    "Walk_UL": (2, list(range(len(WALK))), FPS_WALK, True),
    "Back_I": (3, list(range(len(IDLE_BACK))), FPS_IDLE, False),
    "Back_IL": (3, list(range(len(IDLE_BACK))), FPS_IDLE, True),
}


def write_unity(rows):
    meta = OUT + ".meta"
    used = [(r, c) for r, row in enumerate(rows) for c in range(len(row))]
    old_guid = re.search(r"^guid: (\w+)", open(meta).read(), re.M).group(1) if os.path.exists(meta) else None
    need = "%s_%d" % (BASE, 3 * COLS + len(IDLE_BACK) - 1)
    if old_guid is None or ("second: %s\n" % need) not in open(meta).read():
        # erstes Mal im 8x4-Raster: Meta neu, guid behalten
        csk.BASE, csk.F, csk.COLS, csk.ROWS = BASE, F, COLS, ROWS
        csk.write_meta(meta, used)
        if old_guid:
            text = open(meta).read()
            text = re.sub(r"^guid: \w+", "guid: " + old_guid, text, count=1, flags=re.M)
            with open(meta, "w", newline="\n") as fh:
                fh.write(text)
        print("Meta neu (8x4):", os.path.relpath(meta, ROOT))
    text = open(meta).read()
    guid = re.search(r"^guid: (\w+)", text, re.M).group(1)
    table = dict((n, int(i)) for n, i in re.findall(r"^      (%s_\d+): (-?\d+)$" % BASE, text, re.M))

    for short, (r, seq, rate, flip) in CLIPS.items():
        name = "Mochi_" + short
        path = os.path.join(ANIM_DIR, name + ".anim")
        ids = [table["%s_%d" % (BASE, r * COLS + c)] for c in seq]
        with open(path, "w", newline="\n") as fh:
            fh.write(csk.clip_text(name, guid, ids, rate, flip))
    print("Clips neu geschrieben:", len(CLIPS))


def main():
    rows = frames()
    sheet = Image.new("RGBA", (COLS * F, ROWS * F), (0, 0, 0, 0))
    for r, row in enumerate(rows):
        for c, im in enumerate(row):
            sheet.alpha_composite(im, (c * F, r * F))
    if "--dry" not in sys.argv:
        sheet.save(OUT)
        print("geschrieben:", OUT)
        write_unity(rows)

    if "--preview" in sys.argv:
        path = sys.argv[sys.argv.index("--preview") + 1]
        s = 6
        bg = Image.new("RGBA", sheet.size, (90, 110, 90, 255))
        bg.alpha_composite(sheet)
        bg.resize((sheet.width * s, sheet.height * s), Image.NEAREST).save(path)
        gif = []
        for i in range(48):
            t = i / 24
            f = Image.new("RGBA", (4 * F + 18, F + 6), (90, 110, 90, 255))
            f.alpha_composite(rows[0][IDLE_FRONT_SEQ[int(t * FPS_IDLE) % len(IDLE_FRONT_SEQ)]], (3, 3))
            f.alpha_composite(rows[1][int(t * FPS_WALK) % len(WALK)], (F + 7, 3))
            f.alpha_composite(rows[2][int(t * FPS_WALK) % len(WALK)], (2 * F + 11, 3))
            f.alpha_composite(rows[3][int(t * FPS_IDLE) % len(IDLE_BACK)], (3 * F + 15, 3))
            gif.append(f.resize((f.width * s, f.height * s), Image.NEAREST).convert("P"))
        gif[0].save(os.path.splitext(path)[0] + ".gif", save_all=True,
                    append_images=gif[1:], duration=1000 // 24, loop=0)


if __name__ == "__main__":
    main()
