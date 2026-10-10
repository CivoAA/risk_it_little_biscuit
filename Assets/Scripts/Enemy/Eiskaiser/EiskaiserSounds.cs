using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Klaenge des Eiskaisers, im Code erzeugt (wie <see cref="VerkohlterSounds"/>):
///
///   Charge   glaeserne Glocken, die aufsteigen, waehrend die Krone laedt
///   Stomp    tiefer Stampfer mit klirrendem Eis obendrauf
///   Kick     dumpfer Tritt + Knacken (Kaiser-Kick)
///   Gulp     die Lawinenkugel schluckt einen Gegner
///   Freeze   Eis schiesst zu: Knistern, das in einen hohen Ton zufriert
///   Scratch  kurzes Kratzen beim Freistrampeln
///   Shatter  Eisblock zerspringt
///
/// Alles ueber die Effekt-Gruppe des AudioControllers (Lautstaerke-Regler).
/// </summary>
public static class EiskaiserSounds
{
    private const int Rate = 44100;

    private static AudioClip charge, stomp, kick, gulp, freeze, scratch, shatter;
    private static AudioSource oneShots;

    public static void Charge() => Play(charge ??= Bells("EiskaiserLaden", 1.1f, rising: true), 0.55f);
    public static void Stomp() => Play(stomp ??= Impact("EiskaiserStampfer", 0.9f, 62f, 30f, 0.8f), 1f);
    public static void Kick() => Play(kick ??= Impact("EiskaiserKick", 0.45f, 95f, 45f, 0.6f), 0.8f);
    public static void Gulp() => Play(gulp ??= Impact("EiskaiserSchlucken", 0.16f, 190f, 80f, 0.5f), 0.3f);
    public static void Freeze() => Play(freeze ??= Freezing("EiskaiserZufrieren", 0.55f), 0.8f);
    public static void Scratch() => Play(scratch ??= Crackle("EiskaiserKratzen", 0.07f, 0.9f), 0.35f);
    public static void Shatter() => Play(shatter ??= Crackle("EiskaiserBersten", 0.5f, 0.25f, bells: true), 0.9f);

    // ------------------------------------------------------------ Ausgabe

    private static void Play(AudioClip clip, float volume)
    {
        AudioSource src = OneShots();
        if (src != null && clip != null) src.PlayOneShot(clip, volume);
    }

    private static AudioSource OneShots() => oneShots != null ? oneShots : oneShots = NewSource("EiskaiserSounds");

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
        AudioClip clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // ------------------------------------------------------------ Synthese

    /// <summary>Glaeserne Glocken: unharmonische Teiltoene, kurz angeschlagen, nacheinander.</summary>
    private static AudioClip Bells(string name, float seconds, bool rising)
    {
        int n = Mathf.RoundToInt(seconds * Rate);
        var data = new float[n];
        float[] notes = { 1046.5f, 1318.5f, 1568f, 2093f, 2637f };
        for (int k = 0; k < notes.Length; k++)
        {
            int start = Mathf.RoundToInt(k * seconds / (notes.Length + 0.5f) * Rate);
            float f = rising ? notes[k] : notes[notes.Length - 1 - k];
            for (int i = start; i < n; i++)
            {
                float t = (i - start) / (float)Rate;
                float env = Mathf.Min(1f, t / 0.002f) * Mathf.Exp(-t * 5f);
                float s = Mathf.Sin(2f * Mathf.PI * f * t)
                        + 0.5f * Mathf.Sin(2f * Mathf.PI * f * 2.76f * t) * Mathf.Exp(-t * 9f)
                        + 0.25f * Mathf.Sin(2f * Mathf.PI * f * 5.4f * t) * Mathf.Exp(-t * 16f);
                data[i] += s * env * 0.22f;
            }
        }
        for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);
        return Make(name, data);
    }

    /// <summary>Tiefer Schlag (Sinus faellt) plus klirrendes Eis obendrauf.</summary>
    private static AudioClip Impact(string name, float seconds, float f0, float f1, float iceAmount)
    {
        int n = Mathf.RoundToInt(seconds * Rate);
        var data = new float[n];
        var rng = new System.Random(5);
        float phase = 0f, hp = 0f, prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float q = t / seconds;
            phase += 2f * Mathf.PI * Mathf.Lerp(f0, f1, Mathf.Sqrt(q)) / Rate;
            float body = Mathf.Sin(phase) * Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-q * 6f);
            float white = (float)(rng.NextDouble() * 2 - 1);
            hp = 0.9f * (hp + white - prev);              // Hochpass: helles Klirren
            prev = white;
            float ice = hp * Mathf.Exp(-t * 14f) * iceAmount * 0.6f;
            float tink = rng.NextDouble() < 0.002 * (1f - q) ? 0.5f : 0f;
            data[i] = Mathf.Clamp(body * 0.9f + ice + tink * Mathf.Sin(t * 2f * Mathf.PI * 3200f), -1f, 1f);
        }
        return Make(name, data);
    }

    /// <summary>Knistern, das sich verdichtet und in einem hohen, klaren Ton erstarrt.</summary>
    private static AudioClip Freezing(string name, float seconds)
    {
        int n = Mathf.RoundToInt(seconds * Rate);
        var data = new float[n];
        var rng = new System.Random(9);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float q = t / seconds;
            float crackle = rng.NextDouble() < 0.02 * (1f - q) ? (float)(rng.NextDouble() * 2 - 1) : 0f;
            float tone = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(1800f, 2600f, q) * t) * q * Mathf.Exp(-(q - 0.6f) * (q - 0.6f) * 8f);
            float env = q > 0.85f ? (1f - q) / 0.15f : 1f;
            data[i] = Mathf.Clamp((crackle * 0.7f + tone * 0.35f) * env, -1f, 1f);
        }
        return Make(name, data);
    }

    /// <summary>Kurzes Bersten/Kratzen; mit bells klingen Splitter nach.</summary>
    private static AudioClip Crackle(string name, float seconds, float decay, bool bells = false)
    {
        int n = Mathf.RoundToInt(seconds * Rate);
        var data = new float[n];
        var rng = new System.Random(17);
        float hp = 0f, prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float white = (float)(rng.NextDouble() * 2 - 1);
            hp = 0.85f * (hp + white - prev);
            prev = white;
            float s = hp * Mathf.Exp(-t / (seconds * decay)) * 0.9f;
            if (bells)
            {
                s += 0.18f * Mathf.Sin(2f * Mathf.PI * 2960f * t) * Mathf.Exp(-t * 9f)
                   + 0.14f * Mathf.Sin(2f * Mathf.PI * 4180f * t) * Mathf.Exp(-t * 12f);
            }
            data[i] = Mathf.Clamp(s * Mathf.Min(1f, t / 0.002f), -1f, 1f);
        }
        return Make(name, data);
    }
}
