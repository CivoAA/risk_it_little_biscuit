"""
Gemeinsames Werkzeug fuer die Gegner der Eiswelt (Tools/eis_minzkugel.py,
eis_raketeneis.py, eis_softi.py).

Alle drei zeichnen gleich: Formen als Felder (< 1 innen) auf einem SS-fach
feineren Raster, Mehrheitsentscheid je Bildpixel, Licht aus der Feldnormalen
(oben links, wie Props und Gegner sonst), Umriss als letzter Schritt. Hier
liegt nur, was sich nicht je Gegner unterscheidet: Farben lesen, Feder,
Abtasten, Umriss, Speichern (Streifen + .meta + Bestiarium) und die Vorschau.
"""

import math
import os
import shutil
import sys
import uuid

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "Assets", "Art", "Gegner", "new", "eis")
BESTIARY_DIR = os.path.join(ROOT, "Assets", "Resources", "Bestiary")
PPU = 32

# Vanilleschnee aus Tools/eis_props.py - Hintergrund der Vorschau
SNOW_BG = (205, 208, 234, 255)
WAFFLE_BG = (201, 138, 62, 255)

LIGHT = np.array([-0.55, 0.62, 0.56])
LIGHT = LIGHT / np.linalg.norm(LIGHT)


def rgb(h, a=255):
    h = h.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


def mix(a, b, t):
    return tuple(int(round(a[i] * (1 - t) + b[i] * t)) for i in range(3)) + (a[3],)


# ================================================================ Bewegung

def smooth(a, b, x):
    if x <= a:
        return 0.0
    if x >= b:
        return 1.0
    t = (x - a) / (b - a)
    return t * t * (3 - 2 * t)


def ease_out(a, b, x):
    if x <= a:
        return 0.0
    if x >= b:
        return 1.0
    t = (x - a) / (b - a)
    return 1 - (1 - t) ** 3


def wrap(x):
    return x - math.floor(x)


class Spring:
    """Gedaempfte Feder, die einem Ziel folgt (Nachwabbeln, Nachschwingen)."""

    def __init__(self, k, d):
        self.k, self.d = k, d
        self.x = None
        self.v = 0.0

    def step(self, target, dt, kick=0.0):
        if self.x is None:
            self.x = target
        self.v += kick
        a = self.k * (target - self.x) - self.d * self.v
        self.v += a * dt
        self.x += self.v * dt
        return self.x


def periodic(n_frames, fps, step_fn, reps=6, sub=40):
    """
    Laesst step_fn(k_fliessend, dt) ueber mehrere Schleifen laufen und gibt
    die Werte der letzten Schleife je ganzem Bild zurueck. Federn sind dann
    eingeschwungen und die Schleife nahtlos.
    """
    dt = 1.0 / (fps * sub)
    out = None
    for _ in range(reps):
        rec = []
        for i in range(n_frames * sub):
            v = step_fn(i / sub, dt)
            if i % sub == 0:
                rec.append(v)
        out = rec
    return out


# ================================================================ Raster

def grid(cw, ch, ss, ox, ground):
    """Feines Raster: X nach rechts ab ox, Y nach oben ab der Bodenzeile."""
    ys, xs = np.mgrid[0:ch * ss, 0:cw * ss]
    X = (xs + 0.5) / ss - ox
    Y = ground - (ys + 0.5) / ss
    return X, Y


def pixel_grid(cw, ch, ox, ground):
    ys, xs = np.mgrid[0:ch, 0:cw]
    return xs + 0.5 - ox, ground - (ys + 0.5)


def downsample(owner, cw, ch, ss, empty=0, keep_thin=()):
    """
    Mehrheitsentscheid je Bildpixel. Leer gewinnt bei mehr als der Haelfte;
    Klassen in keep_thin ueberleben schon ab 40 % (duenne Teile wie Stiele).
    """
    o = owner.reshape(ch, ss, cw, ss).transpose(0, 2, 1, 3).reshape(ch, cw, ss * ss)
    codes = np.unique(owner)
    counts = np.stack([(o == c).sum(-1) for c in codes], -1)
    solid = codes != empty
    if solid.any():
        best = codes[solid][np.argmax(counts[..., solid], -1)]
    else:
        best = np.full((ch, cw), empty, owner.dtype)
    pix = best.copy()
    pix[(o == empty).sum(-1) > ss * ss // 2] = empty
    for k in keep_thin:
        pix[((o == k).sum(-1) >= ss * ss * 0.4) & (pix == empty)] = k
    return pix


def lambert(F_fn, X, Y, depth, eps=0.5):
    """
    Licht aus einem Formfeld F (< 1 innen): Gradient = Flaechenneigung,
    sqrt(1 - F) = Woelbung zum Betrachter. depth staucht/streckt die Woelbung.
    """
    F = F_fn(X, Y)
    gx = (F_fn(X + eps, Y) - F_fn(X - eps, Y)) / (2 * eps)
    gy = (F_fn(X, Y + eps) - F_fn(X, Y - eps)) / (2 * eps)
    z = np.sqrt(np.clip(1 - F, 0, 1))
    nx, ny, nz = gx * depth, gy * depth, 2 * z
    ln = np.sqrt(nx * nx + ny * ny + nz * nz) + 1e-6
    lam = (nx * LIGHT[0] + ny * LIGHT[1] + nz * LIGHT[2]) / ln
    return lam, nx / ln, ny / ln


def ramp(l, pal, cuts=(0.86, 0.66, 0.40, 0.12)):
    """pal = (deep, shade, mid, base, light, hi) -> Farbe zum Lichtwert l."""
    deep, shade, mid, base, light, hi = pal
    if l > cuts[0]:
        return hi
    if l > cuts[1]:
        return light
    if l > cuts[2]:
        return base
    if l > cuts[3]:
        return mid
    if l > -0.15:
        return shade
    return deep


def neighbours(pix, r, c):
    h, w = pix.shape
    out = []
    for a, b in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        rr, cc = r + a, c + b
        out.append(pix[rr, cc] if 0 <= rr < h and 0 <= cc < w else 0)
    return out


def outline(img, pix, line_of, empty=(0,), skip=()):
    """Jeder bemalte Pixel mit leerem Nachbarn wird Umriss (Farbe je Klasse)."""
    out = img.copy()
    h, w = pix.shape
    for r in range(h):
        for c in range(w):
            m = pix[r, c]
            if m in empty or m in skip:
                continue
            if any(n in empty for n in neighbours(pix, r, c)):
                col = line_of(m)
                if col is not None:
                    out[r, c] = col
    return out


def put(img, r, c, col, only=None, pix=None):
    h, w = img.shape[:2]
    if not (0 <= r < h and 0 <= c < w):
        return False
    if only is not None and pix[r, c] not in only:
        return False
    img[r, c] = col
    return True


def stamp(img, rows, r0, c0, colors, mirror=False, pix=None, only=None):
    """Text-Stempel: jedes Zeichen eine Farbe aus colors, '.' = frei."""
    for dr, row in enumerate(rows):
        row = row[::-1] if mirror else row
        for dc, ch in enumerate(row):
            if ch == "." or ch not in colors:
                continue
            put(img, r0 + dr, c0 + dc, colors[ch], only, pix)


# ================================================================ Speichern

def save_all(name, frames, cell_w, cell_h, pivot_px, fps, bestiary_id=None,
             bestiary_frame=0, preview=None, preview_bg=SNOW_BG, per_row=None):
    """
    Streifen nach Assets/Art/Gegner/new/eis/<name>.png (+ .meta nur beim
    ersten Mal), Bestiarium-Bild (zugeschnitten, nur deckende Pixel) und auf
    Wunsch eine Vorschau (GIF 8-fach + Bogen 3-fach).
    pivot_px = (Spalte, Zeile von oben) des Pivots im Bild.
    """
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    from unity_meta import write_strip_meta

    strip = np.concatenate(frames, axis=1)
    os.makedirs(OUT_DIR, exist_ok=True)
    png = os.path.join(OUT_DIR, name + ".png")
    Image.fromarray(strip).save(png)
    meta = png + ".meta"
    if not os.path.exists(meta):
        write_strip_meta(meta, name, len(frames), cell_w, cell_h, PPU,
                         pivot=(pivot_px[0] / cell_w, (cell_h - pivot_px[1]) / cell_h),
                         max_size=8192 if strip.shape[1] > 2048 else 2048)
    print("%s  %d Bilder  %dx%d  %g fps" % (name, len(frames), cell_w, cell_h, fps))

    if bestiary_id:
        a = frames[bestiary_frame]
        solid = a[..., 3] == 255
        rows = np.where(solid.any(1))[0]
        cols = np.where(solid.any(0))[0]
        crop = a[rows[0]:rows[-1] + 1, cols[0]:cols[-1] + 1].copy()
        crop[crop[..., 3] < 255] = 0
        path = os.path.join(BESTIARY_DIR, bestiary_id + ".png")
        Image.fromarray(crop).save(path)
        bmeta = path + ".meta"
        if not os.path.exists(bmeta):
            src = os.path.join(BESTIARY_DIR, "EvilSlime.png.meta")
            with open(src, "r") as fh:
                text = fh.read()
            text = text.replace("cdfa7c35c19f454f813a4a6b1fe67fbc", uuid.uuid4().hex)
            with open(bmeta, "w", newline="\n") as fh:
                fh.write(text)

    if preview:
        k = 8
        pics = []
        for fr in frames:
            im = Image.new("RGBA", (cell_w, cell_h), preview_bg)
            im.alpha_composite(Image.fromarray(fr))
            pics.append(im.resize((cell_w * k, cell_h * k), Image.NEAREST).convert("P", palette=Image.ADAPTIVE))
        pics[0].save(preview, save_all=True, append_images=pics[1:], duration=int(round(1000 / fps)), loop=0)
        per = per_row or len(frames)
        rows_n = int(math.ceil(len(frames) / per))
        sheet = Image.new("RGBA", (cell_w * per, cell_h * rows_n), preview_bg)
        for n, fr in enumerate(frames):
            sheet.alpha_composite(Image.fromarray(fr), ((n % per) * cell_w, (n // per) * cell_h))
        sheet.resize((sheet.width * 3, sheet.height * 3), Image.NEAREST).save(preview.replace(".gif", "_sheet.png"))
    return png


def arg_preview():
    args = sys.argv[1:]
    if "--preview" in args:
        return args[args.index("--preview") + 1]
    return None
