"""
Zeichnet die zwei neuen Keks-Waffen: Schoko-Salve und Milch-Tunker.

  Assets/Resources/Weapons/choco_chip.png      8 Bilder 12x12  PPU 32  fliegendes Schokostueck
                                                                     (dreht sich ueber die Bilder)
  Assets/Resources/Weapons/milk_wave.png       10 Bilder 128x128 PPU 32  Milchring, der sich
                                                                     ausbreitet (Bild 0 = Spritzer
                                                                     am Keks, Bild 9 = verlaufen)
  Assets/Art/Icons/fin_choco_chips.png         64x64 (32er Raster x2)  weaponIcon am Player-Prefab
  Assets/Art/Icons/fin_milk_dunk.png           64x64
  Assets/Resources/Workbench/<id>_14.png/_10.png  Werkbank, von Hand gesetzt

Die Waffen-Skripte (ChocoChips.cs, MilkDunk.cs) laden die Streifen per
Resources.LoadAll und haben einen Rueckfall, falls sie fehlen. Der Milchring
waechst NICHT gleichmaessig: Radius je Bild = RING_R[i] (Pixel). Dieselbe
Kurve steht als MilkDunk.RingCurve im Code - wer sie hier aendert, aendert
sie dort mit, sonst passt der Schaden nicht mehr zum Bild.

.meta schreibt das Skript nur, wenn noch keine existiert (sonst verliert das
Prefab seine Verweise auf die Icons).

Aufruf aus dem Projektordner:  python Tools/schoko_milch.py [--preview pfad.png] [--dry]
"""

import math
import os
import random
import shutil
import sys
import uuid

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from unity_meta import write_strip_meta  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RES_WEAPONS = os.path.join(ROOT, "Assets", "Resources", "Weapons")
ICONS = os.path.join(ROOT, "Assets", "Art", "Icons")
WORKBENCH = os.path.join(ROOT, "Assets", "Resources", "Workbench")

CLEAR = (0, 0, 0, 0)


def hx(s, a=255):
    s = s.lstrip("#")
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), a)


LINE = hx("#2a1712")
K_EDGE, K_DARK, K_MID, K_LIGHT, K_HI = hx("#8a4f22"), hx("#b8702f"), hx("#d99a4e"), hx("#efbd72"), hx("#fbdc9c")
CHOC_D, CHOC, CHOC_M, CHOC_L, CHOC_HI = hx("#2e160c"), hx("#4a2616"), hx("#5f321d"), hx("#7a4428"), hx("#a8684a")
# Milch: warmes Weiss, Schatten leicht blaeulich
MILK_HI, MILK, MILK_M, MILK_D = hx("#ffffff"), hx("#fbf6ec"), hx("#e3e4e8"), hx("#b9c3d3")
MILK_LINE = hx("#6f7f99")

# Licht von oben links (Bildkoordinaten: y nach unten)
LX, LY = -0.62, -0.78


def outline(img, col, diagonal=False):
    src = img.copy()
    w, h = img.size
    sp = src.load()
    dp = img.load()
    n = [(1, 0), (-1, 0), (0, 1), (0, -1)]
    if diagonal:
        n += [(1, 1), (1, -1), (-1, 1), (-1, -1)]
    for y in range(h):
        for x in range(w):
            if sp[x, y][3]:
                continue
            for dx, dy in n:
                nx, ny = x + dx, y + dy
                if 0 <= nx < w and 0 <= ny < h and sp[nx, ny][3] > 128:
                    dp[x, y] = col
                    break
    return img


# ----------------------------------------------------------------------------
#  Schokostueck
# ----------------------------------------------------------------------------

CHIP_SIZE = 12
CHIP_FRAMES = 8

# Umriss in Polarform: Radius je Winkel, absichtlich unrund (gebrochenes Stueck)
CHIP_LOBES = [(0.0, 4.3), (0.9, 3.6), (1.7, 4.1), (2.5, 3.3), (3.3, 4.2), (4.2, 3.5), (5.1, 3.9)]


def chip_radius(a):
    a %= 2 * math.pi
    pts = CHIP_LOBES + [(CHIP_LOBES[0][0] + 2 * math.pi, CHIP_LOBES[0][1])]
    for (a0, r0), (a1, r1) in zip(pts, pts[1:]):
        if a0 <= a <= a1:
            t = (a - a0) / (a1 - a0)
            t = t * t * (3 - 2 * t)
            return r0 + (r1 - r0) * t
    return CHIP_LOBES[0][1]


def chip_frame(rot):
    img = Image.new("RGBA", (CHIP_SIZE, CHIP_SIZE), CLEAR)
    px = img.load()
    c = (CHIP_SIZE - 1) / 2.0
    for y in range(CHIP_SIZE):
        for x in range(CHIP_SIZE):
            dx, dy = x - c, y - c
            d = math.hypot(dx, dy)
            a = math.atan2(dy, dx) - rot
            r = chip_radius(a)
            if d > r:
                continue
            # Kuppel: Normale aus Abstand zur Mitte, Licht bleibt fest oben links
            nz = math.sqrt(max(0.0, 1 - (d / (r + 0.6)) ** 2))
            nx_, ny_ = (dx / (r + 0.6), dy / (r + 0.6))
            light = nx_ * LX + ny_ * LY + nz * 0.55
            if light > 0.75:
                col = CHOC_HI
            elif light > 0.45:
                col = CHOC_L
            elif light > 0.15:
                col = CHOC_M
            elif light > -0.2:
                col = CHOC
            else:
                col = CHOC_D
            px[x, y] = col
    return outline(img, LINE)


def chip_strip():
    frames = [chip_frame(2 * math.pi * i / CHIP_FRAMES) for i in range(CHIP_FRAMES)]
    strip = Image.new("RGBA", (CHIP_SIZE * CHIP_FRAMES, CHIP_SIZE), CLEAR)
    for i, f in enumerate(frames):
        strip.paste(f, (i * CHIP_SIZE, 0))
    return strip, frames


# ----------------------------------------------------------------------------
#  Milchring
# ----------------------------------------------------------------------------

WAVE_SIZE = 128
# Radius (Pixel) und Ringdicke je Bild. MilkDunk.RingCurve = RING_R / RING_R[-1].
RING_R = [7, 17, 27, 36, 44, 50, 55, 58, 60, 61]
RING_W = [6, 7, 7, 6, 6, 5, 4, 4, 3, 3]
# Deckkraft der Milchflaeche innen und des Rings
FILL_A = [170, 120, 90, 70, 55, 42, 30, 20, 10, 0]
RING_A = [255, 255, 255, 255, 250, 235, 210, 170, 120, 70]

DROPS = 14


def wave_frame(i, rng_seed=7):
    n = WAVE_SIZE
    img = Image.new("RGBA", (n, n), CLEAR)
    px = img.load()
    c = (n - 1) / 2.0
    r = RING_R[i]
    w = RING_W[i]
    t = i / (len(RING_R) - 1)

    # Wellige Aussenkante: ein paar Spitzen wie bei einer Milchkrone
    def edge(a):
        return r + 1.3 * math.sin(a * 9 + 0.7) + 0.8 * math.sin(a * 5 - 1.1)

    for y in range(n):
        for x in range(n):
            dx, dy = x - c, y - c
            d = math.hypot(dx, dy)
            a = math.atan2(dy, dx)
            e = edge(a)
            if d > e:
                continue
            if d >= e - w:
                # Ring: oben links hell, unten rechts Schatten
                k = (dx * LX + dy * LY) / max(d, 0.001)
                depth = (e - d) / max(w, 1)
                if depth < 0.34 and k > 0.15:
                    col = MILK_HI
                elif depth > 0.7 or k < -0.55:
                    col = MILK_D if k < -0.3 else MILK_M
                else:
                    col = MILK
                px[x, y] = (col[0], col[1], col[2], RING_A[i])
            elif FILL_A[i] > 0:
                # Innen: duenne Milchflaeche, zur Mitte hin klarer
                f = (d / max(e - w, 1)) ** 1.5
                a_ = int(FILL_A[i] * (0.25 + 0.75 * f))
                col = MILK_HI if f > 0.6 else MILK
                px[x, y] = (col[0], col[1], col[2], a_)

    # Kontur nur aussen am Ring (blaugrau, leicht durchsichtig zum Ende hin)
    if i < len(RING_R) - 2:
        src = img.copy()
        sp = src.load()
        la = int(255 * (1 - t * 0.8))
        for y in range(n):
            for x in range(n):
                if sp[x, y][3]:
                    continue
                d = math.hypot(x - c, y - c)
                if d < r - 2:
                    continue
                for ddx, ddy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    q = sp[min(n - 1, max(0, x + ddx)), min(n - 1, max(0, y + ddy))]
                    if q[3] > 100:
                        px[x, y] = (MILK_LINE[0], MILK_LINE[1], MILK_LINE[2], la)
                        break

    # Tropfen: fliegen ueber den Ring hinaus und werden kleiner
    rng = random.Random(rng_seed)
    if 1 <= i <= 8:
        for k in range(DROPS):
            a = 2 * math.pi * k / DROPS + rng.uniform(-0.15, 0.15)
            speed = rng.uniform(1.0, 1.25)
            dist = r * speed + 3 + i * 0.8
            size = 2 if (i < 5 and k % 3 != 0) else 1
            if i >= 7 and k % 2:
                continue
            x0 = int(round(c + math.cos(a) * dist))
            y0 = int(round(c + math.sin(a) * dist))
            for yy in range(y0, y0 + size):
                for xx in range(x0, x0 + size):
                    if 0 <= xx < n and 0 <= yy < n:
                        px[xx, yy] = MILK_HI if (xx == x0 and yy == y0) else MILK_M
            # Kontur um den Tropfen
            for yy in range(y0 - 1, y0 + size + 1):
                for xx in range(x0 - 1, x0 + size + 1):
                    if 0 <= xx < n and 0 <= yy < n and px[xx, yy][3] == 0:
                        px[xx, yy] = (MILK_LINE[0], MILK_LINE[1], MILK_LINE[2], 200)
    return img


def wave_strip():
    frames = [wave_frame(i) for i in range(len(RING_R))]
    strip = Image.new("RGBA", (WAVE_SIZE * len(frames), WAVE_SIZE), CLEAR)
    for i, f in enumerate(frames):
        strip.paste(f, (i * WAVE_SIZE, 0))
    return strip, frames


# ----------------------------------------------------------------------------
#  Icons (32er Raster, dann x2)
# ----------------------------------------------------------------------------

def cookie_disc(img, cx, cy, r, chips=()):
    """Runder Keks mit Licht oben links, Schokostueckchen an chips (relativ)."""
    px = img.load()
    w, h = img.size
    for y in range(h):
        for x in range(w):
            dx, dy = x - cx, y - cy
            d = math.hypot(dx, dy)
            if d > r:
                continue
            k = (dx * LX + dy * LY) / max(r, 1)
            if d > r - 1.2:
                col = K_EDGE if k < 0.2 else K_DARK
            elif k > 0.45:
                col = K_HI
            elif k > 0.1:
                col = K_LIGHT
            elif k > -0.35:
                col = K_MID
            else:
                col = K_DARK
            px[x, y] = col
    for (ox, oy) in chips:
        x0, y0 = int(cx + ox), int(cy + oy)
        for (ddx, ddy, col) in ((0, 0, CHOC_L), (1, 0, CHOC), (0, 1, CHOC), (1, 1, CHOC_D)):
            if 0 <= x0 + ddx < w and 0 <= y0 + ddy < h:
                px[x0 + ddx, y0 + ddy] = col


def paste_chip(img, frame, x, y):
    img.alpha_composite(frame, (x, y))


def icon_choco_chips():
    img = Image.new("RGBA", (32, 32), CLEAR)
    # Keks unten links, angeschnitten
    cookie_disc(img, 9, 23, 8.5, chips=((-3, -2), (2, 1), (-1, 3)))
    # Kruemel-/Bewegungsstriche
    px = img.load()
    for (x0, y0, x1, y1) in ((15, 15, 18, 12), (13, 11, 15, 9), (19, 20, 22, 18)):
        steps = max(abs(x1 - x0), abs(y1 - y0))
        for s in range(steps + 1):
            x = round(x0 + (x1 - x0) * s / steps)
            y = round(y0 + (y1 - y0) * s / steps)
            px[x, y] = hx("#f3e2c6", 200 if s else 120)
    img = outline(img, LINE)
    # drei fliegende Stuecke nach oben rechts, groesser werdend
    frames = [chip_frame(a) for a in (0.4, 1.9, 3.6)]
    small = frames[0].resize((9, 9), Image.NEAREST)
    paste_chip(img, small, 13, 4)
    paste_chip(img, frames[1], 19, 9)
    paste_chip(img, frames[2], 20, -1)
    return img


def icon_milk_dunk():
    img = Image.new("RGBA", (32, 32), CLEAR)
    px = img.load()
    # Milchflaeche (Ellipse) unten
    cx, cy = 15.5, 22.0
    for y in range(32):
        for x in range(32):
            ex = (x - cx) / 14.0
            ey = (y - cy) / 6.0
            if ex * ex + ey * ey <= 1:
                px[x, y] = MILK_M if ey > 0.35 else MILK
    # Keks steckt halb drin
    cookie = Image.new("RGBA", (32, 32), CLEAR)
    cookie_disc(cookie, 15.5, 15.0, 8.0, chips=((-4, -3), (1, -5), (2, 0), (-3, 2)))
    cp = cookie.load()
    for y in range(32):
        for x in range(32):
            if cp[x, y][3] and y < 20:
                px[x, y] = cp[x, y]
            elif cp[x, y][3] and y >= 20:
                # eingetauchter Teil: etwas dunkler durch die Milch gesehen
                ex = (x - cx) / 14.0
                ey = (y - cy) / 6.0
                if ex * ex + ey * ey > 1:
                    continue
    # Milchkrone um den Keks: Zacken links und rechts
    for (x, top) in ((4, 15), (5, 17), (6, 14), (7, 18), (24, 18), (25, 14), (26, 17), (27, 15)):
        for y in range(top, 21):
            px[x, y] = MILK_HI if y == top else MILK
    # Tropfen
    for (x, y) in ((3, 10), (8, 8), (23, 8), (28, 11), (6, 5), (26, 5)):
        px[x, y] = MILK_HI
        px[x, y + 1] = MILK
    # Milchrand vorne ueber dem Keks
    for x in range(7, 25):
        px[x, 20] = MILK_HI
    img = outline(img, MILK_LINE)
    # Keks bekommt seine dunkle Kontur oben
    for y in range(32):
        for x in range(32):
            if px[x, y] == MILK_LINE:
                for ddx, ddy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + ddx, y + ddy
                    if 0 <= nx < 32 and 0 <= ny < 32 and px[nx, ny] in (K_EDGE, K_DARK, K_MID, K_LIGHT, K_HI):
                        px[x, y] = LINE
                        break
    return img


# Werkbank, von Hand. Kontur ergaenzt small_icon.
PAL = {
    "C": CHOC_D, "c": CHOC, "h": CHOC_L, "k": K_MID, "K": K_LIGHT, "e": K_DARK,
    "m": MILK, "M": MILK_HI, "d": MILK_M,
}
WB_OUTLINE = (0x3B, 0x2B, 0x33, 255)

SMALL = {
    ("choco_chips", 14): [
        "......hc..",
        "......cC..",
        "...hc.....",
        "...cC..hc.",
        ".......cC.",
        ".Kk.......",
        "KkKk......",
        "kckk......",
        "kkce......",
        ".ee.......",
    ],
    ("choco_chips", 10): [
        "....hc",
        "....cC",
        ".hc...",
        ".cC...",
        "Kk....",
        "ce....",
    ],
    ("milk_dunk", 14): [
        "M........M",
        "...KKk....",
        "..KkckK...",
        ".M.kkce.M.",
        ".m.ekke.m.",
        "mm.....mmm",
        "mMMMMMMMMm",
        "mmmmmmmmmm",
        ".dmmmmmmd.",
        "..dddddd..",
    ],
    ("milk_dunk", 10): [
        "..Kkk.",
        ".Kcke.",
        "m.eke.",
        "mMMMMm",
        "mmmmmm",
        ".dddd.",
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

def new_single_meta(path, template):
    """Werkbank-.meta: Vorlage (Single-Sprite) kopieren, guid neu."""
    if os.path.exists(path):
        return
    with open(template, "r", newline="") as fh:
        text = fh.read()
    lines = text.split("\n")
    for k, ln in enumerate(lines):
        if ln.startswith("guid: "):
            lines[k] = "guid: " + uuid.uuid4().hex + ("\r" if ln.endswith("\r") else "")
        if "spriteID: " in ln and "spriteID: \r" not in ln and ln.strip() != "spriteID:":
            pre = ln[: ln.index("spriteID: ") + len("spriteID: ")]
            lines[k] = pre + uuid.uuid4().hex + ("\r" if ln.endswith("\r") else "")
    with open(path, "w", newline="") as fh:
        fh.write("\n".join(lines))


def main():
    dry = "--dry" in sys.argv
    preview = sys.argv[sys.argv.index("--preview") + 1] if "--preview" in sys.argv else None

    chips, chip_frames = chip_strip()
    wave, wave_frames = wave_strip()
    i_chips = icon_choco_chips().resize((64, 64), Image.NEAREST)
    i_milk = icon_milk_dunk().resize((64, 64), Image.NEAREST)
    small = {key: small_icon(rows, key[1]) for key, rows in SMALL.items()}

    if not dry:
        os.makedirs(RES_WEAPONS, exist_ok=True)
        out = [
            (os.path.join(RES_WEAPONS, "choco_chip.png"), chips, ("choco_chip", CHIP_FRAMES, CHIP_SIZE, CHIP_SIZE, 32)),
            (os.path.join(RES_WEAPONS, "milk_wave.png"), wave, ("milk_wave", len(wave_frames), WAVE_SIZE, WAVE_SIZE, 32)),
            (os.path.join(ICONS, "fin_choco_chips.png"), i_chips, ("fin_choco_chips", 1, 64, 64, 64)),
            (os.path.join(ICONS, "fin_milk_dunk.png"), i_milk, ("fin_milk_dunk", 1, 64, 64, 64)),
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
        print("Geschrieben: choco_chip, milk_wave, fin_choco_chips, fin_milk_dunk, Werkbank-Icons")

    if preview:
        sc = 4
        sheet = Image.new("RGBA", (1400, 900), (58, 52, 64, 255))
        # Schokostuecke
        for i, f in enumerate(chip_frames):
            sheet.alpha_composite(f.resize((CHIP_SIZE * 6, CHIP_SIZE * 6), Image.NEAREST), (10 + i * 80, 10))
        # Milchwelle
        for i, f in enumerate(wave_frames):
            sheet.alpha_composite(f.resize((WAVE_SIZE * 2 // 2, WAVE_SIZE * 2 // 2), Image.NEAREST),
                                  (10 + (i % 5) * 136, 100 + (i // 5) * 136))
        sheet.alpha_composite(i_chips.resize((64 * sc, 64 * sc), Image.NEAREST), (720, 100))
        sheet.alpha_composite(i_milk.resize((64 * sc, 64 * sc), Image.NEAREST), (720 + 64 * sc + 20, 100))
        x = 720
        for (wid, size), img in small.items():
            bg = Image.new("RGBA", (size * 10, size * 10), (200, 190, 160, 255))
            bg.alpha_composite(img.resize((size * 10, size * 10), Image.NEAREST))
            sheet.alpha_composite(bg, (x, 400))
            x += size * 10 + 16
        sheet.save(preview)
        print("Vorschau:", preview)


if __name__ == "__main__":
    main()
