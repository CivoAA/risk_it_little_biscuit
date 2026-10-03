"""
Fledermaus (grosser Gegner): nach Nicks Vorlage neu gezeichnet und animiert.
Die Daumenkrallen an den Fluegeln sind kleine Schwerter.

  Assets/Art/Gegner/new/boss/
    fledermaus_flug.png        10 Bilder  Fluegelschlag (Schleife), 12 fps
    fledermaus_flug_blink.png  10 Bilder  derselbe Schlag, blinzelt (ab und zu einstreuen)

Bildgroesse 112x112, PPU 32, Pivot = Bildmitte.

Aufbau eines Bildes (hinten -> vorne):
  Fluegel   je Seite ein Skelett (Schulter, Ellbogen, Handgelenk, 3 Finger),
            Flughaut als Polygon mit eingebuchteter Hinterkante, 4x4 abgetastet;
            Schattierung nach Abstand zu Knochen/Kante/Koerper
  Koerper   Kopf + Rumpf + Ohren + Fuesse als eine Silhouette, Kugel-Licht
            von links oben, Pelzzacken an Wangen/Scheitel, Brustlatz
  Gesicht   Stempel (Augen, Nase, Maul mit Reisszaehnen)
  Schwerter am Handgelenk, drehen sich mit dem Unterarm
Jedes Teil bekommt einen eigenen 1-px-Umriss, damit es sich vom Teil
dahinter abhebt.

Aufruf aus dem Projektordner:
  python Tools/fledermaus.py [--preview pfad.gif]
"""

import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TOOLS = os.path.join(ROOT, "Tools")
OUT_DIR = os.path.join(ROOT, "Assets", "Art", "Gegner", "new", "boss")

W = H = 112
CX = 56
PPU = 32
FPS = 12
SS = 4


def rgb(h):
    h = h.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), 255)


OUT = rgb("#1c1029")
FUR = [rgb("#2a1a3e"),    # 0 tiefster Schatten
       rgb("#3a2656"),    # 1 Schatten
       rgb("#4e3472"),    # 2 Grundton
       rgb("#66488f"),    # 3 Licht
       rgb("#8566b0")]    # 4 Kante im Licht
MEM = [rgb("#5c0f3c"),    # 0 Haut ganz dunkel
       rgb("#8e1a50"),    # 1 dunkel (an Knochen/Kante)
       rgb("#c42e62"),    # 2 Grundton
       rgb("#e8507a"),    # 3 hell
       rgb("#ff8296")]    # 4 Glanz
CREAM = [rgb("#9c8496"), rgb("#cdb8bc"), rgb("#efe2dc"), rgb("#fff8f0")]
EYE = {"O": OUT, "D": rgb("#a8400c"), "o": rgb("#e07a10"), "y": rgb("#ffb627"),
       "Y": rgb("#ffe066"), "P": OUT, "W": rgb("#ffffff")}
NOSE = rgb("#e8507a")
MOUTH = rgb("#4a0a2a")
TONGUE = rgb("#e8507a")
FANG = rgb("#fff8f0")
STEEL = [rgb("#3c4766"), rgb("#7d8fb0"), rgb("#c3d1e8"), rgb("#ffffff")]
GOLD = [rgb("#7a3e10"), rgb("#d08a20"), rgb("#ffd45a")]
GRIP = rgb("#4a2418")

LIGHT = np.array([-0.45, -0.62, 0.64])
LIGHT /= np.linalg.norm(LIGHT)

# ------------------------------------------------------------------ Hilfen


def hires(draw_fn):
    """Zeichnet mit PIL in 4-facher Aufloesung und gibt die Deckung je Pixel zurueck."""
    im = Image.new("L", (W * SS, H * SS), 0)
    d = ImageDraw.Draw(im)
    draw_fn(d, SS)
    a = np.asarray(im, dtype=np.float32) / 255.0
    return a.reshape(H, SS, W, SS).mean(axis=(1, 3))


def poly_mask(pts, thr=0.5):
    def f(d, s):
        d.polygon([(x * s, y * s) for x, y in pts], fill=255)
    return hires(f) >= thr


def ellipse_cov(cx, cy, rx, ry):
    def f(d, s):
        d.ellipse([(cx - rx) * s, (cy - ry) * s, (cx + rx) * s, (cy + ry) * s], fill=255)
    return hires(f)


def grid():
    ys, xs = np.mgrid[0:H, 0:W].astype(np.float32)
    return xs + 0.5, ys + 0.5


XS, YS = grid()


def seg_dist(ax, ay, bx, by):
    dx, dy = bx - ax, by - ay
    l2 = dx * dx + dy * dy
    t = np.clip(((XS - ax) * dx + (YS - ay) * dy) / max(l2, 1e-6), 0, 1)
    return np.hypot(XS - (ax + t * dx), YS - (ay + t * dy))


def capsule(pts, r):
    """Dicke Linie durch pts, Deckung per Abtastung."""
    def f(d, s):
        for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
            d.line([(x0 * s, y0 * s), (x1 * s, y1 * s)], fill=255, width=max(1, int(round(2 * r * s))))
        for x, y in pts:
            d.ellipse([(x - r) * s, (y - r) * s, (x + r) * s, (y + r) * s], fill=255)
    return hires(f)


def dilate(m):
    o = m.copy()
    o[1:, :] |= m[:-1, :]
    o[:-1, :] |= m[1:, :]
    o[:, 1:] |= m[:, :-1]
    o[:, :-1] |= m[:, 1:]
    return o


def erode(m):
    return ~dilate(~m)


def polar(p, ang, length):
    a = math.radians(ang)
    return (p[0] + math.cos(a) * length, p[1] - math.sin(a) * length)


def bezier(a, c, b, n=10):
    return [((1 - t) ** 2 * a[0] + 2 * (1 - t) * t * c[0] + t * t * b[0],
             (1 - t) ** 2 * a[1] + 2 * (1 - t) * t * c[1] + t * t * b[1])
            for t in (i / n for i in range(1, n + 1))]


def mirror_pt(p):
    return (W - p[0], p[1])


def stempel(rows):
    return [list(r) for r in rows]


def put(img, st, x, y, cols, flip=False):
    for j, row in enumerate(st):
        if flip:
            row = row[::-1]
        for i, ch in enumerate(row):
            if ch in cols:
                yy, xx = y + j, x + i
                if 0 <= yy < H and 0 <= xx < W:
                    img[yy, xx] = cols[ch]


def paint(img, mask, col):
    img[mask] = col


def layer(img, mask, colors_fn, outline=True):
    """Ein Teil mit eigenem Umriss auf das Bild setzen."""
    if outline:
        ring = dilate(mask) & ~mask
        img[ring] = OUT
    colors_fn(img, mask)


# ------------------------------------------------------------------ Fluegel


def wing_skeleton(shoulder, a1, fold, scale, side):
    """Skelett fuer den rechten Fluegel (side=+1) bzw. gespiegelt (side=-1).
    a1: Hebung des Oberarms in Grad (0 = waagerecht, + = hoch)
    fold: 0 = ganz gespannt, 1 = eingefaltet (Aufschlag)"""
    L_up, L_fore = 8.5 * scale, 13.0 * scale
    a2 = a1 - 14 - 70 * fold                       # Unterarm knickt beim Falten ab
    fl = [27.0 * scale * (1 - 0.20 * fold), 25.0 * scale * (1 - 0.24 * fold), 20.0 * scale * (1 - 0.22 * fold)]
    spread = 1 - 0.6 * fold
    fa = [a2 + 2 - 14 * fold, a2 - 2 - 40 * spread - 28 * fold, a2 - 6 - 78 * spread - 34 * fold]
    sx, sy = shoulder
    loc = (0.0, 0.0)
    elbow = polar(loc, a1, L_up)
    wrist = polar(elbow, a2, L_fore)
    tips = [polar(wrist, a, l) for a, l in zip(fa, fl)]
    flank = (-1.0, 12.0)
    thumb_ang = min(a2 + 58 - 18 * fold, 96)   # nie zum Kopf hin kippen

    def tr(p):
        return (sx + side * p[0], sy + p[1])

    return {
        "shoulder": tr(loc), "elbow": tr(elbow), "wrist": tr(wrist),
        "tips": [tr(t) for t in tips], "flank": tr(flank),
        "thumb": thumb_ang if side > 0 else 180 - thumb_ang,
        "a2": a2,
    }


def wing_outline(sk):
    w = sk["wrist"]
    pts = [sk["shoulder"], sk["elbow"], w, sk["tips"][0]]
    edges = []
    chain = sk["tips"] + [sk["flank"]]
    for a, b in zip(chain, chain[1:]):
        mid = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
        k = 0.42
        c = (mid[0] + (w[0] - mid[0]) * k, mid[1] + (w[1] - mid[1]) * k)
        seg = bezier(a, c, b, 14)
        edges.append([a] + seg)
        pts += seg
    return pts, edges


def draw_wing(img, sk, light_side):
    pts, edges = wing_outline(sk)
    mem = poly_mask(pts)
    arm_cov = capsule([sk["shoulder"], sk["elbow"], sk["wrist"]], 1.55)
    arm = arm_cov >= 0.45
    fingers = np.zeros((H, W), bool)
    fdist = np.full((H, W), 99.0, np.float32)
    for t in sk["tips"]:
        fdist = np.minimum(fdist, seg_dist(*sk["wrist"], *t))
    # Finger: 1 px breit, am Handgelenk etwas kraeftiger
    for i, t in enumerate(sk["tips"]):
        c = capsule([sk["wrist"], t], 0.6)
        fingers |= c >= 0.42
    knuckle = ellipse_cov(sk["wrist"][0], sk["wrist"][1], 2.0, 2.0) >= 0.5
    full = mem | arm | knuckle

    edist = np.full((H, W), 99.0, np.float32)
    for e in edges:
        for a, b in zip(e, e[1:]):
            edist = np.minimum(edist, seg_dist(*a, *b))
    adist = np.minimum(seg_dist(*sk["shoulder"], *sk["elbow"]), seg_dist(*sk["elbow"], *sk["wrist"]))
    bdist = np.minimum(fdist, adist)
    body_d = np.hypot(XS - CX, (YS - sk["shoulder"][1] - 4) * 0.9)

    def colors(img, m):
        # Flughaut: hell in der Mitte jeder Bahn, dunkel an Knochen, Kante und Koerper
        v = 1.0 + np.clip((bdist - 0.8) / 3.2, 0, 1) * 2.0
        v -= np.clip((1.8 - edist) / 1.0, 0, 1) * 1.0
        v -= np.clip((17 - body_d) / 6.0, 0, 1) * 1.4
        v += light_side * 0.3
        # Lichtkante: zum Handgelenk hin wird die Haut heller
        wd = np.hypot(XS - sk["wrist"][0], YS - sk["wrist"][1])
        v += np.clip((14 - wd) / 14.0, 0, 1) * 0.6 * (bdist > 2.2)
        idx = np.clip(np.floor(v), 0, 4).astype(int)
        for k in range(5):
            img[m & (idx == k)] = MEM[k]
        # Finger
        img[m & fingers & ~arm] = FUR[0]
        # Arm: Pelz, oben Licht
        img[m & arm] = FUR[2]
        top = arm & ~np.roll(arm, 1, axis=0)
        img[m & top] = FUR[3]
        img[m & knuckle] = FUR[2]
        kt = knuckle & ~np.roll(knuckle, 1, axis=0)
        img[m & kt] = FUR[4]
    layer(img, full, colors)
    return full


# ------------------------------------------------------------------ Schwert


def draw_sword(img, base, ang):
    """Kleines Schwert, Griff am Handgelenk, Klinge zeigt in Richtung ang."""
    a = math.radians(ang)
    ux, uy = math.cos(a), -math.sin(a)      # entlang der Klinge
    vx, vy = -uy, ux                         # quer
    # Abtasten in Schwert-Koordinaten
    sub = (np.arange(W * SS) + 0.5) / SS
    gx, gy = np.meshgrid(sub, sub)
    s = (gx - base[0]) * ux + (gy - base[1]) * uy
    t = (gx - base[0]) * vx + (gy - base[1]) * vy
    mat = np.zeros(gx.shape, np.int8)
    pommel = (s + 1.6) ** 2 + t ** 2 < 1.3 ** 2
    grip = (s > -1.0) & (s < 3.0) & (np.abs(t) < 0.8)
    guard = (s > 2.6) & (s < 4.2) & (np.abs(t) < 3.6)
    blade_len = 16.5
    half = np.where(s > blade_len - 3.5, 1.5 * (blade_len - s) / 3.5, 1.5)
    blade = (s >= 4.2) & (s < blade_len) & (np.abs(t) < half)
    mat[grip] = 1
    mat[pommel] = 2
    mat[guard] = 2
    mat[blade & (t >= 0)] = 3      # helle Seite (zum Licht)
    mat[blade & (t < 0)] = 4
    # Mehrheitsentscheid je Pixel
    m4 = mat.reshape(H, SS, W, SS).transpose(0, 2, 1, 3).reshape(H, W, SS * SS)
    cov = (m4 > 0).mean(axis=2)
    mask = cov >= 0.4
    res = np.zeros((H, W), np.int8)
    for k in (1, 2, 3, 4):
        cnt = (m4 == k).sum(axis=2)
        better = cnt > np.take_along_axis(
            np.stack([(m4 == j).sum(axis=2) for j in (0, 1, 2, 3, 4)], axis=2),
            np.maximum(res, 0)[..., None].astype(int), axis=2)[..., 0]
        res = np.where(mask & ((res == 0) | better), k, res)
    # welche Seite liegt zum Licht? (Licht von links oben)
    lit = 3 if (vx * LIGHT[0] + vy * LIGHT[1]) > 0 else 4
    def colors(img, m):
        img[m & (res == 1)] = GRIP
        img[m & (res == 2)] = GOLD[1]
        img[m & (res == lit)] = STEEL[2]
        img[m & (res == (7 - lit))] = STEEL[1]
        gold = m & (res == 2)
        img[gold & ~np.roll(gold, 1, axis=0)] = GOLD[2]
        # Glanzpunkt an der Spitze
        tip = (int(base[0] + ux * (blade_len - 1.5)), int(base[1] + uy * (blade_len - 1.5)))
        if 0 <= tip[1] < H and 0 <= tip[0] < W and m[tip[1], tip[0]]:
            img[tip[1], tip[0]] = STEEL[3]
    layer(img, mask, colors)


# ------------------------------------------------------------------ Koerper

EYE_L = stempel([
    "OO......",
    "OyOO....",
    "OyyyOO..",
    "OYyWPPyO",
    "OYyPPPoO",
    ".OoPPoDO",
    "..OOOOO.",
])

EYE_HALB = stempel([
    "OO......",
    "OFOO....",
    "OFFFOO..",
    "OFFFFFOO",
    "OOyPPyoO",
    ".OoPPoDO",
    "..OOOOO.",
])
EYE_ZU = stempel([
    "OO......",
    "OFOO....",
    "OFFFOO..",
    "OFFFFFOO",
    "OFFFFFFO",
    ".OOFFFOO",
    "...OOO..",
])
EYE_LID = dict(EYE, F=FUR[3])

MOUTH_OPEN = stempel([
    "O.......O",
    "OFMMMMMFO",
    ".OFMMMFO.",
    "..OTTTO..",
    "...OOO...",
])
MOUTH_WIDE = stempel([
    "O.......O",
    "OFMMMMMFO",
    ".FMMMMMF.",
    ".OMTTTMO.",
    "..OTTTO..",
    "...OOO...",
])


def body_masks(dy, ear_tilt, foot_dy, ear_dy=0):
    hx, hy = CX, 49 + dy
    head = ellipse_cov(hx, hy, 14.5, 12.5) >= 0.5
    # Pelzzacken an den Wangen und am Scheitel
    tufts = []
    for side in (-1, 1):
        tufts.append([(hx + side * 13.5, hy + 1), (hx + side * 18.0, hy + 4.5), (hx + side * 14.5, hy + 5),
                      (hx + side * 17.0, hy + 8.5), (hx + side * 12.0, hy + 8)])
    tufts.append([(hx - 4, hy - 11), (hx - 1, hy - 17), (hx + 1, hy - 12), (hx + 4, hy - 16), (hx + 5, hy - 10)])
    for t in tufts:
        head |= poly_mask(t)
    bx, by = CX, 66 + dy
    body = ellipse_cov(bx, by, 10.0, 10.5) >= 0.5
    ears = []
    inner = []
    for side in (-1, 1):
        base_o = (hx + side * 15.0, hy - 2)
        base_i = (hx + side * 3.0, hy - 10)
        ang = math.radians(side * (24 + ear_tilt))
        tip = (hx + side * 14.0 + math.sin(ang) * 6, hy - 34 + ear_dy + abs(ear_tilt) * 0.15)
        mid_o = (hx + side * 21.0, hy - 16)
        e = [base_i, (hx + side * 5.0, hy - 21), tip, (hx + side * 20.5, hy - 24), mid_o, base_o]
        ears.append(poly_mask(e))
        # Innenohr: Richtung Ohrmitte geschrumpft
        cxe = sum(p[0] for p in e) / len(e)
        cye = sum(p[1] for p in e) / len(e) + 2
        ie = [(cxe + (p[0] - cxe) * 0.62, cye + (p[1] - cye) * 0.66) for p in e]
        inner.append(poly_mask(ie))
    feet = np.zeros((H, W), bool)
    claws = []
    for side in (-1, 1):
        fx = CX + side * 4.5
        fy = 75 + dy + foot_dy
        feet |= poly_mask([(fx - 1.8, fy - 4), (fx + 1.8, fy - 4), (fx + 1.6 + side * 0.4, fy + 2.0), (fx - 1.6 + side * 0.4, fy + 2.0)])
        for k in (-1, 0, 1):
            claws.append((int(round(fx + side * 0.4 + k * 1.3 - 0.5)), int(round(fy + 2.5))))
    return {"head": head, "head_c": (hx, hy), "body": body, "body_c": (bx, by),
            "ears": ears, "inner": inner, "feet": feet, "claws": claws}


def shade_sphere(cx, cy, rx, ry, bias=0.0):
    nx = (XS - cx) / rx
    ny = (YS - cy) / ry
    nz = np.sqrt(np.clip(1 - nx * nx - ny * ny, 0.02, 1))
    n = np.stack([nx, ny, nz], axis=-1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return n @ LIGHT + bias


def draw_body(img, dy, ear_tilt, foot_dy, mouth="open", ear_dy=0, eyes="auf"):
    B = body_masks(dy, ear_tilt, foot_dy, ear_dy)
    ears = B["ears"][0] | B["ears"][1]
    sil = B["head"] | B["body"] | ears | B["feet"]
    hx, hy = B["head_c"]
    bx, by = B["body_c"]

    def colors(img, m):
        lh = shade_sphere(hx, hy - 2, 17, 15)
        lb = shade_sphere(bx, by, 11, 11.5, -0.08)
        l = np.where(B["head"] | ears, lh, lb)
        idx = np.select([l < 0.05, l < 0.42, l < 0.78], [1, 2, 2], 3)
        idx = np.where(l < 0.05, 1, np.where(l < 0.45, 2, 3))
        for k in (1, 2, 3):
            img[m & (idx == k)] = FUR[k]
        # Kinnschatten auf dem Rumpf
        chin = B["body"] & ~B["head"] & (YS < hy + 16 + 0.0) & dilate(dilate(B["head"]))
        img[m & chin] = FUR[1]
        # Rand-Licht oben links (1 px Kante)
        edge = m & ~erode(m)
        rim = edge & (l > 0.55)
        img[rim] = FUR[4]
        # Ohren: Innenseite rosa, zur Spitze heller
        for side, inner in zip((-1, 1), B["inner"]):
            ys = YS[inner]
            if ys.size == 0:
                continue
            t = (YS - ys.min()) / max(1.0, ys.max() - ys.min())
            iidx = np.where(t < 0.35, 3, np.where(t < 0.75, 2, 1))
            for k in (1, 2, 3):
                img[inner & (iidx == k)] = MEM[k]
            # dunkle Falte an der Innenkante
            fold = inner & ~erode(inner) & ((XS - hx) * side < (XS[inner].mean() - hx) * side)
            img[fold] = MEM[0]
        # Fuesse
        img[m & B["feet"]] = FUR[1]
        ft = B["feet"] & ~np.roll(B["feet"], 1, axis=1)
        img[m & ft] = FUR[2]
        # Brustlatz (cremefarbener Pelz, unten gezackt)
        latz = np.zeros((H, W), bool)
        pts = [(hx - 9, hy + 9), (hx + 9, hy + 9), (hx + 8, hy + 15), (hx + 5, hy + 13.5), (hx + 4, hy + 18),
               (hx + 1, hy + 15.5), (hx - 1, hy + 19.5), (hx - 3, hy + 15.5), (hx - 5, hy + 17.5),
               (hx - 6, hy + 13.5), (hx - 8.5, hy + 15)]
        latz = poly_mask(pts) & m
        lc = shade_sphere(hx, hy + 12, 10, 8)
        cidx = np.where(lc < 0.2, 0, np.where(lc < 0.55, 1, 2))
        for k in (0, 1, 2):
            img[latz & (cidx == k)] = CREAM[k]
    layer(img, sil, colors)

    # Krallen unten an den Fuessen
    for x, y in B["claws"]:
        if 0 <= y < H - 1:
            img[y, x] = CREAM[2]
            img[y + 1, x] = OUT
            for xx in (x - 1, x + 1):
                if img[y, xx][3] == 0:
                    img[y, xx] = OUT

    # Gesicht
    ex, ey = int(hx) - 11, int(hy) - 3
    # Brauenschatten
    for i in range(6):
        for side in (0, 1):
            x = ex + 1 + i if side == 0 else int(hx) + 10 - i
            y = ey - 1 + i // 2
            img[y, x] = FUR[1]
    if eyes == "auf":
        put(img, EYE_L, ex, ey, EYE)
        put(img, EYE_L, int(hx) + 3, ey, EYE, flip=True)
        # Glanzpunkt immer oben links
        img[ey + 3, int(hx) + 3 + 3] = EYE["W"]
        img[ey + 3, int(hx) + 3 + 4] = EYE["P"]
    else:
        st = EYE_HALB if eyes == "halb" else EYE_ZU
        put(img, st, ex, ey, EYE_LID)
        put(img, st, int(hx) + 3, ey, EYE_LID, flip=True)
    # Nase
    img[int(hy) + 3, int(hx) - 1] = NOSE
    img[int(hy) + 3, int(hx)] = NOSE
    img[int(hy) + 4, int(hx) - 1] = OUT
    img[int(hy) + 4, int(hx)] = OUT
    st = MOUTH_WIDE if mouth == "wide" else MOUTH_OPEN
    put(img, st, int(hx) - 5, int(hy) + 5,
        {"O": OUT, "M": MOUTH, "T": TONGUE, "F": FANG})


# ------------------------------------------------------------------ Bilder

# Fluegelschlag: (Oberarm-Hebung, Falten, Laenge, Koerper dy)
FLUG = [
    (78, 0.25, 0.92, 1),
    (66, 0.00, 0.98, 1),
    (44, 0.00, 1.00, 0),
    (16, 0.00, 1.00, -1),
    (-10, 0.00, 0.97, -2),
    (-22, 0.25, 0.93, -2),
    (-8, 0.65, 0.90, -1),
    (22, 0.90, 0.90, 0),
    (50, 0.80, 0.90, 1),
    (70, 0.55, 0.90, 1),
]


def bild(a1, fold, scale, dy, ear_tilt, foot_dy, mouth="open", ear_dy=0, eyes="auf"):
    img = np.zeros((H, W, 4), np.uint8)
    shoulder_y = 60 + dy
    sks = []
    for side in (-1, 1):
        sk = wing_skeleton((CX + side * 7.0, shoulder_y), a1, fold, scale, side)
        draw_wing(img, sk, 1 if side < 0 else 0)
        sks.append(sk)
    draw_body(img, dy, ear_tilt, foot_dy, mouth, ear_dy, eyes)
    for sk in sks:
        draw_sword(img, sk["wrist"], sk["thumb"])
    return img


def flug(blink=False):
    frames = []
    n = len(FLUG)
    dys = [f[3] for f in FLUG]
    for i, (a1, fold, sc, dy) in enumerate(FLUG):
        v = dys[i] - dys[i - 1]                 # Koerper steigt -> Ohren/Fuesse haengen nach
        v_prev = dys[i - 1] - dys[i - 2]
        lag = 0.6 * v + 0.4 * v_prev
        ear_tilt = 6 * (-lag)
        ear_dy = int(round(max(0, -lag)))
        foot_dy = int(round(-lag))
        mouth = "wide" if i in (3, 4, 5) else "open"
        eyes = {1: "halb", 2: "zu", 3: "halb"}.get(i, "auf") if blink else "auf"
        frames.append(bild(a1, fold, sc, dy, ear_tilt, foot_dy, mouth, ear_dy, eyes))
    return frames


def schreibe(name, frames, pivot=(0.5, 0.5)):
    os.makedirs(OUT_DIR, exist_ok=True)
    path = os.path.join(OUT_DIR, name + ".png")
    sheet = Image.new("RGBA", (W * len(frames), H))
    for i, f in enumerate(frames):
        sheet.paste(Image.fromarray(f), (i * W, 0))
    sheet.save(path)
    meta = path + ".meta"
    if not os.path.exists(meta):
        sys.path.insert(0, TOOLS)
        from unity_meta import write_strip_meta
        write_strip_meta(meta, name, len(frames), W, H, PPU, pivot=pivot)
    print("%-22s %2d Bilder  %dx%d" % (name, len(frames), W, H))


def vorschau(frames, path, scale=4):
    bg = (44, 40, 58, 255)
    out = []
    for f in frames:
        c = Image.new("RGBA", (W, H), bg)
        c.alpha_composite(Image.fromarray(f))
        out.append(c.convert("RGB").resize((W * scale, H * scale), Image.NEAREST))
    out[0].save(path, save_all=True, append_images=out[1:], duration=int(1000 / FPS), loop=0)
    sheet = Image.new("RGB", (W * scale * 5, H * scale * 2), bg[:3])
    for i, f in enumerate(out):
        sheet.paste(f, ((i % 5) * W * scale, (i // 5) * H * scale))
    sheet.save(os.path.splitext(path)[0] + "_sheet.png")


def main():
    frames = flug()
    blink = flug(blink=True)
    if "--preview" in sys.argv:
        path = sys.argv[sys.argv.index("--preview") + 1]
        vorschau(frames, path)
        vorschau(blink, os.path.splitext(path)[0] + "_blink.gif")
        return
    schreibe("fledermaus_flug", frames)
    schreibe("fledermaus_flug_blink", blink)


if __name__ == "__main__":
    main()
