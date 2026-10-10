"""
Vorschaubilder des Lebkuchen-Geisterwalds fuer die Levelauswahl im Hub (Station 4):

  Assets/Art/new/Hub/level_preview_geist.png        50x50  (Station, Mitte 30x30 zu sehen)
  Assets/Art/new/Hub/level_preview_geist_wide.png   82x46  (Panorama, dreifach, schwenkt auf/ab)

Seitenansicht wie bei den anderen Welten: Nachthimmel mit einem Keks-Mond
(mit Biss!), dahinter blasse Baumreihen, vorne knorrige Lebkuchenbaeume als
Scherenschnitt, Grabsteine, eine gluehende Kuerbislaterne und Bodennebel.
Rahmen 1 px in #3b2433 wie die anderen Vorschauen.

Aufruf aus dem Projektordner:  python Tools/geist_vorschau.py [--preview a.png]
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
SKY = [hx("#120e24"), hx("#1a1430"), hx("#241a3e"), hx("#30214c"), hx("#3e2a58"), hx("#4e3462"), hx("#5e3e66")]
MOON = [hx("#b07a44"), hx("#d6a464"), hx("#f0cc8a"), hx("#fff0c4")]
CHOC = hx("#5a3220")
FAR = [hx("#2a1f40"), hx("#33264c")]
TREE = [hx("#1c1220"), hx("#2c1c2c"), hx("#4a2e30")]
ICING = hx("#c8c4e4")
STONE = [hx("#3e2a2e"), hx("#5a3e3a"), hx("#7a5648")]
GROUND = [hx("#1a1220"), hx("#221828"), hx("#2c2032")]
PUMPKIN = [hx("#8a3a12"), hx("#d06a22"), hx("#f6a040")]
FIRE = [hx("#ffb83c"), hx("#ffe58a")]
FOG = hx("#8c86b8")
WISP = [hx("#7af0e0"), hx("#c890ff")]


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
            th = ((x % 2) * 2 + (y % 2) * 3) % 4 / 4 + 0.125
            p.put(x, y, SKY[min(len(SKY) - 1, i + (1 if f > th else 0))])


def stars(p, rng, n, maxy):
    for _ in range(n):
        x, y = rng.randrange(p.w), rng.randrange(max(1, maxy))
        p.put(x, y, mix(p.get(x, y), MOON[3], 0.7 if rng.random() < 0.7 else 1.0))


def moon(p, cx, cy, r):
    """Keks-Mond: rund, Schokostueckchen, oben rechts angebissen, leichter Schein."""
    for y in range(int(cy - r - 3), int(cy + r + 4)):
        for x in range(int(cx - r - 3), int(cx + r + 4)):
            d = math.hypot(x + 0.5 - cx, y + 0.5 - cy)
            if r < d <= r + 1.3:
                p.put(x, y, mix(p.get(x, y), MOON[2], 0.22))
            elif r + 1.3 < d <= r + 2.6:
                p.put(x, y, mix(p.get(x, y), MOON[2], 0.09))
    bx, by, br = cx + r * 0.75, cy - r * 0.7, r * 0.5
    for y in range(int(cy - r), int(cy + r + 1)):
        for x in range(int(cx - r), int(cx + r + 1)):
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            if dx * dx + dy * dy > r * r:
                continue
            if (x + 0.5 - bx) ** 2 + (y + 0.5 - by) ** 2 < br * br:
                continue
            lit = -(dx * 0.6 + dy * 0.8) / r
            p.put(x, y, MOON[3] if lit > 0.45 else MOON[2] if lit > -0.25 else MOON[1])
    for ox, oy in ((-2, 1), (1, 3), (-1, -2), (3, 0)):
        if ox * ox + oy * oy < (r - 1) ** 2:
            p.put(int(cx + ox), int(cy + oy), CHOC)


def far_trees(p, horizon, rng, col):
    """Blasse Baumreihe am Horizont: duenne Staemme, kahle Kronen."""
    x = rng.randrange(3)
    while x < p.w:
        h = rng.randint(6, 11)
        for k in range(h):
            p.put(x, horizon - k, col)
        for k in range(rng.randint(2, 4)):
            yy = horizon - h + k * 2 + 1
            side = 1 if k % 2 else -1
            p.put(x + side, yy, col)
            p.put(x + side * 2, yy - 1, col)
        x += rng.randint(3, 6)
    for x in range(p.w):
        for y in range(horizon - 1, horizon + 1):
            p.put(x, y, col)


def ginger_tree(p, x, base, h, rng, flip=False):
    """Knorriger Lebkuchenbaum als Scherenschnitt, Zuckerguss auf den Asten."""
    s = -1 if flip else 1
    for k in range(h):
        w = 2 if k < h * 0.4 else 1
        lean = int(round(math.sin(k * 0.25) * 0.8))
        for d in range(-w + 1, w + 1):
            p.put(x + d + lean, base - k, TREE[1] if d < 1 else TREE[0])
    top = base - h
    for bi, (dx, dy, ln) in enumerate(((-1, -1, 6), (1, -1, 5), (-1, 0, 4), (1, 0, 6))):
        bx, by = x, top + 2 + bi * 2
        for k in range(ln):
            bx += dx * s
            if k % 2 == 0:
                by += dy
            p.put(bx, by, TREE[1])
            if k % 2 == 1:
                p.put(bx, by - 1, ICING)
        p.put(bx, by - 1, TREE[1])
        p.put(bx - dx * s, by - 2, TREE[1])     # eingerollte Spitze
    # Wurzeln
    for d in (-3, -2, 2, 3):
        p.put(x + d, base, TREE[0])


def tombstone(p, x, base, w, h):
    for yy in range(base - h, base + 1):
        for xx in range(x - w // 2, x + w // 2 + 1):
            top = base - h + (1 if abs(xx - x) == w // 2 else 0)
            if yy >= top:
                p.put(xx, yy, STONE[2] if xx < x else STONE[1])
    p.put(x, base - h + 2, ICING)
    p.put(x - 1, base - h + 3, ICING)
    p.put(x, base - h + 3, ICING)
    p.put(x + 1, base - h + 3, ICING)
    p.put(x, base - h + 4, ICING)


def pumpkin(p, cx, base):
    for yy in range(base - 4, base + 1):
        for xx in range(cx - 3, cx + 4):
            if ((xx - cx) / 3.5) ** 2 + ((yy - base + 2) / 2.6) ** 2 <= 1:
                p.put(xx, yy, PUMPKIN[2] if xx < cx else PUMPKIN[1])
    p.put(cx - 1, base - 3, FIRE[1])
    p.put(cx + 1, base - 3, FIRE[1])
    for xx in range(cx - 1, cx + 2):
        p.put(xx, base - 1, FIRE[0])
    p.put(cx, base - 5, hx("#3a4a24"))
    # Lichtschein auf dem Boden
    for yy in range(base - 6, base + 4):
        for xx in range(cx - 8, cx + 9):
            d = math.hypot(xx - cx, (yy - base + 1) * 1.6)
            if 3.5 < d < 8 and (xx + yy) % 2 == 0:
                p.put(xx, yy, mix(p.get(xx, yy), PUMPKIN[1], 0.25 * (1 - d / 8)))


def ground(p, horizon):
    for y in range(horizon, p.h):
        for x in range(p.w):
            t = (y - horizon) / max(1, p.h - horizon)
            p.put(x, y, GROUND[2] if y == horizon else GROUND[1] if t < 0.5 else GROUND[0])


def fog(p, y0, rng):
    for y in range(y0 - 2, y0 + 3):
        for x in range(p.w):
            band = 0.5 + 0.5 * math.sin(x * 0.18 + y * 0.7)
            if band > 0.35 and (x + y * 2) % 3 != 0:
                a = 0.32 * (1 - abs(y - y0) / 3)
                p.put(x, y, mix(p.get(x, y), FOG, a))


def path(p, horizon):
    for y in range(horizon + 1, p.h):
        t = (y - horizon) / max(1, p.h - horizon)
        half = 1 + t * 7
        cx = p.w * 0.5 + math.sin(t * 3) * 4
        for x in range(int(cx - half), int(cx + half) + 1):
            col = STONE[1] if x < cx else mix(STONE[1], STONE[0], 0.5)
            if (y - horizon) % 3 == 0 or (x + (y // 3) * 2) % 4 == 0:
                col = mix(STONE[0], ICING, 0.25)
            p.put(x, y, col)


def draw(w, h, seed, moon_x, horizon):
    rng = random.Random(seed)
    p = Pic(w, h)
    sky(p, horizon)
    stars(p, rng, w // 4, horizon - 4)
    moon(p, moon_x, 10, 6.5)
    far_trees(p, horizon - 2, rng, FAR[0])
    far_trees(p, horizon, rng, FAR[1])
    ground(p, horizon)
    path(p, horizon)
    fog(p, horizon + 2, rng)
    for tx, th, fl in ((5, 24, False), (w - 7, 26, True), (w // 2 - 15, 16, False), (w // 2 + 14, 18, True)):
        ginger_tree(p, tx, horizon + 4, th, rng, fl)
    tombstone(p, w // 2 - 7, horizon + 6, 5, 6)
    tombstone(p, w // 2 + 9, horizon + 8, 5, 5)
    pumpkin(p, w // 2 - 1, h - 6)
    for k in range(3):
        x, y = rng.randrange(4, w - 4), rng.randrange(horizon - 6, h - 6)
        p.put(x, y, WISP[k % 2])
    fog(p, h - 3, rng)
    for x in range(w):
        p.put(x, 0, FRAME)
        p.put(x, h - 1, FRAME)
    for y in range(h):
        p.put(0, y, FRAME)
        p.put(w - 1, y, FRAME)
    return p.im


def main():
    station = draw(50, 50, 3, 33, 30)
    wide = draw(82, 46, 7, 58, 28)
    if "--preview" in sys.argv:
        out = sys.argv[sys.argv.index("--preview") + 1]
        c = Image.new("RGBA", (50 + 82 + 6, 50), (40, 40, 40, 255))
        c.paste(station, (0, 0))
        c.paste(wide, (56, 0))
        c.resize((c.width * 5, c.height * 5), Image.NEAREST).save(out)
        return
    for name, im in (("level_preview_geist", station), ("level_preview_geist_wide", wide)):
        path_ = os.path.join(OUT_DIR, name + ".png")
        im.save(path_)
        if not os.path.exists(path_ + ".meta"):
            write_strip_meta(path_ + ".meta", name, 1, im.width, im.height, 50, pivot=(0, 0))
        print("geschrieben:", path_)


if __name__ == "__main__":
    main()
