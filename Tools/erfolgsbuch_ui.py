# -*- coding: utf-8 -*-
"""Zeichnet die 25 UI-Sprites des Erfolge-/Unlocks-Buchs neu.

Aufruf aus dem Projektordner:
    python Tools/erfolgsbuch_ui.py

Schreibt nach Assets/Resources/AchievementsBook/ui/, danach den Atlas unter
Assets/Art/UI_Objects/AchievementsBook/atlas/ und zwei Vorschaubilder.
Alle Dateinamen, Bildgroessen und 9-Slice-Raender bleiben exakt wie in
ACHIEVEMENTS_BOOK_UI.md - die .meta-Dateien werden nicht angefasst, die GUIDs
aendern sich also nicht und AchievementsBookPanel.cs braucht keine Zeile.

Stilregeln:
  * Palette aus achievements_book.gpl. Zwei Toene sind dazugekommen, weil die
    Gold- und die Rotrampe sonst keinen dunklen Fuss haben: GOLD_D2 und RED_D.
  * Geschattet wird in flaechigen Baendern mit klarer Kante, so wie im Hub.
    Gedithert wird nur dort, wo eine Flaeche wirklich verlaufen muss - in der
    Kapselfluessigkeit und an den Uebergaengen grosser Flaechen.
  * Licht kommt immer von oben links.
  * Alles, was in der Mitte eines 9-Slice-Sprites liegt, ist entweder einfarbig
    oder eine durchgehende waagrechte Linie. Nur so ueberlebt es das Dehnen.
"""
import json
import math
import os
import random
import sys

from PIL import Image

# --------------------------------------------------------------- Palette
INK     = (59, 43, 51)      # 3b2b33  Kontur, durchgehend 1px
PARCH   = (242, 220, 188)   # f2dcbc  Pergament
PARCH2  = (224, 196, 159)   # e0c49f  Pergament Schatten
PARCH3  = (217, 177, 137)   # d9b189  Pergament tief
PARCH4  = (185, 151, 114)   # b99772  gedaempft, Rahmen der Belohnungsbox
WOOD_L2 = (200, 160, 120)   # c8a078  Holz hell
WOOD_L  = (168, 122, 82)    # a87a52  Holz Glanz
WOOD    = (140, 90, 60)     # 8c5a3c  Holz Grundton
WOOD_D  = (111, 70, 48)     # 6f4630  Holz dunkel
WOOD_D2 = (90, 52, 33)      # 5a3421  Holz Schatten
WOOD_D3 = (77, 46, 30)      # 4d2e1e  Holz tief
TAB_HL  = (138, 90, 61)     # 8a5a3d  Reiter-Glanz
LOCK_O  = (156, 130, 104)   # 9c8268  Kontur gesperrt
RED_L   = (212, 86, 108)    # d4566c  Fortschritt hell
RED     = (184, 58, 82)     # b83a52  Fortschritt
RED_D   = (142, 42, 62)     # 8e2a3e  Fortschritt Fuss          (neu)
GOLD_L  = (247, 215, 106)   # f7d76a  Kapselgelb hell
GOLD    = (232, 185, 60)    # e8b93c  Kapselgelb
GOLD_D  = (201, 146, 42)    # c9922a  Kapselrahmen
GOLD_D2 = (168, 118, 31)    # a8761f  Gold Fuss                 (neu)
CREAM   = (255, 244, 224)   # fff4e0  Glanzlicht

# --------------------------------------------------------------- Werkzeug
BAYER = ((0, 8, 2, 10), (12, 4, 14, 6), (3, 11, 1, 9), (15, 7, 13, 5))


def dither(x, y, f):
    """True, wenn an dieser Stelle der hellere von zwei Toenen gesetzt wird."""
    return f > (BAYER[y & 3][x & 3] + 0.5) / 16.0


def ramp(stops, t):
    """Zwei Nachbarfarben und die Lage dazwischen aus einer Farbrampe."""
    t = min(max(t, 0.0), 1.0)
    for i in range(len(stops) - 1):
        t0, c0 = stops[i]
        t1, c1 = stops[i + 1]
        if t <= t1:
            f = 0.0 if t1 == t0 else (t - t0) / (t1 - t0)
            return c0, c1, f
    return stops[-1][1], stops[-1][1], 0.0


class Canvas:
    def __init__(self, w, h, fill=None):
        self.w = w
        self.h = h
        self.px = [[fill] * w for _ in range(h)]

    def set(self, x, y, c):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.px[y][x] = c

    def get(self, x, y):
        if 0 <= x < self.w and 0 <= y < self.h:
            return self.px[y][x]
        return None

    def hline(self, x0, x1, y, c):
        for x in range(x0, x1 + 1):
            self.set(x, y, c)

    def vline(self, x, y0, y1, c):
        for y in range(y0, y1 + 1):
            self.set(x, y, c)

    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.set(x, y, c)

    def frame(self, x0, y0, x1, y1, c):
        self.hline(x0, x1, y0, c)
        self.hline(x0, x1, y1, c)
        self.vline(x0, y0, y1, c)
        self.vline(x1, y0, y1, c)

    def cut_corners(self, x0, y0, x1, y1, c=None):
        """Nimmt die vier Eckpixel weg - macht aus dem Kasten eine Marke."""
        for x, y in ((x0, y0), (x1, y0), (x0, y1), (x1, y1)):
            self.set(x, y, c)

    def stamp(self, x0, y0, art, key):
        """Zeichnet ein Textmuster. key bildet Zeichen auf Farben ab."""
        for dy, line in enumerate(art):
            for dx, ch in enumerate(line):
                c = key.get(ch, "skip")
                if c != "skip":
                    self.set(x0 + dx, y0 + dy, c)

    def image(self):
        img = Image.new("RGBA", (self.w, self.h), (0, 0, 0, 0))
        out = img.load()
        for y in range(self.h):
            row = self.px[y]
            for x in range(self.w):
                c = row[x]
                if c is not None:
                    out[x, y] = (c[0], c[1], c[2], 255)
        return img


# =====================================================================
#  bg_capsule - die Kapselfluessigkeit, 9-Slice 5
# =====================================================================
#
# Der Kern ist ein gedithertes Gefaelle statt harter Baender: das Sprite wird
# auf ungewoehnlichen Seitenverhaeltnissen gedehnt, und ein Verlauf haelt das
# aus, feste 8px-Baender nicht. Die Kanten, die scharf bleiben muessen - Rahmen
# und Schrauben - liegen alle im 5px-Rand, den das 9-Slice nicht anfasst.

LIQUID = ((0.00, GOLD_L), (0.30, GOLD_L), (0.58, GOLD), (0.86, GOLD_D), (1.00, GOLD_D2))
BRIGHTER = {GOLD_D2: GOLD_D, GOLD_D: GOLD, GOLD: GOLD_L, GOLD_L: CREAM, CREAM: CREAM}
# Der Glasschein darf nie bis ins Creme gehen - sonst frisst er ein weisses
# Loch in die Fluessigkeit, statt nur ueber ihr zu liegen.
SHEEN = {GOLD_D2: GOLD_D, GOLD_D: GOLD, GOLD: GOLD_L, GOLD_L: GOLD_L, CREAM: CREAM}
DARKER = {CREAM: GOLD_L, GOLD_L: GOLD, GOLD: GOLD_D, GOLD_D: GOLD_D2, GOLD_D2: GOLD_D2}


def bg_capsule():
    W, H = 320, 180
    c = Canvas(W, H)
    x0, y0, x1, y1 = 5, 5, W - 6, H - 6      # Innenflaeche hinter dem Rahmen
    span = float(y1 - y0)

    # 1. Fluessigkeit: gedithertes Gefaelle von oben hell nach unten satt. Die
    #    Schichtgrenzen schwingen leicht - waagrecht schnurgerade sieht aus wie
    #    eine Wand, nicht wie Fluessigkeit. Ein Verlauf haelt ausserdem das
    #    Dehnen auf ungewoehnlichen Seitenverhaeltnissen aus, feste Baender nicht.
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            wob = math.sin(x / 71.0) * 3.4 + math.sin(x / 23.0 + 1.7) * 1.6
            a, b, f = ramp(LIQUID, (y - y0 + wob) / span)
            c.set(x, y, b if dither(x, y, f) else a)

    # 2. Glasschein: zwei Diagonalbaender, das breite schwach, das schmale klar.
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            d = x * 0.55 + y
            if 24 <= d <= 70:
                frac = 0.30
            elif 88 <= d <= 100:
                frac = 0.50
            else:
                continue
            if dither(x + 2, y, frac):
                c.set(x, y, SHEEN[c.get(x, y)])

    # 3. Feine Schwebeteilchen - nimmt der grossen Flaeche die Leere.
    rnd = random.Random(20260921)
    for _ in range(110):
        x = rnd.randrange(x0, x1 + 1)
        y = rnd.randrange(y0, y1 + 1)
        c.set(x, y, BRIGHTER[c.get(x, y)])

    # 4. Vignette: zum Rand hin eine Stufe dunkler, damit die Kapsel rund wirkt.
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            d = min(x - x0, x1 - x, y - y0, y1 - y)
            if d < 22:
                if dither(x, y + 1, (22 - d) / 30.0):
                    c.set(x, y, DARKER[c.get(x, y)])

    # 6. Rahmen: 4px Messing mit Fase, innen die 1px-Kontur. Zusammen genau die
    #    5px, die als 9-Slice-Rand in der .meta stehen.
    top = (GOLD_L, GOLD, GOLD_D, GOLD_D2)          # aussen -> innen, oben/links
    bot = (GOLD_D2, GOLD_D, GOLD, GOLD)            # aussen -> innen, unten/rechts
    for i in range(4):
        c.hline(i, W - 1 - i, i, top[i])                    # oben
        c.vline(i, i, H - 1 - i, top[i])                    # links
        c.hline(i, W - 1 - i, H - 1 - i, bot[i])            # unten
        c.vline(W - 1 - i, i, H - 1 - i, bot[i])            # rechts
    c.frame(4, 4, W - 5, H - 5, INK)

    # 7. Schrauben, nur in den Ecken: in der Mitte einer Kante wuerde das
    #    9-Slice sie beim Dehnen verschmieren.
    bolt = ("XoX", "o#o", "XoX")
    key = {"X": GOLD_D2, "o": GOLD_L, "#": CREAM}
    for bx, by in ((0, 0), (W - 3, 0), (0, H - 3), (W - 3, H - 3)):
        c.stamp(bx, by, bolt, key)
        c.set(bx + 1, by + 2, GOLD_D2)

    return c.image()


# =====================================================================
#  bubble_01..08 - Luftblasen, 6 bis 16px
# =====================================================================


def bubble(d):
    c = Canvas(d, d)
    r = d / 2.0
    cx = cy = r

    def inside(x, y, rr):
        return (x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2 <= rr * rr

    for y in range(d):
        for x in range(d):
            if not inside(x, y, r):
                continue
            edge = not (inside(x - 1, y, r) and inside(x + 1, y, r)
                        and inside(x, y - 1, r) and inside(x, y + 1, r))
            if edge:
                c.set(x, y, INK)
            else:
                c.set(x, y, GOLD_L)

    # Innenkante oben links eine Stufe dunkler. Erst dadurch hat der Glanzpunkt
    # etwas, wovon er sich abheben kann - auf reinem Hellgelb verschwindet er.
    inner = r - 1.35
    for y in range(d):
        for x in range(d):
            if c.get(x, y) != GOLD_L or inside(x, y, inner):
                continue
            if (x + 0.5 - cx) + (y + 0.5 - cy) < 0:
                c.set(x, y, GOLD)

    # Brechungssichel unten rechts - macht aus dem Kreis erst eine Blase.
    if d >= 9:
        for y in range(d):
            for x in range(d):
                if c.get(x, y) != GOLD_L:
                    continue
                dx, dy = x + 0.5 - cx, y + 0.5 - cy
                if dx + dy > r * 0.72 and math.hypot(dx, dy) > r * 0.45:
                    c.set(x, y, CREAM)

    # Glanzpunkt oben links, Groesse waechst mit der Blase.
    gx = gy = int(round(r - r * 0.5)) - 1
    c.set(gx, gy, CREAM)
    if d >= 8:
        c.set(gx + 1, gy, CREAM)
    if d >= 12:
        c.set(gx, gy + 1, CREAM)
    if d >= 16:
        c.set(gx + 1, gy + 1, CREAM)

    return c.image()


# =====================================================================
#  book_panel - die aufgeschlagene Doppelseite, 290x144, 9-Slice 3/5/3/3
# =====================================================================
#
# Korpus 288x142 bei (0,0), rechts und unten 2px Schlagschatten. Der Bund liegt
# lokal bei x=140..147 und bekommt sein eigenes Sprite (book_spine), hier wird
# nur die Woelbung der Seiten dorthin gezeichnet.

SPINE_L, SPINE_R = 140, 147       # lokale Spalten, die book_spine spaeter deckt

# Wie weit die Woelbung vom Bund weg reicht. War 18 und lief damit bis unter den
# Anfang des Detailtextes - der begann sichtbar im Dunkeln. Mit 15 und einer
# duenneren dunklen Kante ist der Falz bei lokal x=163 zu Ende, der Text setzt
# bei 166 auf sauberem Papier an (siehe DetailLeft in AchievementsBookPanel.cs).
GUTTER = 15

# Der grosse Icon-Rahmen, lokal. Muss zu SlotXL im Panel passen: dort global
# (224,32), das Buch beginnt bei x=16.
SLOT_X, SLOT_Y, SLOT_S = 208, 9, 32


def book_panel():
    W, H = 290, 144
    CW, CH = 288, 142
    c = Canvas(W, H)

    # Schlagschatten, 2px nach unten rechts.
    c.rect(2, CH, W - 1, H - 1, GOLD_D)
    c.rect(CW, 2, W - 1, H - 1, GOLD_D)

    # Grundflaeche und Kontur.
    c.rect(0, 0, CW - 1, CH - 1, PARCH)
    c.frame(0, 0, CW - 1, CH - 1, INK)
    c.hline(1, CW - 2, 1, CREAM)                 # Lichtkante oben

    # Woelbung zum Bund: beide Seiten laufen zur Mitte hin in den Schatten.
    for y in range(2, CH - 1):
        for x in range(1, CW - 1):
            if x < SPINE_L:
                t = (x - (SPINE_L - GUTTER)) / float(GUTTER)   # 0 aussen .. 1 am Bund
            elif x > SPINE_R:
                t = ((SPINE_R + GUTTER) - x) / float(GUTTER)
            else:
                continue
            if t <= 0.0:
                continue
            t = min(t, 1.0)
            if t > 0.93:
                c.set(x, y, PARCH3)
            elif t > 0.72:
                c.set(x, y, PARCH2)
            elif dither(x, y, (t - 0.30) / 0.42):
                c.set(x, y, PARCH2)

    # Seitenstapel: aussen liegen die Kanten der darunterliegenden Blaetter.
    c.vline(1, 2, CH - 2, PARCH2)
    c.vline(2, 2, CH - 2, PARCH3)
    c.vline(CW - 2, 2, CH - 2, PARCH2)
    c.vline(CW - 3, 2, CH - 2, PARCH3)
    c.hline(1, CW - 2, CH - 2, PARCH3)           # unten der 2px-Seitenschatten
    c.hline(1, CW - 2, CH - 3, PARCH3)

    # Schlagschatten unter dem grossen Icon-Rahmen. Der steht immer - das Panel
    # blendet nur das Icon darin aus, nie den Rahmen - also darf der Schatten
    # fest ins Papier. 2px nach unten rechts, dieselbe Richtung wie der
    # Buchschatten, nach aussen eine Stufe heller, damit er nicht als Balken liest.
    SX, SY, SS = SLOT_X, SLOT_Y, SLOT_S
    for i, col in ((0, PARCH3), (1, PARCH2)):
        c.vline(SX + SS + i, SY + 2 + i, SY + SS + 1, col)
        c.hline(SX + 2 + i, SX + SS + 1, SY + SS + i, col)

    # Zierlinien links und rechts neben dem Icon-Rahmen. Die Flaeche dort ist
    # frei - Titel und Text beginnen erst bei y=44 - und blieb sonst leer.
    # Symmetrisch um die Mitte des Rahmens, 4px Luft zu ihm, und ausserhalb des
    # Falzschattens (der endet bei GUTTER, also lokal x=163).
    mid = SLOT_X + SLOT_S // 2
    gap = SLOT_S // 2 + 4
    run = 28
    for a, b, capx in ((mid - gap - run, mid - gap, mid - gap - run - 2),
                       (mid + gap, mid + gap + run, mid + gap + run + 2)):
        c.hline(a, b, 24, PARCH3)
        c.hline(a, b, 25, CREAM)
        c.vline(capx, 23, 25, PARCH3)
        c.set(capx, 26, CREAM)

    # Papierfaser ganz zum Schluss und sehr sparsam - schon bei ein paar
    # Prozent sieht die Seite aus wie verrauscht statt wie Papier.
    rnd = random.Random(4711)
    for _ in range(520):
        x = rnd.randrange(3, CW - 3)
        y = rnd.randrange(3, CH - 4)
        cur = c.get(x, y)
        if cur == PARCH:
            c.set(x, y, PARCH2 if rnd.random() < 0.75 else CREAM)
        elif cur == PARCH2 and rnd.random() < 0.4:
            c.set(x, y, PARCH3)

    return c.image()


# =====================================================================
#  book_spine - der Bund, 8x138, 9-Slice 0/2/0/2
# =====================================================================


def book_spine():
    W, H = 8, 138
    c = Canvas(W, H)

    # Querschnitt: an beiden Falzen die dunkle Kante, dazwischen woelbt sich
    # der Lederruecken ins Licht. Bewusst heller gehalten als der erste
    # Entwurf - eine fast schwarze Leiste teilt die Doppelseite in zwei
    # getrennte Fenster, statt sie zusammenzuhalten.
    cols = (INK, WOOD_D, WOOD_L, WOOD_L2, WOOD_L, WOOD, WOOD_D2, INK)
    for x, col in enumerate(cols):
        c.vline(x, 0, H - 1, col)

    # Drei Buende, wie die erhabenen Rippen auf einem gebundenen Buchruecken:
    # obere Kante im Licht, untere im Schatten. Das liest sich auf 8px Breite
    # deutlich ruhiger als ein durchlaufender Heftfaden.
    up = {WOOD_D: WOOD, WOOD_L: WOOD_L2, WOOD_L2: PARCH3, WOOD: WOOD_L, WOOD_D2: WOOD_D}
    down = {WOOD_D: WOOD_D3, WOOD_L: WOOD, WOOD_L2: WOOD_L, WOOD: WOOD_D, WOOD_D2: WOOD_D3}
    for hub in (34, 69, 104):
        for x in range(1, W - 1):
            c.set(x, hub - 1, up[cols[x]])
            c.set(x, hub + 1, down[cols[x]])

    # Kappen oben und unten - der Ruecken laeuft nicht offen aus.
    c.hline(0, W - 1, 0, INK)
    c.hline(0, W - 1, H - 1, INK)
    for x in range(1, W - 1):
        c.set(x, 1, up[cols[x]])
        c.set(x, H - 2, down[cols[x]])

    return c.image()


# =====================================================================
#  Reiter - 60x14, 9-Slice 2
# =====================================================================


def tab_active():
    # 60x16, nicht 60x14: die unteren beiden Zeilen liegen im Spiel ueber der
    # Kontur (y=23) und der Lichtkante (y=24) der Buchseite. Ohne sie schwebt
    # der Reiter als eigene Karte ueber dem Buch, statt daran zu haengen.
    # AchievementsBookPanel zieht den aktiven Reiter dafuer um TabOverlap
    # hoeher; der inaktive bleibt 14px.
    W, H = 60, 16
    c = Canvas(W, H)
    c.rect(1, 1, W - 2, H - 1, PARCH)
    c.hline(1, W - 2, 0, INK)
    c.vline(0, 1, H - 1, INK)                    # Seitenwand bis auf die Seite
    c.vline(W - 1, 1, H - 1, INK)
    c.hline(1, W - 2, 1, CREAM)                  # Lichtkante oben
    c.vline(1, 2, H - 1, CREAM)                  # linke Kante im Licht
    c.vline(W - 2, 2, H - 1, PARCH2)             # rechte Kante im Schatten
    # Unten offen: die letzten Zeilen gehen ohne Kante in die Buchseite ueber.
    return c.image()


def tab_inactive():
    W, H = 60, 14
    c = Canvas(W, H)
    # Reine Querbaender - dieses Sprite wird als Zurueck-Knopf auf 52px
    # gestaucht, und nur waagrechte Linien ueberstehen das sauber.
    bands = (
        (1, 1, TAB_HL), (2, 6, WOOD_D), (7, 9, WOOD_D2),
        (10, 11, WOOD_D3), (12, 12, WOOD_D3),
    )
    for a, b, col in bands:
        for y in range(a, b + 1):
            c.hline(1, W - 2, y, col)
    c.hline(1, W - 2, 0, INK)
    c.hline(1, W - 2, H - 1, INK)
    c.vline(0, 1, H - 2, INK)
    c.vline(W - 1, 1, H - 2, INK)
    c.vline(1, 2, H - 3, TAB_HL)                 # linke Kante im Licht
    c.vline(W - 2, 2, H - 3, WOOD_D3)            # rechte Kante im Schatten
    c.cut_corners(0, 0, W - 1, H - 1)
    return c.image()


# =====================================================================
#  Listenzeilen - 132x21
# =====================================================================


def row_normal():
    # Nicht mehr voellig leer: eine haarfeine Linie unter dem Textfeld gibt der
    # Liste den Rhythmus eines linierten Kochbuchs. Unter dem Icon-Slot bleibt
    # sie weg, sonst laeuft sie in den Rahmen.
    c = Canvas(132, 21)
    c.hline(25, 128, 20, PARCH2)
    return c.image()


def row_done():
    W, H = 132, 21
    c = Canvas(W, H)
    # Goldene Plakette: zwei Flaechen, dazwischen zwei Ditherzeilen. Ein voller
    # Verlauf ueber die ganze Hoehe sieht auf 21px nur nach Rauschen aus.
    c.rect(1, 1, W - 2, H - 2, GOLD_L)
    for x in range(1, W - 1):
        if dither(x, 15, 0.5):
            c.set(x, 15, GOLD)
    c.rect(1, 16, W - 2, H - 2, GOLD)
    c.frame(0, 0, W - 1, H - 1, GOLD_D)
    c.hline(1, W - 2, 1, CREAM)                  # Lichtkante oben
    c.hline(1, W - 2, H - 2, GOLD_D)             # Schattenkante unten
    c.vline(1, 2, H - 3, GOLD_L)
    c.vline(W - 2, 2, H - 3, GOLD_D)
    c.cut_corners(0, 0, W - 1, H - 1)

    # Haken rechts, lokal (121,7,7,7). Graviert: dunkler Strich, darunter ein
    # Lichtsaum - auf Gold liest sich das als eingeschlagen, nicht aufgeklebt.
    check = (
        "......X",
        ".....XX",
        "X....XX",
        "XX..XX.",
        ".XXXXX.",
        "..XXX..",
        "...X...",
    )
    for dy, line in enumerate(check):
        for dx, ch in enumerate(line):
            if ch == "X":
                c.set(121 + dx, 7 + dy, WOOD_D2)
    for dy, line in enumerate(check):
        for dx, ch in enumerate(line):
            if ch == "X" and c.get(121 + dx, 8 + dy) != WOOD_D2:
                c.set(121 + dx, 8 + dy, CREAM)
    return c.image()


def row_hover():
    # Overlay: nur ein Rahmen, innen transparent. Wird auch auf 32x32 gezogen,
    # deshalb steckt jede Einzelheit im 2px-Rand.
    W, H = 132, 21
    c = Canvas(W, H)
    c.frame(0, 0, W - 1, H - 1, WOOD)
    c.hline(1, W - 2, 1, WOOD_L)
    c.cut_corners(0, 0, W - 1, H - 1)
    for cx, cy, sx, sy in ((0, 0, 1, 1), (W - 1, 0, -1, 1), (0, H - 1, 1, -1), (W - 1, H - 1, -1, -1)):
        c.set(cx + sx, cy, WOOD_D)
        c.set(cx, cy + sy, WOOD_D)
    return c.image()


def row_selected():
    # Zwei Ringe plus rote Eckwinkel. Innen bleibt alles frei, sonst deckt die
    # Auswahl den Zeilentext zu (siehe ACHIEVEMENTS_BOOK_UI.md, 5.3).
    W, H = 132, 21
    c = Canvas(W, H)
    c.frame(0, 0, W - 1, H - 1, INK)
    c.hline(1, W - 2, 1, WOOD_L)
    c.hline(1, W - 2, H - 2, WOOD_D)
    c.vline(1, 2, H - 3, WOOD)
    c.vline(W - 2, 2, H - 3, WOOD)
    c.cut_corners(0, 0, W - 1, H - 1)
    for cx, cy, sx, sy in ((1, 1, 1, 1), (W - 2, 1, -1, 1), (1, H - 2, 1, -1), (W - 2, H - 2, -1, -1)):
        c.set(cx, cy, RED)
        c.set(cx + sx, cy, RED)
        c.set(cx, cy + sy, RED)
    return c.image()


# =====================================================================
#  Fortschrittszellen - 9x4
# =====================================================================


def progress_cell_full():
    c = Canvas(9, 4)
    c.hline(0, 8, 0, RED_L)
    c.hline(0, 8, 1, RED)
    c.hline(0, 8, 2, RED)
    c.hline(0, 8, 3, RED_D)
    c.set(1, 0, CREAM)
    c.set(2, 0, CREAM)
    c.vline(0, 0, 3, RED_D)
    c.vline(8, 0, 3, RED_D)
    c.set(0, 0, RED)
    return c.image()


def progress_cell_empty():
    c = Canvas(9, 4)
    c.hline(0, 8, 0, PARCH4)                     # oben eingesenkt
    c.hline(0, 8, 1, PARCH3)
    c.hline(0, 8, 2, PARCH2)
    c.hline(0, 8, 3, PARCH2)
    c.vline(0, 0, 3, PARCH4)
    c.vline(8, 0, 3, PARCH4)
    return c.image()


# =====================================================================
#  Icon-Slots
# =====================================================================


def slot_unlocked(size, owned=False):
    """Holzrahmen. Aussen eine Fase im Licht, innen die Gegenfase.

    ``owned=True`` vergoldet den aeusseren Ring. Das ist der Rahmen fuer
    Unlocks, die man schon hat: "freigeschaltet" war vorher nur daran zu
    erkennen, dass die Kachel *nicht* die blasse Vertiefung war - eine
    Abwesenheit, und die sieht man nicht. Jetzt gibt es dafuer ein eigenes
    Zeichen. Der innere Ring bleibt Holz, sonst frisst das Gold die Motive,
    die selbst gelb sind.
    """
    c = Canvas(size, size)
    band = 3 if size <= 21 else 4
    last = size - 1

    if band == 3:
        rings = ((WOOD_L, WOOD_D2), (WOOD_D2, WOOD_L))
    else:
        rings = ((WOOD_L2, WOOD_D2), (WOOD, WOOD), (WOOD_D2, WOOD_L))
    if owned:
        rings = ((GOLD_L, GOLD_D2),) + rings[1:]

    c.rect(band, band, last - band, last - band, PARCH)   # Pergament im Fenster
    for i, (lit, dark) in enumerate(rings, start=1):
        c.hline(i, last - i, i, lit)
        c.vline(i, i, last - i, lit)
        c.hline(i, last - i, last - i, dark)
        c.vline(last - i, i, last - i, dark)
        mitre = GOLD_D if owned and i == 1 else WOOD
        c.set(last - i, i, mitre)                # Gehrung oben rechts
        c.set(i, last - i, mitre)                # Gehrung unten links
    c.frame(0, 0, last, last, INK)
    c.cut_corners(0, 0, last, last)

    if band == 4:
        # Maserung und vier Naegel - nur der grosse Rahmen hat dafuer genug
        # Flaeche, im kleinen wuerde beides zu Rauschen.
        for x in (9, 14, 20, 24):
            c.set(x, 2, WOOD_D)
            c.set(x + 1, last - 2, WOOD_D)
        for y in (9, 15, 22):
            c.set(2, y, WOOD_D)
            c.set(last - 2, y + 1, WOOD_D)
        for nx, ny in ((2, 2), (last - 2, 2), (2, last - 2), (last - 2, last - 2)):
            c.set(nx, ny, CREAM if owned else WOOD_L2)
            c.set(nx, ny + 1, GOLD_D2 if owned else WOOD_D3)

    if owned:
        # Ein kurzer Glanz in der oberen linken Ecke des Goldrands. Zusammen-
        # haengend, nicht verstreut - einzelne helle Pixel sehen auf Gold nach
        # Beschaedigung aus, nicht nach Licht.
        c.set(1, 1, CREAM)
        c.set(2, 1, CREAM)
        c.set(1, 2, CREAM)
    return c.image()


def slot_locked(size):
    """Leere Vertiefung im Pergament - das Motiv darueber wird nur gedaempft."""
    c = Canvas(size, size)
    band = 3 if size <= 21 else 4
    last = size - 1

    c.rect(1, 1, last - 1, last - 1, PARCH3)
    c.hline(1, last - 1, 1, PARCH4)              # eingesenkt: oben dunkel
    c.vline(1, 1, last - 1, PARCH4)
    c.hline(1, last - 1, last - 1, PARCH2)       # unten faellt Licht hinein
    c.vline(last - 1, 1, last - 1, PARCH2)
    if band == 4:
        c.hline(2, last - 2, 2, PARCH3)
        c.vline(2, 2, last - 2, PARCH3)
    c.frame(0, 0, last, last, LOCK_O)
    c.cut_corners(0, 0, last, last)

    # Angedeutete Schraffur, damit das Fenster als leer lesbar bleibt. Sie ist
    # absichtlich unterbrochen - durchgezogene Diagonalen wuerden mit dem
    # gedaempften Motiv streiten, das darueber liegt.
    for y in range(band, last - band + 1):
        for x in range(band, last - band + 1):
            if (x + y) % 8 == 0 and (x * 5 + y) % 3 == 0:
                c.set(x, y, PARCH2)
    return c.image()


# =====================================================================
#  reward_box - 128x20, 9-Slice 18/2/4/2
# =====================================================================


def reward_box():
    W, H = 128, 20
    c = Canvas(W, H)
    c.rect(1, 1, W - 2, H - 2, PARCH2)
    c.hline(1, W - 2, 1, PARCH)                  # Lichtkante oben
    c.hline(1, W - 2, H - 3, PARCH3)
    c.hline(1, W - 2, H - 2, PARCH3)
    c.vline(1, 1, H - 2, PARCH)
    c.vline(W - 2, 1, H - 2, PARCH3)
    c.frame(0, 0, W - 1, H - 1, PARCH4)
    c.cut_corners(0, 0, W - 1, H - 1)

    # Goldfassung fuer das Belohnungs-Icon, lokal (4,4,12,12).
    sx, sy, s = 4, 4, 12
    stops = ((0.0, GOLD_L), (1.0, GOLD))
    for y in range(sy + 1, sy + s - 1):
        a, b, f = ramp(stops, (y - sy - 1) / float(s - 3))
        for x in range(sx + 1, sx + s - 1):
            c.set(x, y, b if dither(x, y, f) else a)
    c.frame(sx, sy, sx + s - 1, sy + s - 1, GOLD_D)
    c.hline(sx + 1, sx + s - 2, sy + s - 2, GOLD_D2)
    c.set(sx + 1, sy + 1, CREAM)
    c.set(sx + 2, sy + 1, CREAM)
    c.set(sx + 1, sy + 2, CREAM)
    c.cut_corners(sx, sy, sx + s - 1, sy + s - 1, PARCH2)

    # Keine Rille zwischen Fassung und Text mehr: die Box wird als 9-Slice auf
    # die Breite der Detailspalte gestaucht, und eine 1px-Linie in der
    # gestauchten Mitte wuerde dabei verschluckt oder verdoppelt.
    return c.image()


# =====================================================================
#  icon_locked - 12x12, Notnagel wenn zu einem Eintrag kein Bild da ist
# =====================================================================


def icon_locked():
    c = Canvas(12, 12)
    art = (
        "............",
        "...XXXXXX...",
        "...Xh..sX...",
        "...Xh..sX...",
        "...Xh..sX...",
        "..XXXXXXXX..",
        ".XhhhhhhhhX.",
        ".XbbbkkbbbX.",
        ".XbbbkkbbbX.",
        ".XbbbbbbbbX.",
        ".XssssssssX.",
        "..XXXXXXXX..",
    )
    key = {"X": INK, "h": WOOD_L, "b": WOOD, "s": WOOD_D2, "k": WOOD_D3}
    c.stamp(0, 0, art, key)
    return c.image()


# =====================================================================
#  Ausgabe
# =====================================================================

BUBBLE_SIZES = (6, 8, 8, 10, 12, 12, 14, 16)


def build_all():
    out = {
        "bg_capsule": bg_capsule(),
        "book_panel": book_panel(),
        "book_spine": book_spine(),
        "tab_active": tab_active(),
        "tab_inactive": tab_inactive(),
        "row_normal": row_normal(),
        "row_done": row_done(),
        "row_hover": row_hover(),
        "row_selected": row_selected(),
        "progress_cell_full": progress_cell_full(),
        "progress_cell_empty": progress_cell_empty(),
        "slot_small_unlocked": slot_unlocked(21),
        "slot_small_locked": slot_locked(21),
        "slot_large_unlocked": slot_unlocked(32),
        "slot_large_owned": slot_unlocked(32, owned=True),
        "slot_large_locked": slot_locked(32),
        "reward_box": reward_box(),
        "icon_locked": icon_locked(),
    }
    for i, size in enumerate(BUBBLE_SIZES, start=1):
        out["bubble_%02d" % i] = bubble(size)
    return out


def write_sprites(sprites, folder):
    os.makedirs(folder, exist_ok=True)
    for name, img in sprites.items():
        img.save(os.path.join(folder, name + ".png"))
    return len(sprites)


def write_atlas(sprites, atlas_png, atlas_json):
    """Packt in genau die Rechtecke, die schon im JSON stehen - dann bleibt der
    Atlas gueltig, ohne dass irgendwo Koordinaten nachgezogen werden muessen."""
    with open(atlas_json, "r", encoding="utf-8") as fh:
        data = json.load(fh)
    size = data["meta"]["size"]
    sheet = Image.new("RGBA", (size["w"], size["h"]), (0, 0, 0, 0))
    for name, entry in data["frames"].items():
        img = sprites.get(name)
        if img is None:
            continue
        f = entry["frame"]
        sheet.paste(img, (f["x"], f["y"]), img)
    sheet.save(atlas_png)


def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    res = os.path.join(root, "Assets", "Resources", "AchievementsBook", "ui")
    art = os.path.join(root, "Assets", "Art", "UI_Objects", "AchievementsBook")

    sprites = build_all()
    n = write_sprites(sprites, res)
    print("%d Sprites -> %s" % (n, res))

    atlas_json = os.path.join(art, "atlas", "achievements_book_atlas.json")
    if os.path.exists(atlas_json):
        write_atlas(sprites, os.path.join(art, "atlas", "achievements_book_atlas.png"), atlas_json)
        print("Atlas aktualisiert")

    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import erfolgsbuch_vorschau
    erfolgsbuch_vorschau.write_previews(sprites, root, art)
    print("Vorschauen aktualisiert")

    return sprites


if __name__ == "__main__":
    main()
