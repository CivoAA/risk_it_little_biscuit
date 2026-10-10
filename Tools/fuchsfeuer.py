"""
Bilder fuer "Fuchsfeuer" (Kitsunebi) - Inaris Startwaffe (foxfire):

  Assets/Resources/Weapons/foxfire_wisp.png   6 Bilder 16x16 PPU 32  Irrlicht: blaue Flamme mit zwei
                                                                     Ohrzipfeln (ein kleiner Fuchsgeist)
  Assets/Resources/Weapons/foxfire_spark.png  6 Bilder 12x12 PPU 32  Lauffeuer-Funke (kleines Irrlicht)
  Assets/Resources/Weapons/foxfire_trail.png  4 Bilder 12x12 PPU 32  Nachleuchten, 0 = gross ... 3 = klein
  Assets/Resources/Weapons/foxfire_burst_<R>.png  7 Bilder           Aufprall: Flammenring, der aufbluehrt -
                                                  je Ringradius R (Pixel) ein eigener Streifen
  Assets/Resources/Weapons/foxfire_mark.png   4 Bilder 12x14 PPU 32  brennender Gegner: Flaemmchen obendrauf
  Assets/Art/Icons/fin_foxfire.png            64x64 PPU 64           Icon (32er Pixel x2)
  Assets/Resources/Workbench/foxfire_14.png / foxfire_10.png        Werkbank

Die Flammen entstehen aus einem "Hitzefeld": Kreise mit Staerke, das Maximum
entscheidet ueber die Farbstufe (weisser Kern -> hellblau -> blau -> Rand).
Farben passen zu Inaris Halstuch (Tools/char_fuchs.py).

WICHTIG: Die Lauf-Kamera ist pixelgenau (32 PPU, ohne Upscale-Textur) - jede
Skalierung ungleich 1 macht die Pixel ungleich gross und das Bild matschig.
Darum gibt es jede Groesse als eigenes Bild (Funke, Nachleuchten, Ringe),
der Code skaliert nichts. Gerade Kantenlaengen, Pivot Mitte = Pixelkante.
Kein halbdurchsichtiger Schein im Spiel: der wirkt unscharf (nur im Icon).
.meta schreibt das Skript nur beim ersten Lauf.

Aufruf:  python Tools/fuchsfeuer.py [--preview pfad.png] [--dry]
"""

import math
import os
import random
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

# Fuchsfeuer, vom Kern nach aussen
CORE = hx("#ffffff")
C1 = hx("#dcf7ff")
C2 = hx("#8fdcff")
C3 = hx("#4aa6ff")
C4 = hx("#2e6ae0")
RIM = hx("#2a3c9c")
GLOW = hx("#4aa6ff", 90)

# (Schwelle, Farbe) - von heiss nach kalt
STEPS = [(0.80, CORE), (0.62, C1), (0.44, C2), (0.27, C3), (0.13, C4), (0.04, RIM)]


def color_for(h, steps=STEPS):
    for t, c in steps:
        if h >= t:
            return c
    return None


def render(w, h, circles, glow=False):
    """circles: (cx, cy, r, k) - Hitze = k * (1 - d/r), Maximum gewinnt."""
    img = Image.new("RGBA", (w, h), CLEAR)
    px = img.load()
    for y in range(h):
        for x in range(w):
            best = 0.0
            for (cx, cy, r, k) in circles:
                d = math.hypot(x + 0.5 - cx, y + 0.5 - cy)
                if d < r:
                    best = max(best, k * (1 - d / r))
            c = color_for(best)
            if c:
                px[x, y] = c
    if glow:
        # weicher Schein: halbdurchsichtiger Pixelrand um die Flamme
        src = img.copy().load()
        for y in range(h):
            for x in range(w):
                if src[x, y][3]:
                    continue
                if any(0 <= x + a < w and 0 <= y + b < h and src[x + a, y + b][3] == 255
                       for a, b in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                    px[x, y] = GLOW
    return img


def chain(x0, y0, x1, y1, r0, r1, k0, k1, n=6):
    return [(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, r0 + (r1 - r0) * t, k0 + (k1 - k0) * t)
            for t in (i / (n - 1) for i in range(n))]


# ----------------------------------------------------------------------------
#  Irrlicht
# ----------------------------------------------------------------------------

WISP = 16
WISP_FRAMES = 6


def fox_flame(cx, cy, s, phase, sway=1.0):
    """Runder Flammenkoerper mit zwei Ohrzipfeln und einer flackernden Mittelzunge."""
    c = [(cx, cy, 5.2 * s, 1.15)]
    ear = 0.6 * math.sin(phase) * sway
    for side in (-1, 1):
        ex = cx + side * 2.6 * s
        tipx = cx + side * (3.3 * s) + ear * (1 if side > 0 else 0.6)
        c += chain(ex, cy - 2.4 * s, tipx, cy - 8.2 * s + abs(ear) * 0.5, 2.6 * s, 0.75 * s, 0.85, 0.36, 7)
    mid = math.sin(phase * 1.0 + 1.3) * 0.6 * sway
    c += chain(cx, cy - 3.0 * s, cx + mid, cy - 4.8 * s, 1.6 * s, 0.8 * s, 0.6, 0.25, 4)
    return c


def wisp_frame(i):
    ph = i / WISP_FRAMES * 2 * math.pi
    bob = round(math.sin(ph) * 0.6)
    cy = 10.2 + bob * 0.5
    img = render(WISP, WISP, fox_flame(8.0, cy, 1.0, ph))
    px = img.load()
    # Fuchsgeist-Gesicht: zwei schmale, laechelnde Augen
    ey = int(cy)
    for x in (5, 6, 9, 10):
        px[x, ey] = RIM

    # zwei Funken, die nach oben steigen
    for k, (sx, base) in enumerate(((3, 6), (12, 4))):
        y = int(base - ((i + k * 3) % WISP_FRAMES) * 0.8)
        if 0 <= y < WISP and not px[sx, y][3]:
            px[sx, y] = C2 if (i + k) % 2 else C1
    return img


def wisp_strip():
    frames = [wisp_frame(i) for i in range(WISP_FRAMES)]
    sheet = Image.new("RGBA", (WISP * WISP_FRAMES, WISP), CLEAR)
    for i, f in enumerate(frames):
        sheet.alpha_composite(f, (i * WISP, 0))
    return sheet, frames


# ----------------------------------------------------------------------------
#  Aufprall
# ----------------------------------------------------------------------------

BURST_FRAMES = 7

# Ringradien in Pixeln (= Tiles x 32). Der Code nimmt den naechstpassenden,
# skaliert aber nie: 24 Lauffeuer, 40 normal, 54 ab Stufe 5, 70 mit AOE-Buffs.
BURST_RADII = (24, 40, 54, 70)


def burst_size(r_end):
    return 2 * (r_end + 10)


def burst_frame(i, r_end):
    t = i / (BURST_FRAMES - 1)
    size = burst_size(r_end)
    c = size / 2
    s = r_end / 21.0                         # Zungen wachsen etwas mit, Pixel bleiben 1:1
    tz = min(1.5, s)
    circles = []
    # Kernblitz, schnell weg
    if i < 3:
        circles.append((c, c, (12.0 - i * 3.5) * tz, 1.3 - i * 0.25))
    # Flammenring: Zungen zeigen nach aussen und nach oben
    R = r_end * (0.28 + 0.72 * (1 - (1 - t) ** 2))
    heat = 1.0 - 0.75 * t
    circ = 2 * math.pi * R
    # durchgehendes Feuerband, damit der Ring nicht wie eine Perlenkette aussieht
    m = max(12, int(circ / 2.5))
    for k in range(m):
        a = k / m * 2 * math.pi
        circles.append((c + math.cos(a) * R, c + math.sin(a) * R * 0.8, 2.6 * tz * (1 - t * 0.4), heat * 0.62))
    # Flammenzungen: nach aussen und nach oben, abwechselnd hoch und niedrig
    n = max(14, int(circ / 5.5))
    for k in range(n):
        a = k / n * 2 * math.pi + i * 0.12
        x, y = c + math.cos(a) * R, c + math.sin(a) * R * 0.8
        tall = 1.0 if k % 2 == 0 else 0.6
        tongue = (5.0 * (1 - t) + 2.0) * tz * tall
        tx = x + math.cos(a) * tongue * 0.45
        ty = y + math.sin(a) * tongue * 0.35 - tongue
        circles += chain(x, y, tx, ty, 3.6 * tz * (1 - t * 0.5), 1.0, heat * 1.1, heat * 0.4, 5)
    img = render(size, size, circles)
    px = img.load()
    rnd = random.Random(7 + i + r_end)
    for _ in range(int(10 * s * (1 - t)) + 3):
        a = rnd.random() * 2 * math.pi
        d = R + rnd.uniform(2, 7)
        x, y = int(c + math.cos(a) * d), int(c + math.sin(a) * d * 0.8 - t * 4)
        if 0 <= x < size and 0 <= y < size:
            px[x, y] = C1 if rnd.random() < 0.5 else C2
    # zum Ende hin ausduennen: jeder zweite Pixel weg wirkt wie Verpuffen
    if i >= BURST_FRAMES - 2:
        for y in range(size):
            for x in range(size):
                if (x + y + i) % 2 == 0:
                    px[x, y] = CLEAR
    return img


def burst_strip(r_end):
    size = burst_size(r_end)
    frames = [burst_frame(i, r_end) for i in range(BURST_FRAMES)]
    sheet = Image.new("RGBA", (size * BURST_FRAMES, size), CLEAR)
    for i, f in enumerate(frames):
        sheet.alpha_composite(f, (i * size, 0))
    return sheet, frames


# ----------------------------------------------------------------------------
#  Lauffeuer-Funke und Nachleuchten (eigene kleine Bilder statt Skalierung)
# ----------------------------------------------------------------------------

SPARK = 12
TRAIL = 12


def spark_strip():
    frames = []
    for i in range(WISP_FRAMES):
        ph = i / WISP_FRAMES * 2 * math.pi
        img = render(SPARK, SPARK, fox_flame(6.0, 7.6, 0.72, ph))
        px = img.load()
        px[4, 7] = RIM
        px[7, 7] = RIM
        frames.append(img)
    sheet = Image.new("RGBA", (SPARK * len(frames), SPARK), CLEAR)
    for i, f in enumerate(frames):
        sheet.alpha_composite(f, (i * SPARK, 0))
    return sheet, frames


def trail_strip():
    """Runde Flammenbaelle, von gross nach klein - der Code blendet sie zusaetzlich aus."""
    frames = [render(TRAIL, TRAIL, [(6.0, 6.0, r, 0.9)]) for r in (5.2, 4.2, 3.2, 2.2)]
    sheet = Image.new("RGBA", (TRAIL * len(frames), TRAIL), CLEAR)
    for i, f in enumerate(frames):
        sheet.alpha_composite(f, (i * TRAIL, 0))
    return sheet, frames


# ----------------------------------------------------------------------------
#  Brand-Markierung
# ----------------------------------------------------------------------------

MARK_W, MARK_H = 12, 14
MARK_FRAMES = 4


def mark_frame(i):
    ph = i / MARK_FRAMES * 2 * math.pi
    circles = [(6.0, 10.0, 3.6, 1.0)]
    circles += chain(6.0, 8.5, 6.0 + math.sin(ph) * 1.2, 2.5, 2.4, 0.8, 0.8, 0.3, 5)
    circles += chain(4.0, 9.5, 2.6 + math.sin(ph + 2) * 0.6, 5.5, 1.5, 0.6, 0.55, 0.25, 4)
    circles += chain(8.0, 9.5, 9.4 + math.sin(ph + 4) * 0.6, 5.0, 1.5, 0.6, 0.55, 0.25, 4)
    return render(MARK_W, MARK_H, circles)


def mark_strip():
    frames = [mark_frame(i) for i in range(MARK_FRAMES)]
    sheet = Image.new("RGBA", (MARK_W * MARK_FRAMES, MARK_H), CLEAR)
    for i, f in enumerate(frames):
        sheet.alpha_composite(f, (i * MARK_W, 0))
    return sheet, frames


# ----------------------------------------------------------------------------
#  Icon
# ----------------------------------------------------------------------------

def icon():
    """Grosser Fuchsgeist aus Flamme, dahinter zwei kleine Irrlichter."""
    img = Image.new("RGBA", (32, 32), CLEAR)
    for (x, y, s, ph) in ((6, 12, 0.55, 1.0), (26, 9, 0.5, 2.5)):
        img.alpha_composite(render(32, 32, fox_flame(x, y, s, ph)))
    big = render(32, 32, fox_flame(16.0, 20.0, 1.95, 0.7, sway=0.6), glow=True)
    img.alpha_composite(big)
    px = img.load()
    # Fuchsmaske im Kern: zwei schraege Augenschlitze
    for (x, y) in ((11, 20), (12, 20), (12, 21), (13, 21),
                   (20, 20), (19, 20), (19, 21), (18, 21)):
        px[x, y] = RIM
    return outline(img, hx("#1a2466"))


PAL = {"w": CORE, "l": C1, "b": C2, "B": C3, "d": C4}
WB_OUTLINE = (0x3B, 0x2B, 0x33, 255)

SMALL = {
    ("foxfire", 14): [
        "..B......B",
        ".Bb.....bB",
        ".bdB...Bdb",
        ".BbbBBBbbB",
        "BbblllbbbB",
        "BblwwwwlbB",
        "BbwdwwdwbB",
        "BblwwwwlbB",
        ".BbblllbB.",
        "..BBBBBB..",
    ],
    ("foxfire", 10): [
        ".B....B",
        ".bB..Bb",
        "BbbBBbbB",
        "BlwwwwlB",
        "BbwddwbB",
        ".BBBBBB.",
    ],
}


def small_icon(rows, canvas):
    img = Image.new("RGBA", (canvas, canvas), CLEAR)
    off = 2
    for y, row in enumerate(rows):
        row = row.ljust(canvas - 4, ".")
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

    wisp, wisp_frames = wisp_strip()
    spark, spark_frames = spark_strip()
    trail, trail_frames = trail_strip()
    bursts = {r: burst_strip(r) for r in BURST_RADII}
    burst_frames = bursts[40][1]
    mark, mark_frames = mark_strip()
    ic = icon().resize((64, 64), Image.NEAREST)
    small = {key: small_icon(rows, key[1]) for key, rows in SMALL.items()}

    if not dry:
        out = [
            (os.path.join(RES_WEAPONS, "foxfire_wisp.png"), wisp, ("foxfire_wisp", WISP_FRAMES, WISP, WISP, 32)),
            (os.path.join(RES_WEAPONS, "foxfire_spark.png"), spark, ("foxfire_spark", WISP_FRAMES, SPARK, SPARK, 32)),
            (os.path.join(RES_WEAPONS, "foxfire_trail.png"), trail, ("foxfire_trail", 4, TRAIL, TRAIL, 32)),
            (os.path.join(RES_WEAPONS, "foxfire_mark.png"), mark, ("foxfire_mark", MARK_FRAMES, MARK_W, MARK_H, 32)),
            (os.path.join(ICONS, "fin_foxfire.png"), ic, ("fin_foxfire", 1, 64, 64, 64)),
        ]
        for r, (strip, _) in bursts.items():
            base = "foxfire_burst_%d" % r
            out.append((os.path.join(RES_WEAPONS, base + ".png"), strip,
                        (base, BURST_FRAMES, burst_size(r), burst_size(r), 32)))
        for path, img, (base, frames, w, h, ppu) in out:
            img.save(path)
            if not os.path.exists(path + ".meta"):
                write_strip_meta(path + ".meta", base, frames, w, h, ppu)
        template = os.path.join(WORKBENCH, "salad_fan_14.png.meta")
        for (wid, size), img in small.items():
            path = os.path.join(WORKBENCH, f"{wid}_{size}.png")
            img.save(path)
            new_single_meta(path + ".meta", template)
        print("Geschrieben: foxfire_wisp/spark/trail/mark, foxfire_burst_%s, fin_foxfire, Werkbank-Icons"
              % "/".join(str(r) for r in BURST_RADII))

    if preview:
        sheet = Image.new("RGBA", (1000, 760), (40, 44, 58, 255))
        for i, f in enumerate(wisp_frames):
            sheet.alpha_composite(f.resize((WISP * 6, WISP * 6), Image.NEAREST), (10 + i * 104, 10))
        # Ringe 1:1 (Spielgroesse), groesster und kleinster zum Vergleich
        for i, f in enumerate(burst_frames):
            sheet.alpha_composite(f, (10 + i * 104, 110))
        for i, f in enumerate(spark_frames + trail_frames):
            sheet.alpha_composite(f.resize((f.width * 4, f.height * 4), Image.NEAREST), (760 + (i % 5) * 50, 10 + (i // 5) * 50))
        for i, f in enumerate(mark_frames):
            sheet.alpha_composite(f.resize((MARK_W * 6, MARK_H * 6), Image.NEAREST), (10 + i * 80, 230))
        sheet.alpha_composite(ic.resize((256, 256), Image.NEAREST), (10, 330))
        x = 300
        for (wid, size), img in small.items():
            bg = Image.new("RGBA", (size * 10, size * 10), (200, 190, 160, 255))
            bg.alpha_composite(img.resize((size * 10, size * 10), Image.NEAREST))
            sheet.alpha_composite(bg, (x, 330))
            x += size * 10 + 16
        # Spielgroesse x4: Inari mit drei Irrlichtern im Faecher hinter sich
        try:
            import char_fuchs
            fox = char_fuchs.frame(char_fuchs.IDLE[0], True)
            demo = Image.new("RGBA", (80, 56), (92, 120, 84, 255))
            for k, (dx, dy) in enumerate(((-14, -18), (0, -24), (14, -18))):
                demo.alpha_composite(wisp_frames[(k * 2) % WISP_FRAMES], (40 + dx - 8, 44 + dy - 8))
            demo.alpha_composite(fox, (24, 22))
            demo.alpha_composite(mark_frames[0], (64, 30))
            sheet.alpha_composite(demo.resize((80 * 4, 56 * 4), Image.NEAREST), (600, 330))
        except Exception as exc:  # Vorschau ist nur Beiwerk
            print("Demo ohne Fuchs:", exc)
        sheet.save(preview)
        print("Vorschau:", preview)


if __name__ == "__main__":
    main()
