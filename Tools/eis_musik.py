"""
Musik der Eiswelt (Eisgletscher, Map_World5): "Gletscherkrone".

Komplett im Code komponiert und synthetisiert - keine Samples:
  D-Moll, 126 BPM, 56 Takte (~1:47), nahtlose Schleife (Hallfahne wird an
  den Anfang gefaltet).

  Instrumente
    Chip-Floete   Pulswelle 25 %, weich gefiltert, Vibrato - traegt die Melodie
    Glockenspiel  unharmonische Teiltoene, verdoppelt die Melodie eine Oktave hoeher
    Celesta       Sechzehntel-Arpeggios ueber den Akkord - das Funkeln
    Flaeche       verstimmte Saegezaehne, Tiefpass, langsamer Einsatz
    Zupfbass      Dreieck mit Sub-Sinus, Achtel-Muster
    Schlitten-    hohe Teiltoene + Rauschen, Achtel mit Betonung
    gloeckchen
    Schlagwerk    Kick (Sinus-Sweep), Snare (Rauschen + Ton), Hi-Hat, Becken

  Aufbau (Takte)
    Intro    4   Flaeche, Celesta, Gloeckchen - der Schnee faellt
    A        8   Hauptthema, Beat setzt ein
    A'       8   Thema + Gegenstimme + Glockenspiel
    B        8   Refrain in F-Dur, treibender
    C        8   Breakdown: kein Kick, schwebend
    A''      8   Thema voll, Becken
    B'       8   Refrain voll
    Wende    4   Ueberleitung mit Wirbel zurueck zum Intro

  Assets/music/eis.wav   (44.1 kHz, 16 bit, Stereo; .meta wie wald.wav)

Aufruf aus dem Projektordner:  python Tools/eis_musik.py
"""

import math
import os
import wave

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "music", "eis.wav")

SR = 44100
BPM = 126
BEAT = 60.0 / BPM
BAR = 4 * BEAT
rng = np.random.RandomState(1234)

# ================================================================ Noten

NOTE = {"C": 0, "C#": 1, "Db": 1, "D": 2, "D#": 3, "Eb": 3, "E": 4, "F": 5, "F#": 6, "Gb": 6,
        "G": 7, "G#": 8, "Ab": 8, "A": 9, "A#": 10, "Bb": 10, "B": 11}


def midi(name):
    n = name[:-1]
    o = int(name[-1])
    return 12 * (o + 1) + NOTE[n]


def hz(m):
    return 440.0 * 2 ** ((m - 69) / 12)


# Akkorde: Grundton + Akkordtoene (als Namen ohne Oktave)
CHORDS = {
    "Dm": ("D", ["D", "F", "A"]),
    "Bb": ("Bb", ["Bb", "D", "F"]),
    "F": ("F", ["F", "A", "C"]),
    "C": ("C", ["C", "E", "G"]),
    "Gm": ("G", ["G", "Bb", "D"]),
    "A": ("A", ["A", "C#", "E"]),
    "Bbmaj7": ("Bb", ["Bb", "D", "F", "A"]),
    "Am7": ("A", ["A", "C", "E", "G"]),
    "Gm7": ("G", ["G", "Bb", "D", "F"]),
    "Asus": ("A", ["A", "D", "E"]),
}

PROG_A = ["Dm", "Bb", "F", "C", "Dm", "Bb", "Gm", "A"]
PROG_B = ["F", "C", "Dm", "Bb", "F", "C", "Bb", "A"]
PROG_C = ["Bbmaj7", "Am7", "Gm7", "Asus", "Bbmaj7", "Am7", "Gm7", "A"]
PROG_I = ["Dm", "Bb", "Gm", "A"]

# Melodien: (Note oder None, Schlaege)
MEL_A = [
    [("A4", 1), ("D5", 1), ("E5", .5), ("F5", .5), ("E5", .5), ("D5", .5)],
    [("F5", 1.5), ("E5", .5), ("D5", 1), ("Bb4", 1)],
    [("C5", 1), ("A4", .5), ("C5", .5), ("F5", 1), ("G5", .5), ("F5", .5)],
    [("E5", 2), ("C5", 1), ("E5", 1)],
    [("A4", 1), ("D5", 1), ("E5", .5), ("F5", .5), ("G5", .5), ("A5", .5)],
    [("Bb5", 1.5), ("A5", .5), ("G5", 1), ("F5", 1)],
    [("G5", 1), ("F5", .5), ("E5", .5), ("D5", 1), ("E5", 1)],
    [("C#5", 1), ("E5", 1), ("A5", 2)],
]
# Gegenstimme fuer A' (tiefer, ruhiger)
COUNTER_A = [
    [("F4", 2), ("A4", 2)],
    [("D4", 2), ("F4", 2)],
    [("A4", 2), ("C5", 2)],
    [("G4", 2), ("C5", 1), ("G4", 1)],
    [("F4", 2), ("A4", 2)],
    [("F4", 2), ("D4", 2)],
    [("Bb4", 2), ("G4", 2)],
    [("A4", 2), ("C#5", 2)],
]
MEL_B = [
    [("C6", 1.5), ("A5", .5), ("F5", 2)],
    [("G5", 1.5), ("E5", .5), ("C5", 2)],
    [("D5", .5), ("E5", .5), ("F5", .5), ("A5", .5), ("D6", 1), ("C6", 1)],
    [("Bb5", 2), ("A5", 1), ("G5", 1)],
    [("A5", 1.5), ("G5", .5), ("F5", 1), ("A5", 1)],
    [("G5", 1), ("E5", 1), ("C5", 1), ("E5", 1)],
    [("F5", 1), ("G5", 1), ("A5", 1), ("Bb5", 1)],
    [("A5", 3), (None, 1)],
]
MEL_C = [
    [("D5", 4)], [("C5", 4)], [("Bb4", 4)], [("A4", 2), ("C#5", 2)],
    [("F5", 4)], [("E5", 4)], [("D5", 4)], [("C#5", 2), ("E5", 2)],
]

# ================================================================ Klangerzeuger

def env_adsr(n, a, d, s, r, sr=SR):
    t = np.arange(n) / sr
    dur = n / sr
    e = np.ones(n)
    e = np.where(t < a, t / max(a, 1e-4), e)
    dd = (t >= a) & (t < a + d)
    e = np.where(dd, 1 - (1 - s) * (t - a) / max(d, 1e-4), e)
    e = np.where(t >= a + d, s, e)
    rel = t > dur - r
    e = np.where(rel, e * np.clip((dur - t) / max(r, 1e-4), 0, 1), e)
    return e


def osc_pulse(f, n, duty=0.25, vib=0.0):
    t = np.arange(n) / SR
    ph = np.cumsum(np.full(n, f) * (1 + vib * np.sin(2 * np.pi * 5.2 * t) * np.clip((t - 0.15) / 0.2, 0, 1)) / SR)
    frac = ph % 1.0
    return np.where(frac < duty, 1.0, -duty / (1 - duty))


def osc_saw(f, n, detune=0.0):
    t = np.arange(n) / SR
    ph = (f * (1 + detune)) * t + rng.rand()
    return 2 * (ph % 1.0) - 1


def osc_tri(f, n):
    t = np.arange(n) / SR
    ph = (f * t) % 1.0
    return 4 * np.abs(ph - 0.5) - 1


def smooth_kernel(x, k):
    """Billiger Tiefpass: gleitender Mittelwert (vektorisiert)."""
    if k <= 1:
        return x
    c = np.cumsum(np.concatenate([np.zeros(k), x]))
    return (c[k:] - c[:-k]) / k


def flute(f, dur, vel=1.0):
    n = int(dur * SR) + int(0.25 * SR)
    a = osc_pulse(f, n, 0.25, vib=0.006) + 0.5 * osc_pulse(f * 1.003, n, 0.5, vib=0.006)
    a = smooth_kernel(a, max(2, int(SR / (f * 3.2))))
    e = env_adsr(n, 0.02, 0.12, 0.75, 0.22)
    return a * e * 0.22 * vel


def bell(f, dur, vel=1.0):
    n = int(min(dur + 1.6, 2.6) * SR)
    t = np.arange(n) / SR
    s = (np.sin(2 * np.pi * f * t) * np.exp(-t * 2.6)
         + 0.45 * np.sin(2 * np.pi * f * 2.76 * t) * np.exp(-t * 5.0)
         + 0.22 * np.sin(2 * np.pi * f * 5.40 * t) * np.exp(-t * 9.0)
         + 0.05 * np.sin(2 * np.pi * f * 8.93 * t) * np.exp(-t * 14.0))
    s *= np.clip(t / 0.002, 0, 1)
    return s * 0.16 * vel


def celesta(f, dur, vel=1.0):
    n = int(0.9 * SR)
    t = np.arange(n) / SR
    s = (np.sin(2 * np.pi * f * t) + 0.3 * np.sin(2 * np.pi * f * 2 * t) * np.exp(-t * 6)) * np.exp(-t * 4.2)
    s *= np.clip(t / 0.003, 0, 1)
    return s * 0.09 * vel


def pad(freqs, dur, vel=1.0):
    n = int((dur + 1.0) * SR)
    s = np.zeros(n)
    for f in freqs:
        for d in (-0.006, 0.0, 0.007):
            s += osc_saw(f, n, d)
    s = smooth_kernel(s, 28)
    s = smooth_kernel(s, 28)
    e = env_adsr(n, 0.45, 0.3, 0.85, 0.9)
    return s * e * 0.022 * vel / max(1, len(freqs) / 3)


def pluck_bass(f, dur, vel=1.0):
    n = int((dur + 0.08) * SR)
    t = np.arange(n) / SR
    s = osc_tri(f, n) * 0.8 + np.sin(2 * np.pi * f / 2 * t) * 0.5
    s = smooth_kernel(s, 6)
    e = np.exp(-t * 5.5) * 0.7 + 0.3 * np.exp(-t * 1.2)
    e *= env_adsr(n, 0.004, 0, 1, 0.04)
    return s * e * 0.30 * vel


def kick(vel=1.0):
    n = int(0.32 * SR)
    t = np.arange(n) / SR
    f = 45 + 95 * np.exp(-t * 28)
    ph = 2 * np.pi * np.cumsum(f) / SR
    s = np.sin(ph) * np.exp(-t * 9) + 0.15 * rng.randn(n) * np.exp(-t * 120)
    return s * 0.55 * vel


def snare(vel=1.0):
    n = int(0.22 * SR)
    t = np.arange(n) / SR
    noise = rng.randn(n)
    noise = noise - smooth_kernel(noise, 6)              # Hochpass
    s = noise * np.exp(-t * 18) * 0.5 + np.sin(2 * np.pi * 190 * t) * np.exp(-t * 25) * 0.4
    return s * 0.33 * vel


def hat(vel=1.0, open_=False):
    n = int((0.25 if open_ else 0.06) * SR)
    t = np.arange(n) / SR
    noise = rng.randn(n)
    noise = noise - smooth_kernel(noise, 3)
    return noise * np.exp(-t * (14 if open_ else 70)) * 0.065 * vel


def sleigh(vel=1.0):
    """Schlittengloeckchen: ein paar hohe, leicht verstimmte Glocken + Klirrrauschen."""
    n = int(0.18 * SR)
    t = np.arange(n) / SR
    s = np.zeros(n)
    for f in (5200, 6100, 7350, 8900):
        f2 = f * (1 + rng.uniform(-0.02, 0.02))
        s += np.sin(2 * np.pi * f2 * t + rng.rand() * 6) * np.exp(-t * rng.uniform(25, 40))
    noise = rng.randn(n)
    noise = noise - smooth_kernel(noise, 2)
    s += noise * np.exp(-t * 45) * 0.35
    # mehrere Schellen knapp nacheinander
    out = s.copy()
    for d in (0.008, 0.017):
        k = int(d * SR)
        out[k:] += s[:-k] * 0.6
    return out * 0.024 * vel


def crash(vel=1.0):
    n = int(2.2 * SR)
    t = np.arange(n) / SR
    noise = rng.randn(n)
    noise = noise - smooth_kernel(noise, 3)
    return noise * np.exp(-t * 1.8) * 0.10 * vel


def roll(dur, vel=1.0):
    """Snare-Wirbel, anschwellend (Ueberleitung)."""
    n = int(dur * SR)
    out = np.zeros(n)
    hits = int(dur / (BEAT / 4))
    for i in range(hits):
        s = snare(0.35 + 0.65 * i / hits)
        k = int(i * BEAT / 4 * SR)
        m = min(len(s), n - k)
        out[k:k + m] += s[:m] * vel
    return out


# ================================================================ Mischpult

class Track:
    def __init__(self, seconds, pan=0.0, wet=0.0):
        self.buf = np.zeros(int(seconds * SR) + SR * 4)
        self.pan = pan
        self.wet = wet

    def add(self, sig, at):
        k = int(at * SR)
        m = min(len(sig), len(self.buf) - k)
        if m > 0:
            self.buf[k:k + m] += sig[:m]


def chord_freqs(name, octave=4):
    root, tones = CHORDS[name]
    base = midi(root + str(octave - 1))
    out = []
    for i, tname in enumerate(tones):
        m = midi(tname + str(octave))
        while m < midi("A3"):
            m += 12
        while m > midi("E5"):
            m -= 12
        out.append(hz(m))
    return out, hz(base)


def arpeggio(name, start, bars, tr, vel=1.0, up_oct=0):
    root, tones = CHORDS[name]
    notes = []
    for o in (5 + up_oct, 6 + up_oct):
        for tn in tones:
            notes.append(midi(tn + str(o)))
    notes = sorted(notes)
    seq = notes + notes[-2:0:-1]
    steps = int(bars * 16)
    for i in range(steps):
        f = hz(seq[i % len(seq)])
        tr.add(celesta(f, BEAT / 4, vel * (1.0 if i % 4 == 0 else 0.7)), start + i * BEAT / 4)


def melody(lines, start, tr, inst, octave_shift=0, vel=1.0):
    t = start
    for bar in lines:
        for note, beats in bar:
            if note is not None:
                f = hz(midi(note) + 12 * octave_shift)
                tr.add(inst(f, beats * BEAT * 0.95, vel), t)
            t += beats * BEAT


def bassline(prog, start, tr, style="pulse", vel=1.0):
    for b, name in enumerate(prog):
        root, _ = CHORDS[name]
        f = hz(midi(root + "2"))
        t0 = start + b * BAR
        if style == "long":
            tr.add(pluck_bass(f, BAR * 0.9, vel * 0.9), t0)
            continue
        pattern = [1, 0, 1, 1, 0, 1, 1, 2] if style == "pulse" else [1, 1, 2, 1, 1, 1, 2, 1]
        for i, p in enumerate(pattern):
            if p == 0:
                continue
            ff = f * (2 if p == 2 else 1)
            tr.add(pluck_bass(ff, BEAT / 2 * 0.9, vel * (1.0 if i % 2 == 0 else 0.8)), t0 + i * BEAT / 2)


def pads(prog, start, tr, vel=1.0):
    for b, name in enumerate(prog):
        fs, _ = chord_freqs(name)
        tr.add(pad(fs, BAR, vel), start + b * BAR)


def drums(bars, start, kd, sn, hh, sl, style="groove", vel=1.0):
    for b in range(bars):
        t0 = start + b * BAR
        for i in range(8):
            t = t0 + i * BEAT / 2
            if style in ("groove", "drive"):
                if i in (0, 4) or (style == "drive" and i in (2, 6)):
                    kd.add(kick(vel), t)
                if style == "groove" and i == 5 and b % 2 == 1:
                    kd.add(kick(vel * 0.7), t)
                if i in (2, 6):
                    sn.add(snare(vel), t)
                hh.add(hat(vel * (1.0 if i % 2 else 0.6), open_=(i == 7 and b % 4 == 3)), t)
            sl.add(sleigh(vel * (1.0 if i % 2 == 0 else 0.6)), t + (0.012 if i % 2 else 0))


# ================================================================ Lied

def compose():
    sections = [("intro", 4), ("A", 8), ("A2", 8), ("B", 8), ("C", 8), ("A3", 8), ("B2", 8), ("turn", 4)]
    total_bars = sum(b for _, b in sections)
    length = total_bars * BAR

    T = {
        "lead": Track(length, pan=0.0, wet=0.28),
        "bell": Track(length, pan=0.25, wet=0.45),
        "counter": Track(length, pan=-0.3, wet=0.35),
        "arp": Track(length, pan=-0.2, wet=0.5),
        "pad": Track(length, pan=0.0, wet=0.6),
        "bass": Track(length, pan=0.0, wet=0.05),
        "kick": Track(length, pan=0.0, wet=0.04),
        "snare": Track(length, pan=0.05, wet=0.25),
        "hat": Track(length, pan=0.3, wet=0.1),
        "sleigh": Track(length, pan=-0.35, wet=0.3),
        "fx": Track(length, pan=0.0, wet=0.4),
    }

    t = 0.0
    for name, bars in sections:
        if name == "intro":
            pads(PROG_I, t, T["pad"], 1.5)
            for b, ch in enumerate(PROG_I):
                arpeggio(ch, t + b * BAR, 1, T["arp"], 1.1)
            drums(bars, t, T["kick"], T["snare"], T["hat"], T["sleigh"], style="none", vel=0.9)
            bassline(PROG_I, t, T["bass"], "long", 1.0)
            # Vorgeschmack auf das Thema, hoch im Glockenspiel
            melody([[("A5", 1), ("D6", 1), ("E6", 1), ("F6", 1)], [("E6", 4)],
                    [("D6", 1), ("Bb5", 1), ("G5", 2)], [("A5", 2), ("C#6", 2)]], t, T["bell"], bell, vel=0.7)
        elif name in ("A", "A2", "A3"):
            pads(PROG_A, t, T["pad"], 0.8)
            bassline(PROG_A, t, T["bass"], "pulse")
            melody(MEL_A, t, T["lead"], flute, vel=1.0)
            for b, ch in enumerate(PROG_A):
                arpeggio(ch, t + b * BAR, 1, T["arp"], 0.55 if name == "A" else 0.7)
            drums(bars, t, T["kick"], T["snare"], T["hat"], T["sleigh"], "groove", 0.9 if name == "A" else 1.0)
            if name != "A":
                melody(MEL_A, t, T["bell"], bell, octave_shift=1, vel=0.8)
                melody(COUNTER_A, t, T["counter"], flute, octave_shift=0, vel=0.55)
            if name == "A3":
                T["fx"].add(crash(1.0), t)
        elif name in ("B", "B2"):
            pads(PROG_B, t, T["pad"], 1.0)
            bassline(PROG_B, t, T["bass"], "drive")
            melody(MEL_B, t, T["lead"], flute, vel=1.05)
            melody(MEL_B, t, T["bell"], bell, octave_shift=0, vel=0.7)
            for b, ch in enumerate(PROG_B):
                arpeggio(ch, t + b * BAR, 1, T["arp"], 0.75)
            drums(bars, t, T["kick"], T["snare"], T["hat"], T["sleigh"], "drive", 1.0)
            T["fx"].add(crash(0.9 if name == "B" else 1.1), t)
            T["fx"].add(crash(0.6), t + 4 * BAR)
        elif name == "C":
            pads(PROG_C, t, T["pad"], 1.1)
            bassline(PROG_C, t, T["bass"], "long", 0.8)
            melody(MEL_C, t, T["bell"], bell, octave_shift=0, vel=1.0)
            melody(MEL_C, t, T["counter"], flute, octave_shift=-1, vel=0.45)
            for b, ch in enumerate(PROG_C):
                arpeggio(ch, t + b * BAR, 1, T["arp"], 0.8, up_oct=0)
            drums(bars, t, T["kick"], T["snare"], T["hat"], T["sleigh"], "none", 0.7)
            # in der zweiten Haelfte kommt der Beat leise zurueck
            drums(2, t + 6 * BAR, T["kick"], T["snare"], T["hat"], T["sleigh"], "groove", 0.55)
        elif name == "turn":
            pads(PROG_I, t, T["pad"], 0.9)
            bassline(PROG_I, t, T["bass"], "pulse", 0.9)
            for b, ch in enumerate(PROG_I):
                arpeggio(ch, t + b * BAR, 1, T["arp"], 0.7)
            melody([[("D5", 2), ("F5", 2)], [("F5", 2), ("D5", 2)], [("G5", 2), ("Bb5", 2)], [("A5", 2), ("C#6", 2)]],
                   t, T["bell"], bell, vel=0.9)
            drums(3, t, T["kick"], T["snare"], T["hat"], T["sleigh"], "groove", 0.85)
            T["snare"].add(roll(BAR, 0.9), t + 3 * BAR)
        t += bars * BAR
    return T, length


# ================================================================ Hall + Mix

def impulse_response(seconds=2.4):
    n = int(seconds * SR)
    t = np.arange(n) / SR
    ir = np.zeros((n, 2))
    for ch in range(2):
        noise = rng.randn(n)
        noise = smooth_kernel(noise, 3)                   # etwas dumpfer
        ir[:, ch] = noise * np.exp(-t * 2.6) * np.clip(t / 0.02, 0, 1)
    ir /= np.sqrt((ir ** 2).sum(0, keepdims=True))
    return ir


def fft_conv(x, h):
    n = len(x) + len(h) - 1
    size = 1 << (n - 1).bit_length()
    return np.fft.irfft(np.fft.rfft(x, size) * np.fft.rfft(h, size), size)[:n]


def mix(T, length):
    total = len(next(iter(T.values())).buf)
    dry = np.zeros((total, 2))
    send = np.zeros(total)
    for tr in T.values():
        l = math.cos((tr.pan + 1) * math.pi / 4)
        r = math.sin((tr.pan + 1) * math.pi / 4)
        dry[:, 0] += tr.buf * l * (1 - tr.wet * 0.5)
        dry[:, 1] += tr.buf * r * (1 - tr.wet * 0.5)
        send += tr.buf * tr.wet
    ir = impulse_response()
    wet = np.zeros((total + len(ir) - 1, 2))
    for ch in range(2):
        wet[:, ch] = fft_conv(send, ir[:, ch]) * 0.55
    out = np.zeros_like(wet)
    out[:total] += dry
    out += wet

    # Nahtlose Schleife: alles nach dem Ende (Hallfahne, ausklingende Noten) nach vorn falten
    n = int(round(length * SR))
    loop = out[:n].copy()
    tail = out[n:]
    k = 0
    while k < len(tail):
        m = min(n, len(tail) - k)
        loop[:m] += tail[k:k + m]
        k += m

    # sanfte Saettigung + Pegel
    loop = np.tanh(loop * 1.4) / 1.4
    loop *= 0.89 / np.abs(loop).max()
    return loop


def main():
    T, length = compose()
    data = mix(T, length)
    pcm = (np.clip(data, -1, 1) * 32767).astype(np.int16)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with wave.open(OUT, "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    rms = 20 * math.log10(math.sqrt((data ** 2).mean()))
    print("eis.wav  %.1f s  RMS %.1f dBFS" % (len(data) / SR, rms))

    meta = OUT + ".meta"
    if not os.path.exists(meta):
        import uuid
        src = open(os.path.join(os.path.dirname(OUT), "wald.wav.meta")).read()
        open(meta, "w", newline="\n").write(src.replace("56b195d28eea480ba70e21313510ac90", uuid.uuid4().hex))


if __name__ == "__main__":
    main()
