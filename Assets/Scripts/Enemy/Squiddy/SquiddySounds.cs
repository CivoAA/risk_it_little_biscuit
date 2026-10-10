using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Klaenge des Gespensts / Squiddys, im Code erzeugt (wie <see cref="EiskaiserSounds"/>):
///
///   Wooo       Theremin-Heulen, gleitet hoch und runter (Gespenst lacht)
///   Sink       Abtauchen: Luftzug nach unten, Gluckern
///   Buh        Schrei: rauer Vokal "BUH" mit tiefem Schlag drunter
///   Spit       ein Mini-Geist flitzt los ("fffuu")
///   Rumble     Zittern unter dem Laken
///   Blub       Gelee schwappt unter dem Stoff
///   Rip        Stoff wird gerissen / hochgerissen
///   Whoosh     Laken fliegt weg
///   Pling      Heiligenschein: heller Glockenton + Engelschor in Dur
///   Zap        kurzes Knistern (Funken, Nessel)
///   Charge     Hochspannung laedt: Summen steigt
///   Discharge  Entladung: Knall + Donnerrollen
///   Erupt      Tentakel bricht aus dem Boden
///   Plop       Baby-Quallen ploppen raus
///   Pop        ein Baby zerplatzt mit Blitz
///   Ascend     Squiddy faehrt auf: langer Chor
///
/// Alles ueber die Effekt-Gruppe des AudioControllers (Lautstaerke-Regler).
/// </summary>
public static class SquiddySounds
{
    private const int Rate = 44100;

    private static AudioClip wooo, sink, buh, spit, rumble, blub, rip, whoosh, pling, zap, charge,
                             discharge, erupt, plop, pop, ascend;
    private static AudioSource oneShots;

    public static void Wooo() => Play(wooo ??= Theremin("SquiddyWooo", 1.3f), 0.45f);
    public static void Sink() => Play(sink ??= Swoosh("SquiddySink", 0.7f, 900f, 120f, 0.8f), 0.6f);
    public static void Emerge() => Play(sink ??= Swoosh("SquiddySink", 0.7f, 900f, 120f, 0.8f), 0.5f);
    public static void Buh() => Play(buh ??= Scream("SquiddyBuh", 0.75f), 0.9f);
    public static void Spit() => Play(spit ??= Swoosh("SquiddySpit", 0.18f, 2600f, 900f, 0.4f), 0.16f);
    public static void Rumble() => Play(rumble ??= Rumbling("SquiddyRumble", 1.4f), 0.7f);
    public static void Blub() => Play(blub ??= Bubble("SquiddyBlub", 0.22f, 180f, 420f), 0.55f);
    public static void Rip() => Play(rip ??= Ripping("SquiddyRip", 0.55f), 0.85f);
    public static void Whoosh() => Play(whoosh ??= Swoosh("SquiddyWhoosh", 0.6f, 300f, 1800f, 0.9f), 0.7f);
    public static void Pling() => Play(pling ??= Choir("SquiddyPling", 2.4f, true), 0.75f);
    public static void Zap() => Play(zap ??= Crackle("SquiddyZap", 0.16f), 0.3f);
    public static void Charge() => Play(charge ??= Buzz("SquiddyCharge", 1.7f), 0.55f);
    public static void Discharge() => Play(discharge ??= Thunder("SquiddyDischarge", 1.3f), 1f);
    public static void Erupt() => Play(erupt ??= Bubble("SquiddyErupt", 0.3f, 90f, 260f, true), 0.35f);
    public static void Plop() => Play(plop ??= Bubble("SquiddyPlop", 0.16f, 300f, 700f), 0.5f);
    public static void Pop() => Play(pop ??= Crackle("SquiddyPop", 0.3f, true), 0.45f);
    public static void Ascend() => Play(ascend ??= Choir("SquiddyAscend", 3.6f, false), 0.8f);

    // ------------------------------------------------------------ Ausgabe

    private static void Play(AudioClip clip, float volume)
    {
        AudioSource src = OneShots();
        if (src != null && clip != null) src.PlayOneShot(clip, volume);
    }

    private static AudioSource OneShots() => oneShots != null ? oneShots : oneShots = NewSource("SquiddySounds");

    private static AudioSource NewSource(string name)
    {
        var go = new GameObject(name);
        RunScene.Place(go, "Effekte");
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        AudioMixerGroup group = null;
        if (AudioController.Instance != null && AudioController.Instance.EarthHit != null)
            group = AudioController.Instance.EarthHit.outputAudioMixerGroup;
        src.outputAudioMixerGroup = group;
        return src;
    }

    private static AudioClip Make(string name, float[] data)
    {
        // Weich ein- und ausblenden, damit nichts knackt
        int fade = Mathf.Min(220, data.Length / 4);
        for (int i = 0; i < fade; i++)
        {
            float k = i / (float)fade;
            data[i] *= k;
            data[data.Length - 1 - i] *= k;
        }
        AudioClip clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static float Noise(System.Random rng) => (float)(rng.NextDouble() * 2 - 1);

    // ------------------------------------------------------------ Synthese

    /// <summary>Theremin: Sinus mit Vibrato, gleitet hoch, kippt, gleitet runter - "wuuuhuuu".</summary>
    private static AudioClip Theremin(string name, float seconds)
    {
        int n = Mathf.RoundToInt(seconds * Rate);
        var data = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float q = t / seconds;
            float f = 330f + 260f * Mathf.Sin(q * Mathf.PI * 1.4f) - 90f * q;
            f *= 1f + 0.025f * Mathf.Sin(2f * Mathf.PI * 6.2f * t);
            phase += 2f * Mathf.PI * f / Rate;
            float env = Mathf.Min(1f, t / 0.15f) * Mathf.Clamp01((seconds - t) / 0.35f);
            data[i] = (Mathf.Sin(phase) * 0.8f + 0.15f * Mathf.Sin(phase * 2f)) * env * 0.5f;
        }
        return Make(name, data);
    }

    /// <summary>Luftzug: gefiltertes Rauschen, dessen Helligkeit von f0 nach f1 wandert.</summary>
    private static AudioClip Swoosh(string name, float seconds, float f0, float f1, float amount)
    {
        int n = Mathf.RoundToInt(seconds * Rate);
        var data = new float[n];
        var rng = new System.Random(3);
        float lp = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float q = t / seconds;
            float f = Mathf.Lerp(f0, f1, q);
            float a = Mathf.Clamp01(2f * Mathf.PI * f / Rate);
            lp += a * (Noise(rng) - lp);
            lp2 += a * (lp - lp2);
            float env = Mathf.Sin(Mathf.PI * Mathf.Pow(q, 0.7f));
            data[i] = lp2 * env * amount * 2.2f;
        }
        return Make(name, data);
    }

    /// <summary>"BUH!": rauer Saegezahn mit U-Formanten, Tonhoehe faellt; dazu ein tiefer Schlag.</summary>
    private static AudioClip Scream(string name, float seconds)
    {
        int n = Mathf.RoundToInt(seconds * Rate);
        var data = new float[n];
        var rng = new System.Random(8);
        float phase = 0f, b1 = 0f, b2 = 0f, lowPhase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float q = t / seconds;
            float f = 210f - 80f * q + 6f * Mathf.Sin(2f * Mathf.PI * 7f * t);
            phase += f / Rate;
            float saw = 2f * (phase - Mathf.Floor(phase + 0.5f));
            float src = saw + 0.35f * Noise(rng);
            // zwei einfache Tiefpaesse ~ dunkler "U"-Vokal
            b1 += 0.12f * (src - b1);
            b2 += 0.12f * (b1 - b2);
            float env = Mathf.Min(1f, t / 0.03f) * Mathf.Exp(-q * 2.2f);
            lowPhase += 2f * Mathf.PI * Mathf.Lerp(80f, 40f, q) / Rate;
            float thump = Mathf.Sin(lowPhase) * Mathf.Exp(-t * 9f);
            data[i] = Mathf.Clamp(b2 * env * 1.6f + thump * 0.8f, -1f, 1f);
        }
        return Make(name, data);
    }

    private static AudioClip Rumbling(string name, float seconds)
    {
        int n = Mathf.RoundToInt(seconds * Rate);
        var data = new float[n];
        var rng = new System.Random(11);
        float lp = 0f, phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float q = t / seconds;
            lp += 0.01f * (Noise(rng) - lp);
            phase += 2f * Mathf.PI * (48f + 8f * Mathf.Sin(t * 9f)) / Rate;
            float wobble = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 11f * t);
            float env = Mathf.Sin(Mathf.PI * q);
            data[i] = Mathf.Clamp((lp * 6f + Mathf.Sin(phase) * 0.45f) * wobble * env, -1f, 1f);
        }
        return Make(name, data);
    }

    /// <summary>Gelee-Blubb: Sinus, dessen Tonhoehe hochschnellt (Blase), optional mit Erdkrachen.</summary>
    private static AudioClip Bubble(string name, float seconds, float f0, float f1, bool dirt = false)
    {
        int n = Mathf.RoundToInt(seconds * Rate);
        var data = new float[n];
        var rng = new System.Random(21);
        float phase = 0f, hp = 0f, prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float q = t / seconds;
            phase += 2f * Mathf.PI * Mathf.Lerp(f0, f1, Mathf.Sqrt(q)) / Rate;
            float env = Mathf.Min(1f, t / 0.005f) * Mathf.Exp(-q * 4f);
            float s = Mathf.Sin(phase) * env * 0.8f;
            if (dirt)
            {
                float w = Noise(rng);
                hp = 0.7f * (hp + w - prev);
                prev = w;
                s += hp * Mathf.Exp(-t * 18f) * 0.5f;
            }
            data[i] = Mathf.Clamp(s, -1f, 1f);
        }
        return Make(name, data);
    }

    /// <summary>Stoff reisst: dichte, unregelmaessige Knackser in hellem Rauschen.</summary>
    private static AudioClip Ripping(string name, float seconds)
    {
        int n = Mathf.RoundToInt(seconds * Rate);
        var data = new float[n];
        var rng = new System.Random(5);
        float hp = 0f, prev = 0f, burst = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float q = t / seconds;
            if (rng.NextDouble() < 0.004) burst = 1f;
            burst *= 0.996f;
            float w = Noise(rng);
            hp = 0.8f * (hp + w - prev);
            prev = w;
            float env = Mathf.Min(1f, t / 0.02f) * (1f - q * 0.7f);
            data[i] = Mathf.Clamp(hp * (0.25f + burst) * env * 1.1f, -1f, 1f);
        }
        return Make(name, data);
    }

    /// <summary>Engelschor: weiche Saegezaehne in Dur, leicht verstimmt, mit Glockenschlag vorn.</summary>
    private static AudioClip Choir(string name, float seconds, bool bell)
    {
        int n = Mathf.RoundToInt(seconds * Rate);
        var data = new float[n];
        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };     // C-Dur
        float[] detune = { -0.004f, 0.003f, 0.0045f };
        var phases = new float[notes.Length * detune.Length];
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float q = t / seconds;
            float s = 0f;
            int k = 0;
            for (int a = 0; a < notes.Length; a++)
            {
                for (int d = 0; d < detune.Length; d++, k++)
                {
                    float f = notes[a] * (1f + detune[d]) * (1f + 0.006f * Mathf.Sin(2f * Mathf.PI * 5.1f * t + k));
                    phases[k] += f / Rate;
                    phases[k] -= Mathf.Floor(phases[k]);
                    s += (2f * phases[k] - 1f) * 0.08f;
                }
            }
            lp += 0.08f * (s - lp);                                   // Saegen weich machen = "Aaah"
            float env = Mathf.Min(1f, t / 0.25f) * Mathf.Clamp01((seconds - t) / (seconds * 0.45f));
            float v = lp * env * 1.4f;
            if (bell)
            {
                float be = Mathf.Exp(-t * 3.5f);
                v += (Mathf.Sin(2f * Mathf.PI * 2093f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * 2093f * 2.76f * t) * Mathf.Exp(-t * 8f)) * be * 0.35f;
            }
            data[i] = Mathf.Clamp(v, -1f, 1f);
            _ = q;
        }
        return Make(name, data);
    }

    private static AudioClip Crackle(string name, float seconds, bool tone = false)
    {
        int n = Mathf.RoundToInt(seconds * Rate);
        var data = new float[n];
        var rng = new System.Random(17);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float q = t / seconds;
            float c = rng.NextDouble() < 0.09 * (1f - q) ? Noise(rng) : 0f;
            float s = c * 0.9f;
            if (tone) s += Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(900f, 300f, q) * t) * Mathf.Exp(-t * 14f) * 0.5f;
            data[i] = Mathf.Clamp(s, -1f, 1f);
        }
        return Make(name, data);
    }

    /// <summary>Summen, das in Tonhoehe und Dichte steigt (Saegezahn + Knistern).</summary>
    private static AudioClip Buzz(string name, float seconds)
    {
        int n = Mathf.RoundToInt(seconds * Rate);
        var data = new float[n];
        var rng = new System.Random(23);
        float phase = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float q = t / seconds;
            phase += Mathf.Lerp(60f, 240f, q * q) / Rate;
            float saw = 2f * (phase - Mathf.Floor(phase + 0.5f));
            lp += 0.2f * (saw - lp);
            float c = rng.NextDouble() < 0.02 + 0.08 * q ? Noise(rng) * 0.6f : 0f;
            float env = Mathf.Min(1f, t / 0.1f) * (0.4f + 0.6f * q);
            data[i] = Mathf.Clamp((lp * 0.6f + c) * env, -1f, 1f);
        }
        return Make(name, data);
    }

    private static AudioClip Thunder(string name, float seconds)
    {
        int n = Mathf.RoundToInt(seconds * Rate);
        var data = new float[n];
        var rng = new System.Random(29);
        float lp = 0f, hp = 0f, prev = 0f, phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float q = t / seconds;
            float w = Noise(rng);
            hp = 0.6f * (hp + w - prev);
            prev = w;
            lp += 0.015f * (w - lp);
            phase += 2f * Mathf.PI * Mathf.Lerp(70f, 30f, q) / Rate;
            float crack = hp * Mathf.Exp(-t * 22f);
            float roll = lp * 7f * Mathf.Exp(-q * 2.5f) * (0.7f + 0.3f * Mathf.Sin(t * 23f));
            float boom = Mathf.Sin(phase) * Mathf.Exp(-t * 5f);
            data[i] = Mathf.Clamp(crack * 1.1f + roll + boom * 0.8f, -1f, 1f);
        }
        return Make(name, data);
    }
}
