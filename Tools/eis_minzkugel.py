"""
Minzkugel (EnemyId.Minzkugel, Eiswelt): eine Kugel Minz-Schoko-Eis, die auf
ihrer eigenen Schmelzpfuetze ueber den Gletscher schlittert.

  * Schlittschuh-Takt: zurueckwippen (stauchen, nach hinten lehnen), abstossen
    (strecken, nach vorn kippen, Schneespritzer hinten), gleiten (Federn auf
    Stauchung und Neigung schwingen nach).
  * Koerper = Metaball-Feld: die Kugel plus der typische Portionierer-Rand
    unten (eine Reihe kleiner Wuelste), Licht aus der Feldnormalen.
  * Schokostueckchen sitzen fest im Koerper und machen jede Verformung mit.
  * Oben steckt ein Waffelfaecher an einer Drehfeder - er haengt der Neigung
    hinterher und wippt nach.
  * Hinten laeuft ein Schmelztropfen am Rand herunter, loest sich und
    platscht in die Pfuetze; die Pfuetze zieht sich beim Abstossen lang.
  * Zwei Schleifen: normal und Blinzeln mit eisigem Atemwoelkchen.

  Assets/Art/Gegner/new/eis/minzkugel_gleit.png   2 x 12 Bilder, 40x40, PPU 32
  Pivot = Bodenmitte unter der Kugel.
  Assets/Resources/Bestiary/Minzkugel.png          Bild 0, zugeschnitten

Aufruf aus dem Projektordner:
  python Tools/eis_minzkugel.py [--preview pfad.gif]
"""

import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from eis_gegner_kit import (Spring, arg_preview, downsample, ease_out, grid,  # noqa: E402
                            lambert, outline, periodic, pixel_grid, put, ramp,
                            rgb, save_all, smooth, stamp, wrap)

NAME = "minzkugel_gleit"
CELL_W, CELL_H = 40, 40
GROUND = 36
CX = 20.0
FPS = 14
PER = 12
LOOPS = 2
N = PER * LOOPS
SS = 6

R = 10.5                 # Kugelradius in Ruhe
LIP_Y = 2.6              # Hoehe des Portionierer-Rands
LIP_B = 1.4              # Tiefe der Rand-Ellipse (Ansicht leicht von oben)

C = {
    "line":   rgb("16302f"),
    "line_b": rgb("10221f"),
    "deep":   rgb("1c665c"),
    "shade":  rgb("2a8a7b"),
    "mid":    rgb("43ad96"),
    "base":   rgb("6acdb0"),
    "light":  rgb("9be6ca"),
    "hi":     rgb("d2fbe8"),
    "glint":  rgb("ffffff"),
    "bounce": rgb("58b9b4"),     # kuehle Rueckstrahlung vom Schnee unten
    # Schoko
    "ch_d":   rgb("2a140e"),
    "ch_m":   rgb("4d2b1c"),
    "ch_l":   rgb("7d4b31"),
    # Waffel
    "wf_line": rgb("4a2810"),
    "wf_d":   rgb("9a6229"),
    "wf_m":   rgb("c98a3e"),
    "wf_l":   rgb("e6b464"),
    "wf_hi":  rgb("f8dc9a"),
    # Pfuetze
    "pd_rim": rgb("7fa6d0"),
    "pd_b":   rgb("b4dcef"),
    "pd_l":   rgb("e2f6ff"),
    "pd_g":   rgb("ffffff"),
    # Gesicht
    "eye":    rgb("0f1e1d"),
    "eglint": rgb("ffffff"),
    "brow":   rgb("12302c"),
    "mouth":  rgb("0f1e1d"),
    "maw":    rgb("6a1f2e"),
    "tongue": rgb("e06a86"),
    "fang":   rgb("ffffff"),
    "blush":  rgb("f28aa6"),
    # Atem / Schnee
    "puff":   rgb("eef4ff"),
    "puff_s": rgb("b9c6ec"),
    "snow":   rgb("ffffff"),
    "snow_s": rgb("cdd0ea"),
    "shadow": (64, 58, 110, 70),
}

MINT = (C["deep"], C["shade"], C["mid"], C["base"], C["light"], C["hi"])

# ================================================================ Bewegung

W0, W1 = 0.00, 0.30      # zurueckwippen
P0, P1 = 0.30, 0.42      # abstossen


def targets(p):
    w = smooth(W0, W1, p) * (1 - smooth(P0, P1, p))
    s = ease_out(P0, P1, p) * (1 - smooth(0.55, 1.0, p))
    sq = -0.10 * w + 0.12 * s            # < 0 gestaucht, > 0 gestreckt
    lean = 1.4 * w - 2.4 * s             # + nach hinten (rechts), - nach vorn
    lift = 1.0 * math.sin(math.pi * smooth(P0, 0.62, p))
    return sq, lean, lift


def simulate():
    sq_s = Spring(520.0, 11.0)
    le_s = Spring(420.0, 10.0)
    wf_s = Spring(260.0, 7.0)            # Waffelfaecher (Grad)
    prev = {"lean": None}

    def step(k, dt):
        loop = int(k // PER)
        p = (k % PER) / PER
        sq, lean, lift = targets(p)
        push = abs(p - P0) < 0.5 / (PER * 40)
        land = abs(p - 0.62) < 0.5 / (PER * 40)
        sqv = sq_s.step(sq, dt, (6.0 if push else 0.0) - (5.0 if land else 0.0))
        lev = le_s.step(lean, dt, -30.0 if push else 0.0)
        dl = 0.0 if prev["lean"] is None else lev - prev["lean"]
        prev["lean"] = lev
        wf = wf_s.step(lev * 6.0, dt, dl * 30.0)
        return dict(p=p, loop=loop, sq=sqv, lean=lev, lift=lift, wf=wf)

    return periodic(N, FPS, step)


# ================================================================ Formen

def body_geo(st):
    sq = st["sq"]
    rx = R * (1 - 0.55 * sq)
    ry = R * 0.94 * (1 + sq)
    cy = LIP_Y + 0.6 + ry * 0.80 + st["lift"]
    return rx, ry, cy


def lean_shift(st, Y):
    rx, ry, cy = body_geo(st)
    t = np.clip((Y - st["lift"]) / (2 * ry), 0, 1.3)
    return st["lean"] * t ** 1.3


LIP_BALLS = [(t, 1.9 + 0.5 * math.sin(t * 3.7 + 1.1)) for t in
             np.linspace(0, 2 * math.pi, 13, endpoint=False) + 0.2]
EXP = 3.0                # Verschmelzung: hoeher = knappere Naehte


def body_pot(st, X, Y):
    rx, ry, cy = body_geo(st)
    Xs = X - lean_shift(st, Y)
    F = (Xs / rx) ** 2 + ((Y - cy) / ry) ** 2
    pot = 1.0 / np.maximum(F, 1e-4) ** EXP
    ly = LIP_Y + st["lift"]
    for t, r in LIP_BALLS:
        bx = math.cos(t) * rx * 0.86
        by = ly + math.sin(t) * LIP_B
        d2 = (Xs - bx) ** 2 + (Y - by) ** 2
        pot = pot + (r * r / np.maximum(d2, 1e-4)) ** EXP
    return pot


def body_F(st):
    def F(X, Y):
        return np.maximum(body_pot(st, X, Y), 1e-6) ** (-1 / EXP)
    return F


def drip(st):
    """Schmelztropfen hinten: laeuft am Rand herunter, faellt, platscht."""
    rx, ry, cy = body_geo(st)
    t = wrap(st["p"] - 0.05)
    x = rx * 0.78 + 0.6
    top = LIP_Y + st["lift"] + 2.0
    if t < 0.45:                       # Faden waechst nach unten
        u = t / 0.45
        return ("run", x, top, top - 1.0 - 3.0 * u, 0.9 + 0.5 * u)
    if t < 0.62:                       # faellt
        u = (t - 0.45) / 0.17
        return ("fall", x + 0.8 * u, top - 4.0 - (top - 3.0) * u * u)
    if t < 0.82:
        return ("splat", x + 1.0, (t - 0.62) / 0.20)
    return None


# ================================================================ Waffelfaecher

WAFER_R = 9.5
WAFER_HALF = math.radians(40)
WAFER_BASE = 24.0                       # Grad gegen die Senkrechte, nach hinten
_wafer_cache = {}


def wafer_angle(st):
    a = WAFER_BASE + st["wf"] + st["lean"] * 4.0
    return round(a / 7.5) * 7.5


def wafer_mask(st, X, Y):
    rx, ry, cy = body_geo(st)
    ax = lean_shift(st, np.array(cy + ry))[()] + rx * 0.18
    ay = cy + ry * 0.62
    ang = math.radians(wafer_angle(st))
    du = (math.sin(ang), math.cos(ang))                  # Achse: nach oben, nach hinten gekippt
    U = (X - ax) * du[0] + (Y - ay) * du[1]
    V = (X - ax) * du[1] - (Y - ay) * du[0]
    d = np.sqrt(U * U + V * V)
    phi = np.arctan2(V, U)
    m = (d <= WAFER_R) & (np.abs(phi) <= WAFER_HALF) & (U > 0)
    # Aussenrand mit fuenf Boegen (je Falte einer)
    lobes = 5 / (2 * WAFER_HALF)
    m &= d <= WAFER_R - 0.9 * np.abs(np.sin((phi + WAFER_HALF) * lobes * math.pi))
    return m, (ax, ay, du)


# ================================================================ Rendern

EMPTY, SHADOW, PUDDLE, BODY, WAFER, DROP = range(6)


def render(st):
    X, Y = grid(CELL_W, CELL_H, SS, CX, GROUND)
    rx, ry, cy = body_geo(st)
    owner = np.zeros(X.shape, np.int16)

    # Schatten + Pfuetze (auf dem Boden, Ansicht von oben gestaucht)
    owner[((X - 0.5) / (rx + 5.0)) ** 2 + ((Y + 0.4) / 3.0) ** 2 <= 1.0] = SHADOW
    stretch = 1.0 + 0.35 * smooth(P0, 0.5, st["p"]) * (1 - smooth(0.5, 1.0, st["p"]))
    pw = (rx + 4.0) * stretch
    pcx = 1.5 + (pw - rx - 4.0) * 0.8                    # zieht sich nach hinten lang
    owner[((X - pcx) / pw) ** 2 + ((Y + 0.1) / 2.6) ** 2 <= 1.0] = PUDDLE

    pot = body_pot(st, X, Y)
    dr = drip(st)
    if dr and dr[0] == "run":
        _, x, y0, y1, w = dr
        seg = (np.abs(X - x) <= w * 0.5 + 0.15 * (Y - y1)) & (Y <= y0) & (Y >= y1)
        owner[(pot < 1.0) & seg & (Y >= 0)] = DROP
        owner[((X - x) ** 2 + (Y - y1) ** 2 <= (w * 0.75) ** 2)] = DROP
    owner[(pot >= 1.0) & (Y >= 0)] = BODY
    wm, _ = wafer_mask(st, X, Y)
    owner[wm & (pot < 2.6)] = WAFER      # steckt in der Kugel, unten verdeckt

    pix = downsample(owner, CELL_W, CELL_H, SS, keep_thin=(DROP, WAFER))
    return pix


def shade(pix, st):
    img = np.zeros((CELL_H, CELL_W, 4), np.uint8)
    Xp, Yp = pixel_grid(CELL_W, CELL_H, CX, GROUND)
    rx, ry, cy = body_geo(st)
    lam, nx, ny = lambert(body_F(st), Xp, Yp, depth=ry * 0.6)
    lipline = LIP_Y + st["lift"] + 2.4
    _, (ax, ay, du) = wafer_mask(st, Xp, Yp)

    for r in range(CELL_H):
        for c in range(CELL_W):
            m = pix[r, c]
            if m == EMPTY:
                continue
            x, y = Xp[r, c], Yp[r, c]
            if m == SHADOW:
                img[r, c] = C["shadow"]
            elif m == PUDDLE:
                # Glanzband laeuft ueber die Pfuetze, unten ein dunkler Rand
                gx = (x + 6.0 - 12.0 * st["p"])
                if pix[min(r + 1, CELL_H - 1), c] != PUDDLE:
                    img[r, c] = C["pd_rim"]
                elif abs(gx - 0.6 * (y + 0.5)) < 0.8 and y > -1.0:
                    img[r, c] = C["pd_g"]
                elif y > 0.4:
                    img[r, c] = C["pd_l"]
                else:
                    img[r, c] = C["pd_b"]
            elif m == BODY:
                l = lam[r, c]
                yb = y - st["lift"]
                if yb < 1.0:
                    col = C["deep"]
                elif y < lipline:
                    # Portionierer-Rand liegt im Schatten der Kugel: eine Stufe dunkler,
                    # Wuelste nur oben angelichtet
                    # nach Hoehe im Rand statt nach Normale - die vielen kleinen
                    # Wuelste wuerden sonst ein Streifenrauschen ergeben
                    h = (y - st["lift"]) / max(lipline - st["lift"], 1e-3)
                    side = (x - lean_shift(st, np.array(y))[()]) / rx
                    if h > 0.72:
                        col = C["base"] if side < 0.1 else C["mid"]
                    elif h > 0.38:
                        col = C["mid"] if side < -0.2 else (C["shade"] if side < 0.55 else C["bounce"])
                    else:
                        col = C["shade"] if side < 0.3 else C["deep"]
                else:
                    col = ramp(l, MINT)
                # Rille ueber dem Portionierer-Rand
                if abs(y - lipline) < 0.5 and abs(x - lean_shift(st, np.array(y))[()]) < rx * 0.8:
                    col = C["shade"] if l > 0.3 else C["deep"]
                img[r, c] = col
            elif m == DROP:
                img[r, c] = C["light"] if pix[r, min(c + 1, CELL_W - 1)] != DROP else C["base"]
            elif m == WAFER:
                U = (x - ax) * du[0] + (y - ay) * du[1]
                V = (x - ax) * du[1] - (y - ay) * du[0]
                # Falten: radiale Rillen, je Falte links hell, rechts dunkel
                fold = (math.atan2(V, U) + WAFER_HALF) / (2 * WAFER_HALF) * 5
                f = fold - math.floor(fold)
                if f < 0.16 or f > 0.90:
                    col = C["wf_d"]
                elif f < 0.45:
                    col = C["wf_l"]
                else:
                    col = C["wf_m"]
                img[r, c] = col

    def line_of(m):
        if m == SHADOW:
            return None
        if m == PUDDLE:
            return None
        if m == WAFER:
            return C["wf_line"]
        return C["line"]

    out = outline(img, pix, line_of, empty=(EMPTY, SHADOW, PUDDLE))
    # Waffel gegen Kugel abgrenzen
    for r in range(CELL_H):
        for c in range(CELL_W):
            if pix[r, c] == WAFER and BODY in [pix[min(max(r + a, 0), CELL_H - 1), min(max(c + b, 0), CELL_W - 1)]
                                              for a, b in ((1, 0), (0, 1), (0, -1))]:
                out[r, c] = C["wf_line"]
    # oberste Waffelkante faengt Licht
    for r in range(CELL_H):
        for c in range(CELL_W):
            if pix[r, c] == WAFER and tuple(out[r, c]) != C["wf_line"] and r > 0 and pix[r - 1, c] == WAFER \
                    and tuple(out[r - 1, c]) == C["wf_line"] and Yp[r, c] > ay + 3:
                out[r, c] = C["wf_hi"]
    return out, lam


# ================================================================ Details

# Schokostueckchen in Kugelkoordinaten (u = x/rx, v = (y-cy)/ry)
CHIPS = [(-0.50, 0.42, 2), (0.12, 0.66, 1), (0.55, 0.32, 2), (0.66, -0.30, 1),
         (0.28, -0.02, 2), (-0.74, -0.10, 1), (-0.30, 0.78, 1), (0.40, -0.55, 1)]


def chips(img, pix, st, face_box):
    rx, ry, cy = body_geo(st)
    for u, v, size in CHIPS:
        y = cy + v * ry
        x = u * rx + lean_shift(st, np.array(y))[()]
        c = int(math.floor(CX + x))
        r = int(math.floor(GROUND - y))
        cells = [(r, c)] if size == 1 else [(r, c), (r, c + 1), (r + 1, c)]
        for i, (rr, cc) in enumerate(cells):
            if face_box[0] <= rr <= face_box[1] and face_box[2] <= cc <= face_box[3]:
                continue
            if 0 <= rr < CELL_H and 0 <= cc < CELL_W and pix[rr, cc] == BODY \
                    and tuple(img[rr, cc]) not in (C["line"],):
                img[rr, cc] = C["ch_m"] if (i == 0 and size == 2) else C["ch_d"]


FROST = [(-0.35, 0.80), (0.05, 0.90), (0.30, 0.82), (-0.60, 0.55), (0.62, 0.58)]


def frost(img, pix, st):
    """Reifkoernchen oben auf der Kugel."""
    rx, ry, cy = body_geo(st)
    for i, (u, v) in enumerate(FROST):
        y = cy + v * ry
        x = u * rx + lean_shift(st, np.array(y))[()]
        r, c = int(math.floor(GROUND - y)), int(math.floor(CX + x))
        if 0 <= r < CELL_H and 0 <= c < CELL_W and pix[r, c] == BODY                 and tuple(img[r, c]) in (C["base"], C["light"], C["mid"]):
            img[r, c] = C["hi"] if i % 2 else C["glint"]


def glint(img, pix, lam):
    best, pos = -9, None
    for r in range(CELL_H):
        for c in range(CELL_W):
            if pix[r, c] == BODY and tuple(img[r, c]) == C["hi"] and lam[r, c] > best:
                best, pos = lam[r, c], (r, c)
    if pos:
        r, c = pos
        for dr, dc in ((0, 0), (0, -1), (-1, 0)):
            if pix[r + dr, c + dc] == BODY and tuple(img[r + dr, c + dc]) == C["hi"]:
                img[r + dr, c + dc] = C["glint"]


EYES = {
    "boese": ("b...",
              ".bb.",
              "w##.",
              "###."),
    "kneif": ("b...",
              ".bb.",
              "....",
              "###."),
    "zu":    ("....",
              "....",
              "....",
              "###."),
}
MOUTHS = {
    "grins": ("mffm",
              ".mm."),
    "auf":   ("mfrm",
              "mrtm",
              ".mm."),
    "zu":    (".mm.",),
}


def face(img, pix, st, k):
    p, loop = st["p"], st["loop"]
    rx, ry, cy = body_geo(st)
    eye, mouth = "boese", "grins"
    if W1 - 0.12 <= p < P1:
        eye, mouth = "kneif", "zu"
    elif P1 <= p < 0.62:
        mouth = "auf"
    if loop == 1 and (k % PER) in (7, 8):
        eye = "zu"
    if loop == 1 and (k % PER) in (9, 10, 11):
        mouth = "auf"
    fy = cy + 0.05 * ry
    fx = -0.32 * rx + lean_shift(st, np.array(fy))[()]
    r0 = int(round(GROUND - fy)) - 3
    cm = int(round(CX + fx))
    cols = {"#": C["eye"], "w": C["eglint"], "b": C["brow"], "m": C["mouth"],
            "r": C["maw"], "t": C["tongue"], "f": C["fang"]}
    only = (BODY,)
    stamp(img, EYES[eye], r0, cm - 4, cols, pix=pix, only=only)
    stamp(img, EYES[eye], r0, cm + 1, cols, mirror=True, pix=pix, only=only)
    # Wangen
    for cc in (cm - 5, cm + 4):
        put(img, r0 + 4, cc, C["blush"], only, pix)
    m = MOUTHS[mouth]
    stamp(img, m, r0 + 5, cm - 2, cols, pix=pix, only=only)
    return (r0, r0 + 7, cm - 5, cm + 5)


def drops_and_splats(img, pix, st):
    dr = drip(st)
    if not dr:
        return
    if dr[0] == "fall":
        _, x, y = dr
        c, r = int(round(CX + x)), int(round(GROUND - y))
        stamp(img, ("#", "l", "#"), r - 1, c, {"#": C["line"], "l": C["light"]})
    elif dr[0] == "splat":
        _, x, u = dr
        c = int(round(CX + x))
        r = GROUND - 1
        if u < 0.5:
            for dc, dy in ((-2, 1 + int(2 * u)), (2, 1 + int(2 * u))):
                put(img, r - dy, c + dc, C["pd_l"])
        ring = ("l.l",) if u < 0.5 else ("l...l",)
        stamp(img, ring, r, c - len(ring[0]) // 2, {"l": C["pd_g"]})


def snow_kick(img, st):
    """Schneespritzer hinten beim Abstossen."""
    p = st["p"]
    if not (P0 <= p < P0 + 0.30):
        return
    u = (p - P0) / 0.30
    rx, _, _ = body_geo(st)
    base_c = CX + rx + 1.5
    for i, (vx, vy) in enumerate(((3.0, 4.0), (5.0, 2.6), (1.8, 5.2))):
        x = base_c + vx * u * 2.0
        y = vy * u * 2.0 - 9.0 * u * u
        if y < 0:
            continue
        put(img, int(round(GROUND - 1 - y)), int(round(x)), C["snow"] if i != 1 else C["snow_s"])


def breath(img, st, k):
    """Eisiges Atemwoelkchen in Schleife 2."""
    if st["loop"] != 1:
        return
    j = k % PER - 9
    if not (0 <= j <= 2):
        return
    rx, ry, cy = body_geo(st)
    c = int(round(CX - 0.32 * rx - 6 - 2 * j))
    r = int(round(GROUND - cy)) + 2 - j
    shapes = [(".pp.", "pPPp", ".pp."), ("..p..", ".pPp.", "pPPPp", ".pPp."), (".p.p.", "p...p", ".p.p.")]
    stamp(img, shapes[j], r - 1, c - 2, {"p": C["puff_s"], "P": C["puff"]})


# ================================================================ Ablauf

def draw(states, k):
    st = states[k]
    pix = render(st)
    img, lam = shade(pix, st)
    glint(img, pix, lam)
    frost(img, pix, st)
    box = face(img, pix, st, k)
    chips(img, pix, st, box)
    drops_and_splats(img, pix, st)
    snow_kick(img, st)
    breath(img, st, k)
    return img


def main():
    states = simulate()
    frames = [draw(states, k) for k in range(N)]
    save_all(NAME, frames, CELL_W, CELL_H, (CX, GROUND), FPS,
             bestiary_id="Minzkugel", preview=arg_preview(), per_row=PER)


if __name__ == "__main__":
    main()
