"""
Effekte zum Gespenst / Squiddy (Tools/squiddy.py zeichnet den Boss selbst).
Farben und Licht kommen aus squiddy.py, damit alles aus einem Guss ist.

  Assets/Art/Gegner/new/boss/   (PPU 32)
    squiddy_laken_flug.png    12  112x112  Mitte: 0-3 entfaltet sich aus dem Knaeuel, 4-11 flattert (Schleife)
    squiddy_laken_boden.png    8  112x64   unten: 0-6 sinkt zusammen, 7 liegt (Gesicht schaut traurig)
    squiddy_spukgeist.png     13  24x24    Mitte: 0-7 fliegt (Schleife, schaut nach rechts), 8-12 verpufft
    squiddy_schatten.png       8  64x32    Mitte: Spukschatten mit Augen (Schleife, 5 blinzelt)
    squiddy_buh_welle.png      8  224x224  Mitte: Schreck-Druckwelle (Radius aussen 104 px = 3.25)
    squiddy_nessel_tentakel.png 12 40x88   unten: 0-2 Riss glueht, 3 bricht aus, 4-6 peitscht, 7-9 zurueck, 10-11 aus
    squiddy_baby.png          14  32x40    unten: 0-7 schwimmt (Schleife), 8-13 zerplatzt mit Blitz
    squiddy_entladung.png      8  336x336  Mitte: Hochspannung entlaedt sich (Radius 160 px = 5.0)

Aufruf: python Tools/squiddy_fx.py [--nur laken_flug,baby] [--preview ordner]
"""

import math
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import squiddy as S  # noqa: E402
from eis_gegner_kit import LIGHT, rgb, smooth  # noqa: E402

TAU = math.pi * 2
SS = 4


# ================================================================ kleines Werkzeug

def blank(w, h):
    return np.zeros((h, w, 4), np.uint8)


def put(img, r, c, col):
    if 0 <= r < img.shape[0] and 0 <= c < img.shape[1]:
        img[r, c] = col


def fine(w, h, ox, oy):
    """Feines Raster, X nach rechts ab ox, Y nach OBEN ab Zeile oy (Bildpixel)."""
    ys, xs = np.mgrid[0:h * SS, 0:w * SS]
    return (xs + 0.5) / SS - ox, oy - (ys + 0.5) / SS


def down(owner, lam, w, h):
    o = owner.reshape(h, SS, w, SS).transpose(0, 2, 1, 3).reshape(h, w, SS * SS)
    l = lam.reshape(h, SS, w, SS).transpose(0, 2, 1, 3).reshape(h, w, SS * SS)
    codes = np.unique(owner)
    pix = np.zeros((h, w), np.int16)
    if len(codes) > 1 or codes[0] != 0:
        counts = np.stack([(o == c).sum(-1) for c in codes], -1)
        solid = codes != 0
        if solid.any():
            pix = codes[solid][np.argmax(counts[..., solid], -1)].astype(np.int16)
    pix[(o == 0).sum(-1) > SS * SS // 2] = 0
    sel = o == pix[..., None]
    lm = np.where(sel.any(-1), (l * sel).sum(-1) / np.maximum(sel.sum(-1), 1), 0.0)
    return pix, lm


def outline(img, pix, line_of):
    h, w = pix.shape
    out = img.copy()
    for r in range(h):
        for c in range(w):
            m = pix[r, c]
            if m == 0:
                continue
            for dr, dc in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                rr, cc = r + dr, c + dc
                if not (0 <= rr < h and 0 <= cc < w) or pix[rr, cc] == 0:
                    col = line_of.get(m)
                    if col is not None:
                        out[r, c] = col
                    break
    return out


def bolt(img, r0, c0, r1, c1, rng, core=S.SPARK[0], glow=S.SPARK[2], rough=0.28, depth=3):
    pts = [(float(r0), float(c0)), (float(r1), float(c1))]
    for _ in range(depth):
        nxt = [pts[0]]
        for (ra, ca), (rb, cb) in zip(pts, pts[1:]):
            L = math.hypot(rb - ra, cb - ca)
            off = rng.uniform(-rough, rough) * L
            d = max(L, 1e-3)
            nxt += [((ra + rb) / 2 + off * (cb - ca) / d, (ca + cb) / 2 - off * (rb - ra) / d), (rb, cb)]
        pts = nxt
    cells = []
    for (ra, ca), (rb, cb) in zip(pts, pts[1:]):
        n = int(max(abs(rb - ra), abs(cb - ca))) + 1
        for i in range(n + 1):
            cells.append((int(round(ra + (rb - ra) * i / n)), int(round(ca + (cb - ca) * i / n))))
    cs = set(cells)
    if glow is not None:
        for r, c in cells:
            for dr, dc in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                if (r + dr, c + dc) not in cs:
                    put(img, r + dr, c + dc, glow)
    for r, c in cells:
        put(img, r, c, core)


def with_alpha(col, a):
    return (col[0], col[1], col[2], int(max(0, min(255, a))))


# ================================================================ Laken (flach)

def sheet_shape(s, t):
    """Laken-Umriss in eigenen Koordinaten: s quer -1..1, t hoch -1..1 (oben rund, unten Zipfel)."""
    top = (s ** 2 + np.maximum(t - 0.25, 0) ** 2 / 0.75 ** 2) <= 1.0
    body = (np.abs(s) <= 1.0) & (t <= 0.25)
    lobe = (0.5 + 0.5 * np.cos(math.pi * 4 * s)) ** 1.6
    hem = t >= -1.0 + 0.16 * lobe
    return (top | body) & hem


def sheet_face(s, t, kind="grin"):
    """Augen- und Mundloch im leeren Laken (echte Loecher - man sieht den Boden)."""
    holes = np.zeros(s.shape, bool)
    for side in (-1, 1):
        cx, cy = side * 0.33, 0.40
        e = ((s - cx) / 0.15) ** 2 + ((t - cy) / 0.18) ** 2 <= 1.0
        if kind == "traurig":
            e &= (t - cy) <= 0.08 - 0.6 * side * (s - cx)
        holes |= e
    if kind == "traurig":
        m = ((s / 0.2) ** 2 + ((t - 0.02) / 0.1) ** 2 <= 1.0) & ((t - 0.02) >= 0.02 - 0.12 * (s / 0.2) ** 2)
    else:
        m = (s / 0.11) ** 2 + ((t - 0.08) / 0.13) ** 2 <= 1.0
    return holes | m


SHEET_CLS, SHEET_BACK = 1, 2


def render_sheet(w, h, cx, cy, W, H, rot=0.0, turn=1.0, wave_amp=0.0, wave_ph=0.0, wave_k=1.0,
                 crumple=0.0, face="grin", squash=1.0):
    """Ein flaches Laken: W x H Pixel gross, um rot Grad gedreht, turn<0 = Rueckseite (gespiegelt)."""
    X, Y = fine(w, h, cx, cy)
    r = math.radians(rot)
    x = X * math.cos(r) + Y * math.sin(r)
    y = -X * math.sin(r) + Y * math.cos(r)
    sx = max(abs(turn), 0.12)
    s = x / (W / 2 * sx)
    # Welle laeuft quer ueber das Tuch (senkrechte Verschiebung)
    disp = wave_amp * np.sin(math.pi * wave_k * s + wave_ph)
    t = (y - disp) / (H / 2 * squash)
    # Knautschen: Umriss wellig
    if crumple > 0:
        ang = np.arctan2(t, s)
        rr = np.sqrt(s * s + t * t)
        k = 1 + crumple * (0.12 * np.sin(ang * 5 + 0.7) + 0.08 * np.sin(ang * 9 + 2.0))
        s, t = s / k, t / k
        _ = rr
    shape = sheet_shape(s, t)
    holes = sheet_face(s * np.sign(turn) if turn != 0 else s, t, face) & shape
    shape &= ~holes
    owner = np.zeros(X.shape, np.int16)
    owner[shape] = SHEET_CLS if turn >= 0 else SHEET_BACK
    # Licht: Neigung der Welle + Rundung zum Rand + Knautschfalten
    slope = wave_amp * math.pi * wave_k / (W / 2 * sx) * np.cos(math.pi * wave_k * s + wave_ph)
    lam = 0.55 + 0.28 * np.clip(slope, -1.5, 1.5) - 0.25 * np.abs(s) ** 3 + 0.18 * t
    if crumple > 0:
        lam = lam + crumple * 0.35 * np.sin(s * 7.5 + t * 4.0 + 1.1) * np.cos(t * 5.0 - s * 2.0)
    return owner, lam.astype(np.float32)


def shade_sheet(pix, lam):
    h, w = pix.shape
    img = blank(w, h)
    for r in range(h):
        for c in range(w):
            m = pix[r, c]
            l = lam[r, c]
            if m == SHEET_CLS:
                img[r, c] = S.SHEET[3] if l > 0.75 else S.SHEET[2] if l > 0.38 else S.SHEET[1] if l > 0.05 else S.SHEET[0]
            elif m == SHEET_BACK:
                img[r, c] = S.SHEET[2] if l > 0.7 else S.SHEET[1] if l > 0.3 else S.SHEET_UNDER[1] if l > 0.0 else S.SHEET_UNDER[0]
    return outline(img, pix, {SHEET_CLS: S.SHEET_LINE, SHEET_BACK: S.SHEET_LINE})


def laken_flug():
    """Das weggeschleuderte Laken: entfaltet sich und flattert. 112x112, Mitte."""
    w = h = 112
    frames = []
    for k in range(12):
        if k < 4:
            e = smooth(0, 1, (k + 1) / 4)
            owner, lam = render_sheet(w, h, 56, 56, 46 + 38 * e, 30 + 44 * e, rot=-30 + 25 * e,
                                      turn=1.0, wave_amp=4 + 6 * e, wave_ph=k * 1.3, crumple=1.2 * (1 - e) + 0.15,
                                      squash=0.6 + 0.4 * e)
        else:
            q = (k - 4) / 8
            ph = TAU * q
            owner, lam = render_sheet(w, h, 56, 56, 84, 74, rot=-6 + 10 * math.sin(ph),
                                      turn=math.cos(ph) * 0.35 + 0.75, wave_amp=7 + 2 * math.sin(ph * 2),
                                      wave_ph=-ph * 2, wave_k=1.6, crumple=0.15, squash=0.95 + 0.06 * math.sin(ph))
        pix, lm = down(owner, lam, w, h)
        frames.append(shade_sheet(pix, lm))
    return frames, w, h, (0.5, 0.5)


def laken_boden():
    """Landung: das leere Laken sackt zusammen und bleibt liegen. 112x64, Pivot unten."""
    w, h = 112, 64
    frames = []
    for k in range(8):
        e = smooth(0, 1, min(1.0, (k + 1) / 7))
        bounce = 0.12 * math.sin(e * math.pi * 2.2) * (1 - e)
        sq = 0.62 - 0.32 * e + bounce
        owner, lam = render_sheet(w, h, 56, h - 4 - 30 * sq * 0.9, 84 + 14 * e, 74, rot=0, turn=1.0,
                                  wave_amp=5 * (1 - e) + 1.5, wave_ph=k * 0.9, wave_k=2.2, crumple=0.25 + 0.45 * e,
                                  face="grin" if k < 5 else "traurig", squash=sq)
        pix, lm = down(owner, lam, w, h)
        img = shade_sheet(pix, lm)
        # weicher Schatten unter dem Tuch
        sh = blank(w, h)
        for r in range(h):
            for c in range(w):
                if ((c + 0.5 - 56) / (48 + 6 * e)) ** 2 + ((r + 0.5 - (h - 4)) / 5.0) ** 2 <= 1:
                    sh[r, c] = S.SHADOW
        out = sh.copy()
        m = img[..., 3] > 0
        out[m] = img[m]
        frames.append(out)
    return frames, w, h, (0.5, 4 / h)


# ================================================================ Mini-Spukgeist

GHOST_CLS = 1


def spukgeist():
    """Kleiner, wuetender Zuckergeist (Geisterreigen). 24x24, schaut nach rechts."""
    w = h = 24
    frames = []
    aura = rgb("b48cff", 150)
    for k in range(13):
        img = blank(w, h)
        if k < 8:
            ph = TAU * k / 8
            X, Y = fine(w, h, 12, 13)
            # Kopf rund, Schleppe nach links (fliegt nach rechts), Zipfel wedeln
            head = ((X - 1.5) / 7.5) ** 2 + ((Y - 1.0) / 7.5) ** 2 <= 1.0
            tx = np.clip(-(X - 1.5) / 11.0, 0, 1)
            half = 7.5 * (1 - tx ** 1.4) + 0.5
            wig = 1.8 * np.sin(tx * 6.0 - ph) * tx
            tail = (X <= 1.5) & (X >= -9.5) & (np.abs(Y - 1.0 + 2.5 * tx - wig) <= half * (1 - 0.15 * tx))
            lob = np.cos(Y * 1.4 + ph) * 1.2
            tail &= X >= -9.5 + lob * tx
            shape = head | tail
            lam = 0.6 - 0.05 * (X - 1.5) * -0.0 + 0.06 * (Y - 1.0) - 0.04 * np.abs(X - 1.5)
            owner = np.where(shape, GHOST_CLS, 0).astype(np.int16)
            pix, lm = down(owner, lam.astype(np.float32), w, h)
            for r in range(h):
                for c in range(w):
                    if pix[r, c]:
                        l = lm[r, c]
                        img[r, c] = S.SHEET[3] if l > 0.75 else S.SHEET[2] if l > 0.25 else S.SHEET[1]
            img = outline(img, pix, {GHOST_CLS: S.SHEET_LINE})
            # violetter Schein aussen
            glow = img.copy()
            for r in range(h):
                for c in range(w):
                    if img[r, c, 3] == 0 and any(0 <= r + a < h and 0 <= c + b < w and pix[r + a, c + b]
                                                 for a, b in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                        glow[r, c] = aura
            img = glow
            # Gesicht: boese Augen + offener Mund
            bob = 0
            ey = 11 + bob
            for (r, c) in ((ey - 1, 13), (ey, 13), (ey, 14), (ey - 1, 17), (ey, 17), (ey, 16)):
                put(img, r, c, S.HOLE)
            put(img, ey - 2, 12, S.HOLE)
            put(img, ey - 2, 18, S.HOLE)
            for (r, c) in ((ey + 3, 15), (ey + 3, 16), (ey + 4, 15), (ey + 4, 16)):
                put(img, r, c, S.HOLE)
            if k % 4 < 2:
                put(img, ey + 4, 16, rgb("ff9cc8"))
        else:
            q = (k - 8) / 5
            rng = np.random.RandomState(4)
            for i in range(9):
                ang = TAU * i / 9 + rng.uniform(-0.2, 0.2)
                d = 2 + 9 * q
                rr = int(round(12 - math.sin(ang) * d))
                cc = int(round(12 + math.cos(ang) * d))
                col = S.SHEET[3] if q < 0.5 else S.SHEET[1]
                size = 2 if q < 0.6 else 1
                for a in range(size):
                    for b in range(size):
                        put(img, rr + a, cc + b, col)
            if q < 0.3:
                for a in range(-3, 4):
                    for b in range(-3, 4):
                        if a * a + b * b <= 9:
                            put(img, 12 + a, 12 + b, with_alpha(S.SHEET[3], 200))
        frames.append(img)
    return frames, w, h, (0.5, 0.5)


# ================================================================ Spukschatten

def schatten():
    """Dunkle Pfuetze mit glimmenden Augen - so jagt der abgetauchte Geist den Spieler."""
    w, h = 64, 32
    frames = []
    for k in range(8):
        img = blank(w, h)
        ph = TAU * k / 8
        cx, cy = 32, 17
        for r in range(h):
            for c in range(w):
                dx, dy = (c + 0.5 - cx) / 27.0, (r + 0.5 - cy) / 9.0
                ang = math.atan2(dy, dx)
                rr = math.hypot(dx, dy) / (1 + 0.06 * math.sin(ang * 4 + ph) + 0.04 * math.sin(ang * 7 - ph * 2))
                if rr <= 1.0:
                    img[r, c] = S.PUDDLE[0] if rr < 0.62 else S.PUDDLE[1]
                    if 0.9 < rr:
                        img[r, c] = rgb("0e0718")
                # Wellenring wandert nach aussen
                ring = 0.45 + 0.5 * ((k / 8) % 1.0)
                if abs(rr - ring) < 0.05 and rr < 0.9:
                    img[r, c] = S.PUDDLE[2] if k < 6 else S.PUDDLE[1]
        blink = k == 5
        for side in (-1, 1):
            ex = cx + side * 7
            rows = ([".##.", "####"] if not blink else ["....", ".##."])
            if side < 0:
                rows = [rw[::-1] for rw in rows]
            for dr, row in enumerate(rows):
                for dc, ch in enumerate(row):
                    if ch == "#":
                        # boese: innen tiefer
                        lift = (1 if (dc < 2) == (side > 0) else 0) if not blink else 0
                        put(img, cy - 4 + dr + lift, ex - 2 + dc, S.PUPIL[1] if dr == 0 else S.PUPIL[0])
        frames.append(img)
    return frames, w, h, (0.5, 0.5)


# ================================================================ Schreck-Welle

def buh_welle():
    w, h = 224, 224
    frames = []
    R = 104.0
    rng = np.random.RandomState(12)
    wisps = [(rng.uniform(0, TAU), rng.uniform(0.75, 1.0)) for _ in range(14)]
    for k in range(8):
        img = blank(w, h)
        q = (k + 1) / 8
        rad = R * (1 - (1 - q) ** 2.2)
        th = max(1.5, 7.0 * (1 - q) + 1.5)
        a = int(255 * (1 - smooth(0.55, 1.0, q)) * 0.95 + 20)
        for r in range(h):
            for c in range(w):
                dx, dy = c + 0.5 - w / 2, (r + 0.5 - h / 2)
                d = math.hypot(dx, dy)
                if abs(d - rad) <= th:
                    inner = d < rad - th * 0.3
                    col = S.SHEET[3] if not inner else S.SHEET[1]
                    img[r, c] = with_alpha(col, a)
                elif rad - th - 5 < d < rad - th and k < 5:
                    img[r, c] = with_alpha(S.SHEET[0], a * 0.45)
        # Geisterfetzen fliegen mit
        for ang, sp in wisps:
            d = rad * sp
            for i in range(5):
                aa = ang - i * 0.035
                c = int(w / 2 + math.cos(aa) * d)
                r = int(h / 2 - math.sin(aa) * d)
                put(img, r, c, with_alpha(S.SHEET[2] if i else S.SHEET[3], a * (1 - i / 6)))
        frames.append(img)
    return frames, w, h, (0.5, 0.5)


# ================================================================ Nessel-Tentakel aus dem Boden

def nessel_tentakel():
    w, h = 40, 88
    G = h - 8                                   # Bodenzeile
    frames = []
    heights = [0, 0, 0, 34, 62, 64, 60, 40, 18, 4, 0, 0]
    rings = [0.35, 0.7, 1.0, 1.0, 1.0, 1.0, 0.95, 0.9, 0.8, 0.7, 0.45, 0.2]
    bends = [0, 0, 0, 0.2, -0.6, 0.8, -0.3, 0.2, 0.0, 0, 0, 0]
    for k in range(12):
        img = blank(w, h)
        # glimmender Riss / Ring im Boden
        rg = rings[k]
        for r in range(h):
            for c in range(w):
                dx, dy = (c + 0.5 - w / 2) / (15 * rg + 0.01), (r + 0.5 - G) / (5 * rg + 0.01)
                d = math.hypot(dx, dy)
                if d <= 1.0:
                    img[r, c] = S.PUDDLE[0] if d < 0.6 else S.PUDDLE[1]
                    if d > 0.82:
                        img[r, c] = rgb("8a5cd8") if k < 10 else S.PUDDLE[2]
        if 1 <= k <= 2:
            rng = np.random.RandomState(k)
            bolt(img, G, w // 2 - 10, G - 1, w // 2 + 9, rng, rough=0.35, depth=2)
        H = heights[k]
        if H > 0:
            X, Y = fine(w, h, w / 2, G)
            owner = np.zeros(X.shape, np.int16)
            lam = np.zeros(X.shape, np.float32)
            n = 60
            b = bends[k]
            for i in range(n + 1):
                s = i / n
                u = b * 14 * s ** 1.6 + 3 * math.sin(s * 5 + k) * s
                v = H * s
                if s > 0.82:
                    u += b * 10 * (s - 0.82) / 0.18 * (1 if b >= 0 else -1) * 0.6
                rad = 4.6 * (1 - 0.7 * s) + 0.6
                dx, dy = X - u, Y - v
                d2 = dx * dx + dy * dy
                m = d2 <= rad * rad
                owner[m] = 1
                lam[m] = (dx[m] / rad) * LIGHT[0] + (dy[m] / rad) * LIGHT[1] + np.sqrt(np.clip(1 - d2[m] / rad ** 2, 0, 1)) * LIGHT[2]
            owner[Y < 0] = 0
            pix, lm = down(owner, lam, w, h)
            t = blank(w, h)
            for r in range(h):
                for c in range(w):
                    if pix[r, c]:
                        l = lm[r, c]
                        t[r, c] = S.TENT_F[3] if l > 0.6 else S.TENT_F[2] if l > 0.2 else S.TENT_F[1]
            t = outline(t, pix, {1: S.JELLY_LINE})
            m = t[..., 3] > 0
            img[m] = t[m]
            # Strom laeuft am Tentakel hoch
            if 4 <= k <= 6:
                rng = np.random.RandomState(k * 3)
                y0 = G - int(H * 0.25)
                y1 = G - int(H * 0.85)
                bolt(img, y0, w // 2 + int(b * 4) - 6, y1, w // 2 + int(b * 12) + 5, rng, rough=0.3, depth=3)
            # Erdbrocken beim Ausbruch
            if k == 3:
                for (r, c) in ((G - 6, 8), (G - 9, 12), (G - 4, 30), (G - 10, 27), (G - 13, 18)):
                    put(img, r, c, rgb("5a3a28"))
                    put(img, r, c + 1, rgb("7a5238"))
        frames.append(img)
    return frames, w, h, (0.5, 8 / h)


# ================================================================ Baby-Squiddy

def baby():
    """Mini-Squiddy mit Heiligenschein. 32x40, Pivot unten (schwebt im Bild)."""
    w, h = 32, 40
    G = h - 3
    frames = []
    for k in range(14):
        img = blank(w, h)
        if k < 8:
            ph = TAU * k / 8
            pl = math.sin(ph) * 0.5 + 0.5 if k < 3 else math.cos((k - 2) / 6 * math.pi / 2)
            pl = 0.5 + 0.5 * math.sin(ph)
            rise = 2.0 * pl
            X, Y = fine(w, h, w / 2, G)
            owner = np.zeros(X.shape, np.int16)
            lam = np.zeros(X.shape, np.float32)
            a = 10.0 * (1 - 0.12 * pl)
            b = 9.0 * (1 + 0.12 * pl)
            rim = 15.0 + rise
            # Schatten
            sh = ((X / 9) ** 2 + ((Y + 0.5) / 2.2) ** 2) <= 1
            owner[sh] = 9
            # Tentakel
            for i, tu in enumerate((-6.0, -2.0, 2.0, 6.0)):
                for j in range(30):
                    s = j / 30
                    u = tu * (1 + 0.25 * (1 - pl) * s) + 2.2 * math.sin(s * 5 - ph + i * 1.7) * s
                    v = rim - 1 - 11 * s
                    rad = 1.9 * (1 - 0.5 * s)
                    m = (X - u) ** 2 + (Y - v) ** 2 <= rad * rad
                    owner[m] = 3
                    lam[m] = 0.4 - 0.3 * (X[m] - u) / rad
            # Glocke
            xx = np.abs(X) / a
            yy = (Y - rim) / b
            bell = (xx ** 2.2 + np.maximum(yy, 0) ** 2 <= 1) & (yy > -0.22)
            owner[bell] = 1
            lam[bell] = (0.75 - 0.5 * X[bell] / a + 0.35 * yy[bell]).astype(np.float32)
            pix, lm = down(owner, lam, w, h)
            for r in range(h):
                for c in range(w):
                    m = pix[r, c]
                    l = lm[r, c]
                    if m == 1:
                        img[r, c] = S.BELL[4] if l > 0.95 else S.BELL[3] if l > 0.55 else S.BELL[2]
                    elif m == 3:
                        img[r, c] = S.TENT_F[2] if l > 0.4 else S.TENT_F[1]
                    elif m == 9:
                        img[r, c] = S.SHADOW
            img = outline(img, np.where(pix == 9, 0, pix), {1: S.JELLY_LINE, 3: S.JELLY_LINE})
            # Gesicht: zufriedene Striche + kleines w
            ry = int(round(G - rim - b * 0.45))
            for side in (-1, 1):
                for dc in range(3):
                    put(img, ry, w // 2 + side * (3 + dc) - (1 if side > 0 else 0), S.FACE)
            for dc, dr in ((-2, 0), (-1, 1), (0, 0), (1, 1), (2, 0)):
                put(img, ry + 3 + dr, w // 2 + dc, S.FACE)
            put(img, ry - 3, w // 2 - 5, S.SPEC[1])
            put(img, ry - 4, w // 2 - 4, S.SPEC[1])
            # Heiligenschein
            hy = G - rim - b - 1 + (1 if pl > 0.5 else 0)
            for dc in range(-6, 7):
                rr = int(round(hy + (0 if abs(dc) < 5 else -1 if abs(dc) == 5 else -1)))
                put(img, rr - 1, w // 2 + dc, S.GOLD[3] if dc < 3 else S.GOLD[2])
                if abs(dc) >= 5:
                    put(img, rr - 2, w // 2 + dc, S.GOLD[2])
            for dc in range(-4, 5):
                put(img, int(hy) - 3, w // 2 + dc, S.GOLD[4] if dc < 0 else S.GOLD[3])
        else:
            q = (k - 8) / 6
            rng = np.random.RandomState(7 + k)
            cy, cx = G - 18, w // 2
            if k == 8:
                for r in range(h):
                    for c in range(w):
                        if (c + 0.5 - cx) ** 2 + (r + 0.5 - cy) ** 2 <= 81:
                            img[r, c] = S.BELL_HOT[4] if (c + 0.5 - cx) ** 2 + (r + 0.5 - cy) ** 2 <= 36 else S.BELL_HOT[2]
            for i in range(4 if k < 12 else 2):
                ang = rng.uniform(0, TAU)
                L = 6 + 9 * q
                bolt(img, cy, cx, int(cy - math.sin(ang) * L), int(cx + math.cos(ang) * L), rng, depth=2)
            for i in range(6):
                ang = TAU * i / 6 + 0.4
                d = 4 + 10 * q
                put(img, int(cy - math.sin(ang) * d), int(cx + math.cos(ang) * d), S.BELL[3] if q < 0.6 else S.BELL[1])
            if k <= 10:
                put(img, cy - 12 - int(6 * q), cx - 1, S.GOLD[3])
                put(img, cy - 12 - int(6 * q), cx, S.GOLD[3])
                put(img, cy - 12 - int(6 * q), cx + 1, S.GOLD[3])
        frames.append(img)
    return frames, w, h, (0.5, 3 / h)


# ================================================================ Entladung

def entladung():
    w, h = 336, 336
    R = 160.0
    frames = []
    for k in range(8):
        img = blank(w, h)
        rng = np.random.RandomState(31 + k)
        cx, cy = w / 2, h / 2
        q = (k + 1) / 8
        fade = 1 - smooth(0.4, 1.0, q)
        # Blitzscheibe am Anfang
        if k <= 2:
            rad = R * (0.35 + 0.65 * q / 0.375) if k < 2 else R
            a = (230, 170, 90)[k]
            for r in range(h):
                for c in range(w):
                    d = math.hypot(c + 0.5 - cx, r + 0.5 - cy)
                    if d <= min(rad, R):
                        img[r, c] = with_alpha(S.BELL_HOT[3] if d < rad * 0.6 else S.BELL_HOT[2], a * (0.55 + 0.45 * (1 - d / R)))
        # Ring aus Strom am Rand
        if k < 7:
            n = 28
            pts = []
            for i in range(n):
                ang = TAU * i / n + rng.uniform(-0.05, 0.05)
                rr = R * (0.97 + rng.uniform(-0.03, 0.03))
                pts.append((int(cy - math.sin(ang) * rr), int(cx + math.cos(ang) * rr)))
            for i in range(n):
                if fade < 0.5 and rng.uniform() < 0.5:
                    continue
                (r0, c0), (r1, c1) = pts[i], pts[(i + 1) % n]
                bolt(img, r0, c0, r1, c1, rng, rough=0.3, depth=2,
                     glow=S.SPARK[2] if k < 4 else S.BELL[3])
        # Blitze vom Zentrum nach aussen
        nb = (10, 10, 8, 6, 4, 3, 2, 0)[k]
        for i in range(nb):
            ang = rng.uniform(0, TAU)
            L = R * rng.uniform(0.55, 1.0)
            r1 = int(cy - math.sin(ang) * L)
            c1 = int(cx + math.cos(ang) * L)
            bolt(img, int(cy), int(cx), r1, c1, rng, rough=0.25, depth=4)
        if k == 7:
            img[..., 3] = (img[..., 3] * 0.5).astype(np.uint8)
        frames.append(img)
    return frames, w, h, (0.5, 0.5)


FX = {
    "laken_flug": laken_flug,
    "laken_boden": laken_boden,
    "spukgeist": spukgeist,
    "schatten": schatten,
    "buh_welle": buh_welle,
    "nessel_tentakel": nessel_tentakel,
    "baby": baby,
    "entladung": entladung,
}


def main():
    args = sys.argv[1:]
    only = args[args.index("--nur") + 1].split(",") if "--nur" in args else None
    pv = args[args.index("--preview") + 1] if "--preview" in args else None
    write = "--kein-export" not in args
    for name, fn in FX.items():
        if only and name not in only:
            continue
        frames, w, h, pivot = fn()
        if write:
            S.save("squiddy_" + name, frames, w, h, pivot=pivot)
        if pv:
            S.preview(name, frames, pv, scale=3)


if __name__ == "__main__":
    main()
