"""
Der Eiskaiser (EnemyId.Eiskaiser, Endboss der Eiswelt): ein Kaiserpinguin aus
Eiscreme. Ruecken und Kopf Schokoeis, Bauch Vanilleeis mit Vanillepuenktchen,
Schnabel, Ohrflecken und Fuesse Mangosorbet, eine Waffelkrone mit Eisjuwelen
und ein Schokotaler-Orden auf der Brust.

Alle Posen kommen aus EINEM Modell (Koerper-Koordinaten u nach vorn, v nach
oben, Ursprung zwischen den Fuessen). Eine Pose dreht, staucht und verschiebt
das Modell; Flossen, Fuesse, Schnabel und Krone haben eigene Gelenke. Licht
aus der Feldnormalen, darum sitzt die Schattierung auch im Liegen richtig.

  Assets/Art/Gegner/new/boss/
    eiskaiser_walk.png       24 Bilder  watscheln (2 Schritte), 2. Schleife blinzelt
    eiskaiser_kick.png       10 Bilder  Kaiser-Kick: 0-3 ausholen (3 halten), 4 Tritt, 5-9 ausschwingen
    eiskaiser_cast.png       14 Bilder  Flossen hoch, Krone glueht (7-9 halten), 10 = Stampfer
    alle 128x128, PPU 32, 12 fps, Pivot 4 px ueber der Unterkante, Bild schaut nach rechts.
    eiskaiser_kristall.png    7 Bilder  16x28, Pivot unten: Kristall des Frostrings (0-3 wachsen, 4-6 funkeln)
    eiskaiser_eisblock.png   10 Bilder  40x52, Pivot unten: friert den Spieler ein
                                        (0-2 zufrieren, 3-6 halten, 7 Riss, 8-9 zerspringen)
    eiskaiser_kugel.png      24 Bilder  72x72, Pivot am Boden: Lawinenkugel, 7 Groessen x 4 Rollbilder
                                        (ab Groesse 2 stecken bunte Brocken geschluckter Gegner drin)
    eiskaiser_platzt.png      8 Bilder  112x112, Pivot Mitte: Lawinenkugel zerplatzt
  Assets/Resources/Bestiary/Eiskaiser.png

Aufruf aus dem Projektordner:
  python Tools/eiskaiser.py [--preview pfad.gif]
"""

import math
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from eis_gegner_kit import LIGHT, downsample, outline, put, ramp, rgb, stamp  # noqa: E402
from unity_meta import write_strip_meta  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "Assets", "Art", "Gegner", "new", "boss")
BESTIARY = os.path.join(ROOT, "Assets", "Resources", "Bestiary", "Eiskaiser.png")

CELL = 128
GROUND = CELL - 4
CX = 64.0
PPU = 32
FPS = 12
SS = 3


def pal(*h):
    return tuple(rgb(x) for x in h)


CHOC = pal("1a0b08", "2a1510", "3e2218", "553020", "6e432c", "98653f")
CAPE = pal("4a0c18", "7a1426", "a8203a", "d0344c", "ee6070", "ffb0b8")
CREAM = pal("9a8aa0", "c4bccc", "e4dcea", "f6f2fa", "ffffff", "ffffff")
VAN = pal("a3815a", "c8aa80", "e2cca2", "f4e6c8", "fff6e2", "ffffff")
MANGO = pal("7e3208", "b85814", "e07c22", "ff9e34", "ffc45e", "fff0b0")
WAF = pal("5e3216", "8c5424", "b97836", "d89a4a", "eebc6c", "f8dc9a")
GOLD = pal("6a4410", "a8741a", "d8a42a", "f4cc4a", "ffe78a", "fffbe0")
GEM = pal("1e3c7a", "2e66b4", "4a9ae0", "7ec8f6", "bfeaff", "ffffff")
ICE = pal("2c4a86", "4a78b8", "78aee0", "a8d6f2", "d8f2ff", "ffffff")

C = {
    "line": rgb("160a08"),
    "l_cream": rgb("4a3c5a"),
    "l_van": rgb("5a3e2a"),
    "l_mango": rgb("5a2006"),
    "l_waf": rgb("3a1c0a"),
    "l_gold": rgb("4a2c08"),
    "l_ice": rgb("1c2e5e"),
    "eye": rgb("0e0606"),
    "eglint": rgb("ffffff"),
    "eyew": rgb("fff8ec"),
    "brow": rgb("120806"),
    "maw": rgb("5a1420"),
    "tongue": rgb("e8607a"),
    "bean": rgb("5a3a24"),
    "ribbon": rgb("c42a3a"),
    "ribbon_d": rgb("7e1622"),
    "shadow": (64, 58, 110, 80),
    "snow": rgb("ffffff"),
    "snow_m": rgb("dfe4f6"),
    "snow_s": rgb("a9b0dc"),
    "star": rgb("ffe066"),
    "star_d": rgb("d89a1a"),
    "spark": rgb("ffffff"),
    "spark_b": rgb("8fdcff"),
}

# ================================================================ Modell (Koerper-Koordinaten)

BODY_C = (0.0, 37.0)
BODY_R = (27.0, 36.0)
HEAD_C = (6.0, 67.0)
HEAD_R = 19.0
EXP = 3.0
SHOULDER_F = (-3.0, 52.0)
SHOULDER_B = (-10.0, 53.0)
FLIP_L = 27.0
FLIP_W = 7.0
BEAK_BASE = (21.0, 67.0)
BEAK_L = 12.5
EYE_AT = (14.0, 73.0)
CROWN_AT = (4.0, 84.0)
MEDAL_AT = (15.0, 43.0)
FOOT_F = (7.0, 1.6)
FOOT_B = (-7.0, 1.4)

# Vanillepuenktchen im Bauch (u, v)
BEANS = []
# Rillen im Schokoeis (Mittelpunkt u, v, Laenge)
RIDGES = []
FROST = [(-8, 79), (-14, 72)]


def default_pose(**kw):
    p = dict(a=0.0, sx=1.0, sy=1.0, oy=0.0, anc=0.0, ground=True,
             ff=8.0, bf=8.0, fstep=0.0, flift=0.0, bstep=0.0, blift=0.0,
             beak=0.0, eye="normal", crown=0.0, glow=0.0, shadow=1.0,
             burst=None, blink=False, dx=0.0, cape=0.0, capeamp=1.0, swoosh=False)
    p.update(kw)
    return p


class Frame:
    """Eine Pose als Abbildung Welt <-> Koerper."""

    def __init__(self, p):
        self.p = p
        self.ca = math.cos(math.radians(p["a"]))
        self.sa = math.sin(math.radians(p["a"]))
        self.ox, self.oy = 0.0, 0.0
        # Erst ohne Versatz die Lage bestimmen, dann verankern
        if p["ground"]:
            ys = [self.to_world(u, v)[1] for u, v in self.hull()]
            self.oy = -min(ys) + p["oy"]
        else:
            self.oy = p["oy"]
        bx = self.to_world(BODY_C[0], BODY_C[1])[0]
        self.ox = -p["anc"] * bx + p["dx"]

    def hull(self):
        pts = []
        for k in range(48):
            t = 2 * math.pi * k / 48
            pts.append((BODY_C[0] + BODY_R[0] * math.cos(t), BODY_C[1] + BODY_R[1] * math.sin(t)))
            pts.append((HEAD_C[0] + HEAD_R * math.cos(t), HEAD_C[1] + HEAD_R * math.sin(t)))
        for fu, fv in (FOOT_F, FOOT_B):
            pts += [(fu - 8, fv - 1.5), (fu + 8, fv - 1.5)]
        return pts

    def to_world(self, u, v):
        u, v = u * self.p["sx"], v * self.p["sy"]
        x = u * self.ca + v * self.sa
        y = -u * self.sa + v * self.ca
        return x + self.ox, y + self.oy

    def to_local(self, X, Y):
        x, y = X - self.ox, Y - self.oy
        u = x * self.ca - y * self.sa
        v = x * self.sa + y * self.ca
        return u / self.p["sx"], v / self.p["sy"]

    def px(self, u, v):
        x, y = self.to_world(u, v)
        return int(math.floor(GROUND - y)), int(math.floor(CX + x))


def body_F(U, V):
    F1 = ((U - BODY_C[0]) / (BODY_R[0] * (1 + 0.10 * np.clip((BODY_C[1] - V) / BODY_R[1], -1, 1)))) ** 2 \
        + ((V - BODY_C[1]) / BODY_R[1]) ** 2
    F2 = ((U - HEAD_C[0]) ** 2 + (V - HEAD_C[1]) ** 2) / HEAD_R ** 2
    pot = 1 / np.maximum(F1, 1e-4) ** EXP + 1 / np.maximum(F2, 1e-4) ** EXP
    return pot ** (-1 / EXP)


def belly_mask(U, V):
    a = ((U - 10.0) / 19.0) ** 2 + ((V - 32.0) / 29.0) ** 2 <= 1.0
    b = ((U - 14.0) / 9.5) ** 2 + ((V - 54.0) / 11.0) ** 2 <= 1.0
    return a | b


def patch_mask(U, V):
    # Ohrfleck: schraeger Tropfen hinter dem Auge, laeuft zum Hals
    uu, vv = U - 9.5, V - 60.0
    r = math.radians(25)
    x = uu * math.cos(r) + vv * math.sin(r)
    y = -uu * math.sin(r) + vv * math.cos(r)
    return (x / 4.2) ** 2 + (y / (8.5 - 0.25 * np.clip(y, -8, 8))) ** 2 <= 1.0


def flipper(U, V, shoulder, phi):
    """Flosse: Paddel vom Schultergelenk, phi = 0 haengt, + nach vorn/oben."""
    r = math.radians(phi)
    d = (math.sin(r), -math.cos(r))
    n = (d[1], -d[0])
    du, dv = U - shoulder[0], V - shoulder[1]
    s = du * d[0] + dv * d[1]
    t = du * n[0] + dv * n[1]
    w = FLIP_W * np.clip(1 - np.abs(s / FLIP_L) ** 2.2, 0, 1) ** 0.5 * (0.75 + 0.25 * np.clip(s / 6, 0, 1))
    m = (s >= -2.5) & (s <= FLIP_L) & (np.abs(t + 0.8 * np.clip(s / FLIP_L, 0, 1) ** 2) <= w)
    return m, s, t


def foot(U, V, base, step, lift):
    fu, fv = base[0] + step, base[1] + lift
    du, dv = U - fu, V - fv
    m = ((du / 8.0) ** 2 + (dv / 2.6) ** 2 <= 1.0) & (dv > -2.6)
    # drei Zehen vorn
    for k, off in enumerate((-1.6, 0.0, 1.6)):
        m |= ((du - 7.2) ** 2 + (dv - off * 0.7) ** 2 <= 1.6 ** 2) & (dv > -2.4)
    return m


def beak(U, V, open_):
    """Ober- und Unterschnabel; open_ 0..1 klappt den unteren auf."""
    bu, bv = BEAK_BASE
    r0 = math.radians(-20)
    d = (math.cos(r0), math.sin(r0))
    n = (-d[1], d[0])
    du, dv = U - bu, V - bv
    s = du * d[0] + dv * d[1]
    t = du * n[0] + dv * n[1]
    upper = (s >= -2) & (s <= BEAK_L) & (t >= -0.3) & (t <= 4.6 * np.clip(1 - s / BEAK_L, 0, None) ** 0.75 + 0.3)
    ro = r0 - math.radians(34 * open_)
    d2 = (math.cos(ro), math.sin(ro))
    n2 = (-d2[1], d2[0])
    s2 = du * d2[0] + dv * d2[1]
    t2 = du * n2[0] + dv * n2[1]
    lower = (s2 >= -2) & (s2 <= BEAK_L * 0.86) & (t2 <= 0.3) & (t2 >= -3.4 * np.clip(1 - s2 / (BEAK_L * 0.86), 0, None) ** 0.8 - 0.2)
    mouth = np.zeros_like(upper)
    if open_ > 0.05:
        ang = np.arctan2(dv, du)
        rr = np.sqrt(du * du + dv * dv)
        mouth = (rr <= BEAK_L * 0.8) & (ang <= r0 + 0.02) & (ang >= ro - 0.02) & ~lower & ~upper
    return upper, lower, mouth


def crown(U, V, tilt):
    """Waffelkrone: Band + fuenf Zacken. Gibt Maske, Band-Maske und Zackenspitzen zurueck."""
    cu, cv = CROWN_AT
    r = math.radians(tilt)
    du, dv = U - cu, V - cv
    x = du * math.cos(r) + dv * math.sin(r)
    y = -du * math.sin(r) + dv * math.cos(r)
    band = (np.abs(x) <= 14.0 - 0.05 * y * y) & (y >= -1.5) & (y <= 5.5)
    spikes = np.zeros_like(band)
    tips = []
    for c, h in ((-11.5, 13.0), (-6.0, 16.0), (0.0, 20.0), (6.0, 16.0), (11.5, 13.0)):
        w = 3.1 * np.clip(1 - (y - 4.5) / (h - 4.5), 0, 1)
        spikes |= (y >= 4.5) & (y <= h) & (np.abs(x - c) <= w)
        tips.append((c, h))
    tip_world = [(cu + c * math.cos(r) - h * math.sin(r), cv + c * math.sin(r) + h * math.cos(r)) for c, h in tips]
    return band | spikes, band, x, y, tip_world


def back_edge(V):
    t = np.clip((V - BODY_C[1]) / BODY_R[1], -1, 1)
    return -BODY_R[0] * (1 + 0.10 * np.clip(-t, -1, 1)) * np.sqrt(np.clip(1 - t * t, 0, 1))


def cape_mask(U, V, ph, amp):
    """Umhang aus Erdbeersosse: haengt vom Nacken den Ruecken hinab, wird unten weit."""
    top, bot = 60.0, 5.0 + 1.2 * np.sin(U * 0.55 + ph)
    k = np.clip((top - V) / (top - bot), 0, 1)
    outer = back_edge(V) - (2.5 + 9.5 * k ** 1.1) - amp * k * (1.2 + np.sin(V * 0.22 + ph * 1.0))
    return (V <= top) & (V >= bot) & (U >= outer) & (U <= 4.0)


def cape_trim(U, V, ph, amp):
    """Hermelin aus Schlagsahne an der Unterkante des Umhangs."""
    m = np.zeros(U.shape, bool)
    for i in range(7):
        vb = 5.5 + 1.2 * math.sin(-4 - 2.4 * i + ph)
        ub = -6 - 2.6 * i
        k = 1.0
        ub = ub - amp * 0.3
        m |= (U - ub) ** 2 + (V - vb) ** 2 <= 2.4 ** 2
    return m & cape_mask(U, V - 1.5, ph, amp + 0.5) | m & (V < 9)


def collar(U, V):
    """Kragen: Sahnewuelste um den Nacken, ueber die Schultern."""
    m = np.zeros(U.shape, bool)
    for i in range(6):
        t = i / 5
        vv = 61.0 - 7.0 * t
        uu = float(back_edge(np.array(vv))) + 1.5 + 17.0 * t ** 1.3
        m |= (U - uu) ** 2 + (V - vv) ** 2 <= (3.6 - 0.6 * t) ** 2
    return m


def medal(U, V):
    mu, mv = MEDAL_AT
    coin = (U - mu) ** 2 + (V - mv) ** 2 <= 4.2 ** 2
    ribbon = (np.abs(U - mu) <= 2.2) & (V > mv + 3.0) & (V <= mv + 8.5)
    return coin, ribbon


# ================================================================ Rendern

(EMPTY, SHADOW, BFLIP, BFOOT, BODY, BELLY, PATCH, BEAKU, BEAKL, MOUTH,
 COIN, RIBBON, FFOOT, FFLIP, CROWN, BAND, CAPEC, TRIM, COLLAR) = range(19)


def render(fr):
    p = fr.p
    ys, xs = np.mgrid[0:CELL * SS, 0:CELL * SS]
    X = (xs + 0.5) / SS - CX
    Y = GROUND - (ys + 0.5) / SS
    U, V = fr.to_local(X, Y)
    owner = np.zeros(X.shape, np.int16)

    # Schatten auf dem Boden
    cxw = fr.to_world(BODY_C[0], BODY_C[1])[0]
    lying = abs(math.sin(math.radians(p["a"])))
    sw = (24 + 26 * lying) * p["shadow"]
    if sw > 2:
        owner[((X - cxw) / sw) ** 2 + ((Y + 0.5) / 4.0) ** 2 <= 1.0] = SHADOW

    cape = cape_mask(U, V, p["cape"], p["capeamp"])
    owner[cape] = CAPEC
    owner[cape_trim(U, V, p["cape"], p["capeamp"])] = TRIM
    bf, _, _ = flipper(U, V, SHOULDER_B, p["bf"])
    owner[bf] = BFLIP
    owner[foot(U, V, FOOT_B, p["bstep"], p["blift"])] = BFOOT
    F = body_F(U, V)
    body = F <= 1.0
    owner[body] = BODY
    owner[body & belly_mask(U, V)] = BELLY
    owner[body & patch_mask(U, V) & ~belly_mask(U, V) | body & patch_mask(U, V) & (V > 52)] = PATCH
    up, lo, mouth = beak(U, V, p["beak"])
    owner[mouth] = MOUTH
    owner[lo] = BEAKL
    owner[up] = BEAKU
    owner[collar(U, V)] = COLLAR
    coin, ribbon = medal(U, V)
    owner[ribbon & body] = RIBBON
    owner[coin & body] = COIN
    owner[foot(U, V, FOOT_F, p["fstep"], p["flift"])] = FFOOT
    ff, _, _ = flipper(U, V, SHOULDER_F, p["ff"])
    owner[ff] = FFLIP
    cm, band, _, _, _ = crown(U, V, p["crown"])
    owner[cm] = CROWN
    owner[band] = BAND
    owner[(Y < 0) & (owner != SHADOW)] = EMPTY
    return downsample(owner, CELL, CELL, SS, keep_thin=(RIBBON,))


def tone4(l, pal_, hi=False):
    """Vier klare Toene statt sechs - ruhigere Flaechen. hi: hellster Ton erlaubt."""
    if hi and l > 0.80:
        return pal_[4]
    if l > 0.62:
        return pal_[4] if not hi else pal_[3]
    if l > 0.30:
        return pal_[3] if not hi else pal_[2]
    if l > -0.05:
        return pal_[2] if not hi else pal_[1]
    return pal_[1]


def cleanup(img, pix, classes):
    """Einzelne verirrte Pixel (kein Nachbar gleicher Farbe) bekommen die Farbe der Mehrheit."""
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
            same = [n for k, n in enumerate(nb) if pix[r + ((1, -1, 0, 0)[k]), c + ((0, 0, 1, -1)[k])] == pix[r, c]]
            if same:
                out[r, c] = max(set(same), key=same.count)
    return out


def sphere_light(nx, ny):
    nz = math.sqrt(max(0.0, 1 - min(1.0, nx * nx + ny * ny)))
    n = np.array([nx, ny, nz + 0.1])
    return float(n @ LIGHT / np.linalg.norm(n))


def shade(pix, fr):
    p = fr.p
    img = np.zeros((CELL, CELL, 4), np.uint8)
    ys, xs = np.mgrid[0:CELL, 0:CELL]
    X = xs + 0.5 - CX
    Y = GROUND - (ys + 0.5)
    U, V = fr.to_local(X, Y)
    # Licht des Koerpers aus dem Feld (Normale in Welt-Koordinaten)
    e = 0.6
    F = body_F(U, V)

    def Fw(dx, dy):
        u2, v2 = fr.to_local(X + dx, Y + dy)
        return body_F(u2, v2)

    gx = (Fw(e, 0) - Fw(-e, 0)) / (2 * e)
    gy = (Fw(0, e) - Fw(0, -e)) / (2 * e)
    z = np.sqrt(np.clip(1 - F, 0, 1))
    nx, ny, nz = gx * 22, gy * 22, 2 * z
    ln = np.sqrt(nx * nx + ny * ny + nz * nz) + 1e-6
    lam = (nx * LIGHT[0] + ny * LIGHT[1] + nz * LIGHT[2]) / ln

    ca, sa = fr.ca, fr.sa
    for r in range(CELL):
        for c in range(CELL):
            m = pix[r, c]
            if m == EMPTY:
                continue
            u, v = U[r, c], V[r, c]
            if m == SHADOW:
                img[r, c] = C["shadow"]
            elif m in (BODY, BELLY, PATCH):
                l = lam[r, c]
                if m == BODY:
                    col = tone4(l, CHOC)
                    for ru, rv, ln_ in RIDGES:                      # Portionierer-Rillen
                        if abs(v - rv - 0.25 * (u - ru)) < 0.6 and abs(u - ru) < ln_ / 2:
                            col = CHOC[1] if l < 0.5 else CHOC[2]
                elif m == BELLY:
                    col = VAN[4] if l > 0.55 else VAN[3] if l > 0.2 else VAN[2] if l > -0.15 else VAN[1]
                else:
                    col = ramp(l + 0.1, MANGO, cuts=(0.90, 0.66, 0.38, 0.05))
                img[r, c] = col
            elif m in (BFLIP, FFLIP):
                sh = SHOULDER_F if m == FFLIP else SHOULDER_B
                phi = p["ff"] if m == FFLIP else p["bf"]
                _, s, t = flipper(np.array(u), np.array(v), sh, phi)
                s, t = float(s), float(t)
                # Flosse: Schoko oben, Vanille-Kante an der Unterseite (Kaiserpinguin)
                l = 0.55 - 0.08 * t
                rr = math.radians(phi)
                # Weltrichtung der Flossenflaeche: leicht dem Licht zu
                l += 0.15 * (math.cos(rr) * ca)
                if m == BFLIP:
                    l -= 0.35
                col = CHOC[3] if (l > 0.45 and m == FFLIP) else CHOC[2] if m == FFLIP else CHOC[1]
                img[r, c] = col
            elif m in (FFOOT, BFOOT):
                l = 0.6 if m == FFOOT else 0.2
                img[r, c] = ramp(l + (0.15 if v > 2.5 else 0), MANGO)
            elif m in (BEAKU, BEAKL):
                bu, bv = BEAK_BASE
                sl = (u - bu) / BEAK_L
                l = 0.75 - 0.4 * sl if m == BEAKU else 0.3 - 0.2 * sl
                col = ramp(l, MANGO)
                if m == BEAKU and v > bv + 1.6 - 2.4 * sl:
                    col = MANGO[4] if sl < 0.6 else MANGO[3]
                img[r, c] = col
            elif m == MOUTH:
                img[r, c] = C["maw"] if v > BEAK_BASE[1] - 3.2 else C["tongue"]
            elif m == CAPEC:
                # Sosse: glaenzend, Falten laufen senkrecht
                fold = math.sin(u * 0.42 + 0.12 * v + p["cape"])
                col = CAPE[3] if fold > 0.35 else (CAPE[2] if fold > -0.45 else CAPE[1])
                img[r, c] = col
            elif m in (TRIM, COLLAR):
                img[r, c] = CREAM[3] if (lam[r, c] > 0.2 or m == TRIM and v > 6) else CREAM[2]
            elif m == COIN:
                du, dv = (u - MEDAL_AT[0]) / 4.2, (v - MEDAL_AT[1]) / 4.2
                l = sphere_light(du * ca + dv * sa, -du * sa + dv * ca)
                col = ramp(l, GOLD)
                if 1.2 < math.hypot(u - MEDAL_AT[0], v - MEDAL_AT[1]) < 2.4 and l < 0.8:
                    col = GOLD[2]
                img[r, c] = col
            elif m == RIBBON:
                img[r, c] = C["ribbon"] if u < MEDAL_AT[0] else C["ribbon_d"]
            elif m in (CROWN, BAND):
                _, _, x, y, _ = crown(np.array(u), np.array(v), p["crown"])
                x, y = float(x), float(y)
                if m == BAND:
                    col = GOLD[3] if x < 3 else GOLD[2]
                    if abs(((x + 2.0) % 4.0) - 2.0) < 0.5:
                        col = GOLD[1] if x > 3 else GOLD[2]
                    if y > 4.6:
                        col = GOLD[4]
                else:
                    # Zacke: Licht von links, Kante zur naechsten Zacke dunkler
                    cx_ = round(x / 5.75) * 5.75
                    col = GOLD[4] if x < cx_ - 0.4 else (GOLD[3] if x < cx_ + 0.8 else GOLD[2])
                img[r, c] = col

    lines = {BODY: C["line"], BELLY: C["l_van"], PATCH: C["line"], BFLIP: C["line"], FFLIP: C["line"],
             BFOOT: C["l_mango"], FFOOT: C["l_mango"], BEAKU: C["l_mango"], BEAKL: C["l_mango"],
             MOUTH: C["l_mango"], COIN: C["l_gold"], RIBBON: C["ribbon_d"], CROWN: C["l_gold"], BAND: C["l_gold"],
             CAPEC: C["line"], TRIM: C["l_cream"], COLLAR: C["l_cream"]}
    out = outline(img, pix, lambda m: lines.get(m), empty=(EMPTY, SHADOW))

    # Innenkanten: Teile, die vor dem Koerper liegen, bekommen einen Strich
    def edge(cls, against, col):
        for r in range(1, CELL - 1):
            for c in range(1, CELL - 1):
                if pix[r, c] == cls and any(pix[r + a, c + b] in against for a, b in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                    out[r, c] = col

    edge(FFLIP, (BODY, BELLY, PATCH, COIN, RIBBON, BFLIP), C["line"])
    edge(BEAKU, (BODY, BELLY, PATCH, MOUTH), C["l_mango"])
    edge(BEAKL, (BODY, BELLY, PATCH, MOUTH, BEAKU), C["l_mango"])
    edge(FFOOT, (BODY, BELLY, BFOOT), C["l_mango"])
    edge(CROWN, (BODY, PATCH), C["l_gold"])
    edge(BAND, (BODY, PATCH), C["l_gold"])
    edge(COIN, (BELLY, BODY, RIBBON), C["l_gold"])
    edge(COLLAR, (BODY, BELLY, PATCH, CAPEC, BFLIP), C["l_cream"])
    edge(BODY, (CAPEC,), C["line"])
    edge(TRIM, (CAPEC,), C["l_cream"])
    out = cleanup(out, pix, (BODY, BELLY, PATCH, CAPEC, TRIM, COLLAR, FFLIP, BFLIP, CROWN, BAND))

    # Vanillepuenktchen, Reif
    for bu, bv in BEANS:
        r, c = fr.px(bu, bv)
        if 0 <= r < CELL and 0 <= c < CELL and pix[r, c] == BELLY and tuple(out[r, c]) != C["l_van"]:
            out[r, c] = C["bean"]
    for k, (fu, fv) in enumerate(FROST):
        r, c = fr.px(fu, fv)
        if 0 <= r < CELL and 0 <= c < CELL and pix[r, c] == BODY and tuple(out[r, c]) != C["line"]:
            out[r, c] = CHOC[5] if k % 2 else VAN[4]
    return out


def gems(img, pix, fr):
    """Eisjuwelen auf den Zackenspitzen und im Band; glow 0..1 laesst sie strahlen."""
    p = fr.p
    _, _, _, _, tips = crown(np.zeros(1), np.zeros(1), p["crown"])
    g = p["glow"]
    for k, (u, v) in enumerate(tips):
        if k != 2:
            continue                                   # nur die Mittelzacke traegt den grossen Stein
        r, c = fr.px(u, v - 5.0)
        rows = (".o.",
                "oHo",
                "oMo",
                ".o.")
        stamp(img, rows, r - 1, c - 1, {"o": C["l_ice"], "H": GEM[5] if g > 0.3 else GEM[4],
                                         "M": GEM[3 + int(2 * g)] if g < 0.9 else GEM[5]})
    # Band: drei kleine Steine
    for cu in (-7.0, 0.0, 7.0):
        ru, rv = CROWN_AT[0] + cu, CROWN_AT[1] + 2.0
        r, c = fr.px(ru, rv)
        col = GEM[4] if g < 0.6 else GEM[5]
        put(img, r, c, col)
        put(img, r, c + 1, GEM[2] if g < 0.6 else GEM[4])
    if g > 0.5:
        # Funkelkreuze auf den Zackenspitzen
        for k, (u, v) in enumerate(tips):
            r, c = fr.px(u, v + 1.5)
            n = 2 if k == 2 else 1
            if g > 0.9:
                n += 1
            for d in range(1, n + 1):
                for a, b in ((d, 0), (-d, 0), (0, d), (0, -d)):
                    if 0 <= r + a < CELL and 0 <= c + b < CELL and img[r + a, c + b, 3] == 0:
                        img[r + a, c + b] = C["spark"] if d == 1 else C["spark_b"]
    if g > 0.05:
        # Strahlen um die Krone: ein Kranz Funkel, der mit glow waechst
        cu, cv = CROWN_AT
        r0, c0 = fr.px(cu, cv + 8)
        n = 6
        rad = 8 + 10 * g
        for k in range(n):
            t = 2 * math.pi * k / n + g * 2.1
            r = int(round(r0 - math.sin(t) * rad * 0.6))
            c = int(round(c0 + math.cos(t) * rad))
            if 0 <= r < CELL and 0 <= c < CELL and img[r, c, 3] == 0:
                put(img, r, c, C["spark"] if k % 2 else C["spark_b"])
                if g > 0.6:
                    for a, b in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                        if img[r + a, c + b, 3] == 0:
                            put(img, r + a, c + b, C["spark_b"])


EYES = {
    # Auge schaut nach rechts: Braue faellt zum Schnabel hin (streng)
    "normal": ("bb......",
               ".bbbb...",
               "...bbbb.",
               "..wWWWw.",
               ".wW##gW.",
               ".W###gW.",
               ".W####W.",
               "..WWWW.."),
    "zu":     ("bb......",
               ".bbbb...",
               "...bbbb.",
               "........",
               "........",
               ".######.",
               "..####..",
               "........"),
    "wild":   ("b.......",
               ".bbb....",
               "...bbbbb",
               ".wWWWWw.",
               "wW##gWW.",
               "W###gWW.",
               "W####WW.",
               ".WWWWW.."),
    "dizzy":  ("........",
               "........",
               ".#....#.",
               "..#..#..",
               "...##...",
               "..#..#..",
               ".#....#.",
               "........"),
    "wut":    ("bbb.....",
               ".bbbbb..",
               "...bbbbb",
               "....bWW.",
               ".wW##gW.",
               ".W###gW.",
               ".W####W.",
               "..WWWW.."),
}


def eye(img, pix, fr):
    p = fr.p
    kind = "zu" if p["blink"] else p["eye"]
    r, c = fr.px(EYE_AT[0], EYE_AT[1])
    rows = EYES[kind]
    cols = {"#": C["eye"], "g": C["eglint"], "w": C["eyew"], "W": C["eyew"], "b": C["brow"]}
    stamp(img, rows, r - 4, c - 4, cols, pix=pix, only=(BODY, PATCH, BELLY))


# ================================================================ Effekte im Bild

def burst(img, fr, k):
    """Stampfer: Schnee- und Eisbrocken stieben zu beiden Seiten."""
    b = fr.p["burst"]
    if b is None:
        return
    for side in (-1, 1):
        for i, (vx, vy) in enumerate(((6, 8), (10, 5), (3, 11), (13, 3))):
            x = side * (20 + vx * b * 1.6)
            y = vy * b * 2.2 - 9 * b * b
            if y < 0:
                continue
            r, c = int(round(GROUND - 1 - y)), int(round(CX + x))
            put(img, r, c, C["snow"] if i % 2 else C["spark_b"])
            put(img, r, c + side, C["snow_m"])
    # Bodenring
    w = int(22 + 18 * b)
    for dx in range(-w, w + 1):
        if abs(dx) > w - 6 and (dx + k) % 2 == 0:
            put(img, GROUND - 1, int(CX + dx), C["snow"])


# ================================================================ Animationen

def walk_poses():
    out = []
    for loop in range(2):
        for k in range(12):
            ph = 2 * math.pi * k / 12
            s = math.sin(ph)
            out.append(default_pose(
                a=4.0 * s, oy=abs(math.sin(ph)) * 1.6,
                fstep=4.5 * s, flift=max(0.0, 3.0 * s), bstep=-4.5 * s, blift=max(0.0, -3.0 * s),
                ff=14 + 12 * math.sin(ph + 0.6), bf=14 - 12 * math.sin(ph + 0.6),
                crown=-3.0 * math.cos(ph), blink=(loop == 1 and k in (7, 8)), cape=ph))
    return out


def cast_poses():
    out = []
    for k in range(7):                                         # Flossen hoch, Krone laedt
        t = (k + 1) / 7
        e = t * t * (3 - 2 * t)
        out.append(default_pose(a=-6 * e, sy=1 + 0.05 * e, ff=10 - 150 * e, bf=10 - 168 * e,
                                glow=e, eye="wut", beak=0.6 * max(0, (k - 4) / 2), crown=2 * e))
    for k in range(3):                                         # halten (Code kann hier warten)
        out.append(default_pose(a=-6 + 0.8 * (k % 2), sy=1.05 + 0.01 * (k % 2), ff=-140 - 5 * (k % 2), bf=-158 + 4 * (k % 2),
                                glow=1.0, eye="wut", beak=0.7, crown=2))
    out.append(default_pose(a=5, sy=0.86, sx=1.06, ff=-15, bf=-15, glow=0.5, eye="wut", beak=1.0, burst=0.25, crown=-5))
    out.append(default_pose(a=3, sy=0.94, ff=-5, bf=-5, glow=0.2, eye="wut", beak=0.6, burst=0.6, crown=4))
    out.append(default_pose(a=1, sy=1.02, ff=5, bf=5, eye="normal", beak=0.2, burst=0.95, crown=-2))
    out.append(default_pose(a=0, ff=8, bf=8))
    return out


def swoosh(img, fr):
    """Tritt-Schwung: weisser Bogen vor dem Fuss (nur im Kick-Bild)."""
    if not fr.p.get("swoosh"):
        return
    r0, c0 = fr.px(FOOT_F[0] + fr.p["fstep"], FOOT_F[1] + fr.p["flift"])
    for i in range(14):
        a = math.radians(-70 + i * 10)
        rr = int(round(r0 - 3 - math.sin(a) * 11))
        cc = int(round(c0 + math.cos(a) * 11))
        put(img, rr, cc, C["snow"] if i % 3 else C["spark_b"])
        if 3 < i < 11:
            put(img, rr, cc - 1, C["snow_m"])


def draw(pose, k):
    fr = Frame(pose)
    pix = render(fr)
    img = shade(pix, fr)
    eye(img, pix, fr)
    gems(img, pix, fr)
    swoosh(img, fr)
    burst(img, fr, pose["burst"] and int(pose["burst"] * 10) or 0)
    return img


# ================================================================ Kaiser-Kick + Lawinenkugel

def kick_poses():
    """0-3 Ausholen (3 = halten), 4 = Tritt, 5-9 ausschwingen."""
    keys = [
        dict(a=-3, ff=30, bf=-20, crown=1),
        dict(a=-7, ff=50, bf=-35, fstep=-4, flift=2, crown=3, cape=0.6),
        dict(a=-10, sy=1.02, ff=60, bf=-45, fstep=-8, flift=4, eye="wut", crown=5, cape=1.2),
        dict(a=-12, sy=1.03, ff=66, bf=-50, fstep=-10, flift=6, eye="wut", beak=0.3, crown=6, cape=1.6),
        dict(a=10, sx=1.04, ff=-30, bf=40, fstep=16, flift=11, eye="wild", beak=1.0, crown=-6, cape=3.0, swoosh=True),
        dict(a=8, ff=-20, bf=30, fstep=14, flift=7, eye="wild", beak=0.8, crown=-4, cape=3.6),
        dict(a=4, ff=0, bf=15, fstep=8, flift=3, beak=0.4, crown=-1, cape=4.2),
        dict(a=1, ff=8, bf=8, fstep=3, flift=1, crown=1, cape=4.8),
        dict(a=0, ff=8, bf=8, cape=5.4),
        dict(a=0, ff=8, bf=8, cape=6.0),
    ]
    return [default_pose(**k) for k in keys]


KUGEL_R = [8, 11, 14, 18, 22, 27, 32]      # Radius je Groesse (px)
KUGEL_CELL = 72
KUGEL_GROUND = 69
KUGEL_FRAMES = 4
SNOW = pal("6e6aa0", "8f8cc0", "b4b4de", "d6d8f0", "eef0fb", "ffffff")
BITS = [rgb("6acdb0"), rgb("e23a42"), rgb("fbd648"), rgb("d89a4a"), rgb("f798ba")]


def kugel_lumps():
    """Klumpen auf der Kugel (Breite, Laenge) - viermal gleich, damit 4 Bilder je 90 Grad nahtlos rollen."""
    rng = np.random.RandomState(4)
    base = [(rng.uniform(-1.1, 1.1), rng.uniform(0, math.pi / 2), rng.randint(0, 5), rng.uniform(0, 1)) for _ in range(7)]
    out = []
    for q in range(4):
        for lat, lon, col, kind in base:
            out.append((lat, lon + q * math.pi / 2, col, kind))
    return out


LUMPS = kugel_lumps()


# Brocken geschluckter Gegner: kleine Stempel (o = Umriss)
BITS_STAMPS = [
    ("mM", "Mm"),                              # Minzkugel-Stueck
    ("rr", "yy"),                              # Raketeneis-Schichten
    ("Ww", "wd"),                              # Waffelecke
    ("Pp", "pp"),                              # Softeis-Tupfer
]
BIT_COLS = {"m": rgb("43ad96"), "M": rgb("9be6ca"), "r": rgb("e23a42"), "y": rgb("fbd648"),
            "w": rgb("c98a3e"), "W": rgb("f0c070"), "d": rgb("8c5424"), "p": rgb("e66e9c"), "P": rgb("ffc4d8")}


def kugel(size, k):
    r = KUGEL_R[size]
    W = H = KUGEL_CELL
    cx, cy = W / 2, KUGEL_GROUND - r
    img = np.zeros((H, W, 4), np.uint8)
    for row in range(H):
        for c in range(W):
            if ((c + 0.5 - cx) / (r * 1.05)) ** 2 + ((row + 0.5 - KUGEL_GROUND) / max(2.5, r * 0.22)) ** 2 <= 1:
                img[row, c] = C["shadow"]
    roll = k * (math.pi / 2) / KUGEL_FRAMES
    # Klumpige Kontur: der Rand wellt sich, und die Wellen rollen mit
    solid = np.zeros((H, W), bool)
    for row in range(H):
        for c in range(W):
            dx, dy = c + 0.5 - cx, cy - (row + 0.5)
            ang = math.atan2(dy, dx)
            bump = 0.9 * (0.5 + 0.5 * math.sin(ang * 7 + roll * 4)) + 0.5 * math.sin(ang * 11 - roll * 4)
            rr = r - 0.6 + 0.6 * bump * min(1.0, r / 12)
            if dx * dx + dy * dy > rr * rr:
                continue
            nx, ny = dx / r, dy / r
            nz = math.sqrt(max(0.0, 1 - nx * nx - ny * ny))
            l = float(np.array([nx, ny, nz]) @ LIGHT)
            img[row, c] = SNOW[4] if l > 0.72 else SNOW[3] if l > 0.42 else SNOW[2] if l > 0.1 else SNOW[1]
            solid[row, c] = True
    # Schneeklumpen: helle Kuppe, darunter Schattensichel - wandern beim Rollen nach unten
    for lat, lon, col, kind in LUMPS:
        a = lon + roll
        x = math.sin(lat) * 0.85
        y = math.cos(a) * math.cos(lat)
        z = math.sin(a) * math.cos(lat)
        if z < 0.3:
            continue
        c = int(math.floor(cx + x * r * 0.9))
        row = int(math.floor(cy - y * r * 0.9))
        if size >= 2 and kind > 0.62 - 0.07 * size:
            st = BITS_STAMPS[col % len(BITS_STAMPS)]
            sc = 2 if size >= 4 else 1                     # in grossen Kugeln groessere Brocken
            for dr, line in enumerate(st):
                for dc, ch in enumerate(line):
                    for er in range(sc):
                        for ec in range(sc):
                            rr2, cc2 = row + dr * sc + er, c + dc * sc + ec
                            if ch != "." and 0 <= rr2 < H and 0 <= cc2 < W and solid[rr2, cc2]:
                                img[rr2, cc2] = BIT_COLS[ch]
            continue
        big = r >= 13
        cells = [(0, 0, SNOW[5]), (0, 1, SNOW[5] if big else SNOW[4]), (1, 0, SNOW[2]), (1, 1, SNOW[1])]
        if big:
            cells.append((-1, 0, SNOW[4]))
        for dr, dc, col_ in cells:
            if 0 <= row + dr < H and 0 <= c + dc < W and solid[row + dr, c + dc]:
                img[row + dr, c + dc] = col_
    out = img.copy()
    for row in range(H):
        for c in range(W):
            if solid[row, c] and any(not (0 <= row + a < H and 0 <= c + b < W) or not solid[row + a, c + b]
                                     for a, b in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                out[row, c] = rgb("2e2c5a")
    return out


def mix(a, b, t):
    return tuple(int(round(a[i] * (1 - t) + b[i] * t)) for i in range(3)) + (255,)


PUFF = ("..oooo..",
        ".oWWWWo.",
        "oWWWWWWo",
        "oWWWWSSo",
        ".oSSSSo.",
        "..oooo..")


def kugel_platzt():
    """Schnee-Explosion: Blitz, fette Schneebrocken, Pulverwolken, bunte Splitter. 8 Bilder 112x112, Pivot Mitte."""
    W = H = 112
    cx, cy = W / 2, H / 2
    rng = np.random.RandomState(9)
    clumps = [(rng.uniform(0, 2 * math.pi), rng.uniform(0.5, 1.0), int(rng.choice([2, 3, 3, 4]))) for _ in range(18)]
    puffs = [(rng.uniform(0, 2 * math.pi), rng.uniform(0.3, 0.7)) for _ in range(7)]
    bits = [(rng.uniform(0, 2 * math.pi), rng.uniform(0.6, 1.1), rng.randint(0, 5)) for _ in range(14)]
    frames = []
    outline_c = rgb("2e2c5a")
    for k in range(8):
        t = (k + 1) / 8
        img = np.zeros((H, W, 4), np.uint8)
        if k <= 1:                                         # Blitz: weisse Scheibe
            rad = 16 + 12 * k
            for row in range(H):
                for c in range(W):
                    d = math.hypot(c + 0.5 - cx, row + 0.5 - cy)
                    if d <= rad:
                        img[row, c] = SNOW[5] if d < rad - 2 else (SNOW[3] if k == 0 else outline_c)
        if k >= 1:                                         # Pulverwolken treiben nach aussen
            for ang, sp in puffs:
                d = 10 + 30 * sp * (1 - (1 - t) ** 2)
                x = int(cx + math.cos(ang) * d) - 4
                y = int(cy - math.sin(ang) * d * 0.75) - 3
                for dr, line in enumerate(PUFF):
                    for dc, ch in enumerate(line):
                        if ch == "." or (k >= 6 and (dr + dc + k) % 2):
                            continue
                        rr, cc = y + dr, x + dc
                        if 0 <= rr < H and 0 <= cc < W:
                            img[rr, cc] = {"o": SNOW[2], "W": SNOW[5], "S": SNOW[3]}[ch]
        for ang, sp, size in clumps:                       # Schneebrocken fliegen und fallen
            d = (6 + 44 * sp) * (1 - (1 - t) ** 1.5)
            x = cx + math.cos(ang) * d
            y = cy - math.sin(ang) * d * 0.75 + 18 * t * t
            s_ = size if t < 0.75 else max(1, size - 1)
            for dr in range(-s_, s_ + 1):
                for dc in range(-s_, s_ + 1):
                    if dr * dr + dc * dc > s_ * s_ + 0.5:
                        continue
                    rr, cc = int(y) + dr, int(x) + dc
                    if not (0 <= rr < H and 0 <= cc < W):
                        continue
                    edge = dr * dr + dc * dc > (s_ - 1) * (s_ - 1) + 0.5
                    if edge:
                        img[rr, cc] = outline_c if (dr > 0 or dc > 0) else SNOW[2]
                    else:
                        img[rr, cc] = SNOW[5] if dr <= 0 and dc <= 0 else SNOW[3]
        for ang, sp, col in bits:                          # bunte Splitter (das Geschluckte)
            d = (8 + 48 * sp) * (1 - (1 - t) ** 1.4)
            x = int(cx + math.cos(ang) * d)
            y = int(cy - math.sin(ang) * d * 0.75 + 22 * t * t)
            for dr, dc in ((0, 0), (0, 1)):
                if 0 <= y + dr < H and 0 <= x + dc < W:
                    img[y + dr, x + dc] = BITS[col]
        if k == 7:
            img[..., 3] = (img[..., 3] * 0.5).astype(np.uint8)
        frames.append(img)
    return frames, W, H


# ================================================================ Effekt-Streifen

def kristall():
    """Kristall des Frostrings: waechst aus dem Boden, funkelt."""
    W, H = 16, 28
    frames = []
    heights = [5, 13, 22, 25, 24, 24, 24]
    for k, h in enumerate(heights):
        img = np.zeros((H, W, 4), np.uint8)
        g = H - 1
        shards = [(-3.5, 0.7 * h, 2.6, -0.25), (0.5, h, 3.4, 0.05), (4.0, 0.55 * h, 2.3, 0.3)]
        for si, (x0, hh, w, lean) in enumerate(shards):
            for r in range(H):
                yy = g - r
                if yy < 0 or yy > hh:
                    continue
                t = yy / max(hh, 1)
                half = w * (1 - t ** 1.6) + 0.3
                xc = x0 + lean * yy
                for c in range(W):
                    x = c + 0.5 - W / 2
                    if abs(x - xc) <= half:
                        side = (x - xc) / max(half, 0.5)
                        l = 0.75 - 0.5 * side + 0.25 * t
                        col = ramp(l - (0.25 if si != 1 else 0), ICE)
                        img[r, c] = col
        # Umriss
        solid = img[..., 3] > 0
        out = img.copy()
        for r in range(H):
            for c in range(W):
                if solid[r, c] and any(not (0 <= r + a < H and 0 <= c + b < W) or not solid[r + a, c + b]
                                       for a, b in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                    out[r, c] = C["l_ice"]
        if k >= 4:                                             # Funkeln wandert die Spitze hoch
            yy = [6, 13, 20][k - 4]
            r = g - yy
            c = int(W / 2 + 0.5 + 0.05 * yy)
            for a, b in ((0, 0), (1, 0), (-1, 0), (0, 1), (0, -1)):
                if 0 <= r + a < H and 0 <= c + b < W and solid[r + a, c + b]:
                    out[r + a, c + b] = C["spark"]
        if k == 3:
            for a, b in ((-3, -6), (-5, 6), (-8, 0)):
                rr, cc = g - h + 4 + a, W // 2 + b
                if 0 <= rr < H and 0 <= cc < W and out[rr, cc, 3] == 0:
                    out[rr, cc] = C["spark_b"]
        frames.append(out)
    return frames, W, H


def eisblock():
    """Eisblock um den Spieler: durchscheinend, harte Kanten, Glanz, Risse, Splitter."""
    W, H = 40, 52
    frames = []
    bx0, bx1, by0, by1 = 4, 35, 6, 50              # Blockflaeche (Spalten, Zeilen)
    for k in range(10):
        img = np.zeros((H, W, 4), np.uint8)
        if k <= 6:
            grow = min(1.0, (k + 1) / 3)
            top = int(by1 - (by1 - by0) * grow)
            for r in range(top, by1 + 1):
                for c in range(bx0, bx1 + 1):
                    edge = r in (top, by1) or c in (bx0, bx1)
                    inner = r in (top + 1,) or c in (bx0 + 1,)
                    if edge:
                        img[r, c] = C["l_ice"]
                    elif inner:
                        img[r, c] = (*ICE[4][:3], 230)
                    else:
                        # Flaeche: kuehles Blau, nach unten dichter
                        a = int(70 + 60 * (r - top) / max(1, by1 - top))
                        img[r, c] = (*ICE[3][:3], a)
            # Deckel: Oberseite in Aufsicht
            if top + 4 < by1:
                for c in range(bx0 + 1, bx1):
                    for rr in (top + 1, top + 2, top + 3):
                        img[rr, c] = (*ICE[4][:3], 200) if rr < top + 3 else (*ICE[2][:3], 210)
            # Glanzstreifen schraeg, wandert in der Haltezeit
            if k >= 2:
                off = (k - 3) * 3
                for r in range(top + 4, by1 - 1):
                    for c in (bx0 + 3 + (r - top) // 3 + off, bx0 + 4 + (r - top) // 3 + off):
                        if bx0 + 1 < c < bx1 - 1:
                            img[r, c] = (255, 255, 255, 190)
            # Reif an den Kanten
            for r, c in ((by1 - 1, bx0 + 3), (by1 - 1, bx0 + 9), (by1 - 1, bx1 - 4), (top + 5, bx1 - 2), (top + 9, bx0 + 2)):
                if top < r < by1:
                    img[r, c] = (255, 255, 255, 255)
        elif k == 7:                                   # Risse
            frames_prev = frames[6].copy()
            img = frames_prev
            for r, c in ((14, 10), (15, 11), (16, 11), (17, 12), (18, 14), (19, 15), (20, 15), (16, 12),
                         (30, 26), (31, 25), (32, 25), (33, 24), (34, 22), (32, 26), (33, 27)):
                img[r, c] = C["l_ice"]
        else:                                          # Splitter fliegen
            u = (k - 7) / 2
            rng = np.random.RandomState(11)
            for i in range(14):
                ang = rng.uniform(0, 2 * math.pi)
                dist = 6 + 14 * u * rng.uniform(0.7, 1.2)
                cx = W / 2 + math.cos(ang) * dist
                cy = 30 - math.sin(ang) * dist * 0.8 + 10 * u * u
                c, r = int(cx), int(cy)
                for a, b in ((0, 0), (0, 1), (1, 0)):
                    if 0 <= r + a < H and 0 <= c + b < W:
                        img[r + a, c + b] = ICE[3] if (a + b) else (C["l_ice"] if k == 9 else ICE[5])
        frames.append(img)
    return frames, W, H


# ================================================================ Speichern

def save(name, frames, w, h, pivot):
    strip = np.concatenate(frames, axis=1)
    os.makedirs(OUT_DIR, exist_ok=True)
    png = os.path.join(OUT_DIR, name + ".png")
    Image.fromarray(strip).save(png)
    if not os.path.exists(png + ".meta"):
        write_strip_meta(png + ".meta", name, len(frames), w, h, PPU, pivot=pivot,
                         max_size=4096 if strip.shape[1] > 2048 else 2048)
    print("%-22s %2d Bilder %dx%d" % (name, len(frames), w, h))


def main():
    args = sys.argv[1:]
    anims = [("eiskaiser_walk", walk_poses()), ("eiskaiser_kick", kick_poses()),
             ("eiskaiser_cast", cast_poses())]
    only = args[args.index("--nur") + 1].split(",") if "--nur" in args else None
    rendered = {}
    for name, poses in anims:
        if only and name.split("_", 1)[1] not in only:
            continue
        frames = [draw(p, k) for k, p in enumerate(poses)]
        rendered[name] = frames
        save(name, frames, CELL, CELL, (CX / CELL, (CELL - GROUND) / CELL))
    if not only:
        for name, fn, piv in (("eiskaiser_kristall", kristall, "bottom"), ("eiskaiser_eisblock", eisblock, "bottom"),
                              ("eiskaiser_platzt", kugel_platzt, "center")):
            frames, w, h = fn()
            save(name, frames, w, h, (0.5, 1.0 / h) if piv == "bottom" else (0.5, 0.5))
            rendered[name] = frames
        frames = [kugel(sz, k) for sz in range(len(KUGEL_R)) for k in range(KUGEL_FRAMES)]
        save("eiskaiser_kugel", frames, KUGEL_CELL, KUGEL_CELL, (0.5, (KUGEL_CELL - KUGEL_GROUND) / KUGEL_CELL))
        rendered["eiskaiser_kugel"] = frames

        a = rendered["eiskaiser_walk"][0]
        solid = a[..., 3] == 255
        rows = np.where(solid.any(1))[0]
        cols = np.where(solid.any(0))[0]
        crop = a[rows[0]:rows[-1] + 1, cols[0]:cols[-1] + 1].copy()
        crop[crop[..., 3] < 255] = 0
        Image.fromarray(crop).save(BESTIARY)
        if not os.path.exists(BESTIARY + ".meta"):
            import uuid
            src = open(os.path.join(os.path.dirname(BESTIARY), "EvilSlime.png.meta")).read()
            open(BESTIARY + ".meta", "w", newline="\n").write(
                src.replace("cdfa7c35c19f454f813a4a6b1fe67fbc", uuid.uuid4().hex))

    if "--preview" in args:
        path = args[args.index("--preview") + 1]
        bg = (205, 208, 234, 255)
        seq = []
        for name in ("eiskaiser_walk", "eiskaiser_kick", "eiskaiser_cast"):
            seq += rendered.get(name, [])
        pics = []
        for fr in seq:
            im = Image.new("RGBA", (CELL, CELL), bg)
            im.alpha_composite(Image.fromarray(fr))
            pics.append(im.resize((CELL * 4, CELL * 4), Image.NEAREST).convert("P", palette=Image.ADAPTIVE))
        if pics:
            pics[0].save(path, save_all=True, append_images=pics[1:], duration=int(1000 / FPS), loop=0)


if __name__ == "__main__":
    main()
