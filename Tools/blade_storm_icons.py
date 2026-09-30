"""
Void-Kunai fuer die Evo Blade Storm (Blade Swarm + Void Spike).

Hebt sich bewusst vom normalen Kunai (Tools/kunai_icons.py) ab:
  Farbe  - Obsidian-Violett mit leuchtenden Lavendel-Kanten statt Stahl/Rot
  Form   - laengeres, schlankes Blatt, zwei nach hinten gezogene Widerhaken,
           Void-Kristall zwischen Griff und Klinge
  Icons  - zwei gekreuzte Kunai statt einem

Schreibt nur Pixel, alle .meta bleiben:

  Assets/Art/Waffen/fin_vanguards_edge.png          64x128 (32x64 Raster, 2x), Spitze nach OBEN
      -> Ingame-Klinge (Prefab "EvoBlade"; BladeStormEvoPrefab dreht mit "Winkel - 90")
  Assets/Art/Icons/fin_sword_swarm_icon.png         64x64  -> weaponIcon/weaponImage der Evo
  Assets/Resources/Achievements/blade_swarm_evo.png 64x64  Erfolg (Rahmen bleibt, Unterlinie violett)
  Assets/Resources/AchievementsBook/blade_swarm_evo_32/21/12.png  Erfolgsbuch
  Assets/Resources/Workbench/evo_blade_storm_14/_10.png           Werkbank

Aufruf aus dem Projektordner:  python Tools/blade_storm_icons.py [--preview pfad.png]
"""

import math
import os
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def hx(s):
    s = s.lstrip("#")
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), 255)


STYLES = {
    "game": {
        "outline": "#140a1f",
        "core": "#2b1745", "core_hi": "#46256f",
        "edge": "#9b5cf0", "edge_hi": "#d9aaff", "ridge": "#f6e3ff",
        "gem": "#ff4fd8", "gem_hi": "#ffc4f2",
        "wrap_hi": "#5f4a80", "wrap": "#3a2d4f", "wrap_lo": "#1f1830",
    },
    "workbench": {
        "outline": "#3b2b33",
        "core": "#2b1745", "core_hi": "#46256f",
        "edge": "#9b5cf0", "edge_hi": "#d9aaff", "ridge": "#f6e3ff",
        "gem": "#ff4fd8", "gem_hi": "#ffc4f2",
        "wrap_hi": "#5f4a80", "wrap": "#3a2d4f", "wrap_lo": "#1f1830",
    },
    # Erfolgsbuch: gedaempftes Violett auf Papier (wie der rote Bumerang dort)
    "book": {
        "outline": "#3b2b33",
        "core": "#4f3563", "core_hi": "#6a4a82",
        "edge": "#8a62b0", "edge_hi": "#b692d6", "ridge": "#dcc6ec",
        "gem": "#c24f9e", "gem_hi": "#e79bcf",
        "wrap_hi": "#7a6a8a", "wrap": "#5a4a66", "wrap_lo": "#3e3148",
    },
}

UNDERLINE = {"ach": "#7b3fc6", "book": "#8a62b0"}


# ----------------------------------------------------------------------------
#  Parametrisches Void-Kunai entlang einer beliebigen Achse
# ----------------------------------------------------------------------------

def in_tri(px, py, a, b, c):
    def s(p1, p2, p3):
        return (p1[0] - p3[0]) * (p2[1] - p3[1]) - (p2[0] - p3[0]) * (p1[1] - p3[1])
    d1, d2, d3 = s((px, py), a, b), s((px, py), b, c), s((px, py), c, a)
    neg = d1 < 0 or d2 < 0 or d3 < 0
    pos = d1 > 0 or d2 > 0 or d3 > 0
    return not (neg and pos)


def void_kunai(w, h, p0, p1, barb=0.20):
    """Rollenbild {(x, y): Rolle}. p0 = Ringmitte, p1 = Spitze (Pixelmitten)."""
    ax, ay = p1[0] - p0[0], p1[1] - p0[1]
    L = math.hypot(ax, ay)
    ax, ay = ax / L, ay / L
    px_, py_ = -ay, ax                      # v < 0 = Lichtseite (links/oben)

    R = max(1.6, 0.085 * L)                 # Ring aussen
    R_in = R * 0.45
    hw = max(0.8, 0.04 * L)                 # halbe Griffbreite
    t_h1 = 0.34 * L                         # Griffende
    t_g = 0.37 * L                          # Kristall
    g_r = max(1.2, 0.05 * L)
    t_b = 0.41 * L                          # Klingenansatz
    W = max(2.2, 0.12 * L)                  # halbe Klingenbreite (max)
    Wb = max(2.5, barb * L)                 # Widerhaken-Spitze (quer)
    eb = max(0.9, 0.035 * L)                # Leuchtkante

    roles = {}
    for y in range(h):
        for x in range(w):
            dx, dy = x - p0[0], y - p0[1]
            t = dx * ax + dy * ay
            v = dx * px_ + dy * py_
            av = abs(v)
            side_hi = v < -0.01

            # Ring (hohl, leuchtend)
            dist = math.hypot(dx, dy)
            if R_in < dist <= R and t <= R * 0.6:
                roles[(x, y)] = "edge_hi" if (dx + dy) < -0.5 else "edge"
                continue
            # Griff
            if R * 0.55 <= t <= t_h1 and av <= hw:
                band = int(math.floor((t + v) / 2.0)) % 2
                if av > hw - 0.8:
                    roles[(x, y)] = "wrap_hi" if side_hi else "wrap_lo"
                else:
                    roles[(x, y)] = "wrap_hi" if band == 0 else "wrap"
                continue
            # Kristall (Raute)
            if abs(t - t_g) + av <= g_r + 0.2:
                roles[(x, y)] = "gem_hi" if (side_hi and t > t_g - 0.3) else "gem"
                continue
            # Widerhaken: nach hinten gezogene Dreiecke
            w0 = W * 0.55
            tri = ((t_b - 0.4, w0 * 0.6), (t_b + 0.08 * L, w0), (t_b - 0.13 * L, Wb))
            if t < t_b + 0.08 * L and in_tri(t, av, *tri):
                roles[(x, y)] = "edge_hi" if side_hi else "edge"
                continue
            # Klinge
            if t_b - 0.5 <= t <= L + 0.3:
                u = min(1.0, max(0.0, (t - t_b) / (L - t_b)))
                if u < 0.16:
                    bw = W * (0.55 + 0.45 * u / 0.16)
                else:
                    bw = W * (1 - (u - 0.16) / 0.84) ** 1.1
                if av <= bw + 0.3:
                    if -0.75 < v <= 0.0 or (av < 0.3):
                        roles[(x, y)] = "ridge"
                    elif av > bw - eb:
                        roles[(x, y)] = "edge_hi" if side_hi else "edge"
                    else:
                        roles[(x, y)] = "core_hi" if side_hi else "core"
    return roles


def render(roles, w, h, style):
    pal = STYLES[style]
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    for (x, y), r in roles.items():
        img.putpixel((x, y), hx(pal[r]))
    return outline(img, pal["outline"])


def outline(img, col):
    src = img.copy()
    w, h = img.size
    for y in range(h):
        for x in range(w):
            if src.getpixel((x, y))[3]:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < w and 0 <= ny < h and src.getpixel((nx, ny))[3]:
                    img.putpixel((x, y), hx(col))
                    break
    return img


def cross_pair(a):
    """a = vordere Klinge (unten links -> oben rechts). Spiegelt eine
    abgedunkelte Kopie als hintere Klinge dahinter."""
    n = a.width
    # hintere Klinge abgedunkelt, damit die Kreuzung Tiefe bekommt
    b = a.transpose(Image.FLIP_LEFT_RIGHT)
    px = b.load()
    for y in range(n):
        for x in range(n):
            r, g, bl, al = px[x, y]
            if al:
                px[x, y] = (int(r * 0.68), int(g * 0.68), int(bl * 0.72), al)
    out = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    out.alpha_composite(b)
    out.alpha_composite(a)
    return out


def crossed(n, margin, style):
    """Zwei gekreuzte Void-Kunai auf n x n (je mit eigener Kontur).
    Widerhaken hier kleiner - gekreuzt wird es sonst zu unruhig."""
    m = margin + 0.5
    a = render(void_kunai(n, n, (m + 1.0, n - 1 - m - 1.0), (n - 1 - m, m), barb=0.13), n, n, style)
    return cross_pair(a)


# ----------------------------------------------------------------------------
#  6x6 von Hand: gekreuzte Klingen, parametrisch ist das nur noch Matsch
# ----------------------------------------------------------------------------

SMALL_X = [
    "E....E",
    ".R..R.",
    "..EG..",
    "..GE..",
    ".w..w.",
    "E....E",
]
SMALL_ROLES = {"E": "edge_hi", "R": "ridge", "G": "gem", "w": "wrap_hi", "e": "edge",
               "c": "core", "v": "wrap"}

# 10x10 von Hand: eine Klinge, cross_pair() macht das X daraus
KUNAI_10 = [
    "........ER",
    ".......ERe",
    "......ERce",
    ".....ERce.",
    ".....Gce..",
    "....wG....",
    "...wv.....",
    "..wv......",
    "EE........",
    "Ee........",
]


def hand_crossed(rows, style):
    n = len(rows)
    pal = STYLES[style]
    img = Image.new("RGBA", (n + 2, n + 2), (0, 0, 0, 0))
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch != ".":
                img.putpixel((x + 1, y + 1), hx(pal[SMALL_ROLES[ch]]))
    return cross_pair(outline(img, pal["outline"]))


def small_x(canvas, style, off, with_outline=True):
    img = Image.new("RGBA", (canvas, canvas), (0, 0, 0, 0))
    pal = STYLES[style]
    for y, row in enumerate(SMALL_X):
        for x, ch in enumerate(row):
            if ch != ".":
                img.putpixel((x + off, y + off), hx(pal[SMALL_ROLES[ch]]))
    return outline(img, pal["outline"]) if with_outline else img


# ----------------------------------------------------------------------------
#  Rahmen der Erfolgs-Icons: altes Bild nehmen, Innenflaeche leeren,
#  Unterlinie umfaerben, neues Motiv drauf
# ----------------------------------------------------------------------------

def reframe(path, scale, clear, fills, underline, motif, at):
    base = Image.open(path).convert("RGBA")
    if scale > 1:
        base = base.resize((base.width // scale, base.height // scale), Image.NEAREST)
    x0, y0, x1, y1, col = clear
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            base.putpixel((x, y), hx(col))
    for (fx0, fy, fx1, col) in fills:
        for x in range(fx0, fx1 + 1):
            base.putpixel((x, fy), hx(col))
    ux0, uy, ux1, ucol = underline
    for x in range(ux0, ux1 + 1):
        base.putpixel((x, uy), hx(ucol))
    base.alpha_composite(motif, at)
    if scale > 1:
        base = base.resize((base.width * scale, base.height * scale), Image.NEAREST)
    return base


def build():
    out = {}
    A = lambda p: os.path.join(ROOT, p)

    # Ingame: 32x64 Raster, Spitze oben, 2x hoch -> 64x128 (PPU 128, Prefab-Scale 2)
    ingame = render(void_kunai(32, 64, (15.5, 57.0), (15.5, 6.0)), 32, 64, "game")
    out["Assets/Art/Waffen/fin_vanguards_edge.png"] = ingame.resize((64, 128), Image.NEAREST)

    out["Assets/Art/Icons/fin_sword_swarm_icon.png"] = crossed(32, 1, "game").resize((64, 64), Image.NEAREST)

    # Erfolg 64 (32er Raster): Innenflaeche x2..30 / y2..27, Unterlinie Zeile 28
    out["Assets/Resources/Achievements/blade_swarm_evo.png"] = reframe(
        A("Assets/Resources/Achievements/blade_swarm_evo.png"), 2,
        (2, 2, 30, 27, "#f6d8a0"), [], (2, 28, 29, UNDERLINE["ach"]),
        crossed(24, 0, "game"), (4, 3))

    # Erfolgsbuch 32
    out["Assets/Resources/AchievementsBook/blade_swarm_evo_32.png"] = reframe(
        A("Assets/Resources/AchievementsBook/blade_swarm_evo_32.png"), 1,
        (6, 6, 25, 23, "#f2dcbc"),
        [(5, 5, 26, "#e0c49f")] + [(5, y, 5, "#e0c49f") for y in range(6, 24)]
        + [(26, y, 26, "#e0c49f") for y in range(6, 24)]
        + [(6, 24, 25, "#b99772"), (6, 25, 6, "#b99772"), (25, 25, 25, "#b99772")],
        (7, 25, 24, UNDERLINE["book"]),
        crossed(20, 0, "book"), (6, 5))

    # Erfolgsbuch 21
    out["Assets/Resources/AchievementsBook/blade_swarm_evo_21.png"] = reframe(
        A("Assets/Resources/AchievementsBook/blade_swarm_evo_21.png"), 1,
        (4, 4, 16, 14, "#f2dcbc"), [(4, 15, 16, "#e0c49f")],
        (6, 16, 14, UNDERLINE["book"]),
        crossed(13, 0, "book"), (4, 3))

    # Erfolgsbuch 12
    out["Assets/Resources/AchievementsBook/blade_swarm_evo_12.png"] = reframe(
        A("Assets/Resources/AchievementsBook/blade_swarm_evo_12.png"), 1,
        (3, 3, 8, 8, "#f2dcbc"), [], (3, 9, 8, UNDERLINE["book"]),
        small_x(6, "book", 0, with_outline=False), (3, 3))

    # Werkbank: 10x10 Motiv + Kontur in 14, 6x6 in 10
    wb14 = Image.new("RGBA", (14, 14), (0, 0, 0, 0))
    wb14.alpha_composite(hand_crossed(KUNAI_10, "workbench"), (1, 1))
    out["Assets/Resources/Workbench/evo_blade_storm_14.png"] = wb14
    out["Assets/Resources/Workbench/evo_blade_storm_10.png"] = small_x(10, "workbench", 2)
    return out


def main():
    out = build()
    if "--preview" in sys.argv:
        path = sys.argv[sys.argv.index("--preview") + 1]
        cell = 272
        sheet = Image.new("RGBA", (cell * len(out), cell * 2), (60, 60, 70, 255))
        for i, (name, img) in enumerate(out.items()):
            k = max(1, 256 // max(img.size))
            bg = Image.new("RGBA", (256, 256 * 2 if img.height > img.width else 256),
                           (225, 205, 165, 255) if "Workbench" in name else (60, 60, 70, 255))
            k = min(256 // img.width, bg.height // img.height)
            bg.alpha_composite(img.resize((img.width * k, img.height * k), Image.NEAREST))
            sheet.alpha_composite(bg, (i * cell + 8, 8))
        sheet.save(path)
        print("Vorschau:", path)
        return
    for rel, img in out.items():
        img.save(os.path.join(ROOT, rel))
    print("Geschrieben:", ", ".join(out))


if __name__ == "__main__":
    main()
