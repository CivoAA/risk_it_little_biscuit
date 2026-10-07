"""
Keks-Koenig (Endboss der Kueche): komplett neu gezeichnet und animiert.

Der alte Koenig war ein 34-px-Bild, 30-fach hochgezogen und fuer die
Animation als Ganzes gedreht - schiefe Riesenpixel. Hier entsteht jedes Bild
nativ im Weltraster (PPU 32) aus Formeln:

  Assets/Art/Gegner/new/boss/
    kekskoenig_lauf.png          12  Watscheln (Schleife)
    kekskoenig_lauf_blinzeln.png 12  dasselbe, blinzelt
    kekskoenig_ausholen.png      10  Charge-Anlauf: zuruecklehnen, scharren
    kekskoenig_zittern.png        4  Anlauf halten (Schleife), Dampf
    kekskoenig_rennen.png         6  Charge (Schleife)
    kekskoenig_bremsen.png       10  Rutschen, Stauchen, Aufrichten
    kekskoenig_werfen.png        14  Keks-Regen: Keks heben + in den Himmel werfen
    kekskoenig_wut.png           16  Phase-2-Wechsel: Risse brechen auf, Gebruell
    (alle ausser wut auch als *_p2: rissig, gluehend, wuetend)
    kekskoenig_keks.png           8  fallender Regen-Keks (dreht sich)
    kekskoenig_einschlag.png      9  Krumen-Explosion beim Einschlag

  alle PPU 32, 12 fps. Pivot = Mitte des Kekses in Ruhe (wie das alte Bild:
  transform.position ist die Koerpermitte, von dort laufen Charge-Bahn und
  Treffertests).

Aufbau eines Bildes:
  Jede Ebene (Umhang, Beine, Arme, Kekskante, Keks, Gesicht, Krone, Effekte)
  ist eine Formel im eigenen, mitbewegten Koordinatensystem und wird 4x4
  abgetastet. Pro Pixel gewinnt erst das Objekt mit den meisten Treffern,
  dann dessen haeufigste Farbe - so gibt es keine Mischfarben und keine
  Fransen. Danach 1-px-Umriss (aussen dunkel, innen in der Objektfarbe) und
  ein Aufraeumschritt gegen Einzelpixel.
  Bewegung: Verschiebungen immer auf ganze Pixel, Krone und Umhang haengen an
  Federn (Simulation ueber mehrere Durchlaeufe, damit Schleifen nahtlos sind).

Groesse:
  Standard sind 256er-Zellen (Keks gut 4 Einheiten breit). Mit --klein
  entsteht alles in 128er-Zellen (halb so gross) - wieder aus den Formeln,
  nicht durch Verkleinern: das feine Abtastgitter wird pro Pixel doppelt so
  grob aufgeloest, kleine Details (Poren, Mini-Stuecke) fallen weg, Krone und
  Risse werden kraeftiger. Die .meta-Dateien behalten guid + Sprite-IDs, nur
  die Rechtecke werden angepasst - das Prefab bleibt also unberuehrt, und
  EnemyKeckKönig.cs liest die Groesse selbst am Bild ab.
  Zurueck auf gross: einfach ohne --klein laufen lassen.

Aufruf aus dem Projektordner:
  python Tools/kekskoenig.py [--klein] [--preview ordner] [--nur name,name]
"""

import math
import os
import re
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from unity_meta import write_strip_meta  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "Assets", "Art", "Gegner", "new", "boss")
BESTIARY = os.path.join(ROOT, "Assets", "Resources", "Bestiary", "KeksKoenig.png")

CELL_W, CELL_H = 256, 256
BX, BY = 128, 140            # Keksmitte in Ruhe (= Pivot)
GROUND = BY + 76             # Fusssohlen in Ruhe
PPU = 32
FPS = 12
PIVOT = (BX / CELL_W, 1.0 - BY / CELL_H)
R0 = 62                      # Keksradius in Ruhe

SS = 4

# --klein: alle Koenig-Streifen in halber Groesse (siehe Kopf)
KLEIN = "--klein" in sys.argv

# ------------------------------------------------------------------ Palette

PAL = [(0, 0, 0, 0)]
NAME = {}


def farbe(name, hexcode, alpha=255):
    h = hexcode.lstrip("#")
    PAL.append((int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), alpha))
    NAME[name] = len(PAL) - 1
    return len(PAL) - 1


def ramp(prefix, codes):
    return [farbe("%s%d" % (prefix, i), c) for i, c in enumerate(codes)]


OUT = farbe("out", "#2a120a")
# Teig: 0 Innenlinie .. 6 Glanz
DOUGH = ramp("d", ["#4e2410", "#7a3a16", "#a2551f", "#c4772c", "#dd9a3f", "#efbc60", "#fad992"])
CRUST = ramp("k", ["#3e1c0c", "#5e2c12", "#82401a", "#a85a24"])          # Kante/Rand
CHOC = ramp("c", ["#1e0b06", "#34160c", "#4e2414", "#6e3820", "#9a5a38"])
CREAM = ramp("w", ["#c99a6a", "#ecd2a6", "#fff4dc", "#ffffff"])
GOLD = ramp("g", ["#4a2008", "#9a5810", "#d08a1e", "#f0b432", "#fcd75a", "#fff2a8"])
RUBY = ramp("r", ["#4a0612", "#8e1426", "#d0303c", "#ff7a7a", "#ffe4e4"])
SAPH = ramp("s", ["#0a1640", "#18348e", "#2e6ad8", "#86c0ff"])
VELV = ramp("v", ["#2e0612", "#5a0e20", "#8a1a2c", "#b82c3a", "#e05058"])
FUR = ramp("f", ["#6e6474", "#b4aabe", "#e2dce8", "#ffffff"])
FURSPOT = farbe("fs", "#1a1420")
BOOT = ramp("b", ["#1c0e0a", "#3a1e14", "#5a3020", "#7e4a30"])
MOUTHC = ramp("m", ["#2a0806", "#5a1612", "#b0484a", "#e07a72"])
GLOW = ramp("o", ["#7a1e08", "#d0501a", "#f08a2e", "#ffc24a", "#fff0a0"])
DUST = ramp("u", ["#8a6a50", "#c8a47c", "#ead2ae", "#fff0d8"])
STEAM = ramp("e", ["#9aa0b4", "#d4d8e4", "#ffffff"])
SHADOW = farbe("shadow", "#2a120a", 110)
SPEED = farbe("speed", "#fff4dc", 190)

PAL_ARR = np.array(PAL, np.uint8)

# Objekte: Reihenfolge = Tiefe (spaeter = weiter vorn)
O_NONE, O_SHADOW, O_CAPE, O_LEGB, O_ARMB, O_BAND, O_BODY, O_DECAL, O_LEGF, O_ARMF, O_CROWN, O_HELD, O_FX = range(13)
# Umriss gegen Luft / Innenlinie gegen weiter hinten liegende Objekte
OUTLINE = {
    O_CAPE: (OUT, VELV[0]), O_LEGB: (OUT, BOOT[0]), O_ARMB: (OUT, DOUGH[0]),
    O_BAND: (OUT, CRUST[0]), O_BODY: (OUT, DOUGH[0]), O_LEGF: (OUT, BOOT[0]),
    O_ARMF: (OUT, DOUGH[0]), O_CROWN: (OUT, GOLD[0]), O_HELD: (OUT, DOUGH[0]),
}
TRANSPARENT_FOR_OUTLINE = {O_NONE, O_SHADOW, O_FX}


# ------------------------------------------------------------------ Leinwand

class Canvas:
    def __init__(self, w=CELL_W, h=CELL_H):
        self.w, self.h = w, h
        ys, xs = np.mgrid[0:h * SS, 0:w * SS]
        self.X = (xs + 0.5) / SS
        self.Y = (ys + 0.5) / SS
        self.obj = np.zeros((h * SS, w * SS), np.int16)
        self.col = np.zeros((h * SS, w * SS), np.int16)

    def paint(self, obj, mask, col):
        if not mask.any():
            return
        self.obj[mask] = obj
        if np.isscalar(col):
            self.col[mask] = col
        else:
            self.col[mask] = col[mask]

    def resolve(self):
        """Pro Pixel: Objekt mit den meisten Treffern, dann dessen haeufigste Farbe."""
        h, w = self.h, self.w
        ob = self.obj.reshape(h, SS, w, SS).transpose(0, 2, 1, 3).reshape(h, w, SS * SS)
        co = self.col.reshape(h, SS, w, SS).transpose(0, 2, 1, 3).reshape(h, w, SS * SS)
        objm = mode(ob)
        key = np.where(ob == objm[:, :, None], co, -1)
        colm = mode(key, ignore=-1)
        colm[objm == O_NONE] = 0
        return objm, colm


def mode(a, ignore=None):
    s = np.sort(a, axis=-1)
    n = s.shape[-1]
    cnt = np.zeros(s.shape, np.int16)
    for j in range(n):
        cnt[..., j] = (s == s[..., j:j + 1]).sum(-1)
    if ignore is not None:
        cnt[s == ignore] = -1
    # bei Gleichstand gewinnt der hoehere Wert (= weiter vorn / heller) - stabil
    score = cnt.astype(np.int32) * 4096 + np.arange(n)[None, None, :]
    idx = score.argmax(-1)
    return np.take_along_axis(s, idx[..., None], -1)[..., 0]


def outline(objm, colm):
    h, w = objm.shape
    res = colm.copy()
    pad_o = np.pad(objm, 1, constant_values=O_NONE)
    for o, (outer, inner) in OUTLINE.items():
        me = objm == o
        if not me.any():
            continue
        air = np.zeros_like(me)
        behind = np.zeros_like(me)
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            nb = pad_o[1 + dy:1 + dy + h, 1 + dx:1 + dx + w]
            is_air = np.isin(nb, list(TRANSPARENT_FOR_OUTLINE))
            air |= is_air
            behind |= (~is_air) & (nb < o) & (nb != o)
        res[me & behind & ~air] = inner
        res[me & air] = outer
    return res


def cleanup(objm, colm, keep):
    """Einzelpixel, die anders sind als alle 4 Nachbarn desselben Objekts -> Mehrheit."""
    h, w = colm.shape
    res = colm.copy()
    pc = np.pad(colm, 1, constant_values=-1)
    po = np.pad(objm, 1, constant_values=-1)
    nbs = []
    for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
        nbs.append((pc[1 + dy:1 + dy + h, 1 + dx:1 + dx + w], po[1 + dy:1 + dy + h, 1 + dx:1 + dx + w]))
    same = np.zeros((h, w), np.int8)
    for c, o in nbs:
        same += (c == colm)
    lone = (same == 0) & (objm != O_NONE) & ~np.isin(colm, list(keep))
    ys, xs = np.nonzero(lone)
    for y, x in zip(ys, xs):
        cand = [int(c[y, x]) for c, o in nbs if o[y, x] == objm[y, x] and c[y, x] >= 0
                and c[y, x] not in (OUT,)]
        if len(cand) >= 3:
            best = max(set(cand), key=cand.count)
            if cand.count(best) >= 2:
                res[y, x] = best
    return res


def to_image(colm):
    return Image.fromarray(PAL_ARR[colm], "RGBA")


# ------------------------------------------------------------------ Mathe

LIGHT = np.array([-0.55, 0.62, 0.56])
LIGHT /= np.linalg.norm(LIGHT)


def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def stufen(I, ramp_idx, grenzen):
    """I -> Rampenfarbe: grenzen aufsteigend, len(grenzen) = len(ramp_idx) - 1."""
    k = np.zeros(I.shape, np.int16)
    for g in grenzen:
        k += (I >= g)
    return np.array(ramp_idx, np.int16)[k]


def _hash(ix, iy, seed):
    v = np.sin(ix * 127.1 + iy * 311.7 + seed * 74.7) * 43758.5453
    return v - np.floor(v)


def vnoise(x, y, seed=0):
    ix, iy = np.floor(x), np.floor(y)
    fx, fy = x - ix, y - iy
    fx = fx * fx * (3 - 2 * fx)
    fy = fy * fy * (3 - 2 * fy)
    a = _hash(ix, iy, seed)
    b = _hash(ix + 1, iy, seed)
    c = _hash(ix, iy + 1, seed)
    d = _hash(ix + 1, iy + 1, seed)
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


class Frame2D:
    """Mitbewegtes Koordinatensystem: Mitte (cx, cy) in Pixeln, Drehung a (Grad, + = nach links kippen),
    Masse rx/ry (lokale Einheit 1 = rx bzw. ry Pixel). Lokal: y nach oben."""

    def __init__(self, cx, cy, rx, ry, a=0.0):
        self.cx, self.cy, self.rx, self.ry = cx, cy, rx, ry
        self.a = math.radians(a)
        self.c, self.s = math.cos(self.a), math.sin(self.a)

    def local(self, X, Y):
        dx = X - self.cx
        dy = self.cy - Y
        u = dx * self.c + dy * self.s
        v = -dx * self.s + dy * self.c
        return u / self.rx, v / self.ry

    def world(self, u, v):
        U, V = u * self.rx, v * self.ry
        x = self.cx + U * self.c - V * self.s
        y = self.cy - (U * self.s + V * self.c)
        return x, y

    def rot_normal(self, nu, nv):
        return nu * self.c - nv * self.s, nu * self.s + nv * self.c


def capsule(X, Y, x0, y0, x1, y1, r0, r1=None):
    if r1 is None:
        r1 = r0
    px, py = X - x0, Y - y0
    bx, by = x1 - x0, y1 - y0
    L2 = bx * bx + by * by
    t = np.clip((px * bx + py * by) / max(L2, 1e-6), 0, 1)
    dx, dy = px - bx * t, py - by * t
    d = np.sqrt(dx * dx + dy * dy)
    r = r0 + (r1 - r0) * t
    return d < r, d / np.maximum(r, 1e-6), t, dx, dy


def window(c, x0, y0, x1, y1):
    """Ausschnitt der Abtastgitter (Tempo): Rueckgabe Slices + X/Y-Teilgitter."""
    xa = max(0, int((x0) * SS))
    xb = min(c.w * SS, int(math.ceil(x1 * SS)))
    ya = max(0, int((y0) * SS))
    yb = min(c.h * SS, int(math.ceil(y1 * SS)))
    sl = (slice(ya, yb), slice(xa, xb))
    return sl, c.X[sl], c.Y[sl]


def paint_win(c, sl, obj, mask, col):
    if not mask.any():
        return
    o = c.obj[sl]
    k = c.col[sl]
    o[mask] = obj
    if np.isscalar(col):
        k[mask] = col
    else:
        k[mask] = col[mask]


def poly_inside(X, Y, pts):
    """Gerade/ungerade-Regel, vektorisiert. pts: Liste (x, y)."""
    inside = np.zeros(X.shape, bool)
    n = len(pts)
    for i in range(n):
        x0, y0 = pts[i]
        x1, y1 = pts[(i + 1) % n]
        if y0 == y1:
            continue
        cond = (Y >= min(y0, y1)) & (Y < max(y0, y1))
        xc = x0 + (Y - y0) * (x1 - x0) / (y1 - y0)
        inside ^= cond & (X < xc)
    return inside


# ------------------------------------------------------------------ Keks (Koerper)

# Biss oben rechts: Kreise in lokalen Einheiten (Mitte u, v, Radius)
BITE = [(0.80, 0.66, 0.15), (0.91, 0.50, 0.14), (0.68, 0.80, 0.12)]
BITE_P2 = [(-0.98, -0.22, 0.11), (-0.88, -0.44, 0.09)]   # Phase 2: Brocken fehlen


def disc_r(u, v, P):
    th = np.arctan2(v, u)
    r = np.sqrt(u * u + v * v)
    edge = 1.0 + 0.016 * np.sin(5 * th + 0.7) + 0.011 * np.sin(11 * th + 2.1) + 0.006 * np.sin(17 * th)
    inside = r < edge
    bites = BITE + (BITE_P2 if P.get("p2") else [])
    for (bu, bv, br) in bites:
        inside &= (u - bu) ** 2 + (v - bv) ** 2 > br * br
    return inside, r / edge


def body_frame(P):
    return Frame2D(P["bx"], P["by"], P["rx"], P["ry"], P["a"])


def draw_band(c, P):
    """Kekskante: dieselbe Scheibe, nach hinten rechts versetzt (er schaut nach links)."""
    F = Frame2D(P["bx"] + 6, P["by"] + 2, P["rx"], P["ry"], P["a"])
    pad = max(P["rx"], P["ry"]) + 10
    sl, X, Y = window(c, F.cx - pad, F.cy - pad, F.cx + pad, F.cy + pad)
    u, v = F.local(X, Y)
    inside, rr = disc_r(u, v, P)
    # Kante: oben heller, unten dunkler, eine Lichtkante oben
    th = np.arctan2(v, u)
    I = np.sin(th) * 0.5 + 0.5 + 0.12 * (vnoise(th * 9, 0.5, 3) - 0.5)
    col = stufen(I, [CRUST[1], CRUST[2], CRUST[3]], [0.35, 0.68])
    paint_win(c, sl, O_BAND, inside, col)


def draw_body(c, P):
    F = body_frame(P)
    pad = max(P["rx"], P["ry"]) + 6
    sl, X, Y = window(c, F.cx - pad, F.cy - pad, F.cx + pad, F.cy + pad)
    u, v = F.local(X, Y)
    inside, rr = disc_r(u, v, P)

    # Flache Oberseite, erst zum Rand hin gewoelbt
    r = np.clip(rr, 0, 1)
    k = 0.92 * smooth(0.45, 1.02, r)
    nu, nv = u / np.maximum(r, 1e-6) * k, v / np.maximum(r, 1e-6) * k
    nz = np.sqrt(np.clip(1 - k * k, 0.04, 1))
    wx, wy = F.rot_normal(nu, nv)
    I = wx * LIGHT[0] + wy * LIGHT[1] + nz * LIGHT[2] + 0.17 + 0.07 * (1 - r)
    # gebackene Struktur (grob, wandert mit dem Keks)
    I = I + 0.09 * (vnoise(u * 4.5 + 3, v * 4.5, 1) - 0.5) + 0.04 * (vnoise(u * 11, v * 11, 2) - 0.5)
    # Rand braeunt
    crust = smooth(0.80, 0.97, rr)
    I = I - 0.24 * crust
    col = stufen(I, [DOUGH[1], DOUGH[2], DOUGH[3], DOUGH[4], DOUGH[5]], [0.22, 0.42, 0.62, 0.88])
    # Glanzfleck links oben
    gl = ((u + 0.42) / 0.20) ** 2 + ((v - 0.50) / 0.11) ** 2 < 1
    gl2 = ((u + 0.58) / 0.06) ** 2 + ((v - 0.30) / 0.06) ** 2 < 1
    col = np.where((gl | gl2) & (col >= DOUGH[4]), DOUGH[6], col)
    # Biss: Bruchkante innen heller (frischer Teig)
    bites = BITE + (BITE_P2 if P.get("p2") else [])
    for (bu, bv, br) in bites:
        d = np.sqrt((u - bu) ** 2 + (v - bv) ** 2)
        ring = (d < br + 0.07) & inside
        col = np.where(ring, np.where(v - bv > -0.02 * (1 if bu > 0 else -1), DOUGH[5], DOUGH[3]), col)
    paint_win(c, sl, O_BODY, inside, col)
    return F


def lp(F, U, V):
    """Lokale Ruhe-Pixel (Keksradius R0) -> Welt."""
    return F.world(U / R0, V / R0)


def local_px(F, X, Y):
    u, v = F.local(X, Y)
    return u * R0, v * R0


# Schokostuecke (lokale Ruhe-Pixel): Mitte, Groesse, Form-Seed
CHIPS = [(36, 30, 8.5, 1), (46, -8, 7.0, 2), (30, -42, 8.5, 3), (-42, -32, 7.0, 4),
         (-40, 32, 6.0, 5), (17, 46, 5.5, 6), (2, -52, 5.0, 7), (53, 14, 4.5, 8), (-52, 4, 4.5, 9)]
# Teigporen
PORES = [(22, 18), (-24, -48), (44, -24), (-30, 44), (10, -38), (54, -16), (-52, -12),
         (28, 52), (-14, 52), (36, 8), (-36, -52), (20, -24)]


def chip_poly(cx, cy, s, seed):
    rng = np.random.RandomState(seed)
    n = 7
    pts = []
    for i in range(n):
        th = 2 * math.pi * i / n + rng.uniform(-0.25, 0.25)
        rr = s * rng.uniform(0.78, 1.12)
        pts.append((cx + rr * math.cos(th), cy + rr * math.sin(th) * 0.86))
    return pts


def draw_chips(c, P, F):
    klein = P.get("klein", False)
    for (cx, cy, s, seed) in CHIPS:
        if klein and s < 6.5:
            continue
        x, y = lp(F, cx, cy)
        sl, X, Y = window(c, x - s * 2, y - s * 2, x + s * 2, y + s * 2)
        U, V = local_px(F, X, Y)
        outer = poly_inside(U, V, chip_poly(cx, cy, s, seed))
        inner = poly_inside(U, V, chip_poly(cx - 0.6, cy + 0.6, s * 0.72, seed))
        du, dv = (U - cx) / s, (V - cy) / s
        I = -du * 0.6 + dv * 0.7
        col = np.where(inner, stufen(I, [CHOC[2], CHOC[3]], [0.05]), CHOC[1])
        hi = ((du + 0.38) ** 2 + (dv - 0.38) ** 2) < 0.045
        col = np.where(hi & inner, CHOC[4], col)
        # Schatten des Stuecks auf dem Teig rechts unten
        sh = poly_inside(U, V, chip_poly(cx + 1.3, cy - 1.3, s, seed)) & ~outer
        paint_win(c, sl, O_DECAL, sh & (c.obj[sl] == O_BODY), DOUGH[2])
        paint_win(c, sl, O_DECAL, outer, col)
    for (px, py) in ([] if klein else PORES):
        x, y = lp(F, px, py)
        sl, X, Y = window(c, x - 3, y - 3, x + 3, y + 3)
        U, V = local_px(F, X, Y)
        d = ((U - px) / 1.6) ** 2 + ((V - py) / 1.1) ** 2
        lo = ((U - px - 0.8) / 1.6) ** 2 + ((V - py + 1.0) / 1.1) ** 2
        on = c.obj[sl] == O_BODY
        paint_win(c, sl, O_DECAL, (lo < 1) & (d >= 1) & on, DOUGH[5])
        paint_win(c, sl, O_DECAL, (d < 1) & on, DOUGH[2])


# ------------------------------------------------------------------ Gesicht

EYE = (-9.0, 13.0)       # Augenmitte (lokale Ruhe-Pixel)
SOCKET_R, SCLERA_R, IRIS_R = 21.5, 16.5, 9.5
MOUTH = (-6.0, -28.0)


def draw_eye(c, P, F):
    ex, ey = EYE
    x, y = lp(F, ex, ey)
    sl, X, Y = window(c, x - 34, y - 40, x + 34, y + 34)
    U, V = local_px(F, X, Y)
    du, dv = U - ex, V - ey
    d = np.sqrt(du * du + dv * dv)
    p2 = P.get("p2", False)

    # Schoko-Ring (wie ein Chocolate Chip um das Auge)
    sock = d < SOCKET_R
    I = (-du * 0.55 + dv * 0.65) / SOCKET_R
    ring = stufen(I, [CHOC[1], CHOC[2], CHOC[3]], [-0.25, 0.3])
    ring = np.where(d > SOCKET_R - 1.3, CHOC[0], ring)
    paint_win(c, sl, O_DECAL, sock, ring)

    # Augapfel
    scl = d < SCLERA_R
    shade = ((du + 3.5) ** 2 + (dv - 3.5) ** 2) > (SCLERA_R + 0.5) ** 2   # Schattensichel rechts unten
    s_col = np.where(shade, CREAM[1], CREAM[2])
    s_col = np.where(d > SCLERA_R - 1.2, np.where(dv > 0, CHOC[0], CREAM[0]), s_col)
    paint_win(c, sl, O_DECAL, scl, s_col)

    # Iris + Pupille
    lx, ly = P.get("look", (-4.0, -1.0))
    ix, iy = ex + lx, ey + ly
    di = np.sqrt((U - ix) ** 2 + (V - iy) ** 2)
    iris = (di < IRIS_R) & scl
    if p2:
        icol = np.where(di > IRIS_R - 1.4, GLOW[0], np.where(V - iy < -1.5, GLOW[3], GLOW[2]))
        pup = (((U - ix) / 2.2) ** 2 + ((V - iy) / 6.2) ** 2 < 1) & scl
    else:
        icol = np.where(di > IRIS_R - 1.4, CHOC[0], np.where((V - iy) < -2.0 + (U - ix) * 0.3, CHOC[3], CHOC[2]))
        pup = (di < 5.0 * P.get("pupil", 1.0)) & scl
    paint_win(c, sl, O_DECAL, iris, icol)
    paint_win(c, sl, O_DECAL, pup, CHOC[0])
    hl = ((U - ix + 3.6) ** 2 + (V - iy - 3.4) ** 2 < 2.6 ** 2) & iris
    hl2 = ((U - ix - 3.4) ** 2 + (V - iy + 3.6) ** 2 < 1.2 ** 2) & iris
    paint_win(c, sl, O_DECAL, hl | hl2, CREAM[3])

    # Lider: oben (lid 0 = offen, 1 = zu), unten (low). Bei Wut V-foermig.
    lid = P.get("lid", 0.0)
    low = P.get("low", 0.0)
    ang = P.get("brow", 0.0)
    if lid > 0 or ang > 0.25:
        t = du / SCLERA_R
        line = SCLERA_R - lid * 2 * SCLERA_R - max(0.0, ang - 0.25) * 9 * (1 - np.abs(t))
        up = scl & (dv > line)
        Il = (-du * 0.5 + (dv - line) * 0.9) / SCLERA_R
        lcol = stufen(Il, [DOUGH[3], DOUGH[4], DOUGH[5]], [0.0, 0.5])
        lcol = np.where(dv < line + 1.6, CHOC[0], lcol)
        paint_win(c, sl, O_DECAL, up, lcol)
    if low > 0:
        line = -SCLERA_R + low * 2 * SCLERA_R
        dn = scl & (dv < line)
        lcol = np.where(dv > line - 1.4, CHOC[0], DOUGH[3])
        paint_win(c, sl, O_DECAL, dn, lcol)


def draw_brow(c, P, F):
    """Augenbraue aus Schokoglasur. brow: -1 erstaunt .. 0 stolz .. 1 wuetend."""
    ex, ey = EYE
    ang = P.get("brow", 0.0)
    raise_ = P.get("brow_up", 0.0)
    x, y = lp(F, ex, ey + 26)
    sl, X, Y = window(c, x - 40, y - 26, x + 40, y + 22)
    U, V = local_px(F, X, Y)
    t = (U - ex) / 29.0
    a = max(0.0, ang)
    arch = 3.5 * (1 - t * t)
    furrow = -11.0 * (1 - np.abs(t)) + 2.0
    vc = ey + 26.5 + raise_ + (1 - a) * arch + a * furrow + min(0.0, ang) * -3.0 * (1 - t * t)
    half = 4.3 * (1 - 0.40 * t * t)
    dv = V - vc
    on = (np.abs(t) <= 1.0) & (np.abs(dv) < half)
    col = np.where(dv > half * 0.25, CHOC[3], np.where(dv < -half * 0.45, CHOC[1], CHOC[2]))
    col = np.where((np.abs(dv) > half - 1.0) | (np.abs(t) > 0.94), CHOC[0], col)
    col = np.where((dv > 0) & (dv < half - 1.0) & (np.abs(t + 0.35) < 0.18), CREAM[1], col)
    paint_win(c, sl, O_DECAL, on, col)


def draw_mouth(c, P, F):
    """mouth: open 0..1, smile -1..1, wide, teeth none/top/grit, tongue."""
    m = P.get("mouth", {})
    op = m.get("open", 0.0)
    sm = m.get("smile", -0.4)
    wd = m.get("wide", 1.0)
    teeth = m.get("teeth", "top")
    mx, my = MOUTH
    hw = 27.0 * wd
    x, y = lp(F, mx, my)
    sl, X, Y = window(c, x - hw - 12, y - 34, x + hw + 12, y + 16)
    U, V = local_px(F, X, Y)
    t = (U - mx) / hw
    vu = my + sm * 7.0 * t * t - sm * 1.5
    depth = 3.2 + op * 22.0
    vl = vu - depth * np.clip(1 - t * t, 0, 1) ** 0.7
    edge = (np.abs(t) < 1 + 1.4 / hw) & (V < vu + 1.3) & (V > vl - 1.3)
    inner = (np.abs(t) < 1) & (V < vu) & (V > vl)
    paint_win(c, sl, O_DECAL, edge, CHOC[0])
    col = np.full(U.shape, MOUTHC[0], np.int16)
    col = np.where(V > vu - 2.0 - op * 3, MOUTHC[1], col)
    if op > 0.3 and m.get("tongue", True):
        ton = (np.abs(t) < 0.55) & (((U - mx + 2) / (hw * 0.55)) ** 2 + ((V - vl) / (6 + op * 4)) ** 2 < 1)
        col = np.where(ton, MOUTHC[2], col)
        col = np.where(ton & (V > vl + 3.5 + op * 4), MOUTHC[3], col)
    if teeth != "none" and (op > 0.12 or teeth == "grit"):
        n = 6 if wd >= 1 else 5
        k = (t + 1) / 2 * n
        frac = k - np.floor(k)
        gap = (frac < 0.12) | (frac > 0.88)
        th = 4.5 + op * 2.0
        top = (V > vu - th) & (np.abs(t) < 0.86) & ~gap
        tcol = np.where(V < vu - th + 1.5, CREAM[0], np.where(frac < 0.4, CREAM[3], CREAM[2]))
        col = np.where(top, tcol, col)
        if teeth == "grit":
            bot = (V < vl + th) & (np.abs(t) < 0.80) & ~gap & (V < vu - th - 1)
            col = np.where(bot, np.where(V > vl + th - 1.5, CREAM[0], CREAM[2]), col)
    paint_win(c, sl, O_DECAL, inner, col)


# Risse (Phase 2): Polylinien in lokalen Ruhe-Pixeln, wachsen von der Krone und vom Biss aus
CRACKS = [
    [(-2, 60), (-4, 50), (-1, 44), (-5, 38)],
    [(14, 58), (18, 50), (18, 43), (25, 36), (30, 26)],
    [(-22, 56), (-26, 48), (-29, 41), (-37, 35), (-46, 32)],
    [(46, 38), (42, 30), (43, 21), (39, 12), (40, 3), (36, -6)],
    [(-56, -8), (-48, -12), (-50, -20), (-41, -28)],
    [(28, -58), (24, -50), (30, -44), (23, -35)],
    [(-30, -54), (-26, -46), (-34, -42)],
    [(59, -14), (51, -18), (53, -27)],
]


def draw_cracks(c, P, F):
    rev = P.get("crack", 0.0)
    if rev <= 0:
        return
    hot = P.get("glow", 1.0)
    core = GLOW[3] if hot > 0.5 else GLOW[2]
    for poly in CRACKS:
        L = [math.hypot(poly[i + 1][0] - poly[i][0], poly[i + 1][1] - poly[i][1]) for i in range(len(poly) - 1)]
        total = sum(L)
        show = total * rev
        acc = 0.0
        for i, seg in enumerate(L):
            if acc >= show:
                break
            f = min(1.0, (show - acc) / seg)
            (u0, v0), (u1, v1) = poly[i], poly[i + 1]
            u1, v1 = u0 + (u1 - u0) * f, v0 + (v1 - v0) * f
            x0, y0 = lp(F, u0, v0)
            x1, y1 = lp(F, u1, v1)
            sl, X, Y = window(c, min(x0, x1) - 5, min(y0, y1) - 5, max(x0, x1) + 5, max(y0, y1) + 5)
            wdt = 1.5 - 0.6 * (acc / total)
            if P.get("klein"):
                wdt *= 1.9
            on_body = np.isin(c.obj[sl], [O_BODY, O_DECAL])
            m0 = capsule(X, Y, x0, y0, x1, y1, wdt + 0.7)[0]
            m1 = capsule(X, Y, x0, y0, x1, y1, wdt * 0.5 + 0.25)[0]
            paint_win(c, sl, O_DECAL, m0 & on_body, CHOC[0])
            paint_win(c, sl, O_DECAL, m1 & on_body, core)
            acc += seg


# ------------------------------------------------------------------ Arme, Beine

def draw_arm(c, P, F, arm, obj):
    """arm: (Schulter-Winkel Grad, Hand (U, V) lokale Ruhe-Pixel, Daumenrichtung Grad)."""
    th, (hu, hv) = arm[0], arm[1]
    su, sv = 0.86 * R0 * math.cos(math.radians(th)), 0.86 * R0 * math.sin(math.radians(th))
    sx, sy = lp(F, su, sv)
    hx, hy = lp(F, hu, hv)
    hx, hy = math.floor(hx + 0.5), math.floor(hy + 0.5)
    sl, X, Y = window(c, min(sx, hx) - 14, min(sy, hy) - 14, max(sx, hx) + 14, max(sy, hy) + 14)
    m, dn, tt, dx, dy = capsule(X, Y, sx, sy, hx, hy, 7.5, 6.0)
    nx, ny = dx / 7.0, -dy / 7.0
    nz = np.sqrt(np.clip(1 - nx * nx - ny * ny, 0.05, 1))
    I = nx * LIGHT[0] + ny * LIGHT[1] + nz * LIGHT[2]
    col = stufen(I, [DOUGH[2], DOUGH[3], DOUGH[4], DOUGH[5]], [0.2, 0.5, 0.8])
    paint_win(c, sl, obj, m, col)
    hr = 8.5
    dxh, dyh = X - hx, Y - hy
    dh = np.sqrt(dxh ** 2 + dyh ** 2)
    hand = dh < hr
    nx, ny = dxh / hr, -dyh / hr
    nz = np.sqrt(np.clip(1 - nx * nx - ny * ny, 0.05, 1))
    I = nx * LIGHT[0] + ny * LIGHT[1] + nz * LIGHT[2]
    hcol = stufen(I, [DOUGH[2], DOUGH[3], DOUGH[4], DOUGH[5]], [0.2, 0.48, 0.78])
    tdir = arm[2] if len(arm) > 2 else 90
    tx = hx + 7.0 * math.cos(math.radians(tdir))
    ty = hy - 7.0 * math.sin(math.radians(tdir))
    thumb = (X - tx) ** 2 + (Y - ty) ** 2 < 3.8 ** 2
    paint_win(c, sl, obj, thumb, DOUGH[4])
    paint_win(c, sl, obj, hand, hcol)
    return (hx, hy)


def draw_leg(c, P, F, leg, obj):
    """leg: (Huefte U, Fuss-x Versatz, Hub in Pixeln)."""
    hu, fdx, lift = leg[0], leg[1], leg[2]
    hip_x, hip_y = lp(F, hu, -0.80 * R0)
    foot_x = math.floor(P["bx"] + hu * 0.9 + fdx + 0.5)
    sole = math.floor(P.get("ground", GROUND) - lift + 0.5)
    ank_x, ank_y = foot_x + 1, sole - 9
    sl, X, Y = window(c, min(hip_x, foot_x) - 20, min(hip_y, ank_y) - 10, max(hip_x, foot_x) + 20, sole + 3)
    m, dn, tt, dx, dy = capsule(X, Y, hip_x, hip_y, ank_x, ank_y, 7.5)
    nx = dx / 7.5
    I = -nx * 0.6 + 0.55
    col = stufen(I, [DOUGH[2], DOUGH[3], DOUGH[4]], [0.3, 0.75])
    paint_win(c, sl, obj, m, col)
    bw, bh = 15.0, 12.0
    bcx = foot_x - 3
    e = ((X - bcx) / bw) ** 2 + ((Y - sole) / bh) ** 2 < 1
    boot = e & (Y < sole)
    I = -(X - bcx) / bw * 0.45 + (sole - Y) / bh * 0.6
    bcol = stufen(I, [BOOT[1], BOOT[2], BOOT[3]], [0.15, 0.55])
    bcol = np.where(Y > sole - 2.2, BOOT[0], bcol)
    cuff = boot & (Y < sole - bh + 3.6)
    bcol = np.where(cuff, np.where(X < bcx - 2, GOLD[4], GOLD[2]), bcol)
    paint_win(c, sl, obj, boot, bcol)


# ------------------------------------------------------------------ Umhang

def bez(p0, p1, p2, n):
    out = []
    for i in range(n + 1):
        t = i / n
        out.append(((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0],
                    (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1]))
    return out


def draw_cape(c, P, F):
    """cape: dict(sway px, stream 0..1, phase, lift px)."""
    cp = P.get("cape", {})
    sway = cp.get("sway", 0.0)
    stream = cp.get("stream", 0.0)
    ph = cp.get("phase", 0.0)
    lift = cp.get("lift", 0.0)
    sL = lp(F, -0.70 * R0, 0.62 * R0)
    sR = lp(F, 0.70 * R0, 0.62 * R0)
    hem_y = P["by"] + 0.80 * R0 + 8 - lift
    hw = 74.0
    hL = (P["bx"] - hw + sway + stream * 54, hem_y - stream * 40)
    hR = (P["bx"] + hw + sway + stream * 42, hem_y - stream * 22)
    cL = (sL[0] - 30 + sway * 0.5 + stream * 10, sL[1] + 44)
    cR = (sR[0] + 30 + sway * 0.5 + stream * 32, sR[1] + 40)
    left = bez(sL, cL, hL, 14)
    right = bez(hR, cR, sR, 14)
    hem = []
    n = 24
    for i in range(1, n):
        t = i / n
        x = hL[0] + (hR[0] - hL[0]) * t
        y = hL[1] + (hR[1] - hL[1]) * t + (3.0 + stream * 4) * math.sin(t * math.pi * 4 + ph) * (0.4 + 0.6 * t)
        hem.append((x, y))
    pts = left + hem + right
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    sl, X, Y = window(c, min(xs) - 2, min(ys) - 2, max(xs) + 2, max(ys) + 2)
    m = poly_inside(X, Y, pts)
    hx = np.array([p[0] for p in [hL] + hem + [hR]])
    hy = np.array([p[1] for p in [hL] + hem + [hR]])
    top_y = (sL[1] + sR[1]) / 2
    rel = np.clip((Y - top_y) / max(1.0, hem_y - top_y), 0, 1)
    s = (X - P["bx"] - sway * rel) / (hw * (0.6 + 0.4 * rel))
    fold = np.sin(s * math.pi * 3.2 + ph * 0.5 + stream * 3)
    I = 0.5 + 0.30 * fold * (0.3 + 0.7 * rel) - 0.25 * (1 - rel) - 0.20 * s
    col = stufen(I, [VELV[1], VELV[2], VELV[3], VELV[4]], [0.22, 0.5, 0.78])
    hem_at = np.interp(X, hx, hy) if np.all(np.diff(hx) > 0) else np.full(X.shape, hem_y)
    fur = Y > hem_at - 8
    fI = 0.5 + 0.35 * fold
    fcol = stufen(fI, [FUR[1], FUR[2], FUR[3]], [0.35, 0.7])
    spots_k = np.floor((X - P["bx"]) / 13.0)
    sx = (X - P["bx"]) - (spots_k * 13 + 6.5)
    spot = (np.abs(sx) < 1.3) & (Y > hem_at - 6.5) & (Y < hem_at - 2.0) & ((spots_k % 2) == 0)
    fcol = np.where(spot, FURSPOT, fcol)
    col = np.where(fur, fcol, col)
    paint_win(c, sl, O_CAPE, m, col)


# ------------------------------------------------------------------ Krone

CROWN_MID = 15.0     # Mitte der Krone ueber der Reif-Unterkante (Drehpunkt im Flug)
SPIKES = [(-21.0, 13.0, 5.6), (-10.5, 18.0, 6.0), (0.0, 25.0, 6.4), (10.5, 18.0, 6.0), (21.0, 13.0, 5.6)]


def draw_crown(c, P, F):
    if P.get("no_crown"):
        return
    if P.get("crown_abs"):
        # frei in der Welt (in der Hand, im Flug): Mitte der Krone + Drehung um diese Mitte
        mx, my, tilt = P["crown_abs"]
        t = math.radians(tilt)
        cx = int(math.floor(mx + CROWN_MID * math.sin(t) + 0.5))
        cy = int(math.floor(my + CROWN_MID * math.cos(t) + 0.5))
        C = Frame2D(cx, cy, 1, 1, tilt)
    else:
        cr = P.get("crown", (0, 0, 0.0))
        ax, ay = lp(F, 1.0, 0.86 * R0)
        cx, cy = int(math.floor(ax + cr[0] + 0.5)), int(math.floor(ay + cr[1] + 0.5))
        C = Frame2D(cx, cy, 1, 1, P["a"] * 0.6 + cr[2])
    sl, X, Y = window(c, cx - 50, cy - 50, cx + 50, cy + 50)
    p, q = C.local(X, Y)
    W = 25.5
    qb = -2.6 * np.sqrt(np.clip(1 - (p / (W + 0.5)) ** 2, 0, 1))
    qt = 12.0 + qb * 0.6

    cap = (np.abs(p) < 22) & (q >= qt - 1) & (q < qt + 15 * np.sqrt(np.clip(1 - (p / 22) ** 2, 0, 1)))
    Ic = -p / 22 * 0.4 + (q - qt) / 15 * 0.5
    ccol = stufen(Ic, [VELV[1], VELV[2], VELV[3]], [0.05, 0.45])
    paint_win(c, sl, O_CROWN, cap, ccol)

    klein = P.get("klein", False)
    for (pc, h, w) in SPIKES:
        if klein:
            # Fuers Mini-Portraet: kraeftigere Zacken, sonst bleibt nur der Umriss
            h, w = h * 0.8, w * 1.45
        qtc = 12.0 - 2.6 * math.sqrt(max(0.0, 1 - (pc / (W + 0.5)) ** 2)) * 0.6
        rel = (q - qtc) / h
        sp = (q >= qt - 1) & (rel <= 1) & (np.abs(p - pc) <= w * (1 - np.clip(rel, 0, 1)) ** 0.9)
        scol = np.where(p < pc - 0.5, GOLD[4], np.where(p < pc + 1.5, GOLD[3], GOLD[2]))
        scol = np.where((np.abs(p - pc) < 0.9) & (rel > 0.15), GOLD[5], scol)
        paint_win(c, sl, O_CROWN, sp, scol)
        bq = qtc + h + 1.5
        db = np.sqrt((p - pc) ** 2 + (q - bq) ** 2)
        ball = db < (4.8 if klein else 3.4)
        bcol = np.where(((p - pc + 1.2) ** 2 + (q - bq - 1.2) ** 2) < 1.6, GOLD[5],
                        np.where((p - pc) - (q - bq) > 1.0, GOLD[2], GOLD[4]))
        paint_win(c, sl, O_CROWN, ball, bcol)

    band = (np.abs(p) <= W) & (q >= qb) & (q <= qt)
    I = -p / W * 0.5 + 0.55
    bcol = stufen(I, [GOLD[2], GOLD[3], GOLD[4]], [0.3, 0.75])
    bcol = np.where(q > qt - 2.2, np.where(p < 4, GOLD[5], GOLD[4]), bcol)
    bcol = np.where(q < qb + 2.0, GOLD[1], bcol)
    paint_win(c, sl, O_CROWN, band, bcol)

    def gem(gx, gy, rx, ry, rampc):
        e = ((p - gx) / rx) ** 2 + ((q - gy) / ry) ** 2
        setting = (e < ((rx + 1.4) / rx) ** 2) & band
        paint_win(c, sl, O_CROWN, setting, GOLD[1])
        stone = e < 1
        gI = -(p - gx) / rx * 0.5 + (q - gy) / ry * 0.6
        gcol = stufen(gI, rampc[1:4], [-0.2, 0.35])
        sp = ((p - gx + rx * 0.35) ** 2 + (q - gy - ry * 0.35) ** 2) < (min(rx, ry) * 0.32) ** 2
        gcol = np.where(sp, rampc[-1], gcol)
        paint_win(c, sl, O_CROWN, stone, gcol)

    gem(0.0, 6.0, 5.2, 5.6, RUBY)
    gem(-15.5, 5.5, 3.2, 3.4, SAPH)
    gem(15.5, 5.5, 3.2, 3.4, SAPH)


# ------------------------------------------------------------------ Schatten, Effekte

def draw_shadow(c, P):
    if P.get("no_shadow"):
        return
    w = P.get("shadow_w", 60.0)
    g = P.get("ground", GROUND)
    sl, X, Y = window(c, BX - w - 40, g - 12, BX + w + 40, g + 12)
    m = ((X - P.get("shadow_x", BX)) / w) ** 2 + ((Y - g) / 8.5) ** 2 < 1
    paint_win(c, sl, O_SHADOW, m, SHADOW)


def puff(c, x, y, r, rampc, obj=O_FX):
    """Woelkchen mit eigenem Umriss (rampc[0]) und Licht von links oben."""
    if r <= 0.6:
        return
    sl, X, Y = window(c, x - r - 2, y - r - 2, x + r + 2, y + r + 2)
    d = np.sqrt((X - x) ** 2 + (Y - y) ** 2)
    m = d < r
    lit = ((X - x + r * 0.3) ** 2 + (Y - y + r * 0.3) ** 2) < (r * 0.62) ** 2
    col = np.where(lit, rampc[-1], rampc[-2])
    col = np.where(((X - x - r * 0.25) ** 2 + (Y - y - r * 0.3) ** 2 > (r * 0.95) ** 2), rampc[1], col)
    col = np.where(d > r - 1.1, rampc[0], col)
    paint_win(c, sl, obj, m, col)


def dampf(c, x, y, r):
    """Dampfwolke aus drei Woelkchen."""
    puff(c, x - r * 0.7, y + r * 0.35, r * 0.75, STEAM)
    puff(c, x + r * 0.7, y + r * 0.45, r * 0.65, STEAM)
    puff(c, x, y - r * 0.2, r, STEAM)


def crumb(c, x, y, s, shade=0):
    x, y = math.floor(x + 0.5), math.floor(y + 0.5)
    sl, X, Y = window(c, x - 1, y - 1, x + s + 1, y + s + 1)
    m = (X >= x) & (X < x + s) & (Y >= y) & (Y < y + s)
    col = np.where((X >= x + s - 1) | (Y >= y + s - 1), DOUGH[1], DOUGH[4 - shade])
    if s <= 2:
        col = np.where((X < x + 1) & (Y < y + 1), DOUGH[4 - shade], DOUGH[2])
    paint_win(c, sl, O_FX, m, col)


def speed_line(c, x0, y, length):
    sl, X, Y = window(c, x0, y, x0 + length, y + 1)
    m = (X >= x0) & (X < x0 + length) & (Y >= y) & (Y < y + 1)
    paint_win(c, sl, O_FX, m, SPEED)


def draw_held(c, P):
    """Keks, den er in den Haenden haelt / wirft: (x, y, r)."""
    h = P.get("held")
    if not h:
        return
    x, y, r = h
    keks(c, x, y, r, 0.0, O_HELD)


def keks(c, x, y, r, spin, obj):
    x, y = math.floor(x + 0.5), math.floor(y + 0.5)
    sl, X, Y = window(c, x - r - 2, y - r - 2, x + r + 2, y + r + 2)
    dx, dy = X - x, y - Y
    d = np.sqrt(dx * dx + dy * dy)
    th = np.arctan2(dy, dx)
    edge = r * (1 + 0.04 * np.sin(5 * th + spin * 1.3))
    m = d < edge
    rr = d / edge
    I = (-dx * 0.55 + dy * 0.6) / r * 0.6 + 0.55 - 0.3 * smooth(0.65, 0.95, rr)
    col = stufen(I, [DOUGH[2], DOUGH[3], DOUGH[4], DOUGH[5]], [0.25, 0.48, 0.72])
    for k in range(4):
        a = spin + k * 1.7 + 0.4
        cr_ = r * (0.45 if k % 2 else 0.30)
        chx, chy = cr_ * math.cos(a), cr_ * math.sin(a)
        chip = ((dx - chx) ** 2 + (dy - chy) ** 2) < (max(1.2, r * 0.17)) ** 2
        col = np.where(chip, CHOC[1], col)
    if obj == O_FX:
        col = np.where(d > edge - 1.1, OUT, col)
    paint_win(c, sl, obj, m, col)


# ------------------------------------------------------------------ Bild zusammensetzen

ARM_L = (196.0, (-74.0, -14.0), 70)     # Schulter-Winkel, Hand, Daumen
ARM_R = (-16.0, (74.0, -14.0), 110)
LEG_L = (-24.0, 0.0, 0.0)
LEG_R = (22.0, 0.0, 0.0)


def basis(**kw):
    P = dict(bx=BX, by=BY, rx=R0, ry=R0, a=0.0, look=(-4.0, -1.0), lid=0.0, low=0.0,
             brow=0.0, brow_up=0.0, pupil=1.0,
             mouth=dict(open=0.0, smile=-0.7, wide=1.0, teeth="top"),
             arms=[ARM_L, ARM_R], arms_front=False, legs=[LEG_L, LEG_R],
             crown=(0, 0, 0.0), cape={}, crack=0.0, glow=1.0, p2=False,
             fx_back=[], fx=[], held=None, ground=GROUND, shadow_w=60.0, shadow_x=BX)
    P.update(kw)
    return P


KEEP = set([CREAM[3], RUBY[4], SAPH[3], GOLD[5], GLOW[3], GLOW[4], DOUGH[6], CHOC[4], FURSPOT, SPEED])


def render(P):
    c = render_canvas(P)
    objm, colm = c.resolve()
    colm = outline(objm, colm)
    colm = cleanup(objm, colm, KEEP)
    return colm


def render_klein(P, n, k=8):
    """Dasselbe Bild auf n x n Pixel (statt CELL), direkt aus den Formeln:
    das feine Abtastgitter wird auf n*k Punkte je Achse umgelegt und pro
    Pixel per Mehrheit aufgeloest - kein Verkleinern fertiger Pixel."""
    objm, colm = aufloesen(render_canvas(P), n, k)
    colm = outline(objm, colm)
    colm = cleanup(objm, colm, KEEP)
    return colm


def aufloesen(c, n, k=8):
    """Leinwand -> n x n Pixel per Mehrheit (n = Zellgroesse: wie resolve())."""
    if n == c.w:
        return c.resolve()
    H = c.obj.shape[0]
    idx = ((np.arange(n * k) + 0.5) * H / (n * k)).astype(int)
    ob = c.obj[np.ix_(idx, idx)].reshape(n, k, n, k).transpose(0, 2, 1, 3).reshape(n, n, k * k)
    co = c.col[np.ix_(idx, idx)].reshape(n, k, n, k).transpose(0, 2, 1, 3).reshape(n, n, k * k)
    objm = mode(ob)
    key = np.where(ob == objm[:, :, None], co, -1)
    colm = mode(key, ignore=-1)
    colm[objm == O_NONE] = 0
    return objm, colm


def koenig_bild(P):
    """Ein Koenig-Bild in der gewaehlten Groesse (siehe --klein)."""
    if not KLEIN:
        return render(P)
    Q = dict(P)
    Q["klein"] = True
    return render_klein(Q, CELL_W // 2, 8)


def render_canvas(P):
    c = Canvas()
    draw_shadow(c, P)
    for fn in P["fx_back"]:
        fn(c)
    F = body_frame(P)
    draw_cape(c, P, F)
    arm_obj = O_ARMF if P["arms_front"] else O_ARMB
    for leg in P["legs"]:
        draw_leg(c, P, F, leg, O_LEGB)
    if not P["arms_front"]:
        for arm in P["arms"]:
            draw_arm(c, P, F, arm, O_ARMB)
    draw_band(c, P)
    draw_body(c, P)
    draw_chips(c, P, F)
    draw_cracks(c, P, F)
    draw_eye(c, P, F)
    draw_brow(c, P, F)
    draw_mouth(c, P, F)
    if P["arms_front"]:
        for arm in P["arms"]:
            draw_arm(c, P, F, arm, O_ARMF)
    draw_crown(c, P, F)
    draw_held(c, P)
    for fn in P["fx"]:
        fn(c)
    return c


def to_rgba(colm):
    return PAL_ARR[colm]


if __name__ == "__main__" and "--test" in sys.argv:
    out = sys.argv[sys.argv.index("--test") + 1]
    img = to_rgba(render(basis()))
    im = Image.fromarray(img, "RGBA")
    im.save(os.path.join(out, "kk_rest.png"))
    bg = Image.new("RGBA", im.size, (70, 92, 70, 255))
    bg.alpha_composite(im)
    bg.resize((im.width * 3, im.height * 3), Image.NEAREST).save(os.path.join(out, "kk_rest_x3.png"))
    print("ok")


if __name__ == "__main__" and "--faces" in sys.argv:
    out = sys.argv[sys.argv.index("--faces") + 1]
    poses = [basis(),
             basis(brow=1.0, mouth=dict(open=0.0, smile=-0.9, teeth="grit", wide=0.9)),
             basis(brow=1.0, mouth=dict(open=0.9, smile=-0.3, wide=1.05)),
             basis(lid=1.0),
             basis(brow=-1.0, brow_up=3, mouth=dict(open=0.5, smile=0.0, wide=0.6, teeth="none")),
             basis(p2=True, crack=1.0, brow=1.0, mouth=dict(open=0.35, smile=-0.8, teeth="grit"))]
    ims = [Image.fromarray(to_rgba(render(P)), "RGBA") for P in poses]
    W = sum(i.width for i in ims)
    bg = Image.new("RGBA", (W, CELL_H), (70, 92, 70, 255))
    x = 0
    for i in ims:
        bg.alpha_composite(i, (x, 0))
        x += i.width
    bg.resize((W * 2, CELL_H * 2), Image.NEAREST).save(os.path.join(out, "kk_faces.png"))
    print("ok")


# ------------------------------------------------------------------ Bewegung

def feder(ziel, k=0.30, d=0.55, runden=3, start=None, lo=-12.0, hi=12.0):
    """Folger an einer Feder hinter einer Zielbahn her. Rueckgabe: Versatz je Bild.
    runden > 1 fuer Schleifen (eingeschwungen), 1 fuer einmalige Ablaeufe."""
    pos = ziel[0] if start is None else start
    vel = 0.0
    out = []
    for _ in range(runden):
        out = []
        for z in ziel:
            for _ in range(4):
                acc = (z - pos) * k - vel * d
                vel += acc
                pos += vel / 4.0
            off = max(lo, min(hi, pos - z))
            pos = z + off
            out.append(off)
    return out


def ri(x):
    return int(math.floor(x + 0.5))


def kopf(P):
    a = math.radians(P["a"])
    return P["bx"] - math.sin(a) * 0.86 * P["ry"], P["by"] - math.cos(a) * 0.86 * P["ry"]


def krone_federn(poses, loop, k=0.28, d=0.5, extra=None):
    """Krone haengt locker auf dem Kopf: x/y federn nach, kippt mit."""
    hx = [kopf(P)[0] for P in poses]
    hy = [kopf(P)[1] for P in poses]
    aa = [P["a"] for P in poses]
    r = 3 if loop else 1
    ox = feder(hx, k, d, r, lo=-5, hi=5)
    oy = feder(hy, k, d, r, lo=-3, hi=10)
    oa = feder(aa, 0.22, 0.45, r, lo=-8, hi=8)
    for i, P in enumerate(poses):
        ey = extra[i] if extra else 0
        P["crown"] = (ri(ox[i]), ri(min(0, oy[i]) * 0.6 + max(0, oy[i]) * 0.3) - ey, round(oa[i] * 0.8))


def umhang_federn(poses, loop):
    xs = [P["bx"] - math.sin(math.radians(P["a"])) * 40 for P in poses]
    r = 3 if loop else 1
    ox = feder(xs, 0.18, 0.35, r, lo=-14, hi=14)
    for i, P in enumerate(poses):
        cp = P.setdefault("cape", {})
        cp["sway"] = cp.get("sway", 0.0) + ox[i] * 0.9


def lerp(a, b, t):
    return a + (b - a) * t


def ease(t):
    return t * t * (3 - 2 * t)


def hand(u, v, base, thumb=None):
    return (base[0], (u, v), base[2] if thumb is None else thumb)


# ------------------------------------------------------------------ Animationen

def lauf(blink=False):
    n = 12
    poses = []
    for i in range(n):
        ph = 2 * math.pi * i / n
        s, co = math.sin(ph), math.cos(ph)
        contact = abs(s) < 0.3
        P = basis()
        P["a"] = round((-4.5 * s + 1.0) * 2) / 2
        P["by"] = BY - ri(3.0 * abs(s)) + (1 if contact else 0)
        P["rx"] = R0 + (2 if contact else 0)
        P["ry"] = R0 - (2 if contact else 0)
        P["legs"] = [(-24.0, 7.0 * co - 2, ri(10 * max(0.0, s))), (22.0, -7.0 * co + 2, ri(10 * max(0.0, -s)))]
        P["arms"] = [hand(-74 + 3 * co, -14 - 4 * s, ARM_L), hand(74 - 3 * co, -14 + 4 * s, ARM_R)]
        P["cape"] = {"phase": ph}
        P["brow"] = 0.15
        P["shadow_w"] = 60 - (abs(s) * 2)
        if blink:
            P["lid"] = {2: 0.55, 3: 1.0, 4: 1.0, 5: 0.45}.get(i, 0.0)
        poses.append(P)
    krone_federn(poses, True)
    umhang_federn(poses, True)
    return poses


def stehen():
    """Stillstehen (Fenster zum Draufhauen): schweres, stolzes Atmen."""
    n = 8
    poses = []
    for i in range(n):
        ph = 2 * math.pi * i / n
        s = math.sin(ph)
        P = basis()
        P["ry"] = R0 + ri(1.4 * s)
        P["rx"] = R0 - ri(1.4 * s)
        P["by"] = BY - ri(1.4 * s)
        P["arms"] = [hand(-74, -14 - ri(2 * s), ARM_L), hand(74, -14 - ri(2 * s), ARM_R)]
        P["cape"] = {"phase": ph * 0.5}
        P["brow"] = 0.1
        P["mouth"] = dict(open=0.0, smile=-0.7, teeth="top")
        P["lid"] = 0.12
        poses.append(P)
    krone_federn(poses, True)
    return poses


AUSHOLEN_END = None


def ausholen():
    n = 10
    poses = []
    for i in range(n):
        t = ease(min(1.0, i / (n - 3)))
        P = basis()
        P["a"] = round(lerp(0, -10, t))
        P["by"] = BY + ri(lerp(0, 7, t))
        P["rx"] = R0 + ri(lerp(0, 5, t))
        P["ry"] = R0 - ri(lerp(0, 6, t))
        P["brow"] = lerp(0.1, 1.0, min(1.0, t * 1.5))
        P["low"] = lerp(0, 0.22, t)
        P["look"] = (-5.0, -2.0)
        P["mouth"] = dict(open=lerp(0.0, 0.2, t), smile=-0.9, wide=lerp(1.0, 0.92, t), teeth="grit")
        P["arms"] = [hand(lerp(-74, -72, t), lerp(-14, 10, t), ARM_L, 30),
                     hand(lerp(74, 80, t), lerp(-14, 8, t), ARM_R, 150)]
        P["legs"] = [(-24.0, lerp(0, -8, t), 0), (22.0, lerp(0, 8, t), 0)]
        P["cape"] = {"phase": t * 2, "lift": lerp(0, 3, t)}
        P["shadow_w"] = lerp(60, 66, t)
        poses.append(P)
    krone_federn(poses, False)
    umhang_federn(poses, False)
    global AUSHOLEN_END
    AUSHOLEN_END = poses[-1]
    return poses


def zittern():
    """Anlauf halten: zittern, mit dem Vorderfuss scharren, Dampf aus den Ohren - aeh, der Krone."""
    base = ausholen()[-1]
    poses = []
    jit = [(1, 0), (-1, 1), (1, 1), (-1, 0)]
    for i in range(4):
        P = dict(base)
        P["bx"] = base["bx"] + jit[i][0]
        P["by"] = base["by"] + jit[i][1]
        scrape = i % 2 == 1
        P["legs"] = [(-24.0, -8 + (6 if scrape else 0), 3 if scrape else 0), (22.0, 8.0, 0)]
        P["crown"] = (base["crown"][0] - jit[i][0], base["crown"][1], base["crown"][2])
        P["glow"] = 1.0 if i % 2 else 0.3
        gx = BX - 24 * 0.9 - 8
        fx = []
        if scrape:
            fx.append(lambda c, gx=gx: (puff(c, gx + 22, GROUND - 5, 5.5, DUST), puff(c, gx + 31, GROUND - 9, 4.0, DUST)))
        else:
            fx.append(lambda c, gx=gx: (puff(c, gx + 30, GROUND - 7, 4.5, DUST), puff(c, gx + 39, GROUND - 13, 3.0, DUST)))
        hy = base["by"] - 0.86 * base["ry"] - 18
        sz = [(5.0, 3.0), (6.5, 4.0), (5.0, 5.0), (3.5, 6.0)][i]
        fx.append(lambda c, hy=hy, sz=sz: dampf(c, BX - 40 - sz[0], hy - sz[0] * 1.3, sz[0]) or
                  dampf(c, BX + 44 + sz[1], hy - sz[1] * 1.3, sz[1]))
        P["fx"] = fx
        poses.append(P)
    return poses


def rennen():
    n = 6
    poses = []
    for i in range(n):
        ph = 2 * math.pi * i / n
        s, co = math.sin(ph), math.cos(ph)
        P = basis()
        P["a"] = 13 + round(1.5 * s)
        P["by"] = BY + 3 - ri(3 * abs(math.sin(ph * 1.0)))
        P["rx"] = R0 + 4
        P["ry"] = R0 - 4
        P["bx"] = BX - 4
        P["brow"] = 1.0
        P["look"] = (-6.0, -1.0)
        P["mouth"] = dict(open=0.75, smile=-0.2, wide=1.05, teeth="top")
        P["legs"] = [(-24.0, -14 * co - 4, ri(9 * max(0.0, s))), (22.0, 14 * co - 2, ri(9 * max(0.0, -s)))]
        P["arms"] = [hand(-62, -38 + 3 * s, ARM_L, 20), hand(84, 4 - 3 * s, ARM_R, 160)]
        P["cape"] = {"stream": 1.0, "phase": ph * 2}
        P["shadow_x"] = BX - 4
        g = GROUND
        k = i
        fx = []
        # Staub fliegt hinter ihm weg (wandert pro Bild nach rechts)
        for j in range(3):
            age = (k + j * 2) % 6
            fx.append(lambda c, age=age, j=j: puff(c, BX + 30 + age * 10 + j * 5, g - 7 - age * 2.5, 10.0 - age * 1.5, DUST))
        P["fx_back"] = fx
        poses.append(P)
    krone_federn(poses, True, k=0.35)
    for P in poses:
        P["crown"] = (P["crown"][0] + 3, P["crown"][1], P["crown"][2] - 4)
    return poses


def bremsen():
    # a, by-Versatz, rx+, ry-, Staub-Alter, Mund auf
    tab = [(13, 3, 4, -4, 0, 0.75), (2, 4, 4, -4, 1, 0.75), (-8, 6, 8, -8, 2, 0.6), (-6, 5, 7, -7, 3, 0.4),
           (-1, 2, 2, -2, 4, 0.2), (3, -2, -2, 3, 5, 0.1), (1, -1, -1, 2, 6, 0.0), (-1, 0, 1, -1, 7, 0.0),
           (0, 0, 0, 0, 8, 0.0), (0, 0, 0, 0, 9, 0.0)]
    poses = []
    for i, (a, dy, drx, dry, age, op) in enumerate(tab):
        P = basis()
        P["a"] = a
        P["by"] = BY + dy
        P["rx"] = R0 + drx
        P["ry"] = R0 + dry
        t = i / (len(tab) - 1)
        P["brow"] = lerp(1.0, 0.3, t)
        P["mouth"] = dict(open=op, smile=lerp(-0.2, -0.8, t), wide=1.0, teeth="top" if op > 0.15 else "top")
        P["low"] = 0.0
        if i in (2, 3):
            P["lid"] = 0.35
            P["mouth"] = dict(open=0.35, smile=-0.9, wide=0.95, teeth="grit")
        slide = [-10, -14, -12, -8, -4, -1, 0, 0, 0, 0][i]
        P["legs"] = [(-24.0, slide - 4, 0), (22.0, slide + 2, 0)]
        ah = [(-62, -38), (-72, -4), (-78, 8), (-76, 2), (-75, -8), (-74, -16), (-74, -15), (-74, -14), (-74, -14), (-74, -14)][i]
        P["arms"] = [hand(ah[0], ah[1], ARM_L), hand(-ah[0] + [10, 4, 2, 2, 1, 0, 0, 0, 0, 0][i], ah[1] + [42, 6, -2, 0, 0, 0, 0, 0, 0, 0][i], ARM_R)]
        P["cape"] = {"stream": max(0.0, 1 - i * 0.35), "phase": i * 1.3}
        g = GROUND
        fx = []
        if age <= 6:
            fx.append(lambda c, age=age: [puff(c, BX - 46 - age * 6 - j * 9, g - 4 - age * 1.5 - j * 3, max(0.0, 8.5 - age * 1.3 - j), DUST) for j in range(3)])
            fx.append(lambda c, age=age: [puff(c, BX + 40 + age * 5 + j * 8, g - 3 - age - j * 2, max(0.0, 6.0 - age * 1.0 - j), DUST) for j in range(2)])
        P["fx"] = fx
        poses.append(P)
    krone_federn(poses, False)
    umhang_federn(poses, False)
    return poses


def werfen():
    # Phasen: 0-3 bueckt sich, 4-7 stemmt Keks hoch, 8-9 wirft, 10-13 lacht + setzt ab
    poses = []
    n = 14
    for i in range(n):
        P = basis()
        if i <= 3:
            # holt Schwung: geht in die Knie, Arme schwingen nach unten aussen
            t = ease(i / 3)
            P["by"] = BY + ri(6 * t)
            P["rx"], P["ry"] = R0 + ri(3 * t), R0 - ri(5 * t)
            P["arms"] = [hand(-74 - 4 * t, -14 - 16 * t, ARM_L, 60), hand(74 + 4 * t, -14 - 16 * t, ARM_R, 120)]
            P["look"] = (-3.0, -2.0 + 6 * t)
            P["brow"] = lerp(0.1, 0.5, t)
            P["mouth"] = dict(open=0.0, smile=lerp(-0.7, 0.5, t), wide=0.9, teeth="top")
            P["lid"] = 0.3 * t
        elif i <= 7:
            # reisst die Arme hoch, ueber der Krone backt ein Keks heran
            t = ease((i - 4) / 3)
            P["by"] = BY + ri(lerp(6, -2, t))
            P["rx"], P["ry"] = R0 + ri(lerp(3, -2, t)), R0 + ri(lerp(-5, 3, t))
            hx_l, hy_l = lerp(-84, -40, t), lerp(-20, 90, t)
            P["arms"] = [(lerp(190, 150, t), (hx_l, hy_l), lerp(60, 0, t)), (lerp(-10, 30, t), (-hx_l - 4, hy_l), lerp(120, 180, t))]
            P["look"] = (-1.0, lerp(4, 8, t))
            P["brow"] = lerp(0.5, -0.6, t)
            P["brow_up"] = lerp(0, 2, t)
            P["mouth"] = dict(open=lerp(0.1, 0.5, t), smile=0.5, wide=lerp(0.9, 0.8, t), teeth="top")
            F = body_frame(P)
            kx, ky = lp(F, -2, 0.86 * R0 + 50)
            P["held"] = (kx, ky, [5, 9, 12, 14][i - 4])
        elif i <= 9:
            k = i - 8
            P["by"] = BY - 6 + k * 2
            P["rx"], P["ry"] = R0 - 4, R0 + 6
            P["arms"] = [(140.0, (-34, 94 + k * 3), 0), (40.0, (30, 94 + k * 3), 180)]
            P["look"] = (-1.0, 8.0)
            P["brow"] = -0.8
            P["brow_up"] = 3
            P["mouth"] = dict(open=0.8, smile=0.4, wide=0.9, teeth="top")
            yk = [-14, -60][k]
            P["fx"] = [lambda c, yk=yk: (keks(c, BX - 18, yk + 20, 9, 0.5, O_FX), keks(c, BX + 2, yk, 12, 1.5, O_FX),
                                          keks(c, BX + 20, yk + 28, 8, 2.5, O_FX))]
        else:
            t = ease((i - 10) / 3)
            laugh = [0.7, 0.4, 0.7, 0.1][i - 10]
            P["by"] = BY + ri(lerp(-2, 0, t)) + (1 if i % 2 else 0)
            P["ry"] = R0 + ri(lerp(3, 0, t))
            P["rx"] = R0 - ri(lerp(3, 0, t))
            hl = (lerp(-60, -74, t), lerp(40, -14, t))
            P["arms"] = [(lerp(160, 196, t), hl, 70), (lerp(20, -16, t), (-hl[0], hl[1]), 110)]
            P["look"] = (-4.0, lerp(3, -1, t))
            P["lid"] = 0.35 if i < 13 else 0.12
            P["brow"] = lerp(-0.3, 0.15, t)
            P["mouth"] = dict(open=laugh, smile=0.8 - t, wide=1.0, teeth="top")
        P["cape"] = {"phase": i * 0.5}
        poses.append(P)
    krone_federn(poses, False, extra=None)
    umhang_federn(poses, False)
    return poses


def wut():
    """Phase-2-Wechsel: Schreck, Zittern, Risse brechen auf, Gebruell, Krone springt."""
    poses = []
    n = 16
    for i in range(n):
        P = basis()
        if i <= 3:
            P["pupil"] = 0.6
            P["brow"] = -1.0
            P["brow_up"] = 3
            P["mouth"] = dict(open=0.25, smile=0.0, wide=0.55, teeth="none")
            P["by"] = BY - (2 if i == 1 else 0)
            P["ry"] = R0 + (2 if i == 1 else 0)
            P["bx"] = BX + [0, 0, 1, -1][i]
        elif i <= 7:
            t = (i - 4) / 3
            P["bx"] = BX + (1 if i % 2 else -1)
            P["by"] = BY + 4
            P["rx"], P["ry"] = R0 + 3, R0 - 4
            P["brow"] = 1.0
            P["mouth"] = dict(open=0.2, smile=-0.9, wide=0.92, teeth="grit")
            P["crack"] = lerp(0.15, 0.55, t)
            P["glow"] = 0.3 if i % 2 else 1.0
            P["low"] = 0.2
            P["arms"] = [hand(-70, 6, ARM_L, 30), hand(72, 6, ARM_R, 150)]
        elif i <= 12:
            k = i - 8
            P["p2"] = True
            P["crack"] = min(1.0, 0.7 + k * 0.15)
            P["by"] = BY - [6, 5, 4, 4, 3][k] + (k % 2)
            P["rx"], P["ry"] = R0 + 2, R0 + 4
            P["bx"] = BX + (1 if k % 2 else -1)
            P["brow"] = 1.0
            P["mouth"] = dict(open=1.0, smile=-0.2, wide=1.1, teeth="top")
            P["arms"] = [hand(-84, 20 - k, ARM_L, 30), hand(86, 20 - k, ARM_R, 150)]
            P["glow"] = 1.0 if k % 2 == 0 else 0.6
            fx = []
            # Krumen aus den abgeplatzten Stellen + Dampf
            for j, (sx, sy, vx, vy) in enumerate([(-58, 12, -5, -4), (-52, 28, -6, -1), (-50, 4, -4, 2), (58, -6, 4, -5), (40, -40, 5, -3)]):
                fx.append(lambda c, k=k, sx=sx, sy=sy, vx=vx, vy=vy, j=j: crumb(c, BX + sx + vx * (k + 1) * 3, BY - sy + vy * (k + 1) * 3 + 1.5 * (k + 1) ** 2, 3 if j % 2 else 2, j % 2))
            sz = 4.0 + k * 1.2
            hy = BY - 70
            fx.append(lambda c, sz=sz, hy=hy, k=k: dampf(c, BX - 46 - k * 4, hy - k * 4, sz) or dampf(c, BX + 50 + k * 4, hy - k * 3, sz * 0.9))
            P["fx"] = fx
        else:
            k = i - 13
            P["p2"] = True
            P["crack"] = 1.0
            P["glow"] = [0.6, 1.0, 0.6][k]
            P["by"] = BY + [1, 0, 0][k]
            P["ry"] = R0 + [-1, 1, 0][k]
            P["brow"] = 1.0
            P["mouth"] = dict(open=[0.4, 0.25, 0.15][k], smile=-0.8, teeth="grit")
        P["cape"] = {"phase": i * 0.7, "stream": 0.25 if 8 <= i <= 12 else 0.0, "lift": 6 if 8 <= i <= 11 else 0}
        poses.append(P)
    extra = [0] * n
    for i, e in zip(range(8, 13), [14, 10, 4, 1, 0]):
        extra[i] = e
    krone_federn(poses, False, extra=extra)
    return poses


# ------------------------------------------------------------------ Kronen-Bumerang (nur Phase 2)

CROWN_CELL = 96


def krone_lokal(P, U, V, tilt):
    """Krone frei an einer Stelle im Keks-Koordinatensystem (Mitte der Krone)."""
    F = body_frame(P)
    x, y = lp(F, U, V)
    P["crown_abs"] = (x, y, tilt)


# Kopf-Mitte der Krone in Ruhe (lokal): Reif-Unterkante + CROWN_MID nach oben
CROWN_REST_V = 0.86 * R0 + CROWN_MID


def kronenwurf():
    """Nimmt die Krone ab, holt hinter dem Kopf aus und schleudert sie als Bumerang.
    Bild 8 = Abwurf (ab da fliegt sie im Code)."""
    # a, rechte Hand (Schulterwinkel, U, V, Daumen), Krone (U, V, Kippung) oder None, Mund auf
    tab = [
        (0, (-16, 74, -14, 110), "kopf", 0.0),
        (0, (10, 70, 22, 120), "kopf", 0.0),
        (1, (30, 52, 56, 150), "kopf", 0.0),
        (1, (40, 30, 72, 180), (2, CROWN_REST_V + 2, -4), 0.0),
        (-3, (40, 36, 90, 180), (20, CROWN_REST_V + 20, -12), 0.1),
        (-8, (30, 60, 92, 180), (46, CROWN_REST_V + 22, -38), 0.15),
        (-11, (20, 78, 84, 200), (64, CROWN_REST_V + 14, -72), 0.2),
        (8, (60, -8, 96, 300), (-34, CROWN_REST_V + 28, 40), 0.7),
        (13, (120, -60, 54, 300), None, 0.95),
        (10, (160, -74, 18, 270), None, 0.8),
        (4, (180, -74, 0, 250), None, 0.4),
        (0, (-16, 72, 0, 110), None, 0.15),
    ]
    poses = []
    for i, (a, rh, cr, op) in enumerate(tab):
        P = basis()
        P["a"] = a
        if i == 6:
            P["rx"], P["ry"], P["by"] = R0 + 3, R0 - 3, BY + 2
        if i == 8:
            P["rx"], P["ry"] = R0 - 2, R0 + 2
        lh = (-80, -4) if 4 <= i <= 7 else (-62, -40) if 8 <= i <= 10 else (-74, -14)
        P["arms"] = [hand(lh[0], lh[1], ARM_L),
                     (rh[0], (rh[1], rh[2]), rh[3])]
        P["brow"] = 1.0
        P["look"] = (-5.0, -1.0) if i < 4 else (-6.0, 1.0)
        P["mouth"] = dict(open=op, smile=-0.3 if op > 0.5 else -0.9, wide=1.0, teeth="top" if op > 0.5 else "grit")
        P["cape"] = {"phase": i * 0.6, "stream": 0.2 if 7 <= i <= 9 else 0.0}
        if cr is None:
            P["no_crown"] = True
        elif cr != "kopf":
            krone_lokal(P, cr[0], cr[1], cr[2])
        if i == 8:
            # Wischer: kurze Goldspur, wo die Krone eben noch war
            F = body_frame(P)
            sx, sy = lp(F, -40, CROWN_REST_V + 22)
            P["fx"] = [lambda c, sx=sx, sy=sy: [puff(c, sx - 6 - k * 10, sy + k * 2, 7.0 - k * 1.5, GOLD[1:5]) for k in range(4)]]
        poses.append(P)
    krone_federn(poses, False)
    umhang_federn(poses, False)
    return poses


def ohnekrone():
    """Wartet ohne Krone (Schleife): Haende bereit, Blick nach oben, es dampft aus dem Kopf."""
    poses = []
    for i in range(8):
        ph = 2 * math.pi * i / 8
        s = math.sin(ph)
        P = basis()
        P["no_crown"] = True
        P["ry"] = R0 + ri(1.4 * s)
        P["rx"] = R0 - ri(1.4 * s)
        P["by"] = BY - ri(1.4 * s)
        P["arms"] = [hand(-72, 6 + ri(2 * s), ARM_L, 30), hand(72, 6 + ri(2 * s), ARM_R, 150)]
        P["look"] = (-3.0, 5.0)
        P["brow"] = 1.0
        P["mouth"] = dict(open=0.12, smile=-0.9, wide=0.9, teeth="grit")
        P["cape"] = {"phase": ph * 0.5}
        top = BY - R0 - 6
        sz = [5.0, 7.0, 8.5, 6.5, 5.0, 7.0, 8.5, 6.5][i]
        side = -1 if i < 4 else 1
        P["fx"] = [lambda c, sz=sz, side=side, top=top, i=i: dampf(c, BX + side * 14, top - (i % 4) * 4, sz)]
        poses.append(P)
    return poses


def fangen():
    """Krone faellt zurueck auf den Kopf: auffangen, Stauchen, Nachfedern, Grinsen."""
    # Krone ueber dem Kopf (dy Pixel) bzw. auf dem Kopf mit Federversatz
    tab = [(-34, None), (-14, None), (None, (0, 3, 0)), (None, (0, -4, 3)), (None, (0, -1, -2)),
           (None, (0, 1, 1)), (None, (0, 0, 0)), (None, (0, 0, 0))]
    poses = []
    for i, (dy, cr) in enumerate(tab):
        P = basis()
        if i <= 1:
            F = body_frame(P)
            x, y = lp(F, 1.0, CROWN_REST_V)
            P["crown_abs"] = (x, y + dy, [8, 3][i])
            P["arms"] = [(150.0, (-30, 86), 0), (30.0, (32, 86), 180)]
            P["look"] = (-1.0, 7.0)
            P["mouth"] = dict(open=0.3, smile=0.2, wide=0.7, teeth="top")
        else:
            P["crown"] = cr
            if i == 2:
                P["by"], P["rx"], P["ry"] = BY + 3, R0 + 3, R0 - 4
            if i == 3:
                P["ry"] = R0 + 2
            k = min(1.0, (i - 2) / 3)
            P["arms"] = [hand(lerp(-50, -74, k), lerp(50, -14, k), ARM_L, 30),
                         hand(lerp(50, 74, k), lerp(50, -14, k), ARM_R, 150)]
            P["look"] = (-4.0, -1.0)
            P["mouth"] = dict(open=0.28, smile=0.8, wide=1.0, teeth="top")
            P["lid"] = 0.3
        P["brow"] = 0.7
        P["cape"] = {"phase": i * 0.6}
        poses.append(P)
    return poses


def krone_dreh_bilder():
    """Fliegende Krone, dreht sich in der Bildebene (8 Bilder = halbe Drehung je
    Schleife waere zu langsam - es sind 45-Grad-Schritte, eine ganze Drehung)."""
    out = []
    for i in range(8):
        c = Canvas(CROWN_CELL, CROWN_CELL)
        P = {"crown_abs": (CROWN_CELL / 2, CROWN_CELL / 2, -45.0 * i), "a": 0.0}
        if KLEIN:
            P["klein"] = True
        draw_crown(c, P, None)
        # Glanzstern auf zwei Bildern
        if i in (1, 5):
            stern(c, CROWN_CELL / 2 - 14, CROWN_CELL / 2 - 16)
        objm, colm = aufloesen(c, CROWN_CELL // 2 if KLEIN else CROWN_CELL)
        colm = outline(objm, colm)
        colm = cleanup(objm, colm, KEEP)
        out.append(PAL_ARR[colm])
    return out


def stern(c, x, y):
    x, y = ri(x), ri(y)
    sl, X, Y = window(c, x - 5, y - 5, x + 6, y + 6)
    dx, dy = np.abs(X - x - 0.5), np.abs(Y - y - 0.5)
    m = ((dx < 0.6) & (dy < 4.5)) | ((dy < 0.6) & (dx < 4.5)) | ((dx < 1.5) & (dy < 1.5))
    paint_win(c, sl, O_FX, m, np.where((dx < 0.6) & (dy < 0.6), GOLD[5], GOLD[4]))


# ------------------------------------------------------------------ Phase 2

EMBERS = [(-30, 58, 0), (24, 54, 3), (44, 28, 5), (-46, 30, 2), (6, 60, 4)]


def phase2(poses, loop_len=None):
    """Phase-2-Fassung: rissig, gluehend, wuetender. Glut steigt aus den Rissen."""
    out = []
    n = len(poses)
    L = loop_len or n
    for i, P in enumerate(poses):
        Q = dict(P)
        Q["p2"] = True
        Q["crack"] = 1.0
        Q["glow"] = 1.0 if (i // 2) % 2 == 0 else 0.4
        Q["brow"] = max(P.get("brow", 0.0), 0.7)
        m = dict(P.get("mouth", {}))
        if m.get("open", 0) < 0.15 and m.get("teeth") != "grit":
            m = dict(open=0.12, smile=-0.9, wide=0.95, teeth="grit")
        Q["mouth"] = m
        F = body_frame(Q)
        emb = []
        for (u, v, off) in EMBERS:
            age = (i + off) % 6
            x, y = lp(F, u, v)
            emb.append((x + math.sin(age + off) * 2, y - age * 5, age))
        Q["fx"] = list(P.get("fx", [])) + [lambda c, emb=emb: [ember(c, x, y, a) for (x, y, a) in emb]]
        out.append(Q)
    return out


def ember(c, x, y, age):
    if age >= 5:
        return
    x, y = ri(x), ri(y)
    s = 2 if age < 3 else 1
    sl, X, Y = window(c, x - 1, y - 1, x + 3, y + 3)
    m = (X >= x) & (X < x + s) & (Y >= y) & (Y < y + s)
    col = GLOW[4] if age == 0 else GLOW[3] if age < 3 else GLOW[2]
    paint_win(c, sl, O_FX, m, col)


# ------------------------------------------------------------------ Regen-Keks + Einschlag

KEKS_CELL = 32
BOOM_W, BOOM_H = 128, 96


def keks_bilder():
    out = []
    for i in range(8):
        c = Canvas(KEKS_CELL, KEKS_CELL)
        keks(c, 16, 16, 12, i * math.pi / 4 * 0.5 * 2, O_FX)
        objm, colm = c.resolve()
        out.append(PAL_ARR[colm])
    return out


def einschlag_bilder():
    out = []
    cx, cy = BOOM_W // 2, BOOM_H - 30
    rng = np.random.RandomState(7)
    bits = [(rng.uniform(0, 2 * math.pi), rng.uniform(0.6, 1.0), rng.randint(2, 4)) for _ in range(14)]
    for i in range(9):
        c = Canvas(BOOM_W, BOOM_H)
        t = i / 8
        # Staubring breitet sich flach aus
        if i <= 7:
            rr = 10 + 46 * ease(min(1, i / 6))
            sz = max(0.0, 9 - i * 1.1)
            for k in range(10):
                a = k / 10 * 2 * math.pi + 0.3
                puff(c, cx + rr * math.cos(a), cy + rr * 0.42 * math.sin(a) - i * 0.6, sz * (0.8 + 0.3 * (k % 2)), DUST)
        # Keks zerbricht: Brocken fliegen im Bogen
        if i <= 6:
            for (a, sp, s) in bits:
                d = sp * 44 * ease(min(1, i / 5))
                x = cx + d * math.cos(a)
                y = cy + d * 0.45 * math.sin(a) - 26 * sp * math.sin(min(1.0, i / 6) * math.pi)
                crumb(c, x, y, s, int(a * 3) % 2)
        if i <= 1:
            keks(c, cx, cy - (6 if i == 0 else 2), 12 - i * 3, 0.0, O_FX)
            puff(c, cx, cy, 13 - i * 2, DUST)
        objm, colm = c.resolve()
        out.append(PAL_ARR[colm])
    return out


# ------------------------------------------------------------------ Ausgabe

def strip(frames):
    h, w = frames[0].shape[:2]
    out = np.zeros((h, w * len(frames), 4), np.uint8)
    for i, f in enumerate(frames):
        out[:, i * w:(i + 1) * w] = f
    return out


def schreiben(name, frames, w, h, pivot, preview=None):
    os.makedirs(OUT_DIR, exist_ok=True)
    path = os.path.join(OUT_DIR, name + ".png")
    Image.fromarray(strip(frames), "RGBA").save(path, optimize=True)
    meta = path + ".meta"
    if not os.path.exists(meta):
        write_strip_meta(meta, name, len(frames), w, h, PPU, pivot=pivot,
                         max_size=8192 if w * len(frames) > 2048 else 2048)
    else:
        meta_rechtecke(meta, name, len(frames), w, h)
    print("  %-30s %2d Bilder  %dx%d" % (name, len(frames), w, h))


def meta_rechtecke(meta, name, n, w, h):
    """Zellgroesse in einer bestehenden .meta umstellen. guid und Sprite-IDs
    bleiben, damit das Prefab seine Verweise behaelt."""
    text = open(meta, encoding="utf-8").read()
    for k in range(n):
        pat = re.compile(r"(name: %s_%d\n      rect:\n        serializedVersion: 2\n"
                         r"        x: )-?[\d.]+(\n        y: )-?[\d.]+(\n        width: )[\d.]+(\n        height: )[\d.]+"
                         % (re.escape(name), k))
        text, cnt = pat.subn(lambda m: "%s%d%s0%s%d%s%d" % (m.group(1), k * w, m.group(2), m.group(3), w, m.group(4), h),
                             text)
        assert cnt == 1, (meta, k)
    with open(meta, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)


def gif(path, frames, fps=FPS, scale=2, bg=(70, 92, 70, 255)):
    ims = []
    for f in frames:
        im = Image.fromarray(f, "RGBA")
        b = Image.new("RGBA", im.size, bg)
        b.alpha_composite(im)
        ims.append(b.convert("RGB").resize((im.width * scale, im.height * scale), Image.NEAREST))
    ims[0].save(path, save_all=True, append_images=ims[1:], duration=int(1000 / fps), loop=0)


def anims():
    return [
        ("kekskoenig_lauf", lauf(), True),
        ("kekskoenig_lauf_blinzeln", lauf(blink=True), True),
        ("kekskoenig_stehen", stehen(), True),
        ("kekskoenig_ausholen", ausholen(), False),
        ("kekskoenig_zittern", zittern(), True),
        ("kekskoenig_rennen", rennen(), True),
        ("kekskoenig_bremsen", bremsen(), False),
        ("kekskoenig_werfen", werfen(), False),
    ]


def main():
    prev = None
    if "--preview" in sys.argv:
        prev = sys.argv[sys.argv.index("--preview") + 1]
    nur = None
    if "--nur" in sys.argv:
        nur = sys.argv[sys.argv.index("--nur") + 1].split(",")
    alle = {}
    for name, poses, loop in anims():
        alle[name] = poses
        alle[name + "_p2"] = phase2(poses)
    alle["kekskoenig_wut"] = wut()
    # Kronen-Bumerang gibt es nur in Phase 2
    alle["kekskoenig_kronenwurf_p2"] = phase2(kronenwurf())
    alle["kekskoenig_ohnekrone_p2"] = phase2(ohnekrone())
    alle["kekskoenig_fangen_p2"] = phase2(fangen())
    for name, poses in alle.items():
        if nur and not any(n in name for n in nur):
            continue
        frames = [to_rgba(koenig_bild(P)) for P in poses]
        if prev:
            gif(os.path.join(prev, name + ".gif"), frames)
        else:
            schreiben(name, frames, frames[0].shape[1], frames[0].shape[0], PIVOT)
    if not nur or "keks" in nur:
        kb = keks_bilder()
        eb = einschlag_bilder()
        if prev:
            gif(os.path.join(prev, "keks.gif"), kb, scale=6)
            gif(os.path.join(prev, "einschlag.gif"), eb, scale=4)
        else:
            kd = krone_dreh_bilder()
            schreiben("kekskoenig_krone_dreh", kd, kd[0].shape[1], kd[0].shape[0], (0.5, 0.5))
            schreiben("kekskoenig_keks", kb, KEKS_CELL, KEKS_CELL, (0.5, 0.5))
            schreiben("kekskoenig_einschlag", eb, BOOM_W, BOOM_H, (0.5, 30 / BOOM_H))
    if not prev and not nur:
        # Mini-Portraet: passt ungeskaliert in die 52er-Bosskachel der Levelauswahl
        best = to_rgba(render_klein(basis(klein=True, no_shadow=True), 76, 10))
        ys, xs = np.nonzero(best[:, :, 3] > 0)
        crop = best[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
        Image.fromarray(crop, "RGBA").save(BESTIARY)
        print("  Bestiarium", crop.shape[1], "x", crop.shape[0])


if __name__ == "__main__" and "--test" not in sys.argv and "--faces" not in sys.argv:
    main()
