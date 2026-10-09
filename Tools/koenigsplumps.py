"""
Bilder fuer den Koenigsplumps (royal_splat) - die Startwaffe des
Schleimkoenigs (Charakter 7). Ein Klops Traubengelee mit einer ganzen
Weintraube darin fliegt im hohen Bogen auf die dichteste Gegnergruppe,
klatscht auf und huepft von Gruppe zu Gruppe weiter.

Farben = Traubengelee aus Tools/char_schleimkoenig.py.

  Assets/Resources/Weapons/
    splat_blob.png          4 Bilder 28x28, Pivot unten: rund, gestreckt, gequetscht, wabbelnd
    splat_mini.png          3 Bilder 16x16, Pivot unten (Spaltklopse der letzten Stufe)
    splat_shadow.png        4 Bilder 24x8, gross (am Boden) ... klein (hoch oben)
    splat_mark_<r>.png      2 Bilder: Landepunkt-Ring, genau so gross wie der Treffer
    splat_hit_<r>.png       8 Bilder: Aufschlag - Platscher, Ring, Spritzer
    splat_puddle_<r>.png    4 Bilder: klebrige Pfuetze (Stufe 4+), Glanz wandert
  Assets/Art/Icons/fin_royal_splat.png             64x64 PPU 64 (32er Pixel x2)
  Assets/Resources/Workbench/royal_splat_14/_10    Werkbank

<r> = Radius in Pixeln (32 = 1 Tile). Die Lauf-Kamera ist pixelgenau, es
wird nie skaliert - RoyalSplat nimmt den naechstpassenden Ring.
.meta nur beim ersten Lauf.

Aufruf:  python Tools/koenigsplumps.py [--preview pfad.png] [--dry]
"""

import math
import os
import random
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from unity_meta import write_strip_meta  # noqa: E402
from schoko_milch import new_single_meta, outline  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RES_WEAPONS = os.path.join(ROOT, "Assets", "Resources", "Weapons")
ICONS = os.path.join(ROOT, "Assets", "Art", "Icons")
WORKBENCH = os.path.join(ROOT, "Assets", "Resources", "Workbench")

# Treffer-Radien in Pixeln; RoyalSplat.HitRadii muss gleich sein
HIT_RADII = [32, 40, 48, 60, 72]
PUDDLE_RADII = [28, 36, 46]

CLEAR = (0, 0, 0, 0)


def hx(s, a=255):
    s = s.lstrip('#')
    return tuple(int(s[i:i + 2], 16) for i in (0, 2, 4)) + (a,)


OL = hx('#4a2378')
OLD = hx('#33155a')
S0 = hx('#f3e2ff')
S1 = hx('#cfa2f5')
S2 = hx('#ad78e6')
S3 = hx('#8d58cf')
S4 = hx('#7240b4')
SR = hx('#c493f2')
BU = hx('#e2c4ff')
V0 = hx('#d9c9ff')
V1 = hx('#7d62e8')
V2 = hx('#5640c0')
VO = hx('#2a1a6a')
LF = hx('#6cc04a')
LFD = hx('#3c7f2a')
G1 = hx('#ffd23f')
G2 = hx('#efa52a')
GO = hx('#6e3b12')

N4 = ((1, 0), (-1, 0), (0, 1), (0, -1))


def with_alpha(c, a):
    return c[:3] + (a,)


# ----------------------------------------------------------------------------
#  Klops
# ----------------------------------------------------------------------------

def jelly(size, rx, ry, grape=True, bottom=None, phase=0):
    """Geleeklops: oben rund, unten flach, Unterkante = unterste Zeile.
    Gleiche Schattierung wie der Schleimkoenig, innen schimmert eine Traube."""
    img = Image.new("RGBA", (size, size), CLEAR)
    px = img.load()
    cx = size / 2
    bottom = size - 1 if bottom is None else bottom
    cy = bottom + 1 - ry
    m = set()
    for y in range(size):
        for x in range(size):
            dx = (x + 0.5 - cx) / rx
            dy = (y + 0.5 - cy) / ry
            n = 2.0 if dy < 0 else 3.4
            if abs(dx) ** n + abs(dy) ** n <= 1.0:
                m.add((x, y))
    m = {(x, y) for (x, y) in m if sum((x + a, y + b) in m for a, b in N4) >= 2}
    low = {}
    for (x, y) in m:
        low[x] = max(low.get(x, -1), y)
    for (x, y) in m:
        edge = any((x + a, y + b) not in m for a, b in N4)
        dx = (x + 0.5 - cx) / rx
        dy = (y + 0.5 - cy) / ry
        if edge:
            px[x, y] = OLD if dy > 0.3 else OL
            continue
        light = -0.5 * dx - 0.85 * dy
        if y == low[x] - 1 and abs(dx) < 0.8:
            c = SR
        elif light > 0.62:
            c = S1
        elif dx > 0.86 or (dy > 0.45 and dx > 0.66):
            c = S4
        elif dx > 0.6 or dy > 0.55 or (dx > 0.4 and dy > 0.2):
            c = S3
        else:
            c = S2
        px[x, y] = c

    def inside(x, y):
        return (x, y) in m and not any((x + a, y + b) not in m for a, b in N4)

    if grape:
        # Traube im Gelee: etwas rechts unter der Mitte, Rand dunkler, Glanz
        gr = max(2.2, min(rx, ry) * 0.42)
        gx, gy = cx + rx * 0.12, cy + ry * 0.12
        for y in range(size):
            for x in range(size):
                d = math.hypot(x + 0.5 - gx, y + 0.5 - gy)
                if d <= gr and inside(x, y):
                    px[x, y] = V2 if d > gr - 1.0 else V1
        hxp, hyp = int(gx - gr * 0.45), int(gy - gr * 0.45)
        if inside(hxp, hyp):
            px[hxp, hyp] = V0
        # Stielchen
        sx, sy = int(gx), int(gy - gr) - 1
        if inside(sx, sy):
            px[sx, sy] = LFD

    # Glanz oben links + Punkt
    top = min(y for (_, y) in m)
    gx0 = int(round(cx - rx * 0.55))
    for (x, y, c) in ((gx0, top + 2, S0), (gx0 + 1, top + 2, S0), (gx0 + 2, top + 1, S0),
                      (gx0 - 1, top + 3, S0), (gx0 - 1, top + 4, S1)):
        if inside(x, y):
            px[x, y] = c
    x2 = int(round(cx + rx * 0.6))
    if inside(x2, top + 3):
        px[x2, top + 3] = S1
    # Blaeschen
    for (bx, by) in ((cx - rx * 0.4, cy + ry * 0.35 - (phase % 3)), (cx - rx * 0.1, cy - ry * 0.25)):
        if inside(int(bx), int(by)):
            px[int(bx), int(by)] = BU
    return img


BLOB = 28
# rund, gestreckt (Flug), gequetscht (Aufschlag), wabbelnd (nachfedern)
BLOB_POSES = [(10.5, 9.5), (8.0, 12.5), (13.0, 6.5), (11.5, 8.5)]

MINI = 16
MINI_POSES = [(6.0, 5.5), (4.6, 7.0), (7.4, 3.8)]


def blob_strip(size, poses, grape):
    frames = [jelly(size, rx, ry, grape, phase=i) for i, (rx, ry) in enumerate(poses)]
    sheet = Image.new("RGBA", (size * len(frames), size), CLEAR)
    for i, f in enumerate(frames):
        sheet.alpha_composite(f, (i * size, 0))
    return sheet, frames


# ----------------------------------------------------------------------------
#  Schatten, Landepunkt
# ----------------------------------------------------------------------------

SHADOW_W, SHADOW_H = 24, 8


def shadow_strip():
    frames = []
    for i, k in enumerate((1.0, 0.8, 0.62, 0.46)):
        img = Image.new("RGBA", (SHADOW_W, SHADOW_H), CLEAR)
        px = img.load()
        rx, ry = 11.0 * k, 3.4 * k + 0.4
        for y in range(SHADOW_H):
            for x in range(SHADOW_W):
                d = ((x + 0.5 - SHADOW_W / 2) / rx) ** 2 + ((y + 0.5 - SHADOW_H / 2) / ry) ** 2
                if d <= 1.0:
                    px[x, y] = (40, 14, 60, 120 if d < 0.55 else 80)
        frames.append(img)
    sheet = Image.new("RGBA", (SHADOW_W * 4, SHADOW_H), CLEAR)
    for i, f in enumerate(frames):
        sheet.alpha_composite(f, (i * SHADOW_W, 0))
    return sheet, frames


def ring_size(r):
    return 2 * r + 8


def mark_frame(r, i):
    """Gestrichelter Ring genau am Trefferrand, vier Kerben nach innen.
    Bild 1 dreht die Striche einen halben Schritt weiter."""
    s = ring_size(r)
    img = Image.new("RGBA", (s, s), CLEAR)
    px = img.load()
    c = s / 2
    dashes = max(12, int(2 * math.pi * r / 7))
    step = 2 * math.pi / dashes
    for y in range(s):
        for x in range(s):
            dx, dy = x + 0.5 - c, y + 0.5 - c
            d = math.hypot(dx, dy)
            if abs(d - r) <= 0.75:
                a = (math.atan2(dy, dx) + i * step / 2) % (2 * math.pi)
                if (a / step) % 1.0 < 0.55:
                    px[x, y] = with_alpha(S1, 230)
            elif abs(d - r + 1.0) <= 0.5:
                a = (math.atan2(dy, dx) + i * step / 2) % (2 * math.pi)
                if (a / step) % 1.0 < 0.55:
                    px[x, y] = with_alpha(OL, 150)
    # Kerben oben/unten/links/rechts
    for (ux, uy) in ((0, -1), (0, 1), (-1, 0), (1, 0)):
        for k in range(2, 5):
            x = int(c + ux * (r - k))
            y = int(c + uy * (r - k))
            px[x, y] = with_alpha(S0, 220)
    # Mitte: kleines Kreuz
    for (x, y) in ((0, 0), (-1, 0), (1, 0), (0, -1), (0, 1)):
        px[int(c) + x, int(c) + y] = with_alpha(S1, 160)
    return img


def strip(frames, w):
    sheet = Image.new("RGBA", (w * len(frames), frames[0].height), CLEAR)
    for i, f in enumerate(frames):
        sheet.alpha_composite(f, (i * w, 0))
    return sheet


# ----------------------------------------------------------------------------
#  Aufschlag
# ----------------------------------------------------------------------------

HIT_FRAMES = 8


def drop(px, s, x, y, rad, alpha=255):
    """Geleetropfen mit Kontur und Glanzpunkt."""
    pts = []
    for yy in range(int(y - rad - 1), int(y + rad + 2)):
        for xx in range(int(x - rad - 1), int(x + rad + 2)):
            if (xx + 0.5 - x) ** 2 + (yy + 0.5 - y) ** 2 <= rad * rad and 0 <= xx < s and 0 <= yy < s:
                pts.append((xx, yy))
    ps = set(pts)
    for (xx, yy) in pts:
        edge = any((xx + a, yy + b) not in ps for a, b in N4)
        px[xx, yy] = with_alpha(OL if edge else S2, alpha)
    if rad >= 1.6 and pts:
        hx0 = min(pts, key=lambda p: p[0] + p[1])
        cand = (hx0[0] + 1, hx0[1] + 1)
        if cand in ps:
            px[cand] = with_alpha(S0, alpha)


def hit_frame(r, i):
    s = ring_size(r)
    img = Image.new("RGBA", (s, s), CLEAR)
    px = img.load()
    c = s / 2
    t = i / (HIT_FRAMES - 1)
    rng = random.Random(r * 7 + 3)

    # Platsch-Kern (nur die ersten Bilder): flache, helle Scheibe
    if i <= 2:
        core = r * (0.42 + 0.14 * i)
        for y in range(s):
            for x in range(s):
                d = math.hypot(x + 0.5 - c, (y + 0.5 - c) * 1.15)
                if d <= core:
                    if d > core - 1:
                        col = OL
                    elif i == 0 and d < core * 0.6:
                        col = S0
                    elif d < core * 0.75:
                        col = S1 if i < 2 else S2
                    else:
                        col = S2 if i < 2 else S3
                    px[x, y] = with_alpha(col, 255 if i < 2 else 200)

    # Ring rollt nach aussen und wird duenner
    if 1 <= i <= 5:
        rr = r * (0.5 + 0.5 * min(1.0, (i - 1) / 3.0))
        th = [0, 6.0, 5.0, 4.0, 3.0, 2.2][i]
        gap_seed = rng.random() * 6.28
        for y in range(s):
            for x in range(s):
                dx, dy = x + 0.5 - c, y + 0.5 - c
                d = math.hypot(dx, dy)
                if abs(d - rr) <= th / 2:
                    a = math.atan2(dy, dx)
                    # ab Bild 4 reisst der Ring auf
                    if i >= 4 and math.sin(a * 7 + gap_seed) > 0.35 - (i - 4) * 0.35:
                        continue
                    light = -(dx + dy) / max(d, 1e-6)
                    rel = d - rr
                    if rel > th / 2 - 1:
                        col = OLD
                    elif rel < -th / 2 + 1 and th >= 3.5:
                        col = OL
                    elif rel < 0:
                        col = S0 if light > 0.55 and i <= 3 else S1
                    else:
                        col = S2 if light > -0.3 else S3
                    px[x, y] = col

    # Spritzer fliegen raus und landen als kleine Kleckse
    n = 10 if r < 50 else 14
    for k in range(n):
        a = k * 2 * math.pi / n + rng.uniform(-0.18, 0.18)
        reach = r * rng.uniform(0.82, 1.08)
        rad = rng.choice((2.0, 2.5, 3.0))
        if i == 0:
            continue
        p = min(1.0, (i - 0.5) / 4.0)
        ease = 1 - (1 - p) ** 2
        d = reach * (0.35 + 0.65 * ease)
        x = c + math.cos(a) * d
        y = c + math.sin(a) * d - math.sin(p * math.pi) * r * 0.12
        if i <= 4:
            drop(px, s, x, y, rad)
        else:
            # gelandet: flacher Klecks, blasst aus
            alpha = [255, 255, 255, 255, 255, 230, 150, 70][i]
            flat = rad + 0.6
            for yy in range(int(y - 2), int(y + 3)):
                for xx in range(int(x - flat - 1), int(x + flat + 2)):
                    if ((xx + 0.5 - x) / flat) ** 2 + ((yy + 0.5 - y) / (flat * 0.55)) ** 2 <= 1 and 0 <= xx < s and 0 <= yy < s:
                        px[xx, yy] = with_alpha(S3 if yy > y else S2, alpha)
    return img


def hit_strip(r):
    frames = [hit_frame(r, i) for i in range(HIT_FRAMES)]
    return strip(frames, ring_size(r)), frames


# ----------------------------------------------------------------------------
#  Pfuetze
# ----------------------------------------------------------------------------

PUDDLE_FRAMES = 4


def puddle_frame(r, i):
    s = 2 * r + 4
    img = Image.new("RGBA", (s, s), CLEAR)
    px = img.load()
    c = s / 2
    rng = random.Random(r)
    lobes = [(rng.uniform(0, 6.28), rng.uniform(0.025, 0.05), k) for k in (3, 5, 7)]

    def radius(a):
        return r * (0.92 + sum(amp * math.sin(k * a + ph) for ph, amp, k in lobes))

    m = set()
    for y in range(s):
        for x in range(s):
            dx, dy = x + 0.5 - c, (y + 0.5 - c) * 1.25   # flach, liegt am Boden
            if math.hypot(dx, dy) <= radius(math.atan2(dy, dx)):
                m.add((x, y))
    for (x, y) in m:
        edge = any((x + a, y + b) not in m for a, b in N4)
        dx, dy = x + 0.5 - c, (y + 0.5 - c) * 1.25
        d = math.hypot(dx, dy) / r
        if edge:
            col = with_alpha(OL, 200)
        elif d > 0.78:
            col = with_alpha(S2, 185)
        else:
            col = with_alpha(S3, 160)
        px[x, y] = col
    # wandernder Glanzstreifen + Blaeschen, die aufploppen
    for k in range(3):
        a = -2.3 + 0.35 * k
        rr = r * (0.55 + 0.08 * ((i + k) % PUDDLE_FRAMES))
        x, y = int(c + math.cos(a) * rr), int(c + math.sin(a) * rr / 1.25)
        if (x, y) in m:
            px[x, y] = with_alpha(S0, 220)
            if (x + 1, y) in m:
                px[x + 1, y] = with_alpha(S1, 200)
    for k in range(4):
        a = k * 1.7 + 0.6
        rr = r * (0.25 + 0.15 * k)
        x, y = int(c + math.cos(a) * rr), int(c + math.sin(a) * rr / 1.25)
        if (x, y) in m and (i + k) % 4 != 3:
            px[x, y] = with_alpha(BU, 230)
    return img


def puddle_strip(r):
    frames = [puddle_frame(r, i) for i in range(PUDDLE_FRAMES)]
    return strip(frames, 2 * r + 4), frames


# ----------------------------------------------------------------------------
#  Icons
# ----------------------------------------------------------------------------

def icon():
    """Klops mitten im Sprung ueber einem Platscher, Flugbahn als Punkte, oben eine kleine Krone."""
    img = Image.new("RGBA", (32, 32), CLEAR)
    px = img.load()
    # Platscher unten
    for (x, y, rad) in ((5, 28, 1.6), (26, 27, 2.0), (9, 25, 1.4), (23, 24, 1.4)):
        drop(px, 32, x, y, rad)
    for x in range(8, 24):
        for y in range(27, 31):
            if ((x + 0.5 - 16) / 8) ** 2 + ((y + 0.5 - 29) / 2) ** 2 <= 1:
                px[x, y] = S3 if y > 28 else S2
    # Flugbahn
    for (x, y) in ((3, 15), (4, 12), (6, 9)):
        px[x, y] = S1
    blob = jelly(28, 10.0, 9.0, True)
    img.alpha_composite(blob.crop((2, 4, 28, 28)), (4, 3))
    # Mini-Krone obendrauf
    crown = ["o.o.o", "oYoYo", "YYYYY", "yyyyy"]
    key = {'o': G1, 'Y': G1, 'y': G2}
    for dy, row in enumerate(crown):
        for dx, ch in enumerate(row):
            if ch != '.':
                px[14 + dx, 2 + dy] = key[ch]
    return outline(img, hx('#22102e'))


SMALL = {
    ("royal_splat", 14): [
        "....y.y...",
        "....yyy...",
        "..OOOOOO..",
        ".OlsssssO.",
        "OlssssvssO",
        "OsssvvvssO",
        "OsssvvvdsO",
        "OddssvddddO",
        ".OOOOOOOOO.",
        "d.........d",
    ],
    ("royal_splat", 10): [
        "...y.y",
        "..OOOO.",
        ".OlsssO",
        "OsssvsdO",
        "OddvvddO",
        ".OOOOOO.",
    ],
}
SMALL_PAL = {"O": OL, "l": S0, "s": S2, "d": S3, "v": V1, "y": G1}
WB_OUTLINE = (0x3B, 0x2B, 0x33, 255)


def small_icon(rows, canvas):
    img = Image.new("RGBA", (canvas, canvas), CLEAR)
    off = 1
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch != "." and x + off < canvas and y + off < canvas:
                img.putpixel((x + off, y + off), SMALL_PAL[ch])
    return outline(img, WB_OUTLINE)


# ----------------------------------------------------------------------------

def main():
    dry = "--dry" in sys.argv
    preview = sys.argv[sys.argv.index("--preview") + 1] if "--preview" in sys.argv else None

    blob, blob_frames = blob_strip(BLOB, BLOB_POSES, True)
    mini, mini_frames = blob_strip(MINI, MINI_POSES, False)
    shadow, shadow_frames = shadow_strip()
    marks = {r: strip([mark_frame(r, i) for i in range(2)], ring_size(r)) for r in HIT_RADII}
    hits = {r: hit_strip(r) for r in HIT_RADII}
    puddles = {r: puddle_strip(r) for r in PUDDLE_RADII}
    ic = icon().resize((64, 64), Image.NEAREST)
    small = {key: small_icon(rows, key[1]) for key, rows in SMALL.items()}

    if not dry:
        out = [
            ("splat_blob", blob, len(BLOB_POSES), BLOB, BLOB, (0.5, 0.0)),
            ("splat_mini", mini, len(MINI_POSES), MINI, MINI, (0.5, 0.0)),
            ("splat_shadow", shadow, 4, SHADOW_W, SHADOW_H, (0.5, 0.5)),
        ]
        for r in HIT_RADII:
            out.append(("splat_mark_%d" % r, marks[r], 2, ring_size(r), ring_size(r), (0.5, 0.5)))
            out.append(("splat_hit_%d" % r, hits[r][0], HIT_FRAMES, ring_size(r), ring_size(r), (0.5, 0.5)))
        for r in PUDDLE_RADII:
            out.append(("splat_puddle_%d" % r, puddles[r][0], PUDDLE_FRAMES, 2 * r + 4, 2 * r + 4, (0.5, 0.5)))
        for base, img, frames, w, h, pivot in out:
            path = os.path.join(RES_WEAPONS, base + ".png")
            img.save(path)
            if not os.path.exists(path + ".meta"):
                write_strip_meta(path + ".meta", base, frames, w, h, 32, pivot=pivot)
        path = os.path.join(ICONS, "fin_royal_splat.png")
        ic.save(path)
        if not os.path.exists(path + ".meta"):
            write_strip_meta(path + ".meta", "fin_royal_splat", 1, 64, 64, 64)
        template = os.path.join(WORKBENCH, "salad_fan_14.png.meta")
        for (wid, size), img in small.items():
            path = os.path.join(WORKBENCH, "%s_%d.png" % (wid, size))
            img.save(path)
            new_single_meta(path + ".meta", template)
        print("Geschrieben: splat_blob/mini/shadow, splat_mark/hit_%s, splat_puddle_%s, fin_royal_splat, Werkbank"
              % ("/".join(map(str, HIT_RADII)), "/".join(map(str, PUDDLE_RADII))))

    if preview:
        s = 4
        bg = (58, 74, 58, 255)
        r = 48
        hit_frames = hits[r][1]
        W = ring_size(r)
        sheet = Image.new("RGBA", (W * HIT_FRAMES + 20, 3 * W + 140), bg)
        for i, f in enumerate(hit_frames):
            sheet.alpha_composite(f, (10 + i * W, 10))
        sheet.alpha_composite(marks[r], (10, W + 20))
        sheet.alpha_composite(puddles[36][0], (2 * W + 30, W + 20))
        sheet.alpha_composite(blob, (10, 2 * W + 30))
        sheet.alpha_composite(mini, (140, 2 * W + 30))
        sheet.alpha_composite(shadow, (200, 2 * W + 30))
        sheet.alpha_composite(ic, (320, 2 * W + 30))
        for k, ((wid, size), img) in enumerate(small.items()):
            sheet.alpha_composite(img, (400 + k * 20, 2 * W + 30))
        sheet.resize((sheet.width * s, sheet.height * s), Image.NEAREST).save(preview)


if __name__ == "__main__":
    main()
