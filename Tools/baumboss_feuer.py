"""
Flammenwurf des Baum-Bosses - getrennt vom Boss, beliebig lang ohne Verzerren.

  Assets/Art/Gegner/new/boss/
    baumboss_fire_start.png   8 x 32x32  Ansatz am Maul, wird von 8 auf volle Dicke breiter
    baumboss_fire_mid.png     8 x 32x32  Mittelstueck, wird so oft wie noetig aneinandergereiht
    baumboss_fire_end.png     8 x 32x32  Spitze: reisst in Zungen auf, verraucht
  PPU 32 (wie der Boss), Pivot links Mitte, Strahl zeigt nach rechts (+x).
  Zusammengesetzt und abgespielt von Assets/Scripts/Enemy/FlameBeam.cs.
  Laengen in 4-px-Schritten; ein Teil bei pos px zeigt Frame (f - pos/4) mod 8,
  dann passen sich ueberlappende Teile pixelgenau.

Alle drei Teile rechnen mit demselben Feuerfeld, das in x mit 32 px
periodisch ist und pro Frame 4 px nach aussen stroemt (8 Frames = eine
Kachel). Darum passen Ansatz | Mitte | Mitte | ... | Spitze an jeder
Kachelgrenze und in jedem Frame pixelgenau aneinander.

Aufruf aus dem Projektordner:  python Tools/baumboss_feuer.py
"""

import math
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "Assets", "Art", "Gegner", "new", "boss")

T = 32         # Kachelbreite
H = 32         # Hoehe aller Teile
N = 8          # Frames
FLOW = T // N  # px pro Frame
PPU = 32
YC = (H - 1) / 2.0
DICKE = 9.5    # halbe Strahldicke im Mittelstueck

TAU = 2 * math.pi


def hx(s, a=255):
    s = s.lstrip("#")
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), a)


# heiss -> kalt
STUFEN = [
    (0.80, hx("#fffbe6")),
    (0.62, hx("#ffe066")),
    (0.45, hx("#ffa126")),
    (0.30, hx("#f2601c")),
    (0.18, hx("#c2301a")),
    (0.09, hx("#7a1a12")),
]
RAUCH = [hx("#5a4a4a"), hx("#3f3536")]
FUNKE = [hx("#ffe066"), hx("#ffa126")]


def feuerfeld(x, y, f):
    """Grundmuster: haengt nur von (x - 4f) ab und ist in x mit T periodisch - reines
    Stroemen. Darum sieht ein Teil bei pos px mit Frame f - pos/4 exakt so aus wie das
    Feld an dieser Stelle. Liefert (Mittellinie, Dicke-Faktor, Turbulenz)."""
    u = (x - FLOW * f) / T * TAU
    mitte = YC + 0.8 * np.sin(u + 1.0) + 0.6 * np.sin(2 * u + 2.3) + 0.4 * np.sin(3 * u + 0.7)
    dick = 1.0 + 0.14 * np.sin(3 * u + 0.5) + 0.08 * np.sin(2 * u + 1.7)
    turb = (0.12 * np.sin(2 * u + y * 0.7 + 0.4)
            + 0.10 * np.sin(3 * u - y * 1.1 + 2.0)
            + 0.07 * np.sin(1 * u + y * 1.9 + 4.1)
            + 0.05 * np.sin(5 * u + y * 0.6 + 1.3)
            + 0.03 * np.sin(7 * u - y * 1.7 + 5.2))
    return mitte, dick, turb


def faerben(v):
    out = np.zeros(v.shape + (4,), np.uint8)
    for grenze, col in reversed(STUFEN):
        out[v > grenze] = col
    return out


def teil(art, f):
    Y, X = np.mgrid[0:H, 0:T].astype(float)
    mitte, dick, turb = feuerfeld(X, Y, f)
    xn = X / (T - 1)
    if art == "start":
        # von schmal am Maul auf volle Dicke; am Maul weissgluehend
        prof = 0.4 + 0.6 * (1 - (1 - xn) ** 2)
        mitte = YC + (mitte - YC) * (0.3 + 0.7 * xn)
    elif art == "end":
        prof = 1.0 + 0.3 * xn
    else:
        prof = np.ones_like(X)
    d = np.abs(Y - mitte) / (DICKE * dick * prof)
    v = 1.0 - d + turb
    if art == "start":
        v += 0.28 * (1 - xn) ** 2
        # Muendungsblitz direkt am Maul
        v += 0.35 * np.exp(-((X / 3.0) ** 2) - ((Y - YC) / 5.0) ** 2)
    if art == "end":
        u = (X - FLOW * f) / T * TAU
        # zerreisst in einzelne Zungen und laeuft aus
        v -= 0.42 * xn ** 1.2 * (0.5 + 0.5 * np.sin(4 * u + Y * 0.8))
        v -= 0.95 * xn ** 2.2
    img = faerben(v)

    if art == "end":
        # Rauchfetzen am Ende (periodisch mit N)
        for i in range(3):
            q = ((f / N) + i / 3.0) % 1.0
            cx = 14 + q * 16
            cy = YC + (i - 1) * 6 - q * 5
            r = 0.8 + 1.4 * (1 - q)
            for yy in range(int(cy - r) - 1, int(cy + r) + 2):
                for xx in range(int(cx - r) - 1, int(cx + r) + 2):
                    if 0 <= yy < H and 0 <= xx < T and img[yy, xx, 3] == 0:
                        if (xx - cx) ** 2 + (yy - cy) ** 2 <= r * r:
                            img[yy, xx] = RAUCH[0] if q < 0.6 else RAUCH[1]
    if art != "start":
        # Funken ausserhalb des Strahls, wandern mit der Stroemung (x periodisch)
        for i in range(4):
            sx = int(round(i * 8.3 + 3 + FLOW * f)) % T
            seite = 1 if i % 2 else -1
            sy = int(round(YC + seite * (DICKE + 2.5 + 2 * math.sin(i * 1.7))))
            if art == "end" and sx > 26:
                continue
            if 0 <= sy < H and img[sy, sx, 3] == 0:
                img[sy, sx] = FUNKE[i % 2]
    return img


def build():
    return {art: [teil(art, f) for f in range(N)] for art in ("start", "mid", "end")}


def frame_bei(f, pos):
    """Frame eines Teils, das bei pos px sitzt: 4 px Versatz sind genau ein Frame Stroemung."""
    return (f - pos // FLOW) % N


def strahl(fire, laenge, k):
    """Setzt den Strahl genau wie FlameBeam.cs zusammen (fuer Vorschauen). laenge in px."""
    laenge = max(T, laenge - laenge % FLOW)
    img = Image.new("RGBA", (laenge, H))
    end_pos = laenge - T
    teile = []
    if end_pos > T:
        n = (end_pos - 1) // T
        teile += [("mid", max(T, min(T * (i + 1), end_pos - T))) for i in range(n)]
    teile += [("start", 0), ("end", end_pos)]  # Ansatz ueber der Mitte, Spitze ganz oben
    for art, pos in teile:
        img.alpha_composite(Image.fromarray(fire[art][frame_bei(k, pos)]), (pos, 0))
    return img


def main():
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    from unity_meta import write_strip_meta
    os.makedirs(OUT_DIR, exist_ok=True)
    for art, frames in build().items():
        name = "baumboss_fire_" + art
        path = os.path.join(OUT_DIR, name + ".png")
        sheet = Image.new("RGBA", (T * N, H))
        for i, f in enumerate(frames):
            sheet.paste(Image.fromarray(f), (i * T, 0))
        sheet.save(path)
        if not os.path.exists(path + ".meta"):
            write_strip_meta(path + ".meta", name, N, T, H, PPU, pivot=(0.0, 0.5))
        print("%-22s %d Frames  %s" % (name, N, path))


if __name__ == "__main__":
    main()
