"""
Zeichnet den Kaffeepool neu - in jeder Groesse als EIGENES Bild, nie skaliert.

Die Lauf-Kamera ist pixelgenau (32 PPU, 320x180, ohne Upscale-RT). Frueher
wurde ein 256er-Bild (PPU 64) per localScale 1.75 ... 2.5 x AOE aufgezogen -
jedes Texel landete auf 0.9 ... 1.4 Bildschirmpixeln: Mixels. Jetzt gibt es
eine Groessenstufe je 32 px Breite, und PixelPool.cs nimmt die naechste.

Ausgabe (alle PPU 32, Pivot Mitte):

  Assets/Resources/Weapons/coffee_pool_<W>.png   12 Bilder W x H (H = 0.62 W)
      0-2   Einschenken: die Lache laeuft aus der Mitte auf, Spritzer
      3-8   Schleife: Crema-Spirale dreht sich, Blasen steigen und platzen
      9-11  Versickern: zieht sich zusammen, Spirale loest sich auf
  Assets/Resources/Weapons/coffee_steam.png      7 Bilder 10x18  Dampfkringel

  W = 160, 192, ... 416  (Breite der Lache inkl. Umriss)

Aufruf:  python Tools/kaffeepool.py [--preview pfad.png] [--dry]
"""

import math
import os
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from unity_meta import write_strip_meta  # noqa: E402
from schoko_milch import hx  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RES_WEAPONS = os.path.join(ROOT, "Assets", "Resources", "Weapons")

CLEAR = (0, 0, 0, 0)

LINE = hx("#2a1712")
EDGE = hx("#4f2814")
COF_D = hx("#2b140a")
COF = hx("#3c1d0e")
COF_M = hx("#4f2814")
COF_L = hx("#683720")
CREMA_D = hx("#8f522a")
CREMA = hx("#bf7d42")
CREMA_L = hx("#e2ab6f")
CREMA_HI = hx("#f6dcaa")

STEAM = hx("#f4ece0")
STEAM_D = hx("#cfc4c9")

WIDTHS = list(range(160, 417, 32))
RATIO = 0.62
GROW = 3
LOOP = 6
DRAIN = 3
FRAMES = GROW + LOOP + DRAIN

# Wellen am Rand: (Harmonische, Staerke, Phase, Umlaeufe je Schleife)
WOBBLE = [(3, 0.022, 0.4, 1), (5, 0.016, 2.1, -1), (7, 0.010, 4.0, 2), (11, 0.006, 1.3, -2)]


def hsh(x, y, s=0):
    n = (x * 374761393 + y * 668265263 + s * 2147483647) & 0xFFFFFFFF
    n = ((n ^ (n >> 13)) * 1274126177) & 0xFFFFFFFF
    return ((n ^ (n >> 16)) & 0xFFFF) / 65535.0


def size_for(w):
    h = int(round(w * RATIO / 2.0)) * 2
    return w, h


def edge_at(theta, phase):
    e = 1.0
    for k, amp, ph, turns in WOBBLE:
        e += amp * math.sin(k * theta + ph + turns * phase)
    return e


def pool_frame(w, h, f, phase, swirl_amount, rim_scale, splash, bubbles, foam=()):
    """f = Groesse der Lache (0..1), phase = 0..2pi Schleifenwinkel."""
    img = Image.new("RGBA", (w, h), CLEAR)
    px = img.load()
    cx, cy = (w - 1) / 2.0, (h - 1) / 2.0
    a, b = w / 2.0 - 1.5, h / 2.0 - 1.5          # Platz fuer den Umriss
    rim = max(3, int(round((4 + w / 64.0) * rim_scale)))
    arms = 2

    for y in range(h):
        for x in range(w):
            u, v = (x - cx) / a, (y - cy) / b
            r = math.hypot(u, v)
            th = math.atan2(v, u)
            e = edge_at(th, phase) * f
            if r > e:
                continue
            rad = math.hypot(a * math.cos(th), b * math.sin(th))
            d = (e - r) * rad                       # Abstand zum Rand in Pixeln

            # ---- Crema-Rand: schaumig, unten (vorn) einen Pixel dicker
            rim_here = rim + (1 if v > 0.35 else 0) + 1.3 * math.sin(th * (9 + w // 40) + 0.7) + 0.8 * math.sin(th * 23 + 2.0)
            if d < 1.0:
                col = EDGE
            elif d < rim_here:
                if d > rim_here - 1.0:
                    col = CREMA_D
                elif v < -0.15 and d < rim_here - 1.5:
                    col = CREMA_L
                else:
                    col = CREMA
                px[x, y] = col
                continue
            else:
                # ---- Kaffee: oben unter dem Rand Schatten, unten heller Glanz
                dd = d - rim_here
                if v < 0 and dd < 2.0 + rim * 0.5:
                    col = COF_D
                elif v > 0.25 and dd < 3.0 and abs(u) < 0.75:
                    col = COF_M
                else:
                    col = COF
                # leichte Tiefe zur Mitte
                if r < 0.30 * f and col == COF:
                    col = COF_M

                # ---- Crema-Spirale (perspektivisch flach wie die Lache)
                if swirl_amount > 0 and r < 0.74 * f and r > 0.06 * f:
                    rr = r / max(0.01, f)
                    ang = th - phase / arms * 1.0 - rr * 7.2
                    m = (ang * arms / (2 * math.pi)) % 1.0
                    dist = min(m, 1 - m) * (2 * math.pi / arms) / 7.2 * min(a, b) * f
                    width = (1.2 + w / 110.0) * swirl_amount * (1.0 - 0.5 * rr)
                    if rr > 0.5:                     # Arm laeuft spitz aus
                        width *= max(0.0, 1.0 - (rr - 0.5) / 0.24)
                    if dist < width:
                        col = CREMA_L if dist < width * 0.5 else CREMA
                    elif dist < width + 1.0 and width > 1.0 and swirl_amount > 0.5:
                        col = CREMA_D if col in (COF, COF_M) else col
            px[x, y] = col

    # ---- Glanzlicht oben links: zwei kleine Lichtlinsen auf dem Kaffee
    if f > 0.6:
        for (gu, gv, gl) in ((-0.42, -0.30, 0.10), (-0.24, -0.42, 0.045)):
            gx0, gy0 = cx + gu * a * f, cy + gv * b * f
            hl = max(2, int(gl * w * 0.45))
            for dx in range(-hl, hl + 1):
                for dy in (-1, 0, 1):
                    if (dx / (hl + 0.5)) ** 2 + (dy / 1.3) ** 2 > 1.0:
                        continue
                    ix, iy = int(round(gx0 + dx)), int(round(gy0 + dy))
                    if 0 <= ix < w and 0 <= iy < h and px[ix, iy] in (COF, COF_M, COF_D):
                        px[ix, iy] = CREMA_HI if dy == 0 and abs(dx) < hl * 0.6 else COF_L

    # ---- Schaumblasen auf dem Crema-Rand
    for (fa, fs) in foam:
        e = edge_at(fa, phase) * f
        rad = math.hypot(a * math.cos(fa), b * math.sin(fa))
        mid = e * rad - (rim + 0.5 + fs * 0.3)
        fx = int(round(cx + math.cos(fa) * mid * a / rad))
        fy = int(round(cy + math.sin(fa) * mid * b / rad))
        rr = max(1, int(round(fs * rim_scale)))
        for dx in range(-rr, rr + 1):
            for dy in range(-rr, rr + 1):
                if dx * dx + dy * dy > rr * rr + rr * 0.6:
                    continue
                ix, iy = fx + dx, fy + dy
                if 0 <= ix < w and 0 <= iy < h and px[ix, iy][3]:
                    c = CREMA_L
                    if dx + dy <= -rr:
                        c = CREMA_HI
                    elif dx + dy >= rr:
                        c = CREMA
                    px[ix, iy] = c

    # ---- Blasen
    for (bu, bv, size, stage) in bubbles:
        bx, by = int(round(cx + bu * a * f)), int(round(cy + bv * b * f))
        draw_bubble(px, w, h, bx, by, size, stage)

    # ---- Spritzer beim Einschenken
    for (su, sv, s) in splash:
        sx, sy = int(round(cx + su * a)), int(round(cy + sv * b))
        for dx in range(-s, s + 1):
            for dy in range(-s, s + 1):
                if dx * dx + dy * dy <= s * s + s and 0 <= sx + dx < w and 0 <= sy + dy < h:
                    px[sx + dx, sy + dy] = COF_M if dy <= -s + 0 and dx <= 0 else COF

    outline_px(img, LINE)
    return img


def draw_bubble(px, w, h, x, y, size, stage):
    def put(xx, yy, c):
        if 0 <= xx < w and 0 <= yy < h and px[xx, yy][3]:
            px[xx, yy] = c

    if stage == 0:          # klein auftauchend
        put(x, y, CREMA)
        put(x + 1, y, CREMA_D)
    elif stage in (1, 2):   # Blase: Ring + Glanzpunkt
        r = size if stage == 2 else max(1, size - 1)
        for dx in range(-r - 1, r + 2):
            for dy in range(-r - 1, r + 2):
                dd = math.hypot(dx, dy * 1.15)
                if r - 0.5 <= dd < r + 0.6:
                    put(x + dx, y + dy, CREMA_L if dy < 0 else CREMA)
                elif dd < r - 0.5:
                    put(x + dx, y + dy, COF_L)
        put(x - r // 2, y - r // 2, CREMA_HI)
    elif stage == 3:        # geplatzt: Tropfen fliegen weg
        for dx, dy in ((-size - 1, 0), (size + 1, 0), (0, -size - 1), (-size, -size), (size, -size)):
            put(x + dx, y + dy, CREMA_L)
        put(x, y, CREMA_D)


def outline_px(img, col):
    w, h = img.size
    src = img.copy().load()
    dp = img.load()
    for y in range(h):
        for x in range(w):
            if src[x, y][3]:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < w and 0 <= ny < h and src[nx, ny][3] > 128:
                    dp[x, y] = col
                    break


def bubble_plan(w):
    """Feste Blasen je Groesse: (u, v, Groesse, erstes Schleifenbild)."""
    count = 3 + w // 80
    plan = []
    for i in range(count):
        ang = hsh(i, w, 21) * 2 * math.pi
        rr = 0.25 + 0.42 * hsh(i, w, 22)
        size = 2 + int(hsh(i, w, 23) * (1.2 + w / 220.0))
        start = int(hsh(i, w, 24) * LOOP)
        plan.append((math.cos(ang) * rr, math.sin(ang) * rr, size, start))
    return plan


def foam_plan(w):
    """Schaumblasen auf dem Rand: (Winkel, Radius)."""
    out = []
    n = 6 + w // 18
    for i in range(n):
        ang = (i + 0.6 * hsh(i, w, 61)) / n * 2 * math.pi
        out.append((ang, 2.0 + (1.0 + w / 120.0) * hsh(i, w, 62)))
    return out


def bubbles_at(plan, k):
    out = []
    for (u, v, s, start) in plan:
        stage = (k - start) % LOOP
        if stage < 4:
            out.append((u, v, s, stage))
    return out


def pool_strip(width):
    w, h = size_for(width)
    plan = bubble_plan(w)
    foam = foam_plan(w)
    frames = []
    # Einschenken
    for i, f in enumerate((0.42, 0.72, 0.93)):
        splash = []
        if i < 2:
            for j in range(5 + w // 64):
                ang = hsh(j, w, 31 + i) * 2 * math.pi
                rr = f + 0.08 + 0.10 * hsh(j, w, 41)
                splash.append((math.cos(ang) * rr, math.sin(ang) * rr, 1 if hsh(j, w, 51) > 0.4 else 0))
        frames.append(pool_frame(w, h, f, 0.0, 0.35 * i, 0.6 + 0.2 * i, splash, [], foam))
    # Schleife
    for k in range(LOOP):
        phase = 2 * math.pi * k / LOOP
        frames.append(pool_frame(w, h, 1.0, phase, 1.0, 1.0, [], bubbles_at(plan, k), foam))
    # Versickern
    for i, f in enumerate((0.86, 0.66, 0.44)):
        frames.append(pool_frame(w, h, f, 2 * math.pi * (i + 1) / LOOP, 0.6 - 0.25 * i, 0.9 - 0.15 * i, [], [], foam))
    return frames, w, h


# ----------------------------------------------------------------------------
#  Dampf
# ----------------------------------------------------------------------------

STEAM_W, STEAM_H, STEAM_FRAMES = 10, 18, 7


def steam_frame(i):
    """Ein Woelkchen steigt auf, wird kleiner und zerfaellt."""
    img = Image.new("RGBA", (STEAM_W, STEAM_H), CLEAR)
    px = img.load()
    t = i / (STEAM_FRAMES - 1)
    puffs = [(0.0, 2.6 - 1.4 * t), (2.8 + 1.5 * t, 1.8 - 1.2 * t)]
    for k, (lag, r) in enumerate(puffs):
        if r < 0.6:
            continue
        cy = STEAM_H - 4 - t * 9.0 - lag
        cx = STEAM_W / 2 - 0.5 + 1.6 * math.sin(t * 3.5 + k * 2.0)
        for y in range(STEAM_H):
            for x in range(STEAM_W):
                dx, dy = x - cx, y - cy
                if dx * dx + dy * dy <= r * r + 0.4:
                    shade = dx + dy > r * 0.6
                    px[x, y] = STEAM_D if shade else STEAM
    if t > 0.75:     # am Ende zerfaellt das Woelkchen in Punkte
        for y in range(STEAM_H):
            for x in range(STEAM_W):
                if px[x, y][3] and (x + y + i) % 2:
                    px[x, y] = CLEAR
    return img


def save_strip(frames, name, w, h, dry):
    path = os.path.join(RES_WEAPONS, name + ".png")
    sheet = Image.new("RGBA", (w * len(frames), h), CLEAR)
    for i, fr in enumerate(frames):
        sheet.alpha_composite(fr, (i * w, 0))
    if dry:
        return
    sheet.save(path)
    if not os.path.exists(path + ".meta"):
        size = 2048
        while size < sheet.width:
            size *= 2
        write_strip_meta(path + ".meta", name, len(frames), w, h, 32, max_size=size)
    print("geschrieben:", path)


def main():
    args = sys.argv[1:]
    dry = "--dry" in args
    preview = args[args.index("--preview") + 1] if "--preview" in args else None

    strips = {}
    for width in WIDTHS:
        frames, w, h = pool_strip(width)
        strips[width] = (frames, w, h)
        save_strip(frames, "coffee_pool_%d" % width, w, h, dry)
    steam = [steam_frame(i) for i in range(STEAM_FRAMES)]
    save_strip(steam, "coffee_steam", STEAM_W, STEAM_H, dry)

    if preview:
        frames, w, h = strips[224]
        sheet = Image.new("RGBA", (w * 6 + 70, h * 2 * 2 + 30 + 300), (88, 112, 78, 255))
        for i, fr in enumerate(frames):
            sheet.alpha_composite(fr, (10 + (i % 6) * (w + 10), 10 + (i // 6) * (h + 10)))
        # Schleifenbild x2 zum Pixel-Zaehlen
        big = frames[GROW].resize((w * 2, h * 2), Image.NEAREST)
        sheet.alpha_composite(big, (10, 2 * (h + 10) + 10))
        for i, fr in enumerate(steam):
            sheet.alpha_composite(fr.resize((40, 72), Image.NEAREST), (w * 2 + 30 + i * 46, 2 * (h + 10) + 10))
        try:
            import char_keks  # noqa: F401
        except Exception:
            pass
        sheet.save(preview)
        # alle Groessen 1:1
        allw = sum(s[1] + 8 for s in strips.values()) + 8
        maxh = max(s[2] for s in strips.values())
        sizes = Image.new("RGBA", (allw, maxh + 16), (88, 112, 78, 255))
        x = 8
        for frames, w, h in strips.values():
            sizes.alpha_composite(frames[GROW + 2], (x, 8))
            x += w + 8
        sizes.save(preview.replace(".png", "_sizes.png"))
        print("Vorschau:", preview)


if __name__ == "__main__":
    main()
