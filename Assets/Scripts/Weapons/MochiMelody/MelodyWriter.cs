using UnityEngine;

/// <summary>
/// Mochis Melodie-Schreiber: denkt sich zu jedem Akkord des Stuecks drei
/// Toene aus, die passen. Auf der ersten Note eines Motivs steht immer ein
/// Akkordton, dazwischen duerfen Toene aus der Pentatonik des Stuecks
/// stehen - aber nie einer, der einen Halbton neben einem Akkordton liegt.
///
/// Damit es nach einer Melodie klingt und nicht nach Zufall, kommen die
/// Motive aus festen Phrasen (4 Motive = 4 Takte), die sich wiederholen;
/// welche Phrasen, haengt am Stueck (<see cref="SongSheet.seed"/>). Gleiches
/// Stueck, gleiches Mochi-Lied.
///
/// Toene sind Halbtoene ueber C5 (der Ton von melody_tone.wav).
/// </summary>
public class MelodyWriter
{
    private const int Lowest = -5;    // G4
    private const int Highest = 14;   // D6

    private enum Move { ArpUp, ArpDown, StepUp, StepDown, Neighbor, Leap, Home }

    /// <summary>Eine Phrase = vier Motive, je Motiv die Bewegung von Note 1 zu 2 und 2 zu 3.</summary>
    private static readonly Move[][][] Phrases =
    {
        new[]
        {
            new[] { Move.ArpUp, Move.ArpUp },
            new[] { Move.StepUp, Move.ArpDown },
            new[] { Move.ArpDown, Move.StepDown },
            new[] { Move.Neighbor, Move.Home },
        },
        new[]
        {
            new[] { Move.StepUp, Move.StepUp },
            new[] { Move.Leap, Move.StepDown },
            new[] { Move.ArpUp, Move.ArpDown },
            new[] { Move.StepDown, Move.Home },
        },
        new[]
        {
            new[] { Move.ArpUp, Move.StepUp },
            new[] { Move.ArpDown, Move.ArpDown },
            new[] { Move.Neighbor, Move.ArpUp },
            new[] { Move.ArpDown, Move.Home },
        },
        new[]
        {
            new[] { Move.Leap, Move.ArpDown },
            new[] { Move.StepDown, Move.StepUp },
            new[] { Move.ArpUp, Move.ArpUp },
            new[] { Move.StepDown, Move.Home },
        },
    };

    /// <summary>Wo das Motiv ungefaehr anfaengt - ein Bogen ueber die Phrase.</summary>
    private static readonly int[][] Arches =
    {
        new[] { 2, 7, 9, 4 },
        new[] { 4, 2, 9, 7 },
        new[] { 0, 5, 11, 4 },
    };

    private int last = 4;
    private int[] voicing;

    public void Reset()
    {
        last = 4;
        voicing = null;
    }

    /// <summary>
    /// Ton fuer Note <paramref name="index"/> (0-2) im Motiv
    /// <paramref name="motif"/> (zaehlt Akkorde seit Beginn).
    /// </summary>
    public int Note(SongSheet sheet, Chord chord, int motif, int index)
    {
        // Zwei Phrasen im Wechsel, nach 8 Motiven (Frage + Antwort) die naechsten zwei
        int phrase = (sheet.seed + (motif / 4) % 2 + (motif / 16) * 2) % Phrases.Length;
        Move[] moves = Phrases[phrase][motif % 4];

        int pitch;
        if (index == 0)
        {
            int[] arch = Arches[(sheet.seed + motif / 8) % Arches.Length];
            // die Antwort-Phrase liegt etwas hoeher
            int target = arch[motif % 4] + ((motif / 4) % 2 == 1 ? 2 : 0);
            pitch = NearestChordTone(chord, target, last);
        }
        else
        {
            pitch = Apply(moves[index - 1], sheet, chord, last);
        }

        pitch = Fold(pitch);
        last = pitch;
        return pitch;
    }

    private int Apply(Move move, SongSheet sheet, Chord chord, int from)
    {
        switch (move)
        {
            case Move.ArpUp: return ChordToneAbove(chord, from, 1);
            case Move.ArpDown: return ChordToneBelow(chord, from, 1);
            case Move.Leap: return ChordToneAbove(chord, from, 2);
            case Move.StepUp: return ScaleStep(sheet, chord, from, +1);
            case Move.StepDown: return ScaleStep(sheet, chord, from, -1);
            case Move.Neighbor: return ScaleStep(sheet, chord, from, +1);
            case Move.Home:
                // zurueck zum Grundton (Schluss der Phrase), nah an der letzten Note
                return NearestPitchClass(chord.root, from);
            default: return from;
        }
    }

    // ------------------------------------------------------------------
    //  Akkord-Ring: Bass + bis zu drei Toene, eng gefuehrt
    // ------------------------------------------------------------------

    /// <summary>Die Toene des Akkord-Rings: Grundton tief, darueber die anderen Akkordtoene.</summary>
    public int[] Voicing(Chord chord)
    {
        int upperCount = Mathf.Min(3, chord.tones.Length);
        int skip = chord.tones.Length > 3 ? 1 : 0;   // Vierklang: Grundton oben weglassen
        int[] result = new int[upperCount + 1];
        result[0] = chord.root - 12;                 // C4 .. H4 unter dem Rest

        for (int i = 0; i < upperCount; i++)
        {
            int pc = chord.tones[i + skip];
            int reference = voicing != null && i + 1 < voicing.Length ? voicing[i + 1] : 3 + i * 3;
            int p = NearestPitchClass(pc, reference);
            while (p < -3) p += 12;
            while (p > 12) p -= 12;
            result[i + 1] = p;
        }
        voicing = result;
        return result;
    }

    /// <summary>Halbtoene fuer das Glitzern (C-Dur-Dreiklang im Sample) passend zum Akkord.</summary>
    public static int ChimeShift(Chord chord)
    {
        // Dur: auf den Grundton. Moll: Dur-Dreiklang der kleinen Terz (macht m7). Sonst Quinte.
        int pc = chord.minor ? chord.root + 3 : chord.root;
        if (!chord.minor && !IsMajorTriad(chord)) pc = chord.root + 7;
        int shift = ((pc % 12) + 12) % 12;
        if (shift > 6) shift -= 12;
        return shift;
    }

    private static bool IsMajorTriad(Chord chord)
    {
        return chord.Contains(chord.root + 4) && chord.Contains(chord.root + 7);
    }

    // ------------------------------------------------------------------
    //  Tonmaterial
    // ------------------------------------------------------------------

    private static int NearestChordTone(Chord chord, int target, int last)
    {
        int best = target;
        float bestScore = float.MaxValue;
        for (int p = Lowest; p <= Highest; p++)
        {
            if (!chord.Contains(p)) continue;
            float score = Mathf.Abs(p - target) + 0.5f * Mathf.Abs(p - last);
            if (score < bestScore)
            {
                bestScore = score;
                best = p;
            }
        }
        return best;
    }

    private static int ChordToneAbove(Chord chord, int from, int count)
    {
        int p = from;
        for (int n = 0; n < count; n++)
        {
            do { p++; } while (!chord.Contains(p) && p < from + 24);
        }
        return p;
    }

    private static int ChordToneBelow(Chord chord, int from, int count)
    {
        int p = from;
        for (int n = 0; n < count; n++)
        {
            do { p--; } while (!chord.Contains(p) && p > from - 24);
        }
        return p;
    }

    /// <summary>Naechster sicherer Ton in Richtung <paramref name="dir"/>: Pentatonik ohne Reibung, oder Akkordton.</summary>
    private static int ScaleStep(SongSheet sheet, Chord chord, int from, int dir)
    {
        for (int p = from + dir; Mathf.Abs(p - from) <= 5; p += dir)
        {
            if (IsSafe(sheet, chord, p)) return p;
        }
        return dir > 0 ? ChordToneAbove(chord, from, 1) : ChordToneBelow(chord, from, 1);
    }

    private static bool IsSafe(SongSheet sheet, Chord chord, int pitch)
    {
        if (chord.Contains(pitch)) return true;
        if (!InScale(sheet, pitch)) return false;
        // ein Halbton neben einem Akkordton reibt - weglassen
        return !chord.Contains(pitch + 1) && !chord.Contains(pitch - 1);
    }

    private static bool InScale(SongSheet sheet, int pitch)
    {
        int pc = ((pitch % 12) + 12) % 12;
        for (int i = 0; i < sheet.scale.Length; i++)
        {
            if (sheet.scale[i] == pc) return true;
        }
        return false;
    }

    private static int NearestPitchClass(int pitchClass, int near)
    {
        int pc = ((pitchClass % 12) + 12) % 12;
        int baseOctave = Mathf.FloorToInt(near / 12f) * 12;
        int best = baseOctave + pc;
        for (int o = -12; o <= 12; o += 12)
        {
            int p = baseOctave + pc + o;
            if (Mathf.Abs(p - near) < Mathf.Abs(best - near)) best = p;
        }
        return best;
    }

    /// <summary>Im Tonumfang halten - zu hoch klingt der Ton gequetscht, zu tief matschig.</summary>
    private static int Fold(int pitch)
    {
        while (pitch > Highest) pitch -= 12;
        while (pitch < Lowest) pitch += 12;
        return pitch;
    }
}
