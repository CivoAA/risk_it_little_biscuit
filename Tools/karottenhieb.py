"""
Zeichnet die Hieb-Animation des Karottenschwerts (SwordSlash, Startwaffe des
Zwiebelritters).

  Assets/Resources/Weapons/carrot_slash.png            6 x 96x192  PPU 32  Pivot links mittig
  Assets/Resources/Weapons/carrot_slash_finisher.png   7 x 96x192  PPU 32  dritter Hieb einer Serie:
                                                       dicker, golden, Funkeln, mehr Stuecke

Bild zeigt nach rechts (+x), Drehpunkt = Spieler. Der Bogen hat Radius 80 px
= 2.5 Tiles bei PPU 32 - genau die Tiefe (range) des Hiebs, die Breite von
5 Tiles ist der Durchmesser. SwordSlash skaliert per SlashArcRadius auf die
echte Trefferflaeche, der Rand drumherum ist nur Platz fuer Spritzer.

Ablauf (Vorhand): das Schwert fegt von oben (+y) nach unten, dahinter eine
orange Wischspur; zum Schluss zerfaellt die Spur in Karottenstueckchen und
Blaetter. Die Rueckhand spiegelt SwordSlash per flipY.

.meta entsteht nur beim ersten Lauf (danach bleiben die Sprite-IDs).

Aufruf aus dem Projektordner:  python Tools/karottenhieb.py [--preview pfad.png]
"""

import math
import os
import random
import sys

from PIL import Image

from unity_meta import write_strip_meta

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Resources", "Weapons")

W, H = 96, 192          # Leinwand, Pivot bei (0, H/2)
R = 80.0                # Bogenradius in px = 2.5 Tiles
PPU = 32
CLEAR = (0, 0, 0, 0)


def hx(s, a=255):
    s = s.lstrip("#")
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), a)


def mix(c1, c2, t):
    t = max(0.0, min(1.0, t))
    return tuple(int(round(c1[i] + (c2[i] - c1[i]) * t)) for i in range(4))


LINE = hx("#3a1410")
O_D, O, O_L, O_H = hx("#b2431c"), hx("#e8702a"), hx("#f89a3e"), hx("#ffc970")
CREAM = hx("#fff4cf")
G_D, G, G_L = hx("#2c6a2c"), hx("#4ea83c"), hx("#93d95c")
GOLD, GOLD_L = hx("#ffc94a"), hx("#ffe89a")

# Bayer 4x4 fuer gestufte Transparenz ohne Halbtoene
BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]


def dither(x, y, amount):
    return amount * 16 > BAYER[y % 4][x % 4] + 0.5


class Px:
    def __init__(self):
        self.img = Image.new("RGBA", (W, H), CLEAR)
        self.px = self.img.load()

    def set(self, x, y, c):
        if 0 <= x < W and 0 <= y < H:
            self.px[x, y] = c

    def get(self, x, y):
        if 0 <= x < W and 0 <= y < H:
            return self.px[x, y]
        return CLEAR


def to_local(x, y):
    """Pixel -> Koordinaten relativ zum Pivot, y nach oben."""
    return x + 0.5, (H / 2.0) - (y + 0.5)


# ----------------------------------------------------------------------------
#  Wischspur
# ----------------------------------------------------------------------------

def smear(p, front, span, fade, finisher=False):
    """Sichelfoermige Spur hinter dem Schwert.

    front: Winkel der Klinge (Grad, 0 = rechts, +90 = oben)
    span:  wie weit die Spur nach hinten (= zu groesseren Winkeln) reicht
    fade:  0..1, die Spur wird duenner und zerfaellt vom Ende her
    """
    for y in range(H):
        for x in range(W):
            lx, ly = to_local(x, y)
            r = math.hypot(lx, ly)
            if r > R + 0.5 or r < 20:
                continue
            a = math.degrees(math.atan2(ly, lx))
            d = (a - front) / span if span > 0 else 2.0
            if d < 0.0 or d > 1.0 - fade * 0.55:
                continue
            # vorne dick, nach hinten spitz auslaufend, aussen am Radius
            thick = (42.0 if finisher else 30.0) * (1.0 - d) ** 0.75 * (1.0 - fade * 0.75) + 1.0
            r_out = R
            s = (r_out - r) / thick          # 0 = Aussenkante, 1 = Innenkante
            if s > 1.0:
                continue
            if s > 0.8 and not dither(x, y, 0.5):
                continue                     # weiche Innenkante
            if fade > 0.5 and not dither(x, y, 1.6 - fade * 1.3):
                continue
            if s < 0.1 or r > R - 1.2:
                col = CREAM
            elif finisher and s < 0.22:
                col = GOLD_L
            elif finisher and s < 0.42:
                col = GOLD
            elif s < 0.26:
                col = O_H
            elif s < 0.6:
                col = O_L
            else:
                col = O
            if d > 0.7 and col == O:
                col = O_L                    # Schweif heller, luftiger
            # Ringkerben wie bei einer Karotte, quer zur Spur
            if 0.08 < d < 0.65 and 0.3 < s < 0.85:
                if (a - front) % 16.0 < 1.5:
                    col = O_D
            p.set(x, y, col)

    if finisher and span > 40:
        # zweiter, innerer Schwung: duenne helle Sichel, eilt etwas hinterher
        for y in range(H):
            for x in range(W):
                lx, ly = to_local(x, y)
                r = math.hypot(lx, ly)
                a = math.degrees(math.atan2(ly, lx))
                d = (a - front - 12.0) / (span * 0.8) if span > 0 else 2.0
                if d < 0.0 or d > 1.0 - fade * 0.7:
                    continue
                rr = R * 0.5
                th = 3.5 * (1.0 - d) * (1.0 - fade) + 0.6
                if abs(r - rr) > th:
                    continue
                if fade > 0.4 and not dither(x, y, 1.4 - fade * 1.2):
                    continue
                p.set(x, y, CREAM if abs(r - rr) < th * 0.45 else GOLD_L)


# ----------------------------------------------------------------------------
#  Karottenschwert
# ----------------------------------------------------------------------------

def carrot_sword(p, angle):
    """Karotte als Klinge, Kraut als Griff, vom Pivot nach aussen."""
    ca, sa = math.cos(math.radians(angle)), math.sin(math.radians(angle))
    r0, r1 = 22.0, R - 1.0          # Klinge von hier bis zur Spitze
    solid = {}
    for y in range(H):
        for x in range(W):
            lx, ly = to_local(x, y)
            u = lx * ca + ly * sa           # entlang der Klinge
            v = -lx * sa + ly * ca          # quer, + = in Rueckrichtung (oben bei Vorhand)
            if r0 - 1 <= u <= r1:
                t = (u - r0) / (r1 - r0)
                hw = 0.6 + 7.0 * (1.0 - t) ** 0.8
                if abs(v) <= hw:
                    s = v / hw
                    col = O
                    if s > 0.35:
                        col = O_L
                    if s > 0.7 and t < 0.85:
                        col = O_H
                    if s < -0.45:
                        col = O_D
                    # Ringe der Karotte
                    for rt in (0.14, 0.3, 0.47, 0.63, 0.78):
                        if abs(t - rt) * (r1 - r0) < 0.7 and s < 0.3:
                            col = O_D
                    solid[(x, y)] = col
                    continue
            # Kraut: drei Blaetter, die zum Spieler hin auffaechern
            if 2.0 <= u <= r0 + 1.0:
                for off, length, width in ((0.0, 19.0, 3.6), (-6.0, 15.0, 3.0), (6.0, 15.0, 3.0)):
                    tt = (r0 + 1.0 - u) / length
                    if 0.0 <= tt <= 1.0:
                        centre = off * tt
                        hw = width * math.sin(math.pi * min(1.0, tt * 1.1 + 0.08))
                        if abs(v - centre) <= hw:
                            q = (v - centre) / max(hw, 0.01)
                            col = G if q < 0.3 else G_L
                            if q < -0.5:
                                col = G_D
                            if abs(q) < 0.18 and tt > 0.2:
                                col = G_D              # Blattrippe
                            solid[(x, y)] = col
                            break
    for (x, y), c in solid.items():
        p.set(x, y, c)
    # Kontur
    for (x, y) in solid:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (x + dx, y + dy)
            if n not in solid:
                p.set(n[0], n[1], LINE)
    # Glanzpunkt an der Schneide
    tipx = int(math.floor((r1 - 10) * ca))
    tipy = int(math.floor(H / 2.0 - (r1 - 10) * sa))
    if (tipx, tipy) in solid:
        p.set(tipx, tipy, CREAM)


# ----------------------------------------------------------------------------
#  Spritzer
# ----------------------------------------------------------------------------

def chunks(p, seed, front, span, progress, count=8, reach=13):
    """Karottenwuerfel und Blaetter, die vom Bogen weg nach aussen fliegen."""
    rng = random.Random(seed)
    for i in range(count):
        a = front + (0.08 + 0.8 * i / (count - 1.0) + rng.uniform(-0.04, 0.04)) * span
        r = R - 4 + progress * rng.uniform(6, reach)
        x = int(round(r * math.cos(math.radians(a))))
        y = int(round(H / 2.0 - r * math.sin(math.radians(a))))
        leaf = i % 3 == 1
        size = 3 if progress < 0.6 else 2
        body, hi, shade = (G, G_L, G_D) if leaf else (O, O_H, O_D)
        for dy in range(size):
            for dx in range(size):
                c = body
                if dx == 0 and dy == 0:
                    c = hi
                elif dx == size - 1 and dy == size - 1:
                    c = shade
                p.set(x + dx, y + dy, c)
        for dx in range(-1, size + 1):
            for dy in range(-1, size + 1):
                if (dx in (-1, size) or dy in (-1, size)) and not (dx in (-1, size) and dy in (-1, size)):
                    if p.get(x + dx, y + dy)[3] == 0:
                        p.set(x + dx, y + dy, LINE)


def sparkles(p, front, span, t):
    """Vierzackiges Funkeln auf der Aussenkante (Finisher). t 0..1 = Lebenslauf."""
    for k, (pos, delay) in enumerate(((0.15, 0.0), (0.45, 0.15), (0.75, 0.3), (0.3, 0.45))):
        life = (t - delay) / 0.55
        if life < 0 or life > 1:
            continue
        a = math.radians(front + pos * span)
        cx = int(round((R + 2) * math.cos(a)))
        cy = int(round(H / 2.0 - (R + 2) * math.sin(a)))
        arm = 4 if life < 0.4 else 3 if life < 0.7 else 1
        for dx, dy in ((1, 1), (-1, 1), (1, -1), (-1, -1)):
            if arm >= 3:
                p.set(cx + dx, cy + dy, GOLD)
        for i in range(-arm, arm + 1):
            c = CREAM if abs(i) <= 1 else GOLD_L if abs(i) <= 2 else GOLD
            p.set(cx + i, cy, c)
            p.set(cx, cy + i, c)
        p.set(cx, cy, (255, 255, 255, 255))


# ----------------------------------------------------------------------------
#  Frames
# ----------------------------------------------------------------------------

# (Klingenwinkel, Spurlaenge, Aufloesung, Schwert?, Spritzer-Fortschritt)
FRAMES = [
    (64, 24, 0.0, True, None),
    (26, 62, 0.0, True, None),
    (-16, 100, 0.0, True, None),
    (-56, 136, 0.1, True, 0.3),
    (-80, 156, 0.45, False, 0.65),
    (-84, 160, 0.8, False, 1.0),
]


# Finisher: ein Bild mehr, laengere Spur, mehr Stuecke, Funkeln
FINISHER = [
    (66, 26, 0.0, True, None),
    (30, 66, 0.0, True, None),
    (-10, 106, 0.0, True, 0.0),
    (-50, 140, 0.0, True, 0.25),
    (-78, 162, 0.2, True, 0.5),
    (-86, 168, 0.5, False, 0.75),
    (-88, 170, 0.82, False, 1.0),
]


def frame(f):
    p = Px()
    front, span, fade, sword, spray = FRAMES[f]
    smear(p, front, span, fade)
    if spray is not None:
        chunks(p, 7, front, span, spray)
    if sword:
        carrot_sword(p, front)
    return p.img


def finisher_frame(f):
    p = Px()
    front, span, fade, sword, spray = FINISHER[f]
    smear(p, front, span, fade, finisher=True)
    if spray is not None:
        chunks(p, 11, front, span, spray, count=14, reach=16)
    if sword:
        carrot_sword(p, front)
    sparkles(p, front, span, f / (len(FINISHER) - 1.0))
    return p.img


def write_sheet(name, fn, n):
    img = Image.new("RGBA", (W * n, H), CLEAR)
    for f in range(n):
        img.paste(fn(f), (f * W, 0))
    path = os.path.join(OUT, name + ".png")
    img.save(path)
    if not os.path.exists(path + ".meta"):
        write_strip_meta(path + ".meta", name, n, W, H, PPU, (0.0, 0.5))
    print("geschrieben:", os.path.relpath(path, ROOT))
    return img


def main():
    preview = sys.argv[sys.argv.index("--preview") + 1] if "--preview" in sys.argv else None
    os.makedirs(OUT, exist_ok=True)
    normal = write_sheet("carrot_slash", frame, len(FRAMES))
    fin = write_sheet("carrot_slash_finisher", finisher_frame, len(FINISHER))

    if preview:
        pv = Image.new("RGBA", (fin.width + 8, 2 * H + 12), (74, 96, 64, 255))
        pv.alpha_composite(normal, (4, 4))
        pv.alpha_composite(fin, (4, H + 8))
        pv.resize((pv.width * 2, pv.height * 2), Image.NEAREST).save(preview)


if __name__ == "__main__":
    main()
