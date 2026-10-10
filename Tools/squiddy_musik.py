"""
Boss-Thema fuer Phase 2 des Geisterwald-Endbosses: "Squiddys Gelee-Gloria".

Setzt im Moment ein, in dem sich der Heiligenschein auf Squiddy senkt
(EnemySquiddy.Halo -> AudioController.SwapRunMusic). Das Thema des
Geisterwalds (Tools/geist_musik.py) kippt von e-Moll nach E-DUR - das Licht
geht an -, wird schneller und bekommt Glanz: ein quietschiger Gelee-Synth
singt die Melodie, der Engelschor "Aaah" traegt den Refrain, ein Glockenspiel
klingelt wie der Heiligenschein, und Squiddys Strom knistert als Schlagwerk.

Gebaut fuer die Mochi-Melodie (SongSheet.cs, Eintrag "squiddy"):
  - E-Dur, 132 BPM, gerade Achtel, erste Eins genau bei 0
  - Schleife = genau 40 Takte, Hallfahne nach vorn gefaltet
  - PROG_* unten 1:1 wie in SongSheet.cs
  - Mochi-Platz (G4-D6): der Gelee-Synth spielt H3-E5, Teil C ist Mochis Buehne

  Aufbau (Takte)
    Intro    4   Chor + Glockenspiel-Arpeggio, Bass setzt ein, Wirbel
    A        8   Thema im Gelee-Synth, Huepf-Bass, voller Beat
    A'       8   + Theremin-Echo, Cembalo-Glitzer, Chor-Teppich
    B        8   Refrain: Orgel + Engelschor, Strom-Knistern auf 2 und 4
    C        8   Breakdown halbes Tempo - Mochis Buehne
    Wende    4   Anlauf, Glissando hoch, zurueck zum Intro

  Assets/music/squiddy.wav  (44.1 kHz, 16 bit, Stereo; .meta wie wald.wav)

Aufruf aus dem Projektordner:  python Tools/squiddy_musik.py
"""

import math
import os
import sys
import wave

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import eis_musik as kit  # noqa: E402
import geist_musik as geist  # noqa: E402  (Theremin, Cembalo, Chor, Orgel, Klapper)

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "music", "squiddy.wav")

SR = kit.SR
BPM = 132
BEAT = 60.0 / BPM
BAR = 4 * BEAT
rng = np.random.RandomState(39)

midi = kit.midi
hz = kit.hz

CHORDS = {
    "E": ("E", ["E", "G#", "B"]),
    "A": ("A", ["A", "C#", "E"]),
    "B": ("B", ["B", "D#", "F#"]),
    "C": ("C", ["C", "E", "G"]),
    "D": ("D", ["D", "F#", "A"]),
    "G#m": ("G#", ["G#", "B", "D#"]),
    "C#m": ("C#", ["C#", "E", "G#"]),
}
geist.CHORDS.update(CHORDS)          # chord_notes() aus geist_musik liest dort

PROG_I = ["E", "E", "C", "B"]
PROG_A = ["E", "C", "A", "B", "E", "C", "D", "B"]
PROG_B = ["A", "B", "G#m", "C#m", "A", "B", "E", "E"]
PROG_C = ["E", "A", "E", "B", "E", "A", "C", "B"]
PROG_T = ["A", "C", "B", "B"]

SECTIONS = [("intro", 4), ("A", 8), ("A2", 8), ("B", 8), ("C", 8), ("turn", 4)]


def song_chords():
    progs = {"intro": PROG_I, "A": PROG_A, "A2": PROG_A, "B": PROG_B, "C": PROG_C, "turn": PROG_T}
    return [c for name, _ in SECTIONS for c in progs[name]]


# Das Geisterwald-Thema in Dur (notiert eine Oktave hoeher, der Synth spielt eine tiefer)
MEL_A = [
    [("B4", 1), ("E5", 1), ("D#5", .5), ("E5", .5), ("G#5", 1)],
    [("E5", 1.5), ("D5", .5), ("C5", 1), ("G4", 1)],
    [("A4", .5), ("B4", .5), ("C#5", 1), ("E5", 1), ("D5", .5), ("C#5", .5)],
    [("B4", 2), ("D#5", 1), ("F#5", 1)],
    [("G#5", 1), ("F#5", .5), ("E5", .5), ("B4", 1), ("E5", 1)],
    [("G5", 1.5), ("A5", .5), ("G5", 1), ("E5", 1)],
    [("F#5", 1), ("E5", .5), ("D5", .5), ("A4", 1), ("D5", 1)],
    [("D#5", 1), ("F#5", 1), ("B5", 2)],
]
MEL_B = [
    [("A5", 1), ("E5", 1), ("C#5", 1), ("E5", 1)],
    [("F#5", 1.5), ("E5", .5), ("D#5", 2)],
    [("B5", 1), ("G#5", .5), ("F#5", .5), ("D#5", 1), ("G#5", 1)],
    [("E5", 3), (None, 1)],
    [("C#6", 1), ("B5", .5), ("A5", .5), ("E5", 1), ("A5", 1)],
    [("F#5", 1), ("G#5", 1), ("A5", 1), ("D#5", 1)],
    [("E5", 1), ("B4", 1), ("E5", 1), ("G#5", 1)],
    [("E6", 2), (None, 2)],
]


# ================================================================ Klangerzeuger

def jelly_lead(f, dur, vel=1.0, f_from=None):
    """Quietschiger Gelee-Synth: Pulswelle mit wanderndem Tastgrad, Gleiten, Wabbel-Vibrato."""
    n = int((dur + 0.2) * SR)
    t = np.arange(n) / SR
    glide = np.full(n, f)
    if f_from is not None:
        g = np.clip(t / 0.05, 0, 1)
        glide = f_from * (f / f_from) ** g
    vib = 1 + 0.010 * np.sin(2 * np.pi * 6.4 * t) * np.clip((t - 0.1) / 0.2, 0, 1)
    ph = np.cumsum(glide * vib) / SR
    duty = 0.32 + 0.14 * np.sin(2 * np.pi * 1.8 * t)
    frac = ph % 1.0
    s = np.where(frac < duty, 1.0, -duty / (1 - duty))
    s = kit.smooth_kernel(s, max(2, int(SR / (f * 4.0))))
    # "Blubb" am Tonanfang: kurzes Hochziehen der Tonhoehe
    s += 0.25 * np.sin(2 * np.pi * np.cumsum(glide * (1 + 0.5 * np.exp(-t * 60))) / SR) * np.exp(-t * 20)
    e = kit.env_adsr(n, 0.012, 0.1, 0.7, 0.12)
    return s * e * 0.13 * vel


def hop_bass(f, vel=1.0, length=None):
    """Huepf-Bass: weicher Saegezahn, schnell abklingend, mit Sub."""
    length = length or BEAT * 0.48
    n = int((length + 0.05) * SR)
    t = np.arange(n) / SR
    s = kit.osc_saw(f, n) * 0.6 + np.sin(2 * np.pi * f * t) * 0.6 + np.sin(np.pi * f * t) * 0.35
    s = kit.smooth_kernel(s, 10)
    e = np.exp(-t * 7) * 0.8 + 0.2
    e *= kit.env_adsr(n, 0.003, 0, 1, 0.04)
    return s * e * 0.24 * vel


def angel_choir(freqs, dur, vel=1.0):
    """'Aaah': wie der Geisterchor, aber mit offenem A-Formant (~730 / ~1100 Hz)."""
    n = int((dur + 0.8) * SR)
    s = np.zeros(n)
    for f in freqs:
        for d in (-0.005, 0.0, 0.006):
            s += kit.osc_saw(f, n, d)
    spec = np.fft.rfft(s)
    fr = np.fft.rfftfreq(n, 1 / SR)
    shape = (np.exp(-((fr - 730) / 150) ** 2) + 0.6 * np.exp(-((fr - 1100) / 200) ** 2)
             + 0.25 * np.exp(-((fr - 2600) / 400) ** 2) + 0.3 * np.exp(-((fr - 300) / 120) ** 2))
    s = np.fft.irfft(spec * shape, n)
    t = np.arange(n) / SR
    e = kit.env_adsr(n, 0.35, 0.3, 0.85, 0.7)
    return s * e * 0.075 * vel / max(1, len(freqs) / 3)


def glock(f, vel=1.0):
    """Glockenspiel - der Heiligenschein klingelt."""
    n = int(1.2 * SR)
    t = np.arange(n) / SR
    s = (np.sin(2 * np.pi * f * t) * np.exp(-t * 3.2) + 0.35 * np.sin(2 * np.pi * f * 2.76 * t) * np.exp(-t * 8)
         + 0.12 * np.sin(2 * np.pi * f * 5.4 * t) * np.exp(-t * 15))
    return s * np.clip(t / 0.002, 0, 1) * 0.07 * vel


def zap(vel=1.0):
    """Strom-Knistern als Snare: Rauschstoss mit Knacksern + kurzer Summton."""
    n = int(0.16 * SR)
    t = np.arange(n) / SR
    noise = rng.randn(n)
    noise = noise - kit.smooth_kernel(noise, 3)
    crack = np.where(rng.rand(n) < 0.03, rng.randn(n) * 2, 0) * np.exp(-t * 25)
    buzz = np.sign(np.sin(2 * np.pi * 120 * t)) * np.exp(-t * 30) * 0.3
    return (noise * np.exp(-t * 35) * 0.45 + crack * 0.25 + buzz) * 0.3 * vel


def gliss(dur, vel=1.0):
    """Glissando hoch (Wende): Gelee-Synth rutscht zwei Oktaven."""
    n = int(dur * SR)
    t = np.arange(n) / SR
    f = hz(midi("B3")) * 4 ** (t / dur)
    ph = np.cumsum(f) / SR
    s = np.where(ph % 1.0 < 0.35, 1.0, -0.35 / 0.65)
    s = kit.smooth_kernel(s, 6)
    return s * np.clip(t / dur, 0, 1) ** 1.5 * 0.09 * vel


# ================================================================ Bausteine

Track = kit.Track


def lead_line(lines, start, tr, inst, vel=1.0, shift=-12):
    t = start
    prev = None
    for bar in lines:
        for note, beats in bar:
            if note is not None:
                f = hz(midi(note) + shift)
                tr.add(inst(f, beats * BEAT * 0.92, vel, prev), t)
                prev = f
            else:
                prev = None
            t += beats * BEAT


def bassline(prog, start, tr, vel=1.0, style="hop"):
    for b, name in enumerate(prog):
        root = midi(CHORDS[name][0] + "2")
        if root > midi("A2"):
            root -= 12
        t0 = start + b * BAR
        if style == "long":
            tr.add(hop_bass(hz(root), vel, BEAT * 1.8), t0)
            tr.add(hop_bass(hz(root + 7), vel * 0.7, BEAT * 1.6), t0 + 2 * BEAT)
            continue
        # Achtel: Grundton - Oktave huepfen, auf der 4-und Quinte
        for i in range(8):
            m = root if i % 2 == 0 else root + 12
            if i == 7:
                m = root + 7
            tr.add(hop_bass(hz(m), vel * (1.0 if i % 2 == 0 else 0.65)), t0 + i * BEAT / 2)


def glock_arp(prog, start, tr, vel=1.0):
    for b, name in enumerate(prog):
        notes = geist.chord_notes(name, midi("E5"), midi("E7"))
        for i in range(4):
            m = notes[(i * 2 + b) % len(notes)]
            tr.add(glock(hz(m), vel * (1.0 if i == 0 else 0.7)), start + b * BAR + i * BEAT)


def sparkle(prog, start, tr, vel=1.0):
    """Cembalo-Glitzer in Sechzehnteln, nur auf Zaehlzeit 2 und 4."""
    for b, name in enumerate(prog):
        notes = geist.chord_notes(name, midi("E3"), midi("B4"))
        for beat in (1, 3):
            for k in range(4):
                m = notes[(k + beat) % len(notes)]
                tr.add(geist.harpsichord(hz(m), vel * (0.9 - 0.15 * k), 0.22), start + b * BAR + (beat + k / 4) * BEAT)


def choir_pads(prog, start, tr, vel=1.0):
    for b, name in enumerate(prog):
        fs = [hz(m) for m in geist.chord_notes(name, midi("B2"), midi("G#4"))[:3]]
        tr.add(angel_choir(fs, BAR, vel), start + b * BAR)


def organ_pads(prog, start, tr, vel=1.0):
    for b, name in enumerate(prog):
        fs = [hz(m) for m in geist.chord_notes(name, midi("E3"), midi("B4"))[:3]]
        tr.add(geist.organ(fs, BAR, vel), start + b * BAR)


def drums(bars, start, T, style="drive", vel=1.0):
    for b in range(bars):
        t0 = start + b * BAR
        for i in range(8):
            t = t0 + i * BEAT / 2
            if style == "drive":
                if i in (0, 3, 4):
                    T["kick"].add(kit.kick(vel * (1.0 if i != 3 else 0.6)), t)
                if i in (2, 6):
                    T["zap"].add(zap(vel), t)
                T["hat"].add(kit.hat(vel * (0.9 if i % 2 else 0.5)), t)
            elif style == "half":
                if i == 0:
                    T["kick"].add(kit.kick(vel * 0.8), t)
                if i == 4:
                    T["zap"].add(zap(vel * 0.7), t)
                if i % 2 == 1:
                    T["hat"].add(kit.hat(vel * 0.5), t)
        T["bones"].add(geist.bones(vel * 0.8), t0 + 3.5 * BEAT)


def roll(dur, vel=1.0):
    n = int(dur * SR)
    out = np.zeros(n)
    hits = int(dur / (BEAT / 4))
    for i in range(hits):
        s = zap(0.3 + 0.7 * i / hits)
        k = int(i * BEAT / 4 * SR)
        m = min(len(s), n - k)
        out[k:k + m] += s[:m] * vel
    return out


# ================================================================ Lied

def compose():
    total_bars = sum(b for _, b in SECTIONS)
    length = total_bars * BAR
    T = {
        "lead": Track(length, pan=0.0, wet=0.28),
        "echo": Track(length, pan=0.25, wet=0.5),
        "harp": Track(length, pan=-0.3, wet=0.25),
        "organ": Track(length, pan=0.15, wet=0.4),
        "choir": Track(length, pan=-0.1, wet=0.6),
        "glock": Track(length, pan=0.35, wet=0.5),
        "bass": Track(length, pan=0.0, wet=0.06),
        "kick": Track(length, pan=0.0, wet=0.04),
        "zap": Track(length, pan=0.1, wet=0.25),
        "hat": Track(length, pan=0.3, wet=0.1),
        "bones": Track(length, pan=-0.35, wet=0.3),
        "bell": Track(length, pan=0.0, wet=0.5),
    }
    t = 0.0
    for name, bars in SECTIONS:
        if name == "intro":
            choir_pads(PROG_I, t, T["choir"], 1.0)
            glock_arp(PROG_I, t, T["glock"], 1.0)
            T["bell"].add(geist.midnight_bell(0.8), t)
            bassline(PROG_I[2:], t + 2 * BAR, T["bass"], 0.8, "long")
            T["zap"].add(roll(BAR, 0.8), t + 3 * BAR)
        elif name in ("A", "A2"):
            lead_line(MEL_A, t, T["lead"], jelly_lead, 1.0)
            bassline(PROG_A, t, T["bass"])
            drums(bars, t, T, "drive", 1.0)
            if name == "A2":
                # Theremin singt die Melodie einen Achtel spaeter als Echo nach
                lead_line(MEL_A, t + BEAT / 2, T["echo"], geist.theremin, 0.55, shift=0)
                sparkle(PROG_A, t, T["harp"], 0.7)
                choir_pads(PROG_A, t, T["choir"], 0.6)
            T["glock"].add(glock(hz(midi("E6")), 1.0), t)
        elif name == "B":
            lead_line(MEL_B, t, T["lead"], jelly_lead, 1.05)
            organ_pads(PROG_B, t, T["organ"], 0.9)
            choir_pads(PROG_B, t, T["choir"], 1.1)
            bassline(PROG_B, t, T["bass"], 1.0)
            glock_arp(PROG_B, t, T["glock"], 0.8)
            drums(bars, t, T, "drive", 1.05)
            T["bell"].add(geist.midnight_bell(0.9), t)
        elif name == "C":
            bassline(PROG_C, t, T["bass"], 0.8, "long")
            choir_pads(PROG_C, t, T["choir"], 0.55)
            sparkle(PROG_C, t, T["harp"], 0.45)
            drums(bars, t, T, "half", 0.8)
        elif name == "turn":
            bassline(PROG_T, t, T["bass"], 0.9)
            drums(3, t, T, "drive", 0.9)
            T["lead"].add(gliss(2 * BAR, 1.0), t + 2 * BAR)
            T["zap"].add(roll(BAR, 1.0), t + 3 * BAR)
            organ_pads(PROG_T, t, T["organ"], 0.7)
        t += bars * BAR
    return T, length


def main():
    T, length = compose()
    data = kit.mix(T, length)
    pcm = (np.clip(data, -1, 1) * 32767).astype(np.int16)
    with wave.open(OUT, "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    rms = 20 * math.log10(math.sqrt((data ** 2).mean()))
    print("squiddy.wav  %.1f s  RMS %.1f dBFS  (%d Takte, %d BPM)" % (len(data) / SR, rms, len(song_chords()), BPM))
    print("SongSheet:", " ".join(song_chords()))
    meta = OUT + ".meta"
    if not os.path.exists(meta):
        import uuid
        src = open(os.path.join(os.path.dirname(OUT), "wald.wav.meta")).read()
        open(meta, "w", newline="\n").write(src.replace("56b195d28eea480ba70e21313510ac90", uuid.uuid4().hex))


if __name__ == "__main__":
    main()
