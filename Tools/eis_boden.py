"""
Zeichnet den Boden der Eiswelt (Level 3, Szene Map_World5): Vanilleschnee,
darunter schaut an kahlen Stellen eine riesige goldene Waffel hervor.

  Assets/Art/Tiles_Eis/eis_boden.png     N x 32x32, PPU 32 (1 Tile = 1 Einheit)
  Assets/Art/Tiles_Eis/eis_layout.json   fertig belegte 9 Chunks + Props + Glatteis

Aufbau wie Tools/vulkan_boden.py (Marching Squares an den Ecken): jede Ecke
ist Waffel (0) oder Schnee (1), die Grenze im Tile kommt aus einem Feld, das an
den Kanten nur von den Kanten-Ecken abhaengt - jede Kante passt an jeden
Nachbarn. Das Waffelgitter hat 8 px Teilung und laeuft darum ueber alle Tiles.

Reihenfolge im Streifen (Sprite eis_boden_<i>):
   0..11   Schnee voll   (SNOW)
  12..19   Waffel voll   (WAFFLE)
  20..47   Uebergaenge: Ecken-Code 1..14 je zwei Varianten -> 20 + (code-1)*2 + v
           Ecken-Code = tl*8 + tr*4 + bl*2 + br  (1 = Schnee)

Props und Glatteis (Bilder + Masse aus Tools/eis_props.py -> eis_props.json,
also das zuerst laufen lassen) verteilt diese Datei gleich mit.

Aufruf aus dem Projektordner:
  python Tools/eis_boden.py                    Tiles + Layout schreiben
  python Tools/eis_boden.py --preview a.png    Kameraausschnitt mit Figur
  python Tools/eis_boden.py --world a.png      alle 9 Chunks
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
OUT_DIR = os.path.join(ROOT, "Assets", "Art", "Tiles_Eis")
SHEET = os.path.join(OUT_DIR, "eis_boden.png")
LAYOUT = os.path.join(OUT_DIR, "eis_layout.json")
PROP_DIR = os.path.join(ROOT, "Assets", "Art", "World-Objects", "Eis")
PROP_INFO = os.path.join(PROP_DIR, "eis_props.json")

T = 32
CHUNK_W, CHUNK_H = 52, 40


def hx(s):
    s = s.lstrip("#")
    return np.array([int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), 255], dtype=np.uint8)


# Schnee kuehl-lavendel und eine Spur dunkler als der Schnee auf den Props,
# damit die sich abheben. Weisse Gegner (Marshmellos) behalten ihre Kontur.
S = {
    "shadow": hx("#9c9ec6"),
    "low": hx("#adafd2"),
    "dip": hx("#b7b9da"),
    "base": hx("#c3c5e2"),
    "mid": hx("#cacce7"),
    "light": hx("#d5d7ee"),
    "hi": hx("#e4e6f6"),
    "white": hx("#ffffff"),
    "frost": hx("#a9cdec"),
}
# Waffel goldbraun, etwas getoastet und gedeckt - der Keks soll nicht darin verschwinden.
W = {
    "ink": hx("#5a3018"),
    "pit_dark": hx("#7a4520"),
    "pit": hx("#8e5426"),
    "pit_light": hx("#a1652e"),
    "ridge": hx("#b57a38"),
    "ridge_hi": hx("#cc9548"),
    "glint": hx("#e2b464"),
}
SYRUP = [hx("#6e2a0e"), hx("#9a4410"), hx("#c46a1a"), hx("#f0a84a")]
SPRINK = [hx("#ff6f9a"), hx("#ffd34e"), hx("#6fe0b6"), hx("#6fb2ff"), hx("#c88cff")]
CHOC = [hx("#3a1e12"), hx("#5a3220"), hx("#7a4a2e")]
CHERRY = [hx("#6a1222"), hx("#b0222c"), hx("#e8545c"), hx("#ffb6b4")]

SNOW = ["plain", "plain2", "plain3", "plain4", "ripples", "ripples2", "sparkle",
        "sprinkles", "frost", "crumbs", "lump", "cherry"]
SNOW_W = [26, 26, 26, 26, 3, 3, 4, 1.2, 1.2, 0.8, 1.6, 0.25]
WAFFLE = ["plain", "plain2", "plain3", "plain4", "syrup", "syrup2", "sugar", "crumb"]
WAFFLE_W = [20, 20, 20, 20, 1.6, 1.6, 5, 1.2]

MIX_FIRST = len(SNOW) + len(WAFFLE)   # 20
MIX_VARIANTS = 2


def mix_index(code, v):
    return MIX_FIRST + (code - 1) * MIX_VARIANTS + v


def put(img, x, y, col):
    if 0 <= x < T and 0 <= y < T:
        img[y, x] = col


# --- Feld fuer die Materialgrenze (wie Vulkan) ---------------------------------

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
    n = 1.5 * (fbm(X / 8.0, Y / 8.0, 6161, octaves=3, period=(4, 4)) - 0.5)
    n = n + 0.08 * (value_noise(X / 2.0, Y / 2.0, 77, period=(16, 16)) - 0.5)
    m = m + np.clip(n, -0.47, 0.47)
    ew = np.clip(np.minimum(np.minimum(X, T - X), np.minimum(Y, T - Y)) / 11.0, 0, 1)
    m = m + 0.34 * (fbm(X / 6.0, Y / 6.0, 9100 + vseed, octaves=2) - 0.5) * ew
    return m


# --- Grundflaechen ----------------------------------------------------------------

def waffle_face(seed):
    """Waffelgitter mit 8 px Teilung: 2 px Grat, 6 px Mulde. Licht oben links."""
    rng = random.Random(seed)
    img = np.empty((T, T, 4), dtype=np.uint8)
    for y in range(T):
        for x in range(T):
            gx, gy = x % 8, y % 8
            if gx < 2 or gy < 2:
                # Grat: Oberkante/linke Kante im Licht
                img[y, x] = W["ridge_hi"] if (gy == 0 and gx >= 2) or (gx == 0 and gy >= 2) else W["ridge"]
            else:
                # Mulde: oben/links Schatten vom Grat, unten rechts aufgehellt
                if gx == 2 or gy == 2:
                    img[y, x] = W["pit_dark"]
                elif gx == 7 or gy == 7:
                    img[y, x] = W["pit_light"]
                else:
                    img[y, x] = W["pit"]
    # kleine Unregelmaessigkeiten: Kruemel auf den Graten, Toastflecken
    for _ in range(rng.randint(3, 6)):
        x, y = rng.randrange(T), rng.randrange(T)
        if x % 8 < 2 or y % 8 < 2:
            img[y, x] = W["glint"] if rng.random() < 0.5 else W["pit_light"]
    for _ in range(rng.randint(1, 3)):
        cx, cy = rng.randrange(4) * 8 + 4, rng.randrange(4) * 8 + 4
        for dx, dy in ((0, 0), (1, 0), (0, 1)):
            img[cy + dy, cx + dx] = W["pit_dark"]
    return img


def snow_face(seed):
    rng = random.Random(seed)
    img = np.empty((T, T, 4), dtype=np.uint8)
    img[:] = S["base"]
    # kleine Mulden und Glanzkoerner, gleichmaessig verteilt (keine grossen Flecken)
    for _ in range(rng.randint(3, 5)):
        x, y = rng.randint(0, T - 5), rng.randint(0, T - 1)
        for k in range(rng.randint(2, 4)):
            img[y, (x + k) % T] = S["mid"]
    for _ in range(rng.randint(2, 4)):
        x, y = rng.randint(0, T - 3), rng.randint(0, T - 2)
        img[y, x] = S["dip"]
        img[y, x + 1] = S["dip"]
        img[(y + 1) % T, x + 1] = S["light"]
    for _ in range(rng.randint(4, 7)):
        img[rng.randrange(T), rng.randrange(T)] = S["light"]
    for _ in range(rng.randint(1, 2)):
        img[rng.randrange(T), rng.randrange(T)] = S["hi"]
    return img


def ripples(img, rng, count):
    ys = sorted(rng.sample(range(6, 27, 3), count))
    for y0 in ys:
        x0 = rng.randint(2, 9)
        x1 = rng.randint(20, 29)
        ph = rng.random() * 6.28
        for x in range(x0, x1):
            y = y0 + int(round(1.1 * math.sin(x * 0.38 + ph)))
            if min(x - x0, x1 - 1 - x) == 0 and rng.random() < 0.5:
                continue
            put(img, x, y - 1, S["hi"])
            put(img, x, y, S["dip"])


def snow_tile(kind, seed):
    rng = random.Random(seed * 13 + 5)
    img = snow_face(seed)
    if kind == "ripples":
        ripples(img, rng, 2)
    elif kind == "ripples2":
        ripples(img, rng, 3)
    elif kind == "sparkle":
        for _ in range(rng.randint(3, 5)):
            x, y = rng.randint(3, T - 4), rng.randint(3, T - 4)
            put(img, x, y, S["white"])
            if rng.random() < 0.5:
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    put(img, x + dx, y + dy, S["hi"])
    elif kind == "sprinkles":
        for _ in range(rng.randint(3, 5)):
            x, y = rng.randint(3, T - 5), rng.randint(3, T - 5)
            col = rng.choice(SPRINK)
            dx, dy = rng.choice(((1, 0), (0, 1), (1, 1)))
            put(img, x, y, col)
            put(img, x + dx, y + dy, col)
            put(img, x + dx, y + dy + 1, S["dip"])
    elif kind == "frost":
        # Eisblume: sechs Strahlen mit Seitenaestchen
        cx, cy = rng.randint(11, 20), rng.randint(11, 20)
        for k in range(6):
            a = k * math.pi / 3 + rng.random() * 0.2
            for r in range(1, 6):
                x, y = cx + int(round(math.cos(a) * r)), cy + int(round(math.sin(a) * r))
                put(img, x, y, S["frost"] if r < 5 else S["hi"])
                if r == 3:
                    for s in (-1, 1):
                        put(img, x + int(round(math.cos(a + s * 0.9) * 1.5)),
                            y + int(round(math.sin(a + s * 0.9) * 1.5)), S["hi"])
        put(img, cx, cy, S["white"])
    elif kind == "crumbs":
        # Kekskruemel - hier ist schon mal ein Keks durchgelaufen
        for _ in range(rng.randint(4, 6)):
            x, y = rng.randint(4, T - 6), rng.randint(4, T - 6)
            put(img, x, y, W["ridge_hi"])
            put(img, x + 1, y, W["ridge"])
            put(img, x, y + 1, W["pit"])
            put(img, x + 1, y + 1, S["dip"])
    elif kind == "lump":
        cx, cy = rng.randint(10, 21), rng.randint(11, 20)
        for y in range(cy - 3, cy + 4):
            for x in range(cx - 5, cx + 6):
                d = ((x - cx) / 5.5) ** 2 + ((y - cy) / 3.5) ** 2
                if d <= 1:
                    put(img, x, y, S["hi"] if y < cy - 1 else S["light"] if y < cy + 2 else S["mid"])
        for x in range(cx - 4, cx + 5):
            put(img, x, cy + 4, S["low"])
        put(img, cx - 2, cy - 2, S["white"])
    elif kind == "cherry":
        cx, cy = rng.randint(12, 19), rng.randint(14, 19)
        for y in range(cy - 2, cy + 3):
            for x in range(cx - 2, cx + 3):
                if (x - cx) ** 2 + (y - cy) ** 2 <= 5:
                    put(img, x, y, CHERRY[2] if x + y < cx + cy - 1 else CHERRY[1])
        put(img, cx - 1, cy - 1, CHERRY[3])
        for x in range(cx - 2, cx + 3):
            put(img, x + 1, cy + 3, S["low"])
        put(img, cx + 1, cy - 3, CHOC[1])
        put(img, cx + 2, cy - 4, CHOC[1])
        put(img, cx + 3, cy - 5, CHOC[2])
    return img


def waffle_tile(kind, seed):
    rng = random.Random(seed * 17 + 3)
    img = waffle_face(seed)
    kind = kind.rstrip("0123456789")
    if kind == "syrup":
        # Sirup-Lache: laeuft ueber Grate und Mulden, glaenzt oben links
        cx, cy = rng.randint(11, 20), rng.randint(11, 20)
        rx, ry = rng.uniform(6, 9), rng.uniform(4, 6)
        ph = rng.random() * 6.28
        inside = set()
        for y in range(T):
            for x in range(T):
                a = math.atan2(y - cy, x - cx)
                rr = 1 + 0.18 * math.sin(3 * a + ph)
                if ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= rr * rr:
                    inside.add((x, y))
        for (x, y) in inside:
            pit = x % 8 >= 2 and y % 8 >= 2
            img[y, x] = SYRUP[2] if pit else SYRUP[1]
        for (x, y) in inside:
            if (x, y + 1) not in inside:
                img[y, x] = SYRUP[0]
            elif (x, y - 1) not in inside:
                img[y, x] = SYRUP[3] if (x + y) % 3 else SYRUP[2]
        for dx in range(3):
            put(img, int(cx - rx / 2) + dx, int(cy - ry / 2), SYRUP[3])
        put(img, int(cx - rx / 2), int(cy - ry / 2) + 1, SYRUP[3])
    elif kind == "sugar":
        for _ in range(rng.randint(14, 22)):
            put(img, rng.randrange(T), rng.randrange(T), S["hi"] if rng.random() < 0.6 else S["light"])
    elif kind == "crumb":
        cx, cy = rng.randint(10, 20), rng.randint(10, 20)
        for y in range(cy - 2, cy + 3):
            for x in range(cx - 3, cx + 3):
                if ((x - cx + 0.5) / 3) ** 2 + ((y - cy) / 2.5) ** 2 <= 1:
                    put(img, x, y, CHOC[2] if y < cy else CHOC[1])
        put(img, cx - 1, cy - 1, hx("#a26a44"))
        for x in range(cx - 2, cx + 3):
            put(img, x + 1, cy + 3, W["ink"])
    return img


# --- Uebergaenge: Schneewehe ueber der Waffel ---------------------------------------

def transition_tile(code, v):
    corners = ((code >> 3) & 1, (code >> 2) & 1, (code >> 1) & 1, code & 1)
    m = edge_field(corners, code * 10 + v, pad=1)
    snow = m > 0.5
    nb = (np.roll(snow, 1, 0).astype(int) + np.roll(snow, -1, 0) + np.roll(snow, 1, 1) + np.roll(snow, -1, 1))
    snow = np.where(snow & (nb <= 1), False, np.where(~snow & (nb >= 3), True, snow))
    seed = 700 + code * 7 + v
    waf = waffle_face(seed)
    sno = snow_face(seed + 50)
    img = np.where(snow[1:-1, 1:-1, None], sno, waf).copy()
    inner = m[1:-1, 1:-1]
    a = snow
    for y in range(T):
        for x in range(T):
            Y, X = y + 1, x + 1
            if a[Y, X]:
                if not a[Y - 1, X]:
                    img[y, x] = S["white"] if (x + y) % 3 else S["hi"]   # Oberkante der Wehe
                elif not a[Y + 1, X]:
                    img[y, x] = S["low"]
                elif not a[Y, X + 1]:
                    img[y, x] = S["dip"]
                elif not a[Y - 2, X] if Y >= 2 else False:
                    img[y, x] = S["hi"]
            else:
                if a[Y - 1, X]:
                    img[y, x] = W["ink"]                 # Schatten unter der Wehe
                elif Y >= 2 and a[Y - 2, X]:
                    img[y, x] = W["pit_dark"]
                elif inner[y, x] > 0.40 and (x * 3 + y * 5) % 7 == 0:
                    img[y, x] = S["light"]               # Puderschnee auf der Waffel
                elif inner[y, x] > 0.33 and (x + 2 * y) % 9 == 0:
                    img[y, x] = S["base"]
    return img


def build():
    tiles = []
    for i, k in enumerate(SNOW):
        tiles.append(snow_tile(k, 10 + i))
    for i, k in enumerate(WAFFLE):
        tiles.append(waffle_tile(k, 40 + i))
    for code in range(1, 15):
        for v in range(MIX_VARIANTS):
            tiles.append(transition_tile(code, v))
    sheet = np.zeros((T, T * len(tiles), 4), dtype=np.uint8)
    for i, t in enumerate(tiles):
        sheet[:, i * T:(i + 1) * T] = t
    return Image.fromarray(sheet, "RGBA"), len(tiles)


# --- Welt -------------------------------------------------------------------------------

SNOW_THRESHOLD = 0.37     # Feld > Schwelle = Schnee; so bleibt die Waffel in Inseln


def vertex_field(chunk):
    ys, xs = np.mgrid[0:CHUNK_H + 1, 0:CHUNK_W + 1].astype(np.float64)
    g = fbm(xs / 6.5, ys / 8.0, 2718, octaves=3, period=(8, 5), gain=0.55)
    h = fbm(xs / 6.5 + chunk * 97.0, ys / 8.0 + chunk * 41.0, 777 + chunk * 11, octaves=3, gain=0.55)
    dist = np.minimum(np.minimum(xs, CHUNK_W - xs), np.minimum(ys, CHUNK_H - ys))
    w = np.clip(1.0 - dist / 7.0, 0, 1)
    w = w * w * (3 - 2 * w)
    f = w * g + (1 - w) * h
    v = (f > SNOW_THRESHOLD).astype(np.int64)
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
    v = vertex_field(chunk)
    rng = random.Random(5000 + chunk * 131)
    rows = []
    for j in range(CHUNK_H):
        row = []
        for i in range(CHUNK_W):
            bl, br = v[j, i], v[j, i + 1]
            tl, tr = v[j + 1, i], v[j + 1, i + 1]
            code = tl * 8 + tr * 4 + bl * 2 + br
            if code == 15:
                idx = pick(rng, SNOW_W)
            elif code == 0:
                idx = len(SNOW) + pick(rng, WAFFLE_W)
            else:
                idx = mix_index(code, rng.randrange(MIX_VARIANTS))
            row.append(int(idx))
        rows.append(row)
    return rows


def load_props():
    if not os.path.exists(PROP_INFO):
        return [], []
    with open(PROP_INFO, encoding="utf-8") as fh:
        d = json.load(fh)
    return d["props"], d["ponds"]


START_CLEAR = 5.0
EDGE_MARGIN = 1.5
GAP = 1.4             # wie minDistance am ChunkPropRandomizer
PONDS_PER_CHUNK = 6
PROPS_PER_CHUNK = 24


def rect_of(p, x, y):
    l, r_ = -p["pivotX"] / 32.0, (p["frameW"] - p["pivotX"]) / 32.0
    b, t = -p["pivotY"] / 32.0, (p["frameH"] - p["pivotY"]) / 32.0
    return (x + l, y + b, x + r_, y + t)


def place(chunk, props, ponds):
    rng = random.Random(4400 + chunk * 53)
    placed = []

    def try_place(p, kind, count):
        n, tries = 0, 0
        while n < count and tries < 600:
            tries += 1
            q = p()
            rect0 = rect_of(q, 0, 0)
            x = rng.uniform(-CHUNK_W / 2 + EDGE_MARGIN - rect0[0], CHUNK_W / 2 - EDGE_MARGIN - rect0[2])
            y = rng.uniform(-CHUNK_H / 2 + EDGE_MARGIN - rect0[1], CHUNK_H / 2 - EDGE_MARGIN - rect0[3])
            x = round(x * 32) / 32
            y = round(y * 32) / 32
            rect = rect_of(q, x, y)
            # Startplatz in der Mitte frei lassen
            if chunk == 4 and rect[0] < START_CLEAR and rect[2] > -START_CLEAR \
                    and rect[1] < START_CLEAR and rect[3] > -START_CLEAR:
                continue
            if any(max(o["rect"][0] - rect[2], rect[0] - o["rect"][2],
                       o["rect"][1] - rect[3], rect[1] - o["rect"][3]) < GAP for o in placed):
                continue
            placed.append({"name": (kind + q["name"]), "x": x, "y": y, "rect": rect})
            n += 1

    if ponds:
        pw = [p["weight"] for p in ponds]
        try_place(lambda: ponds[pick(rng, pw)], "glatteis_", PONDS_PER_CHUNK)
    if props:
        sw = [p["weight"] for p in props]
        try_place(lambda: props[pick(rng, sw)], "", PROPS_PER_CHUNK)
    return [{"name": q["name"], "x": q["x"], "y": q["y"]} for q in placed]


def layout():
    props, ponds = load_props()
    chunks = []
    for c in range(9):
        chunks.append({"tiles": chunk_tiles(c), "props": place(c, props, ponds)})
    return {"width": CHUNK_W, "height": CHUNK_H, "minX": -CHUNK_W // 2, "minY": -CHUNK_H // 2,
            "chunks": chunks}


# --- Vorschau ----------------------------------------------------------------------------

def render_chunk(sheet, data):
    img = Image.new("RGBA", (CHUNK_W * T, CHUNK_H * T))
    for j, row in enumerate(data["tiles"]):
        for i, idx in enumerate(row):
            img.paste(sheet.crop((idx * T, 0, idx * T + T, T)), (i * T, (CHUNK_H - 1 - j) * T))
    return img


def paste_props(img, items, ox, oy, frame=4):
    props, ponds = load_props()
    info = {p["name"]: p for p in props}
    info.update({"glatteis_" + p["name"]: p for p in ponds})
    # flach zuerst, dann stehend von hinten (oben) nach vorne
    order = sorted(items, key=lambda q: (not q["name"].startswith("glatteis_"), -q["y"]))
    for q in order:
        p = info.get(q["name"])
        if p is None:
            continue
        path = os.path.join(PROP_DIR, "eis_%s.png" % q["name"])
        if not os.path.exists(path):
            continue
        strip = Image.open(path).convert("RGBA")
        fw, fh = p["frameW"], p["frameH"]
        f = frame if q["name"].startswith("glatteis_") else 0
        fr = strip.crop((f * fw, 0, f * fw + fw, fh))
        px = ox + q["x"] * T - p["pivotX"]
        py = oy - q["y"] * T - (fh - p["pivotY"])
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
            k = sys.argv.index("--world")
            out = sys.argv[k + 1]
            big = Image.new("RGBA", (3 * CHUNK_W * T, 3 * CHUNK_H * T))
            for c in range(9):
                im = render_chunk(sheet, data["chunks"][c])
                paste_props(im, data["chunks"][c]["props"], CHUNK_W * T // 2, CHUNK_H * T // 2)
                big.paste(im, ((c % 3) * CHUNK_W * T, (c // 3) * CHUNK_H * T))
            scale = float(sys.argv[k + 2]) if len(sys.argv) > k + 2 else 0.25
            big.resize((int(big.width * scale), int(big.height * scale)), Image.LANCZOS).save(out)
        else:
            k = sys.argv.index("--preview")
            out = sys.argv[k + 1]
            c = int(sys.argv[k + 2]) if len(sys.argv) > k + 2 else 4
            ox = int(sys.argv[k + 3]) if len(sys.argv) > k + 3 else 0
            oy = int(sys.argv[k + 4]) if len(sys.argv) > k + 4 else 0
            im = render_chunk(sheet, data["chunks"][c])
            paste_props(im, data["chunks"][c]["props"], CHUNK_W * T // 2, CHUNK_H * T // 2)
            cx, cy = CHUNK_W * T // 2 + ox * T, CHUNK_H * T // 2 - oy * T
            view = im.crop((cx - 240, cy - 135, cx + 240, cy + 135))
            fig = figure()
            if fig is not None:
                view.alpha_composite(fig, (220, 115))
            view.resize((960, 540), Image.NEAREST).save(out)
        return
    sheet.save(SHEET)
    if not os.path.exists(SHEET + ".meta"):
        write_strip_meta(SHEET + ".meta", "eis_boden", count, T, T, 32)
    with open(LAYOUT, "w", encoding="utf-8", newline="\n") as fh:
        json.dump(layout(), fh, separators=(",", ":"))
    print("geschrieben:", SHEET, count, "Tiles +", LAYOUT)


if __name__ == "__main__":
    main()
