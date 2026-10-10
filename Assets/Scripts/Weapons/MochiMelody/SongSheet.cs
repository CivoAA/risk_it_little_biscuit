using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Notenblatt eines Lauf-Musikstuecks fuer die Mochi-Melodie: Tempo, wo im
/// Clip der erste Taktanfang liegt, die Akkorde Takt fuer Takt und die
/// Tonleiter, aus der Mochi Durchgangstoene nimmt. Damit spielt Mochi im Takt
/// und in der Tonart der Level-Musik mit, statt dagegen.
///
/// Gefunden wird ein Blatt ueber den Namen des Clips, der gerade als
/// Lauf-Musik laeuft (AudioController.audioSources[1]). Ein Stueck ohne Blatt
/// heisst: Mochi spielt frei in ihrem eigenen Tempo (<see cref="Solo"/>).
///
/// Die Werte stammen aus Tools/eis_musik.py, kueche_musik.py, vulkan_musik.py,
/// herz_musik.py, geist_musik.py, squiddy_musik.py (komponiert) bzw. aus einer
/// Beat-/Akkord-Analyse der fertigen Dateien (Wald, Off to Osaka) - siehe
/// Tools/musik_analyse.py. Neues Stueck: dort analysieren, hier eintragen.
/// </summary>
public class SongSheet
{
    /// <summary>Name des AudioClips (ohne Endung).</summary>
    public string clipName;

    public float bpm;

    /// <summary>Sekunden vom Clip-Anfang bis zur ersten Eins.</summary>
    public double firstDownbeat;

    /// <summary>
    /// So viele Takte gehoeren zur Schleife. Was danach im Clip noch kommt
    /// (Ausklang bis zum Loop-Punkt), ist fuer Mochi Pause.
    /// </summary>
    public int bars;

    /// <summary>Akkord je Takt, wiederholt sich, wenn kuerzer als <see cref="bars"/>.</summary>
    public Chord[] chords;

    /// <summary>
    /// Statt Akkorden pro Takt: drei sichere Toene pro Schlag (aus
    /// Tools/musik_analyse.py --maske). Fuer Stuecke, deren Harmonie zu
    /// schnell oder zu bunt wechselt, als dass ein Akkord pro Takt passt.
    /// </summary>
    public Chord[] beatChords;

    /// <summary>Tonleiter fuer Durchgangstoene (Tonklassen 0 = C).</summary>
    public int[] scale;

    /// <summary>Waehlt Mochis Phrasen - gleiches Stueck, gleiche Melodie.</summary>
    public int seed;

    public double BeatLength => 60.0 / bpm;
    public double BarLength => 4.0 * BeatLength;

    /// <summary>Was an Schlag <paramref name="beat"/> (seit der ersten Eins) klingt.</summary>
    public Chord ChordAt(double beat)
    {
        if (beatChords != null && beatChords.Length > 0)
        {
            int i = (int)System.Math.Floor(beat + 1e-6);
            return beatChords[Mathf.Clamp(i, 0, beatChords.Length - 1)];
        }
        int bar = (int)System.Math.Floor(beat / 4.0 + 1e-6);
        int n = chords.Length;
        return chords[((bar % n) + n) % n];
    }

    // ------------------------------------------------------------------
    //  Die Stuecke
    // ------------------------------------------------------------------

    // Tonleitern: Pentatonik reicht fuer Durchgangstoene - darin gibt es keine
    // Halbtonschritte, die sich mit dem Akkord reiben.
    private static readonly int[] DMinorPenta = { 2, 5, 7, 9, 0 };      // D F G A C
    private static readonly int[] BMinorPenta = { 11, 2, 4, 6, 9 };     // H D E Fis A
    private static readonly int[] CMajorPenta = { 0, 2, 4, 7, 9 };      // C D E G A
    private static readonly int[] GMajorPenta = { 7, 9, 11, 2, 4 };     // G A H D E
    private static readonly int[] CMinorPenta = { 0, 3, 5, 7, 10 };     // C Es F G B
    private static readonly int[] EMinorPenta = { 4, 7, 9, 11, 2 };     // E G A H D
    private static readonly int[] EMajorPenta = { 4, 6, 8, 11, 1 };     // E Fis Gis H Cis

    private static readonly Dictionary<string, SongSheet> sheets = new Dictionary<string, SongSheet>
    {
        // "Gletscherkrone" (Eiswelt): d-Moll, 126 BPM, 56 Takte, beginnt genau
        // auf der Eins. Akkorde 1:1 aus Tools/eis_musik.py (compose()).
        ["eis"] = new SongSheet
        {
            clipName = "eis",
            bpm = 126f,
            firstDownbeat = 0.0,
            bars = 56,
            chords = Concat(
                "Dm Bb Gm A",                                   // Intro
                "Dm Bb F C Dm Bb Gm A",                         // A
                "Dm Bb F C Dm Bb Gm A",                         // A'
                "F C Dm Bb F C Bb A",                           // B (Refrain)
                "Bbmaj7 Am7 Gm7 Asus Bbmaj7 Am7 Gm7 A",         // C (Breakdown)
                "Dm Bb F C Dm Bb Gm A",                         // A''
                "F C Dm Bb F C Bb A",                           // B'
                "Dm Bb Gm A"),                                  // Wende
            scale = DMinorPenta,
            seed = 3,
        },

        // "Mochis Kuechenparade" (Kueche): G-Dur, 120 BPM, gerade Achtel, 56
        // Takte, beginnt genau auf der Eins - fuer Mochi komponiert. Akkorde
        // 1:1 aus Tools/kueche_musik.py (SECTIONS/PROG_*).
        ["kueche"] = new SongSheet
        {
            clipName = "kueche",
            bpm = 120f,
            firstDownbeat = 0.0,
            bars = 56,
            chords = Concat(
                "G Em C D",                                     // Intro
                "G Em C D G Em Am D",                           // A
                "G Em C D G Em Am D",                           // A'
                "C D Bm Em Am D G G",                           // B (Refrain)
                "Em C G D Em C Am D",                           // C (Mochis Buehne)
                "G Em C D G Em Am D",                           // A''
                "C D Bm Em Am D G G",                           // B'
                "G Em Am D"),                                   // Wende
            scale = GMajorPenta,
            seed = 0,
        },

        // "Glutpfad" (Vulkan): c-Moll, 124 BPM, gerade Achtel, 56 Takte, beginnt
        // genau auf der Eins - fuer Mochi komponiert. Akkorde 1:1 aus
        // Tools/vulkan_musik.py (SECTIONS/PROG_*).
        ["vulkan"] = new SongSheet
        {
            clipName = "vulkan",
            bpm = 124f,
            firstDownbeat = 0.0,
            bars = 56,
            chords = Concat(
                "Cm Cm Ab G",                                   // Intro
                "Cm Ab Bb G Cm Ab Fm G",                        // A
                "Cm Ab Bb G Cm Ab Fm G",                        // A'
                "Ab Bb Eb Cm Ab Bb G G",                        // B (Refrain)
                "Fm Cm Ab G Fm Cm Db G",                        // C (Mochis Buehne)
                "Cm Ab Bb G Cm Ab Fm G",                        // A''
                "Ab Bb Eb Cm Ab Bb G G",                        // B'
                "Cm Ab Fm G"),                                  // Wende
            scale = CMinorPenta,
            seed = 4,
        },

        // "Herz der Glut" (Herzkammer des Verkohlten): c-Moll, 90 BPM, 36 Takte,
        // beginnt genau auf der Eins (= Landung in der Kammer). Ein Herzschlag
        // = zwei Viertel - VerkohlterMusik.HeartClock stellt das Herz danach.
        // Akkorde 1:1 aus Tools/herz_musik.py.
        ["herzkammer"] = new SongSheet
        {
            clipName = "herzkammer",
            bpm = 90f,
            firstDownbeat = 0.0,
            bars = 36,
            chords = Concat(
                "Cm Cm",                                        // Einschlag
                "Cm Ab Bb G Cm Ab Fm G",                        // A (Motiv)
                "Ab Bb Eb Cm Ab Bb G G",                        // B (Refrain)
                "Fm Cm Ab G Fm Cm Db G",                        // C (Mochis Buehne)
                "Cm Ab Bb G Cm Ab Fm G",                        // D (Hoehepunkt)
                "Ab G"),                                        // Wende
            scale = CMinorPenta,
            seed = 5,
        },

        // "Mitternacht im Lebkuchenwald" (Geisterwald): e-Moll, 116 BPM, gerade
        // Achtel, 56 Takte, beginnt genau auf der Eins - fuer Mochi komponiert.
        // Akkorde 1:1 aus Tools/geist_musik.py (SECTIONS/PROG_*), "B" = H-Dur.
        ["geist"] = new SongSheet
        {
            clipName = "geist",
            bpm = 116f,
            firstDownbeat = 0.0,
            bars = 56,
            chords = Concat(
                "Em Em C B",                                    // Intro
                "Em C Am B Em C D B",                           // A
                "Em C Am B Em C D B",                           // A'
                "C D G Em Am B Em Em",                          // B (Refrain)
                "Em Am Em B Em Am C B",                         // C (Mochis Buehne)
                "Em C Am B Em C D B",                           // A''
                "C D G Em Am B Em Em",                          // B'
                "Am C B B"),                                    // Wende
            scale = EMinorPenta,
            seed = 6,
        },

        // "Squiddys Gelee-Gloria" (Phase 2 des Geisterwald-Bosses): E-Dur, 132 BPM,
        // gerade Achtel, 40 Takte, beginnt genau auf der Eins. Akkorde 1:1 aus
        // Tools/squiddy_musik.py (SECTIONS/PROG_*), "B" = H-Dur.
        ["squiddy"] = new SongSheet
        {
            clipName = "squiddy",
            bpm = 132f,
            firstDownbeat = 0.0,
            bars = 40,
            chords = Concat(
                "E E C B",                                      // Intro
                "E C A B E C D B",                              // A
                "E C A B E C D B",                              // A'
                "A B G#m C#m A B E E",                          // B (Refrain)
                "E A E B E A C B",                              // C (Mochis Buehne)
                "A C B B"),                                     // Wende
            scale = EMajorPenta,
            seed = 7,
        },

        // Wald: d-Moll, 132 BPM, Dm-B-F-C in Schleife (16 Takte), Analyse.
        // Der Clip ist 0,2 s laenger als 16 Takte - der Rest ist Pause.
        ["wald"] = new SongSheet
        {
            clipName = "wald",
            bpm = 132.01f,
            firstDownbeat = 0.024,
            bars = 16,
            chords = Concat("Dm Bb F C"),
            scale = DMinorPenta,
            seed = 1,
        },

        // "Off to Osaka" (Standard-Lauf-Musik, z.B. Vulkan): 117 BPM, h-Moll mit schnellen,
        // teils chromatischen Wechseln - ein Akkord pro Takt passt da nicht.
        // Darum sichere Toene pro Schlag aus der Analyse (212 Schlaege), die
        // Pentatonik nur fuer Durchgangstoene. Der Clip ist kein sauberer
        // Loop - nach Takt 53 ist bis zum Neustart Pause.
        ["Off to Osaka"] = new SongSheet
        {
            clipName = "Off to Osaka",
            bpm = 117f,
            firstDownbeat = 0.070,
            bars = 53,
            chords = Concat("Bm"),
            beatChords = Masks(
        "164 169 b68 174 264 72b 614 b62 462 b64 972 6b1 64b 649 739 63b ",
        "b26 24a 269 b63 9b4 409 260 924 427 b63 427 47b 64b 649 739 63b ",
        "4b2 2a4 269 136 7b4 407 06a 60a 27a 297 725 042 609 816 631 631 ",
        "16a 26b 168 460 b72 b16 571 157 42b b64 924 47b 64b 149 739 63b ",
        "4b2 24a 269 136 47b 470 064 406 427 b27 175 416 641 164 360 613 ",
        "46b 047 259 936 146 047 206 360 462 7b4 274 17b 61b 964 6b3 630 ",
        "b26 2a4 269 936 b74 047 a50 a50 72a 2b9 b27 027 631 814 613 631 ",
        "269 b26 462 062 47b b42 175 571 4b6 b42 b26 4b1 64b 169 b36 369 ",
        "4b2 74a 269 163 b74 047 260 260 426 b63 962 7b1 1b3 149 063 630 ",
        "42b 2a7 269 937 4b7 407 260 260 426 b64 927 47b 63b 149 136 306 ",
        "4b2 2a7 269 136 7b4 407 306 a02 72b 29b 725 749 461 168 b61 631 ",
        "269 26b 462 460 27b 16b 974 351 462 b63 427 471 6b4 194 b63 631 ",
        "b26 2a4 269 b37 4b7 409 260 964 269 731 962 269 269 269 296 269 ",
        "296 269 b64 b64 "),
            scale = BMinorPenta,
            seed = 2,
        },
    };

    /// <summary>
    /// Mochi allein (kein Blatt fuer die laufende Musik): die alte Kadenz
    /// C-a-F-G in C-Dur. Tempo setzt dann der Cooldown.
    /// </summary>
    public static readonly SongSheet Solo = new SongSheet
    {
        clipName = "",
        bpm = 0f,
        bars = 4,
        chords = Concat("C Am F G"),
        scale = CMajorPenta,
        seed = 0,
    };

    public static SongSheet Find(AudioClip clip)
    {
        if (clip == null) return null;
        return sheets.TryGetValue(clip.name, out SongSheet sheet) ? sheet : null;
    }

    /// <summary>"b26 24a ..." - je Schlag Grundton + zwei Toene als Hex-Tonklasse.</summary>
    private static Chord[] Masks(params string[] parts)
    {
        List<Chord> list = new List<Chord>();
        foreach (string part in parts)
        {
            foreach (string token in part.Split(' '))
            {
                if (token.Length > 0) list.Add(Chord.FromHex(token));
            }
        }
        return list.ToArray();
    }

    private static Chord[] Concat(params string[] parts)
    {
        List<Chord> list = new List<Chord>();
        foreach (string part in parts)
        {
            foreach (string name in part.Split(' '))
            {
                if (name.Length > 0) list.Add(Chord.Parse(name));
            }
        }
        return list.ToArray();
    }
}

/// <summary>Ein Akkord als Grundton + Tonklassen (0 = C).</summary>
public class Chord
{
    public string name;
    public int root;
    public int[] tones;

    /// <summary>Moll-Terz statt Dur-Terz (fuer das Glitzern des Akkord-Rings).</summary>
    public bool minor;

    private static readonly Dictionary<string, int[]> Qualities = new Dictionary<string, int[]>
    {
        [""] = new[] { 0, 4, 7 },
        ["m"] = new[] { 0, 3, 7 },
        ["sus"] = new[] { 0, 5, 7 },
        ["7"] = new[] { 0, 4, 7, 10 },
        ["maj7"] = new[] { 0, 4, 7, 11 },
        ["m7"] = new[] { 0, 3, 7, 10 },
    };

    private static readonly Dictionary<char, int> Letters = new Dictionary<char, int>
    {
        ['C'] = 0, ['D'] = 2, ['E'] = 4, ['F'] = 5, ['G'] = 7, ['A'] = 9, ['B'] = 11,
    };

    /// <summary>"Dm", "Bb", "F#m", "Bbmaj7", "Asus" ... (englisch: B = H, Bb = B).</summary>
    public static Chord Parse(string name)
    {
        int root = Letters[name[0]];
        int i = 1;
        if (i < name.Length && name[i] == '#') { root++; i++; }
        else if (i < name.Length && name[i] == 'b') { root--; i++; }
        root = (root + 12) % 12;

        string quality = name.Substring(i);
        int[] steps = Qualities[quality];
        int[] tones = new int[steps.Length];
        for (int k = 0; k < steps.Length; k++) tones[k] = (root + steps[k]) % 12;

        return new Chord
        {
            name = name,
            root = root,
            tones = tones,
            minor = quality == "m" || quality == "m7",
        };
    }

    /// <summary>"b26" = Grundton H, dazu D und Fis (Tonklassen hex, 0 = C).</summary>
    public static Chord FromHex(string token)
    {
        int[] tones = new int[token.Length];
        for (int i = 0; i < token.Length; i++) tones[i] = System.Convert.ToInt32(token[i].ToString(), 16);
        Chord chord = new Chord { name = token, root = tones[0], tones = tones };
        chord.minor = chord.Contains(chord.root + 3) && !chord.Contains(chord.root + 4);
        return chord;
    }

    public bool Contains(int pitchClass)
    {
        pitchClass = ((pitchClass % 12) + 12) % 12;
        for (int i = 0; i < tones.Length; i++)
        {
            if (tones[i] == pitchClass) return true;
        }
        return false;
    }
}
