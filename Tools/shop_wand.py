"""Waende fuer Ricos Laden im Hub, passend zum Steinboden aus shop_boden.py.

Rueckwand = 8x3 Kacheln (x 53..60, y 38..40 der Wand-Tilemap), als ein Bild gezeichnet
und zerschnitten: Stuckleiste, Pflaumen-Tapete mit Rauten, Messingleiste, Schiefer-Taefelung.
Deko: Regale mit Waren.
Dazu Mauerkanten (Gelaender-Tilemap): links/rechts am Boden, vorne mit Tueroeffnung.

Erzeugt nach Assets/Art/new/Hub/ShopBoden/:
  shop_wand_<spalte>_<reihe>  spalte 0..7 = x 53..60, reihe 0..2 = y 38..40
  shop_kante_l / shop_kante_r       Seitenmauer auf den Bodenzellen x 53 / x 60
  shop_front_ecke_l / _ecke_r       vorne links (53,32) / rechts (60,32)
  shop_front                         vorne (54,32), (59,32)
  shop_front_tuer_l / _tuer_r        Tuerkante (55,32) / (58,32)
Aufruf: python Tools/shop_wand.py [--preview]
"""
import os
import random
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import shop_boden as sb  # noqa: E402

hx = sb.hx
T = sb.T
OUT = sb.OUT

# Wand
STUCK_D = hx("#241c2e")
STUCK = hx("#3b2d47")
STUCK_H = hx("#5b4a66")
TAPETE = hx("#4e3d57")
TAPETE_D = hx("#45354e")
TAPETE_M = hx("#5c4966")      # Rautenmuster
MESSING = hx("#d6b050")
MESSING_D = hx("#a87b3a")
TAEFEL = hx("#4a4f6a")
TAEFEL_H = hx("#5f6584")
TAEFEL_D = hx("#383c55")
SOCKEL = hx("#2a2a3f")
# Mauerkrone (Seiten/vorne)
KRONE = hx("#3b2d47")
KRONE_H = hx("#5b4a66")
KRONE_D = hx("#241c2e")
# Deko
HOLZ_D = hx("#634145")
HOLZ_DD = hx("#3b2433")
HOLZ_H = hx("#79414c")
STERN = hx("#e6eaff")
KAESE = hx("#e8c25a")
KAESE_D = hx("#c49347")
ROT = hx("#d44f64")
ROSA = hx("#e48193")
CREME = hx("#e6c493")
PETROL = hx("#3e6e76")
PETROL_H = hx("#6fa3a0")
GLAS = hx("#9fb6c8")
KEKS = hx("#b1773e")
KEKS_D = hx("#634145")

W, H = 8 * T, 3 * T
BAND = 6  # Breite der Mauerkrone


def rect(px, x0, y0, x1, y1, c):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            px[x, y] = c


def rueckwand():
    im = Image.new("RGBA", (W, H))
    px = im.load()
    rnd = random.Random(3)
    for y in range(H):
        for x in range(W):
            if y < 8:      # Stuckleiste
                c = STUCK_D if y in (0, 7) else STUCK_H if y == 2 else STUCK
                if y == 5 and x % 4 == 0:
                    c = STUCK_D
            elif y < 62:   # Tapete mit Rauten
                u, v = (x + 4) % 12, (y - 8) % 12
                d = abs(u - 6) + abs(v - 6)
                c = TAPETE_M if d == 2 else TAPETE
                if (x // 12) % 2 == 1 and c == TAPETE:
                    c = TAPETE_D if rnd.random() < 0.5 else TAPETE
                if y == 8:
                    c = TAPETE_D
            elif y < 65:   # Messingleiste
                c = MESSING if y == 62 else MESSING_D if y == 64 else hx("#c49347")
            elif y < 91:   # Taefelung: Felder je 32 px
                u = x % 32
                c = TAEFEL
                if y in (66, 89) or u in (1, 30):
                    c = TAEFEL_D
                if 68 <= y <= 87 and 4 <= u <= 27:
                    c = TAEFEL_H if (y == 68 or u == 4) else TAEFEL_D if (y == 87 or u == 27) else TAEFEL
                if y == 65:
                    c = TAEFEL_D
            else:          # Sockel
                c = SOCKEL
            px[x, y] = c
    deko(px)
    # Terminal-Tafel (liegt eigentlich auf der boden-Tilemap, x 54-55 / y 38-39) mit einmalen,
    # damit sie unabhaengig von der Zeichenreihenfolge der Tilemaps sichtbar bleibt
    tafel = Image.open(os.path.join(sb.ROOT, "Assets", "Art", "new", "Hub", "tafel1.png")).convert("RGBA")
    im.alpha_composite(tafel, (T, T))
    # Seitenmauern laufen bis oben durch
    for y in range(H):
        for i in range(BAND):
            px[i, y] = KRONE_D if i == 0 else KRONE_H if i == BAND - 1 else KRONE
            px[W - 1 - i, y] = KRONE_D if i == 0 else KRONE_H if i == BAND - 1 else KRONE
        # Schatten der Seitenmauer auf der Wand
        for i, f in ((BAND, 0.6), (BAND + 1, 0.8)):
            for x in (i, W - 1 - i):
                r, g, b, a = px[x, y]
                px[x, y] = (int(r * f), int(g * f), int(b * f), a)
    return im


def regal(px, x0, x1, y, waren):
    """Brett von x0..x1 auf Hoehe y (Oberkante), Waren stehen darauf."""
    rect(px, x0, y, x1, y + 1, HOLZ_H)
    rect(px, x0, y + 2, x1, y + 2, HOLZ_DD)
    for kx in (x0 + 2, x1 - 2):
        rect(px, kx, y + 3, kx, y + 5, HOLZ_DD)
        px[kx - 1, y + 3] = HOLZ_DD
    x = x0 + 2
    for w in waren:
        x = w(px, x, y - 1) + 2


def glas(farbe):
    def f(px, x, y):  # Marmeladenglas 6x8, y = Boden
        rect(px, x, y - 7, x + 5, y, GLAS)
        rect(px, x + 1, y - 5, x + 4, y - 1, farbe)
        rect(px, x, y - 8, x + 5, y - 7, CREME)
        px[x + 1, y - 4] = STERN
        return x + 5
    return f


def trank(farbe, hell):
    def f(px, x, y):  # bauchige Flasche 7x10
        rect(px, x + 2, y - 9, x + 4, y - 9, HOLZ_H)          # Korken
        rect(px, x + 2, y - 8, x + 4, y - 6, GLAS)            # Hals
        rect(px, x, y - 5, x + 6, y, farbe)
        rect(px, x + 1, y - 6, x + 5, y - 6, farbe)
        px[x + 1, y - 4] = hell
        px[x + 1, y - 3] = hell
        rect(px, x, y, x + 6, y, HOLZ_DD)
        return x + 6
    return f


def kaese(px, x, y):  # Kaeseecke 10x7
    for i in range(7):
        rect(px, x + i * 9 // 7 if i else x, y - i, x + 9, y - i, KAESE)
    rect(px, x, y, x + 9, y, KAESE_D)
    for lx, ly in ((x + 6, y - 2), (x + 8, y - 4), (x + 3, y - 1)):
        px[lx, ly] = KAESE_D
    return x + 9


def keksglas(px, x, y):  # Keksglas 9x11 mit Keksen
    rect(px, x, y - 9, x + 8, y, GLAS)
    rect(px, x + 1, y - 4, x + 7, y - 1, KEKS)
    for kx, ky in ((x + 2, y - 3), (x + 5, y - 2), (x + 6, y - 4)):
        px[kx, ky] = KEKS_D
    rect(px, x + 1, y - 7, x + 4, y - 5, KEKS)
    px[x + 2, y - 6] = KEKS_D
    rect(px, x - 1, y - 11, x + 9, y - 10, HOLZ_H)
    rect(px, x + 3, y - 12, x + 5, y - 12, HOLZ_H)
    px[x + 1, y - 8] = STERN
    return x + 9


def deko(px):
    # links (x 53, neben der Tafel): Regal mit Glaesern
    regal(px, 8, 30, 34, [glas(ROT), glas(ROSA), trank(PETROL, PETROL_H)])
    # rechts (x 60): Regal mit Kaese + Keksglas
    regal(px, 7 * T + 1, 7 * T + 24, 34, [kaese, keksglas])
    # zwischen Fackel (x 58) und rechtem Regal (x 59): kleines Brett mit Traenken
    regal(px, 6 * T + 6, 6 * T + 26, 46, [trank(ROT, ROSA), trank(PETROL, PETROL_H)])


def schatten(px, x, y, f):
    r, g, b, a = px[x, y]
    px[x, y] = (int(r * f), int(g * f), int(b * f), a)


def kante(links):
    """Seitenmauer: Krone am Rand, Schatten auf dem Boden, Rest durchsichtig."""
    im = Image.new("RGBA", (T, T), (0, 0, 0, 0))
    px = im.load()
    for y in range(T):
        for i in range(BAND):
            x = i if links else T - 1 - i
            px[x, y] = KRONE_D if i == 0 else KRONE_H if i == BAND - 1 else KRONE
        for i, a in ((BAND, 110), (BAND + 1, 60)):
            x = i if links else T - 1 - i
            px[x, y] = (20, 16, 32, a)
        if y % 8 == 7:  # Steinfugen in der Krone
            for i in range(1, BAND - 1):
                px[i if links else T - 1 - i, y] = KRONE_D
    return im


def front(links_ecke=False, rechts_ecke=False, tuer_l=False, tuer_r=False):
    """Vordere Mauer: Krone oben in der Zelle (direkt unter der untersten Bodenreihe)."""
    im = Image.new("RGBA", (T, T), (0, 0, 0, 0))
    px = im.load()
    x0 = 0
    x1 = T - 1
    for y in range(0, 9):
        for x in range(x0, x1 + 1):
            c = KRONE_H if y == 0 else KRONE_D if y == 8 else KRONE
            if y == 4 and x % 8 == 7:
                c = KRONE_D
            px[x, y] = c
    if tuer_l:   # Oeffnung rechts: Pfostenkante
        rect(px, T - 2, 0, T - 1, 8, KRONE_D)
        rect(px, T - 3, 0, T - 3, 7, KRONE_H)
    if tuer_r:
        rect(px, 0, 0, 1, 8, KRONE_D)
        rect(px, 2, 0, 2, 7, KRONE_H)
    if links_ecke or rechts_ecke:
        for i in range(BAND):
            x = i if links_ecke else T - 1 - i
            for y in range(0, 9):
                px[x, y] = KRONE_D if i == 0 else KRONE
            px[x, 8] = KRONE_D
    return im


def bauen():
    os.makedirs(OUT, exist_ok=True)
    tiles = {}
    wand = rueckwand()
    for r in range(3):
        for s in range(8):
            tiles["shop_wand_%d_%d" % (s, r)] = wand.crop((s * T, (2 - r) * T, s * T + T, (3 - r) * T))
    tiles["shop_kante_l"] = kante(True)
    tiles["shop_kante_r"] = kante(False)
    tiles["shop_front"] = front()
    tiles["shop_front_ecke_l"] = front(links_ecke=True)
    tiles["shop_front_ecke_r"] = front(rechts_ecke=True)
    tiles["shop_front_tuer_l"] = front(tuer_l=True)
    tiles["shop_front_tuer_r"] = front(tuer_r=True)
    for name, im in tiles.items():
        im.save(os.path.join(OUT, name + ".png"))
    return tiles


# Belegung: (Tilemap, x, y) -> Kachel
def belegung():
    b = {}
    for r in range(3):
        for s in range(8):
            b[("Wand", 53 + s, 38 + r)] = "shop_wand_%d_%d" % (s, r)
    for y in range(33, 38):
        b[("Gelaender", 53, y)] = "shop_kante_l"
        b[("Gelaender", 60, y)] = "shop_kante_r"
    b[("Gelaender", 53, 32)] = "shop_front_ecke_l"
    b[("Gelaender", 54, 32)] = "shop_front"
    b[("Gelaender", 55, 32)] = "shop_front_tuer_l"
    b[("Gelaender", 58, 32)] = "shop_front_tuer_r"
    b[("Gelaender", 59, 32)] = "shop_front"
    b[("Gelaender", 60, 32)] = "shop_front_ecke_r"
    return b


if __name__ == "__main__":
    tiles = bauen()
    boden = sb.bauen()
    print("geschrieben:", ", ".join(sorted(tiles)))
    if "--preview" in sys.argv:
        pv = Image.new("RGBA", (10 * T, 10 * T), (12, 10, 20, 255))
        for x, y in sb.zellen():
            pv.alpha_composite(boden[sb.kachel_fuer(x, y)], ((x - 52) * T, (40 - y) * T))
        for (_, x, y), n in belegung().items():
            pv.alpha_composite(tiles[n], ((x - 52) * T, (40 - y) * T))
        pv.resize((pv.width * 2, pv.height * 2), Image.NEAREST).save(
            os.path.join(os.environ.get("TEMP", "."), "shop_raum_preview.png"))
