"""
Der Verkohlte (Zwischenboss der Dunkelwelt): ein im Ofen vergessener Keks.
Schwarze Holzkohle-Kruste, darunter glueht er noch - Risse, geschmolzene
Schokostuecke und eine abgebrochene Ecke, aus der Flammen schlagen.

  Assets/Art/Gegner/new/boss/
    verkohlter_idle.png            12 Bilder  atmet, Glut pulsiert von innen nach aussen (Schleife)
    verkohlter_walk.png            12 Bilder  watschelt, zwei Schritte (Schleife)
    verkohlter_ausbruch.png        22 Bilder  Glutausbruch: laedt auf, Kruste platzt auf,
                                              Blitz + Druckwelle, Kruste schnappt zurueck
    verkohlter_asche_weg.png       16 Bilder  zerfaellt von oben in Asche und Funken (Teleport)
    verkohlter_asche_da.png        16 Bilder  setzt sich aus Funken wieder zusammen
    verkohlter_schatten_idle.png   12 Bilder  Schattenklon (violette Void-Glut statt Feuer)
    verkohlter_schatten_walk.png   12 Bilder
    verkohlter_tod.png             24 Bilder  ueberhitzt, Kruste fliegt weg, Aschehaufen glimmt aus
  alle 128x128, PPU 32, 12 fps, Pivot = Mitte der Bodenlinie.

Aufbau eines Bildes (alles in Koerper-Koordinaten, damit Muster beim
Stauchen/Strecken am Keks kleben):
  Kruste    Holzkohle-Schollen (feines Voronoi), Licht von links oben,
            violettes Randlicht rechts unten (Void-Welt)
  Glut      grobe Risse (Voronoi-Kanten, verwackelt), Hitzewelle laeuft vom
            Kern nach aussen, Glut faerbt die Kruste daneben rot an
  Gesicht   Stempel, je Bild ein Ausdruck
  Effekte   Flammen aus der Bruchstelle, Funken, Rauch - alles periodisch,
            damit die Schleifen nahtlos sind

Aufruf aus dem Projektordner:
  python Tools/verkohlter.py [--preview pfad.gif] [--nur vorschau]
"""

import math
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TOOLS = os.path.join(ROOT, "Tools")
OUT_DIR = os.path.join(ROOT, "Assets", "Art", "Gegner", "new", "boss")
BESTIARY = os.path.join(ROOT, "Assets", "Resources", "Bestiary", "Verkohlter.png")

CELL = 128
GROUND = CELL - 4            # erste Zeile UNTER den Fuessen
CX = 64
PPU = 32
FPS = 12
PIVOT = (CX / CELL, 4 / CELL)

R = 29.0                     # Koerperradius in Ruhe
FOOT = 4                     # Fuesse unter dem Koerper
BOTTOM = GROUND - FOOT + 1   # Unterkante Koerper

TAU = math.pi * 2


def rgb(h, a=255):
    h = h.lstrip("#")
    return np.array((int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a), np.uint8)


# Holzkohle, kalt-warm
C = [rgb("#0a0608"),   # 0 Umriss
     rgb("#170f12"),   # 1 Fugen
     rgb("#211719"),   # 2 Schatten
     rgb("#2e2120"),   # 3 Grund
     rgb("#3d2c28"),   # 4 Licht
     rgb("#544036"),   # 5 Kante oben links
     rgb("#6e5444")]   # 6 Glanzkante
# Kruste, von der Glut angestrahlt
E = [rgb("#2c1210"), rgb("#4a1a12"), rgb("#6e2414")]
# Violettes Randlicht
V = [rgb("#2c2148"), rgb("#4f3a82"), rgb("#7a5cc0")]

FIRE = [rgb("#3d0907"),  # 0 erkaltet
        rgb("#7e1608"),  # 1
        rgb("#c8350c"),  # 2
        rgb("#f56d17"),  # 3
        rgb("#ffb43a"),  # 4
        rgb("#ffe68a"),  # 5
        rgb("#fffbea")]  # 6 weissglut
VOID = [rgb("#1a0b2e"),
        rgb("#3a1470"),
        rgb("#6420b8"),
        rgb("#9446ee"),
        rgb("#bb7cff"),
        rgb("#dcb8ff"),
        rgb("#f8f0ff")]
VOID_E = [rgb("#1d1230"), rgb("#2d1650"), rgb("#43207a")]

SMOKE = [rgb("#5a4f5c", 170), rgb("#463c4a", 130), rgb("#352d3a", 90)]

# ------------------------------------------------------------------ Rauschen

def hash2(ix, iy, seed=0):
    h = (ix * 374761393 + iy * 668265263 + seed * 1442695041) & 0xFFFFFFFF
    h = ((h ^ (h >> 13)) * 1274126177) & 0xFFFFFFFF
    return ((h ^ (h >> 16)) & 0xFFFF) / 65535.0


def vnoise(x, y, seed=0):
    ix = np.floor(x).astype(np.int64)
    iy = np.floor(y).astype(np.int64)
    fx = x - ix
    fy = y - iy
    fx = fx * fx * (3 - 2 * fx)
    fy = fy * fy * (3 - 2 * fy)
    a = hash2(ix, iy, seed)
    b = hash2(ix + 1, iy, seed)
    c = hash2(ix, iy + 1, seed)
    d = hash2(ix + 1, iy + 1, seed)
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def fbm(x, y, seed=0, oct=3):
    s, a, n = 0.0, 0.5, 0.0
    for o in range(oct):
        s = s + a * vnoise(x, y, seed + o * 17)
        n += a
        a *= 0.5
        x = x * 2.03
        y = y * 2.03
    return s / n


def seeds(spacing, extent, seed, jitter=0.8):
    rng = np.random.RandomState(seed)
    pts = []
    k = int(extent / spacing) + 1
    for j in range(-k, k + 1):
        for i in range(-k, k + 1):
            pts.append(((i + 0.5 + (rng.rand() - 0.5) * jitter) * spacing,
                        (j + 0.5 + (rng.rand() - 0.5) * jitter) * spacing))
    return np.array(pts)


FINE = seeds(6.5, R + 8, 3, 0.9)
COARSE = seeds(15.0, R + 10, 11, 0.95)
_rng = np.random.RandomState(5)
ACTIVE = _rng.rand(len(COARSE), len(COARSE)) < 0.45
ACTIVE = ACTIVE | ACTIVE.T


def voronoi(u, v, pts):
    d = (u[..., None] - pts[:, 0]) ** 2 + (v[..., None] - pts[:, 1]) ** 2
    idx = np.argsort(d, axis=-1)[..., :2]
    d1 = np.sqrt(np.take_along_axis(d, idx[..., :1], -1)[..., 0])
    d2 = np.sqrt(np.take_along_axis(d, idx[..., 1:2], -1)[..., 0])
    return d1, d2, idx[..., 0], idx[..., 1]


# ------------------------------------------------------------------ Form

NOTCH_A = -0.92        # Bruchstelle oben rechts (v zeigt nach unten)
NOTCH_W = 0.42
NOTCH_D = 7.5


def notch_profile(th):
    d = (th - NOTCH_A + math.pi) % TAU - math.pi
    p = np.clip(1 - (d / NOTCH_W) ** 2, 0, None)
    jag = (np.floor((d + 1) * 9) % 3) * 0.6
    return np.where(p > 0, p ** 0.6 * NOTCH_D + jag * (p > 0.15), 0.0)


def redge(th):
    r = R * (1 + 0.020 * np.cos(3 * th + 0.4) + 0.016 * np.cos(5 * th + 1.9)
             + 0.012 * np.cos(7 * th + 0.7) + 0.010 * np.cos(11 * th + 2.2))
    # Brandkante: ein paar abgebroeselte Stufen
    r = r - 0.9 * (np.cos(13 * th + 1.3) > 0.82)
    return r - notch_profile(th)


# geschmolzene Schokostuecke (Koerper-Koordinaten, Radius)
CHIPS = [(-17, -12, 2.8), (6, -20, 2.2), (-22, 6, 2.4), (18, 9, 2.6), (-6, 19, 2.3), (13, -6, 1.7), (-3, -24, 1.6)]

EYE_V = -3
EYE_U = 9

# ------------------------------------------------------------------ Koerper

YY, XX = np.mgrid[0:CELL, 0:CELL].astype(np.float64)
XX += 0.5
YY += 0.5

LIGHT = np.array([-0.55, -0.75, 0.62])
LIGHT /= np.linalg.norm(LIGHT)


def pose(**kw):
    P = dict(t=0.0, sx=1.0, sy=1.0, lean=0.0, dx=0.0, dy=0.0, heat=1.0, pulse=1.0,
             eye_open=1.0, anger=1.0, mouth_open=0.4, feet=(0.0, 0.0), arms=(0.0, 0.0),
             void=False, shake=(0, 0), flames=1.0, embers=1.0, smoke=1.0, flash=0.0,
             crack_boost=0.0)
    P.update(kw)
    return P


def to_body(P, X=XX, Y=YY):
    dx = P["dx"] + P["shake"][0]
    dy = P["dy"] - P["shake"][1]
    h = (BOTTOM - dy) - Y
    u = (X - CX - dx - P["lean"] * h) / P["sx"]
    v = (Y - (BOTTOM - dy)) / P["sy"] + R
    return u, v


def to_screen(P, u, v):
    dx = P["dx"] + P["shake"][0]
    dy = P["dy"] - P["shake"][1]
    Y = (v - R) * P["sy"] + (BOTTOM - dy)
    h = (BOTTOM - dy) - Y
    X = u * P["sx"] + CX + dx + P["lean"] * h
    return X, Y


def neighbours_out(mask):
    m = np.pad(mask, 1)
    return mask & ~(m[:-2, 1:-1] & m[2:, 1:-1] & m[1:-1, :-2] & m[1:-1, 2:])


def dilate(mask, n=1):
    m = mask.copy()
    for _ in range(n):
        p = np.pad(m, 1)
        m = m | p[:-2, 1:-1] | p[2:, 1:-1] | p[1:-1, :-2] | p[1:-1, 2:]
    return m


def body_layers(P):
    """Koerper ohne Gesicht/Effekte. Gibt Bild, Maske und Schollen-Id (fuer Ausbruch/Tod) zurueck."""
    pal = VOID if P["void"] else FIRE
    epal = VOID_E if P["void"] else E
    t = P["t"]
    u, v = to_body(P)
    th = np.arctan2(v, u)
    rr = np.hypot(u, v)
    re = redge(th)
    mask = rr < re

    img = np.zeros((CELL, CELL, 4), np.uint8)

    # Glieder hinter dem Koerper: Arme und Fuesse
    limbs = np.zeros((CELL, CELL), bool)
    limb_lum = np.zeros((CELL, CELL))
    for side, k in ((-1, 0), (1, 1)):
        # Fuss
        fx = CX + P["dx"] + P["shake"][0] + side * 11 + side * 0.5 * (P["sx"] - 1) * 20
        lift = P["feet"][k]
        fy = GROUND - 2.5 - lift
        e = ((XX - fx) / 6.5) ** 2 + ((YY - fy) / 3.2) ** 2
        m = e < 1
        limbs |= m
        limb_lum = np.where(m, -0.25 - 0.5 * ((YY - fy) / 3.2), limb_lum)
        # Arm (Stummel an der Seite, schwingt)
        au, av = side * (R - 1.5), 7 + P["arms"][k]
        ax, ay = to_screen(P, au, av)
        ax += side * 2.5
        e = ((XX - ax) / 4.2) ** 2 + ((YY - ay) / 5.0) ** 2
        m = e < 1
        limbs |= m
        limb_lum = np.where(m, -0.1 - 0.45 * ((YY - ay) / 5.0) - 0.3 * side * ((XX - ax) / 4.2), limb_lum)
    limbs &= ~mask

    # --- Kruste
    edge = np.clip((rr / re - 0.55) / 0.45, 0, 1)
    nx = u / np.maximum(rr, 1e-6) * edge ** 1.5
    ny = v / np.maximum(rr, 1e-6) * edge ** 1.5
    nz = np.sqrt(np.clip(1 - nx ** 2 - ny ** 2, 0, 1))
    lum = nx * LIGHT[0] + ny * LIGHT[1] + nz * LIGHT[2]

    d1, d2, i1, i2 = voronoi(u, v, FINE)
    cellv = np.array([hash2(i, 7, 1) for i in range(len(FINE))])[i1]
    seam = (d2 - d1) < 0.9
    # Schollen: Kante oben links jeder Scholle heller (Facetten)
    su = u - FINE[i1, 0]
    sv = v - FINE[i1, 1]
    facet = -(su * 0.55 + sv * 0.8) / 6.5
    tone = lum * 2.6 + (cellv - 0.5) * 0.9 + facet * 0.7 + (fbm(u * 0.5, v * 0.5, 4) - 0.5) * 0.8
    band = np.digitize(tone, [0.1, 0.85, 1.55, 2.15, 2.6]) + 1   # 1..6
    band = np.where(seam, np.maximum(band - 2, 1), band)
    band = np.clip(band, 1, 6)

    # --- Glutrisse (verwackelte grobe Voronoi-Kanten)
    wu = u + (fbm(u * 0.18, v * 0.18, 21) - 0.5) * 7
    wv = v + (fbm(u * 0.18 + 9, v * 0.18, 22) - 0.5) * 7
    e1, e2, j1, j2 = voronoi(wu, wv, COARSE)
    act = ACTIVE[j1, j2]
    width = 1.05 + 0.6 * (1 - np.clip(rr / R, 0, 1)) + P["crack_boost"]
    gap = e2 - e1
    crack = act & (gap < width) & mask
    # Gesicht frei halten
    face = (np.abs(u) < 17) & (v > EYE_V - 8) & (v < 17)
    crack &= ~face
    # Bruchstelle: geschmolzener Rand
    notch_d = re - rr
    nd = (th - NOTCH_A + math.pi) % TAU - math.pi
    wound = mask & (np.abs(nd) < NOTCH_W * 1.05) & (notch_d < 3.2) & (notch_profile(th) > 0.5)

    core = 1 - np.clip(rr / R, 0, 1)
    pulse = 0.5 + 0.5 * np.sin(TAU * (t - (1 - core) * 0.9))
    heat = (0.25 + 0.45 * (1 - gap / width) + 0.35 * core + 0.3 * pulse * P["pulse"]) * P["heat"]

    # Schokostuecke: geschmolzen
    chip = np.zeros_like(mask)
    chip_heat = np.zeros_like(u)
    chip_rim = np.zeros_like(mask)
    for (cu, cv, cr) in CHIPS:
        dd = np.hypot((u - cu) * 1.0, (v - cv) * 1.15) + (fbm(u * 0.9, v * 0.9, 31) - 0.5) * 1.4
        m = (dd < cr) & mask
        chip |= m
        chip_rim |= (dd < cr + 1.0) & ~m & mask
        ph = 0.5 + 0.5 * np.sin(TAU * (t + cu * 0.03 + cv * 0.02))
        chip_heat = np.where(m, (1.15 - dd / cr * 0.75 + 0.15 * ph * P["pulse"]) * P["heat"], chip_heat)

    hot = crack | wound | chip
    # Glut faerbt die Kruste daneben an
    near1 = dilate(hot, 1) & mask & ~hot
    near2 = dilate(hot, 2) & mask & ~hot & ~near1

    out = neighbours_out(mask | limbs)

    # --- zusammensetzen
    for b in range(1, 7):
        img[mask & (band == b)] = C[b]
    glow = P["heat"] * (0.6 + 0.4 * pulse)
    img[near2 & (glow > 0.55)] = epal[0]
    img[near1 & (glow > 0.35)] = epal[1]
    img[near1 & (glow > 0.85) & (band >= 4)] = epal[2]

    hk = np.clip(np.floor(heat * 4.2), 0, 6).astype(int)
    for k in range(7):
        img[crack & (hk == k)] = pal[k]
    wk = np.clip(np.floor((1.1 - notch_d / 3.2 * 0.6 + 0.25 * pulse) * P["heat"] * 4.6), 0, 6).astype(int)
    for k in range(7):
        img[wound & (wk == k)] = pal[k]
    img[chip_rim & ~hot] = pal[0]
    ck = np.clip(np.floor(chip_heat * 5.0), 0, 6).astype(int)
    for k in range(7):
        img[chip & (ck == k)] = pal[k]

    # Glieder
    lb = np.clip(np.digitize(limb_lum, [-0.75, -0.35, 0.05, 0.4]) + 1, 1, 5)
    for b in range(1, 6):
        img[limbs & (lb == b)] = C[b]
    lin = limbs & ~out
    lrim = neighbours_out(lin) & lin & (limb_lum < -0.45)
    img[lrim] = V[1]

    # violettes Randlicht unten rechts (nur innen am Umriss)
    rim = neighbours_out(mask & ~out) & mask & ~hot
    rl = (u * 0.45 + v * 0.9) / np.maximum(rr, 1e-6)
    img[rim & (rl > 0.55)] = V[1]
    img[rim & (rl > 0.8)] = V[2]
    rim2 = neighbours_out(mask & ~out & ~rim) & mask & ~hot
    img[rim2 & (rl > 0.8)] = V[0]
    # helle Kante oben links (Feuerschein der Bruchstelle)
    img[rim & (rl < -0.75) & (band >= 3)] = C[6]

    img[out] = C[0]

    full = mask | limbs
    # Schollen-Id fuer Ausbruch/Tod: grobe Zellen, unverwackelt
    _, _, piece, _ = voronoi(u, v, COARSE)
    return img, full, piece, (u, v), hot


def face(img, P, pal):
    """Augen + Maul per Formel: schraeg abgeschnittene Glutaugen unter Kohle-Brauen,
    zackiges Grinsen mit Kohle-Zaehnen. P: eye_open, anger, mouth_open."""
    ex, ey = to_screen(P, 0, EYE_V)
    ex = math.floor(ex + 0.5)
    ey = math.floor(ey + 0.5)
    hot = min(1.0, P["heat"] + 0.3)
    lay = np.zeros((CELL, CELL), np.int8) - 1        # -1 nichts, 0..6 Glut, 10+ Kruste
    for side in (-1, 1):
        cx = ex + side * EYE_U + 0.5
        cy = ey + 0.5
        s = (XX - cx) * -side                         # positiv = zur Gesichtsmitte
        y = YY - cy
        op = P["eye_open"]
        ell = (s / 4.8) ** 2 + (y / (3.6 * max(op, 0.25))) ** 2 < 1
        cut = -3.6 + (s + 4.8) * 0.5 * P["anger"]     # Oberkante faellt zur Mitte hin
        eye = ell & (y > cut - (1 - op) * 0) & (y > -3.6 * op)
        hd = np.hypot((s - 0.8) / 4.8, (y - 1.0) / 3.4)
        k = np.clip(np.floor((1.25 - hd) * 4.6 * hot), 1, 6).astype(int)
        k = np.where(eye & (np.abs(s - 0.2) < 0.9) & (np.abs(y + 0.2) < 0.9) & (op > 0.5), 6, k)
        sock = dilate(eye, 1) & ~eye
        lay[sock & (lay < 0)] = 10
        lay[eye] = k[eye]
        # Braue: Kohlewulst ueber der Schnittkante, innen tiefer
        bw = (s > -6.5) & (s < 6.0)
        brow = bw & (y <= cut - 1.0) & (y > cut - 3.6) & ~eye
        top = bw & (y <= cut - 2.6) & (y > cut - 3.6)
        lay[brow & (lay < 0)] = 13
        lay[top & brow] = 15
    # Maul
    mo = P["mouth_open"]
    mx = ex + 0.5
    my = ey + 8.5
    hw = 10.5 + 1.5 * mo
    xn = (XX - mx) / hw
    inx = np.abs(xn) <= 1
    yt = my - 1.5 + 2.5 * (1 - xn ** 2) - 1.5 * mo * (1 - xn ** 2)
    yl = yt + 0.9 + mo * 7.5 * np.clip(1 - xn ** 2, 0, 1) ** 0.7 + 0.8 * (1 - xn ** 2)
    mouth = inx & (YY > yt) & (YY < yl)
    rel = np.clip((YY - yt) / np.maximum(yl - yt, 1), 0, 1)
    k = np.clip(np.floor((0.5 + rel * 0.9 + (1 - np.abs(xn)) * 0.5) * 4 * hot), 1, 6).astype(int)
    # Zaehne: Kohle-Dreiecke von oben, kleinere von unten
    ph = np.abs(((XX - mx) % 3.4) - 1.7)
    teeth = mouth & (YY - yt < 2.2 - ph * 1.2) & (np.abs(xn) < 0.9)
    bteeth = mouth & (yl - YY < 1.4 - ph * 0.9) & (np.abs(xn) < 0.75) & (mo > 0.3)
    sock = dilate(mouth, 1) & ~mouth
    lay[sock & ((lay < 0) | (lay >= 10))] = 10
    lay[mouth] = k[mouth]
    lay[teeth | bteeth] = 14
    for kk in range(7):
        img[lay == kk] = pal[kk]
    img[lay == 10] = C[0]
    img[lay == 13] = C[2]
    img[lay == 14] = C[3]
    img[lay == 15] = C[5]


# ------------------------------------------------------------------ Effekte

def put(img, x, y, col):
    x = int(math.floor(x + 0.5))
    y = int(math.floor(y + 0.5))
    if 0 <= x < CELL and 0 <= y < CELL:
        if col[3] < 255 and img[y, x, 3] > 0:
            a = col[3] / 255.0
            img[y, x, :3] = (img[y, x, :3] * (1 - a) + col[:3] * a).astype(np.uint8)
        else:
            img[y, x] = col


def notch_base(P):
    rb = R - NOTCH_D + 1.5
    return to_screen(P, rb * math.cos(NOTCH_A), rb * math.sin(NOTCH_A))


def flames(img, P, pal, t):
    """Flammenzungen aus der Bruchstelle. t in [0,1) - Schleife."""
    if P["flames"] <= 0:
        return
    bx, by = notch_base(P)
    tongues = [(-4.5, 13, 0.0, 4.2), (0.5, 19, 0.37, 5.0), (5.0, 11, 0.71, 3.6), (-1.5, 8, 0.2, 3.0)]
    layer = np.zeros((CELL, CELL), np.int8) - 1
    n = math.cos(NOTCH_A), math.sin(NOTCH_A)
    for (off, H, ph, Wd) in tongues:
        H = H * P["flames"] * (0.78 + 0.22 * math.sin(TAU * (t * 2 + ph)) + 0.08 * math.sin(TAU * (t * 3 + ph * 2)))
        base_x = bx + off * -n[1] * 0.9 + off * 0.25
        base_y = by + off * n[0] * 0.5 + 2
        for s in np.linspace(0, 1, 40):
            hgt = s * H
            sway = (2.2 * math.sin(TAU * (t * 2 + ph + s * 0.7)) + 1.2 * math.sin(TAU * (t * 3 - s))) * s ** 1.4
            cx = base_x + sway + s * 3.0 * P["flames"] - P["lean"] * hgt * 2
            cy = base_y - hgt
            w = Wd * (1 - s) ** 0.75 * (0.6 + 0.4 * math.sin(math.pi * min(1, s * 3 + 0.3)))
            for yy in range(int(cy - 1), int(cy + 2)):
                for xx in range(int(cx - w - 1), int(cx + w + 2)):
                    if not (0 <= xx < CELL and 0 <= yy < CELL):
                        continue
                    d = abs(xx + 0.5 - cx) / max(w, 0.35)
                    if d > 1 or abs(yy + 0.5 - cy) > 0.8:
                        continue
                    k = 2 + int((1 - d) * 3.2 + (1 - s) * 1.6)
                    k = min(k, 6)
                    if s > 0.7:
                        k = min(k, 3)
                    layer[yy, xx] = max(layer[yy, xx], k)
        # abreissende Flammenfetzen
        q = (t * 2 + ph) % 1
        fx = base_x + 2 * math.sin(TAU * (q + ph)) + q * 3
        fy = base_y - H - 2 - q * 8
        if q < 0.6:
            k = 4 if q < 0.25 else (3 if q < 0.45 else 2)
            for (ox, oy) in ((0, 0), (0, -1)) if q < 0.3 else ((0, 0),):
                xi, yi = int(fx + ox), int(fy + oy)
                if 0 <= xi < CELL and 0 <= yi < CELL:
                    layer[yi, xi] = max(layer[yi, xi], k)
    m = layer >= 0
    # Umriss der Flammen in dunklem Rot (nur wo nichts anderes ist)
    o = dilate(m, 1) & ~m & (img[..., 3] == 0)
    img[o] = pal[1]
    for k in range(7):
        img[layer == k] = pal[k]


def embers(img, P, pal, t, n=14, seed=7, src=None, spread=1.0):
    """Funken: steigen auf, schwanken, kuehlen ab. Periodisch in t."""
    if P["embers"] <= 0:
        return
    rng = np.random.RandomState(seed)
    for i in range(n):
        ph = rng.rand()
        su = (rng.rand() - 0.5) * 2 * R * 0.9 * spread
        sv = (rng.rand() - 0.7) * R * 0.9
        if src is not None and rng.rand() < 0.5:
            sx, sy = src
        else:
            sx, sy = to_screen(P, su, sv)
        speed = 26 + rng.rand() * 22
        q = (t + ph) % 1
        if q > 0.85 * P["embers"]:
            continue
        x = sx + math.sin(TAU * (q * 1.5 + ph)) * 3 + q * 6 * (rng.rand() - 0.3)
        y = sy - q * speed
        k = 6 if q < 0.12 else 5 if q < 0.3 else 4 if q < 0.5 else 3 if q < 0.7 else 2
        put(img, x, y, pal[k])
        if q < 0.35 and i % 3 == 0:
            put(img, x, y + 1, pal[max(k - 2, 1)])


def smoke(img, P, t, n=5, seed=3):
    if P["smoke"] <= 0:
        return
    bx, by = notch_base(P)
    rng = np.random.RandomState(seed)
    lay = np.zeros((CELL, CELL, 4), np.uint8)
    for i in range(n):
        ph = i / n + rng.rand() * 0.05
        q = (t + ph) % 1
        x = bx + 6 + q * 14 + math.sin(TAU * (q + ph)) * 3
        y = by - 14 - q * 34
        r = 1.5 + q * 3.5
        k = 0 if q < 0.35 else 1 if q < 0.7 else 2
        if q > 0.92:
            continue
        for yy in range(int(y - r - 1), int(y + r + 2)):
            for xx in range(int(x - r - 1), int(x + r + 2)):
                if 0 <= xx < CELL and 0 <= yy < CELL:
                    dd = math.hypot(xx + 0.5 - x, (yy + 0.5 - y) * 1.15)
                    if dd < r and lay[yy, xx, 3] == 0:
                        kk = k if dd < r * 0.65 else min(k + 1, 2)
                        lay[yy, xx] = SMOKE[kk]
                        lay[yy, xx, 3] = int(SMOKE[kk][3] * P["smoke"])
    # Rauch liegt hinter dem Keks
    under = img[..., 3] == 0
    img[under & (lay[..., 3] > 0)] = lay[under & (lay[..., 3] > 0)]


# ------------------------------------------------------------------ Bild

# Schattenklon: Kruste kalt-violett statt warm-braun
CV = [rgb("#07060c"), rgb("#110d1c"), rgb("#1a1428"), rgb("#251d38"), rgb("#33284a"), rgb("#45375f"), rgb("#5c4a7c")]


def kalt(img):
    for a, b in zip(C, CV):
        m = np.all(img == a, axis=-1)
        img[m] = b


def bild(P, fx=True):
    pal = VOID if P["void"] else FIRE
    img, mask, piece, uv, hot = body_layers(P)
    canvas = np.zeros_like(img)
    if fx:
        smoke(canvas, P, P["t"])
    a = img[..., 3] > 0
    canvas[a] = img[a]
    face(canvas, P, pal)
    if P["void"]:
        kalt(canvas)
    if fx:
        flames(canvas, P, pal, P["t"])
        embers(canvas, P, pal, P["t"], src=notch_base(P))
    if P["flash"] > 0:
        flash(canvas, P["flash"], pal)
    return canvas


def flash(img, f, pal):
    m = img[..., 3] > 200
    o = neighbours_out(m)
    k = 6 if f > 0.66 else 5
    img[m & ~o] = pal[k]
    img[o] = pal[3] if f > 0.66 else pal[2]


# ------------------------------------------------------------------ Animationen

def idle(void=False):
    fr = []
    for i in range(12):
        t = i / 12
        b = math.sin(TAU * t)
        fr.append(bild(pose(t=t, sx=1 - 0.018 * b, sy=1 + 0.03 * b, arms=(round(1.2 * b), round(1.2 * b)),
                            void=void)))
    return fr


def walk(void=False):
    fr = []
    for i in range(12):
        t = i / 12
        s = math.sin(TAU * t)
        lift = abs(math.sin(math.pi * 2 * t))
        fl = max(0, math.sin(TAU * t)) * 3
        fr_ = max(0, -math.sin(TAU * t)) * 3
        P = pose(t=t, dy=round(lift * 2.2), lean=0.045 * s, sx=1 + 0.03 * (1 - lift), sy=1 - 0.04 * (1 - lift),
                 feet=(round(fl), round(fr_)), arms=(round(-2 * s), round(2 * s)), void=void)
        fr.append(bild(P))
    return fr


def burst(void=False):
    """Glutausbruch: 0-9 laden, 10 Blitz, 11-15 Druckwelle, 16-21 zurueck."""
    N = 22
    fr = []
    rng = np.random.RandomState(4)
    offs = {}
    for i, (px, py) in enumerate(COARSE):
        d = math.hypot(px, py) + 1e-6
        offs[i] = (px / d, py / d, 0.7 + rng.rand() * 0.6)
    for i in range(N):
        t = (i / N) * 2 % 1
        if i < 10:
            q = i / 9
            sep = max(0, (q - 0.35) / 0.65) * 3.0
            P = pose(void=void, t=t, sx=1 + 0.08 * q, sy=1 - 0.12 * q, heat=1 + 0.9 * q, pulse=1 + q,
                     mouth_open=max(0.0, (q - 0.3) / 0.7),
                     shake=((i % 2) * 2 - 1 if q > 0.4 else 0, 0), flames=1 + 0.8 * q, crack_boost=0.9 * q,
                     arms=(-round(3 * q), -round(3 * q)), smoke=1 - q)
            fl = 0
        elif i == 10:
            sep = 4.0
            P = pose(void=void, t=t, sx=1.12, sy=1.1, heat=2.2, mouth_open=1.0, flames=2.2, crack_boost=1.2,
                     arms=(-5, -5), smoke=0)
            fl = 1.0
        elif i < 16:
            q = (i - 11) / 4
            sep = 4.0 * (1 - q) ** 2
            P = pose(void=void, t=t, sx=1.06 - 0.06 * q, sy=1.04 - 0.04 * q, heat=2.0 - 0.9 * q,
                     mouth_open=1 - 0.6 * q, flames=2.0 - q, crack_boost=1.0 * (1 - q), arms=(-4 + round(3 * q),) * 2, smoke=0)
            fl = 0
        else:
            q = (i - 16) / 5
            sep = 0
            b = math.sin(math.pi * q) * (1 - q)
            P = pose(void=void, t=t, sx=1 + 0.04 * b, sy=1 - 0.05 * b, heat=1.1 - 0.1 * q,
                     mouth_open=0.4 * (1 - q), flames=1.0, smoke=q, embers=1)
            fl = 0
        pal = VOID if void else FIRE
        img, mask, piece, uv, hot = body_layers(P)
        if sep > 0:
            # Kruste platzt auf: grobe Schollen wandern nach aussen, dahinter weissglut
            core = np.zeros_like(img)
            u, v = uv
            rr = np.hypot(u, v)
            cm = mask & (rr < R * 0.92)
            ck = np.clip(np.floor((1.25 - rr / R) * 5 * min(P["heat"], 2.0) / 1.6), 2, 6).astype(int)
            for k in range(7):
                core[cm & (ck == k)] = pal[k]
            moved = np.zeros_like(img)
            for pid in np.unique(piece[mask]):
                ox, oy, s = offs[pid]
                m = mask & (piece == pid)
                ox_i = int(round(ox * sep * s))
                oy_i = int(round(oy * sep * s))
                ys, xs = np.nonzero(m)
                ys2, xs2 = ys + oy_i, xs + ox_i
                ok = (ys2 >= 0) & (ys2 < CELL) & (xs2 >= 0) & (xs2 < CELL)
                sub = img[ys[ok], xs[ok]].copy()
                # Schollenkante zum Spalt hin: Glutkante
                edge_m = neighbours_out(m)[ys[ok], xs[ok]]
                sub[edge_m] = pal[2] if sep < 2 else pal[3]
                moved[ys2[ok], xs2[ok]] = sub
            canvas = core
            a = moved[..., 3] > 0
            canvas[a] = moved[a]
            # Umriss neu um alles
            full = canvas[..., 3] > 0
            o = dilate(full, 1) & ~full
            canvas[o] = C[0]
        else:
            canvas = np.zeros_like(img)
            smoke(canvas, P, t)
            a = img[..., 3] > 0
            canvas[a] = img[a]
        face(canvas, P, pal)
        if void:
            kalt(canvas)
        flames(canvas, P, pal, t)
        if 10 <= i <= 16:
            shockwave(canvas, (i - 10) / 6, pal)
        embers(canvas, P, pal, t, n=14 + (24 if 9 <= i <= 15 else 0), src=notch_base(P))
        if 10 <= i <= 15:
            spray(canvas, (i - 10) / 5, pal)
        if fl:
            flash(canvas, fl, pal)
        fr.append(canvas)
    return fr


def shockwave(img, q, pal):
    """Bodenring (flache Ellipse) laeuft nach aussen."""
    rx = 18 + q * 44
    ry = rx * 0.3
    cy = GROUND - 1
    th = 2.2 * (1 - q) + 0.8
    for y in range(CELL):
        for x in range(CELL):
            d = math.hypot((x + 0.5 - CX) / rx, (y + 0.5 - cy) / ry)
            dd = (d - 1) * rx
            if -th < dd < 0.6:
                if img[y, x, 3] > 0 and y < cy - 2:
                    continue
                k = 6 if q < 0.2 else 5 if q < 0.45 else 4 if q < 0.7 else 2
                if dd < -th * 0.5:
                    k = max(k - 2, 1)
                img[y, x] = pal[k]


def spray(img, q, pal):
    rng = np.random.RandomState(12)
    cx, cy = CX, BOTTOM - R
    for i in range(26):
        a = rng.rand() * TAU
        sp = 30 + rng.rand() * 34
        x = cx + math.cos(a) * sp * q
        y = cy + math.sin(a) * sp * q * 0.8 + 30 * q * q
        if q > 0.3 + rng.rand() * 0.7:
            continue
        k = 6 if q < 0.25 else 5 if q < 0.5 else 3
        put(img, x, y, pal[k])
        put(img, x - math.cos(a) * 1.2, y - math.sin(a) * 1.2, pal[max(k - 2, 1)])


def ash_out(void=False):
    """Zerfaellt von oben in Asche, die Abbruchkante glueht. Rueckwaerts = asche_da."""
    N = 16
    fr = []
    pal = VOID if void else FIRE
    for i in range(N):
        t = (i / N) * 2 % 1
        q = i / (N - 1)
        heat = 1 + 0.6 * min(1, q * 4)
        P = pose(void=void, t=t, heat=heat, eye_open=0.3 if q > 0.1 else 1.0,
                 flames=max(0, 1 - q * 2.5), smoke=max(0, 1 - q * 3), embers=0 if q > 0.2 else 1,
                 sy=1 + 0.03 * math.sin(math.pi * min(1, q * 3)))
        canvas = bild(P)
        u, v = to_body(P)
        val = 0.62 * ((v + R) / (2 * R)) + 0.38 * fbm(u * 0.25, v * 0.25, 77)
        T = -0.15 + q * 1.25
        body = canvas[..., 3] > 0
        gone = body & (val < T)
        burn = body & (val >= T) & (val < T + 0.07)
        burn2 = body & (val >= T + 0.07) & (val < T + 0.12)
        canvas[burn2] = pal[3]
        canvas[burn] = pal[5]
        canvas[gone] = 0
        # Asche/Funken aus dem schon Zerfallenen: fliegt nach oben rechts weg
        ys, xs = np.nonzero(body & (val < T) & (val > T - 0.35))
        rng = np.random.RandomState(i * 3 + 1)
        sel = rng.rand(len(ys)) < 0.16
        for y, x, vv in zip(ys[sel], xs[sel], val[ys[sel], xs[sel]]):
            age = (T - vv) / 0.35
            ox = age * (10 + (x * 7 % 9)) + math.sin(y * 0.7 + age * 6) * 2
            oy = -age * (16 + (y * 5 % 11))
            k = 6 if age < 0.15 else 5 if age < 0.35 else 3 if age < 0.6 else 1
            col = pal[k] if (x + y) % 3 else SMOKE[0]
            put(canvas, x + ox, y + oy, col)
        # Aschehaeufchen am Boden waechst
        pile = min(1, q * 1.6)
        if pile > 0.05:
            ash_pile(canvas, pile, pal, glow=max(0, 1 - q * 1.2), t=t)
        if void:
            kalt(canvas)
        fr.append(canvas)
    return fr


def ash_pile(img, s, pal, glow=0.0, t=0.0):
    w = 6 + 16 * s
    h = 1 + 5 * s
    for y in range(int(GROUND - h - 1), GROUND):
        for x in range(int(CX - w - 1), int(CX + w + 2)):
            dx = (x + 0.5 - CX) / w
            top = GROUND - h * (1 - dx * dx) - (vnoise(np.array(x * 0.6), np.array(3.0)) - 0.5) * 1.5
            if abs(dx) <= 1 and y + 0.5 >= top:
                k = 3 if y + 0.5 < top + 1.2 else 2
                if (x * 3 + y * 5) % 7 == 0:
                    k = 4
                col = C[k]
                if glow > 0 and y > top + 1 and hash2(x, y, 9) < glow * 0.35 * (0.6 + 0.4 * math.sin(TAU * t + x)):
                    col = pal[3] if hash2(x, y, 10) < 0.5 else pal[2]
                if img[y, x, 3] == 0:
                    img[y, x] = col
    m = img[..., 3] > 0
    o = dilate(m, 1) & ~m
    o[: int(GROUND - h - 2)] = False
    img[o] = C[0]


def death():
    """Ueberhitzt, Kruste fliegt in Brocken weg, Aschehaufen glimmt aus."""
    N = 24
    fr = []
    pal = FIRE
    rng = np.random.RandomState(9)
    offs = {}
    for i, (px, py) in enumerate(COARSE):
        d = math.hypot(px, py) + 1e-6
        offs[i] = (px / d * (0.8 + rng.rand() * 0.8), py / d * (0.6 + rng.rand() * 0.6) - 0.9, rng.rand())
    for i in range(N):
        t = (i / N) * 2 % 1
        if i < 9:
            q = i / 8
            P = pose(t=t, heat=1 + 1.2 * q, pulse=1 + 2 * q, crack_boost=1.4 * q, mouth_open=q,
                     sx=1 + 0.05 * q, sy=1 + 0.05 * q, shake=(int(round(math.sin(i * 2.7) * 2 * q)),
                                                         int(round(math.cos(i * 3.1) * q))),
                     flames=1 + q, smoke=1 - q)
            canvas = bild(P)
            if i == 8:
                flash(canvas, 1.0, pal)
            fr.append(canvas)
            continue
        q = (i - 9) / (N - 10)          # 0..1
        P = pose(t=t, heat=1.6 - 1.2 * q, crack_boost=0.6, eye_open=0.3, flames=0, smoke=0, embers=0)
        img, mask, piece, uv, hot = body_layers(P)
        canvas = np.zeros_like(img)
        pile = min(1, 0.4 + q * 1.2)
        ash_pile(canvas, pile, pal, glow=1 - q * 0.8, t=t)
        tt = q * 1.6                     # Flugzeit
        for pid in np.unique(piece[mask]):
            ox, oy, r = offs[pid]
            sp = 26 + 14 * r
            m = mask & (piece == pid)
            ys, xs = np.nonzero(m)
            cyy = ys.mean()
            dx = ox * sp * tt
            dy = oy * sp * tt + 130 * tt * tt
            # Landet am Boden -> bleibt als Kohlebrocken liegen und glimmt aus
            if cyy + dy > GROUND - 3:
                lx = int(xs.mean() + dx)
                if 2 <= lx < CELL - 3:
                    canvas[GROUND - 2:GROUND, lx - 1:lx + 2] = C[3]
                    canvas[GROUND - 2, lx - 1:lx + 2] = C[4]
                    canvas[GROUND - 3, lx - 2:lx + 3] = C[0]
                    canvas[GROUND - 2:GROUND, lx - 2] = C[0]
                    canvas[GROUND - 2:GROUND, lx + 2] = C[0]
                    if q < 0.75 and pid % 2 == 0:
                        canvas[GROUND - 1, lx] = pal[3 if q < 0.4 else 2]
                continue
            ys2 = (ys + dy).astype(int)
            xs2 = (xs + dx).astype(int)
            ok = (ys2 >= 0) & (ys2 < CELL) & (xs2 >= 0) & (xs2 < CELL)
            sub = img[ys[ok], xs[ok]].copy()
            ed = neighbours_out(m)[ys[ok], xs[ok]]
            sub[ed] = pal[2] if q < 0.4 else C[0]
            canvas[ys2[ok], xs2[ok]] = sub
        # Kern: weissgluehende Kugel, die schrumpft und in den Haufen sinkt
        cr = 9 * (1 - q) ** 1.3
        if cr > 0.6:
            ccx, ccy = CX, BOTTOM - R + q * (R + 2)
            for y in range(int(ccy - cr - 1), int(ccy + cr + 2)):
                for x in range(int(CX - cr - 1), int(CX + cr + 2)):
                    if 0 <= x < CELL and 0 <= y < CELL:
                        d = math.hypot(x + 0.5 - CX, y + 0.5 - ccy) / cr
                        if d < 1:
                            img_k = 6 if d < 0.4 else 5 if d < 0.7 else 3
                            canvas[y, x] = pal[img_k]
        embers(canvas, pose(t=t, embers=max(0.0, 1 - q)), pal, t, n=16, seed=21,
               src=(CX, GROUND - 4))
        if q < 0.3:
            shockwave(canvas, q / 0.3, pal)
        fr.append(canvas)
    return fr


# ------------------------------------------------------------------ Kruemelwurf

KR = 12          # Zelle des Wurfkruemels
GF_W, GF_H = 40, 24   # Zelle des Glutflecks


def kruemel():
    """Verkohlter Brocken im Flug, 4 Bilder = eine Umdrehung (pixelgenau gedreht)."""
    base = np.zeros((KR, KR, 4), np.uint8)
    yy, xx = np.mgrid[0:KR, 0:KR] + 0.5
    u, v = xx - KR / 2, yy - KR / 2
    th = np.arctan2(v, u)
    r = 3.9 + 0.8 * np.cos(3 * th + 0.5) + 0.5 * np.cos(5 * th + 2.0)
    m = np.hypot(u, v) < r
    lum = -(u * 0.6 + v * 0.8) / 4
    band = np.clip(np.digitize(lum, [-0.4, 0.0, 0.4]) + 2, 2, 5)
    for b in range(2, 6):
        base[m & (band == b)] = C[b]
    crack = m & (np.abs(u - v * 0.4 - 0.3) < 0.55) & (np.hypot(u, v) < r - 1)
    base[crack] = FIRE[3]
    base[crack & (np.abs(v) < 0.8)] = FIRE[5]
    base[neighbours_out(m)] = C[0]
    return [np.ascontiguousarray(np.rot90(base, -k)) for k in range(4)]


def glutfleck(void=False):
    """Brennender Boden, wo ein Kruemel einschlaegt. 8 Bilder Schleife, Pivot Mitte."""
    pal = VOID if void else FIRE
    fr = []
    yy, xx = np.mgrid[0:GF_H, 0:GF_W] + 0.5
    cx, cy = GF_W / 2, GF_H / 2 + 3
    for i in range(8):
        t = i / 8
        img = np.zeros((GF_H, GF_W, 4), np.uint8)
        e = np.hypot((xx - cx) / 13.0, (yy - cy) / 5.5) + (fbm(xx * 0.3, yy * 0.3, 61) - 0.5) * 0.35
        m = e < 1
        img[m] = C[2]
        img[m & (e < 0.75)] = C[1]
        n = fbm(xx * 0.45, yy * 0.7, 62)
        pulse = 0.5 + 0.5 * np.sin(TAU * (t - e * 0.8))
        h = (1 - e) * 1.6 + n * 1.2 + pulse * 0.6 - 0.9
        k = np.clip(np.floor(h * 3.2), 0, 6).astype(int)
        hot = m & (h > 0.35)
        for kk in range(1, 7):
            img[hot & (k == kk)] = pal[kk]
        img[neighbours_out(m) & ~hot] = C[0]
        # kleine Flammen, die auf- und abzuengeln
        for j, (fx, ph, hh) in enumerate(((-7, 0.0, 6), (-1, 0.4, 8), (6, 0.7, 5), (2, 0.2, 4))):
            q = 0.5 + 0.5 * math.sin(TAU * (t + ph))
            H = hh * (0.5 + 0.5 * q)
            bx, by = cx + fx, cy - 1
            for sy in range(int(H) + 1):
                w = max(0, int((1 - sy / max(H, 1)) * 2.2 + 0.3))
                sway = int(round(math.sin(TAU * (t * 2 + ph) + sy * 0.6) * sy / 6))
                for ox in range(-w, w + 1):
                    x, y = int(bx + ox + sway), int(by - sy)
                    if 0 <= x < GF_W and 0 <= y < GF_H:
                        kk = 5 if (abs(ox) < w * 0.5 and sy < H * 0.5) else 3 if sy < H * 0.75 else 2
                        img[y, x] = pal[kk]
        fr.append(img)
    return fr


# ------------------------------------------------------------------ Ausgabe

def schreibe(name, frames, pivot=PIVOT):
    os.makedirs(OUT_DIR, exist_ok=True)
    path = os.path.join(OUT_DIR, name + ".png")
    h, w = frames[0].shape[:2]
    sheet = Image.new("RGBA", (w * len(frames), h))
    for i, f in enumerate(frames):
        sheet.paste(Image.fromarray(f), (i * w, 0))
    sheet.save(path)
    meta = path + ".meta"
    if not os.path.exists(meta):
        sys.path.insert(0, TOOLS)
        from unity_meta import write_strip_meta
        write_strip_meta(meta, name, len(frames), w, h, PPU, pivot=pivot, max_size=4096)
    print("%-28s %2d Bilder" % (name, len(frames)))


def main():
    args = sys.argv[1:]
    A = dict(idle=idle(), walk=walk(), burst=burst(), ash=ash_out(),
             sidle=idle(True), swalk=walk(True), death=death())
    A["ash_in"] = A["ash"][::-1]
    A["sburst"] = burst(True)
    A["sash"] = ash_out(True)
    nur = args[args.index("--nur") + 1] if "--nur" in args else None
    if nur != "vorschau":
        schreibe("verkohlter_idle", A["idle"])
        schreibe("verkohlter_walk", A["walk"])
        schreibe("verkohlter_ausbruch", A["burst"])
        schreibe("verkohlter_asche_weg", A["ash"])
        schreibe("verkohlter_asche_da", A["ash_in"])
        schreibe("verkohlter_schatten_idle", A["sidle"])
        schreibe("verkohlter_schatten_walk", A["swalk"])
        schreibe("verkohlter_tod", A["death"])
        schreibe("verkohlter_schatten_ausbruch", A["sburst"])
        schreibe("verkohlter_schatten_asche_weg", A["sash"])
        schreibe("verkohlter_schatten_asche_da", A["sash"][::-1])
        schreibe("verkohlter_kruemel", kruemel(), pivot=(0.5, 0.5))
        schreibe("verkohlter_glutfleck", glutfleck(), pivot=(0.5, (GF_H / 2 - 3) / GF_H))
        b = Image.fromarray(bild(pose(t=0.25), fx=False))
        b = b.crop(b.getbbox())
        b.save(BESTIARY)
        print("Bestiarium:", BESTIARY, b.size)
    if "--preview" in args:
        vorschau(A, args[args.index("--preview") + 1])


# ------------------------------------------------------------------ Vorschau

def dunkler_boden(w, h):
    bg = np.zeros((h, w, 4), np.uint8)
    bg[:] = (20, 16, 26, 255)
    yy, xx = np.mgrid[0:h, 0:w]
    n = fbm(xx * 0.15, yy * 0.15, 50)
    bg[n > 0.58] = (27, 21, 34, 255)
    bg[n < 0.38] = (15, 12, 20, 255)
    spk = (xx * 7 + yy * 13 + (xx // 5) * (yy // 3)) % 41 == 0
    bg[spk] = (40, 31, 48, 255)
    return bg


def licht(bg, cx, cy, r, s, col=(255, 110, 30)):
    """Lichtkegel der Glut auf dem Boden, in Stufen (Pixel-Look)."""
    h, w = bg.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w]
    d = np.hypot((xx - cx) / r, (yy - cy) / (r * 0.55))
    lv = np.floor(np.clip(1 - d, 0, 1) * 4) / 4 * s
    out = bg.copy().astype(np.float64)
    for c in range(3):
        out[..., c] = out[..., c] + lv * col[c] * 0.22
    return np.clip(out, 0, 255).astype(np.uint8)


def vorschau(A, path, scale=4):
    W, H = 260, 150
    base = dunkler_boden(W, H)
    gy = 120
    frames = []

    def comp(sprites, glow=1.0, shake=(0, 0), lightcol=(255, 110, 30)):
        bg = base.copy()
        for (img, x, l, lc) in sprites:
            bg = licht(bg, x, gy, 46, l, lc)
        for (img, x, l, lc) in sprites:
            x0 = int(x - CX + shake[0])
            y0 = gy - GROUND + shake[1]
            sub = bg[max(0, y0):y0 + CELL, max(0, x0):x0 + CELL]
            im = img[max(0, -y0):max(0, -y0) + sub.shape[0], max(0, -x0):max(0, -x0) + sub.shape[1]]
            a = im[..., 3:4].astype(np.float64) / 255
            sub[..., :3] = (sub[..., :3] * (1 - a) + im[..., :3] * a).astype(np.uint8)
        return bg

    F = (255, 110, 30)
    Vv = (150, 70, 255)
    for k in range(2):
        for f in A["idle"]:
            frames.append(comp([(f, 130, 1.0, F)]))
    x = 70.0
    for k in range(3):
        for f in A["walk"]:
            x += 1.5
            frames.append(comp([(f, x, 1.0, F)]))
    for i, f in enumerate(A["burst"]):
        sh = (0, 0)
        l = 1.0
        if 9 <= i <= 13:
            sh = ((i % 2) * 4 - 2, (i % 3) - 1)
            l = 2.5 - (i - 9) * 0.3
        frames.append(comp([(f, x, l, F)], shake=sh))
    for f in A["idle"][:6]:
        frames.append(comp([(f, x, 1.0, F)]))
    for i, f in enumerate(A["ash"]):
        frames.append(comp([(f, x, max(0.2, 1 - i / 16), F)]))
    for i, f in enumerate(A["ash_in"]):
        frames.append(comp([(f, 180, min(1, 0.2 + i / 16), F)]))
    # Phase 2: Schattenklone
    for k in range(2):
        for j in range(12):
            frames.append(comp([(A["sidle"][j], 70, 0.8, Vv), (A["idle"][j], 130, 1.0, F),
                                (A["sidle"][(j + 6) % 12], 190, 0.8, Vv)]))
    for i, f in enumerate(A["death"]):
        sh = ((i % 2) * 2 - 1, 0) if i < 9 else (0, 0)
        l = 2.5 if i == 8 else max(0.15, 1.2 - (i - 9) / 15) if i > 8 else 1 + i / 8
        frames.append(comp([(f, 130, l, F)], shake=sh))
    for _ in range(10):
        frames.append(frames[-1])

    ims = [Image.fromarray(f[..., :3]).resize((W * scale, H * scale), Image.NEAREST) for f in frames]
    ims[0].save(path, save_all=True, append_images=ims[1:], duration=int(1000 / FPS), loop=0)
    print("Vorschau:", path, len(ims), "Bilder")


if __name__ == "__main__":
    main()
