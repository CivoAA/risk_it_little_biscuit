"""
Der Verkohlte, Phase 3: er saugt den Spieler ein, und drinnen schlaegt sein
Herz. Alles hier passt zu Tools/verkohlter.py (gleiche Paletten, gleiche
Kruste) und nimmt dessen Koerper-Renderer mit.

  Assets/Resources/Verkohlter/
    verkohlter_einsaugen.png   24 Bilder 128x128  0-7 holt Luft (blaeht sich auf, Maul reisst auf),
                                                  8-15 Sog-Schleife (Schlund dreht sich),
                                                  16-23 Schlucken (Maul zu, Schluck rutscht runter, grinst)
    verkohlter_sog.png          8 Bilder 160x160  Windschlieren, die in den Schlund laufen (Schleife)
    verkohlter_herz.png        16 Bilder 144x152  das Glutherz: Bum-bumm, dann Ruhe (Schleife, 12 fps)
    verkohlter_herz_tod.png    38 Bilder 288x248  Zerfall: 0-9 ueberhitzen, 10 Blitz, 11-28 Kruste bricht,
                                                  Brocken fallen ins Becken, Kern verglueht, 29-37 Reste/Rauch
    herzkammer_hinten.png       1 Bild   1152x704  die Kammer: Kuppel, Rueckwand, Boden (Bild 0 der Glut)
    herzkammer_glut.png        16 Bilder (Ausschnitt) alles, was im Herzschlag pulsiert - liegt genau
                                                  auf herzkammer_hinten (Pivot rechnet das Skript aus)
    herzkammer_vorne.png        1 Bild   1152x704  die vordere Kante - liegt VOR dem Spieler
  alle PPU 32, Point-Filter. Herz und Kammer laufen auf derselben Uhr
  (16 Bilder, 12 fps) - der Code spielt beide mit demselben Bildindex.

Die Kammer ist groesser als das Bild (Boden 944x492 px, die Kamera zeigt
480x270): die Kamera folgt dem Spieler, entfernt sich aber hoechstens so weit
vom Herzen, dass seine Mitte immer im Bild bleibt (VerkohlterArt.CameraSlack). Mitte
des Bildes = Herz. Ausserhalb von 960x544 ist alles schwarz - das legt der
Code als grosse Flaeche dahinter. herzkammer_glut liegt als 4x4-Raster vor.
Der Herzschlag auf dem Boden ist bewusst dezent (Nick: "etwas unauffaelliger").

Aufruf aus dem Projektordner:
  python Tools/verkohlter_herz.py [--preview ordner] [--nur herz|herztod|einsaugen|sog|kammer]
"""

import json
import math
import os
import sys

import numpy as np
from PIL import Image

TOOLS = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, TOOLS)
import verkohlter as V  # noqa: E402
from unity_meta import write_strip_meta  # noqa: E402

ROOT = os.path.dirname(TOOLS)
OUT_DIR = os.path.join(ROOT, "Assets", "Resources", "Verkohlter")
PPU = 32
TAU = math.pi * 2

C, FIRE, E, VIO = V.C, V.FIRE, V.E, V.V
rgb = V.rgb
fbm, vnoise, hash2 = V.fbm, V.vnoise, V.hash2
dilate, neighbours_out = V.dilate, V.neighbours_out

BEAT_FRAMES = 16


def voronoi(u, v, pts):
    return V.voronoi(u, v, pts)


def seeds(spacing, extent, seed, jitter=0.85):
    return V.seeds(spacing, extent, seed, jitter)


def put(img, x, y, col):
    h, w = img.shape[:2]
    x = int(math.floor(x + 0.5))
    y = int(math.floor(y + 0.5))
    if 0 <= x < w and 0 <= y < h:
        if col[3] < 255 and img[y, x, 3] > 0:
            a = col[3] / 255.0
            img[y, x, :3] = (img[y, x, :3] * (1 - a) + col[:3] * a).astype(np.uint8)
            img[y, x, 3] = max(img[y, x, 3], col[3])
        else:
            img[y, x] = col


def with_alpha(col, a):
    c = col.copy()
    c[3] = a
    return c


def edge_distance(mask, cap=24):
    """Abstand jedes Innenpixels zum Rand (4er-Nachbarschaft, schichtweise abgetragen)."""
    d = np.zeros(mask.shape, np.float64)
    m = mask.copy()
    for k in range(1, cap + 1):
        if not m.any():
            break
        d[m] = k
        p = np.pad(m, 1)
        m = m & p[:-2, 1:-1] & p[2:, 1:-1] & p[1:-1, :-2] & p[1:-1, 2:]
    return d


def blur(a, n=1):
    for _ in range(n):
        p = np.pad(a, 1, mode="edge")
        a = (p[:-2, 1:-1] + p[2:, 1:-1] + p[1:-1, :-2] + p[1:-1, 2:] + 2 * a) / 6.0
    return a


# ====================================================================== Herz

HW, HH = 144, 152          # Zelle
HCX, HCY = 72.0, 86.0      # Herzmitte in der Zelle (Pivot)
HK = 46.0                  # Pixel je Einheit der Herzformel (halbe Breite)

H_FINE = seeds(8.0, 80, 41, 0.9)
H_COARSE = seeds(15.0, 80, 43, 0.95)
_r = np.random.RandomState(44)
H_ACTIVE = _r.rand(len(H_COARSE), len(H_COARSE)) < 0.42
H_ACTIVE = H_ACTIVE | H_ACTIVE.T

# geschmolzene Schokostuecke im Herzen (Herzkoordinaten in Pixeln, Radius)
H_CHIPS = [(-24, -18, 3.6), (22, -22, 3.2), (-31, 3, 2.7), (27, 5, 3.0), (-12, 21, 2.6), (10, 14, 2.2), (-2, -30, 2.0)]


def beat_curve(i):
    """Bum-bumm: Bild 1 zieht zusammen, 2 = BUM, 5 = bumm, ab 7 Ruhe."""
    table = {
        0: (1.00, 1.00, 1.00),
        1: (0.95, 0.96, 0.95),
        2: (1.10, 1.06, 1.85),
        3: (1.06, 1.04, 1.70),
        4: (0.99, 1.00, 1.25),
        5: (1.06, 1.04, 1.70),
        6: (1.03, 1.02, 1.35),
    }
    if i in table:
        return table[i]
    q = (i - 7) / (BEAT_FRAMES - 7)
    breathe = 0.008 * math.sin(math.pi * q)
    return (1.0 + breathe, 1.0 + breathe * 0.5, 1.15 - 0.2 * q)


def heart_frame(i, o=None):
    """Ein Bild des Herzens. i = Bild des Herzschlags. o (optional) uebersteuert
    sx, sy, heat, t, extra (Rissbreite), flames, dx, dy, halo, sparks - und bekommt
    mask/u/v zurueck (fuer den Zerfall)."""
    sx, sy, heat = beat_curve(i)
    t = i / BEAT_FRAMES
    o = o if o is not None else {}
    sx, sy, heat, t = o.get("sx", sx), o.get("sy", sy), o.get("heat", heat), o.get("t", t)
    extra = o.get("extra", 0.0)
    ox, oy = o.get("dx", 0.0), o.get("dy", 0.0)
    yy, xx = np.mgrid[0:HH, 0:HW].astype(np.float64)
    xx += 0.5
    yy += 0.5
    # Herzkoordinaten (Pixel, y nach unten), mit Schlag skaliert
    u = (xx - HCX - ox) / sx
    v = (yy - HCY - oy) / sy
    # leichte Schraeglage + Wackeln am Rand: verkohlt, nicht gestanzt
    wob = (fbm(u * 0.12 + 3, v * 0.12, 51) - 0.5) * 2.6
    X = (u + v * 0.05) / HK
    Y = -(v + wob) / HK + 0.22
    # Herzformel mit kraeftigen Boegen: x^2 + (1.25y - sqrt|x|)^2 < 1
    ax = np.sqrt(np.abs(X) + 0.004)
    f = X * X + (1.25 * Y - ax) ** 2 - 1
    mask = f < 0

    img = np.zeros((HH, HW, 4), np.uint8)

    # --- Woelbung: Hoehe aus dem Randabstand, Normale aus dem Gradienten
    d = edge_distance(mask, 28)
    z = np.sqrt(np.clip(1 - (1 - np.clip(d / 20.0, 0, 1)) ** 2, 0, 1))
    z = blur(z, 2)
    gy, gx = np.gradient(z)
    nx, ny = -gx * 9, -gy * 9
    nz = np.ones_like(nx)
    nl = np.sqrt(nx ** 2 + ny ** 2 + nz ** 2)
    lum = (nx * V.LIGHT[0] + ny * V.LIGHT[1] + nz * V.LIGHT[2]) / nl

    # --- Kruste (Schollen)
    d1, d2, i1, _ = voronoi(u, v, H_FINE)
    cellv = np.array([hash2(k, 9, 2) for k in range(len(H_FINE))])[i1]
    seam = (d2 - d1) < 0.9
    su, sv = u - H_FINE[i1, 0], v - H_FINE[i1, 1]
    facet = -(su * 0.55 + sv * 0.8) / 6.0
    tone = lum * 4.2 + (cellv - 0.5) * 0.8 + facet * 0.6 + (fbm(u * 0.4, v * 0.4, 52) - 0.5) * 0.6 - 1.55
    band = np.clip(np.digitize(tone, [0.1, 0.8, 1.45, 2.0, 2.5]) + 1, 1, 6)
    band = np.where(seam, np.maximum(band - 2, 1), band)

    # --- Glutrisse
    wu = u + (fbm(u * 0.2, v * 0.2, 53) - 0.5) * 7
    wv = v + (fbm(u * 0.2 + 7, v * 0.2, 54) - 0.5) * 7
    e1, e2, j1, j2 = voronoi(wu, wv, H_COARSE)
    gap = e2 - e1
    core = np.clip(d / 25.0, 0, 1)
    width = 0.9 + 0.7 * core + 0.55 * (heat - 1.0) + extra
    crack = H_ACTIVE[j1, j2] & (gap < width) & mask & (d > 2)

    # --- Mittelnaht: von der Kerbe oben zur Spitze, leicht geschwungen
    seam_x = 1.8 * np.sin(v * 0.11 + 0.6) + v * 0.05
    top_v = -HK * 0.6
    seam_line = mask & (np.abs(u - seam_x) < (0.8 + 0.5 * (heat - 1) + 0.6 * core)) & (v > top_v) & (d > 1)

    # --- Schokostuecke
    chip = np.zeros_like(mask)
    chip_rim = np.zeros_like(mask)
    chip_heat = np.zeros_like(u)
    for (cu, cv, cr) in H_CHIPS:
        dd = np.hypot(u - cu, (v - cv) * 1.15) + (fbm(u * 0.9, v * 0.9, 55) - 0.5) * 1.3
        m = (dd < cr) & mask
        chip |= m
        chip_rim |= (dd < cr + 1.0) & ~m & mask
        chip_heat = np.where(m, (1.1 - dd / cr * 0.7) * heat, chip_heat)

    hot = crack | seam_line | chip
    near1 = dilate(hot, 1) & mask & ~hot
    near2 = dilate(hot, 2) & mask & ~hot & ~near1

    for b in range(1, 7):
        img[mask & (band == b)] = C[b]
    img[near2 & (heat > 1.3)] = E[0]
    img[near1] = E[1] if heat < 1.5 else E[2]

    # Glut: je naeher am Kern und je staerker der Schlag, desto heller
    hval = (0.35 + 0.45 * (1 - np.clip(gap / np.maximum(width, 0.01), 0, 1)) + 0.55 * core) * heat
    hk = np.clip(np.floor(hval * 3.4), 1, 6).astype(int)
    for k in range(7):
        img[crack & (hk == k)] = FIRE[k]
    sk = np.clip(np.floor((0.6 + 0.9 * core) * heat * 3.0), 2, 6).astype(int)
    for k in range(7):
        img[seam_line & (sk == k)] = FIRE[k]
    img[chip_rim & ~hot] = FIRE[0]
    ck = np.clip(np.floor(chip_heat * 4.6), 1, 6).astype(int)
    for k in range(7):
        img[chip & (ck == k)] = FIRE[k]

    # violettes Randlicht unten rechts, helle Kante oben links
    out = neighbours_out(mask)
    inner = mask & ~out
    rim = neighbours_out(inner) & inner & ~hot
    rr = np.hypot(u, v) + 1e-6
    rl = (u * 0.45 + v * 0.9) / rr
    img[rim & (rl > 0.55)] = VIO[0]
    img[rim & (rl > 0.85)] = VIO[1]
    img[rim & (rl < -0.7) & (band >= 3)] = C[6]
    # Glanzlicht oben links auf der linken Woelbung
    glint = mask & (np.hypot(u + 28, v + 32) < 3.0) & ~hot
    img[glint] = C[6]
    img[mask & (np.hypot(u + 25.5, v + 35) < 1.4) & ~hot] = rgb("#8a6f5a")
    img[out] = C[0]

    # --- Flammen aus der Kerbe oben
    o["mask"], o["u"], o["v"] = mask, u, v
    o["body"] = img.copy()
    flame_layer(img, HCX + 0.5 + ox, HCY - HK * 0.58 * sy + 1 + oy, t,
                o.get("flames", 1.7 + 0.7 * (heat - 1.0)))

    # --- Hitzeschein ums Herz (gestuft, halbdurchsichtig)
    body = img[..., 3] > 0
    r1 = dilate(body, 1) & ~body
    r2 = dilate(body, 3) & ~body & ~r1
    r3 = dilate(body, 7) & ~body & ~r1 & ~r2
    g = (heat - 0.55) / 1.3
    if not o.get("halo", True):
        g = -10
    if g > -1:
        img[r1] = with_alpha(FIRE[3] if g > 0.7 else FIRE[2], int(min(220, 90 + 130 * g)))
        img[r2] = with_alpha(FIRE[1], int(min(150, 45 + 100 * g)))
        img[r3] = with_alpha(FIRE[1], int(min(80, 18 + 45 * g)))

    # --- Funken, die vom Herzen aufsteigen
    rng = np.random.RandomState(61)
    for k in range(10 if o.get("sparks", True) else 0):
        ph = rng.rand()
        sx0 = HCX + (rng.rand() - 0.5) * 76
        sy0 = HCY + (rng.rand() - 0.6) * 50
        q = (t * 2 + ph) % 1
        if q > 0.85:
            continue
        x = sx0 + math.sin(TAU * (q + ph)) * 3
        y = sy0 - q * 34
        kk = 6 if q < 0.15 else 5 if q < 0.35 else 4 if q < 0.55 else 3 if q < 0.7 else 2
        if img[int(min(HH - 1, max(0, y))), int(min(HW - 1, max(0, x))), 3] == 255:
            continue
        put(img, x, y, FIRE[kk])

    # Beim BUM: kurzer Hitzeblitz auf der Kruste
    if i == 2 and "heat" not in o:
        hotmask = mask & ~out & (band >= 4) & ~hot & (core > 0.35)
        img[hotmask] = E[2]
    return img


def flame_layer(img, bx, by, t, power):
    """Flammenzungen aus der Herzkerbe. t in [0,1), zwei Schwuenge pro Schleife."""
    h, w = img.shape[:2]
    layer = np.zeros((h, w), np.int8) - 1
    tongues = [(-5.0, 10, 0.0, 3.4), (0.0, 16, 0.37, 4.4), (5.0, 9, 0.71, 3.0), (-2.0, 6, 0.2, 2.4), (3.0, 7, 0.55, 2.2)]
    for (off, H, ph, Wd) in tongues:
        H = H * power * (0.78 + 0.22 * math.sin(TAU * (t * 2 + ph)) + 0.08 * math.sin(TAU * (t * 3 + ph * 2)))
        for s in np.linspace(0, 1, 40):
            hgt = s * H
            sway = (1.8 * math.sin(TAU * (t * 2 + ph + s * 0.7)) + 1.0 * math.sin(TAU * (t * 3 - s))) * s ** 1.4
            cx = bx + off + sway
            cy = by - hgt
            ww = Wd * (1 - s) ** 0.75 * (0.6 + 0.4 * math.sin(math.pi * min(1, s * 3 + 0.3)))
            for yy in range(int(cy - 1), int(cy + 2)):
                for xx in range(int(cx - ww - 1), int(cx + ww + 2)):
                    if not (0 <= xx < w and 0 <= yy < h):
                        continue
                    dd = abs(xx + 0.5 - cx) / max(ww, 0.35)
                    if dd > 1 or abs(yy + 0.5 - cy) > 0.8:
                        continue
                    k = 2 + int((1 - dd) * 3.2 + (1 - s) * 1.6)
                    k = min(k, 6)
                    if s > 0.7:
                        k = min(k, 3)
                    layer[yy, xx] = max(layer[yy, xx], k)
    m = layer >= 0
    o = dilate(m, 1) & ~m & (img[..., 3] == 0)
    img[o] = FIRE[1]
    for k in range(7):
        img[layer == k] = FIRE[k]


def herz():
    return [heart_frame(i) for i in range(BEAT_FRAMES)]


# ================================================================ Herz-Tod

DW, DH = 288, 248           # Zelle des Zerfalls
DCX, DCY = 144, 104         # Herzmitte darin (= Pivot, wie beim Herzen)
POOL_DY = 56                # Becken liegt so weit unter der Herzmitte (PCY - HEART_Y)
TOD_HEAT = 9                # Bilder 0-9: ueberhitzen
TOD_FLASH = 10              # Bild 10: weisser Blitz
TOD_BREAK = 18              # Bilder 11-28: Kruste fliegt, Kern verglueht
TOD_COOL = 9                # Bilder 29-37: Reste glimmen aus, Rauch


def paste(dst, src, x0, y0):
    h, w = src.shape[:2]
    a = src[..., 3] > 0
    ys, xs = np.nonzero(a)
    yd, xd = ys + y0, xs + x0
    ok = (yd >= 0) & (yd < dst.shape[0]) & (xd >= 0) & (xd < dst.shape[1])
    dst[yd[ok], xd[ok]] = src[ys[ok], xs[ok]]


def in_pool(x, y):
    """Liegt ein Punkt der Zerfall-Zelle im Lavabecken? (Becken-Ellipse unter dem Herzen)"""
    return ((x - DCX) / (POOL_RX - 4)) ** 2 + ((y - (DCY + POOL_DY)) / (POOL_RY - 3)) ** 2 < 1


def herz_tod():
    """Das Herz zerfaellt: ueberhitzt und zittert, ein Blitz, die Kruste bricht in
    Brocken auseinander, die ins Becken und auf den Boden fallen, der Kern
    verglueht zu nichts. Die letzten Bilder bleiben als Reste liegen."""
    frames = []
    ox, oy = int(DCX - HCX), int(DCY - HCY)
    rng = np.random.RandomState(301)

    # --- 0-9: ueberhitzen, immer schnellere Schlaege, Zittern
    last = None
    for i in range(TOD_HEAT):
        q = i / (TOD_HEAT - 1)
        beat = math.sin(i * 2.4) * (0.03 + 0.05 * q)
        o = dict(sx=1.0 + beat + 0.05 * q, sy=1.0 - beat * 0.6 + 0.04 * q, heat=1.3 + 1.4 * q, t=(i / 6) % 1,
                 extra=1.6 * q, flames=1.8 + 1.6 * q, dx=round(math.sin(i * 3.1) * 2 * q),
                 dy=round(math.cos(i * 2.3) * q), sparks=True)
        img = heart_frame(-1, o)
        cell = np.zeros((DH, DW, 4), np.uint8)
        paste(cell, img, ox, oy)
        frames.append(cell)
        last = o

    # Schollen fuer den Bruch: grobe Zellen des letzten Ueberhitzt-Bildes
    body = last["body"]
    mask = last["mask"]
    u, v = last["u"], last["v"]
    _, _, piece, _ = voronoi(u, v, H_COARSE)
    piece = np.where(mask, piece, -1)

    # --- 10: Blitz
    cell = np.zeros((DH, DW, 4), np.uint8)
    flash_img = body.copy()
    m = flash_img[..., 3] > 0
    big = dilate(m, 2)
    fl = np.zeros_like(flash_img)
    fl[big] = FIRE[5]
    fl[m] = FIRE[6]
    fl[dilate(big, 2) & ~big] = with_alpha(FIRE[4], 170)
    paste(cell, fl, ox, oy)
    frames.append(cell)

    # Brocken vorbereiten: Pixel, Startmitte, Flugbahn, Landestelle
    pieces = []
    for pid in np.unique(piece[piece >= 0]):
        pm = piece == pid
        ys, xs = np.nonzero(pm)
        if len(ys) < 6:
            continue
        cy, cx = ys.mean(), xs.mean()
        dirx, diry = cx - HCX, cy - HCY
        d = math.hypot(dirx, diry) + 1e-6
        speed = 45 + rng.rand() * 55
        vx = dirx / d * speed * (0.8 + rng.rand() * 0.5) + (rng.rand() - 0.5) * 20
        vy = diry / d * speed * 0.5 - (50 + rng.rand() * 60)       # erst hoch
        land = POOL_DY + (rng.rand() - 0.3) * 40                      # Bodenhoehe (unter Herzmitte)
        sub = body[ys, xs].copy()
        edge = neighbours_out(pm)[ys, xs]
        pieces.append(dict(ys=ys - cy, xs=xs - cx, cx=cx + ox, cy=cy + oy, vx=vx, vy=vy,
                           land=DCY + land, sub=sub, edge=edge, landed=None, sunk=False,
                           spin=rng.rand() < 0.5))
    sparks = [(rng.rand() * TAU, 60 + rng.rand() * 110, rng.rand()) for _ in range(46)]

    g = 330.0                    # Schwerkraft px/s^2
    dt = 1.0 / 12

    def cool(sub, edge, k):
        """Brocken kuehlt ab: Glut eine Stufe dunkler je k, Kanten zuerst."""
        out = sub.copy()
        for lvl in range(6, 0, -1):
            col = FIRE[lvl]
            hit = np.all(out == col, axis=-1)
            out[hit] = FIRE[max(0, lvl - k)] if lvl - k > 0 else E[0]
        out[edge] = FIRE[max(1, 3 - k)] if k < 3 else C[0]
        return out

    # --- 11-28: Bruch
    for j in range(TOD_BREAK + TOD_COOL):
        i = TOD_FLASH + 1 + j
        tt = (j + 1) * dt
        q = j / (TOD_BREAK - 1) if j < TOD_BREAK else 1.0
        cell = np.zeros((DH, DW, 4), np.uint8)

        # Bodenring: Druckwelle auf dem Boden ums Becken
        if j < 7:
            rq = j / 6
            rx = 40 + rq * 120
            ry = rx * 0.44
            cyy = DCY + POOL_DY
            yy, xx = np.mgrid[0:DH, 0:DW] + 0.5
            dd = np.hypot((xx - DCX) / rx, (yy - cyy) / ry)
            th = (2.4 - 1.6 * rq) / rx
            ring = (np.abs(dd - 1) < th)
            col = FIRE[5] if rq < 0.25 else FIRE[4] if rq < 0.5 else FIRE[3] if rq < 0.75 else FIRE[2]
            cell[ring] = with_alpha(col, int(230 - 150 * rq))

        # Kern: weissgluehende Kugel, die zittert, schrumpft und vergeht
        cr = 22 * (1 - q) ** 1.4 if j < TOD_BREAK else 0
        if cr > 0.7:
            yy, xx = np.mgrid[0:DH, 0:DW] + 0.5
            jx = math.sin(j * 2.7) * 1.5 * (1 - q)
            d = np.hypot(xx - DCX - jx, (yy - DCY) * 1.05) / cr
            core = d < 1
            k = np.where(d < 0.45, 6, np.where(d < 0.75, 5, 4))
            if q > 0.55:
                k = k - 1
            for kk in range(3, 7):
                cell[core & (k == kk)] = FIRE[kk]
            halo = (d >= 1) & (d < 1.5)
            cell[halo & (cell[..., 3] == 0)] = with_alpha(FIRE[3], 120)

        # Brocken
        for pc in pieces:
            if pc["sunk"]:
                continue
            if pc["landed"] is None:
                x = pc["cx"] + pc["vx"] * tt
                y = pc["cy"] + pc["vy"] * tt + 0.5 * g * tt * tt
                falling = pc["vy"] + g * tt > 0
                if falling and y >= pc["land"]:
                    if in_pool(x, pc["land"]):
                        pc["sunk"] = True
                        pc["splash"] = (x, pc["land"], j)
                        continue
                    pc["landed"] = (x, pc["land"], j)
                    x, y = pc["landed"][0], pc["landed"][1]
            else:
                x, y = pc["landed"][0], pc["landed"][1]
            age = j if pc["landed"] is None else j
            k = 0 if j < 4 else 1 if j < 10 else 2 if j < 18 else 3 if j < 23 else 4
            sub = cool(pc["sub"], pc["edge"], k)
            ys, xs = pc["ys"], pc["xs"]
            if pc["landed"] is not None:
                # liegt: flach gedrueckt, unten an der Landelinie
                ys = ys * 0.6
                ys = ys - ys.max()
            elif pc["spin"]:
                a = tt * 7
                ys, xs = xs * math.sin(a) + ys * math.cos(a), xs * math.cos(a) - ys * math.sin(a)
            yi = np.round(ys + y).astype(int)
            xi = np.round(xs + x).astype(int)
            ok = (yi >= 0) & (yi < DH) & (xi >= 0) & (xi < DW)
            cell[yi[ok], xi[ok]] = sub[ok]
            # Umriss fuer liegende Brocken
        lay = cell[..., 3] == 255
        cell[dilate(lay, 1) & ~lay & (cell[..., 3] == 0)] = C[0]

        # Spritzer, wo Brocken ins Becken fallen
        for pc in pieces:
            if pc.get("splash") is None:
                continue
            sx0, sy0, j0 = pc["splash"]
            a = j - j0
            if 0 <= a < 4:
                for (ddx, ddy) in ((-2, -1 - a), (2, -1 - a), (0, -2 - a * 2), (-1, 0), (1, 0)):
                    put(cell, sx0 + ddx * (1 + a * 0.5), sy0 + ddy, FIRE[5] if a < 2 else FIRE[3])

        # Funkenregen
        if j < 14:
            for (ang, sp, ph) in sparks:
                tq = tt * (0.8 + ph * 0.4)
                x = DCX + math.cos(ang) * sp * tq
                y = DCY + math.sin(ang) * sp * tq * 0.8 + 0.5 * g * 0.6 * tq * tq
                if 0 <= x < DW and 0 <= y < DH:
                    kk = 6 if j < 3 else 5 if j < 6 else 4 if j < 9 else 3 if j < 12 else 2
                    put(cell, x, y, FIRE[kk])
                    if j < 8:
                        put(cell, x - math.cos(ang) * 1.2, y - math.sin(ang) * 1.2, FIRE[max(kk - 2, 1)])

        # Rauch aus dem Becken, wenn alles vorbei ist
        if 10 <= j < TOD_BREAK + TOD_COOL - 1:      # letztes Bild = liegende Reste, ohne Rauch
            for k2 in range(5):
                ph = k2 / 5
                rq = ((j - 10) / 12 + ph) % 1
                x = DCX + (k2 - 2) * 14 + math.sin(rq * 6 + k2) * 3
                y = DCY + POOL_DY - 4 - rq * 70
                r = 2 + rq * 4
                col = V.SMOKE[0] if rq < 0.4 else V.SMOKE[1] if rq < 0.75 else V.SMOKE[2]
                for yy2 in range(int(y - r), int(y + r) + 1):
                    for xx2 in range(int(x - r), int(x + r) + 1):
                        if 0 <= xx2 < DW and 0 <= yy2 < DH and cell[yy2, xx2, 3] == 0 \
                                and math.hypot(xx2 + 0.5 - x, yy2 + 0.5 - y) < r:
                            cell[yy2, xx2] = col
        frames.append(cell)
    return frames


# ================================================================ Einsaugen

def face_maw(img, P, maw, spin, squint):
    """Augen wie im Original, dazu ein riesiger Schlund statt des Grinsens.
    maw 0..1 = wie weit offen, spin = Drehung des Strudels (0..1)."""
    pal = FIRE
    CELL = V.CELL
    XX, YY = V.XX, V.YY
    ex, ey = V.to_screen(P, 0, V.EYE_V)
    ex = math.floor(ex + 0.5)
    ey = math.floor(ey + 0.5) - int(round(2 * maw))
    hot = min(1.0, P["heat"] + 0.3)
    lay = np.zeros((CELL, CELL), np.int8) - 1
    for side in (-1, 1):
        cx = ex + side * (V.EYE_U + round(1.5 * maw)) + 0.5
        cy = ey + 0.5
        s = (XX - cx) * -side
        y = YY - cy
        op = 1 - squint * 0.3
        ell = (s / 4.8) ** 2 + (y / (3.6 * op)) ** 2 < 1
        cut = -3.6 + (s + 4.8) * 0.5 * P["anger"]
        eye = ell & (y > cut) & (y > -3.6 * op)
        hd = np.hypot((s - 0.8) / 4.8, (y - 1.0) / 3.4)
        k = np.clip(np.floor((1.25 - hd) * 4.6 * hot), 1, 6).astype(int)
        k = np.where(eye & (np.abs(s - 0.2) < 0.9) & (np.abs(y + 0.2) < 0.9), 6, k)
        sock = dilate(eye, 1) & ~eye
        lay[sock & (lay < 0)] = 10
        lay[eye] = k[eye]
        bw = (s > -6.5) & (s < 6.0)
        brow = bw & (y <= cut - 1.0) & (y > cut - 3.6) & ~eye
        top = bw & (y <= cut - 2.6) & (y > cut - 3.6)
        lay[brow & (lay < 0)] = 13
        lay[top & brow] = 15

    # Schlund: Ellipse, innen ein Glutring und ein dunkler, drehender Strudel
    mx = ex + 0.5
    my = ey + 10.5 + 2.5 * maw
    rx = 6.0 + 9.5 * maw
    ry = 1.6 + 9.0 * maw
    du = (XX - mx) / rx
    dv = (YY - my) / ry
    dd = np.hypot(du, dv)
    mouth = dd < 1
    ang = np.arctan2(YY - my, XX - mx)
    # Lippe: Glut am Rand, dahinter Kehle
    lip = mouth & (dd > 0.78)
    throat = mouth & ~lip
    rad = dd / 0.78
    swirl = np.sin(ang * 3 - rad * 7.0 + TAU * spin)
    k_throat = np.where(rad < 0.32, 0, np.where(swirl > 0.35, np.where(rad < 0.6, 2, 3), np.where(rad < 0.55, 0, 1)))
    k_throat = np.where((rad > 0.85) & (swirl > -0.2), 4, k_throat)
    k_lip = np.where(dv < 0, 4, 5)
    k_lip = np.where(dd > 0.92, 3, k_lip)
    # Zaehne oben und unten (Kohle), kleiner, wenn weit offen
    ph = np.abs(((XX - mx) % 4.2) - 2.1)
    up = mouth & (dv < 0) & ((dd > 1 - (0.28 - ph * 0.12) * (1.3 - 0.5 * maw)))
    dn = mouth & (dv > 0) & ((dd > 1 - (0.2 - ph * 0.09) * (1.3 - 0.5 * maw))) & (np.abs(du) < 0.8)
    sock = dilate(mouth, 1) & ~mouth
    lay[sock & ((lay < 0) | (lay >= 10))] = 10
    lay[throat] = 20 + k_throat[throat]
    lay[lip] = 20 + k_lip[lip]
    lay[(up | dn) & (maw > 0.15)] = 14
    for kk in range(7):
        img[lay == kk] = pal[kk]
        img[lay == 20 + kk] = pal[kk] if kk > 0 else C[0]
    img[lay == 10] = C[0]
    img[lay == 13] = C[2]
    img[lay == 14] = C[3]
    img[lay == 15] = C[5]
    return (mx, my)


def inhale_streams(img, mouth, t, power, n=22, seed=71):
    """Funken und Asche, die in den Schlund gerissen werden (spiralig)."""
    rng = np.random.RandomState(seed)
    mx, my = mouth
    for k in range(n):
        ph = rng.rand()
        a0 = rng.rand() * TAU
        r0 = 30 + rng.rand() * 30
        q = (t * 2 + ph) % 1
        r = r0 * (1 - q) ** 1.4
        a = a0 + q * 2.4
        x = mx + math.cos(a) * r
        y = my + math.sin(a) * r * 0.8
        if r < 3:
            continue
        kk = 3 if q < 0.4 else 4 if q < 0.75 else 5
        col = FIRE[kk] if k % 3 else V.SMOKE[0]
        if power < 1 and rng.rand() > power:
            continue
        # kurzer Schweif nach aussen
        tx = math.cos(a) * 1.2
        ty = math.sin(a) * 1.0
        put(img, x, y, col)
        if k % 2 == 0:
            put(img, x + tx, y + ty, FIRE[max(kk - 2, 1)] if k % 3 else V.SMOKE[1])


def einsaugen():
    fr = []
    N_IN, N_LOOP, N_GULP = 8, 8, 8
    # --- 0-7 Luft holen
    for i in range(N_IN):
        q = i / (N_IN - 1)
        e = q * q * (3 - 2 * q)
        t = (i / 8) % 1
        P = V.pose(t=t, sx=1 + 0.10 * e, sy=1 + 0.09 * e, dy=round(-1 * e), heat=1 + 0.5 * e,
                   pulse=1 + e, flames=1 - 0.4 * e, smoke=1 - e, arms=(-round(3 * e), -round(3 * e)),
                   crack_boost=0.4 * e, anger=1.2)
        img, *_ = V.body_layers(P)
        canvas = np.zeros_like(img)
        V.smoke(canvas, P, t)
        a = img[..., 3] > 0
        canvas[a] = img[a]
        mouth = face_maw(canvas, P, e, spin=i / 8, squint=e)
        V.flames(canvas, P, FIRE, t)
        if i >= 4:
            inhale_streams(canvas, mouth, t, power=(i - 3) / 4)
        fr.append(canvas)
    # --- 8-15 Sog (Schleife)
    for i in range(N_LOOP):
        t = i / N_LOOP
        wob = math.sin(TAU * t * 2)
        P = V.pose(t=t, sx=1.10 + 0.012 * wob, sy=1.09 - 0.012 * wob, dy=-1, heat=1.5, pulse=2.0,
                   flames=0.6, smoke=0, arms=(-3, -3), crack_boost=0.4 + 0.15 * (i % 2), anger=1.2,
                   shake=(1 if i % 2 else -1, 0))
        img, *_ = V.body_layers(P)
        canvas = np.zeros_like(img)
        a = img[..., 3] > 0
        canvas[a] = img[a]
        mouth = face_maw(canvas, P, 1.0, spin=t, squint=1.0)
        V.flames(canvas, P, FIRE, t)
        inhale_streams(canvas, mouth, t, power=1.0, n=28)
        fr.append(canvas)
    # --- 16-23 Schlucken: Maul zu, Backen dick, Schluck rutscht runter, Grinsen
    gulp = [
        dict(sx=1.16, sy=0.97, maw=0.0, mo=0.0, heat=1.9, dy=0, flash=0.0, arms=(-4, -4), eye=0.2),
        dict(sx=1.13, sy=1.02, maw=0.0, mo=0.0, heat=1.8, dy=-1, flash=0.0, arms=(-3, -3), eye=0.3),
        dict(sx=1.06, sy=1.08, maw=0.0, mo=0.0, heat=1.6, dy=-2, flash=0.0, arms=(-2, -2), eye=0.5),
        dict(sx=1.12, sy=0.93, maw=0.0, mo=0.1, heat=2.0, dy=0, flash=0.0, arms=(1, 1), eye=0.5),
        dict(sx=1.05, sy=0.98, maw=0.0, mo=0.25, heat=1.6, dy=0, flash=0.0, arms=(0, 0), eye=0.8),
        dict(sx=1.02, sy=1.0, maw=0.0, mo=0.55, heat=1.35, dy=0, flash=0.0, arms=(-1, -1), eye=1.0),
        dict(sx=1.0, sy=1.01, maw=0.0, mo=0.6, heat=1.2, dy=0, flash=0.0, arms=(-1, -1), eye=1.0),
        dict(sx=1.0, sy=1.0, maw=0.0, mo=0.5, heat=1.1, dy=0, flash=0.0, arms=(0, 0), eye=1.0),
    ]
    for i, g in enumerate(gulp):
        t = (i / 8) % 1
        P = V.pose(t=t, sx=g["sx"], sy=g["sy"], dy=g["dy"], heat=g["heat"], pulse=1.6, flames=1.0 + 0.4 * (i < 4),
                   smoke=min(1.0, i / 4), arms=g["arms"], eye_open=g["eye"], mouth_open=g["mo"],
                   anger=0.7 if i >= 5 else 1.2, crack_boost=0.6 if i == 3 else 0.2)
        canvas = V.bild(P)
        # Der Schluck: ein Gluehball unter der Kruste rutscht von der Kehle in den Bauch
        if 1 <= i <= 4:
            q = (i - 1) / 3
            bu, bv = 0, 9 + q * 12
            gx, gy = V.to_screen(P, bu, bv)
            rr = 5.5 - q * 1.5
            for yy in range(int(gy - rr - 1), int(gy + rr + 2)):
                for xx in range(int(gx - rr - 1), int(gx + rr + 2)):
                    if 0 <= xx < V.CELL and 0 <= yy < V.CELL and canvas[yy, xx, 3] == 255:
                        d = math.hypot(xx + 0.5 - gx, (yy + 0.5 - gy) * 1.2) / rr
                        if d < 1:
                            col = canvas[yy, xx]
                            if np.array_equal(col, C[0]):
                                continue
                            canvas[yy, xx] = FIRE[4] if d < 0.45 else (FIRE[3] if d < 0.75 else E[2])
        if i == 3:
            V.shockwave(canvas, 0.25, FIRE)
        fr.append(canvas)
    return fr


def mund_offset():
    """Wo der Schlund im Sog sitzt (Pixel ueber dem Pivot, seitlich) - fuer den C#-Code."""
    P = V.pose(t=0, sx=1.10, sy=1.09, dy=-1, heat=1.5)
    canvas = np.zeros((V.CELL, V.CELL, 4), np.uint8)
    mx, my = face_maw(canvas, P, 1.0, 0, 1.0)
    return mx - V.CX, (V.CELL - 4) - my


# ===================================================================== Sog

SW = 160


def sog():
    """Windschlieren, die spiralig in die Mitte laufen. 8 Bilder Schleife, Pivot Mitte."""
    fr = []
    rng = np.random.RandomState(81)
    streaks = []
    for k in range(26):
        streaks.append((rng.rand() * TAU, rng.rand(), 0.55 + rng.rand() * 0.6, rng.rand()))
    ash = [rgb("#d8cfd2", 170), rgb("#a99ea3", 140), rgb("#7a6e74", 110)]
    for i in range(8):
        t = i / 8
        img = np.zeros((SW, SW, 4), np.uint8)
        c = SW / 2
        for (a0, ph, lenf, kind) in streaks:
            q = (t + ph) % 1
            # Radius faellt beschleunigt nach innen, Winkel dreht dabei mit
            for s in np.linspace(0, 1, 60):
                qq = q - s * 0.16 * lenf
                if qq < 0:
                    continue
                r = 76 * (1 - qq) ** 1.25
                if r < 13:
                    continue
                a = a0 + qq * 2.2
                x = c + math.cos(a) * r
                y = c + math.sin(a) * r * 0.78
                fade = 1 - s
                if kind < 0.68:
                    k = 0 if fade > 0.6 else 1 if fade > 0.3 else 2
                    col = ash[k]
                    if r < 30:
                        col = with_alpha(FIRE[3] if fade > 0.5 else FIRE[2], col[3])
                else:
                    k = 4 if fade > 0.6 else 3 if fade > 0.3 else 2
                    col = with_alpha(FIRE[k], 200)
                xi, yi = int(math.floor(x)), int(math.floor(y))
                if 0 <= xi < SW and 0 <= yi < SW and img[yi, xi, 3] < col[3]:
                    img[yi, xi] = col
        fr.append(img)
    return fr


# ================================================================== Kammer
#
# Ein Krater im Inneren des Verkohlten: ein ovaler Boden aus verkohlten
# Schollen, hinten eine hohe Wand aus Keksteig (mit Luftblasen), die nach
# vorne hin flacher wird, vorne nur ein Wulst. Drumherum die dunkle Kuppel,
# unten der Abgrund mit fernem Glutschein. In der Mitte das Lavabecken, ueber
# dem das Herz schwebt; neun Glutadern laufen von dort strahlenfoermig bis die
# Wand hinauf, und jeder Herzschlag rollt als Welle durch sie hindurch.

KW, KH = 1152, 704
KCX, KCY = 576, 368         # Bildmitte = Herzmitte (Bezugspunkt im Code)
HEART_X, HEART_Y = KCX, KCY
FCX, FCY = 576.0, 384.0     # Mitte der Bodenellipse
FRX, FRY = 472.0, 246.0     # Boden-Ellipse (29.5 x 15.4 Einheiten)
PCX, PCY = 576.0, 424.0     # Mitte des Lavabeckens (unter dem Herzen)
WALL_H = 54                 # Hoehe der Rueckwand (hinten), zu den Seiten halb so hoch
LIP = 8                     # Aussenseite des vorderen Wulsts
POOL_RX, POOL_RY = 64.0, 28.0
N_VEINS = 13

VOIDC = rgb("#0a0608")
DOME = [rgb("#0a0608"), rgb("#0f090b"), rgb("#150d0e"), rgb("#1d1111"), rgb("#281513"), rgb("#341b15")]
ABYSS = [rgb("#0a0608"), rgb("#12080a"), rgb("#1c0b0b"), rgb("#2a0e0b"), rgb("#3d120b")]


def gvoronoi(x, y, spacing, seed, jitter=0.9):
    """Voronoi auf einem verwackelten Raster: je Pixel nur die 3x3 Nachbarzellen.
    Gibt Abstand zur naechsten/zweitnaechsten Saat (Pixel) und deren Zell-Ids."""
    gx = x / spacing
    gy = y / spacing
    ix = np.floor(gx).astype(np.int64)
    iy = np.floor(gy).astype(np.int64)
    b1 = np.full(x.shape, 1e9)
    b2 = np.full(x.shape, 1e9)
    id1 = np.zeros(x.shape, np.int64)
    id2 = np.zeros(x.shape, np.int64)
    for oy in (-1, 0, 1):
        for ox in (-1, 0, 1):
            cx = ix + ox
            cy = iy + oy
            px = cx + 0.5 + (hash2(cx, cy, seed) - 0.5) * jitter
            py = cy + 0.5 + (hash2(cx, cy, seed + 1) - 0.5) * jitter
            d = np.hypot(gx - px, gy - py) * spacing
            cid = cx * 100003 + cy
            closer = d < b1
            second = ~closer & (d < b2)
            b2 = np.where(closer, b1, np.where(second, d, b2))
            id2 = np.where(closer, id1, np.where(second, cid, id2))
            b1 = np.where(closer, d, b1)
            id1 = np.where(closer, cid, id1)
    return b1, b2, id1, id2


def drop_specks(mask, min_size):
    """Entfernt zusammenhaengende Stuecke unter min_size Pixeln (4er-Nachbarschaft)."""
    h, w = mask.shape
    seen = np.zeros_like(mask)
    out = mask.copy()
    ys, xs = np.nonzero(mask)
    for y0, x0 in zip(ys, xs):
        if seen[y0, x0]:
            continue
        stack = [(y0, x0)]
        seen[y0, x0] = True
        part = []
        while stack:
            y, x = stack.pop()
            part.append((y, x))
            for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                ny, nx = y + dy, x + dx
                if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    stack.append((ny, nx))
        if len(part) < min_size:
            for y, x in part:
                out[y, x] = False
    return out


def cell_hash(cid, seed):
    return hash2(cid, cid // 7 + 3, seed)


def pair_hash(a, b, seed):
    lo = np.minimum(a, b)
    hi = np.maximum(a, b)
    return hash2(lo, hi, seed)


def rim_heights(du):
    """Wandhoehe hinten (oben im Bild) und Wulsthoehe vorne je Spalte."""
    s = np.sqrt(np.clip(1 - du * du, 0, 1))
    back = WALL_H * (1 + s) / 2
    front = 3 + (WALL_H * 0.55) * (1 - s) / 2
    return back, front, s


class KammerStatik:
    """Alles, was sich zwischen den Bildern nicht aendert - einmal berechnet."""

    def __init__(self):
        yy, xx = np.mgrid[0:KH, 0:KW].astype(np.float64)
        xx += 0.5
        yy += 0.5
        self.xx, self.yy = xx, yy
        du = (xx - FCX) / FRX
        dv = (yy - FCY) / FRY
        self.du = du
        rf = np.hypot(du, dv)
        self.rf = rf
        self.floor = rf < 1.0
        back, front, s = rim_heights(np.clip(du, -1, 1))
        y_back = FCY - FRY * s            # Boden-Oberkante je Spalte
        y_front = FCY + FRY * s           # Boden-Unterkante je Spalte
        inside_x = np.abs(du) <= 1.0
        self.y_back, self.y_front = y_back, y_front
        # Rueckwand: von der Bodenkante bis "back" Pixel darueber
        self.wall = inside_x & (yy < y_back) & (yy >= y_back - back) & ~self.floor & (yy < FCY + 2)
        self.wall_t = np.clip((y_back - yy) / np.maximum(back, 1), 0, 1)   # 0 = Fuss, 1 = Oberkante
        # Wulst vorne: deckt den Boden um "front" Pixel ab, dazu die Aussenseite
        self.lip_top = inside_x & (yy >= y_front - front) & (yy < y_front) & (yy > FCY - 2)
        self.lip_face = inside_x & (yy >= y_front) & (yy < y_front + LIP) & (yy > FCY - 2)
        # seitlich: der Wulst laeuft in die Wand ueber (ein paar Pixel neben der Ellipse)
        side = (np.abs(du) > 1.0) & (np.abs(du) < 1.0 + 7 / FRX) & (np.abs(yy - FCY) < WALL_H * 0.5)
        self.lip_side = side
        self.lip_side &= False
        self.lip = self.lip_top | self.lip_face

        # ---------------------------------------------------------- Kuppel
        below_lip = inside_x & (yy >= y_front + LIP)
        self.dome = ~self.floor & ~self.wall & ~self.lip & ~below_lip & ~(inside_x & (yy > FCY))
        self.abyss = ~self.floor & ~self.wall & ~self.lip & ~self.dome
        # Gewoelbe: Baender um den Krater, nah an der Wandkante vom Glutschein
        # angestrahlt, nach aussen schwarz. Dazwischen Rippen (dunkle Fugen).
        de = np.hypot((xx - FCX) / (FRX + 8.0), (yy - (FCY - WALL_H)) / (FRY + 8.0))
        de = de + (fbm(xx * 0.03, yy * 0.04, 111) - 0.5) * 0.18
        self.dome_de = de
        holes = fbm(xx * 0.07, yy * 0.1, 112)
        lvl = 4.6 - (de - 1.0) * 6.5 + (fbm(xx * 0.06, yy * 0.08, 117) - 0.5) * 1.1
        lvl -= np.clip(yy - (FCY - 10), 0, None) * 0.05
        rib = np.abs(((de * 9.0 + (fbm(xx * 0.04, yy * 0.04, 118) - 0.5) * 0.8) % 1.0) - 0.5) < 0.07
        self.dome_hole = self.dome & (holes > 0.7) & (de > 1.08)
        lvl = np.where(self.dome_hole, lvl - 1.4, lvl)
        lvl = np.where(rib & self.dome, lvl - 0.9, lvl)
        self.dome_k = np.clip(np.floor(lvl), 0, 5).astype(int)
        self.dome_hole_rim = self.dome & dilate(self.dome_hole, 1) & ~self.dome_hole & (self.dome_k >= 2)             & np.roll(self.dome_hole, 1, 0)
        e1, e2, j1, j2 = gvoronoi(xx + (fbm(xx * 0.02, yy * 0.02, 113) - 0.5) * 50,
                                  yy + (fbm(xx * 0.02 + 5, yy * 0.02, 114) - 0.5) * 50, 46.0, 115)
        self.far_crack = self.dome & ((e2 - e1) < 1.1) & (self.dome_k >= 2) & (pair_hash(j1, j2, 116) < 0.32)

        # ----------------------------------------------------------- Wand
        wn = fbm(xx * 0.05, yy * 0.12, 121)
        strata = np.floor(yy * 0.28 + (fbm(xx * 0.03, yy * 0.03, 122) - 0.5) * 7) % 3
        wl = 2.1 - self.wall_t * 1.0 + (wn - 0.5) * 1.3 + strata * 0.35 - np.abs(du) ** 2 * 1.3
        self.wall_k = np.clip(np.floor(wl * 1.7), 0, 5).astype(int)
        wh = fbm(xx * 0.14, yy * 0.2, 123)
        self.wall_hole = self.wall & (wh > 0.71) & (self.wall_t > 0.15) & (self.wall_t < 0.85)
        self.wall_hole_lit = self.wall & dilate(self.wall_hole, 1) & ~self.wall_hole & np.roll(self.wall_hole, 1, 0)
        self.wall_top = self.wall & ~np.roll(self.wall, 1, 0)
        self.wall_top2 = self.wall & ~np.roll(self.wall, 2, 0) & ~self.wall_top
        self.wall_outline = dilate(self.wall, 1) & ~self.wall & ~self.floor & (yy < FCY)

        # ----------------------------------------------------------- Boden
        fu = xx - FCX
        fv = (yy - FCY) * 1.55
        d1, d2, i1, _ = gvoronoi(fu, fv, 15.0, 131)
        self.cellv = cell_hash(i1, 132)
        self.seam = self.floor & ((d2 - d1) < 1.3)
        # Schollenkante oben links heller (Facetten wie auf seinem Koerper)
        self.facet = self.floor & ~self.seam & ((d2 - d1) < 2.6) & (np.roll(self.seam, 1, 0) | np.roll(self.seam, 1, 1))
        self.rp = np.hypot((xx - PCX) / FRX, (yy - PCY) / FRY)
        self.light = np.clip(1.12 - self.rp * 0.95, 0, 1)
        self.fnoise = fbm(xx * 0.06, yy * 0.1, 133)
        fh = fbm(xx * 0.11, yy * 0.18, 134)
        self.fhole = self.floor & (fh > 0.755) & (self.rp > 0.3) & (rf < 0.9)
        self.fhole_lit = np.roll(self.fhole, 1, 0) & ~self.fhole & self.floor
        self.spk = self.floor & ((np.floor(xx) * 7 + np.floor(yy) * 13 + np.floor(xx / 5) * np.floor(yy / 3)) % 53 == 0)
        self.floor_edge = self.floor & neighbours_out(self.floor)
        self.edge_shadow = self.floor & (rf > 0.92) & (yy < FCY)

        # ----------------------------------------------------------- Adern
        self.veins, self.vr = self._veins()
        self.veins &= self.floor & ~self.fhole
        self.veins = drop_specks(self.veins, 6)
        self.vedge = dilate(self.veins, 1) & self.floor & ~self.veins & ~self.fhole
        self.wall_veins = self._wall_veins()
        self.wall_vedge = dilate(self.wall_veins, 1) & self.wall & ~self.wall_veins

        # ----------------------------------------------------------- Becken
        pd = np.hypot((xx - PCX) / POOL_RX, (yy - PCY) / POOL_RY)
        self.pd = pd
        self.pool = pd < 1.0
        rim = (pd < 1.0 + 7.0 / POOL_RX) & ~self.pool
        self.prim = rim
        self.prim_out = neighbours_out(rim | self.pool) & (rim | self.pool)
        self.pa = np.arctan2((yy - PCY) / POOL_RY, (xx - PCX) / POOL_RX)
        self.pshadow = np.hypot((xx - PCX) / 28.0, (yy - PCY + 1) / 8.5) < 1.0
        self.pshadow2 = np.hypot((xx - PCX) / 39.0, (yy - PCY + 1) / 12.5) < 1.0
        self.crust_isl = self.pool & (fbm(xx * 0.2, yy * 0.45, 151) > 0.68) & (pd > 0.5) & ~self.pshadow2

        # ------------------------------------------------- Brocken + Chips
        self.rocks = self._rocks()
        rng = np.random.RandomState(161)
        self.chips = []
        for k in range(130):
            a = rng.rand() * TAU
            r = 0.3 + rng.rand() * 0.95
            cx = PCX + math.cos(a) * r * FRX
            cy = PCY + math.sin(a) * r * FRY
            if not (0 <= int(cy) < KH and 0 <= int(cx) < KW) or self.rf[int(cy), int(cx)] > 0.9:
                continue
            cr = 1.5 + rng.rand() * 1.9
            m = np.hypot(xx - cx, (yy - cy) * 1.5) < cr
            m &= self.floor & ~self.veins & ~self.pool & ~self.fhole
            if m.sum() < 2:
                continue
            core = m & (np.hypot(xx - cx + 0.4, (yy - cy + 0.4) * 1.5) < cr * 0.45)
            ring = dilate(m, 1) & self.floor & ~m & ~self.veins & ~self.pool
            self.chips.append((r, m, core, ring))

        self.floor &= ~self.lip_top

    def _veins(self):
        xx, yy = self.xx, self.yy
        du = (xx - PCX) / FRX
        dv = (yy - PCY) / FRY
        r = np.hypot(du, dv)
        a = np.arctan2(dv, du)
        # Schlaengeln in Pixeln (nicht im Winkel) - sonst zerfasern die Adern weit draussen
        rad_px = np.hypot(xx - PCX, (yy - PCY) * 1.6) + 1.0
        warp_px = (fbm(xx * 0.012, yy * 0.018, 101) - 0.5) * 70 + (fbm(xx * 0.06, yy * 0.08, 102) - 0.5) * 9
        aw = a + warp_px / rad_px * np.clip(r / 0.25, 0, 1)
        seg = TAU / N_VEINS
        offs = np.array([hash2(k, 1, 103) for k in range(N_VEINS)])
        k = np.floor((aw + math.pi) / seg).astype(int) % N_VEINS
        centre = -math.pi + (k + 0.5) * seg + (offs[k] - 0.5) * seg * 0.45
        dang = np.abs(((aw - centre + math.pi) % TAU) - math.pi)
        dist_px = dang * np.hypot(xx - PCX, (yy - PCY) * 1.6)
        wid = 3.8 - 1.8 * np.clip(r / 1.2, 0, 1.0)
        main = (dist_px < wid / 2 + 0.3) & (r > 0.18)
        # Seitenaeste: zweite Schar, versetzt, nur im aeusseren Ring
        k2 = np.floor((aw + math.pi) / seg + 0.5).astype(int) % N_VEINS
        offs2 = np.array([hash2(j, 2, 104) for j in range(N_VEINS)])
        start2 = np.array([0.45 + hash2(j, 3, 105) * 0.25 for j in range(N_VEINS)])
        c2 = -math.pi + k2 * seg + (offs2[k2] - 0.5) * seg * 0.3
        d2 = np.abs(((aw - c2 + math.pi) % TAU) - math.pi) * np.hypot(xx - PCX, (yy - PCY) * 1.6)
        branch = (d2 < 0.85) & (r > start2[k2]) & (r < 1.0)
        return main | branch, r

    def _wall_veins(self):
        """Wo eine Bodenader an die Rueckwand stoesst, kriecht sie die Wand hoch."""
        m = np.zeros((KH, KW), bool)
        hits = self.veins & (self.rf > 0.95) & (self.yy < FCY - 6)
        ys, xs = np.nonzero(hits)
        done = set()
        for y0, x0 in zip(ys, xs):
            key = x0 // 3
            if key in done:
                continue
            done.add(key)
            reach = 0.55 + 0.45 * hash2(int(x0), 7, 141)
            x = float(x0)
            y = int(y0)
            while y > 0:
                y -= 1
                if not self.wall[y, int(round(x))] and y < y0 - 2:
                    break
                if self.wall_t[y, int(round(x))] > reach:
                    break
                x += (hash2(int(x0), y, 142) - 0.5) * 1.1
                xi = int(round(x))
                if 0 <= xi < KW and self.wall[y, xi]:
                    m[y, xi] = True
                    # Seitentrieb
                    if hash2(int(x0), y, 143) < 0.06:
                        dx = 1 if hash2(int(x0), y, 144) < 0.5 else -1
                        for s in range(1, 5):
                            xs2 = xi + dx * s
                            ys2 = y - s // 2
                            if 0 <= xs2 < KW and self.wall[ys2, xs2]:
                                m[ys2, xs2] = True
        return m

    def _rocks(self):
        """Kohlebrocken am Rand des Bodens."""
        xx, yy = self.xx, self.yy
        rng = np.random.RandomState(171)
        lay = np.zeros((KH, KW), np.int8)
        for k in range(64):
            a = rng.rand() * TAU
            if math.sin(a) > 0.55:          # vorne liegen keine (wuerden verdeckt)
                continue
            r = 0.82 + rng.rand() * 0.12
            cx = FCX + math.cos(a) * r * FRX
            cy = FCY + math.sin(a) * r * FRY
            w = 2.5 + rng.rand() * 3.5
            h = w * 0.7
            m = (np.hypot((xx - cx) / w, (yy - cy) / h) < 1) & self.floor & ~self.veins
            if not m.any():
                continue
            top = m & (yy < cy - h * 0.2)
            lay[m] = 3
            lay[top] = 4
            lay[m & (xx < cx - w * 0.3) & (yy < cy)] = 5
            sh = (np.hypot((xx - cx - 1.5) / (w + 1), (yy - cy - 1.5) / (h * 0.8)) < 1) & self.floor & ~m
            lay[sh & (lay == 0)] = 1
            o = neighbours_out(m)
            lay[o] = 9
        return lay


_STATIK = None


def statik():
    global _STATIK
    if _STATIK is None:
        _STATIK = KammerStatik()
    return _STATIK


def beat_wave(t, r, speed=2.6, width=0.13, delay=0.0):
    """Zwei Wellen (BUM, bumm), die vom Becken nach aussen laufen. r = Ellipsenradius."""
    out = 0.0
    for t0, amp in ((2 / BEAT_FRAMES, 1.0), (5 / BEAT_FRAMES, 0.6)):
        front = ((t - t0 - delay) % 1.0) * speed
        out = out + amp * np.exp(-((r - front) / width) ** 2)
    return out


def kammer_frame(i):
    S = statik()
    t = i / BEAT_FRAMES
    _, _, heat = beat_curve(i)
    xx, yy = S.xx, S.yy
    img = np.zeros((KH, KW, 4), np.uint8)
    img[:] = VOIDC

    # ------------------------------------------------------------- Kuppel
    for k in range(6):
        img[S.dome & (S.dome_k == k)] = DOME[k]
    img[S.dome_hole_rim] = DOME[5]
    img[S.far_crack & (S.dome_k <= 2)] = FIRE[0]
    img[S.far_crack & (S.dome_k > 2)] = E[0]

    # Stalaktiten von oben (Silhouetten, Glutspitze)
    for (sx, sw, sh, seed) in STALACTITES:
        for y in range(0, int(sh) + 1):
            q = y / sh
            half = sw * (1 - q) ** 1.25 * (0.88 + 0.12 * math.sin(y * 0.8 + seed))
            for x in range(int(sx - half - 1), int(sx + half + 2)):
                if 0 <= x < KW and abs(x + 0.5 - sx) <= half:
                    edge = abs(x + 0.5 - sx) > half - 1.1
                    col = C[0] if edge else (C[2] if x < sx - half * 0.3 else C[1])
                    if not edge and x < sx - half * 0.3 and (y % 7 == seed % 7):
                        col = C[3]
                    if q > 0.84 and not edge:
                        col = FIRE[2] if q > 0.92 else E[1]
                    img[y, x] = col

    # ------------------------------------------------------------- Abgrund
    # dunkel, nach unten ein ferner Glutschein (Lava tief unten)
    ab = np.clip((yy - FCY - FRY) / (KH - FCY - FRY), 0, 1) * 3.2 + (fbm(xx * 0.03, yy * 0.05, 181) - 0.5) * 1.2 \
        - np.abs((xx - KCX) / 620.0) * 1.2
    ab = ab * np.clip(1.25 - np.abs(S.du), 0, 1)
    ak = np.clip(np.floor(ab), 0, 4).astype(int)
    for k in range(5):
        img[S.abyss & (ak == k)] = ABYSS[k]

    draw_spikes(img, S, heat)

    # --------------------------------------------------------------- Wand
    wall_cols = [C[0], C[1], C[2], C[3], C[4], C[5]]
    for k in range(6):
        img[S.wall & (S.wall_k == k)] = wall_cols[k]
    # Glutschein vom Becken auf dem Wandfuss (beim Schlag staerker)
    warm = S.wall & (S.wall_t < 0.35 + 0.1 * (heat - 1)) & (np.abs(S.du) < 0.55) & (S.wall_k >= 2)
    img[warm] = E[0]
    img[warm & (S.wall_t < 0.15) & (heat > 1.3)] = E[1]
    img[S.wall_hole] = C[0]
    img[S.wall_hole_lit] = C[1]
    img[S.wall_top] = C[5]
    img[S.wall_top & (np.abs(S.du) < 0.5)] = C[6]
    img[S.wall_top2] = C[4]
    img[S.wall_outline] = C[0]
    # Adern die Wand hinauf: Welle kommt kurz nach dem Boden an
    wr = 1.0 + S.wall_t * 0.45
    wv = 0.55 + 0.35 * (1 - S.wall_t) + 0.7 * beat_wave(t, wr)
    wk = np.clip(np.floor(wv * 2.3), 1, 6).astype(int)
    for k in range(1, 7):
        img[S.wall_veins & (wk == k)] = FIRE[k]
    img[S.wall_vedge & (wv > 1.3)] = E[1]
    img[S.wall_vedge & (wv <= 1.3) & (S.wall_k >= 2)] = E[0]

    # --------------------------------------------------------------- Boden
    lightb = S.light + (heat - 1.0) * 0.08 * np.clip(1 - S.rp * 1.2, 0, 1)
    ftone = lightb * 2.6 + (S.cellv - 0.5) * 0.85 + (S.fnoise - 0.5) * 0.7 - 0.15
    fk = np.clip(np.floor(ftone * 1.55) + 1, 1, 5).astype(int)
    fk = np.where(S.seam, np.maximum(fk - 2, 1), fk)
    floor_cols = [C[0], C[1], C[2], C[3], C[4], C[5]]
    for k in range(1, 6):
        img[S.floor & (fk == k)] = floor_cols[k]
    img[S.facet & (fk >= 3)] = C[5]
    # Glut faerbt den Boden um das Becken rot
    wl = np.clip((0.42 - S.rp) / 0.42, 0, 1) * (1.0 + (heat - 1.0) * 0.25) * 1.6
    warmf = S.floor & ~S.seam & (S.rp < 0.42)
    img[warmf & (wl > 0.8) & (fk == 3)] = E[1]
    img[warmf & (wl > 0.5) & (fk >= 4)] = E[1]
    img[warmf & (wl > 1.05) & (fk >= 4)] = E[2]
    img[warmf & (wl > 1.5) & (fk >= 4)] = E[2]
    img[S.fhole] = C[0]
    img[S.fhole_lit] = C[4]
    img[S.spk & (fk >= 2)] = rgb("#5a4e55")
    img[S.edge_shadow & (fk >= 2)] = C[1]
    img[S.floor_edge & (yy < FCY)] = C[0]

    # Brocken
    rk = S.rocks
    img[S.floor & (rk == 1)] = C[1]
    img[rk == 3] = C[3]
    img[rk == 4] = C[4]
    img[rk == 5] = C[5]
    img[rk == 9] = C[0]

    # Glutadern mit Puls
    vh = 0.75 + 0.6 * (1 - np.clip(S.vr, 0, 1)) + 0.75 * beat_wave(t, S.vr)
    vk = np.clip(np.floor(vh * 2.2), 1, 6).astype(int)
    for k in range(1, 7):
        img[S.veins & (vk == k)] = FIRE[k]
    img[S.vedge & (vh > 1.45)] = E[1]
    img[S.vedge & (vh <= 1.45)] = E[0]

    # Schokostuecke, glimmen auf, wenn die Welle vorbeikommt
    for (r, m, core, ring) in S.chips:
        pv = float(beat_wave(t, np.array(r)))
        kk = 2 + int(round(0.6 + 1.0 * min(pv, 1.0)))
        img[ring] = C[0]
        img[m] = FIRE[min(kk, 5)]
        img[core] = FIRE[min(kk + 1, 6)]

    # Bodenwelle: nur beim BUM ein feiner, dunkelroter Ring, der schnell verglimmt
    q = (t - 2 / BEAT_FRAMES) % 1.0
    if q < 0.3:
        ringr = 0.22 + q * 2.0
        ring = S.floor & (np.abs(S.rp - ringr) < 0.005) & ~S.pool & ~S.prim & ~S.veins
        img[ring] = E[2] if q < 0.12 else E[1]

    # ------------------------------------------------------------- Becken
    img[S.prim] = C[3]
    img[S.prim & (yy < PCY - 2)] = C[4]
    img[S.prim & (yy < PCY - POOL_RY * 0.75)] = C[5]
    inner_lit = S.prim & (S.pd < 1.0 + 2.5 / POOL_RX) & (yy > PCY)
    img[inner_lit] = E[2] if heat > 1.3 else E[1]
    flow = np.sin(S.pa * 3 + S.pd * 9 - TAU * t) + 0.6 * np.sin(S.pa * 5 - S.pd * 6 + TAU * t * 2 + 1.3)
    lv = 2.0 + 0.45 * flow + (heat - 1.0) * 1.4 - S.pd * 0.8
    lv = np.where(S.pshadow2, lv - 0.7, lv)
    lv = np.where(S.pshadow, lv - 1.2, lv)
    lk = np.clip(np.floor(lv * 1.5), 1, 6).astype(int)
    for k in range(1, 7):
        img[S.pool & (lk == k)] = FIRE[k]
    img[S.crust_isl] = FIRE[0]
    img[S.crust_isl & np.roll(~S.crust_isl, 1, 0)] = E[1]
    img[S.prim_out] = C[0]
    # Blasen, die im Becken aufsteigen und platzen
    rng = np.random.RandomState(191)
    for k in range(7):
        a = rng.rand() * TAU
        r = 0.25 + rng.rand() * 0.6
        ph = rng.rand()
        q = (t * 2 + ph) % 1.0
        bx = PCX + math.cos(a) * r * POOL_RX
        by = PCY + math.sin(a) * r * POOL_RY
        if S.pshadow[int(by), int(bx)]:
            continue
        if q < 0.5:
            put(img, bx, by, FIRE[6] if q > 0.35 else FIRE[5])
        elif q < 0.62:
            for (ox, oy) in ((-1, 0), (1, 0), (0, -1)):
                put(img, bx + ox, by + oy, FIRE[5])

    # ----------------------------------------------------------- Wulst vorne
    # (eigene Ebene, liegt VOR dem Spieler - hier nur, damit das Gesamtbild
    # in der Vorschau stimmt; herzkammer_hinten bekommt ihn nicht)
    return img


def _spikes():
    """Krustenzacken entlang der Wandkrone: (du, Hoehe, halbe Breite, Neigung zur Mitte)."""
    rng = np.random.RandomState(211)
    out = []
    n = 23
    for k in range(n):
        du = -0.93 + 1.86 * k / (n - 1) + (rng.rand() - 0.5) * 0.05
        big = k % 2 == 1
        h = (44 + rng.rand() * 22) if big else (24 + rng.rand() * 14)
        w = (12 + rng.rand() * 4) if big else (8 + rng.rand() * 3)
        out.append((du, h, w, -du * 0.32))
    return out


def _stalactites():
    rng = np.random.RandomState(212)
    out = []
    x = 10.0
    seed = 1
    while x < KW - 6:
        big = rng.rand() < 0.4
        out.append((x, 12 + rng.rand() * 6 if big else 6 + rng.rand() * 5,
                    44 + rng.rand() * 22 if big else 18 + rng.rand() * 18, seed))
        x += 38 + rng.rand() * 44
        seed += 1
    return out


SPIKES = _spikes()
STALACTITES = _stalactites()


def draw_spikes(img, S, heat):
    """Riesige Krustenzacken auf der Wandkrone - wie Zaehne von innen. Glut leckt an den Kanten
    zur Kammer hin. (du, Hoehe, halbe Breite am Fuss, Neigung zur Mitte)"""
    for (du0, h, w, lean) in SPIKES:
        bx = FCX + du0 * FRX
        s0 = math.sqrt(max(0.0, 1 - du0 * du0))
        base_y = FCY - FRY * s0 - WALL_H * (1 + s0) / 2 + 3
        for y in range(int(base_y - h - 1), int(base_y) + 1):
            q = (base_y - y) / h               # 0 Fuss, 1 Spitze
            if q < 0 or q > 1:
                continue
            cx = bx + lean * (q ** 1.6) * h * 1.4 + math.sin(q * 5 + du0 * 9) * 0.8
            half = w * (1 - q) ** 1.15 + 0.4
            for x in range(int(cx - half - 1), int(cx + half + 2)):
                if not (0 <= x < KW and 0 <= y < KH):
                    continue
                dx = x + 0.5 - cx
                if abs(dx) > half:
                    continue
                if S.wall[y, x] or S.floor[y, x]:
                    continue
                edge = abs(dx) > half - 1.0
                # Licht kommt von unten aus der Kammer: die Seite zur Mitte glimmt
                side = dx * (-1 if du0 > 0 else 1)          # positiv = zur Kammermitte
                if edge:
                    col = C[0]
                elif side > half * 0.45 and q < 0.75:
                    col = E[1] if heat > 1.3 else E[0]
                elif side > 0:
                    col = C[3]
                else:
                    col = C[2] if q < 0.6 else C[1]
                if not edge and q < 0.18:
                    col = C[1]
                img[y, x] = col
        # Glutader in der Zacke
        for k in range(int(h * 0.55)):
            q = k / h
            x = int(round(bx + lean * (q ** 1.6) * h * 1.4 + (0.6 if du0 < 0 else -0.6)))
            y = int(round(base_y - k))
            if 0 <= x < KW and 0 <= y < KH and not S.wall[y, x]:
                img[y, x] = FIRE[2] if q < 0.3 else FIRE[1]


def kammer_vorne():
    """Der vordere Wulst und die seitlichen Uebergaenge - liegt VOR dem Spieler."""
    S = statik()
    xx, yy = S.xx, S.yy
    img = np.zeros((KH, KW, 4), np.uint8)
    n = fbm(xx * 0.08, yy * 0.15, 201)
    # Oberseite: von vorne beleuchtet (das Herz steht dahinter, also eher dunkel, Kante oben hell)
    top_k = np.clip(np.floor(2.4 + (n - 0.5) * 1.6 - np.abs(S.du) * 0.8), 1, 4).astype(int)
    for k in range(1, 5):
        img[S.lip_top & (top_k == k)] = C[k + 1]
    edge = S.lip_top & ~np.roll(S.lip_top, 1, 0)
    img[edge] = C[5]
    img[edge & (np.abs(S.du) < 0.45)] = C[6]
    # Aussenseite: dunkel, unten Glutschein aus dem Abgrund
    fq = np.clip((yy - S.y_front) / LIP, 0, 1)
    face_k = np.where(fq > 0.7, 1, np.where(fq > 0.35, 2, 3))
    img[S.lip_face & (face_k == 3)] = C[2]
    img[S.lip_face & (face_k == 2)] = C[1]
    img[S.lip_face & (face_k == 1)] = ABYSS[3]
    # Glutrisse in der Aussenseite
    cr = S.lip_face & (np.abs(((xx * 0.07 + fbm(xx * 0.05, yy * 0.2, 202) * 6) % 13) - 6.5) < 0.35) & (fq < 0.8)
    img[cr] = FIRE[2]
    img[cr & (fq < 0.3)] = FIRE[3]
    # Seiten
    sk = np.clip(np.floor(2 + (n - 0.5) * 2), 1, 3).astype(int)
    for k in range(1, 4):
        img[S.lip_side & (sk == k)] = C[k + 1]
    allm = S.lip
    o = dilate(allm, 1) & ~allm & ~S.floor & ~S.wall
    img[o] = C[0]
    img[neighbours_out(allm) & allm & (yy >= S.y_front + LIP - 1)] = C[0]
    # Fuge zwischen Boden und Wulst
    img[dilate(S.lip_top, 1) & ~S.lip & (yy < S.y_front) & (yy > FCY)] = C[0]
    return img


def kammer():
    frames = []
    for i in range(BEAT_FRAMES):
        frames.append(kammer_frame(i))
        print("  Kammer Bild %d/%d" % (i + 1, BEAT_FRAMES))
    # Bereich, in dem sich zwischen den Bildern etwas aendert
    diff = np.zeros((KH, KW), bool)
    for f in frames[1:]:
        diff |= np.any(f != frames[0], axis=-1)
    ys, xs = np.nonzero(diff)
    x0, x1 = int(xs.min()) - 1, int(xs.max()) + 2
    y0, y1 = int(ys.min()) - 1, int(ys.max()) + 2
    # gerade Masse, damit der Pivot auf ganzen Pixeln liegt
    if (x1 - x0) % 2:
        x1 += 1
    if (y1 - y0) % 2:
        y1 += 1
    crops = [f[y0:y1, x0:x1].copy() for f in frames]
    return frames[0], crops, (x0, y0, x1, y1), kammer_vorne()


# ================================================================== Ausgabe

def schreibe_raster(name, frames, pivot, cols=4):
    """Wie schreibe, aber die Bilder im Raster (Zeile fuer Zeile von oben) - fuer grosse Bilder."""
    from unity_meta import META_HEAD, SPRITE
    import random
    import uuid
    os.makedirs(OUT_DIR, exist_ok=True)
    path = os.path.join(OUT_DIR, name + ".png")
    h, w = frames[0].shape[:2]
    rows = (len(frames) + cols - 1) // cols
    sheet = Image.new("RGBA", (w * cols, h * rows))
    for i, f in enumerate(frames):
        sheet.paste(Image.fromarray(f), ((i % cols) * w, (i // cols) * h))
    sheet.save(path)
    meta = path + ".meta"
    if not os.path.exists(meta):
        rng = random.Random()
        names = ["%s_%d" % (name, i) for i in range(len(frames))]
        ids = [rng.randint(1 << 60, 1 << 62) for _ in names]
        idtable = "".join("  - first:\n      213: %d\n    second: %s\n" % (i, n) for n, i in zip(names, ids))
        sprites = ""
        for k, (n, i) in enumerate(zip(names, ids)):
            x = (k % cols) * w
            y = (rows - 1 - k // cols) * h          # Unity zaehlt von unten
            sp = SPRITE.format(name=n, x=x, w=w, h=h, px=pivot[0], py=pivot[1], sid=uuid.uuid4().hex, iid=i)
            sprites += sp.replace("        y: 0\n", "        y: %d\n" % y)
        nametable = "".join("      %s: %d\n" % (n, i) for n, i in sorted(zip(names, ids)))
        text = META_HEAD.format(guid=uuid.uuid4().hex, ppu=PPU, idtable=idtable, sprites=sprites, nametable=nametable)
        size = 2048
        while size < max(sheet.size):
            size *= 2
        text = text.replace("maxTextureSize: 2048", "maxTextureSize: %d" % size)
        with open(meta, "w", newline="\n") as fh:
            fh.write(text)
    print("%-26s %2d Bilder  %dx%d (Raster %dx%d)" % (name, len(frames), w, h, cols, rows))


def schreibe(name, frames, pivot, max_size=8192):
    os.makedirs(OUT_DIR, exist_ok=True)
    path = os.path.join(OUT_DIR, name + ".png")
    h, w = frames[0].shape[:2]
    sheet = Image.new("RGBA", (w * len(frames), h))
    for i, f in enumerate(frames):
        sheet.paste(Image.fromarray(f), (i * w, 0))
    sheet.save(path)
    meta = path + ".meta"
    if not os.path.exists(meta):
        write_strip_meta(meta, name, len(frames), w, h, PPU, pivot=pivot, max_size=max_size)
    print("%-26s %2d Bilder  %dx%d" % (name, len(frames), w, h))


def main():
    args = sys.argv[1:]
    nur = args[args.index("--nur") + 1] if "--nur" in args else None
    prev = args[args.index("--preview") + 1] if "--preview" in args else None
    out = {}
    if nur in (None, "herz"):
        out["herz"] = herz()
        schreibe("verkohlter_herz", out["herz"], pivot=(HCX / HW, 1 - HCY / HH))
    if nur in (None, "herztod"):
        out["tod"] = herz_tod()
        schreibe_raster("verkohlter_herz_tod", out["tod"], pivot=(DCX / DW, 1 - DCY / DH), cols=8)
    if nur in (None, "einsaugen"):
        out["ein"] = einsaugen()
        schreibe("verkohlter_einsaugen", out["ein"], pivot=V.PIVOT)
        mx, my = mund_offset()
        print("Schlund relativ zum Pivot: x %+.1f px, y %+.1f px  (= %.3f / %.3f Einheiten)"
              % (mx, my, mx / PPU, my / PPU))
    if nur in (None, "sog"):
        out["sog"] = sog()
        schreibe("verkohlter_sog", out["sog"], pivot=(0.5, 0.5))
    if nur in (None, "kammer"):
        back, crops, (x0, y0, x1, y1), front = kammer()
        out["kammer"] = (back, crops, (x0, y0, x1, y1), front)
        schreibe("herzkammer_hinten", [back], pivot=(KCX / KW, 1 - KCY / KH))
        # Pivot so, dass der Ausschnitt genau auf seiner Stelle im Gesamtbild liegt
        cw, ch = x1 - x0, y1 - y0
        schreibe_raster("herzkammer_glut", crops, pivot=((KCX - x0) / cw, 1 - (KCY - y0) / ch))
        schreibe("herzkammer_vorne", [front], pivot=(KCX / KW, 1 - KCY / KH))
        info = dict(floor_center=[(FCX - KCX) / PPU, -(FCY - KCY) / PPU],
                    pool_center=[(PCX - KCX) / PPU, -(PCY - KCY) / PPU],
                    floor_radius=[FRX / PPU, FRY / PPU], pool_radius=[POOL_RX / PPU, POOL_RY / PPU],
                    glut_rect=[x0, y0, x1, y1])
        print("Kammer:", json.dumps(info))
    if prev:
        vorschau(out, prev)


# ================================================================= Vorschau

def vorschau(out, folder, scale=3):
    os.makedirs(folder, exist_ok=True)
    if "herz" in out:
        fr = out["herz"]
        bg = (24, 16, 18, 255)
        ims = []
        for f in fr:
            im = Image.new("RGBA", (HW, HH), bg)
            im.alpha_composite(Image.fromarray(f))
            ims.append(im.convert("RGB").resize((HW * 4, HH * 4), Image.NEAREST))
        ims[0].save(os.path.join(folder, "herz.gif"), save_all=True, append_images=ims[1:], duration=83, loop=0)
        sheet = Image.new("RGB", (HW * 8, HH * 2), bg[:3])
        for i, im in enumerate(fr):
            b = Image.new("RGBA", (HW, HH), bg)
            b.alpha_composite(Image.fromarray(im))
            sheet.paste(b.convert("RGB"), ((i % 8) * HW, (i // 8) * HH))
        sheet.resize((sheet.width * 2, sheet.height * 2), Image.NEAREST).save(os.path.join(folder, "herz_sheet.png"))
    if "tod" in out:
        fr = out["tod"]
        bg = (34, 26, 28, 255)
        cols = 8
        rows = (len(fr) + cols - 1) // cols
        sheet = Image.new("RGB", (DW * cols, DH * rows), bg[:3])
        for i, f in enumerate(fr):
            b = Image.new("RGBA", (DW, DH), bg)
            b.alpha_composite(Image.fromarray(f))
            sheet.paste(b.convert("RGB"), ((i % cols) * DW, (i // cols) * DH))
        sheet.save(os.path.join(folder, "herztod_sheet.png"))
    if "ein" in out:
        fr = out["ein"]
        bg = (40, 32, 38, 255)
        sheet = Image.new("RGB", (128 * 8, 128 * 3), bg[:3])
        for i, f in enumerate(fr):
            b = Image.new("RGBA", (128, 128), bg)
            b.alpha_composite(Image.fromarray(f))
            sheet.paste(b.convert("RGB"), ((i % 8) * 128, (i // 8) * 128))
        sheet.resize((sheet.width * 2, sheet.height * 2), Image.NEAREST).save(os.path.join(folder, "einsaugen_sheet.png"))
    if "sog" in out:
        fr = out["sog"]
        bg = (40, 32, 38, 255)
        sheet = Image.new("RGB", (SW * 4, SW * 2), bg[:3])
        for i, f in enumerate(fr):
            b = Image.new("RGBA", (SW, SW), bg)
            b.alpha_composite(Image.fromarray(f))
            sheet.paste(b.convert("RGB"), ((i % 4) * SW, (i // 4) * SW))
        sheet.resize((sheet.width * 2, sheet.height * 2), Image.NEAREST).save(os.path.join(folder, "sog_sheet.png"))
    if "kammer" in out:
        back, crops, (x0, y0, x1, y1), front = out["kammer"]
        herzfr = out.get("herz")
        ims = []
        for i, c in enumerate(crops):
            im = Image.fromarray(back.copy())
            im.paste(Image.fromarray(c), (x0, y0))
            if herzfr is not None:
                hb = -2.0 * math.sin(TAU * i / BEAT_FRAMES)
                im.alpha_composite(Image.fromarray(herzfr[i]), (int(HEART_X - HCX), int(HEART_Y - HCY + round(hb))))
            im.alpha_composite(Image.fromarray(front))
            # Kamera-Ausschnitt 480x270 um das Herz
            cam = im.crop((KCX - 240, KCY - 135, KCX + 240, KCY + 135))
            ims.append(cam.convert("RGB"))
            if i == 0:
                im.convert("RGB").resize((KW * 2, KH * 2), Image.NEAREST).save(os.path.join(folder, "kammer_ganz.png"))
                # Kamera am Anschlag unten links (Spieler in der Ecke)
                cx, cy = KCX - 230, KCY + 125
                im.crop((cx - 240, cy - 135, cx + 240, cy + 135)).convert("RGB").resize(
                    (480 * 3, 270 * 3), Image.NEAREST).save(os.path.join(folder, "kammer_ecke.png"))
        ims[0].resize((480 * 3, 270 * 3), Image.NEAREST).save(os.path.join(folder, "kammer.png"))
        ims[2].resize((480 * 3, 270 * 3), Image.NEAREST).save(os.path.join(folder, "kammer_bum.png"))
        big = [x.resize((960, 540), Image.NEAREST) for x in ims]
        big[0].save(os.path.join(folder, "kammer.gif"), save_all=True, append_images=big[1:], duration=83, loop=0)
    print("Vorschau in", folder)


if __name__ == "__main__":
    main()
