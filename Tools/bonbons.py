"""
Zeichnet die XP-Bonbons (ExpPickup / SpawnExp).

  Assets/Resources/PickUps/candy_small.png    12 x 32x32  PPU 32  rot, unter 50 XP
  Assets/Resources/PickUps/candy_medium.png   12 x 32x32  PPU 32  blau mit Wirbel, ab 50 XP
  Assets/Resources/PickUps/candy_big.png      12 x 32x32  PPU 32  lila in Goldfolie, ab 300 XP
  Assets/Resources/PickUps/candy_lucky.png    12 x 32x32  PPU 32  golden, doppelte XP (LuckyXpChance)

Eingewickeltes Bonbon von der Seite: runder Kern, links und rechts die
gedrehten Papierenden. Je mehr XP, desto groesser und edler. Die Schleife
(ExpPickup, 8 fps) ist ruhiger als bei Herz/Magnet, weil oft hunderte
Bonbons herumliegen: 1 px Schweben, ein Glanzstreifen, Funkeln nur bei den
grossen.

Hilfsfunktionen (Licht, Kontur, Schatten, Glanz) kommen aus pickups.py.

.meta entsteht nur beim ersten Lauf (danach bleiben die Sprite-IDs).

Aufruf aus dem Projektordner:  python Tools/bonbons.py [--preview pfad.png]
"""

import math
import os
import sys

from PIL import Image

from pickups import (CLEAR, FRAMES, L, OUT, PPU, ROOT, S, glint, hx, mix, norm, outline,
                     preview, put, shadow, star, strip)
from unity_meta import write_strip_meta


def candy_layer(rx, ry, end_len, body, wrap, line, swirl=0):
    """
    Bonbon mit Mittelpunkt in der Ebene; Rueckgabe {(x, y): Farbe} ab (0, 0)
    plus Breite/Hoehe inklusive Kontur.
    rx/ry = Halbachsen des Kerns, end_len = Laenge eines Papierendes.
    swirl = Anzahl heller Wirbelstreifen auf dem Kern (0 = schlicht).
    """
    W = int(round(2 * rx)) + 2 * end_len
    H = int(round(2 * ry)) + 4
    cx, cy = W / 2.0, H / 2.0
    px = {}

    # Papierenden: Hals am Kern, nach aussen auffaechernd, Zackenkante
    for side in (-1, 1):
        for i in range(end_len):
            x = int(cx + side * (rx - 0.5)) + side * (i + 1) - (1 if side < 0 else 0)
            t = (i + 1) / end_len
            half = 1.0 + (H / 2.0 - 1.0) * t ** 0.6
            if i == end_len - 1:
                half -= 0.5
            for y in range(H):
                dy = y + 0.5 - cy
                if abs(dy) > half:
                    continue
                # Falten: helle und dunkle Bahnen, die zum Hals zusammenlaufen
                fold = (dy / max(half, 0.5)) * 2.0
                k = int(math.floor(fold + 2.0))
                shade = ["mid", "base", "light", "base", "mid"][max(0, min(4, k))]
                if dy < -half + 1.2 and i > 0:
                    shade = "light"
                if side > 0 and shade == "light":
                    shade = "base"
                # Krausung an der Aussenkante: 2 px breite Zacken
                if i == end_len - 1:
                    if (y // 2) % 2 == 1:
                        continue
                    shade = "light"
                px[(x, y)] = wrap[shade]
            # Knick direkt am Kern
            if i == 0:
                for y in range(int(cy - 1), int(cy + 1)):
                    px[(x, y)] = wrap["mid"]

    # Kern: Ellipsen-Kuppel mit Licht von links oben
    for y in range(H):
        for x in range(W):
            fx, fy = (x + 0.5 - cx) / rx, (y + 0.5 - cy) / ry
            d2 = fx * fx + fy * fy
            if d2 > 1.0:
                continue
            nrm = norm((fx, -fy, math.sqrt(max(0.0, 1.0 - d2)) + 0.25))
            b = sum(a * c for a, c in zip(nrm, L))
            stripe = False
            if swirl:
                ang = math.atan2(fy, fx)
                r = math.sqrt(d2)
                stripe = r > 0.2 and math.sin(swirl * ang + r * 2.6) > 0.55
            if b > 0.97:
                col = body["hi"]
            elif b > 0.8:
                col = body["light"]
            elif b > 0.52:
                col = body["base"]
            elif b > 0.25:
                col = body["mid"]
            else:
                col = body["dark"]
            if stripe:
                col = mix(col, body["stripe"], 0.6)
            px[(x, y)] = col

    # Glanzlicht oben links auf dem Kern
    gx, gy = int(cx - rx * 0.45), int(cy - ry * 0.55)
    for q in [(gx, gy), (gx + 1, gy), (gx - 1, gy + 1)]:
        px[q] = body["spec"]

    shifted = {(x + 1, y + 1): c for (x, y), c in px.items()}
    return outline(shifted, line), W + 2, H + 2


def compose(layer, w, h, frame, line, sparkle=None):
    img = Image.new("RGBA", (S, S), CLEAR)
    ox = (S - w) // 2
    top = (S - h) // 2 - 1
    # ruhiger als Herz/Magnet: nur 0/1 px
    lift = 1 if 3 <= frame < 9 else 0
    shadow(img, S / 2.0, top + h - 2, max(4, w - 12 - lift * 2 + (w % 2)))
    px = glint(layer, line, frame, start=0, steps=4, strength=0.45)
    for (x, y), c in px.items():
        put(img, ox + x, top + y - lift, c)
    if sparkle:
        sparkle(img, frame, ox, top - lift, w, h)
    return img


def sparkles(img, frame, ox, oy, w, h):
    core, arm = hx("ffffff"), hx("fff1a0", 200)
    for (x, y, phase) in [(ox + w - 7, oy + 1, 1), (ox + 5, oy + h - 3, 7)]:
        k = (frame - phase) % FRAMES
        if k < 3:
            star(img, x, y, [1, 2, 1][k], core, arm)


MINT = {"light": hx("f2fff8"), "base": hx("c6f0dc"), "mid": hx("8fd3b4")}
FOIL = {"light": hx("fff4b8"), "base": hx("ffcf4a"), "mid": hx("d68a1e")}

KINDS = {
    "candy_small": dict(
        rx=5.5, ry=4.5, end_len=4, swirl=0, line=hx("4a1226"), wrap=MINT,
        body={"spec": hx("ffffff"), "hi": hx("ffc9b8"), "light": hx("ff806a"), "base": hx("f04c3e"),
              "mid": hx("c42a36"), "dark": hx("861a38")}),
    "candy_medium": dict(
        rx=6.5, ry=5.5, end_len=5, swirl=3, line=hx("1c1f4f"), wrap=MINT,
        body={"spec": hx("ffffff"), "hi": hx("d9eeff"), "light": hx("86c4ff"), "base": hx("438ff0"),
              "mid": hx("2c60c6"), "dark": hx("213d8e"), "stripe": hx("ffffff")}),
    "candy_big": dict(
        rx=7.5, ry=6.5, end_len=5, swirl=3, line=hx("2b0f40"), wrap=FOIL, sparkle=sparkles,
        body={"spec": hx("ffffff"), "hi": hx("f3d6ff"), "light": hx("c784f7"), "base": hx("8d42d8"),
              "mid": hx("6124ab"), "dark": hx("3c1572"), "stripe": hx("ffd6f4")}),
    "candy_lucky": dict(
        rx=6.5, ry=5.5, end_len=5, swirl=3, line=hx("5a2a10"), wrap=MINT, sparkle=sparkles,
        body={"spec": hx("ffffff"), "hi": hx("fff6bf"), "light": hx("ffe066"), "base": hx("ffc23a"),
              "mid": hx("e8921c"), "dark": hx("a85a17"), "stripe": hx("fffbe0")}),
}


def build():
    sheets = {}
    for name, k in KINDS.items():
        layer, w, h = candy_layer(k["rx"], k["ry"], k["end_len"], k["body"], k["wrap"], k["line"],
                                  k["swirl"])
        sheets[name] = [compose(layer, w, h, f, k["line"], k.get("sparkle")) for f in range(FRAMES)]
    return sheets


def main():
    sheets = build()
    if "--preview" in sys.argv:
        preview(sheets, sys.argv[sys.argv.index("--preview") + 1])
        return
    os.makedirs(OUT, exist_ok=True)
    for name, frames in sheets.items():
        path = os.path.join(OUT, name + ".png")
        strip(frames).save(path)
        if not os.path.exists(path + ".meta"):
            write_strip_meta(path + ".meta", name, FRAMES, S, S, PPU)
        print("geschrieben:", os.path.relpath(path, ROOT))


if __name__ == "__main__":
    main()
