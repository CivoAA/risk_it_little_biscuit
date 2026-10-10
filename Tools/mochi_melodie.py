"""
Bilder und Toene fuer die Mochi-Melodie (mochi_melody) - Mochis Startwaffe
(Charakter 5). Mochi singt im Takt: auf den Schlaegen 1-3 fliegt je eine
Note los und jagt einen Gegner, auf der Vier platzt ein bunter Akkord-Ring um
sie herum. Die Toene spielen dabei wirklich eine kleine Melodie.

Farben: Mochi-Rosa + Kopfhoerer-Minze aus Tools/char_erdbeere.py, dazu
Lavendel und Zitrone fuer den Akkord.

  Assets/Resources/Weapons/
    melody_note_<k>.png     4 Bilder 14x16 je Farbe k (0 rosa, 1 minze, 2 lavendel, 3 zitrone)
    melody_spark.png        5 Bilder 7x7: Glitzer der Notenspur
    melody_hit.png          7 Bilder 28x28: Notentreffer
    melody_chord_<r>.png    9 Bilder: Akkord-Ring mit Radius r (Pixel; 32 = 1 Tile)
  Assets/Resources/Sounds/
    melody_tone.wav         weicher Spieluhr-/Marimbaton (C5), MochiMelody stimmt per pitch um
    melody_chime.wav        Glitzern fuer den Akkord
  Assets/Art/Icons/fin_mochi_melody.png            64x64 PPU 64 (32er Pixel x2)
  Assets/Resources/Workbench/mochi_melody_14/_10   Werkbank

Die Lauf-Kamera ist pixelgenau - es wird nie skaliert, MochiMelody nimmt den
naechstpassenden Ring (CHORD_RADII = MochiMelody.ChordRadii). .meta nur beim
ersten Lauf.

Aufruf:  python Tools/mochi_melodie.py [--preview pfad.png] [--dry]
"""

import math
import os
import struct
import sys
import wave

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from unity_meta import write_strip_meta  # noqa: E402
from schoko_milch import new_single_meta, outline  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RES_WEAPONS = os.path.join(ROOT, "Assets", "Resources", "Weapons")
RES_SOUNDS = os.path.join(ROOT, "Assets", "Resources", "Sounds")
ICONS = os.path.join(ROOT, "Assets", "Art", "Icons")
WORKBENCH = os.path.join(ROOT, "Assets", "Resources", "Workbench")

CHORD_RADII = [40, 52, 64, 80]
CHORD_FRAMES = 9
HIT_FRAMES = 7
NOTE_W, NOTE_H = 14, 16

CLEAR = (0, 0, 0, 0)


def hx(s, a=255):
    s = s.lstrip('#')
    return tuple(int(s[i:i + 2], 16) for i in (0, 2, 4)) + (a,)


def with_alpha(c, a):
    return c[:3] + (max(0, min(255, int(a))),)


WHITE = hx('#ffffff')

# je Farbe: Kontur, Schatten, Fuellung, Licht
PALETTES = [
    (hx('#7a1f4f'), hx('#e0588f'), hx('#ff8fb8'), hx('#ffd6e6')),   # rosa (Mochi)
    (hx('#1f5560'), hx('#3fae95'), hx('#7fe0c6'), hx('#d9fff2')),   # minze (Kopfhoerer)
    (hx('#43246e'), hx('#9a74e0'), hx('#c7a4ff'), hx('#efe3ff')),   # lavendel
    (hx('#7a4a12'), hx('#f0b43c'), hx('#ffe27a'), hx('#fff6c9')),   # zitrone
]

# ---------------------------------------------------------------- Noten

EIGHTH = [            # Achtelnote
    ".....nn....",
    ".....nnn...",
    ".....n.nn..",
    ".....n..nn.",
    ".....n...n.",
    ".....n...n.",
    ".....n..n..",
    ".....n.....",
    "..nnnn.....",
    ".nnhnnn....",
    ".nhnnnn....",
    ".nnnnns....",
    "..nnss.....",
]
BEAMED = [            # zwei Achtel mit Balken
    "...nnnnnnnn",
    "...nnnnnnnn",
    "...n......n",
    "...n......n",
    "...n......n",
    "...n......n",
    "...n......n",
    ".nnn....nnn",
    "nhnn...nhnn",
    "nnns...nnns",
    ".ss.....ss.",
]
SHAPES = [EIGHTH, EIGHTH, BEAMED, EIGHTH]


def note_frame(k, i):
    """i: 0 ruhig, 1 Kopf nach rechts geneigt, 2 ruhig + Funkeln, 3 nach links."""
    ol, sh, fi, li = PALETTES[k]
    rows = SHAPES[k]
    img = Image.new("RGBA", (NOTE_W, NOTE_H), CLEAR)
    h = len(rows)
    oy = (NOTE_H - h) // 2
    ox = (NOTE_W - len(rows[0])) // 2
    lean = {0: 0, 1: 1, 2: 0, 3: -1}[i]
    for y, row in enumerate(rows):
        shift = lean if y < h // 2 else 0
        for x, ch in enumerate(row):
            if ch == '.':
                continue
            c = {'n': fi, 's': sh, 'h': li}[ch]
            if ch == 'h' and i == 2:
                c = WHITE
            img.putpixel((ox + x + shift, oy + y), c)
    return outline(img, ol)


def note_strip(k):
    frames = [note_frame(k, i) for i in range(4)]
    return strip(frames, NOTE_W, NOTE_H), frames


def strip(frames, w, h=None):
    h = h or w
    out = Image.new("RGBA", (w * len(frames), h), CLEAR)
    for i, f in enumerate(frames):
        out.alpha_composite(f, (i * w, 0))
    return out


# ---------------------------------------------------------------- Glitzer + Treffer

def spark_frame(i):
    img = Image.new("RGBA", (7, 7), CLEAR)
    r = [3, 3, 2, 1, 1][i]
    col = [WHITE, PALETTES[0][3], PALETTES[0][2], PALETTES[0][2], PALETTES[0][1]][i]
    a = [255, 255, 230, 200, 150][i]
    for d in range(-r, r + 1):
        img.putpixel((3 + d, 3), with_alpha(col, a))
        img.putpixel((3, 3 + d), with_alpha(col, a))
    if i < 2:
        img.putpixel((3, 3), WHITE)
    return img


def hit_frame(i):
    S = 28
    img = Image.new("RGBA", (S, S), CLEAR)
    px = img.load()
    c = (S - 1) / 2.0
    t = (i + 1) / HIT_FRAMES
    rr = 3 + 9 * (1 - (1 - t) ** 2)
    width = 2.2 if i < 3 else 1.2
    alpha = 255 if i < 4 else int(255 * (1 - (i - 3) / 4))
    for y in range(S):
        for x in range(S):
            d = math.hypot(x - c, y - c)
            if abs(d - rr) < width / 2 + 0.3:
                ang = math.atan2(y - c, x - c)
                k = int(((ang + math.pi) / (2 * math.pi)) * 4 + i) % 4
                px[x, y] = with_alpha(PALETTES[k][2] if i > 0 else WHITE, alpha)
    if i == 0:
        for y in range(S):
            for x in range(S):
                if math.hypot(x - c, y - c) < 3.2:
                    px[x, y] = WHITE
    # vier Sternchen fliegen schraeg raus
    for k, ang in enumerate((45, 135, 225, 315)):
        a = math.radians(ang)
        d = 4 + 9 * t
        sx, sy = int(round(c + math.cos(a) * d)), int(round(c + math.sin(a) * d))
        sz = 1 if i < 5 else 0
        col = with_alpha(PALETTES[k][3] if i < 3 else PALETTES[k][2], alpha)
        for dd in range(-sz, sz + 1):
            if 0 <= sx + dd < S:
                px[sx + dd, sy] = col
            if 0 <= sy + dd < S:
                px[sx, sy + dd] = col
    return img


# ---------------------------------------------------------------- Akkord-Ring

MINI = ["...x.", "...xx", "...x.", ".xxx.", "xxx..", ".x..."]


def chord_size(r):
    return 2 * r + 10


def chord_frame(r, i):
    S = chord_size(r)
    img = Image.new("RGBA", (S, S), CLEAR)
    px = img.load()
    c = (S - 1) / 2.0
    t = (i + 1) / CHORD_FRAMES
    e = 1 - (1 - t) ** 3
    outer = r * (0.3 + 0.7 * e)
    inner = outer * 0.72
    fade = 1.0 if i < 5 else 1 - (i - 4) / (CHORD_FRAMES - 4)
    w_out = 4.2 if i < 3 else (3.2 if i < 6 else 2.0)
    w_in = 1.4 if i < 6 else 1.0
    spin = i * 0.18
    for y in range(S):
        for x in range(S):
            d = math.hypot(x - c, y - c)
            ang = math.atan2(y - c, x - c) + spin
            k = int(((ang / (2 * math.pi)) % 1.0) * 8) % 4
            ol, sh, fi, li = PALETTES[k]
            if abs(d - outer) <= w_out / 2:
                # aussen hell, innen Farbe - liest sich als Welle
                col = li if d > outer - 0.4 else fi
                if i == 0:
                    col = WHITE
                px[x, y] = with_alpha(col, 255 * fade)
            elif abs(d - (outer + w_out / 2 + 0.6)) < 0.5 and i < 7:
                px[x, y] = with_alpha(sh, 200 * fade)
            elif abs(d - inner) <= w_in / 2 and i >= 1:
                px[x, y] = with_alpha(fi, 170 * fade)
    # Noten reiten auf dem Ring
    if 1 <= i <= CHORD_FRAMES - 2:
        for n in range(6):
            a = n * math.pi / 3 + spin * 1.6
            nx = int(round(c + math.cos(a) * outer)) - 2
            ny = int(round(c + math.sin(a) * outer)) - 3
            ol, sh, fi, li = PALETTES[n % 4]
            cells = set((nx + dx, ny + dy) for dy, row in enumerate(MINI)
                        for dx, ch in enumerate(row) if ch == 'x')
            for (X, Y) in cells:
                for a2, b2 in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    q = (X + a2, Y + b2)
                    if q not in cells and 0 <= q[0] < S and 0 <= q[1] < S:
                        px[q] = with_alpha(ol, 255 * fade)
            for (X, Y) in cells:
                if 0 <= X < S and 0 <= Y < S:
                    px[X, Y] = with_alpha(WHITE if i < 3 else fi, 255 * fade)
    return img


def chord_strip(r):
    frames = [chord_frame(r, i) for i in range(CHORD_FRAMES)]
    return strip(frames, chord_size(r)), frames


# ---------------------------------------------------------------- Icons

def icon():
    img = Image.new("RGBA", (32, 32), CLEAR)
    big = note_frame(0, 2)
    small = note_frame(2, 0)
    # Schallwellen links
    px = img.load()
    for k, rr in enumerate((6, 9)):
        for a in range(-50, 51, 4):
            x = int(round(9 - math.cos(math.radians(a)) * rr))
            y = int(round(18 + math.sin(math.radians(a)) * rr))
            if 0 <= x < 32 and 0 <= y < 32:
                px[x, y] = PALETTES[1][2 - k]
    img.alpha_composite(small, (17, 1))
    img.alpha_composite(big, (7, 12))
    for (x, y) in ((28, 20), (27, 21), (29, 21), (28, 22), (4, 6)):
        px[x, y] = PALETTES[3][2] if y != 6 else WHITE
    return outline(img, hx('#2a1026'))


SMALL = {
    ("mochi_melody", 14): [
        "......pp...",
        "......ppp..",
        "......p.pp.",
        "......p..p.",
        "......p..p.",
        "......p.p..",
        "......p....",
        "...pppp....",
        "..plppp....",
        "..ppppd....",
        "...ppd.....",
    ],
    ("mochi_melody", 10): [
        "....pp..",
        "....p.p.",
        "....p..p",
        "....p...",
        "..ppp...",
        ".plpp...",
        ".pppd...",
        "..pd....",
    ],
}
SMALL_PAL = {"p": PALETTES[0][2], "l": PALETTES[0][3], "d": PALETTES[0][1]}
WB_OUTLINE = (0x3B, 0x2B, 0x33, 255)


def small_icon(rows, canvas):
    img = Image.new("RGBA", (canvas, canvas), CLEAR)
    off = 1
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch != "." and x + off < canvas and y + off < canvas:
                img.putpixel((x + off, y + off), SMALL_PAL[ch])
    return outline(img, WB_OUTLINE)


# ---------------------------------------------------------------- Toene

RATE = 44100


def write_wav(path, samples):
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, s)) * 32000)) for s in samples))


def tone():
    """Weicher Marimba-/Spieluhrton auf C5: Grundton + leise Oberton, schneller Anschlag, Ausklang."""
    f = 523.25
    n = int(RATE * 0.55)
    out = []
    for i in range(n):
        t = i / RATE
        env = min(1.0, t / 0.004) * math.exp(-t * 7.5)
        s = (math.sin(2 * math.pi * f * t)
             + 0.28 * math.sin(2 * math.pi * f * 4 * t) * math.exp(-t * 30)
             + 0.12 * math.sin(2 * math.pi * f * 2 * t))
        out.append(0.55 * env * s)
    return out


def chime():
    """Glitzern: drei hohe Glockentoene kurz nacheinander."""
    n = int(RATE * 0.7)
    out = [0.0] * n
    for k, (f, start) in enumerate(((2093.0, 0.0), (2637.0, 0.05), (3136.0, 0.1))):
        s0 = int(start * RATE)
        for i in range(s0, n):
            t = (i - s0) / RATE
            env = min(1.0, t / 0.003) * math.exp(-t * 9)
            out[i] += 0.16 * env * (math.sin(2 * math.pi * f * t) + 0.3 * math.sin(2 * math.pi * f * 2.76 * t))
    return out


# ----------------------------------------------------------------------------

def main():
    dry = "--dry" in sys.argv
    preview = sys.argv[sys.argv.index("--preview") + 1] if "--preview" in sys.argv else None

    notes = [note_strip(k) for k in range(4)]
    spark = strip([spark_frame(i) for i in range(5)], 7)
    hit_frames = [hit_frame(i) for i in range(HIT_FRAMES)]
    hit = strip(hit_frames, 28)
    chords = {r: chord_strip(r) for r in CHORD_RADII}
    ic = icon().resize((64, 64), Image.NEAREST)
    small = {key: small_icon(rows, key[1]) for key, rows in SMALL.items()}

    if not dry:
        out = [("melody_note_%d" % k, notes[k][0], 4, NOTE_W, NOTE_H) for k in range(4)]
        out.append(("melody_spark", spark, 5, 7, 7))
        out.append(("melody_hit", hit, HIT_FRAMES, 28, 28))
        for r in CHORD_RADII:
            out.append(("melody_chord_%d" % r, chords[r][0], CHORD_FRAMES, chord_size(r), chord_size(r)))
        for base, img, frames, w, h in out:
            path = os.path.join(RES_WEAPONS, base + ".png")
            img.save(path)
            if not os.path.exists(path + ".meta"):
                write_strip_meta(path + ".meta", base, frames, w, h, 32,
                                 max_size=4096 if w * frames > 2048 else 2048)
        path = os.path.join(ICONS, "fin_mochi_melody.png")
        ic.save(path)
        if not os.path.exists(path + ".meta"):
            write_strip_meta(path + ".meta", "fin_mochi_melody", 1, 64, 64, 64)
        template = os.path.join(WORKBENCH, "salad_fan_14.png.meta")
        for (wid, size), img in small.items():
            path = os.path.join(WORKBENCH, "%s_%d.png" % (wid, size))
            img.save(path)
            new_single_meta(path + ".meta", template)
        os.makedirs(RES_SOUNDS, exist_ok=True)
        write_wav(os.path.join(RES_SOUNDS, "melody_tone.wav"), tone())
        write_wav(os.path.join(RES_SOUNDS, "melody_chime.wav"), chime())
        print("Geschrieben: melody_note_0-3, spark, hit, chord_%s, fin_mochi_melody, Werkbank, Toene"
              % "/".join(map(str, CHORD_RADII)))

    if preview:
        s = 4
        bg = (58, 74, 58, 255)
        r = 64
        W = chord_size(r)
        sheet = Image.new("RGBA", (W * 5 + 20, 2 * W + 120), bg)
        for i, f in enumerate(chords[r][1]):
            sheet.alpha_composite(f, (10 + (i % 5) * W, 10 + (i // 5) * W))
        y = 2 * W + 20
        for k in range(4):
            sheet.alpha_composite(notes[k][0], (10, y + k * 18))
        sheet.alpha_composite(spark, (80, y))
        sheet.alpha_composite(hit, (80, y + 12))
        sheet.alpha_composite(ic, (300, y))
        for k, ((wid, size), img) in enumerate(small.items()):
            sheet.alpha_composite(img, (380 + k * 20, y))
        sheet.resize((sheet.width * s, sheet.height * s), Image.NEAREST).save(preview)


if __name__ == "__main__":
    main()
