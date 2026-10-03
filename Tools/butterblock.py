"""
Zeichnet das Butterblast-Geschoss im Stil der Gegner/Figuren: ein 48x48-
Butterblock (Form wie der Marshmallow-Wuerfel, aber ohne Gesicht), der im
Flug schmilzt und an den Seiten Butter verliert. Hinten zieht er eckige
Partikel wie der Feuerball.

Stilregeln (abgeschaut bei Bestiary/*, Chars/*):
  - eine warme, dunkle Kontur um alles, keine schwarzen Linien
  - wenige flaechige Toene pro Material, weiche Schattierung unten rechts
  - Licht oben links, ein klarer Glanzfleck

  Assets/Art/Waffen/fin_milch.png   8 x 160x96  PPU 64  (Prefab-Scale 2 -> 32 px/Einheit)

Der Dateiname bleibt (Prefab, Animation und .meta verweisen darauf). Die
Sprites fin_milch_0..7 haben ihren Pivot in der Blockmitte (x 122, y 48).

Flugrichtung zeigt im Bild nach RECHTS. BobaWeapon dreht das Sprite auf den
Flugwinkel und setzt flipY beim Flug nach links - die Oberseite bleibt oben.

Alles ist periodisch in f / FRAMES, die Animation laeuft nahtlos.

Aufruf aus dem Projektordner:  python Tools/butterblock.py [--preview pfad.png] [--dry]
"""

import math
import os
import random
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SHEET = os.path.join(ROOT, "Assets", "Art", "Waffen", "fin_milch.png")

FW, FH = 160, 96
FRAMES = 8
TAU = 2 * math.pi
CLEAR = (0, 0, 0, 0)

CX, CY = 122, 48          # Blockmitte (Pixelkanten), = Pivot
SIZE = 48
TOP_H = 11                # sichtbare Oberseite (3/4-Ansicht wie der Marshmallow)


def hx(s):
    s = s.lstrip("#")
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), 255)


OUTLINE = hx("#5a3218")
WHITE = hx("#ffffff")
HI = hx("#fffbe6")
TOP = hx("#fff3b8")
TOP2 = hx("#ffeaa0")
BODY = hx("#ffe080")
BODY2 = hx("#f9d06a")
SHADE = hx("#eeb954")
DEEP = hx("#d69a3e")

# Fluessige Butter: etwas satter als der Block
LQ_HI = hx("#fffbe6")
LQ = hx("#ffe48a")
LQ_MID = hx("#ffd25a")
LQ_DARK = hx("#efae3f")


def block_box(f):
    """Squash & Stretch: der Block federt im Flug leicht."""
    s = round(2 * math.sin(TAU * f / FRAMES))
    w, h = SIZE + s, SIZE - s
    x0 = CX - w // 2
    y0 = CY - h // 2
    return x0, y0, x0 + w - 1, y0 + h - 1


def rounded(x, y, x0, y0, x1, y1, r):
    if x < x0 or x > x1 or y < y0 or y > y1:
        return False
    cx = min(max(x, x0 + r), x1 - r)
    cy = min(max(y, y0 + r), y1 - r)
    return (x - cx) ** 2 + (y - cy) ** 2 <= r * r + 0.5


class Img:
    def __init__(self):
        self.px = {}

    def put(self, x, y, c):
        x, y = int(x), int(y)
        if 0 <= x < FW and 0 <= y < FH:
            self.px[(x, y)] = c


# ------------------------------------------------------------------ Fluessigkeit

def metaballs(balls):
    field = {}
    for y in range(FH):
        for x in range(FW):
            s = 0.0
            for (bx, by, r) in balls:
                dx, dy = x + 0.5 - bx, y + 0.5 - by
                if abs(dx) > r * 3 + 2 or abs(dy) > r * 3 + 2:
                    continue
                s += (r * r / (dx * dx + dy * dy + 0.3)) ** 1.5
            if s >= 1.0:
                field[(x, y)] = s
    return field


def liquid_balls(f, box):
    x0, y0, x1, y1 = box
    phase = f / FRAMES
    balls = []

    # Pfuetzchen an Ober- und Unterkante (dort laeuft die Butter ab)
    for side in (-1, 1):
        edge = y0 if side < 0 else y1
        for i in range(5):
            x = x1 - 10 - i * 6.5
            bulge = 0.6 + 0.25 * i + 0.5 * math.sin(i * 2.1 + TAU * phase)
            balls.append((x, edge + 0.5 + side * bulge, 2.6 + 0.15 * i))

    # dicke Tropfnasen unten: wachsen, Fahrtwind zieht sie schraeg nach hinten
    for (nx, off) in ((x0 + 32, 0.0), (x0 + 17, 0.5)):
        t = (phase + off) % 1.0
        length = 3 + 9 * t
        sweep = 5 * t
        for s in range(5):
            u = s / 4
            r = 3.0 - 1.1 * u + (1.6 * t if s == 4 else 0)
            balls.append((nx - sweep * u, y1 + 1 + length * u, r))

    # oben reisst der Wind die Schmelze nach hinten weg
    t = (phase + 0.3) % 1.0
    for s in range(5):
        u = s / 4
        r = 2.8 - 1.1 * u + (1.3 * t if s == 4 else 0)
        balls.append((x0 + 24 - 8 * t * u, y0 - (2 + 6 * t) * u, r))

    # abgerissene Tropfen: fliegen hinten seitlich weg und werden kleiner
    for side in (-1, 1):
        for k in range(3):
            t = (phase + k / 3 + (0.17 if side > 0 else 0)) % 1.0
            x = x0 + 10 - 50 * t
            y = (y0 - 2 if side < 0 else y1 + 4) + side * 12 * math.sqrt(t)
            balls.append((x, y, 3.6 * (1 - t) + 1.4))
    return balls


def draw_liquid(img, f, box, solid):
    cells = metaballs(liquid_balls(f, box))

    def out(x, y):
        return (x, y) not in cells and (x, y) not in solid

    for (x, y) in cells:
        if (x, y) in solid:
            continue
        c = LQ
        if out(x, y + 1) or out(x + 1, y):
            c = LQ_DARK
        elif out(x, y + 2) or out(x + 2, y):
            c = LQ_MID
        elif (out(x - 1, y - 1) or out(x - 2, y - 1)) and not out(x, y - 1):
            c = LQ_HI
        img.put(x, y, c)
    return set(cells)


# ------------------------------------------------------------------ Partikel

def draw_particles(img, f, box):
    """Eckige Butterflocken hinter dem Block (Sprache wie beim Feuerball)."""
    x0, y0, x1, y1 = box
    phase = f / FRAMES
    rng = random.Random(9)
    for i in range(14):
        off = rng.random()
        lane = rng.uniform(-18, 18)
        spread = rng.uniform(4, 10) * (1 if lane >= 0 else -1)
        t = (phase + off) % 1.0
        x = x0 - 2 - 70 * t
        y = CY + lane + spread * t
        size = 4 if t < 0.3 else (3 if t < 0.6 else (2 if t < 0.85 else 1))
        col = (HI, TOP, BODY, SHADE)[min(3, int(t * 4))]
        for dy in range(size):
            for dx in range(size):
                img.put(round(x) + dx, round(y) + dy, col)


# ------------------------------------------------------------------ Block

def block_mask(box):
    x0, y0, x1, y1 = box
    return {(x, y) for y in range(y0, y1 + 1) for x in range(x0, x1 + 1)
            if rounded(x, y, x0, y0, x1, y1, 7)}


def draw_block(img, f, box, mask):
    x0, y0, x1, y1 = box
    face_y = y0 + TOP_H
    for (x, y) in mask:
        if y < face_y:
            c = TOP
            if y >= face_y - 3:
                c = TOP2
        else:
            c = BODY
            if x >= x1 - 5 or y >= y1 - 6:
                c = BODY2
            if (x >= x1 - 2 and y >= face_y + 4) or y >= y1 - 2:
                c = SHADE
            if x >= x1 - 3 and y >= y1 - 3:
                c = DEEP
        img.put(x, y, c)

    # innere Lichtkante oben links (wie beim Marshmallow)
    for (x, y) in mask:
        if (x - 1, y) not in mask and y < y1 - 8:
            img.put(x, y, HI)
        if (x, y - 1) not in mask and x < x1 - 8:
            img.put(x, y, HI)
    # Kante Oberseite -> Front
    for x in range(x0 + 2, x1 - 1):
        if (x, face_y) in mask:
            img.put(x, face_y, BODY2 if x < x1 - 3 else SHADE)
    # Glanzfleck auf der Oberseite
    for (gx, gy, w) in ((x0 + 6, y0 + 3, 7), (x0 + 6, y0 + 4, 4), (x0 + 15, y0 + 3, 2)):
        for d in range(w):
            if (gx + d, gy) in mask:
                img.put(gx + d, gy, WHITE)


def front_drips(f, box):
    x0, y0, x1, y1 = box
    phase = f / FRAMES
    # (x, Grundlaenge, Phase, Breite)
    spec = [(x0 + 8, 15, 0.0, 6), (x0 + 22, 9, 0.35, 5), (x1 - 8, 19, 0.6, 6)]
    return [(x, base + 3 * math.sin(TAU * (phase + off)), w) for (x, base, off, w) in spec]


def draw_front_drips(img, f, box, mask):
    x0, y0, x1, y1 = box
    face_y = y0 + TOP_H
    for (xc, length, w) in front_drips(f, box):
        bot = face_y + length
        r = w / 2 + 0.6
        cells = set()
        for y in range(face_y - 1, int(bot + r) + 1):
            for x in range(int(xc - r) - 1, int(xc + r) + 2):
                stem = y <= bot and abs(x + 0.5 - xc) <= w / 2
                head = (x + 0.5 - xc) ** 2 + (y + 0.5 - bot) ** 2 <= r * r
                if (stem or head) and (x, y) in mask:
                    cells.add((x, y))
        for (x, y) in cells:
            c = LQ_MID
            if (x - 1, y) not in cells:
                c = LQ_HI
            elif (x - 2, y) not in cells:
                c = LQ
            elif (x + 1, y) not in cells or (x, y + 1) not in cells:
                c = LQ_DARK
            img.put(x, y, c)
        # Schatten der Nase auf dem Block
        for (x, y) in cells:
            for q in ((x + 1, y + 1), (x, y + 1), (x + 1, y)):
                if q not in cells and q in mask and q[1] > face_y:
                    img.put(q[0], q[1], SHADE)
        # Glanzpunkt im Tropfenkopf
        img.put(int(xc - 1), int(bot), WHITE)


# ------------------------------------------------------------------ Bild

def frame(f):
    box = block_box(f)
    mask = block_mask(box)
    back = Img()
    draw_particles(back, f, box)
    liq = Img()
    liq_cells = draw_liquid(liq, f, box, mask)
    blk = Img()
    draw_block(blk, f, box, mask)
    draw_front_drips(blk, f, box, mask)

    final = {}
    for layer in (back, liq, blk):
        final.update(layer.px)
    solid = set(liq_cells) | mask

    img = Image.new("RGBA", (FW, FH), CLEAR)
    px = img.load()
    for p, c in final.items():
        px[p] = c
    # eine Kontur um Block + Fluessigkeit (Partikel bleiben ohne, wie beim Feuerball)
    for (x, y) in solid:
        for (ox, oy) in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            q = (x + ox, y + oy)
            if q in solid or not (0 <= q[0] < FW and 0 <= q[1] < FH):
                continue
            px[q] = OUTLINE
    return img


def main():
    sheet = Image.new("RGBA", (FW * FRAMES, FH), CLEAR)
    for f in range(FRAMES):
        sheet.paste(frame(f), (f * FW, 0))
    if "--dry" not in sys.argv:
        sheet.save(SHEET)
        print("geschrieben:", SHEET)
    if "--preview" in sys.argv:
        preview = sys.argv[sys.argv.index("--preview") + 1]
        cols, sc = 2, 3
        rows = (FRAMES + cols - 1) // cols
        bg = Image.new("RGBA", (FW * cols * sc, FH * rows * sc), (54, 74, 52, 255))
        frames = []
        for f in range(FRAMES):
            fr = sheet.crop((f * FW, 0, f * FW + FW, FH))
            bg.alpha_composite(fr.resize((FW * sc, FH * sc), Image.NEAREST),
                               ((f % cols) * FW * sc, (f // cols) * FH * sc))
            g = Image.new("RGBA", (FW, FH), (54, 74, 52, 255))
            g.alpha_composite(fr)
            frames.append(g.resize((FW * 3, FH * 3), Image.NEAREST).convert("RGB"))
        bg.save(preview)
        gif = preview.rsplit(".", 1)[0] + ".gif"
        frames[0].save(gif, save_all=True, append_images=frames[1:], duration=83, loop=0)
        print("Vorschau:", preview, gif)


if __name__ == "__main__":
    main()
