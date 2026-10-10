"""
Musik der Herzkammer (Verkohlter, Phase 3): "Herz der Glut" + Finale.

Komplett im Code komponiert und synthetisiert; Klangerzeuger aus
Tools/vulkan_musik.py (Blech, Chor, Taiko, Pauke, Glocke, Braam ...).

Das Herz schlaegt im Takt der Musik:
  - 90 BPM, ein Herzschlag (16 Bilder @ 12 fps = 1,333 s) = genau zwei
    Schlaege. "BUM" (Bild 2) liegt auf 1 und 3, "bumm" (Bild 5) eine
    punktierte Sechzehntel (0,25 s) spaeter - genau dort schlagen hier die
    Taiko. VerkohlterHerzkammer stellt seine Uhr nach der Musik
    (MusicClock), solange dieses Stueck laeuft.
  - c-Moll, das Motiv des Verkohlten aus vulkan_musik.py (MEL_A) kehrt halb
    so schnell als Hoerner, Trompeten und Chor wieder.

Gebaut fuer die Mochi-Melodie (SongSheet.cs, Eintrag "herzkammer"):
  - erste Eins genau bei 0, Schleife = genau 36 Takte (96 s), Hallfahne nach
    vorn gefaltet; eine Viertel = 0,667 s (Mochi spielt Viertel, mit
    Cooldown-Buff Achtel)
  - Akkorde (PROG_*) 1:1 wie in SongSheet.cs und Tools/mochi_mitspielen.py
  - Teil C ist Mochis Buehne: nur Herzschlag, Summchor, Glocke.

  Aufbau (Takte)
    Auftakt  2   Einschlag (Landung in der Kammer), Chor, Glocke, Paukenwirbel
    A        8   Motiv in den Hoernern, Streicher-Ostinato, Herzschlag-Taiko
    B        8   Refrain: Trompeten, voller Chor, Becken, Braams
    C        8   Dunkel: Summchor, Glocke, nur das Herz - Mochis Buehne
    D        8   Hoehepunkt: Motiv in Hoernern + Trompeten + Chor
    Wende    2   Braams auf As und G, Wirbel, Sog zurueck in den Einschlag

  Finale (kein Loop): das Herz birst -> Einschlag, Fanfare des Motivs in
  C-DUR, Chor und Glocke klingen aus.

  Assets/Resources/Music/herzkammer.wav, herzkammer_finale.wav
  (44.1 kHz, 16 bit, Stereo; .meta wie eis.wav, aber im Hintergrund laden)

Aufruf aus dem Projektordner:  python Tools/herz_musik.py
"""

import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import vulkan_musik as vk  # noqa: E402

ROOT = vk.ROOT
OUT_DIR = os.path.join(ROOT, "Assets", "Resources", "Music")
OUT = os.path.join(OUT_DIR, "herzkammer.wav")
OUT_FINALE = os.path.join(OUT_DIR, "herzkammer_finale.wav")

SR = vk.SR
BPM = 90
BEAT = 60.0 / BPM
BAR = 4 * BEAT
DUB = 0.25          # Sekunden vom "BUM" zum "bumm" (3 Bilder @ 12 fps)
rng = np.random.RandomState(1312)
vk.rng = rng        # die Klangerzeuger ziehen ihren Zufall von hier

midi = vk.midi
hz = vk.hz
Track = vk.Track
CHORDS = vk.CHORDS

PROG_I = ["Cm", "Cm"]
PROG_A = vk.PROG_A                                   # Cm Ab Bb G Cm Ab Fm G
PROG_B = vk.PROG_B                                   # Ab Bb Eb Cm Ab Bb G G
PROG_C = ["Fm", "Cm", "Ab", "G", "Fm", "Cm", "Db", "G"]
PROG_D = vk.PROG_A
PROG_T = ["Ab", "G"]

SECTIONS = [("intro", 2), ("A", 8), ("B", 8), ("C", 8), ("D", 8), ("turn", 2)]


def song_chords():
    progs = {"intro": PROG_I, "A": PROG_A, "B": PROG_B, "C": PROG_C, "D": PROG_D, "turn": PROG_T}
    return [c for name, _ in SECTIONS for c in progs[name]]


# ================================================================ Klaenge

def horn(f, dur, vel=1.0):
    return vk.brass(f, dur, vel, bright=0.7, voices=4, attack=0.08)


def trumpet(f, dur, vel=1.0):
    return vk.brass(f, dur, vel, bright=1.6, voices=3, attack=0.03)


def trombone(f, dur, vel=1.0):
    return vk.brass(f, dur, vel, bright=0.9, voices=3, attack=0.06)


def choir_line(f, dur, vel=1.0):
    """Chor singt die Melodie mit ("Ah")."""
    return vk.choir([f], dur, vel, "a", singers=5, attack=0.12, release=0.3)


def heart_lub(vel=1.0):
    """Das BUM: grosse Taiko + Sub - der Herzschlag im Orchester."""
    s = vk.taiko(vel, pitch=0.85)
    n = len(s)
    t = np.arange(n) / SR
    s += np.sin(2 * np.pi * np.cumsum(38 + 30 * np.exp(-t * 18)) / SR) * np.exp(-t * 4) * 0.45 * vel
    return s


def heart_dub(vel=1.0):
    return vk.taiko(vel * 0.62, pitch=1.05)


# ================================================================ Bausteine

def heartbeat(bars, start, T, vel=1.0, double=False):
    """Herzschlag auf 1 und 3 (+ 'bumm' 0,25 s spaeter). double: dazu Taiko-Achtel auf 2 und 4."""
    for b in range(bars):
        t0 = start + b * BAR
        for beat in (0, 2):
            T["heart"].add(heart_lub(vel), t0 + beat * BEAT)
            T["heart"].add(heart_dub(vel), t0 + beat * BEAT + DUB)
        if double:
            for beat in (1, 1.5, 3, 3.5):
                T["taiko"].add(vk.taiko(vel * (0.6 if beat % 1 else 0.75), 1.15), t0 + beat * BEAT)


def ostinato(prog, start, tr, vel=1.0, low=True):
    """Streicher in Sechzehnteln (0,167 s) - das Blut, das durch die Kammer rast."""
    for b, name in enumerate(prog):
        lo, hi = (midi("C3"), midi("C4")) if low else (midi("C4"), midi("C5"))
        notes = vk.chord_notes(name, lo, hi)
        pat = [0, 0, 2, 0, 1, 0, 2, 1, 0, 0, 2, 0, 1, 2, 1, 2]
        for i, p in enumerate(pat):
            m = notes[min(p, len(notes) - 1)]
            acc = 1.0 if i % 4 == 0 else 0.6
            tr.add(vk.strings_spic(hz(m), vel * acc, 0.12), start + b * BAR + i * BEAT / 4)


def low_strings(prog, start, tr, vel=1.0):
    for b, name in enumerate(prog):
        r = vk.root_of(name, 2)
        if r < midi("C2"):
            r += 12
        tr.add(vk.strings_long([hz(r), hz(r + 12)], BAR * 0.98, vel), start + b * BAR)


def high_strings(prog, start, tr, vel=1.0):
    for b, name in enumerate(prog):
        fs = [hz(m) for m in vk.chord_notes(name, midi("G4"), midi("G5"))[:3]]
        tr.add(vk.strings_long(fs, BAR * 0.98, vel), start + b * BAR)


def choir_chords(prog, start, tr, vel=1.0, vowel="a", low=midi("G3"), high=midi("Eb5")):
    for b, name in enumerate(prog):
        fs = [hz(m) for m in vk.chord_notes(name, low, high)[:4]]
        tr.add(vk.choir(fs, BAR * 0.9, vel, vowel, singers=4, attack=0.4, release=0.45), start + b * BAR)


def bass_pedal(prog, start, tr, vel=1.0):
    """Kontrabass-Glut: Grundton in Achteln auf 1 und 3, gezogen."""
    for b, name in enumerate(prog):
        r = vk.root_of(name, 1)
        if r < midi("E1"):
            r += 12
        for beat in (0, 2):
            tr.add(vk.glut_bass(hz(r), BEAT * 1.8, vel), start + b * BAR + beat * BEAT)


def march(bars, start, tr, vel=1.0):
    """Kleine Trommel: Marschfigur auf 2 und 4."""
    for b in range(bars):
        t0 = start + b * BAR
        for beat, v in ((1, 1.0), (1.75, 0.5), (3, 1.0), (3.5, 0.45), (3.75, 0.6)):
            tr.add(vk.snare(vel * v), t0 + beat * BEAT)


# ================================================================ Lied

def tracks(length):
    return {
        "horn": Track(length, pan=-0.12, wet=0.35),
        "trumpet": Track(length, pan=0.15, wet=0.38),
        "trombone": Track(length, pan=-0.3, wet=0.35),
        "choir": Track(length, pan=0.0, wet=0.6),
        "choir_mel": Track(length, pan=0.05, wet=0.55),
        "strings": Track(length, pan=-0.2, wet=0.3),
        "strings_hi": Track(length, pan=0.25, wet=0.5),
        "strings_lo": Track(length, pan=-0.05, wet=0.35),
        "bass": Track(length, pan=0.0, wet=0.08),
        "heart": Track(length, pan=0.0, wet=0.22),
        "taiko": Track(length, pan=0.2, wet=0.25),
        "timp": Track(length, pan=-0.15, wet=0.3),
        "snare": Track(length, pan=0.1, wet=0.3),
        "bell": Track(length, pan=0.3, wet=0.55),
        "fx": Track(length, pan=0.0, wet=0.45),
    }


# Pegel je Spur (dB), gemessen wie in vulkan_musik.py
GAINS = {"horn": 12, "trumpet": 9, "trombone": 8, "choir": 13, "choir_mel": 12, "strings": 9,
         "strings_hi": 11, "strings_lo": 9, "bass": -4, "heart": -5, "taiko": -5, "timp": -6, "snare": 0,
         "bell": 5, "fx": -5}


def compose():
    total_bars = sum(b for _, b in SECTIONS)
    length = total_bars * BAR
    T = tracks(length)
    C2 = hz(midi("C2"))
    G2 = hz(midi("G2"))

    t = 0.0
    for name, bars in SECTIONS:
        if name == "intro":
            # Einschlag - hier landet der Spieler in der Kammer
            T["fx"].add(vk.braam(midi("C2"), 4.5, 1.0), t)
            T["fx"].add(vk.boom(1.0), t)
            T["fx"].add(vk.crash(1.1, 3.5), t)
            T["timp"].add(vk.timpani(C2, 1.2), t)
            T["bell"].add(vk.church_bell(hz(midi("C3")), 1.0), t)
            T["choir"].add(vk.choir([hz(m) for m in (midi("C4"), midi("Eb4"), midi("G4"), midi("C5"))],
                                    BAR * 2 - 0.3, 1.2, "a", singers=5, attack=0.05, release=0.8), t)
            low_strings(PROG_I, t, T["strings_lo"], 1.0)
            # Herzschlag ab der 3 des ersten Takts
            T["heart"].add(heart_lub(0.8), t + 2 * BEAT)
            T["heart"].add(heart_dub(0.8), t + 2 * BEAT + DUB)
            heartbeat(1, t + BAR, T, 0.9)
            # zweiter Takt: Ostinato schwillt an, Paukenwirbel in das Thema
            ostinato(["Cm"], t + BAR, T["strings"], 0.55)
            T["timp"].add(vk.timp_roll(G2, BAR, 0.1, 0.9), t + BAR)
        elif name in ("A", "D"):
            climax = name == "D"
            melody_lines = vk.MEL_A
            vk.melody(melody_lines, t, BEAT, T["horn"], horn, vel=1.0)
            vk.melody(vk.COUNTER_A, t, BEAT, T["trombone"], trombone, vel=0.75)
            ostinato(PROG_A, t, T["strings"], 0.9 if climax else 0.75)
            bass_pedal(PROG_A, t, T["bass"], 0.9)
            heartbeat(bars, t, T, 1.0, double=climax)
            choir_chords(PROG_A, t, T["choir"], 0.75 if climax else 0.55, "o")
            if climax:
                vk.melody(melody_lines, t, BEAT, T["trumpet"], trumpet, octave_shift=1, vel=0.7)
                vk.melody(melody_lines, t, BEAT, T["choir_mel"], choir_line, octave_shift=0, vel=1.0)
                high_strings(PROG_A, t, T["strings_hi"], 0.8)
                march(bars, t, T["snare"], 0.9)
                for b in (0, 4):
                    T["fx"].add(vk.braam(vk.root_of(PROG_A[b], 2), 3.0, 0.8), t + b * BAR)
                    T["fx"].add(vk.crash(1.0, 3.0), t + b * BAR)
            else:
                T["fx"].add(vk.crash(0.8, 3.0), t)
            for b in (0, 4):
                T["bell"].add(vk.church_bell(hz(vk.root_of(PROG_A[b], 3)), 0.6, decay=1.7), t + b * BAR)
                T["timp"].add(vk.timpani(hz(vk.root_of(PROG_A[b], 2)), 1.0), t + b * BAR)
            # Auftakt-Wirbel in den naechsten Teil
            T["timp"].add(vk.timp_roll(G2, BAR / 2, 0.15, 0.8), t + (bars - 0.5) * BAR)
        elif name == "B":
            vk.melody(vk.MEL_B, t, BEAT, T["trumpet"], trumpet, vel=0.95)
            vk.melody(vk.MEL_B, t, BEAT, T["horn"], horn, octave_shift=-1, vel=0.8)
            vk.melody(vk.MEL_B, t, BEAT, T["choir_mel"], choir_line, vel=0.8)
            choir_chords(PROG_B, t, T["choir"], 1.0, "a")
            ostinato(PROG_B, t, T["strings"], 0.9)
            high_strings(PROG_B, t, T["strings_hi"], 0.7)
            bass_pedal(PROG_B, t, T["bass"], 1.0)
            heartbeat(bars, t, T, 1.0, double=True)
            march(bars, t, T["snare"], 0.8)
            for b in (0, 4):
                T["fx"].add(vk.braam(vk.root_of(PROG_B[b], 2), 3.0, 0.9), t + b * BAR)
                T["fx"].add(vk.crash(1.0, 3.0), t + b * BAR)
                T["timp"].add(vk.timpani(hz(vk.root_of(PROG_B[b], 2)), 1.1), t + b * BAR)
            T["timp"].add(vk.timp_roll(G2, BAR, 0.1, 0.7), t + (bars - 1) * BAR)
        elif name == "C":
            # Mochis Buehne: Summchor, Glocke, das Herz - sonst nichts
            choir_chords(PROG_C, t, T["choir"], 0.9, "u", low=midi("C3"), high=midi("C5"))
            low_strings(PROG_C, t, T["strings_lo"], 0.8)
            heartbeat(bars, t, T, 0.8)
            for b in range(0, bars, 2):
                T["bell"].add(vk.church_bell(hz(vk.root_of(PROG_C[b], 3)), 0.8, decay=1.7), t + b * BAR)
            # zweite Haelfte: das Ostinato kommt leise zurueck, am Ende Wirbel + Sog
            ostinato(PROG_C[4:], t + 4 * BAR, T["strings"], 0.45)
            T["snare"].add(vk.snare_roll(2 * BAR, BEAT, 0.05, 0.9), t + 6 * BAR)
            T["fx"].add(vk.riser(2 * BAR, 0.9), t + 6 * BAR)
            T["timp"].add(vk.timp_roll(G2, BAR, 0.2, 1.0), t + 7 * BAR)
        elif name == "turn":
            for b, ch in enumerate(PROG_T):
                T["fx"].add(vk.braam(vk.root_of(ch, 2), BAR, 1.0), t + b * BAR)
                T["timp"].add(vk.timpani(hz(vk.root_of(ch, 2)), 1.2), t + b * BAR)
            vk.melody([[("C5", 2), ("Eb5", 2)], [("D5", 2), ("B4", 2)]], t, BEAT, T["trumpet"], trumpet, vel=0.9)
            vk.melody([[("Ab3", 4)], [("G3", 4)]], t, BEAT, T["trombone"], trombone, vel=0.9)
            choir_chords(PROG_T, t, T["choir"], 1.1, "a")
            ostinato(PROG_T, t, T["strings"], 0.9)
            heartbeat(bars, t, T, 1.0, double=True)
            T["snare"].add(vk.snare_roll(BAR, BEAT, 0.1, 1.0), t + BAR)
            T["fx"].add(vk.riser(BAR, 1.0), t + BAR)
        t += bars * BAR
    return T, length


# ================================================================ Finale

def compose_finale():
    """Das Herz birst (Blitz = 0 s): Einschlag, Fanfare in C-Dur, Chor + Glocke klingen aus."""
    length = 10.0
    T = tracks(length)
    C2 = hz(midi("C2"))
    T["fx"].add(vk.boom(1.2), 0.0)
    T["fx"].add(vk.crash(1.3, 4.0), 0.0)
    T["fx"].add(vk.braam(midi("C2"), 3.5, 1.0), 0.0)
    T["timp"].add(vk.timpani(C2, 1.3), 0.0)

    # Fanfare: das Motiv des Verkohlten, aber in Dur
    fan = [[("G4", .5), ("C5", .5), ("E5", 1), ("G5", 4)]]
    t0 = 0.8
    vk.melody(fan, t0, BEAT, T["trumpet"], trumpet, vel=1.0, legato=0.98)
    vk.melody(fan, t0, BEAT, T["horn"], horn, octave_shift=-1, vel=0.9, legato=0.98)
    vk.melody(fan, t0, BEAT, T["choir_mel"], choir_line, vel=0.9, legato=0.98)
    T["timp"].add(vk.timp_roll(hz(midi("G2")), 0.75, 0.2, 0.9), 0.05)
    t1 = t0 + 2 * BEAT
    T["timp"].add(vk.timpani(C2, 1.2), t1)
    T["fx"].add(vk.crash(1.0, 4.0), t1)
    T["bell"].add(vk.church_bell(hz(midi("C3")), 1.1), t1)
    T["bell"].add(vk.church_bell(hz(midi("G3")), 0.7), t1 + 2 * BEAT)
    major = [hz(midi(n)) for n in ("C3", "G3", "C4", "E4", "G4", "C5")]
    T["choir"].add(vk.choir(major[2:], 5.6, 1.3, "a", singers=5, attack=0.3, release=2.5), t1)
    T["strings_hi"].add(vk.strings_long([hz(midi(n)) for n in ("E5", "G5", "C6")], 5.5, 1.0), t1)
    T["strings_lo"].add(vk.strings_long(major[:2], 5.5, 1.1), t1)
    T["trombone"].add(trombone(hz(midi("C3")), 4.5, 0.9), t1)
    T["trombone"].add(trombone(hz(midi("G3")), 4.5, 0.7), t1)
    return T, length


def main():
    T, length = compose()
    vk.apply_gains(T, GAINS)
    data = vk.mix(T, length, ir_seconds=3.6, ir_decay=1.7, wet_gain=0.6, drive=1.1)
    vk.write_wav(OUT, data, "herzkammer.wav")
    print("  %d Takte, %d BPM, Schleife %.4f s" % (len(song_chords()), BPM, length))
    print("SongSheet:", " ".join(song_chords()))

    T, length = compose_finale()
    vk.apply_gains(T, GAINS)
    data = vk.mix(T, None, ir_seconds=3.6, ir_decay=1.5, wet_gain=0.65, drive=1.1)
    # Hallfahne kuerzen und sanft auslaufen lassen (kein Loop)
    data = data[:int(11.5 * SR)]
    n = len(data)
    tail = int(1.5 * SR)
    data[n - tail:] *= np.linspace(1, 0, tail)[:, None] ** 2
    vk.write_wav(OUT_FINALE, data, "herzkammer_finale.wav")


if __name__ == "__main__":
    main()
