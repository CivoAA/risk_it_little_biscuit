"""
Musik der Vulkanwelt (Map_World4): "Glutpfad".

Komplett im Code komponiert und synthetisiert, wie Tools/eis_musik.py. Hier
liegt ausserdem der "schwere" Baukasten (Blech, Chor, Taiko, Pauke, Amboss,
Glocke, Braam ...), den Tools/herz_musik.py fuer die Herzkammer mitbenutzt.

Leitmotiv: das Thema dieses Stuecks (MEL_A) ist das Motiv des Verkohlten.
In der Herzkammer kehrt es halb so schnell als Blech + Chor wieder.

Gebaut fuer die Mochi-Melodie (SongSheet.cs, Eintrag "vulkan"):
  - c-Moll (harmonisch: G-Dur als Dominante), 124 BPM, gerade Achtel - eine
    Viertel = 0,48 s liegt nah an Mochis Cooldown (0,44-0,55 s)
  - erste Eins genau bei 0, Schleife = genau 56 Takte, Hallfahne nach vorn
    gefaltet - der Takt laeuft ueber den Loop-Punkt einfach weiter
  - Akkorde unten (PROG_*) sind 1:1 die in SongSheet.cs - wer hier etwas
    aendert, aendert es dort (und in Tools/mochi_mitspielen.py) mit
  - Platz fuer Mochi (Spieluhr G4-D6): das Thema liegt im Horn tief (C4-G5,
    meist darunter), helle Glocken gibt es nur im Intro, Teil C ist
    Mochis Buehne: Bass, Taiko, Lavablubbern.

  Instrumente
    Hoerner       Blech additiv, Helligkeit folgt der Lautstaerke - das Thema
    Glutbass      Saegezahn + Sub, angezerrt, Achtel-Ostinato
    Streicher     Spiccato-Sechzehntel im Refrain
    Chor          "Ah" mit Formanten, Flaeche im Refrain
    Taiko         tiefe Trommeln statt Kick
    Amboss        Schmiede auf 2 und 4 - der Vulkan arbeitet
    Lava          Blubbern (Sinus mit fallender Tonhoehe), Grollen

  Aufbau (Takte)
    Intro    4   Grollen, Amboss, Motiv-Andeutung im Horn
    A        8   Thema im Horn, Glutbass, Taiko
    A'       8   Thema + Gegenstimme, Amboss, Becken
    B        8   Refrain, Chor + Streicher
    C        8   Breakdown - Mochis Buehne
    A''      8   Thema voll, Oktave hoeher verdoppelt
    B'       8   Refrain voll
    Wende    4   Ueberleitung mit Taiko-Wirbel zurueck zum Intro

  Assets/music/vulkan.wav   (44.1 kHz, 16 bit, Stereo; .meta wie eis.wav)

Aufruf aus dem Projektordner:  python Tools/vulkan_musik.py
"""

import math
import os
import sys
import wave

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import eis_musik as kit  # noqa: E402  (Huellkurven, Filter, Track)

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "music", "vulkan.wav")

SR = kit.SR
BPM = 124
BEAT = 60.0 / BPM
BAR = 4 * BEAT
rng = np.random.RandomState(666)

midi = kit.midi
hz = kit.hz
smooth = kit.smooth_kernel
env_adsr = kit.env_adsr
Track = kit.Track

# ================================================================ Akkorde

CHORDS = {
    "Cm": ("C", ["C", "Eb", "G"]),
    "Ab": ("Ab", ["Ab", "C", "Eb"]),
    "Bb": ("Bb", ["Bb", "D", "F"]),
    "G": ("G", ["G", "B", "D"]),
    "Fm": ("F", ["F", "Ab", "C"]),
    "Eb": ("Eb", ["Eb", "G", "Bb"]),
    "Db": ("Db", ["Db", "F", "Ab"]),
    "C": ("C", ["C", "E", "G"]),
}

PROG_I = ["Cm", "Cm", "Ab", "G"]
PROG_A = ["Cm", "Ab", "Bb", "G", "Cm", "Ab", "Fm", "G"]
PROG_B = ["Ab", "Bb", "Eb", "Cm", "Ab", "Bb", "G", "G"]
PROG_C = ["Fm", "Cm", "Ab", "G", "Fm", "Cm", "Db", "G"]
PROG_T = ["Cm", "Ab", "Fm", "G"]

SECTIONS = [("intro", 4), ("A", 8), ("A2", 8), ("B", 8), ("C", 8), ("A3", 8), ("B2", 8), ("turn", 4)]


def song_chords():
    """Akkord je Takt fuer die ganze Schleife - so steht es in SongSheet.cs."""
    progs = {"intro": PROG_I, "A": PROG_A, "A2": PROG_A, "A3": PROG_A, "B": PROG_B, "B2": PROG_B,
             "C": PROG_C, "turn": PROG_T}
    return [c for name, _ in SECTIONS for c in progs[name]]


# ================================================================ Melodien

# Das Motiv des Verkohlten (Horn). (Note oder None, Schlaege)
MEL_A = [
    [("C4", .5), ("G4", .5), ("C5", 1), ("D5", .5), ("Eb5", 1), ("D5", .5)],
    [("C5", 1.5), ("Ab4", .5), ("Eb4", 2)],
    [("F4", .5), ("Bb4", .5), ("D5", 1), ("F5", .5), ("Eb5", 1), ("D5", .5)],
    [("B4", 3), (None, 1)],
    [("C4", .5), ("G4", .5), ("C5", 1), ("D5", .5), ("Eb5", 1), ("G5", .5)],
    [("F5", 1.5), ("Eb5", .5), ("C5", 2)],
    [("Ab4", 1), ("C5", 1), ("F5", 1), ("Eb5", .5), ("D5", .5)],
    [("D5", 1), ("B4", 1), ("G4", 2)],
]
# Gegenstimme (Posaune, tief, lange Toene)
COUNTER_A = [
    [("G3", 2), ("Eb3", 2)],
    [("Eb3", 2), ("C3", 2)],
    [("D3", 2), ("F3", 2)],
    [("D3", 2), ("B2", 2)],
    [("G3", 2), ("Eb3", 2)],
    [("C3", 2), ("Eb3", 2)],
    [("C3", 2), ("Ab3", 2)],
    [("B2", 2), ("D3", 2)],
]
# Refrain
MEL_B = [
    [("Eb5", 2), ("C5", 1), ("Eb5", 1)],
    [("F5", 2), ("D5", 1), ("Bb4", 1)],
    [("G5", 3), ("F5", .5), ("Eb5", .5)],
    [("Eb5", 2), ("C5", 2)],
    [("Ab4", 1), ("C5", 1), ("Eb5", 2)],
    [("F5", 2), ("D5", 1), ("F5", 1)],
    [("G5", 2), ("F5", 1), ("D5", 1)],
    [("B4", 2), ("D5", 2)],
]
TEASER = [[("C4", .5), ("G4", .5), ("C5", 3)], [(None, 4)], [("Eb4", .5), ("Ab4", .5), ("C5", 3)], [("B4", 4)]]


# ================================================================ Klangerzeuger (schwer)

def _phase(f, n, vib=0.0, vib_rate=5.0, vib_delay=0.25, scoop=0.0, seed_phase=None):
    """Phase (rad) mit verzoegertem Vibrato und einem kleinen Anschleifen von unten."""
    t = np.arange(n) / SR
    v = 1 + vib * np.sin(2 * np.pi * vib_rate * t + (seed_phase if seed_phase is not None else rng.rand() * 6.28)) \
        * np.clip((t - vib_delay) / 0.3, 0, 1)
    if scoop:
        v *= 2 ** (-scoop * np.exp(-t / 0.035) / 12)
    return 2 * np.pi * np.cumsum(f * v) / SR + rng.rand() * 6.28


def brass(f, dur, vel=1.0, bright=1.0, voices=3, attack=0.05):
    """Blech additiv: obere Teiltoene kommen erst mit der Lautstaerke - so 'oeffnet' der Ton."""
    n = int((dur + 0.25) * SR)
    e = env_adsr(n, attack, 0.15, 0.78, 0.22)
    # Anblas-Spitze
    t = np.arange(n) / SR
    e = e * (1 + 0.35 * np.exp(-t / 0.06) * np.clip(t / attack, 0, 1))
    out = np.zeros(n)
    kmax = int(min(26, 7500 / f))
    for v in range(voices):
        det = (v - (voices - 1) / 2) * 0.004
        ph = _phase(f * (1 + det), n, vib=0.004, vib_rate=4.8 + v * 0.3, scoop=0.4)
        for k in range(1, kmax + 1):
            out += np.sin(k * ph) * (e ** (1 + 0.45 * (k - 1) / bright)) / k ** 0.85
    return out * 0.07 * vel / voices


def braam(root_midi, dur=3.2, vel=1.0):
    """Riesiger tiefer Blechcluster (Grundton, Quinte, Oktave), schwillt und zerrt."""
    n = int((dur + 0.4) * SR)
    t = np.arange(n) / SR
    e = np.clip(t / 0.08, 0, 1) * np.exp(-t * 0.55) * env_adsr(n, 0.001, 0, 1, 0.5)
    out = np.zeros(n)
    for m in (root_midi, root_midi + 7, root_midi + 12):
        f = hz(m)
        for d in (-0.006, 0.0, 0.006):
            ph = 2 * np.pi * f * (1 + d) * t + rng.rand() * 6
            kmax = int(min(40, 6000 / f))
            for k in range(1, kmax + 1):
                out += np.sin(k * ph) * (e ** (1 + 0.25 * (k - 1))) / k
    out = np.tanh(out * 0.45) * 0.5
    return out * vel


VOWELS = {
    #       (Frequenz, Breite, Staerke)
    "a": [(750, 110, 1.0), (1150, 130, 0.55), (2600, 200, 0.22), (3300, 250, 0.12)],
    "o": [(450, 90, 1.0), (800, 110, 0.6), (2650, 200, 0.12), (3400, 250, 0.06)],
    "u": [(330, 80, 1.0), (700, 110, 0.25), (2500, 200, 0.06)],
}


def _formant(freq, vowel):
    g = 0.02
    for F, B, A in VOWELS[vowel]:
        g = g + A / (1 + ((freq - F) / B) ** 2)
    return g


def choir(freqs, dur, vel=1.0, vowel="a", singers=4, attack=0.35, release=0.6):
    """Chor: pro Ton mehrere Saenger (verstimmt, eigenes Vibrato), Obertoene durch Formanten."""
    n = int((dur + release + 0.1) * SR)
    e = env_adsr(n, attack, 0.2, 0.9, release)
    out = np.zeros(n)
    for f in freqs:
        kmax = int(min(40, 4500 / f))
        for s in range(singers):
            det = rng.uniform(-0.007, 0.007)
            ph = _phase(f * (1 + det), n, vib=0.009, vib_rate=rng.uniform(4.6, 5.8), vib_delay=0.1)
            # Lautstaerke jedes Saengers atmet ein wenig
            t = np.arange(n) / SR
            sway = 1 + 0.12 * np.sin(2 * np.pi * rng.uniform(0.3, 0.7) * t + rng.rand() * 6)
            for k in range(1, kmax + 1):
                out += np.sin(k * ph) * _formant(k * f, vowel) * sway / k ** 0.3
    breath = rng.randn(n)
    breath = smooth(breath, 3) - smooth(breath, 30)
    out += breath * 0.25
    return out * e * 0.016 * vel / max(1.0, math.sqrt(len(freqs)))


def strings_spic(f, vel=1.0, length=0.13):
    """Streicher spiccato: kurzer, gedaempfter Saegezahn-Ensembleton."""
    n = int((length + 0.12) * SR)
    t = np.arange(n) / SR
    e = np.clip(t / 0.006, 0, 1) * np.exp(-t * 11)
    out = np.zeros(n)
    kmax = int(min(18, 6000 / f))
    for v in range(3):
        ph = 2 * np.pi * f * (1 + (v - 1) * 0.005) * t + rng.rand() * 6
        for k in range(1, kmax + 1):
            out += np.sin(k * ph) / k * np.exp(-t * k * 2.5)
    bow = rng.randn(n)
    bow = (smooth(bow, 2) - smooth(bow, 12)) * np.exp(-t * 60) * 0.4
    return (out + bow) * e * 0.05 * vel


def strings_long(freqs, dur, vel=1.0):
    """Streicherflaeche: viele verstimmte Saegezaehne, weicher Einsatz, Vibrato."""
    n = int((dur + 0.6) * SR)
    e = env_adsr(n, 0.5, 0.2, 0.9, 0.6)
    out = np.zeros(n)
    for f in freqs:
        kmax = int(min(24, 6000 / f))
        for v in range(4):
            ph = _phase(f * (1 + rng.uniform(-0.005, 0.005)), n, vib=0.005, vib_rate=rng.uniform(5, 6))
            for k in range(1, kmax + 1):
                out += np.sin(k * ph) / k ** 1.2
    out = smooth(out, 4)
    return out * e * 0.012 * vel / max(1.0, math.sqrt(len(freqs)))


def glut_bass(f, dur, vel=1.0):
    """Saegezahn + Sub-Sinus, angezerrt und gedaempft - brummt wie heisser Stein."""
    n = int((dur + 0.06) * SR)
    t = np.arange(n) / SR
    ph = (f * t + rng.rand()) % 1.0
    s = (2 * ph - 1) * 0.7 + np.sin(2 * np.pi * f * t) * 0.9
    s = np.tanh(s * 2.2)
    s = smooth(smooth(s, 7), 5)
    e = np.exp(-t * 3.0) * 0.5 + 0.5
    e *= env_adsr(n, 0.004, 0, 1, 0.03)
    return s * e * 0.26 * vel


def taiko(vel=1.0, pitch=1.0):
    """Grosse Trommel: tiefer Sinus-Sweep, Fell-Resonanz, Schlaegel-Rauschen."""
    n = int(0.9 * SR)
    t = np.arange(n) / SR
    f = (52 + 70 * np.exp(-t * 22)) * pitch
    body = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 5.5)
    shell = np.sin(2 * np.pi * 130.8 * t) * np.exp(-t * 14) * 0.25        # Kessel auf C3 - passt zu c-Moll
    noise = rng.randn(n)
    noise = smooth(noise, 14) * np.exp(-t * 35) * 1.2
    return (body + shell + noise) * 0.6 * vel


def taiko_small(vel=1.0):
    """Kleine Trommel (Shime) - trockenes Klacken fuer Achtel. Bewusst ohne
    klaren Ton: ein gestimmtes Klacken in jeder Achtel rieb sich mit jedem
    Akkord, in dem sein Ton fehlt."""
    n = int(0.2 * SR)
    t = np.arange(n) / SR
    s = (np.sin(2 * np.pi * 2310 * t) * np.exp(-t * 70) + 0.6 * np.sin(2 * np.pi * 3370 * t) * np.exp(-t * 90)
         + 0.4 * np.sin(2 * np.pi * 523.3 * t) * np.exp(-t * 60))
    noise = rng.randn(n)
    s += (noise - smooth(noise, 5)) * np.exp(-t * 50) * 0.5
    return s * 0.12 * vel


def timpani(f, vel=1.0):
    """Pauke: Fell-Teiltoene (nicht ganz harmonisch), Schlaegel-Daempfer."""
    n = int(2.0 * SR)
    t = np.arange(n) / SR
    s = np.zeros(n)
    for ratio, amp, dec in ((1.0, 1.0, 2.2), (1.504, 0.5, 3.0), (1.742, 0.3, 3.8), (2.0, 0.25, 4.5),
                            (2.245, 0.12, 5.5)):
        s += np.sin(2 * np.pi * f * ratio * t + rng.rand()) * amp * np.exp(-t * dec)
    s += np.sin(2 * np.pi * f * 0.5 * t) * 0.3 * np.exp(-t * 4)
    noise = rng.randn(n)
    s += smooth(noise, 10) * np.exp(-t * 40) * 0.8
    s *= np.clip(t / 0.003, 0, 1)
    return s * 0.28 * vel


def timp_roll(f, dur, v0=0.2, v1=1.0, rate=14.0):
    n = int(dur * SR)
    out = np.zeros(n + 2 * SR)
    hits = int(dur * rate)
    for i in range(hits):
        q = i / max(1, hits - 1)
        s = timpani(f, (v0 + (v1 - v0) * q ** 1.6) * rng.uniform(0.85, 1.0)) * 0.5
        k = int(i / rate * SR)
        out[k:k + len(s)] += s
    return out


def anvil(vel=1.0):
    """Amboss: hart, hoch, unharmonisch klingend - die Schmiede im Berg."""
    n = int(0.7 * SR)
    t = np.arange(n) / SR
    f = 1046.5                     # C6 - ein Amboss auf D rieb sich mit jedem Es
    s = (np.sin(2 * np.pi * f * t) * np.exp(-t * 7)
         + 0.7 * np.sin(2 * np.pi * f * 2.58 * t) * np.exp(-t * 11)
         + 0.4 * np.sin(2 * np.pi * f * 4.33 * t) * np.exp(-t * 16)
         + 0.25 * np.sin(2 * np.pi * f * 6.1 * t) * np.exp(-t * 24))
    noise = rng.randn(n)
    s += (noise - smooth(noise, 3)) * np.exp(-t * 90) * 0.8
    return s * np.clip(t / 0.0005, 0, 1) * 0.045 * vel


def church_bell(f, vel=1.0, decay=1.0):
    """Grosse Glocke: Brummton, Grundton, kleine Terz, Quinte, Oktave ... langes Ausschwingen."""
    n = int(5.0 * SR)
    t = np.arange(n) / SR
    s = np.zeros(n)
    for ratio, amp, dec in ((0.5, 0.6, 0.5), (1.0, 1.0, 0.8), (1.183, 0.5, 1.2), (1.506, 0.35, 1.5),
                            (2.0, 0.45, 1.6), (2.514, 0.07, 2.4), (2.662, 0.09, 2.6), (3.011, 0.12, 3.2),
                            (4.166, 0.08, 4.0)):
        beat = 1 + 0.06 * np.sin(2 * np.pi * rng.uniform(0.8, 2.0) * t)    # Schwebung
        s += np.sin(2 * np.pi * f * ratio * t + rng.rand() * 6) * amp * np.exp(-t * dec * decay) * beat
    noise = rng.randn(n)
    s += (noise - smooth(noise, 4)) * np.exp(-t * 80) * 0.3
    return s * np.clip(t / 0.002, 0, 1) * 0.10 * vel


def crash(vel=1.0, length=2.6):
    n = int(length * SR)
    t = np.arange(n) / SR
    noise = rng.randn(n)
    noise = noise - smooth(noise, 3)
    shimmer = np.zeros(n)
    for f in (3150, 4420, 5870, 7300):
        shimmer += np.sin(2 * np.pi * f * t + rng.rand() * 6) * np.exp(-t * 2.5)
    return (noise * 0.9 + shimmer * 0.15) * np.exp(-t * 1.6) * 0.09 * vel


def snare(vel=1.0):
    n = int(0.25 * SR)
    t = np.arange(n) / SR
    noise = rng.randn(n)
    noise = noise - smooth(noise, 6)
    s = noise * np.exp(-t * 16) * 0.55 + np.sin(2 * np.pi * 196 * t) * np.exp(-t * 22) * 0.45
    return s * 0.3 * vel


def snare_roll(dur, beat, v0=0.2, v1=1.0):
    n = int(dur * SR)
    out = np.zeros(n + SR)
    hits = int(round(dur / (beat / 4)))
    for i in range(hits):
        q = i / max(1, hits - 1)
        s = snare(v0 + (v1 - v0) * q)
        k = int(i * beat / 4 * SR)
        out[k:k + len(s)] += s
    return out


def riser(dur, vel=1.0):
    """Rauschen, das heller und lauter wird, plus steigender Ton - zieht in den naechsten Teil."""
    n = int(dur * SR)
    t = np.arange(n) / SR
    q = t / dur
    noise = rng.randn(n)
    lo = smooth(noise, 24)
    hi = noise - smooth(noise, 4)
    s = lo * (1 - q) + hi * q
    f = 80 * 2 ** (q * 4)
    s += 0.3 * np.sin(2 * np.pi * np.cumsum(f) / SR)
    return s * q ** 2.2 * 0.12 * vel


def boom(vel=1.0):
    """Tiefer Einschlag (Sub + Rauschwolke)."""
    n = int(3.0 * SR)
    t = np.arange(n) / SR
    f = 30 + 50 * np.exp(-t * 6)
    s = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 1.5)
    noise = rng.randn(n)
    s += smooth(noise, 30) * np.exp(-t * 3) * 1.5
    return s * 0.55 * vel


def rumble(dur, vel=1.0):
    """Grollen im Berg: tiefes, langsam wogendes Rauschen."""
    n = int(dur * SR)
    t = np.arange(n) / SR
    noise = rng.randn(n)
    s = smooth(smooth(noise, 120), 60) * 6
    s *= 0.6 + 0.4 * np.sin(2 * np.pi * 0.23 * t + rng.rand() * 6)
    return s * env_adsr(n, 0.8, 0, 1, 0.8) * 0.5 * vel


def bubble(vel=1.0):
    """Lavablase: Sinus mit fallender Tonhoehe, ploppt."""
    n = int(0.16 * SR)
    t = np.arange(n) / SR
    f0 = rng.uniform(180, 420)
    f = f0 * (1 + 1.4 * np.exp(-t * 30))
    s = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.clip(t / 0.004, 0, 1) * np.exp(-t * 26)
    return s * 0.09 * vel


# ================================================================ Bausteine

def chord_notes(name, low, high, chords=CHORDS):
    root, tones = chords[name]
    out = []
    for o in range(0, 8):
        for tn in tones:
            m = midi(tn + str(o))
            if low <= m <= high:
                out.append(m)
    return sorted(out)


def root_of(name, octave, chords=CHORDS):
    return midi(chords[name][0] + str(octave))


def melody(lines, start, beat, tr, inst, octave_shift=0, vel=1.0, legato=0.94):
    t = start
    for bar in lines:
        for note, beats in bar:
            if note is not None:
                f = hz(midi(note) + 12 * octave_shift)
                tr.add(inst(f, beats * beat * legato, vel), t)
            t += beats * beat


# ================================================================ Hall + Mix

def impulse_response(seconds=2.6, decay=2.4, dark=3):
    n = int(seconds * SR)
    t = np.arange(n) / SR
    ir = np.zeros((n, 2))
    for ch in range(2):
        noise = rng.randn(n)
        noise = smooth(noise, dark)
        # fruehe Reflexionen
        er = np.zeros(n)
        for d in rng.uniform(0.008, 0.07, 9):
            er[int(d * SR)] += rng.uniform(0.3, 0.8) * (1 if rng.rand() > 0.5 else -1)
        ir[:, ch] = noise * np.exp(-t * decay) * np.clip(t / 0.03, 0, 1) + er * 6
    ir /= np.sqrt((ir ** 2).sum(0, keepdims=True))
    return ir


def mix(T, length, ir_seconds=2.6, ir_decay=2.4, wet_gain=0.55, drive=1.5, peak=0.89):
    """Wie kit.mix, aber mit einstellbarem Raum + Glue-Saettigung. Nahtlose Schleife."""
    total = len(next(iter(T.values())).buf)
    dry = np.zeros((total, 2))
    send = np.zeros(total)
    for tr in T.values():
        l = math.cos((tr.pan + 1) * math.pi / 4)
        r = math.sin((tr.pan + 1) * math.pi / 4)
        dry[:, 0] += tr.buf * l * (1 - tr.wet * 0.5)
        dry[:, 1] += tr.buf * r * (1 - tr.wet * 0.5)
        send += tr.buf * tr.wet
    ir = impulse_response(ir_seconds, ir_decay)
    wet = np.zeros((total + len(ir) - 1, 2))
    for ch in range(2):
        wet[:, ch] = kit.fft_conv(send, ir[:, ch]) * wet_gain
    out = np.zeros_like(wet)
    out[:total] += dry
    out += wet

    if length is None:
        loop = out
    else:
        n = int(round(length * SR))
        loop = out[:n].copy()
        tail = out[n:]
        k = 0
        while k < len(tail):
            m = min(n, len(tail) - k)
            loop[:m] += tail[k:k + m]
            k += m

    # Pegel vor der Saettigung auf einen festen Wert, dann sanft zusammenkleben
    loop = loop / (np.percentile(np.abs(loop), 99.9) + 1e-9)
    loop = np.tanh(loop * drive) / math.tanh(drive)
    loop *= peak / np.abs(loop).max()
    return loop


def write_wav(path, data, label):
    pcm = (np.clip(data, -1, 1) * 32767).astype(np.int16)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    rms = 20 * math.log10(math.sqrt((data ** 2).mean()))
    print("%s  %.3f s  RMS %.1f dBFS" % (label, len(data) / SR, rms))

    meta = path + ".meta"
    if not os.path.exists(meta):
        import uuid
        src = open(os.path.join(ROOT, "Assets", "music", "eis.wav.meta")).read()
        src = src.replace("493e51aac37f4dfdb73fc6f93e6a6a16", uuid.uuid4().hex)
        # erst im Bosskampf gebraucht - im Hintergrund laden, kein Ruckler
        src = src.replace("loadInBackground: 0", "loadInBackground: 1")
        open(meta, "w", newline="\n").write(src)


# ================================================================ Lied

def bass_ostinato(prog, start, tr, style="drive", vel=1.0):
    """Glutbass in Achteln: Grundton, Oktave, Quinte als Auftakt zum naechsten Takt."""
    for b, name in enumerate(prog):
        r = root_of(name, 2)
        if r < midi("E2"):
            r += 12
        t0 = start + b * BAR
        if style == "long":
            tr.add(glut_bass(hz(r), BAR * 0.95, vel * 0.9), t0)
            continue
        if style == "drive":
            pat = [0, 0, 12, 0, 0, 0, 12, 7]
        else:  # "pulse": Luecken fuer Mochi
            pat = [0, None, 0, None, 0, 12, None, 7]
        for i, p in enumerate(pat):
            if p is None:
                continue
            tr.add(glut_bass(hz(r + p), BEAT / 2 * 0.85, vel * (1.0 if i % 2 == 0 else 0.75)), t0 + i * BEAT / 2)


def taiko_groove(bars, start, T, style="groove", vel=1.0):
    for b in range(bars):
        t0 = start + b * BAR
        if style == "none":
            continue
        # Taiko: 1, 2-und, 3, (4-und im Takt 2)
        hits = [(0, 1.0), (1.5, 0.7), (2, 0.95)]
        if style == "drive":
            hits += [(3, 0.8), (3.5, 0.65)]
        elif b % 2 == 1:
            hits += [(3.5, 0.7)]
        for beat, v in hits:
            T["taiko"].add(taiko(vel * v), t0 + beat * BEAT)
        # Snare auf 2 und 4
        for beat in (1, 3):
            T["snare"].add(snare(vel * 0.8), t0 + beat * BEAT)
        # kleine Trommel in Achteln
        for i in range(8):
            T["shime"].add(taiko_small(vel * (1.0 if i % 2 == 0 else 0.55)), t0 + i * BEAT / 2)


def anvils(bars, start, tr, vel=1.0):
    for b in range(bars):
        t0 = start + b * BAR
        tr.add(anvil(vel), t0 + 1 * BEAT)
        tr.add(anvil(vel * 0.85), t0 + 3 * BEAT)
        if b % 4 == 3:
            tr.add(anvil(vel * 0.6), t0 + 3.5 * BEAT)


def spiccato(prog, start, tr, vel=1.0):
    """Sechzehntel auf dem Grundton mit Akkordtoenen (C3-C4) - der Puls des Refrains."""
    for b, name in enumerate(prog):
        notes = chord_notes(name, midi("C3"), midi("C4"))
        r = notes[0]
        pat = [0, 0, 1, 0, 0, 2, 0, 1, 0, 0, 1, 0, 2, 0, 1, 2]
        for i, p in enumerate(pat):
            m = notes[min(p, len(notes) - 1)] if p else r
            tr.add(strings_spic(hz(m), vel * (1.0 if i % 4 == 0 else 0.65)), start + b * BAR + i * BEAT / 4)


def choir_pads(prog, start, tr, vel=1.0, vowel="a"):
    for b, name in enumerate(prog):
        fs = [hz(m) for m in chord_notes(name, midi("G3"), midi("Eb5"))[:4]]
        tr.add(choir(fs, BAR * 0.98, vel, vowel), start + b * BAR)


def lava(bars, start, tr, density=3, vel=1.0):
    for b in range(bars):
        for _ in range(density):
            tr.add(bubble(vel * rng.uniform(0.5, 1.0)), start + b * BAR + rng.uniform(0, BAR))


def compose():
    total_bars = sum(b for _, b in SECTIONS)
    length = total_bars * BAR

    T = {
        "horn": Track(length, pan=0.0, wet=0.3),
        "horn_hi": Track(length, pan=0.15, wet=0.35),
        "trombone": Track(length, pan=-0.25, wet=0.3),
        "choir": Track(length, pan=0.0, wet=0.55),
        "strings": Track(length, pan=-0.2, wet=0.25),
        "bass": Track(length, pan=0.0, wet=0.04),
        "taiko": Track(length, pan=0.0, wet=0.18),
        "shime": Track(length, pan=0.3, wet=0.15),
        "snare": Track(length, pan=0.05, wet=0.25),
        "anvil": Track(length, pan=-0.35, wet=0.3),
        "fx": Track(length, pan=0.0, wet=0.4),
        "lava": Track(length, pan=0.25, wet=0.3),
    }

    t = 0.0
    for name, bars in SECTIONS:
        if name == "intro":
            T["fx"].add(rumble(bars * BAR, 1.0), t)
            bass_ostinato(PROG_I, t, T["bass"], "long", 0.9)
            melody(TEASER, t, BEAT, T["horn"], brass, vel=0.8)
            lava(bars, t, T["lava"], 4)
            for b in range(bars):
                T["anvil"].add(anvil(0.7), t + b * BAR + 3 * BEAT)
                T["taiko"].add(taiko(0.75), t + b * BAR)
            T["fx"].add(riser(BAR, 0.8), t + 3 * BAR)
        elif name in ("A", "A2", "A3"):
            melody(MEL_A, t, BEAT, T["horn"], brass, vel=1.0)
            bass_ostinato(PROG_A, t, T["bass"], "pulse" if name == "A" else "drive")
            taiko_groove(bars, t, T, "groove", 0.9 if name == "A" else 1.0)
            if name != "A":
                anvils(bars, t, T["anvil"], 0.9)
                melody(COUNTER_A, t, BEAT, T["trombone"], brass, vel=0.6)
            if name == "A3":
                melody(MEL_A, t, BEAT, T["horn_hi"], brass, octave_shift=1, vel=0.45)
                choir_pads(PROG_A, t, T["choir"], 0.7, "o")
                spiccato(PROG_A, t, T["strings"], 0.6)
            T["fx"].add(crash(0.8 if name == "A" else 1.0), t)
            T["fx"].add(boom(0.6), t)
        elif name in ("B", "B2"):
            melody(MEL_B, t, BEAT, T["horn"], brass, vel=1.05)
            choir_pads(PROG_B, t, T["choir"], 1.0 if name == "B" else 1.2, "a")
            spiccato(PROG_B, t, T["strings"], 0.85)
            bass_ostinato(PROG_B, t, T["bass"], "drive")
            taiko_groove(bars, t, T, "drive", 1.0)
            anvils(bars, t, T["anvil"], 0.8)
            T["fx"].add(crash(1.0), t)
            T["fx"].add(crash(0.7), t + 4 * BAR)
            T["fx"].add(boom(0.8), t)
        elif name == "C":
            # Mochis Buehne: kein Thema, kein Blech - nur Bass, Taiko, Lava
            bass_ostinato(PROG_C, t, T["bass"], "pulse", 0.85)
            for b in range(bars):
                T["taiko"].add(taiko(0.8), t + b * BAR)
                T["taiko"].add(taiko(0.6), t + b * BAR + 2.5 * BEAT)
                T["anvil"].add(anvil(0.45), t + b * BAR + 3 * BEAT)
            for b in range(4, 8):
                for i in range(8):
                    T["shime"].add(taiko_small(0.5 if i % 2 == 0 else 0.3), t + b * BAR + i * BEAT / 2)
            lava(bars, t, T["lava"], 5, 0.9)
            T["fx"].add(rumble(bars * BAR, 0.6), t)
            T["fx"].add(riser(2 * BAR, 0.7), t + 6 * BAR)
        elif name == "turn":
            bass_ostinato(PROG_T, t, T["bass"], "drive", 0.9)
            melody([[("C5", 2), ("Eb5", 2)], [("C5", 2), ("Ab4", 2)], [("F4", 2), ("Ab4", 2)], [("B4", 4)]],
                   t, BEAT, T["horn"], brass, vel=0.85)
            choir_pads(PROG_T, t, T["choir"], 0.8, "o")
            taiko_groove(3, t, T, "groove", 0.9)
            for i in range(8):                       # Taiko-Wirbel in den Loop-Punkt
                T["taiko"].add(taiko(0.45 + 0.07 * i, 1.0 + 0.04 * i), t + 3 * BAR + i * BEAT / 2)
            T["fx"].add(riser(BAR, 0.6), t + 3 * BAR)
        t += bars * BAR
    return T, length


# Pegel je Spur (dB) - gemessen: ohne das ging das Horn unter Bass und Taiko unter
GAINS = {"horn": 11, "horn_hi": 10, "trombone": 7, "choir": 11, "strings": 7, "bass": -3, "taiko": -3,
         "shime": 5, "snare": 0, "anvil": 13, "fx": -4, "lava": 6}


def apply_gains(T, gains):
    for k, db in gains.items():
        T[k].buf *= 10 ** (db / 20)


def main():
    T, length = compose()
    apply_gains(T, GAINS)
    data = mix(T, length, ir_seconds=2.4, ir_decay=2.6, wet_gain=0.5, drive=1.1)
    write_wav(OUT, data, "vulkan.wav")
    print("  %d Takte, %d BPM, Schleife %.4f s" % (len(song_chords()), BPM, length))
    print("SongSheet:", " ".join(song_chords()))


if __name__ == "__main__":
    main()
