"""
Vorschaubilder der Vulkanwelt fuer die Levelauswahl im Hub (Station 5):

  Assets/Art/new/Hub/level_preview_vulkan.png        50x50  (Station, Mitte 30x30 zu sehen)
  Assets/Art/new/Hub/level_preview_vulkan_wide.png   82x46  (Panorama, dreifach, schwenkt auf/ab)

Seitenansicht wie Kueche und Wald: glimmender Himmel, Vulkan mit Ausbruch,
Lavastroeme, vorne Basalt mit Asche und einem Lavaloch wie im Spiel.
Rahmen 1 px in #3b2433 wie die anderen Vorschauen.

Aufruf aus dem Projektordner:  python Tools/vulkan_vorschau.py [--preview a.png]
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


FRAME = hx("#3b2433")
SKY = [hx("#1d1220"), hx("#2a1626"), hx("#3e1c2a"), hx("#5a2429"), hx("#7e3226"), hx("#a44a26")]
CLOUD = [hx("#2a1e28"), hx("#3a2a33"), hx("#4c3639"), hx("#6a4038")]
MOUNT = [hx("#1a1319"), hx("#261c24"), hx("#332630"), hx("#45343c")]
FAR = [hx("#3a1f2a"), hx("#4a2630")]
LAVA = [hx("#8e2412"), hx("#c43c14"), hx("#e8661c"), hx("#fa9a2c"), hx("#ffcf55"), hx("#fff3b0")]
GROUND = [hx("#221b22"), hx("#2a2229"), hx("#342b33"), hx("#3c333b"), hx("#4e434b"), hx("#5a4e55")]
ASH = [hx("#5e5558"), hx("#776d70"), hx("#896e70"), hx("#968b8a")]


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


def draw(w, h, seed, volcano_x, horizon):
    rng = random.Random(seed)
    p = Pic(w, h)

    # Himmel: Baender, unten gluehend (Ordered Dither an den Uebergaengen)
    bayer = [[0, 2], [3, 1]]
    for y in range(h):
        t = y / max(1, horizon) * (len(SKY) - 1)
        i = int(t)
        f = t - i
        for x in range(w):
            c = SKY[min(i, len(SKY) - 1)]
            if f * 4 > bayer[y % 2][x % 2] + 1.6 and i + 1 < len(SKY):
                c = SKY[i + 1]
            p.put(x, y, c)

    # Rauchsaeule ueber dem Krater, von unten rot angeleuchtet
    top = int(horizon * 0.42)
    crater_y = top + 2
    for k in range(22):
        cy = crater_y - 2 - k * 1.5
        cx = volcano_x + math.sin(k * 0.55) * 2.0 + k * 0.5
        r = 2.4 + k * 0.5 + 1.2 * math.sin(k * 1.7)
        for y in range(int(cy - r), int(cy + r) + 1):
            for x in range(int(cx - r - 1), int(cx + r + 2)):
                d = ((x - cx) / (r + 1)) ** 2 + ((y - cy) / r) ** 2
                if d <= 1:
                    lit = (y - cy) / r
                    col = CLOUD[3] if lit > 0.45 and k < 9 else CLOUD[2] if lit > 0 else CLOUD[1]
                    if d > 0.75:
                        col = CLOUD[1] if col != CLOUD[3] else CLOUD[2]
                    p.put(x, y, col)
    # flache Aschewolken am Himmel
    for (cx, cy, r) in [(w * 0.18, horizon * 0.22, 7), (w * 0.86, horizon * 0.3, 6), (w * 0.62, horizon * 0.12, 5)]:
        for y in range(int(cy - 2), int(cy + 3)):
            for x in range(int(cx - r), int(cx + r)):
                d = ((x - cx) / r) ** 2 + ((y - cy) / 2.4) ** 2
                if d <= 1:
                    p.put(x, y, CLOUD[1] if y > cy else CLOUD[0])

    # ferne Bergkette
    for x in range(w):
        hgt = horizon - 4 - int(3 * math.sin(x * 0.21 + seed) + 2 * math.sin(x * 0.53))
        for y in range(hgt, horizon + 2):
            p.put(x, y, FAR[0] if y > hgt else FAR[1])

    # Vulkan: Kegel mit eingekerbtem Krater, Licht von links-oben
    base_half = int(w * 0.42) if w > 60 else int(w * 0.5)
    for y in range(top, horizon + 4):
        t = (y - top) / max(1, horizon + 4 - top)
        half = 3 + t ** 0.85 * base_half
        wob = int(1.2 * math.sin(y * 0.9 + seed))
        for x in range(int(volcano_x - half) + wob, int(volcano_x + half) + 1 + wob):
            rel = (x - volcano_x - wob) / max(1, half)
            col = MOUNT[3] if rel < -0.45 else MOUNT[2] if rel < -0.05 else MOUNT[1]
            if -0.5 < rel < -0.4 and (x + y) % 2:
                col = MOUNT[2]
            if -0.1 < rel < 0.0 and (x + y) % 2:
                col = MOUNT[1]
            p.put(x, y, col)
        # Kontur, rechts vom gluehenden Himmel angestrahlt
        p.put(int(volcano_x - half) + wob - 1, y, MOUNT[0])
        p.put(int(volcano_x + half) + wob, y, SKY[3] if y > top + 3 else MOUNT[0])
        p.put(int(volcano_x + half) + wob + 1, y, MOUNT[0])
    for x in range(int(volcano_x - 4), int(volcano_x + 5)):
        p.put(x, top - 1, MOUNT[0])
    # Kratergluehen
    for x in range(int(volcano_x - 2), int(volcano_x + 3)):
        p.put(x, top, LAVA[4] if abs(x - volcano_x) < 2 else LAVA[2])
        p.put(x, top + 1, LAVA[1])
    p.put(int(volcano_x), top - 1, LAVA[5])
    # Lavastroeme die Flanke hinunter
    for s, dirn in ((0, -1), (1, 1), (2, 1)):
        x = volcano_x + dirn * (1 + s)
        y = top + 1
        length = int((horizon - top) * (0.75 if s < 2 else 0.45))
        for k in range(length):
            y += 1
            x += dirn * (0.35 + 0.25 * math.sin(k * 0.7 + s * 2))
            c = LAVA[3] if k < length * 0.4 else LAVA[2] if k < length * 0.75 else LAVA[1]
            p.put(int(x), y, c)
            if k % 4 == 0:
                p.put(int(x) + dirn, y, LAVA[0])
    # Lavabomben
    for (dx, dy, c) in ((-5, -7, LAVA[4]), (4, -9, LAVA[3]), (7, -4, LAVA[4]), (-8, -3, LAVA[2])):
        p.put(int(volcano_x + dx), top + dy, c)
        p.put(int(volcano_x + dx) - (1 if dx > 0 else -1), top + dy + 1, LAVA[0])

    # Boden: Basalt mit Asche, Kante oben im Licht
    ground_top = horizon + 3
    for x in range(w):
        gy = ground_top + int(1.5 * math.sin(x * 0.3 + seed * 1.3))
        for y in range(gy, h):
            c = GROUND[3] if y > gy + 1 else GROUND[4]
            if y == gy:
                c = GROUND[5]
            p.put(x, y, c)
        p.put(x, gy - 1, GROUND[0])
        # Aschewehen
        if math.sin(x * 0.17 + seed) > 0.35:
            for y in range(gy, gy + 2):
                p.put(x, y, ASH[2] if y == gy else ASH[1])
    # Poren und Steine
    for _ in range(int(w * h / 90)):
        x, y = rng.randrange(w), rng.randrange(ground_top + 3, h)
        p.put(x, y, GROUND[1])
        p.put(x + 1, y + 1, GROUND[4])

    # Lavaloch im Vordergrund (wie im Spiel: Kruste, Rueckwand, Lava)
    px, py = int(w * (0.3 if w > 60 else 0.36)), h - 9
    rx, ry = (11, 3.6) if w > 60 else (9, 3.2)
    for y in range(int(py - ry - 3), int(py + ry + 4)):
        for x in range(int(px - rx - 3), int(px + rx + 4)):
            d = ((x - px) / rx) ** 2 + ((y - py) / ry) ** 2
            if d <= 1:
                inner = ((x - px) / rx) ** 2 + ((y - 1.6 - py) / ry) ** 2
                if inner > 1:
                    c = MOUNT[1] if y < py - ry + 1.5 else LAVA[0]       # Rueckwand
                else:
                    v = math.sin(x * 0.8 + y * 1.3) + math.sin(x * 0.31 - y)
                    c = LAVA[4] if v > 1.0 else LAVA[3] if v > -0.2 else LAVA[2]
                    if abs(v - 0.4) < 0.12:
                        c = LAVA[5]
                p.put(x, y, c)
            elif d <= 1.45:
                c = GROUND[0] if d <= 1.12 and y < py else (LAVA[1] if d <= 1.12 else GROUND[4] if y < py else GROUND[2])
                p.put(x, y, c)
    # Glut-Schimmer um das Loch
    for y in range(int(py - ry - 4), int(py + ry + 5)):
        for x in range(int(px - rx - 5), int(px + rx + 6)):
            d = ((x - px) / (rx + 4)) ** 2 + ((y - py) / (ry + 3)) ** 2
            c = p.get(x, y) if 0 <= x < w and 0 <= y < h else None
            if c and 1.45 < ((x - px) / rx) ** 2 + ((y - py) / ry) ** 2 and d <= 1 and (x + y) % 2 == 0:
                p.put(x, y, tuple(int(c[i] * 0.7 + LAVA[1][i] * 0.3) for i in range(3)) + (255,))
    # Funken ueber dem Loch
    for (dx, dy) in ((-3, -6), (2, -8), (5, -5)):
        p.put(px + dx, int(py - ry) + dy, LAVA[4])

    # Rahmen
    for x in range(w):
        p.put(x, 0, FRAME)
        p.put(x, h - 1, FRAME)
    for y in range(h):
        p.put(0, y, FRAME)
        p.put(w - 1, y, FRAME)
    return p.im


def main():
    station = draw(50, 50, 3, 25, 31)
    wide = draw(82, 46, 7, 52, 28)
    if "--preview" in sys.argv:
        out = sys.argv[sys.argv.index("--preview") + 1]
        c = Image.new("RGBA", (50 + 82 + 6, 50), (40, 40, 40, 255))
        c.paste(station, (0, 0))
        c.paste(wide, (56, 0))
        c.resize((c.width * 5, c.height * 5), Image.NEAREST).save(out)
        return
    for name, im in (("level_preview_vulkan", station), ("level_preview_vulkan_wide", wide)):
        path = os.path.join(OUT_DIR, name + ".png")
        im.save(path)
        if not os.path.exists(path + ".meta"):
            write_strip_meta(path + ".meta", name, 1, im.width, im.height, 50, pivot=(0, 0))
        print("geschrieben:", path)


if __name__ == "__main__":
    main()
