"""
Raketeneis (EnemyId.Raketeneis, Eiswelt): ein Wassereis in drei Schichten -
Kirsche, Zitrone, Limette - mit Raketenflossen, das auf seinem Holzstiel wie
auf einem Pogo-Stick ueber den Gletscher springt.

  * Sprungtakt: abfedern (rutscht den Stiel hinunter, lehnt zurueck),
    Absprung (streckt sich, Frostwolke puffert aus dem Stiel - es ist ja eine
    Rakete), Flug mit Vorlage, Landung mit Stauchung und Schneespritzern.
  * Schatten bleibt am Boden und schrumpft mit der Flughoehe.
  * Die Flughoehe steckt im Bild: der Pivot sitzt am Boden. Bewegung nur in
    der Luft (HopMovement, Luftbilder 3..9 je Schleife, cycleFrames 12).
  * Zwei Schleifen: normal und Blinzeln (im Flug - "Juhu").

  Assets/Art/Gegner/new/eis/raketeneis_hops.png   2 x 12 Bilder, 32x56, PPU 32
  Pivot = Bodenmitte unter dem Stiel.
  Assets/Resources/Bestiary/Raketeneis.png         Bild 0, zugeschnitten

Aufruf aus dem Projektordner:
  python Tools/eis_raketeneis.py [--preview pfad.gif]
"""

import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from eis_gegner_kit import (LIGHT, arg_preview, downsample, grid, outline,  # noqa: E402
                            pixel_grid, put, ramp, rgb, save_all, stamp)

NAME = "raketeneis_hops"
CELL_W, CELL_H = 32, 56
GROUND = 53
CX = 16.0
FPS = 14
PER = 12
LOOPS = 2
N = PER * LOOPS
SS = 6
AIR_FIRST, AIR_LAST = 3, 9

# Masse (lokal, y nach oben ab Stielende)
STICK_W = 2.0            # halbe Breite
STICK_TOP = 12.0
BODY0 = 8.0              # Unterkante Eis
G_TOP = 16.5             # Limette bis hier
Y_TOP = 27.0             # Zitrone bis hier
TIP = 37.0               # Kirschspitze
HW_G, HW_Y, HW_R = 7.0, 6.2, 5.6


def pal(*h):
    return tuple(rgb(x) for x in h)


KIRSCH = pal("5e0c1e", "8e1428", "bc2232", "e23a42", "ff6e66", "ffc4b6")
ZITRO = pal("8a5a12", "c08a1c", "e8b52a", "fbd648", "fff08a", "fffbe0")
LIMET = pal("1f5a1c", "2f8a2a", "4fb53a", "7ad64a", "b0ee78", "e6ffc4")
C = {
    "l_red": rgb("3a0a14"),
    "l_yel": rgb("4a2c0a"),
    "l_grn": rgb("10361a"),
    "l_stk": rgb("4e3018"),
    "stk_d": rgb("a67c46"),
    "stk_m": rgb("d4ae74"),
    "stk_l": rgb("eed4a0"),
    "glint": rgb("ffffff"),
    "frost": rgb("f4fbff"),
    "eye":   rgb("1a0c0a"),
    "eglint": rgb("ffffff"),
    "brow":  rgb("4a2c0a"),
    "mouth": rgb("2a1008"),
    "maw":   rgb("7a1e22"),
    "tongue": rgb("f07a7a"),
    "tooth": rgb("ffffff"),
    "blush": rgb("ff8a6a"),
    "puff":  rgb("f4f6ff"),
    "puff_m": rgb("c4cbee"),
    "puff_s": rgb("8d97cc"),
    "spark": rgb("ffffff"),
    "spark_b": rgb("9fd8ff"),
    "shadow": (64, 58, 110, 70),
}

# Schluesselbilder: (Hoehe, Stauchung, Rutsch am Stiel, Neigung)
#   Neigung < 0 = nach vorn (links, Blickrichtung), > 0 = zurueck
KEYS = [
    (0.0, -0.04, 0.0, 0.0),     # 0 erholt
    (0.0, -0.13, 1.5, 0.6),     # 1 abfedern
    (0.0, -0.22, 3.0, 1.0),     # 2 tief
    (2.0, 0.20, 0.0, -1.4),     # 3 Absprung
    (6.0, 0.11, 0.0, -2.2),     # 4
    (9.0, 0.04, 0.0, -1.8),     # 5
    (10.5, 0.0, 0.0, -0.8),     # 6 Scheitel
    (9.5, 0.02, 0.0, 0.4),      # 7
    (6.5, 0.06, 0.0, 1.0),      # 8
    (2.5, 0.10, 0.0, 0.6),      # 9
    (0.0, -0.20, 2.0, -0.4),    # 10 Landung
    (0.0, 0.07, 0.0, 0.0),      # 11 nachfedern
]


def state(k):
    j = k % PER
    lift, sq, slide, tilt = KEYS[j]
    return dict(j=j, loop=k // PER, lift=lift, sq=sq, slide=slide, tilt=tilt)


# ================================================================ Formen

def hw_at(yb):
    """Halbe Breite des Eises auf Hoehe yb (lokal, ungestaucht)."""
    hw = np.where(yb < G_TOP, HW_G, np.where(yb < Y_TOP, HW_Y,
                  HW_R * np.clip((TIP - yb) / (TIP - Y_TOP), 0, 1) ** 0.75))
    # runde Unterkante der Limette
    r = 2.6
    lo = yb - BODY0
    bottom = np.where(lo < r, HW_G - r + np.sqrt(np.clip(r * r - (r - lo) ** 2, 0, None)), hw)
    hw = np.where(yb < BODY0 + r, bottom, hw)
    # Spitze rund
    return hw


def local(st, X, Y):
    """Weltraster -> lokale Koordinaten des Eises (xl, yb) und des Stiels (xs, ys)."""
    ys = Y - st["lift"]
    shear = st["tilt"] * np.clip(ys, 0, None) / 30.0
    xs = X - shear
    # Eis rutscht am Stiel hinunter und staucht sich ueber seiner Unterkante
    base = BODY0 - st["slide"]
    yb = (ys - base) / (1 + st["sq"]) + BODY0
    xl = xs / (1 - 0.5 * st["sq"])
    return xl, yb, xs, ys


EMPTY, SHADOW, STICK, GREEN, YELLOW, RED, FIN = range(7)


def classes(st, X, Y):
    xl, yb, xs, ys = local(st, X, Y)
    owner = np.zeros(X.shape, np.int16)
    sw = max(0.0, 1 - st["lift"] / 14.0)
    owner[((X - 0.3) / (7.5 * (0.55 + 0.45 * sw))) ** 2 + ((Y + 0.3) / 2.2) ** 2 <= 1.0] = SHADOW
    stick = (np.abs(xs) <= STICK_W) & (ys >= 0) & (ys <= STICK_TOP)
    stick &= ~((ys < 1.0) & (np.abs(xs) > STICK_W - 1.0) & ((np.abs(xs) - STICK_W + 1.0) ** 2 + (1.0 - ys) ** 2 > 1.0))
    owner[stick] = STICK
    # Flossen: Dreiecke an der Limette, nach unten aussen
    for sgn in (-1, 1):
        u = sgn * xl
        fin = (u >= HW_G - 1.0) & (yb >= BODY0 - 2.0) & (yb <= BODY0 + 6.0)
        fin &= (u - (HW_G - 1.0)) <= (BODY0 + 6.0 - yb) * 0.55
        fin &= u <= HW_G + 3.2
        owner[fin] = FIN
    hw = hw_at(yb)
    inside = (np.abs(xl) <= hw) & (yb >= BODY0) & (yb <= TIP)
    owner[inside & (yb < G_TOP)] = GREEN
    owner[inside & (yb >= G_TOP) & (yb < Y_TOP)] = YELLOW
    owner[inside & (yb >= Y_TOP)] = RED
    return owner


def render(st):
    X, Y = grid(CELL_W, CELL_H, SS, CX, GROUND)
    owner = classes(st, X, Y)
    owner[Y < 0] = EMPTY
    return downsample(owner, CELL_W, CELL_H, SS, keep_thin=(STICK, FIN))


PAL = {GREEN: LIMET, YELLOW: ZITRO, RED: KIRSCH, FIN: KIRSCH}
LINE = {GREEN: "l_grn", YELLOW: "l_yel", RED: "l_red", FIN: "l_red", STICK: "l_stk"}


def shade(pix, st):
    img = np.zeros((CELL_H, CELL_W, 4), np.uint8)
    Xp, Yp = pixel_grid(CELL_W, CELL_H, CX, GROUND)
    xl, yb, xs, ys = local(st, Xp, Yp)
    hw = hw_at(yb)
    for r in range(CELL_H):
        for c in range(CELL_W):
            m = pix[r, c]
            if m == EMPTY:
                continue
            if m == SHADOW:
                img[r, c] = C["shadow"]
                continue
            if m == STICK:
                u = xs[r, c] / STICK_W
                img[r, c] = C["stk_l"] if u < -0.2 else (C["stk_m"] if u < 0.5 else C["stk_d"])
                continue
            if m == FIN:
                img[r, c] = KIRSCH[3] if xl[r, c] < 0 else KIRSCH[1]
                continue
            # Zylinder (Kegel oben): Normale aus der Lage quer zur Achse
            u = float(np.clip(xl[r, c] / max(hw[r, c], 0.8), -1, 1))
            ny = 0.0
            if m == RED:
                ny = 0.42
            elif yb[r, c] > G_TOP - 1.3 and m == GREEN or yb[r, c] > Y_TOP - 1.3 and m == YELLOW:
                ny = 0.55                                       # Schulter zur naechsten Schicht
            nz = math.sqrt(max(0.0, 1 - u * u))
            n = np.array([u, ny, nz])
            n /= np.linalg.norm(n)
            l = float(n @ LIGHT)
            col = ramp(l, PAL[m], cuts=(0.90, 0.70, 0.42, 0.10))
            img[r, c] = col

    def line_of(m):
        return C[LINE[m]] if m in LINE else None

    out = outline(img, pix, line_of, empty=(EMPTY, SHADOW))
    # Schichtfugen: oberster Pixel der unteren Schicht wird dunkler
    for r in range(1, CELL_H):
        for c in range(CELL_W):
            m, up = pix[r, c], pix[r - 1, c]
            if (m, up) in ((GREEN, YELLOW), (YELLOW, RED)) and tuple(out[r, c]) != C[LINE[m]]:
                out[r, c] = PAL[up][1] if Xp[r, c] - st["tilt"] * 0.5 > 0 else PAL[up][2]
            if m == STICK and up in (GREEN, FIN):
                out[r, c] = C["l_stk"]
            if m in (GREEN, YELLOW, RED) and up == STICK:
                pass
    # Flossen vom Koerper trennen
    for r in range(CELL_H):
        for c in range(1, CELL_W - 1):
            if pix[r, c] == FIN and (pix[r, c - 1] == GREEN or pix[r, c + 1] == GREEN):
                out[r, c] = C["l_red"]
    return out


# ================================================================ Details

def world_px(st, xl, yb):
    """Lokaler Punkt -> Bildpixel (r, c)."""
    base = BODY0 - st["slide"]
    ys = (yb - BODY0) * (1 + st["sq"]) + base
    xs = xl * (1 - 0.5 * st["sq"])
    x = xs + st["tilt"] * max(ys, 0) / 30.0
    y = ys + st["lift"]
    return int(math.floor(GROUND - y)), int(math.floor(CX + x))


def frost(img, pix, st):
    """Reif und Glanzkante auf der Lichtseite."""
    for xl, yb in ((-3.6, 12.0), (-4.2, 21.5), (-2.4, 30.5), (3.5, 14.8), (-1.6, 34.0), (2.8, 23.8)):
        r, c = world_px(st, xl, yb)
        if 0 <= r < CELL_H and 0 <= c < CELL_W and pix[r, c] in (GREEN, YELLOW, RED):
            img[r, c] = C["frost"]
    # senkrechter Glanzstreif auf jeder Schicht
    for y0, y1, x in ((BODY0 + 2.5, G_TOP - 2.0, -4.6), (G_TOP + 1.5, Y_TOP - 2.0, -4.0), (Y_TOP + 1.0, Y_TOP + 4.0, -2.9)):
        yb = y0
        while yb <= y1:
            r, c = world_px(st, x, yb)
            if 0 <= r < CELL_H and 0 <= c < CELL_W and pix[r, c] in (GREEN, YELLOW, RED):
                img[r, c] = PAL[pix[r, c]][5]
            yb += 1.0


EYES = {
    "frech": ("bb.",
              "w#.",
              "##."),
    "kneif": ("b..",
              ".bb",
              "bb."),
    "gross": ("##.",
              "#w#",
              "###",
              ".#."),
    "zu":    ("...",
              "...",
              "###"),
    "juhu":  (".#.",
              "#.#",
              "..."),
}
MOUTHS = {
    "grins": ("m...m",
              ".mmm."),
    "schrei": (".mmm.",
               "mrrrm",
               "mrtrm",
               ".mmm."),
    "zahn":  ("mmmmm",
              "mfffm",
              ".mmm."),
    "zu":    (".mmm.",),
}


def face(img, pix, st):
    j, loop = st["j"], st["loop"]
    eye, mouth = "frech", "grins"
    if j in (1, 2):
        eye, mouth = "kneif", "zahn"
    elif j == 3:
        eye, mouth = "kneif", "schrei"
    elif 4 <= j <= 8:
        eye, mouth = "gross", "schrei" if j < 6 else "grins"
        if loop == 1:
            eye = "juhu"
    elif j == 10:
        eye, mouth = "kneif", "zu"
    xl0 = -1.4                                     # Gesicht leicht nach vorn
    r_eye, c_mid = world_px(st, xl0, Y_TOP - 3.6)
    cols = {"#": C["eye"], "w": C["eglint"], "b": C["brow"], "m": C["mouth"],
            "r": C["maw"], "t": C["tongue"], "f": C["tooth"]}
    rows = EYES[eye]
    only = (YELLOW, GREEN)
    stamp(img, rows, r_eye, c_mid - 3, cols, pix=pix, only=only)
    stamp(img, rows, r_eye, c_mid + 1, cols, mirror=True, pix=pix, only=only)
    put(img, r_eye + 3, c_mid - 4, C["blush"], only, pix)
    put(img, r_eye + 3, c_mid + 3, C["blush"], only, pix)
    m = MOUTHS[mouth]
    stamp(img, m, r_eye + len(rows) + 1, c_mid - 2, cols, pix=pix, only=only)


PUFFS = {
    1: ("..p..",
        ".pPp.",
        "..p.."),
    2: (".ppp.",
        "pPPPp",
        "pPPmp",
        ".pmp."),
    3: (".pp.pp.",
        "pPPpPPp",
        "pPPPPmp",
        ".pmmmp."),
    4: (".p...p.",
        "p.P.P.p",
        ".p...p."),
}


def puff(img, r, c, size):
    rows = PUFFS[size]
    stamp(img, rows, r - len(rows) + 1, c - len(rows[0]) // 2,
          {"p": C["puff_s"], "P": C["puff"], "m": C["puff_m"]})


def exhaust(img, st):
    """Frost-Rueckstoss: Wolke unter dem Stiel beim Absprung, Spur im Steigflug."""
    j = st["j"]
    g = GROUND - 1
    foot = GROUND - 1 - int(round(st["lift"]))           # Stielende
    if j == 3:
        puff(img, g, int(CX + 1), 3)
        puff(img, g, int(CX - 5), 2)
        puff(img, g, int(CX + 6), 2)
    elif j == 4:
        puff(img, foot + 3, int(CX + 1), 2)
        puff(img, g, int(CX - 6), 3)
        puff(img, g, int(CX + 7), 3)
        puff(img, g, int(CX + 1), 1)
    elif j == 5:
        puff(img, foot + 3, int(CX + 2), 1)
        puff(img, g, int(CX - 8), 4)
        puff(img, g, int(CX + 9), 4)
    # Funkelkristalle steigen aus der Wolke auf
    if j in (4, 5, 6, 7):
        k = j - 4
        for x, y, col in ((-6, 3 + 2 * k, "spark"), (7, 2 + 3 * k, "spark_b"), (1, 4 + 2 * k, "spark"),
                          (-2, 1 + 3 * k, "spark_b")):
            if y + 1 < st["lift"] or abs(x) > 3:
                put(img, g - y, int(CX + x), C[col])


def landing(img, st):
    j = st["j"]
    if j == 10:
        for dx in (-6, 6):
            puff(img, GROUND - 1, int(CX + dx), 1)
    elif j == 11:
        for dx in (-8, 8):
            puff(img, GROUND - 1, int(CX + dx), 2)
    elif j == 0 and st["loop"] in (0, 1):
        for dx in (-9, 9):
            puff(img, GROUND - 2, int(CX + dx), 4)


# ================================================================ Ablauf

def draw(k):
    st = state(k)
    pix = render(st)
    img = shade(pix, st)
    frost(img, pix, st)
    face(img, pix, st)
    exhaust(img, st)
    landing(img, st)
    return img


def main():
    frames = [draw(k) for k in range(N)]
    save_all(NAME, frames, CELL_W, CELL_H, (CX, GROUND), FPS,
             bestiary_id="Raketeneis", preview=arg_preview(), per_row=PER)


if __name__ == "__main__":
    main()
