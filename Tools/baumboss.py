"""
Baum-Boss (Treant): freigestellt, auf 128x128 gebracht und animiert.

  Tools/baumboss_quelle.png      Rohsprite im echten Pixelraster (59x50),
                                 einmalig aus dem KI-Bild zurueckgerechnet
  Assets/Art/Gegner/new/boss/
    baumboss_walk.png            12 x 128x128  Laufzyklus (Schleife)
    baumboss_charge.png          24 x 128x128  holt 2 s Luft, Glut sammelt sich im Maul
    baumboss_roar.png             4 x 128x128  reisst das Maul auf (rueckwaerts = zumachen)
    baumboss_roar_loop.png        6 x 128x128  Maul offen, Feuer zuengelt (Schleife,
                                               solange der Flammenwurf laeuft)
  alle PPU 32 (wie die Welt), Pivot unten Mitte (Fuesse). Abspielen mit 12 fps.
    baumboss_walk_back.png      12 x 128x128  Laufzyklus von hinten (laeuft nach oben)
    baumboss_charge_back.png    24 x 128x128  } dasselbe von hinten: speit er nach oben,
    baumboss_roar_back.png       4 x 128x128  } dreht er sich nicht um - man sieht den
    baumboss_roar_loop_back.png  6 x 128x128  } Feuerschein um ihn herum

Der Flammenwurf selbst ist getrennt: Tools/baumboss_feuer.py + FlameBeam.cs.
Wo er ansetzt, steht in MAUL_PIXEL (Pixel im 128er-Frame, von oben links).

Die Vorlage ist hochskalierte Pixelart (Raster ~12.4 px). Sie wird auf ihr
Raster zurueckgerechnet, per Scale2x verdoppelt (runde Kanten statt Bloecke)
und dann pro Frame verformt: ein glattes Verschiebungsfeld biegt Wurzelbeine,
Rumpf und Arme. Krone und Haende bewegen sich starr (nur der Hals biegt
sich), sonst reissen die Blaetter zeilenweise auseinander.

Aufruf aus dem Projektordner:
  python Tools/baumboss.py [--quelle bild.png] [--preview pfad.gif]
"""

import math
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
QUELLE = os.path.join(ROOT, "Tools", "baumboss_quelle.png")
OUT_DIR = os.path.join(ROOT, "Assets", "Art", "Gegner", "new", "boss")

CELL = 128
PPU = 32  # wie die Welt (EnemyCatalog.PixelsPerUnit): ein Bildpixel = ein Weltpixel
FPS = 12
PIVOT = (0.5, 6 / CELL)  # Fuesse stehen 6 px ueber dem Zellenboden

# Lage der Vorlage im Frame
SRC_W, SRC_H = 118, 100
OX = (CELL - SRC_W) // 2
OY = CELL - SRC_H - 6


# ---------------------------------------------------------------- Vorlage

def aus_ki_bild(path):
    """KI-Bild -> Sprite im echten Raster, Hintergrund transparent, zugeschnitten."""
    im = np.array(Image.open(path).convert("RGB")).astype(int)
    h = im.shape[0]
    n, off = 101, 4.4  # per Kantensuche ermittelt
    b = h / n
    a = np.zeros((n, n, 3), int)
    for j in range(n):
        for i in range(n):
            y0 = min(int(off + j * b + b * 0.3), h - 1)
            x0 = min(int(off + i * b + b * 0.3), h - 1)
            y1 = max(int(off + j * b + b * 0.7), y0 + 1)
            x1 = max(int(off + i * b + b * 0.7), x0 + 1)
            a[j, i] = np.median(im[y0:y1, x0:x1].reshape(-1, 3), 0)

    # Grau ist Hintergrund oder Bodenschatten - auch eingeschlossene Luecken
    sat = a.max(2) - a.min(2)
    mean = a.mean(2)
    fg = ~((sat < 14) & (mean > 60) & (mean < 130))
    ys, xs = np.nonzero(fg)
    rgba = np.zeros((n, n, 4), np.uint8)
    rgba[..., :3] = a
    rgba[..., 3] = fg * 255
    return Image.fromarray(rgba[ys.min():ys.max() + 1, xs.min():xs.max() + 1])


def scale2x(a):
    """EPX/Scale2x auf RGBA-Array (h, w, 4) -> (2h, 2w, 4)."""
    h, w = a.shape[:2]
    p = np.pad(a, ((1, 1), (1, 1), (0, 0)), mode="edge")
    P = p[1:-1, 1:-1]
    A = p[:-2, 1:-1]   # oben
    B = p[1:-1, 2:]    # rechts
    C = p[1:-1, :-2]   # links
    D = p[2:, 1:-1]    # unten

    def eq(u, v):
        return np.all(u == v, axis=2)

    e1 = np.where((eq(C, A) & ~eq(C, D) & ~eq(A, B))[..., None], A, P)
    e2 = np.where((eq(A, B) & ~eq(A, C) & ~eq(B, D))[..., None], B, P)
    e3 = np.where((eq(D, C) & ~eq(D, B) & ~eq(C, A))[..., None], C, P)
    e4 = np.where((eq(B, D) & ~eq(B, A) & ~eq(D, C))[..., None], D, P)
    out = np.zeros((2 * h, 2 * w, 4), a.dtype)
    out[0::2, 0::2] = e1
    out[0::2, 1::2] = e2
    out[1::2, 0::2] = e3
    out[1::2, 1::2] = e4
    return out


def rueckseite(nat):
    """Rueckansicht aus der Vorlage: Gesicht weg (Rinde statt Augen und Maul), die
    Aeste in der Krone verschwinden hinter dem Laub, dann gespiegelt - von hinten
    ist sein rechter Arm links."""
    a = nat.copy().astype(int)
    h, w = a.shape[:2]
    rgb = a[..., :3]
    lum = rgb.mean(2)
    braun = (rgb[..., 0] > rgb[..., 1] + 8) & (rgb[..., 1] >= rgb[..., 2] - 6)
    gruen = (rgb[..., 1] > rgb[..., 0] + 6)
    dunkel = lum < 60

    def hsh(x, y):
        return ((x * 73856093) ^ (y * 19349663)) % 997 / 997.0

    # Rindenfarben aus dem Stamm selbst, dunkel -> hell
    probe = rgb[40:46, 22:37][braun[40:46, 22:37] & ~dunkel[40:46, 22:37]]
    probe = probe[np.argsort(probe.mean(1))]
    stufen = [probe[int(q * (len(probe) - 1))] for q in (0.05, 0.3, 0.55, 0.8, 0.97)]
    nut = np.array([46, 24, 20])

    # 1) Gesicht -> Rinde: senkrechte Borkenplatten mit Rillen, rund schattiert
    for y in range(22, 39):
        for x in range(21, 38):
            if a[y, x, 3] == 0 or gruen[y, x]:
                continue
            # Silhouettenrand (Kontur) stehen lassen
            if x in (21, 37) and dunkel[y, x]:
                continue
            welle = 0.8 * math.sin(y * 0.55 + x * 0.3) + 0.5 * math.sin(y * 1.3)
            u = x + welle
            rund = math.cos((x - 29.0) / 9.5 * math.pi / 2)  # Licht in der Mitte
            if (u % 3.4) < 0.75:
                col = nut if rund < 0.35 else stufen[0]
            else:
                lvl = 1 + rund * 2.6 + (hsh(x // 2, y // 3) - 0.5) * 0.5 - ((u % 3.4) > 2.7) * 0.9
                col = stufen[int(max(1, min(4, round(lvl))))]
            # ein Astloch, wo vorne das Maul ist
            if (x - 29) ** 2 / 4.0 + (y - 33) ** 2 / 2.2 <= 1.0:
                col = nut if (x - 29) ** 2 / 2.0 + (y - 33) ** 2 / 1.0 <= 1.0 else stufen[0]
            a[y, x, :3] = col

    # 2) Krone: innere Aeste liegen von hinten unter dem Laub
    laub = [np.array([36, 67, 40]), np.array([47, 90, 42]), np.array([70, 120, 48])]
    for y in range(0, 20):
        for x in range(17, 42):
            if a[y, x, 3] == 0 or not (braun[y, x] or (dunkel[y, x] and not gruen[y, x])):
                continue
            if y >= 16 and 22 <= x <= 36:
                continue  # Stamm, der in die Krone laeuft
            # nur, was rechts und links Laub hat - die Spitzen ragen weiter heraus
            links = gruen[y, max(0, x - 7):x].any()
            rechts = gruen[y, x + 1:x + 8].any()
            if not (links and rechts):
                continue
            v = hsh(x, y)
            a[y, x, :3] = laub[0] if v < 0.45 else (laub[1] if v < 0.85 else laub[2])

    return np.ascontiguousarray(a[:, ::-1]).astype(np.uint8)


# ---------------------------------------------------------------- Verformung

def ss(e0, e1, x):
    """smoothstep, auch mit e0 > e1 (dann fallend)."""
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


POSE_KEYS = ("fussL", "fussR", "rumpf_dy", "rumpf_dx", "krone_dx", "krone_dy",
             "armL", "armR", "armL_dx", "armR_dx")


def pose(**kw):
    m = {k: 0.0 for k in POSE_KEYS}
    m.update(kw)
    return m


def mischen(a, b, t):
    return {k: a[k] + (b[k] - a[k]) * t for k in POSE_KEYS}


def gewichte(w, h):
    """Einflussbereiche der Koerperteile (einmal berechnet)."""
    Y, X = np.mgrid[0:h, 0:w].astype(float)
    x, y = X / 2.0, Y / 2.0  # Rasterkoordinaten der Vorlage (59x50)
    g = {}
    bein = ss(35.0, 44.0, y)
    g["L"] = ss(26.0, 19.0, x) * bein
    g["R"] = ss(33.0, 40.0, x) * bein
    mitte = ss(40.0, 49.0, y) * (1 - ss(26.0, 19.0, x)) * (1 - ss(33.0, 40.0, x))
    g["rumpf"] = 1.0 - g["L"] - g["R"] - mitte * 0.7
    # Krone samt Seitenbueschen und Ranken starr. Gebogen wird nur der Stamm (glatte
    # Rinde, Zeile 20..30) - aussen daneben haengt alles bis Zeile 21 an der Krone,
    # darunter beginnen die Arme.
    stamm = ss(18.0, 21.0, x) * ss(41.0, 38.0, x)
    g["krone"] = stamm * ss(30.0, 20.0, y) + (1 - stamm) * ss(23.0, 20.0, y)
    # Arme: Unterarm biegt, Hand bleibt starr
    band = ss(20.0, 23.0, y) * ss(47.0, 43.0, y)
    g["armL"] = ss(19.0, 11.0, x) * band
    g["armR"] = ss(40.0, 48.0, x) * band
    return g


def feld(g, m):
    """Verschiebungsfeld (dx, dy) in 2x-Pixeln, +y = unten."""
    dy = g["rumpf"] * m["rumpf_dy"] + g["L"] * m["fussL"] + g["R"] * m["fussR"]
    dx = g["rumpf"] * m["rumpf_dx"]
    # Schwungfuss dreht leicht nach aussen
    dx += g["L"] * (m["fussL"] * 0.25) + g["R"] * (-m["fussR"] * 0.25)
    dx += g["krone"] * m["krone_dx"]
    dy += g["krone"] * m["krone_dy"]
    dy += g["armL"] * m["armL"] + g["armR"] * m["armR"]
    dx += g["armL"] * m["armL_dx"] + g["armR"] * m["armR_dx"]
    return dx, dy


def warp(src, dx, dy):
    """Inverses Sampling: out(x, y) = src(x - dx, y - dy)."""
    h, w = src.shape[:2]
    out = np.zeros((CELL, CELL, 4), np.uint8)
    fdx = np.zeros((CELL, CELL))
    fdy = np.zeros((CELL, CELL))
    fdx[OY:OY + h, OX:OX + w] = dx
    fdy[OY:OY + h, OX:OX + w] = dy
    # Rand nach aussen fortsetzen, damit angehobene Teile nicht abgeschnitten werden
    for f in (fdx, fdy):
        f[:OY, :] = f[OY, :]
        f[OY + h:, :] = f[OY + h - 1, :]
        f[:, :OX] = f[:, OX:OX + 1]
        f[:, OX + w:] = f[:, OX + w - 1:OX + w]
    Y, X = np.mgrid[0:CELL, 0:CELL]
    # floor(+0.5) statt np.round: das rundet .5 zur geraden Zahl und laesst Zeilen doppelt/ausfallen
    sx = np.floor(X - fdx + 0.5).astype(int) - OX
    sy = np.floor(Y - fdy + 0.5).astype(int) - OY
    ok = (sx >= 0) & (sx < w) & (sy >= 0) & (sy < h)
    out[ok] = src[sy[ok], sx[ok]]
    return out


# ---------------------------------------------------------------- Farben + Helfer

def hx(s):
    s = s.lstrip("#")
    return np.array([int(s[i:i + 2], 16) for i in (0, 2, 4)], float)


EYE_CORE = hx("#fffbe0")
EYE_GLOW = hx("#ffd84a")
FIRE_WHITE = hx("#fff6d0")
EMBER_HOT = hx("#ffd04a")
EMBER = hx("#ff7a1a")
FIRE_RED = hx("#e0401a")
MAW_MID = hx("#a3290e")
MAW_DEEP = hx("#5c1209")
MAW_EDGE = hx("#2a0a08")
TOOTH_HI = hx("#e3bb84")
TOOTH = hx("#b98450")
TOOTH_SH = hx("#7a4a2a")
LEAF = [hx("#2f5a2a"), hx("#5f9a34"), hx("#a9cf55")]
DUST = [hx("#d8c3a0"), hx("#b39b7a"), hx("#8a7560")]


def blend(img, y, x, col, a):
    if 0 <= y < CELL and 0 <= x < CELL and img[y, x, 3] > 0 and a > 0:
        c = img[y, x, :3].astype(float)
        img[y, x, :3] = np.clip(c + (col - c) * min(a, 1.0), 0, 255).astype(np.uint8)


def put(img, y, x, col, hinter=False):
    """hinter=True: nur in die Luft malen - was auf den Baum faellt, liegt hinter ihm."""
    y, x = int(round(y)), int(round(x))
    if 0 <= y < CELL and 0 <= x < CELL:
        if hinter and img[y, x, 3] > 0:
            return
        img[y, x, :3] = col.astype(np.uint8)
        img[y, x, 3] = 255


def finde_augen_maul(src):
    """Augen = helle gelbe Pixel, Maul = sehr dunkle Pixel im Gesichtsbereich (2x-Koord.)."""
    rgb = src[..., :3].astype(int)
    al = src[..., 3] > 0
    lum = rgb.mean(2)
    Y, X = np.mgrid[0:src.shape[0], 0:src.shape[1]]
    gesicht = (Y > 48) & (Y < 80) & (X > 36) & (X < 76)
    augen = al & gesicht & (lum > 190) & (rgb[..., 2] < rgb[..., 0])
    maul = al & (Y >= 66) & (Y < 74) & (X >= 50) & (X < 68) & (lum < 45)
    return augen, maul


def ziel(mask, dx, dy):
    """Quellpixel einer Maske -> Frame-Koordinaten nach der Verformung."""
    ys, xs = np.nonzero(mask)
    return [(int(math.floor(y + dy[y, x] + 0.5)) + OY, int(math.floor(x + dx[y, x] + 0.5)) + OX)
            for y, x in zip(ys, xs)]


# ---------------------------------------------------------------- Gesicht

def augen(img, pts, staerke, k):
    """Glutschein auf die Rinde + heller Kern. staerke 0..~1.6"""
    r = 2.6 + 2.0 * staerke
    a0 = 0.22 + 0.4 * min(staerke, 1.2)
    ri = int(math.ceil(r))
    for (y, x) in pts:
        for ry in range(-ri, ri + 1):
            for rx in range(-ri, ri + 1):
                d = math.hypot(rx, ry * 1.2)
                if 0 < d <= r:
                    blend(img, y + ry, x + rx, EYE_GLOW, a0 * (1 - d / (r + 0.2)) ** 1.4)
    core = EYE_CORE if staerke > 0.35 else EYE_CORE * 0.6 + EYE_GLOW * 0.4
    for (y, x) in pts:
        put(img, y, x, core)


def gesichtslicht(img, cx, cy, r, staerke):
    """Feuerschein aus dem Maul auf die Rinde (nur auf deckenden Pixeln)."""
    if staerke <= 0:
        return
    ri = int(r)
    for yy in range(-ri, ri + 1):
        for xx in range(-ri, ri + 1):
            d = math.hypot(xx, yy * 1.15)
            if d < r:
                blend(img, cy + yy, cx + xx, EMBER, staerke * 0.5 * (1 - d / r) ** 1.6)


def maul_zu(img, pts, glut, k):
    """Geschlossenes Maul: dunkler Rand, Glut im Spalt. glut 0..~1.5"""
    s = set(pts)
    for (y, x) in pts:
        if any((y + a, x + b) not in s for a, b in ((1, 0), (-1, 0), (0, 1), (0, -1))):
            put(img, y, x, MAW_EDGE)
        else:
            f = 0.5 + 0.5 * math.sin(x * 1.3 + k * 2.3)
            if f * glut > 0.55:
                col = FIRE_WHITE if f * glut > 1.05 else EMBER_HOT
            elif f + glut > 0.7:
                col = EMBER
            else:
                col = MAW_MID
            put(img, y, x, col)


def maul_offen(img, cx, top, o, k):
    """Aufgerissenes Maul mit Holzzaehnen, innen Feuer. o 0..1"""
    hw = 8.0 + 4.5 * o
    tiefe = 5.0 + 15.0 * o
    oben = tiefe * 0.32
    cy = top + oben
    unten = tiefe - oben
    for y in range(int(top) - 1, int(top + tiefe) + 2):
        for x in range(int(cx - hw) - 1, int(cx + hw) + 2):
            ry = oben if y < cy else unten
            d = math.hypot((x - cx) / hw, (y - cy) / ry)
            # Rand leicht zerfranst wie gebrochenes Holz
            d += 0.06 * math.sin(x * 2.1 + y * 0.7)
            if d > 1.0:
                continue
            fl = 0.07 * math.sin(x * 0.9 + y * 0.7 + k * 2.7)
            if d > 0.84:
                col = MAW_EDGE
            elif d > 0.68 + fl:
                col = MAW_DEEP
            elif d > 0.5 + fl:
                col = FIRE_RED
            elif d > 0.32 + fl:
                col = EMBER
            elif d > 0.16 + fl:
                col = EMBER_HOT
            else:
                col = FIRE_WHITE
            put(img, y, x, col)

    def rand_y(x, unten_rand):
        u = max(0.0, 1 - ((x - cx) / hw) ** 2)
        return cy + (unten if unten_rand else -oben) * math.sqrt(u) * 0.86

    def zahn(bx, by, laenge, richtung):
        for i in range(laenge):
            breite = 3 if i < laenge - 1 else 1
            yy = by + richtung * i
            for j in range(breite):
                xx = bx - breite // 2 + j
                col = TOOTH_SH if j == 0 and breite == 3 else (TOOTH_HI if i == 0 and j == 1 else TOOTH)
                put(img, yy, xx, col)

    lang = int(round(2 + 3 * o))
    for t in (-0.58, 0.0, 0.58):
        bx = int(round(cx + t * hw))
        zahn(bx, int(round(rand_y(bx, False))), lang, 1)
    if o > 0.3:
        for t in (-0.36, 0.36):
            bx = int(round(cx + t * hw))
            zahn(bx, int(round(rand_y(bx, True))), lang - 1, -1)
    return hw, cy, top + tiefe


def zuengeln(img, cx, cy, hw, oben_y, k, staerke, hinter=False):
    """Feuerzungen, die oben und seitlich aus dem Maul schlagen (der Ansatz des Feuers)."""
    for i, t in enumerate((-0.85, -0.45, 0.0, 0.45, 0.85)):
        f = 0.5 + 0.5 * math.sin(k * 2.2 + i * 1.9)
        hoehe = (2 + 6 * f) * staerke * (0.6 if abs(t) > 0.7 else 1.0)
        bx = cx + t * hw
        by = oben_y + abs(t) * 3 + 1
        n = int(round(hoehe))
        for j in range(n):
            u = j / max(1, n)
            breite = 2 if u < 0.5 else 1
            sway = math.sin(k * 1.7 + i + j * 0.6) * u * 1.5 + t * j * 0.5
            col = EMBER_HOT if u < 0.35 else (EMBER if u < 0.7 else FIRE_RED)
            for b in range(breite):
                put(img, by - j, bx + sway + b - breite // 2, col, hinter)
    # ein paar Funken steigen auf
    for i in range(3):
        q = ((k / 6.0) + i / 3.0) % 1.0
        put(img, oben_y - 4 - q * 16, cx + (i - 1) * 7 + math.sin(q * 6 + i) * 3,
            EMBER_HOT if q < 0.5 else EMBER, hinter)


def randlicht(img, cx, cy, staerke, k):
    """Gegenlicht: Feuer VOR ihm, man sieht ihn von hinten. Die Raender von Stamm,
    Armen und Krone gluehen orange, je naeher am Maul, desto staerker."""
    if staerke <= 0:
        return
    deck = img[..., 3] > 0
    # Abstand zum Rand: 0 = Randpixel, 1 = eins weiter innen, ...
    rand = np.zeros(deck.shape, int) + 9
    innen = deck.copy()
    for d in range(3):
        nb = innen.copy()
        nb[1:, :] &= innen[:-1, :]
        nb[:-1, :] &= innen[1:, :]
        nb[:, 1:] &= innen[:, :-1]
        nb[:, :-1] &= innen[:, 1:]
        rand[innen & ~nb] = d
        innen = nb
    R = 70.0
    ys, xs = np.nonzero(deck & (rand < 3))
    for y, x in zip(ys, xs):
        r = math.hypot(x - cx, (y - cy) * 1.25)
        if r >= R:
            continue
        flacker = 0.85 + 0.15 * math.sin(x * 0.7 + y * 0.4 + k * 2.1)
        a = staerke * 0.95 * (1 - rand[y, x] / 3.0) * (1 - r / R) ** 0.6 * flacker
        col = EMBER_HOT if rand[y, x] == 0 and a > 0.5 else EMBER
        blend(img, y, x, col, a)


def glutsaum(img, cx, cy, staerke, k):
    """Feuerschein, der von vorn um ihn herum in die Luft leckt: ein flackernder
    Saum aus Glutpixeln direkt ausserhalb der Silhouette, nahe am Maul dichter."""
    if staerke <= 0:
        return
    deck = img[..., 3] > 0
    aussen1 = ~deck & (np.roll(deck, 1, 0) | np.roll(deck, -1, 0) | np.roll(deck, 1, 1) | np.roll(deck, -1, 1))
    ring2 = deck | aussen1
    aussen2 = ~ring2 & (np.roll(ring2, 1, 0) | np.roll(ring2, -1, 0) | np.roll(ring2, 1, 1) | np.roll(ring2, -1, 1))
    R = 60.0
    for maske, col, faktor in ((aussen1, EMBER, 1.0), (aussen2, FIRE_RED, 0.55)):
        ys, xs = np.nonzero(maske)
        for y, x in zip(ys, xs):
            r = math.hypot(x - cx, (y - cy) * 1.2)
            if r >= R or y > cy - 24:
                continue
            nah = (1 - r / R) ** 0.7
            flacker = 0.5 + 0.5 * math.sin(x * 1.3 + y * 0.9 + k * 2.4)
            if flacker * nah * staerke * faktor * 1.6 > 0.35:
                put(img, y, x, EMBER_HOT if col is EMBER and nah > 0.7 and flacker > 0.8 else col)


def stichflamme(img, cx, cy, k, staerke):
    """Von hinten: das Feuer geht vor ihm hoch und schlaegt hinter der Krone heraus -
    Zungen, die von der oberen Kronenkante aufsteigen, dazu Funken darueber."""
    deck = img[..., 3] > 0
    for i, t in enumerate((-0.8, -0.45, -0.15, 0.15, 0.45, 0.8)):
        x0 = int(round(cx + t * 30))
        spalte = np.nonzero(deck[:int(cy), x0])[0]
        if len(spalte) == 0:
            continue
        kante = spalte[0]  # oberste deckende Zeile
        f = 0.5 + 0.5 * math.sin(k * 2.3 + i * 1.7)
        mitte = 1.0 - abs(t) * 0.5
        hoehe = (7 + 12 * f) * staerke * mitte
        n = int(round(hoehe)) + 3  # die ersten Zeilen stecken hinter der Krone
        for j in range(n):
            u = j / max(1, n)
            breite = 4 if u < 0.3 else (3 if u < 0.55 else (2 if u < 0.8 else 1))
            sway = math.sin(k * 1.6 + i + j * 0.5) * u * 1.8 + t * j * 0.4
            col = EMBER_HOT if u < 0.4 else (EMBER if u < 0.75 else FIRE_RED)
            for bb in range(breite):
                put(img, kante + 3 - j, x0 + sway + bb - breite // 2, col, hinter=True)
    for i in range(5):
        q = ((k / 6.0) + i / 5.0) % 1.0
        put(img, 18 - q * 16, cx + (i - 2) * 10 + math.sin(q * 6 + i) * 3,
            EMBER_HOT if q < 0.5 else EMBER, hinter=True)


def funken_sog(img, cx, cy, menge, k, hinter=False):
    """Glutfunken werden von allen Seiten ins Maul gesogen (Aufladen)."""
    n = int(round(12 * menge))
    for j in range(n):
        for back in (2, 1, 0):  # Schweif aus zwei aelteren Positionen
            q = ((k - back * 0.5) / 12.0 + j / 12.0) % 1.0
            r = 42 * (1 - q) ** 1.15 + 1
            ang = j * 2.399 + q * 2.2
            x = cx + r * math.cos(ang)
            y = cy + r * math.sin(ang) * 0.75
            if back:
                put(img, y, x, MAW_MID if back == 2 else FIRE_RED, hinter)
                continue
            col = EMBER if q < 0.6 else EMBER_HOT
            put(img, y, x, col, hinter)
            if q < 0.85:  # groesser, solange er noch weit weg ist
                put(img, y, x + 1, col, hinter)
                put(img, y + 1, x, col, hinter)
                put(img, y + 1, x + 1, FIRE_RED, hinter)


# ---------------------------------------------------------------- Umgebung

def blaetter(img, p, menge=1.0, wackeln=0.0):
    """Blaetter fallen in Schleife aus der Krone (loopt nahtlos)."""
    specs = [(0.00, 30, 1), (0.37, 88, -1), (0.71, 60, 1), (0.55, 44, -1), (0.18, 76, 1)]
    for off, x0, dirn in specs[:max(1, int(round(3 * menge)))]:
        q = (p + off) % 1.0
        y = 22 + q * 92
        x = x0 + dirn * (6 * math.sin(q * math.pi * 3) + q * 10) + wackeln
        if y > 118:
            continue
        flip = int(q * 12) % 2
        col_d, col_m, col_l = LEAF
        if flip:
            put(img, y, x, col_m); put(img, y, x + 1, col_l); put(img, y + 1, x, col_d)
        else:
            put(img, y, x, col_m); put(img, y + 1, x + 1, col_l); put(img, y, x + 1, col_d)


def wolke(img, fx, fy, t, weite=1.0):
    for side in (-1, 1):
        for i in range(2):
            r = (5.0 - i * 1.5) * (1.0 - t * 0.6)
            px = fx + side * (9 + t * 16 * weite + i * 8)
            py = fy - 3 - t * 6 - i * 2
            col = DUST[min(2, int(t * 3))]
            ri = int(math.ceil(r))
            for yy in range(-ri, ri + 1):
                for xx in range(-ri, ri + 1):
                    if xx * xx + yy * yy * 1.3 <= r * r + 0.5:
                        put(img, py + yy, px + xx, col)


FUESSE = ((OX + 30, OY + SRC_H - 1), (OX + SRC_W - 26, OY + SRC_H - 1))


def staub(img, p):
    """Staubwolken an dem Fuss, der gerade aufgesetzt hat (links bei p=0.5, rechts bei p=0)."""
    dauer = 0.26
    for land, (fx, fy) in ((0.5, FUESSE[0]), (0.0, FUESSE[1])):
        age = (p - land) % 1.0
        if age <= dauer:
            wolke(img, fx, fy, age / dauer)


# ---------------------------------------------------------------- Ablaeufe

class Boss:
    def __init__(self, hinten=False):
        self.hinten = hinten
        nat = np.array(Image.open(QUELLE).convert("RGBA"))
        vorn = scale2x(nat)
        self.src = scale2x(rueckseite(nat)) if hinten else vorn
        assert self.src.shape[:2] == (SRC_H, SRC_W), self.src.shape
        self.g = gewichte(SRC_W, SRC_H)
        self.eyes, self.mouth = finde_augen_maul(vorn)
        if hinten:
            # Alles gespiegelt: Einflussbereiche (Ranken, Arme, Beine) und - in
            # frame() - die x-Richtung der Bewegung. Ohne das verlagert er von
            # hinten das Gewicht auf das Bein, das er gerade hebt, und die Krone
            # schwingt zur falschen Seite.
            self.g = {k: np.ascontiguousarray(v[:, ::-1]) for k, v in self.g.items()}
            self.eyes = np.zeros_like(self.eyes)
            # Das Maul sieht man nicht, es zaehlt aber als Ziel fuer Glut und Licht
            self.mouth = np.ascontiguousarray(self.mouth[:, ::-1])

    def frame(self, m):
        dx, dy = feld(self.g, m)
        if self.hinten:
            dx = -dx
        img = warp(self.src, dx, dy)
        eyes = ziel(self.eyes, dx, dy)
        mouth = ziel(self.mouth, dx, dy)
        ys = [p[0] for p in mouth] or [0]
        xs = [p[1] for p in mouth] or [0]
        mund = ((min(xs) + max(xs)) / 2.0, min(ys), (min(ys) + max(ys)) / 2.0)
        if self.hinten:
            mouth = []  # nicht zeichnen
        return img, eyes, mouth, mund


def walk(boss):
    frames = []
    n = 12
    for k in range(n):
        p = k / n
        s = math.sin(2 * math.pi * p)

        def lift(q):
            # Schwungbein: schnell hoch, schwer runter (Stampfer)
            return 0.0 if q >= 0.5 else 10.0 * math.sin(math.pi * (q / 0.5) ** 0.8)

        # Krone folgt dem Rumpf mit Verzoegerung (Nachschwingen), auf ganze Pixel
        lag = 0.12
        sl = math.sin(2 * math.pi * (p - lag))
        rumpf_dy = 2.4 - 5.0 * abs(s) ** 0.7
        # Krone wippt weich nach: am tiefsten kurz nach jedem Aufstampfen
        krone_dy = 1.0 * math.cos(4 * math.pi * (p - lag)) - 0.5
        m = pose(fussL=-lift(p), fussR=-lift((p + 0.5) % 1.0),
                 rumpf_dy=rumpf_dy, rumpf_dx=1.6 * s,
                 # Krone bekommt eine eigene glatte Bahn (absolut, nicht gerundet): da sie
                 # starr ist, rundet sie als Ganzes und rueckt in 1-px-Schritten
                 krone_dx=2.0 * sl - 1.6 * s,
                 krone_dy=krone_dy - rumpf_dy,
                 armL=4.0 * math.sin(2 * math.pi * (p - 0.06)),
                 armR=-4.0 * math.sin(2 * math.pi * (p - 0.06)),
                 armL_dx=-1.2 * math.cos(2 * math.pi * p), armR_dx=1.2 * math.cos(2 * math.pi * p))
        img, eyes, mouth, _ = boss.frame(m)
        pulse = 0.5 + 0.5 * math.sin(2 * math.pi * p * 2)
        augen(img, eyes, 0.3 + 0.4 * pulse, k)
        maul_zu(img, mouth, pulse, k)
        staub(img, p)
        blaetter(img, p)
        frames.append(img)
    return frames


# Haltung am Ende des Aufladens und beim Bruellen
POSE_GELADEN = pose(rumpf_dy=-2.0, krone_dy=-2.0, armL=-9.0, armR=-9.0, armL_dx=-2.0, armR_dx=2.0)
POSE_BRUELL = pose(rumpf_dy=2.5, krone_dy=1.0, armL=3.0, armR=3.0, armL_dx=-3.0, armR_dx=3.0)


# Zitter-/Bebenmuster (6er-Schleife, ganze Pixel)
ZITTERN = (1, -1, 0, 1, -1, 0)
BEBEN_Y = (0, 1, 0, 0, 1, 0)


def charge(boss):
    """2 s: holt Luft, Arme gehen hoch, Funken werden ins Maul gesogen, zum Ende zittert er."""
    frames = []
    n = 24
    for k in range(n):
        t = k / (n - 1)
        e = t * t * (3 - 2 * t)
        m = mischen(pose(), POSE_GELADEN, e)
        # Atmen am Anfang, Zittern zum Ende hin
        m["rumpf_dy"] += round(0.8 * math.sin(t * math.pi * 3)) if t < 0.4 else 0
        if t > 0.5:
            # Zittern: Rumpf ruckt, Krone folgt einen Frame spaeter (gleicher Weg)
            amp = 2 if t > 0.85 else 1
            jetzt = ZITTERN[k % len(ZITTERN)] * amp
            vorher = ZITTERN[(k - 1) % len(ZITTERN)] * amp
            m["rumpf_dx"] += jetzt
            m["krone_dx"] += vorher - jetzt
            m["krone_dy"] += -1 if k % 3 == 0 else 0  # Blaetter beben
            m["armL"] += jetzt
            m["armR"] -= jetzt
        for key in ("rumpf_dx", "rumpf_dy", "krone_dx", "krone_dy"):
            m[key] = round(m[key])
        img, eyes, mouth, (cx, top, cy) = boss.frame(m)
        glut = 0.3 + 1.1 * e
        if boss.hinten:
            # Von hinten: die Glut vorn leuchtet ihm um die Raender, die Funken
            # verschwinden hinter dem Stamm
            randlicht(img, cx, cy - 26, max(0.0, e - 0.1) * 0.9, k)
            if e > 0.6:
                glutsaum(img, cx, cy, (e - 0.6) / 0.4 * 0.5, k)
            funken_sog(img, cx, cy, min(1.0, t * 1.5), k, hinter=True)
        else:
            gesichtslicht(img, int(cx), int(cy), 10 + 8 * e, max(0.0, e - 0.25))
            augen(img, eyes, 0.3 + 1.1 * e, k)
            maul_zu(img, mouth, glut, k)
            funken_sog(img, cx, cy, min(1.0, t * 1.5), k)
        blaetter(img, k / n, menge=1.0 + e, wackeln=(k % 2) * (1 if t > 0.5 else 0))
        frames.append(img)
    return frames


def roar_frame(boss, m, o, k, zungen):
    img, eyes, mouth, (cx, top, cy) = boss.frame(m)
    if boss.hinten:
        # Das Feuer geht von ihm weg: man sieht den Schein ringsum und Zungen,
        # die seitlich am Stamm vorbeischlagen
        randlicht(img, cx, cy - 26, 0.8 + 0.4 * o, k)
        glutsaum(img, cx, cy, 0.5 + 0.5 * o, k)
        stichflamme(img, cx, cy, k, 0.4 + 0.6 * o)
        return img, (cx, cy)
    gesichtslicht(img, int(cx), int(cy + 4 * o), 14 + 8 * o, 0.5 + 0.5 * o)
    augen(img, eyes, 1.2 + 0.3 * o + 0.1 * (k % 2), k)
    maul_zu(img, mouth, 1.4, k)  # bleibt unter dem offenen Maul verdeckt
    hw, mcy, _ = maul_offen(img, cx, top - 1, o, k)
    if zungen > 0:
        zuengeln(img, cx, mcy, hw, top - 1, k, zungen)
    return img, (cx, mcy)


def roar(boss):
    """4 Frames: Maul reisst auf, Oberkoerper wirft sich nach vorn."""
    frames = []
    for i, o in enumerate((0.3, 0.62, 0.9, 1.0)):
        u = 1 - (1 - (i + 1) / 4.0) ** 2
        m = mischen(POSE_GELADEN, POSE_BRUELL, u)
        # Krone schwingt ueber (Ueberschwung im 3. Frame)
        m["krone_dy"] += (2 if i == 2 else 0)
        for key in ("rumpf_dx", "rumpf_dy", "krone_dx", "krone_dy"):
            m[key] = round(m[key])
        img, _ = roar_frame(boss, m, o, i, 0.0 if i < 2 else 0.5 * (i - 1))
        frames.append(img)
    return frames


def roar_loop(boss):
    """6 Frames Schleife: Maul offen, Feuer zuengelt, ganzer Baum bebt."""
    frames = []
    mund = None
    n = 6
    for k in range(n):
        jetzt = ZITTERN[k % n]
        vorher = ZITTERN[(k - 1) % n]
        m = dict(POSE_BRUELL)
        # Rueckstoss-Beben: Krone geht denselben Weg einen Frame spaeter
        m["rumpf_dx"] += jetzt
        m["krone_dx"] += vorher - jetzt
        m["rumpf_dy"] += BEBEN_Y[k % n]
        m["krone_dy"] += BEBEN_Y[(k - 1) % n] - BEBEN_Y[k % n]
        m["armL"] += 1.5 * math.sin(2 * math.pi * k / n)
        m["armR"] += 1.5 * math.sin(2 * math.pi * k / n + math.pi)
        o = 1.0 if k % 3 else 0.94
        img, mund = roar_frame(boss, m, o, k, 1.0)
        frames.append(img)
    return frames


# ---------------------------------------------------------------- Ausgabe

def schreibe(name, frames):
    os.makedirs(OUT_DIR, exist_ok=True)
    path = os.path.join(OUT_DIR, name + ".png")
    sheet = Image.new("RGBA", (CELL * len(frames), CELL))
    for i, f in enumerate(frames):
        sheet.paste(Image.fromarray(f), (i * CELL, 0))
    sheet.save(path)
    meta = path + ".meta"
    if not os.path.exists(meta):
        sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
        from unity_meta import write_strip_meta
        write_strip_meta(meta, name, len(frames), CELL, CELL, PPU, pivot=PIVOT)
    print("%-22s %2d Frames  %s" % (name, len(frames), path))


def maul_pixel(boss):
    """Mitte des offenen Mauls im Bruell-Frame (Frame-Pixel, von oben links)."""
    m = dict(POSE_BRUELL)
    for key in ("rumpf_dx", "rumpf_dy", "krone_dx", "krone_dy"):
        m[key] = round(m[key])
    _, mund = roar_frame(boss, m, 1.0, 0, 0.0)
    return mund


def main():
    args = sys.argv[1:]
    if "--quelle" in args:
        aus_ki_bild(args[args.index("--quelle") + 1]).save(QUELLE)
        print("Quelle geschrieben:", QUELLE)
    boss = Boss()
    hinten = Boss(hinten=True)
    anims = {
        "baumboss_walk": walk(boss),
        "baumboss_walk_back": walk(hinten),
        "baumboss_charge": charge(boss),
        "baumboss_roar": roar(boss),
        "baumboss_roar_loop": roar_loop(boss),
        "baumboss_charge_back": charge(hinten),
        "baumboss_roar_back": roar(hinten),
        "baumboss_roar_loop_back": roar_loop(hinten),
    }
    for name, frames in anims.items():
        schreibe(name, frames)
    mx, my = maul_pixel(boss)
    print("Maul (Frame-Pixel): x %.1f  y %.1f  ->  lokal (%.3f, %.3f) Einheiten ueber dem Pivot"
          % (mx, my, (mx - CELL * PIVOT[0]) / PPU, (CELL * (1 - PIVOT[1]) - my) / PPU))

    if "--preview" in args:
        vorschau(anims, (mx, my), args[args.index("--preview") + 1])


def boden(w, h):
    bg = Image.new("RGBA", (w, h), (58, 74, 52, 255))
    px = bg.load()
    for y in range(h):
        for x in range(w):
            if (x * 7 + y * 13 + (x // 5) * (y // 3)) % 23 == 0:
                px[x, y] = (70, 90, 60, 255)
    for y in range(h - 16, h - 2):
        for x in range(18, 110):
            if ((x - 64) / 44.0) ** 2 + ((y - (h - 9)) / 5.5) ** 2 <= 1:
                px[x, y] = (40, 52, 38, 255)
    return bg


def vorschau(anims, maul, path, scale=3):
    """GIF: Laufen, Aufladen, Bruellen + Flammenwurf, Maul zu - als eine Szene."""
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import baumboss_feuer as feuer
    fire = feuer.build()

    W, H = 360, CELL
    bg = boden(W, H)
    seq = []  # (boss_frame, flammenlaenge oder None, feuer_k)
    for _ in range(2):
        seq += [(f, None) for f in anims["baumboss_walk"]]
    seq += [(f, None) for f in anims["baumboss_charge"]]
    seq += [(f, None) for f in anims["baumboss_roar"]]
    loop = anims["baumboss_roar_loop"]
    voll = W - int(maul[0]) - 4
    for i in range(30):
        laenge = min(voll, int(voll * (i + 1) / 4.0))  # waechst in ~0.3 s auf volle Laenge
        if i >= 26:
            laenge = int(voll * (30 - i) / 5.0)
        seq.append((loop[i % len(loop)], laenge))
    seq += [(f, None) for f in reversed(anims["baumboss_roar"])]

    imgs = []
    for k, (f, laenge) in enumerate(seq):
        im = bg.copy()
        im.alpha_composite(Image.fromarray(f), (0, 0))
        if laenge:
            strahl = feuer.strahl(fire, laenge, k)
            sx = int(round(maul[0]))
            sy = int(round(maul[1])) - strahl.height // 2
            im.alpha_composite(strahl, (sx, sy))
        imgs.append(im.convert("RGB").resize((W * scale, H * scale), Image.NEAREST))
    imgs[0].save(path, save_all=True, append_images=imgs[1:], duration=int(1000 / FPS), loop=0)
    print("Vorschau:", path)


if __name__ == "__main__":
    main()
