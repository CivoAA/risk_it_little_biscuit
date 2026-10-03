"""
Zeichnet den Void Spike (Leerenstachel) neu: ingame-Animation + Icons.

  Assets/Art/Waffen/fin_void_sugar.png        26 Frames je 48x96, PPU 64 (Prefab-Scale 2
                                             -> 1 Texel = 1 Weltpixel), Pivot = Portalmitte
  Assets/Animations/Voidspike.anim            Keyframes + Zeiten neu (Halte-Frames, kein Loop)
  Assets/Art/Icons/icon_void_sugar.png        32x32 Waffen-/Inventar-Icon (Sprite-Rect 32x31)
  Assets/Resources/Workbench/void_spike_14/_10.png

Eine Zuckerstange als Stachel: rot-weiss gedreht, spitz, schiesst wie ein
Bohrer aus einem violetten Leerenportal (Wirbel im Inneren, leuchtender Rand),
federt nach, glitzert an der Spitze, sinkt zurueck, und das Portal implodiert.

Die alte PNG war ein herunterskaliertes Bild (800+ Mischfarben) mit Frames, die
nicht im 32er Raster sassen. Hier: nur Palettenfarben, jedes Frame im Raster.

Die .meta der PNG behaelt guid und alle Sprite-IDs (Prefab + Anim verweisen
darauf); das Skript setzt darin nur Rechtecke und Pivot neu.

Aufruf aus dem Projektordner:  python Tools/void_spike.py [--preview pfad.png] [--dry]
"""

import math
import os
import random
import re
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SHEET = os.path.join(ROOT, "Assets", "Art", "Waffen", "fin_void_sugar.png")
ANIM = os.path.join(ROOT, "Assets", "Animations", "Voidspike.anim")
ICON = os.path.join(ROOT, "Assets", "Art", "Icons", "icon_void_sugar.png")
WORKBENCH = os.path.join(ROOT, "Assets", "Resources", "Workbench")

FW, FH = 48, 96          # Framegroesse
CX, CY = 23.5, 12.0      # Pivot in Pixeln (x Spaltenmitte -> 1px-Spitze, y = Portalmitte)
FRAMES = 26

CLEAR = (0, 0, 0, 0)


def hx(s):
    s = s.lstrip("#")
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), 255)


# Zuckerstange
OUT = hx("#2a0d26")
W_HI, W, W_SH, W_DEEP = hx("#ffffff"), hx("#f6ecef"), hx("#d8c3d6"), hx("#a88fb4")
R_HI, R, R_SH, R_DEEP = hx("#ff7a7a"), hx("#e8293f"), hx("#b0163f"), hx("#6e0f3c")
# Leere (Paletten wie Blade Storm)
P_OUT, P_DEEP, P_D1, P_D2 = hx("#140a1f"), hx("#0b0514"), hx("#1c0d33"), hx("#2b1745")
P_ARM, P_ARM_HI = hx("#46256f"), hx("#6b3aa6")
RIM_D, RIM, RIM_HI, RIM_W = hx("#6a2fb0"), hx("#9b5cf0"), hx("#d9aaff"), hx("#f6e3ff")
GLOW, GLOW_HI = hx("#ff4fd8"), hx("#ffc4f2")

# Reflektiertes Portallicht unten am Stachel
TINT = {W_HI: W_SH, W: W_SH, W_SH: RIM_HI, W_DEEP: RIM, R_HI: R, R: R_SH, R_SH: RIM_D, R_DEEP: P_ARM}
SINK = {W_HI: W_DEEP, W: W_DEEP, W_SH: RIM_D, W_DEEP: P_ARM, R_HI: R_DEEP, R: R_DEEP, R_SH: P_ARM, R_DEEP: P_D2}


# ----------------------------------------------------------------------------
#  Zeitplan: je Frame Dauer + Zustand
#  h = Stachelhoehe, s = Breitenfaktor, rx/ry = Portal, spin = Streifendrehung
# ----------------------------------------------------------------------------

def plan():
    P = []

    def f(dur, **k):
        d = dict(h=0, s=1.0, rx=17, ry=6.0, spin=0.0, portal="open", shake=0)
        d.update(k)
        d["dur"] = dur
        P.append(d)

    # Portal oeffnet sich (Vorwarnung)
    f(0.03, rx=3, ry=1.0, portal="seed")
    f(0.03, rx=9, ry=3.0)
    f(0.03, rx=15, ry=5.0)
    f(0.04, rx=18, ry=6.5, portal="charge")
    # Ausbruch: Schmiere, Ueberschwingen, Stauchen, Ausfedern
    f(0.03, h=36, s=0.78, spin=0.55, portal="burst")
    f(0.03, h=66, s=0.86, spin=1.00)
    f(0.03, h=53, s=1.16, spin=1.30)
    f(0.03, h=60, s=0.96, spin=1.48)
    f(0.04, h=58, spin=1.60)
    # Stehen, langsam weiterdrehen
    for i in range(5):
        f(0.04, h=58, spin=1.70 + 0.09 * i)
    # Zittern, dann zurueck in die Leere
    f(0.03, h=59, spin=2.20, shake=1)
    f(0.025, h=40, s=0.9, spin=2.05)
    f(0.025, h=18, s=0.85, spin=1.85)
    f(0.025, h=5, s=0.85, spin=1.70, portal="gulp")
    # Portal implodiert
    f(0.03, rx=17, ry=6.0, portal="flash")
    f(0.025, rx=12, ry=4.0)
    f(0.025, rx=7, ry=2.5)
    f(0.025, rx=3, ry=1.0, portal="seed")
    f(0.03, portal="poof1")
    f(0.03, portal="poof2")
    f(0.03, portal="none")
    f(0.03, portal="none")
    assert len(P) == FRAMES, len(P)
    t = 0.0
    for d in P:
        d["t"] = t
        t += d["dur"]
    return P


T_BURST = 0.13   # Zeitpunkt des Ausbruchs (Frame 4)


# ----------------------------------------------------------------------------
#  Zeichenhilfen
# ----------------------------------------------------------------------------

class Canvas:
    def __init__(self, w, h, cx, cy):
        self.w, self.h, self.cx, self.cy = w, h, cx, cy
        self.img = Image.new("RGBA", (w, h), CLEAR)
        self.px = self.img.load()

    def put(self, i, j, c):
        """i Spalte, j Zeile von UNTEN."""
        if 0 <= i < self.w and 0 <= j < self.h:
            self.px[i, self.h - 1 - j] = c

    def get(self, i, j):
        if 0 <= i < self.w and 0 <= j < self.h:
            return self.px[i, self.h - 1 - j]
        return CLEAR

    def at(self, x, y, c):
        """x/y relativ zum Pivot (y nach oben), gerundet auf das Raster."""
        self.put(int(math.floor(self.cx + x)), int(math.floor(self.cy + y)), c)

    def coords(self):
        for j in range(self.h):
            for i in range(self.w):
                yield i, j, i + 0.5 - self.cx, j + 0.5 - self.cy


def outline(cv, mask, color):
    """Kontur um eine Maskenmenge (Pixel-Tupel), nur auf leere Pixel."""
    for (i, j) in list(mask):
        for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (i + di, j + dj)
            if n not in mask and cv.get(*n)[3] == 0:
                cv.put(n[0], n[1], color)


# ----------------------------------------------------------------------------
#  Portal
# ----------------------------------------------------------------------------

def portal_d(x, y, rx, ry):
    return math.sqrt((x / rx) ** 2 + (y / ry) ** 2)


def draw_portal_back(cv, rx, ry, t, mode, big=True):
    """Innenraum + hinterer Rand. Liefert die Maske des ganzen Portals."""
    mask = set()
    rim_w = 1.6 if big else 1.0
    for i, j, x, y in cv.coords():
        d = portal_d(x, y, rx, ry)
        if d > 1.0:
            continue
        mask.add((i, j))
        # Abstand zum Rand in Pixeln (grob)
        inner = portal_d(x, y, max(rx - rim_w * 1.4, 0.5), max(ry - rim_w, 0.4))
        if mode in ("seed",):
            cv.put(i, j, GLOW_HI if abs(x) < rx * 0.45 else GLOW)
            continue
        if mode == "flash":
            if inner > 1.0:
                cv.put(i, j, RIM_W if y > -0.8 else GLOW_HI)
            else:
                cv.put(i, j, P_DEEP if inner > 0.35 else (GLOW if inner > 0.18 else W_HI))
            continue
        if inner > 1.0:
            # Rand: hinten hell (Licht von oben), vorne kraeftig
            if y > 0.6:
                c = RIM_HI if abs(x) < rx * 0.55 else RIM
            elif y > -0.8:
                c = RIM
            else:
                c = RIM_D
            if mode in ("charge", "burst", "gulp"):
                c = {RIM_D: RIM, RIM: RIM_HI, RIM_HI: RIM_W}[c]
            cv.put(i, j, c)
            continue
        # Innen: Wirbel aus zwei Armen, dreht sich mit der Zeit
        a = math.atan2(y / ry, x / rx)
        r = inner
        speed = 26.0 if mode in ("charge", "gulp") else 14.0
        v = math.sin(2 * a + r * 7.0 - t * speed)
        if r > 0.82:
            c = P_D2
            if v > 0.3:
                c = P_ARM
        elif r > 0.45:
            c = P_D1
            if v > 0.55:
                c = P_ARM if v < 0.9 else P_ARM_HI
            elif v > 0.15:
                c = P_D2
        elif r > 0.28:
            c = P_D1 if v > 0.2 else P_DEEP
        else:
            c = P_DEEP
        cv.put(i, j, c)
    # Sterne im Nichts (blinken pro Frame)
    if mode in ("open", "burst") and rx >= 12 and big:
        rng = random.Random(int(t * 1000) // 25)
        sx = rng.choice((-1, 1)) * rng.uniform(rx * 0.3, rx * 0.55)
        sy = rng.uniform(-ry * 0.3, ry * 0.3)
        cv.at(sx, sy, RIM_W if rng.random() < 0.5 else GLOW_HI)
    return mask


def draw_portal_front(cv, rx, ry, mode, big=True):
    """Vordere Lippe ueber dem Stachelfuss."""
    if mode in ("seed", "flash"):
        return
    rim_w = 1.6 if big else 1.0
    for i, j, x, y in cv.coords():
        if y > 0:
            continue
        d = portal_d(x, y, rx, ry)
        inner = portal_d(x, y, max(rx - rim_w * 1.4, 0.5), max(ry - rim_w, 0.4))
        if d <= 1.0 and inner > 1.0 and abs(x) < rx * 0.8:
            c = RIM_D if y < -0.8 else RIM
            # Glanzlinie auf der Lippe
            if big and -ry + 1.0 < y < -ry + 2.2 and abs(x) < rx * 0.35:
                c = RIM
            if mode in ("charge", "burst", "gulp"):
                c = {RIM_D: RIM, RIM: RIM_HI}[c]
            cv.put(i, j, c)


def glow_ring(cv, rx, ry, color, density=1.0, seed=0):
    """Dünner Ring knapp ausserhalb, ausgeduennt fuer 'Verblassen'."""
    rng = random.Random(seed)
    for i, j, x, y in cv.coords():
        d = portal_d(x, y, rx, ry)
        d_in = portal_d(x, y, rx - 1.0, ry - 0.7)
        if d <= 1.0 and d_in > 1.0 and cv.get(i, j)[3] == 0:
            if density >= 1.0 or rng.random() < density:
                cv.put(i, j, color)


# ----------------------------------------------------------------------------
#  Stachel
# ----------------------------------------------------------------------------

def spike_radius(h, H, R):
    if h < 0 or h > H:
        return -1
    k = h / H
    # kraeftiger Koerper, der sich zur Spitze hin zuspitzt
    return R * (1.0 - k ** 1.25) ** 0.85 + 0.02


def draw_spike(cv, H, rad, spin, base=-2.0, stripes=2, pitch=16.0, sink_rows=4, tint_rows=8, gloss=True):
    """Gedrehte Zuckerstange als Kegel. Streifen wickeln sich um den Koerper
    (asin der Querposition), spin dreht sie - beim Ausbruch bohrt er sich hoch."""
    if H <= 0:
        return set()
    mask = set()
    for i, j, x, y in cv.coords():
        h = y - base
        r = spike_radius(h, H - base, rad)
        if r < 0 or abs(x) > r:
            continue
        u = max(-1.0, min(1.0, x / max(r, 0.5)))
        theta = math.asin(u)
        ph = (theta / (2 * math.pi)) * stripes + h / pitch - spin
        f = ph - math.floor(ph)
        red = f < 0.40
        # Licht von oben links
        if u < -0.55:
            tone = 0
        elif u < 0.30:
            tone = 1
        elif u < 0.72:
            tone = 2
        else:
            tone = 3
        c = (R_HI, R, R_SH, R_DEEP)[tone] if red else (W_HI, W, W_SH, W_DEEP)[tone]
        # durchgehende Glanzlinie (Zuckerglasur) links der Mitte
        if gloss and r > 2.6 and -0.5 <= u < -0.22 and (H - base) - h > 6:
            c = W_HI if not red else R_HI
        # Spitze weiss glasiert
        if (H - base) - h < 2.5:
            c = W_HI if u <= 0.0 else W_SH
        # unten frisst die Leere am Stachel: zackige dunkle Kante, darueber Portallicht
        lick = sink_rows + (1.5 * math.sin(x * 1.9 + spin * 9.0) if sink_rows > 1 else 0)
        if y < base + lick - 1.5:
            c = P_D2 if (i + j) % 3 else P_ARM
        elif y < base + lick:
            c = SINK.get(c, c)
        elif y < base + tint_rows and u > 0.6 or y < base + lick + 1.5:
            c = TINT.get(c, c)
        cv.put(i, j, c)
        mask.add((i, j))
    return mask


def outline_px(cv, mask):
    return {(i + di, j + dj) for (i, j) in mask for di, dj in ((1, 0), (-1, 0), (0, 1))}


def spike_outline(cv, mask, base):
    """Kontur, aber nicht unterhalb der Portalmitte (dort steckt er in der Leere)."""
    for (i, j) in list(mask):
        for di, dj in ((1, 0), (-1, 0), (0, 1)):
            n = (i + di, j + dj)
            if n in mask:
                continue
            y = n[1] + 0.5 - cv.cy
            if y < 1.0 and di != 0:
                continue
            cv.put(n[0], n[1], OUT)


def sparkle(cv, x, y, size, core=W_HI, arm=GLOW_HI, tip=RIM_HI):
    """Vierzackiger Stern."""
    cv.at(x, y, core)
    for k in range(1, size + 1):
        c = arm if k < size else tip
        for dx, dy in ((k, 0), (-k, 0), (0, k), (0, -k)):
            cv.at(x + dx, y + dy, c)
    if size >= 3:
        for dx, dy in ((1, 1), (-1, 1), (1, -1), (-1, -1)):
            cv.at(x + dx, y + dy, tip)


# ----------------------------------------------------------------------------
#  Partikel (deterministisch, ueber echte Zeit gerechnet)
# ----------------------------------------------------------------------------

def make_shards():
    rng = random.Random(7)
    shards = []
    for k in range(10):
        side = -1 if k % 2 == 0 else 1
        vx = side * rng.uniform(40, 120)
        vy = rng.uniform(150, 260)
        x0 = side * rng.uniform(1, 5)
        col = rng.choice([W, W_HI, R, R_HI, GLOW_HI, W])
        big = rng.random() < 0.4
        shards.append((x0, vx, vy, col, big))
    return shards


SHARDS = make_shards()
GRAV = 900.0


def draw_shards(cv, t, behind=frozenset()):
    dt = t - T_BURST
    if dt < 0 or dt > 0.42:
        return
    for (x0, vx, vy, col, big) in SHARDS:
        x = x0 + vx * dt * 0.55
        y = 2.0 + vy * dt - 0.5 * GRAV * dt * dt
        if y < -3:
            continue
        if (int(math.floor(cv.cx + x)), int(math.floor(cv.cy + y))) in behind:
            continue
        c = col if dt < 0.28 else {W: W_SH, W_HI: W, R: R_SH, R_HI: R, GLOW_HI: GLOW}.get(col, col)
        cv.at(x, y, c)
        if big and dt < 0.3:
            cv.at(x + (1 if vx > 0 else -1), y, c)


def make_wisps():
    rng = random.Random(3)
    return [(rng.uniform(-15, 15), rng.uniform(0.0, 0.75), rng.uniform(25, 45), rng.choice([RIM, RIM_HI, GLOW]))
            for _ in range(14)]


WISPS = make_wisps()


def draw_wisps(cv, t, rx):
    if rx < 12:
        return
    for (x0, t0, vy, col) in WISPS:
        dt = t - t0
        if dt < 0 or dt > 0.22:
            continue
        x = x0 * rx / 17.0 + math.sin(dt * 30 + x0) * 1.2
        y = 2.0 + vy * dt
        if cv.get(int(math.floor(cv.cx + x)), int(math.floor(cv.cy + y)))[3]:
            continue
        cv.at(x, y, col if dt < 0.14 else RIM_D)


def shockwave(cv, t):
    dt = t - T_BURST
    if dt < 0 or dt > 0.2:
        return
    k = dt / 0.2
    rx = 18 + 5.5 * k
    ry = 6.5 + 2.5 * k
    col = RIM_W if k < 0.2 else (RIM_HI if k < 0.55 else RIM)
    glow_ring(cv, rx, ry, col, density=1.0 - k * 0.75, seed=int(dt * 1000))


def speed_lines(cv, H, rad):
    for x, l, c in ((-(rad + 3), 14, RIM_HI), (rad + 3, 11, RIM_HI), (-(rad + 6), 8, RIM), (rad + 5, 16, RIM_W)):
        top = H * 0.75
        for k in range(l):
            cv.at(x, top - k * 1.0 - (3 if x > 0 else 0), c)


# ----------------------------------------------------------------------------
#  Ein Frame
# ----------------------------------------------------------------------------

def render_frame(d):
    cv = Canvas(FW, FH, CX + d["shake"] * 0, CY)
    mode, t = d["portal"], d["t"]
    if mode == "none":
        return cv.img
    if mode in ("poof1", "poof2"):
        if mode == "poof1":
            sparkle(cv, 0, 0, 3)
            for dx, dy in ((-6, 2), (6, 2), (-4, -2), (5, -1)):
                cv.at(dx, dy, GLOW_HI)
            for dx, dy in ((-9, 4), (9, 3)):
                cv.at(dx, dy, RIM)
        else:
            sparkle(cv, 0, 1, 1, core=GLOW_HI, arm=RIM_HI, tip=RIM)
            for dx, dy in ((-8, 4), (8, 4), (-11, 6), (12, 5)):
                cv.at(dx, dy, RIM if abs(dx) < 10 else RIM_D)
        return cv.img

    rx, ry = d["rx"], d["ry"]
    shake = d["shake"]
    if shake:
        cv.cx = CX + 1

    pmask = draw_portal_back(cv, rx, ry, t, mode)
    # Kontur unter dem Portal
    for (i, j) in list(pmask):
        for di, dj in ((0, -1), (1, 0), (-1, 0)):
            n = (i + di, j + dj)
            if n not in pmask and cv.get(*n)[3] == 0:
                cv.put(n[0], n[1], P_OUT)
    if mode in ("charge", "burst", "flash", "gulp"):
        glow_ring(cv, rx + 2.0, ry + 1.3, GLOW if mode != "flash" else GLOW_HI, density=0.5, seed=int(t * 999))

    H, rad = d["h"], 8.5 * d["s"]
    base = -2.0
    smask = draw_spike(cv, H, rad, d["spin"], base=base)
    spike_outline(cv, smask, base)
    draw_portal_front(cv, rx, ry, mode)

    if mode == "burst" or d["h"] == 66:
        speed_lines(cv, H, rad)
    if mode == "charge":
        sparkle(cv, 0, 4, 2)
    shockwave(cv, t)
    draw_shards(cv, t, smask | outline_px(cv, smask))
    draw_wisps(cv, t, rx)

    # Glitzern an der Spitze nach dem Ausfedern
    if H >= 55:
        top = H - 0.5
        dt = t - 0.29
        if 0 <= dt < 0.05:
            sparkle(cv, 0, top + 1, 4)
        elif 0.05 <= dt < 0.1:
            sparkle(cv, 0, top + 1, 2)
        elif 0.1 <= dt < 0.14:
            cv.at(0, top + 1, W_HI)
        # zweiter, kleiner Blitz waehrend des Stehens
        if 0.18 <= dt < 0.22:
            sparkle(cv, -1, top - 9, 1, core=W_HI, arm=W_HI, tip=GLOW_HI)
    if mode == "gulp":
        sparkle(cv, 0, 6, 2, core=W_HI, arm=GLOW_HI, tip=GLOW)
    return cv.img


# ----------------------------------------------------------------------------
#  Icons
# ----------------------------------------------------------------------------

# Werkbank-Icons als Schablone (zu klein fuer die Formeln). Zeichen -> Farbe:
#  O Kontur  h Zuckerglanz  L/P/p Portalrand hell/mittel/dunkel  V/v Leere/Wirbel
#  x Stachel: Streifen schraeg, rechte Spalte im Schatten, linke mit Glanz;
#    in Zeilen mit Portalrand (L/P) wird er vom Leerenlicht getoent
SMALL_KEY = {"O": OUT, "h": W_HI, "L": RIM_HI, "P": RIM, "p": RIM_D, "V": P_DEEP, "v": P_ARM}
SMALL = {
    14: [
        ".......O......",
        "......OhO.....",
        "......OxO.....",
        ".....OxxxO....",
        ".....OxxxO....",
        ".....OxxxO....",
        "....OxxxxxO...",
        "....OxxxxxO...",
        "....OxxxxxO...",
        "...OLxxxxxLO..",
        "..OPVxxxxxVPO.",
        "..OpVVvVvVVpO.",
        "...OpppppppO..",
        "....OOOOOOO...",
    ],
    10: [
        ".....O....",
        "....OhO...",
        "....OxO...",
        "...OxxxO..",
        "...OxxxO..",
        "...OxxxO..",
        "..OLxxxLO.",
        ".OPVxxxVPO",
        ".OpppppppO",
        "..OOOOOOO.",
    ],
}


def small_icon(size):
    img = Image.new("RGBA", (size, size), CLEAR)
    for y, row in enumerate(SMALL[size]):
        assert len(row) == size, (size, y, row)
        xs = [x for x, ch in enumerate(row) if ch == "x"]
        for x, ch in enumerate(row):
            if ch == ".":
                continue
            if ch != "x":
                img.putpixel((x, y), SMALL_KEY[ch])
                continue
            red = ((x + y) // 2) % 2 == 0
            tone = 1
            if x == xs[-1] and len(xs) > 1:
                tone = 2
            elif x == xs[0] and len(xs) >= 5:
                tone = 0
            c = (R_HI, R, R_SH)[tone] if red else (W_HI, W, W_SH)[tone]
            if "P" in row:
                c = SINK.get(c, c)
            elif "L" in row:
                c = TINT.get(c, c)
            img.putpixel((x, y), c)
    return img


def icon(size):
    if size in SMALL:
        return small_icon(size)
    # 32er: Portal unten, Stachel steht, Funkeln an der Spitze. Oberste Zeile
    # bleibt leer (Sprite-Rect der .meta ist 32x31 ab unten).
    cv = Canvas(32, 32, 15.5, 5.0)
    rx, ry = 13.5, 4.6
    pmask = draw_portal_back(cv, rx, ry, 0.37, "open")
    for (i, j) in list(pmask):
        for di, dj in ((0, -1), (1, 0), (-1, 0)):
            n = (i + di, j + dj)
            if n not in pmask and cv.get(*n)[3] == 0:
                cv.put(n[0], n[1], P_OUT)
    base = -1.5
    smask = draw_spike(cv, 25.0, 6.2, 0.3, base=base, pitch=8.0, sink_rows=3, tint_rows=5)
    spike_outline(cv, smask, base)
    draw_portal_front(cv, rx, ry, "open")
    sparkle(cv, 5.0, 21.0, 2)
    cv.at(-9, 12, GLOW_HI)
    cv.at(10, 8, RIM_HI)
    cv.at(-12, 17, RIM)
    return cv.img


# ----------------------------------------------------------------------------
#  .meta / .anim
# ----------------------------------------------------------------------------

def patch_meta(path):
    text = open(path, encoding="utf-8").read()
    px, py = CX / FW, CY / FH

    def fix(m):
        k = int(m.group(1))
        return ("name: fin_void_sugar_%d\n      rect:\n        serializedVersion: 2\n"
                "        x: %d\n        y: 0\n        width: %d\n        height: %d\n"
                "      alignment: 9\n      pivot: {x: %s, y: %s}" % (k, k * FW, FW, FH, repr(px), repr(py)))

    text, n = re.subn(r"name: fin_void_sugar_(\d+)\n      rect:\n        serializedVersion: 2\n"
                      r"        x: [^\n]*\n        y: [^\n]*\n        width: [^\n]*\n        height: [^\n]*\n"
                      r"      alignment: [^\n]*\n      pivot: \{[^}]*\}", fix, text)
    assert n == FRAMES, n
    ids = {}
    for m in re.finditer(r"      (fin_void_sugar_\d+): (-?\d+)", text):
        ids[int(m.group(1).rsplit("_", 1)[1])] = int(m.group(2))
    assert len(ids) == FRAMES
    return text, [ids[k] for k in range(FRAMES)]


def patch_anim(path, ids, P):
    text = open(path, encoding="utf-8").read()
    guid = "19b0876294899214ea46c172c5a38281"
    keys = "".join("    - time: %s\n      value: {fileID: %d, guid: %s, type: 3}\n" % (repr(round(d["t"], 4)), i, guid)
                   for d, i in zip(P, ids))
    text, n = re.subn(r"(  m_PPtrCurves:\n  - serializedVersion: 2\n    curve:\n)(?:    - time: [^\n]*\n      value: [^\n]*\n)+",
                      lambda m: m.group(1) + keys, text)
    assert n == 1
    mapping = "".join("    - {fileID: %d, guid: %s, type: 3}\n" % (i, guid) for i in ids)
    text, n = re.subn(r"(    pptrCurveMapping:\n)(?:    - \{[^\n]*\n)+", lambda m: m.group(1) + mapping, text)
    assert n == 1
    end = P[-1]["t"] + P[-1]["dur"]
    text = re.sub(r"m_StopTime: [^\n]*", "m_StopTime: %s" % repr(round(end, 4)), text)
    text = text.replace("m_LoopTime: 1", "m_LoopTime: 0")
    return text, end


# ----------------------------------------------------------------------------

def main():
    args = sys.argv[1:]
    dry = "--dry" in args
    preview = args[args.index("--preview") + 1] if "--preview" in args else None

    P = plan()
    sheet = Image.new("RGBA", (FW * FRAMES, FH), CLEAR)
    for k, d in enumerate(P):
        sheet.paste(render_frame(d), (k * FW, 0))
    icons = {s: icon(s) for s in (32, 14, 10)}

    if preview:
        bg = (58, 84, 58, 255)
        sc = 3
        rows = 2
        per = (FRAMES + rows - 1) // rows
        out = Image.new("RGBA", (per * FW * sc, rows * FH * sc + 140), bg)
        for k in range(FRAMES):
            fr = sheet.crop((k * FW, 0, k * FW + FW, FH)).resize((FW * sc, FH * sc), Image.NEAREST)
            out.alpha_composite(fr, ((k % per) * FW * sc, (k // per) * FH * sc))
        x = 4
        for s, im in icons.items():
            m = 128 // s if s > 10 else 12
            r = im.resize((s * m, s * m), Image.NEAREST)
            out.alpha_composite(r, (x, rows * FH * sc + 6))
            x += s * m + 12
        out.save(preview)

    meta_text, ids = patch_meta(SHEET + ".meta")
    anim_text, end = patch_anim(ANIM, ids, P)
    print("Clip-Laenge %.3f s, Ausbruch bei %.3f s" % (end, T_BURST))
    if dry:
        return
    sheet.save(SHEET)
    # beide Dateien sind CRLF
    with open(SHEET + ".meta", "w", encoding="utf-8", newline="\r\n") as fh:
        fh.write(meta_text)
    with open(ANIM, "w", encoding="utf-8", newline="\r\n") as fh:
        fh.write(anim_text)
    icon32 = icons[32]
    icon32.save(ICON)
    icons[14].save(os.path.join(WORKBENCH, "void_spike_14.png"))
    icons[10].save(os.path.join(WORKBENCH, "void_spike_10.png"))
    print("geschrieben")


if __name__ == "__main__":
    main()
