"""
Musik des Hauptmenues: "Ofenwarm" - das Titelthema.

Komplett im Code komponiert und synthetisiert, wie Tools/kueche_musik.py
(Hall, Mix und ein paar Klangerzeuger kommen aus Tools/eis_musik.py).

Das Menue ist eine Kueche am Abend, das Holzschild haengt an Ketten, ein
Keks wartet aufs Abenteuer. Darum: gemuetlich, warm, ein bisschen verschmitzt -
und in der Bruecke ein Funke "Risk it".

  - F-Dur, 96 BPM, leichter Swing (Achtel 62:38)
  - Das Hauptmotiv singt den Titel: "Risk it, lit-tle bis-cuit"
    (A4 C5 | F5 . E5 D5 | C5)
  - Schleife = genau 48 Takte (120 s), Hallfahne nach vorn gefaltet
  - Das Lied beginnt leise (Spieluhr + E-Piano): Das Menue blendet die Musik
    nach dem Studio-Logo ein und startet sie dafuer von vorn.

  Instrumente
    Okarina       das Thema, warm, Vibrato erst nach dem Anblasen
    Spieluhr      Motiv im Intro, Antworten in den Pausen, Doppelung im Finale
    E-Piano       FM-Rhodes, Akkorde - traegt Intro und ruhigen Teil
    Gitarre       Karplus-Strong, Swing-Schlagmuster
    Kontrabass    gezupft, Grundton/Quinte, in der Bruecke laufend
    Streicher     Flaeche in Bruecke und Finale
    Schlagwerk    Besen auf der Snare, weicher Kick, Schneebesen-Shaker,
                  Loeffel am Glas, Wirbel in der Ueberleitung
    Ofen          ganz leises Knistern unter allem

  Aufbau (Takte)
    Intro   4   Spieluhr spielt das Motiv, E-Piano, Ofen knistert
    A       8   Thema in der Okarina, Gitarre + Bass + Besen
    A'      8   Thema, Spieluhr antwortet in den Pausen
    B       8   Bruecke in d-Moll - hoeher, weiter, Streicher, laufender Bass
    C       8   Ruhepunkt: E-Piano + Spieluhr-Variation, nur Shaker
    A''     8   Thema voll, Spieluhr doppelt eine Oktave hoeher
    Wende   4   Ueberleitung mit Besenwirbel zurueck ins Intro

  Assets/music/main_menu.wav   (44.1 kHz, 16 bit, Stereo; .meta wie wald.wav)

Aufruf aus dem Projektordner:  python Tools/menu_musik.py
"""

import math
import os
import sys
import wave

import numpy as np
from scipy.signal import lfilter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import eis_musik as kit  # noqa: E402  (Hall, Mix, Celesta, Flaeche, Kick)

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "music", "main_menu.wav")

SR = kit.SR
BPM = 96
BEAT = 60.0 / BPM
BAR = 4 * BEAT
SWING = 0.62          # Lage der "und"-Achtel im Schlag (0.5 = gerade)
rng = np.random.RandomState(1717)

midi = kit.midi
hz = kit.hz


def swung(beat):
    """Schlagposition -> geswingte Schlagposition (nur die Achtel verschieben sich)."""
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
    "F": ("F", ["F", "A", "C"]),
    "Am": ("A", ["A", "C", "E"]),
    "Bb": ("A#", ["A#", "D", "F"]),
    "C": ("C", ["C", "E", "G"]),
    "C7": ("C", ["C", "E", "G", "A#"]),
    "Dm": ("D", ["D", "F", "A"]),
    "Gm": ("G", ["G", "A#", "D", "F"]),
    "A7": ("A", ["A", "C#", "E", "G"]),
    "Fmaj7": ("F", ["F", "A", "C", "E"]),
}

PROG_I = ["Fmaj7", "Am", "Bb", "C"]
PROG_A = ["F", "Am", "Bb", "C", "F", "Dm", "Gm", "C7"]
PROG_B = ["Dm", "Gm", "F", "C", "Dm", "Bb", "Gm", "A7"]
PROG_C = ["Dm", "Bb", "F", "C", "Dm", "Bb", "Gm", "C7"]
PROG_T = ["F", "Dm", "Gm", "C7"]

SECTIONS = [("intro", 4), ("A", 8), ("A2", 8), ("B", 8), ("C", 8), ("A3", 8), ("turn", 4)]

# ================================================================ Melodien
# (Note oder None, Schlaege). "A#" = Bb.

MEL_A = [
    [("A4", .5), ("C5", .5), ("F5", 1), ("E5", .5), ("D5", .5), ("C5", 1)],   # Risk it, lit-tle bis-cuit
    [("E5", 1.5), ("D5", .5), ("C5", 1), ("A4", 1)],
    [("D5", .5), ("C5", .5), ("A#4", 1), ("A4", .5), ("G4", .5), ("F4", 1)],
    [("G4", 3), (None, 1)],
    [("A4", .5), ("C5", .5), ("F5", 1), ("E5", .5), ("D5", .5), ("C5", 1)],
    [("F5", 1.5), ("E5", .5), ("D5", 1), ("A4", 1)],
    [("A#4", .5), ("D5", .5), ("C5", .5), ("A#4", .5), ("G4", 1), ("F4", 1)],
    [("G4", 1), ("A4", .5), ("A#4", .5), ("C5", 1.5), (None, .5)],
]
MEL_B = [
    [("D5", 1.5), ("E5", .5), ("F5", 1), ("A5", 1)],
    [("G5", 1.5), ("F5", .5), ("D5", 2)],
    [("C5", 1), ("F5", 1), ("A5", 1.5), ("G5", .5)],
    [("G5", 2), ("E5", 1), ("C5", 1)],
    [("D5", 1.5), ("E5", .5), ("F5", 1), ("A5", 1)],
    [("A#5", 1.5), ("A5", .5), ("G5", 1), ("F5", 1)],
    [("G5", 1.5), ("F5", .5), ("D5", 1), ("A#4", 1)],
    [("A4", 1), ("C#5", 1), ("E5", 1), ("G5", 1)],
]
# Spieluhr: das Motiv im Intro, eine Oktave hoch
BOX_INTRO = [
    [("A5", .5), ("C6", .5), ("F6", 1), ("E6", .5), ("D6", .5), ("C6", 1)],
    [("E6", 1.5), ("D6", .5), ("C6", 1), ("A5", 1)],
    [("D6", .5), ("C6", .5), ("A#5", 1), ("A5", .5), ("G5", .5), ("F5", 1)],
    [("G5", 2), ("C6", 1), ("E6", 1)],
]
# Antworten in den Pausen des Themas (Takt 4 und 8 von A')
BOX_ANSWER_4 = [(1.0, "E6", .5), (1.5, "G6", .5), (2.0, "C7", 1), (3.0, "G6", 1)]
BOX_ANSWER_8 = [(2.5, "E6", .5), (3.0, "G6", .5), (3.5, "A#6", .5)]
# Ruhiger Teil: das Motiv verlangsamt, in Moll gefaerbt
BOX_C = [
    [("A5", 1), ("D6", 1), ("F6", 2)],
    [("F6", 1), ("D6", 1), ("A#5", 2)],
    [("A5", 1), ("C6", 1), ("F6", 1), ("A6", 1)],
    [("G6", 3), (None, 1)],
    [("A5", 1), ("D6", 1), ("F6", 2)],
    [("A#6", 1.5), ("A6", .5), ("F6", 2)],
    [("G6", 1), ("F6", 1), ("D6", 1), ("A#5", 1)],
    [("C6", 2), ("E6", 1), ("G6", 1)],
]
TURN_MEL = [
    [("A4", .5), ("C5", .5), ("F5", 2), (None, 1)],
    [("F5", .5), ("E5", .5), ("D5", 1), ("A4", 2)],
    [("A#4", .5), ("D5", .5), ("G5", 2), (None, 1)],
    [("E5", 1), ("D5", 1), ("C5", 1), ("G4", 1)],
]


# ================================================================ Klangerzeuger

def ocarina(f, dur, vel=1.0):
    """Runder Ton, kaum Obertoene, Luft beim Anblasen, spaetes Vibrato."""
    n = int((dur + 0.25) * SR)
    t = np.arange(n) / SR
    vib = 1 + 0.0055 * np.sin(2 * np.pi * 5.3 * t) * np.clip((t - 0.22) / 0.3, 0, 1)
    ph = 2 * np.pi * np.cumsum(np.full(n, f) * vib) / SR
    s = np.sin(ph) + 0.16 * np.sin(2 * ph) + 0.07 * np.sin(3 * ph) + 0.02 * np.sin(4 * ph)
    noise = rng.randn(n)
    noise = kit.smooth_kernel(noise, 3) - kit.smooth_kernel(noise, 14)
    chiff = noise * np.exp(-t * 35) * 0.35 + noise * 0.025
    e = kit.env_adsr(n, 0.03, 0.12, 0.82, 0.2)
    return (s + chiff) * e * 0.15 * vel


def music_box(f, dur=1.0, vel=1.0):
    """Stahlzunge: Grundton lang, ein paar unharmonische Teiltoene kurz, kleiner Klick."""
    n = int(max(1.8, dur + 1.0) * SR)
    t = np.arange(n) / SR
    s = (np.sin(2 * np.pi * f * t) * np.exp(-t * 2.4)
         + 0.22 * np.sin(2 * np.pi * f * 2.0 * t) * np.exp(-t * 5.0)
         + 0.10 * np.sin(2 * np.pi * f * 5.4 * t) * np.exp(-t * 13)
         + 0.05 * np.sin(2 * np.pi * f * 8.9 * t) * np.exp(-t * 25))
    click = rng.randn(n) * np.exp(-t * 900) * 0.15
    s = (s + click) * np.clip(t / 0.0015, 0, 1)
    return s * 0.07 * vel


def rhodes(f, dur, vel=1.0):
    """FM-E-Piano: Modulator 1:1 mit fallendem Index, heller Zungen-Pling beim Anschlag."""
    n = int((dur + 0.5) * SR)
    t = np.arange(n) / SR
    index = (1.5 * np.exp(-t * 5) + 0.22) * (0.6 + 0.4 * vel)
    s = np.sin(2 * np.pi * f * t + index * np.sin(2 * np.pi * f * t))
    s += 0.10 * np.sin(2 * np.pi * f * 14.0 * t) * np.exp(-t * 45)
    env = np.exp(-t * 0.9) * np.clip(t / 0.003, 0, 1)
    rel = np.clip((dur + 0.5 - t) / 0.35, 0, 1)
    # leichtes Tremolo wie beim echten Rhodes-Verstaerker
    trem = 1 + 0.06 * np.sin(2 * np.pi * 4.5 * t)
    return s * env * rel * trem * 0.085 * vel


def ks_string(f, dur, vel=1.0, bright=0.5, decay=0.996):
    """Karplus-Strong: Rauschstoss in einer gedaempften Verzoegerungsschleife."""
    n = int((dur + 0.15) * SR)
    period = SR / f - 0.5
    N = max(2, int(round(period)))
    burst = rng.randn(N)
    k = max(1, int((1 - bright) * 10))
    burst = kit.smooth_kernel(burst, k)
    burst -= burst.mean()
    x = np.zeros(n)
    x[:N] = burst
    a = np.zeros(N + 2)
    a[0] = 1.0
    a[N] = -decay * 0.5
    a[N + 1] = -decay * 0.5
    y = lfilter([1.0], a, x)
    # Abdaempfen am Notenende (Hand legt sich auf die Saite)
    t = np.arange(n) / SR
    y *= np.clip((dur + 0.15 - t) / 0.12, 0, 1)
    return y / (np.abs(y).max() + 1e-9) * vel


def guitar_note(f, dur, vel=1.0):
    return ks_string(f, dur, vel, bright=0.55, decay=0.995) * 0.14


def upright(f, dur, vel=1.0):
    """Kontrabass gezupft: dunkle Saite + Sinus-Koerper + Fingerdaumen."""
    n = int((dur + 0.15) * SR)
    t = np.arange(n) / SR
    body = np.sin(2 * np.pi * f * t) * np.exp(-t * 2.2) + 0.3 * np.sin(4 * np.pi * f * t) * np.exp(-t * 4)
    string = ks_string(f, dur, 1.0, bright=0.15, decay=0.997)[:n]
    thump = kit.smooth_kernel(rng.randn(n), 20) * np.exp(-t * 60) * 0.6
    rel = np.clip((dur + 0.15 - t) / 0.1, 0, 1)
    return (body * 0.7 + string * 0.5 + thump) * rel * np.clip(t / 0.004, 0, 1) * 0.16 * vel


def brush(vel=1.0):
    """Besen auf der Snare: weiches Rauschen mit Wisch davor."""
    n = int(0.35 * SR)
    t = np.arange(n) / SR
    noise = rng.randn(n)
    noise = kit.smooth_kernel(noise, 2) - kit.smooth_kernel(noise, 12)
    env = np.clip(t / 0.008, 0, 1) * np.exp(-t * 14)
    tone = np.sin(2 * np.pi * 190 * t) * np.exp(-t * 30) * 0.25
    return (noise * env * 0.22 + tone * 0.28) * vel


def swish(dur, vel=1.0):
    """Besen kreist ueber das Fell (anschwellend, fuer Ueberleitungen)."""
    n = int(dur * SR)
    t = np.arange(n) / SR
    noise = rng.randn(n)
    noise = kit.smooth_kernel(noise, 3) - kit.smooth_kernel(noise, 16)
    env = (t / dur) ** 2
    return noise * env * 0.09 * vel


def soft_kick(vel=1.0):
    s = kit.kick(vel * 0.45)
    return kit.smooth_kernel(s, 6)


def shaker(vel=1.0):
    """Schneebesen in der Schuessel."""
    n = int(0.1 * SR)
    t = np.arange(n) / SR
    noise = rng.randn(n)
    noise = kit.smooth_kernel(noise, 2) - kit.smooth_kernel(noise, 8)
    env = np.clip(t / 0.015, 0, 1) * np.exp(-t * 32)
    return noise * env * 0.12 * vel


def clink(vel=1.0):
    """Loeffel an Glas, auf C7 (liegt in F-Dur gut)."""
    n = int(0.6 * SR)
    t = np.arange(n) / SR
    f = hz(midi("C7"))
    s = (np.sin(2 * np.pi * f * t) * np.exp(-t * 10)
         + 0.45 * np.sin(2 * np.pi * f * 2.32 * t) * np.exp(-t * 18)
         + 0.2 * np.sin(2 * np.pi * f * 3.9 * t) * np.exp(-t * 30))
    return s * np.clip(t / 0.001, 0, 1) * 0.075 * vel


def fire(seconds):
    """Ofen: einzelnes Knacken (Poisson) ueber einem ganz leisen, tiefen Rauschen."""
    n = int(seconds * SR)
    bed = kit.smooth_kernel(rng.randn(n), 60) * 0.02
    out = bed
    tpos = 0.0
    while True:
        tpos += rng.exponential(1 / 5.0)
        k = int(tpos * SR)
        if k >= n - SR // 10:
            break
        m = int(rng.uniform(0.002, 0.008) * SR)
        pop = rng.randn(m) * np.exp(-np.arange(m) / (m / 4))
        pop = pop - kit.smooth_kernel(pop, 3)
        out[k:k + m] += pop * rng.uniform(0.01, 0.06) * (2.0 if rng.rand() < 0.08 else 1.0)
    return out


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


def melody(lines, start, tr, inst, octave_shift=0, vel=1.0, legato=0.92):
    for b, bar in enumerate(lines):
        pos = 0.0
        t0 = start + b * BAR
        for note, beats in bar:
            if note is not None:
                on = at(t0, pos)
                off = at(t0, pos + beats)
                f = hz(midi(note) + 12 * octave_shift)
                # Akzent auf schweren Zeiten, leiser auf den "und"-Achteln
                acc = 1.0 if abs(pos - round(pos)) < 1e-6 else 0.85
                tr.add(inst(f, (off - on) * legato, vel * acc), on)
            pos += beats


def box_hits(hits, bar_start, tr, vel=1.0):
    for beat, note, beats in hits:
        tr.add(music_box(hz(midi(note)), beats * BEAT, vel), at(bar_start, beat))


def strum(prog, start, tr, vel=1.0, pattern="swing"):
    """Gitarre: D . D U . U D U - runter von tief nach hoch, rauf leiser und nur die oberen Saiten."""
    strokes = {
        "swing": [(0, "D", 1.0), (1, "D", 0.8), (1.5, "U", 0.55), (2.5, "U", 0.55), (3, "D", 0.8), (3.5, "U", 0.5)],
        "half": [(0, "D", 0.9), (2, "D", 0.75)],
    }[pattern]
    for b, name in enumerate(prog):
        notes = chord_notes(name, midi("E3"), midi("F4"))
        t0 = start + b * BAR
        for i, (beat, d, v) in enumerate(strokes):
            nxt = strokes[i + 1][0] if i + 1 < len(strokes) else 4.0
            on = at(t0, beat)
            length = at(t0, nxt) - on + 0.05
            seq = notes if d == "D" else list(reversed(notes))[:3]
            for j, m in enumerate(seq):
                tr.add(guitar_note(hz(m), length - j * 0.012, vel * v * (1 - j * 0.06)), on + j * 0.012)


def ep_comp(prog, start, tr, vel=1.0, style="pads"):
    """E-Piano: ganze Akkorde (pads) oder Charleston-Anschlag 1 und 2-und (comp)."""
    for b, name in enumerate(prog):
        notes = chord_notes(name, midi("F3"), midi("E5"))[:4]
        t0 = start + b * BAR
        if style == "pads":
            for j, m in enumerate(notes):
                tr.add(rhodes(hz(m), BAR * 0.95, vel), t0 + j * 0.008)
        else:
            for beat, v, length in ((0, 1.0, 1.2), (1.5, 0.8, 2.2)):
                for m in notes:
                    tr.add(rhodes(hz(m), length * BEAT, vel * v), at(t0, beat))


def bass(prog, start, tr, style="two", vel=1.0):
    """Grundton auf 1, Quinte auf 3 (two) oder laufende Viertel zum naechsten Akkord (walk)."""
    for b, name in enumerate(prog):
        root, tones = CHORDS[name]
        r = midi(root + "2")
        if r < midi("E2"):
            r += 12
        fifth = midi(tones[2] + "2")
        while fifth <= r:
            fifth += 12
        t0 = start + b * BAR
        if style == "two":
            tr.add(upright(hz(r), BEAT * 1.7, vel), at(t0, 0))
            tr.add(upright(hz(fifth), BEAT * 1.4, vel * 0.85), at(t0, 2))
            # Anlauf auf der "und" der Vier zum naechsten Grundton
            nxt = prog[(b + 1) % len(prog)]
            nr = midi(CHORDS[nxt][0] + "2")
            if nr < midi("E2"):
                nr += 12
            step = nr - 1 if nr - 1 != r else nr + 2
            tr.add(upright(hz(step), BEAT * 0.4, vel * 0.55), at(t0, 3.5))
        elif style == "walk":
            third = chord_notes(name, r + 1, r + 7)
            third = third[0] if third else r + 4
            nxt = prog[(b + 1) % len(prog)]
            nr = midi(CHORDS[nxt][0] + "2")
            if nr < midi("E2"):
                nr += 12
            approach = nr + 1 if nr + 1 != fifth else nr - 1
            for beat, m, v in ((0, r, 1.0), (1, third, 0.8), (2, fifth, 0.9), (3, approach, 0.75)):
                tr.add(upright(hz(m), BEAT * 0.9, vel * v), t0 + beat * BEAT)
        else:  # long
            tr.add(upright(hz(r), BAR * 0.9, vel), t0)


def strings(prog, start, tr, vel=1.0):
    for b, name in enumerate(prog):
        fs = [hz(m) for m in chord_notes(name, midi("A3"), midi("F5"))[:4]]
        tr.add(kit.pad(fs, BAR, vel), start + b * BAR)


def drums(bars, start, T, style="groove", vel=1.0):
    for b in range(bars):
        t0 = start + b * BAR
        if style in ("groove", "full"):
            T["kick"].add(soft_kick(vel), at(t0, 0))
            T["kick"].add(soft_kick(vel * 0.7), at(t0, 2))
            if style == "full" and b % 2 == 1:
                T["kick"].add(soft_kick(vel * 0.5), at(t0, 2.5))
            T["brush"].add(brush(vel), at(t0, 1))
            T["brush"].add(brush(vel), at(t0, 3))
        # Schneebesen auf allen (geswingten) Achteln, die "und" lauter
        if style != "none":
            for i in range(8):
                T["shaker"].add(shaker(vel * (1.0 if i % 2 else 0.5)), at(t0, i * 0.5))
        if style in ("groove", "full") and b % 4 == 3:
            T["clink"].add(clink(vel), at(t0, 3.5))


# ================================================================ Lied

def compose():
    total_bars = sum(b for _, b in SECTIONS)
    length = total_bars * BAR

    T = {
        "lead": Track(length, pan=0.0, wet=0.28),
        "box": Track(length, pan=0.3, wet=0.45),
        "ep": Track(length, pan=-0.15, wet=0.3),
        "gtr": Track(length, pan=-0.35, wet=0.18),
        "bass": Track(length, pan=0.0, wet=0.06),
        "str": Track(length, pan=0.1, wet=0.55),
        "cel": Track(length, pan=0.4, wet=0.5),
        "kick": Track(length, pan=0.0, wet=0.05),
        "brush": Track(length, pan=0.1, wet=0.2),
        "shaker": Track(length, pan=0.35, wet=0.12),
        "clink": Track(length, pan=-0.4, wet=0.35),
        "fire": Track(length, pan=-0.2, wet=0.1),
    }

    T["fire"].add(fire(length), 0)

    t = 0.0
    for name, bars in SECTIONS:
        if name == "intro":
            ep_comp(PROG_I, t, T["ep"], 0.55, "pads")
            melody(BOX_INTRO, t, T["box"], music_box, vel=1.4)
            bass(PROG_I[2:], t + 2 * BAR, T["bass"], "long", 0.6)
            T["brush"].add(swish(BAR, 0.8), t + 3 * BAR)
            drums(1, t + 3 * BAR, T, "light", 0.6)
        elif name in ("A", "A2", "A3"):
            full = name == "A3"
            melody(MEL_A, t, T["lead"], ocarina, vel=1.0 if name == "A" else 1.05)
            strum(PROG_A, t, T["gtr"], 0.9 if name == "A" else 1.0)
            bass(PROG_A, t, T["bass"], "two")
            ep_comp(PROG_A, t, T["ep"], 0.55, "comp" if name != "A" else "pads")
            drums(bars, t, T, "full" if full else "groove", 0.85 if name == "A" else 1.0)
            if name == "A2":
                box_hits(BOX_ANSWER_4, t + 3 * BAR, T["box"], 1.4)
                box_hits(BOX_ANSWER_8, t + 7 * BAR, T["box"], 1.25)
            if full:
                melody(MEL_A, t, T["box"], music_box, octave_shift=1, vel=0.55)
                strings(PROG_A, t, T["str"], 0.9)
                for b in range(0, 8, 2):
                    notes = chord_notes(PROG_A[b], midi("C6"), midi("C7"))
                    T["cel"].add(kit.celesta(hz(notes[-1]), BEAT, 0.8), at(t + b * BAR, 2.5))
        elif name == "B":
            melody(MEL_B, t, T["lead"], ocarina, vel=1.05)
            strings(PROG_B, t, T["str"], 1.1)
            strum(PROG_B, t, T["gtr"], 1.0)
            bass(PROG_B, t, T["bass"], "walk", 1.0)
            drums(bars, t, T, "full", 1.0)
            # Streicher-Anlauf in den letzten Takt (A7 drueckt nach d-Moll)
            T["brush"].add(swish(BAR, 1.0), t + 7 * BAR)
        elif name == "C":
            ep_comp(PROG_C, t, T["ep"], 0.5, "pads")
            melody(BOX_C, t, T["box"], music_box, vel=1.35)
            bass(PROG_C, t, T["bass"], "long", 0.6)
            drums(bars, t, T, "light", 0.75)
            strings(PROG_C[4:], t + 4 * BAR, T["str"], 0.5)
            T["brush"].add(swish(BAR, 0.9), t + 7 * BAR)
        elif name == "turn":
            melody(TURN_MEL, t, T["lead"], ocarina, vel=0.95)
            strum(PROG_T, t, T["gtr"], 0.85)
            bass(PROG_T, t, T["bass"], "two", 0.9)
            ep_comp(PROG_T, t, T["ep"], 0.5, "comp")
            drums(3, t, T, "groove", 0.8)
            # Letzter Takt: Bass + Wirbel, dann zurueck ins leise Intro
            for i in range(8):
                T["brush"].add(brush(0.25 + 0.5 * i / 8), t + 3 * BAR + i * BEAT / 2)
            T["clink"].add(clink(1.0), at(t + 3 * BAR, 3.5))
        t += bars * BAR
    return T, length


def main():
    T, length = compose()
    if "--pegel" in sys.argv:
        for name, tr in T.items():
            rms = math.sqrt((tr.buf ** 2).mean()) + 1e-12
            print("%-7s %6.1f dB" % (name, 20 * math.log10(rms)))
    data = kit.mix(T, length)
    pcm = (np.clip(data, -1, 1) * 32767).astype(np.int16)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with wave.open(OUT, "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    rms = 20 * math.log10(math.sqrt((data ** 2).mean()))
    print("main_menu.wav  %.1f s  RMS %.1f dBFS  (%d Takte, %d BPM)"
          % (len(data) / SR, rms, sum(b for _, b in SECTIONS), BPM))

    meta = OUT + ".meta"
    if not os.path.exists(meta):
        import uuid
        src = open(os.path.join(os.path.dirname(OUT), "wald.wav.meta")).read()
        open(meta, "w", newline="\n").write(src.replace("56b195d28eea480ba70e21313510ac90", uuid.uuid4().hex))


if __name__ == "__main__":
    main()
