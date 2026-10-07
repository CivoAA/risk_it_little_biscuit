"""
Marshmello (EnemyId.Marshmello): neu gezeichnet und animiert.

Gleiche Figur wie das alte freeze_marshmallow.png - ein kastiger Marshmallow
auf zwei Stummelbeinen mit Knopfaugen und Zahngrinsen -, aber jetzt in echten
Marshmallow-Farben (Puderweiss mit rosa Schatten statt Eisblau) und als
Zylinder mit sichtbarem Deckel:

  * Ruhiger Watschelgang: das schwingende Bein hebt ab, der Koerper kippt
    leicht auf das Standbein und hebt sich um einen Pixel, beim Aufsetzen
    gibt er einen Pixel nach. Kippen haengt an einer gedaempften Feder.
  * Bei jedem Schritt staubt Puderzucker unter dem Fuss auf.
  * Zwei Schleifen: Watscheln, und Watscheln mit Blinzeln.

  Assets/Art/Gegner/new/marshmallow_watschel.png   2 x 12 Bilder, 40x44
  PPU 32, Pivot unten Mitte, Fuesse 3 px ueber dem Pivot - genau wie beim
  alten 32er-Bild, damit Trefferkreis (r 0.45, Versatz 0.5) und
  Lebensbalken stimmen.
  Assets/Resources/Bestiary/Marshmello.png          Bild 0, zugeschnitten

Aufruf aus dem Projektordner:
  python Tools/marshmallow.py [--preview pfad.gif]
"""

import math
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "Assets", "Art", "Gegner", "new")
NAME = "marshmallow_watschel"
BESTIARY = os.path.join(ROOT, "Assets", "Resources", "Bestiary", "Marshmello.png")

CELL_W, CELL_H = 40, 44
GROUND = 41                  # erste Zeile unter den Fuessen (3 px ueber dem Pivot)
CX = 20.0                    # Koerpermitte = Pivot
PPU = 32
FPS = 16
PER = 12                     # Bilder je Schleife (= zwei Schritte)
LOOPS = 2                    # Watscheln, Watscheln mit Blinzeln
N = PER * LOOPS
SS = 6                       # Unterabtastung

# Koerper in Ruhe (lokal: x um die Mitte, y ab Koerperunterkante nach oben)
HALF = 12.0                  # halbe Breite
BODY_H = 21.0                # Hoehe inkl. Deckel
CAP_E = 3.6                  # Halbachse der Deckel-Ellipse (Draufsicht-Anteil)
BOT_E = 1.6                  # Rundung der Unterkante
LEG = 3                      # sichtbare Beinlaenge
LEG_X = 6.5                  # Beinabstand zur Mitte
LEG_W = 5


def rgb(h, a=255):
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


C = {
    "line":    rgb("3a1d33"),
    "deep":    rgb("b9799a"),
    "shade":   rgb("d898b5"),
    "mid":     rgb("ebbacd"),
    "base":    rgb("fadfe8"),
    "light":   rgb("fff0f4"),
    "hi":      rgb("fffaf8"),
    "glint":   rgb("ffffff"),
    "blush":   rgb("f39bb8"),
    "eye":     rgb("2a1426"),
    "eglint":  rgb("ffffff"),
    "mouth":   rgb("2a1426"),
    "maw":     rgb("7a2c4a"),
    "tongue":  rgb("e2647f"),
    "tooth":   rgb("fffaf8"),
    "dust":    rgb("fffdfb", 235),
    "dust2":   rgb("f7e3ea", 190),
    "shadow":  (40, 20, 35, 70),
}


# ================================================================ Bewegung

class Spring:
    """Gedaempfte Feder, die einem Ziel folgt."""

    def __init__(self, k, d, x0):
        self.k, self.d = k, d
        self.x = x0
        self.v = 0.0

    def step(self, target, dt, kick=0.0):
        self.v += kick
        a = self.k * (target - self.x) - self.d * self.v
        self.v += a * dt
        self.x += self.v * dt
        return self.x


def walk_targets(p):
    """Ziele fuer Watschel-Phase p (0..1). Erste Haelfte schwingt links,
    zweite rechts; Aufsetzen bei p = 0.5 und 1.0."""
    q = (2 * p) % 1.0
    left = p < 0.5
    s = math.sin(math.pi * q)
    lift = 1.0 * s
    foot = 2.0 * s                           # schwingender Fuss
    tilt = (-3.0 if left else 3.0) * s        # Grad, >0 = gegen den Uhrzeiger
    return lift, foot, left, tilt


def simulate():
    """Federn ueber mehrere Durchlaeufe, letzter Durchlauf = nahtlose Schleife.
    Hoehe und Schraeglage werden auf ganze Pixel gerundet, damit die
    Oberkante nicht flimmert."""
    sub = 40
    dt = 1.0 / (FPS * sub)
    sq = Spring(700.0, 13.0, 1.0)      # Stauchung (Hoehenfaktor)
    tl = Spring(380.0, 15.0, 0.0)      # Kippen (Grad)
    sh = Spring(260.0, 9.0, 0.0)       # Deckel schwappt (px)
    out = None
    for rep in range(6):
        rec = []
        prev_tilt = 0.0
        for i in range(N * sub):
            k = i / sub
            p = (k % PER) / PER
            lift, foot, left, tilt = walk_targets(p)
            contact = ((2 * p) % 1.0) < 1.0 / (PER * sub)
            s = sq.step(1.0, dt, -0.9 if contact else 0.0)
            t = tl.step(tilt, dt)
            d_t = t - prev_tilt
            prev_tilt = t
            # Der Deckel haengt dem Kippen etwas hinterher
            h = sh.step(0.0, dt, d_t * 0.5)
            if i % sub == 0:
                rec.append(dict(loop=int(k // PER), f=int(k) % PER, p=p, lift=lift, foot=foot,
                                left=left, tilt=t, sy=round(BODY_H * s) / BODY_H,
                                shear=float(round(h)),
                                eye="auge", mouth="grins", legs="boden", dust=[]))
        out = rec
    # Blinzeln in Schleife 2
    for j in (7, 8):
        out[PER + j]["eye"] = "zu"
    out[PER + 9]["eye"] = "halb"
    # Puderzucker: je Aufsetzen ein Woelkchen unter dem Fuss
    for loop in range(2):
        for (f0, side) in ((0, 1), (6, -1)):
            for age in range(4):
                fr = (loop * PER + f0 + age) % N
                out[fr]["dust"].append((side * LEG_X, age, 1.0))
    return out


# ================================================================ Geometrie

def to_local(st, X, Y):
    """Welt (X ab Mitte, Y ab Boden nach oben) -> lokale Koerperkoordinaten."""
    sy = st["sy"]
    sx = 1.0 / math.sqrt(sy)
    oy = LEG + round(st["lift"])
    # Kippen als senkrechte Scherung: Seitenkanten bleiben pixelgerade
    k = math.tan(math.radians(st["tilt"]))
    qx = X
    qy = (Y - oy) - X * k
    hy = BODY_H * sy
    qx = qx - st["shear"] * np.clip(qy / hy, 0, 1.2) ** 1.4
    return qx / sx, qy / sy


def to_world(st, x, y):
    sy = st["sy"]
    sx = 1.0 / math.sqrt(sy)
    oy = LEG + round(st["lift"])
    x, y = x * sx, y * sy
    x = x + st["shear"] * min(max(y / (BODY_H * sy), 0), 1.2) ** 1.4
    k = math.tan(math.radians(st["tilt"]))
    return x, y + x * k + oy


def half_at(y):
    """Leicht bauchige Seiten."""
    return HALF * (1 + 0.035 * np.sin(np.pi * np.clip(y / BODY_H, 0, 1)))


def inside_body(x, y):
    a = half_at(y)
    t = np.clip(x / a, -1, 1)
    r = np.sqrt(1 - t * t)
    top = BODY_H - CAP_E + CAP_E * r
    bot = BOT_E * (1 - r)
    return (np.abs(x) <= a) & (y >= bot) & (y <= top)


# ================================================================ Rendern

EMPTY, SHADOW, LEGP, BODY = range(4)


def leg_feet(st):
    """Fusspositionen (Spalte links, Zeile Fussunterkante) und Beinoberkante."""
    feet = []
    for side in (-1, 1):
        bx, by = to_world(st, side * LEG_X, BOT_E * 0.5)
        lifted = 0
        if st["legs"] == "boden" and st["loop"] < 2:
            if (side < 0) == st["left"]:
                lifted = int(round(st["foot"]))
        if st["legs"] == "haengen":
            foot_y = int(round(by)) - LEG
        else:
            foot_y = lifted
        c0 = int(round(CX + bx - LEG_W / 2))
        top = int(round(by)) + 1
        feet.append((c0, foot_y, max(top, foot_y + 2)))
    return feet


def render(st):
    pix = np.zeros((CELL_H, CELL_W), np.int16)
    # Schatten: kleiner und schmaler, je hoeher er springt
    lift = st["lift"]
    w = (HALF / math.sqrt(st["sy"]) + 1.5) * (1 - lift / 22.0)
    hh = 1.6 * (1 - lift / 30.0)
    ys, xs = np.mgrid[0:CELL_H, 0:CELL_W]
    X = xs + 0.5 - CX
    Y = GROUND - (ys + 0.5)
    pix[((X / w) ** 2 + ((Y + 0.2) / hh) ** 2) <= 1.0] = SHADOW

    # Beine: Rechtecke vom Fuss bis unter den Koerper
    for (c0, fy, top) in leg_feet(st):
        for yy in range(fy, top + 1):
            r = GROUND - 1 - yy
            for c in range(c0, c0 + LEG_W):
                if 0 <= r < CELL_H and 0 <= c < CELL_W:
                    pix[r, c] = LEGP

    # Koerper unterabgetastet
    H, W = CELL_H * SS, CELL_W * SS
    ys, xs = np.mgrid[0:H, 0:W]
    X = (xs + 0.5) / SS - CX
    Y = GROUND - (ys + 0.5) / SS
    lx, ly = to_local(st, X, Y)
    m = inside_body(lx, ly)
    cnt = m.reshape(CELL_H, SS, CELL_W, SS).sum(axis=(1, 3))
    pix[cnt > SS * SS // 2] = BODY
    return pix


LIGHT = np.array([-0.68, 0.45, 0.58])
LIGHT = LIGHT / np.linalg.norm(LIGHT)


def hash01(a, b):
    v = (a * 73856093) ^ (b * 19349663)
    v = (v ^ (v >> 13)) * 1274126177
    return ((v ^ (v >> 16)) & 0xFFFF) / 65535.0


def shade(pix, st):
    img = np.zeros((CELL_H, CELL_W, 4), np.uint8)
    ys, xs = np.mgrid[0:CELL_H, 0:CELL_W]
    X = xs + 0.5 - CX
    Y = GROUND - (ys + 0.5)
    lx, ly = to_local(st, X, Y)
    cap = np.zeros((CELL_H, CELL_W), bool)

    def at(r, c):
        return pix[r, c] if 0 <= r < CELL_H and 0 <= c < CELL_W else EMPTY

    for r in range(CELL_H):
        for c in range(CELL_W):
            m = pix[r, c]
            if m == EMPTY:
                continue
            if m == SHADOW:
                img[r, c] = C["shadow"]
                continue
            if m == LEGP:
                # Stummelbein: links Licht, rechts Schatten, Sohle dunkel
                col = C["mid"]
                if at(r, c - 2) != LEGP:
                    col = C["base"]
                elif at(r, c + 2) != LEGP:
                    col = C["shade"]
                if at(r + 2, c) != LEGP:
                    col = C["shade"]
                if at(r - 1, c) == BODY or at(r - 2, c) == BODY:
                    col = C["deep"]                    # Schlagschatten unter dem Koerper
                img[r, c] = col
                continue
            x, y = lx[r, c], ly[r, c]
            a = float(half_at(y))
            t = max(-1.0, min(1.0, x / a))
            rr = math.sqrt(1 - t * t)
            rim = BODY_H - CAP_E - CAP_E * rr
            if y > rim + 0.5:
                # Deckel: flach, hell, nach rechts hinten etwas dunkler
                v = 0.95 - 0.22 * max(t, 0) - 0.05 * (y - rim) / (2 * CAP_E)
                col = C["hi"] if v > 0.86 else C["light"] if v > 0.76 else C["base"]
                cap[r, c] = True
            else:
                # Mantel: Zylinder-Normale, unten Kante nach unten gerundet
                nx, ny, nz = t, 0.0, rr
                if y < 2.5:
                    ny = -(2.5 - y) / 2.5 * 0.9

                ln = math.sqrt(nx * nx + ny * ny + nz * nz) + 1e-6
                lam = (nx * LIGHT[0] + ny * LIGHT[1] + nz * LIGHT[2]) / ln
                if y < 1.0:
                    col = C["deep"]
                elif lam > 0.88:
                    col = C["hi"]
                elif lam > 0.72:
                    col = C["light"]
                elif lam > 0.45:
                    col = C["base"]
                elif lam > 0.18:
                    col = C["mid"]
                else:
                    col = C["shade"]
                # Puder-Struktur, fest im Koerper verankert
                hv = hash01(int(math.floor(x + 40)), int(math.floor(y + 40)))
                if hv < 0.07 and col in (C["base"], C["light"]):
                    col = C["mid"] if col == C["base"] else C["base"]
                elif hv > 0.95 and col in (C["mid"], C["base"]):
                    col = C["light"]
            img[r, c] = col

    # Kante zwischen Deckel und Mantel: Mantel-Pixel direkt unter dem Deckel
    # bekommen eine weiche Falte, links heller, rechts dunkler.
    for r in range(1, CELL_H):
        for c in range(CELL_W):
            if pix[r, c] == BODY and not cap[r, c] and cap[r - 1, c]:
                t = lx[r, c] / HALF
                img[r, c] = C["base"] if t < -0.45 else C["mid"] if t < 0.45 else C["shade"]

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
                out[r, c] = C["line"]          # Koerper-Unterkante ueber dem Bein
    return out


# ================================================================ Gesicht

# '#' dunkel, 'w' Glanz/weiss, 'm' Mund, 'f' Zahn, 'r' Rachen, 't' Zunge
EYES = {
    "auge":  (".###.",
              "##ww#",
              "##ww#",
              "#####",
              ".###."),
    "halb":  (".....",
              ".....",
              "#####",
              "###w#",
              ".###."),
    "zu":    (".....",
              ".....",
              ".....",
              "#####",
              "....."),
    "kneif": ("##...",
              "..##.",
              "....#",
              "..##.",
              "##..."),
    "gross": (".###.",
              "#www#",
              "#ww##",
              "#w###",
              ".###."),
    "froh":  (".....",
              ".###.",
              "#...#",
              ".....",
              "....."),
}
MOUTHS = {
    "grins": (".#######.",
              "#f#f#f#f#",
              ".#######."),
    "beiss": (".#######.",
              "#fffffff#",
              ".#######."),
    "auf":   ("..#####..",
              ".#f#f#f#.",
              "#rrrrrrr#",
              "#rrrttrr#",
              ".#rtttr#.",
              "..#####.."),
    "froh":  ("#.......#",
              ".#f#f#f#.",
              "..#####.."),
}


def stamp(img, pix, rows, r0, c0, mirror=False):
    for dr, row in enumerate(rows):
        row = row[::-1] if mirror else row
        for dc, ch in enumerate(row):
            if ch == ".":
                continue
            r, c = r0 + dr, c0 + dc
            if not (0 <= r < CELL_H and 0 <= c < CELL_W) or pix[r, c] != BODY:
                continue
            img[r, c] = {"#": C["eye"], "w": C["eglint"], "m": C["mouth"], "f": C["tooth"],
                         "r": C["maw"], "t": C["tongue"]}[ch]


def face(img, pix, st):
    # Gesicht etwas nach rechts: der Marshmello schaut in Laufrichtung
    fx, fy = to_world(st, 1.5, BODY_H * 0.47)
    c_mid = int(round(CX + fx))
    r_eye = int(round(GROUND - fy)) - 4
    rows = EYES[st["eye"]]
    mir = st["eye"] == "kneif"
    stamp(img, pix, rows, r_eye, c_mid - 6)
    stamp(img, pix, rows, r_eye, c_mid + 2, mirror=mir)
    # Baeckchen
    for c in (c_mid - 9, c_mid - 8, c_mid + 7, c_mid + 8):
        r = r_eye + 5
        if 0 <= r < CELL_H and 0 <= c < CELL_W and pix[r, c] == BODY \
                and tuple(img[r, c]) != C["line"]:
            img[r, c] = C["blush"]
    m = MOUTHS[st["mouth"]]
    stamp(img, pix, m, r_eye + 6, c_mid - len(m[0]) // 2)


def glint(img, pix, st):
    """Glanzpunkt oben links auf dem Mantel."""
    gx, gy = to_world(st, -HALF * 0.62, BODY_H - CAP_E * 2.6)
    c, r = int(round(CX + gx)), int(round(GROUND - gy))
    for dr, dc in ((0, 0), (1, 0)):
        rr, cc = r + dr, c + dc
        if 0 <= rr < CELL_H and 0 <= cc < CELL_W and pix[rr, cc] == BODY \
                and tuple(img[rr, cc]) not in (C["line"],):
            img[rr, cc] = C["glint"]


# Puderwoelkchen: Partikel (dx, dy-Steigung, Lebensdauer in Bildern)
DUST = [(-1.6, 0.9, 3), (-2.8, 0.4, 4), (1.7, 1.0, 3), (2.9, 0.5, 4), (0.4, 1.6, 2)]


def dust(img, st):
    for (x0, age, big) in st["dust"]:
        if big > 1.5:
            parts = [(dx * 2.6, dy * 1.3, life + 1) for dx, dy, life in DUST] + \
                    [(-4.5, 0.6, 5), (4.5, 0.7, 5), (-6.0, 0.2, 4), (6.2, 0.3, 4)]
        else:
            parts = DUST
        for i, (dx, dy, life) in enumerate(parts):
            if age >= life:
                continue
            u = (age + 1) / life
            x = x0 + dx * (age + 1) * 0.9
            y = dy * (age + 1) * (1.4 - 0.35 * age)
            c = int(round(CX + x))
            r = GROUND - 1 - int(round(max(y, 0)))
            col = C["dust"] if u < 0.6 else C["dust2"]
            pts = [(0, 0)]
            if age == 0 or (big > 1.5 and age == 1):
                pts += [(0, 1 if dx > 0 else -1), (-1, 0)]
            elif age == 1 or (big > 1.5 and age == 2 and i % 2 == 0):
                pts.append((0, 1 if dx > 0 else -1))
            for (pr, pc) in pts:
                rr, cc = r + pr, c + pc
                if 0 <= rr < CELL_H and 0 <= cc < CELL_W and img[rr, cc, 3] < 200:
                    img[rr, cc] = col


# ================================================================ Ablauf

def draw(st):
    pix = render(st)
    img = shade(pix, st)
    glint(img, pix, st)
    face(img, pix, st)
    dust(img, st)
    return img


def main():
    args = sys.argv[1:]
    states = simulate()
    frames = [draw(st) for st in states]
    strip = np.concatenate(frames, axis=1)
    os.makedirs(OUT_DIR, exist_ok=True)
    png = os.path.join(OUT_DIR, NAME + ".png")
    Image.fromarray(strip).save(png)
    meta = png + ".meta"
    if not os.path.exists(meta):
        sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
        from unity_meta import write_strip_meta
        write_strip_meta(meta, NAME, len(frames), CELL_W, CELL_H, PPU, pivot=(0.5, 0.0))
    print("%s  %d Bilder  %dx%d  %d fps" % (NAME, len(frames), CELL_W, CELL_H, FPS))

    # Bestiarium: Bild 0 ohne Schatten
    a = frames[0]
    solid = a[..., 3] == 255
    rows = np.where(solid.any(1))[0]
    cols = np.where(solid.any(0))[0]
    Image.fromarray(a).crop((cols[0], rows[0], cols[-1] + 1, rows[-1] + 1)).save(BESTIARY)

    if "--preview" in args:
        path = args[args.index("--preview") + 1]
        k = 8
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
        sheet.resize((sheet.width * 3, sheet.height * 3), Image.NEAREST).save(path.replace(".gif", "_sheet.png"))


if __name__ == "__main__":
    main()
