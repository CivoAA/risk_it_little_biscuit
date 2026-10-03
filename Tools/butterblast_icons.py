"""
Zeichnet die Icons des Butterblast passend zum Geschoss (Tools/butterblock.py):
der schmelzende Butterblock, Flocken fliegen nach hinten weg.

  Assets/Art/Icons/fin_butter_beam_new.png       64x64  (32er Raster, 2x) -> weaponImage + weaponIcon
  Assets/Resources/Workbench/butterblast_14.png  14x14  (10x10 Motiv + Kontur) -> Werkbank-Kachel
  Assets/Resources/Workbench/butterblast_10.png  10x10  ( 6x6 Motiv + Kontur)  -> Evo-Chip / Evo-Zeile

Alle .meta bleiben, es aendern sich nur die Pixel. Die kleinen Groessen sind
von Hand gesetzt (wie in Tools/waffen_icons.py).

Aufruf aus dem Projektordner:  python Tools/butterblast_icons.py [--preview pfad.png] [--dry]
"""

import os
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ICON = os.path.join(ROOT, "Assets", "Art", "Icons", "fin_butter_beam_new.png")
WORKBENCH = os.path.join(ROOT, "Assets", "Resources", "Workbench")

WB_OUTLINE = (0x3B, 0x2B, 0x33, 255)
ICON_OUTLINE = (0x4a, 0x28, 0x18, 255)


def hx(s):
    s = s.lstrip("#")
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), 255)


PAL = {
    "W": "#ffffff",
    "H": "#fffbe6",   # Glanz
    "T": "#fff3b8",   # Oberseite
    "t": "#ffeaa0",
    "B": "#ffe080",   # Front
    "b": "#f9d06a",
    "s": "#eeb954",   # Schatten
    "d": "#d69a3e",
    "L": "#ffd25a",   # fluessige Butter
    "l": "#efae3f",
    "E": "#3a1f14",   # Augen
    "M": "#7a2a22",   # Mund
    "R": "#ef7a6a",   # Zunge
    "P": "#ff9f86",   # Baeckchen
}

# 32x32, Flugrichtung rechts. Kontur kommt automatisch dazu.
BIG = [
    "................................",
    "................................",
    "................................",
    "...................LL...........",
    "............LL....LHLL..........",
    "...........LHL.LLLLLLLLLL.......",
    "............LtTTTTTTTTTTTTTt....",
    "...........tHWWWTTTTTTTTTTTTt...",
    "...........THWTTTTTTTTTTTTTTt...",
    "...........HTTTTTTTTTTTTTTTTt...",
    "...........HttttttttttttttttT...",
    "...........HLbbbbbbbbbLbbbbbs...",
    "...........HLHBBBBBBBBLHBBBBs...",
    ".....HH....HLHBBEEBBBBLLBEEBs...",
    ".....HH....HLLBEEEBBBBBBEEEbs...",
    "...........BLLBEWEBBBBBBEWEbs...",
    ".HH........BlLBEEEBBBBBBEEEbs...",
    ".HH........BBlBBEBBMMMBBBEBbs...",
    "...........BBBPPBBMMMMMBPPBbs...",
    "......T....BBBBBBBBMRRMBBBBbs...",
    "...........BBBBBBBBBRRBBBBBbs...",
    "...TT......bBBBBBBBBBBBBBBBbs...",
    "...TT......bbbbbbbbbbbbbbbbss...",
    "...........sssssssssssssssssd...",
    "............sLLLsssssssLLLsd....",
    "............LHLLL.....LHLLL.....",
    "............LLLl......LLLl......",
    ".............LLl.......Ll.......",
    ".............LHl................",
    "..............l.................",
    "................................",
    "................................",
]

S14 = [
    "..HTTTTTTt",
    "..TTTTTTTt",
    "..bLbbbbbs",
    "H.BLEBBEBs",
    "..BlEBBEBs",
    "..BBPMMPBs",
    ".HBBBBBBBs",
    "..ssssssdd",
    "...LL..L..",
    "....l.....",
]

S10 = [
    ".TTTTt",
    ".bLbbs",
    "HBEBEs",
    ".BPMPs",
    ".sssss",
    "..l.l.",
]


def plain(rows):
    """Die Zeichnungen enthalten noch das alte Gesicht - es wird zu Butter."""
    table = str.maketrans({"E": "B", "W": "B", "M": "B", "R": "B", "P": "B"})
    return [r.translate(table) for r in rows]


def with_drips(rows, drips):
    """Tropfnasen ueber die Front: (x, letzte Zeile des Stiels, erste Zeile)."""
    g = [list(r) for r in rows]
    for (x, end, start) in drips:
        for y in range(start, end + 1):
            g[y][x], g[y][x + 1] = "H", "L"
            g[y][x + 2] = "s"
        g[end + 1][x], g[end + 1][x + 1] = "L", "l"
        g[end + 1][x + 2] = "s"
        g[end + 2][x], g[end + 2][x + 1] = "s", "s"
    return ["".join(r) for r in g]


def from_rows(rows, size, off, outline):
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch != ".":
                img.putpixel((x + off, y + off), hx(PAL[ch]))
    src = img.copy()
    for y in range(size):
        for x in range(size):
            if src.getpixel((x, y))[3]:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < size and 0 <= ny < size and src.getpixel((nx, ny))[3]:
                    img.putpixel((x, y), outline)
                    break
    return img


def flakes_without_outline(img, rows, off):
    """Die eckigen Flocken links bleiben ohne Kontur (wie am Geschoss)."""
    flakes = set()
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch in "HT" and x < 9:
                flakes.add((x + off, y + off))
    for (x, y) in flakes:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            q = (x + dx, y + dy)
            if q not in flakes and img.getpixel(q)[3] and img.getpixel(q) in (ICON_OUTLINE, WB_OUTLINE):
                img.putpixel(q, (0, 0, 0, 0))


def main():
    for rows, n in ((BIG, 32), (S14, 10), (S10, 6)):
        assert len(rows) == n and all(len(r) == n for r in rows), n

    rows = with_drips(plain(BIG), [(18, 15, 11), (23, 18, 11)])
    big = from_rows(rows, 32, 0, ICON_OUTLINE)
    flakes_without_outline(big, rows, 0)
    big = big.resize((64, 64), Image.NEAREST)
    small = plain(S14)
    small[3] = small[3][:7] + "L" + small[3][8:]
    small[4] = small[4][:7] + "l" + small[4][8:]
    s14 = from_rows(small, 14, 2, WB_OUTLINE)
    s10 = from_rows(plain(S10), 10, 2, WB_OUTLINE)

    if "--dry" not in sys.argv:
        big.save(ICON)
        s14.save(os.path.join(WORKBENCH, "butterblast_14.png"))
        s10.save(os.path.join(WORKBENCH, "butterblast_10.png"))
        print("geschrieben:", ICON)

    if "--preview" in sys.argv:
        out = sys.argv[sys.argv.index("--preview") + 1]
        sheet = Image.new("RGBA", (64 * 4 + 14 * 10 + 10 * 10 + 40, 64 * 4), (54, 74, 52, 255))
        sheet.alpha_composite(big.resize((256, 256), Image.NEAREST), (0, 0))
        bg = Image.new("RGBA", (14 * 10 + 10 * 10 + 10, 14 * 10), (200, 190, 160, 255))
        bg.alpha_composite(s14.resize((140, 140), Image.NEAREST), (0, 0))
        bg.alpha_composite(s10.resize((100, 100), Image.NEAREST), (150, 0))
        sheet.alpha_composite(bg, (276, 0))
        sheet.save(out)
        print("Vorschau:", out)


if __name__ == "__main__":
    main()
