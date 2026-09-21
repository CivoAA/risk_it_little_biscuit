# -*- coding: utf-8 -*-
"""Baut die beiden Mockups aus ACHIEVEMENTS_BOOK_UI.md aus den echten Sprites.

Wird von Tools/erfolgsbuch_ui.py mitaufgerufen und schreibt
preview_filled.png (Reiter ERFOLGE) und preview_filled_unlocks.png
(Reiter UNLOCKS) nach Assets/Art/UI_Objects/AchievementsBook/.

Nur fuer die Doku. Im Spiel setzt AchievementsBookPanel.cs dieselben Sprites
nach denselben Koordinaten zusammen - hier stehen sie noch einmal, damit man
eine Aenderung an der Grafik sofort im Zusammenhang sieht, ohne Unity zu
starten. Die Schrift ist deshalb auch nur ein 3x5-Ersatz und nicht Jersey10.
"""
import os

from PIL import Image

from erfolgsbuch_ui import (Canvas, INK, PARCH, PARCH3, PARCH4, WOOD_D, WOOD_D3)

# --------------------------------------------------------------- Ersatzfont
FONT = {
    "A": ".X.,X.X,XXX,X.X,X.X", "B": "XX.,X.X,XX.,X.X,XX.",
    "C": ".XX,X..,X..,X..,.XX", "D": "XX.,X.X,X.X,X.X,XX.",
    "E": "XXX,X..,XX.,X..,XXX", "F": "XXX,X..,XX.,X..,X..",
    "G": ".XX,X..,X.X,X.X,.XX", "H": "X.X,X.X,XXX,X.X,X.X",
    "I": "XXX,.X.,.X.,.X.,XXX", "J": "..X,..X,..X,X.X,.X.",
    "K": "X.X,X.X,XX.,X.X,X.X", "L": "X..,X..,X..,X..,XXX",
    "M": "X.X,XXX,XXX,X.X,X.X", "N": "XX.,X.X,X.X,X.X,X.X",
    "O": ".X.,X.X,X.X,X.X,.X.", "P": "XX.,X.X,XX.,X..,X..",
    "Q": ".X.,X.X,X.X,XX.,.XX", "R": "XX.,X.X,XX.,X.X,X.X",
    "S": ".XX,X..,.X.,..X,XX.", "T": "XXX,.X.,.X.,.X.,.X.",
    "U": "X.X,X.X,X.X,X.X,XXX", "V": "X.X,X.X,X.X,X.X,.X.",
    "W": "X.X,X.X,XXX,XXX,X.X", "X": "X.X,X.X,.X.,X.X,X.X",
    "Y": "X.X,X.X,.X.,.X.,.X.", "Z": "XXX,..X,.X.,X..,XXX",
    "0": "XXX,X.X,X.X,X.X,XXX", "1": ".X.,XX.,.X.,.X.,XXX",
    "2": "XX.,..X,.X.,X..,XXX", "3": "XXX,..X,.XX,..X,XXX",
    "4": "X.X,X.X,XXX,..X,..X", "5": "XXX,X..,XX.,..X,XX.",
    "6": ".XX,X..,XXX,X.X,XXX", "7": "XXX,..X,.X.,X..,X..",
    "8": "XXX,X.X,XXX,X.X,XXX", "9": "XXX,X.X,XXX,..X,XX.",
    "/": "..X,..X,.X.,X..,X..", "-": "...,...,XXX,...,...",
    ".": "...,...,...,...,.X.", ":": "...,.X.,...,.X.,...",
    "+": "...,.X.,XXX,.X.,...", " ": "...,...,...,...,...",
    # Umlaute sind 7 statt 5 Zeilen hoch; draw_text setzt sie dafuer 2 Zeilen
    # hoeher an. Die Beschriftungsflaechen haben oben genug Luft dafuer.
    "Ä": "X.X,...,.X.,X.X,XXX,X.X,X.X",
    "Ö": "X.X,...,.X.,X.X,X.X,X.X,.X.",
    "Ü": "X.X,...,X.X,X.X,X.X,X.X,XXX",
}


def text_w(s):
    return max(0, len(s) * 4 - 1)


def draw_text(c, x, y, s, col):
    for ch in s.upper().replace("ß", "SS"):
        g = FONT.get(ch)
        if g is not None:
            rows = g.split(",")
            top = y - 2 if len(rows) > 5 else y
            for dy, row in enumerate(rows):
                for dx, p in enumerate(row):
                    if p == "X":
                        c.set(x + dx, top + dy, col)
        x += 4


def label(w, h, s, col, x=0, y=0):
    c = Canvas(w, h)
    draw_text(c, x, y, s, col)
    return c.image()


def centered(w, h, s, col, y=0):
    return label(w, h, s, col, (w - text_w(s)) // 2, y)


def wrap(s, w):
    """Wortumbruch wie TextMeshPro ihn machen wuerde - damit die Vorschau nicht
    heiler aussieht als das Spiel."""
    lines, cur = [], ""
    for word in s.split(" "):
        probe = word if not cur else cur + " " + word
        if text_w(probe) <= w:
            cur = probe
        else:
            if cur:
                lines.append(cur)
            cur = word
    if cur:
        lines.append(cur)
    return lines


# --------------------------------------------------------------- Werkzeug
def slice9(img, w, h, border):
    """Neunerteilung von Hand - PIL kennt kein 9-Slice."""
    l, b, r, t = border
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    sw, sh = img.size
    cols = ((0, l, 0, l), (l, sw - r, l, w - r), (sw - r, sw, w - r, w))
    rows = ((0, t, 0, t), (t, sh - b, t, h - b), (sh - b, sh, h - b, h))
    for sx0, sx1, dx0, dx1 in cols:
        for sy0, sy1, dy0, dy1 in rows:
            if sx1 <= sx0 or sy1 <= sy0 or dx1 <= dx0 or dy1 <= dy0:
                continue
            part = img.crop((sx0, sy0, sx1, sy1))
            if part.size != (dx1 - dx0, dy1 - dy0):
                part = part.resize((dx1 - dx0, dy1 - dy0), Image.NEAREST)
            out.alpha_composite(part, (dx0, dy0))
    return out


def dim(img, col):
    """Gesperrte Motive werden nur gedaempft, nicht geschwaerzt."""
    out = img.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            r, g, b, a = px[x, y]
            if not a:
                continue
            # Genau das, was Unity mit Image.color macht: Multiplikation.
            px[x, y] = (r * col[0] // 255, g * col[1] // 255, b * col[2] // 255, a)
    return out


def icon(root, key, size):
    f = os.path.join(root, "Assets", "Resources", "AchievementsBook",
                     "%s_%d.png" % (key, size))
    return Image.open(f).convert("RGBA") if os.path.exists(f) else None


# --------------------------------------------------------------- Beispieldaten
ACH_DEMO = (
    ("first_game", "DER ERSTE LAUF", "", True),
    ("first_win", "GLÜCKWUNSCH!", "0 / 1", False),
    ("kill_100", "KRÜMELBRECHER", "64 / 100", False),
    ("kill_1000", "100 KEKSE", "64 / 1000", False),
    ("first_death", "ERSTER TOD", "0 / 1", False),
    ("death", "AUFGEGEBEN", "0 / 10", False),
)

# Detailspalte: dieselben Zahlen wie DetailLeft/DetailW/SlotXL in
# AchievementsBookPanel.cs. Beginnt bei 182, damit der Text nicht mehr im
# Falzschatten anfaengt.
DET_X, DET_W, SLOT_X = 182, 117, 224
UNL_DEMO = ("boba_gun", "spikefork", "shurikookie", "celestial_star", "candy_bomb",
            "blade_swarm", "time_laser", "boomerang_evo", "weapon_slot", "buff_slot")


def preview(sp, root, unlocks=False):
    base = Image.new("RGBA", (320, 180), (0, 0, 0, 0))

    def put(img, x, y):
        base.alpha_composite(img, (x, y))

    put(sp["bg_capsule"], 0, 0)
    for name, x, y in (("bubble_02", 8, 40), ("bubble_01", 9, 96), ("bubble_03", 306, 62),
                       ("bubble_04", 305, 126), ("bubble_01", 7, 150)):
        put(sp[name], x, y)
    put(sp["book_panel"], 16, 23)
    put(sp["book_spine"], 156, 25)

    # Reiter, Zurueck-Knopf
    for i, txt in enumerate(("ERFOLGE", "UNLOCKS")):
        on = (i == 1) == unlocks
        put(sp["tab_active" if on else "tab_inactive"], 16 + i * 62, 9)
        put(centered(56, 10, txt, INK if on else PARCH, 2), 18 + i * 62, 11)
    put(slice9(sp["tab_inactive"], 52, 14, (2, 2, 2, 2)), 251, 9)
    put(centered(48, 10, "ZURÜCK", PARCH, 2), 253, 11)

    # Fortschrittsleiste: eine Zelle leuchtet, sobald etwas offen ist, alle
    # zehn erst bei wirklich allem.
    done, total = (4, 10) if unlocks else (7, 41)
    full = 0 if done == 0 else 10 if done >= total else max(1, min(9, done * 10 // total))
    for i in range(10):
        put(sp["progress_cell_full" if i < full else "progress_cell_empty"], 20 + i * 10, 29)
    txt = "%d / %d" % (done, total)
    put(label(34, 10, txt, WOOD_D, 34 - text_w(txt), 2), 121, 26)

    if unlocks:
        for i, key in enumerate(UNL_DEMO):
            x, y = 20 + (i % 4) * 33, 36 + (i // 4) * 36
            on = i < 4
            put(sp["slot_large_owned" if on else "slot_large_locked"], x, y)
            im = icon(root, key, 32)
            if im is not None:
                put(im if on else dim(im, PARCH4), x, y)
            if i == 1:
                put(slice9(sp["row_hover"], 32, 32, (2, 2, 2, 2)), x, y)
        put(slice9(sp["row_selected"], 32, 32, (2, 2, 2, 2)), 20, 36)
        det_key, det_title, det_done = UNL_DEMO[0], "BOBA BLASTER", True
        det_desc = ("SCHIESST ZÄHE PERLEN", "AUF ALLES WAS", "SICH BEWEGT")
    else:
        for i, (key, title, prog, dn) in enumerate(ACH_DEMO):
            y = 36 + i * 21
            put(sp["row_done"] if dn else sp["row_normal"], 20, y)
            # Reihenfolge wie in CreateRow: Hover ist erstes Kind und liegt
            # damit unter Icon und Text, die Auswahl liegt als letztes obenauf.
            if i == 2:
                put(sp["row_hover"], 20, y)
            put(sp["slot_small_unlocked" if dn else "slot_small_locked"], 21, y)
            im = icon(root, key, 21)
            if im is not None:
                put(im if dn else dim(im, PARCH4), 21, y)
            col = WOOD_D3 if dn else WOOD_D
            put(label(94, 10, title, col, 0, 2), 45, y + 2)
            if prog:
                put(label(94, 9, prog, col if dn else PARCH4, 0, 1), 45, y + 12)
            if i == 0:
                put(sp["row_selected"], 20, y)
        det_key, det_title, det_done = ACH_DEMO[0][0], "DER ERSTE LAUF", True
        det_desc = ("STARTE DEIN ERSTES", "SPIEL UND ÜBERLEBE", "DIE ERSTE WELLE")

    # Detailseite
    slot = "slot_large_locked"
    if det_done:
        slot = "slot_large_owned" if unlocks else "slot_large_unlocked"
    put(sp[slot], SLOT_X, 32)
    im = icon(root, det_key, 32)
    if im is not None:
        put(im if det_done else dim(im, PARCH4), SLOT_X, 32)
    put(centered(DET_W, 14, det_title, WOOD_D3, 4), DET_X, 67)
    c = Canvas(DET_W, 34)
    for i, line in enumerate(det_desc):
        draw_text(c, 0, 3 + i * 11, line, WOOD_D)
    put(c.image(), DET_X, 84)
    for line in det_desc:
        assert text_w(line) <= DET_W, "Detailzeile zu breit: " + line
    # Die Fortschrittszeile bleibt bei erledigten Eintraegen leer - genau wie
    # im Spiel, wo CollectEntries dort "" setzt.
    c = Canvas(DET_W, 1)
    c.hline(0, DET_W - 1, 0, PARCH3)
    put(c.image(), DET_X, 132)

    # Belohnung (Erfolge) bzw. Statuszeile (freigeschaltete Unlocks) - dieselbe
    # Box, im Spiel als 9-Slice auf die Breite der Spalte gestaucht.
    box = "FREIGESCHALTET" if unlocks else "+25 COOKIE SOULS"
    if unlocks and not det_done:
        box = None
    if box:
        put(slice9(sp["reward_box"], DET_W, 20, (18, 2, 4, 2)), DET_X, 138)
        im = icon(root, "_badge_unlocked", 12)
        if im is not None:
            put(im, DET_X + 4, 142)
        c = Canvas(DET_W - 24, 14)
        for i, line in enumerate(wrap(box, DET_W - 24)):
            draw_text(c, 0, 4 + i * 7, line, WOOD_D)
        put(c.image(), DET_X + 20, 141)
    return base


def write_previews(sprites, root, folder):
    preview(sprites, root, False).save(os.path.join(folder, "preview_filled.png"))
    preview(sprites, root, True).save(os.path.join(folder, "preview_filled_unlocks.png"))
