"""
Boss-Portraits fuer die Levelauswahl: alle vier Weltbosse als gleich grosse
48x48-Kacheln (Assets/Resources/LevelSelect/Bosses/[EnemyId].png).

Quelle sind die Bestiarium-Bilder. Jeder Boss bekommt einen Ausschnitt
(grosse Figuren nur Kopf + Oberkoerper) und wird dann auf 48 px gebracht.
Verkleinert wird pixelweise "nach Mehrheit": jedes Zielpixel nimmt die
haeufigste Farbe seines Quellfelds, dunkle Konturen gewinnen schon ab einem
Drittel - so bleibt der Umriss geschlossen statt zu zerbroeseln. Zum Schluss
eine einheitliche 1-px-Kontur um die Silhouette, damit alle vier gleich
"gestanzt" aussehen.

    python Tools/boss_portraits.py           # schreibt PNGs (+ .meta, falls neu)
    python Tools/boss_portraits.py --preview # zusaetzlich Tools/out/boss_portraits.png
"""

import os
import sys
from collections import Counter

from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from unity_meta import write_strip_meta  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "Assets", "Resources", "Bestiary")
DST = os.path.join(ROOT, "Assets", "Resources", "LevelSelect", "Bosses")
SIZE = 48
OUTLINE = (24, 14, 18, 255)

# EnemyId -> Ausschnitt (x, y, w, h) im Bestiarium-Bild, None = ganzes Bild.
# Der Ausschnitt wird quadratisch aufgefuellt und dann auf SIZE gebracht.
BOSSES = {
    "KeksKoenig": None,
    "Glutwurz": None,
    "Eiskaiser": (2, 0, 66, 66),     # Krone bis Orden, Kaiser von vorn halb
    "Verkohlter": None,
    "Squiddy": (14, 0, 81, 81),      # das Gespenst (Squiddy bleibt die Ueberraschung)
}


def is_dark(c):
    return c[3] > 0 and (c[0] + c[1] + c[2]) < 150


def downscale(img, size):
    """Mehrheits-Verkleinerung, Konturen werden bevorzugt."""
    if img.width == size and img.height == size:
        return img.copy()
    src = img.load()
    out = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    dst = out.load()
    sx, sy = img.width / size, img.height / size
    for ty in range(size):
        y0, y1 = int(ty * sy), max(int(ty * sy) + 1, int((ty + 1) * sy))
        for tx in range(size):
            x0, x1 = int(tx * sx), max(int(tx * sx) + 1, int((tx + 1) * sx))
            cols = [src[x, y] for y in range(y0, y1) for x in range(x0, x1)]
            opaque = [c for c in cols if c[3] >= 128]
            if len(opaque) * 2 < len(cols):
                continue
            dark = [c for c in opaque if is_dark(c)]
            pool = dark if len(dark) * 3 >= len(opaque) else opaque
            c = Counter(pool).most_common(1)[0][0]
            dst[tx, ty] = (c[0], c[1], c[2], 255)
    return out


def square(img, box):
    if box:
        img = img.crop((box[0], box[1], box[0] + box[2], box[1] + box[3]))
    img = img.crop(img.getbbox())
    # Platz fuer die Kontur lassen: Inhalt auf SIZE-2 bringen
    side = max(img.width, img.height)
    sq = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    # waagrecht mittig, unten buendig (Figuren stehen)
    sq.alpha_composite(img, ((side - img.width) // 2, side - img.height))
    return sq


def outline(img):
    px = img.load()
    out = img.copy()
    o = out.load()
    w, h = img.size
    for y in range(h):
        for x in range(w):
            if px[x, y][3]:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < w and 0 <= ny < h and px[nx, ny][3]:
                    o[x, y] = OUTLINE
                    break
    return out


def build(name, box):
    img = Image.open(os.path.join(SRC, name + ".png")).convert("RGBA")
    body = downscale(square(img, box), SIZE - 2)
    tile = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    tile.alpha_composite(body, (1, 1))
    return outline(tile)


def main():
    os.makedirs(DST, exist_ok=True)
    tiles = []
    for name, box in BOSSES.items():
        tile = build(name, box)
        tiles.append(tile)
        path = os.path.join(DST, name + ".png")
        tile.save(path)
        if not os.path.exists(path + ".meta"):
            write_strip_meta(path + ".meta", name, 1, SIZE, SIZE, SIZE)
        print("geschrieben:", os.path.relpath(path, ROOT))

    if "--preview" in sys.argv:
        pad = 6
        sheet = Image.new("RGBA", (len(tiles) * (SIZE + pad) + pad, SIZE + 2 * pad), (52, 44, 58, 255))
        for i, t in enumerate(tiles):
            sheet.alpha_composite(t, (pad + i * (SIZE + pad), pad))
        os.makedirs(os.path.join(ROOT, "Tools", "out"), exist_ok=True)
        out = os.path.join(ROOT, "Tools", "out", "boss_portraits.png")
        sheet.resize((sheet.width * 6, sheet.height * 6), Image.NEAREST).save(out)
        print("Vorschau:", os.path.relpath(out, ROOT))


if __name__ == "__main__":
    main()
