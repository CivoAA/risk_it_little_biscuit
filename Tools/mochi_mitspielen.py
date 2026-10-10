"""
Mochi spielt mit - Vorhoeren ohne Unity.

Rendert die Level-Musik mit Mochis Melodie darueber, so wie MochiMelody.cs sie
im Spiel spielt (MusicClock-Raster + MelodyWriter + Akkord-Ring + Glitzern),
als ob ueberall Gegner stehen. Dazu zum Vergleich die alte Fassung (feste
C-Dur-Melodie im eigenen Tempo, 0,55 s pro Schlag).

Ausserdem eine Messung: wie viel Energie hat die Musik in dem Moment auf den
Halbtoenen direkt neben Mochis Ton ("Reibung")? Klein = passt.

Die Logik ist eine Abschrift von Assets/Scripts/Weapons/MochiMelody/
(SongSheet.cs, MusicClock.cs, MelodyWriter.cs, MochiMelody.Plan) - wer dort
etwas aendert, aendert es hier mit.

Aufruf aus dem Projektordner:
    python Tools/mochi_mitspielen.py                 # alle drei, Viertel
    python Tools/mochi_mitspielen.py --achtel        # Achtel (Cooldown-Buff)
    python Tools/mochi_mitspielen.py --sekunden 40

Ausgabe: Tools/out/mochi_mitspielen/<stueck>_neu.wav / _alt.wav
Braucht numpy + soundfile (mp3), fuer die Messung librosa.
"""

import math
import os
import sys

import numpy as np
import soundfile as sf

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Tools", "out", "mochi_mitspielen")
SR = 44100

LETTERS = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}
QUAL = {"": [0, 4, 7], "m": [0, 3, 7], "sus": [0, 5, 7], "7": [0, 4, 7, 10],
        "maj7": [0, 4, 7, 11], "m7": [0, 3, 7, 10]}


class Chord:
    def __init__(self, name):
        r = LETTERS[name[0]]
        i = 1
        if i < len(name) and name[i] == "#":
            r += 1; i += 1
        elif i < len(name) and name[i] == "b":
            r -= 1; i += 1
        self.root = r % 12
        q = name[i:]
        self.tones = [(self.root + s) % 12 for s in QUAL[q]]
        self.minor = q in ("m", "m7")
        self.name = name

    def has(self, p):
        return p % 12 in self.tones

    @staticmethod
    def hexed(token):
        c = Chord.__new__(Chord)
        c.tones = [int(x, 16) for x in token]
        c.root = c.tones[0]
        c.minor = c.has(c.root + 3) and not c.has(c.root + 4)
        c.name = token
        return c


def osaka_masks():
    """beatChords von "Off to Osaka" direkt aus SongSheet.cs - eine Quelle fuer beide."""
    import re
    src = open(os.path.join(ROOT, "Assets/Scripts/Weapons/MochiMelody/SongSheet.cs"), encoding="utf-8").read()
    block = src[src.index("beatChords = Masks("):]
    block = block[:block.index(")")]
    return [Chord.hexed(t) for t in re.findall(r"\b[0-9a-b]{3}\b", block)]


def chords(*parts):
    return [Chord(n) for p in parts for n in p.split()]


D_PENTA = [2, 5, 7, 9, 0]
B_PENTA = [11, 2, 4, 6, 9]

G_PENTA = [7, 9, 11, 2, 4]
C_MIN_PENTA = [0, 3, 5, 7, 10]

SHEETS = {
    "kueche": dict(file="Assets/music/kueche.wav", bpm=120.0, first=0.0, bars=56, seed=0, scale=G_PENTA,
                   chords=chords("G Em C D", "G Em C D G Em Am D", "G Em C D G Em Am D",
                                 "C D Bm Em Am D G G", "Em C G D Em C Am D",
                                 "G Em C D G Em Am D", "C D Bm Em Am D G G", "G Em Am D")),
    "eis": dict(file="Assets/music/eis.wav", bpm=126.0, first=0.0, bars=56, seed=3, scale=D_PENTA,
                chords=chords("Dm Bb Gm A", "Dm Bb F C Dm Bb Gm A", "Dm Bb F C Dm Bb Gm A",
                              "F C Dm Bb F C Bb A", "Bbmaj7 Am7 Gm7 Asus Bbmaj7 Am7 Gm7 A",
                              "Dm Bb F C Dm Bb Gm A", "F C Dm Bb F C Bb A", "Dm Bb Gm A")),
    "vulkan": dict(file="Assets/music/vulkan.wav", bpm=124.0, first=0.0, bars=56, seed=4, scale=C_MIN_PENTA,
                   chords=chords("Cm Cm Ab G", "Cm Ab Bb G Cm Ab Fm G", "Cm Ab Bb G Cm Ab Fm G",
                                 "Ab Bb Eb Cm Ab Bb G G", "Fm Cm Ab G Fm Cm Db G",
                                 "Cm Ab Bb G Cm Ab Fm G", "Ab Bb Eb Cm Ab Bb G G", "Cm Ab Fm G")),
    "herzkammer": dict(file="Assets/Resources/Music/herzkammer.wav", bpm=90.0, first=0.0, bars=36, seed=5,
                       scale=C_MIN_PENTA,
                       chords=chords("Cm Cm", "Cm Ab Bb G Cm Ab Fm G", "Ab Bb Eb Cm Ab Bb G G",
                                     "Fm Cm Ab G Fm Cm Db G", "Cm Ab Bb G Cm Ab Fm G", "Ab G")),
    "wald": dict(file="Assets/music/wald.wav", bpm=132.01, first=0.024, bars=16, seed=1, scale=D_PENTA,
                 chords=chords("Dm Bb F C")),
    "osaka": dict(file="Assets/Audio/Off to Osaka.mp3", bpm=117.0, first=0.070, bars=53, seed=2, scale=B_PENTA,
                  chords=chords("Bm"), beat_chords=osaka_masks()),
}

# ------------------------------------------------------------------ MelodyWriter (Abschrift)

LOW, HIGH = -5, 14
A_UP, A_DN, S_UP, S_DN, NB, LEAP, HOME = range(7)
PHRASES = [
    [[A_UP, A_UP], [S_UP, A_DN], [A_DN, S_DN], [NB, HOME]],
    [[S_UP, S_UP], [LEAP, S_DN], [A_UP, A_DN], [S_DN, HOME]],
    [[A_UP, S_UP], [A_DN, A_DN], [NB, A_UP], [A_DN, HOME]],
    [[LEAP, A_DN], [S_DN, S_UP], [A_UP, A_UP], [S_DN, HOME]],
]
ARCHES = [[2, 7, 9, 4], [4, 2, 9, 7], [0, 5, 11, 4]]


class Writer:
    def __init__(self):
        self.last = 4
        self.voicing = None

    def note(self, sheet, ch, motif, index):
        phrase = (sheet["seed"] + (motif // 4) % 2 + (motif // 16) * 2) % len(PHRASES)
        moves = PHRASES[phrase][motif % 4]
        if index == 0:
            arch = ARCHES[(sheet["seed"] + motif // 8) % len(ARCHES)]
            target = arch[motif % 4] + (2 if (motif // 4) % 2 == 1 else 0)
            p = nearest_chord_tone(ch, target, self.last)
        else:
            p = apply(moves[index - 1], sheet, ch, self.last)
        while p > HIGH:
            p -= 12
        while p < LOW:
            p += 12
        self.last = p
        return p

    def voice(self, ch):
        upper = min(3, len(ch.tones))
        skip = 1 if len(ch.tones) > 3 else 0
        res = [ch.root - 12]
        for i in range(upper):
            pc = ch.tones[i + skip]
            ref = self.voicing[i + 1] if self.voicing and i + 1 < len(self.voicing) else 3 + i * 3
            p = nearest_pc(pc, ref)
            while p < -3:
                p += 12
            while p > 12:
                p -= 12
            res.append(p)
        self.voicing = res
        return res


def chime_shift(ch):
    pc = ch.root + 3 if ch.minor else ch.root
    if not ch.minor and not (ch.has(ch.root + 4) and ch.has(ch.root + 7)):
        pc = ch.root + 7
    s = pc % 12
    return s - 12 if s > 6 else s


def nearest_chord_tone(ch, target, last):
    best, bs = target, 1e9
    for p in range(LOW, HIGH + 1):
        if not ch.has(p):
            continue
        s = abs(p - target) + 0.5 * abs(p - last)
        if s < bs:
            bs, best = s, p
    return best


def arp(ch, frm, count, d):
    p = frm
    for _ in range(count):
        p += d
        while not ch.has(p) and abs(p - frm) < 24:
            p += d
    return p


def safe(sheet, ch, p):
    if ch.has(p):
        return True
    if p % 12 not in sheet["scale"]:
        return False
    return not ch.has(p + 1) and not ch.has(p - 1)


def scale_step(sheet, ch, frm, d):
    p = frm + d
    while abs(p - frm) <= 5:
        if safe(sheet, ch, p):
            return p
        p += d
    return arp(ch, frm, 1, d)


def nearest_pc(pc, near):
    base = math.floor(near / 12) * 12
    best = base + pc % 12
    for o in (-12, 0, 12):
        p = base + pc % 12 + o
        if abs(p - near) < abs(best - near):
            best = p
    return best


def apply(m, sheet, ch, frm):
    if m == A_UP: return arp(ch, frm, 1, 1)
    if m == A_DN: return arp(ch, frm, 1, -1)
    if m == LEAP: return arp(ch, frm, 2, 1)
    if m in (S_UP, NB): return scale_step(sheet, ch, frm, 1)
    if m == S_DN: return scale_step(sheet, ch, frm, -1)
    if m == HOME: return nearest_pc(ch.root, frm)
    return frm


# ------------------------------------------------------------------ Klang (wie melody_tone / melody_chime)

def tone_sample():
    n = int(SR * 0.55)
    t = np.arange(n) / SR
    f = 523.25
    env = np.minimum(1, t / 0.004) * np.exp(-t * 7.5)
    s = np.sin(2 * np.pi * f * t) + 0.28 * np.sin(2 * np.pi * f * 4 * t) * np.exp(-t * 30) + 0.12 * np.sin(2 * np.pi * f * 2 * t)
    return 0.55 * env * s


def chime_sample():
    n = int(SR * 0.7)
    out = np.zeros(n)
    for f, st in ((2093.0, 0.0), (2637.0, 0.05), (3136.0, 0.1)):
        k = int(st * SR)
        t = np.arange(n - k) / SR
        env = np.minimum(1, t / 0.003) * np.exp(-t * 9)
        out[k:] += 0.16 * env * (np.sin(2 * np.pi * f * t) + 0.3 * np.sin(2 * np.pi * f * 2.76 * t))
    return out


TONE = tone_sample()
CHIME = chime_sample()


def pitched(sample, semis):
    """Wie AudioSource.pitch: schneller abspielen = hoeher und kuerzer."""
    r = 2 ** (semis / 12)
    idx = np.arange(0, len(sample) - 1, r)
    return np.interp(idx, np.arange(len(sample)), sample)


def add(buf, sig, at, vol):
    k = int(round(at * SR))
    if k >= len(buf):
        return
    m = min(len(sig), len(buf) - k)
    buf[k:k + m] += sig[:m] * vol


# ------------------------------------------------------------------ Ablauf

def events_new(sheet, seconds, step_beats):
    """(Zeit, [Halbtoene], Lautstaerke, ist_ton, Akkord) wie MochiMelody.Plan im Takt.
    Laeuft wie im Spiel ueber Schleifen weiter (MusicClock: Raster pro Schleife)."""
    beat = 60.0 / sheet["bpm"]
    clip_len = sf.info(os.path.join(ROOT, sheet["file"])).duration
    w = Writer()
    motif = 0
    ev = []
    shift = 1 if step_beats < 1 else 0
    loop = 0
    while loop * clip_len < seconds:
        j = 0
        while True:
            b = j * step_beats
            t = sheet["first"] + b * beat
            if b >= sheet["bars"] * 4 or t >= clip_len:
                break
            t += loop * clip_len
            if t > seconds:
                return ev
            if sheet.get("beat_chords"):
                ch = sheet["beat_chords"][min(int(math.floor(b + 1e-6)), len(sheet["beat_chords"]) - 1)]
            else:
                ch = sheet["chords"][int(math.floor(b / 4 + 1e-6)) % len(sheet["chords"])]
            slot = (j + shift) % 4
            if slot < 3:
                ev.append((t, [w.note(sheet, ch, motif, slot)], 0.2, True, ch))
            else:
                motif += 1
                ev.append((t, w.voice(ch), 0.11, False, ch))
                ev.append((t, ["chime", chime_shift(ch)], 0.16, False, ch))
            j += 1
        loop += 1
    return ev


TUNE = [[0, 4, 7], [9, 7, 4], [5, 4, 2], [2, 7, 11]]
OLD_CHORDS = [[-12, 0, 4, 7], [-15, -3, 0, 4], [-19, -7, -3, 0], [-17, -5, -1, 2]]


def events_old(seconds, start=0.6, step=0.55):
    ev = []
    t, beat, bar = start, 0, 0
    while t < seconds:
        if beat < 3:
            ev.append((t, [TUNE[bar % 4][beat]], 0.22, True, None))
        else:
            ev.append((t, OLD_CHORDS[bar % 4], 0.13, False, None))
            ev.append((t, ["chime", 0], 0.2, False, None))
            bar += 1
        beat = (beat + 1) % 4
        t += step
    return ev


def render(music, ev, mochi_gain=1.0):
    buf = music.copy()
    voice = np.zeros(len(buf))
    for t, notes, vol, _, _ in ev:
        if notes and notes[0] == "chime":
            add(voice, pitched(CHIME, notes[1]), t, vol)
            continue
        for p in notes:
            add(voice, pitched(TONE, p), t, vol)
    out = buf + voice[:, None] * mochi_gain
    peak = np.abs(out).max()
    if peak > 0.99:
        out *= 0.99 / peak
    return out


def friction(music_mono, ev):
    """Mittlere Reibung: Chroma-Energie einen Halbton neben Mochis Toenen / Energie auf dem Ton."""
    import librosa
    C = librosa.feature.chroma_cqt(y=music_mono.astype(np.float32), sr=SR, hop_length=512,
                                   fmin=librosa.note_to_hz("C3"), n_octaves=4)
    rub, hit, n = 0.0, 0.0, 0
    for t, notes, _, is_tone, _ in ev:
        if not is_tone:
            continue
        f = int(t * SR / 512)
        frames = C[:, f:f + 8]
        if frames.shape[1] == 0:
            continue
        c = frames.mean(1)
        c = c / (c.max() + 1e-9)
        pc = notes[0] % 12
        rub += max(c[(pc + 1) % 12], c[(pc - 1) % 12])
        hit += c[pc]
        n += 1
    return rub / max(n, 1), hit / max(n, 1)


def main():
    seconds = float(sys.argv[sys.argv.index("--sekunden") + 1]) if "--sekunden" in sys.argv else 32.0
    step = 0.5 if "--achtel" in sys.argv else 1.0
    measure = "--ohne-messung" not in sys.argv
    os.makedirs(OUT, exist_ok=True)
    for name, sheet in SHEETS.items():
        if not os.path.exists(os.path.join(ROOT, sheet["file"])):
            print("%-6s fehlt: %s" % (name, sheet["file"]))
            continue
        music, sr = sf.read(os.path.join(ROOT, sheet["file"]), always_2d=True)
        assert sr == SR, sr
        reps = int(math.ceil(seconds * SR / len(music)))
        music = np.tile(music, (reps, 1))[:int(seconds * SR)] * 0.8
        new = events_new(sheet, seconds, step)
        old = events_old(seconds)
        tag = "_achtel" if step < 1 else ""
        sf.write(os.path.join(OUT, name + "_neu" + tag + ".wav"), render(music, new), SR)
        sf.write(os.path.join(OUT, name + "_alt.wav"), render(music, old), SR)
        line = "%-6s %3d Mochi-Toene" % (name, sum(1 for e in new if e[3]))
        if measure:
            mono = music.mean(1)
            rn, hn = friction(mono, new)
            ro, ho = friction(mono, old)
            line += "   Reibung alt %.2f -> neu %.2f   Treffer alt %.2f -> neu %.2f" % (ro, rn, ho, hn)
        print(line)
    print("->", OUT)


if __name__ == "__main__":
    main()
