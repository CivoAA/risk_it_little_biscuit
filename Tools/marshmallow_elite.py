"""
Elite-Marshmello (EnemyId.EliteMarshmello): der geroestete grosse Bruder.

Gleiche Bauweise wie der normale Marshmello (Tools/marshmallow.py - Zylinder
mit Deckel, Stummelbeine), aber deutlich anders und groesser:

  * ueber dem Lagerfeuer geroestet: dunkle Kruste auf Deckel und oberem
    Mantel, darunter goldbraun; gerade Seiten, keine Stufen.
  * boese: dicke schraege Brauen, Zahnreihe mit zwei Hauern.
  * schwerer, ruhiger Gang: langsamere Schritte, 1 px Wippen, kaum Kippen.
    Hoehe und Schraeglage nur auf ganzen Pixeln.
  * zwei Schleifen: Laufen, Laufen mit Blinzeln.

  Assets/Art/Gegner/new/marshmallow_elite_lauf.png   2 x 16 Bilder, 56x56
  PPU 32, Pivot unten Mitte, Fuesse 3 px ueber dem Pivot.
  Assets/Resources/Bestiary/EliteMarshmello.png       Bild 0, zugeschnitten

Aufruf aus dem Projektordner:
  python Tools/marshmallow_elite.py [--preview pfad.gif]
"""

import math
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import marshmallow as mm   # noqa: E402  Form, Beine, Rendern werden geteilt

ROOT = mm.ROOT
OUT_DIR = mm.OUT_DIR
NAME = "marshmallow_elite_lauf"
BESTIARY = os.path.join(ROOT, "Assets", "Resources", "Bestiary", "EliteMarshmello.png")

# Masse ueberschreiben: die geteilten Funktionen lesen sie aus dem Modul
mm.CELL_W, mm.CELL_H = 56, 56
mm.GROUND = 53
mm.CX = 28.0
mm.HALF = 17.0
mm.BODY_H = 30.0
mm.CAP_E = 4.6
mm.BOT_E = 2.0
mm.LEG = 4
mm.LEG_X = 9.0
mm.LEG_W = 7
mm.half_at = lambda y: mm.HALF + 0.0 * y   # gerade Seiten: keine Stufen
CELL_W, CELL_H, GROUND, CX = mm.CELL_W, mm.CELL_H, mm.GROUND, mm.CX
HALF, BODY_H, CAP_E = mm.HALF, mm.BODY_H, mm.CAP_E

PPU = 32
FPS = 16
PER = 16                     # Bilder je Schleife (= zwei Schritte, schwerer)
LOOPS = 2
N = PER * LOOPS

rgb = mm.rgb
C = {
    "line":    rgb("2b1208"),
    # Kruste, dunkel -> hell
    "char":    rgb("5e2810"),
    "cr_dd":   rgb("6e3214"),
    "cr_d":    rgb("8f461b"),
    "cr_m":    rgb("b0602a"),
    "cr_l":    rgb("cd8139"),
    "cr_hi":   rgb("e3a552"),
    # Fuellung, dunkel -> hell
    "fi_d":    rgb("b97a45"),
    "fi_m":    rgb("d89d5e"),
    "fi_b":    rgb("ebbd7f"),
    "fi_l":    rgb("f6d6a2"),
    "fi_hi":   rgb("fde9c6"),
    "glint":   rgb("fffbf2"),
    "eye":     rgb("200c06"),
    "eglint":  rgb("ffffff"),
    "tooth":   rgb("fff6ea"),
    "maw":     rgb("5a1a10"),
    "dust":    rgb("fff3e2", 235),
    "dust2":   rgb("f1dcc4", 190),
    "shadow":  (35, 18, 8, 75),
}
mm.C = dict(mm.C, shadow=C["shadow"], dust=C["dust"], dust2=C["dust2"])

EMPTY, SHADOW, LEGP, BODY = mm.EMPTY, mm.SHADOW, mm.LEGP, mm.BODY


# ================================================================ Bewegung

def simulate():
    sub = 40
    dt = 1.0 / (FPS * sub)
    sq = mm.Spring(600.0, 14.0, 1.0)
    tl = mm.Spring(300.0, 16.0, 0.0)
    out = None
    for rep in range(6):
        rec = []
        for i in range(N * sub):
            k = i / sub
            p = (k % PER) / PER
            q = (2 * p) % 1.0
            left = p < 0.5
            s = math.sin(math.pi * q)
            contact = q < 1.0 / (PER * sub)
            sy = sq.step(1.0, dt, -0.7 if contact else 0.0)
            t = tl.step((-1.8 if left else 1.8) * s, dt)
            if i % sub == 0:
                rec.append(dict(loop=int(k // PER), f=int(k) % PER, p=p,
                                lift=1.0 * s, foot=2.0 * s, left=left,
                                tilt=t, sy=round(BODY_H * sy) / BODY_H, shear=0.0,
                                eye="auge", legs="boden", dust=[]))
        out = rec
    # Blinzeln in Schleife 2
    for j in (9, 10):
        out[PER + j]["eye"] = "zu"
    out[PER + 11]["eye"] = "halb"
    # Puder am Fuss bei jedem Aufsetzen
    for loop in range(LOOPS):
        for (f0, side) in ((0, 1), (PER // 2, -1)):
            for age in range(4):
                out[(loop * PER + f0 + age) % N]["dust"].append((side * mm.LEG_X, age, 1.0))
    return out


# ================================================================ Material

def crust_line(x):
    """Unterkante der dunklen Kruste (lokales y), sanft gewellt, fest im Koerper."""
    base = BODY_H * 0.58 + 0.7 * math.sin(x * 0.42 + 0.7)
    for (cx, w, d) in ():
        u = (x - cx) / w
        if abs(u) < 1.0:
            base -= d * math.sqrt(1 - u * u)      # runde Nase nach unten
    return base


def shade(pix, st):
    img = np.zeros((CELL_H, CELL_W, 4), np.uint8)
    ys, xs = np.mgrid[0:CELL_H, 0:CELL_W]
    lx, ly = mm.to_local(st, xs + 0.5 - CX, GROUND - (ys + 0.5))
    cap = np.zeros((CELL_H, CELL_W), bool)
    crust = np.zeros((CELL_H, CELL_W), bool)
    L = mm.LIGHT

    def at(r, c):
        return pix[r, c] if 0 <= r < CELL_H and 0 <= c < CELL_W else EMPTY

    cr = ["cr_dd", "cr_d", "cr_m", "cr_l", "cr_hi"]
    fi = ["fi_d", "fi_m", "fi_b", "fi_l", "fi_hi"]

    for r in range(CELL_H):
        for c in range(CELL_W):
            m = pix[r, c]
            if m == EMPTY:
                continue
            if m == SHADOW:
                img[r, c] = C["shadow"]
                continue
            if m == LEGP:
                col = C["fi_m"]
                if at(r, c - 2) != LEGP:
                    col = C["fi_b"]
                elif at(r, c + 2) != LEGP:
                    col = C["fi_d"]
                if at(r + 2, c) != LEGP:
                    col = C["cr_d"]                # angeroestete Sohle
                if at(r - 1, c) == BODY or at(r - 2, c) == BODY:
                    col = C["fi_d"]
                img[r, c] = col
                continue
            x, y = lx[r, c], ly[r, c]
            a = float(mm.half_at(y))
            t = max(-1.0, min(1.0, x / a))
            rr = math.sqrt(1 - t * t)
            rim = BODY_H - CAP_E - CAP_E * rr
            if y > rim + 0.5:
                v = 0.95 - 0.30 * max(t, 0) - 0.08 * (y - rim) / (2 * CAP_E)
                idx = 4 if v > 0.88 else 3 if v > 0.74 else 2
                cap[r, c] = crust[r, c] = True
                col = C[cr[idx]]
            else:
                nx, ny, nz = t, 0.0, rr
                if y < 3.0:
                    ny = -(3.0 - y) / 3.0 * 0.9
                ln = math.sqrt(nx * nx + ny * ny + nz * nz) + 1e-6
                lam = (nx * L[0] + ny * L[1] + nz * L[2]) / ln
                idx = 4 if lam > 0.85 else 3 if lam > 0.60 else 2 if lam > 0.28 else 1 if lam > -0.05 else 0
                is_crust = y > crust_line(x)
                crust[r, c] = is_crust
                if y < 1.0:
                    idx = 0
                col = C[(cr if is_crust else fi)[idx]]
            img[r, c] = col

    for r in range(1, CELL_H):
        for c in range(CELL_W):
            if pix[r, c] != BODY:
                continue
            # Falte zwischen Deckel und Mantel
            if not cap[r, c] and cap[r - 1, c]:
                t = lx[r, c] / HALF
                img[r, c] = C["cr_m"] if t < -0.45 else C["cr_d"] if t < 0.45 else C["cr_dd"]
            # Krustenkante: weicher Uebergang, die unterste Krustenreihe eine Stufe heller
            elif not crust[r, c] and crust[r - 1, c] and not cap[r - 1, c]:
                img[r - 1, c] = C["cr_l"] if lx[r, c] < HALF * 0.4 else C["cr_m"]

    out = img.copy()
    for r in range(CELL_H):
        for c in range(CELL_W):
            m = pix[r, c]
            if m in (EMPTY, SHADOW):
                continue
            nb = [at(r + a, c + b) for a, b in ((1, 0), (-1, 0), (0, 1), (0, -1))]
            if any(n in (EMPTY, SHADOW) for n in nb):
                out[r, c] = C["line"]
            elif m == BODY and at(r + 1, c) == LEGP:
                out[r, c] = C["line"]
    return out


# ================================================================ Gesicht

# 'B' Braue, '#' Auge, 'w' Glanz
EYES = {
    "auge": ("BB.....",
             ".BBBB..",
             "...BBBB",
             ".#####.",
             "##ww###",
             "##ww###",
             "#######",
             ".#####."),
    "halb": ("BB.....",
             ".BBBB..",
             "...BBBB",
             ".......",
             ".......",
             "#######",
             "####ww#",
             ".#####."),
    "zu":   ("BB.....",
             ".BBBB..",
             "...BBBB",
             ".......",
             ".......",
             ".......",
             "#######",
             "......."),
}
# Zaehne mit zwei Hauern: 'f' Zahn, 'r' Rachen
MOUTH = (".###########.",
         "#fr#f#f#f#rf#",
         "#.#rrrrrrr#.#",
         ".###########.")


def stamp(img, pix, rows, r0, c0, mirror=False):
    for dr, row in enumerate(rows):
        row = row[::-1] if mirror else row
        for dc, ch in enumerate(row):
            if ch == ".":
                continue
            r, c = r0 + dr, c0 + dc
            if 0 <= r < CELL_H and 0 <= c < CELL_W and pix[r, c] == BODY:
                img[r, c] = {"B": C["eye"], "#": C["eye"], "w": C["eglint"],
                             "f": C["tooth"], "r": C["maw"]}[ch]


def face(img, pix, st):
    fx, fy = mm.to_world(st, 1.0, BODY_H * 0.46)
    c_mid = int(round(CX + fx))
    r0 = int(round(GROUND - fy)) - 7
    rows = EYES[st["eye"]]
    stamp(img, pix, rows, r0, c_mid - 9)
    stamp(img, pix, rows, r0, c_mid + 2, mirror=True)
    stamp(img, pix, MOUTH, r0 + 10, c_mid - 6)


# ================================================================ Ablauf

def draw(st, k):
    pix = mm.render(st)
    img = shade(pix, st)
    face(img, pix, st)
    mm.dust(img, st)
    return img


def main():
    args = sys.argv[1:]
    states = simulate()
    frames = [draw(st, k) for k, st in enumerate(states)]
    strip = np.concatenate(frames, axis=1)
    png = os.path.join(OUT_DIR, NAME + ".png")
    Image.fromarray(strip).save(png)
    meta = png + ".meta"
    if not os.path.exists(meta):
        from unity_meta import write_strip_meta
        write_strip_meta(meta, NAME, len(frames), CELL_W, CELL_H, PPU, pivot=(0.5, 0.0))
    print("%s  %d Bilder  %dx%d  %d fps" % (NAME, len(frames), CELL_W, CELL_H, FPS))

    # Bestiarium: Bild 0 ohne Schatten
    a = frames[0]
    solid = a[..., 3] == 255
    rows = np.where(solid.any(1))[0]
    cols = np.where(solid.any(0))[0]
    crop = a[rows[0]:rows[-1] + 1, cols[0]:cols[-1] + 1].copy()
    crop[crop[..., 3] < 255] = 0
    Image.fromarray(crop).save(BESTIARY)

    if "--preview" in args:
        path = args[args.index("--preview") + 1]
        k = 6
        bg = (120, 150, 110, 255)
        pics = []
        for fr in frames:
            im = Image.new("RGBA", (CELL_W, CELL_H), bg)
            im.alpha_composite(Image.fromarray(fr))
            pics.append(im.resize((CELL_W * k, CELL_H * k), Image.NEAREST).convert("P"))
        pics[0].save(path, save_all=True, append_images=pics[1:], duration=int(1000 / FPS), loop=0)
        sheet = Image.new("RGBA", (CELL_W * PER, CELL_H * LOOPS), bg)
        for n, fr in enumerate(frames):
            sheet.alpha_composite(Image.fromarray(fr), ((n % PER) * CELL_W, (n // PER) * CELL_H))
        sheet.resize((sheet.width * 2, sheet.height * 2), Image.NEAREST).save(path.replace(".gif", "_sheet.png"))


if __name__ == "__main__":
    main()
