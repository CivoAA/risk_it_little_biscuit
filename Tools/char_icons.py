"""
Zeichnet die Portraets fuer die Keksdose der Charakterauswahl im Hub
(HubCharacterSelectUI) - ein 42x42-Bild pro Fenster, aus dem Idle-Frame der
Figur heraus.

Aufbau von hinten nach vorn:
  - Strahlenkranz in der Akzentfarbe des Charakters (CharacterLooks.asset),
    harte Baender, zum Rand hin dunkler
  - Glitzer-Sterne, die je Frame an- und ausgehen
  - Schatten auf dem Boden
  - die Figur 1:1 als Sticker: helle Innenkontur, dunkle Aussenkontur,
    darunter ein versetzter Schlagschatten

Pro Charakter entsteht ein Streifen mit 4 Frames (168x42). Frame 0 ist das
ruhige Bild; laeuft der Cursor auf dem Platz, spielt die UI 0-1-2-3 ab: die
Figur huepft, der Kranz dreht sich ein Stueck weiter.

  Assets/Art/Chars/Icon_<Name>.png   (+ .meta beim ersten Lauf, 4 Sprites)

Neuer Charakter: unten in CHARS eintragen (Sheet, Akzent wie im
CharacterLooks.asset) und das Portraet dort unter 'portrait' eintragen.

Aufruf aus dem Projektordner:  python Tools/char_icons.py [--preview pfad.png]
"""

import math
import os
import sys

from PIL import Image

from unity_meta import write_strip_meta

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CHARS_DIR = os.path.join(ROOT, "Assets", "Art", "Chars")

SIZE = 42
FRAMES = 4
BOB = [0, 1, 2, 1]            # Figur hebt ab - Pixel nach oben je Frame
FEET_Y = 38                   # unterste Figurzeile in Ruhe

INK = (0x1C, 0x14, 0x19, 255)
CREAM = (0xFF, 0xF4, 0xE0, 255)
CLEAR = (0, 0, 0, 0)

# name, Sheet, Akzent (wie CharacterLooks.asset, 0..1)
CHARS = [
    ("Keks",    "Char_Keks.png",    (0.88, 0.63, 0.31)),
    ("Jam",     "Char_Jam.png",     (0.65, 0.48, 0.85)),
    ("Onigiri", "Char_Onigiri.png", (0.45, 0.68, 0.55)),
]


def shade(rgb, k):
    """Heller/dunkler, dunkle Toene leicht Richtung Pflaume verschoben."""
    r, g, b = rgb
    if k < 1:
        # Schatten werden kuehler/roetlicher statt nur grau - wirkt gemalt
        r, g, b = r * k + 0.10 * (1 - k) * 0.6, g * k, b * k + 0.12 * (1 - k) * 0.6
    else:
        r, g, b = r + (1 - r) * (k - 1), g + (1 - g) * (k - 1), b + (1 - b) * (k - 1)
    return (int(max(0, min(1, r)) * 255), int(max(0, min(1, g)) * 255),
            int(max(0, min(1, b)) * 255), 255)


def idle_frame(sheet):
    """Erste Zelle oben links (Zeile 1 = Idle von vorn), auf Inhalt beschnitten."""
    im = Image.open(os.path.join(CHARS_DIR, sheet)).convert("RGBA")
    cell = im.crop((0, 0, 32, 32))
    return cell.crop(cell.getbbox())


def dilate(mask, w, h):
    out = set(mask)
    for (x, y) in mask:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if 0 <= nx < w and 0 <= ny < h:
                out.add((nx, ny))
    return out


def stamp(px, shape, x, y):
    """Setzt ein kleines ASCII-Motiv mit der Mitte auf (x, y)."""
    rows, pal = shape
    h, w = len(rows), len(rows[0])
    for j, row in enumerate(rows):
        for i, ch in enumerate(row):
            if ch == ".":
                continue
            X, Y = x + i - w // 2, y + j - h // 2
            if 0 <= X < SIZE and 0 <= Y < SIZE:
                px[X, Y] = pal[ch]


STAR = ([".a.", "aca", ".a."], {"a": (0xFB, 0xE3, 0x9A, 255), "c": CREAM})
STAR_BIG = (["..a..", "..a..", "aacaa", "..a..", "..a.."], {"a": (0xFB, 0xE3, 0x9A, 255), "c": CREAM})

# Wo die Partikel sitzen: an den Raendern, nie hinter der Figur
SPOTS = [(5, 6), (36, 8), (35, 27), (5, 26), (28, 3), (13, 3)]

MOTIFS = {
    # Kruemel: kleine Keksbrocken mit dunkler Kante und hellem Glanz
    "Keks": {
        "shapes": {
            "s": (["lb", "bd"], {"l": (0xFF, 0xE8, 0xB0, 255), "b": (0xD9, 0xA0, 0x55, 255), "d": (0x5A, 0x34, 0x21, 255)}),
            "m": ([".dd.", "dlbd", "dbbd", ".dd."], {"l": (0xFF, 0xE8, 0xB0, 255), "b": (0xD9, 0xA0, 0x55, 255), "d": (0x5A, 0x34, 0x21, 255)}),
            "*": STAR,
        },
        "spots": [(5, 6, "m"), (36, 8, "*"), (35, 27, "s"), (5, 26, "s"), (28, 3, "s"), (13, 3, "*")],
    },
    # Marmelade: Herzchen und Blasen
    "Jam": {
        "shapes": {
            "h": (["j.j", "jjj", ".j."], {"j": (0xF0, 0x8A, 0x9A, 255)}),
            "o": ([".w.", "w.w", ".w."], {"w": (0xE6, 0xD2, 0xF7, 255)}),
            "*": STAR,
        },
        "spots": [(5, 6, "h"), (36, 8, "o"), (35, 27, "h"), (5, 26, "o"), (28, 3, "*"), (13, 3, "o")],
    },
    # Onigiri: Reiskoerner und Kirschblueten
    "Onigiri": {
        "shapes": {
            "r": (["cw", "wc"], {"c": CREAM, "w": (0xDD, 0xD5, 0xC8, 255)}),
            "p": ([".p.", "ppp", ".p."], {"p": (0xF3, 0xC4, 0xBC, 255)}),
            "*": STAR,
        },
        "spots": [(5, 6, "p"), (36, 8, "r"), (35, 27, "p"), (5, 26, "r"), (28, 3, "*"), (13, 3, "r")],
    },
}


def draw_frame(fig, accent, motif, f):
    img = Image.new("RGBA", (SIZE, SIZE), CLEAR)
    px = img.load()

    # ---- Strahlenkranz ------------------------------------------------
    cx, cy = 20.5, 19.5
    rays = 10
    step = 2 * math.pi / rays
    turn = f * step / 2          # halber Strahl je Frame: 4 Frames = 2 Strahlen
    ray_a = shade(accent, 0.74)
    ray_b = shade(accent, 0.40)
    halo = shade(accent, 0.95)
    halo_in = shade(accent, 1.30)
    rim = shade(accent, 0.26)
    for y in range(SIZE):
        for x in range(SIZE):
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            d = math.hypot(dx, dy)
            a = math.atan2(dy, dx) + turn
            c = ray_a if int(math.floor(a / step)) % 2 == 0 else ray_b
            if d > 22:
                c = shade((c[0] / 255, c[1] / 255, c[2] / 255), 0.70)
            if d < 17:
                c = halo
            if d < 14:
                c = halo_in
            px[x, y] = c

    # Boden: eine dunkle Kante unten, damit die Figur irgendwo steht
    for y in range(SIZE - 3, SIZE):
        for x in range(SIZE):
            px[x, y] = rim

    # ---- Partikel je Charakter ----------------------------------------
    for i, (sx, sy, kind) in enumerate(motif["spots"]):
        if (i + f) % 3 == 2:     # jeder ist mal aus - es funkelt
            continue
        drift = (f + i) % 2      # leichtes Auf und Ab
        stamp(px, motif["shapes"][kind], sx, sy - drift)

    # ---- Bodenschatten ------------------------------------------------
    bob = BOB[f]
    fw, fh = fig.size
    sw = max(8, fw - 4 - 2 * bob)
    sx0 = (SIZE - sw) // 2
    for x in range(sx0, sx0 + sw):
        edge = x in (sx0, sx0 + sw - 1)
        for y in (FEET_Y + 1, FEET_Y + 2):
            if edge and y == FEET_Y + 2:
                continue
            if 0 <= y < SIZE:
                px[x, y] = INK

    # ---- Figur als Sticker --------------------------------------------
    ox = (SIZE - fw) // 2
    oy = FEET_Y - fh + 1 - bob
    fpx = fig.load()
    body = {(ox + x, oy + y) for y in range(fh) for x in range(fw) if fpx[x, y][3] > 0}
    inner = dilate(body, SIZE, SIZE)
    outer = dilate(inner, SIZE, SIZE)

    shadow_col = shade(accent, 0.22)
    for (x, y) in outer:
        if x + 1 < SIZE and y + 1 < SIZE:
            px[x + 1, y + 1] = shadow_col
    for (x, y) in outer:
        px[x, y] = INK
    for (x, y) in inner:
        px[x, y] = CREAM
    for y in range(fh):
        for x in range(fw):
            p = fpx[x, y]
            if p[3] > 0:
                px[ox + x, oy + y] = p[:3] + (255,)

    # Glanzpunkt oben links auf der hellen Kontur - wie ein Lichtreflex
    return img


def strip(name, sheet, accent):
    fig = idle_frame(sheet)
    out = Image.new("RGBA", (SIZE * FRAMES, SIZE), CLEAR)
    for f in range(FRAMES):
        out.paste(draw_frame(fig, accent, MOTIFS[name], f), (f * SIZE, 0))
    return out


def main():
    preview = None
    if "--preview" in sys.argv:
        preview = sys.argv[sys.argv.index("--preview") + 1]

    strips = []
    for name, sheet, accent in CHARS:
        img = strip(name, sheet, accent)
        strips.append(img)
        base = "Icon_" + name
        png = os.path.join(CHARS_DIR, base + ".png")
        img.save(png)
        if not os.path.exists(png + ".meta"):
            write_strip_meta(png + ".meta", base, FRAMES, SIZE, SIZE, 100)
        print("geschrieben:", os.path.relpath(png, ROOT))

    if preview:
        pv = Image.new("RGBA", (SIZE * FRAMES + 8, len(strips) * (SIZE + 4) + 4), (40, 30, 36, 255))
        for i, s in enumerate(strips):
            pv.paste(s, (4, 4 + i * (SIZE + 4)))
        pv.resize((pv.width * 5, pv.height * 5), Image.NEAREST).save(preview)


if __name__ == "__main__":
    main()
