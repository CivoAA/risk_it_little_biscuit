"""
Zeichnet den Boden der Vulkanwelt (Level 5, Szene Map_World4): dunkler,
poriger Basalt mit Aschewehen darueber.

  Assets/Art/Tiles_Vulkan/vulkan_boden.png     N x 32x32, PPU 32 (1 Tile = 1 Einheit)
  Assets/Art/Tiles_Vulkan/vulkan_layout.json   fertig belegte 9 Chunks + Lavaloecher

Zwei Materialien mit weichen Uebergaengen (Marching Squares an den Ecken):
jede Ecke einer Zelle ist Basalt (0) oder Asche (1), das Tile zur Zelle haengt
an seinen vier Ecken. Die Grenze innerhalb des Tiles kommt aus einem Feld, das
an den Kanten nur von den beiden Kanten-Ecken abhaengt (bilinear + Rauschen,
das sich alle 32 px wiederholt) - darum passt jede Kante an jeden Nachbarn.

Reihenfolge im Streifen (Sprite vulkan_boden_<i>):
   0..13   Basalt voll    (BASALT)
  14..19   Asche voll     (ASH)
  20..47   Uebergaenge: Ecken-Code 1..14 je zwei Varianten -> 20 + (code-1)*2 + v
           Ecken-Code = tl*8 + tr*4 + bl*2 + br  (1 = Asche)

Die Chunks (52 x 40, WorldManager3x3) werden beim Weiterlaufen im Kreis
geschoben, jeder muss an jeden passen: die Ecken auf den Chunk-Raendern kommen
aus einem Feld, das sich alle 52 x 40 wiederholt, nur das Innere ist je Chunk
eigen. Die Lavaloecher verteilt diese Datei gleich mit (Bilder und Masse aus
Tools/vulkan_lava.py -> vulkan_lava.json, also das zuerst laufen lassen).

Aufruf aus dem Projektordner:
  python Tools/vulkan_boden.py                    Tiles + Layout schreiben
  python Tools/vulkan_boden.py --preview a.png    Ausschnitt mit Figur ansehen
  python Tools/vulkan_boden.py --world a.png      alle 9 Chunks mit Lavaloechern
Die .meta wird nur beim ersten Mal geschrieben (sonst verlieren Tiles ihre Verweise).
"""

import json
import math
import os
import random
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from unity_meta import write_strip_meta  # noqa: E402
from vulkan_noise import fbm, grid, value_noise  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "Assets", "Art", "Tiles_Vulkan")
SHEET = os.path.join(OUT_DIR, "vulkan_boden.png")
LAYOUT = os.path.join(OUT_DIR, "vulkan_layout.json")
LAVA_DIR = os.path.join(ROOT, "Assets", "Art", "World-Objects", "Vulkan")
LAVA_INFO = os.path.join(LAVA_DIR, "vulkan_lava.json")

T = 32
CHUNK_W, CHUNK_H = 52, 40


def hx(s):
    s = s.lstrip("#")
    return np.array([int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), 255], dtype=np.uint8)


# Kuehle, leicht violette Steine - dagegen leuchtet die Lava. Mittel-dunkel,
# damit dunkle Gegner (der Verkohlte) nicht im Boden verschwinden.
B = {
    "ink": hx("#221b22"),
    "pit": hx("#2a2229"),
    "shade": hx("#342b33"),
    "base": hx("#3c333b"),
    "mid": hx("#443a42"),
    "light": hx("#4e434b"),
    "hi": hx("#5a4e55"),
    "dust": hx("#5a5054"),
    "joint": hx("#372e36"),
}
A = {
    "shadow": hx("#4a4045"),
    "low": hx("#675d61"),
    "thin": hx("#6e6467"),
    "base": hx("#776d70"),
    "mid": hx("#7f7577"),
    "light": hx("#897e7f"),
    "hi": hx("#968b8a"),
}
EMBER = [hx("#3e1a17"), hx("#6e2416"), hx("#a8381a"), hx("#d8642a"), hx("#f4a04a")]
OBSIDIAN = [hx("#16111a"), hx("#231c2c"), hx("#3a3050"), hx("#8a90b8"), hx("#d6dcf4")]
BONE = [hx("#5a4e4a"), hx("#8a7e72"), hx("#b2a594"), hx("#d0c4b0")]
ROCK = [hx("#2a2229"), hx("#4a4048"), hx("#5c5058"), hx("#6e6168"), hx("#857679")]

BASALT = ["plain", "plain2", "plain3", "plain4", "pores", "pores2", "pebbles", "pebbles2",
          "crack", "ember", "obsidian", "rock", "rock2", "dust"]
BASALT_W = [26, 26, 26, 26, 3, 3, 1.6, 1.6, 2.5, 1.2, 1.0, 1.0, 1.0, 3]
ASH = ["plain", "plain2", "ripples", "ripples2", "bone", "rock"]
ASH_W = [30, 30, 7, 7, 0.8, 2.2]

MIX_FIRST = len(BASALT) + len(ASH)   # 20
MIX_VARIANTS = 2


def mix_index(code, v):
    return MIX_FIRST + (code - 1) * MIX_VARIANTS + v


# --- Feld fuer die Materialgrenze --------------------------------------------

def edge_field(corners, vseed, pad=1):
    """Feld m auf (T+2pad)^2; > 0.5 = Asche. corners = (tl, tr, bl, br)."""
    tl, tr, bl, br = corners
    n = T + 2 * pad
    X, Y = grid(n, n)
    X = X - pad
    Y = Y - pad
    s = X / T
    t = Y / T
    top = tl + (tr - tl) * s
    bot = bl + (br - bl) * s
    m = top + (bot - top) * t
    # Rauschen wiederholt sich alle 32 px -> an jeder Kante gleich.
    # Kraeftig wellen, aber nie so weit, dass eine reine Kante kippt (|n| < 0.47).
    n = 1.5 * (fbm(X / 8.0, Y / 8.0, 4242, octaves=3, period=(4, 4)) - 0.5)
    n = n + 0.08 * (value_noise(X / 2.0, Y / 2.0, 99, period=(16, 16)) - 0.5)
    m = m + np.clip(n, -0.47, 0.47)
    # Variante: nur im Inneren, an den Kanten 0.
    ew = np.clip(np.minimum(np.minimum(X, T - X), np.minimum(Y, T - Y)) / 11.0, 0, 1)
    m = m + 0.34 * (fbm(X / 6.0, Y / 6.0, 7000 + vseed, octaves=2) - 0.5) * ew
    return m


# --- Grundflaechen ------------------------------------------------------------

def put(img, x, y, col):
    if 0 <= x < T and 0 <= y < T:
        img[y, x] = col


# Saeulenbasalt: ein Fugennetz aus Sechsecken, das ueber alle Tiles weiterlaeuft.
# Jede Fuge trifft die Kante an festen Punkten (oben/unten bei x=JOINT_X, links/
# rechts bei y=JOINT_Y), innen sitzen zwei Knoten mit je drei Fugen -> Waben.
JOINT_X = 9
JOINT_Y = 19
JOINT_VARIANTS = 8


def bresenham(x0, y0, x1, y1):
    pts = []
    dx, dy = abs(x1 - x0), -abs(y1 - y0)
    sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
    err = dx + dy
    while True:
        pts.append((x0, y0))
        if x0 == x1 and y0 == y1:
            return pts
        e2 = 2 * err
        if e2 >= dy:
            err += dy
            x0 += sx
        if e2 <= dx:
            err += dx
            y0 += sy


def wobbly(rng, a, b):
    """Fuge von a nach b mit einem verschobenen Zwischenpunkt."""
    mx = (a[0] + b[0]) / 2 + rng.uniform(-2.5, 2.5)
    my = (a[1] + b[1]) / 2 + rng.uniform(-2.5, 2.5)
    m = (int(round(mx)), int(round(my)))
    return bresenham(a[0], a[1], m[0], m[1])[:-1] + bresenham(m[0], m[1], b[0], b[1])


def joints(seed):
    """Fugen-Pixel eines Tiles: Liste von Segmenten (Pixellisten)."""
    rng = random.Random(seed * 7 + 1)
    if seed % 2 == 0:
        # Knoten oben links + unten rechts
        j1 = (JOINT_X + 3 + rng.randint(-3, 5), 9 + rng.randint(-4, 3))
        j2 = (JOINT_X + 14 + rng.randint(-5, 4), JOINT_Y + 4 + rng.randint(-3, 4))
        top, left, right, bottom = j1, j1, j2, j2
    else:
        # gespiegelt: oben rechts + unten links
        j1 = (JOINT_X + 12 + rng.randint(-5, 4), 9 + rng.randint(-4, 4))
        j2 = (JOINT_X - 3 + rng.randint(-3, 4), JOINT_Y + 5 + rng.randint(-3, 3))
        top, right, left, bottom = j1, j1, j2, j2
    segs = [
        [(JOINT_X, 0), (JOINT_X, 1)] + wobbly(rng, (JOINT_X, 2), top),
        [(0, JOINT_Y)] + wobbly(rng, (1, JOINT_Y), left),
        wobbly(rng, j1, j2),
        wobbly(rng, right, (T - 2, JOINT_Y)) + [(T - 1, JOINT_Y)],
        wobbly(rng, bottom, (JOINT_X, T - 3)) + [(JOINT_X, T - 2), (JOINT_X, T - 1)],
    ]
    return segs


def draw_joints(img, seed, glow_seg=None):
    segs = joints(seed)
    rng_light = random.Random(seed * 5 + 2)
    crack = set(p for seg in segs for p in seg)
    for (x, y) in crack:
        if (x, y + 1) not in crack and y + 1 < T and rng_light.random() < 0.5:
            img[y + 1, x] = B["mid"]            # Lichtkante der unteren Saeule, nur stellenweise
    for (x, y) in crack:
        img[y, x] = B["joint"]
    if glow_seg is not None:
        seg = segs[glow_seg]
        for i, (x, y) in enumerate(seg):
            mid = 0.2 < i / max(1, len(seg)) < 0.8
            img[y, x] = EMBER[3] if mid and i % 3 == 1 else EMBER[2] if mid else EMBER[1]
    return crack


def basalt_face(seed, glow_seg=None):
    rng = random.Random(seed)
    img = np.empty((T, T, 4), dtype=np.uint8)
    img[:] = B["base"]
    # Keine grossen Flecken: die enden an der Tile-Kante und zeichnen ein
    # Gitter. Nur kleine Dinge, gleichmaessig verteilt - auch an der Kante.
    for _ in range(rng.randint(3, 5)):
        x, y = rng.randint(0, T - 3), rng.randint(0, T - 2)
        for k in range(rng.randint(2, 4)):
            img[y, (x + k) % T] = B["mid"]
        img[(y + 1) % T, (x + 1) % T] = B["shade"]
    for _ in range(rng.randint(2, 4)):
        x, y = rng.randint(0, T - 2), rng.randint(0, T - 2)
        img[y, x] = B["shade"]
        img[y, x + 1] = B["shade"]
    # Poren: dunkles Loch, unten rechts heller Rand.
    for _ in range(rng.randint(3, 6)):
        x, y = rng.randint(2, T - 4), rng.randint(2, T - 4)
        img[y, x] = B["pit"]
        if rng.random() < 0.35:
            img[y, x + 1] = B["pit"]
            img[y + 1, x + 2] = B["light"]
        img[y + 1, x + 1] = B["light"]
    # einzelne Glanzkoerner
    for _ in range(rng.randint(2, 5)):
        img[rng.randint(1, T - 2), rng.randint(1, T - 2)] = B["light"]
    draw_joints(img, seed % JOINT_VARIANTS, glow_seg)
    return img


def ash_face(seed):
    rng = random.Random(seed)
    img = np.empty((T, T, 4), dtype=np.uint8)
    img[:] = A["base"]
    for _ in range(rng.randint(3, 5)):
        x, y = rng.randint(0, T - 5), rng.randint(0, T - 1)
        for k in range(rng.randint(3, 5)):
            img[y, x + k] = A["mid"]
    # feines Korn
    for _ in range(rng.randint(8, 13)):
        x, y = rng.randint(0, T - 1), rng.randint(0, T - 1)
        img[y, x] = A["light"] if rng.random() < 0.55 else A["low"]
    for _ in range(rng.randint(2, 4)):
        x, y = rng.randint(1, T - 2), rng.randint(1, T - 2)
        img[y, x] = B["mid"]      # dunkle Schlacke-Koernchen
    return img


# --- Deko ----------------------------------------------------------------------

def stone(img, cx, cy, w, h, pal, rng, outline=True):
    """Kleiner Stein, Licht oben links, Schatten unten rechts."""
    pts = []
    for y in range(cy - h, cy + h + 1):
        for x in range(cx - w, cx + w + 1):
            d = ((x - cx) / (w + .5)) ** 2 + ((y - cy) / (h + .5)) ** 2
            if d <= 1.0:
                pts.append((x, y, d))
    s = set((x, y) for x, y, _ in pts)
    # Schatten nach unten rechts
    for x, y, _ in pts:
        for dx, dy in ((1, 1), (0, 1)):
            if (x + dx, y + dy) not in s:
                put(img, x + dx, y + dy, B["ink"] if outline else B["shade"])
    for x, y, d in pts:
        lx, ly = (x - cx) / (w + .5), (y - cy) / (h + .5)
        lit = -(lx * 0.7 + ly * 0.8)
        if (x - 1, y) not in s or (x, y - 1) not in s:
            col = pal[3] if lit > -0.2 else pal[1]
        elif (x + 1, y) not in s or (x, y + 1) not in s:
            col = pal[1]
        else:
            col = pal[3] if lit > 0.35 else pal[2]
        put(img, x, y, col)
    if w >= 2:
        put(img, cx - w // 2, cy - h // 2 if h > 1 else cy - 1 if h > 0 else cy, pal[4])


def crack(img, rng, glow=False):
    x, y = rng.randint(4, 8), rng.randint(9, 22)
    end = rng.randint(22, 27)
    path = []
    while x < end:
        path.append((x, y))
        x += 1
        r = rng.random()
        if r < 0.3:
            y -= 1
        elif r < 0.6:
            y += 1
        y = max(4, min(T - 5, y))
        if rng.random() < 0.12 and len(path) > 3:
            # kleiner Seitenast
            bx, by = x, y
            for _ in range(rng.randint(2, 4)):
                by += rng.choice([-1, 1])
                bx += rng.choice([0, 1])
                path.append((bx, by))
    for (px, py) in path:
        if glow:
            put(img, px, py - 1, EMBER[1])
            put(img, px, py + 1, EMBER[0])
        else:
            put(img, px, py - 1, B["light"])
    for i, (px, py) in enumerate(path):
        if glow:
            mid = 0.25 < i / max(1, len(path)) < 0.75
            put(img, px, py, EMBER[3] if mid and i % 3 else EMBER[2])
        else:
            put(img, px, py, B["ink"])


def basalt_tile(kind, seed):
    rng = random.Random(seed * 17 + 3)
    img = basalt_face(seed)
    kind = kind.rstrip("0123456789")
    if kind == "pores":
        for _ in range(8):
            x, y = rng.randint(3, T - 5), rng.randint(3, T - 5)
            img[y, x] = B["ink"]
            img[y, x + 1] = B["pit"]
            img[y + 1, x] = B["pit"]
            img[y + 1, x + 1] = B["hi"]
    elif kind == "pebbles":
        spots = [(9, 10), (21, 19), (12, 23)]
        rng.shuffle(spots)
        for i, (x, y) in enumerate(spots[:rng.randint(2, 3)]):
            stone(img, x + rng.randint(-2, 2), y + rng.randint(-2, 2), 2 if i == 0 else 1, 1, ROCK, rng)
    elif kind == "crack":
        crack(img, rng)
    elif kind == "ember":
        img = basalt_face(seed, glow_seg=2)
    elif kind == "obsidian":
        cx, cy = rng.randint(11, 20), rng.randint(11, 20)
        shard = [(0, -3), (1, -2), (0, -2), (-1, -1), (0, -1), (1, -1), (2, -1),
                 (-1, 0), (0, 0), (1, 0), (2, 0), (-2, 1), (-1, 1), (0, 1), (1, 1), (2, 1), (3, 1)]
        for dx, dy in shard:
            put(img, cx + dx, cy + dy, OBSIDIAN[1])
        for dx in range(-2, 4):
            put(img, cx + dx, cy + 2, B["ink"])
        for dx, dy in ((0, -2), (-1, 0), (0, -1), (-1, 1)):
            put(img, cx + dx, cy + dy, OBSIDIAN[2])
        put(img, cx, cy - 3, OBSIDIAN[3])
        put(img, cx - 1, cy - 1, OBSIDIAN[3])
        put(img, cx, cy - 2, OBSIDIAN[4])
        put(img, cx + 2, cy, OBSIDIAN[0])
        put(img, cx + 3, cy + 1, OBSIDIAN[0])
        # zweiter Splitter
        ox, oy = cx + rng.choice([-7, 6]), cy + rng.choice([-5, 5])
        for dx, dy in ((0, 0), (1, 0), (0, -1)):
            put(img, ox + dx, oy + dy, OBSIDIAN[1])
        put(img, ox, oy - 1, OBSIDIAN[3])
        put(img, ox, oy + 1, B["ink"])
        put(img, ox + 1, oy + 1, B["ink"])
    elif kind == "rock":
        stone(img, rng.randint(12, 19), rng.randint(13, 18), 4, 3, ROCK, rng)
        stone(img, rng.randint(6, 9), rng.randint(22, 25), 1, 1, ROCK, rng)
    elif kind == "dust":
        for _ in range(rng.randint(9, 14)):
            x, y = rng.randint(3, T - 4), rng.randint(3, T - 4)
            img[y, x] = B["dust"]
            if rng.random() < 0.4:
                img[y, x + 1] = B["hi"]
    return img


def ripples(img, rng, count):
    ys = sorted(rng.sample(range(7, 26, 3), count))
    for y0 in ys:
        x0 = rng.randint(3, 9)
        x1 = rng.randint(21, 28)
        ph = rng.random() * 6.28
        for x in range(x0, x1):
            y = y0 + int(round(1.2 * math.sin(x * 0.42 + ph)))
            fade = min(x - x0, x1 - 1 - x)
            if fade == 0 and rng.random() < 0.5:
                continue
            put(img, x, y - 1, A["hi"])
            put(img, x, y, A["low"])


def ash_tile(kind, seed):
    rng = random.Random(seed * 13 + 5)
    img = ash_face(seed)
    if kind == "ripples":
        ripples(img, rng, 2)
    elif kind == "ripples2":
        ripples(img, rng, 3)
    elif kind == "bone":
        # angekohlter Knochen, halb in der Asche
        cx, cy = rng.randint(11, 19), rng.randint(13, 19)
        for dx in range(-4, 5):
            put(img, cx + dx, cy, BONE[2])
            put(img, cx + dx, cy + 1, BONE[1])
        for ex in (-5, 5):
            put(img, cx + ex, cy - 1, BONE[2])
            put(img, cx + ex, cy, BONE[3])
            put(img, cx + ex, cy + 1, BONE[1])
            put(img, cx + ex, cy + 2, BONE[0])
        for dx in range(-3, 4):
            put(img, cx + dx, cy - 1, BONE[3] if dx < 0 else BONE[2])
        for dx in range(-5, 6):
            put(img, cx + dx, cy + 2 if abs(dx) < 5 else cy + 3, A["low"])
        put(img, cx + 2, cy, BONE[0])   # Russfleck
        put(img, cx + 3, cy, BONE[0])
    elif kind == "rock":
        stone(img, rng.randint(12, 19), rng.randint(13, 18), 3, 2, ROCK, rng)
        # Asche haeuft sich unten am Stein
        for x in range(9, 23):
            if img[22, x].tolist() == A["base"].tolist() and rng.random() < 0.5:
                img[22, x] = A["light"]
    return img


# --- Uebergaenge ------------------------------------------------------------------

def transition_tile(code, v):
    corners = ((code >> 3) & 1, (code >> 2) & 1, (code >> 1) & 1, code & 1)
    m = edge_field(corners, code * 10 + v, pad=1)
    ash = m > 0.5
    # einzelne Pixel glaetten, die wirken sonst wie Dreck
    nb = (np.roll(ash, 1, 0).astype(int) + np.roll(ash, -1, 0) + np.roll(ash, 1, 1) + np.roll(ash, -1, 1))
    ash = np.where(ash & (nb <= 1), False, np.where(~ash & (nb >= 3), True, ash))
    rng_seed = 900 + code * 7 + v
    bas = basalt_face(rng_seed)
    ashf = ash_face(rng_seed + 50)
    img = np.where(ash[1:-1, 1:-1, None], ashf, bas).copy()
    inner = m[1:-1, 1:-1]
    a = ash
    for y in range(T):
        for x in range(T):
            Y, X = y + 1, x + 1
            if a[Y, X]:
                if not a[Y - 1, X]:
                    img[y, x] = A["hi"]        # Oberkante der Wehe im Licht
                elif not a[Y + 1, X] or not a[Y, X + 1]:
                    img[y, x] = A["low"]
                elif inner[y, x] < 0.56:
                    img[y, x] = A["thin"]      # duenne Asche am Rand
            else:
                if a[Y - 1, X]:
                    img[y, x] = B["shade"]     # Schatten unter der Wehe
                elif a[Y - 1, X - 1] and not a[Y, X - 1]:
                    img[y, x] = B["shade"]
                elif inner[y, x] > 0.42 and (x + y) % 2 == 0:
                    img[y, x] = B["dust"]      # Ascheschleier auf dem Stein
                elif inner[y, x] > 0.36 and (x + 2 * y) % 5 == 0:
                    img[y, x] = B["dust"]
    return img


def build():
    tiles = []
    for i, k in enumerate(BASALT):
        tiles.append(basalt_tile(k, 10 + i))
    for i, k in enumerate(ASH):
        tiles.append(ash_tile(k, 40 + i))
    for code in range(1, 15):
        for v in range(MIX_VARIANTS):
            tiles.append(transition_tile(code, v))
    sheet = np.zeros((T, T * len(tiles), 4), dtype=np.uint8)
    for i, t in enumerate(tiles):
        sheet[:, i * T:(i + 1) * T] = t
    return Image.fromarray(sheet, "RGBA"), len(tiles)


# --- Welt: Ecken-Feld, Tiles, Lavaloecher ----------------------------------------

ASH_THRESHOLD = 0.58


def vertex_field(chunk):
    """Ecken (53 x 41) eines Chunks: 1 = Asche. Raender aus dem gemeinsamen Feld."""
    ys, xs = np.mgrid[0:CHUNK_H + 1, 0:CHUNK_W + 1].astype(np.float64)
    # gemeinsames Feld, wiederholt sich alle 52 x 40 Ecken (8 x 5 Zellen, flach gezogen = Wehen)
    g = fbm(xs / 6.5, ys / 8.0, 31337, octaves=3, period=(8, 5), gain=0.55)
    h = fbm(xs / 6.5 + chunk * 97.0, ys / 8.0 + chunk * 41.0, 555 + chunk * 11, octaves=3, gain=0.55)
    dist = np.minimum(np.minimum(xs, CHUNK_W - xs), np.minimum(ys, CHUNK_H - ys))
    w = np.clip(1.0 - dist / 7.0, 0, 1)
    w = w * w * (3 - 2 * w)
    f = w * g + (1 - w) * h
    v = (f > ASH_THRESHOLD).astype(np.int64)
    # einzelne Ecken im Inneren saeubern (wirken wie Krater-Pickel)
    for y in range(1, CHUNK_H):
        for x in range(1, CHUNK_W):
            if dist[y, x] < 1:
                continue
            nb = v[y - 1, x] + v[y + 1, x] + v[y, x - 1] + v[y, x + 1]
            if v[y, x] == 1 and nb == 0:
                v[y, x] = 0
            elif v[y, x] == 0 and nb == 4:
                v[y, x] = 1
    return v


def pick(rng, weights):
    r = rng.random() * sum(weights)
    for i, w in enumerate(weights):
        r -= w
        if r < 0:
            return i
    return 0


def chunk_tiles(chunk):
    """Tile-Index je Zelle, Zeilen von unten (y=-20) nach oben (y=19)."""
    v = vertex_field(chunk)
    rng = random.Random(8000 + chunk * 131)
    rows = []
    for j in range(CHUNK_H):
        row = []
        for i in range(CHUNK_W):
            bl, br = v[j, i], v[j, i + 1]
            tl, tr = v[j + 1, i], v[j + 1, i + 1]
            code = tl * 8 + tr * 4 + bl * 2 + br
            if code == 0:
                idx = pick(rng, BASALT_W)
            elif code == 15:
                idx = len(BASALT) + pick(rng, ASH_W)
            else:
                idx = mix_index(code, rng.randrange(MIX_VARIANTS))
            row.append(int(idx))
        rows.append(row)
    return rows


def load_lava():
    if not os.path.exists(LAVA_INFO):
        return []
    with open(LAVA_INFO, encoding="utf-8") as fh:
        return json.load(fh)["pools"]


# Wie im WorldManager3x3: Index 0 = oben links, 4 = Mitte (Spielerstart).
POOLS_PER_CHUNK = 22
START_CLEAR = 6.0     # Einheiten um (0,0) im Mittel-Chunk bleiben frei
EDGE_MARGIN = 1.5
POOL_GAP = 1.6       # wie minDistance am ChunkPropRandomizer


def place_pools(chunk, pools):
    if not pools:
        return []
    rng = random.Random(4400 + chunk * 53)
    placed = []
    # Gewichte: kleine haeufiger, Spalten selten
    weights = [p["weight"] for p in pools]
    tries = 0
    while len(placed) < POOLS_PER_CHUNK and tries < 800:
        tries += 1
        p = pools[pick(rng, weights)]
        rx, ry = p["halfW"], p["halfH"]
        # Bildrechteck (mit Kruste und Brandspur) relativ zur Mitte
        l, r_ = -p["pivotX"] / 32.0, (p["frameW"] - p["pivotX"]) / 32.0
        b, t = -p["pivotY"] / 32.0, (p["frameH"] - p["pivotY"]) / 32.0
        x = rng.uniform(-CHUNK_W / 2 + EDGE_MARGIN + rx, CHUNK_W / 2 - EDGE_MARGIN - rx)
        y = rng.uniform(-CHUNK_H / 2 + EDGE_MARGIN + ry, CHUNK_H / 2 - EDGE_MARGIN - ry)
        x = round(x * 32) / 32
        y = round(y * 32) / 32
        if chunk == 4 and math.hypot(x, y) < START_CLEAR + rx:
            continue
        ok = True
        rect = (x + l, y + b, x + r_, y + t)
        for q in placed:
            # Abstand Bildrand zu Bildrand - wie ChunkPropRandomizer.spacingByBounds
            qr = q["rect"]
            gap_x = max(qr[0] - rect[2], rect[0] - qr[2])
            gap_y = max(qr[1] - rect[3], rect[1] - qr[3])
            if max(gap_x, gap_y) < POOL_GAP:
                ok = False
                break
        if ok:
            placed.append({"name": p["name"], "x": x, "y": y, "rect": rect})
    return [{"name": q["name"], "x": q["x"], "y": q["y"]} for q in placed]


def layout():
    pools = load_lava()
    chunks = []
    for c in range(9):
        chunks.append({"tiles": chunk_tiles(c), "pools": place_pools(c, pools)})
    return {"width": CHUNK_W, "height": CHUNK_H, "minX": -CHUNK_W // 2, "minY": -CHUNK_H // 2,
            "chunks": chunks}


# --- Vorschau -------------------------------------------------------------------------

def render_chunk(sheet, data):
    img = Image.new("RGBA", (CHUNK_W * T, CHUNK_H * T))
    for j, row in enumerate(data["tiles"]):
        for i, idx in enumerate(row):
            tile = sheet.crop((idx * T, 0, idx * T + T, T))
            img.paste(tile, (i * T, (CHUNK_H - 1 - j) * T))
    return img


def paste_pools(img, pools, origin_x, origin_y, frame=0):
    """origin = Pixelposition von Welt (0,0) des Chunks im Bild."""
    for p in pools:
        path = os.path.join(LAVA_DIR, "vulkan_lava_%s.png" % p["name"])
        info = next((q for q in load_lava() if q["name"] == p["name"]), None)
        if info is None or not os.path.exists(path):
            continue
        strip = Image.open(path).convert("RGBA")
        fw, fh = info["frameW"], info["frameH"]
        fr = strip.crop((frame * fw, 0, frame * fw + fw, fh))
        px = origin_x + p["x"] * T - info["pivotX"]
        py = origin_y - p["y"] * T - (fh - info["pivotY"])
        img.alpha_composite(fr, (int(round(px)), int(round(py))))


def figure():
    keks = os.path.join(ROOT, "Assets", "Art", "Chars", "Char_Keks.png")
    if not os.path.exists(keks):
        return None
    return Image.open(keks).convert("RGBA").crop((0, 0, 40, 40))


def main():
    sheet, count = build()
    os.makedirs(OUT_DIR, exist_ok=True)
    if "--preview" in sys.argv or "--world" in sys.argv:
        data = layout()
        if "--world" in sys.argv:
            out = sys.argv[sys.argv.index("--world") + 1]
            big = Image.new("RGBA", (3 * CHUNK_W * T, 3 * CHUNK_H * T))
            for c in range(9):
                im = render_chunk(sheet, data["chunks"][c])
                paste_pools(im, data["chunks"][c]["pools"], CHUNK_W * T // 2, CHUNK_H * T // 2)
                big.paste(im, ((c % 3) * CHUNK_W * T, (c // 3) * CHUNK_H * T))
            scale = float(sys.argv[sys.argv.index("--world") + 2]) if len(sys.argv) > sys.argv.index("--world") + 2 else 0.25
            big.resize((int(big.width * scale), int(big.height * scale)), Image.LANCZOS).save(out)
        else:
            out = sys.argv[sys.argv.index("--preview") + 1]
            c = int(sys.argv[sys.argv.index("--preview") + 2]) if len(sys.argv) > sys.argv.index("--preview") + 2 else 4
            im = render_chunk(sheet, data["chunks"][c])
            paste_pools(im, data["chunks"][c]["pools"], CHUNK_W * T // 2, CHUNK_H * T // 2)
            # Kameraausschnitt 480x270 um die Mitte, dazu die Figur
            cx, cy = CHUNK_W * T // 2, CHUNK_H * T // 2
            view = im.crop((cx - 240, cy - 135, cx + 240, cy + 135))
            fig = figure()
            if fig is not None:
                view.alpha_composite(fig, (220, 115))
            view.resize((960, 540), Image.NEAREST).save(out)
        return
    sheet.save(SHEET)
    meta = SHEET + ".meta"
    if not os.path.exists(meta):
        write_strip_meta(meta, "vulkan_boden", count, T, T, 32)
    with open(LAYOUT, "w", encoding="utf-8", newline="\n") as fh:
        json.dump(layout(), fh, separators=(",", ":"))
    print("geschrieben:", SHEET, count, "Tiles +", LAYOUT)


if __name__ == "__main__":
    main()
