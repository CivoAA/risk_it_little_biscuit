"""
Elite-Fliegenpilz: nach Nicks Vorlage neu gezeichnet und huepfend animiert.

  Tools/fliegenpilz_elite_quelle.png   Nicks Vorlage (KI-Pixelart, kein sauberes
                                       Raster - darum per Formel nachgezeichnet,
                                       Farben/Form/Punkte/Gesicht daraus)
  Assets/Art/Gegner/new/elite/
    fliegenpilz_elite_hop.png          12 Bilder, 44x44, eine Hopser-Schleife
  PPU 32 (stellt die Werkstatt ohnehin ein), Pivot Zellmitte.

Der Pilz kommt nur in der Luft vom Fleck: HopMovement am Prefab liest das
Animationsbild ab. Luftbilder = AIR_FIRST..AIR_LAST (stehen auch im Katalog,
EnemyCatalog.HopAirFrames). Die Sprunghoehe steckt hier im Bild.

Aufbau eines Bildes (von hinten nach vorn):
  Stiel     Formel, unten leicht bauchig, Licht von links
  Kopf      die helle Hutunterseite mit dem Gesicht - bewegt sich mit dem Hut
  Hut       Glocke mit Punkten, laeuft beim Landen einen Pixel nach
Jede Ebene wird 4x4 abgetastet, dann bekommt der Umriss die dunkle Kante.

Aufruf aus dem Projektordner:
  python Tools/fliegenpilz_elite.py [--preview pfad.gif]
"""

import math
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "Assets", "Art", "Gegner", "new", "elite")
NAME = "fliegenpilz_elite_hop"
BESTIARY = os.path.join(ROOT, "Assets", "Resources", "Bestiary", "EliteFliegenpilz.png")

CELL_W, CELL_H = 44, 44
GROUND = CELL_H - 2          # Zeile, auf der der Stiel steht
CX = CELL_W / 2.0
PPU = 32
FPS = 12
SS = 4

# Luftbilder (fuer HopMovement)
AIR_FIRST, AIR_LAST = 3, 9

# Ruhemass (Pixel)
STEM_H = 10.0
HEAD_H = 7.0
CAP_W, CAP_H = 16.0, 15.0    # halbe Breite, Hoehe


def rgb(h):
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), 255)


C = {
    "cap_line": rgb("4a1a16"),
    "red_hi":   rgb("f5503e"),
    "red":      rgb("e2202a"),
    "red_sh":   rgb("b4141e"),
    "red_rim":  rgb("861019"),
    "spot":     rgb("fff6e6"),
    "spot_sh":  rgb("ead4bc"),
    "head_hi":  rgb("f6e2c4"),
    "head":     rgb("eacca6"),
    "head_sh":  rgb("c89a70"),
    "stem_line": rgb("522a1c"),
    "stem_hi":  rgb("fcf2de"),
    "stem":     rgb("f0dcbc"),
    "stem_sh":  rgb("d6b088"),
    "eye":      rgb("2a120e"),
    "glint":    rgb("ffffff"),
    "blush":    rgb("f29496"),
    "mouth":    rgb("5a2018"),
}

# Punkte auf dem Hut: (u = x / halbe Breite, v = Hoehe / Huthoehe, Radius px)
SPOTS = [
    (0.02, 0.90, 1.9),
    (-0.30, 0.66, 2.4),
    (0.42, 0.68, 1.5),
    (0.12, 0.40, 2.6),
    (-0.62, 0.36, 1.8),
    (-0.30, 0.18, 1.3),
    (0.58, 0.22, 1.6),
    (0.86, 0.40, 1.3),
    (-0.86, 0.10, 1.5),
    (-0.52, 0.80, 1.0),
]

# Bilder: Stiel (Breite, Hoehe), Hoehe ueber Boden, Hut (Breite, Hoehe),
# Hut-Nachlauf (px, negativ = sackt ein), Gesicht
FRAMES = [
    # sx    sy    lift  hx    hy    lag  Gesicht
    (1.00, 1.00, 0,  1.00, 1.00, 0, "normal"),   # 0 steht
    (1.08, 0.84, 0,  1.04, 0.96, 0, "normal"),   # 1 geht in die Knie
    (1.16, 0.70, 0,  1.09, 0.90, 0, "kneif"),    # 2 ganz tief
    (0.88, 1.24, 2,  0.94, 1.06, 0, "normal"),   # 3 hopp!
    (0.93, 1.14, 6,  0.97, 1.04, 0, "freude"),   # 4
    (0.98, 1.04, 9,  1.00, 1.00, 0, "freude"),   # 5
    (1.00, 1.00, 10, 1.02, 0.98, 0, "freude"),   # 6 oben
    (0.98, 1.04, 9,  1.00, 1.01, 0, "normal"),   # 7
    (0.95, 1.10, 6,  0.98, 1.03, 0, "normal"),   # 8
    (0.94, 1.12, 2,  0.97, 1.04, 0, "normal"),   # 9 gleich unten
    (1.18, 0.72, 0,  1.10, 0.88, -1, "kneif"),   # 10 plumps
    (0.97, 1.06, 0,  0.98, 1.03, 0, "normal"),   # 11 federt nach
]

EMPTY, STEM, HEAD, CAP = 0, 1, 2, 3


def stem_hw(t):
    """Halbe Stielbreite bei relativer Hoehe t (0 unten, 1 oben)."""
    bauch = 2.2 * math.exp(-((t - 0.28) / 0.30) ** 2)
    hw = 5.0 + bauch
    if t < 0.12:                       # unten abgerundet
        hw *= 0.80 + 0.20 * (t / 0.12)
    return hw


def head_hw(t):
    """Hutunterseite: t = 0 oben (am Hut), 1 unten (am Stiel)."""
    return 12.0 - 5.5 * t ** 1.4


def cap_inside(x, v):
    """x in px (halbe Breite CAP_W), v = Hoehe / CAP_H. Glocke mit haengendem Rand."""
    u = abs(x) / CAP_W
    if u > 1.0:
        return False
    rand = -0.13 * u ** 4                       # aussen haengt der Rand
    if v < rand:
        return False
    if v <= 0:
        return True
    if v >= 1.0:
        return False
    return u <= (1.0 - v ** 2.3) ** 0.55


def render_layers(f):
    sx, sy, lift, hx, hy, lag, _ = f
    stem_top = lift + STEM_H * sy
    cap_base = stem_top + HEAD_H * hy + lag
    mat = np.zeros((CELL_H, CELL_W), np.int8)
    # Lagekoordinaten je Pixel fuer die Schattierung merken
    loc = np.zeros((CELL_H, CELL_W, 2), np.float32)

    for row in range(CELL_H):
        for col in range(CELL_W):
            votes = [0, 0, 0, 0]
            acc = np.zeros((4, 2))
            for a in range(SS):
                for b in range(SS):
                    X = col + (a + 0.5) / SS - CX
                    Y = GROUND - (row + (b + 0.5) / SS) + 1.0
                    m = EMPTY
                    lx = ly = 0.0
                    # Hut
                    vc = (Y - cap_base) / (CAP_H * hy)
                    if cap_inside(X / hx, vc):
                        m, lx, ly = CAP, X / hx / CAP_W, vc
                    else:
                        th = (cap_base - Y) / (HEAD_H * hy)
                        if 0 <= th <= 1 and abs(X / hx) <= head_hw(th):
                            m, lx, ly = HEAD, X / hx / head_hw(th), th
                        else:
                            ts = (Y - lift) / (STEM_H * sy)
                            if 0 <= ts <= 1.05 and abs(X / sx) <= stem_hw(min(ts, 1)):
                                m, lx, ly = STEM, X / sx / stem_hw(min(ts, 1)), ts
                    votes[m] += 1
                    acc[m] += (lx, ly)
            m = int(np.argmax(votes))
            if votes[EMPTY] > SS * SS // 2:
                m = EMPTY
            elif votes[m] < max(votes[1:]):
                m = int(np.argmax(votes[1:])) + 1
            if m == EMPTY:
                continue
            mat[row, col] = m
            loc[row, col] = acc[m] / votes[m]
    return mat, loc, cap_base, hx, hy


def shade(mat, loc, cap_base, hx, hy):
    img = np.zeros((CELL_H, CELL_W, 4), np.uint8)
    H, W = mat.shape

    def at(r, c):
        return mat[r, c] if 0 <= r < H and 0 <= c < W else EMPTY

    for r in range(H):
        for c in range(W):
            m = mat[r, c]
            if m == EMPTY:
                continue
            u, v = loc[r, c]
            if m == CAP:
                light = -0.55 * u + 0.75 * v
                col = C["red_hi"] if light > 0.55 else C["red"] if light > -0.05 else C["red_sh"]
                if at(r + 1, c) == HEAD or at(r + 1, c) == STEM:
                    col = C["red_rim"]
                elif at(r + 2, c) in (HEAD, STEM) and u > 0.2:
                    col = C["red_sh"]
            elif m == HEAD:
                col = C["head"]
                if at(r - 1, c) == CAP:
                    col = C["head_sh"]          # Schatten direkt unterm Hut
                elif u < -0.35 and v > 0.3:
                    col = C["head_hi"]
                elif u > 0.6:
                    col = C["head_sh"]
            else:
                col = C["stem"]
                if at(r - 1, c) == HEAD:
                    col = C["stem_sh"]          # Schatten unterm Kopf
                elif u < -0.15:
                    col = C["stem_hi"]
                elif u > 0.55:
                    col = C["stem_sh"]
            img[r, c] = col

    # Punkte (auf dem Hut, in Hutkoordinaten)
    for (su, sv, rad) in SPOTS:
        for r in range(H):
            for c in range(W):
                if mat[r, c] != CAP:
                    continue
                X = (c + 0.5 - CX) / hx
                Y = (GROUND - (r + 0.5) + 1.0 - cap_base) / hy
                dx = X - su * CAP_W
                dy = (Y - sv * CAP_H) / 0.85
                d = math.hypot(dx, dy)
                if d <= rad:
                    img[r, c] = C["spot_sh"] if (dx + dy * -1) > rad * 0.55 and rad > 1.4 else C["spot"]

    # Umriss: Randpixel werden dunkel
    out = img.copy()
    for r in range(H):
        for c in range(W):
            m = mat[r, c]
            if m == EMPTY:
                continue
            rand = any(at(r + dr, c + dc) == EMPTY for dr, dc in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            if not rand:
                continue
            out[r, c] = C["cap_line"] if m == CAP else C["stem_line"]
    return out


def face(img, mat, f, cap_base, hy):
    """Gesicht auf der Hutunterseite, Mitte = Pilzmitte."""
    kind = f[6]
    # Augenzeile: knapp unter dem Hut
    eye_y = cap_base - HEAD_H * hy * 0.42          # Mitte der Augen (Welt, nach oben)
    er = int(round(GROUND + 1.0 - eye_y))           # Zeile
    cx = int(CX)                                    # erste Spalte rechts der Mitte

    def put(r, c, col):
        if 0 <= r < CELL_H and 0 <= c < CELL_W and mat[r, c] != EMPTY:
            img[r, c] = col

    for side, ex in ((-1, cx - 5), (1, cx + 3)):    # linkes Auge Spalten ex..ex+1
        if kind == "kneif":                          # > <
            if side < 0:
                put(er - 1, ex, C["eye"]); put(er, ex + 1, C["eye"]); put(er + 1, ex, C["eye"])
            else:
                put(er - 1, ex + 1, C["eye"]); put(er, ex, C["eye"]); put(er + 1, ex + 1, C["eye"])
        elif kind == "freude":                       # ^ ^
            m = ex if side < 0 else ex + 1           # Spitze, spiegelgleich
            put(er, m - 1, C["eye"]); put(er - 1, m, C["eye"]); put(er, m + 1, C["eye"])
        else:                                        # runde Knopfaugen 2x3 mit Glanz
            for dr in (-1, 0, 1):
                put(er + dr, ex, C["eye"]); put(er + dr, ex + 1, C["eye"])
            put(er - 1, ex, C["glint"])
        # Baeckchen
        bc = ex - 2 if side < 0 else ex + 3
        put(er + 2, bc, C["blush"]); put(er + 2, bc + (1 if side < 0 else -1), C["blush"])

    # Mund
    mr = er + 2
    if kind == "freude":                             # offen, kleines o
        put(mr, cx - 1, C["mouth"]); put(mr, cx, C["mouth"])
        put(mr + 1, cx - 1, C["mouth"]); put(mr + 1, cx, C["mouth"])
    elif kind == "kneif":
        put(mr, cx - 1, C["mouth"]); put(mr, cx, C["mouth"])
    else:                                            # Laecheln
        put(mr, cx - 2, C["mouth"]); put(mr + 1, cx - 1, C["mouth"])
        put(mr + 1, cx, C["mouth"]); put(mr, cx + 1, C["mouth"])


def draw(f):
    mat, loc, cap_base, hx, hy = render_layers(f)
    img = shade(mat, loc, cap_base, hx, hy)
    face(img, mat, f, cap_base, hy)
    return img


def main():
    args = sys.argv[1:]
    frames = [draw(f) for f in FRAMES]
    strip = np.concatenate(frames, axis=1)
    os.makedirs(OUT_DIR, exist_ok=True)
    png = os.path.join(OUT_DIR, NAME + ".png")
    Image.fromarray(strip).save(png)
    meta = png + ".meta"
    if not os.path.exists(meta):
        sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
        from unity_meta import write_strip_meta
        write_strip_meta(meta, NAME, len(frames), CELL_W, CELL_H, PPU)
    print("%s  %d Bilder  %dx%d  Luft %d..%d" % (NAME, len(frames), CELL_W, CELL_H, AIR_FIRST, AIR_LAST))

    # Bestiarium: erstes Bild, auf die Figur zugeschnitten
    first = Image.fromarray(frames[0])
    first.crop(first.getbbox()).save(BESTIARY)

    if "--preview" in args:
        path = args[args.index("--preview") + 1]
        k = 8
        pics = []
        for fr in frames:
            im = Image.new("RGBA", (CELL_W, CELL_H), (120, 150, 110, 255))
            im.alpha_composite(Image.fromarray(fr))
            pics.append(im.resize((CELL_W * k, CELL_H * k), Image.NEAREST).convert("P"))
        pics[0].save(path, save_all=True, append_images=pics[1:], duration=int(1000 / FPS), loop=0)
        sheet = Image.new("RGBA", strip.shape[1::-1], (120, 150, 110, 255))
        sheet.alpha_composite(Image.fromarray(strip))
        sheet.resize((sheet.width * 6, sheet.height * 6), Image.NEAREST).save(path.replace(".gif", "_sheet.png"))


if __name__ == "__main__":
    main()
