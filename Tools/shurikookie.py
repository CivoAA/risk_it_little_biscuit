"""
Zeichnet den Shurikookie neu (ingame + Waffenicon + Werkbank-Icons).

  Assets/Art/Waffen/shurikookie_wurf.png       25x25  PPU 32  der geworfene Stern (Prefab
                                                             "Shuriken Weapon", Scale 1)
  Assets/Resources/Shop/shurikookie.png        64x64          Shop-/Waffen-/Unlock-Icon:
                                                             derselbe Stern, Pixel x2
  Assets/Resources/Workbench/shurikookie_14.png  14x14
  Assets/Resources/Workbench/shurikookie_10.png  10x10

Ein Keks-Wurfstern: vier geschwungene Klingen aus Keksteig, die Schneiden
sind mit Zuckerguss glasiert, Schokostueckchen auf den Klingen, in der Mitte
ein Loch mit Schokoring. Kontur und Toene wie Keksaege/Bestiarium.

Die .meta von shurikookie.png bleibt, wie sie ist (Player-Prefab + Szenen
verweisen auf das Sprite Waffe_shuriken_0, Rechteck x 2, y 2, 59x59 - der
Stern liegt darin mittig). Der Stern dreht sich ingame per Animation
(Shuriken.anim, gegen den Uhrzeigersinn); die Klingen laufen darum mit der
glasierten Schneide voran.

Aufruf aus dem Projektordner:  python Tools/shurikookie.py [--preview pfad.png] [--dry]
"""

import math
import os
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
INGAME = os.path.join(ROOT, "Assets", "Art", "Waffen", "shurikookie_wurf.png")
ICON = os.path.join(ROOT, "Assets", "Resources", "Shop", "shurikookie.png")
WORKBENCH = os.path.join(ROOT, "Assets", "Resources", "Workbench")

INGAME_SIZE = 25

CLEAR = (0, 0, 0, 0)


def hx(s, a=255):
    s = s.lstrip("#")
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), a)


LINE = hx("#2a1712")
# Keks, dunkel -> hell (wie Keksaege)
K_EDGE, K_DARK, K_MID, K_LIGHT, K_HI = hx("#8a4f22"), hx("#b8702f"), hx("#d99a4e"), hx("#efbd72"), hx("#fbdc9c")
CHOC_D, CHOC, CHOC_L = hx("#2e160c"), hx("#4a2616"), hx("#7a4428")
# Zuckerguss auf den Schneiden
ICE_D, ICE, ICE_HI = hx("#e6d6c4"), hx("#fff6ea"), hx("#ffffff")

# Licht von oben links (y nach oben)
LX, LY = -0.62, 0.78

# Klinge nach oben, von Hand: je Zeile (x von, x bis) ab der Spitze. Die linke
# Kante ist die gerade, glasierte Schneide, die rechte haengt hohl durch.
# Die anderen drei Klingen sind diese hier, um je 90 Grad gedreht.
BLADES = {
    25: [(8, 9), (7, 10), (7, 10), (7, 11), (7, 12), (7, 13), (7, 14), (7, 15), (7, 16)],
    13: [(4, 5), (3, 5), (3, 6), (3, 7)],
    10: [(3, 4), (2, 4), (2, 5)],
}
# Mittelkeks: Radius, Loch, Schokoring (in Pixeln)
BODY = {25: (5.6, 1.1, 3.0), 13: (3.1, 0.0, 1.6), 10: (2.6, 0.0, 0.0)}
# Schokostueck je Klinge (links oben, 2x2) in der Klinge nach oben
CHIP = {25: (10, 5)}


def rot(x, y, size, k):
    """k mal um 90 Grad gegen den Uhrzeigersinn drehen (Bildkoordinaten, y nach unten)."""
    for _ in range(k):
        x, y = y, size - 1 - x
    return x, y


def star(size):
    img = Image.new("RGBA", (size, size), CLEAR)
    px = img.load()
    c = (size - 1) / 2.0
    body_r, hole_r, ring_r = BODY[size]

    mask = [[False] * size for _ in range(size)]
    ice = [[False] * size for _ in range(size)]
    chip = {}
    for y in range(size):
        for x in range(size):
            r = math.hypot(x - c, y - c)
            if hole_r <= r <= body_r or (hole_r == 0 and r <= body_r):
                mask[y][x] = True
    for k in range(4):
        for y, (x0, x1) in enumerate(BLADES[size]):
            for x in range(x0, x1 + 1):
                rx, ry = rot(x, y, size, k)
                mask[ry][rx] = True
            if 0 < y < len(BLADES[size]) - 1 and size > 13:
                rx, ry = rot(x0 + 1, y, size, k)
                ice[ry][rx] = True
        if size in CHIP:
            cx, cy = CHIP[size]
            for (ox, oy, col) in ((0, 0, CHOC_L), (1, 0, CHOC), (0, 1, CHOC), (1, 1, CHOC_D)):
                rx, ry = rot(cx + ox, cy + oy, size, k)
                chip[(rx, ry)] = col
    def m(x, y):
        return 0 <= x < size and 0 <= y < size and mask[y][x]

    edge = [[m(x, y) and not (m(x - 1, y) and m(x + 1, y) and m(x, y - 1) and m(x, y + 1))
             for x in range(size)] for y in range(size)]

    def e(x, y):
        return 0 <= x < size and 0 <= y < size and edge[y][x]

    for y in range(size):
        for x in range(size):
            if not mask[y][x]:
                continue
            if edge[y][x]:
                px[x, y] = LINE
                continue
            dx, dy = x - c, c - y
            r = math.hypot(dx, dy)
            col = K_MID
            if e(x - 1, y) or e(x, y - 1):
                col = K_LIGHT
            elif e(x + 1, y) or e(x, y + 1):
                col = K_DARK
            if ice[y][x]:
                col = ICE if (dx * LX + dy * LY) > -0.5 else ICE_D
            if r <= ring_r:
                col = CHOC_L if (dx * LX + dy * LY) > 0 else CHOC
            if (x, y) in chip:
                col = chip[(x, y)]
            px[x, y] = col
    return img


def icon64(src):
    big = src.resize((src.width * 2, src.height * 2), Image.NEAREST)
    out = Image.new("RGBA", (64, 64), CLEAR)
    # Mitte des Sprite-Rechtecks Waffe_shuriken_0 (x 2..61, y 2..61 von unten)
    off = 2 + (59 - big.width) // 2
    out.alpha_composite(big, (off, 64 - 2 - 59 + (59 - big.height) // 2))
    return out


def main():
    preview = None
    if "--preview" in sys.argv:
        preview = sys.argv[sys.argv.index("--preview") + 1]
    dry = "--dry" in sys.argv

    ingame = star(INGAME_SIZE)
    icon = icon64(ingame)
    w14 = Image.new("RGBA", (14, 14), CLEAR)
    w14.alpha_composite(star(13), (0, 1))
    w10 = star(10)

    if preview:
        pics = [ingame, icon, w14, w10]
        out = Image.new("RGBA", (4 * 270, 270), (52, 50, 62, 255))
        for i, p in enumerate(pics):
            k = 256 // max(p.size)
            out.alpha_composite(p.resize((p.width * k, p.height * k), Image.NEAREST), (i * 270, 0))
        out.save(preview)
    if not dry:
        ingame.save(INGAME)
        if not os.path.exists(INGAME + ".meta"):
            sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
            from unity_meta import write_strip_meta
            write_strip_meta(INGAME + ".meta", "shurikookie_wurf", 1, INGAME_SIZE, INGAME_SIZE, 32)
        icon.save(ICON)
        w14.save(os.path.join(WORKBENCH, "shurikookie_14.png"))
        w10.save(os.path.join(WORKBENCH, "shurikookie_10.png"))


if __name__ == "__main__":
    main()
