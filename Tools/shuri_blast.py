"""
Shuri-Blast (Evo aus Shurikookie + Butterblast) neu: der weisse Zuckerguss-
Wurfstern mit Gesicht, jetzt gross, glasiert und animiert.

  Assets/Art/Waffen/shuri_blast_spin.png      48 Bilder 112x112 nebeneinander, PPU 32
      -> Prefab "ShuriBlastEvoPrefab" (Scale 1) spielt sie per SpriteFlipbook ab
  Assets/Art/Icons/fin_white_Shuriken.png     64x64 volle Aufloesung -> weaponIcon/weaponImage
  Assets/Resources/Achievements/shuri_blast_evo.png                  Erfolg (Rahmen bleibt)
  Assets/Resources/AchievementsBook/shuri_blast_evo_32/21/12.png     Erfolgsbuch
  Assets/Resources/Workbench/evo_shuril_blast_14/_10.png             Werkbank

Der Stern: vier geschwungene Klingen aus Butterkeks, dick mit weissem Zucker-
guss glasiert (der Guss tropft an der hohlen Hinterkante ueber den Keksrand),
die gerade Vorderkante ist eine Schneide aus Zuckerglas, Schokostuecke auf
den Klingen, in der Mitte das alte Gesicht (Kulleraugen, gerader Mund).

Animation: die Klingen drehen sich gegen den Uhrzeigersinn (15 Grad je Bild,
6 Bilder je Vierteldrehung), das Gesicht bleibt aufrecht und blinzelt einmal
je Schleife, hinter den Spitzen ziehen Wischbogen, oben links glitzert das
Licht im Guss. Jedes Bild wird neu gezeichnet statt gedreht - die Pixel
bleiben sauber und das Licht kommt immer von oben links.

Alle .meta bleiben, nur shuri_blast_spin.png bekommt beim ersten Lauf eine neue.
Die Sternformen kommen aus Geo/render (Ingame + 64er Icons), die winzigen
Groessen aus tiny_star bzw. von Hand (SMALL_6).

Aufruf aus dem Projektordner:  python Tools/shuri_blast.py [--preview pfad.png] [--gif pfad.gif] [--icons pfad.png] [--dry]
"""

import math
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

SPIN = os.path.join(ROOT, "Assets", "Art", "Waffen", "shuri_blast_spin.png")
FRAMES = 48
STEP = 15.0          # Grad je Bild
SIZE = 112           # Ingame-Bild
PPU = 32


def hx(s, a=255):
    s = s.lstrip("#")
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), a)


PAL = {
    # Kontur: Schattenseite fast schwarz, Lichtseite warmes Braun
    "line": hx("#2b1714"), "line_lit": hx("#6e3f26"),
    # Butterkeks-Rand
    "k_dark": hx("#b9763a"), "k": hx("#dea65a"), "k_lit": hx("#f3cd85"),
    # Zuckerguss
    "g_deep": hx("#bfb3c4"), "g_shade": hx("#ddd5d8"), "g": hx("#f6f1e8"), "g_lit": hx("#ffffff"),
    # Schneide aus Zuckerglas
    "e_dark": hx("#9fb9d6"), "e": hx("#dcecff"), "e_hi": hx("#ffffff"),
    # Schoko
    "c_dark": hx("#36190f"), "c": hx("#5b2e1b"), "c_lit": hx("#8c5634"),
    # Gesicht
    "eye": hx("#231a2c"), "eye_lo": hx("#463a5e"), "eye_hi": hx("#ffffff"),
    "blush": hx("#f4a6a0"), "mouth": hx("#3a2230"),
    "smear": (255, 248, 226),
}

# Licht von oben links (Bildkoordinaten, y nach unten)
LIGHT = np.array([-0.70, -0.72])


# ----------------------------------------------------------------------------
#  Geometrie
# ----------------------------------------------------------------------------

class Geo:
    """Masse relativ zur Spitzenlaenge rt (Pixel von der Mitte)."""

    def __init__(self, rt, body=0.42, base_w=0.33, root=0.16, tip_lean=0.10, bow=0.10, fat=1.9):
        self.rt = rt
        self.rb = rt * body          # Mittelkeks
        self.w = rt * base_w         # halbe Klingenbreite am Ansatz
        self.u0 = rt * root          # Klingenansatz (innerhalb des Mittelkekses)
        self.lean = rt * tip_lean    # Spitze nach vorn geneigt
        self.bow = rt * bow          # Hinterkante haengt kurz vor der Spitze hohl durch
        self.fat = fat               # >1: Arm bleibt lange breit und laeuft spitz aus

    def blade_v(self, u):
        """Vorderkante (gerade) und Hinterkante (hohl) bei Abstand u."""
        t = np.clip((u - self.u0) / (self.rt - self.u0), 0.0, 1.0)
        v_lead = self.w * 0.85 + (self.lean - self.w * 0.85) * t
        tf = t ** self.fat
        v_trail = -self.w + (self.lean + self.w) * tf + self.bow * np.sin(np.pi * tf) ** 2
        return v_lead, v_trail


def coords(n):
    c = (n - 1) / 2.0
    ys, xs = np.mgrid[0:n, 0:n]
    return xs - c, c - ys        # X nach rechts, Y nach oben


def blade_frame(X, Y, ang):
    """Klingen-Koordinaten: u entlang der Klinge, v zur Vorderseite (gegen den Uhrzeigersinn)."""
    ca, sa = math.cos(ang), math.sin(ang)
    u = X * ca + Y * sa
    v = -X * sa + Y * ca
    return u, v


def erode(m, square):
    out = m.copy()
    out[1:, :] &= m[:-1, :]
    out[:-1, :] &= m[1:, :]
    out[:, 1:] &= m[:, :-1]
    out[:, :-1] &= m[:, 1:]
    if square:
        out[1:, 1:] &= m[:-1, :-1]
        out[1:, :-1] &= m[:-1, 1:]
        out[:-1, 1:] &= m[1:, :-1]
        out[:-1, :-1] &= m[1:, 1:]
    out[0, :] = out[-1, :] = out[:, 0] = out[:, -1] = False
    return out


def depth(mask):
    """1 = Randpixel, 2 = eins weiter innen ... (Achteck-Abstand)."""
    d = np.zeros(mask.shape, float)
    cur = mask.copy()
    i = 0
    while cur.any():
        i += 1
        d[cur] = i
        cur = erode(cur, square=(i % 2 == 0))
    return d


def facing(h):
    """Wie sehr die Flaeche bei Hoehe h zum Licht zeigt (-1..1)."""
    gy, gx = np.gradient(h)
    n = np.hypot(gx, gy) + 1e-6
    # Aussennormale zeigt bergab: -grad
    return (-gx * LIGHT[0] - gy * LIGHT[1]) / n


# ----------------------------------------------------------------------------
#  Ein Bild
# ----------------------------------------------------------------------------

def render(n, rt, theta, opts):
    """theta: Drehung der Klingen in Grad (gegen den Uhrzeigersinn)."""
    g = Geo(rt, **opts.get("geo", {}))
    X, Y = coords(n)
    r = np.hypot(X, Y)

    sil = r <= g.rb
    blade_id = np.full((n, n), -1)
    bu = np.zeros((n, n))
    bv = np.zeros((n, n))
    lead_d = np.full((n, n), 99.0)
    for k in range(4):
        ang = math.radians(theta + 90 * k + opts.get("base_angle", 45))
        u, v = blade_frame(X, Y, ang)
        vl, vt = g.blade_v(u)
        inside = (u >= g.u0) & (u <= g.rt) & (v <= vl) & (v >= vt)
        sil |= inside
        own = inside | ((u > 0) & (np.abs(v) < g.w + 2) & (blade_id < 0))
        blade_id[own] = k
        bu[own] = u[own]
        bv[own] = v[own]
        # Abstand zur geraden Vorderkante (senkrecht zur Kante, grob)
        lead_d[inside] = np.minimum(lead_d[inside], (vl - v)[inside])

    d = depth(sil)
    out = np.zeros((n, n, 4), np.uint8)

    rim = opts.get("rim", 3.0)
    wob = opts.get("wobble", 1.3)
    # Glasurgrenze: tropft an der Hinterkante, am Mittelkeks leicht gewellt
    phase = np.where(blade_id >= 0, blade_id * 1.9, 0.0)
    on_blade = (blade_id >= 0) & (r > g.rb - 1)
    th = np.where(on_blade,
                  rim + wob * np.sin(bu * (6.0 / rt * 2.2) + phase) * (bv < 0),
                  rim - 0.6 + 0.5 * wob * np.sin(6 * np.arctan2(Y, X) - math.radians(theta) * 6))
    glaze = sil & (d > th)
    edge = sil & ~glaze & (lead_d < opts.get("edge", 2.6)) & on_blade & (r > g.rb + 1)

    cookie_lit = facing(np.minimum(d, 4.0))
    gd = depth(glaze)
    glaze_lit = facing(np.minimum(gd, opts.get("pillow", 4.0)))

    def put(mask, col):
        out[mask] = col

    # Keksrand
    kr = sil & ~glaze
    put(kr, PAL["k"])
    put(kr & (cookie_lit > 0.35), PAL["k_lit"])
    put(kr & (cookie_lit < -0.35), PAL["k_dark"])
    # Guss: Kissen mit Licht oben links, Schatten unten rechts
    put(glaze, PAL["g"])
    put(glaze & (gd <= 2) & (glaze_lit > 0.30), PAL["g_lit"])
    put(glaze & (gd <= opts.get("shade_w", 4)) & (glaze_lit < -0.20), PAL["g_shade"])
    put(glaze & (gd <= opts.get("deep_w", 2)) & (glaze_lit < -0.45), PAL["g_deep"])
    # Schneide: Zuckerglas, Licht haengt an der Klingenrichtung
    if opts.get("edge", 2.6) > 0:
        put(edge, PAL["e"])
        put(edge & (cookie_lit < -0.2), PAL["e_dark"])
        put(edge & (cookie_lit > 0.5) & (d >= 2), PAL["e_hi"])
    # Kontur: gross innen auf dem Rand, klein aussen herum (sonst frisst sie die Arme)
    if opts.get("outside"):
        grow = sil.copy()
        grow[1:, :] |= sil[:-1, :]
        grow[:-1, :] |= sil[1:, :]
        grow[:, 1:] |= sil[:, :-1]
        grow[:, :-1] |= sil[:, 1:]
        line = grow & ~sil
        put(line, PAL["line"])
        put(line & ((-X * 0.7 + Y * 0.7) > g.rb * 0.6), PAL["line_lit"])
    else:
        line = sil & (d == 1)
        put(line, PAL["line"])
        put(line & (cookie_lit > 0.55), PAL["line_lit"])

    # Zuckerkristalle und Schokostuecke (in Klingenkoordinaten, drehen mit):
    # t = 0 Ansatz .. 1 Spitze, q = -1 Hinterkante .. 1 Vorderkante
    def blade_point(k, t, q):
        ang = math.radians(theta + 90 * k + opts.get("base_angle", 45))
        u = g.u0 + t * (g.rt - g.u0)
        vl, vt = g.blade_v(np.array(u))
        v = (vl + vt) / 2 + q * (vl - vt) / 2
        px = u * math.cos(ang) - v * math.sin(ang)
        py = u * math.sin(ang) + v * math.cos(ang)
        return int(round((n - 1) / 2.0 + px)), int(round((n - 1) / 2.0 - py))

    for (t, q) in opts.get("sugar", []):
        for k in range(4):
            cx, cy = blade_point(k, t, q)
            if glaze[cy, cx] and glaze[cy + 1, cx]:
                out[cy, cx] = PAL["g_lit"]
                out[cy + 1, cx] = PAL["g_shade"]
    for (t, q, shape) in opts.get("chips", []):
        for k in range(4):
            cx, cy = blade_point(k, t, q)
            stamp(out, CHIPS[shape], cx, cy, glaze)

    # Wischbogen hinter den Spitzen
    sm = opts.get("smear")
    if sm:
        smear(out, X, Y, r, sil, g, theta, opts.get("base_angle", 45), sm)

    return out, sil, glaze


CHIPS = {
    "huge": [".abb.", "aabbc", "abbcc", ".bcc."],
    "big": [".ab.", "abbc", "bbcc", ".cc."],
    "mid": ["ab.", "bbc", ".cc"],
    "small": ["ab", "bc"],
    "dot": ["b"],
}
CHIP_COL = {"a": "c_lit", "b": "c", "c": "c_dark"}


def stamp(out, rows, cx, cy, where):
    h, w = len(rows), len(rows[0])
    x0, y0 = cx - w // 2, cy - h // 2
    n = out.shape[0]
    for j, row in enumerate(rows):
        for i, ch in enumerate(row):
            x, y = x0 + i, y0 + j
            if ch != "." and 0 <= x < n and 0 <= y < n and where[y, x]:
                out[y, x] = PAL[CHIP_COL[ch]]


def smear(out, X, Y, r, sil, g, theta, base, sm):
    span, width, alphas = sm
    phi = np.degrees(np.arctan2(Y, X))
    tip_off = math.degrees(math.atan2(g.lean, g.rt))
    for k in range(4):
        tip = theta + 90 * k + base + tip_off
        behind = (tip - phi) % 360          # Grad hinter der Spitze
        t = behind / span
        band = (behind > 0.5) & (t < 1) & (r <= g.rt - 0.5) & (r >= g.rt - 0.5 - width * (1 - t) - 0.8)
        band &= ~sil
        for lim, a in alphas:
            m = band & (t < lim) & (out[..., 3] == 0)
            out[m] = (*PAL["smear"], a)


# ----------------------------------------------------------------------------
#  Gesicht (von Hand, bleibt aufrecht)
# ----------------------------------------------------------------------------

FACE_COL = {"X": "eye", "L": "eye_lo", "W": "eye_hi", "M": "mouth", "P": "blush"}

# 112er Bild: Augen 6x9 mit zwei Lichtern, Mund mit kleinem Haken, Baeckchen
EYE_BIG = [
    ".XXXX.",
    "XXXXXX",
    "XWWXXX",
    "XWWXXX",
    "XXXXXX",
    "XXXXXX",
    "XXXXWX",
    "XLLLLX",
    ".XLLX.",
]
EYE_BIG_HALF = [
    "XXXXXX",
    "XWWXXX",
    "XXXXWX",
    "XLLLLX",
    ".XLLX.",
]
EYE_BIG_SHUT = [
    "X....X",
    ".XXXX.",
]
MOUTH_BIG = [
    "M.......",
    ".MMMMMMM",
]
BLUSH_BIG = [".PPP.", "PPPPP"]


def face_big(out, n, blink):
    c = n // 2
    ey = c - 8
    for side in (-1, 1):
        ex = c - 3 + side * 9
        if blink == 0:
            draw(out, EYE_BIG, ex, ey)
        elif blink == 1:
            draw(out, EYE_BIG_HALF, ex, ey + 4)
        else:
            draw(out, EYE_BIG_SHUT, ex, ey + 6)
        draw(out, BLUSH_BIG, ex + (0 if side < 0 else 1) + side * 3, ey + 11)
    draw(out, MOUTH_BIG, c - 4, ey + 12)


def draw(out, rows, x0, y0):
    for j, row in enumerate(rows):
        for i, ch in enumerate(row):
            if ch == ".":
                continue
            out[y0 + j, x0 + i] = PAL[FACE_COL[ch]]


# ----------------------------------------------------------------------------
#  Ingame-Streifen
# ----------------------------------------------------------------------------

INGAME = {
    "rim": 3.4, "wobble": 1.6, "edge": 2.6, "pillow": 4.0, "shade_w": 3,
    "chips": [(0.52, -0.12, "huge"), (0.77, 0.08, "big"), (0.33, 0.50, "mid")],
    "sugar": [(0.45, 0.30), (0.62, -0.45), (0.80, -0.20), (0.30, -0.35), (0.66, 0.50)],
    "smear": (46.0, 6.0, ((0.30, 220), (0.62, 140), (1.0, 70))),
}
RT = 54.0

# Glitzern oben links im Guss: Bildnummer -> Muster
SPARKLE = {
    10: ["W"], 11: [".W.", "WWW", ".W."], 12: ["..W..", "..W..", "WWWWW", "..W..", "..W.."],
    13: [".W.", "WWW", ".W."], 14: ["W"],
}
SPARKLE_AT = (-15, -15)     # relativ zur Mitte, Bildkoordinaten
BLINK = {40: 1, 41: 2, 42: 2, 43: 1}


def spin_frame(i):
    theta = i * STEP
    img, sil, glaze = render(SIZE, RT, theta, INGAME)
    face_big(img, SIZE, BLINK.get(i, 0))
    if i in SPARKLE:
        rows = SPARKLE[i]
        c = SIZE // 2
        x0 = c + SPARKLE_AT[0] - len(rows[0]) // 2
        y0 = c + SPARKLE_AT[1] - len(rows) // 2
        for j, row in enumerate(rows):
            for k, ch in enumerate(row):
                if ch == "W" and glaze[y0 + j, x0 + k]:
                    img[y0 + j, x0 + k] = PAL["g_lit"]
    return Image.fromarray(img, "RGBA")


def spin_strip():
    frames = [spin_frame(i) for i in range(FRAMES)]
    strip = Image.new("RGBA", (SIZE * FRAMES, SIZE), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        strip.paste(f, (i * SIZE, 0))
    return frames, strip


# ----------------------------------------------------------------------------
#  Icons: derselbe Stern klein gezeichnet, Gesicht je Groesse von Hand
# ----------------------------------------------------------------------------

def small_opts(rt, chips=(), sugar=(), smear_=None, edge=1.2):
    return {"outside": True, "geo": {"base_w": 0.40, "fat": 2.4, "body": 0.48, "tip_lean": 0.12},
            "rim": 1.6 if rt > 8 else 1.2, "wobble": 0.6 if rt > 8 else 0.0, "edge": edge,
            "pillow": 2.0, "shade_w": 1, "deep_w": 0, "chips": list(chips),
            "sugar": list(sugar), "smear": smear_}


# Gesichter: Augen-Muster, Abstand der Augen-Mitten, Augen-/Mund-/Baeckchen-y relativ zur Mitte
FACE_64 = {
    "eyes": [".XX.", "XWXX", "XXXX", "XLLX", ".XX."], "gap": 11, "ey": -4,
    "mouth": ["M....", ".MMMM"], "my": 2, "blush": ["PPP"], "by": 1,
}
FACE_24 = {"eyes": ["X", "X"], "gap": 4, "ey": -2, "mouth": ["MM"], "my": 1, "blush": None}


def face_small(img, n, f):
    c = (n - 1) / 2.0
    ew = len(f["eyes"][0])
    for side in (-1, 1):
        x0 = int(round(c + side * f["gap"] / 2.0 - (ew - 1) / 2.0))
        draw(img, f["eyes"], x0, int(round(c)) + f["ey"])
        if f.get("blush"):
            bx = x0 + (-1 if side < 0 else ew - 1)
            draw(img, f["blush"], bx, int(round(c)) + f["ey"] + len(f["eyes"]) + f["by"])
    if f.get("mouth"):
        mw = len(f["mouth"][0])
        draw(img, f["mouth"], int(round(c - (mw - 1) / 2.0)), int(round(c)) + f["my"])


def star_icon(n, rt, theta, face, **kw):
    img, sil, glaze = render(n, rt, theta, small_opts(rt, **kw))
    if face:
        face_small(img, n, face)
    return Image.fromarray(img, "RGBA")


ACH_UNDERLINE = "#e2a44a"
BOOK_UNDERLINE = "#d29a52"


def icons():
    from blade_storm_icons import reframe
    A = lambda p: os.path.join(ROOT, p)
    out = {}

    # Waffenicon: volle 64er Aufloesung wie ingame (wie das alte Icon), Wischbogen
    # zeigen die Drehung
    big = {"rim": 2.2, "wobble": 0.9, "edge": 1.8, "pillow": 3.0, "shade_w": 2, "deep_w": 1,
           "chips": [(0.52, -0.12, "big"), (0.78, 0.08, "small")],
           "sugar": [(0.40, 0.35), (0.66, -0.45)],
           "smear": (40.0, 3.5, ((0.35, 220), (0.7, 140), (1.0, 70)))}
    ic, _, glaze = render(64, 29.5, 8, big)
    face_small(ic, 64, FACE_64)
    out["Assets/Art/Icons/fin_white_Shuriken.png"] = Image.fromarray(ic, "RGBA")
    big_ach = dict(big, smear=None)

    # Erfolg 64: Rahmen im 32er Raster (Innenflaeche x2..30 / y2..27, Unterlinie
    # Zeile 28), der Stern darin in voller Aufloesung wie beim alten Icon
    out["Assets/Resources/Achievements/shuri_blast_evo.png"] = reframe(
        A("Assets/Resources/Achievements/shuri_blast_evo.png"), 2,
        (2, 2, 30, 27, "#f6d8a0"), [], (2, 28, 29, ACH_UNDERLINE),
        Image.new("RGBA", (1, 1)), (0, 0))
    ach, _, _ = render(52, 24.0, 8, big_ach)
    face_small(ach, 52, FACE_64)
    out["Assets/Resources/Achievements/shuri_blast_evo.png"].alpha_composite(
        Image.fromarray(ach, "RGBA"), (6, 4))

    # Erfolgsbuch 32
    out["Assets/Resources/AchievementsBook/shuri_blast_evo_32.png"] = reframe(
        A("Assets/Resources/AchievementsBook/shuri_blast_evo_32.png"), 1,
        (6, 6, 25, 23, "#f2dcbc"),
        [(5, 5, 26, "#e0c49f")] + [(5, y, 5, "#e0c49f") for y in range(6, 24)]
        + [(26, y, 26, "#e0c49f") for y in range(6, 24)]
        + [(6, 24, 25, "#b99772"), (6, 25, 6, "#b99772"), (25, 25, 25, "#b99772")],
        (7, 25, 24, BOOK_UNDERLINE),
        star_icon(20, 8.6, 8, FACE_24, chips=[(0.62, -0.1, "dot")]), (6, 5))

    # Erfolgsbuch 21
    out["Assets/Resources/AchievementsBook/shuri_blast_evo_21.png"] = reframe(
        A("Assets/Resources/AchievementsBook/shuri_blast_evo_21.png"), 1,
        (4, 4, 16, 14, "#f2dcbc"), [(4, 15, 16, "#e0c49f")],
        (6, 16, 14, BOOK_UNDERLINE),
        tiny_star(13, 6.4, 0, [(5, 5), (7, 5)]), (4, 3))

    # Erfolgsbuch 12
    out["Assets/Resources/AchievementsBook/shuri_blast_evo_12.png"] = reframe(
        A("Assets/Resources/AchievementsBook/shuri_blast_evo_12.png"), 1,
        (3, 3, 8, 8, "#f2dcbc"), [], (3, 9, 8, BOOK_UNDERLINE),
        hand(SMALL_6), (3, 3))

    # Werkbank: 14 (Motiv mit Kontur) und 10
    out["Assets/Resources/Workbench/evo_shuril_blast_14.png"] = tiny_star(14, 6.9, 0, [(5, 6), (8, 6)], cover=0.45)
    out["Assets/Resources/Workbench/evo_shuril_blast_10.png"] = tiny_star(10, 4.6, 0, [], cover=0.4, arm=0.26, body=0.40)
    return out


def tiny_star(n, rt, theta, eyes, cover=0.42, ss=8, arm=0.27, body=0.36):
    """Winzige Sterne: Silhouette aus dem Flaechenanteil einer feinen Fassung,
    dann nach Rollen gefaerbt (Kontur aussen, Guss, Licht oben links, Keks unten rechts)."""
    N = n * ss
    R = rt * ss
    X, Y = coords(N)
    sil = np.hypot(X, Y) <= R * body
    for k in range(4):
        u, v = blade_frame(X, Y, math.radians(theta + 90 * k + 45))
        # gleich breiter Arm, das letzte Drittel laeuft zur (leicht vorgeneigten) Spitze zu
        t = np.clip((u / R - 0.62) / 0.38, 0, 1)
        hw = R * arm * (1 - t)
        sil |= (u >= 0) & (u <= R) & (v <= hw + 0.12 * R * t) & (v >= -hw + 0.12 * R * t)
    m = sil.reshape(n, ss, n, ss).mean(axis=(1, 3)) >= cover
    out = np.zeros((n, n, 4), np.uint8)
    out[m] = PAL["g"]
    inner = erode(np.pad(m, 1), square=False)[1:-1, 1:-1]
    rim = m & ~inner
    ys, xs = np.mgrid[0:n, 0:n]
    c = (n - 1) / 2.0
    lit = (xs - c) + (ys - c) < 0
    out[rim & lit] = PAL["g_lit"]
    out[rim & ~lit] = PAL["k"]
    out[rim & ~lit & ((xs - c) + (ys - c) > n * 0.35)] = PAL["k_dark"]
    grow = np.pad(m, 1)
    grow = (grow[1:-1, 1:-1] | grow[:-2, 1:-1] | grow[2:, 1:-1] | grow[1:-1, :-2] | grow[1:-1, 2:])
    out[grow & ~m] = PAL["line"]
    for (x, y) in eyes:
        out[y, x] = PAL["eye"]
    return Image.fromarray(out, "RGBA")


# Ganz klein von Hand: o Kontur, l Licht, g Guss, s Gussschatten, k/d Keks, x Auge
SMALL_COL = {"o": "line", "k": "k", "d": "k_dark", "g": "g", "l": "g_lit", "s": "g_shade", "x": "eye"}
SMALL_6 = [
    "o....o",
    ".lggk.",
    ".ggsk.",
    ".gssk.",
    ".kkkd.",
    "o....o",
]

def hand(rows):
    img = Image.new("RGBA", (len(rows[0]), len(rows)), (0, 0, 0, 0))
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch != ".":
                img.putpixel((x, y), PAL[SMALL_COL[ch]])
    return img


# ----------------------------------------------------------------------------

def main():
    args = sys.argv
    dry = "--dry" in args
    frames, strip = spin_strip()

    if "--preview" in args:
        path = args[args.index("--preview") + 1]
        k = 3
        cols = 8
        sheet = Image.new("RGBA", (cols * SIZE * k, 2 * SIZE * k), (58, 64, 52, 255))
        for i in range(0, 16):
            f = frames[i * 3 if i < 8 else 40 + (i - 8)]
            sheet.alpha_composite(f.resize((SIZE * k, SIZE * k), Image.NEAREST),
                                  ((i % cols) * SIZE * k, (i // cols) * SIZE * k))
        sheet.save(path)
        print("Vorschau:", path)
    if "--gif" in args:
        path = args[args.index("--gif") + 1]
        k = 3
        gif = []
        for f in frames:
            bg = Image.new("RGBA", (SIZE * k, SIZE * k), (70, 92, 60, 255))
            bg.alpha_composite(f.resize((SIZE * k, SIZE * k), Image.NEAREST))
            gif.append(bg.convert("RGB"))
        gif[0].save(path, save_all=True, append_images=gif[1:], duration=28, loop=0)
        print("GIF:", path)

    out = icons()
    if "--icons" in args:
        path = args[args.index("--icons") + 1]
        cell = 272
        sheet = Image.new("RGBA", (cell * len(out), 272), (60, 60, 70, 255))
        for i, (name, img) in enumerate(out.items()):
            bg = Image.new("RGBA", (256, 256),
                           (225, 205, 165, 255) if "Workbench" in name else (60, 60, 70, 255))
            k = 256 // max(img.size)
            bg.alpha_composite(img.resize((img.width * k, img.height * k), Image.NEAREST))
            sheet.alpha_composite(bg, (i * cell + 8, 8))
        sheet.save(path)
        print("Icons:", path)

    if not dry:
        for name, img in out.items():
            img.save(os.path.join(ROOT, name))
        strip.save(SPIN)
        if not os.path.exists(SPIN + ".meta"):
            from unity_meta import write_strip_meta
            write_strip_meta(SPIN + ".meta", "shuri_blast_spin", FRAMES, SIZE, SIZE, PPU, max_size=8192)


if __name__ == "__main__":
    main()
