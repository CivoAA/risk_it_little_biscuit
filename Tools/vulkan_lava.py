"""
Zeichnet die Lavaloecher der Vulkanwelt (Level 5, Szene Map_World4) - die
Hindernisse der Karte. Ein Loch ist flach im Boden: von schraeg oben sieht man
hinten die Felswand hinunter, vorne glimmt die Lava bis an die Lippe.

  Assets/Art/World-Objects/Vulkan/vulkan_lava_<name>.png   FRAMES Bilder nebeneinander, PPU 32
  Assets/Art/World-Objects/Vulkan/vulkan_lava.json         Masse, Pivot, Kollisionsumriss

Aufbau eines Bildes (von aussen nach innen):
  - Brandspur: halbtransparent dunkel, nah am Rand warm angeglueht (atmet mit)
  - Kruste: erkalteter Stein um das Loch, Licht oben links, Glut an der Kante
  - Rueckwand: oben dunkler Basalt mit Schichten, unten von der Lava angestrahlt
  - Lava: zaeh ziehende Schollen mit gluehenden Fugen, Blasen, die platzen
    und Funken werfen. Die Bewegung laeuft im Kreis -> die Schleife ist nahtlos.

Pivot = Mitte der Oeffnung. Die Kollision ist die Oeffnung, 2 px eingerueckt:
der Keks darf auf der Kruste stehen, aber nicht hinein. VulkanBuilder
(Tools -> Welt -> Vulkan einrichten) baut daraus PolygonCollider2D-Prefabs.

Aufruf aus dem Projektordner:
  python Tools/vulkan_lava.py                    Bilder + json schreiben
  python Tools/vulkan_lava.py --preview a.png    alle Loecher, Bild 0, vergroessert
  python Tools/vulkan_lava.py --gif a.gif NAME   ein Loch als Animation
Die .meta wird nur beim ersten Mal geschrieben (sonst verlieren Prefabs ihre Verweise).
"""

import json
import math
import os
import random
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from unity_meta import write_strip_meta  # noqa: E402
from vulkan_noise import fbm, value_noise  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "Assets", "Art", "World-Objects", "Vulkan")
INFO = os.path.join(OUT_DIR, "vulkan_lava.json")
PPU = 32
FRAMES = 12
FPS = 8

MARGIN = 12         # Platz fuer Kruste + Brandspur
SPARK_ROOM = 10     # oben extra Platz, damit Funken aus dem Loch fliegen koennen
COLLIDER_INSET = 2.0


def hx(s):
    s = s.lstrip("#")
    return np.array([int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), 255], dtype=np.float64)


# Lava von kalt nach heiss. Bewusst in wenigen Stufen - Pixelart, kein Verlauf.
LAVA = [hx("#5a160f"), hx("#8e2412"), hx("#c43c14"), hx("#e8661c"), hx("#fa9a2c"), hx("#ffcf55"), hx("#fff3b0")]
CRUST = [hx("#24131a"), hx("#3a1a18"), hx("#4e221a"), hx("#68301e"), hx("#8c4524")]
WALL = [hx("#1b151b"), hx("#251d24"), hx("#30262d"), hx("#3d2a2a"), hx("#5a2e22"), hx("#8a3c1e"), hx("#c45a20")]
RIM = [hx("#1a141a"), hx("#2a2229"), hx("#3a3038"), hx("#4c4148"), hx("#605359"), hx("#76676b")]
GLOW = hx("#ff7a2a")
SCORCH = hx("#140c10")

# name, halbe Breite/Hoehe der Oeffnung (px), Wandhoehe, Form, Gewicht beim Verteilen
POOLS = [
    ("klein_a", 24, 15, 6, "blob", 3.0),
    ("klein_b", 20, 14, 5, "blob", 3.0),
    ("mittel_a", 36, 22, 7, "blob", 2.2),
    ("mittel_b", 32, 24, 7, "lobes", 2.0),
    ("gross_a", 52, 31, 9, "blob", 1.2),
    ("gross_b", 48, 34, 9, "lobes", 1.0),
    ("spalt_a", 62, 10, 5, "fissure", 0.9),
    ("spalt_b", 46, 9, 5, "fissure", 0.9),
]


# --- Form --------------------------------------------------------------------------

def opening_mask(w, h, cx, cy, rx, ry, shape, seed):
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float64)
    dx = (xs + 0.5 - cx)
    dy = (ys + 0.5 - cy)
    ang = np.arctan2(dy / ry, dx / rx)
    rng = random.Random(seed)
    if shape == "fissure":
        u = dx / rx                                   # -1..1 entlang des Spalts
        ph = rng.random() * 6.28
        centre = ry * 0.9 * np.sin(u * 2.4 + ph) + ry * 0.5 * np.sin(u * 5.1 + ph * 2)
        half = ry * np.clip(1 - np.abs(u) ** 2.2, 0, 1) ** 0.55
        half = half * (1 + 0.35 * (value_noise(xs / 6.0, ys * 0, seed) - 0.5))
        return np.abs(dy - centre) < half
    # Radius ueber den Winkel verwackeln (periodisch -> keine Naht bei +-pi)
    t = (ang + math.pi) / (2 * math.pi) * 8.0
    wob = value_noise(t, np.zeros_like(t), seed, period=(8, 1)) - 0.5
    wob2 = value_noise(t * 2, np.zeros_like(t), seed + 1, period=(16, 1)) - 0.5
    r = 1 + 0.32 * wob + 0.12 * wob2
    if shape == "lobes":
        r = r + 0.16 * np.cos(3 * ang + rng.random() * 6.28)
    d = np.sqrt((dx / rx) ** 2 + (dy / ry) ** 2)
    return d < r


def distance_outside(mask):
    """Abstand jedes Pixels zur Oeffnung (0 innen)."""
    h, w = mask.shape
    inner = mask & ~(np.roll(mask, 1, 0) & np.roll(mask, -1, 0) & np.roll(mask, 1, 1) & np.roll(mask, -1, 1))
    by, bx = np.nonzero(inner)
    ys, xs = np.mgrid[0:h, 0:w]
    d = np.full((h, w), 1e9)
    for k in range(0, len(bx), 256):
        sx = bx[k:k + 256][None, None, :]
        sy = by[k:k + 256][None, None, :]
        dd = np.sqrt((xs[..., None] - sx) ** 2 + (ys[..., None] - sy) ** 2).min(axis=2)
        d = np.minimum(d, dd)
    d[mask] = 0
    return d


def distance_inside(mask):
    return distance_outside(~mask)


def shifted_down(mask, k):
    """mask an (x, y - k): True, wenn k px weiter oben noch Oeffnung ist."""
    out = np.zeros_like(mask)
    out[k:] = mask[:-k] if k > 0 else mask
    return out


# --- Zeichnen -------------------------------------------------------------------------

def ramp(pal, v):
    v = np.clip(v, 0, 0.9999) * len(pal)
    return np.array(pal)[v.astype(int)]


class Pool:
    def __init__(self, name, rx, ry, wall, shape, weight):
        self.name, self.rx, self.ry, self.wall, self.shape, self.weight = name, rx, ry, wall, shape, weight
        self.seed = sum(ord(c) * (i + 3) for i, c in enumerate(name))
        # Erst grosszuegig zeichnen, dann auf die echte Oeffnung + Rand zuschneiden.
        # Der Pivot (Mitte) bleibt auf ganzen Pixeln.
        bw, bh = 2 * (int(rx * 1.6) + MARGIN + 4), 2 * (int(ry * 3) + MARGIN + SPARK_ROOM + 4)
        big = opening_mask(bw, bh, bw / 2, bh / 2, rx, ry, shape, self.seed)
        ys, xs = np.nonzero(big)
        pad = MARGIN + 2
        x0, x1 = xs.min() - pad, xs.max() + 1 + pad
        y0, y1 = ys.min() - pad - SPARK_ROOM, ys.max() + 1 + pad
        self.open = big[y0:y1, x0:x1]
        self.h, self.w = self.open.shape
        self.cx = bw / 2 - x0
        self.cy = bh / 2 - y0
        self.out = distance_outside(self.open)
        # Rueckwand: Pixel, ueber denen innerhalb der Wandhoehe die Oeffnung endet
        self.lava = self.open & shifted_down(self.open, wall)
        self.wallm = self.open & ~self.lava
        self.lava_in = distance_inside(self.lava)    # Abstand zum Lavarand
        # Wandtiefe: wie weit ist der obere Rand weg (1 = ganz unten an der Wand)
        depth = np.zeros(self.open.shape)
        for k in range(1, wall + 1):
            depth = np.where(self.wallm & ~shifted_down(self.open, k) & (depth == 0), k, depth)
        self.depth = depth
        rng = random.Random(self.seed)
        self.rim_w = 3.2 + (rx + ry) / 36.0
        self.rim_noise = value_noise(np.mgrid[0:self.h, 0:self.w][1] / 3.0,
                                     np.mgrid[0:self.h, 0:self.w][0] / 3.0, self.seed + 5)
        self.bubbles = self.make_bubbles(rng)
        # Helligkeitsstufen + Schollenanteil aus Bild 0 ableiten
        ys_, xs_ = np.mgrid[0:self.h, 0:self.w].astype(np.float64)
        heat = fbm((xs_ + 2.4) / 16.0, ys_ * 1.7 / 16.0, self.seed + 21, octaves=2)[self.lava]
        self.heat_q = np.quantile(heat, [0.08, 0.3, 0.68, 0.9])
        c = (fbm((xs_ + 2.4) / 12.0 + 40, ys_ * 1.7 / 12.0, self.seed + 41, octaves=1) * 0.85
             + value_noise((xs_ + 2.4) / 3.0, ys_ * 1.7 / 3.0, self.seed + 43) * 0.15)[self.lava]
        self.plate_q = float(np.quantile(c, 0.86))

    def make_bubbles(self, rng):
        ys, xs = np.nonzero(self.lava & (self.lava_in >= 3))
        if len(xs) == 0:
            return []
        n = max(2, int(len(xs) / 260))
        out = []
        for i in range(n):
            k = rng.randrange(len(xs))
            out.append((int(xs[k]), int(ys[k]), (i * FRAMES // n + rng.randint(0, 2)) % FRAMES,
                        1 if rng.random() < 0.6 else 2))
        return out

    # -- einzelne Ebenen --
    def frame(self, f):
        h, w = self.open.shape
        img = np.zeros((h, w, 4))
        a = f / FRAMES * 2 * math.pi
        flicker = 0.5 + 0.5 * math.sin(a)            # atmet einmal pro Schleife
        ys, xs = np.mgrid[0:h, 0:w].astype(np.float64)

        # 1. Brandspur + Glut-Schimmer ausserhalb der Kruste
        halo = (self.out > 0) & (self.out <= self.rim_w + 8)
        k = np.clip((self.out - self.rim_w) / 8.0, 0, 1)
        bayer = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0
        dith = bayer[(ys.astype(int) % 4), (xs.astype(int) % 4)]
        scorch_a = np.where(k < 0.35, 110, np.where(k < 0.7, 70, 34))
        keep = halo & ((1 - k) > dith * 0.9)
        glow_mix = np.clip(0.75 - k, 0, 1) * (0.6 + 0.4 * flicker)
        col = SCORCH * (1 - glow_mix[..., None]) + GLOW * glow_mix[..., None]
        img[keep, :3] = col[keep, :3]
        img[keep, 3] = scorch_a[keep] + 60 * glow_mix[keep]

        # 2. Kruste: einzelne Brocken rund ums Loch, jeder mit Licht oben und
        #    Schatten unten - so wirkt der Rand aufgeworfen statt aufgemalt.
        rim = (self.out > 0) & (self.out <= self.rim_w + (self.rim_noise - 0.5) * 2.2)
        ang = np.arctan2((ys + 0.5 - self.cy) / self.ry, (xs + 0.5 - self.cx) / self.rx)
        k_count = max(8, int((self.rx + self.ry) / 4.5))
        jag = value_noise(xs / 3.0, ys / 3.0, self.seed + 77) * 0.9
        chunk = np.floor((ang + math.pi) / (2 * math.pi) * k_count + jag).astype(int) % k_count
        # zweite Reihe Brocken weiter aussen
        chunk = chunk + np.where(self.out > self.rim_w * 0.55 + jag, 100, 0)
        crng = random.Random(self.seed + 4)
        base_tone = {c: crng.choice([2, 3, 3, 4]) for c in np.unique(chunk)}
        tone = np.vectorize(base_tone.get)(chunk).astype(float)
        up = np.roll(chunk, 1, 0)
        dn = np.roll(chunk, -1, 0)
        lf = np.roll(chunk, 1, 1)
        up_rim = np.roll(rim, 1, 0)
        dn_rim = np.roll(rim, -1, 0)
        tone += np.where(~up_rim | (up != chunk) | (lf != chunk), 1, 0)
        tone -= np.where(~dn_rim | (dn != chunk), 1, 0)
        tone = np.clip(tone, 1, 5)
        rimcol = np.array(RIM)[tone.astype(int)]
        img[rim] = rimcol[rim]
        # Kruste aussen mit dunkler Kontur abschliessen
        outline = (self.out > 0) & ~rim & (np.roll(rim, 1, 0) | np.roll(rim, -1, 0) | np.roll(rim, 1, 1) | np.roll(rim, -1, 1))
        img[outline] = np.array([RIM[0][0], RIM[0][1], RIM[0][2], 255])
        # Glut auf der Kruste: die Lava strahlt die Innenkante an, vorne staerker
        near = ys + 0.5 > self.cy
        lip = rim & (self.out <= 1.5)
        img[lip & ~near] = RIM[0]
        glowlip = LAVA[2] * (0.75 + 0.25 * flicker) + RIM[3] * (0.25 - 0.25 * flicker)
        img[lip & near] = glowlip
        warm = rim & (self.out > 1.5) & (self.out <= 3.0)
        tint = np.where(near, 0.42, 0.22)[..., None] * (0.8 + 0.2 * flicker)
        img[warm] = (img * (1 - tint) + LAVA[2] * tint)[warm]
        img[rim, 3] = 255

        # 3. Rueckwand: Basalt in Schichten, unten von der Lava angestrahlt
        if self.wallm.any():
            d = self.depth / self.wall                 # 0 oben .. 1 unten
            row = self.depth.astype(int)
            band = (row + (value_noise(xs / 4.0, ys * 0, self.seed + 3) * 2).astype(int)) % 3
            idx = 1 + band % 2 + np.where(d > 0.45, 1, 0)
            joint = (value_noise(xs / 2.3, row * 1.0, self.seed + 11) > 0.7)
            idx = np.where(joint, idx - 1, idx)
            glow = (d - 0.6) / 0.4 * (2.2 + 1.2 * flicker)
            idx = np.maximum(idx, np.where(d > 0.6, 3 + glow, 0)).astype(int)
            idx = np.clip(idx, 0, 6)
            wc = np.array(WALL)[idx]
            img[self.wallm] = wc[self.wallm]
            img[self.wallm & (self.depth == 1)] = WALL[0]

        # 4. Lava: orange Grund, gluehende Adern, zaehe dunkle Schollen
        if self.lava.any():
            r = 2.4
            qx = xs + r * math.cos(a)
            qy = ys * 1.7 + r * math.sin(a)
            heat = fbm(qx / 16.0, qy / 16.0, self.seed + 21, octaves=2)
            v = heat + 0.03 * flicker - np.clip(2.0 - self.lava_in, 0, 2) * 0.04
            # Stufen nach den Anteilen dieses Lochs - jedes Loch gleich hell
            q = self.heat_q
            col = np.where((v < q[1])[..., None], LAVA[2], np.where((v < q[2])[..., None], LAVA[3], LAVA[4]))
            col = np.where((v > q[3])[..., None], LAVA[5], col)
            col = np.where((v < q[0])[..., None], LAVA[1], col)
            # Adern = Hoehenlinien eines zweiten Rauschens, laufen gegenlaeufig
            vn = fbm((xs - r * math.cos(a)) / 10.0, (ys * 1.7 - r * math.sin(a)) / 10.0, self.seed + 31, octaves=2)
            vein = np.abs(vn - 0.5) < 0.03
            vein_soft = (np.abs(vn - 0.5) < 0.065) & ~vein
            col = np.where(vein_soft[..., None], np.maximum(col, LAVA[4]), col)
            col = np.where(vein[..., None], LAVA[5], col)
            hot = vein & (heat > 0.55)
            col = np.where(hot[..., None], LAVA[6], col)
            # Schollen
            c = fbm(qx / 12.0 + 40, qy / 12.0, self.seed + 41, octaves=1) * 0.85                 + value_noise(qx / 3.0, qy / 3.0, self.seed + 43) * 0.15
            plate = c > self.plate_q
            seam = (c > self.plate_q - 0.03) & ~plate
            col = np.where(seam[..., None], LAVA[5], col)
            pc = np.where((c > self.plate_q + 0.05)[..., None], CRUST[2], CRUST[3])
            col = np.where(plate[..., None], pc, col)
            ptop = plate & ~np.roll(plate, 1, 0)
            col[ptop] = CRUST[4]
            pbot = plate & ~np.roll(plate, -1, 0)
            col[pbot] = CRUST[0]
            # Uferkruste: erkaltet, mit heisser Fuge davor
            shore_noise = value_noise(xs / 2.5, ys / 2.5, self.seed + 51) > 0.42
            shore = (self.lava_in <= 1.2) & shore_noise
            col = np.where(shore[..., None], CRUST[2], col)
            hot_line = (self.lava_in > 1.2) & (self.lava_in <= 2.2) & shore_noise
            col = np.where(hot_line[..., None], LAVA[5], col)
            # Wandfuss: hier schlaegt die Lava an den Fels
            foot = self.lava & shifted_down(self.wallm, 1)
            col[foot] = LAVA[6] if flicker > 0.6 else LAVA[5]
            col = self.draw_bubbles(col, f)
            img[self.lava] = col[self.lava]
            img[self.lava, 3] = 255

        self.draw_sparks(img, f)
        return np.clip(img, 0, 255).astype(np.uint8)

    def draw_bubbles(self, col, f):
        h, w = self.open.shape
        for (bx, by, t0, size) in self.bubbles:
            age = (f - t0) % FRAMES
            if age > 5:
                continue

            def p(x, y, c):
                if 0 <= x < w and 0 <= y < h and self.lava[y, x]:
                    col[y, x] = c
            if age <= 2:
                rad = [0, 1, size][age]
                for dy in range(-rad - 1, rad + 2):
                    for dx in range(-rad - 1, rad + 2):
                        dd = (dx / (rad + 0.6)) ** 2 + (dy / (rad + 0.6)) ** 2
                        if dd <= 1:
                            p(bx + dx, by + dy, LAVA[5])
                        elif dd <= 1.9 and dy >= 0:
                            p(bx + dx, by + dy, LAVA[1])
                p(bx - (1 if rad else 0), by - (1 if rad else 0), LAVA[6])
            elif age == 3:
                for dx, dy in ((-2, 0), (2, 0), (-1, -1), (1, -1), (-1, 1), (1, 1), (0, 1)):
                    p(bx + dx, by + dy, LAVA[5])
                p(bx, by, LAVA[1])
            else:
                for dx, dy in ((-2, 0), (2, 0), (-1, 1), (1, 1)):
                    p(bx + dx, by + dy, LAVA[3 if age == 4 else 2])
        return col

    def draw_sparks(self, img, f):
        h, w = self.open.shape
        for (bx, by, t0, size) in self.bubbles:
            age = (f - t0) % FRAMES
            if not 3 <= age <= 6:
                continue
            k = age - 3
            for j, (sx, vy) in enumerate(((-1, 3), (1 + size, 4))):
                x = bx + sx * (1 + k // 2) * (1 if j else 1)
                y = by - 2 - vy * k + k * k // 2
                if 0 <= x < w and 0 <= y < h:
                    c = LAVA[6] if k == 0 else LAVA[5] if k == 1 else LAVA[4] if k == 2 else LAVA[3]
                    img[y, x, :3] = c[:3]
                    img[y, x, 3] = 255

    def collider(self):
        """Umriss der Oeffnung in Einheiten, Pivot = Mitte, y nach oben."""
        pts = []
        n = 28
        for i in range(n):
            ang = 2 * math.pi * i / n
            dx, dy = math.cos(ang), math.sin(ang)
            r = 0.0
            while True:
                x = self.cx + dx * (r + 0.5)
                y = self.cy + dy * (r + 0.5)
                ix, iy = int(x), int(y)
                if not (0 <= ix < self.w and 0 <= iy < self.h) or not self.open[iy, ix]:
                    break
                r += 0.5
            r = max(1.0, r - COLLIDER_INSET)
            pts.append([round(dx * r / PPU, 4), round(-dy * r / PPU, 4)])
        return pts

    def info(self):
        ys, xs = np.nonzero(self.open)
        return {
            "name": self.name, "frames": FRAMES, "fps": FPS,
            "frameW": self.w, "frameH": self.h,
            "pivotX": self.cx, "pivotY": self.h - self.cy,
            "halfW": round(max(abs(xs.min() - self.cx), abs(xs.max() + 1 - self.cx)) / PPU, 3),
            "halfH": round(max(abs(ys.min() - self.cy), abs(ys.max() + 1 - self.cy)) / PPU, 3),
            "weight": self.weight,
            "collider": self.collider(),
        }

    def strip(self):
        out = Image.new("RGBA", (self.w * FRAMES, self.h))
        for f in range(FRAMES):
            out.paste(Image.fromarray(self.frame(f), "RGBA"), (f * self.w, 0))
        return out


def pools():
    return [Pool(*p) for p in POOLS]


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    if "--preview" in sys.argv:
        out = sys.argv[sys.argv.index("--preview") + 1]
        ps = pools()
        sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
        import vulkan_boden
        sheet, _ = vulkan_boden.build()
        W, H = 520, 240
        bg = Image.new("RGBA", (W, H))
        for y in range(0, H, 32):
            for x in range(0, W, 32):
                i = ((x * 7 + y * 13) // 32) % 3
                bg.paste(sheet.crop((i * 32, 0, i * 32 + 32, 32)), (x, y))
        x, y, rowh = 4, 4, 0
        for p in ps:
            fr = Image.fromarray(p.frame(int(sys.argv[sys.argv.index("--preview") + 2]) if len(sys.argv) > sys.argv.index("--preview") + 2 else 0), "RGBA")
            if x + p.w > W:
                x, y, rowh = 4, y + rowh + 2, 0
            bg.alpha_composite(fr, (x, y))
            x += p.w + 2
            rowh = max(rowh, p.h)
        bg.resize((W * 3, H * 3), Image.NEAREST).save(out)
        return
    if "--gif" in sys.argv:
        out = sys.argv[sys.argv.index("--gif") + 1]
        name = sys.argv[sys.argv.index("--gif") + 2]
        p = next(q for q in pools() if q.name == name)
        import vulkan_boden
        sheet, _ = vulkan_boden.build()
        frames = []
        for f in range(FRAMES):
            bg = Image.new("RGBA", (p.w, p.h))
            for y in range(0, p.h, 32):
                for x in range(0, p.w, 32):
                    bg.paste(sheet.crop((0, 0, 32, 32)), (x, y))
            bg.alpha_composite(Image.fromarray(p.frame(f), "RGBA"))
            frames.append(bg.resize((p.w * 4, p.h * 4), Image.NEAREST).convert("RGB"))
        frames[0].save(out, save_all=True, append_images=frames[1:], duration=1000 // FPS, loop=0)
        return
    infos = []
    for p in pools():
        path = os.path.join(OUT_DIR, "vulkan_lava_%s.png" % p.name)
        p.strip().save(path)
        meta = path + ".meta"
        if not os.path.exists(meta):
            write_strip_meta(meta, "vulkan_lava_%s" % p.name, FRAMES, p.w, p.h, PPU,
                             pivot=(p.cx / p.w, 1 - p.cy / p.h))
        infos.append(p.info())
        print("geschrieben:", path, p.w, "x", p.h)
    with open(INFO, "w", encoding="utf-8", newline="\n") as fh:
        json.dump({"pools": infos}, fh, indent=1)


if __name__ == "__main__":
    main()
