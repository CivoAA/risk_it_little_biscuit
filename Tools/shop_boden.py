"""Boden fuer Ricos Laden im Hub: Achteck-Steinfliesen in Schiefer-Blaugrau + roter Laeufer.

Erzeugt 32x32-Kacheln (PPU 32) nach Assets/Art/new/Hub/ShopBoden/:
  shop_fliese_0..2    Achteck-Fliesen (0 schlicht, 1 Riss, 2 Abplatzer)
  shop_fliese_wand    oberste Reihe mit Schatten der Wand
  shop_teppich_<c><r> Laeufer 2x4 Kacheln, c 0/1 = links/rechts,
                      r 0 = unten (Eingang) .. 3 = oben (vor der Theke), Fransen an beiden Enden

Palette aus dem Hub (rico, podium) genommen; bewusst kein Holz, das beisst sich mit Tresen/Werkbank.
Aufruf: python Tools/shop_boden.py [--preview]
"""
import os
import random
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "new", "Hub", "ShopBoden")
T = 32


def hx(s):
    s = s.lstrip("#")
    return tuple(int(s[i:i + 2], 16) for i in (0, 2, 4)) + (255,)


# Stein: kuehles Schiefer-Blaugrau (aus Ricos Fell), damit Tresen + Werkbank (Holz) davor stehen
FUGE = hx("#3b3a52")
STEIN = [hx("#6e7491"), hx("#6a708c"), hx("#737995")]
STEIN_HELL = hx("#868ca8")
STEIN_DUNKEL = hx("#5a5f7a")
SPRENKEL = hx("#62687f")
EINLAGE = hx("#3e6e76")
EINLAGE_RAND = hx("#2f5059")
EINLAGE_HELL = hx("#6fa3a0")

# Teppich
T_RAND = hx("#3b2433")
T_GOLD = hx("#d6b050")
T_GOLD_D = hx("#c49347")
T_ROT = hx("#9b2f45")
T_ROT_D = hx("#84273d")
T_ROSA = hx("#e48193")
T_CREME = hx("#e6c493")


def fliese(seed, riss=False, abplatzer=False):
    """Achteck-Fliesen (16 px Raster) mit petrolfarbenen Rauten-Einlagen an den Ecken."""
    rnd = random.Random(seed)
    im = Image.new("RGBA", (T, T))
    px = im.load()
    ton = {(ox, oy): STEIN[rnd.randrange(len(STEIN))] for ox in range(2) for oy in range(2)}
    for y in range(T):
        for x in range(T):
            u, v = x % 16, y % 16
            a = min(15 - u, u + 1)   # Abstand zur Fugenspalte 15
            b = min(15 - v, v + 1)
            if a + b <= 3:
                c = EINLAGE_HELL if (a, b) == (1, 2) and u < 8 and v < 8 else                     EINLAGE_RAND if a + b == 3 else EINLAGE
            elif a + b == 4 or a == 0 or b == 0:
                c = FUGE
            elif u == 0 or v == 0 or a + b == 5 and (u < 8 and v < 8):
                c = STEIN_HELL
            elif u == 14 or v == 14 or a + b == 5:
                c = STEIN_DUNKEL
            else:
                c = SPRENKEL if rnd.random() < 0.06 else ton[(x // 16, y // 16)]
            px[x, y] = c
    if riss:
        for x, y in [(3, 9), (4, 8), (5, 8), (6, 7), (7, 7), (8, 6), (9, 6), (9, 5), (10, 4)]:
            px[x, y] = FUGE
        px[8, 7] = STEIN_HELL
    if abplatzer:
        for x, y in [(21, 26), (22, 26), (21, 27), (22, 27), (23, 27), (22, 28)]:
            px[x, y] = STEIN_DUNKEL
        px[21, 25] = STEIN_HELL
    return im


def wandschatten(im):
    im = im.copy()
    px = im.load()
    for y, f in enumerate([0.55, 0.68, 0.8, 0.9]):
        for x in range(T):
            r, g, b, a = px[x, y]
            px[x, y] = (int(r * f), int(g * f), int(b * f), a)
    return im


def teppich(grund):
    """Laeufer ueber 2x4 Kacheln, grund = Funktion(cx, cy) -> Parkettkachel."""
    W, H = 2 * T, 4 * T
    im = Image.new("RGBA", (W, H))
    for cy in range(4):
        for cx in range(2):
            im.paste(grund(cx, cy), (cx * T, (3 - cy) * T))
    px = im.load()
    x0, x1 = 5, W - 6          # Teppichkante links/rechts (inklusive)
    y0, y1 = 6, H - 11         # oben / unten (vor den Fransen)
    rnd = random.Random(7)
    # Schlagschatten rechts/unten
    for y in range(y0 + 1, y1 + 2):
        r, g, b, a = px[x1 + 1, y]
        px[x1 + 1, y] = (int(r * 0.7), int(g * 0.7), int(b * 0.7), a)
    for x in range(x0 + 1, x1 + 2):
        r, g, b, a = px[x, y1 + 1]
        px[x, y1 + 1] = (int(r * 0.7), int(g * 0.7), int(b * 0.7), a)
    mx = (x0 + x1) / 2
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            d = min(x - x0, x1 - x, y - y0, y1 - y)
            if d == 0:
                c = T_RAND
            elif d <= 3:
                c = T_GOLD if d != 3 else T_GOLD_D
            elif d == 4:
                c = T_ROT_D
            elif d == 6:
                c = T_GOLD_D
            else:
                c = T_ROT_D if rnd.random() < 0.12 else T_ROT
            px[x, y] = c
    # Rauten in der Mitte, alle 16 px
    for cy in range(y0 + 16, y1 - 8, 16):
        for dy in range(-4, 5):
            w = 4 - abs(dy)
            for dx in range(-w, w + 1):
                rand = abs(dx) == w
                px[int(mx + dx + 0.5), cy + dy] = T_GOLD if rand else T_ROSA
        px[int(mx + 0.5), cy] = T_CREME
    # kleine Punkte zwischen den Rauten
    for cy in range(y0 + 24, y1 - 8, 16):
        for dx in (-9, 10):
            px[int(mx + dx), cy] = T_GOLD_D
    # Fransen an beiden Enden
    for x in range(x0 + 1, x1, 2):
        laenge = 4 + (x // 2) % 2
        for i in range(laenge):
            spitze = i == laenge - 1
            px[x, y1 + 1 + i] = T_GOLD_D if spitze else T_CREME
            px[x, y0 - 1 - i] = T_GOLD_D if spitze else T_CREME
    return im


def bauen():
    os.makedirs(OUT, exist_ok=True)
    tiles = {
        "shop_fliese_0": fliese(1),
        "shop_fliese_1": fliese(1, riss=True),
        "shop_fliese_2": fliese(1, abplatzer=True),
    }
    tiles["shop_fliese_wand"] = wandschatten(tiles["shop_fliese_0"])
    gross = teppich(lambda cx, cy: tiles["shop_fliese_0"])
    for cy in range(4):
        for cx in range(2):
            tiles["shop_teppich_%d%d" % (cx, cy)] = gross.crop(
                (cx * T, (3 - cy) * T, cx * T + T, (4 - cy) * T))
    for name, im in tiles.items():
        im.save(os.path.join(OUT, name + ".png"))
    return tiles


# Belegung im Hub (Zellen der boden-Tilemap), auch fuer die Vorschau
LADEN_X = range(53, 61)
LADEN_Y = range(33, 38)
EINGANG = [(56, 32), (57, 32)]
TEPPICH_X0, TEPPICH_Y0 = 56, 32


def kachel_fuer(x, y):
    if TEPPICH_X0 <= x <= TEPPICH_X0 + 1 and TEPPICH_Y0 <= y <= TEPPICH_Y0 + 3:
        return "shop_teppich_%d%d" % (x - TEPPICH_X0, y - TEPPICH_Y0)
    if y == max(LADEN_Y):
        return "shop_fliese_wand"
    h = (x * 7 + y * 13) % 11
    return "shop_fliese_1" if h == 3 else "shop_fliese_2" if h == 8 else "shop_fliese_0"


def zellen():
    return [(x, y) for y in LADEN_Y for x in LADEN_X] + EINGANG


if __name__ == "__main__":
    tiles = bauen()
    print("geschrieben:", ", ".join(sorted(tiles)))
    if "--preview" in sys.argv:
        pv = Image.new("RGBA", (len(LADEN_X) * T, 6 * T), (40, 40, 60, 255))
        for x, y in zellen():
            pv.paste(tiles[kachel_fuer(x, y)], ((x - 53) * T, (37 - y) * T))
        pv.resize((pv.width * 3, pv.height * 3), Image.NEAREST).save(
            os.path.join(os.environ.get("TEMP", "."), "shop_boden_preview.png"))
