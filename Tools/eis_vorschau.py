"""
Vorschaubilder der Eiswelt fuer die Levelauswahl im Hub (Station 3):

  Assets/Art/new/Hub/level_preview_eis.png        50x50  (Station, Mitte 30x30 zu sehen)
  Assets/Art/new/Hub/level_preview_eis_wide.png   82x46  (Panorama, dreifach, schwenkt auf/ab)

Seitenansicht wie bei den anderen Welten: Daemmerhimmel mit Polarlicht,
Berge aus Eiskugeln mit Sahneschnee (der groesste traegt eine Kirsche),
davor Waffeltannen, vorne Schnee mit einer Glatteis-Pfuetze.
Rahmen 1 px in #3b2433 wie die anderen Vorschauen.

Aufruf aus dem Projektordner:  python Tools/eis_vorschau.py [--preview a.png]
Die .meta wird nur beim ersten Mal geschrieben.
"""

import math
import os
import random
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from unity_meta import write_strip_meta  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "Assets", "Art", "new", "Hub")


def hx(s):
    s = s.lstrip("#")
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), 255)


def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3)) + (255,)


FRAME = hx("#3b2433")
SKY = [hx("#2c2a5e"), hx("#383472"), hx("#4a4286"), hx("#62549a"), hx("#7e68aa"), hx("#a07eb6"), hx("#c896bc")]
AURORA = [hx("#6fe0b6"), hx("#a8f0d0"), hx("#f6a0c8")]
SCOOPS = [
    [hx("#82284a"), hx("#b94468"), hx("#e46e8c"), hx("#f6a0b4")],     # Erdbeer
    [hx("#3c6438"), hx("#5a8e4e"), hx("#82b868"), hx("#acd88c")],     # Pistazie
    [hx("#3a3a78"), hx("#5a5aa8"), hx("#8a86d0"), hx("#b8b4ea")],     # Blaubeer
    [hx("#48281a"), hx("#6a3e25"), hx("#8c5935"), hx("#b07a50")],     # Schoko
]
CREAM = [hx("#a9a9d2"), hx("#cdd0ea"), hx("#e8eaf8"), hx("#ffffff")]
WAFFLE = [hx("#6e3f1c"), hx("#9a6229"), hx("#c98a3e"), hx("#e6b464")]
SNOW = [hx("#9c9ec6"), hx("#b7b9da"), hx("#c3c5e2"), hx("#d5d7ee"), hx("#e4e6f6")]
ICE = [hx("#3b5f96"), hx("#5684bc"), hx("#7eaedb"), hx("#b1d8f1"), hx("#effbff")]
CHERRY = [hx("#560e1c"), hx("#c32a3c"), hx("#e8545c"), hx("#ffb6b4")]


class Pic:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.im = Image.new("RGBA", (w, h), SKY[0])
        self.px = self.im.load()

    def put(self, x, y, c):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.px[x, y] = c

    def get(self, x, y):
        return self.px[x, y]


def sky(p, horizon):
    for y in range(p.h):
        t = min(1.0, y / max(1, horizon))
        k = t * (len(SKY) - 1)
        i = int(k)
        f = k - i
        for x in range(p.w):
            # Bayer-Dither zwischen zwei Stufen
            th = ((x % 2) * 2 + (y % 2) * 3) % 4 / 4 + 0.125
            p.put(x, y, SKY[min(len(SKY) - 1, i + (1 if f > th else 0))])


def aurora(p, rng, top, amp):
    """Polarlicht: senkrechte Vorhangstreifen, oben hell, nach unten ausgeblendet."""
    ph = rng.random() * 6
    for x in range(p.w):
        yc = top + amp * math.sin(x * 0.09 + ph) + 1.2 * math.sin(x * 0.27 + ph * 2)
        length = 5 + 3 * math.sin(x * 0.5 + ph) + 2 * math.sin(x * 1.3)
        strength = 0.35 + 0.25 * math.sin(x * 0.07 + ph * 3)
        if strength < 0.2:
            continue
        for k in range(int(length)):
            y = int(round(yc + k))
            if not 0 <= y < p.h:
                continue
            a = strength * (1 - k / length) ** 1.5
            col = AURORA[1] if k == 0 else AURORA[0]
            if k == 0 or (a > 0.1 and (x + y) % 2 == 0):
                p.put(x, y, mix(p.get(x, y), col, min(0.6, a + 0.1)))


def stars(p, rng, n, maxy):
    for _ in range(n):
        x, y = rng.randrange(p.w), rng.randrange(max(1, maxy))
        p.put(x, y, mix(p.get(x, y), CREAM[3], 0.8))


def scoop_mountain(p, cx, base, r, pal, rng, cherry=False):
    """Eiskugel als Berg: Kugel sitzt im Horizont, Sahnekappe mit Nasen."""
    cy = base - r * 0.45
    ph = rng.random() * 6
    for y in range(int(cy - r) - 1, base + 1):
        for x in range(int(cx - r) - 1, int(cx + r) + 2):
            dx, dy = (x + 0.5 - cx) / r, (y + 0.5 - cy) / r
            if dx * dx + dy * dy > 1:
                continue
            lit = -(dx * 0.75 + dy * 0.65)
            col = pal[3] if lit > 0.6 else pal[2] if lit > 0.05 else pal[1] if lit > -0.55 else pal[0]
            edge = -0.64 + 0.06 * math.sin(dx * 9 + ph) + (0.22 if math.sin(dx * 5 + ph) > 0.8 else 0)
            if dy < edge:
                col = CREAM[3] if lit > 0.6 else CREAM[2] if lit > 0.0 else CREAM[1]
            elif dy < edge + 0.12:
                col = pal[0] if col != pal[3] else pal[1]
            p.put(x, y, col)
    if cherry:
        kx, ky = int(cx), int(cy - r) - 1
        for y in range(ky - 2, ky + 3):
            for x in range(kx - 2, kx + 3):
                if (x - kx) ** 2 + (y - ky) ** 2 <= 5:
                    p.put(x, y, CHERRY[2] if x + y < kx + ky else CHERRY[1])
        p.put(kx - 1, ky - 1, CHERRY[3])
        p.put(kx + 1, ky - 3, WAFFLE[0])
        p.put(kx + 2, ky - 4, WAFFLE[0])


def waffle_tree(p, x, base, h):
    """Kleine Waffeltanne: drei Dreiecke, Schnee auf den Kanten."""
    tiers = 3
    th = max(4, h // 2)
    for t in range(tiers):
        bottom = base - 2 - t * (h - th) // (tiers - 1)
        hw = max(2, th // 2 + 2 - t)
        for k in range(th):
            y = bottom - k
            half = int(round(hw * (1 - k / th)))
            for dx in range(-half, half + 1):
                col = WAFFLE[2] if dx < 0 else WAFFLE[1]
                if (dx + y) % 3 == 0 or (dx - y) % 3 == 0:
                    col = WAFFLE[1] if dx < 0 else WAFFLE[0]
                if abs(dx) == half or k == th - 1:
                    col = CREAM[3] if dx <= 0 else CREAM[1]
                if k == 0:
                    col = WAFFLE[0]
                p.put(x + dx, y, col)
    p.put(x, base - 1, WAFFLE[0])
    p.put(x, base, WAFFLE[0])


def ground(p, horizon, rng):
    for y in range(horizon, p.h):
        for x in range(p.w):
            t = (y - horizon) / max(1, p.h - horizon)
            col = SNOW[4] if y == horizon else SNOW[3] if t < 0.25 else SNOW[2]
            if t > 0.25 and (x * 3 + y * 5) % 17 == 0:
                col = SNOW[1]
            p.put(x, y, col)
    # sanfte Huegelkante
    for x in range(p.w):
        bump = int(round(1.2 * math.sin(x * 0.2) + 0.8 * math.sin(x * 0.53 + 1)))
        for y in range(horizon - 2, horizon + 1):
            if y >= horizon + bump - 1:
                p.put(x, y, SNOW[4] if y == horizon + bump - 1 else SNOW[3])


def pond(p, cx, cy, rx, ry):
    for y in range(cy - ry - 1, cy + ry + 2):
        for x in range(cx - rx - 1, cx + rx + 2):
            d = ((x + 0.5 - cx) / rx) ** 2 + ((y + 0.5 - cy) / ry) ** 2
            if d <= 1:
                col = ICE[3] if d > 0.6 else ICE[2]
                if y < cy - ry * 0.4 and d > 0.5:
                    col = ICE[1]
                p.put(x, y, col)
            elif d <= 1.5:
                p.put(x, y, SNOW[4] if y > cy else SNOW[1])
    for k in range(rx):
        x, y = cx - rx // 2 + k, cy + ry // 2 - k // 2
        if ((x + 0.5 - cx) / rx) ** 2 + ((y + 0.5 - cy) / ry) ** 2 < 0.8:
            p.put(x, y, ICE[4])


def snowflakes(p, rng, n):
    for _ in range(n):
        x, y = rng.randrange(1, p.w - 1), rng.randrange(1, p.h - 1)
        p.put(x, y, CREAM[3])


def draw(w, h, seed, peak_x, horizon):
    rng = random.Random(seed)
    p = Pic(w, h)
    sky(p, horizon)
    stars(p, rng, w // 6, horizon // 2)
    aurora(p, rng, 4, 2.5)
    # hintere, blassere Kugelberge, vorne die grosse Erdbeerkugel mit Kirsche
    for cx, r, pal in ((peak_x - 24, 14, SCOOPS[2]), (peak_x + 24, 15, SCOOPS[1])):
        scoop_mountain(p, cx, horizon, r, [mix(c, SKY[5], 0.3) for c in pal], rng)
    scoop_mountain(p, peak_x, horizon + 3, 17, SCOOPS[0], rng, cherry=True)
    ground(p, horizon, rng)
    for tx, th in ((peak_x - 20, 13), (peak_x - 30, 10), (peak_x + 22, 12), (peak_x + 33, 9), (4, 11), (w - 5, 10)):
        if 2 <= tx < w - 2:
            waffle_tree(p, tx, horizon + 3, th)
    pond(p, peak_x - 4, h - 8, 13, 3)
    snowflakes(p, rng, w // 4)
    for x in range(w):
        p.put(x, 0, FRAME)
        p.put(x, h - 1, FRAME)
    for y in range(h):
        p.put(0, y, FRAME)
        p.put(w - 1, y, FRAME)
    return p.im


def main():
    station = draw(50, 50, 3, 25, 33)
    wide = draw(82, 46, 7, 46, 30)
    if "--preview" in sys.argv:
        out = sys.argv[sys.argv.index("--preview") + 1]
        c = Image.new("RGBA", (50 + 82 + 6, 50), (40, 40, 40, 255))
        c.paste(station, (0, 0))
        c.paste(wide, (56, 0))
        c.resize((c.width * 5, c.height * 5), Image.NEAREST).save(out)
        return
    for name, im in (("level_preview_eis", station), ("level_preview_eis_wide", wide)):
        path = os.path.join(OUT_DIR, name + ".png")
        im.save(path)
        if not os.path.exists(path + ".meta"):
            write_strip_meta(path + ".meta", name, 1, im.width, im.height, 50, pivot=(0, 0))
        print("geschrieben:", path)


if __name__ == "__main__":
    main()
