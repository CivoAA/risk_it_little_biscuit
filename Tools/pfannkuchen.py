"""
Pfannkuchen (EnemyId.Pancake): komplett neu gezeichnet und animiert.

Gleiches Prinzip wie das alte fin_pencake.png - ein grimmiger Dreierstapel mit
Butter und Sirup, der beim Huepfen auseinanderzieht -, aber jede Lage ist hier
ein eigener Koerper mit eigener Flugkurve:

  * Alle Lagen fallen mit derselben Schwerkraft, die obere fliegt nur laenger.
    Darum hebt der Stapel von oben nach unten ab (Ziehharmonika), landet von
    unten nach oben (plopp - plopp - plopp - Butter) und die Abstaende in der
    Luft bleiben gleich gross, statt dass Lagen ineinanderrutschen.
  * Jede Landung staucht die getroffene Lage und alles darunter (gedaempfte
    Feder), die oberen Lagen schwingen danach seitlich ein bisschen nach.
  * Sirup laeuft ueber den Rand; eine Nase haengt bis auf den mittleren
    Pfannkuchen und zieht beim Abheben einen klebrigen Faden.
  * Gesicht wie das alte (grimmig), kneift beim Anlauf, reisst in der Luft die
    Augen auf, kneift beim Aufprall. Zweite Schleife blinzelt.

  Assets/Art/Gegner/new/pfannkuchen_hop.png    2 Hopser x 16 Bilder, 64x96
  PPU 32, Pivot auf der Bodenlinie (Spalte 32, Zeile GROUND).
  Assets/Resources/Bestiary/Pancake.png         Bild 0, zugeschnitten

Der Stapel huepft nur im Bild: der Gegner laeuft wie bisher gleichmaessig
(kein HopMovement), genau wie der alte.

Aufruf aus dem Projektordner:
  python Tools/pfannkuchen.py [--preview pfad.gif]
"""

import math
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "Assets", "Art", "Gegner", "new")
NAME = "pfannkuchen_hop"
BESTIARY = os.path.join(ROOT, "Assets", "Resources", "Bestiary", "Pancake.png")

CELL_W, CELL_H = 64, 96
GROUND = 92                  # Zeile unter dem untersten Pfannkuchen
CX = CELL_W / 2.0
PPU = 32
FPS = 16
PER_HOP = 16                 # Bilder je Hopser
HOPS = 2                     # zweiter Hopser blinzelt
SS = 4


def rgb(h, a=255):
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


C = {
    "line":      rgb("3e1c0a"),   # Umriss aussen
    "crust_d":   rgb("6e3512"),   # Trennlinie zwischen den Lagen
    "crust":     rgb("9c5221"),   # gebraeunte Unterseite
    "gold_d":    rgb("c27630"),
    "gold":      rgb("de9a45"),
    "batter_sh": rgb("e3ad5c"),
    "batter":    rgb("f2c878"),
    "batter_hi": rgb("fbe3a6"),
    "top_sh":    rgb("bc6c2c"),
    "top":       rgb("da8c3e"),
    "top_hi":    rgb("efae5e"),
    "hole":      rgb("a35a22"),
    "syr_line":  rgb("5a2205"),
    "syr_sh":    rgb("9e4610"),
    "syr":       rgb("cf6e18"),
    "syr_hi":    rgb("f2a33a"),
    "syr_glint": rgb("ffe2a0"),
    "but_line":  rgb("8a5c12"),
    "but_sh":    rgb("f0c040"),
    "but":       rgb("ffe066"),
    "but_top":   rgb("fff3b0"),
    "but_glint": rgb("ffffff"),
    "eye":       rgb("24100a"),
    "glint":     rgb("ffffff"),
    "brow":      rgb("3e1c0a"),
    "mouth":     rgb("4a1a08"),
    "blush":     rgb("ee8a62"),
    "shadow":    (36, 18, 8, 80),
}

# ---------------------------------------------------------------- Stapel

# Lagen von unten: halbe Breite, Dicke (Band), x-Versatz (handgemacht schief)
LAYERS = [
    (23.5, 10.0, 0.0),
    (23.0, 10.0, 0.8),
    (22.0, 12.0, -0.6),
]
# Sirupnasen auf dem obersten: (u = x / halbe Breite, Laenge px, halbe Breite)
# Die lange Nase (LONG_U) laeuft bis auf den mittleren Pfannkuchen weiter.
LONG_U = -0.60
DRIPS = [(LONG_U, 13.0, 1.3), (-0.86, 3.5, 1.1), (0.52, 5.5, 1.3), (0.82, 3.0, 1.0)]
RY_K = 0.27                  # Hoehe der Deckelellipse relativ zur Breite
BUTTER_HW, BUTTER_H, BUTTER_D = 5.5, 5.0, 3.0   # halbe Breite, Front, Deckel

# Flug: alle mit derselben Schwerkraft; w = halbe Flugzeit (Phase)
PEAK = 0.53
W0 = 0.150                    # unterste Lage
H0 = 7.0                      # deren Sprunghoehe (px)
G = H0 / W0 ** 2
GAPS = [0.0, 7.0, 8.0, 5.0]   # Luftspalt zur Ebene darunter (Mitte, Oben, Butter)


def half_times():
    w = [W0]
    for i in range(1, 4):
        w.append(math.sqrt(w[-1] ** 2 + GAPS[i] / G))
    return w


W = half_times()


def lift(i, p):
    """Hoehe der Flugkurve von Ebene i (0..2 Pfannkuchen, 3 Butter) bei Phase p."""
    d = p - PEAK
    return max(0.0, G * (W[i] ** 2 - d * d))


def vel(i, p):
    if abs(p - PEAK) >= W[i]:
        return 0.0
    return -2.0 * G * (p - PEAK)


def land_time(i):
    return PEAK + W[i]


def takeoff_time(i):
    return PEAK - W[i]


def spring(t, amp, tau=0.05, period=0.11):
    if t < 0:
        return 0.0
    return amp * math.exp(-t / tau) * math.cos(2 * math.pi * t / period)


def smooth(a, b, x):
    if x <= a:
        return 0.0
    if x >= b:
        return 1.0
    t = (x - a) / (b - a)
    return t * t * (3 - 2 * t)


def squash(i, p):
    """Dickenfaktor der Ebene i (1 = Ruhe)."""
    s = 1.0
    # Atmen in der Ruhe
    s += 0.025 * math.sin(2 * math.pi * p * 2 + 0.6 * i) * (1 - smooth(0.10, 0.16, p))
    # Anlauf: alles sackt zusammen, unten am meisten
    deep = [0.30, 0.25, 0.19, 0.22][i]
    t0 = takeoff_time(3) - 0.13
    down = smooth(t0, takeoff_time(3) - 0.015, p)
    up = smooth(takeoff_time(i) - 0.03, takeoff_time(i) + 0.01, p)
    s -= deep * down * (1 - up)
    # in der Luft: nach Tempo gestreckt
    v = vel(i, p)
    if v != 0.0:
        vmax = 2.0 * G * W[i]
        s += 0.22 * (abs(v) / vmax) ** 1.3
    # Landungen: die eigene und die aller Lagen darueber
    for j in range(i, 4):
        amp = [0.30, 0.20, 0.16, 0.12][j] * (1.0 if j == i else 0.75)
        s -= spring(p - land_time(j), amp)
        s -= spring(p + 1 - land_time(j), amp)   # Ausschwingen ueber den Schleifenrand
    return max(0.55, s)


def sway(i, p):
    """Seitliches Nachschwingen nach der Landung (px), oben staerker."""
    x = 0.0
    for j in range(1, 4):
        t = p - land_time(j)
        if t >= 0:
            x += [0, 0.7, 1.0, 0.9][j] * math.exp(-t / 0.07) * math.sin(2 * math.pi * t / 0.13) * (i / 2.0)
    return x


def pose(p):
    """Alle Ebenen fuer Phase p: Liste von dicts (base, T, R, ry, x)."""
    out = []
    base = lift(0, p)
    prev_lift = lift(0, p)
    for i, (R, T, xo) in enumerate(LAYERS):
        if i > 0:
            l = lift(i, p)
            base += l - prev_lift
            prev_lift = l
        s = squash(i, p)
        wf = s ** -0.55
        Ri = R * wf
        ry = R * RY_K * (0.85 + 0.15 * wf)
        Ti = T * s
        out.append(dict(base=base, T=Ti, R=Ri, ry=ry, x=xo + sway(i, p), s=s))
        base += Ti
    # Butter auf dem obersten Deckel
    l = lift(3, p)
    s = squash(3, p)
    top = out[-1]
    bb = top["base"] + top["T"] + top["ry"] * 0.95 + (l - prev_lift)
    out.append(dict(base=bb, s=s, x=top["x"] + 0.3 + sway(3, p) * 1.2,
                    hw=BUTTER_HW * s ** -0.5, h=BUTTER_H * s, d=BUTTER_D * (0.8 + 0.2 * s)))
    return out


# ---------------------------------------------------------------- Zeichnen

EMPTY, SHADOW = 0, 1
SIDE, TOPS = 10, 20          # + Lagenindex
SYRUP = 30                   # + Lagenindex (Sirup auf Lage i)
STRAND = 40
BUT_FRONT, BUT_TOP = 50, 51


def edge_noise(i, X, R):
    """Handgemachter, leicht welliger Rand (nur im Deckel sichtbar)."""
    a = np.arctan2(0.0, 1.0) + X / max(R, 1) * 2.6
    return 1.0 + 0.025 * np.sin(a * 3 + i * 1.7) + 0.015 * np.sin(a * 7 + i)


def pancake_masks(L, X, Y, i):
    R, ry, T, b, xo = L["R"], L["ry"], L["T"], L["base"], L["x"]
    Xr = (X - xo)
    yb = b + ry               # Mitte der Bodenellipse
    yt = yb + T               # Mitte der Deckelellipse
    k = np.clip((Y - (yb + yt) / 2) / (T / 2 + 1e-6), -1, 1)
    hw = R * (1.0 - 0.06 * k ** 4)
    band = (Y >= yb) & (Y <= yt) & (np.abs(Xr) <= hw)
    bot = ((Xr / (R * 0.94)) ** 2 + ((Y - yb) / ry) ** 2 <= 1.0)
    top = ((Xr / (R * 0.94 * edge_noise(i, Xr, R))) ** 2 + ((Y - yt) / ry) ** 2 <= 1.0)
    side = (band | bot) & ~top
    return side, top


def syrup_mask(L, X, Y, i, p, layers):
    """Sirup auf Lage i: Pfuetze auf dem Deckel (nur oben) + Nasen ueber den Rand."""
    R, ry, T, b, xo = L["R"], L["ry"], L["T"], L["base"], L["x"]
    Xr = X - xo
    yt = b + ry + T
    m = np.zeros(X.shape, bool)
    s = L["s"]
    if i == 2:
        ang = np.arctan2((Y - yt) / ry, Xr / R)
        rr = 0.74 + 0.08 * np.sin(3 * ang + 1.0) + 0.05 * np.sin(5 * ang + 2.0)
        m |= ((Xr / R) ** 2 + ((Y - yt - 0.6) / ry) ** 2) <= rr ** 2
        drips = DRIPS
    elif i == 1:
        # Fortsetzung der langen Nase auf dem mittleren Pfannkuchen
        drips = [((LONG_U * layers[2]["R"] + layers[2]["x"] - xo) / R, 5.0, 1.2)]
    else:
        drips = []
    for (u, length, wd) in drips:
        x0 = u * R
        rim = yt - ry * math.sqrt(max(0.0, 1 - (x0 / R) ** 2)) * 0.98
        length = length * (0.7 + 0.3 * s ** 1.5) if i == 2 else length
        y1 = rim - length
        top = rim + 2.0
        if i == 2:
            length = min(length, T + 0.8)  # nicht unter die Lage hinaus
        body = (np.abs(Xr - x0) <= wd) & (Y <= top) & (Y >= y1)
        tip = ((Xr - x0) ** 2 + (Y - y1) ** 2) <= (wd + 0.35) ** 2
        m |= body | tip
    return m


def strand_mask(layers, X, Y):
    """Klebriger Faden zwischen oberem und mittlerem Pfannkuchen."""
    up, lo = layers[2], layers[1]
    gap = up["base"] - (lo["base"] + lo["T"])
    if gap < 0.4:
        return np.zeros(X.shape, bool)
    x0 = up["x"] + LONG_U * up["R"]
    y_top = up["base"] + up["ry"] * (1 - math.sqrt(max(0, 1 - (LONG_U / 0.94) ** 2)))
    ul = (x0 - lo["x"]) / lo["R"]
    y_lo = lo["base"] + lo["ry"] + lo["T"] - lo["ry"] * math.sqrt(max(0, 1 - (ul / 0.94) ** 2)) * 0.8
    # duenner in der Mitte, Tropfen an beiden Enden
    t = np.clip((Y - y_lo) / max(y_top - y_lo, 1e-3), 0, 1)
    wd = 0.45 + 0.9 * (np.abs(t - 0.5) * 2) ** 3 - 0.12 * min(gap, 6) / 6
    xm = x0 + 0.6 * math.sin(gap * 0.6) * np.sin(t * math.pi)
    m = (np.abs(X - xm) <= wd) & (Y >= y_lo - 0.6) & (Y <= y_top + 1.0)
    return m


def butter_masks(B, X, Y):
    xo, b, hw, h, d = B["x"], B["base"], B["hw"], B["h"], B["d"]
    Xr = X - xo
    # Front: abgerundetes Rechteck (schmilzt unten etwas breiter)
    yy = (Y - b) / h
    hw_y = hw * (1.0 + 0.18 * np.clip(1 - yy, 0, 1) ** 3)
    front = (Y >= b) & (Y <= b + h) & (np.abs(Xr) <= hw_y)
    corner = (np.abs(Xr) > hw - 1.2) & (Y < b + 1.0) & (np.abs(Xr) > hw_y - 0.4)
    front &= ~corner
    # Deckel: Parallelogramm-artige Ellipse nach hinten
    topm = ((np.abs(Xr) <= hw * 0.98) & (Y > b + h) & (Y <= b + h + d)
            & ~((np.abs(Xr) > hw - 1.0) & (Y > b + h + d - 1.0)))
    return front, topm


def render(p):
    layers = pose(p)
    H, Wd = CELL_H * SS, CELL_W * SS
    ys, xs = np.mgrid[0:H, 0:Wd]
    X = (xs + 0.5) / SS - CX
    Y = GROUND - (ys + 0.5) / SS            # nach oben, 0 = Boden

    owner = np.zeros((H, Wd), np.int16)

    # Schatten (wird kleiner, je hoeher die unterste Lage ist)
    h0 = layers[0]["base"]
    sw = LAYERS[0][0] * (1.02 - 0.22 * h0 / H0) * layers[0]["s"] ** -0.3
    sh = ((X / sw) ** 2 + ((Y + 0.3) / 2.6) ** 2) <= 1.0
    owner[sh] = SHADOW

    for i in range(3):
        L = layers[i]
        side, top = pancake_masks(L, X, Y, i)
        owner[side] = SIDE + i
        owner[top] = TOPS + i
        if i == 1:
            owner[syrup_mask(L, X, Y, 1, p, layers)] = SYRUP + 1
            owner[strand_mask(layers, X, Y)] = STRAND
        if i == 2:
            owner[syrup_mask(L, X, Y, 2, p, layers)] = SYRUP + 2

    front, btop = butter_masks(layers[3], X, Y)
    owner[front] = BUT_FRONT
    owner[btop] = BUT_TOP

    # Pixel = haeufigster Besitzer seiner SSxSS Unterpixel (leer bei Mehrheit leer)
    o = owner.reshape(CELL_H, SS, CELL_W, SS).transpose(0, 2, 1, 3).reshape(CELL_H, CELL_W, SS * SS)
    codes = np.unique(owner)
    counts = np.stack([(o == c).sum(-1) for c in codes], -1)
    pix = codes[np.argmax(counts, -1)]
    empty = (o == EMPTY).sum(-1)
    pix[empty > SS * SS // 2] = EMPTY
    # Faden darf duenn sein: schon ab einem Viertel Abdeckung
    strand_cov = (o == STRAND).sum(-1)
    pix[(strand_cov >= SS * SS // 4) & (pix != BUT_FRONT) & (pix != BUT_TOP)] = STRAND
    return pix, layers


def is_cake(c):
    return SIDE <= c < SIDE + 3 or TOPS <= c < TOPS + 3


def layer_of(c):
    if SIDE <= c < SIDE + 3:
        return c - SIDE
    if TOPS <= c < TOPS + 3:
        return c - TOPS
    if SYRUP <= c < SYRUP + 3:
        return c - SYRUP
    return 9


def shade(pix, layers, p):
    Hh, Ww = pix.shape
    img = np.zeros((Hh, Ww, 4), np.uint8)

    def at(r, c):
        return pix[r, c] if 0 <= r < Hh and 0 <= c < Ww else EMPTY

    for r in range(Hh):
        for c in range(Ww):
            m = pix[r, c]
            if m == EMPTY:
                continue
            X = c + 0.5 - CX
            Y = GROUND - (r + 0.5)
            if m == SHADOW:
                img[r, c] = C["shadow"]
                continue
            if SIDE <= m < SIDE + 3:
                L = layers[m - SIDE]
                u = (X - L["x"]) / L["R"]
                # Hoehe im Band: 0 = Unterkante (vorne), 1 = Oberkante
                yb = L["base"] + L["ry"] * (1 - math.sqrt(max(0, 1 - min(1, (u / 0.94) ** 2))))
                v = (Y - yb) / max(L["T"] + L["ry"] * 0.2, 1)
                light = -0.9 * u + 0.25
                if v < 0.16:
                    col = C["crust"] if light > -0.2 else C["crust_d"]
                elif v < 0.30:
                    col = C["gold"] if light > 0.1 else C["gold_d"]
                elif v < 0.80:
                    col = C["batter_hi"] if light > 0.75 else C["batter"] if light > -0.15 else C["batter_sh"]
                else:
                    col = C["gold"] if light > -0.3 else C["gold_d"]
                # Rand unter dem Deckel: heller Wulst
                if at(r - 1, c) == TOPS + (m - SIDE):
                    col = C["batter_hi"] if light > -0.2 else C["batter"]
            elif TOPS <= m < TOPS + 3:
                i = m - TOPS
                L = layers[i]
                u = (X - L["x"]) / L["R"]
                yt = L["base"] + L["ry"] + L["T"]
                v = (Y - yt) / L["ry"]
                light = -0.6 * u + 0.55 * v
                col = C["top_hi"] if light > 0.35 else C["top"] if light > -0.35 else C["top_sh"]
                # Loecher im Teig (feste Positionen je Lage)
                hx = (c * 7 + r * 13 + i * 5) % 23
                if hx == 0 and abs(u) < 0.8:
                    col = C["hole"]
            elif SYRUP <= m < SYRUP + 3 or m == STRAND:
                i = layer_of(m)
                L = layers[min(i, 2)]
                u = (X - L["x"]) / L["R"]
                below = at(r + 1, c)
                col = C["syr"]
                yt = L["base"] + L["ry"] + L["T"]
                rim = yt - L["ry"] * math.sqrt(max(0.0, 1 - min(1, (u / 0.94) ** 2)))
                if m == STRAND:
                    col = C["syr_hi"] if (c + 0.5 - CX) < layers[2]["x"] + LONG_U * layers[2]["R"] + 0.2 else C["syr"]
                elif i == 2 and Y > rim + 0.5:
                    # Pfuetze: glaenzt oben links, dunkler nach vorn
                    v = (Y - yt) / L["ry"]
                    light = -0.7 * u + 0.6 * v
                    col = C["syr_hi"] if light > 0.25 else C["syr"] if light > -0.45 else C["syr_sh"]
                    if -0.56 < u < -0.26 and 0.22 < v < 0.52:
                        col = C["syr_glint"]
                else:
                    # Nase: linke Spalte Licht
                    if at(r, c - 1) not in (m,) or u < -0.5:
                        col = C["syr_hi"]
                if below not in (m, STRAND) and m != STRAND and not (SYRUP <= below < SYRUP + 3):
                    col = C["syr_sh"]         # Unterkante der Nase / Pfuetze
            elif m == BUT_FRONT:
                B = layers[3]
                u = (X - B["x"]) / B["hw"]
                col = C["but"] if u < 0.45 else C["but_sh"]
            elif m == BUT_TOP:
                col = C["but_top"]
            img[r, c] = col

    out = img.copy()
    for r in range(Hh):
        for c in range(Ww):
            m = pix[r, c]
            if m in (EMPTY, SHADOW):
                continue
            nb = [at(r + dr, c + dc) for dr, dc in ((1, 0), (-1, 0), (0, 1), (0, -1))]
            if any(n in (EMPTY, SHADOW) for n in nb):
                if m in (BUT_FRONT, BUT_TOP):
                    out[r, c] = C["but_line"]
                elif SYRUP <= m < SYRUP + 3 or m == STRAND:
                    out[r, c] = C["syr_line"]
                else:
                    out[r, c] = C["line"]
                continue
            # Trennlinie: Pfannkuchen ueber einer tieferen Lage
            if is_cake(m):
                dn = at(r + 1, c)
                if is_cake(dn) and layer_of(dn) < layer_of(m):
                    out[r, c] = C["crust_d"]
                # Deckel vor Seitenwand der hoeheren Lage: Schatten
                if TOPS <= m < TOPS + 3 and is_cake(at(r - 1, c)) and layer_of(at(r - 1, c)) > layer_of(m):
                    out[r, c] = C["top_sh"]
            if m == BUT_FRONT and at(r - 1, c) == BUT_TOP:
                out[r, c] = C["but_top"] if (c + 0.5 - CX - layers[3]["x"]) < 0 else C["but"]
            if m == BUT_FRONT and is_cake(at(r + 1, c)):
                out[r, c] = C["but_line"]
            if (SYRUP <= m < SYRUP + 3) and is_cake(at(r + 1, c)) is False and at(r + 1, c) in (EMPTY,):
                out[r, c] = C["syr_line"]
    # Glanzpunkte
    B = layers[3]
    gr = int(round(GROUND - (B["base"] + B["h"] * 0.70)))
    gc = int(math.floor(CX + B["x"] - B["hw"] + 2.0))
    if 0 <= gr < Hh and 0 <= gc < Ww and pix[gr, gc] == BUT_FRONT:
        out[gr, gc] = C["but_glint"]
    return out


# ---------------------------------------------------------------- Gesicht

def face_kind(k):
    """Gesicht fuer Bild k der ganzen Schleife."""
    p = (k % PER_HOP) / PER_HOP
    hop = k // PER_HOP
    if hop == 1 and abs(p - 1 / PER_HOP) < 1e-6:
        return "zu"
    if takeoff_time(3) - 0.13 <= p < takeoff_time(2):
        return "kneif"
    if takeoff_time(2) <= p < land_time(2) - 0.02:
        return "auf" if p < PEAK + 0.04 else "oh"
    if land_time(2) - 0.02 <= p < land_time(2) + 0.13:
        return "aua"
    return "grimmig"


def face(img, pix, layers, kind):
    L = layers[2]
    s = L["s"]
    wf = s ** -0.55
    yb = L["base"] + L["ry"]
    mid_y = L["base"] + L["T"] * 0.55               # Augenmitte (Welt)
    er = int(round(GROUND - mid_y))
    cx = int(math.floor(CX + L["x"]))               # erste Spalte rechts der Mitte
    sep = int(round(5 * wf))

    def put(r, c, col):
        if 0 <= r < CELL_H and 0 <= c < CELL_W and SIDE + 2 == pix[r, c]:
            img[r, c] = col

    for side in (-1, 1):
        ex = cx - sep - 1 if side < 0 else cx + sep - 1   # Auge = Spalten ex, ex+1
        inner = ex + 1 if side < 0 else ex                # Spalte zur Mitte hin
        outer = ex if side < 0 else ex + 1
        if kind == "kneif":                                # angestrengt: > <
            if side < 0:
                put(er - 1, ex, C["eye"]); put(er, ex + 1, C["eye"]); put(er + 1, ex, C["eye"])
            else:
                put(er - 1, ex + 1, C["eye"]); put(er, ex, C["eye"]); put(er + 1, ex + 1, C["eye"])
            put(er - 3, outer, C["brow"]); put(er - 2, inner, C["brow"])
        elif kind == "aua":                                # zugekniffen, Striche
            put(er, ex, C["eye"]); put(er, ex + 1, C["eye"])
            put(er - 1, inner + (1 if side < 0 else -1), C["eye"])
            put(er - 3, outer, C["brow"]); put(er - 3, inner, C["brow"])
        elif kind == "zu":                                 # Blinzeln (grimmig)
            put(er + 1, ex, C["eye"]); put(er + 1, ex + 1, C["eye"])
            put(er - 3, outer + (-1 if side < 0 else 1), C["brow"])
            put(er - 3, outer, C["brow"])
            put(er - 2, inner, C["brow"])
            put(er - 2, inner + (1 if side < 0 else -1), C["brow"])
        elif kind in ("auf", "oh"):                        # weit offen, Brauen hoch
            for dr in (-1, 0, 1, 2):
                put(er + dr, ex, C["eye"]); put(er + dr, ex + 1, C["eye"])
            put(er - 1, outer, C["glint"])
            put(er - 3, ex, C["brow"]); put(er - 3, ex + 1, C["brow"])
        else:                                              # grimmig wie das alte
            for dr in (-1, 0, 1):
                put(er + dr, ex, C["eye"]); put(er + dr, ex + 1, C["eye"])
            put(er - 1, outer, C["glint"])
            # Braue faellt zur Mitte ab
            put(er - 3, outer + (-1 if side < 0 else 1), C["brow"])
            put(er - 3, outer, C["brow"])
            put(er - 2, inner, C["brow"])
            put(er - 2, inner + (1 if side < 0 else -1), C["brow"])
        # Baeckchen
        bc = ex - 2 if side < 0 else ex + 3
        put(er + 2, bc, C["blush"]); put(er + 2, bc + (1 if side < 0 else -1), C["blush"])

    mr = er + 3
    if kind in ("auf",):
        put(mr, cx - 2, C["mouth"]); put(mr, cx - 1, C["mouth"]); put(mr, cx, C["mouth"]); put(mr, cx + 1, C["mouth"])  # zusammengebissen
    elif kind == "oh":
        put(mr, cx - 1, C["mouth"]); put(mr, cx, C["mouth"])
        put(mr + 1, cx - 1, C["mouth"]); put(mr + 1, cx, C["mouth"])  # kleines o
    elif kind == "aua":
        put(mr, cx - 2, C["mouth"]); put(mr - 1, cx - 1, C["mouth"])
        put(mr, cx, C["mouth"]); put(mr - 1, cx + 1, C["mouth"])       # Zickzack
    else:                                                              # Schmollmund
        put(mr + 1, cx - 2, C["mouth"]); put(mr, cx - 1, C["mouth"])
        put(mr, cx, C["mouth"]); put(mr + 1, cx + 1, C["mouth"])


def draw(k):
    p = (k % PER_HOP) / PER_HOP
    pix, layers = render(p)
    img = shade(pix, layers, p)
    face(img, pix, layers, face_kind(k))
    return img


def main():
    args = sys.argv[1:]
    frames = [draw(k) for k in range(PER_HOP * HOPS)]
    strip = np.concatenate(frames, axis=1)
    os.makedirs(OUT_DIR, exist_ok=True)
    png = os.path.join(OUT_DIR, NAME + ".png")
    Image.fromarray(strip).save(png)
    meta = png + ".meta"
    if not os.path.exists(meta):
        sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
        from unity_meta import write_strip_meta
        write_strip_meta(meta, NAME, len(frames), CELL_W, CELL_H, PPU,
                         pivot=(0.5, (CELL_H - GROUND) / CELL_H))
    print("%s  %d Bilder  %dx%d  %d fps" % (NAME, len(frames), CELL_W, CELL_H, FPS))

    first = Image.fromarray(frames[0])
    bb = list(first.getbbox())
    # Bestiarium ohne Schatten zuschneiden
    a = np.array(first)
    solid = (a[..., 3] == 255)
    rows = np.where(solid.any(1))[0]
    cols = np.where(solid.any(0))[0]
    first.crop((cols[0], rows[0], cols[-1] + 1, rows[-1] + 1)).save(BESTIARY)

    if "--preview" in args:
        path = args[args.index("--preview") + 1]
        k = 6
        pics = []
        for fr in frames:
            im = Image.new("RGBA", (CELL_W, CELL_H), (120, 150, 110, 255))
            im.alpha_composite(Image.fromarray(fr))
            pics.append(im.resize((CELL_W * k, CELL_H * k), Image.NEAREST).convert("P"))
        pics[0].save(path, save_all=True, append_images=pics[1:], duration=int(1000 / FPS), loop=0)
        # Kontaktbogen: 2 Reihen a PER_HOP
        sheet = Image.new("RGBA", (CELL_W * PER_HOP, CELL_H * HOPS), (120, 150, 110, 255))
        for n, fr in enumerate(frames):
            sheet.alpha_composite(Image.fromarray(fr), ((n % PER_HOP) * CELL_W, (n // PER_HOP) * CELL_H))
        sheet.resize((sheet.width * 2, sheet.height * 2), Image.NEAREST).save(path.replace(".gif", "_sheet.png"))


if __name__ == "__main__":
    main()
