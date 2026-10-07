"""
Boeser Slime (EnemyId.EvilSlime): komplett neu gezeichnet und animiert.

Gleiche Figur wie das alte fin_evil_slime.png - ein grimmiger Vanillepudding-
Schleim mit einem Hackbeil im Schaedel -, aber diesmal kriecht er wirklich:

  * Raupengang in drei Takten: hinten zusammenziehen (wird hoch und schmal,
    lehnt sich zurueck), vorne vorschnellen (lang und flach, Maul auf),
    nachwabbeln (gedaempfte Feder auf Hoehe und Neigung).
  * Der Koerper ist ein Metaball-Feld: Fuss-Wulst, Kopf-Kuppel, ein Huegel um
    die Beil-Wunde und abreissende Tropfen verschmelzen weich miteinander,
    Tropfen schnueren sich mit Hals ab statt einfach zu verschwinden.
  * Das Beil ist ein starrer Koerper an einer gedaempften Drehfeder: es haengt
    der Kopfneigung hinterher und schwingt nach. Im Schleim sieht man die
    Klinge schwach durchschimmern.
  * Hinten fliegt bei jedem Zusammenziehen ein Tropfen weg und zerplatzt.
  * Ein Blaeschen steigt langsam durch den Pudding.
  * Drei Schleifen: normal, Blinzeln, und "Boing" - nach einem besonders
    harten Vorschnellen ploppt das Beil hoch und vibriert.

  Assets/Art/Gegner/new/boeser_slime_kriech.png   3 x 12 Bilder, 48x48
  PPU 32, Pivot 15 px ueber der Bodenlinie - genau wie das alte 32er-Bild,
  damit Trefferkreis (r 0.3, Versatz -0.18) und Lebensbalken stimmen.
  Assets/Resources/Bestiary/EvilSlime.png          Bild 0, zugeschnitten

Der Slime kriecht nur im Bild: der Gegner laeuft weiter gleichmaessig.

Aufruf aus dem Projektordner:
  python Tools/boeser_slime.py [--preview pfad.gif]
"""

import math
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "Assets", "Art", "Gegner", "new")
NAME = "boeser_slime_kriech"
BESTIARY = os.path.join(ROOT, "Assets", "Resources", "Bestiary", "EvilSlime.png")

CELL_W, CELL_H = 48, 48
GROUND = 44                  # erste Zeile unter dem Slime
PIVOT_UP = 15                # Pivot so weit ueber dem Boden wie beim alten Bild
CX = 23.0                    # Koerpermitte (Spalte); Beilgriff braucht rechts Platz
PPU = 32
FPS = 16
PER = 12                     # Bilder je Kriechschritt
LOOPS = 3                    # normal, Blinzeln, Boing
N = PER * LOOPS
SS = 6                       # Unterabtastung


def rgb(h, a=255):
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


C = {
    "line":     rgb("2a1411"),
    "line_lo":  rgb("4a2c22"),   # Umriss am Boden / zum Beil hin
    "deep":     rgb("9a7d63"),
    "shade":    rgb("bfa383"),
    "mid":      rgb("d9c49f"),
    "base":     rgb("ebdcbb"),
    "light":    rgb("f6ecd6"),
    "hi":       rgb("fffaee"),
    "glint":    rgb("ffffff"),
    "bounce":   rgb("d2b896"),   # Rueckstrahlung unten rechts
    # Beil
    "st_line":  rgb("221f27"),
    "st_dark":  rgb("59606d"),
    "st_mid":   rgb("8a93a1"),
    "st_lite":  rgb("b9c2cd"),
    "st_edge":  rgb("e6edf3"),
    "st_glint": rgb("ffffff"),
    "wd_line":  rgb("24120b"),
    "wd_dark":  rgb("5b2f1d"),
    "wd_mid":   rgb("80462a"),
    "wd_lite":  rgb("a4633b"),
    "rivet":    rgb("e8cf8a"),
    # Gesicht
    "eye":      rgb("1c0d0b"),
    "eglint":   rgb("ffffff"),
    "mouth":    rgb("1c0d0b"),
    "maw":      rgb("5e1a1c"),
    "tongue":   rgb("a8424a"),
    "fang":     rgb("fffaee"),
    "twang":    rgb("fff3c4"),
    "shadow":   (30, 14, 10, 70),
}


# ================================================================ Bewegung

def smooth(a, b, x):
    if x <= a:
        return 0.0
    if x >= b:
        return 1.0
    t = (x - a) / (b - a)
    return t * t * (3 - 2 * t)


def ease_out(a, b, x):
    if x <= a:
        return 0.0
    if x >= b:
        return 1.0
    t = (x - a) / (b - a)
    return 1 - (1 - t) ** 3


def wrap(x):
    return x - math.floor(x)


# Takte innerhalb eines Schritts (Phase 0..1)
G0, G1 = 0.00, 0.36          # Sammeln: hinten zieht nach vorn
S0, S1 = 0.36, 0.56          # Vorschnellen: vorne schiesst los
STRIDE = 5.0                 # px je Schritt
REST_HALF = 12.0             # halbe Laenge in Ruhe
REST_H = 16.0                # Hoehe in Ruhe
DOME_SINK = 0.15             # Kuppelmitte so weit (x Hoehe) unter dem Boden
TOP_EXP = 2.6                # > 2: runde, flache Kuppel statt Spitze
FLARE = 0.12                 # Fuss-Wulst
AREA = REST_HALF * REST_H


def targets(p, loop):
    """Ziele der Koerperform fuer Phase p (ohne Feder)."""
    g = smooth(G0, G1, p)
    s = ease_out(S0, S1, p)
    hard = 1.25 if loop == 2 else 1.0
    # Kanten: hinten zieht nach, vorne schnellt nach; plus gleichmaessiges
    # Zurueckgleiten, damit die Mitte ueber den Schritt am Fleck bleibt.
    # Beide Kanten gleiten gleichmaessig zurueck (STRIDE * p) und springen
    # in ihrem Takt um STRIDE vor - so ist die Schleife nahtlos.
    over = 1.6 * hard * math.sin(math.pi * smooth(S0, 0.80, p))   # Front schiesst ueber
    xb = REST_HALF + STRIDE * (p - g)
    xf = -REST_HALF + STRIDE * (p - s) - over
    half = (xb - xf) / 2
    h = REST_H * (REST_HALF / half) ** 0.85
    # Neigung: beim Sammeln nach hinten (+), beim Schnellen nach vorn (-)
    lean = 0.9 * math.sin(math.pi * g) * (1 - s) - 2.6 * hard * math.sin(math.pi * min(1.0, s * 1.15))
    # Kuppel-Schwerpunkt: hinten beim Sammeln, vorne beim Schnellen
    skew = 0.06 * g * (1 - s) - 0.20 * s * (1 - smooth(0.6, 0.95, p))
    return xf, xb, h, lean, skew


class Spring:
    """Gedaempfte Feder, die einem Ziel folgt (fuer Nachwabbeln)."""

    def __init__(self, k, d):
        self.k, self.d = k, d
        self.x = None
        self.v = 0.0

    def step(self, target, dt, kick=0.0):
        if self.x is None:
            self.x = target
        self.v += kick
        a = self.k * (target - self.x) - self.d * self.v
        self.v += a * dt
        self.x += self.v * dt
        return self.x


def simulate():
    """Feder-Simulation ueber viele Schleifen, letzte Schleife = periodisch."""
    sub = 40                         # Teilschritte je Bild
    dt = 1.0 / (FPS * sub)
    h_s = Spring(900.0, 14.0)        # Hoehe wabbelt
    l_s = Spring(700.0, 12.0)        # Neigung wabbelt
    a_s = Spring(300.0, 11.0)        # Beilwinkel (Grad) haengt nach
    out = None
    total = N * sub
    for rep in range(6):
        rec = []
        prev_lean = None
        for i in range(total):
            k = i / sub                          # Bild (fliessend)
            loop = int(k // PER)
            p = (k % PER) / PER
            xf, xb, h, lean, skew = targets(p, loop)
            # Aufschlag der Front: Kick auf Hoehe (flacher) und Neigung
            land = abs(p - S1) < 0.5 / (PER * sub)
            hard = 1.25 if loop == 2 else 1.0
            hk = -55.0 * hard if land else 0.0
            lk = 40.0 * hard if land else 0.0
            hh = h_s.step(h, dt, hk)
            ll = l_s.step(lean, dt, lk)
            # Beil: Ziel = Grundwinkel + Kopfneigung, kriegt Drehschwung ab
            dl = 0.0 if prev_lean is None else (ll - prev_lean)
            prev_lean = ll
            ang = a_s.step(ll * 1.6, dt, -dl * 4.0)
            pp, tt = 0.0, 0.0
            if i % sub == 0:
                rec.append(dict(p=p, loop=loop, xf=xf, xb=xb, h=hh, lean=ll, skew=skew,
                                ang=ang, pop=max(0.0, pp), tw=tt))
        out = rec
    # Boing in Schleife 3 als feste Schluesselbilder: das Beil ploppt hoch,
    # vibriert hin und her und sinkt zurueck.
    first = int(math.ceil(S1 * PER))
    for j, (pp, tt) in enumerate(BOING):
        out[2 * PER + first + j]["pop"] = pp
        out[2 * PER + first + j]["tw"] = tt
    return out


BOING = [(3.0, 15.0), (3.0, -15.0), (2.0, 7.5), (1.0, -7.5), (0.0, 0.0)]   # (px, Grad) je Bild


# ================================================================ Koerper

def body_F(st, X, Y):
    """Formfeld der Kuppel: < 1 innen, 1 auf dem Rand."""
    xf, xb, h, lean, skew = st["xf"], st["xb"], st["h"], st["lean"], st["skew"]
    xc = (xf + xb) / 2
    half = (xb - xf) / 2
    yn = np.clip(Y / h, 0, 1.5)
    Xs = X - xc - lean * yn ** 1.6               # Neigung: oben verschoben, unten fest
    t = Xs / half
    t = t - skew * (1 - np.clip(t, -1, 1) ** 2)  # Schwerpunkt verlagert
    t = t / (1 + FLARE * np.exp(-np.maximum(Y, 0) / 1.4))   # Fuss-Wulst
    c = DOME_SINK * h
    e = c / (h + c)
    t = t * (1 - e ** TOP_EXP) ** (1 / 2.0)
    return np.abs(t) ** 2.0 + np.abs((Y + c) / (h + c)) ** TOP_EXP


def ball(X, Y, x, y, r):
    d2 = (X - x) ** 2 + (Y - y) ** 2
    return (r * r / np.maximum(d2, 1e-4)) ** 1.6


def body_pot(st, X, Y):
    return 1.0 / np.maximum(body_F(st, X, Y), 1e-4) ** 1.6


def surface_y(st, x):
    """Hoehe der Kuppel ueber Weltspalte x."""
    ys = np.linspace(0, st["h"] * 1.4, 600)
    F = body_F(st, np.full_like(ys, x), ys)
    inside = np.where(F <= 1.0)[0]
    return ys[inside[-1]] if len(inside) else 0.0


def surface_tilt(st, x):
    a = surface_y(st, x - 1.5)
    b = surface_y(st, x + 1.5)
    return math.degrees(math.atan2(a - b, 3.0))   # >0: Flaeche faellt nach rechts


# ================================================================ Tropfen

def droplets(st):
    """Weggeschleuderte Tropfen hinten."""
    p, loop = st["p"], st["loop"]
    t = wrap(p - (G1 - 0.08))
    xb, h = st["xb"], st["h"]
    sx, sy = xb - 3.0, h * 0.50
    big = 1.15 if loop == 2 else 1.0
    if t < 0.10:                                 # Hals bildet sich
        a = t / 0.10
        return [("neck", sx + 2.2 * a, sy + 0.6 * a, (1.5 + 0.3 * a) * big)]
    if t < 0.46:                                 # Flug nach hinten
        u = (t - 0.10) / 0.36
        x = sx + 2.6 + 8.0 * u
        y = sy + 0.6 + 5.0 * u - (sy + 4.5) * u * u
        return [("fly", x, max(y, 1.4), 1.6 * big, u)]
    if t < 0.70:                                 # Platsch
        u = (t - 0.46) / 0.24
        return [("splat", sx + 10.6, u)]
    return []


# ================================================================ Beil

# Lokale Beilkoordinaten: u entlang der Achse (+ zum Griff), v quer (+ Ruecken).
# u = 0 ist der Eintritt in die Kuppel.
BLADE_U0, BLADE_U1 = -4.5, 6.4
BLADE_SPINE, BLADE_EDGE = 1.9, -4.9
HOLE = (4.4, 0.2)                # Aufhaengeloch: ein dunkler Pixel
BOLSTER_U1 = 7.6
HANDLE_U1 = 15.2
HANDLE_W = 1.75
RIVETS = (10.2, 13.0)
BASE_ANGLE = 45.0               # Griff nach rechts oben
ANGLE_STEP = 7.5                # Beil nur in sauberen Winkelstufen
ENTRY_DEPTH = -1.5              # Eintritt so tief unter der Kuppelhaut
STAMP = 48                      # Leinwand des Beils, Ursprung in der Mitte
_stamps = {}


def cleaver_stamp(angle):
    """Beil als fertiges Pixelbild bei 'angle' Grad (gecacht). Ursprung = Ecke (STAMP/2, STAMP/2)."""
    if angle in _stamps:
        return _stamps[angle]
    n = STAMP * SS
    ys, xs = np.mgrid[0:n, 0:n]
    X = (xs + 0.5) / SS - STAMP / 2
    Y = STAMP / 2 - (ys + 0.5) / SS
    r = math.radians(angle)
    du = (math.cos(r), math.sin(r))
    dv = (-math.sin(r), math.cos(r))
    U = X * du[0] + Y * du[1]
    V = X * dv[0] + Y * dv[1]

    cls = np.zeros((n, n), np.int8)
    # Klinge: Schneide mit leichtem Bauch, Spitzenecke an der Schneide rund
    edge = BLADE_EDGE - 0.35 * np.sin(np.clip((U - BLADE_U0) / (BLADE_U1 - BLADE_U0), 0, 1) * math.pi)
    blade = (U >= BLADE_U0) & (U <= BLADE_U1) & (V <= BLADE_SPINE) & (V >= edge)
    cu, cv, cr = BLADE_U0 + 1.6, BLADE_EDGE + 1.6, 1.6
    blade &= ~((U < cu) & (V < cv) & ((U - cu) ** 2 + (V - cv) ** 2 > cr * cr))
    cls[blade] = 1
    bolster = (U > BLADE_U1) & (U <= BOLSTER_U1) & (np.abs(V) <= HANDLE_W + 0.15)
    cls[bolster] = 2
    handle = (U > BOLSTER_U1) & (U <= HANDLE_U1) & (np.abs(V) <= HANDLE_W)
    handle &= ~((U > HANDLE_U1 - 0.9) & (np.abs(V) > HANDLE_W - 0.7))
    cls[handle] = 3

    o = cls.reshape(STAMP, SS, STAMP, SS).transpose(0, 2, 1, 3).reshape(STAMP, STAMP, SS * SS)
    counts = np.stack([(o == c).sum(-1) for c in range(4)], -1)
    pix = np.argmax(counts[..., 1:], -1) + 1
    pix[counts[..., 0] > SS * SS // 2] = 0

    img = np.zeros((STAMP, STAMP, 4), np.uint8)
    for rr in range(STAMP):
        for cc in range(STAMP):
            m = pix[rr, cc]
            if m == 0:
                continue
            x = cc + 0.5 - STAMP / 2
            y = STAMP / 2 - (rr + 0.5)
            u = x * du[0] + y * du[1]
            v = x * dv[0] + y * dv[1]
            if m == 1:
                if v < BLADE_EDGE + 1.6:
                    col = C["st_edge"]                     # geschliffene Fase
                elif v < BLADE_EDGE + 2.6:
                    col = C["st_lite"]                     # Uebergang zur Fase
                elif v > BLADE_SPINE - 1.2:
                    col = C["st_lite"]                     # Ruecken faengt Licht
                else:
                    col = C["st_mid"]
            elif m == 2:
                col = C["st_lite"] if v > 0.3 else C["st_mid"]
            else:
                col = C["wd_lite"] if v > 0.55 else C["wd_mid"] if v > -0.6 else C["wd_dark"]
            img[rr, cc] = col

    def at(rr, cc):
        return pix[rr, cc] if 0 <= rr < STAMP and 0 <= cc < STAMP else 0

    out = img.copy()
    for rr in range(STAMP):
        for cc in range(STAMP):
            m = pix[rr, cc]
            if m == 0:
                continue
            nb = [at(rr + a, cc + b) for a, b in ((1, 0), (-1, 0), (0, 1), (0, -1))]
            if 0 in nb:
                out[rr, cc] = C["wd_line"] if m == 3 else C["st_line"]
            elif m == 3 and 2 in nb:
                out[rr, cc] = C["wd_line"]
            elif m == 2 and 1 in nb:
                out[rr, cc] = C["st_dark"]
    # Aufhaengeloch und Glanzpunkt an der Spitze (nur auf Flaechenpixeln)
    for (pu, pv, col) in ((HOLE[0], HOLE[1], C["st_line"]),
                          (BLADE_U0 + 1.6, BLADE_SPINE - 1.4, C["st_glint"])):
        x = pu * du[0] + pv * dv[0] + STAMP / 2
        y = STAMP / 2 - (pu * du[1] + pv * dv[1])
        cc, rr = int(math.floor(x)), int(math.floor(y))
        if pix[rr, cc] == 1 and tuple(out[rr, cc]) != C["st_line"]:
            out[rr, cc] = col
    for ru in RIVETS:
        x = ru * du[0] + STAMP / 2
        y = STAMP / 2 - ru * du[1]
        cc, rr = int(math.floor(x)), int(math.floor(y))
        if pix[rr, cc] == 3 and tuple(out[rr, cc]) != C["wd_line"]:
            out[rr, cc] = C["rivet"]
    _stamps[angle] = (out, pix)
    return _stamps[angle]


def cleaver_pose(st):
    """Eintrittspunkt (ganze Pixel) und Winkelstufe des Beils."""
    xc = (st["xf"] + st["xb"]) / 2
    ax = xc + 2.5 + st["lean"] * 0.85
    ay = surface_y(st, ax) - ENTRY_DEPTH
    tilt = surface_tilt(st, ax)
    ang = BASE_ANGLE + 0.5 * tilt + st["ang"] + st["tw"]
    ang = round(ang / ANGLE_STEP) * ANGLE_STEP
    r = math.radians(ang)
    ax += math.cos(r) * st["pop"]
    ay += math.sin(r) * st["pop"]
    return int(round(CX + ax)), int(round(GROUND - ay)), ang


# ================================================================ Rendern

EMPTY, SHADOW, BODY, DROP, SPLAT = range(5)


def render(st):
    H, W = CELL_H * SS, CELL_W * SS
    ys, xs = np.mgrid[0:H, 0:W]
    X = (xs + 0.5) / SS - CX
    Y = GROUND - (ys + 0.5) / SS

    pot = body_pot(st, X, Y)
    dropm = np.zeros((H, W), bool)
    splatm = np.zeros((H, W), bool)
    for dr in droplets(st):
        if dr[0] == "neck":
            _, x, y, r = dr
            pot = pot + ball(X, Y, x, y, r)
        elif dr[0] == "fly":
            continue                             # fliegender Tropfen = Stempel
        else:
            _, x, u = dr
            w = 2.0 + 1.8 * u
            hh = 2.0 * (1 - u) + 0.9
            splatm |= ((X - x) / w) ** 2 + (Y / hh) ** 2 <= 1.0
            if u < 0.6:                          # zwei Spritzer
                for sx, sy in ((x - 2.2 - 2.0 * u, 1.8 + 3.0 * u - 6 * u * u),
                               (x + 2.4 + 2.2 * u, 1.4 + 3.4 * u - 7 * u * u)):
                    if sy > 0.4:
                        splatm |= (X - sx) ** 2 + (Y - sy) ** 2 <= 0.6 ** 2
    bodym = (pot >= 1.0) & (Y >= 0)
    splatm &= Y >= 0

    owner = np.zeros((H, W), np.int16)
    xc = (st["xf"] + st["xb"]) / 2
    half = (st["xb"] - st["xf"]) / 2
    owner[((X - xc - 0.6) / (half + 1.8)) ** 2 + ((Y + 0.3) / 2.0) ** 2 <= 1.0] = SHADOW
    owner[splatm] = SPLAT
    owner[bodym] = BODY
    owner[dropm] = DROP

    o = owner.reshape(CELL_H, SS, CELL_W, SS).transpose(0, 2, 1, 3).reshape(CELL_H, CELL_W, SS * SS)
    codes = np.unique(owner)
    counts = np.stack([(o == c).sum(-1) for c in codes], -1)
    pix = codes[np.argmax(counts, -1)]
    pix[(o == EMPTY).sum(-1) > SS * SS // 2] = EMPTY
    pix[((o == DROP).sum(-1) >= SS * SS * 0.4) & (pix == EMPTY)] = DROP
    return pix


LIGHT = np.array([-0.55, 0.62, 0.56])
LIGHT = LIGHT / np.linalg.norm(LIGHT)


def body_lambert(st):
    """Licht je Pixel aus der Normalen des Formfelds (Pseudo-3D)."""
    ys, xs = np.mgrid[0:CELL_H, 0:CELL_W]
    X = xs + 0.5 - CX
    Y = GROUND - (ys + 0.5)
    e = 0.5
    F = body_F(st, X, Y)
    gx = (body_F(st, X + e, Y) - body_F(st, X - e, Y)) / (2 * e)
    gy = (body_F(st, X, Y + e) - body_F(st, X, Y - e)) / (2 * e)
    z = np.sqrt(np.clip(1 - F, 0, 1))
    k = st["h"] * 0.55
    # F waechst nach aussen: der Gradient zeigt dorthin, wohin die Flaeche schaut
    nx, ny, nz = gx * k, gy * k, 2 * z
    ln = np.sqrt(nx * nx + ny * ny + nz * nz) + 1e-6
    lam = (nx * LIGHT[0] + ny * LIGHT[1] + nz * LIGHT[2]) / ln
    return lam, Y, nx / ln


def shade(pix, st):
    Hh, Ww = pix.shape
    img = np.zeros((Hh, Ww, 4), np.uint8)
    lam, Ymap, nxm = body_lambert(st)

    def at(r, c):
        return pix[r, c] if 0 <= r < Hh and 0 <= c < Ww else EMPTY

    for r in range(Hh):
        for c in range(Ww):
            m = pix[r, c]
            if m == EMPTY:
                continue
            if m == SHADOW:
                img[r, c] = C["shadow"]
                continue
            if m == BODY:
                Y = Ymap[r, c]
                l = lam[r, c]
                if Y < 1.0:
                    col = C["deep"]
                elif Y < 2.0:
                    col = C["bounce"] if nxm[r, c] > 0.3 else C["shade"]
                elif l > 0.86:
                    col = C["hi"]
                elif l > 0.66:
                    col = C["light"]
                elif l > 0.40:
                    col = C["base"]
                elif l > 0.12:
                    col = C["mid"]
                else:
                    col = C["shade"]
            elif m == DROP:
                col = C["light"] if at(r - 1, c) == EMPTY and at(r, c - 1) == EMPTY else C["base"]
                if at(r + 1, c) != DROP:
                    col = C["mid"]
            else:
                col = C["mid"] if at(r - 1, c) == SPLAT else C["light"]
            img[r, c] = col

    out = img.copy()
    for r in range(Hh):
        for c in range(Ww):
            m = pix[r, c]
            if m in (EMPTY, SHADOW):
                continue
            nb = [at(r + a, c + b) for a, b in ((1, 0), (-1, 0), (0, 1), (0, -1))]
            if any(n in (EMPTY, SHADOW) for n in nb):
                out[r, c] = C["line_lo"] if m == SPLAT else C["line"]
    return out


def insert_cleaver(img, pix, st):
    """Beil einsetzen: ausserhalb deckend, im Pudding schimmert die Klinge durch."""
    c0, r0, ang = cleaver_pose(st)
    stamp, spix = cleaver_stamp(ang)
    tint = np.array(C["st_dark"][:3], float)
    for rr in range(STAMP):
        for cc in range(STAMP):
            if spix[rr, cc] == 0:
                continue
            r, c = r0 - STAMP // 2 + rr, c0 - STAMP // 2 + cc
            if not (0 <= r < CELL_H and 0 <= c < CELL_W):
                continue
            if pix[r, c] == BODY and tuple(img[r, c]) == C["line"]:
                continue                                   # Haut vor der Klinge
            inside = pix[r, c] == BODY
            if inside:
                if spix[rr, cc] != 1:
                    continue
                base = img[r, c, :3].astype(float)
                # Klingenumriss schimmert staerker als die Flaeche
                k = 0.34 if tuple(stamp[rr, cc]) == C["st_line"] else 0.18
                img[r, c, :3] = (base * (1 - k) + tint * k).astype(np.uint8)
            else:
                img[r, c] = stamp[rr, cc]
    return c0, r0, ang


# ================================================================ Gesicht

# Stempel: '#' Auge, 'w' Glanz, 'b' Braue, 'm' Mund, 'r' Rachen,
# 't' Zunge, 'f' Zahn. Vorderes (linkes) Auge; das hintere wird gespiegelt.
EYES = {
    "grimm": ("#....",              # schraeger Keil, innen tiefer
              "###..",
              "#w###",
              ".####"),
    "kneif": ("##...",              # > <
              "..##.",
              "##..."),
    "zu":    ("#....",
              ".###.",
              "...##"),
}
MOUTHS = {
    "grimm": ("mmmmm",              # Fletschen mit zwei Hauern
              "mfrfm",
              ".mmm."),
    "kneif": ("m.m.m",              # Zaehne zusammengebissen (Zickzack)
              ".m.m."),
    "fauch": (".mmmmm.",
              "mfrrrfm",
              "mrrttrm",
              ".mmmmm."),
    "auf":   ("mmmmm",
              "mfrfm",
              "mrtrm",
              ".mmm."),
}


def face_kind(st, k):
    p, loop = st["p"], st["loop"]
    eye, mouth = "grimm", "grimm"
    if G1 - 0.18 <= p < S0:
        eye, mouth = "kneif", "kneif"
    elif S0 <= p < S1 + 0.10:
        eye, mouth = "grimm", "fauch"
    elif S1 + 0.10 <= p < S1 + 0.20:
        eye, mouth = "grimm", "auf"
    if loop == 1 and (k % PER) in (9, 10):
        eye = "zu"
    return eye, mouth


def stamp_face(out, pix, rows, r0, c0, mirror=False):
    for dr, row in enumerate(rows):
        row = row[::-1] if mirror else row
        for dc, ch in enumerate(row):
            if ch == ".":
                continue
            r, c = r0 + dr, c0 + dc
            if not (0 <= r < CELL_H and 0 <= c < CELL_W) or pix[r, c] != BODY:
                continue
            out[r, c] = {"#": C["eye"], "w": C["eglint"], "b": C["line"], "m": C["mouth"],
                         "r": C["maw"], "t": C["tongue"], "f": C["fang"]}[ch]


def face(img, pix, st, k):
    eye, mouth = face_kind(st, k)
    xf, xb, h = st["xf"], st["xb"], st["h"]
    xc = (xf + xb) / 2
    half = (xb - xf) / 2
    yv = 0.48
    fx = xc - 0.30 * half + st["lean"] * yv ** 1.6
    fy = h * yv
    r_eye = int(round(GROUND - fy)) - 2
    c_mid = int(round(CX + fx))
    rows = EYES[eye]
    w = len(rows[0])
    gap = 1 if half < 11 else 2                    # Nasenruecken zwischen den Augen
    stamp_face(img, pix, rows, r_eye, c_mid - gap // 2 - w)
    stamp_face(img, pix, rows, r_eye, c_mid + (gap - gap // 2), mirror=True)
    m = MOUTHS[mouth]
    mr = r_eye + 5
    stamp_face(img, pix, m, mr, c_mid - len(m[0]) // 2)


def glint(img, pix, st):
    """Glanzlicht am hellsten Punkt der Kuppel."""
    lam, _, _ = body_lambert(st)
    best, pos = -9, None
    for r in range(CELL_H):
        for c in range(CELL_W):
            if pix[r, c] == BODY and tuple(img[r, c]) == C["hi"] and lam[r, c] > best:
                best, pos = lam[r, c], (r, c)
    if pos is None:
        return
    r, c = pos
    for dr, dc in ((0, 0), (0, 1), (1, 0)):
        if pix[r + dr, c + dc] == BODY and tuple(img[r + dr, c + dc]) == C["hi"]:
            img[r + dr, c + dc] = C["glint"]


def bubble(img, pix, st, k):
    """Ein Blaeschen steigt ueber die ganze Schleife durch den Pudding."""
    u = k / N
    if u > 0.9:
        return
    xc = (st["xf"] + st["xb"]) / 2
    half = (st["xb"] - st["xf"]) / 2
    x = xc + 0.40 * half + 1.0 * math.sin(u * 2 * math.pi * 2)
    y = 2.5 + u * (st["h"] - 4.5)
    c, r = int(math.floor(CX + x)), int(math.floor(GROUND - y))
    for (dr, dc, col) in ((0, 0, C["hi"]), (0, 1, C["light"]), (1, 0, C["light"])):
        rr, cc = r + dr, c + dc
        if 0 <= rr < CELL_H and 0 <= cc < CELL_W and pix[rr, cc] == BODY \
                and tuple(img[rr, cc]) in (C["base"], C["mid"], C["shade"]):
            img[rr, cc] = col


def twang_lines(img, st, pose):
    """Boing-Striche neben dem Griffende, solange das Beil vibriert."""
    if st["loop"] != 2 or abs(st["tw"]) < 3.0:
        return
    c0, r0, ang = pose
    rad = math.radians(ang)
    tip = HANDLE_U1 + 1.5
    x = c0 + math.cos(rad) * tip
    y = r0 - math.sin(rad) * tip
    marks = ((2, 0), (3, 0), (-1, -2), (-1, -3)) if st["tw"] > 0 else ((2, 1), (3, 1), (0, -3), (0, -4))
    for dx, dy in marks:
        c, r = int(round(x + dx)), int(round(y + dy))
        if 0 <= r < CELL_H and 0 <= c < CELL_W and img[r, c, 3] == 0:
            img[r, c] = C["twang"]


DROP_STAMPS = {
    "rund": (".##.",
             "#lb#",
             "#bm#",
             ".##."),
    "lang": (".#.",
             "#l#",
             "#b#",
             "#m#",
             ".#."),
}


def flying_drops(img, st):
    """Fliegende Tropfen als handgesetzte Stempel (sauberer als gerastert)."""
    for dr in droplets(st):
        if dr[0] != "fly":
            continue
        _, x, y, r, u = dr
        rows = DROP_STAMPS["lang" if u > 0.55 else "rund"]
        c0 = int(round(CX + x)) - len(rows[0]) // 2
        r0 = int(round(GROUND - y)) - len(rows) // 2
        for i, row in enumerate(rows):
            for j, ch in enumerate(row):
                rr, cc = r0 + i, c0 + j
                if ch == "." or not (0 <= rr < CELL_H and 0 <= cc < CELL_W) or rr >= GROUND:
                    continue
                img[rr, cc] = {"#": C["line"], "l": C["hi"], "b": C["light"], "m": C["mid"]}[ch]


# ================================================================ Ablauf

def draw(states, k):
    st = states[k]
    pix = render(st)
    img = shade(pix, st)
    glint(img, pix, st)
    bubble(img, pix, st, k)
    face(img, pix, st, k)
    flying_drops(img, st)
    pose = insert_cleaver(img, pix, st)
    twang_lines(img, st, pose)
    return img


def main():
    args = sys.argv[1:]
    states = simulate()
    frames = [draw(states, k) for k in range(N)]
    strip = np.concatenate(frames, axis=1)
    os.makedirs(OUT_DIR, exist_ok=True)
    png = os.path.join(OUT_DIR, NAME + ".png")
    Image.fromarray(strip).save(png)
    meta = png + ".meta"
    if not os.path.exists(meta):
        sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
        from unity_meta import write_strip_meta
        write_strip_meta(meta, NAME, len(frames), CELL_W, CELL_H, PPU,
                         pivot=(CX / CELL_W, (CELL_H - GROUND + PIVOT_UP) / CELL_H))
    print("%s  %d Bilder  %dx%d  %d fps" % (NAME, len(frames), CELL_W, CELL_H, FPS))

    # Bestiarium: Bild 0 ohne Schatten
    a = frames[0]
    solid = a[..., 3] == 255
    rows = np.where(solid.any(1))[0]
    cols = np.where(solid.any(0))[0]
    Image.fromarray(a).crop((cols[0], rows[0], cols[-1] + 1, rows[-1] + 1)).save(BESTIARY)

    if "--preview" in args:
        path = args[args.index("--preview") + 1]
        k = 8
        bg = (120, 150, 110, 255)
        pics = []
        for fr in frames:
            im = Image.new("RGBA", (CELL_W, CELL_H), bg)
            im.alpha_composite(Image.fromarray(fr))
            pics.append(im.resize((CELL_W * k, CELL_H * k), Image.NEAREST).convert("P"))
        pics[0].save(path, save_all=True, append_images=pics[1:], duration=int(1000 / FPS), loop=0)
        sheet = Image.new("RGBA", (CELL_W * PER, CELL_H * LOOPS), bg)
        for n, fr in enumerate(frames):
            sheet.alpha_composite(Image.fromarray(fr), ((n % PER) * CELL_W, (n // PER) * CELL_H))
        sheet.resize((sheet.width * 3, sheet.height * 3), Image.NEAREST).save(path.replace(".gif", "_sheet.png"))


if __name__ == "__main__":
    main()
