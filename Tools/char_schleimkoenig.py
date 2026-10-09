"""
Zeichnet den kleinen "Schleimkoenig" als spielbaren Charakter - die Mini-
Ausgabe des Zwischenbosses (Tools/schleimkoenig.py). Ein Wackelpudding aus
Traubengelee mit goldener Krone (Hermelinband, roter Stein) und einem Zepter
mit Weintraube obendrauf. Die Krone ist eine Spur zu klein und haengt beim
Huepfen hinterher, im Gelee steigen Blaeschen auf.

Ausgabe: Assets/Art/Chars/Char_SlimeKing.png, 256x128 = 8x4 Zellen a 32x32.
Mehr Bilder als die anderen Charaktere (die haben 4x4), darum schreibt das
Skript auch die Clips selbst:

  Zeile 1  Idle vorn     (8 Bilder: Wabbeln; im Clip 16 Schritte mit Blinzeln + Kronenfunkeln)
  Zeile 2  Laufen vorn   (6 Bilder: ein ganzer Hopser) - rechts/links per FlipX
  Zeile 3  Laufen hinten (6 Bilder)
  Zeile 4  Idle hinten   (4 Bilder)

Unterkante = unterste Zeile der Zelle (footRows 0), Pivot unten mittig, PPU 32.

Beim ersten Lauf entstehen .meta, Clips und Controller (Klon von
Animations/Char_Fuchs/Fuchs_Char.controller) - danach werden nur noch Bild
und Clip-Inhalte neu geschrieben, guids/IDs bleiben.

Aufruf:  python Tools/char_schleimkoenig.py [--preview pfad.png] [--dry]
"""

import math
import os
import random
import re
import sys
import uuid

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Chars", "Char_SlimeKing.png")
ANIM_DIR = os.path.join(ROOT, "Assets", "Animations", "Char_SlimeKing")
TEMPLATE_CTRL = os.path.join(ROOT, "Assets", "Animations", "Char_Fuchs", "Fuchs_Char.controller")
BASE = "Char_SlimeKing"

F = 32
COLS, ROWS = 8, 4
CX = 16.0
BOTTOM = 31          # unterste Zeile des Gelees


def hx(s):
    s = s.lstrip('#')
    return tuple(int(s[i:i + 2], 16) for i in (0, 2, 4)) + (255,)


C = {k: hx(v) for k, v in {
    # Traubengelee
    'ol': '#4a2378',   # Kontur (farbig, nicht schwarz)
    'old': '#33155a',  # Kontur unten
    's0': '#f3e2ff',   # Glanz
    's1': '#cfa2f5',   # Licht
    's2': '#ad78e6',   # Grundton
    's3': '#8d58cf',   # Schatten
    's4': '#7240b4',   # tiefer Schatten
    'sr': '#c493f2',   # Lichtsaum unten (Gelee ist durchscheinend)
    'bu': '#e2c4ff',   # Blaeschen
    # Gold
    'go': '#6e3b12',
    'g0': '#fff3a6',
    'g1': '#ffd23f',
    'g2': '#efa52a',
    'g3': '#c47619',
    # Edelstein
    'r0': '#ffb0b0',
    'r1': '#e8323c',
    'r2': '#9e1830',
    # Hermelin
    'e0': '#ffffff',
    'e1': '#ece2f2',
    'ek': '#2b1838',
    # Weintraube am Zepter
    'v0': '#d9c9ff',
    'v1': '#7d62e8',
    'v2': '#5640c0',
    'vo': '#2a1a6a',
    'lf': '#6cc04a',
    'lfd': '#3c7f2a',
    # Gesicht
    'E': '#2a1440',
    'W': '#ffffff',
    'bl': '#ff8fc0',
    'mo': '#5a1f52',
    'mt': '#ff7aa8',
}.items()}

# Kulleraugen nach Mochi-Rezept: fast ganz Iris, Doppelglanz oben links,
# violetter Schimmer unten - kein grosses Weiss (siehe charakter-essen-und-cute)
EYE = [
    ".EEE.",
    "EWWdE",
    "EddmE",
    "EdmlE",
    ".EEE.",
]
EYE_KEY = {'E': 'E', 'W': 'W', 'd': '#341a58', 'm': '#7a4fd0', 'l': '#dcbcff'}
EYE_SHUT = [
    ".....",
    ".....",
    "E...E",
    ".EEE.",
    ".....",
]
# zusammengekniffen (Landung): ><
EYE_SQUINT_L = [
    ".....",
    "EE...",
    "..EEE",
    "EE...",
    ".....",
]

# Krone, 13 breit, sitzt mit den unteren zwei Zeilen auf dem Kopf.
# P Perle, o Kontur, Y Gold, G Glanz, y/z Goldschatten, R/r/D roter Stein,
# B blaue Steinchen im Reif
CROWN = [
    "......P......",
    ".P...oGo...P.",
    "oGo.oGYyo.oGo",
    "oGYooYrRyooYo",
    "oGYYYRRRDYYyo",
    "oGYYYYRDYYYyo",
    "ozyBzyyyzBzzo",
    ".ooooooooooo.",
]
CROWN_KEY = {'o': 'go', 'Y': 'g1', 'G': 'g0', 'y': 'g2', 'z': 'g3', 'P': 'e0',
             'R': 'r1', 'r': 'r0', 'D': 'r2', 'B': '#5cc8ff'}
# von hinten: kein Stein, dafuer Goldnaht
CROWN_BACK = [r.replace('R', 'Y').replace('r', 'G').replace('D', 'y') for r in CROWN]
CROWN_BACK[4] = "oGYYYyGyyYYyo"
CROWN_BACK[5] = "oGYYYYyYYYYyo"
CROWN_BACK[6] = "ozyyzyyyzyyzo"

# Funkeln auf dem Stein / den Perlen (Idle)
SPARK = ["..W..", ".WWW.", "..W.."]

# Weintraube oben am Zepter, darunter Goldkragen
ORB = [
    "...lL..",
    "..oLl..",
    ".oVVVo.",
    "oVwVVVo",
    "oVVVVvo",
    "oVVVvvo",
    ".ovvvo.",
    ".oGYyo.",
    "..ozo..",
]
ORB_KEY = {'l': 'lf', 'L': 'lfd', 'o': 'vo', 'V': 'v1', 'v': 'v2', 'w': 'v0',
           'G': 'g0', 'Y': 'g1', 'y': 'g2', 'z': 'g3'}


# Posen: rx, ry, lift, Kronenversatz (+ = tiefer), Zepter-Neigung (px oben),
# Haende hoch (px), Augen ('o' offen, '-' zu, 'x' gekniffen), Blaeschen-Phase,
# Spritzer-Phase (0 keiner, 1 frisch, 2 weiter weg), Funkeln (None/0/1)
def pose(rx, ry, lift=0, crown=0, tilt=0, hands=0, eyes='o', bub=0, drop=0, spark=None):
    return dict(rx=rx, ry=ry, lift=lift, crown=crown, tilt=tilt, hands=hands,
                eyes=eyes, bub=bub, drop=drop, spark=spark)


# Idle: Wabbeln in 4 Phasen, zweimal - beim zweiten Mal blinzelt er und der
# Stein funkelt. Die Krone sitzt eine Phase hinterher.
IDLE_FRONT = [
    pose(9.5, 8.5, crown=0, bub=0),
    pose(10.1, 8.0, crown=0, tilt=0, bub=1),
    pose(9.7, 8.3, crown=1, tilt=1, bub=2),
    pose(9.1, 8.9, crown=0, tilt=0, bub=3),
    pose(9.5, 8.5, crown=0, bub=0, spark=0),
    pose(10.1, 8.0, crown=0, eyes='-', bub=1, spark=1),
    pose(9.7, 8.3, crown=1, tilt=1, bub=2),
    pose(9.1, 8.9, crown=0, tilt=0, bub=3),
]
# Clip-Reihenfolge: 3x normal, 1x mit Blinzeln = 2 s bei 8 fps
IDLE_FRONT_SEQ = [0, 1, 2, 3, 0, 1, 2, 3, 0, 1, 2, 3, 4, 5, 6, 7]

# Hopser: Landung, Erholen, Ausholen, Absprung, Scheitel, Fallen.
# Die Krone haengt nach: beim Absprung tief eingedrueckt, oben schwebt sie
# ueber dem Kopf, bei der Landung knallt sie runter.
WALK = [
    pose(11.0, 6.8, 0, crown=1, tilt=-1, hands=0, eyes='x', drop=1),
    pose(10.0, 8.0, 0, crown=0, tilt=0, hands=0, drop=2),
    pose(10.4, 7.4, 0, crown=1, tilt=1, hands=-1),
    pose(8.6, 9.6, 1, crown=1, tilt=1, hands=1),
    pose(9.1, 9.0, 3, crown=-1, tilt=0, hands=2, bub=1),
    pose(9.3, 8.8, 2, crown=-2, tilt=-1, hands=1, bub=2),
]

IDLE_BACK = [
    pose(9.5, 8.5, crown=0, bub=0),
    pose(10.1, 8.0, crown=0, bub=1),
    pose(9.7, 8.3, crown=1, tilt=1, bub=2),
    pose(9.1, 8.9, crown=0, bub=3),
]

FPS_IDLE = 8
FPS_WALK = 12


# ---------------------------------------------------------------- Werkzeuge

def put(img, x, y, c):
    if 0 <= x < F and 0 <= y < F:
        img.load()[x, y] = C[c] if isinstance(c, str) and not c.startswith('#') else (hx(c) if isinstance(c, str) else c)


def stamp(img, rows, x0, y0, key, flip=False):
    for dy, row in enumerate(rows):
        if flip:
            row = row[::-1]
        for dx, ch in enumerate(row):
            if ch != '.':
                put(img, x0 + dx, y0 + dy, key.get(ch, ch))


N4 = ((1, 0), (-1, 0), (0, 1), (0, -1))


def edge(m, x, y):
    return any((x + a, y + b) not in m for a, b in N4)


def disc(m, px_, py_, r):
    for y in range(int(py_ - r - 1), int(py_ + r + 2)):
        for x in range(int(px_ - r - 1), int(px_ + r + 2)):
            if (x + 0.5 - px_) ** 2 + (y + 0.5 - py_) ** 2 <= r * r:
                m.add((x, y))


def tidy(m):
    """Einzelne Ausreisser-Pixel weg, sonst steht ein Konturpunkt allein da."""
    return {(x, y) for (x, y) in m if sum((x + a, y + b) in m for a, b in N4) >= 2}


# ---------------------------------------------------------------- Teile

def body_mask(rx, ry, bottom, hands_y, side):
    """Geleeklecks: oben rund, unten flach und am Boden etwas breitgelaufen,
    dazu zwei Patschehaendchen seitlich. side = Seite der Zepterhand."""
    cy = bottom + 1 - ry
    m = set()
    for y in range(F):
        for x in range(F):
            dx = (x + 0.5 - CX) / rx
            dy = (y + 0.5 - cy) / ry
            n = 2.0 if dy < 0 else 3.6
            if abs(dx) ** n + abs(dy) ** n <= 1.0:
                m.add((x, y))
    # Fuss: unterste zwei Zeilen laufen je einen Pixel breiter
    for y in (bottom, bottom - 1):
        xs = [x for (x, yy) in m if yy == y]
        if xs:
            m.add((min(xs) - 1, y))
            m.add((max(xs) + 1, y))
    hand = {}
    for s in (-1, 1):
        hxp = CX + s * (rx + 0.3)
        hyp = hands_y - (0 if s == side else 1)
        hm = set()
        disc(hm, hxp, hyp, 1.8)
        hand[s] = (hxp, hyp)
        m |= hm
    return tidy(m), cy, hand


def body(img, p, bottom, front, side):
    rx, ry = p['rx'], p['ry']
    hands_y = bottom + 1 - ry * 0.65 - p['hands']
    m, cy, hand = body_mask(rx, ry, bottom, hands_y, side)
    top = min(y for (_, y) in m)
    rows = {}
    for (x, y) in m:
        rows.setdefault(x, []).append(y)
    low = {x: max(ys) for x, ys in rows.items()}

    def shade(x, y):
        dx = (x + 0.5 - CX) / rx
        dy = (y + 0.5 - cy) / ry
        if y == low[x] - 1 and abs(dx) < 0.8:
            return 'sr'                       # Licht scheint unten durchs Gelee
        light = -0.5 * dx - 0.85 * dy
        if light > 0.62:
            return 's1'
        if dx > 0.86 or (dy > 0.45 and dx > 0.66):
            return 's4'
        if dx > 0.6 or dy > 0.55 or (dx > 0.4 and dy > 0.2):
            return 's3'
        return 's2'

    for (x, y) in m:
        if edge(m, x, y):
            dy = (y + 0.5 - cy) / ry
            put(img, x, y, 'old' if dy > 0.3 else 'ol')
        else:
            put(img, x, y, shade(x, y))

    # Glanz oben links: ein Streifen und ein Punkt, wie nasses Gelee
    gx = int(round(CX - rx * 0.55))
    gy = top + 2
    for (x, y, c) in ((gx, gy, 's0'), (gx + 1, gy, 's0'), (gx + 2, gy - 1 if front else gy, 's0'),
                      (gx - 1, gy + 1, 's0'), (gx - 1, gy + 2, 's1')):
        if (x, y) in m and not edge(m, x, y):
            put(img, x, y, c)
    # kleiner Zweitglanz rechts
    x2 = int(round(CX + rx * 0.62))
    if (x2, top + 3) in m and not edge(m, x2, top + 3):
        put(img, x2, top + 3, 's1')

    # Blaeschen steigen im Gelee auf (links hinten, rechts vorn)
    b = p['bub']
    for (bx, by, ph) in ((CX - rx * 0.45, bottom - 2, 0), (CX + rx * 0.35, bottom - 1, 2)):
        k = (b + ph) % 4
        x, y = int(round(bx + (k % 2) * 0.5)), int(round(by - k * 1.5))
        if (x, y) in m and not edge(m, x, y) and (x, y + 1) in m:
            put(img, x, y, 'bu' if k < 3 else 's1')
    return m, cy, top, hand


def face(img, cy, ry, eyes):
    ey = int(round(cy - ry * 0.3))
    xl, xr = int(CX) - 2 - 5, int(CX) + 2
    if eyes == 'o':
        stamp(img, EYE, xl, ey, EYE_KEY)
        stamp(img, EYE, xr, ey, EYE_KEY)
        put(img, xl - 1, ey + 1, 'E')            # Wimper aussen
        put(img, xr + 5, ey + 1, 'E')
    elif eyes == '-':
        stamp(img, EYE_SHUT, xl, ey, EYE_KEY)
        stamp(img, EYE_SHUT, xr, ey, EYE_KEY)
    else:
        stamp(img, EYE_SQUINT_L, xl, ey, EYE_KEY)
        stamp(img, EYE_SQUINT_L, xr, ey, EYE_KEY, flip=True)
    n = int(CX) - 1
    # Mund: kleines Laecheln, offen mit Zunge
    if eyes == 'x':
        for x in (n, n + 1):
            put(img, x, ey + 3, 'mo')
            put(img, x, ey + 4, 'mo')
    else:
        put(img, n - 1, ey + 3, 'mo')
        put(img, n + 2, ey + 3, 'mo')
        put(img, n, ey + 4, 'mo')
        put(img, n + 1, ey + 4, 'mt')
    # Baeckchen
    for x in (xl - 1, xl, xl + 1):
        put(img, x, ey + 5, 'bl')
    for x in (xr + 3, xr + 4, xr + 5):
        put(img, x, ey + 5, 'bl')


def crown(img, top, off, front, spark):
    rows = CROWN if front else CROWN_BACK
    x0 = int(CX) - 6
    y0 = top - 6 + off
    stamp(img, rows, x0, y0, CROWN_KEY)
    if spark is not None and front:
        sx, sy = (x0 + 6, y0 + 4) if spark == 0 else (x0 + 1, y0 + 1)
        for dy, row in enumerate(SPARK):
            for dx, ch in enumerate(row):
                if ch == 'W' and (dx == 2 and dy == 1 or spark == 1):
                    put(img, sx - 2 + dx, sy - 1 + dy, 'e0')


def line_px(x0, y0, x1, y1):
    n = int(max(abs(x1 - x0), abs(y1 - y0))) + 1
    return [(int(round(x0 + (x1 - x0) * t / max(1, n - 1))), int(round(y0 + (y1 - y0) * t / max(1, n - 1))))
            for t in range(n)]


def scepter(img, hand, tilt, side):
    """Goldstab durch die Hand, Weintraube oben. Wird VOR dem Koerper gemalt -
    die Hand liegt dann ueber dem Stab und haelt ihn."""
    hxp, hyp = hand
    bx, by = int(round(hxp - 0.5)), int(round(hyp)) + 4
    tx, ty = bx + tilt * side, int(round(hyp)) - 6
    shaft = line_px(bx, by, tx, ty)
    for (x, y) in shaft:
        for a, b in N4:
            if (x + a, y + b) not in shaft:
                put(img, x + a, y + b, 'go')
    for i, (x, y) in enumerate(shaft):
        put(img, x, y, 'g3' if i < 2 else ('g1' if (y % 3) else 'g2'))
    put(img, bx, by + 1, 'go')
    stamp(img, ORB, tx - 3, ty - 8, ORB_KEY, flip=side < 0)


def drops(img, bottom, rx, phase):
    """Gelee-Spritzer bei der Landung, fliegen seitlich weg."""
    if not phase:
        return
    for s in (-1, 1):
        d = rx + (2 if phase == 1 else 4)
        x = int(round(CX + s * d)) - (1 if s < 0 else 0)
        y = bottom - (1 if phase == 1 else 3)
        put(img, x, y, 's1')
        put(img, x, y + 1, 'ol')
        if phase == 1:
            put(img, x + s, y + 1, 's3')


# ---------------------------------------------------------------- Bild

def frame(p, front):
    img = Image.new("RGBA", (F, F), (0, 0, 0, 0))
    side = 1 if front else -1       # Zepterhand: von vorn rechts im Bild, von hinten links
    bottom = BOTTOM - p['lift']
    rx, ry = p['rx'], p['ry']
    hands_y = bottom + 1 - ry * 0.65 - p['hands']
    _, _, hand = body_mask(rx, ry, bottom, hands_y, side)
    scepter(img, hand[side], p['tilt'], side)
    m, cy, top, _ = body(img, p, bottom, front, side)
    if front:
        face(img, cy, ry, p['eyes'])
    crown(img, top, p['crown'], front, p['spark'])
    drops(img, BOTTOM, rx, p['drop'])
    return img


def frames():
    return [
        [frame(p, True) for p in IDLE_FRONT],
        [frame(p, True) for p in WALK],
        [frame(p, False) for p in WALK],
        [frame(p, False) for p in IDLE_BACK],
    ]


# ---------------------------------------------------------------- Unity

def sprite_name(r, c):
    return "%s_%d" % (BASE, r * COLS + c)


def write_meta(path, used):
    """Grid-.meta im Stil der anderen Charaktere (Pivot unten mittig, PPU 32)."""
    from unity_meta import META_HEAD
    rng = random.Random()
    names, ids, rects = [], [], []
    for r, c in used:
        names.append(sprite_name(r, c))
        ids.append(rng.randint(1 << 60, 1 << 62))
        rects.append((c * F, (ROWS - 1 - r) * F))
    idtable = "".join("  - first:\n      213: %d\n    second: %s\n" % (i, n) for n, i in zip(names, ids))
    sprites = ""
    for n, i, (x, y) in zip(names, ids, rects):
        sprites += (
            "    - serializedVersion: 2\n      name: %s\n      rect:\n        serializedVersion: 2\n"
            "        x: %d\n        y: %d\n        width: %d\n        height: %d\n"
            "      alignment: 7\n      pivot: {x: 0.5, y: 0}\n      border: {x: 0, y: 0, z: 0, w: 0}\n"
            "      customData:\n      outline: []\n      physicsShape: []\n      tessellationDetail: -1\n"
            "      bones: []\n      spriteID: %s\n      internalID: %d\n      vertices: []\n"
            "      indices:\n      edges: []\n      weights: []\n" % (n, x, y, F, F, uuid.uuid4().hex, i))
    nametable = "".join("      %s: %d\n" % (n, i) for n, i in sorted(zip(names, ids)))
    with open(path, "w", newline="\n") as fh:
        fh.write(META_HEAD.format(guid=uuid.uuid4().hex, ppu=32, idtable=idtable,
                                  sprites=sprites, nametable=nametable))


def read_meta(path):
    text = open(path, encoding="utf-8").read()
    guid = re.search(r"^guid: (\w+)", text, re.M).group(1)
    table = dict((n, int(i)) for n, i in re.findall(r"^      (%s_\d+): (-?\d+)$" % BASE, text, re.M))
    return guid, table


KEY = """      - serializedVersion: 3
        time: {t}
        value: {v}
        inSlope: Infinity
        outSlope: Infinity
        tangentMode: 103
        weightedMode: 0
        inWeight: 0
        outWeight: 0
"""

FLIP_CURVE = """  - serializedVersion: 2
    curve:
      serializedVersion: 2
      m_Curve:
{keys}      m_PreInfinity: 2
      m_PostInfinity: 2
      m_RotationOrder: 4
    attribute: m_FlipX
    path:
    classID: 212
    script: {{fileID: 0}}
    flags: 0
"""

CLIP = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!74 &7400000
AnimationClip:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  serializedVersion: 7
  m_Legacy: 0
  m_Compressed: 0
  m_UseHighQualityCurve: 1
  m_RotationCurves: []
  m_CompressedRotationCurves: []
  m_EulerCurves: []
  m_PositionCurves: []
  m_ScaleCurves: []
  m_FloatCurves:
{flip}  m_PPtrCurves:
  - serializedVersion: 2
    curve:
{pptr}    attribute: m_Sprite
    path:
    classID: 212
    script: {{fileID: 0}}
    flags: 2
  m_SampleRate: {rate}
  m_WrapMode: 0
  m_Bounds:
    m_Center: {{x: 0, y: 0, z: 0}}
    m_Extent: {{x: 0, y: 0, z: 0}}
  m_ClipBindingConstant:
    genericBindings:
    - serializedVersion: 2
      path: 0
      attribute: 555744692
      script: {{fileID: 0}}
      typeID: 212
      customType: 0
      isPPtrCurve: 0
      isIntCurve: 0
      isSerializeReferenceCurve: 0
    - serializedVersion: 2
      path: 0
      attribute: 0
      script: {{fileID: 0}}
      typeID: 212
      customType: 23
      isPPtrCurve: 1
      isIntCurve: 0
      isSerializeReferenceCurve: 0
    pptrCurveMapping:
{mapping}  m_AnimationClipSettings:
    serializedVersion: 2
    m_AdditiveReferencePoseClip: {{fileID: 0}}
    m_AdditiveReferencePoseTime: 0
    m_StartTime: 0
    m_StopTime: {stop}
    m_OrientationOffsetY: 0
    m_Level: 0
    m_CycleOffset: 0
    m_HasAdditiveReferencePose: 0
    m_LoopTime: 1
    m_LoopBlend: 0
    m_LoopBlendOrientation: 0
    m_LoopBlendPositionY: 0
    m_LoopBlendPositionXZ: 0
    m_KeepOriginalOrientation: 0
    m_KeepOriginalPositionY: 1
    m_KeepOriginalPositionXZ: 0
    m_HeightFromFeet: 0
    m_Mirror: 0
  m_EditorCurves:
{flip}  m_EulerEditorCurves: []
  m_HasGenericRootTransform: 0
  m_HasMotionFloatCurves: 0
  m_Events: []
"""


def fmt_t(t):
    s = ("%.8f" % t).rstrip('0')
    return s + '0' if s.endswith('.') else s


def clip_text(name, guid, ids, rate, flip):
    n = len(ids)
    times = [fmt_t(i / rate) for i in range(n + 1)]
    flip_keys = "".join(KEY.format(t=t, v=1 if flip else 0) for t in times)
    flip_block = FLIP_CURVE.format(keys=flip_keys)
    ref = "{fileID: %d, guid: %s, type: 3}"
    pptr = "".join("    - time: %s\n      value: %s\n" % (times[i], ref % (sid, guid)) for i, sid in enumerate(ids))
    uniq = list(dict.fromkeys(ids))
    mapping = "".join("    - %s\n" % (ref % (sid, guid)) for sid in uniq)
    return CLIP.format(name=name, flip=flip_block, pptr=pptr, rate=rate, mapping=mapping, stop=times[-1])


# Clipname im Fuchs-Controller -> (Zeile, Bildfolge, fps, FlipX)
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

NATIVE_META = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: {main}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def write_unity(rows):
    meta = OUT + ".meta"
    used = [(r, c) for r, row in enumerate(rows) for c in range(len(row))]
    if not os.path.exists(meta):
        write_meta(meta, used)
        print("neu:", os.path.relpath(meta, ROOT))
    guid, table = read_meta(meta)

    os.makedirs(ANIM_DIR, exist_ok=True)
    folder_meta = ANIM_DIR + ".meta"
    if not os.path.exists(folder_meta):
        with open(folder_meta, "w", newline="\n") as fh:
            fh.write("fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n"
                     "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
                     % uuid.uuid4().hex)

    clip_guid = {}
    for short, (r, seq, rate, flip) in CLIPS.items():
        name = "SlimeKing_" + short
        path = os.path.join(ANIM_DIR, name + ".anim")
        ids = [table[sprite_name(r, c)] for c in seq]
        with open(path, "w", newline="\n") as fh:
            fh.write(clip_text(name, guid, ids, rate, flip))
        if not os.path.exists(path + ".meta"):
            with open(path + ".meta", "w", newline="\n") as fh:
                fh.write(NATIVE_META.format(guid=uuid.uuid4().hex, main=7400000))
        clip_guid[short] = re.search(r"^guid: (\w+)", open(path + ".meta").read(), re.M).group(1)

    ctrl = os.path.join(ANIM_DIR, "SlimeKing_Char.controller")
    if not os.path.exists(ctrl):
        text = open(TEMPLATE_CTRL, encoding="utf-8").read()
        for short in CLIPS:
            src = os.path.join(os.path.dirname(TEMPLATE_CTRL), "Fuchs_%s.anim.meta" % short)
            old = re.search(r"^guid: (\w+)", open(src).read(), re.M).group(1)
            text = text.replace(old, clip_guid[short])
        text = text.replace("m_Name: Fuchs_Char", "m_Name: SlimeKing_Char")
        with open(ctrl, "w", newline="\n") as fh:
            fh.write(text)
        with open(ctrl + ".meta", "w", newline="\n") as fh:
            fh.write(NATIVE_META.format(guid=uuid.uuid4().hex, main=9100000))
        print("neu:", os.path.relpath(ctrl, ROOT))
    print("Controller-guid:", re.search(r"^guid: (\w+)", open(ctrl + ".meta").read(), re.M).group(1))


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
        # GIF: Idle vorn (Clipfolge), Laufen vorn, Laufen hinten, Idle hinten - in Echtzeit
        gif = []
        ms = 1000 // 24
        for i in range(48):
            t = i / 24
            f = Image.new("RGBA", (4 * F + 18, F + 6), (90, 110, 90, 255))
            f.alpha_composite(rows[0][IDLE_FRONT_SEQ[int(t * FPS_IDLE) % len(IDLE_FRONT_SEQ)]], (3, 3))
            f.alpha_composite(rows[1][int(t * FPS_WALK) % len(WALK)], (F + 7, 3))
            f.alpha_composite(rows[2][int(t * FPS_WALK) % len(WALK)], (2 * F + 11, 3))
            f.alpha_composite(rows[3][int(t * FPS_IDLE) % len(IDLE_BACK)], (3 * F + 15, 3))
            gif.append(f.resize((f.width * s, f.height * s), Image.NEAREST).convert("P"))
        gif[0].save(os.path.splitext(path)[0] + ".gif", save_all=True,
                    append_images=gif[1:], duration=ms, loop=0)


if __name__ == "__main__":
    main()
