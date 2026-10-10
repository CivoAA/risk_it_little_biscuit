"""
Zeichnet Mochis Startwaffe, den Mochi-Faden.

  Assets/Resources/Weapons/mochi_klecks.png  6 Bilder 16x16 PPU 32  Erdbeer-Mochi-Klecks: fliegt
                                                                  und klebt dann am Gegner (wabbelt
                                                                  ueber die Bilder, Squash & Stretch)
  Assets/Resources/Weapons/mochi_faden.png   6 Bilder 6x6   PPU 32  Faden-Perlen: 0-2 Fuellung
                                                                  (4/3/2 px), 3-5 Rand (6/5/4 px).
                                                                  MochiChain legt Rand-Perlen unter
                                                                  die Fuellung, so entsteht ein
                                                                  Strang mit durchgehender Kontur.
  Assets/Resources/Weapons/mochi_plopp.png   7 Bilder 32x32 PPU 32  Zurueckschnappen: Staubwoelkchen
                                                                  im Ring + zwei Herzchen
  Assets/Art/Icons/fin_mochi_strand.png      64x64 (32er Raster x2)  weaponIcon am Player-Prefab
  Assets/Resources/Workbench/mochi_strand_14.png/_10.png  Werkbank, von Hand gesetzt

Farben aus Tools/char_erdbeere.py (Mochi selbst). .meta schreibt das Skript nur,
wenn noch keine existiert (sonst verliert das Prefab seine Verweise).

Aufruf aus dem Projektordner:  python Tools/mochi_faden.py [--preview pfad.png] [--dry]
"""

import math
import os
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from unity_meta import write_strip_meta  # noqa: E402
from schoko_milch import new_single_meta, outline, hx  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RES_WEAPONS = os.path.join(ROOT, "Assets", "Resources", "Weapons")
ICONS = os.path.join(ROOT, "Assets", "Art", "Icons")
WORKBENCH = os.path.join(ROOT, "Assets", "Resources", "Workbench")

CLEAR = (0, 0, 0, 0)

# Mochi (wie char_erdbeere.py)
OL, OLD = hx("#a14a70"), hx("#7a3356")
HI, M0, M1, M2, M3 = hx("#ffffff"), hx("#fff4f6"), hx("#ffe1e8"), hx("#f7c3d1"), hx("#e9a0b6")
S_R, S_r, S_D, S_H, S_Y = hx("#ff4d6d"), hx("#d42c55"), hx("#9e1b45"), hx("#ff9aad"), hx("#ffeaa0")
G_L, G_G, G_D = hx("#9be37a"), hx("#5dbb5a"), hx("#2f7d4b")
BLUSH = hx("#ffa3bb")

# Licht von oben links (Bildkoordinaten: y nach unten)
LX, LY = -0.62, -0.78


def put(img, x, y, col):
    if 0 <= x < img.width and 0 <= y < img.height:
        img.putpixel((x, y), col)


def blob(img, cx, cy, rx, ry, shade=True):
    """Gefuellte Mochi-Kuppel mit Licht oben links; ohne Kontur."""
    px = img.load()
    for y in range(img.height):
        for x in range(img.width):
            dx, dy = (x + 0.5 - cx) / rx, (y + 0.5 - cy) / ry
            d = dx * dx + dy * dy
            if d > 1.0:
                continue
            if not shade:
                px[x, y] = M1
                continue
            nz = math.sqrt(max(0.0, 1 - d))
            light = dx * LX + dy * LY + nz * 0.5
            if light > 0.95:
                col = M0
            elif light > 0.35:
                col = M1
            elif light > -0.1:
                col = M2
            else:
                col = M3
            px[x, y] = col


def contour(img):
    """Kontur wie beim Mochi: oben OL, im unteren Drittel dunkler (OLD)."""
    src = img.copy()
    sp = src.load()
    w, h = img.size
    top = min((y for y in range(h) for x in range(w) if sp[x, y][3]), default=0)
    bot = max((y for y in range(h) for x in range(w) if sp[x, y][3]), default=h)
    outline(img, OL)
    px = img.load()
    cut = bot - (bot - top) * 0.3
    for y in range(h):
        for x in range(w):
            if px[x, y] == OL and not sp[x, y][3] and y > cut:
                px[x, y] = OLD
    return img


def strawberry(img, x, y):
    """Winzige Erdbeere (3x3 + Blattkrone), linke obere Ecke bei x,y."""
    for (dx, dy, c) in ((0, 1, S_R), (1, 1, S_R), (2, 1, S_r), (0, 2, S_r), (1, 2, S_R), (2, 2, S_D),
                        (1, 3, S_D), (0, 1, S_H), (1, 2, S_Y)):
        put(img, x + dx, y + dy, c)
    put(img, x, y, G_G)
    put(img, x + 1, y, G_L)
    put(img, x + 2, y, G_G)
    put(img, x + 1, y - 1, G_D)


# ----------------------------------------------------------------------------
#  Klecks
# ----------------------------------------------------------------------------

KLECKS = 16
# Squash & Stretch je Bild: (Breite, Hoehe) der Kuppel in Pixeln (Halbachsen)
KLECKS_SHAPE = [(6.0, 4.6), (6.6, 4.1), (6.9, 3.8), (6.4, 4.3), (5.6, 5.1), (5.3, 5.4)]


def klecks_frame(rx, ry):
    img = Image.new("RGBA", (KLECKS, KLECKS), CLEAR)
    # Boden fest bei y = 12.5, die Kuppel waechst nach oben
    floor = 12.5
    cx, cy = 8.0, floor - ry
    blob(img, cx, cy, rx, ry)
    # Puderzucker-Staub obendrauf
    for (dx, dy) in ((-3, -0.6), (2, -0.4), (-1, 0.5)):
        put(img, int(cx + dx * rx / 6), int(cy - ry * 0.55 + dy), HI)
    # kleine Klebe-Nase unten (laeuft am Gegner runter)
    put(img, int(cx + rx * 0.5), int(floor), M2)
    contour(img)
    # Erdbeere guckt oben raus
    strawberry(img, int(cx) - 1, int(cy - ry) - 2)
    return img


def klecks_strip():
    frames = [klecks_frame(rx, ry) for rx, ry in KLECKS_SHAPE]
    strip = Image.new("RGBA", (KLECKS * len(frames), KLECKS), CLEAR)
    for i, f in enumerate(frames):
        strip.paste(f, (i * KLECKS, 0))
    return strip, frames


# ----------------------------------------------------------------------------
#  Faden-Perlen
# ----------------------------------------------------------------------------

BEAD = 6
FILL_D = [4, 3, 2]
RIM_D = [6, 5, 4]


def disc(d, col, top=None):
    img = Image.new("RGBA", (BEAD, BEAD), CLEAR)
    off = (BEAD - d) // 2
    c = (d - 1) / 2.0
    r2 = (d / 2.0) ** 2 + 0.25
    for y in range(d):
        for x in range(d):
            if (x - c) ** 2 + (y - c) ** 2 <= r2:
                img.putpixel((x + off, y + off), col)
    if top is not None and d >= 3:
        for x in range(d):
            if img.getpixel((x + off, off))[3]:
                img.putpixel((x + off, off), top)
    return img


def bead_strip():
    frames = [disc(d, M1, top=M0) for d in FILL_D] + [disc(d, OL) for d in RIM_D]
    strip = Image.new("RGBA", (BEAD * len(frames), BEAD), CLEAR)
    for i, f in enumerate(frames):
        strip.paste(f, (i * BEAD, 0))
    return strip, frames


# ----------------------------------------------------------------------------
#  Plopp
# ----------------------------------------------------------------------------

PLOPP = 32
PLOPP_FRAMES = 7
PUFFS = 8
# Ringradius, Woelkchen-Radius je Bild
PUFF_R = [2.0, 5.0, 8.0, 10.5, 12.0, 13.0, 13.5]
PUFF_S = [3.2, 3.0, 2.6, 2.2, 1.7, 1.2, 0.8]


def heart(img, x, y, col, rim):
    rows = [".X.X.", "XXXXX", ".XXX.", "..X.."]
    for dy, row in enumerate(rows):
        for dx, ch in enumerate(row):
            if ch == "X":
                put(img, x + dx, y + dy, col)
    put(img, x + 1, y, HI)


def plopp_frame(i):
    img = Image.new("RGBA", (PLOPP, PLOPP), CLEAR)
    c = PLOPP / 2.0
    if i <= 1:
        # Blitz in der Mitte: kleiner Stern
        r = 5 - i * 2
        for k in range(-r, r + 1):
            put(img, int(c) + k, int(c), M0)
            put(img, int(c), int(c) + k, M0)
        for k in range(-r // 2, r // 2 + 1):
            put(img, int(c) + k, int(c) + k, HI)
            put(img, int(c) + k, int(c) - k, HI)
    pr, ps = PUFF_R[i], PUFF_S[i]
    for k in range(PUFFS):
        a = 2 * math.pi * k / PUFFS + 0.2
        px_, py_ = c + math.cos(a) * pr, c + math.sin(a) * pr * 0.8
        if ps >= 1.0:
            blob(img, px_, py_, ps, ps)
        else:
            put(img, int(px_), int(py_), M1)
    if i >= 1:
        outline(img, M3)
    if 2 <= i <= 6:
        rise = (i - 2) * 2
        heart(img, 5, 9 - rise, S_R if i < 5 else BLUSH, OL)
        heart(img, 22, 11 - rise, BLUSH if i < 5 else M2, OL)
    return img


def plopp_strip():
    frames = [plopp_frame(i) for i in range(PLOPP_FRAMES)]
    strip = Image.new("RGBA", (PLOPP * PLOPP_FRAMES, PLOPP), CLEAR)
    for i, f in enumerate(frames):
        strip.paste(f, (i * PLOPP, 0))
    return strip, frames


# ----------------------------------------------------------------------------
#  Icon
# ----------------------------------------------------------------------------

def strand(img, p0, p1, sag, thick):
    """Durchhaengender Faden von p0 nach p1 (Pixel), Dicke wird zur Mitte duenner."""
    n = int(math.hypot(p1[0] - p0[0], p1[1] - p0[1]) * 2) + 2
    for s in range(n + 1):
        t = s / n
        x = p0[0] + (p1[0] - p0[0]) * t
        y = p0[1] + (p1[1] - p0[1]) * t + sag * 4 * t * (1 - t)
        r = thick * (1 - 0.45 * math.sin(math.pi * t))
        for yy in range(int(y - r - 1), int(y + r + 2)):
            for xx in range(int(x - r - 1), int(x + r + 2)):
                if (xx + 0.5 - x) ** 2 + (yy + 0.5 - y) ** 2 <= r * r:
                    put(img, xx, yy, M1)
    # Glanzlinie oben am Faden
    for s in range(2, n - 1, 2):
        t = s / n
        x = p0[0] + (p1[0] - p0[0]) * t
        y = p0[1] + (p1[1] - p0[1]) * t + sag * 4 * t * (1 - t)
        r = thick * (1 - 0.45 * math.sin(math.pi * t))
        if r >= 1.2:
            put(img, int(x), int(y - r + 0.6), M0)


def icon():
    img = Image.new("RGBA", (32, 32), CLEAR)
    # Faeden zuerst, dann die Kleckse drueber
    strand(img, (10, 21), (24, 9), 3.0, 1.6)
    strand(img, (24, 9), (26, 25), 2.0, 1.2)
    blob(img, 24.5, 9.5, 4.2, 3.6)
    blob(img, 26.5, 25.5, 3.4, 3.0)
    blob(img, 10.0, 21.5, 7.6, 6.2)
    for (x, y) in ((6, 18), (9, 17), (13, 18)):
        put(img, x, y, HI)
    img = contour(img)
    strawberry(img, 8, 13)
    strawberry(img, 23, 4)
    return img


# ----------------------------------------------------------------------------
#  Werkbank
# ----------------------------------------------------------------------------

PAL = {"m": M1, "M": M0, "s": M2, "R": S_R, "r": S_D, "g": G_G}
WB_OUTLINE = (0x3B, 0x2B, 0x33, 255)

SMALL = {
    ("mochi_strand", 14): [
        "........g.",
        ".......Rr.",
        "......mMm.",
        ".....m.mm.",
        "....m...m.",
        ".g.m....m.",
        "Rrm.....m.",
        "MMmm...mm.",
        "mmms...ms.",
        ".ss.......",
    ],
    ("mochi_strand", 10): [
        "....Rr",
        "...mMm",
        ".g.m.m",
        "Rrm..m",
        "Mmm.ms",
        ".ss...",
    ],
}


def small_icon(rows, canvas):
    img = Image.new("RGBA", (canvas, canvas), CLEAR)
    off = 2
    for y, row in enumerate(rows):
        assert len(row) == canvas - 4, (row, canvas)
        for x, ch in enumerate(row):
            if ch != ".":
                img.putpixel((x + off, y + off), PAL[ch])
    return outline(img, WB_OUTLINE)


# ----------------------------------------------------------------------------
#  Schreiben
# ----------------------------------------------------------------------------

def main():
    dry = "--dry" in sys.argv
    preview = sys.argv[sys.argv.index("--preview") + 1] if "--preview" in sys.argv else None

    klecks, klecks_frames = klecks_strip()
    beads, bead_frames = bead_strip()
    plopp, plopp_frames = plopp_strip()
    i_strand = icon().resize((64, 64), Image.NEAREST)
    small = {key: small_icon(rows, key[1]) for key, rows in SMALL.items()}

    if not dry:
        os.makedirs(RES_WEAPONS, exist_ok=True)
        out = [
            (os.path.join(RES_WEAPONS, "mochi_klecks.png"), klecks, ("mochi_klecks", len(klecks_frames), KLECKS, KLECKS, 32)),
            (os.path.join(RES_WEAPONS, "mochi_faden.png"), beads, ("mochi_faden", len(bead_frames), BEAD, BEAD, 32)),
            (os.path.join(RES_WEAPONS, "mochi_plopp.png"), plopp, ("mochi_plopp", PLOPP_FRAMES, PLOPP, PLOPP, 32)),
            (os.path.join(ICONS, "fin_mochi_strand.png"), i_strand, ("fin_mochi_strand", 1, 64, 64, 64)),
        ]
        for path, img, (base, frames, w, h, ppu) in out:
            img.save(path)
            if not os.path.exists(path + ".meta"):
                write_strip_meta(path + ".meta", base, frames, w, h, ppu)
        template = os.path.join(WORKBENCH, "salad_fan_14.png.meta")
        for (wid, size), img in small.items():
            path = os.path.join(WORKBENCH, f"{wid}_{size}.png")
            img.save(path)
            new_single_meta(path + ".meta", template)
        print("Geschrieben: mochi_klecks, mochi_faden, mochi_plopp, fin_mochi_strand, Werkbank-Icons")

    if preview:
        sheet = Image.new("RGBA", (1100, 700), (58, 52, 64, 255))
        for i, f in enumerate(klecks_frames):
            sheet.alpha_composite(f.resize((KLECKS * 6, KLECKS * 6), Image.NEAREST), (10 + i * 104, 10))
        for i, f in enumerate(bead_frames):
            sheet.alpha_composite(f.resize((BEAD * 8, BEAD * 8), Image.NEAREST), (10 + i * 56, 120))
        for i, f in enumerate(plopp_frames):
            sheet.alpha_composite(f.resize((PLOPP * 4, PLOPP * 4), Image.NEAREST), (10 + i * 134, 180))
        sheet.alpha_composite(i_strand.resize((256, 256), Image.NEAREST), (10, 320))
        # Probe-Kette in Spielgroesse x4: drei Kleckse mit Perlen-Faden
        demo = Image.new("RGBA", (140, 70), (92, 120, 84, 255))
        pts = [(16, 50), (70, 22), (122, 46)]
        for a, b in zip(pts, pts[1:]):
            for layer in (1, 0):
                n = int(math.hypot(b[0] - a[0], b[1] - a[1]) / 2)
                for s in range(n + 1):
                    t = s / n
                    x = a[0] + (b[0] - a[0]) * t
                    y = a[1] + (b[1] - a[1]) * t + 6 * 4 * t * (1 - t)
                    k = 0 if t < 0.2 or t > 0.8 else (1 if t < 0.35 or t > 0.65 else 2)
                    f = bead_frames[k + 3 * layer]
                    demo.alpha_composite(f, (int(x) - 3, int(y) - 3))
        for p in pts:
            demo.alpha_composite(klecks_frames[0], (p[0] - 8, p[1] - 12))
        sheet.alpha_composite(demo.resize((140 * 4, 70 * 4), Image.NEAREST), (300, 320))
        x = 300
        for (wid, size), img in small.items():
            bg = Image.new("RGBA", (size * 10, size * 10), (200, 190, 160, 255))
            bg.alpha_composite(img.resize((size * 10, size * 10), Image.NEAREST))
            sheet.alpha_composite(bg, (x, 600 - size * 10 + 90))
            x += size * 10 + 16
        sheet.save(preview)
        print("Vorschau:", preview)


if __name__ == "__main__":
    main()
