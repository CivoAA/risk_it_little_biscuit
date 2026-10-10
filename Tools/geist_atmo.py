"""
Stimmungs-Sprites fuer den Lebkuchen-Geisterwald (Geisterwald.cs am Map-Objekt):

  Assets/Art/World-Objects/Geist/Atmo/geist_nebel_<n>.png   Nebelbaenke (halbtransparent, gerastert)
  Assets/Art/World-Objects/Geist/Atmo/geist_zuckergeist.png Zuckergeist, 8 Bilder 18x20:
                                                            0-5 schweben (Schleppe wedelt), 6-7 erschrocken
  Assets/Art/World-Objects/Geist/Atmo/geist_irrlicht.png    Irrlicht, 2 Bilder 7x7 (gross/klein), weiss - Farbe kommt vom Code
  Assets/Art/World-Objects/Geist/Atmo/geist_fledermaus.png  Fledermaus, 4 Bilder 16x10, Fluegelschlag

Alles wird unbeleuchtet gezeichnet (Sprite-Unlit-Default), die Farben sind
also schon die Nachtfarben. Pivot Mitte, PPU 32.

Aufruf aus dem Projektordner:  python Tools/geist_atmo.py [--preview pfad.png]
"""

import math
import os
import random
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from unity_meta import write_strip_meta  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "Assets", "Art", "World-Objects", "Geist", "Atmo")
PPU = 32

BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]


def hx(s, a=255):
    s = s.lstrip("#")
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), a)


# --- Nebel ----------------------------------------------------------------------------

def nebel(w, h, seed):
    """Weiche Nebelbank: Dichte aus ueberlagerten Ellipsen, in 3 Alphastufen gerastert."""
    rng = random.Random(seed)
    blobs = []
    for _ in range(7):
        bx = rng.uniform(0.2, 0.8) * w
        by = rng.uniform(0.4, 0.62) * h
        blobs.append((bx, by, rng.uniform(0.18, 0.32) * w, rng.uniform(0.22, 0.36) * h))
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    px = img.load()
    # Schlieren: langgezogenes Rauschen, damit die Bank nicht wie ein Pfannkuchen aussieht
    from vulkan_noise import fbm, grid
    X, Y = grid(w, h)
    wisp = fbm(X / 14.0, Y / 5.0, seed * 7 + 1, octaves=3).tolist()
    col = (206, 202, 240)
    levels = [(0.22, 34), (0.45, 58), (0.7, 84)]
    for y in range(h):
        for x in range(w):
            d = 0.0
            for bx, by, rx, ry in blobs:
                q = ((x + 0.5 - bx) / rx) ** 2 + ((y + 0.5 - by) / ry) ** 2
                d += max(0.0, 1 - q) ** 1.5
            # zum Rand hin sicher auslaufen
            ex = min(x + 0.5, w - x - 0.5) / (w * 0.18)
            ey = min(y + 0.5, h - y - 0.5) / (h * 0.25)
            d *= min(1.0, ex) * min(1.0, ey)
            d *= 0.55 + 0.75 * wisp[y][x]
            d = min(1.0, d)
            a = 0
            thr = (BAYER[y % 4][x % 4] + 0.5) / 16.0
            for lim, alpha in levels:
                # zwischen zwei Stufen gerastert, damit keine harten Kanten entstehen
                if d > lim - 0.12 + 0.24 * thr:
                    a = alpha
            if a:
                px[x, y] = col + (a,)
    return img


# --- Zuckergeist ------------------------------------------------------------------------

GW, GH = 18, 20
BODY = [hx("#7c74b4"), hx("#b4b0e0"), hx("#dcdaf6"), hx("#f4f4ff"), hx("#ffffff")]
EYE = hx("#2a1e3c")
BLUSH = hx("#ff9cc8")


def ghost(frame, scared=False):
    img = Image.new("RGBA", (GW, GH), (0, 0, 0, 0))
    px = img.load()
    cx = 9.0
    bob = [0, 0, 1, 1, 1, 0][frame % 6] if not scared else 0
    top = 2 + bob
    head_r = 6.5
    hy = top + head_r
    body = set()
    for y in range(GH):
        for x in range(GW):
            dx = x + 0.5 - cx
            if y + 0.5 < hy:
                if dx * dx + (y + 0.5 - hy) ** 2 <= head_r * head_r:
                    body.add((x, y))
            else:
                # Koerper wird nach unten etwas schmaler, Schleppe wellt
                t = (y + 0.5 - hy) / 9.0
                half = head_r * (1 - 0.18 * t)
                wave = math.sin((x * 0.9) + frame * (math.pi / 3)) * 1.3
                bottom = hy + 8 + wave + (1.5 if scared else 0)
                if abs(dx) <= half and y + 0.5 <= bottom:
                    body.add((x, y))
    if scared:
        # erschrocken: Koerper huepft hoch und zittert seitlich
        shift = 1 if frame % 2 else -1
        body = {(x + shift, y - 1) for x, y in body}
    for x, y in body:
        if not (0 <= x < GW and 0 <= y < GH):
            continue
        dx = (x + 0.5 - cx) / head_r
        dy = (y + 0.5 - hy) / head_r
        v = -dx * 0.6 - dy * 0.5
        k = 3 if v > 0.35 else 2 if v > -0.35 else 1
        px[x, y] = BODY[k]
    # Kontur
    src = img.copy().load()
    for y in range(GH):
        for x in range(GW):
            if src[x, y][3]:
                continue
            if any(0 <= x + a < GW and 0 <= y + b < GH and src[x + a, y + b][3]
                   for a, b in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                px[x, y] = BODY[0]
    sx = (1 if frame % 2 else -1) if scared else 0
    ey = int(hy) - 1 + (-1 if scared else 0)
    if scared:
        # weit offene Augen, O-Mund
        for ex in (6, 11):
            for a, b in ((0, 0), (1, 0), (0, 1), (1, 1)):
                px[ex + sx + a, ey + b] = EYE
            px[ex + sx, ey] = BODY[4]
        for a, b in ((0, 0), (1, 0), (0, 1), (1, 1)):
            px[8 + sx + a, ey + 4 + b] = EYE
    else:
        for ex in (6, 11):
            px[ex, ey] = EYE
            px[ex, ey + 1] = EYE
        px[8, ey + 3] = EYE
        px[9, ey + 3] = EYE
        px[5, ey + 2] = BLUSH
        px[12, ey + 2] = BLUSH
        px[6, ey - 2] = BODY[4]     # Glanz
        px[5, ey - 1] = BODY[4]
    return img


# --- Irrlicht --------------------------------------------------------------------------

def irrlicht(big):
    img = Image.new("RGBA", (7, 7), (0, 0, 0, 0))
    px = img.load()
    if big:
        ring = [(2, 1), (3, 1), (4, 1), (1, 2), (5, 2), (1, 3), (5, 3), (1, 4), (5, 4), (2, 5), (3, 5), (4, 5)]
        for x, y in ring:
            px[x, y] = (200, 200, 200, 150)
        for x, y in ((2, 2), (3, 2), (4, 2), (2, 3), (4, 3), (2, 4), (3, 4), (4, 4)):
            px[x, y] = (235, 235, 235, 255)
        px[3, 3] = (255, 255, 255, 255)
        for x, y in ((3, 0), (0, 3), (6, 3), (3, 6)):
            px[x, y] = (170, 170, 170, 70)
    else:
        for x, y in ((3, 2), (2, 3), (4, 3), (3, 4)):
            px[x, y] = (210, 210, 210, 170)
        px[3, 3] = (255, 255, 255, 255)
    return img


# --- Fledermaus ------------------------------------------------------------------------

BAT = [hx("#140c1e"), hx("#2a1c3a"), hx("#463060")]


BAT_FRAMES = [
    ["................",
     "#..............#",
     "##............##",
     ".##....##....##.",
     "..###.#oo#.###..",
     "...##########...",
     "......####......",
     ".......##.......",
     "................",
     "................"],
    ["................",
     "................",
     "................",
     "#......##......#",
     "###...#oo#...###",
     ".##############.",
     "..####.##.####..",
     "...#...##...#...",
     "................",
     "................"],
    ["................",
     "................",
     "................",
     ".......##.......",
     "......#oo#......",
     "...##########...",
     "..####.##.####..",
     ".###...##...###.",
     "##............##",
     "#..............#"],
]


def fledermaus(frame):
    rows = BAT_FRAMES[[0, 1, 2, 1][frame]]
    img = Image.new("RGBA", (16, 10), (0, 0, 0, 0))
    px = img.load()
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch == "#":
                top = y == 0 or rows[y - 1][x] == "."
                px[x, y] = BAT[2] if top else BAT[1] if y < 6 else BAT[0]
            elif ch == "o":
                px[x, y] = (255, 120, 90, 255)
    return img


def strip(frames):
    w, h = frames[0].size
    out = Image.new("RGBA", (w * len(frames), h), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        out.paste(f, (i * w, 0))
    return out


def all_images():
    return {
        "geist_nebel_0": [nebel(176, 60, 1)],
        "geist_nebel_1": [nebel(128, 46, 2)],
        "geist_nebel_2": [nebel(96, 40, 3)],
        "geist_zuckergeist": [ghost(f) for f in range(6)] + [ghost(0, True), ghost(1, True)],
        "geist_irrlicht": [irrlicht(True), irrlicht(False)],
        "geist_fledermaus": [fledermaus(f) for f in range(4)],
    }


def main():
    imgs = all_images()
    if "--preview" in sys.argv:
        out = Image.new("RGBA", (420, 140), (34, 28, 52, 255))
        x = 4
        for name in ("geist_nebel_0", "geist_nebel_1"):
            out.alpha_composite(imgs[name][0], (x, 4))
            x += imgs[name][0].width + 4
        x = 4
        for f in imgs["geist_zuckergeist"] + imgs["geist_irrlicht"] + imgs["geist_fledermaus"]:
            out.alpha_composite(f, (x, 100))
            x += f.width + 4
        out.resize((out.width * 3, out.height * 3), Image.NEAREST).save(sys.argv[sys.argv.index("--preview") + 1])
        return
    os.makedirs(OUT_DIR, exist_ok=True)
    for name, frames in imgs.items():
        path = os.path.join(OUT_DIR, name + ".png")
        strip(frames).save(path)
        w, h = frames[0].size
        if not os.path.exists(path + ".meta"):
            write_strip_meta(path + ".meta", name, len(frames), w, h, PPU)
    print("geschrieben:", len(imgs), "Bilder nach", OUT_DIR)


if __name__ == "__main__":
    main()
