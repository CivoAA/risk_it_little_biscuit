"""
Musik des Lebkuchen-Geisterwalds (Map_World6): "Mitternacht im Lebkuchenwald".

Komplett im Code komponiert und synthetisiert, wie Tools/eis_musik.py (dessen
Klangerzeuger und Mischpult hier mitbenutzt werden). Gruselig, aber suess:
auf Zehenspitzen schleichender Bass, ein Theremin singt das Thema, ein
Cembalo tippelt, um Mitternacht schlaegt die Glocke, Eulen rufen.

Gebaut fuer die Mochi-Melodie (SongSheet.cs, Eintrag "geist"):
  - e-Moll, 116 BPM, gerade Achtel (kein Swing)
  - erste Eins genau bei 0, Schleife = genau 56 Takte, Hallfahne nach vorn
    gefaltet - der Takt laeuft ueber den Loop-Punkt einfach weiter
  - die Akkorde unten (PROG_*) sind 1:1 die in SongSheet.cs - wer hier etwas
    aendert, aendert es dort mit
  - Platz fuer Mochi (G4-D6): das Theremin singt eine Etage tiefer (H2-A4),
    das Cembalo bleibt unter H4, Teil C ist Mochis Buehne.

  Instrumente
    Theremin      Sinus mit breitem Vibrato und Gleiten zwischen den Toenen - das Thema
    Cembalo       hell gezupft, kurz - tippelnde Achtel
    Schleichbass  Pizzicato-Viertel, laeuft chromatisch zum naechsten Grundton
    Orgel         Zugriegel-Sinus mit Tremolo, im Refrain
    Geisterchor   "Uuh" - Saegezahn durch zwei Formanten, im Refrain
    Spieluhr      Celesta ganz oben, selten
    Glocke        Mitternachtsglocke zu Teilanfaengen
    Schlagwerk    weicher Kick, Fingerschnipsen auf 2 und 4, Knochen-Klappern
                  (Holzblock) und Rassel
    Wald          Wind und Eulenrufe im Intro und im Breakdown

  Aufbau (Takte)
    Intro    4   Wind, Cembalo, Eule - der Wald schlaeft (nicht)
    A        8   Thema im Theremin, Bass schleicht los
    A'       8   Thema + Cembalo-Gegenstimme, voller Beat
    B        8   Refrain: Orgel + Geisterchor
    C        8   Breakdown - Mochis Buehne
    A''      8   Thema voll
    B'       8   Refrain voll
    Wende    4   Theremin gleitet hoch, Klapper-Wirbel zurueck zum Intro

  Assets/music/geist.wav   (44.1 kHz, 16 bit, Stereo; .meta wie wald.wav)

Aufruf aus dem Projektordner:  python Tools/geist_musik.py
"""

import math
import os
import sys
import wave

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import eis_musik as kit  # noqa: E402  (Klangerzeuger, Hall, Mix)

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "music", "geist.wav")

SR = kit.SR
BPM = 116
BEAT = 60.0 / BPM
BAR = 4 * BEAT
rng = np.random.RandomState(1031)

midi = kit.midi
hz = kit.hz

# ================================================================ Akkorde

CHORDS = {
    "Em": ("E", ["E", "G", "B"]),
    "C": ("C", ["C", "E", "G"]),
    "Am": ("A", ["A", "C", "E"]),
    "B": ("B", ["B", "D#", "F#"]),
    "D": ("D", ["D", "F#", "A"]),
    "G": ("G", ["G", "B", "D"]),
}

PROG_I = ["Em", "Em", "C", "B"]
PROG_A = ["Em", "C", "Am", "B", "Em", "C", "D", "B"]
PROG_B = ["C", "D", "G", "Em", "Am", "B", "Em", "Em"]
PROG_C = ["Em", "Am", "Em", "B", "Em", "Am", "C", "B"]
PROG_T = ["Am", "C", "B", "B"]

SECTIONS = [("intro", 4), ("A", 8), ("A2", 8), ("B", 8), ("C", 8), ("A3", 8), ("B2", 8), ("turn", 4)]


def song_chords():
    """Akkord je Takt fuer die ganze Schleife - so steht es in SongSheet.cs."""
    progs = {"intro": PROG_I, "A": PROG_A, "A2": PROG_A, "A3": PROG_A, "B": PROG_B, "B2": PROG_B,
             "C": PROG_C, "turn": PROG_T}
    return [c for name, _ in SECTIONS for c in progs[name]]


# Melodien (notiert eine Oktave hoeher, das Theremin spielt sie eine tiefer)
MEL_A = [
    [("B4", 1), ("E5", 1), ("D#5", .5), ("E5", .5), ("G5", 1)],
    [("E5", 1.5), ("D5", .5), ("C5", 1), ("G4", 1)],
    [("A4", .5), ("B4", .5), ("C5", 1), ("E5", 1), ("D5", .5), ("C5", .5)],
    [("B4", 2), ("D#5", 1), ("F#5", 1)],
    [("G5", 1), ("F#5", .5), ("E5", .5), ("B4", 1), ("E5", 1)],
    [("G5", 1.5), ("A5", .5), ("G5", 1), ("E5", 1)],
    [("F#5", 1), ("E5", .5), ("D5", .5), ("A4", 1), ("D5", 1)],
    [("D#5", 1), ("F#5", 1), ("B4", 2)],
]
MEL_B = [
    [("G5", 1), ("E5", 1), ("C5", 1), ("E5", 1)],
    [("F#5", 1.5), ("E5", .5), ("D5", 2)],
    [("B5", 1), ("A5", .5), ("G5", .5), ("D5", 1), ("G5", 1)],
    [("E5", 3), (None, 1)],
    [("C6", 1), ("B5", .5), ("A5", .5), ("E5", 1), ("A5", 1)],
    [("F#5", 1), ("G5", 1), ("A5", 1), ("D#5", 1)],
    [("E5", 1), ("B4", 1), ("E5", 1), ("G5", 1)],
    [("E5", 2), (None, 2)],
]
# Spieluhr-Streusel im Intro, ganz oben (ueber Mochi)
TEASER = [[("E6", 1), ("G6", 1), ("B6", 2)], [("D#6", 4)], [("E6", 1), ("G6", 1), ("C7", 2)], [("B6", 2), ("F#6", 2)]]


# ================================================================ Klangerzeuger

def theremin(f, dur, vel=1.0, f_from=None):
    """Sinus + etwas zweite Harmonische, breites Vibrato, gleitet vom vorigen Ton heran."""
    n = int((dur + 0.25) * SR)
    t = np.arange(n) / SR
    glide = np.ones(n) * f
    if f_from is not None:
        g = np.clip(t / 0.09, 0, 1)
        glide = f_from * (f / f_from) ** g
    depth = 0.012 * np.clip((t - 0.12) / 0.3, 0, 1) + 0.003
    vib = 1 + depth * np.sin(2 * np.pi * 5.6 * t)
    ph = 2 * np.pi * np.cumsum(glide * vib) / SR
    s = np.sin(ph) + 0.18 * np.sin(2 * ph) + 0.06 * np.sin(3 * ph)
    e = kit.env_adsr(n, 0.06, 0.15, 0.85, 0.25)
    return s * e * 0.15 * vel


def harpsichord(f, vel=1.0, length=0.32):
    """Hell gezupft: viele Obertoene, hohe klingen schnell ab, kleiner Zupf-Knack."""
    n = int(length * SR)
    t = np.arange(n) / SR
    s = np.zeros(n)
    for k in range(1, 11):
        s += np.sin(2 * np.pi * f * k * 1.0007 ** k * t) * np.exp(-t * (7 + k * 4)) / (k ** 0.8)
    pluck = rng.randn(n) * np.exp(-t * 400) * 0.25
    s = (s + pluck) * np.clip(t / 0.0015, 0, 1)
    return s * 0.05 * vel


def pizz(f, vel=1.0, length=0.38):
    n = int(length * SR)
    t = np.arange(n) / SR
    s = np.zeros(n)
    for k in range(1, 6):
        s += np.sin(2 * np.pi * f * k * t) * np.exp(-t * (7 + k * 6)) / k
    s *= np.clip(t / 0.002, 0, 1)
    return s * 0.2 * vel


def organ(freqs, dur, vel=1.0):
    """Zugriegel-Orgel: Grundton, Oktave, Quinte darueber, leichtes Tremolo."""
    n = int((dur + 0.5) * SR)
    t = np.arange(n) / SR
    s = np.zeros(n)
    for f in freqs:
        for mult, amp in ((0.5, 0.5), (1, 1.0), (2, 0.45), (3, 0.25), (4, 0.12)):
            s += np.sin(2 * np.pi * f * mult * t) * amp
    trem = 1 + 0.12 * np.sin(2 * np.pi * 6.2 * t)
    e = kit.env_adsr(n, 0.12, 0.2, 0.85, 0.4)
    return s * trem * e * 0.018 * vel / max(1, len(freqs) / 3)


def ghost_choir(freqs, dur, vel=1.0):
    """'Uuh': verstimmte Saegezaehne, durch zwei Formanten (~320 Hz / ~800 Hz) gefiltert."""
    n = int((dur + 0.8) * SR)
    s = np.zeros(n)
    for f in freqs:
        for d in (-0.004, 0.0, 0.005):
            s += kit.osc_saw(f, n, d)
    spec = np.fft.rfft(s)
    fr = np.fft.rfftfreq(n, 1 / SR)
    shape = (np.exp(-((fr - 320) / 110) ** 2) + 0.45 * np.exp(-((fr - 800) / 160) ** 2)
             + 0.08 * np.exp(-((fr - 2300) / 300) ** 2))
    s = np.fft.irfft(spec * shape, n)
    t = np.arange(n) / SR
    wob = 1 + 0.08 * np.sin(2 * np.pi * 0.7 * t)
    e = kit.env_adsr(n, 0.6, 0.3, 0.8, 0.8)
    return s * e * wob * 0.09 * vel / max(1, len(freqs) / 3)


def midnight_bell(vel=1.0):
    """Turmglocke auf E3, lange Fahne - zu Teilanfaengen."""
    n = int(3.2 * SR)
    t = np.arange(n) / SR
    f = hz(midi("E3"))
    s = (np.sin(2 * np.pi * f * t) * np.exp(-t * 1.3)
         + 0.7 * np.sin(2 * np.pi * f * 2.0 * t) * np.exp(-t * 1.8)
         + 0.5 * np.sin(2 * np.pi * f * 2.4 * t) * np.exp(-t * 2.5)      # Moll-Terz-Teilton
         + 0.35 * np.sin(2 * np.pi * f * 3.0 * t) * np.exp(-t * 3.2)
         + 0.2 * np.sin(2 * np.pi * f * 4.2 * t) * np.exp(-t * 5.0)
         + 0.1 * np.sin(2 * np.pi * f * 5.4 * t) * np.exp(-t * 8.0))
    return s * np.clip(t / 0.003, 0, 1) * 0.12 * vel


def soft_kick(vel=1.0):
    return kit.kick(vel * 0.75)


def snap(vel=1.0):
    """Fingerschnipsen: kurzer, heller Rauschstoss mit Ton."""
    n = int(0.12 * SR)
    t = np.arange(n) / SR
    noise = rng.randn(n)
    noise = noise - kit.smooth_kernel(noise, 3)
    s = noise * np.exp(-t * 90) * 0.3 + np.sin(2 * np.pi * 2100 * t) * np.exp(-t * 70) * 0.12
    return s * vel


def bones(vel=1.0, pitch=1.0):
    """Knochen-Klappern: Holzblock, zwei schnelle Schlaege."""
    n = int(0.16 * SR)
    t = np.arange(n) / SR
    out = np.zeros(n)
    for d, a in ((0.0, 1.0), (0.028, 0.6)):
        k = int(d * SR)
        tt = t[:n - k]
        out[k:] += (np.sin(2 * np.pi * 1250 * pitch * tt) + 0.5 * np.sin(2 * np.pi * 2700 * pitch * tt)) \
            * np.exp(-tt * 60) * a
    return out * 0.06 * vel


def rattle(vel=1.0):
    n = int(0.09 * SR)
    t = np.arange(n) / SR
    noise = rng.randn(n)
    noise = kit.smooth_kernel(noise, 2) - kit.smooth_kernel(noise, 7)
    return noise * np.clip(t / 0.01, 0, 1) * np.exp(-t * 40) * 0.08 * vel


def wind(dur, vel=1.0):
    """Wind durch kahle Aeste: gefiltertes Rauschen, das an- und abschwillt."""
    n = int(dur * SR)
    t = np.arange(n) / SR
    noise = rng.randn(n)
    s = kit.smooth_kernel(noise, 30) - kit.smooth_kernel(noise, 120)
    swell = 0.5 + 0.5 * np.sin(2 * np.pi * t / dur * 1.5 - 1.2)
    howl = np.sin(2 * np.pi * np.cumsum(520 + 90 * np.sin(2 * np.pi * 0.31 * t)) / SR) * 0.02
    return (s * 0.5 + howl) * swell * np.clip(t / 0.8, 0, 1) * np.clip((dur - t) / 0.8, 0, 1) * 0.25 * vel


def owl(vel=1.0):
    """Eule: 'Huu - hu-huu', Sinus mit kleinem Abwaertsgleiten, auf B4/G4."""
    out = np.zeros(int(1.6 * SR))
    for d, note, ln in ((0.0, "B4", 0.38), (0.55, "G4", 0.16), (0.78, "G4", 0.5)):
        n = int(ln * SR)
        t = np.arange(n) / SR
        f = hz(midi(note)) * (1 - 0.03 * t / ln)
        ph = 2 * np.pi * np.cumsum(f) / SR
        s = np.sin(ph) + 0.15 * np.sin(2 * ph)
        e = np.sin(np.pi * np.clip(t / ln, 0, 1)) ** 0.6
        k = int(d * SR)
        out[k:k + n] += s * e * 0.06 * vel
    return out


def bone_roll(dur, vel=1.0):
    n = int(dur * SR)
    out = np.zeros(n)
    hits = int(dur / (BEAT / 4))
    for i in range(hits):
        s = bones(0.3 + 0.7 * i / hits, 1.0 + 0.15 * (i % 2))
        k = int(i * BEAT / 4 * SR)
        m = min(len(s), n - k)
        out[k:k + m] += s[:m] * vel
    return out


# ================================================================ Bausteine

Track = kit.Track


def chord_notes(name, low, high):
    root, tones = CHORDS[name]
    out = []
    for o in range(1, 8):
        for tn in tones:
            m = midi(tn + str(o))
            if low <= m <= high:
                out.append(m)
    return sorted(out)


def theremin_line(lines, start, tr, vel=1.0, shift=-12):
    t = start
    prev = None
    for bar in lines:
        for note, beats in bar:
            if note is not None:
                f = hz(midi(note) + shift)
                tr.add(theremin(f, beats * BEAT * 0.96, vel, prev), t)
                prev = f
            else:
                prev = None
            t += beats * BEAT


def melody(lines, start, tr, inst, vel=1.0):
    t = start
    for bar in lines:
        for note, beats in bar:
            if note is not None:
                tr.add(inst(hz(midi(note)), beats * BEAT * 0.9, vel), t)
            t += beats * BEAT


def sneak_bass(prog, start, tr, vel=1.0, style="sneak"):
    """Viertel auf Zehenspitzen: Grundton, Grundton, Quinte, Halbton zum naechsten Grundton."""
    for b, name in enumerate(prog):
        root = midi(CHORDS[name][0] + "2")
        if root > midi("A2"):
            root -= 12
        nxt = prog[(b + 1) % len(prog)]
        nroot = midi(CHORDS[nxt][0] + "2")
        if nroot > midi("A2"):
            nroot -= 12
        fifth = root + 7
        approach = nroot - 1 if nroot != root else root + 1
        t0 = start + b * BAR
        if style == "long":
            tr.add(pizz(hz(root), vel, 1.2), t0)
            tr.add(pizz(hz(fifth), vel * 0.7, 0.8), t0 + 2 * BEAT)
            continue
        for beat, m, v in ((0, root, 1.0), (1, root, 0.7), (2, fifth, 0.85), (3, approach, 0.75)):
            tr.add(pizz(hz(m), vel * v), t0 + beat * BEAT)


def tiptoe(prog, start, tr, vel=1.0, pattern="tip"):
    """Cembalo-Achtel: Akkordtoene auf Zehenspitzen (E3-H4)."""
    for b, name in enumerate(prog):
        notes = chord_notes(name, midi("E3"), midi("B4"))
        seq = [notes[0], notes[2], notes[1], notes[2]] if len(notes) >= 3 else notes
        if pattern == "climb":
            seq = notes[:4] if len(notes) >= 4 else notes + notes[:1]
        for i in range(8):
            if pattern == "tip" and i in (3, 7):
                continue       # Luecke - als wuerde jemand stehenbleiben und lauschen
            m = seq[i % len(seq)]
            tr.add(harpsichord(hz(m), vel * (1.0 if i % 2 == 0 else 0.7)), start + b * BAR + i * BEAT / 2)


def organ_pads(prog, start, tr, vel=1.0):
    for b, name in enumerate(prog):
        fs = [hz(m) for m in chord_notes(name, midi("E3"), midi("B4"))[:3]]
        tr.add(organ(fs, BAR, vel), start + b * BAR)


def choir(prog, start, tr, vel=1.0):
    for b, name in enumerate(prog):
        fs = [hz(m) for m in chord_notes(name, midi("B2"), midi("G4"))[:3]]
        tr.add(ghost_choir(fs, BAR, vel), start + b * BAR)


def music_box(prog, start, tr, vel=1.0):
    for b, name in enumerate(prog):
        notes = chord_notes(name, midi("E6"), midi("E7"))
        m = notes[(b * 2 + 1) % len(notes)]
        tr.add(kit.celesta(hz(m), BEAT, vel), start + b * BAR + 2.5 * BEAT)


def drums(bars, start, T, style="groove", vel=1.0):
    for b in range(bars):
        t0 = start + b * BAR
        for i in range(8):
            t = t0 + i * BEAT / 2
            if style in ("groove", "drive"):
                if i in (0, 4) or (style == "drive" and i == 5 and b % 2 == 1):
                    T["kick"].add(soft_kick(vel), t)
                if i in (2, 6):
                    T["snap"].add(snap(vel), t)
            if style != "none" or i % 2:
                T["rattle"].add(rattle(vel * (1.0 if i % 2 else 0.5)), t)
        # Knochen klappern auf der "und" der Vier (im drive auch auf der Zwei-und)
        T["bones"].add(bones(vel), t0 + 3.5 * BEAT)
        if style == "drive":
            T["bones"].add(bones(vel * 0.7, 1.2), t0 + 1.5 * BEAT)


# ================================================================ Lied

def compose():
    total_bars = sum(b for _, b in SECTIONS)
    length = total_bars * BAR

    T = {
        "lead": Track(length, pan=0.0, wet=0.35),
        "harp": Track(length, pan=-0.3, wet=0.25),
        "organ": Track(length, pan=0.15, wet=0.45),
        "choir": Track(length, pan=-0.1, wet=0.6),
        "box": Track(length, pan=0.35, wet=0.55),
        "bass": Track(length, pan=0.0, wet=0.08),
        "kick": Track(length, pan=0.0, wet=0.04),
        "snap": Track(length, pan=0.15, wet=0.3),
        "bones": Track(length, pan=-0.35, wet=0.3),
        "rattle": Track(length, pan=0.3, wet=0.12),
        "bell": Track(length, pan=0.0, wet=0.5),
        "forest": Track(length, pan=-0.2, wet=0.5),
    }

    t = 0.0
    for name, bars in SECTIONS:
        if name == "intro":
            T["forest"].add(wind(bars * BAR, 1.0), t)
            T["forest"].add(owl(1.0), t + 1 * BAR + 2 * BEAT)
            tiptoe(PROG_I, t, T["harp"], 0.7, "tip")
            sneak_bass(PROG_I, t, T["bass"], 0.8, "long")
            melody(TEASER, t, T["box"], kit.celesta, vel=1.0)
            T["bell"].add(midnight_bell(0.9), t)
        elif name in ("A", "A2", "A3"):
            theremin_line(MEL_A, t, T["lead"], 1.0)
            sneak_bass(PROG_A, t, T["bass"])
            drums(bars, t, T, "groove", 0.8 if name == "A" else 1.0)
            if name != "A":
                tiptoe(PROG_A, t, T["harp"], 0.8, "tip")
            if name == "A3":
                music_box(PROG_A, t, T["box"], 0.9)
                organ_pads(PROG_A, t, T["organ"], 0.55)
            T["bell"].add(midnight_bell(0.8 if name == "A" else 1.0), t)
        elif name in ("B", "B2"):
            theremin_line(MEL_B, t, T["lead"], 1.05)
            organ_pads(PROG_B, t, T["organ"], 1.0)
            choir(PROG_B, t, T["choir"], 0.9 if name == "B" else 1.1)
            sneak_bass(PROG_B, t, T["bass"], 1.0)
            tiptoe(PROG_B, t, T["harp"], 0.6, "climb")
            music_box(PROG_B, t, T["box"], 1.0)
            drums(bars, t, T, "drive", 1.0)
            T["bell"].add(midnight_bell(1.0), t)
        elif name == "C":
            # Mochis Buehne: kein Thema, kein Schnipsen - Bass, Cembalo, Chor ganz leise
            sneak_bass(PROG_C, t, T["bass"], 0.85, "long")
            tiptoe(PROG_C, t, T["harp"], 0.55, "tip")
            choir(PROG_C, t, T["choir"], 0.5)
            drums(bars, t, T, "none", 0.8)
            T["forest"].add(wind(bars * BAR, 0.7), t)
            T["forest"].add(owl(0.8), t + 2 * BAR + 1 * BEAT)
            T["forest"].add(owl(0.7), t + 6 * BAR + 2 * BEAT)
            for b in range(4, 8):
                T["kick"].add(soft_kick(0.5), t + b * BAR)
                T["kick"].add(soft_kick(0.45), t + b * BAR + 2 * BEAT)
        elif name == "turn":
            sneak_bass(PROG_T, t, T["bass"], 0.9)
            tiptoe(PROG_T, t, T["harp"], 0.7, "climb")
            theremin_line([[("C5", 2), ("E5", 2)], [("G5", 2), ("E5", 2)], [("D#5", 2), ("F#5", 2)],
                           [("B5", 3), (None, 1)]], t, T["lead"], 0.85)
            drums(3, t, T, "groove", 0.85)
            drums(1, t + 3 * BAR, T, "none", 0.8)
            T["bones"].add(bone_roll(BAR, 0.9), t + 3 * BAR)
        t += bars * BAR
    return T, length


def main():
    T, length = compose()
    data = kit.mix(T, length)
    pcm = (np.clip(data, -1, 1) * 32767).astype(np.int16)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with wave.open(OUT, "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    rms = 20 * math.log10(math.sqrt((data ** 2).mean()))
    print("geist.wav  %.1f s  RMS %.1f dBFS  (%d Takte, %d BPM)" % (len(data) / SR, rms, len(song_chords()), BPM))
    print("SongSheet:", " ".join(song_chords()))

    meta = OUT + ".meta"
    if not os.path.exists(meta):
        import uuid
        src = open(os.path.join(os.path.dirname(OUT), "wald.wav.meta")).read()
        open(meta, "w", newline="\n").write(src.replace("56b195d28eea480ba70e21313510ac90", uuid.uuid4().hex))


if __name__ == "__main__":
    main()
