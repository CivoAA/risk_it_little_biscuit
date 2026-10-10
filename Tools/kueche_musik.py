"""
Musik der Kueche (Map_World0): "Mochis Kuechenparade".

Komplett im Code komponiert und synthetisiert, wie Tools/eis_musik.py (dessen
Klangerzeuger und Mischpult hier mitbenutzt werden).

Gebaut fuer die Mochi-Melodie (SongSheet.cs, Eintrag "kueche"):
  - G-Dur, 120 BPM, gerade Achtel (kein Swing) - Mochis Viertel und Achtel
    sitzen genau im Raster, eine Viertel = 0,5 s liegt nah an ihrem Cooldown
  - erste Eins genau bei 0, Schleife = genau 56 Takte (112 s), Hallfahne nach
    vorn gefaltet - der Takt laeuft ueber den Loop-Punkt einfach weiter
  - die Akkorde unten (PROG_*) sind 1:1 die in SongSheet.cs - wer hier etwas
    aendert, aendert es dort mit
  - Platz fuer Mochi: ihr Spieluhr-Ton liegt bei G4-D6. Die Melodie des Stuecks
    spielt eine Klarinette eine Etage tiefer (G3-E5), Marimba/Spieluhr-Klaenge
    gibt es im Stueck nicht, und die Melodie laesst Luecken. Teil C ist
    Mochis Buehne: nur Bass, Zupfer, Schneebesen.

  Instrumente
    Klarinette    ungerade Obertoene, weich, mit Vibrato - das Thema
    Zupfstreicher Pizzicato auf 2 und 4 (Hm-ta-Hm-ta), Arpeggios im Intro
    Zupfbass      Grundton/Quinte auf 1 und 3, huepft
    Celesta       ganz oben, selten - Zuckerstreusel
    Flaeche       leise Streicherflaeche im Refrain
    Kuechen-      Topf-Kick, Pfannen-Klatsch, Schneebesen (Achtel-Shaker),
    Schlagwerk    Loeffel am Glas, grosser Topf-Gong zu Teilanfaengen

  Aufbau (Takte)
    Intro    4   Zupfer + Loeffel, die Kueche wacht auf
    A        8   Thema in der Klarinette
    A'       8   Thema + Zupfer-Gegenstimme, voller Beat
    B        8   Refrain, Flaeche + Celesta
    C        8   Breakdown - Mochis Buehne
    A''      8   Thema voll
    B'       8   Refrain voll
    Wende    4   Ueberleitung mit Wirbel zurueck zum Intro

  Assets/music/kueche.wav   (44.1 kHz, 16 bit, Stereo; .meta wie wald.wav)

Aufruf aus dem Projektordner:  python Tools/kueche_musik.py
"""

import math
import os
import sys
import wave

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import eis_musik as kit  # noqa: E402  (Klangerzeuger, Hall, Mix)

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "music", "kueche.wav")

SR = kit.SR
BPM = 120
BEAT = 60.0 / BPM
BAR = 4 * BEAT
rng = np.random.RandomState(4711)

midi = kit.midi
hz = kit.hz

# ================================================================ Akkorde

CHORDS = {
    "G": ("G", ["G", "B", "D"]),
    "Em": ("E", ["E", "G", "B"]),
    "C": ("C", ["C", "E", "G"]),
    "D": ("D", ["D", "F#", "A"]),
    "Am": ("A", ["A", "C", "E"]),
    "Bm": ("B", ["B", "D", "F#"]),
}

PROG_I = ["G", "Em", "C", "D"]
PROG_A = ["G", "Em", "C", "D", "G", "Em", "Am", "D"]
PROG_B = ["C", "D", "Bm", "Em", "Am", "D", "G", "G"]
PROG_C = ["Em", "C", "G", "D", "Em", "C", "Am", "D"]
PROG_T = ["G", "Em", "Am", "D"]

SECTIONS = [("intro", 4), ("A", 8), ("A2", 8), ("B", 8), ("C", 8), ("A3", 8), ("B2", 8), ("turn", 4)]


def song_chords():
    """Akkord je Takt fuer die ganze Schleife - so steht es in SongSheet.cs."""
    progs = {"intro": PROG_I, "A": PROG_A, "A2": PROG_A, "A3": PROG_A, "B": PROG_B, "B2": PROG_B,
             "C": PROG_C, "turn": PROG_T}
    return [c for name, _ in SECTIONS for c in progs[name]]


# Melodien: (Note oder None, Schlaege) - Klarinette, bewusst tief
MEL_A = [
    [("D4", .5), ("G4", .5), ("B4", 1), ("A4", .5), ("G4", .5), ("D4", 1)],
    [("E4", 1), ("G4", .5), ("A4", .5), ("B4", 2)],
    [("G4", .5), ("A4", .5), ("G4", .5), ("E4", .5), ("C4", 1), ("E4", 1)],
    [("D4", 3), (None, 1)],
    [("D4", .5), ("G4", .5), ("B4", 1), ("D5", .5), ("C5", .5), ("B4", 1)],
    [("G4", 1), ("B4", .5), ("G4", .5), ("E4", 2)],
    [("C5", 1), ("B4", .5), ("A4", .5), ("E4", 1), ("A4", 1)],
    [("F#4", 1), ("A4", 1), ("D4", 1), (None, 1)],
]
MEL_B = [
    [("E5", 1.5), ("D5", .5), ("C5", 1), ("G4", 1)],
    [("F#4", 1), ("A4", 1), ("D5", 1.5), ("C5", .5)],
    [("B4", 1.5), ("A4", .5), ("F#4", 1), ("D4", 1)],
    [("E4", 1), ("G4", 1), ("B4", 2)],
    [("C5", 1.5), ("B4", .5), ("A4", 1), ("E4", 1)],
    [("F#4", 1), ("A4", .5), ("B4", .5), ("C5", 1), ("A4", 1)],
    [("B4", 1), ("D5", 1), ("G4", 2)],
    [(None, 4)],
]
# Vorgeschmack im Intro / Celesta-Streusel, ganz oben (ueber Mochi)
TEASER = [[("D6", 1), ("G6", 1), ("B6", 2)], [("G6", 4)], [("E6", 1), ("G6", 1), ("C7", 2)], [("A6", 2), ("F#6", 2)]]


# ================================================================ Klangerzeuger

def clarinet(f, dur, vel=1.0):
    """Ungerade Obertoene, sanfter Einsatz, Vibrato erst nach dem Anblasen."""
    n = int((dur + 0.18) * SR)
    t = np.arange(n) / SR
    vib = 1 + 0.005 * np.sin(2 * np.pi * 5.0 * t) * np.clip((t - 0.2) / 0.25, 0, 1)
    ph = 2 * np.pi * np.cumsum(np.full(n, f) * vib) / SR
    s = np.sin(ph) + 0.42 * np.sin(3 * ph) + 0.2 * np.sin(5 * ph) + 0.08 * np.sin(7 * ph)
    breath = rng.randn(n)
    breath = kit.smooth_kernel(breath, 8) * 0.04 * np.exp(-t * 12)
    e = kit.env_adsr(n, 0.035, 0.1, 0.8, 0.16)
    return (s + breath) * e * 0.16 * vel


def pizz(f, vel=1.0, length=0.45):
    """Gezupfte Saite: hoehere Obertoene klingen schneller ab."""
    n = int(length * SR)
    t = np.arange(n) / SR
    s = np.zeros(n)
    for k in range(1, 7):
        s += np.sin(2 * np.pi * f * k * t) * np.exp(-t * (6 + k * 5)) / k
    s *= np.clip(t / 0.002, 0, 1)
    return s * 0.11 * vel


def pot_kick(vel=1.0):
    """Holzloeffel auf Topfboden: kurzer Kick mit etwas Blech."""
    s = kit.kick(vel * 0.85)
    n = len(s)
    t = np.arange(n) / SR
    s += np.sin(2 * np.pi * 410 * t) * np.exp(-t * 35) * 0.05 * vel
    return s


def pan_clap(vel=1.0):
    """Flache Hand auf Pfanne: Klatschen (mehrere Rauschstoesse) + kurzer Pfannenton."""
    n = int(0.25 * SR)
    t = np.arange(n) / SR
    noise = rng.randn(n)
    noise = noise - kit.smooth_kernel(noise, 5)
    env = np.zeros(n)
    for d in (0.0, 0.011, 0.023):
        k = int(d * SR)
        env[k:] += np.exp(-(t[:n - k]) * 60)
    env += np.exp(-t * 14) * 0.35
    ring = np.sin(2 * np.pi * 880 * t) * np.exp(-t * 22) * 0.12   # A5 - liegt in G-Dur
    return (noise * env * 0.22 + ring) * vel


def whisk(vel=1.0):
    """Schneebesen in der Schuessel: weiches Wischen statt Hi-Hat."""
    n = int(0.11 * SR)
    t = np.arange(n) / SR
    noise = rng.randn(n)
    noise = kit.smooth_kernel(noise, 2) - kit.smooth_kernel(noise, 9)
    env = np.clip(t / 0.018, 0, 1) * np.exp(-t * 28)
    return noise * env * 0.10 * vel


def clink(vel=1.0):
    """Loeffel an Glas: hohe, leicht unharmonische Teiltoene auf D7."""
    n = int(0.5 * SR)
    t = np.arange(n) / SR
    f = hz(midi("D7"))
    s = (np.sin(2 * np.pi * f * t) * np.exp(-t * 11)
         + 0.5 * np.sin(2 * np.pi * f * 2.32 * t) * np.exp(-t * 18)
         + 0.25 * np.sin(2 * np.pi * f * 3.9 * t) * np.exp(-t * 30))
    return s * np.clip(t / 0.001, 0, 1) * 0.035 * vel


def pot_gong(vel=1.0):
    """Grosser Topf mit Kochloeffel angeschlagen, auf G2 - zu Teilanfaengen."""
    n = int(2.2 * SR)
    t = np.arange(n) / SR
    f = hz(midi("G2"))
    s = (np.sin(2 * np.pi * f * t) * np.exp(-t * 2.2)
         + 0.6 * np.sin(2 * np.pi * f * 2.0 * t) * np.exp(-t * 3.0)
         + 0.35 * np.sin(2 * np.pi * f * 2.92 * t) * np.exp(-t * 5.0)
         + 0.2 * np.sin(2 * np.pi * f * 4.1 * t) * np.exp(-t * 8.0))
    return s * np.clip(t / 0.004, 0, 1) * 0.14 * vel


def roll(dur, vel=1.0):
    """Loeffel-Wirbel auf der Pfanne, anschwellend (Ueberleitung)."""
    n = int(dur * SR)
    out = np.zeros(n)
    hits = int(dur / (BEAT / 4))
    for i in range(hits):
        s = pan_clap(0.25 + 0.6 * i / hits)
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


def melody(lines, start, tr, inst, octave_shift=0, vel=1.0):
    t = start
    for bar in lines:
        for note, beats in bar:
            if note is not None:
                f = hz(midi(note) + 12 * octave_shift)
                tr.add(inst(f, beats * BEAT * 0.92, vel), t)
            t += beats * BEAT


def bounce_bass(prog, start, tr, style="bounce", vel=1.0):
    """Grundton auf 1, Quinte auf 3 - im 'drive' noch Achtel dazwischen."""
    for b, name in enumerate(prog):
        root, tones = CHORDS[name]
        r = midi(root + "2")
        if r < midi("E2"):
            r += 12
        fifth = midi(tones[2] + "2")
        while fifth <= r:
            fifth += 12
        t0 = start + b * BAR
        if style == "long":
            tr.add(kit.pluck_bass(hz(r), BAR * 0.9, vel * 0.9), t0)
            continue
        hits = [(0, r, 1.0), (2, fifth, 0.85)]
        if style == "drive":
            hits += [(1.5, r, 0.6), (3.5, r + 12, 0.6)]
        for beat, m, v in hits:
            tr.add(kit.pluck_bass(hz(m), BEAT * 0.8, vel * v), t0 + beat * BEAT)


def oompah(prog, start, tr, vel=1.0):
    """Zupfer-Akkorde auf 2 und 4 (G3-B4) - das Huepfen der Kueche."""
    for b, name in enumerate(prog):
        notes = chord_notes(name, midi("G3"), midi("B4"))[:3]
        for beat in (1, 3):
            for m in notes:
                tr.add(pizz(hz(m), vel * 0.75, 0.3), start + b * BAR + beat * BEAT)


def pizz_arp(prog, start, tr, vel=1.0):
    """Achtel-Arpeggio der Zupfer hoch und runter (D3-D5)."""
    for b, name in enumerate(prog):
        notes = chord_notes(name, midi("D3"), midi("D5"))
        seq = notes + notes[-2:0:-1]
        for i in range(8):
            m = seq[i % len(seq)]
            tr.add(pizz(hz(m), vel * (1.0 if i % 2 == 0 else 0.7)), start + b * BAR + i * BEAT / 2)


def sprinkles(prog, start, tr, vel=1.0):
    """Celesta ganz oben, ein Ton pro Takt auf der 'und' der Zwei."""
    for b, name in enumerate(prog):
        notes = chord_notes(name, midi("D6"), midi("D7"))
        m = notes[(b * 2) % len(notes)]
        tr.add(kit.celesta(hz(m), BEAT, vel), start + b * BAR + 1.5 * BEAT)


def pads(prog, start, tr, vel=1.0):
    for b, name in enumerate(prog):
        fs = [hz(m) for m in chord_notes(name, midi("G3"), midi("D5"))[:3]]
        tr.add(kit.pad(fs, BAR, vel), start + b * BAR)


def drums(bars, start, T, style="groove", vel=1.0):
    for b in range(bars):
        t0 = start + b * BAR
        for i in range(8):
            t = t0 + i * BEAT / 2
            if style in ("groove", "drive"):
                if i in (0, 4) or (style == "drive" and i == 6 and b % 2 == 1):
                    T["kick"].add(pot_kick(vel), t)
                if i in (2, 6):
                    T["clap"].add(pan_clap(vel), t)
            # Schneebesen auf allen Achteln, Betonung auf der "und"
            T["whisk"].add(whisk(vel * (1.0 if i % 2 else 0.55)), t)
        # Loeffel ans Glas auf der "und" der Vier
        T["clink"].add(clink(vel * (0.8 if style == "none" else 1.0)), t0 + 3.5 * BEAT)


# ================================================================ Lied

def compose():
    total_bars = sum(b for _, b in SECTIONS)
    length = total_bars * BAR

    T = {
        "lead": Track(length, pan=0.0, wet=0.22),
        "pizz": Track(length, pan=-0.25, wet=0.25),
        "oompah": Track(length, pan=0.2, wet=0.2),
        "celesta": Track(length, pan=0.35, wet=0.5),
        "pad": Track(length, pan=0.0, wet=0.55),
        "bass": Track(length, pan=0.0, wet=0.05),
        "kick": Track(length, pan=0.0, wet=0.04),
        "clap": Track(length, pan=0.05, wet=0.22),
        "whisk": Track(length, pan=0.3, wet=0.1),
        "clink": Track(length, pan=-0.35, wet=0.35),
        "fx": Track(length, pan=0.0, wet=0.4),
    }

    t = 0.0
    for name, bars in SECTIONS:
        if name == "intro":
            pizz_arp(PROG_I, t, T["pizz"], 0.9)
            bounce_bass(PROG_I, t, T["bass"], "long", 0.9)
            drums(bars, t, T, "none", 0.9)
            melody(TEASER, t, T["celesta"], kit.celesta, vel=1.1)
        elif name in ("A", "A2", "A3"):
            melody(MEL_A, t, T["lead"], clarinet, vel=1.0)
            bounce_bass(PROG_A, t, T["bass"], "bounce")
            oompah(PROG_A, t, T["oompah"], 0.9 if name == "A" else 1.0)
            drums(bars, t, T, "groove", 0.85 if name == "A" else 1.0)
            if name != "A":
                pizz_arp(PROG_A, t, T["pizz"], 0.45)
            if name == "A3":
                sprinkles(PROG_A, t, T["celesta"], 0.9)
            T["fx"].add(pot_gong(0.8 if name == "A" else 1.0), t)
        elif name in ("B", "B2"):
            melody(MEL_B, t, T["lead"], clarinet, vel=1.05)
            pads(PROG_B, t, T["pad"], 1.0 if name == "B" else 1.2)
            bounce_bass(PROG_B, t, T["bass"], "drive")
            oompah(PROG_B, t, T["oompah"], 1.0)
            sprinkles(PROG_B, t, T["celesta"], 1.0)
            drums(bars, t, T, "drive", 1.0)
            T["fx"].add(pot_gong(1.0), t)
        elif name == "C":
            # Mochis Buehne: kein Thema, kein Klatschen - nur Bass, Zupfer, Besen
            bounce_bass(PROG_C, t, T["bass"], "bounce", 0.85)
            pizz_arp(PROG_C, t, T["pizz"], 0.55)
            pads(PROG_C, t, T["pad"], 0.7)
            drums(bars, t, T, "none", 0.85)
            # in der zweiten Haelfte kommt der Topf-Kick leise zurueck
            for b in range(4, 8):
                T["kick"].add(pot_kick(0.5), t + b * BAR)
                T["kick"].add(pot_kick(0.45), t + b * BAR + 2 * BEAT)
        elif name == "turn":
            bounce_bass(PROG_T, t, T["bass"], "bounce", 0.9)
            oompah(PROG_T, t, T["oompah"], 0.9)
            pizz_arp(PROG_T, t, T["pizz"], 0.6)
            melody([[("B4", 2), ("G4", 2)], [("E4", 2), ("G4", 2)], [("A4", 2), ("C5", 2)], [("D5", 2), ("F#4", 2)]],
                   t, T["lead"], clarinet, vel=0.8)
            drums(3, t, T, "groove", 0.85)
            drums(1, t + 3 * BAR, T, "none", 0.8)
            T["clap"].add(roll(BAR, 0.8), t + 3 * BAR)
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
    print("kueche.wav  %.1f s  RMS %.1f dBFS  (%d Takte, %d BPM)" % (len(data) / SR, rms, len(song_chords()), BPM))
    print("SongSheet:", " ".join(song_chords()))

    meta = OUT + ".meta"
    if not os.path.exists(meta):
        import uuid
        src = open(os.path.join(os.path.dirname(OUT), "wald.wav.meta")).read()
        open(meta, "w", newline="\n").write(src.replace("56b195d28eea480ba70e21313510ac90", uuid.uuid4().hex))


if __name__ == "__main__":
    main()
