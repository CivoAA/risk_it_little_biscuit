using System;
using UnityEngine;

/// <summary>
/// Weiss, wo die Lauf-Musik gerade steht - auf den Sample genau, damit Mochi
/// ihre Toene per <see cref="AudioSource.PlayScheduled"/> exakt auf die
/// Schlaege des Stuecks legen kann.
///
/// AudioSource.timeSamples springt nur in Puffer-Schritten. Darum merkt sich
/// die Uhr, zu welcher dspTime das Stueck "angefangen hat", und rechnet von
/// dort weiter; die Messung zieht diesen Anker nur sanft nach. Springt die
/// Musik (neu gestartet, anderes Stueck, Fokus weg), rastet die Uhr neu ein.
///
/// Zeit hier = "Liedzeit": laeuft ueber Schleifen hinweg weiter, Schleife k
/// beginnt bei k * Cliplaenge.
/// </summary>
public static class MusicClock
{
    /// <summary>Ab so viel Abweichung gilt die Musik als gesprungen.</summary>
    private const double JumpTolerance = 0.08;

    /// <summary>So viel einer Messabweichung wird pro Abfrage uebernommen.</summary>
    private const double Smoothing = 0.1;

    private static AudioClip clip;
    private static SongSheet sheet;
    private static double anchorDsp;
    private static double clipLength;
    private static bool locked;
    private static int lastPollFrame = -1;
    private static bool lastPollResult;

    /// <summary>Das Blatt des laufenden Stuecks (nur gueltig, wenn <see cref="Poll"/> true lieferte).</summary>
    public static SongSheet Sheet => sheet;

    /// <summary>Zaehlt hoch, wenn die Uhr neu einrastet - Mochi plant dann neu.</summary>
    public static int LockId { get; private set; }

    /// <summary>Liedzeit jetzt.</summary>
    public static double Now => AudioSettings.dspTime - anchorDsp;

    /// <summary>dspTime, zu der die Liedzeit <paramref name="songTime"/> erklingt.</summary>
    public static double ToDsp(double songTime) => songTime + anchorDsp;

    /// <summary>
    /// Liest die Lauf-Musik ab. True = es laeuft ein Stueck mit Notenblatt und
    /// die Uhr ist eingerastet. Einmal pro Frame rechnen reicht.
    /// </summary>
    public static bool Poll()
    {
        if (lastPollFrame == Time.frameCount) return lastPollResult;
        lastPollFrame = Time.frameCount;
        lastPollResult = PollNow();
        return lastPollResult;
    }

    private static bool PollNow()
    {
        AudioSource src = RunMusic();
        if (src == null || !src.isPlaying || src.clip == null || src.clip.frequency <= 0)
        {
            locked = false;
            return false;
        }

        if (src.clip != clip)
        {
            clip = src.clip;
            sheet = SongSheet.Find(clip);
            clipLength = (double)clip.samples / clip.frequency;
            locked = false;
            if (sheet != null) Debug.Log("[MochiMelody] spielt mit: " + clip.name + " (" + sheet.bpm + " BPM)");
        }
        if (sheet == null) return false;

        double measured = (double)src.timeSamples / clip.frequency;
        double dsp = AudioSettings.dspTime;
        if (!locked)
        {
            Relock(dsp, measured);
            return true;
        }

        double predicted = Mod(dsp - anchorDsp, clipLength);
        double diff = measured - predicted;
        if (diff > clipLength * 0.5) diff -= clipLength;
        else if (diff < -clipLength * 0.5) diff += clipLength;

        if (Math.Abs(diff) > JumpTolerance) Relock(dsp, measured);
        else anchorDsp -= diff * Smoothing;
        return true;
    }

    private static void Relock(double dsp, double measured)
    {
        anchorDsp = dsp - measured;
        locked = true;
        LockId++;
    }

    private static AudioSource RunMusic()
    {
        AudioController audio = AudioController.Instance;
        if (audio == null || audio.audioSources == null || audio.audioSources.Length < 2) return null;
        return audio.audioSources[1];
    }

    /// <summary>Ein Rasterpunkt im Stueck.</summary>
    public struct Step
    {
        /// <summary>Liedzeit des Punkts.</summary>
        public double time;

        /// <summary>Schlaege seit der ersten Eins dieser Schleife (0 = Eins von Takt 1).</summary>
        public double beat;
    }

    /// <summary>
    /// Der erste Rasterpunkt (alle <paramref name="stepBeats"/> Schlaege) nach
    /// <paramref name="after"/>. Vor der ersten Eins und nach dem letzten Takt
    /// einer Schleife liegt keiner.
    /// </summary>
    public static Step NextStep(double after, double stepBeats)
    {
        double stepLength = stepBeats * sheet.BeatLength;
        double loopEnd = Math.Min(clipLength, sheet.firstDownbeat + sheet.bars * sheet.BarLength) - 1e-4;

        long loop = (long)Math.Floor(after / clipLength);
        double local = after - loop * clipLength;

        for (int guard = 0; guard < 3; guard++)
        {
            long j = (long)Math.Floor((local - sheet.firstDownbeat) / stepLength + 1e-9) + 1;
            if (j < 0) j = 0;
            double t = sheet.firstDownbeat + j * stepLength;
            if (t < loopEnd)
            {
                return new Step { time = loop * clipLength + t, beat = j * stepBeats };
            }
            // Rest dieser Schleife ist Pause - weiter mit der naechsten
            loop++;
            local = -1.0;
        }
        return new Step { time = after + stepLength, beat = 0 };
    }

    private static double Mod(double a, double m)
    {
        double r = a % m;
        return r < 0 ? r + m : r;
    }
}
