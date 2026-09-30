"""
Kunai fuer den Klingenschwarm (Blade Swarm) - ersetzt das alte Schwert.

Schreibt nur Pixel, alle .meta bleiben (Sprite-Namen/Pivots unveraendert):

  Assets/Resources/Shop/blade_swarm.png                 64x64 (32er Raster, 2x)
      -> Ingame-Klinge (Prefab "Blade 1"), weaponImage/weaponIcon am Player,
         Shop-Kachel, Unlock-Symbol
  Assets/Art/Waffen/Sword.png                           64x64 dito (altes Prefab "Blade")
  Assets/Resources/Workbench/blade_swarm_14.png / _10   Werkbank (Kontur #3b2b33)
  Assets/Resources/AchievementsBook/blade_swarm_32/21/12  Erfolgsbuch (Sepia)

Das Motiv liegt diagonal: Ring unten links, Spitze oben rechts - so wie das
Schwert vorher. BladeSwarm.cs dreht die Klinge mit "Winkel - 45 Grad", die
Spitze muss also auf 45 Grad (oben rechts) zeigen.

Aufruf aus dem Projektordner:  python Tools/kunai_icons.py [--preview pfad.png]
"""

import math
import os
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def hx(s):
    s = s.lstrip("#")
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), 255)


# Farbrollen -> Farbe, je Stil
STYLES = {
    # Spiel/Shop: kraeftig, dunkle Kontur wie die uebrigen Shop-Bilder
    "game": {
        "outline": "#20201f",
        "ridge": "#f4f8fb", "steel_hi": "#c9d3de", "steel": "#8e99aa", "steel_lo": "#5b6374",
        "wrap_hi": "#d0604f", "wrap": "#9c3a33", "wrap_lo": "#5e2226",
        "ring_hi": "#b8b3b0", "ring": "#77726f", "ring_lo": "#4a4644",
    },
    # Werkbank: gleiche Farben, Kontur der Werkbank-Icons
    "workbench": {
        "outline": "#3b2b33",
        "ridge": "#f4f8fb", "steel_hi": "#c9d3de", "steel": "#8e99aa", "steel_lo": "#5b6374",
        "wrap_hi": "#d0604f", "wrap": "#9c3a33", "wrap_lo": "#5e2226",
        "ring_hi": "#b8b3b0", "ring": "#77726f", "ring_lo": "#4a4644",
    },
    # Erfolgsbuch: Sepia-Toene der uebrigen Buch-Icons
    "book": {
        "outline": "#3b2b33",
        "ridge": "#d9b189", "steel_hi": "#c8a078", "steel": "#9c8268", "steel_lo": "#6f4630",
        "wrap_hi": "#c9922a", "wrap": "#8c5a3c", "wrap_lo": "#4d2e1e",
        "ring_hi": "#b99772", "ring": "#8a5a3d", "ring_lo": "#5a3421",
    },
}


def kunai(n, margin, style):
    """Kunai auf n x n, diagonal, parametrisch. Rueckgabe: {(x, y): Rolle}."""
    roles = {}
    L = n - 1 - 2 * margin                  # Laenge entlang der Achse (in t)
    ring_r = max(1.6, L * 0.12)             # Aussenradius Ring
    ring_in = ring_r * 0.45
    t0 = margin + ring_r * 0.75             # Ringmitte (t = x bei d = 0)
    t_end = n - 1 - margin                  # Spitze
    t_handle0 = t0 + ring_r * 0.6
    t_handle1 = margin + L * 0.46           # Griffende
    t_collar = t_handle1 + 0.5
    t_blade0 = t_collar + 1.0
    hw = 1 if L < 20 else 2                 # halbe Griffbreite (Diagonal-Schritte)
    blade_w = max(3, round(L * 0.19))       # halbe Klingenbreite

    for y in range(n):
        for x in range(n):
            d = x + y - (n - 1)            # quer zur Achse (Diagonal-Schritte)
            t = (x - y + (n - 1)) / 2.0    # entlang der Achse
            # Ring (hohl)
            cx, cy = t0, n - 1 - t0
            dx, dy = x - cx, y - cy
            dist = math.hypot(dx, dy)
            if ring_in < dist <= ring_r and t <= t_handle0:
                roles[(x, y)] = "ring_hi" if (dx + dy) < -0.6 else ("ring_lo" if (dx + dy) > 0.6 else "ring")
                continue
            # Griff (Stoffwicklung, schraeg gewickelt)
            if t_handle0 - 0.5 <= t <= t_handle1 and abs(d) <= hw:
                band = int(math.floor((t * 2 + d) / 3)) % 2
                if d < 0:
                    roles[(x, y)] = "wrap_hi" if band == 0 else "wrap"
                elif d == hw:
                    roles[(x, y)] = "wrap_lo"
                else:
                    roles[(x, y)] = "wrap" if band == 0 else "wrap_lo"
                continue
            # Zwinge zwischen Griff und Klinge
            if t_handle1 < t <= t_blade0 - 0.5 and abs(d) <= hw + 1:
                roles[(x, y)] = "ring_hi" if d < 0 else ("ring_lo" if d > 0 else "ring")
                continue
            # Klinge: Blatt - schnell breit, dann gerade zur Spitze
            if t_blade0 - 0.5 <= t <= t_end:
                u = (t - t_blade0) / (t_end - t_blade0)   # 0 .. 1
                if u < 0.28:
                    w = blade_w * (0.45 + 0.55 * max(0.0, u) / 0.28)
                else:
                    w = blade_w * (1 - (u - 0.28) / 0.72)
                if abs(d) <= w + 0.3:
                    if d == 0:
                        roles[(x, y)] = "ridge"
                    elif d < 0:
                        roles[(x, y)] = "steel_hi" if abs(d) < w - 0.7 else "steel"
                    else:
                        roles[(x, y)] = "steel" if abs(d) < w - 0.7 else "steel_lo"
    return roles


def render(roles, n, style, outline=True, diagonal_outline=False):
    pal = STYLES[style]
    img = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    for (x, y), r in roles.items():
        img.putpixel((x, y), hx(pal[r]))
    if outline:
        src = img.copy()
        nb = [(1, 0), (-1, 0), (0, 1), (0, -1)]
        if diagonal_outline:
            nb += [(1, 1), (1, -1), (-1, 1), (-1, -1)]
        for y in range(n):
            for x in range(n):
                if src.getpixel((x, y))[3]:
                    continue
                for dx, dy in nb:
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < n and 0 <= ny < n and src.getpixel((nx, ny))[3]:
                        img.putpixel((x, y), hx(pal["outline"]))
                        break
    return img


# ----------------------------------------------------------------------------
#  12er/10er von Hand - parametrisch bleibt dort nur Matsch.
#  Kontur kommt automatisch dazu.
#  R = Ring, W/w/v = Wicklung hell/mittel/dunkel, S/s/d = Stahl hell/mittel/dunkel,
#  # = Grat
# ----------------------------------------------------------------------------

SMALL_ROLES = {
    "#": "ridge", "S": "steel_hi", "s": "steel", "d": "steel_lo",
    "W": "wrap_hi", "w": "wrap", "v": "wrap_lo",
    "R": "ring", "r": "ring_lo", "H": "ring_hi",
}

SMALL = {
    # 8x8 Motiv (Buch _12)
    12: [
        ".......#",
        ".....S#s",
        "....S#sd",
        ".....Sd.",
        "....Wv..",
        "...Wv...",
        ".HWv....",
        "Hr......",
    ],
    # 6x6 Motiv (Werkbank _10)
    10: [
        ".....#",
        "...S#d",
        "...Sd.",
        "..Wv..",
        ".Wv...",
        "Hr....",
    ],
}


def small(size, style):
    rows = SMALL[size]
    m = len(rows)
    off = (size - m) // 2
    roles = {}
    for y, row in enumerate(rows):
        assert len(row) == m, (size, row)
        for x, ch in enumerate(row):
            if ch != ".":
                roles[(x + off, y + off)] = SMALL_ROLES[ch]
    return render(roles, size, style)


def build():
    out = {}
    big = render(kunai(32, 1, "game"), 32, "game").resize((64, 64), Image.NEAREST)
    out["Assets/Resources/Shop/blade_swarm.png"] = big
    out["Assets/Art/Waffen/Sword.png"] = big
    out["Assets/Resources/Workbench/blade_swarm_14.png"] = render(kunai(14, 2, "workbench"), 14, "workbench")
    out["Assets/Resources/Workbench/blade_swarm_10.png"] = small(10, "workbench")
    out["Assets/Resources/AchievementsBook/blade_swarm_32.png"] = render(kunai(32, 4, "book"), 32, "book")
    out["Assets/Resources/AchievementsBook/blade_swarm_21.png"] = render(kunai(21, 2, "book"), 21, "book")
    out["Assets/Resources/AchievementsBook/blade_swarm_12.png"] = small(12, "book")
    return out


def main():
    out = build()
    if "--preview" in sys.argv:
        path = sys.argv[sys.argv.index("--preview") + 1]
        cell = 272
        sheet = Image.new("RGBA", (cell * len(out), cell), (60, 60, 70, 255))
        for i, (name, img) in enumerate(out.items()):
            k = 256 // img.width
            bg = Image.new("RGBA", (256, 256), (225, 205, 165, 255) if "Book" in name or "Workbench" in name
                           else (60, 60, 70, 255))
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
