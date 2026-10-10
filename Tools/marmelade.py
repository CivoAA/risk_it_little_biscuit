"""
Zeichnet die Throwing-Jam-Jar-Waffe der Marmelade neu - fliegendes Glas und
Lache, jede Lachengroesse als EIGENES Bild, nie skaliert.

Frueher: das 128er-Icon (PPU 128) flog mit Scale 0.5, die Lache war ein
256er-Bild (PPU 32) mit Scale 0.7 x AOE - beides Mixels auf der pixelgenauen
Lauf-Kamera (32 PPU). Jetzt waehlt PixelPool.cs die naechste Groessenstufe.

Farben aus Assets/Art/Chars/Char_Jam.png (lila Marmelade, blaues Karotuch).

Ausgabe (PPU 32, Pivot Mitte):

  Assets/Resources/Weapons/jam_puddle_<D>.png  12 Bilder  (D = Lachendurchmesser,
      Bild hat Rand fuer Spritzer)
      0-2   Aufprall: Klecks spritzt auf, Scherben und Tropfen fliegen
      3-8   Schleife: Glanz wandert, Blasen blubbern
      9-11  Eintrocknen: zieht sich zusammen
  Assets/Resources/Weapons/jam_jar.png         8 Bilder 20x20  Glas ueberschlaegt sich

  D = 120, 144, ... 264

Aufruf:  python Tools/marmelade.py [--preview pfad.png] [--dry]
"""

import math
import os
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kaffeepool import hsh, outline_px, save_strip  # noqa: E402
from schoko_milch import hx  # noqa: E402

CLEAR = (0, 0, 0, 0)

LINE = hx("#261757")
J_D = hx("#692888")
J = hx("#9638b6")
J_L = hx("#b362ce")
J_HI = hx("#e6c2f0")
GLOSS = hx("#fbeefe")
BERRY = hx("#4e1c6e")
BERRY_L = hx("#7a3496")

GL = hx("#d7e9f4")
GL_HI = hx("#ffffff")
GL_D = hx("#7fb8f5")

LID_L, LID, LID_D = hx("#7fb8f5"), hx("#5c7de4"), hx("#2c40a8")
BAND = hx("#a15d43")

DIAMETERS = list(range(120, 265, 24))
RATIO = 0.84
GROW = 3
LOOP = 6
DRAIN = 3


def canvas_for(d):
    pad = max(10, (d // 10) // 2 * 2)
    w = d + 2 * pad
    h = int(round(d * RATIO / 2.0)) * 2 + 2 * pad
    return w, h, pad


def splat_plan(d):
    """Feste Form je Groesse: Wellen, Ausbuchtungen, Tropfen, Fruchtstuecke, Scherben."""
    s = d
    waves = [(k, 0.035 + 0.03 * hsh(k, s, 1), hsh(k, s, 2) * 6.28) for k in (3, 4, 6, 9)]
    bulges = []
    for i in range(6 + d // 40):
        ang = (i + 0.7 * hsh(i, s, 3)) / (6 + d // 40) * 2 * math.pi
        bulges.append((ang, 0.10 + 0.08 * hsh(i, s, 4)))
    drops = []
    for i in range(5 + d // 30):
        ang = hsh(i, s, 5) * 2 * math.pi
        drops.append((ang, 1.12 + 0.16 * hsh(i, s, 6), 1 + int(hsh(i, s, 7) * (1.5 + d / 120.0))))
    chunks = []
    for i in range(4 + d // 24):
        ang = hsh(i, s, 8) * 2 * math.pi
        rr = 0.15 + 0.6 * math.sqrt(hsh(i, s, 9))
        chunks.append((math.cos(ang) * rr, math.sin(ang) * rr, 2 + int(hsh(i, s, 10) * (1.5 + d / 90.0))))
    shards = []
    for i in range(3 + d // 80):
        ang = hsh(i, s, 11) * 2 * math.pi
        rr = 0.2 + 0.55 * hsh(i, s, 12)
        shards.append((math.cos(ang) * rr, math.sin(ang) * rr, int(hsh(i, s, 13) * 4), 4 + int(d / 60)))
    blubs = []
    for i in range(2 + d // 80):
        ang = hsh(i, s, 14) * 2 * math.pi
        rr = 0.2 + 0.45 * hsh(i, s, 15)
        blubs.append((math.cos(ang) * rr, math.sin(ang) * rr, 2 + int(d / 100), int(hsh(i, s, 16) * LOOP)))
    return waves, bulges, drops, chunks, shards, blubs


def radius_at(th, plan, f, wob):
    waves, bulges = plan[0], plan[1]
    e = 1.0
    for k, amp, ph in waves:
        e += amp * math.sin(k * th + ph + wob * (1 if k % 2 else -1))
    for ang, size in bulges:
        da = math.atan2(math.sin(th - ang), math.cos(th - ang))
        e += size * max(0.0, 1.0 - (da / 0.28) ** 2)
    return e * f


def puddle_frame(d, f, wob, gloss_shift, plan, drops_t, shards_fly, blub_k, chunk_vis=True):
    w, h, pad = canvas_for(d)
    img = Image.new("RGBA", (w, h), CLEAR)
    px = img.load()
    cx, cy = (w - 1) / 2.0, (h - 1) / 2.0
    a, b = d / 2.0 - 1.0, d * RATIO / 2.0 - 1.0
    _, _, drops, chunks, shards, blubs = plan

    def inside(x, y):
        u, v = (x - cx) / a, (y - cy) / b
        return math.hypot(u, v) <= radius_at(math.atan2(v, u), plan, f, wob)

    # ---- Grundform + Schattierung (Licht oben links)
    for y in range(h):
        for x in range(w):
            u, v = (x - cx) / a, (y - cy) / b
            r = math.hypot(u, v)
            th = math.atan2(v, u)
            e = radius_at(th, plan, f, wob)
            if r > e:
                continue
            rad = math.hypot(a * math.cos(th), b * math.sin(th))
            dpx = (e - r) * rad
            # Richtung zum Licht: oben links hell, unten rechts dunkel
            lit = -(math.cos(th) * 0.6 + math.sin(th) * 0.8)
            if dpx < 2.0 + (1.0 if lit < -0.2 else 0.0) and lit < 0.3:
                col = J_D
            elif dpx < 3.0 + d / 60.0 and lit > 0.35:
                col = J_L
            elif dpx < 4.5 + d / 60.0 and lit > 0.62:
                col = GLOSS if lit > 0.86 and dpx < 4.0 + d / 60.0 else J_HI
            elif dpx < 5.0 + d / 50.0 and lit < -0.45:
                col = J_D if (int(x) + int(y)) % 2 == 0 and dpx > 3.5 + d / 50.0 else (J_D if dpx < 3.5 + d / 50.0 else J)
            else:
                col = J
            px[x, y] = col

    # ---- Fruchtstuecke
    if chunk_vis:
        for (u, v, s) in chunks:
            bx, by = cx + u * a * f, cy + v * b * f
            for dx in range(-s, s + 1):
                for dy in range(-s, s + 1):
                    nx, ny = int(round(bx + dx)), int(round(by + dy * 0.85))
                    if (dx * dx + dy * dy) > s * s * 0.8 + hsh(dx, dy, s) * s:
                        continue
                    if 0 <= nx < w and 0 <= ny < h and px[nx, ny] in (J, J_L, J_D):
                        px[nx, ny] = BERRY_L if dx + dy < -s * 0.5 else BERRY
            hx_, hy_ = int(round(bx - s * 0.4)), int(round(by - s * 0.4))
            if 0 <= hx_ < w and 0 <= hy_ < h and px[hx_, hy_] in (BERRY, BERRY_L):
                px[hx_, hy_] = J_HI

    # ---- Glanzlichter: grosser Bogen oben links + kleine Funkel
    if f > 0.6:
        # zwei Lichtlinsen oben links
        for (gu, gv, gw, gh) in ((-0.40, -0.42, 3 + d // 48, 1 + d // 96), (-0.18, -0.56, 1 + d // 72, 1)):
            gx0, gy0 = cx + gu * a * f, cy + gv * b * f
            for dx in range(-gw, gw + 1):
                for dy in range(-gh, gh + 1):
                    if (dx / (gw + 0.5)) ** 2 + (dy / (gh + 0.5)) ** 2 > 1.0:
                        continue
                    ix, iy = int(round(gx0 + dx - dy)), int(round(gy0 + dy))
                    if 0 <= ix < w and 0 <= iy < h and px[ix, iy] in (J, J_L, J_D):
                        px[ix, iy] = GLOSS if abs(dy) < gh or gh == 0 else J_HI
        # Funkel, wandert pro Bild
        for j, (su, sv) in enumerate(((-0.10, -0.55), (0.42, -0.30), (-0.55, 0.10))):
            on = (gloss_shift + j * 2) % LOOP
            if on > 2:
                continue
            sx, sy = int(round(cx + su * a * f)), int(round(cy + sv * b * f))
            pts = [(0, 0)] if on != 1 else [(0, 0), (1, 0), (-1, 0), (0, 1), (0, -1)]
            for dx, dy in pts:
                if 0 <= sx + dx < w and 0 <= sy + dy < h and px[sx + dx, sy + dy][3]:
                    px[sx + dx, sy + dy] = GLOSS if (dx, dy) == (0, 0) else J_HI

    # ---- Glasscherben (liegen in der Lache)
    for (u, v, kind, s) in shards:
        sx, sy = cx + u * a * f, cy + v * b * f
        if shards_fly > 0:
            sx += u * a * shards_fly
            sy += v * b * shards_fly - shards_fly * 6
        draw_shard(px, w, h, int(round(sx)), int(round(sy)), kind, s)

    # ---- Blasen
    if blub_k is not None:
        for (u, v, s, start) in blubs:
            stage = (blub_k - start) % LOOP
            bx, by = int(round(cx + u * a * f)), int(round(cy + v * b * f))
            draw_blub(px, w, h, bx, by, s, stage)

    # ---- Tropfen ausserhalb
    for (ang, rr, s) in drops:
        if drops_t <= 0:
            continue
        rr2 = rr * drops_t
        dx0, dy0 = cx + math.cos(ang) * rr2 * a, cy + math.sin(ang) * rr2 * b
        for dx in range(-s, s + 1):
            for dy in range(-s, s + 1):
                if dx * dx + dy * dy <= s * s + s * 0.5 or (s == 1 and dx >= 0 and dy >= 0):
                    nx, ny = int(round(dx0 + dx)), int(round(dy0 + dy))
                    if 0 <= nx < w and 0 <= ny < h:
                        px[nx, ny] = J_L if dx + dy < 0 and s > 1 else J
        if s >= 2:
            nx, ny = int(round(dx0 - s * 0.4)), int(round(dy0 - s * 0.4))
            if 0 <= nx < w and 0 <= ny < h:
                px[nx, ny] = GLOSS

    outline_px(img, LINE)
    return img


def draw_shard(px, w, h, x, y, kind, s):
    """Kleines Glasdreieck, vier Lagen."""
    for i in range(s):
        for j in range(s - i):
            if kind == 0:
                p = (x + j, y + i)
            elif kind == 1:
                p = (x - j, y + i)
            elif kind == 2:
                p = (x + j, y - i)
            else:
                p = (x - j, y - i)
            if 0 <= p[0] < w and 0 <= p[1] < h:
                edge = i == 0 or j == 0 or j == s - i - 1
                px[p[0], p[1]] = GL_D if edge and i == 0 else (GL if not edge else GL_HI if j == 0 else GL_D)


def draw_blub(px, w, h, x, y, s, stage):
    def put(xx, yy, c):
        if 0 <= xx < w and 0 <= yy < h and px[xx, yy][3]:
            px[xx, yy] = c

    if stage == 0:
        put(x, y, J_L)
    elif stage in (1, 2):
        r = s if stage == 2 else max(1, s - 1)
        for dx in range(-r - 1, r + 2):
            for dy in range(-r - 1, r + 2):
                dd = math.hypot(dx, dy * 1.2)
                if r - 0.5 <= dd < r + 0.6:
                    put(x + dx, y + dy, J_D if dy > 0 else J_L)
                elif dd < r - 0.5:
                    put(x + dx, y + dy, J_L)
        put(x - max(1, r // 2), y - max(1, r // 2), GLOSS)
    elif stage == 3:
        for dx, dy in ((-s - 1, 0), (s + 1, 0), (0, -s - 1), (-s, -s), (s, -s)):
            put(x + dx, y + dy, J_HI)
        put(x, y, J_D)


def puddle_strip(d):
    plan = splat_plan(d)
    frames = []
    # Aufprall
    frames.append(puddle_frame(d, 0.45, 0.0, 0, plan, 0.55, 0.9, None, chunk_vis=False))
    frames.append(puddle_frame(d, 0.82, 0.2, 0, plan, 0.85, 0.45, None))
    frames.append(puddle_frame(d, 1.04, 0.4, 0, plan, 1.0, 0.0, None))
    for k in range(LOOP):
        wob = 0.4 + 2 * math.pi * k / LOOP
        frames.append(puddle_frame(d, 1.0, wob * 0.5 if False else 0.4 + 0.25 * math.sin(2 * math.pi * k / LOOP),
                                   k, plan, 1.0, 0.0, k))
    for i, f in enumerate((0.86, 0.68, 0.48)):
        frames.append(puddle_frame(d, f, 0.4, 0, plan, 1.0 - 0.3 * (i + 1), 0.0, None, chunk_vis=i < 2))
    w, h, _ = canvas_for(d)
    return frames, w, h


# ----------------------------------------------------------------------------
#  Glas im Flug
# ----------------------------------------------------------------------------

JAR = [
    "..oooooooo..",
    ".oLlLlLlLlo.",
    "oLlLlLlLlLlo",
    "olLlLlLlLlLo",
    "oddddddddddo",
    ".obbbbbbbbo.",
    ".oggggggggo.",
    "ogwhjjjjjjJo",
    "ogwhjjjjjjJo",
    "ogwjjjBjjjJo",
    "oghjjjjjJjJo",
    "ogjjBjjjjJJo",
    ".ogJJJJJJJo.",
    "..oooooooo..",
]
JAR_COLORS = {"o": LINE, "L": LID_L, "l": LID, "d": LID_D, "b": BAND, "g": GL,
              "w": GLOSS, "h": J_L, "j": J, "J": J_D, "B": BERRY}
JAR_SIZE = 20
JAR_FRAMES = 8


def jar_frame(i):
    """Rotsprite light: 8x hoch, drehen, Mittenpixel nehmen."""
    up = 8
    src_w, src_h = len(JAR[0]), len(JAR)
    out = Image.new("RGBA", (JAR_SIZE, JAR_SIZE), CLEAR)
    px = out.load()
    ang = -2 * math.pi * i / JAR_FRAMES
    ca, sa = math.cos(ang), math.sin(ang)
    c = (JAR_SIZE - 1) / 2.0
    for y in range(JAR_SIZE):
        for x in range(JAR_SIZE):
            # mehrere Unterproben, die haeufigste Farbe gewinnt
            votes = {}
            for sy in range(up):
                for sx in range(up):
                    fx = x - c + (sx + 0.5) / up - 0.5
                    fy = y - c + (sy + 0.5) / up - 0.5
                    lx = fx * ca + fy * sa + src_w / 2.0
                    ly = -fx * sa + fy * ca + src_h / 2.0
                    ix, iy = int(math.floor(lx)), int(math.floor(ly))
                    ch = JAR[iy][ix] if 0 <= ix < src_w and 0 <= iy < src_h else "."
                    votes[ch] = votes.get(ch, 0) + 1
            best = max(votes.items(), key=lambda kv: kv[1])[0]
            if best != "." and votes[best] >= up * up * 0.3:
                px[x, y] = JAR_COLORS[best]
            elif "o" in votes and votes["o"] > up * up * 0.35:
                px[x, y] = LINE
    return out


def main():
    args = sys.argv[1:]
    dry = "--dry" in args
    preview = args[args.index("--preview") + 1] if "--preview" in args else None

    strips = {}
    for d in DIAMETERS:
        frames, w, h = puddle_strip(d)
        strips[d] = (frames, w, h)
        save_strip(frames, "jam_puddle_%d" % d, w, h, dry)
    jar = [jar_frame(i) for i in range(JAR_FRAMES)]
    save_strip(jar, "jam_jar", JAR_SIZE, JAR_SIZE, dry)

    if preview:
        frames, w, h = strips[144]
        sheet = Image.new("RGBA", (w * 6 + 70, h * 2 + 30 + 120), (88, 112, 78, 255))
        for i, fr in enumerate(frames):
            sheet.alpha_composite(fr, (10 + (i % 6) * (w + 10), 10 + (i // 6) * (h + 10)))
        for i, fr in enumerate(jar):
            sheet.alpha_composite(fr.resize((JAR_SIZE * 4, JAR_SIZE * 4), Image.NEAREST),
                                  (10 + i * 88, 2 * (h + 10) + 10))
        sheet.save(preview)
        allw = sum(s[1] + 8 for s in strips.values()) + 8
        maxh = max(s[2] for s in strips.values())
        sizes = Image.new("RGBA", (allw, maxh + 16), (88, 112, 78, 255))
        x = 8
        for frames, w, h in strips.values():
            sizes.alpha_composite(frames[GROW + 1], (x, 8))
            x += w + 8
        sizes.save(preview.replace(".png", "_sizes.png"))
        print("Vorschau:", preview)


if __name__ == "__main__":
    main()
