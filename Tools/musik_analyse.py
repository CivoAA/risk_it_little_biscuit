"""
Musik-Analyse fuer die Mochi-Melodie (SongSheet.cs): Tempo, erste Eins,
Tonart, Akkorde pro Takt - und fuer Stuecke mit unklarer Harmonie die
sicheren Toene pro Schlag ("Maske").

    python Tools/musik_analyse.py <datei> --bpm 110 140
        -> genaues Tempo + Phase (Raster-Suche auf der Onset-Huellkurve)
    python Tools/musik_analyse.py <datei> --takt <bpm> <phase> [--eins 0..3]
        -> Tonart + Akkord je Takt (diatonische Dreiklaenge + Bass)
    python Tools/musik_analyse.py <datei> --maske <bpm> <phase> <schlaege>
        -> je Schlag drei Toene ohne Halbtonnachbarn, Grundton aus dem Bass,
           als String fuer SongSheet.beatChords

Phase = Sekunden bis zum ersten Schlag. Die Rastersuche liegt ~1,5 ms zu
frueh (an eis.wav geprueft, das genau bei 0 beginnt) - schon eingerechnet.
Wenn das Tempo stark schwankt oder die Achtel lauter sind als die Viertel:
mit doppeltem BPM-Bereich suchen und an den Bassschlaegen pruefen.

Braucht numpy, soundfile, librosa.
"""

import sys

import numpy as np
import librosa
import soundfile as sf

N = ['C', 'C#', 'D', 'Eb', 'E', 'F', 'F#', 'G', 'Ab', 'A', 'Bb', 'B']
BIAS = 0.0015


def load(path):
    y, sr = sf.read(path, always_2d=True)
    return y.mean(1).astype(np.float32), sr


def find_tempo(y, sr, lo, hi):
    hop = 128
    env = librosa.onset.onset_strength(y=y, sr=sr, hop_length=hop)
    t = librosa.frames_to_time(np.arange(len(env)), sr=sr, hop_length=hop)
    best = None
    for bpm in np.arange(lo, hi, 0.01):
        beat = 60 / bpm
        z = (env * np.exp(2j * np.pi * (t % beat) / beat)).sum() / env.sum()
        if best is None or abs(z) > best[0]:
            best = (abs(z), bpm, (np.angle(z) / (2 * np.pi)) % 1 * beat)
    strength, bpm, phase = best
    print("BPM %.2f  Phase %.4f s  (Staerke %.3f)  Schlaege im Clip %.2f"
          % (bpm, phase + BIAS, strength, len(y) / sr / (60 / bpm)))


def beat_chroma(y, sr, bpm, phase):
    hop = 512
    yh, _ = librosa.effects.hpss(y, margin=2.0)
    C = librosa.feature.chroma_cqt(y=yh, sr=sr, hop_length=hop, fmin=librosa.note_to_hz('C3'), n_octaves=4)
    Cb = librosa.feature.chroma_cqt(y=yh, sr=sr, hop_length=hop, fmin=librosa.note_to_hz('C1'), n_octaves=2)
    n = min(C.shape[1], Cb.shape[1])
    beat = 60 / bpm
    nb = int((len(y) / sr - phase) / beat)
    up, lo = [], []
    for i in range(nb):
        a = int((phase + i * beat) * sr / hop)
        b = min(n, int((phase + (i + 1) * beat) * sr / hop))
        up.append(C[:, a:b].mean(1))
        lo.append(Cb[:, a:b].mean(1))
    return np.array(up), np.array(lo)


def chords_per_bar(y, sr, bpm, phase, first):
    bc, bb = beat_chroma(y, sr, bpm, phase)
    tot = bc.sum(0)
    maj = np.array([6.35, 2.23, 3.48, 2.33, 4.38, 4.09, 2.52, 5.19, 2.39, 3.66, 2.29, 2.88])
    mnr = np.array([6.33, 2.68, 3.52, 5.38, 2.6, 3.53, 2.54, 4.75, 3.98, 2.69, 3.34, 3.17])
    keys = [(np.corrcoef(np.roll(maj, k), tot)[0, 1], k, 0) for k in range(12)]
    keys += [(np.corrcoef(np.roll(mnr, k), tot)[0, 1], k, 1) for k in range(12)]
    keys.sort(reverse=True)
    _, r, minor = keys[0]
    print("Tonart:", N[r], "Moll" if minor else "Dur", " (naechste: %s)"
          % ", ".join("%s %s" % (N[k], "Moll" if m else "Dur") for _, k, m in keys[1:3]))
    scale = [0, 2, 3, 5, 7, 8, 10] if minor else [0, 2, 4, 5, 7, 9, 11]

    cands = {}

    def add(root, q):
        iv = {'': [0, 4, 7], 'm': [0, 3, 7], 'dim': [0, 3, 6]}[q]
        v = np.zeros(12)
        v[[(root + k) % 12 for k in iv]] = 1
        cands[N[root % 12] + q] = (v / np.linalg.norm(v), root % 12)

    for i, d in enumerate(scale):
        third = (scale[(i + 2) % 7] - d) % 12
        fifth = (scale[(i + 4) % 7] - d) % 12
        add(r + d, '' if third == 4 else ('dim' if fifth == 6 else 'm'))
    if minor:
        add(r + 7, '')

    novelty = np.r_[0, np.linalg.norm(np.diff(bb / np.linalg.norm(bb, axis=1, keepdims=True), axis=0), axis=1)]
    print("Bass-Wechsel je Schlag im Takt (hoch = Eins):", [round(float(novelty[k::4].mean()), 3) for k in range(4)])

    names = []
    beat = 60 / bpm
    for bar in range((len(bc) - first) // 4):
        s = first + bar * 4
        c = bc[s:s + 4].sum(0)
        c = c / (np.linalg.norm(c) + 1e-9)
        b = bb[s:s + 4].sum(0)
        b = b / (b.max() + 1e-9)
        score = {k: c @ v + 0.35 * b[root] for k, (v, root) in cands.items()}
        best = sorted(score, key=score.get, reverse=True)
        names.append(best[0])
        print("Takt %2d  %6.2f s  %-5s (Abstand %.2f)" % (bar, phase + s * beat, best[0], score[best[0]] - score[best[1]]))
    print(" ".join(names))


def masks(y, sr, bpm, phase, beats):
    bc, bb = beat_chroma(y, sr, bpm, phase)
    out = []
    for i in range(min(beats, len(bc))):
        c = bc[i] / (bc[i].max() + 1e-9)
        s = [c[p] - 0.6 * max(c[(p + 1) % 12], c[(p - 1) % 12]) for p in range(12)]
        sel = []
        for p in np.argsort(s)[::-1]:
            if all(min((p - q) % 12, (q - p) % 12) > 1 for q in sel):
                sel.append(int(p))
            if len(sel) == 3:
                break
        root = max(sel, key=lambda p: bb[i][p])
        tones = [root] + [p for p in sel if p != root]
        out.append("".join("0123456789ab"[p] for p in tones))
    lines = [" ".join(out[k:k + 16]) for k in range(0, len(out), 16)]
    print("beatChords (Grundton zuerst, Tonklasse hex):")
    for line in lines:
        print('    "%s ",' % line)


def main():
    path = sys.argv[1]
    y, sr = load(path)
    a = sys.argv
    if "--bpm" in a:
        i = a.index("--bpm")
        find_tempo(y, sr, float(a[i + 1]), float(a[i + 2]))
    elif "--takt" in a:
        i = a.index("--takt")
        first = int(a[a.index("--eins") + 1]) if "--eins" in a else 0
        chords_per_bar(y, sr, float(a[i + 1]), float(a[i + 2]), first)
    elif "--maske" in a:
        i = a.index("--maske")
        masks(y, sr, float(a[i + 1]), float(a[i + 2]), int(a[i + 3]))
    else:
        print(__doc__)


if __name__ == "__main__":
    main()
