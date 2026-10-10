"""
Zeichnet den Boden des Lebkuchen-Geisterwalds (Level 4, Szene Map_World6):
Waldboden aus Kakao-Erde mit Minzmoos und Gummilaub, durch den sich
Pflasterwege aus Lebkuchen mit Zuckerguss-Fugen schlaengeln.

  Assets/Art/Tiles_Geist/geist_boden.png     N x 32x32, PPU 32 (1 Tile = 1 Einheit)
  Assets/Art/Tiles_Geist/geist_layout.json   fertig belegte 9 Chunks + Props

Aufbau wie Tools/eis_boden.py (Marching Squares an den Ecken): jede Ecke ist
Weg (0) oder Erde (1), die Grenze im Tile kommt aus einem Feld, das an den
Kanten nur von den Kanten-Ecken abhaengt - jede Kante passt an jeden Nachbarn.

Das Pflaster ist ein Voronoi-Muster mit 64 px Periode, also ueber 2x2 Tiles.
Jedes Weg-Tile gibt es darum in vier Vierteln (q = (x % 2) + 2 * (y % 2)),
die Welt waehlt das Viertel nach der Zellposition. 52 x 40 sind gerade - die
Viertel passen auch ueber Chunkgrenzen.

Reihenfolge im Streifen (Sprite geist_boden_<i>):
   0..11   Erde voll                      (SOIL)
  12..27   Weg voll: 12 + q * 4 + v       (PATH, v = Variante)
  28..83   Uebergaenge: 28 + (code - 1) * 4 + q
           Ecken-Code = tl*8 + tr*4 + bl*2 + br  (1 = Erde)

Wege kommen aus den Hoehenlinien eines Rauschfelds (|f - 0.5| klein), dazu ein
paar Lichtungen. Hindernisse stehen nie auf dem Weg (auch nicht nach dem
Umwuerfeln, siehe ChunkPropRandomizer.avoidTiles).

Aufruf aus dem Projektordner:
  python Tools/geist_props.py                     zuerst (Props + json)
  python Tools/geist_boden.py                     Tiles + Layout schreiben
  python Tools/geist_boden.py --preview a.png [chunk dx dy] [--day]   Kameraausschnitt bei Nacht
  python Tools/geist_boden.py --world a.png [scale]                   alle 9 Chunks
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
OUT_DIR = os.path.join(ROOT, "Assets", "Art", "Tiles_Geist")
SHEET = os.path.join(OUT_DIR, "geist_boden.png")
LAYOUT = os.path.join(OUT_DIR, "geist_layout.json")
PROP_DIR = os.path.join(ROOT, "Assets", "Art", "World-Objects", "Geist")
PROP_INFO = os.path.join(PROP_DIR, "geist_props.json")

T = 32
CHUNK_W, CHUNK_H = 52, 40


def hx(s):
    s = s.lstrip("#")
    return np.array([int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), 255], dtype=np.uint8)


# Kakao-Erde: warm, mittelhell - das Nachtlicht macht sie blau-dunkel.
E = {
    "deep": hx("#4a3236"),
    "low": hx("#5a3e40"),
    "base": hx("#6a4a4a"),
    "mid": hx("#765654"),
    "light": hx("#86645e"),
    "hi": hx("#9a786c"),
}
MOSS = [hx("#2e4a40"), hx("#3e6250"), hx("#527c60"), hx("#6c9a72"), hx("#94c090")]
# Lebkuchen-Pflaster und Zuckerguss-Fugen
G = [hx("#6e3c20"), hx("#87502a"), hx("#9c6034"), hx("#ad6e3c"), hx("#c08448"), hx("#d6a064")]
ICING = [hx("#9a96b6"), hx("#bab8d2"), hx("#d8d6e8"), hx("#ecebf6")]
LEAF = [[hx("#8e2c12"), hx("#c4481c"), hx("#e6702e"), hx("#ffa45a")],
        [hx("#94580e"), hx("#c8861a"), hx("#e8ae36"), hx("#ffd870")],
        [hx("#7c1626"), hx("#aa2636"), hx("#d04a50"), hx("#f28078")]]
LICO = [hx("#1e1628"), hx("#33263f"), hx("#4c3a5c"), hx("#7c6694")]
CORN = [hx("#f4f0e6"), hx("#ffb02e"), hx("#f06a1c")]
SPRINK = [hx("#ff6f9a"), hx("#ffd34e"), hx("#6fe0b6"), hx("#c88cff")]
BONE = [hx("#9a8a7a"), hx("#d8c8b0"), hx("#f4e8d4")]

SOIL = ["plain", "plain2", "plain3", "plain4", "moss", "moss2", "leaves", "leaves2",
        "pebbles", "pretzel", "candycorn", "bone"]
SOIL_W = [24, 24, 24, 24, 7, 4, 4, 3, 2.5, 0.7, 0.6, 0.35]
PATH = ["plain", "plain2", "crack", "sprinkles"]
PATH_W = [30, 30, 5, 2.5]

PATH_FIRST = len(SOIL)                 # 12
MIX_FIRST = PATH_FIRST + 4 * len(PATH)  # 28


def path_index(q, v):
    return PATH_FIRST + q * len(PATH) + v


def mix_index(code, q):
    return MIX_FIRST + (code - 1) * 4 + q


def put(img, x, y, col):
    if 0 <= x < T and 0 <= y < T:
        img[y, x] = col


# --- Feld fuer die Materialgrenze (wie Eis/Vulkan) --------------------------------

def edge_field(corners, vseed, pad=1):
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
    nz = 0.5 * (fbm(X / 16.0, Y / 16.0, 5151, octaves=2, period=(2, 2)) - 0.5)
    nz = nz + 0.12 * (value_noise(X / 4.0, Y / 4.0, 99, period=(8, 8)) - 0.5)
    m = m + np.clip(nz, -0.25, 0.25)
    ew = np.clip(np.minimum(np.minimum(X, T - X), np.minimum(Y, T - Y)) / 11.0, 0, 1)
    m = m + 0.12 * (fbm(X / 6.0, Y / 6.0, 8100 + vseed, octaves=2) - 0.5) * ew
    return m


# --- Erde -------------------------------------------------------------------------

_SOIL_N = None


def soil_noise():
    """32-px-periodisches Rauschen fuer leichte Hell/Dunkel-Flecken - nahtlos."""
    global _SOIL_N
    if _SOIL_N is None:
        X, Y = grid(T, T)
        _SOIL_N = fbm(X / 8.0, Y / 8.0, 4242, octaves=3, period=(4, 4))
    return _SOIL_N


def soil_face(seed):
    """Kakao-Erde: ruhige Flaeche, nur einzelne Koerner - kein Muster, das sich je Tile wiederholt."""
    rng = random.Random(seed)
    img = np.empty((T, T, 4), dtype=np.uint8)
    img[:] = E["base"]
    for _ in range(rng.randint(26, 34)):
        x, y = rng.randrange(T), rng.randrange(T)
        img[y, x] = E["low"] if rng.random() < 0.55 else E["mid"]
    # kleine Mulden (2 px) und Kruemel mit Schatten
    for _ in range(rng.randint(3, 5)):
        x, y = rng.randrange(T - 1), rng.randrange(T)
        img[y, x] = E["low"]
        img[y, x + 1] = E["low"]
    for _ in range(rng.randint(2, 4)):
        x, y = rng.randrange(T), rng.randrange(T - 1)
        img[y, x] = E["light"] if rng.random() < 0.7 else E["hi"]
        img[y + 1, x] = E["deep"]
    return img


def tufts(img, rng, n):
    """Minzmoos-Buschel: drei Halme, unten Schatten."""
    for _ in range(n):
        x, y = rng.randint(2, T - 4), rng.randint(3, T - 3)
        put(img, x, y, MOSS[2])
        put(img, x + 1, y, MOSS[2])
        put(img, x + 2, y, MOSS[1])
        put(img, x, y - 1, MOSS[3])
        put(img, x + 2, y - 1, MOSS[2])
        put(img, x + 1, y - 2, MOSS[4] if rng.random() < 0.4 else MOSS[3])
        put(img, x, y + 1, E["deep"])
        put(img, x + 1, y + 1, E["deep"])
        put(img, x + 2, y + 1, E["low"])


def moss_patch(img, rng, cx, cy, rx, ry):
    for y in range(int(cy - ry - 1), int(cy + ry + 2)):
        for x in range(int(cx - rx - 1), int(cx + rx + 2)):
            if not (1 <= x < T - 1 and 1 <= y < T - 1):
                continue
            d = ((x + 0.5 - cx) / rx) ** 2 + ((y + 0.5 - cy) / ry) ** 2
            d += (rng.random() - 0.5) * 0.35
            if d < 1:
                img[y, x] = MOSS[3] if d < 0.2 else MOSS[2] if d < 0.62 else MOSS[1]
    for _ in range(int(rx * ry * 0.5)):
        x = int(cx + rng.uniform(-rx, rx) * 0.8)
        y = int(cy + rng.uniform(-ry, ry) * 0.8)
        if 1 <= x < T - 1 and 1 <= y < T - 1 and (img[y, x] == MOSS[2]).all():
            img[y, x] = MOSS[3]
            put(img, x, y + 1, MOSS[1])
    # Haerchen oben, Schatten unten
    for x in range(int(cx - rx), int(cx + rx) + 1):
        if rng.random() < 0.5:
            put(img, x, int(cy - ry) - 1, MOSS[3])
        put(img, x, int(cy + ry) + 1, E["deep"])


def leaf(img, rng, x, y):
    p = rng.choice(LEAF)
    rot = rng.random() * math.pi
    for k in range(-2, 3):
        px = int(round(x + math.cos(rot) * k))
        py = int(round(y + math.sin(rot) * k * 0.6))
        put(img, px, py, p[2] if k else p[3])
        if abs(k) < 2:
            put(img, px, py - 1, p[3] if k < 0 else p[2])
            put(img, px, py + 1, p[1])
    put(img, int(round(x + math.cos(rot) * 2)) + 1, int(round(y + math.sin(rot) * 1.2)) + 1, E["deep"])


def soil_tile(kind, seed):
    rng = random.Random(seed * 13 + 5)
    img = soil_face(seed)
    if kind == "moss":
        tufts(img, rng, 1)
    elif kind == "moss2":
        tufts(img, rng, 2)
    elif kind.startswith("leaves"):
        for _ in range(2 if kind == "leaves" else 4):
            leaf(img, rng, rng.randint(5, T - 6), rng.randint(5, T - 6))
    elif kind == "pebbles":
        # Zucker-Kiesel: kleine runde Kandis-Steinchen
        for _ in range(rng.randint(4, 6)):
            x, y = rng.randint(3, T - 5), rng.randint(3, T - 5)
            put(img, x, y, ICING[3])
            put(img, x + 1, y, ICING[2])
            put(img, x, y + 1, ICING[1])
            put(img, x + 1, y + 1, ICING[0])
            put(img, x + 1, y + 2, E["deep"])
    elif kind == "pretzel":
        # Salzstange liegt quer
        x0, y0 = rng.randint(6, 10), rng.randint(10, 20)
        for k in range(14):
            x, y = x0 + k, y0 + k // 4
            put(img, x, y, G[4])
            put(img, x, y + 1, G[1])
            put(img, x, y + 2, E["deep"])
            if k % 4 == 1:
                put(img, x, y, ICING[3])
    elif kind == "candycorn":
        for _ in range(rng.randint(1, 2)):
            x, y = rng.randint(6, T - 9), rng.randint(6, T - 9)
            rows = [(0, 2, CORN[0]), (1, 3, CORN[1]), (2, 4, CORN[1]), (3, 5, CORN[2]), (4, 5, CORN[2])]
            for dy, w, col in rows:
                for dx in range(w):
                    put(img, x + dx - w // 2, y + dy, col)
            for dx in range(-2, 3):
                put(img, x + dx, y + 5, E["deep"])
    elif kind == "bone":
        # Knochen-Keks: suess, nicht gruselig
        x, y = rng.randint(8, 16), rng.randint(12, 18)
        for k in range(8):
            put(img, x + k, y, BONE[2] if k % 3 else BONE[1])
            put(img, x + k, y + 1, BONE[1])
        for ex in (x - 1, x + 8):
            for dy in (-1, 0, 1, 2):
                put(img, ex, y + dy, BONE[2] if dy < 1 else BONE[1])
        for k in range(-1, 9):
            put(img, x + k, y + 3, E["deep"])
    return img


# --- Lebkuchen-Pflaster ---------------------------------------------------------------

P = 64     # Periode des Pflasters


def cobble_cells():
    rng = random.Random(1717)
    pts = []
    n = 7
    for j in range(n):
        for i in range(n):
            # versetzte Reihen, damit es nach Pflaster aussieht
            ox = (0.5 if j % 2 else 0.0)
            pts.append(((i + ox + rng.uniform(0.15, 0.85)) * P / n, (j + rng.uniform(0.2, 0.8)) * P / n))
    return pts


_COBBLE = None


def cobble_map():
    """Fuer jedes Pixel im 64er-Feld: Zelle, Randabstand, Richtung zur Zellmitte."""
    global _COBBLE
    if _COBBLE is not None:
        return _COBBLE
    pts = cobble_cells()
    cell = np.zeros((P, P), dtype=np.int64)
    edge = np.zeros((P, P))
    dirx = np.zeros((P, P))
    diry = np.zeros((P, P))
    for y in range(P):
        for x in range(P):
            best = []
            for k, (px, py) in enumerate(pts):
                for ox in (-P, 0, P):
                    for oy in (-P, 0, P):
                        dx = x + 0.5 - (px + ox)
                        dy = (y + 0.5 - (py + oy)) * 1.15
                        best.append((dx * dx + dy * dy, k, dx, dy))
            best.sort()
            d1, k1, dx, dy = best[0]
            d2 = next(b[0] for b in best if b[1] != k1)
            cell[y, x] = k1
            edge[y, x] = math.sqrt(d2) - math.sqrt(d1)
            ln = math.sqrt(dx * dx + dy * dy) + 1e-6
            dirx[y, x] = dx / ln
            diry[y, x] = dy / ln
    _COBBLE = (cell, edge, dirx, diry)
    return _COBBLE


def path_face(q, seed):
    cell, edge, dirx, diry = cobble_map()
    rng = random.Random(seed)
    ox, oy = (q % 2) * T, (q // 2) * T
    # Kachel-Pixel y=0 ist oben; Welt-Zeile y%2 == 1 liegt oben -> in der 64er-Flaeche oben
    img = np.empty((T, T, 4), dtype=np.uint8)
    tone_of_cell = [random.Random(k * 31 + 7).choice((2, 2, 3, 3, 3, 4)) for k in range(64)]
    for y in range(T):
        for x in range(T):
            X, Y = ox + x, oy + y
            e = edge[Y, X]
            k = cell[Y, X]
            if e < 1.1:
                # Zuckerguss-Fuge, unten rechts schattig
                img[y, x] = ICING[2] if (dirx[Y, X] + diry[Y, X]) < 0 else ICING[1]
                continue
            t = tone_of_cell[k]
            lit = -(dirx[Y, X] * -0.6 + diry[Y, X] * -0.8)   # Pixel liegt oben links in der Zelle?
            if e < 2.4:
                if lit < -0.4:
                    t = min(5, t + 1)          # Kante zum Licht
                elif lit > 0.4:
                    t = max(0, t - 1)          # Kante im Schatten
                    if e < 1.8:
                        t = max(0, t - 1)
            img[y, x] = G[t]
    # Poren im Lebkuchen (nur im Inneren, nie auf der Fuge)
    for _ in range(rng.randint(8, 12)):
        x, y = rng.randrange(T), rng.randrange(T)
        if edge[oy + y, ox + x] > 2.6:
            img[y, x] = G[max(0, int(np.argmin([abs(int(img[y, x][0]) - int(g[0])) for g in G])) - 1)]
    return img


def path_tile(kind, q, seed):
    rng = random.Random(seed * 17 + 3)
    img = path_face(q, seed)
    cell, edge, _, _ = cobble_map()
    ox, oy = (q % 2) * T, (q // 2) * T
    if kind == "crack":
        x, y = rng.randint(8, 22), rng.randint(8, 22)
        for k in range(rng.randint(5, 8)):
            if edge[oy + y, ox + x] > 1.5:
                put(img, x, y, G[0])
                put(img, x + 1, y, G[1])
            x += rng.choice((1, 1, 0))
            y += rng.choice((1, 0, -1))
            if not (2 <= x < T - 2 and 2 <= y < T - 2):
                break
    elif kind == "sprinkles":
        for _ in range(rng.randint(3, 5)):
            x, y = rng.randint(3, T - 5), rng.randint(3, T - 5)
            col = rng.choice(SPRINK)
            dx, dy = rng.choice(((1, 0), (0, 1)))
            put(img, x, y, col)
            put(img, x + dx, y + dy, col)
    return img


# --- Uebergaenge: Waldboden mit Moosrand ueber dem Pflaster --------------------------------

def transition_tile(code, q):
    corners = ((code >> 3) & 1, (code >> 2) & 1, (code >> 1) & 1, code & 1)
    m = edge_field(corners, code * 10 + q, pad=2)
    soil = m > 0.5
    # Kante glaetten: 3x3-Mehrheit, zweimal - keine einzelnen Zacken und Inselchen
    for _ in range(2):
        cnt = sum(np.roll(np.roll(soil, dy, 0), dx, 1).astype(int) for dy in (-1, 0, 1) for dx in (-1, 0, 1))
        inner = soil.copy()
        inner[1:-1, 1:-1] = cnt[1:-1, 1:-1] >= 5
        soil = inner
    seed = 900 + code * 7 + q
    pav = path_face(q, seed)
    dirt = soil_face(seed + 50)
    img = np.where(soil[2:-2, 2:-2, None], dirt, pav).copy()
    a = soil
    rng = random.Random(seed)
    for y in range(T):
        for x in range(T):
            Y, X = y + 2, x + 2
            if a[Y, X]:
                if not a[Y - 1, X]:
                    # Oberkante der Erde: Moos-Saum, ab und zu ein Halm
                    img[y, x] = MOSS[3] if (x + y) % 3 else MOSS[2]
                elif not a[Y - 2, X]:
                    img[y, x] = MOSS[1] if (x + y) % 2 else E["low"]
                elif not a[Y + 1, X]:
                    img[y, x] = E["deep"]          # Kante unten: Erde bricht ab
                elif not a[Y, X + 1] or not a[Y, X - 1]:
                    img[y, x] = MOSS[1] if (y % 3) else E["low"]
            else:
                if a[Y - 1, X]:
                    img[y, x] = G[0]               # Schatten der Erdkante auf dem Pflaster
                elif a[Y - 2, X] and (x % 2 == 0):
                    img[y, x] = G[1]
                elif m[Y, X] > 0.43 and (x * 3 + y * 5) % 7 == 0:
                    img[y, x] = E["mid"]           # Erdkruemel auf dem Weg
    return img


def build():
    tiles = []
    for i, k in enumerate(SOIL):
        tiles.append(soil_tile(k, 10 + i))
    for q in range(4):
        for v, k in enumerate(PATH):
            tiles.append(path_tile(k, q, 40 + q * 10 + v))
    for code in range(1, 15):
        for q in range(4):
            tiles.append(transition_tile(code, q))
    sheet = np.zeros((T, T * len(tiles), 4), dtype=np.uint8)
    for i, t in enumerate(tiles):
        sheet[:, i * T:(i + 1) * T] = t
    return Image.fromarray(sheet, "RGBA"), len(tiles)


# --- Welt -------------------------------------------------------------------------------

PATH_HALF = 1.15      # halbe Wegbreite in Einheiten (Abstand zur Hoehenlinie)
CLEARING = 0.8        # zweites Feld darueber = Lichtung mit Pflaster


def blended(chunk, seed_g, seed_h, sx, sy, period, octaves, gain):
    """Feld je Chunk, das zum Rand hin in ein chunk-periodisches Feld uebergeht -
    so passt jeder Chunk an jeden anderen."""
    ys, xs = np.mgrid[0:CHUNK_H + 1, 0:CHUNK_W + 1].astype(np.float64)
    g = fbm(xs / sx, ys / sy, seed_g, octaves=octaves, period=period, gain=gain)
    h = fbm(xs / sx + chunk * 97.0, ys / sy + chunk * 41.0, seed_h + chunk * 11, octaves=octaves, gain=gain)
    dist = np.minimum(np.minimum(xs, CHUNK_W - xs), np.minimum(ys, CHUNK_H - ys))
    w = np.clip(1.0 - dist / 9.0, 0, 1)
    w = w * w * (3 - 2 * w)
    return w * g + (1 - w) * h, xs, ys, dist


def vertex_field(chunk):
    # Wege = Hoehenlinie f = 0.5 eines weichen Felds, ueberall gleich breit:
    # Abstand zur Linie ~ |f - 0.5| / |grad f|
    f, xs, ys, dist = blended(chunk, 3131, 991, 52 / 3, 40 / 3, (3, 3), 2, 0.35)
    gy, gx = np.gradient(f)
    d = np.abs(f - 0.5) / np.maximum(np.sqrt(gx * gx + gy * gy), 1e-4)
    f2, _, _, _ = blended(chunk, 6262, 7272, 13.0, 10.0, (4, 4), 2, 0.5)
    path = (d < PATH_HALF) | (f2 > CLEARING)
    if chunk == 4:
        # Startlichtung: kleiner Pflasterplatz um den Spawn
        path |= ((xs - CHUNK_W / 2) / 3.6) ** 2 + ((ys - CHUNK_H / 2) / 2.8) ** 2 < 1
    v = (~path).astype(np.int64)
    for _ in range(2):
        for y in range(1, CHUNK_H):
            for x in range(1, CHUNK_W):
                if dist[y, x] < 1:
                    continue
                nb = v[y - 1, x] + v[y + 1, x] + v[y, x - 1] + v[y, x + 1]
                if v[y, x] == 1 and nb <= 1:
                    v[y, x] = 0
                elif v[y, x] == 0 and nb >= 3:
                    v[y, x] = 1
    return v


def pick(rng, weights):
    r = rng.random() * sum(weights)
    for i, w in enumerate(weights):
        r -= w
        if r < 0:
            return i
    return 0


def quarter(i, j):
    # i, j = Zellindex im Chunk (minX = -26, minY = -20 sind gerade) -> Weltzelle
    wx, wy = i - CHUNK_W // 2, j - CHUNK_H // 2
    return (wx % 2) + 2 * (1 - wy % 2)     # y waechst nach oben, Bild nach unten


def chunk_tiles(chunk, v=None):
    if v is None:
        v = vertex_field(chunk)
    rng = random.Random(5000 + chunk * 131)
    rows = []
    for j in range(CHUNK_H):
        row = []
        for i in range(CHUNK_W):
            bl, br = v[j, i], v[j, i + 1]
            tl, tr = v[j + 1, i], v[j + 1, i + 1]
            code = tl * 8 + tr * 4 + bl * 2 + br
            q = quarter(i, j)
            if code == 15:
                idx = pick(rng, SOIL_W)
            elif code == 0:
                idx = path_index(q, pick(rng, PATH_W))
            else:
                idx = mix_index(code, q)
            row.append(int(idx))
        rows.append(row)
    return rows


def load_props():
    if not os.path.exists(PROP_INFO):
        return []
    with open(PROP_INFO, encoding="utf-8") as fh:
        return json.load(fh)["props"]


START_CLEAR = 5.0
EDGE_MARGIN = 1.5
GAP = 1.2             # wie minDistance am ChunkPropRandomizer
TREES_PER_CHUNK = 24
PROPS_PER_CHUNK = 34
TREES = ("lebkuchenbaum", "spukbaum", "lakritzbaum")
FLATS_PER_CHUNK = 16


def rect_of(p, x, y):
    l, r_ = -p["pivotX"] / 32.0, (p["frameW"] - p["pivotX"]) / 32.0
    b, t = -p["pivotY"] / 32.0, (p["frameH"] - p["pivotY"]) / 32.0
    return (x + l, y + b, x + r_, y + t)


def on_path(v, x, y):
    """Fuss (lokale Einheiten, Chunkmitte = 0) auf einer Weg-Zelle (irgendeine Ecke Weg)?"""
    for dx in (-0.5, 0.0, 0.5):
        i = int(math.floor(x + dx + CHUNK_W / 2))
        j = int(math.floor(y + CHUNK_H / 2))
        if 0 <= i < CHUNK_W and 0 <= j < CHUNK_H:
            if min(v[j, i], v[j, i + 1], v[j + 1, i], v[j + 1, i + 1]) == 0:
                return True
    return False


def place(chunk, props, v):
    rng = random.Random(4400 + chunk * 53)
    placed = []
    trees = [p for p in props if p["name"] in TREES]
    standing = [p for p in props if not p["flat"] and p["name"] not in TREES]
    flats = [p for p in props if p["flat"]]

    def try_place(pool, count, solid):
        weights = [p["weight"] for p in pool]
        n, tries = 0, 0
        while n < count and tries < 900:
            tries += 1
            q = pool[pick(rng, weights)]
            rect0 = rect_of(q, 0, 0)
            x = rng.uniform(-CHUNK_W / 2 + EDGE_MARGIN - rect0[0], CHUNK_W / 2 - EDGE_MARGIN - rect0[2])
            y = rng.uniform(-CHUNK_H / 2 + EDGE_MARGIN - rect0[1], CHUNK_H / 2 - EDGE_MARGIN - rect0[3])
            x = round(x * 32) / 32
            y = round(y * 32) / 32
            rect = rect_of(q, x, y)
            if chunk == 4 and rect[0] < START_CLEAR and rect[2] > -START_CLEAR \
                    and rect[1] < START_CLEAR and rect[3] > -START_CLEAR:
                continue
            if solid and on_path(v, x, y):
                continue
            if any(max(o["rect"][0] - rect[2], rect[0] - o["rect"][2],
                       o["rect"][1] - rect[3], rect[1] - o["rect"][3]) < GAP for o in placed):
                continue
            placed.append({"name": q["name"], "x": x, "y": y, "rect": rect})
            n += 1

    # gross vor klein, flach zuletzt
    if trees:
        try_place(trees, TREES_PER_CHUNK, True)
    if standing:
        try_place(standing, PROPS_PER_CHUNK, True)
    if flats:
        try_place(flats, FLATS_PER_CHUNK, False)
    return [{"name": q["name"], "x": q["x"], "y": q["y"]} for q in placed]


def layout():
    props = load_props()
    chunks = []
    for c in range(9):
        v = vertex_field(c)
        chunks.append({"tiles": chunk_tiles(c, v), "props": place(c, props, v)})
    return {"width": CHUNK_W, "height": CHUNK_H, "minX": -CHUNK_W // 2, "minY": -CHUNK_H // 2,
            "pathTiles": list(range(PATH_FIRST, MIX_FIRST)) + [
                mix_index(code, q) for code in range(1, 15) for q in range(4)],
            "chunks": chunks}


# --- Vorschau ----------------------------------------------------------------------------

def render_chunk(sheet, data):
    img = Image.new("RGBA", (CHUNK_W * T, CHUNK_H * T))
    for j, row in enumerate(data["tiles"]):
        for i, idx in enumerate(row):
            img.paste(sheet.crop((idx * T, 0, idx * T + T, T)), (i * T, (CHUNK_H - 1 - j) * T))
    return img


def paste_props(img, glow, lights, items, ox, oy, frame=0):
    info = {p["name"]: p for p in load_props()}
    order = sorted(items, key=lambda q: (not info.get(q["name"], {}).get("flat", False), -q["y"]))
    for q in order:
        p = info.get(q["name"])
        if p is None:
            continue
        path = os.path.join(PROP_DIR, "geist_%s.png" % q["name"])
        if not os.path.exists(path):
            continue
        fr = Image.open(path).convert("RGBA")
        fh = p["frameH"]
        px = int(round(ox + q["x"] * T - p["pivotX"]))
        py = int(round(oy - q["y"] * T - (fh - p["pivotY"])))
        img.alpha_composite(fr, (px, py))
        if p["glowFrames"]:
            gs = Image.open(os.path.join(PROP_DIR, "geist_%s_glow.png" % q["name"])).convert("RGBA")
            f = frame % p["glowFrames"]
            glow.alpha_composite(gs.crop((f * p["frameW"], 0, (f + 1) * p["frameW"], fh)), (px, py))
        if p.get("light"):
            lt = p["light"]
            lights.append((ox + (q["x"] + lt["x"]) * T, oy - (q["y"] + lt["y"]) * T, lt))


def figure():
    keks = os.path.join(ROOT, "Assets", "Art", "Chars", "Char_Keks.png")
    if not os.path.exists(keks):
        return None
    return Image.open(keks).convert("RGBA").crop((0, 0, 40, 40))


PLAYER_LIGHT = {"r": 1.0, "g": 0.9, "b": 0.75, "radius": 5.5, "inner": 0.5, "intensity": 0.5}


def main():
    sheet, count = build()
    os.makedirs(OUT_DIR, exist_ok=True)
    if "--preview" in sys.argv or "--world" in sys.argv:
        from geist_licht import night
        data = layout()
        day = "--day" in sys.argv
        args = [a for a in sys.argv if a != "--day"]
        if "--world" in args:
            k = args.index("--world")
            out = args[k + 1]
            big = Image.new("RGBA", (3 * CHUNK_W * T, 3 * CHUNK_H * T))
            for c in range(9):
                im = render_chunk(sheet, data["chunks"][c])
                glow = Image.new("RGBA", im.size)
                lights = []
                paste_props(im, glow, lights, data["chunks"][c]["props"], CHUNK_W * T // 2, CHUNK_H * T // 2)
                im = im if day else night(im, glow, lights)
                big.paste(im, ((c % 3) * CHUNK_W * T, (c // 3) * CHUNK_H * T))
            scale = float(args[k + 2]) if len(args) > k + 2 else 0.25
            big.resize((int(big.width * scale), int(big.height * scale)), Image.LANCZOS).save(out)
        else:
            k = args.index("--preview")
            out = args[k + 1]
            c = int(args[k + 2]) if len(args) > k + 2 else 4
            ox = int(args[k + 3]) if len(args) > k + 3 else 0
            oy = int(args[k + 4]) if len(args) > k + 4 else 0
            im = render_chunk(sheet, data["chunks"][c])
            glow = Image.new("RGBA", im.size)
            lights = []
            paste_props(im, glow, lights, data["chunks"][c]["props"], CHUNK_W * T // 2, CHUNK_H * T // 2)
            cx, cy = CHUNK_W * T // 2 + ox * T, CHUNK_H * T // 2 - oy * T
            fig = figure()
            if fig is not None:
                im.alpha_composite(fig, (cx - 20, cy - 20))
            lights.append((cx, cy, PLAYER_LIGHT))
            if not day:
                im = night(im, glow, lights)
            else:
                im.alpha_composite(glow)
            view = im.crop((cx - 240, cy - 135, cx + 240, cy + 135))
            view.resize((960, 540), Image.NEAREST).save(out)
        return
    sheet.save(SHEET)
    if not os.path.exists(SHEET + ".meta"):
        write_strip_meta(SHEET + ".meta", "geist_boden", count, T, T, 32, max_size=4096)
    with open(LAYOUT, "w", encoding="utf-8", newline="\n") as fh:
        json.dump(layout(), fh, separators=(",", ":"))
    print("geschrieben:", SHEET, count, "Tiles +", LAYOUT)


if __name__ == "__main__":
    main()
