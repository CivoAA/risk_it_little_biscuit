"""
Das Gespenst / Squiddy - Endboss des Lebkuchen-Geisterwalds (EnemyId.Squiddy).

Phase 1 schwebt ein grosses Laken-Gespenst durch den Wald (Familie der
Zuckergeister: lavendelweiss, eigenes Leuchten). Darunter steckt Squiddy,
die Traubengelee-Qualle mit Heiligenschein. Bei halbem Leben kriechen die
Tentakel unter dem Saum hervor, packen das Laken, reissen es hoch und
schleudern es weg - Phase 2 kaempft die Qualle selbst.

EIN Modell fuer beide: Glocke, sechs Tentakel und Heiligenschein sind immer
da, das Laken liegt in Phase 1 darueber. Alles in Koerper-Koordinaten
(u nach rechts, v nach oben, Ursprung am Boden unter der Mitte), eine Pose
verschiebt, staucht und dreht das Ganze. Gezeichnet wird wie bei den anderen
Bossen: Felder auf einem SS-fach feineren Raster, je Ebene Klasse + Licht,
Mehrheitsentscheid je Bildpixel, Licht gemittelt und in wenige klare Toene
gerastert, Umriss als letzter Schritt, Gesichter als Pixel-Stempel.

  Assets/Art/Gegner/new/boss/   (160x160, PPU 32, 12 fps, Pivot Mitte Bodenlinie)
    squiddy_geist_schweben.png     16  Gespenst schwebt (Schleife)
    squiddy_geist_spaeher.png      16  dasselbe, eine Tentakelspitze lugt unter dem Saum hervor
    squiddy_geist_abtauchen.png    10  holt Schwung und taucht in den Boden (Buh!)
    squiddy_geist_buh.png          14  schiesst aus dem Boden, 5 = BUH (5-8 halten), Rest beruhigen
    squiddy_geist_reigen.png       18  Arme hoch, 6-11 Wirbel (Schleife), 12-17 ausklingen
    squiddy_enthuellung.png        48  Phase 2: zittern, Beulen, Tentakel, Laken runter, Heiligenschein
    squiddy_schwimm.png            16  Squiddy schwimmt (Rueckstoss-Puls, Schleife)
    squiddy_nessel.png             14  Tentakel hoch, 5 = in den Boden (5-9 halten), zurueck
    squiddy_brut.png               12  zusammenziehen, 5 = Babys raus
    squiddy_spannung.png           18  laedt auf, 6-11 halten (Schleife), 12 = Entladung
    squiddy_tod.png                20  getroffen, Frieden, winkt (der Code laesst ihn aufsteigen)

Aufruf aus dem Projektordner:
  python Tools/squiddy.py [--nur schwimm,geist_schweben] [--preview ordner]
Effekte (Laken, Mini-Geister, Babys, Wellen ...) stehen in Tools/squiddy_fx.py.
"""

import math
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from eis_gegner_kit import LIGHT, Spring, ease_out, rgb, smooth  # noqa: E402
from unity_meta import write_strip_meta  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "Assets", "Art", "Gegner", "new", "boss")
BESTIARY = os.path.join(ROOT, "Assets", "Resources", "Bestiary", "Squiddy.png")

CELL = 160
GROUND = 152                 # Zeile direkt unter der Bodenlinie
CX = 80.0
PPU = 32
FPS = 12
SS = 4
TAU = math.pi * 2

# ================================================================ Farben

def pal(*h):
    return [rgb(x) for x in h]


# Laken: dieselbe Rampe wie die Zuckergeister (geist_atmo.py), eine Stufe tiefer fuer Falten
SHEET = pal("7c74b4", "b4b0e0", "dcdaf6", "f4f4ff")
SHEET_LINE = rgb("473d82")
SHEET_UNDER = pal("6e66a8", "8e88c6")             # Innenseite des Lakens
HOLE = rgb("170c26")                              # Augen-/Mundloch
HOLE_RIM = rgb("2c1e46")
PUPIL = (rgb("c79bff"), rgb("f3e4ff"))
BLUSH = rgb("ff9cc8")

# Traubengelee
BELL = pal("3c1d58", "552a78", "6f3b96", "8a52b4", "a874cf")
BELL_IN = pal("9a62c2", "b585da")                 # hellerer Kern (durchscheinend)
RIM = pal("7d4aa6", "b98ee0")                     # Saum der Glocke
SPEC = (rgb("e7c8fa"), rgb("ffffff"))
TENT_F = pal("482768", "62348a", "7d47a8", "9b67c6")
TENT_B = pal("2f1846", "432463", "5a3280")
JELLY_LINE = rgb("1b0b27")
FACE = rgb("1b0b27")
CHEEK = rgb("d872c8")

GOLD = pal("6e4012", "a8681f", "d79a42", "f4c866", "fff0b4")
GOLD_LINE = rgb("4a2a08")

SHADOW = (30, 22, 60, 110)
PUDDLE = pal("140a24", "26173f", "6a4fb0")
SPARK = (rgb("ffffff"), rgb("b4f0ff"), rgb("7fd6ff"))

# ================================================================ Masse (Koerper-Koordinaten)

HOVER = 14.0                 # Tentakelspitzen ueber dem Boden (Ruhe)
V_R = 66.0                   # Unterkante (Saum) der Glocke
A = 37.0                     # halbe Glockenbreite
B = 43.0                     # Glockenhoehe
P_EXP = 2.2                 # Superellipse der Glocke
T_LEN = 52.0                 # Tentakellaenge
ANCHOR = (0.0, V_R + 12.0)   # um diesen Punkt wird gestaucht/gedreht

M = 3.0                      # Laken liegt so weit ueber der Glocke
HEM0 = 25.0                  # Saumhoehe des Lakens
FLARE = 7.0                  # so weit faellt das Laken unten weiter aus
LOBES = 4.0                  # Zipfel am Saum (Abstand in halben Breiten)

EYE_SHEET = (13.5, V_R + 15.0)
MOUTH_SHEET = (0.0, V_R + 2.0)
EYE_JELLY = (15.5, V_R + 19.0)
MOUTH_JELLY = (0.0, V_R + 9.0)
HALO_C = (0.0, V_R + B - 5.0)
HALO_R = (24.0, 6.5)

# Tentakel: Ansatz u, Laenge, Grundwinkel (Grad, + = nach aussen), vorn?, Phase, Kraeuseln
TENTACLES = [
    dict(u=-27.0, L=0.86, a0=-16.0, front=False, ph=0.15, curl=-1.0, w=7.0),
    dict(u=26.0, L=0.84, a0=15.0, front=False, ph=0.62, curl=1.0, w=7.0),
    dict(u=-17.0, L=1.00, a0=-6.0, front=True, ph=0.40, curl=-0.8, w=8.0),
    dict(u=-5.5, L=0.94, a0=-2.0, front=True, ph=0.85, curl=0.7, w=7.6),
    dict(u=6.0, L=1.04, a0=2.5, front=True, ph=0.05, curl=-0.6, w=7.6),
    dict(u=17.0, L=0.92, a0=6.0, front=True, ph=0.55, curl=0.9, w=8.0),
]

# ================================================================ Klassen

(EMPTY, SHAD, HALO_B, TB, BODY, BODY_IN, RIMC, TF, HALO_F, SHEETC, SHEET_IN,
 HOLEC, GRAB, FX) = range(14)

LINE_OF = {FX: rgb("0e0718"), HALO_B: GOLD_LINE, HALO_F: GOLD_LINE, TB: JELLY_LINE, TF: JELLY_LINE, GRAB: JELLY_LINE,
           BODY: JELLY_LINE, BODY_IN: JELLY_LINE, RIMC: JELLY_LINE, SHEETC: SHEET_LINE,
           SHEET_IN: SHEET_LINE, HOLEC: HOLE}


def default_pose(**kw):
    p = dict(
        ox=0.0, oy=0.0, sx=1.0, sy=1.0, rot=0.0, hover=0.0,
        pulse=0.0,                         # +1 Glocke zusammengezogen (schmal, hoch)
        t_phase=0.0, t_amp=1.0, t_len=1.0, t_splay=0.0, t_curl=1.0, t_lift=0.0, t_drag=0.0,
        t_show=None,                       # Liste: welche Tentakel (None = alle)
        t_ground=False,                    # Tentakel enden im Boden (Nessel-Stern)
        grab=0.0, grab_up=0.0,             # Greif-Tentakel (Enthuellung)
        sheet=True, lift=0.0, sag=0.0, bundle=0.0, bundle_x=0.0,
        hem_phase=0.0, hem_amp=6.0, flutter=0.0, flare=FLARE,
        arm_l=(0.0, 0.0), arm_r=(0.0, 0.0),   # (hoch 0..1, weit 0..1)
        bumps=(),                          # (u, v, Radius, Staerke)
        face="grin", jelly_face="smug", eye_dx=0.0,
        halo=True, halo_dy=0.0, halo_tilt=-7.0, halo_glow=0.0,
        glow=0.0,                          # Glocke leuchtet (0..1)
        sparks=0.0, spark_seed=0,
        clip_below=None,                   # alles unter dieser Weltzeile weg (Abtauchen)
        shadow=1.0, puddle=0.0,
        shake=(0, 0), wave=None, clip_tent=False, ground_rings=0.0,
    )
    p.update(kw)
    return p


# ================================================================ Pose-Abbildung

class Frame:
    def __init__(self, p):
        self.p = p
        r = math.radians(p["rot"])
        self.ca, self.sa = math.cos(r), math.sin(r)
        self.ax, self.ay = ANCHOR[0], ANCHOR[1] + p["hover"]
        self.ox = p["ox"] + p["shake"][0]
        self.oy = p["oy"] + p["shake"][1]

    def to_local(self, X, Y):
        x = X - self.ax - self.ox
        y = Y - self.ay - self.oy
        u = x * self.ca - y * self.sa
        v = x * self.sa + y * self.ca
        return u / self.p["sx"] + ANCHOR[0], v / self.p["sy"] + ANCHOR[1]

    def to_world(self, u, v):
        x = (u - ANCHOR[0]) * self.p["sx"]
        y = (v - ANCHOR[1]) * self.p["sy"]
        X = x * self.ca + y * self.sa
        Y = -x * self.sa + y * self.ca
        return X + self.ax + self.ox, Y + self.ay + self.oy

    def px(self, u, v):
        X, Y = self.to_world(u, v)
        return int(math.floor(GROUND - Y)), int(math.floor(CX + X))


# ================================================================ Formen

def bell_dims(p):
    c = p["pulse"]
    a = A * (1 - 0.13 * c)
    b = B * (1 + 0.11 * c)
    return a, b


def bell_F(U, V, p):
    a, b = bell_dims(p)
    x = np.abs(U) / a
    y = (V - V_R) / b
    top = x ** P_EXP + np.maximum(y, 0) ** 2
    # Unter dem Saum: flache, leicht gewellte Unterkante (Saum rollt sich ein)
    lip = 5.0 / b * (0.82 + 0.18 * np.cos(x * math.pi * 9))
    bot = x ** P_EXP + (np.minimum(y, 0) / np.maximum(lip, 1e-3)) ** 2
    return np.where(y >= 0, top, bot)


def light_from_field(F_fn, U, V, depth=18.0, eps=0.5):
    F = F_fn(U, V)
    gx = (F_fn(U + eps, V) - F_fn(U - eps, V)) / (2 * eps)
    gy = (F_fn(U, V + eps) - F_fn(U, V - eps)) / (2 * eps)
    z = np.sqrt(np.clip(1 - F, 0, 1))
    nx, ny, nz = gx * depth, gy * depth, 2 * z
    ln = np.sqrt(nx * nx + ny * ny + nz * nz) + 1e-6
    return (nx * LIGHT[0] + ny * LIGHT[1] + nz * LIGHT[2]) / ln, F


def tentacle_path(t, p, i, n=90):
    """Mittellinie eines Tentakels: Liste (u, v, Breite) vom Ansatz zur Spitze."""
    a, b = bell_dims(p)
    u0 = t["u"] * a / A
    v0 = V_R - 2.0
    L = T_LEN * t["L"] * p["t_len"]
    if L < 2:
        return []
    pts = []
    u, v = u0, v0
    ds = L / n
    side = 1.0 if t["u"] > 0 else -1.0
    for k in range(n + 1):
        s = k / n
        ang = t["a0"] + side * p["t_splay"] * (0.6 + 0.4 * abs(t["u"]) / 27)
        wave = math.sin(TAU * (0.85 * s - p["t_phase"] - t["ph"])) * (7 + 6 * s) * p["t_amp"] * s ** 0.7
        curl = t["curl"] * p["t_curl"] * 125 * smooth(0.62, 1.0, s) ** 1.3
        lift = side * p["t_lift"] * 125 * s ** 0.8
        drag = p["t_drag"] * 25 * s * s
        a_deg = ang + wave + curl + lift + drag
        wv = p.get("wave")
        if wv is not None and wv[0] == i:
            a_deg += side * wv[1] * s + 28 * math.sin(TAU * (1.2 * s - wv[2])) * s
        w = t["w"] * (1 - 0.62 * s ** 1.1)
        pts.append((u, v, w))
        r = math.radians(a_deg)
        u += ds * math.sin(r)
        v -= ds * math.cos(r)
    return pts


def stamp_path(pts, fr, X, Y, owner, LAM, cls, extra_light=0.0, hide=None):
    """Kreisscheiben entlang der Mittellinie auf das feine Raster."""
    if not pts:
        return
    p = fr.p
    sx, sy = p["sx"], p["sy"]
    for k, (u, v, w) in enumerate(pts):
        wx, wy = fr.to_world(u, v)
        r = max(0.9, w / 2 * (sx + sy) / 2)
        c0 = int((wx + CX - r - 1) * SS)
        c1 = int((wx + CX + r + 1) * SS) + 1
        r0 = int((GROUND - wy - r - 1) * SS)
        r1 = int((GROUND - wy + r + 1) * SS) + 1
        c0, r0 = max(c0, 0), max(r0, 0)
        c1, r1 = min(c1, CELL * SS), min(r1, CELL * SS)
        if c0 >= c1 or r0 >= r1:
            continue
        dx = X[r0:r1, c0:c1] - wx
        dy = Y[r0:r1, c0:c1] - wy
        d2 = dx * dx + dy * dy
        m = d2 <= r * r
        if hide is not None:
            m &= ~hide[r0:r1, c0:c1]
        # Licht: Rundung quer zur Laufrichtung, Licht von links oben
        q = np.clip(np.sqrt(d2) / r, 0, 1)
        nx = dx / r
        ny = dy / r
        nz = np.sqrt(np.clip(1 - q * q, 0, 1))
        lam = nx * LIGHT[0] + ny * LIGHT[1] + nz * LIGHT[2] + extra_light
        sub_o = owner[r0:r1, c0:c1]
        sub_l = LAM[r0:r1, c0:c1]
        sub_o[m] = cls
        sub_l[m] = lam[m]


def sheet_geom(p):
    """Saumverlauf des Lakens. Liefert Funktionen fuer halbe Breite und Saumhoehe."""
    lift = p["lift"]
    am = A + M

    def half_width(V):
        t = np.clip((V_R - V) / (V_R - HEM0), 0, None)
        return am + p["flare"] * t ** 1.3

    hw_hem = am + p["flare"]
    hem_rise = (V_R + B + M + 6 - HEM0) * lift          # so weit ist der Saum hochgezogen

    def hem(U):
        xn = U / hw_hem
        ph = math.pi * LOBES * xn + p["hem_phase"]
        lobe = (0.5 + 0.5 * np.cos(ph)) ** 1.6
        flut = p["flutter"] * np.sin(ph * 0.5 + p["hem_phase"] * 1.7)
        base = HEM0 + hem_rise
        # Seiten werden hochgezogen, die Mitte haengt durch
        sag = p["sag"] * (1 - np.clip(np.abs(xn), 0, 1) ** 2)
        return base - p["hem_amp"] * lobe * (1 - 0.7 * lift) + flut - sag

    return half_width, hem, hw_hem


def arm_mask(U, V, side, up, out):
    """Laken-Arm: die Tentakel darunter heben die Seite an."""
    s = side
    sh = np.array([s * (A + M - 7.0), V_R + 6.0])
    tip_rest = np.array([s * (A + M + 9.0), V_R - 20.0])
    tip_up = np.array([s * (A + M + 13.0), V_R + 32.0])
    tip_out = np.array([s * (A + M + 27.0), V_R + 8.0])
    tip = tip_rest + (tip_up - tip_rest) * up + (tip_out - tip_rest) * out * (1 - 0.5 * up)
    pit = np.array([s * (A + M + 3.0), V_R - 26.0 + 14.0 * up + 6 * out])
    # Kapsel Schulter -> Spitze, nach aussen duenner
    d = tip - sh
    Ld = np.hypot(*d) + 1e-6
    t = np.clip(((U - sh[0]) * d[0] + (V - sh[1]) * d[1]) / (Ld * Ld), 0, 1)
    px = sh[0] + t * d[0]
    py = sh[1] + t * d[1]
    r = 8.0 - 4.0 * t
    cap = (U - px) ** 2 + (V - py) ** 2 <= r * r
    # Segel zwischen Spitze und Achsel, Unterkante haengt durch
    def side_of(ax, ay, bx, by):
        return (bx - ax) * (V - ay) - (by - ay) * (U - ax)
    e1 = side_of(sh[0], sh[1], tip[0], tip[1])
    e2 = side_of(tip[0], tip[1], pit[0], pit[1])
    e3 = side_of(pit[0], pit[1], sh[0], sh[1])
    tri = ((e1 >= 0) & (e2 >= 0) & (e3 >= 0)) | ((e1 <= 0) & (e2 <= 0) & (e3 <= 0))
    # Durchhang: Bogen von der Spitze zur Achsel nach innen schneiden
    mx, my = (tip[0] + pit[0]) / 2, (tip[1] + pit[1]) / 2
    sag_r = np.hypot(tip[0] - pit[0], tip[1] - pit[1]) * 0.62
    nx, ny = -(pit[1] - tip[1]), (pit[0] - tip[0])
    nl = np.hypot(nx, ny) + 1e-6
    nx, ny = nx / nl * s, ny / nl * s
    cx, cy = mx + nx * sag_r * 0.92, my + ny * sag_r * 0.92
    if ny > 0:            # Bogenmittelpunkt muss unter dem Segel liegen
        cx, cy = mx - nx * sag_r * 0.92, my - ny * sag_r * 0.92
    bite = (U - cx) ** 2 + (V - cy) ** 2 <= (sag_r * 0.98) ** 2
    return (cap | tri) & ~(bite & (V < V_R + 4)), tip


def bundle_mask(U, V, p):
    """Zusammengerafftes Laken ueber der Glocke (beim Hochreissen)."""
    k = p["bundle"]
    if k <= 0.01:
        return np.zeros(U.shape, bool), None
    a, b = bell_dims(p)
    cx = p["bundle_x"]
    cy = V_R + b + M + 4 + 10 * k
    rx = 22 + 18 * k
    ry = 9 + 9 * k
    ang = np.arctan2(V - cy, U - cx)
    wob = 1 + 0.12 * np.sin(ang * 5 + 1.3) + 0.06 * np.sin(ang * 9)
    q = ((U - cx) / rx) ** 2 + ((V - cy) / ry) ** 2
    return q <= wob ** 2, (cx, cy, rx, ry)


# ================================================================ Rendern

def render(p):
    fr = Frame(p)
    ys, xs = np.mgrid[0:CELL * SS, 0:CELL * SS]
    X = (xs + 0.5) / SS - CX
    Y = GROUND - (ys + 0.5) / SS
    U, V = fr.to_local(X, Y)
    owner = np.zeros(X.shape, np.int16)
    LAM = np.zeros(X.shape, np.float32)

    # --- Schatten auf dem Boden
    cxw = fr.to_world(0.0, V_R)[0]
    sh_w = 30 * p["shadow"]
    if sh_w > 2:
        m = ((X - cxw) / sh_w) ** 2 + ((Y + 0.5) / 5.0) ** 2 <= 1.0
        owner[m] = SHAD

    # --- Pfuetze (Abtauchen / Auftauchen)
    if p["puddle"] > 0.02:
        q = np.sqrt((X - cxw) ** 2 / (36 * p["puddle"]) ** 2 + (Y - 1.0) ** 2 / (8.5 * p["puddle"]) ** 2)
        m = q <= 1.0
        owner[m] = FX
        LAM[m] = q[m]

    # --- Heiligenschein hinten
    halo_ring = None
    if p["halo"]:
        halo_ring = halo_field(U, V, p)
        hb = halo_ring[0] & halo_ring[1]
        owner[hb] = HALO_B
        LAM[hb] = halo_ring[2][hb]

    # --- Tentakel hinten
    tpaths = []
    show = p["t_show"]
    for i, t in enumerate(TENTACLES):
        if show is not None and i not in show:
            tpaths.append([])
            continue
        tpaths.append(tentacle_path(t, p, i))
    for i, t in enumerate(TENTACLES):
        if not t["front"]:
            stamp_path(tpaths[i], fr, X, Y, owner, LAM, TB, -0.25)

    # --- Glocke
    lam_b, Fb = light_from_field(lambda u, v: bell_F(u, v, p), U, V, depth=16.0)
    body = Fb <= 1.0
    owner[body] = BODY
    LAM[body] = lam_b[body] + 0.32 * p["glow"]
    a, b = bell_dims(p)
    inner = (((U + 2) / (a * 0.66)) ** 2 + ((V - V_R - 3) / (b * 0.66)) ** 2 <= 1.0) & body & (V > V_R)
    owner[inner] = BODY_IN
    rim = body & (V < V_R + 3.2 + 1.2 * np.cos(U / a * math.pi * 9)) & (V > V_R - 6)
    owner[rim] = RIMC
    LAM[rim] = lam_b[rim]

    # --- Tentakel vorn
    for i, t in enumerate(TENTACLES):
        if t["front"]:
            stamp_path(tpaths[i], fr, X, Y, owner, LAM, TF)

    # --- Heiligenschein vorn
    if halo_ring is not None:
        hf = halo_ring[0] & ~halo_ring[1]
        owner[hf] = HALO_F
        LAM[hf] = halo_ring[2][hf]

    # --- Laken
    if p["sheet"]:
        paint_sheet(U, V, p, owner, LAM, fr, X, Y)

    # --- Greif-Tentakel (vor dem Laken)
    if p["grab"] > 0.01:
        for side in (-1, 1):
            stamp_path(grab_path(p, side), fr, X, Y, owner, LAM, GRAB, 0.05)

    if p["clip_tent"]:
        cut = (Y < 0.5) & np.isin(owner, (TF, TB, GRAB))
        owner[cut] = EMPTY

    if p["clip_below"] is not None:
        cut = (Y < p["clip_below"]) & (owner != SHAD) & (owner != FX)
        owner[cut] = EMPTY

    return fr, owner, LAM


def halo_field(U, V, p):
    cx, cy = HALO_C[0], HALO_C[1] + p["halo_dy"]
    r = math.radians(p["halo_tilt"])
    du, dv = U - cx, V - cy
    x = du * math.cos(r) + dv * math.sin(r)
    y = -du * math.sin(r) + dv * math.cos(r)
    rx, ry = HALO_R
    q = np.sqrt((x / rx) ** 2 + (y / ry) ** 2)
    ring = (q >= 0.74) & (q <= 1.22)
    back = y > 0
    ang = np.arctan2(y / ry, x / rx)
    lam = 0.55 + 0.45 * np.cos(ang - 2.4) - 0.35 * (q - 0.98) * 4 + 0.6 * p["halo_glow"]
    return ring, back, lam.astype(np.float32)


def grab_path(p, side, n=70):
    """Greif-Tentakel: unter dem Saum heraus, aussen am Laken hoch bis zum Griff."""
    g = p["grab"]
    half_width, hem, hw_hem = sheet_geom(p)
    up = smooth(0.0, 0.55, p["lift"])
    u0 = side * ((hw_hem - 9.0) * (1 - up) + 23.0 * up)
    v0 = (HEM0 - 4.0) * (1 - up) + (V_R - 3.0) * up
    if p["bundle"] > 0.01:
        a_, b_ = bell_dims(p)
        k = p["bundle"]
        cy = V_R + b_ + M + 4 + 10 * k
        rx = 22 + 18 * k
        gu, gv = p["bundle_x"] + side * rx * 0.85, cy - 2
    else:
        hem_side = float(hem(np.array(side * hw_hem * 0.97)))
        gv = max(V_R - 16.0, hem_side + 4)
        gu = side * (float(half_width(np.array(gv))) + 1.5)
    reach_u = side * (hw_hem + 12.0)
    reach_v = HEM0 - 12.0
    e = smooth(0.35, 1.0, g)
    tu = reach_u + (gu - reach_u) * e
    tv = reach_v + (gv - reach_v) * e
    vis = min(1.0, g / 0.35)
    c1u = side * (hw_hem + 16.0 + 4 * e)
    c1v = (HEM0 - 16.0 + 6 * e) * (1 - up) + (V_R - 18.0) * up
    pts = []
    for k in range(n + 1):
        s2 = k / n * (0.35 + 0.65 * vis)
        bu = (1 - s2) ** 2 * u0 + 2 * (1 - s2) * s2 * c1u + s2 * s2 * tu
        bv = (1 - s2) ** 2 * v0 + 2 * (1 - s2) * s2 * c1v + s2 * s2 * tv
        # schlaengelt sich, solange er noch tastet
        wig = 4.5 * (1 - e) * math.sin(s2 * TAU * 1.2 - g * 9)
        bu += side * wig * 0.3
        bv += wig
        pts.append((bu, bv, 8.5 * (1 - 0.45 * s2)))
    # Spitze legt sich um die Kante (Griff): kleiner Haken nach innen
    if e > 0.6:
        lu, lv, lw = pts[-1]
        for k in range(1, 10):
            ang = math.radians(20 * k)
            pts.append((lu - side * 3.0 * math.sin(ang), lv + 3.0 * (1 - math.cos(ang)) * 0.8 + 0.6 * k, lw * 0.92))
    return pts


def paint_sheet(U, V, p, owner, LAM, fr, X, Y):
    half_width, hem, hw_hem = sheet_geom(p)
    a_s, b_s = bell_dims(p)
    am, bm = a_s + M, b_s + M
    x = np.abs(U) / am
    yv = (V - V_R) / bm
    dome = (x ** P_EXP + np.maximum(yv, 0) ** 2 <= 1.0) & (V >= V_R)
    hw = half_width(V)
    # Beulen (Tentakel druecken von innen) machen die Seiten breiter
    for (bu, bv, br, bs) in p["bumps"]:
        hw = hw + bs * 13.0 * np.exp(-((V - bv) / br) ** 2) * (np.sign(U) == np.sign(bu))
    skirt = (np.abs(U) <= hw) & (V < V_R)
    shape = dome | skirt
    for side, arm in ((-1, p["arm_l"]), (1, p["arm_r"])):
        up, out = arm
        am_mask, _ = arm_mask(U, V, side, up, out)
        shape |= am_mask
    hem_v = hem(U)
    shape &= V >= hem_v
    bmask, binfo = bundle_mask(U, V, p)
    shape |= bmask

    # Licht: Grundform wie ein Tropfen, Falten laufen vom Saum hoch
    def F_fn(u, v):
        xx = np.abs(u) / am
        top = xx ** P_EXP + (np.maximum(v - V_R, 0) / bm) ** 2
        side = (np.abs(u) / (am + p["flare"] * np.clip((V_R - v) / (V_R - HEM0), 0, None) ** 1.3)) ** 2.4 * 0.98
        return np.where(v >= V_R, top, side)

    lam, _ = light_from_field(F_fn, U, V, depth=14.0)
    t = np.clip((V_R + 6 - V) / (V_R + 6 - HEM0), 0, 1)
    xn = U / hw_hem
    fold = np.cos(math.pi * LOBES * xn + p["hem_phase"])
    lam = lam + 0.20 * fold * t ** 1.6 - 0.10 * t
    for (bu, bv, br, bs) in p["bumps"]:
        d2 = ((U - bu) ** 2 + (V - bv) ** 2) / (br * br)
        g = np.exp(-d2)
        # Beule: oben links Licht, unten rechts Schatten
        lam = lam + bs * 0.45 * g * np.clip(1 + (-(U - bu) * 0.4 + (V - bv) * 0.6) / br, 0, 2)
    # Arme: Licht von oben
    for side, arm in ((-1, p["arm_l"]), (1, p["arm_r"])):
        am_mask, _ = arm_mask(U, V, side, *arm)
        only_arm = am_mask & ~(dome | skirt)
        lam = np.where(only_arm, 0.55 + 0.25 * (-side) * 0.4 + 0.2 * np.clip((V - V_R) / 30, -1, 1), lam)
    if binfo is not None:
        cx, cy, rx, ry = binfo
        q = ((U - cx) / rx) ** 2 + ((V - cy) / ry) ** 2
        bl = 0.85 - 0.7 * q + 0.22 * np.sin((U - cx) * 0.16 + (V - cy) * 0.5 + 0.8) - 0.3 * (U - cx) / rx + 0.25 * (V - cy) / ry
        lam = np.where(bmask, bl, lam)

    owner[shape] = SHEETC
    LAM[shape] = lam[shape]

    # Innenseite: ein schmaler Streifen ueber dem Saum, wenn hochgezogen
    if p["lift"] > 0.02:
        under = shape & (V < hem_v + 2.2 + 3 * p["lift"]) & ~bmask
        owner[under] = SHEET_IN
        LAM[under] = 0.3

    if p["face"] and p["lift"] < 0.25 and p["bundle"] < 0.01:
        paint_sheet_face(U, V, p, owner, shape)


SHEET_FACES = {
    # Augen: (rx, ry, Braue: Schraege (+ = innen tiefer = boese), Hoehe der Brauenkante, Pupille)
    # Mund: (Art, rx, ry, dy)
    "grin": dict(eye=(7.5, 9.0, 0.55, 3.5, "dot"), mouth=("grin", 15.0, 7.0, 0.0)),
    "o": dict(eye=(6.5, 8.5, -0.1, 9.0, "dot"), mouth=("o", 4.5, 5.0, 0.0)),
    "buh": dict(eye=(7.0, 8.0, 0.7, 3.0, "pin"), mouth=("buh", 11.0, 8.5, -8.0)),
    "fies": dict(eye=(8.0, 7.0, 0.85, 1.5, "dot"), mouth=("grin", 16.0, 8.0, 0.0)),
    "bang": dict(eye=(6.5, 9.5, -0.45, 6.0, "small"), mouth=("wavy", 9.0, 3.0, 0.0)),
    "zu": dict(eye=(7.5, 2.0, 0.3, 9.0, None), mouth=("grin", 14.0, 6.0, 0.0)),
    "schock": dict(eye=(7.5, 10.5, -0.2, 11.0, "pin"), mouth=("o", 6.0, 7.5, -1.0)),
}


def paint_sheet_face(U, V, p, owner, shape):
    f = SHEET_FACES[p["face"]]
    rx, ry, slant, brow, _pupil = f["eye"]
    for side in (-1, 1):
        cx = side * EYE_SHEET[0] + p["eye_dx"]
        cy = EYE_SHEET[1]
        du, dv = U - cx, V - cy
        m = (du / rx) ** 2 + (dv / ry) ** 2 <= 1.0
        # Brauenkante: schraeg abgeschnitten, innen (zur Mitte) tiefer
        m &= dv <= brow + slant * side * du
        owner[m & shape] = HOLEC
    kind, mrx, mry, mdy = f["mouth"]
    cx, cy = MOUTH_SHEET[0] + p["eye_dx"] * 0.6, MOUTH_SHEET[1] + mdy
    du, dv = U - cx, V - cy
    if kind == "grin":
        # Sichel: unten rund, oben flacher Bogen, Mundwinkel spitz nach oben
        low = (du / mrx) ** 2 + (dv / mry) ** 2 <= 1.0
        top = dv <= -mry * 0.15 + mry * 0.9 * (du / mrx) ** 2
        m = low & top
    elif kind == "o":
        m = (du / mrx) ** 2 + (dv / mry) ** 2 <= 1.0
    elif kind == "buh":
        ang = np.arctan2(dv, du)
        rr = 1 + 0.10 * np.cos(ang * 7)
        m = (du / mrx) ** 2 + (dv / mry) ** 2 <= rr ** 2
    else:  # wavy
        m = (np.abs(dv - 1.6 * np.sin(du * 0.9)) <= 1.5) & (np.abs(du) <= mrx)
    owner[m & shape] = HOLEC


# ================================================================ Faerben

HOT = [False]
BELL_HOT = pal("8a52b4", "b884dc", "dcb8f4", "f4e6ff", "ffffff")
TENT_HOT = pal("7d47a8", "a878d0", "d8b8f0", "f6eeff")


def tone(cls, l):
    if HOT[0]:
        if cls in (BODY, BODY_IN, RIMC):
            return BELL_HOT[4] if l > 0.9 else BELL_HOT[3] if l > 0.5 else BELL_HOT[2] if l > 0.1 else BELL_HOT[1]
        if cls in (TF, TB):
            return TENT_HOT[3] if l > 0.5 else TENT_HOT[2] if l > 0.0 else TENT_HOT[1]
    if cls == SHEETC:
        return SHEET[3] if l > 0.62 else SHEET[2] if l > 0.22 else SHEET[1] if l > -0.18 else SHEET[0]
    if cls == SHEET_IN:
        return SHEET_UNDER[1] if l > 0.4 else SHEET_UNDER[0]
    if cls == BODY:
        return BELL[4] if l > 0.74 else BELL[3] if l > 0.38 else BELL[2] if l > 0.02 else BELL[1] if l > -0.4 else BELL[0]
    if cls == BODY_IN:
        return BELL_IN[1] if l > 0.45 else BELL_IN[0]
    if cls == RIMC:
        return RIM[1] if l > 0.25 else RIM[0]
    if cls in (TF, GRAB):
        return TENT_F[3] if l > 0.62 else TENT_F[2] if l > 0.22 else TENT_F[1] if l > -0.2 else TENT_F[0]
    if cls == TB:
        return TENT_B[2] if l > 0.4 else TENT_B[1] if l > -0.1 else TENT_B[0]
    if cls in (HALO_B, HALO_F):
        l2 = l - (0.25 if cls == HALO_B else 0.0)
        return GOLD[4] if l2 > 0.95 else GOLD[3] if l2 > 0.55 else GOLD[2] if l2 > 0.15 else GOLD[1]
    if cls == HOLEC:
        return HOLE
    if cls == SHAD:
        return SHADOW
    if cls == FX:
        return PUDDLE[2] if l > 0.78 else PUDDLE[1] if l > 0.5 else PUDDLE[0]
    return (255, 0, 255, 255)


def majority(owner, LAM):
    """Mehrheitsentscheid je Bildpixel + gemitteltes Licht der Gewinnerklasse."""
    o = owner.reshape(CELL, SS, CELL, SS).transpose(0, 2, 1, 3).reshape(CELL, CELL, SS * SS)
    l = LAM.reshape(CELL, SS, CELL, SS).transpose(0, 2, 1, 3).reshape(CELL, CELL, SS * SS)
    codes = np.unique(owner)
    counts = np.stack([(o == c).sum(-1) for c in codes], -1)
    solid = codes != EMPTY
    pix = np.full((CELL, CELL), EMPTY, np.int16)
    if solid.any():
        best = codes[solid][np.argmax(counts[..., solid], -1)]
        pix = best.astype(np.int16)
    empty_n = (o == EMPTY).sum(-1)
    pix[empty_n > SS * SS // 2] = EMPTY
    # duenne Tentakel ueberleben schon ab 35 %
    for k in (TF, TB, GRAB):
        pix[((o == k).sum(-1) >= SS * SS * 0.35) & (pix == EMPTY)] = k
    sel = o == pix[..., None]
    lam = np.where(sel.any(-1), (l * sel).sum(-1) / np.maximum(sel.sum(-1), 1), 0.0)
    return pix, lam


def shade(pix, lam):
    img = np.zeros((CELL, CELL, 4), np.uint8)
    for cls in np.unique(pix):
        if cls == EMPTY:
            continue
        m = pix == cls
        if cls == SHAD:
            img[m] = SHADOW
            continue
        rr, cc = np.where(m)
        for r, c in zip(rr, cc):
            img[r, c] = tone(cls, lam[r, c])
    return img


def nb4(a, r, c, h, w):
    out = []
    for dr, dc in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        rr, cc = r + dr, c + dc
        out.append(a[rr, cc] if 0 <= rr < h and 0 <= cc < w else EMPTY)
    return out


def outlines(img, pix):
    h, w = pix.shape
    out = img.copy()
    front_of = {
        TF: (BODY, BODY_IN, RIMC, TB), GRAB: (SHEETC, SHEET_IN, BODY, TF, TB),
        SHEETC: (BODY, BODY_IN, RIMC, TF, TB, HALO_B, HALO_F),
        SHEET_IN: (BODY, BODY_IN, RIMC, TF, TB),
        HALO_F: (BODY, BODY_IN, RIMC), BODY: (TB, HALO_B), RIMC: (TB, HALO_B), BODY_IN: (TB,),
        TB: (),
    }
    for r in range(h):
        for c in range(w):
            m = pix[r, c]
            if m in (EMPTY, SHAD, HOLEC):
                continue
            if m == FX:
                continue
            nbs = nb4(pix, r, c, h, w)
            if any(n in (EMPTY, SHAD) for n in nbs):
                out[r, c] = LINE_OF.get(m, JELLY_LINE)
            elif any(n in front_of.get(m, ()) for n in nbs):
                # innere Kante: nur wo das Vordere an etwas Hinteres stoesst
                out[r, c] = LINE_OF.get(m, JELLY_LINE)
    # Tentakel vorn: Kante zum gleichen Tentakel-Typ nur bei Ueberkreuzung -> ueber Licht nicht trennbar, ok
    return out


def cleanup(img, pix, classes=(SHEETC, BODY, BODY_IN, TF, TB, GRAB, HALO_F, HALO_B, RIMC)):
    h, w = pix.shape
    out = img.copy()
    for r in range(1, h - 1):
        for c in range(1, w - 1):
            if pix[r, c] not in classes:
                continue
            me = tuple(img[r, c])
            nb = [tuple(img[r + a, c + b]) for a, b in ((1, 0), (-1, 0), (0, 1), (0, -1))]
            if me in nb:
                continue
            same = [n for k, n in enumerate(nb)
                    if pix[r + ((1, -1, 0, 0)[k]), c + ((0, 0, 1, -1)[k])] == pix[r, c]]
            if same:
                out[r, c] = max(set(same), key=same.count)
    return out


# ================================================================ Gesichter (Stempel)

def put(img, r, c, col):
    if 0 <= r < img.shape[0] and 0 <= c < img.shape[1]:
        img[r, c] = col


def stamp(img, rows, r0, c0, colors, mirror=False):
    for dr, row in enumerate(rows):
        if mirror:
            row = row[::-1]
        for dc, ch in enumerate(row):
            if ch in colors:
                put(img, r0 + dr, c0 + dc, colors[ch])


JELLY_EYES = {
    # linkes Auge, Innenseite rechts (wird fuer rechts gespiegelt)
    "smug": ("###########.",
             "############"),
    "kneif": ("##........",
              ".###......",
              "...####...",
              ".###......",
              "##........"),
    "frieden": ("..######..",
                ".##....##.",
                "##......##"),
    "auf": ("..####..",
            ".#WWWW#.",
            "#WWWWWW#",
            "#WWWWWW#",
            ".#WWWW#.",
            "..####.."),
    "glut": ("..####..",
             ".#HHHH#.",
             "#HWWWWH#",
             "#HWWWWH#",
             ".#HHHH#.",
             "..####.."),
    "aua": ("#.......#",
            ".##...##.",
            "...###...",
            ".##...##.",
            "#.......#"),
    "x": ("#....#",
          ".#..#.",
          "..##..",
          ".#..#.",
          "#....#"),
}


def omega(img, fr, width=26, depth=4.0, open_=0.0, col=FACE):
    """Der Squiddy-Mund: breites w mit hochgezogenen Ecken."""
    r0, c0 = fr.px(MOUTH_JELLY[0], MOUTH_JELLY[1])
    half = width / 2
    pts = []
    for k in range(int(width * 3) + 1):
        x = -half + k / 3.0
        t = x / half
        y = depth * (math.cos(TAU * t) + 1) / 2 * 0.55 + 2.2 * t ** 6
        pts.append((x, y))
    # Mundraum offen (fuer Hochspannung): unter der Linie fuellen
    if open_ > 0:
        for x in range(-int(half * 0.55), int(half * 0.55) + 1):
            hh = int(round(open_ * 6 * (1 - (x / (half * 0.55)) ** 2)))
            for d in range(1, hh + 1):
                put(img, r0 + d + 1, c0 + x, rgb("5a1838") if d < hh else col)
    for x, y in pts:
        r = int(round(r0 - y))
        c = int(round(c0 + x))
        put(img, r, c, col)
        put(img, r + 1, c, col)


def jelly_face(img, pix, fr, p):
    kind = p["jelly_face"]
    if kind is None:
        return
    cols = {"#": FACE, "W": rgb("ffffff"), "H": rgb("cfe9ff")}
    for side in (-1, 1):
        r, c = fr.px(side * EYE_JELLY[0], EYE_JELLY[1])
        rows = JELLY_EYES[kind]
        w = len(rows[0])
        h = len(rows)
        mirror = side > 0
        stamp(img, rows, r - h // 2, c - w // 2, cols, mirror=mirror)
    if kind in ("auf", "glut"):
        omega(img, fr, width=22, depth=3.0, open_=0.8)
    elif kind == "frieden":
        omega(img, fr, width=18, depth=2.0)
    elif kind == "aua":
        omega(img, fr, width=16, depth=-3.0)
    else:
        omega(img, fr)
    if kind in ("smug", "frieden", "kneif"):
        for side in (-1, 1):
            r, c = fr.px(side * (EYE_JELLY[0] + 4), EYE_JELLY[1] - 7)
            for dc in range(-2, 3):
                if pix[r, c + dc] in (BODY, BODY_IN):
                    put(img, r, c + dc, CHEEK)


def sheet_pupils(img, pix, fr, p):
    if not p["sheet"] or not p["face"] or p["lift"] >= 0.25 or p["bundle"] >= 0.01:
        return
    f = SHEET_FACES[p["face"]]
    kind = f["eye"][4]
    if kind is None:
        return
    for side in (-1, 1):
        r, c = fr.px(side * EYE_SHEET[0] + p["eye_dx"] + side * 0.5, EYE_SHEET[1] - 1.5)
        if kind == "dot":
            for dr, dc, k in ((0, 0, 1), (0, 1, 0), (1, 0, 0), (1, 1, 0)):
                if 0 <= r + dr < CELL and 0 <= c + dc < CELL and pix[r + dr, c + dc] == HOLEC:
                    put(img, r + dr, c + dc, PUPIL[k])
        elif kind == "pin":
            if 0 <= r < CELL and 0 <= c < CELL and pix[r, c] == HOLEC:
                put(img, r, c, PUPIL[1])
        elif kind == "small":
            put(img, r + 1, c, PUPIL[0])
    # Rand des Lochs etwas heller (Stoffkante)
    h, w = pix.shape


def bolt(img, r0, c0, r1, c1, rng, core=SPARK[0], glow=SPARK[2]):
    """Zickzack-Blitz per Mittelpunkt-Verschiebung: weisser Kern, blauer Saum."""
    pts = [(float(r0), float(c0)), (float(r1), float(c1))]
    for depth in range(3):
        nxt = [pts[0]]
        for (ra, ca), (rb, cb) in zip(pts, pts[1:]):
            L = math.hypot(rb - ra, cb - ca)
            off = rng.uniform(-0.28, 0.28) * L
            nr, nc = (ra + rb) / 2 + off * (cb - ca) / max(L, 1e-3), (ca + cb) / 2 - off * (rb - ra) / max(L, 1e-3)
            nxt += [(nr, nc), (rb, cb)]
        pts = nxt
    cells = []
    for (ra, ca), (rb, cb) in zip(pts, pts[1:]):
        n = int(max(abs(rb - ra), abs(cb - ca))) + 1
        for i in range(n + 1):
            cells.append((int(round(ra + (rb - ra) * i / n)), int(round(ca + (cb - ca) * i / n))))
    for r, c in cells:
        for dr, dc in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            rr, cc = r + dr, c + dc
            if 0 <= rr < CELL and 0 <= cc < CELL and (rr, cc) not in cells:
                put(img, rr, cc, glow)
    for r, c in cells:
        put(img, r, c, core)


def sparks(img, fr, p, k):
    """Elektrische Boegen zwischen benachbarten Tentakeln (wenige, kraeftige)."""
    n = int(round(p["sparks"] * 2.5))
    if n <= 0:
        return
    rng = np.random.RandomState(p["spark_seed"] * 31 + k * 7 + 3)
    show = p["t_show"]
    paths = [tentacle_path(t, p, i) if (show is None or i in show) else [] for i, t in enumerate(TENTACLES)]
    order = [0, 2, 3, 4, 5, 1]                       # links nach rechts
    order = [i for i in order if paths[i]]
    if len(order) < 2:
        return
    for _ in range(n):
        j = rng.randint(0, len(order) - 1)
        a_, b_ = paths[order[j]], paths[order[j + 1]]
        ia = int(rng.uniform(0.3, 0.75) * (len(a_) - 1))
        ib = int(rng.uniform(0.3, 0.75) * (len(b_) - 1))
        ra, ca = fr.px(a_[ia][0], a_[ia][1])
        rb, cb = fr.px(b_[ib][0], b_[ib][1])
        if p["clip_tent"] and (ra >= GROUND or rb >= GROUND):
            continue
        bolt(img, ra, ca, rb, cb, rng)


def halo_glints(img, fr, p):
    if not p["halo"] or p["halo_glow"] < 0.4:
        return
    cx, cy = HALO_C[0], HALO_C[1] + p["halo_dy"]
    g = p["halo_glow"]
    for (u, v, n) in ((cx - HALO_R[0] * 0.75, cy + 1, 2), (cx + HALO_R[0] * 0.9, cy - 2, 3), (cx, cy + HALO_R[1] + 4, 2)):
        r, c = fr.px(u, v)
        n = int(n * g + 0.5)
        for d in range(1, n + 1):
            for a, b in ((d, 0), (-d, 0), (0, d), (0, -d)):
                if img[r + a, c + b, 3] == 0 or d == 1:
                    put(img, r + a, c + b, SPARK[0] if d == 1 else rgb("ffe9a8"))


def specular(img, pix, fr, p):
    """Gelee-Glanz: ein sauberer Bogen oben links, zwei Pixel dick."""
    a, b = bell_dims(p)
    pts = []
    for k in range(40):
        t = k / 39
        ang = math.radians(150 - 38 * t)
        u = a * 0.78 * math.cos(ang)
        v = V_R + b * 0.80 * math.sin(ang) - 3
        pts.append((u, v))
    done = set()
    for i, (u, v) in enumerate(pts):
        r, c = fr.px(u, v)
        if (r, c) in done:
            continue
        done.add((r, c))
        if 0 <= r < CELL and 0 <= c < CELL and pix[r, c] in (BODY, BODY_IN):
            put(img, r, c, SPEC[1] if 0.2 < i / 39 < 0.75 else SPEC[0])


def draw(p, k=0):
    fr, owner, LAM = render(p)
    pix, lam = majority(owner, LAM)
    HOT[0] = p["glow"] > 1.5
    img = shade(pix, lam)
    img = outlines(img, pix)
    img = cleanup(img, pix)
    if p["sheet"]:
        sheet_pupils(img, pix, fr, p)
    if not p["sheet"] or p["lift"] > 0.55:
        visible_face = p["jelly_face"] is not None
        if visible_face and (not p["sheet"] or p["bundle"] > 0.3):
            jelly_face(img, pix, fr, p)
        if not p["sheet"] or p["bundle"] > 0.3:
            specular(img, pix, fr, p)
    halo_glints(img, fr, p)
    sparks(img, fr, p, k)
    return img


# ================================================================ Animationen

def loop_t(k, n):
    return k / n


def geist_schweben(peek=False):
    out = []
    n = 16
    for k in range(n):
        t = k / n
        ph = TAU * t
        bob = 2.5 * math.sin(ph)
        pose = default_pose(
            hover=bob, sheet=True, halo=False, t_len=0.72, t_amp=0.5, t_phase=t,
            hem_phase=0.45 * math.sin(ph) + 0.15, flutter=1.6 * math.sin(ph * 2),
            hem_amp=6.0 + 1.0 * math.sin(ph + 1),
            sx=1 + 0.015 * math.sin(ph + 1.6), sy=1 - 0.015 * math.sin(ph + 1.6),
            rot=1.2 * math.sin(ph + 0.7),
            arm_l=(0.08 + 0.06 * math.sin(ph + 0.4), 0.0), arm_r=(0.08 + 0.06 * math.sin(ph + 2.0), 0.0),
            shadow=1.0 - 0.06 * math.sin(ph),
            face="zu" if (not peek and k in (10, 11)) else "grin",
        )
        if peek:
            # Spitze eines Tentakels lugt raus, rollt sich, verschwindet
            e = smooth(0.15, 0.45, t) * (1 - smooth(0.62, 0.9, t))
            pose["t_show"] = [3]
            pose["t_len"] = 0.72 + 0.34 * e
            pose["t_curl"] = 1.0 + 0.8 * math.sin(TAU * t * 2)
            pose["face"] = "o" if 0.4 < t < 0.7 else "grin"
            pose["eye_dx"] = -1.0 if 0.4 < t < 0.7 else 0.0
        else:
            pose["t_show"] = []
        out.append(pose)
    return out


def squiddy_schwimm():
    """Rueckstoss: zusammenziehen (Schub), ausdehnen (gleiten). Tentakel schleppen nach."""
    n = 16
    out = []
    sp_amp = Spring(40.0, 7.0)
    sp_spl = Spring(55.0, 8.0)
    sp_halo = Spring(70.0, 6.0)

    def pulse_of(t):
        # schnell zusammen (0..0.3), langsam auf (0.3..1)
        if t < 0.3:
            return math.sin(t / 0.3 * math.pi / 2)
        return math.cos((t - 0.3) / 0.7 * math.pi / 2) * 1.0 - 0.25 * math.sin((t - 0.3) / 0.7 * math.pi)

    vals = []
    dt = 1.0 / (FPS * 20)
    for rep in range(6):
        rec = []
        for i in range(n * 20):
            t = i / (n * 20)
            pl = pulse_of(t)
            amp = sp_amp.step(0.55 + 0.6 * (1 - pl), dt)
            spl = sp_spl.step(-14 * pl + 6 * (1 - pl), dt)
            rise = 3.5 * pl
            halo = sp_halo.step(rise, dt)
            if i % 20 == 0:
                rec.append((t, pl, amp, spl, rise, halo))
        vals = rec
    for k, (t, pl, amp, spl, rise, halo) in enumerate(vals):
        out.append(default_pose(
            sheet=False, pulse=pl, hover=rise - 1.0, t_phase=t * 1.0, t_amp=amp, t_splay=spl,
            t_len=1.0 + 0.06 * pl, t_curl=1.0 - 0.45 * pl,
            halo_dy=(halo - rise) * 1.3 + 0.5 * math.sin(TAU * t), halo_tilt=-7 + 3 * math.sin(TAU * t),
            jelly_face="smug", shadow=1.0 - 0.08 * pl,
        ))
    return out


def rest_ghost(**kw):
    base = dict(sheet=True, halo=False, t_len=0.72, t_amp=0.5, t_show=[], face="grin",
                arm_l=(0.08, 0.0), arm_r=(0.08, 0.0))
    base.update(kw)
    return default_pose(**base)


def geist_abtauchen():
    """0-2 Schwung holen, 3 ducken, 4-8 taucht in die Pfuetze, 9 nur noch Pfuetze."""
    out = []
    keys = [
        dict(hover=2, sy=1.03, face="fies", arm_l=(0.2, 0), arm_r=(0.2, 0)),
        dict(hover=5, sy=1.07, sx=0.97, face="fies", arm_l=(0.45, 0), arm_r=(0.45, 0), hem_phase=0.3),
        dict(hover=7, sy=1.10, sx=0.95, face="fies", arm_l=(0.7, 0), arm_r=(0.7, 0), hem_phase=0.6, flutter=2),
        dict(hover=-2, sy=0.84, sx=1.13, face="zu", arm_l=(0.3, 0.3), arm_r=(0.3, 0.3), hem_amp=4, flare=12),
        dict(oy=-22, sy=1.22, sx=0.84, face="fies", puddle=0.55, shadow=0.7, flutter=3, arm_l=(0.9, 0), arm_r=(0.9, 0)),
        dict(oy=-50, sy=1.30, sx=0.80, face="fies", puddle=0.85, shadow=0.4, flutter=3, arm_l=(1, 0), arm_r=(1, 0)),
        dict(oy=-82, sy=1.30, sx=0.82, puddle=1.0, shadow=0.15, arm_l=(1, 0), arm_r=(1, 0)),
        dict(oy=-112, sy=1.2, sx=0.86, puddle=0.95, shadow=0.0, arm_l=(1, 0), arm_r=(1, 0)),
        dict(oy=-150, puddle=0.75, shadow=0.0),
        dict(oy=-200, puddle=0.5, shadow=0.0),
    ]
    for k, kw in enumerate(keys):
        pose = rest_ghost(**kw)
        if k >= 4:
            pose["clip_below"] = 0.0
        out.append(pose)
    return out


def geist_buh():
    """0 Pfuetze, 1-4 schiesst hoch, 5 BUH (5-8 halten, zittert), 9-13 beruhigen."""
    out = []
    keys = [
        dict(oy=-200, puddle=0.6, shadow=0.0),
        dict(oy=-95, sy=1.32, sx=0.80, puddle=1.0, shadow=0.0, face="buh", arm_l=(1, 0), arm_r=(1, 0)),
        dict(oy=-48, sy=1.30, sx=0.82, puddle=0.9, shadow=0.3, face="buh", arm_l=(1, 0), arm_r=(1, 0), flutter=3),
        dict(oy=-12, sy=1.20, sx=0.88, puddle=0.6, shadow=0.7, face="buh", arm_l=(0.8, 0.3), arm_r=(0.8, 0.3), flutter=3),
        dict(oy=6, sy=1.10, sx=0.93, puddle=0.3, face="buh", arm_l=(0.5, 0.7), arm_r=(0.5, 0.7), hem_amp=8, flare=10),
        dict(oy=4, sy=0.94, sx=1.13, face="buh", arm_l=(0.25, 1.0), arm_r=(0.25, 1.0), hem_amp=9, flare=15, flutter=3),
        dict(oy=4, sy=0.96, sx=1.12, face="buh", arm_l=(0.3, 1.0), arm_r=(0.2, 1.0), hem_amp=9, flare=15, flutter=-3, shake=(1, 0)),
        dict(oy=4, sy=0.95, sx=1.13, face="buh", arm_l=(0.2, 1.0), arm_r=(0.3, 1.0), hem_amp=9, flare=15, flutter=3, shake=(-1, 1)),
        dict(oy=4, sy=0.96, sx=1.11, face="buh", arm_l=(0.3, 0.9), arm_r=(0.3, 0.9), hem_amp=9, flare=14, flutter=-3, shake=(1, 0)),
        dict(oy=3, sy=1.04, sx=0.98, face="fies", arm_l=(0.2, 0.5), arm_r=(0.2, 0.5), hem_amp=8, flare=10),
        dict(oy=1, sy=0.98, sx=1.02, face="fies", arm_l=(0.1, 0.25), arm_r=(0.1, 0.25), hem_amp=7, flare=8),
        dict(oy=0, sy=1.01, face="grin", arm_l=(0.1, 0.1), arm_r=(0.1, 0.1)),
        dict(oy=0, sy=1.0, face="grin"),
        dict(oy=0, sy=1.0, face="grin"),
    ]
    for k, kw in enumerate(keys):
        pose = rest_ghost(**kw)
        if k <= 3:
            pose["clip_below"] = 0.0
        out.append(pose)
    return out


def geist_reigen():
    """0-5 Arme hoch, 6-11 Wirbel (Schleife), 12-17 ausklingen."""
    out = []
    for k in range(18):
        if k < 6:
            e = smooth(0, 1, (k + 1) / 6)
            pose = rest_ghost(hover=4 * e, face="fies", arm_l=(0.08 + 0.9 * e, 0.1 * e), arm_r=(0.08 + 0.9 * e, 0.1 * e),
                              hem_phase=1.2 * e * e, rot=-3 * e, sy=1 + 0.04 * e, flutter=2 * e)
        elif k < 12:
            q = (k - 6) / 6
            ph = TAU * q
            pose = rest_ghost(hover=4 + 1.5 * math.sin(ph * 2), face="fies" if k % 2 else "grin",
                              arm_l=(0.95 + 0.05 * math.sin(ph), 0.15 + 0.1 * math.sin(ph)),
                              arm_r=(0.95 - 0.05 * math.sin(ph), 0.15 - 0.1 * math.sin(ph)),
                              hem_phase=1.2 + ph, rot=3 * math.sin(ph), flutter=3 * math.sin(ph * 2), hem_amp=8, flare=11)
        else:
            e = 1 - smooth(0, 1, (k - 11) / 6)
            pose = rest_ghost(hover=4 * e, face="grin", arm_l=(0.08 + 0.9 * e, 0.1 * e), arm_r=(0.08 + 0.9 * e, 0.1 * e),
                              hem_phase=1.2 * e, rot=2 * e * math.sin(k), flutter=2 * e, hem_amp=6 + 2 * e, flare=FLARE + 4 * e)
        out.append(pose)
    return out


def spring_track(x0, v0, target, k, d, frames):
    sp = Spring(k, d)
    sp.x, sp.v = x0, v0
    out = []
    dt = 1.0 / (FPS * 20)
    for i in range(frames * 20):
        v = sp.step(target, dt)
        if i % 20 == 0:
            out.append(v)
    return out


def enthuellung():
    """
     0- 7  zittert, schaut nervoes
     8-15  Beulen wandern unter dem Laken
    16-21  Tentakel kriechen unter dem Saum hervor und packen zu
    22-29  reissen das Laken hoch (Mitte haengt durch), es rafft sich ueber der Glocke
    30     Laken ist weg (fliegt als eigenes Objekt), Squiddy schnellt hoch
    30-37  federt nach
    38-43  Heiligenschein senkt sich, 40 PLING
    44-47  zufriedenes Wackeln
    """
    out = []
    sy_vals = spring_track(1.16, 0.0, 1.0, 90.0, 7.0, 18)
    halo_vals = spring_track(34.0, -40.0, 0.0, 70.0, 5.5, 10)
    for k in range(48):
        if k < 8:
            sh = ((1, 0), (-1, 0), (0, 1), (1, -1), (-1, 0), (0, 0), (1, 1), (-1, 0))[k]
            pose = rest_ghost(face="bang", shake=sh, eye_dx=(-1, 1)[k % 2], sy=1 - 0.02 * (k % 2),
                              hem_phase=0.15 * math.sin(k * 2.1), flutter=1.5 * math.sin(k * 1.7))
        elif k < 16:
            q = (k - 8) / 8
            bumps = (
                (-38, 52 - 18 * q, 7.5, 1.0 * math.sin(math.pi * min(1, q * 1.5))),
                (38, 30 + 22 * q, 8.0, 1.0 * math.sin(math.pi * max(0, min(1, q * 1.5 - 0.3)))),
                (-36, 30 + 8 * math.sin(q * 6), 6.5, 0.9 * math.sin(math.pi * max(0, min(1, q * 2 - 0.9)))),
            )
            pose = rest_ghost(face="bang" if k < 12 else "schock", bumps=bumps, shake=((1, 0), (0, 0), (-1, 0), (0, 1))[k % 4],
                              eye_dx=(-1.5 if k < 12 else 0.0), hem_phase=0.3 * math.sin(k), flutter=2.0 * math.sin(k * 1.3))
        elif k < 22:
            g = (k - 15) / 6
            pose = rest_ghost(face="schock", grab=g, t_show=[0, 1, 3, 4], t_len=0.72 + 0.15 * g, t_amp=0.6,
                              shake=((1, 0), (0, 0))[k % 2], hem_phase=0.2 * math.sin(k))
        elif k < 30:
            q = (k - 21) / 8
            lift = ease_out(0, 1, q) ** 1.2
            bundle = smooth(0.55, 1.0, lift)
            pose = default_pose(sheet=True, halo=False, face="schock", grab=1.0, grab_up=q,
                                lift=lift, sag=10 * math.sin(math.pi * min(1, lift * 1.3)), bundle=bundle,
                                bundle_x=9 * math.sin(q * TAU * 0.75), t_show=[0, 1, 3, 4], t_len=1.0, t_amp=0.3 + 0.4 * q,
                                t_splay=6 * q, t_drag=-0.3 * q, jelly_face="kneif",
                                hover=4 * q, sy=1 + 0.08 * q, pulse=-0.2 * q, hem_amp=5, flare=FLARE + 3 * q)
        else:
            j = k - 30
            sy = sy_vals[min(j, len(sy_vals) - 1)]
            face = "kneif" if j < 2 else "smug"
            halo = k >= 38
            hdy = halo_vals[min(k - 38, len(halo_vals) - 1)] if halo else 0.0
            glow = 0.0
            if 40 <= k <= 43:
                glow = (0.9, 1.0, 0.6, 0.3)[k - 40]
            pose = default_pose(sheet=False, halo=halo, halo_dy=hdy, halo_glow=glow, jelly_face=face,
                                sy=sy, sx=1 + (1 - sy) * 0.8, pulse=(sy - 1) * 2.5, hover=12 * (sy - 1),
                                t_phase=j / 12, t_amp=0.9, t_splay=4 * math.sin(j * 0.6), t_curl=1.0)
            if k >= 44:
                pose["t_phase"] = j / 8
                pose["rot"] = 2.5 * math.sin((k - 44) / 4 * TAU)
        out.append(pose)
    return out


def squiddy_nessel():
    """0-4 Tentakel hoch, Funken; 5 rammt sie in den Boden (5-9 halten); 10-13 zurueck."""
    out = []
    for k in range(14):
        if k < 5:
            e = smooth(0, 1, (k + 1) / 5)
            pose = default_pose(sheet=False, hover=9 * e, pulse=-0.5 * e, t_lift=0.85 * e, t_amp=0.6, t_curl=1 + 0.6 * e,
                                t_phase=k / 10, sparks=0.3 + 0.5 * e, spark_seed=k, jelly_face="glut" if k >= 2 else "smug",
                                glow=0.4 * e, sy=1 + 0.05 * e)
        elif k < 10:
            pose = default_pose(sheet=False, hover=-3 + (1 if k % 2 else 0), pulse=0.7, t_len=1.55, t_amp=0.08,
                                t_splay=22, t_curl=0.0, clip_tent=True, sparks=0.9, spark_seed=k, jelly_face="glut",
                                glow=0.8 if k == 5 else 0.5, sx=1.06, sy=0.94, shake=((1, 0), (0, 0))[k % 2])
        else:
            e = 1 - smooth(0, 1, (k - 9) / 4)
            pose = default_pose(sheet=False, hover=-3 * e, pulse=0.4 * e, t_len=1.0 + 0.4 * e, t_amp=0.3 + 0.6 * (1 - e),
                                t_splay=18 * e, t_curl=1 - e * 0.8, clip_tent=e > 0.5, sparks=0.3 * e, spark_seed=k,
                                jelly_face="smug" if k >= 12 else "glut", t_phase=k / 10)
        out.append(pose)
    return out


def squiddy_brut():
    """0-3 zieht sich zusammen, 4 PLOPP (Babys raus), 5-11 erholt sich."""
    out = []
    keys = [
        dict(pulse=0.3, t_curl=1.2, t_splay=-4, jelly_face="smug"),
        dict(pulse=0.65, t_curl=1.4, t_splay=-8, jelly_face="kneif", sy=1.03),
        dict(pulse=0.95, t_curl=1.6, t_splay=-10, jelly_face="kneif", sy=1.05, shake=(1, 0)),
        dict(pulse=1.1, t_curl=1.7, t_splay=-12, jelly_face="kneif", sy=1.06, shake=(-1, 0)),
        dict(pulse=-0.6, t_curl=0.4, t_splay=14, jelly_face="auf", hover=6, sy=0.92, sx=1.06, t_amp=1.2),
        dict(pulse=-0.4, t_curl=0.6, t_splay=10, jelly_face="auf", hover=5, sy=0.95, t_amp=1.1),
        dict(pulse=-0.15, t_curl=0.8, t_splay=5, jelly_face="smug", hover=3, t_amp=1.0),
        dict(pulse=0.1, t_curl=1.0, t_splay=1, jelly_face="smug", hover=1.5),
        dict(pulse=0.15, jelly_face="smug", hover=0.5),
        dict(pulse=0.05, jelly_face="smug"),
        dict(pulse=0.0, jelly_face="smug"),
        dict(pulse=0.0, jelly_face="smug"),
    ]
    for k, kw in enumerate(keys):
        kw.setdefault("t_phase", k / 12)
        out.append(default_pose(sheet=False, **kw))
    return out


def squiddy_spannung():
    """0-5 laedt (leuchtet auf), 6-11 halten (Schleife), 12 Entladung, 13-17 erholen."""
    out = []
    for k in range(18):
        if k < 6:
            e = smooth(0, 1, (k + 1) / 6)
            pose = default_pose(sheet=False, hover=10 * e, t_lift=0.55 * e, t_splay=8 * e, glow=e, sparks=0.3 + 0.7 * e,
                                spark_seed=k, jelly_face="glut" if k >= 2 else "smug", pulse=-0.3 * e, t_phase=k / 12,
                                halo_glow=0.5 * e)
        elif k < 12:
            pose = default_pose(sheet=False, hover=10 + (1 if k % 2 else -1), t_lift=0.58, t_splay=9, glow=1.0 + 0.2 * (k % 2),
                                sparks=1.2, spark_seed=k, jelly_face="glut", pulse=-0.3 + 0.1 * (k % 2),
                                t_phase=k / 12, t_amp=0.4, shake=((1, 0), (-1, 0))[k % 2], halo_glow=0.6)
        elif k == 12:
            pose = default_pose(sheet=False, hover=8, t_lift=1.0, t_splay=18, glow=2.2, sparks=2.0, spark_seed=k,
                                jelly_face="glut", pulse=-0.6, t_amp=0.1, t_curl=0.2, sx=1.08, sy=0.94, halo_glow=1.0)
        else:
            e = 1 - smooth(0, 1, (k - 12) / 5)
            pose = default_pose(sheet=False, hover=8 * e, t_lift=0.5 * e, t_splay=10 * e, glow=0.8 * e, sparks=0.6 * e,
                                spark_seed=k, jelly_face="smug" if k >= 16 else "glut", pulse=-0.3 * e, t_phase=k / 12)
        out.append(pose)
    return out


def squiddy_tod():
    """0-3 getroffen, 4-9 wird friedlich, 10-19 winkt mit einem Tentakel (Code: Aufstieg)."""
    out = []
    for k in range(20):
        if k < 4:
            pose = default_pose(sheet=False, jelly_face="aua", shake=((2, 0), (-2, 1), (1, -1), (-1, 0))[k],
                                pulse=(0.5, -0.4, 0.3, -0.1)[k], halo_tilt=-7 + (12, -10, 6, -3)[k], halo_dy=(3, -2, 1, 0)[k],
                                t_amp=1.4, t_phase=k / 8)
        elif k < 10:
            e = smooth(0, 1, (k - 3) / 6)
            pose = default_pose(sheet=False, jelly_face="frieden", halo_glow=0.6 * e, t_amp=1.0 - 0.6 * e,
                                t_curl=1 - 0.4 * e, pulse=0.1 * math.sin(k), t_phase=k / 14, hover=2 * e)
        else:
            q = (k - 10) / 10
            pose = default_pose(sheet=False, jelly_face="frieden", halo_glow=0.8 + 0.2 * math.sin(q * TAU * 2),
                                t_amp=0.4, t_curl=0.6, t_phase=k / 14, hover=2,
                                wave=(5, 150.0, q * 2.0), pulse=0.08 * math.sin(q * TAU))
        out.append(pose)
    return out


ANIMS = {
    "geist_schweben": lambda: geist_schweben(False),
    "geist_spaeher": lambda: geist_schweben(True),
    "geist_abtauchen": geist_abtauchen,
    "geist_buh": geist_buh,
    "geist_reigen": geist_reigen,
    "enthuellung": enthuellung,
    "schwimm": squiddy_schwimm,
    "nessel": squiddy_nessel,
    "brut": squiddy_brut,
    "spannung": squiddy_spannung,
    "tod": squiddy_tod,
}


# ================================================================ Speichern / Vorschau

def save(name, frames, w=CELL, h=CELL, pivot=None, out_dir=OUT_DIR):
    strip = np.concatenate(frames, axis=1)
    os.makedirs(out_dir, exist_ok=True)
    png = os.path.join(out_dir, name + ".png")
    Image.fromarray(strip).save(png)
    if pivot is None:
        pivot = (CX / w, (h - GROUND) / h)
    if not os.path.exists(png + ".meta"):
        write_strip_meta(png + ".meta", name, len(frames), w, h, PPU, pivot=pivot,
                         max_size=8192 if strip.shape[1] > 4096 else 4096 if strip.shape[1] > 2048 else 2048)
    print("%-28s %2d Bilder %dx%d" % (name, len(frames), w, h))


BG = (44, 40, 78, 255)


def preview(name, frames, folder, scale=3, fps=FPS):
    os.makedirs(folder, exist_ok=True)
    pics = []
    for fr in frames:
        im = Image.new("RGBA", fr.shape[1::-1], BG)
        im.alpha_composite(Image.fromarray(fr))
        pics.append(im.resize((im.width * scale, im.height * scale), Image.NEAREST).convert("P", palette=Image.ADAPTIVE))
    pics[0].save(os.path.join(folder, name + ".gif"), save_all=True, append_images=pics[1:],
                 duration=int(1000 / fps), loop=0)
    per = min(8, len(frames))
    rows = int(math.ceil(len(frames) / per))
    w, h = frames[0].shape[1], frames[0].shape[0]
    sheet = Image.new("RGBA", (w * per, h * rows), BG)
    for i, fr in enumerate(frames):
        sheet.alpha_composite(Image.fromarray(fr), ((i % per) * w, (i // per) * h))
    sheet.resize((sheet.width * 2, sheet.height * 2), Image.NEAREST).save(os.path.join(folder, name + "_sheet.png"))


def save_bestiary(frame):
    """Bestiarium/Levelauswahl zeigen das Gespenst - Squiddy bleibt die Ueberraschung."""
    solid = frame[..., 3] == 255
    solid &= ~np.all(frame[..., :3] == np.array(SHADOW[:3], np.uint8), -1)
    rows = np.where(solid.any(1))[0]
    cols = np.where(solid.any(0))[0]
    crop = frame[rows[0]:rows[-1] + 1, cols[0]:cols[-1] + 1].copy()
    crop[crop[..., 3] < 255] = 0
    Image.fromarray(crop).save(BESTIARY)
    if not os.path.exists(BESTIARY + ".meta"):
        import uuid
        src = open(os.path.join(os.path.dirname(BESTIARY), "EvilSlime.png.meta")).read()
        open(BESTIARY + ".meta", "w", newline=chr(10)).write(src.replace("cdfa7c35c19f454f813a4a6b1fe67fbc", uuid.uuid4().hex))
    print("Bestiarium", crop.shape[1], "x", crop.shape[0])


def main():
    args = sys.argv[1:]
    only = args[args.index("--nur") + 1].split(",") if "--nur" in args else None
    pv = args[args.index("--preview") + 1] if "--preview" in args else None
    write = "--kein-export" not in args
    for name, fn in ANIMS.items():
        if only and name not in only:
            continue
        poses = fn()
        frames = [draw(p, k) for k, p in enumerate(poses)]
        if write:
            save("squiddy_" + name, frames)
            if name == "geist_schweben":
                save_bestiary(frames[0])
        if pv:
            preview(name, frames, pv)


if __name__ == "__main__":
    main()
