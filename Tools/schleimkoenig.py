"""
Schleimkoenig (Zwischenboss): aus der Vorlage neu gezeichnet und animiert.

  Tools/schleimkoenig_quelle.png   Nicks Vorlage (128x128), nur Krone + Pelz
                                   werden daraus genommen und bereinigt
  Assets/Art/Gegner/new/miniboss/
    schleimkoenig_hop.png          14 Bilder  Hopser (Schleife), Luft = HOP_AIR
    schleimkoenig_hop_blink.png    14 Bilder  derselbe Hopser, blinzelt am Anfang
    schleimkoenig_ducken.png       12 Bilder  holt Schwung fuer den Riesensprung
    schleimkoenig_absprung.png      4 Bilder  schnellt hoch (danach fliegt der Code weiter)
    schleimkoenig_fall.png          2 Bilder  faellt (Schleife)
    schleimkoenig_landung.png      12 Bilder  Aufprall, Wabbeln, wieder gut gelaunt
    schleimkoenig_platsch.png      10 Bilder  Schleimspritzer + Staubring beim Aufprall
    schleimkoenig_schatten.png      8 Bilder  Schatten, gross -> klein (nach Hoehe)
  alle PPU 32 (wie die Welt), 12 fps. Pivot = Mitte der Bodenlinie.

Die Bilder enthalten NUR die Verformung. Wie hoch er gerade ueber dem Boden
schwebt, gibt der Code als ganze Pixel dazu (Tabellen am Ende der Ausgabe,
dieselben Zahlen stehen in EnemySchleimkoenig.cs) - so kann der Schatten am
Boden bleiben und der Riesensprung nahtlos aus dem Bild weiterfliegen.

Aufbau eines Bildes:
  Koerper   per Formel (Superellipse, unten Schleimsaum), 4x4 abgetastet,
            Licht von links oben, Glanz, Blasen, 1-px-Umriss
  Gesicht   Stempel (Augen/Mund/Wangen) - je Bild ein Ausdruck
  Krone     starr aus der Vorlage, laeuft mit Feder-Verzug hinterher
  Zepter    per Formel in jedem Winkel sauber gerastert, in einer Schleimhand

Aufruf aus dem Projektordner:
  python Tools/schleimkoenig.py [--quelle vorlage.png] [--preview pfad.gif]
"""

import math
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TOOLS = os.path.join(ROOT, "Tools")
QUELLE = os.path.join(TOOLS, "schleimkoenig_quelle.png")
OUT_DIR = os.path.join(ROOT, "Assets", "Art", "Gegner", "new", "miniboss")
BESTIARY = os.path.join(ROOT, "Assets", "Resources", "Bestiary", "Schleimkoenig.png")

CELL_W, CELL_H = 192, 176
GROUND = CELL_H - 2          # erste Zeile UNTER dem Koerper
CX = 104                     # Koerpermitte (links Platz fuers Zepter)
PPU = 32
FPS = 12
PIVOT = (CX / CELL_W, 2 / CELL_H)

SS = 4                       # Abtastung je Pixel und Achse

# Ruhemass
W0, H0 = 42.0, 56.0

# ------------------------------------------------------------------ Farben

def rgb(h):
    h = h.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), 255)

BODY = [rgb("#34084f"),   # 0 Umriss
        rgb("#5f1ea6"),   # 1 tiefster Schatten
        rgb("#8338dc"),   # 2 Schatten
        rgb("#a25cf4"),   # 3 Mitte
        rgb("#c080fb"),   # 4 Grundton (wie Vorlage)
        rgb("#d6a8fd"),   # 5 Licht
        rgb("#f4e6ff")]   # 6 Glanz
BOUNCE = rgb("#b06cf8")   # Rueckstrahlung am Boden (Gelee wirkt durchscheinend)

NAVY = rgb("#0e0a2e")
NAVY2 = rgb("#2a2060")
CREAM = rgb("#fcf4c8")
WHITE = rgb("#ffffff")
PINK = rgb("#f070e0")
PINK2 = rgb("#fbb0f2")
MOUTH = rgb("#5a0a3a")
TONGUE = rgb("#ff7aa8")

GOLD = [rgb("#622a06"), rgb("#c07714"), rgb("#e3a02b"), rgb("#f8c140"),
        rgb("#fcd455"), rgb("#fdf197")]
GOLD_OUT = rgb("#3a1424")
GEM = [rgb("#3c0a60"), rgb("#6e1ea8"), rgb("#9a3ee6"), rgb("#c47cfc"), rgb("#f4e0ff")]

SHADOW = (34, 8, 56, 150)

# ------------------------------------------------------------------ Vorlage: Krone + Pelz

# Krone/Pelz in der Vorlage: alles bis Zeile 79, ohne das Zepter links unten.
HAT_BOTTOM = 79      # unterste Pelzzeile
HAT_CENTER_X = 65    # Mitte des Pelzes

# Farbklassen der Vorlage -> saubere Palette (per k-means ermittelt, dann von
# Hand sortiert). Jedes Vorlagenpixel nimmt die naechste Farbe dieser Liste.
HAT_PALETTE = [
    rgb("#2b0632"), rgb("#381349"),                       # Umrisse
    rgb("#620a7e"), rgb("#52128a"), rgb("#8415b5"),       # Samt
    rgb("#9b40ed"), rgb("#d261f9"), rgb("#f6dcff"),       # Edelsteine
    rgb("#622a06"), rgb("#c07714"), rgb("#e3a02b"),       # Gold
    rgb("#f8c140"), rgb("#fcd455"), rgb("#fdf197"),
    rgb("#c5bbc9"), rgb("#d8cfdb"), rgb("#fcfafb"),       # Pelz
]


def lade_hut(path):
    im = np.array(Image.open(path).convert("RGBA")).astype(int)
    h, w = im.shape[:2]
    pal = np.array([p[:3] for p in HAT_PALETTE])
    out = np.zeros((h, w, 4), np.uint8)
    for y in range(h):
        for x in range(w):
            if im[y, x, 3] < 128 or y > HAT_BOTTOM:
                continue
            if x < 28 or (x < 34 and y >= 61):      # Zepter
                continue
            c = im[y, x, :3]
            k = int(((pal - c) ** 2).sum(1).argmin())
            # Unter dem Pelz schaut der Koerper durch - der kommt neu.
            if y >= 76 and k in (2, 3, 4, 5, 6):
                continue
            out[y, x] = HAT_PALETTE[k]
    out = saeubern(out)
    out = pelz_saum(out)
    ys, xs = np.nonzero(out[:, :, 3])
    x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
    hut = out[y0:y1, x0:x1]
    anker = (HAT_CENTER_X - x0, HAT_BOTTOM - y0)   # Pelzmitte unten im Ausschnitt
    return hut, anker


def saeubern(img):
    """Einzelpixel, die anders sind als alle vier Nachbarn, nehmen die Mehrheit an."""
    h, w = img.shape[:2]
    res = img.copy()
    for _ in range(2):
        src = res.copy()
        for y in range(1, h - 1):
            for x in range(1, w - 1):
                if src[y, x, 3] == 0:
                    continue
                nb = [tuple(src[y + dy, x + dx]) for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0))]
                me = tuple(src[y, x])
                if me in nb:
                    continue
                best = max(set(nb), key=nb.count)
                if nb.count(best) >= 3 and best[3] > 0:
                    res[y, x] = best
        # Einzelne lose Pixel ausserhalb weg
        for y in range(1, h - 1):
            for x in range(1, w - 1):
                if res[y, x, 3] and sum(res[y + dy, x + dx, 3] > 0 for dy, dx in
                                        ((0, 1), (0, -1), (1, 0), (-1, 0))) <= 1:
                    res[y, x] = 0
    return res


def pelz_saum(img):
    """Unter den Pelz eine weiche Schattenkante, damit er AUF dem Schleim sitzt."""
    h, w = img.shape[:2]
    res = img.copy()
    fur = {HAT_PALETTE[14], HAT_PALETTE[15], HAT_PALETTE[16]}
    for x in range(w):
        col = [y for y in range(h) if img[y, x, 3]]
        if not col:
            continue
        yb = max(col)
        if tuple(img[yb, x]) in fur:
            res[yb, x] = HAT_PALETTE[14] if tuple(img[yb, x]) == HAT_PALETTE[16] else HAT_PALETTE[1]
    return res


# ------------------------------------------------------------------ Stempel

def stempel(rows, farben):
    h, w = len(rows), len(rows[0])
    a = np.zeros((h, w, 4), np.uint8)
    for y, r in enumerate(rows):
        assert len(r) == w, rows
        for x, ch in enumerate(r):
            if ch != ".":
                a[y, x] = farben[ch]
    return a


FAR = {"n": NAVY, "d": NAVY2, "c": CREAM, "w": WHITE, "p": PINK, "q": PINK2,
       "m": MOUTH, "t": TONGUE, "o": BODY[0]}

EYE_OPEN = stempel([
    "..nnnnnnnn..",
    ".nnnnnnnnnn.",
    "nnccccnnnnnn",
    "nccccccnnnnn",
    "nccccccnnnnn",
    "nccccccnnnnn",
    "nnccccnnnnnn",
    "nnnnnnnnnnnn",
    "nnnnnnnnnnnn",
    "nnnnnnnnnwwn",
    "ndnnnnnnnwwn",
    ".nddnnnnnnn.",
    "..nddddddn..",
], FAR)

EYE_WIDE = stempel([
    "...nnnnnn...",
    ".nnnnnnnnnn.",
    ".nccccnnnnn.",
    "ncccccnnnnnn",
    "nccccccnnnnn",
    "nccccccnnnnn",
    "ncccccnnnnnn",
    "nnccccnnnnnn",
    "nnnnnnnnnnnn",
    "nnnnnnnnwwnn",
    "nnnnnnnnwwnn",
    "ndnnnnnnnnnn",
    ".nddnnnnnnn.",
    "..nddddddn..",
], FAR)

EYE_HALF = stempel([
    "nnnnnnnnnnnn",
    "nnnnnnnnnnnn",
    "nccccnnnnnnn",
    "nnccnnnnnwwn",
    ".nddnnnnnnn.",
    "..nddddddn..",
], FAR)

EYE_CLOSED = stempel([      # zufrieden zu (Blinzeln)
    "nn........nn",
    ".nnn....nnn.",
    "..nnnnnnnn..",
], FAR)

EYE_HAPPY = stempel([       # ^ ^
    "...nnnnnn...",
    ".nnnnnnnnnn.",
    "nnn......nnn",
    "nn........nn",
], FAR)

EYE_SQUEEZE_L = stempel([   # >
    "nn.......",
    "nnnn.....",
    "..nnnn...",
    "....nnnnn",
    "..nnnn...",
    "nnnn.....",
    "nn.......",
], FAR)
EYE_SQUEEZE_R = EYE_SQUEEZE_L[:, ::-1]


def eye_determined(left):
    """Offenes Auge, oben innen schraeg angeschnitten + Braue: entschlossen."""
    e = EYE_OPEN.copy()
    h, w = e.shape[:2]
    for y in range(h):
        for x in range(w):
            xi = (w - 1 - x) if left else x      # Abstand zur Innenseite
            if y < 4.2 - xi * 0.55:
                e[y, x] = 0
    brow = np.zeros((h + 3, w, 4), np.uint8)
    brow[3:] = e
    for x in range(w):
        xi = (w - 1 - x) if left else x
        y = int(round(4.2 - xi * 0.55)) + 3 - 3
        if 0 <= y < h + 3 and xi < 9:
            brow[max(0, y), x] = NAVY
            if y + 1 < h + 3 and brow[y + 1, x, 3] == 0:
                brow[y + 1, x] = NAVY
    return brow


EYE_DET_L = eye_determined(True)
EYE_DET_R = eye_determined(False)

MOUTH_SMILE = stempel([
    "n........n",
    "nn......nn",
    ".nnnnnnnn.",
], FAR)

MOUTH_SMALL = stempel([
    "n....n",
    ".nnnn.",
], FAR)

MOUTH_OPEN = stempel([       # D
    "nnnnnnnnnn",
    "nmmmmmmmmn",
    "nmmmmmmmmn",
    ".nmttttmn.",
    "..nnnnnn..",
], FAR)

MOUTH_O = stempel([
    ".nnnn.",
    "nmmmmn",
    "nmttmn",
    ".nnnn.",
], FAR)

MOUTH_POUT = stempel([       # entschlossen: ~
    ".nn...nn",
    "n..nnn..",
], FAR)

BLUSH = stempel([
    ".qqqq.",
    "pppppp",
    ".pppp.",
], FAR)

SPARKLE = [
    stempel(["w"], FAR),
    stempel([".c.", "cwc", ".c."], FAR),
    stempel(["..c..", "..w..", "cwwwc", "..w..", "..c.."], FAR),
    stempel(["...c...", "...w...", "...w...", "cwwwwwc", "...w...", "...w...", "...c..."], FAR),
]

EYES = {
    "open": (EYE_OPEN, EYE_OPEN),
    "wide": (EYE_WIDE, EYE_WIDE),
    "half": (EYE_HALF, EYE_HALF),
    "closed": (EYE_CLOSED, EYE_CLOSED),
    "happy": (EYE_HAPPY, EYE_HAPPY),
    "squeeze": (EYE_SQUEEZE_L, EYE_SQUEEZE_R),
    "det": (EYE_DET_L, EYE_DET_R),
}
# Wo die Augenmitte im Stempel sitzt (x, y) - damit alle Ausdruecke gleich stehen.
EYE_ANCHOR = {"open": (6, 6.5), "wide": (6, 7), "half": (6, 3.5), "closed": (6, 4),
              "happy": (6, 4), "squeeze": (4.5, 3.5), "det": (6, 9.5)}

MOUTHS = {"smile": MOUTH_SMILE, "small": MOUTH_SMALL, "open": MOUTH_OPEN,
          "o": MOUTH_O, "pout": MOUTH_POUT}


def paste(img, st, x, y, only_on=None):
    """Stempel mit linker oberer Ecke auf (x, y). only_on: Maske, ausserhalb nichts."""
    h, w = st.shape[:2]
    for j in range(h):
        for i in range(w):
            if st[j, i, 3] == 0:
                continue
            X, Y = x + i, y + j
            if 0 <= X < img.shape[1] and 0 <= Y < img.shape[0]:
                if only_on is not None and not only_on[Y, X]:
                    continue
                img[Y, X] = st[j, i]


# ------------------------------------------------------------------ Koerper

def halbbreite(u, P):
    """Halbe Breite des Koerpers auf Hoehe u (Pixel ueber dem Boden)."""
    H, W = P["H"], P["W"]
    v = u / H
    if v <= 0 or v >= 1:
        return 0.0
    p = 2.3
    hw = W * (1 - v ** p) ** (1 / p)
    hw += P["flare"] * math.exp(-u / 4.0)
    if P["rb"] > 0:
        zone = P["rb"] * H * 0.5
        if u < zone:
            k = 1 - u / zone
            hw *= math.sqrt(max(0.0, 1 - k * k))
    hw += P["wob"] * math.sin(v * math.pi * 2.2 + P["wph"]) * 4 * v * (1 - v)
    return max(0.0, hw)


LIGHT = np.array([-0.55, 0.72, 0.55])
LIGHT /= np.linalg.norm(LIGHT)


def koerper(P):
    """Koerper + Hand. Rueckgabe: Bild, Maske, Lage von Gesicht/Hand/Kopf."""
    img = np.zeros((CELL_H, CELL_W, 4), np.uint8)
    bx0 = CX + P["dx"]
    H = P["H"]
    hand = hand_pos(P)

    cov = np.zeros((CELL_H, CELL_W))
    hand_cov = np.zeros((CELL_H, CELL_W))
    for y in range(CELL_H):
        u_c = GROUND - y - 0.5
        if u_c < -1 or u_c > H + 1:
            continue
        for x in range(CELL_W):
            if abs(x - bx0) > P["W"] + P["flare"] + P["wob"] + 4 and abs(x - hand[0]) > 8:
                continue
            n = nh = 0
            for sy in range(SS):
                u = GROUND - y - (sy + 0.5) / SS
                hw = halbbreite(u, P)
                for sx in range(SS):
                    xx = x + (sx + 0.5) / SS - bx0
                    if abs(xx) < hw:
                        n += 1
                    elif hand_in(x + (sx + 0.5) / SS, GROUND - u, hand):
                        nh += 1
            cov[y, x] = n / (SS * SS)
            hand_cov[y, x] = nh / (SS * SS)
    mask = (cov + hand_cov) >= 0.5
    body_only = cov >= 0.5

    # Licht je Pixel ueber die Normale einer Drehfigur
    for y in range(CELL_H):
        for x in range(CELL_W):
            if not mask[y, x]:
                continue
            u = GROUND - y - 0.5
            xx = x + 0.5 - bx0
            if body_only[y, x]:
                hw = max(halbbreite(u, P), 0.5)
                s = max(-1.0, min(1.0, xx / hw))
                d = (halbbreite(u + 1.0, P) - halbbreite(u - 1.0, P)) / 2.0
                nrm = np.array([s, -d * 0.9, math.sqrt(max(0.0, 1 - s * s)) + 0.15])
            else:
                hx, hy, hr = hand
                nrm = np.array([(x + 0.5 - hx) / hr, (hy - (y + 0.5)) / hr, 0.8])
            nrm /= np.linalg.norm(nrm)
            I = float(nrm @ LIGHT) * 0.85 + 0.2
            if I < 0.22:
                k = 1
            elif I < 0.42:
                k = 2
            elif I < 0.66:
                k = 3
            elif I < 0.92:
                k = 4
            else:
                k = 5
            img[y, x] = BODY[k]

    # Rueckstrahlung: kurz ueber dem Boden, innen am Rand heller
    dist = randabstand(mask)
    for y in range(CELL_H):
        for x in range(CELL_W):
            if not body_only[y, x]:
                continue
            u = GROUND - y - 0.5
            if u < H * 0.32 and 2 <= dist[y, x] <= 3 and tuple(img[y, x]) in (BODY[1], BODY[2]):
                img[y, x] = BOUNCE
            if P["rb"] == 0 and u < 2.0 and dist[y, x] > 1:
                img[y, x] = BODY[2] if tuple(img[y, x]) != BODY[1] else BODY[1]

    # Glanz links oben + Punkt, Blasen
    W, Hh = P["W"], P["H"]
    glanz(img, mask, bx0 - 0.56 * W, GROUND - 0.60 * Hh, 0.12 * W, 0.075 * Hh, -0.5)
    punkt(img, mask, bx0 - 0.36 * W, GROUND - 0.72 * Hh, 1.2, BODY[6])
    for (bx, bv, br) in P["blasen"]:
        blase(img, mask, bx0 + bx * W, GROUND - bv * Hh, br)

    # Umriss
    for y in range(CELL_H):
        for x in range(CELL_W):
            if mask[y, x] and dist[y, x] == 1:
                img[y, x] = BODY[0]

    kopf_u = Hh  # Oberkante
    return img, mask, hand


def randabstand(mask):
    """4er-Abstand zum Rand (1 = Randpixel)."""
    h, w = mask.shape
    d = np.where(mask, 999, 0)
    for _ in range(8):
        pad = np.pad(d, 1, constant_values=0)
        nb = np.minimum(np.minimum(pad[:-2, 1:-1], pad[2:, 1:-1]),
                        np.minimum(pad[1:-1, :-2], pad[1:-1, 2:]))
        d = np.where(mask, np.minimum(d, nb + 1), 0)
    return d


def glanz(img, mask, cx, cy, rx, ry, rot):
    c, s = math.cos(rot), math.sin(rot)
    for y in range(int(cy - ry - 3), int(cy + ry + 4)):
        for x in range(int(cx - rx - 3), int(cx + rx + 4)):
            if not (0 <= y < CELL_H and 0 <= x < CELL_W) or not mask[y, x]:
                continue
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            a = (dx * c - dy * s) / max(rx, 1.0)
            b = (dx * s + dy * c) / max(ry, 1.0)
            r = a * a + b * b
            if r <= 0.55:
                img[y, x] = BODY[6]
            elif r <= 1.25:
                img[y, x] = BODY[5]


def punkt(img, mask, cx, cy, r, col):
    for y in range(int(cy - r - 1), int(cy + r + 2)):
        for x in range(int(cx - r - 1), int(cx + r + 2)):
            if 0 <= y < CELL_H and 0 <= x < CELL_W and mask[y, x]:
                if (x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2 <= r * r:
                    img[y, x] = col


def blase(img, mask, cx, cy, r):
    for y in range(int(cy - r - 1), int(cy + r + 2)):
        for x in range(int(cx - r - 1), int(cx + r + 2)):
            if not (0 <= y < CELL_H and 0 <= x < CELL_W) or not mask[y, x]:
                continue
            d = math.hypot(x + 0.5 - cx, y + 0.5 - cy)
            if d <= r + 0.35 and d >= r - 0.65:
                img[y, x] = BODY[5]
    hx, hy = int(cx - r * 0.45), int(cy - r * 0.45)
    if 0 <= hy < CELL_H and 0 <= hx < CELL_W and mask[hy, hx]:
        img[hy, hx] = BODY[6]


# ------------------------------------------------------------------ Hand + Zepter

def hand_pos(P):
    """Mitte und Radius der Schleimhand (Bildpixel)."""
    u = P["H"] * P["hand_v"]
    hw = halbbreite(u, P)
    hx = CX + P["dx"] - hw - 1.0 + P["hand_dx"]
    hy = GROUND - u
    return (hx, hy, 5.2)


def hand_in(x, y, hand):
    hx, hy, hr = hand
    return (x - hx) ** 2 + ((y - hy) * 1.1) ** 2 <= hr * hr


def zepter(winkel):
    """
    Zepter um seinen Griff (0,0) gedreht. Winkel in Grad, 0 = senkrecht,
    positiv = Kopf nach links. Rueckgabe: RGBA-Bild + Lage des Griffs darin.
    """
    R = 52
    size = 2 * R
    a = math.radians(winkel)
    ca, sa = math.cos(a), math.sin(a)
    # Material-Abtastung
    lab = np.zeros((size, size, SS * SS), np.int16)
    for y in range(size):
        for x in range(size):
            k = 0
            for sy in range(SS):
                for sx in range(SS):
                    px = x + (sx + 0.5) / SS - R
                    py = R - (y + (sy + 0.5) / SS)       # nach oben positiv
                    # in Zepter-Koordinaten: s laengs (oben +), t quer (rechts +)
                    s = px * -sa + py * ca
                    t = px * ca + py * sa
                    lab[y, x, k] = zepter_material(s, t)
                    k += 1
    img = np.zeros((size, size, 4), np.uint8)
    for y in range(size):
        for x in range(size):
            vals = lab[y, x]
            filled = vals[vals > 0]
            if len(filled) * 2 < SS * SS:
                continue
            m = np.bincount(filled).argmax()
            img[y, x] = MAT[m]
    # Umriss aussen
    alpha = img[:, :, 3] > 0
    pad = np.pad(alpha, 1)
    ring = (~alpha) & (pad[:-2, 1:-1] | pad[2:, 1:-1] | pad[1:-1, :-2] | pad[1:-1, 2:])
    img[ring] = GOLD_OUT
    return img, (R, R)


# Materialnummern -> Farbe
MAT = {1: GOLD[0], 2: GOLD[1], 3: GOLD[2], 4: GOLD[3], 5: GOLD[4], 6: GOLD[5],
       11: GEM[0], 12: GEM[1], 13: GEM[2], 14: GEM[3], 15: GEM[4]}


def gold_licht(nx, ny):
    """Gold-Ton aus einer 2D-Normalen (Licht von links oben)."""
    I = -0.6 * nx + 0.8 * ny
    if I > 0.75:
        return 6
    if I > 0.35:
        return 5
    if I > 0.0:
        return 4
    if I > -0.45:
        return 3
    return 2


def kugel(s, t, cs, r, gem_r):
    ds, dt = s - cs, t
    d = math.hypot(ds, dt)
    if d > r:
        return 0
    if d > gem_r:
        return gold_licht(dt / d, ds / d) if d > 0 else 4
    # Edelstein: dunkel unten rechts, hell oben links
    q = (-dt * 0.6 + ds * 0.8) / gem_r
    hl = math.hypot(dt + gem_r * 0.38, ds - gem_r * 0.38)
    if hl < gem_r * 0.22:
        return 15
    if hl < gem_r * 0.45:
        return 14
    if d > gem_r - 1.1:
        return 11 if q < 0.2 else 12
    if q > 0.15:
        return 13
    if q > -0.45:
        return 12
    return 11


def zepter_material(s, t):
    # Spitze: Kugel mit kleinem Stein
    m = kugel(s, t, 44.5, 3.4, 1.5)
    if m:
        return m
    # Krallenfassung um den grossen Stein
    m = kugel(s, t, 29.0, 11.0, 8.4)
    if m:
        return m
    # Kragen unter der Kugel
    if 15.5 <= s <= 19.5 and abs(t) <= 3.6:
        return gold_licht(t / 3.6, 0.2)
    # Zierring
    if 2.0 <= s <= 4.4 and abs(t) <= 3.0:
        return gold_licht(t / 3.0, 0.3)
    # Knauf unten
    if math.hypot(s + 17.0, t) <= 3.0:
        return gold_licht(t / 3.0, (s + 17.0) / 3.0)
    # Stab
    if -17.0 <= s <= 18.0 and abs(t) <= 2.0:
        if t < -0.9:
            return 5
        if t > 0.9:
            return 2
        return 4
    return 0


ZEPTER_CACHE = {}


def zepter_bild(winkel):
    w = int(round(winkel))
    if w not in ZEPTER_CACHE:
        ZEPTER_CACHE[w] = zepter(w)
    return ZEPTER_CACHE[w]


def zepter_kugel_lage(winkel, grip):
    """Mitte des grossen Steins in Bildkoordinaten (fuers Funkeln)."""
    a = math.radians(winkel)
    return grip[0] - math.sin(a) * 29.0, grip[1] - math.cos(a) * 29.0


# ------------------------------------------------------------------ Bild zusammensetzen

HUT = None
HUT_ANKER = None


def basis_pose(**kw):
    P = dict(H=H0, W=W0, rb=0.0, flare=1.5, wob=0.0, wph=0.0, dx=0,
             hut=0.0, zepter=16.0, hand_v=0.40, hand_dx=0.0,
             eyes="open", mouth="smile", blush=True, funkeln=None,
             blasen=[(-0.25, 0.30, 1.6), (0.35, 0.22, 1.2)], face_dy=0)
    P.update(kw)
    return P


def volumen(H, k=0.8):
    """Breite zu einer Hoehe, damit er beim Quetschen nicht schrumpft."""
    return W0 * (H0 / H) ** k


def bild(P):
    img, mask, hand = koerper(P)
    bx0 = CX + P["dx"]
    H, W = P["H"], P["W"]
    sx = (W / W0) ** 0.7
    sy = H / H0

    # Gesicht
    eye_dx = 14.0 * sx
    eye_u = min(0.64 * H, H - 15.0) + P["face_dy"]
    lst, rst = EYES[P["eyes"]]
    ax, ay = EYE_ANCHOR[P["eyes"]]
    for side, st in ((-1, lst), (1, rst)):
        ex = bx0 + side * eye_dx
        ey = GROUND - eye_u
        paste(img, st, int(math.floor(ex - ax + 0.5)), int(math.floor(ey - ay + 0.5)), only_on=mask)
    if P["blush"]:
        for side in (-1, 1):
            bx = bx0 + side * 24.0 * sx ** 1.1
            by = GROUND - (0.45 * H + P["face_dy"])
            paste(img, BLUSH, int(round(bx - 3)), int(round(by - 1.5)), only_on=mask)
    m = MOUTHS[P["mouth"]]
    mu = 0.41 * H + P["face_dy"]
    paste(img, m, int(round(bx0 - m.shape[1] / 2.0)), int(round(GROUND - mu - m.shape[0] / 2.0)), only_on=mask)

    # Zepter hinter der Hand
    zb, (gx, gy) = zepter_bild(P["zepter"])
    hx, hy, hr = hand
    ox, oy = int(round(hx - gx)), int(round(hy - gy))
    over = np.zeros_like(img)
    paste(over, zb, ox, oy)
    # Zepter liegt VOR dem Koerper (er haelt es vor sich), aber HINTER der Hand
    hand_mask = np.zeros((CELL_H, CELL_W), bool)
    for y in range(CELL_H):
        for x in range(CELL_W):
            if hand_in(x + 0.5, y + 0.5, hand):
                hand_mask[y, x] = True
    keep = img.copy()
    sel = over[:, :, 3] > 0
    img[sel] = over[sel]
    # Hand drueber (Finger um den Stab): Pixel der Hand aus dem Koerperbild,
    # Umriss nur dort, wo die Hand an Zepter oder Luft grenzt.
    hd = randabstand(hand_mask)
    for y in range(CELL_H):
        for x in range(CELL_W):
            if not hand_mask[y, x]:
                continue
            if hd[y, x] == 1:
                inside_body = mask[y, x] and keep[y, x, 3] and tuple(keep[y, x]) != BODY[0]
                # Randpixel zur Koerperseite hin nicht nachziehen
                touches_body = False
                for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
                    Y, X = y + dy, x + dx
                    if 0 <= Y < CELL_H and 0 <= X < CELL_W and not hand_mask[Y, X] and \
                            keep[Y, X, 3] and tuple(keep[Y, X]) != BODY[0] and not sel[Y, X]:
                        touches_body = True
                img[y, x] = keep[y, x] if (touches_body and inside_body) else BODY[0]
            else:
                img[y, x] = keep[y, x] if keep[y, x, 3] else BODY[4]
    # Fingerfurche: zwei dunkle Pixel quer ueber die Hand
    fy = int(round(hy))
    for fx in (int(round(hx - 1)), int(round(hx + 1))):
        if 0 <= fy < CELL_H and 0 <= fx < CELL_W and hand_mask[fy, fx] and hd[fy, fx] > 1:
            img[fy, fx] = BODY[2]

    # Krone/Pelz
    hh, hw_ = HUT.shape[:2]
    top_u = H - 7 + P["hut"]
    hxp = int(round(bx0 - HUT_ANKER[0]))
    hyp = int(round(GROUND - top_u - HUT_ANKER[1]))
    paste(img, HUT, hxp, hyp)

    # Funkeln: ("krone"|"zepter", stufe)
    if P["funkeln"]:
        wo, stufe = P["funkeln"]
        if wo == "krone":
            cx, cy = hxp + (65 - 12) - 1, hyp + (58 - 21)       # mittlerer Stein der Krone
            cx, cy = hxp + HUT_ANKER[0] - 4, hyp + HUT_ANKER[1] - 25
        else:
            cx, cy = zepter_kugel_lage(P["zepter"], (hx, hy))
            cx, cy = cx - 3, cy - 3
        st = SPARKLE[stufe]
        paste(img, st, int(round(cx - st.shape[1] // 2)), int(round(cy - st.shape[0] // 2)))
    return img


# ------------------------------------------------------------------ Bewegung (Krone federt nach)

def feder(kopf, start=0.0, k=0.32, d=0.62, lo=-2.0, hi=7.0, runden=2):
    """
    Krone haengt an einer Feder ueber dem Kopf. kopf = Kopfhoehe je Bild
    (Pixel, Boden 0, inkl. Flughoehe). Rueckgabe: Versatz Krone gegen Kopf.
    Mehrere Runden, damit eine Schleife eingeschwungen anfaengt.
    """
    pos = kopf[0] + start
    vel = 0.0
    out = []
    for r in range(runden):
        out = []
        for i, h in enumerate(kopf):
            for _ in range(4):
                acc = (h - pos) * k - vel * d
                vel += acc
                pos += vel / 4.0
            off = max(lo, min(hi, pos - h))
            pos = h + off
            out.append(off)
    return out


# ------------------------------------------------------------------ Animationen

# Hopser: (H, Flughoehe, rb, Augen, Mund) je Bild. Luftbilder = HOP_AIR.
HOP = [
    (56, 0, 0.0, "open", "smile"),
    (54, 0, 0.0, "open", "smile"),
    (50, 0, 0.0, "open", "smile"),
    (44, 0, 0.0, "open", "small"),
    (65, 3, 0.15, "open", "o"),
    (62, 11, 0.30, "open", "smile"),
    (59, 16, 0.32, "open", "smile"),
    (56, 18, 0.32, "open", "smile"),
    (57, 16, 0.30, "open", "smile"),
    (60, 10, 0.25, "open", "small"),
    (62, 3, 0.10, "open", "small"),
    (43, 0, 0.0, "happy", "smile"),
    (59, 0, 0.0, "happy", "smile"),
    (54, 0, 0.0, "open", "smile"),
]
HOP_AIR = (4, 10)


def hop(blink=False):
    kopf = [h + lift for (h, lift, *_ ) in HOP]
    hut = feder(kopf)
    frames, lifts = [], []
    n = len(HOP)
    for i, (H, lift, rb, eyes, mouth) in enumerate(HOP):
        prev_lift = HOP[i - 1][1]
        vel = lift - prev_lift
        if blink and i in (0, 1):
            eyes = "half" if i == 0 else "closed"
        P = basis_pose(H=H, W=volumen(H), rb=rb, flare=1.5 if lift == 0 else 0.0,
                       wob=0.8 if i in (12, 13) else 0.0, wph=i * 1.7,
                       hut=hut[i], zepter=16 - vel * 1.2, hand_v=0.40 + max(0, vel) * 0.004,
                       eyes=eyes, mouth=mouth,
                       blasen=[(-0.25, 0.28 + 0.02 * (i % 7), 1.6), (0.35, 0.20 + 0.025 * ((i + 3) % 7), 1.2)],
                       funkeln=("krone", [0, 1, 2, 1][i - 5]) if 5 <= i <= 8 and not blink else None)
        frames.append(bild(P))
        lifts.append(lift)
    return frames, lifts


DUCKEN_H = [54, 52, 50, 47, 45, 43, 41, 40, 39, 38, 37, 37]


def ducken():
    frames = []
    kopf = DUCKEN_H
    hut = feder(kopf, runden=1)
    n = len(DUCKEN_H)
    for i, H in enumerate(DUCKEN_H):
        t = i / (n - 1)
        zitter = (1 if i % 2 else -1) if i >= 6 else 0
        eyes = "open" if i < 2 else "det"
        mouth = "smile" if i < 2 else "pout"
        # Zepter hoch ueber den Kopf: aus 16 Grad nach -6 (ueber den Kopf geneigt)
        winkel = 16 - 22 * min(1.0, t * 1.6)
        funk = None
        if i >= 4:
            funk = ("zepter", [1, 2, 3, 2, 3, 2, 1, 2][i - 4])
        P = basis_pose(H=H, W=volumen(H, 0.85), flare=1.5 + t * 2.5, dx=zitter,
                       hut=hut[i], zepter=winkel, hand_v=0.40 + 0.25 * min(1.0, t * 1.6),
                       hand_dx=-1.0 * min(1.0, t * 1.6),
                       eyes=eyes, mouth=mouth, funkeln=funk,
                       wob=0.6 if i >= 6 else 0.0, wph=i * 2.1)
        frames.append(bild(P))
    return frames


ABSPRUNG = [(69, 4, 0.20), (80, 16, 0.45), (84, 36, 0.55), (82, 62, 0.55)]


def absprung():
    frames, lifts = [], []
    for i, (H, lift, rb) in enumerate(ABSPRUNG):
        P = basis_pose(H=H, W=volumen(H, 0.85), rb=rb, flare=0.0,
                       hut=[-2, -2, 0, 3][i], zepter=30 + i * 2, hand_v=0.50,
                       eyes="det" if i < 2 else "happy", mouth="open")
        frames.append(bild(P))
        lifts.append(lift)
    return frames, lifts


def fall():
    frames = []
    for i in range(2):
        H = 75 + i
        P = basis_pose(H=H, W=volumen(H, 0.85), rb=0.6, flare=0.0, hut=6 + i,
                       zepter=36 + i * 3, hand_v=0.52,
                       eyes="wide", mouth="o", wob=0.7, wph=i * 3.0)
        frames.append(bild(P))
    return frames


LANDUNG = [
    (32, "squeeze", "open", 6.0),
    (30, "squeeze", "open", 6.5),
    (41, "squeeze", "smile", 4.0),
    (62, "open", "o", 0.5),
    (65, "open", "small", 0.0),
    (53, "open", "smile", 1.5),
    (50, "open", "smile", 2.0),
    (58, "happy", "smile", 1.0),
    (59, "happy", "open", 1.0),
    (55, "happy", "open", 1.5),
    (56, "happy", "smile", 1.5),
    (56, "open", "smile", 1.5),
]


def landung():
    kopf = [h for (h, *_ ) in LANDUNG]
    # Krone kommt von oben angeflogen (sie schwebte im Fall 7 px drueber)
    hut = feder([82] + kopf, start=0.0, runden=1, lo=-3.0, hi=9.0)[1:]
    frames = []
    for i, (H, eyes, mouth, flare) in enumerate(LANDUNG):
        P = basis_pose(H=H, W=volumen(H, 0.9), flare=flare,
                       wob=[1.5, 1.4, 1.2, 1.0, 0.9, 0.8, 0.6, 0.5, 0.4, 0.3, 0.2, 0.0][i],
                       wph=i * 2.4, hut=hut[i],
                       zepter=[30, 32, 26, 12, 8, 14, 18, 15, 16, 16, 16, 16][i],
                       hand_v=[0.5, 0.5, 0.46, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4][i],
                       eyes=eyes, mouth=mouth,
                       funkeln=("krone", [1, 2, 3, 2, 1][i - 6]) if 6 <= i <= 10 else None)
        frames.append(bild(P))
    return frames


# ------------------------------------------------------------------ Effekte

PL_W, PL_H = 320, 128
PL_GROUND = PL_H - 24      # Spritzer fliegen bis knapp unter die Bodenlinie (Perspektive)


def platsch():
    """
    Aufprall: Druckwelle, Staubwolken rundherum, Schleimtropfen in Boegen,
    die als kleine Pfuetzen liegen bleiben und eintrocknen.
    """
    rnd = np.random.default_rng(11)
    cx, gy = PL_W / 2.0, PL_GROUND

    tropfen = []
    for k in range(14):
        a = (k + rnd.uniform(0.2, 0.8)) / 14.0 * math.pi      # Faecher nach oben
        x0 = cx + math.cos(a) * rnd.uniform(26, 38)
        y0 = gy - rnd.uniform(4, 14)
        vx = math.cos(a) * rnd.uniform(4.0, 7.0) + (1.5 if math.cos(a) > 0 else -1.5)
        vy = 6.5 + math.sin(a) * rnd.uniform(2.0, 3.5)
        r = rnd.uniform(2.8, 4.6)
        tiefe = rnd.uniform(-7, 9)          # wo er am Boden liegen bleibt (vor/hinter)
        tropfen.append((x0, y0, vx, vy, r, tiefe))

    wolken = []
    for k in range(26):
        ang = (k + 0.5) / 26.0 * 2 * math.pi + rnd.uniform(-0.08, 0.08)
        wolken.append((ang, rnd.uniform(0.88, 1.12)))

    frames = []
    for f in range(10):
        img = np.zeros((PL_H, PL_W, 4), np.uint8)
        t = f / 9.0

        # Druckwelle: nur die ersten Bilder, schnell und duenn
        if f <= 4:
            rx = 44 + 22 * f
            ry = rx * 0.32
            dicke = 2.6 - f * 0.45
            for y in range(int(gy - ry - 3), int(gy + ry + 4)):
                for x in range(int(cx - rx - 3), int(cx + rx + 4)):
                    if not (0 <= y < PL_H and 0 <= x < PL_W):
                        continue
                    d = math.hypot((x + 0.5 - cx) / rx, (y + 0.5 - gy) / ry)
                    if abs(d - 1.0) * rx <= dicke:
                        img[y, x] = (240, 230, 252, 255)

        # Staubwolken: hintere zuerst
        weite = 36 + 58 * (1 - (1 - t) ** 2.2)
        reihe = sorted(wolken, key=lambda w: math.sin(w[0]))
        for ang, gross in reihe:
            pr = (6.0 + 4.0 * math.sin(math.pi * min(1.0, t * 1.5))) * gross * (1.0 - max(0.0, t - 0.55) / 0.45)
            if pr < 1.2:
                continue
            px = cx + math.cos(ang) * weite * gross
            py = gy + math.sin(ang) * weite * 0.30 * gross - f * 0.9
            wolke(img, px, py, pr)

        # Schleimtropfen
        for (x0, y0, vx, vy, r, tiefe) in tropfen:
            g = 1.25
            x = x0 + vx * f
            y = y0 - (vy * f - 0.5 * g * f * f)
            boden = gy + tiefe
            if y >= boden and f > 0:
                # gelandet: wann? -> ab da Pfuetze, die eintrocknet
                tl = (vy + math.sqrt(max(0.0, vy * vy + 2 * g * (boden - y0)))) / g
                xl = x0 + vx * tl
                alter = f - tl
                rr = r * (1.25 - max(0.0, alter - 2.0) * 0.28)
                if rr > 0.9:
                    ellipse(img, xl, boden, rr * 1.7, max(1.0, rr * 0.6), BODY[3], BODY[0])
            else:
                vyy = vy - g * f
                streck = 1.0 + min(0.6, abs(vyy) * 0.06)
                tropf(img, x, y, r * (1.0 - f * 0.03), streck)
        frames.append(img)
    return frames


def wolke(img, cx, cy, r):
    """Staubwolke: oben hell, unten Schatten. Ueberlappen sich zu einem Kranz."""
    h, w = img.shape[:2]
    for y in range(int(cy - r - 1), int(cy + r + 2)):
        for x in range(int(cx - r - 1), int(cx + r + 2)):
            if not (0 <= y < h and 0 <= x < w):
                continue
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            if dx * dx + (dy * 1.15) ** 2 > r * r:
                continue
            hell = -dx * 0.45 - dy * 0.9
            if hell > r * 0.35:
                col = (238, 232, 246, 255)
            elif hell > -r * 0.35:
                col = (214, 202, 228, 255)
            else:
                col = (178, 160, 200, 255)
            # Hellere Pixel einer Nachbarwolke nicht ueberdecken (sonst Kugelkette)
            if img[y, x, 3] and int(img[y, x, :3].astype(int).sum()) > sum(col[:3]) and col[0] < 200:
                continue
            img[y, x] = col


def ellipse(img, cx, cy, rx, ry, fill, edge):
    h, w = img.shape[:2]
    for y in range(int(cy - ry - 2), int(cy + ry + 3)):
        for x in range(int(cx - rx - 2), int(cx + rx + 3)):
            if not (0 <= y < h and 0 <= x < w):
                continue
            d = ((x + 0.5 - cx) / rx) ** 2 + ((y + 0.5 - cy) / ry) ** 2
            if d <= 1.0:
                img[y, x] = fill
    ring(img, edge)


def tropf(img, cx, cy, r, streck=1.0):
    h, w = img.shape[:2]
    ry = r * streck
    rx = r / math.sqrt(streck)
    for y in range(int(cy - ry - 2), int(cy + ry + 3)):
        for x in range(int(cx - rx - 2), int(cx + rx + 3)):
            if 0 <= y < h and 0 <= x < w and ((x + 0.5 - cx) / rx) ** 2 + ((y + 0.5 - cy) / ry) ** 2 <= 1.0:
                img[y, x] = BODY[4]
    if r > 1.6:
        hy, hx = int(cy - ry * 0.4), int(cx - rx * 0.4)
        if 0 <= hy < h and 0 <= hx < w and img[hy, hx, 3]:
            img[hy, hx] = BODY[6]
    ring(img, BODY[0])


def ring(img, edge):
    """Umriss um alles, was noch keinen hat (nur Pixel ohne Farbe drumherum)."""
    a = img[:, :, 3] > 0
    pad = np.pad(a, 1)
    out = (~a) & (pad[:-2, 1:-1] | pad[2:, 1:-1] | pad[1:-1, :-2] | pad[1:-1, 2:])
    # nur um Schleim, nicht um den Staubring
    body_cols = {BODY[3], BODY[4], BODY[6]}
    h, w = a.shape
    for y, x in zip(*np.nonzero(out)):
        nb = [img[Y, X] for Y, X in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1))
              if 0 <= Y < h and 0 <= X < w]
        if any(tuple(c) in body_cols for c in nb):
            img[y, x] = edge


SCH_W, SCH_H = 96, 32
SCHATTEN = [84, 74, 64, 54, 44, 34, 24, 14]


def schatten():
    frames = []
    for wdt in SCHATTEN:
        img = np.zeros((SCH_H, SCH_W, 4), np.uint8)
        rx, ry = wdt / 2.0, max(2.0, wdt * 0.13)
        cx, cy = SCH_W / 2.0, SCH_H / 2.0
        for y in range(SCH_H):
            for x in range(SCH_W):
                if ((x + 0.5 - cx) / rx) ** 2 + ((y + 0.5 - cy) / ry) ** 2 <= 1.0:
                    img[y, x] = SHADOW
        frames.append(img)
    return frames


# ------------------------------------------------------------------ Ausgabe

def schreibe(name, frames, w=CELL_W, h=CELL_H, pivot=PIVOT):
    os.makedirs(OUT_DIR, exist_ok=True)
    path = os.path.join(OUT_DIR, name + ".png")
    sheet = Image.new("RGBA", (w * len(frames), h))
    for i, f in enumerate(frames):
        sheet.paste(Image.fromarray(f), (i * w, 0))
    sheet.save(path)
    meta = path + ".meta"
    if not os.path.exists(meta):
        sys.path.insert(0, TOOLS)
        from unity_meta import write_strip_meta
        write_strip_meta(meta, name, len(frames), w, h, PPU, pivot=pivot, max_size=8192)
    print("%-26s %2d Bilder  %dx%d" % (name, len(frames), w, h))


def main():
    global HUT, HUT_ANKER
    args = sys.argv[1:]
    if "--quelle" in args:
        Image.open(args[args.index("--quelle") + 1]).convert("RGBA").save(QUELLE)
        print("Vorlage kopiert:", QUELLE)
    HUT, HUT_ANKER = lade_hut(QUELLE)

    hop_f, hop_l = hop()
    hopb_f, _ = hop(blink=True)
    duck_f = ducken()
    ab_f, ab_l = absprung()
    fall_f = fall()
    land_f = landung()
    pl_f = platsch()
    sch_f = schatten()

    nur = None
    if "--nur" in args:
        nur = args[args.index("--nur") + 1]
    if nur != "vorschau":
        schreibe("schleimkoenig_hop", hop_f)
        schreibe("schleimkoenig_hop_blink", hopb_f)
        schreibe("schleimkoenig_ducken", duck_f)
        schreibe("schleimkoenig_absprung", ab_f)
        schreibe("schleimkoenig_fall", fall_f)
        schreibe("schleimkoenig_landung", land_f)
        schreibe("schleimkoenig_platsch", pl_f, PL_W, PL_H, (0.5, (PL_H - PL_GROUND) / PL_H))
        schreibe("schleimkoenig_schatten", sch_f, SCH_W, SCH_H, (0.5, 0.5))

        # Bestiarium: Ruhepose, zugeschnitten
        b = Image.fromarray(hop_f[0])
        b = b.crop(b.getbbox())
        os.makedirs(os.path.dirname(BESTIARY), exist_ok=True)
        b.save(BESTIARY)
        print("Bestiarium:", BESTIARY, b.size)

    print("HopLift      =", hop_l, " Luft", HOP_AIR)
    print("AbsprungLift =", ab_l)
    print("Schatten     =", SCHATTEN)

    if "--preview" in args:
        vorschau(dict(hop=hop_f, hopb=hopb_f, duck=duck_f, ab=ab_f, fall=fall_f,
                      land=land_f, pl=pl_f, sch=sch_f), hop_l, ab_l,
                 args[args.index("--preview") + 1])


# ------------------------------------------------------------------ Vorschau

def boden(w, h):
    bg = np.zeros((h, w, 4), np.uint8)
    bg[:, :] = (78, 104, 70, 255)
    for y in range(h):
        for x in range(w):
            if (x * 7 + y * 13 + (x // 5) * (y // 3)) % 29 == 0:
                bg[y, x] = (92, 120, 80, 255)
    return bg


def vorschau(A, hop_l, ab_l, path, scale=3):
    """GIF: zwei Hopser, Riesensprung mit roter Zone, Landung mit Wackelkamera."""
    W, H = 400, 300
    gy = 230                      # Bodenlinie im Bild
    bg = boden(W, H)
    seq = []
    x = 120.0
    schritt = 2.4

    def schatten_fuer(lift):
        i = min(len(SCHATTEN) - 1, int(lift / 14.0))
        return A["sch"][i]

    def frame(body, lift, bx, zone=None, platsch=None, shake=(0, 0), sch=True):
        im = bg.copy()
        if zone is not None:
            zx, zfill = zone
            zonen(im, zx, gy, 56, zfill)
        if sch:
            s = schatten_fuer(lift)
            stamp(im, s, int(bx - SCH_W / 2), int(gy - SCH_H / 2))
        if platsch is not None:
            p, px = platsch
            stamp(im, p, int(px - PL_W / 2), int(gy - PL_GROUND))
        if body is not None:
            stamp(im, body, int(round(bx - CX)), int(round(gy - lift - GROUND)))
        if shake != (0, 0):
            im = np.roll(im, shake, axis=(0, 1))
        return im

    for rep in range(2):
        for i in range(len(A["hop"])):
            f = (A["hopb"] if rep == 1 else A["hop"])[i]
            if HOP_AIR[0] <= i <= HOP_AIR[1]:
                x += schritt * 1.4
            seq.append(frame(f, hop_l[i], x))
    for f in A["duck"]:
        seq.append(frame(f, 0, x))
    for i, f in enumerate(A["ab"]):
        seq.append(frame(f, ab_l[i], x))
    lift = ab_l[-1]
    v = 30
    while lift < 330:
        lift += v
        v += 8
        seq.append(frame(A["ab"][-1], lift, x, sch=False))
    ziel = 280.0
    n_zone = 14
    for k in range(n_zone):
        fill = (k + 1) / n_zone
        hoehe = None
        if k >= n_zone - 4:
            hoehe = 330 * (1 - (k - (n_zone - 4) + 1) / 4.0) ** 1.6
        if hoehe is None:
            seq.append(frame(None, 0, ziel, zone=(ziel, fill), sch=False))
        else:
            seq.append(frame(A["fall"][k % 2], hoehe, ziel, zone=(ziel, fill)))
    shakes = [(3, -2), (-3, 2), (2, 1), (-2, -1), (1, 0), (0, 0)]
    for i, f in enumerate(A["land"]):
        p = A["pl"][i] if i < len(A["pl"]) else None
        sh = shakes[i] if i < len(shakes) else (0, 0)
        seq.append(frame(f, 0, ziel, platsch=(p, ziel) if p is not None else None, shake=sh))
    for i in range(len(A["hop"])):
        seq.append(frame(A["hop"][i], hop_l[i], ziel))

    imgs = [Image.fromarray(s).convert("RGB").resize((W * scale, H * scale), Image.NEAREST) for s in seq]
    imgs[0].save(path, save_all=True, append_images=imgs[1:], duration=int(1000 / FPS), loop=0)
    print("Vorschau:", path, len(imgs), "Bilder")


def stamp(im, st, x, y):
    h, w = st.shape[:2]
    H, W = im.shape[:2]
    x0, y0 = max(0, x), max(0, y)
    x1, y1 = min(W, x + w), min(H, y + h)
    if x0 >= x1 or y0 >= y1:
        return
    src = st[y0 - y:y1 - y, x0 - x:x1 - x].astype(float)
    dst = im[y0:y1, x0:x1].astype(float)
    a = src[:, :, 3:4] / 255.0
    dst[:, :, :3] = src[:, :, :3] * a + dst[:, :, :3] * (1 - a)
    im[y0:y1, x0:x1] = dst.astype(np.uint8)


def zonen(im, cx, gy, r, fill):
    """Grob wie BossTelegraph.Zone: Ring + wachsende Scheibe, flach am Boden."""
    H, W = im.shape[:2]
    for y in range(H):
        for x in range(W):
            dx = (x + 0.5 - cx) / r
            dy = (y + 0.5 - gy) / (r * 0.62)
            d = math.hypot(dx, dy)
            if d <= 1.0:
                a = 0.0
                if d >= 0.84:
                    a = 0.85
                    col = (255, 56, 31)
                elif d <= fill:
                    a = 0.45
                    col = (255, 82, 31)
                else:
                    a = 0.20
                    col = (255, 41, 26)
                im[y, x, :3] = (np.array(col) * a + im[y, x, :3] * (1 - a)).astype(np.uint8)


if __name__ == "__main__":
    main()
