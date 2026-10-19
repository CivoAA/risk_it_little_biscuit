"""
Musik des Hubs: "Keksdose" - das Zuhause zwischen den Laeufen.

Komplett im Code komponiert und synthetisiert, wie Tools/menu_musik.py
(Okarina, Spieluhr, E-Piano, Kontrabass, Besen und Ofen kommen von dort,
Hall/Mix/Flaeche aus Tools/eis_musik.py).

Der Hub ist der Ort, an dem man stundenlang steht: Skilltree, Shop, Werkbank,
Keksdose. Darum kein Ohrwurm mit Ellbogen, sondern ein warmes Lo-Fi-Stueck,
das nie draengt: weiches Filzklavier, viel Luft, Pausen in der Melodie,
Jazz-Akkorde ohne harte Kadenzen, und alle paar Takte etwas Neues, damit das
Ohr nicht muede wird.

  - D-Dur, 78 BPM, sanfter Swing (Achtel 58:42)
  - Schleife = genau 56 Takte (~172 s), Hallfahne nach vorn gefaltet
  - Leichtes Bandleiern (Wow) ueber dem ganzen Mix, periodisch mit der
    Schleife, damit die Naht nicht hoerbar ist
  - Anfang und Ende sind leise: Filzklavier allein, der Ofen knistert

  Instrumente
    Filzklavier   Thema und Arpeggien, gedaempfte Haemmer, leicht verstimmte Chorsaiten
    Okarina       Thema im zweiten Durchgang, Gegenstimme im Finale
    Kalimba       Ostinato im Ruheteil, Funkeln in der Bruecke
    Spieluhr      Antworten in den Melodiepausen
    E-Piano       FM-Rhodes, Akkordflaechen
    Kontrabass    gezupft, ruhig
    Streicher     Flaeche in Bruecke und Finale
    Schlagwerk    Lo-Fi: weicher Kick auf 1 und 2-und, Besen auf 2 und 4, Shaker
    Ofen          ganz leises Knistern unter allem

  Aufbau (Takte)
    Intro   4   Filzklavier allein, gebrochene Akkorde
    A       8   Thema im Klavier, Bass + Rhodes + Besen kommen dazu
    A'      8   Thema auf der Okarina, Klavier arpeggiert, Spieluhr antwortet
    B       8   Bruecke Richtung h-Moll, Streicher, Kalimba funkelt
    C       8   Ruheteil: Kalimba-Ostinato, Rhodes, Spieluhr - kein Schlagzeug
    D       8   Zweites Thema im Klavier, hoeher und freier
    A''     8   Thema im Klavier, Okarina-Gegenstimme, Streicher
    Wende   4   wird wieder leer, landet auf A7sus -> Intro

  Assets/music/hub.wav   (44.1 kHz, 16 bit, Stereo; .meta wie wald.wav)

Aufruf aus dem Projektordner:  python Tools/hub_musik.py   (--pegel zeigt Spurpegel)
"""

import math
import os
import sys
import wave

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import eis_musik as kit  # noqa: E402  (Hall, Mix, Flaeche)
import menu_musik as mm  # noqa: E402  (Okarina, Spieluhr, Rhodes, Bass, Besen, Ofen)

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "music", "hub.wav")

SR = kit.SR
BPM = 78
BEAT = 60.0 / BPM
BAR = 4 * BEAT
SWING = 0.58
rng = np.random.RandomState(2603)

midi = kit.midi
hz = kit.hz


def swung(beat):
    whole = math.floor(beat)
    frac = beat - whole
    if frac < 0.5:
        frac = frac / 0.5 * SWING
    else:
        frac = SWING + (frac - 0.5) / 0.5 * (1 - SWING)
    return whole + frac


def at(bar_start, beat):
    return bar_start + swung(beat) * BEAT


# ================================================================ Akkorde

CHORDS = {
    "Dmaj7": ("D", ["D", "F#", "A", "C#"]),
    "Dmaj9": ("D", ["D", "F#", "C#", "E"]),
    "Bm7": ("B", ["B", "D", "F#", "A"]),
    "Bm9": ("B", ["B", "D", "A", "C#"]),
    "Gmaj7": ("G", ["G", "B", "D", "F#"]),
    "Gmaj9": ("G", ["G", "B", "F#", "A"]),
    "A7sus": ("A", ["A", "D", "E", "G"]),
    "A7": ("A", ["A", "C#", "E", "G"]),
    "A6": ("A", ["A", "C#", "E", "F#"]),
    "F#m7": ("F#", ["F#", "A", "C#", "E"]),
    "Em7": ("E", ["E", "G", "B", "D"]),
    "Em9": ("E", ["E", "G", "D", "F#"]),
    "Cmaj7": ("C", ["C", "E", "G", "B"]),
}

PROG_I = ["Dmaj9", "Bm9", "Gmaj9", "A7sus"]
PROG_A = ["Dmaj7", "Bm7", "Gmaj7", "A7sus", "Dmaj7", "F#m7", "Em7", "A7"]
PROG_B = ["Gmaj7", "A6", "F#m7", "Bm7", "Em7", "F#m7", "Gmaj7", "A7sus"]
PROG_C = ["Bm9", "Gmaj9", "Dmaj9", "A6", "Bm9", "Gmaj9", "Em9", "A7sus"]
PROG_D = ["Gmaj9", "F#m7", "Em9", "Dmaj9", "Gmaj9", "F#m7", "Cmaj7", "A7sus"]
PROG_T = ["Dmaj9", "Bm9", "Em9", "A7sus"]

SECTIONS = [("intro", 4), ("A", 8), ("A2", 8), ("B", 8), ("C", 8), ("D", 8), ("A3", 8), ("turn", 4)]

# ================================================================ Melodien
# (Note oder None, Schlaege)

MEL_A = [
    [("F#5", 1.5), ("E5", .5), ("D5", 1), ("A4", 1)],
    [("B4", 1.5), ("C#5", .5), ("D5", 1), ("F#5", 1)],
    [("E5", 2), ("D5", 1), ("B4", 1)],
    [("A4", 3), (None, 1)],
    [("F#5", 1.5), ("E5", .5), ("D5", 1), ("A5", 1)],
    [("A5", 1.5), ("F#5", .5), ("E5", 1), ("C#5", 1)],
    [("D5", 1), ("E5", 1), ("G5", 1.5), ("F#5", .5)],
    [("E5", 2.5), (None, 1.5)],
]
MEL_B = [
    [("B5", 2), ("A5", 1), ("F#5", 1)],
    [("E5", 2), ("C#5", 1), ("E5", 1)],
    [("F#5", 1.5), ("E5", .5), ("C#5", 1), ("A4", 1)],
    [("B4", 3), (None, 1)],
    [("G5", 2), ("F#5", 1), ("E5", 1)],
    [("A5", 2), ("F#5", 1), ("C#5", 1)],
    [("D5", 1), ("E5", 1), ("F#5", 1), ("B5", 1)],
    [("A5", 3), (None, 1)],
]
# Zweites Thema: hoeher, mehr Bewegung, viel Atem dazwischen
MEL_D = [
    [(None, .5), ("B5", .5), ("D6", .5), ("B5", .5), ("A5", 2)],
    [(None, .5), ("A5", .5), ("C#6", .5), ("A5", .5), ("E5", 2)],
    [(None, .5), ("G5", .5), ("B5", .5), ("D6", .5), ("F#6", 1.5), ("E6", .5)],
    [("D6", 2), ("C#6", 1), ("A5", 1)],
    [(None, .5), ("B5", .5), ("D6", .5), ("B5", .5), ("A5", 1.5), ("F#5", .5)],
    [("A5", 1), ("C#6", 1), ("E6", 2)],
    [("E6", 1.5), ("D6", .5), ("B5", 1), ("G5", 1)],
    [("A5", 2), ("E5", 1), (None, 1)],
]
# Okarina im Finale: lange Toene unter dem Klavierthema
COUNTER_A = [
    [("A4", 2), ("F#4", 2)],
    [("D4", 2), ("F#4", 2)],
    [("G4", 2), ("B4", 2)],
    [("C#5", 2), ("D5", 1), ("E5", 1)],
    [("A4", 2), ("F#4", 2)],
    [("C#5", 2), ("A4", 2)],
    [("B4", 2), ("D5", 2)],
    [("C#5", 2), ("E5", 1), ("G5", 1)],
]
TURN_MEL = [
    [(None, 1), ("F#5", 1), ("E5", 1), ("D5", 1)],
    [("B4", 3), (None, 1)],
    [(None, 1), ("G5", 1), ("F#5", 1), ("E5", 1)],
    [("D5", 1), ("E5", 1), ("A4", 2)],
]
# Spieluhr: Antworten in die Pausen des Themas (Takt 4 und 8)
BOX_ANSWER_4 = [(2.5, "C#6", .5), (3.0, "E6", .5), (3.5, "A6", .5)]
BOX_ANSWER_8 = [(2.5, "A6", .5), (3.0, "F#6", .5), (3.5, "E6", .5)]
BOX_ANSWER_B4 = [(3.0, "F#6", .5), (3.5, "D6", .5)]
BOX_ANSWER_B8 = [(3.0, "E6", .5), (3.5, "C#6", .5)]


# ================================================================ Klangerzeuger

def felt_piano(f, dur, vel=1.0):
    """Gedaempftes Klavier: weiche Haemmer (wenig Obertoene), leicht gespreizte
    Teiltoene, zwei Saiten pro Ton mit Schwebung, Daempfer am Notenende."""
    tail = 1.6
    n = int((dur + tail) * SR)
    t = np.arange(n) / SR
    s = np.zeros(n)
    bright = 0.35 + 0.25 * vel
    height = (f / 261.6) ** 0.5
    B = 0.00018
    for k in range(1, 9):
        fk = f * k * math.sqrt(1 + B * k * k)
        if fk > 9000:
            break
        amp = k ** -1.4 * math.exp(-(k - 1) * (1 - bright) * 0.9)
        dec = (0.7 + 0.55 * (k - 1)) * height
        part = np.sin(2 * np.pi * fk * t + rng.uniform(0, 6.28))
        if k <= 3:
            # zweite Saite, ein paar Cent daneben -> langsames Schweben
            part = 0.5 * part + 0.5 * np.sin(2 * np.pi * fk * 1.0009 * t)
        s += amp * part * np.exp(-t * dec)
    # Filz auf der Saite: kurzer dumpfer Stoss
    thump = kit.smooth_kernel(rng.randn(n), 24) * np.exp(-t * 90) * 0.5
    env = np.clip(t / 0.005, 0, 1)
    damper = np.where(t > dur, np.exp(-(t - dur) * 7.0), 1.0)
    return (s + thump) * env * damper * 0.075 * vel


def kalimba(f, vel=1.0):
    """Daumenklavier: Grundton, ein heller unharmonischer Teilton, Zungenklick."""
    n = int(1.8 * SR)
    t = np.arange(n) / SR
    s = (np.sin(2 * np.pi * f * t) * np.exp(-t * 2.8)
         + 0.28 * np.sin(2 * np.pi * f * 5.93 * t) * np.exp(-t * 22)
         + 0.07 * np.sin(2 * np.pi * f * 3.01 * t) * np.exp(-t * 9))
    click = kit.smooth_kernel(rng.randn(n), 2) * np.exp(-t * 600) * 0.12
    s = (s + click) * np.clip(t / 0.002, 0, 1)
    return s * 0.06 * vel


def lofi_kick(vel=1.0):
    return kit.smooth_kernel(mm.soft_kick(vel * 0.9), 10)


# ================================================================ Bausteine

Track = kit.Track


def chord_notes(name, low, high):
    _, tones = CHORDS[name]
    out = []
    for o in range(1, 8):
        for tn in tones:
            m = midi(tn + str(o))
            if low <= m <= high:
                out.append(m)
    return sorted(out)


def melody(lines, start, tr, inst, octave_shift=0, vel=1.0, legato=0.95, human=0.0):
    for b, bar in enumerate(lines):
        pos = 0.0
        t0 = start + b * BAR
        for note, beats in bar:
            if note is not None:
                on = at(t0, pos)
                off = at(t0, pos + beats)
                f = hz(midi(note) + 12 * octave_shift)
                acc = 1.0 if abs(pos - round(pos)) < 1e-6 else 0.82
                # Klavier: ein Hauch Verzoegerung, wie von Hand gespielt
                jitter = rng.uniform(0, human)
                tr.add(inst(f, (off - on) * legato, vel * acc * rng.uniform(0.92, 1.0)), on + jitter)
            pos += beats


def piano_melody(lines, start, tr, vel=1.0, octave_shift=0):
    """Thema im Klavier, rechts eine Sexte/Terz darunter auf den langen Toenen."""
    melody(lines, start, tr, felt_piano, octave_shift, vel * 1.4, legato=1.0, human=0.012)


def broken_chords(prog, start, tr, vel=1.0, pattern="intro"):
    """Linke Hand: Grundton tief, dann der Akkord sanft gebrochen nach oben."""
    patterns = {
        # (Schlag, Stimme) - Stimme 0 = Bass, 1.. = Akkordtoene von unten
        "intro": [(0, 0), (0.5, 1), (1, 2), (1.5, 3), (2, 4), (3, 3), (3.5, 2)],
        "flow": [(0, 0), (0.5, 2), (1, 3), (1.5, 4), (2, 1), (2.5, 3), (3, 4), (3.5, 3)],
        "sparse": [(0, 0), (1.5, 2), (2, 3), (3, 4)],
    }[pattern]
    for b, name in enumerate(prog):
        root = CHORDS[name][0]
        r = midi(root + "2")
        if r < midi("C#2"):
            r += 12
        upper = chord_notes(name, midi("F#3"), midi("E5"))[:5]
        voices = [r + 12] + upper
        t0 = start + b * BAR
        for beat, v in patterns:
            if v >= len(voices):
                continue
            on = at(t0, beat) + rng.uniform(0, 0.01)
            ln = BAR - swung(beat) * BEAT
            tr.add(felt_piano(hz(voices[v]), ln, vel * (1.0 if v == 0 else 0.62)), on)


def ep_pads(prog, start, tr, vel=1.0):
    for b, name in enumerate(prog):
        notes = chord_notes(name, midi("F#3"), midi("D5"))[:4]
        t0 = start + b * BAR
        for j, m in enumerate(notes):
            tr.add(mm.rhodes(hz(m), BAR * 0.96, vel), t0 + j * 0.012)


def bass(prog, start, tr, style="two", vel=1.0):
    for b, name in enumerate(prog):
        root, tones = CHORDS[name]
        r = midi(root + "2")
        if r < midi("C#2"):
            r += 12
        fifth = r + 7
        t0 = start + b * BAR
        if style == "two":
            tr.add(mm.upright(hz(r), BEAT * 2.2, vel), at(t0, 0))
            tr.add(mm.upright(hz(fifth), BEAT * 0.9, vel * 0.7), at(t0, 2.5))
            tr.add(mm.upright(hz(r + 12), BEAT * 0.5, vel * 0.5), at(t0, 3.5))
        else:  # long
            tr.add(mm.upright(hz(r), BAR * 0.9, vel), t0)


def strings(prog, start, tr, vel=1.0):
    for b, name in enumerate(prog):
        fs = [hz(m) for m in chord_notes(name, midi("A3"), midi("F#5"))[:4]]
        tr.add(kit.pad(fs, BAR, vel), start + b * BAR)


def kalimba_ostinato(prog, start, tr, vel=1.0):
    """Achtel-Muster ueber die Akkordtoene, zwei Oktaven hoch, wiegt hin und her."""
    order = [0, 2, 1, 3, 2, 1, 3, 2]
    for b, name in enumerate(prog):
        notes = chord_notes(name, midi("D5"), midi("A6"))[:4]
        t0 = start + b * BAR
        for i, idx in enumerate(order):
            if idx >= len(notes):
                continue
            v = vel * (1.0 if i % 2 == 0 else 0.7) * rng.uniform(0.85, 1.0)
            tr.add(kalimba(hz(notes[idx]), v), at(t0, i * 0.5))


def kalimba_sparkle(prog, start, tr, vel=1.0):
    """Ein paar Toene oben drauf, nur auf der Vier jedes zweiten Takts."""
    for b, name in enumerate(prog):
        if b % 2 == 0:
            continue
        notes = chord_notes(name, midi("A5"), midi("E7"))
        t0 = start + b * BAR
        for j, m in enumerate(notes[-3:]):
            tr.add(kalimba(hz(m), vel * (1 - j * 0.15)), at(t0, 3 + j * 0.5))


def box_hits(hits, bar_start, tr, vel=1.0):
    for beat, note, beats in hits:
        tr.add(mm.music_box(hz(midi(note)), beats * BEAT, vel), at(bar_start, beat))


def drums(bars, start, T, style="lofi", vel=1.0):
    for b in range(bars):
        t0 = start + b * BAR
        if style in ("lofi", "full"):
            T["kick"].add(lofi_kick(vel), at(t0, 0))
            T["kick"].add(lofi_kick(vel * 0.6), at(t0, 2.5))
            if style == "full" and b % 2 == 1:
                T["kick"].add(lofi_kick(vel * 0.45), at(t0, 3.5))
            T["brush"].add(mm.brush(vel * 0.85), at(t0, 1))
            T["brush"].add(mm.brush(vel * 0.85), at(t0, 3))
        if style != "none":
            for i in range(8):
                T["shaker"].add(mm.shaker(vel * (0.75 if i % 2 else 0.4)), at(t0, i * 0.5))


# ================================================================ Lied

def compose():
    total_bars = sum(b for _, b in SECTIONS)
    length = total_bars * BAR

    T = {
        "piano": Track(length, pan=-0.1, wet=0.32),
        "pianoL": Track(length, pan=-0.25, wet=0.3),
        "lead": Track(length, pan=0.1, wet=0.32),
        "box": Track(length, pan=0.35, wet=0.5),
        "kal": Track(length, pan=0.3, wet=0.4),
        "ep": Track(length, pan=0.2, wet=0.35),
        "bass": Track(length, pan=0.0, wet=0.06),
        "str": Track(length, pan=-0.05, wet=0.6),
        "kick": Track(length, pan=0.0, wet=0.04),
        "brush": Track(length, pan=0.15, wet=0.2),
        "shaker": Track(length, pan=0.4, wet=0.12),
        "fire": Track(length, pan=-0.3, wet=0.1),
    }

    T["fire"].add(mm.fire(length) * 0.6, 0)

    t = 0.0
    for name, bars in SECTIONS:
        if name == "intro":
            broken_chords(PROG_I, t, T["pianoL"], 0.85, "intro")
        elif name == "A":
            piano_melody(MEL_A, t, T["piano"], 1.0)
            broken_chords(PROG_A, t, T["pianoL"], 0.7, "sparse")
            ep_pads(PROG_A, t, T["ep"], 0.35)
            bass(PROG_A, t + 0 * BAR, T["bass"], "two", 0.75)
            drums(4, t + 4 * BAR, T, "lofi", 0.6)
        elif name == "A2":
            melody(MEL_A, t, T["lead"], mm.ocarina, vel=0.52)
            broken_chords(PROG_A, t, T["pianoL"], 0.65, "flow")
            ep_pads(PROG_A, t, T["ep"], 0.3)
            bass(PROG_A, t, T["bass"], "two", 0.8)
            drums(bars, t, T, "lofi", 0.75)
            box_hits(BOX_ANSWER_4, t + 3 * BAR, T["box"], 1.1)
            box_hits(BOX_ANSWER_8, t + 7 * BAR, T["box"], 1.0)
        elif name == "B":
            piano_melody(MEL_B, t, T["piano"], 0.95)
            broken_chords(PROG_B, t, T["pianoL"], 0.6, "sparse")
            strings(PROG_B, t, T["str"], 0.75)
            kalimba_sparkle(PROG_B, t, T["kal"], 0.8)
            bass(PROG_B, t, T["bass"], "two", 0.8)
            drums(bars, t, T, "full", 0.75)
            box_hits(BOX_ANSWER_B4, t + 3 * BAR, T["box"], 1.0)
            box_hits(BOX_ANSWER_B8, t + 7 * BAR, T["box"], 0.9)
        elif name == "C":
            kalimba_ostinato(PROG_C, t, T["kal"], 0.85)
            ep_pads(PROG_C, t, T["ep"], 0.4)
            bass(PROG_C, t, T["bass"], "long", 0.55)
            strings(PROG_C[4:], t + 4 * BAR, T["str"], 0.45)
            melody([[("F#6", 2), ("D6", 2)], [("B5", 4)], [("A5", 1), ("C#6", 1), ("E6", 2)], [("C#6", 4)]],
                   t + 4 * BAR, T["box"], mm.music_box, vel=1.0)
        elif name == "D":
            piano_melody(MEL_D, t, T["piano"], 0.9)
            broken_chords(PROG_D, t, T["pianoL"], 0.6, "flow")
            kalimba_ostinato(PROG_D[:2], t, T["kal"], 0.4)
            ep_pads(PROG_D, t, T["ep"], 0.28)
            bass(PROG_D, t, T["bass"], "two", 0.75)
            drums(bars, t, T, "lofi", 0.7)
        elif name == "A3":
            piano_melody(MEL_A, t, T["piano"], 1.0)
            melody(COUNTER_A, t, T["lead"], mm.ocarina, vel=0.4)
            broken_chords(PROG_A, t, T["pianoL"], 0.6, "sparse")
            strings(PROG_A, t, T["str"], 0.7)
            ep_pads(PROG_A, t, T["ep"], 0.3)
            bass(PROG_A, t, T["bass"], "two", 0.85)
            drums(bars, t, T, "full", 0.8)
            box_hits(BOX_ANSWER_8, t + 7 * BAR, T["box"], 0.9)
        elif name == "turn":
            piano_melody(TURN_MEL, t, T["piano"], 0.8)
            broken_chords(PROG_T, t, T["pianoL"], 0.7, "intro")
            ep_pads(PROG_T[:2], t, T["ep"], 0.22)
            bass(PROG_T[:2], t, T["bass"], "long", 0.5)
            drums(2, t, T, "lofi", 0.45)
        t += bars * BAR
    return T, length


def tape_wow(loop, length):
    """Ganz leichtes Leiern wie von Band - Periode passt ganz in die Schleife."""
    n = len(loop)
    cycles_slow = max(1, round(length / 2.3))
    cycles_fast = max(1, round(length / 0.37))
    i = np.arange(n)
    d = (0.00065 * np.sin(2 * np.pi * cycles_slow * i / n)
         + 0.00008 * np.sin(2 * np.pi * cycles_fast * i / n + 1.0)) * SR
    pos = (i - d - 0.001 * SR) % n
    out = np.zeros_like(loop)
    xp = np.arange(n + 1)
    for ch in range(loop.shape[1]):
        src = np.concatenate([loop[:, ch], loop[:1, ch]])
        out[:, ch] = np.interp(pos, xp, src)
    return out


def main():
    T, length = compose()
    if "--pegel" in sys.argv:
        for name, tr in T.items():
            rms = math.sqrt((tr.buf ** 2).mean()) + 1e-12
            print("%-7s %6.1f dB" % (name, 20 * math.log10(rms)))
    data = kit.mix(T, length)
    data = tape_wow(data, length)
    data *= 0.85 / np.abs(data).max()
    pcm = (np.clip(data, -1, 1) * 32767).astype(np.int16)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with wave.open(OUT, "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    rms = 20 * math.log10(math.sqrt((data ** 2).mean()))
    print("hub.wav  %.1f s  RMS %.1f dBFS  (%d Takte, %d BPM)"
          % (len(data) / SR, rms, sum(b for _, b in SECTIONS), BPM))

    meta = OUT + ".meta"
    if not os.path.exists(meta):
        import uuid
        src = open(os.path.join(os.path.dirname(OUT), "wald.wav.meta")).read()
        open(meta, "w", newline="\n").write(src.replace("56b195d28eea480ba70e21313510ac90", uuid.uuid4().hex))


if __name__ == "__main__":
    main()
