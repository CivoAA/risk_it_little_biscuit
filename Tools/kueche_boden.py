"""
Zeichnet den Kuechenboden (World1 / Szene Map_World0): Fliesen im Schachbrett,
dazu Wand und Sockelleiste fuer die Rand-Reihen.

  Assets/Art/Tiles_Kueche/kueche_boden.png   N x 32x32  PPU 32 (1 Tile = 1 Einheit)

Reihenfolge im Streifen (Sprite kueche_boden_<i>):
   0..9   helle Fliese  (VARIANTS)
  10..19  dunkle Fliese (VARIANTS)
  20      Wand (Tapete)
  21      Sockelleiste oben  (Wand -> Boden, Holz)
  22      Sockelleiste unten (Boden -> Wand, Holz)

Schachbrett: hell, wenn (x + y) gerade ist. Die Chunks der Kueche sind 52
breit - gerade, also laeuft das Muster beim Umsetzen ohne Bruch weiter.

Fliesen sind nahtlos: jede Fliese traegt ihre Fuge nur oben und links, die
Nachbarn liefern den Rest. Licht oben links wie bei Figuren und Props.

Aufruf aus dem Projektordner:  python Tools/kueche_boden.py [--preview pfad.png]
Die .meta wird nur beim ersten Mal geschrieben (sonst verlieren Tiles ihre Verweise).
"""

import os
import random
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from unity_meta import write_strip_meta  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "Assets", "Art", "Tiles_Kueche")
SHEET = os.path.join(OUT_DIR, "kueche_boden.png")

T = 32


def hx(s):
    s = s.lstrip("#")
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), 255)


def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3)) + (255,)


# Pflaume wie die Kuechen-Vorschau im Hub (level_preview_kueche), aber heller,
# damit Gegner, Bonbons und Geschosse davor gut lesbar bleiben.
GROUT = hx("#4e3642")
GROUT_HI = hx("#5c4250")
LIGHT = {"hi": hx("#c4a6a0"), "base": hx("#ad8f8c"), "shade": hx("#9a7c7c"),
         "dot1": hx("#b89a96"), "dot2": hx("#a08281")}
DARK = {"hi": hx("#94737a"), "base": hx("#81626b"), "shade": hx("#70535d"),
        "dot1": hx("#8a6a72"), "dot2": hx("#765862")}

CRUMB = [hx("#6e4228"), hx("#a8693a"), hx("#d79a5a"), hx("#f0c587")]
JAM = [hx("#7a1f35"), hx("#b83350"), hx("#e05a6c"), hx("#ffb3b8")]
MILK = [hx("#c9d4e6"), hx("#e8eef8"), hx("#ffffff")]
FLOUR = hx("#f4ede4")
SUGAR = hx("#ffffff")

VARIANTS = ["plain", "plain2", "gloss", "chip", "crack",
            "crumbs", "flour", "jam", "milk", "sugar"]


def tile_face(pal, seed):
    rng = random.Random(seed)
    im = Image.new("RGBA", (T, T), pal["base"])
    px = im.load()
    # Terrazzo-Tupfen: wenige, kaum sichtbar - der Boden soll ruhig bleiben.
    for _ in range(14):
        x, y = rng.randint(3, T - 3), rng.randint(3, T - 3)
        px[x, y] = pal["dot1"] if rng.random() < 0.5 else pal["dot2"]
        if rng.random() < 0.3:
            px[x + 1, y] = px[x, y]
    # Kante: Licht oben/links, Schatten unten/rechts.
    for i in range(1, T):
        px[i, 1] = pal["hi"]
        px[1, i] = pal["hi"]
        px[i, T - 1] = pal["shade"]
        px[T - 1, i] = pal["shade"]
    px[1, T - 1] = pal["base"]
    px[T - 1, 1] = pal["base"]
    # Fuge oben und links.
    for i in range(T):
        px[i, 0] = GROUT
        px[0, i] = GROUT
    return im


def blob(px, cx, cy, rx, ry, col, rng=None, rough=0.0):
    for y in range(int(cy - ry - 1), int(cy + ry + 2)):
        for x in range(int(cx - rx - 1), int(cx + rx + 2)):
            if not (0 < x < T and 0 < y < T):
                continue
            d = ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2
            if rng is not None and rough:
                d += (rng.random() - 0.5) * rough
            if d <= 1.0:
                px[x, y] = col


def add_variant(im, kind, pal, seed):
    rng = random.Random(seed * 31 + 7)
    px = im.load()
    if kind == "gloss":
        # Spiegelnde Fliese: zwei kurze diagonale Lichtstriche.
        for k in range(7):
            px[6 + k, 14 - k] = pal["hi"]
            if k < 4:
                px[9 + k, 15 - k] = pal["hi"]
    elif kind == "chip":
        # Abgeschlagene Ecke unten rechts - Fuge kommt durch.
        for y in range(T - 5, T):
            for x in range(T - 5, T):
                if (x - (T - 5)) + (y - (T - 5)) >= 4:
                    px[x, y] = GROUT_HI
        for k in range(5):
            x, y = T - 1 - k, T - 5 + k
            if 0 < x < T and 0 < y < T and px[x, y] != GROUT_HI:
                px[x, y] = pal["shade"]
    elif kind == "crack":
        # Feiner Riss von der Kante aus, mit hellem Saum unten.
        x, y = 1, rng.randint(9, 20)
        while x < 22:
            px[x, y] = GROUT
            if y + 1 < T:
                px[x, y + 1] = pal["hi"] if px[x, y + 1] != GROUT else GROUT
            x += 1
            r = rng.random()
            if r < 0.3:
                y = max(3, y - 1)
            elif r < 0.6:
                y = min(T - 4, y + 1)
            if x == 13:
                bx, by = x, y
                for k in range(5):
                    by -= 1
                    bx += rng.choice([0, 1])
                    px[bx, by] = GROUT
    elif kind == "crumbs":
        spots = [(9, 10), (20, 7), (14, 19), (24, 22), (7, 24), (17, 13)]
        for i, (x, y) in enumerate(spots):
            if i % 2 == 0:
                # groesserer Kruemel 3x2 mit Licht oben links
                for dx, dy, c in [(0, 0, 2), (1, 0, 2), (2, 0, 1), (0, 1, 1), (1, 1, 1), (2, 1, 0),
                                  (1, -1, 3)]:
                    px[x + dx, y + dy] = CRUMB[c]
                px[x, y + 2] = pal["shade"]
                px[x + 1, y + 2] = pal["shade"]
                px[x + 2, y + 2] = pal["shade"]
            else:
                px[x, y] = CRUMB[2]
                px[x + 1, y] = CRUMB[1]
                px[x, y + 1] = pal["shade"]
    elif kind == "flour":
        # Mehlstaub: weicher, gestreuter Fleck, nach aussen ausduennend.
        cx, cy = 16 + rng.randint(-3, 3), 16 + rng.randint(-3, 3)
        for y in range(2, T - 1):
            for x in range(2, T - 1):
                d = ((x - cx) / 13.0) ** 2 + ((y - cy) / 9.0) ** 2
                p = 1.0 - d
                if p > 0.8 or (p > 0 and rng.random() < p * 0.75):
                    px[x, y] = FLOUR if p > 0.6 else mix(px[x, y], FLOUR, 0.55)
    elif kind == "jam":
        # Marmeladenklecks plus zwei Spritzer.
        # flacher Schmierer statt runder Tropfen (rund sieht aus wie ein Herz-Pickup)
        blob(px, 15, 17, 8, 3, JAM[1], rng, 0.5)
        blob(px, 12, 16.5, 4, 1.5, JAM[2])
        px[10, 16] = JAM[3]
        for x in range(8, 24):
            y = 21
            if px[x, y - 1] == JAM[1]:
                px[x, y] = JAM[0]
        for sx, sy in [(25, 12), (6, 22)]:
            px[sx, sy] = JAM[1]
            px[sx + 1, sy] = JAM[0]
    elif kind == "milk":
        # Milchpfuetze, flach und glaenzend.
        blob(px, 16, 17, 9, 5, MILK[0], rng, 0.25)
        blob(px, 15, 16, 7.5, 3.8, MILK[1], rng, 0.2)
        for x in range(10, 15):
            px[x, 14] = MILK[2]
        px[18, 15] = MILK[2]
        blob(px, 26, 9, 1.6, 1.2, MILK[1])
        px[26, 10] = MILK[0]
    elif kind == "sugar":
        # Verstreuter Zucker: einzelne glitzernde Koerner.
        for _ in range(16):
            x, y = rng.randint(3, T - 3), rng.randint(3, T - 3)
            px[x, y] = SUGAR
            px[x + 1, y + 1] = pal["shade"]
        for x, y in [(11, 12), (21, 20)]:
            px[x, y] = SUGAR
            px[x - 1, y] = mix(SUGAR, pal["base"], 0.5)
            px[x + 1, y] = mix(SUGAR, pal["base"], 0.5)
            px[x, y - 1] = mix(SUGAR, pal["base"], 0.5)
            px[x, y + 1] = mix(SUGAR, pal["base"], 0.5)


# Wie stark die Deko in die Fliese zurueckgemischt wird. Kleckse duerfen nie
# wie Herzen, Bonbons oder Geschosse aussehen - sie sind nur Stimmung.
DAMPEN = {"jam": 0.45, "milk": 0.4, "crumbs": 0.25, "flour": 0.3, "sugar": 0.35}


def floor_tile(pal, kind, seed):
    im = tile_face(pal, seed)
    if kind in ("plain", "plain2"):
        return im
    before = im.copy()
    add_variant(im, kind, pal, seed)
    k = DAMPEN.get(kind, 0.0)
    if k:
        a, b = im.load(), before.load()
        for y in range(T):
            for x in range(T):
                if a[x, y] != b[x, y]:
                    a[x, y] = mix(a[x, y], b[x, y], k)
    return im


# Wand: warme Creme-Tapete mit feinen Streifen und kleinen Blumen-Rauten wie
# die Wand in der Vorschau. Kachelt in x und y.
WALL_BASE = hx("#e9d9a4")
WALL_STRIPE = hx("#dfcb93")
WALL_DOT = hx("#cfb57f")
WALL_DOT_HI = hx("#f5ead0")


def wall_tile():
    im = Image.new("RGBA", (T, T), WALL_BASE)
    px = im.load()
    for y in range(T):
        for x in (0, 1, 16, 17):
            px[x, y] = WALL_STRIPE
    for cx, cy in [(8, 8), (24, 24)]:
        for dx, dy in [(0, -1), (-1, 0), (1, 0), (0, 1)]:
            px[cx + dx, cy + dy] = WALL_DOT
        px[cx, cy] = WALL_DOT_HI
    return im


WOOD = [hx("#5a3a2a"), hx("#7e5b32"), hx("#a77c44"), hx("#c9975a"), hx("#dfb276")]


def skirting(top):
    """top=True: Wand oben, Leiste unten (an den Boden). Sonst gespiegelt."""
    im = wall_tile()
    px = im.load()
    h = 12
    rows = []
    # Von der Wand zum Boden: Kante, Profil, Flaeche, Schattenkante, Bodenschatten.
    rows.append(WOOD[0])
    rows.append(WOOD[4])
    rows.append(WOOD[3])
    rows += [WOOD[2]] * (h - 6)
    rows.append(WOOD[1])
    rows.append(WOOD[1])
    rows.append(WOOD[0])
    for i, col in enumerate(rows):
        y = (T - h + i) if top else (h - 1 - i)
        for x in range(T):
            px[x, y] = col
    # Holzmaserung: kurze dunkle Striche in der Flaeche, kachelt in x.
    rng = random.Random(5 if top else 6)
    for _ in range(4):
        x0 = rng.randint(0, T - 1)
        yi = rng.randint(3, h - 4)
        y = (T - h + yi) if top else (h - 1 - yi)
        for k in range(rng.randint(4, 8)):
            px[(x0 + k) % T, y] = WOOD[1]
    if not top:
        # unten sieht man die Oberkante der Leiste von oben: Lichtkante
        px_y = h - 1
        for x in range(T):
            px[x, px_y] = WOOD[0]
    return im


def build():
    tiles = []
    for pal, off in ((LIGHT, 0), (DARK, 100)):
        for i, kind in enumerate(VARIANTS):
            tiles.append(floor_tile(pal, kind, off + i))
    tiles.append(wall_tile())
    tiles.append(skirting(True))
    tiles.append(skirting(False))
    sheet = Image.new("RGBA", (T * len(tiles), T), (0, 0, 0, 0))
    for i, t in enumerate(tiles):
        sheet.paste(t, (i * T, 0))
    return sheet, len(tiles)


def preview(sheet, path, cols=26, rows=14, scale=3):
    """Ausschnitt der Kueche: Wand, Leiste, Boden mit verstreuten Varianten."""
    rng = random.Random(4)
    n = len(VARIANTS)
    weights = [40, 40, 6, 1.5, 1.5, 2, 1.2, 0.6, 0.6, 1.2]
    img = Image.new("RGBA", (cols * T, rows * T))
    for r in range(rows):
        for c in range(cols):
            if r < 2:
                idx = 2 * n
            elif r == 2:
                idx = 2 * n + 1
            elif r == rows - 1:
                idx = 2 * n + 2
            else:
                v = rng.choices(range(n), weights)[0]
                idx = v + (0 if (r + c) % 2 == 0 else n)
            img.paste(sheet.crop((idx * T, 0, idx * T + T, T)), (c * T, r * T))
    img.resize((img.width * scale // 2, img.height * scale // 2), Image.NEAREST).save(path)


def main():
    sheet, count = build()
    os.makedirs(OUT_DIR, exist_ok=True)
    if "--preview" in sys.argv:
        preview(sheet, sys.argv[sys.argv.index("--preview") + 1])
        return
    sheet.save(SHEET)
    meta = SHEET + ".meta"
    if not os.path.exists(meta):
        write_strip_meta(meta, "kueche_boden", count, T, T, 32)
    print("geschrieben:", SHEET, count, "Tiles")


if __name__ == "__main__":
    main()
