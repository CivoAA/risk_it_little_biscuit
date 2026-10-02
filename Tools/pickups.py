"""
Zeichnet die Pickups, die Gegner fallen lassen (PickUps.cs / PickUpManager).

  Assets/Resources/PickUps/pickup_heart.png         12 x 32x32  PPU 32  Pivot Mitte
  Assets/Resources/PickUps/pickup_heart_golden.png  12 x 32x32  PPU 32  goldenes Herz (+Max-Leben)
  Assets/Resources/PickUps/pickup_magnet.png        12 x 32x32  PPU 32  Magnet

Jeder Streifen ist eine Idle-Schleife (PickUps spielt sie mit 10 fps): das
Pickup schwebt 0-2 px auf und ab, der Schatten darunter wird dabei kleiner,
und einmal pro Runde laeuft ein Glanzstreifen schraeg drueber. Das goldene
Herz funkelt zusaetzlich, der Magnet knistert an den Polen.

Licht kommt von links oben; Konturfarbe ist ein dunkles Rotbraun wie bei den
Figuren, nicht Schwarz.

.meta entsteht nur beim ersten Lauf (danach bleiben die Sprite-IDs).

Aufruf aus dem Projektordner:  python Tools/pickups.py [--preview pfad.png]
"""

import math
import os
import sys

from PIL import Image

from unity_meta import write_strip_meta

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Resources", "PickUps")

S = 32          # Zellgroesse
FRAMES = 12
PPU = 32
CLEAR = (0, 0, 0, 0)
LIGHT = (-0.55, 0.65, 0.75)      # x rechts, y oben, z zum Betrachter


def hx(s, a=255):
    s = s.lstrip("#")
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), a)


def mix(c1, c2, t):
    t = max(0.0, min(1.0, t))
    return tuple(int(round(c1[i] + (c2[i] - c1[i]) * t)) for i in range(4))


def norm(v):
    l = math.sqrt(sum(c * c for c in v)) or 1.0
    return tuple(c / l for c in v)


L = norm(LIGHT)


# ---------------------------------------------------------------- Formen --

HEART = """
...XXXXX....XXXXX...
..XXXXXXX..XXXXXXX..
.XXXXXXXXXXXXXXXXXX.
XXXXXXXXXXXXXXXXXXXX
XXXXXXXXXXXXXXXXXXXX
XXXXXXXXXXXXXXXXXXXX
XXXXXXXXXXXXXXXXXXXX
.XXXXXXXXXXXXXXXXXX.
.XXXXXXXXXXXXXXXXXX.
..XXXXXXXXXXXXXXXX..
...XXXXXXXXXXXXXX...
....XXXXXXXXXXXX....
.....XXXXXXXXXX.....
......XXXXXXXX......
.......XXXXXX.......
........XXXX........
"""


def mask_from(text):
    rows = [r for r in text.strip("\n").split("\n")]
    return {(x, y) for y, r in enumerate(rows) for x, ch in enumerate(r) if ch == "X"}, len(rows[0]), len(rows)


def dist_to_edge(mask):
    """Euklidischer Abstand jedes Pixels zum naechsten Pixel ausserhalb."""
    xs = [p[0] for p in mask]
    ys = [p[1] for p in mask]
    outside = [(x, y) for x in range(min(xs) - 1, max(xs) + 2)
               for y in range(min(ys) - 1, max(ys) + 2) if (x, y) not in mask]
    return {p: min(math.hypot(p[0] - o[0], p[1] - o[1]) for o in outside) for p in mask}


def dome_normals(mask, depth):
    """Kissen-Normalen: Hoehe waechst vom Rand nach innen, oben abgeflacht."""
    d = dist_to_edge(mask)

    def h(p):
        if p not in d:
            return 0.0
        t = min(1.0, (d[p] - 0.5) / depth)
        return math.sqrt(max(0.0, 1.0 - (1.0 - t) ** 2)) * depth

    n = {}
    for (x, y) in mask:
        gx = h((x + 1, y)) - h((x - 1, y))
        gy = h((x, y + 1)) - h((x, y - 1))        # Bild-y zeigt nach unten
        n[(x, y)] = norm((-gx, gy, 2.0))
    return n


def heart_layer(pal):
    """Gibt {(x, y): Farbe} relativ zur linken oberen Ecke der Kontur zurueck."""
    mask, w, hgt = mask_from(HEART)
    normals = dome_normals(mask, 6.0)
    px = {}
    for p, nrm in normals.items():
        b = sum(a * c for a, c in zip(nrm, L))
        # leichter Verlauf von oben nach unten, damit die Spitze satter wird
        b -= (p[1] / hgt) * 0.18
        if b > 0.93:
            col = pal["hi"]
        elif b > 0.78:
            col = pal["light"]
        elif b > 0.55:
            col = pal["base"]
        elif b > 0.32:
            col = pal["mid"]
        else:
            col = pal["dark"]
        px[(p[0] + 1, p[1] + 1)] = col

    # Glanzlicht auf dem linken Bogen
    for q in [(4, 3), (5, 3), (4, 4), (3, 4), (3, 5), (7, 3)]:
        px[q] = pal["spec"]
    px[(8, 3)] = mix(pal["spec"], pal["hi"], 0.5)
    # Reflexlicht unten rechts an der Innenkante
    for q in [(17, 9), (16, 10), (15, 11), (14, 12)]:
        if q in px:
            px[q] = pal["rim"]
    return outline(px, pal["line"]), w + 2, hgt + 2


def magnet_layer():
    """Hufeisenmagnet, Oeffnung oben: rot lackiert, silberne Pole."""
    W, H = 20, 21
    cx, cy = W / 2.0, 10.5           # Mittelpunkt des Bogens
    r_in, r_out = 3.6, 9.6
    rm, hw = (r_in + r_out) / 2.0, (r_out - r_in) / 2.0
    red = {"spec": hx("ffffff"), "hi": hx("ff8f87"), "light": hx("f2584f"),
           "base": hx("d9303a"), "mid": hx("a81d33"), "dark": hx("6e1430")}
    steel = {"spec": hx("ffffff"), "hi": hx("f4f8ff"), "light": hx("d7e0ec"),
             "base": hx("aebccd"), "mid": hx("7f8ea3"), "dark": hx("566178")}
    pole_rows = 5

    px = {}
    for y in range(H):
        for x in range(W):
            fx, fy = x + 0.5, y + 0.5
            if fy < cy:
                dx = abs(fx - cx)
                if not (r_in <= dx <= r_out):
                    continue
                radial = (1.0 if fx > cx else -1.0, 0.0)
                r = dx
            else:
                r = math.hypot(fx - cx, fy - cy)
                if not (r_in <= r <= r_out):
                    continue
                radial = ((fx - cx) / r, -(fy - cy) / r)
            u = max(-1.0, min(1.0, (r - rm) / hw))
            nrm = norm((radial[0] * u, radial[1] * u, math.sqrt(max(0.0, 1.0 - u * u)) + 0.35))
            b = sum(a * c for a, c in zip(nrm, L))
            pal = steel if y < pole_rows else red
            if b > 0.95:
                col = pal["hi"]
            elif b > 0.8:
                col = pal["light"]
            elif b > 0.5:
                col = pal["base"]
            elif b > 0.22:
                col = pal["mid"]
            else:
                col = pal["dark"]
            if y == pole_rows and pal is red:
                col = mix(col, hx("3d1220"), 0.45)      # Fuge zwischen Lack und Pol
            px[(x + 1, y + 1)] = col

    # Glanzkante auf dem Lack und Lichtpunkte auf den Polen
    for q in [(3, 8), (3, 9), (3, 10), (3, 11), (4, 13)]:
        if q in px:
            px[q] = mix(px[q], red["spec"], 0.55)
    for q in [(3, 2), (3, 3), (15, 2)]:
        if q in px:
            px[q] = steel["spec"]
    return outline(px, hx("3d1424")), W + 2, H + 2


def outline(px, col):
    out = dict(px)
    for (x, y) in px:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            q = (x + dx, y + dy)
            if q not in px:
                out[q] = col
    return out


# ----------------------------------------------------------- Animation --

def bob(frame):
    """0..2 px hoch, weiche Sinusschleife ueber alle Bilder."""
    return int(round(1.0 - math.cos(frame / FRAMES * 2 * math.pi)))


def glint(px, line_col, frame, start=1, steps=5, strength=0.55):
    """Schraeger Glanzstreifen, der von links unten nach rechts oben wandert."""
    k = frame - start
    if not (0 <= k < steps):
        return px
    xs = [p[0] for p in px]
    ys = [p[1] for p in px]
    lo, hi = min(xs) + min(ys), max(xs) + max(ys)
    pos = lo + (hi - lo) * (k + 0.5) / steps
    out = dict(px)
    for (x, y), c in px.items():
        if c == line_col:
            continue
        d = abs((x + y) - pos)
        if d < 1.5:
            out[(x, y)] = mix(c, hx("ffffff"), strength * (1.0 if d < 0.8 else 0.5))
    return out


def shadow(img, cx, y, width):
    """Flacher Schatten aus zwei Zeilen, die obere 2 px schmaler."""
    for row, w in ((0, width - 2), (1, width)):
        x0 = int(round(cx - w / 2.0))
        for x in range(x0, x0 + w):
            put(img, x, y + row, (46, 22, 26, 85))


def star(img, x, y, size, core, arm):
    if size <= 0:
        return
    put(img, x, y, core)
    for i in range(1, size + 1):
        c = arm if i == size else core
        for q in ((x + i, y), (x - i, y), (x, y + i), (x, y - i)):
            put(img, *q, c)


def put(img, x, y, c):
    if 0 <= x < S and 0 <= y < S:
        img.putpixel((x, y), c)


def compose(layer, w, h, frame, line_col, extra=None, top=6):
    img = Image.new("RGBA", (S, S), CLEAR)
    ox = (S - w) // 2
    lift = bob(frame)
    shadow(img, S / 2.0, top + h - 1, w - 6 - lift * 2 + (w % 2))
    px = glint(layer, line_col, frame)
    for (x, y), c in px.items():
        put(img, ox + x, top + y - lift, c)
    if extra:
        extra(img, frame, ox, top - lift)
    return img


# --------------------------------------------------------------- Bilder --

RED = {"spec": hx("ffffff"), "hi": hx("ffb3b3"), "light": hx("ff6b72"), "base": hx("ec3a4b"),
       "mid": hx("c0233f"), "dark": hx("861a3a"), "rim": hx("d9455f"), "line": hx("4a1226")}
GOLD = {"spec": hx("ffffff"), "hi": hx("fff6bf"), "light": hx("ffe066"), "base": hx("ffc23a"),
        "mid": hx("e8921c"), "dark": hx("a85a17"), "rim": hx("f5a83a"), "line": hx("5a2a10")}


def golden_sparkles(img, frame, ox, oy):
    core, arm = hx("ffffff"), hx("fff1a0", 200)
    # zwei Funken, versetzt, wachsen auf und verschwinden
    for (x, y, phase) in [(ox + 19, oy + 1, 0), (ox + 1, oy + 12, 6)]:
        k = (frame - phase) % FRAMES
        size = [1, 2, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0][k]
        if k < 3:
            star(img, x, y, size, core, arm)


def magnet_sparks(img, frame, ox, oy):
    """Kleine blaue Funken ueber den Polen, abwechselnd links und rechts."""
    core, arm = hx("e8fbff"), hx("8fe3ff", 210)
    spots = {0: (ox + 4, oy - 2, 1), 1: (ox + 4, oy - 3, 2), 2: (ox + 4, oy - 4, 1),
             6: (ox + 17, oy - 2, 1), 7: (ox + 17, oy - 3, 2), 8: (ox + 17, oy - 4, 1)}
    if frame in spots:
        x, y, size = spots[frame]
        star(img, x, y, size, core, arm)


def build():
    heart, hw, hh = heart_layer(RED)
    gold, gw, gh = heart_layer(GOLD)
    magnet, mw, mh = magnet_layer()
    return {
        "pickup_heart": [compose(heart, hw, hh, f, RED["line"], top=7) for f in range(FRAMES)],
        "pickup_heart_golden": [compose(gold, gw, gh, f, GOLD["line"], golden_sparkles, top=7)
                                for f in range(FRAMES)],
        "pickup_magnet": [compose(magnet, mw, mh, f, hx("3d1424"), magnet_sparks, top=5)
                          for f in range(FRAMES)],
    }


def strip(frames):
    img = Image.new("RGBA", (S * len(frames), S), CLEAR)
    for i, f in enumerate(frames):
        img.alpha_composite(f, (i * S, 0))
    return img


def preview(sheets, path, scale=6):
    rows = list(sheets.values())
    img = Image.new("RGBA", (S * FRAMES * scale, S * len(rows) * scale), hx("4a6b3f"))
    for r, frames in enumerate(rows):
        big = strip(frames).resize((S * FRAMES * scale, S * scale), Image.NEAREST)
        img.alpha_composite(big, (0, r * S * scale))
    img.save(path)


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
